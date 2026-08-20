"""Per-machine deploy configuration. Replaces configure_repacker.ps1.

One config per host at Build/config/<hostname>.toml, gitignored, with
Build/config/example.toml as the tracked template. Read with tomllib (stdlib
since 3.11), so there is still no third-party dependency.

`set` deliberately does a targeted line edit rather than reserialising the
parsed table. tomllib is a reader only, and any emitter we wrote would silently
drop the comments people put in these files explaining which install a path
points at. Editing in place keeps them.
"""

import os
import socket
import tomllib
from concurrent.futures import ThreadPoolExecutor

from . import context, proc, toolchain

XDK_DEBUG_PORT = 730

# section -> keys. "" is the top level.
SCHEMA = {
    "": ("platform", "og_bundle", "extracted_dir", "new_bundle",
         "spb_source", "shader_source", "launch_args"),
    "bpr": ("game_bundle", "game_exe"),
    "x360": ("ip", "game_path", "xex"),
}

REQUIRED = {
    "": ("platform", "og_bundle", "extracted_dir", "new_bundle"),
    "bpr": ("game_bundle", "game_exe"),
    "x360": ("ip", "game_path", "xex"),
}

# The bundle's own .meta.yaml records which platform it was exported for.
# Repacking a wrong-endian bundle produces a file the game silently rejects,
# so bundle.py checks this before writing anything.
META_PLATFORM = {"bpr": 1, "x360": 2}


def path_for(name=None):
    return os.path.join(context.CONFIG_DIR, (name or context.hostname()) + ".toml")


def example_path():
    return os.path.join(context.CONFIG_DIR, "example.toml")


def available():
    if not os.path.isdir(context.CONFIG_DIR):
        return []
    return sorted(
        os.path.splitext(n)[0]
        for n in os.listdir(context.CONFIG_DIR)
        if n.endswith(".toml") and n != "example.toml"
    )


class Config(object):
    def __init__(self, data, path):
        self.data = data
        self.path = path

    @property
    def platform(self):
        return str(self.data.get("platform", "")).lower()

    def get(self, key, section="", default=None):
        table = self.data if not section else self.data.get(section, {})
        value = table.get(key, default)
        return value

    def resolved(self, key, section="", default=None):
        """A path value, made absolute against the repo root."""
        value = self.get(key, section, default)
        if not value:
            return default
        value = os.path.expandvars(os.path.expanduser(str(value)))
        if not os.path.isabs(value):
            value = os.path.join(context.REPO_ROOT, value)
        return os.path.normpath(value)

    @property
    def spb_source(self):
        """Where the packed resources come from. Defaults to the build output."""
        explicit = self.resolved("spb_source")
        if explicit:
            return explicit
        from . import targets

        platform = targets.BY_NAME.get(self.platform)
        return platform.spb_dir if platform else None

    @property
    def shader_source(self):
        return self.resolved("shader_source") or None

    @property
    def launch_args(self):
        raw = self.get("launch_args", default="")
        return [a for a in str(raw).split() if a]

    def problems(self):
        """Everything wrong with this config, as human-readable lines."""
        out = []
        if self.platform not in ("bpr", "x360"):
            out.append('platform must be "bpr" or "x360" (found %r)'
                       % self.data.get("platform"))
            return out

        for key in REQUIRED[""]:
            if not self.get(key):
                out.append("missing %s" % key)
        for key in REQUIRED[self.platform]:
            if not self.get(key, self.platform):
                out.append("missing [%s].%s" % (self.platform, key))

        og = self.resolved("og_bundle")
        if og and not os.path.isfile(og):
            out.append("og_bundle does not exist: %s" % context.rel(og))

        spb = self.spb_source
        if spb and not os.path.isdir(spb):
            out.append("spb_source does not exist yet (build first): %s" % context.rel(spb))

        if self.platform == "bpr":
            exe = self.resolved("game_exe", "bpr")
            if exe and not os.path.isfile(exe):
                out.append("game_exe does not exist: %s" % exe)
        return out

    def require_valid(self):
        problems = self.problems()
        if problems:
            raise proc.StepError(
                "config %s is not usable:\n  %s" % (context.rel(self.path),
                                                    "\n  ".join(problems)),
                fix="edit it, or run: nushaders.py config set <key> <value>",
            )
        return self


def load(name=None):
    path = path_for(name)
    if not os.path.isfile(path):
        raise proc.StepError(
            "no deploy config at %s" % context.rel(path),
            code=context.EXIT_SKIP,
            fix="run: nushaders.py config new   (copies Build/config/example.toml)",
        )
    with open(path, "rb") as handle:
        try:
            data = tomllib.load(handle)
        except tomllib.TOMLDecodeError as err:
            raise proc.StepError("%s is not valid TOML: %s" % (context.rel(path), err),
                                 fix="fix the syntax, or delete it and run `config new`")
    return Config(data, path)


# --- targeted line editing ---------------------------------------------------


def _format_value(value):
    text = str(value)
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, int) and not isinstance(value, bool):
        return text
    # A Windows path is full of backslashes, which TOML basic strings treat as
    # escapes. Literal strings ('...') take them verbatim.
    if "\\" in text and "'" not in text:
        return "'%s'" % text
    return '"%s"' % text.replace("\\", "\\\\").replace('"', '\\"')


def _section_of(line, current):
    stripped = line.strip()
    if stripped.startswith("[") and stripped.endswith("]"):
        return stripped[1:-1].strip()
    return current


def set_value(path, section, key, value):
    """Replace (or insert) one key, leaving every other byte of the file alone."""
    with open(path, "r", encoding="utf-8") as handle:
        lines = handle.read().splitlines()

    rendered = "%s = %s" % (key, _format_value(value))
    current = ""
    last_line_in_section = None

    for i, line in enumerate(lines):
        new_section = _section_of(line, current)
        if new_section != current:
            current = new_section
        if current != section:
            continue
        last_line_in_section = i
        stripped = line.lstrip()
        # Match a live assignment, and also a commented-out one so that
        # uncommenting-by-setting works the way people expect.
        for prefix in ("", "# ", "#"):
            if stripped.startswith(prefix + key) and "=" in stripped:
                head = stripped[len(prefix):].split("=", 1)[0].strip()
                if head == key:
                    lines[i] = rendered
                    _write_lines(path, lines)
                    return "updated"

    if section and last_line_in_section is None:
        lines += ["", "[%s]" % section, rendered]
    elif last_line_in_section is None:
        lines.append(rendered)
    else:
        lines.insert(last_line_in_section + 1, rendered)
    _write_lines(path, lines)
    return "added"


def _write_lines(path, lines):
    if proc.DRY_RUN:
        proc.info("DRY-RUN: would rewrite %s" % context.rel(path))
        return
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(lines).rstrip("\n") + "\n")


def _locate(key):
    """Which section a bare key belongs to. Ambiguity is an error, not a guess."""
    if "." in key:
        section, _, bare = key.partition(".")
        return section, bare
    owners = [section for section, keys in SCHEMA.items() if key in keys]
    if not owners:
        known = sorted(k for keys in SCHEMA.values() for k in keys)
        raise proc.StepError("unknown key %r" % key,
                             fix="known keys: %s" % ", ".join(known))
    if len(owners) > 1:
        raise proc.StepError("%r exists in several sections" % key,
                             fix="qualify it, e.g. %s.%s" % (owners[0], key))
    return owners[0], key


# --- devkit discovery --------------------------------------------------------


def _port_open(ip, port=XDK_DEBUG_PORT, timeout=1.0):
    try:
        with socket.create_connection((ip, port), timeout=timeout):
            return True
    except OSError:
        return False


def local_subnets():
    """Best-effort /24 prefixes for this host's own addresses."""
    found = []
    try:
        for info in socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET):
            ip = info[4][0]
            if ip.startswith("127."):
                continue
            prefix = ip.rsplit(".", 1)[0]
            if prefix not in found:
                found.append(prefix)
    except OSError:
        pass
    return found


def discover(subnet=None, jobs=128):
    subnets = [subnet] if subnet else local_subnets()
    if not subnets:
        raise proc.StepError("could not determine a local subnet",
                             fix="pass one explicitly: config discover-ip --subnet 192.168.1")

    hits = []
    for prefix in subnets:
        proc.info("scanning %s.1-254 for the XDK debug port (%d)" % (prefix, XDK_DEBUG_PORT))
        candidates = ["%s.%d" % (prefix, i) for i in range(1, 255)]
        with ThreadPoolExecutor(max_workers=jobs) as pool:
            for ip, open_ in zip(candidates, pool.map(_port_open, candidates)):
                if open_:
                    hits.append(ip)
    return hits


# --- verbs -------------------------------------------------------------------


def verb_list():
    proc.step("config list")
    names = available()
    if not names:
        proc.info("no configs yet")
        proc.fix("run: nushaders.py config new")
        return context.EXIT_OK
    mine = context.hostname()
    for name in names:
        try:
            cfg = load(name)
            state = "ok" if not cfg.problems() else "%d problem(s)" % len(cfg.problems())
            platform = cfg.platform or "?"
        except proc.StepError:
            state, platform = "unreadable", "?"
        proc.info("%-24s %-5s %s%s" % (name, platform, state,
                                       "   <- this host" if name == mine else ""))
    return context.EXIT_OK


def verb_show(name=None):
    cfg = load(name)
    proc.step("config show %s" % context.rel(cfg.path))
    for section in ("", cfg.platform):
        for key in SCHEMA.get(section, ()):
            value = cfg.get(key, section)
            if value in (None, ""):
                continue
            label = key if not section else "%s.%s" % (section, key)
            proc.info("%-22s %s" % (label, value))
    proc.info("%-22s %s" % ("(spb_source ->)", context.rel(cfg.spb_source or "")))
    problems = cfg.problems()
    for line in problems:
        proc.warn(line)
    return context.EXIT_SKIP if problems else context.EXIT_OK


def verb_new(name=None, force=False):
    target = path_for(name)
    proc.step("config new %s" % context.rel(target))
    if os.path.exists(target) and not force:
        raise proc.StepError("%s already exists" % context.rel(target),
                             fix="pass --force to overwrite, or edit it directly")
    source = example_path()
    if not os.path.isfile(source):
        raise proc.StepError("template missing: %s" % context.rel(source),
                             fix="restore Build/config/example.toml from git")
    proc.ensure_dir(context.CONFIG_DIR)
    proc.copy_file(source, target)
    proc.info("copied the template; edit it to point at your install")
    return context.EXIT_OK


def verb_set(key, value, name=None):
    path = path_for(name)
    if not os.path.isfile(path):
        raise proc.StepError("no config at %s" % context.rel(path),
                             fix="run: nushaders.py config new")
    section, bare = _locate(key)
    proc.step("config set %s" % key)
    what = set_value(path, section, bare, value)
    proc.info("%s %s = %s" % (what, key, value))
    return context.EXIT_OK


def verb_discover(subnet=None, name=None, write=False):
    proc.step("config discover-ip")
    hits = discover(subnet)
    if not hits:
        proc.info("no devkit found")
        proc.fix("check the devkit is powered on and on this subnet, or pass --subnet")
        return context.EXIT_SKIP
    for ip in hits:
        proc.info("found %s" % ip)
    if write:
        if len(hits) > 1:
            raise proc.StepError("found %d devkits; not guessing" % len(hits),
                                 fix="set it explicitly: config set x360.ip <address>")
        return verb_set("x360.ip", hits[0], name)
    proc.fix("record it with: nushaders.py config set x360.ip %s" % hits[0])
    return context.EXIT_OK


def verb_validate(name=None):
    cfg = load(name)
    proc.step("config validate %s" % context.rel(cfg.path))
    problems = cfg.problems()
    if not problems:
        proc.info("config is usable")
        return context.EXIT_OK
    for line in problems:
        proc.error(line)
    proc.fix("nushaders.py config set <key> <value>")
    return context.EXIT_FAIL


# Legacy .psd1 -> TOML. Key names are the ones repack_and_send.ps1 required.
_PSD1_MAP = {
    "OGShadersBNDLPath": ("", "og_bundle"),
    "ShadersExtractedPath": ("", "extracted_dir"),
    "NewShadersBNDLPath": ("", "new_bundle"),
    "ShaderProgramBufferSourcePath": ("", "spb_source"),
    "ShaderSourcePath": ("", "shader_source"),
    "LaunchArgs": ("", "launch_args"),
    "XDKIP": ("x360", "ip"),
    "XDKGamePath": ("x360", "game_path"),
    "XEXName": ("x360", "xex"),
    "GameBundlePath": ("bpr", "game_bundle"),
    "GameExePath": ("bpr", "game_exe"),
}


def verb_migrate(psd1, name=None, force=False):
    """Read a repack_and_send.<host>.psd1 through PowerShell and write a TOML."""
    import json

    if not os.path.isfile(psd1):
        raise proc.StepError("no such file: %s" % psd1)

    shell = toolchain.require(toolchain.powershell())
    proc.step("config migrate %s" % os.path.basename(psd1))
    done = proc.run(
        [shell, "-NoProfile", "-Command",
         "Import-PowerShellDataFile -LiteralPath '%s' | ConvertTo-Json -Compress" % psd1],
        read_only=True,
    )
    try:
        legacy = json.loads(done.stdout.strip())
    except ValueError:
        raise proc.StepError("could not parse the .psd1 through PowerShell",
                             fix="check it with: Import-PowerShellDataFile %s" % psd1)

    verb_new(name, force=force)
    target = path_for(name)

    platform = str(legacy.get("Platform") or "X360").lower()
    set_value(target, "", "platform", platform)
    migrated = 1
    for old, (section, key) in _PSD1_MAP.items():
        value = legacy.get(old)
        if value in (None, ""):
            continue
        # Only carry over the section that matches the chosen platform.
        if section in ("bpr", "x360") and section != platform:
            continue
        set_value(target, section, key, value)
        migrated += 1

    proc.info("migrated %d values into %s" % (migrated, context.rel(target)))
    cfg = load(name)
    for line in cfg.problems():
        proc.warn(line)
    return context.EXIT_OK

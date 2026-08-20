"""Preflight: what is present, what is missing, and what to do about each miss.

Reports per role, so a contributor who only builds BPR shaders is never told to
install an Xbox 360 devkit SDK. A miss is EXIT_SKIP (2), not a failure — the
machine is simply not set up for that role.
"""

import os

from . import context, toolchain
from . import proc

# Filesystem inputs the build needs, and which roles need them.
PATHS = (
    ("shader source", context.SHADERS_DIR, ("x360", "bpr", "pc"),
     "expected Source/Bundle/gamedb/burnout5/Shaders — is the repo checked out fully?"),
    ("shader includes", context.INCLUDE_DIR, ("x360", "bpr", "pc"),
     "expected Source/Bundle/gamedb/burnout5/Include"),
    ("ResourceDB.json", context.RESOURCE_DB, ("x360",),
     "Reference/ResourceDB.json is required to map unnamed X360 resource IDs"),
)


def _check_role(role):
    """Returns (ok_count, misses) and prints a line per item."""
    misses = []
    ok = 0

    for name, resolver, roles in toolchain.REGISTRY:
        if role not in roles:
            continue
        tool = resolver()
        if tool:
            proc.info("ok   %-21s %s" % (name, tool))
            ok += 1
            if name == "xdk fxc" and not toolchain.xdk_is_6534():
                proc.warn("this is not XDK 6534; compiled Xenos microcode will not be "
                          "byte-comparable against the Breaker reference corpus")
                proc.fix("set XDK6534_BIN if you have the 6534 XDK installed elsewhere")
        else:
            proc.info("MISS %-21s %s" % (name, tool.what))
            proc.fix(tool.fix)
            misses.append(name)

    for name, path, roles, fixline in PATHS:
        if role not in roles:
            continue
        if os.path.exists(path):
            proc.info("ok   %-21s %s" % (name, context.rel(path)))
            ok += 1
        else:
            proc.info("MISS %-21s %s" % (name, context.rel(path)))
            proc.fix(fixline)
            misses.append(name)

    return ok, misses


def _check_corpus(role):
    """The reference bundle this role needs, via the same check `reference status` uses.

    A fresh clone has the BPR corpus (it is committed) but no X360 one, so this
    is the first thing an X360 contributor will hit. Point them at the verb that
    fixes it rather than at a directory path.
    """
    from . import reference

    wanted = {"bpr": ("bpr", None), "x360": ("x360", "Breaker")}.get(role)
    if not wanted:
        return []
    platform, version = wanted
    for corpus in reference.corpora():
        if corpus.platform != platform or corpus.version != version:
            continue
        present, detail = reference._state(corpus)
        label = corpus.label + " corpus"
        if present:
            proc.info("ok   %-21s %s" % (label, detail))
            return []
        proc.info("MISS %-21s %s" % (label, detail))
        proc.fix("unpack the stock bundle: nushaders.py reference import <bundle>%s"
                 % (" --version Breaker" if platform == "x360" else ""))
        return [label]
    return []


def _check_manifest(platform):
    """Manifests are committed, so a miss here is a repo problem, not a machine one."""
    path = os.path.join(context.MANIFEST_DIR, platform + ".json")
    label = "manifest " + platform
    if os.path.exists(path):
        proc.info("ok   %-21s %s" % (label, context.rel(path)))
        return []
    proc.info("MISS %-21s %s" % (label, context.rel(path)))
    proc.fix("run: nushaders.py manifest regen --platform %s" % platform)
    return [label]


def _check_config():
    path = context.default_config_path()
    if os.path.exists(path):
        proc.info("ok   %-21s %s" % ("deploy config", context.rel(path)))
        return []
    proc.info("MISS %-21s %s" % ("deploy config", context.rel(path)))
    proc.fix("copy Build/config/example.toml to %s and fill it in" % context.rel(path))
    return ["deploy config"]


def run(role="all"):
    roles = toolchain.ROLES if role == "all" else (role,)
    all_misses = []

    for r in roles:
        proc.step("doctor %s" % r)
        _ok, misses = _check_role(r)
        all_misses += misses
        if r in ("x360", "bpr"):
            all_misses += _check_corpus(r)
            all_misses += _check_manifest(r)
        if r == "deploy":
            all_misses += _check_config()

    # Dedup: a tool needed by several roles is reported per role but is one miss.
    unique = sorted(set(all_misses))
    if unique:
        proc.info("")
        proc.info("%d missing: %s" % (len(unique), ", ".join(unique)))
        return context.EXIT_SKIP

    proc.info("")
    proc.info("all checks passed")
    return context.EXIT_OK

"""The shader <-> resource-ID mapping, generated once and committed.

Previously this knowledge lived in a line-regex over .debug.xml re-run by three
different scripts, an 8.4 MB ResourceDB.json re-parsed on every build, and a few
IDs hardcoded in deploy_test_bpr_0x32.ps1. Generating it once and committing the
result means the build reads one small file, and — more importantly — that a
change to the ID mapping shows up in code review instead of shifting silently
under a build.

    nushaders.py manifest regen --platform bpr
    nushaders.py manifest check          # re-derive and diff; non-zero on drift
    nushaders.py manifest show --platform bpr

Shared techniques
-----------------
A technique whose annotation carries `string sharedName="X"` maps to ONE engine
resource no matter how many .fx files declare it. Where the copies disagree, we
record every distinct body under `conflicts` and mark one `canonical`. regen
never overwrites a `canonical` a human has edited — that decision is the whole
point of committing this file.
"""

import json
import os
import re
import xml.etree.ElementTree as ET
from collections import defaultdict

from . import context, fx, proc

SCHEMA = 1

# gamedb://burnout5/Shaders/<File>.fx.Shader?ID=<num>[_<Technique>_<Stage>]
_URI_STAGE_RE = re.compile(
    r"Shaders/(?P<fx>.+?)\.fx\.Shader\?ID=\d+_(?P<tech>.+?)_(?P<stage>VertexShader|PixelShader)$"
)
_URI_SHADER_RE = re.compile(r"Shaders/(?P<fx>.+?)\.fx\.Shader\?ID=\d+$")
# Bare shared name, e.g. "ZOnlyOpaqueSingleSided_VertexShader"
_BARE_RE = re.compile(r"^(?P<tech>.+?)_(?P<stage>VertexShader|PixelShader)$")

_STAGE_KEY = {"VertexShader": "vs", "PixelShader": "ps"}


def manifest_path(platform):
    return os.path.join(context.MANIFEST_DIR, platform + ".json")


def debug_xml_path(platform, version="Breaker"):
    if platform == "bpr":
        return os.path.join(context.BPR_REF, ".debug.xml")
    return os.path.join(context.x360_ref(version), ".debug.xml")


# --- reading the reference corpus -------------------------------------------


class ResourceIndex(object):
    """Everything .debug.xml (plus optionally ResourceDB.json) knows about IDs."""

    def __init__(self):
        self.per_technique = {}  # (fx, technique, stage) -> id
        self.shared = {}  # (shared_name, stage) -> id
        self.shader = {}  # fx -> id of the 0x32 Shader resource
        self.other = {}  # id -> (type, name) for everything else

    def _add(self, res_id, res_type, name):
        res_id = res_id.lower()
        stage = _STAGE_KEY.get(res_type)
        if stage:
            m = _URI_STAGE_RE.search(name)
            if m:
                self.per_technique[(m.group("fx"), m.group("tech"), stage)] = res_id
                return
            m = _BARE_RE.match(name)
            if m and _STAGE_KEY.get(m.group("stage")) == stage:
                self.shared[(m.group("tech"), stage)] = res_id
                return
        if res_type == "Shader":
            m = _URI_SHADER_RE.search(name)
            if m:
                self.shader[m.group("fx")] = res_id
                return
        self.other[res_id] = (res_type, name)

    @classmethod
    def from_debug_xml(cls, path):
        index = cls()
        if not os.path.isfile(path):
            raise proc.StepError(
                "debug XML not found: %s" % context.rel(path),
                code=context.EXIT_SKIP,
                fix="extract the stock SHADERS bundle for this platform (yap e)",
            )
        root = ET.parse(path).getroot()
        for node in root.iter("Resource"):
            res_id = node.get("id")
            name = node.get("name")
            if res_id and name:
                index._add(res_id, node.get("type") or "", name)
        return index

    def merge_resource_db(self, path):
        """Fill gaps from ResourceDB.json. Never overrides .debug.xml.

        The X360 build needed this because its bundle's debug XML does not name
        every shader resource. Loaded once here, not once per build.
        """
        if not os.path.isfile(path):
            return 0
        with open(path, "r", encoding="utf-8") as handle:
            db = json.load(handle)
        added = 0
        for res_id, name in db.items():
            if not isinstance(name, str) or "Shaders/" not in name:
                continue
            m = _URI_STAGE_RE.search(name)
            if m:
                key = (m.group("fx"), m.group("tech"), _STAGE_KEY[m.group("stage")])
                if key not in self.per_technique:
                    self.per_technique[key] = res_id.lower()
                    added += 1
                continue
            m = _URI_SHADER_RE.search(name)
            if m and m.group("fx") not in self.shader:
                self.shader[m.group("fx")] = res_id.lower()
                added += 1
        return added


# --- generating --------------------------------------------------------------


def _collect_source_techniques():
    """(per_file, shared) from the .fx corpus.

    per_file: list of (fx_name, Technique)
    shared:   shared_name -> list of (fx_name, Technique)
    """
    per_file = []
    shared = defaultdict(list)
    for path in fx.shader_files(context.SHADERS_DIR):
        name = fx.base_name(path)
        for tech in fx.parse_techniques(path):
            if tech.shared_name:
                shared[tech.shared_name].append((name, tech, path))
            else:
                per_file.append((name, tech, path))
    return per_file, shared


def _canonical_for(shared_name, entries, previous):
    """Pick the source .fx that defines a shared technique, and list disagreements.

    Deterministic default: the body that the most files agree on, ties broken
    alphabetically. Where a human has already recorded a `canonical` in the
    committed manifest we keep it — regen must never silently undo that call.
    """
    by_signature = defaultdict(list)
    for name, tech, path in entries:
        by_signature[fx.technique_signature(path, tech)].append(name)

    groups = sorted(by_signature.items(), key=lambda kv: (-len(kv[1]), sorted(kv[1])[0]))
    default = sorted(groups[0][1])[0]

    kept = previous.get(shared_name, {}).get("canonical")
    canonical = kept if kept and any(kept in names for _s, names in groups) else default

    conflicts = []
    if len(groups) > 1:
        for signature, names in groups:
            conflicts.append({"signature": signature, "sources": sorted(names)})

    return canonical, conflicts, (kept == canonical and kept != default)


def _load_previous(platform):
    path = manifest_path(platform)
    if not os.path.isfile(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8") as handle:
            data = json.load(handle)
    except (OSError, ValueError):
        return {}
    return {entry["shared_name"]: entry for entry in data.get("shared", [])}


def generate(platform, version="Breaker", keep_overrides=True):
    xml_path = debug_xml_path(platform, version)
    index = ResourceIndex.from_debug_xml(xml_path)
    sources = [context.rel(xml_path)]

    if platform == "x360":
        added = index.merge_resource_db(context.RESOURCE_DB)
        if added:
            sources.append(context.rel(context.RESOURCE_DB))
            proc.detail("ResourceDB.json contributed %d ids" % added)

    previous = _load_previous(platform) if keep_overrides else {}
    per_file, shared_src = _collect_source_techniques()

    targets = []
    unresolved = []
    used = set()

    for name, tech, _path in sorted(per_file, key=lambda t: (t[0], t[1].name)):
        entry = {
            "fx": name,
            "technique": tech.name,
            "vs_entry": tech.vs,
            "ps_entry": tech.ps,
        }
        missing = []
        for stage in ("vs", "ps"):
            res_id = index.per_technique.get((name, tech.name, stage))
            if res_id:
                entry[stage] = res_id
                used.add(res_id)
            else:
                missing.append(stage)
        shader_id = index.shader.get(name)
        if shader_id:
            entry["shader"] = shader_id
            used.add(shader_id)
        if missing:
            entry["missing"] = missing
            unresolved.append({"fx": name, "technique": tech.name, "stages": missing})
        targets.append(entry)

    shared = []
    overridden = []
    for shared_name in sorted(shared_src):
        entries = shared_src[shared_name]
        canonical, conflicts, was_override = _canonical_for(shared_name, entries, previous)
        if was_override:
            overridden.append(shared_name)
        tech = next(t for n, t, _p in entries if n == canonical)
        record = {
            "shared_name": shared_name,
            "technique": tech.name,
            "canonical": canonical,
            "vs_entry": tech.vs,
            "ps_entry": tech.ps,
            "sources": sorted(n for n, _t, _p in entries),
        }
        missing = []
        for stage in ("vs", "ps"):
            res_id = index.shared.get((shared_name, stage))
            if res_id:
                record[stage] = res_id
                used.add(res_id)
            else:
                missing.append(stage)
        if missing:
            record["missing"] = missing
            unresolved.append({"shared_name": shared_name, "stages": missing})
        if conflicts:
            record["conflicts"] = conflicts
        shared.append(record)

    # Resources the bundle has that no source technique claims. Not an error —
    # the reference bundle legitimately contains shaders we have no source for —
    # but worth seeing, since it bounds what a full rebuild can replace.
    orphans = []
    for (name, tech, stage), res_id in sorted(index.per_technique.items()):
        if res_id not in used:
            orphans.append({"id": res_id, "fx": name, "technique": tech, "stage": stage})
    for (shared_name, stage), res_id in sorted(index.shared.items()):
        if res_id not in used:
            orphans.append({"id": res_id, "shared_name": shared_name, "stage": stage})

    return {
        "schema": SCHEMA,
        "platform": platform,
        "generated_from": sources,
        "targets": targets,
        "shared": shared,
        "unresolved": unresolved,
        "orphans": orphans,
    }, overridden


def dumps(data):
    """Canonical rendering: sorted keys, 2-space indent, trailing newline, LF."""
    return json.dumps(data, indent=2, sort_keys=True, ensure_ascii=False) + "\n"


def write(platform, data):
    path = manifest_path(platform)
    proc.ensure_dir(os.path.dirname(path))
    if proc.DRY_RUN:
        proc.info("DRY-RUN: would write %s" % context.rel(path))
        return path
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(dumps(data))
    return path


def load(platform):
    path = manifest_path(platform)
    if not os.path.isfile(path):
        raise proc.StepError(
            "no manifest for %s" % platform,
            code=context.EXIT_SKIP,
            fix="run: nushaders.py manifest regen --platform %s" % platform,
        )
    with open(path, "r", encoding="utf-8") as handle:
        data = json.load(handle)
    if data.get("schema") != SCHEMA:
        raise proc.StepError(
            "manifest %s has schema %s, this build system speaks %d"
            % (context.rel(path), data.get("schema"), SCHEMA),
            fix="run: nushaders.py manifest regen --platform %s" % platform,
        )
    return data


# --- summary shared by regen / show / check ---------------------------------


def summarize(data):
    conflicted = [s for s in data["shared"] if s.get("conflicts")]
    proc.info(
        "%d targets, %d shared, %d unresolved, %d orphan ids"
        % (
            len(data["targets"]),
            len(data["shared"]),
            len(data["unresolved"]),
            len(data["orphans"]),
        )
    )
    for entry in conflicted:
        proc.warn(
            "shared technique %s has %d differing implementations across %d files; "
            "building '%s'"
            % (
                entry["shared_name"],
                len(entry["conflicts"]),
                len(entry["sources"]),
                entry["canonical"],
            )
        )
        for group in entry["conflicts"]:
            mark = "->" if entry["canonical"] in group["sources"] else "  "
            proc.info("     %s %s  %s" % (mark, group["signature"], ", ".join(group["sources"])))
        proc.fix(
            "the engine stores ONE resource for %s. If a variant needs its own "
            'behaviour it needs its own id; otherwise reconcile the source, or set '
            '"canonical" in the manifest (regen keeps it).' % entry["shared_name"]
        )
    return len(conflicted)


# --- verbs -------------------------------------------------------------------


def regen(platform=None, version="Breaker", reset=False):
    platforms = [platform] if platform else ["x360", "bpr"]
    for name in platforms:
        proc.step("manifest regen %s" % name)
        data, overridden = generate(name, version, keep_overrides=not reset)
        for shared_name in overridden:
            proc.info("keeping canonical override for %s" % shared_name)
        summarize(data)
        path = write(name, data)
        proc.info("wrote %s" % context.rel(path))
    return context.EXIT_OK


def show(platform):
    data = load(platform)
    proc.step("manifest show %s" % platform)
    summarize(data)
    for entry in data["unresolved"][:20]:
        proc.info(
            "unresolved  %s %s"
            % (entry.get("fx") or entry.get("shared_name"), ",".join(entry["stages"]))
        )
    if len(data["unresolved"]) > 20:
        proc.info("... and %d more unresolved" % (len(data["unresolved"]) - 20))
    return context.EXIT_OK


def check(platform=None, version="Breaker"):
    """Re-derive and compare against what is committed. Non-zero on drift."""
    platforms = [platform] if platform else ["x360", "bpr"]
    drifted = []
    for name in platforms:
        proc.step("manifest check %s" % name)
        fresh, _overridden = generate(name, version, keep_overrides=True)
        path = manifest_path(name)
        if not os.path.isfile(path):
            proc.error("missing %s" % context.rel(path))
            drifted.append(name)
            continue
        with open(path, "r", encoding="utf-8") as handle:
            committed = handle.read()
        if committed != dumps(fresh):
            proc.error("%s is out of date" % context.rel(path))
            drifted.append(name)
        else:
            proc.info("%s up to date" % context.rel(path))

    if drifted:
        raise proc.StepError(
            "manifest drift: %s" % ", ".join(drifted),
            fix="run: nushaders.py manifest regen",
        )
    return context.EXIT_OK

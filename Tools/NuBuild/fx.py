"""Parsing .fx source: techniques, entry points, and the transitive include set.

Replaces three near-identical regexes in build_shader_360.ps1,
build_xbox360_resources.ps1 and compile_pc_bpr.ps1.

The one substantive difference from those: we read the technique's annotation
block. A technique declared as

    technique ZOnlyOpaqueSingleSided
    <
      string sharedName="ZOnlyOpaqueSingleSided";
    >
    { pass p0 { VertexShader = compile vs_3_0 VS_Main_ZOnly(); ... } }

is SHARED — the engine stores one resource for it no matter how many .fx files
declare it (ZOnlyOpaqueSingleSided appears in 55 of the 85 shaders, and the
.debug.xml has exactly one id for it, under the bare name rather than a
gamedb:// URI). The old regex skipped the annotation with `(?:<[^>]*>)?`, so the
bulk build compiled each copy and wrote the same resource id 55 times, last
writer winning silently. manifest.py uses shared_name to collapse those to one
build action and to check the copies actually agree.
"""

import os
import re
from collections import namedtuple

Technique = namedtuple("Technique", "name shared_name vs ps vs_target ps_target")

_COMMENT_RE = re.compile(r"//[^\n]*|/\*.*?\*/", re.DOTALL)
_TECHNIQUE_RE = re.compile(r"\btechnique\s+(\w+)")
_SHARED_NAME_RE = re.compile(r'\bsharedName\s*=\s*"([^"]*)"')
_STAGE_RE = {
    "vs": re.compile(r"VertexShader\s*=\s*compile\s+(vs_\d_\d)\s+(\w+)\s*\("),
    "ps": re.compile(r"PixelShader\s*=\s*compile\s+(ps_\d_\d)\s+(\w+)\s*\("),
}
_INCLUDE_RE = re.compile(r'^\s*#\s*include\s*"([^"]+)"', re.MULTILINE)

_technique_cache = {}
_include_cache = {}


def _read(path):
    # The .fx corpus is a mix of ASCII and the odd Latin-1 byte in a comment.
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        return handle.read()


def _strip_comments(text):
    """Blank out comments, preserving newlines so any error offsets stay sane."""

    def blank(match):
        return re.sub(r"[^\n]", " ", match.group(0))

    return _COMMENT_RE.sub(blank, text)


def _match_block(text, start, opener, closer):
    """Return (body, index_after_close) for the block whose opener is at/after start.

    Brace-counting rather than a non-greedy regex: a technique body contains a
    nested `pass p0 { ... }`, so `\\{(.+?)\\}` stops at the pass's closing brace
    and only happens to still catch the compile statements. That is luck, not a
    parse, and it breaks on a technique with two passes.
    """
    i = text.find(opener, start)
    if i < 0:
        return None, start
    depth = 0
    for j in range(i, len(text)):
        c = text[j]
        if c == opener:
            depth += 1
        elif c == closer:
            depth -= 1
            if depth == 0:
                return text[i + 1 : j], j + 1
    return None, start  # unbalanced


def parse_techniques(fx_path):
    """All techniques in one .fx, in source order. Memoized per path+mtime."""
    key = (os.path.abspath(fx_path), os.path.getmtime(fx_path))
    if key in _technique_cache:
        return _technique_cache[key]

    text = _strip_comments(_read(fx_path))
    out = []

    for m in _TECHNIQUE_RE.finditer(text):
        name = m.group(1)
        pos = m.end()

        # Optional annotation block <...> before the body. Only treat it as an
        # annotation if it really precedes the body, so `technique X { }` and a
        # stray '<' later in the file cannot be confused.
        annotation = ""
        brace = text.find("{", pos)
        angle = text.find("<", pos)
        if angle != -1 and (brace == -1 or angle < brace):
            annotation, pos = _match_block(text, pos, "<", ">")
            annotation = annotation or ""

        body, _end = _match_block(text, pos, "{", "}")
        if body is None:
            continue

        shared = _SHARED_NAME_RE.search(annotation)
        stages = {}
        for stage, pattern in _STAGE_RE.items():
            sm = pattern.search(body)
            stages[stage] = (sm.group(1), sm.group(2)) if sm else (None, None)

        out.append(
            Technique(
                name=name,
                shared_name=shared.group(1) if shared else None,
                vs=stages["vs"][1],
                ps=stages["ps"][1],
                vs_target=stages["vs"][0],
                ps_target=stages["ps"][0],
            )
        )

    _technique_cache[key] = out
    return out


def scan_includes(fx_path, include_dirs):
    """Every .fxh reachable from fx_path, transitively. Absolute paths.

    #ifdef guards around includes are deliberately ignored — this feeds cache
    invalidation, and a superset only ever costs an occasional extra rebuild,
    whereas a subset silently serves stale output. (Diffuse_Opaque_Singlesided
    includes OrenNayar.fxh only under D_OREN_NAYAR; we hash it either way.)
    """
    key = (os.path.abspath(fx_path), tuple(include_dirs))
    if key in _include_cache:
        return _include_cache[key]

    seen = set()
    pending = [os.path.abspath(fx_path)]
    while pending:
        current = pending.pop()
        try:
            text = _read(current)
        except OSError:
            continue
        here = os.path.dirname(current)
        for rel_path in _INCLUDE_RE.findall(text):
            resolved = None
            for base in [here] + list(include_dirs):
                candidate = os.path.normpath(os.path.join(base, rel_path))
                if os.path.isfile(candidate):
                    resolved = candidate
                    break
            if resolved and resolved not in seen:
                seen.add(resolved)
                pending.append(resolved)

    _include_cache[key] = seen
    return seen


_FUNC_START = r"^[A-Za-z_][\w<>,\s\*]*?\b%s\s*\("


def entry_source(fx_path, entry):
    """Source text of one entry-point function, signature through closing brace.

    Used to tell whether the copies of a shared technique actually agree. Returns
    None if the function is not defined in this file (it may come from an .fxh).
    """
    if not entry:
        return None
    text = _strip_comments(_read(fx_path))
    m = re.search(_FUNC_START % re.escape(entry), text, re.MULTILINE)
    if not m:
        return None
    body, end = _match_block(text, m.end() - 1, "{", "}")
    if body is None:
        return None
    return text[m.start() : end]


def technique_signature(fx_path, technique):
    """Stable hash of a technique's VS+PS entry sources, for agreement checks.

    Whitespace is normalized so a reindent does not read as a behaviour change,
    but macro spelling is NOT (tex2D vs SAMPLE2D is a real portedness
    difference and must show up as a conflict).
    """
    import hashlib

    parts = []
    for entry in (technique.vs, technique.ps):
        src = entry_source(fx_path, entry)
        parts.append(re.sub(r"\s+", " ", src).strip() if src else "<undefined:%s>" % entry)
    return hashlib.sha256("\n".join(parts).encode("utf-8")).hexdigest()[:16]


def shader_files(shaders_dir, pattern="*.fx"):
    """Sorted .fx paths under shaders_dir matching a glob."""
    import fnmatch

    if not os.path.isdir(shaders_dir):
        return []
    names = [n for n in os.listdir(shaders_dir) if n.endswith(".fx")]
    if pattern and pattern != "*.fx":
        names = [n for n in names if fnmatch.fnmatch(n, pattern)]
    return [os.path.join(shaders_dir, n) for n in sorted(names)]


def base_name(fx_path):
    return os.path.splitext(os.path.basename(fx_path))[0]

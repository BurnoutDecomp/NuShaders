"""First-time setup: getting the stock shader bundles into Reference/.

A fresh clone does not have everything the build needs. The Remastered corpus is
committed, but none of the Xbox 360 ones are — the repo carries only empty
`place_*_shader_bundle_here` marker files where they belong. Without them,
`build x360`, `manifest regen --platform x360` and `verify x360` have nothing to
read, because the resource-id map is derived from the bundle's own .debug.xml.

Rather than telling people to run YAP by hand and land the output in exactly the
right directory, `reference import` takes a .bndl/.BUNDLE, unpacks it, works out
which platform it is from its .meta.yaml, and puts it where the build looks.

    nushaders.py reference status
    nushaders.py reference import "D:/dumps/SHADERS.bndl" --version Breaker

Supplying the bundles is still the user's job: they come from a game install or
a devkit, and are not ours to distribute.
"""

import os
import shutil
from collections import namedtuple

from . import context, proc, toolchain

# platform: as used by targets/config. version: X360 game build, None for BPR.
Corpus = namedtuple("Corpus", "platform version path label needed_by")

# Marker files the repo ships so the directories exist and are self-describing.
MARKERS = {
    "bpr": "place_BPR_shader_bundle_here",
    "Breaker": "place_360_breaker_shader_bundle_here",
    "1.6": "place_360_1.6_shader_bundle_here",
    "1.8": "place_360_1.8_shader_bundle_here",
}


def corpora():
    out = [Corpus("bpr", None, context.BPR_REF,
                  "BPR (Remastered PC)",
                  "build bpr, verify bpr, manifest regen --platform bpr")]
    for version in context.X360_VERSIONS:
        out.append(Corpus("x360", version, context.x360_ref(version),
                          "X360 %s" % version,
                          "verify x360" if version != "Breaker"
                          else "build x360, verify x360, manifest regen --platform x360"))
    return tuple(out)


def _state(corpus):
    """(present, detail) for one corpus."""
    if not os.path.isdir(corpus.path):
        return False, "directory missing"
    spb = os.path.join(corpus.path, "ShaderProgramBuffer")
    if not os.path.isdir(spb):
        return False, "no ShaderProgramBuffer/"
    pairs = len([n for n in os.listdir(spb) if n.endswith("_primary.dat")])
    has_debug = os.path.isfile(os.path.join(corpus.path, ".debug.xml"))
    if pairs == 0:
        return False, "no resources unpacked"
    detail = "%d shader resources" % pairs
    if not has_debug:
        # The build can still verify, but not derive ids.
        detail += ", NO .debug.xml (manifest regen will not work)"
    return True, detail


def status():
    proc.step("reference status")
    missing = []
    for corpus in corpora():
        present, detail = _state(corpus)
        proc.info("%-4s %-16s %s" % ("ok" if present else "MISS", corpus.label, detail))
        if not present:
            missing.append(corpus)

    if not missing:
        proc.info("")
        proc.info("every reference corpus is in place")
        return context.EXIT_OK

    proc.info("")
    for corpus in missing:
        proc.info("%s is needed by: %s" % (corpus.label, corpus.needed_by))
    proc.fix('unpack one with: nushaders.py reference import <bundle>%s'
             % (" --version <ver>" if any(c.platform == "x360" for c in missing) else ""))
    # Missing corpora are an environment gap, not a failure of this command.
    return context.EXIT_SKIP


def _destination(platform, version):
    if platform == "bpr":
        return context.BPR_REF
    return context.x360_ref(version)


def _normalise_newlines(directory):
    """Convert YAP's CRLF text output to LF. Returns how many files changed.

    YAP writes .debug.xml and the *_imports.yaml sidecars with CRLF, but the
    committed corpus is LF and ImportsYAML.ToYAML() emits LF. Without this,
    importing a bundle silently reintroduces the exact mismatch .gitattributes
    exists to prevent: 86 round-trip tests fail and `git status` shows hundreds
    of line-ending-only modifications, with nothing pointing at the import as
    the cause.

    Binary resources are left alone — a .dat containing 0x0D0A is data, not a
    line ending, and rewriting it would corrupt the corpus.
    """
    changed = 0
    for root, _dirs, files in os.walk(directory):
        for name in files:
            path = os.path.join(root, name)
            try:
                data = open(path, "rb").read()
            except OSError:
                continue
            if b"\x00" in data[:8192] or b"\r\n" not in data:
                continue
            with open(path, "wb") as handle:
                handle.write(data.replace(b"\r\n", b"\n"))
            changed += 1
    return changed


def _describe(directory):
    """Resource-type dirs and counts, for the post-import report."""
    rows = []
    for name in sorted(os.listdir(directory)):
        sub = os.path.join(directory, name)
        if os.path.isdir(sub):
            rows.append((name, len(os.listdir(sub))))
    return rows


def import_bundle(bundle_path, version=None, force=False):
    from . import bundle as bundle_mod

    yap = toolchain.require(toolchain.yap())
    bundle_path = os.path.abspath(os.path.expanduser(bundle_path))
    if not os.path.isfile(bundle_path):
        raise proc.StepError("no such bundle: %s" % bundle_path,
                             fix="point this at a SHADERS.bndl / SHADERS.BUNDLE")

    proc.step("reference import %s" % os.path.basename(bundle_path))

    # Unpack somewhere disposable first. We cannot know which corpus this is
    # until we have read its .meta.yaml, and unpacking straight into a guessed
    # destination would corrupt whatever was already there if the guess is wrong.
    scratch = os.path.join(context.CACHE_DIR, "reference-import")
    proc.remove_tree(scratch)
    proc.ensure_dir(scratch)
    proc.run([yap, "e", bundle_path, scratch])

    meta = bundle_mod.meta_platform(scratch)
    platform = {1: "bpr", 2: "x360"}.get(meta)
    if platform is None:
        raise proc.StepError(
            "could not read a platform from the bundle's .meta.yaml (got %r)" % meta,
            fix="expected platform 1 (BPR) or 2 (X360); is this a SHADERS bundle?",
        )
    proc.info("bundle reports platform %d (%s)" % (meta, platform))

    if platform == "x360":
        if not version:
            raise proc.StepError(
                "this is an Xbox 360 bundle, so which game build is it?",
                fix="pass --version %s  (they cannot be told apart from content)"
                    % "|".join(context.X360_VERSIONS),
            )
    elif version:
        proc.warn("--version is meaningless for a BPR bundle; ignoring it")
        version = None

    destination = _destination(platform, version)
    present, detail = _state(Corpus(platform, version, destination, "", ""))
    if present and not force:
        raise proc.StepError(
            "%s already holds %s" % (context.rel(destination), detail),
            fix="pass --force to replace it",
        )

    if not proc.DRY_RUN:
        # Keep the marker file: it is what documents the directory in a clone
        # that has not imported anything yet.
        marker = MARKERS.get(version or platform)
        parent = os.path.dirname(destination)
        if os.path.isdir(destination):
            shutil.rmtree(destination)
        proc.ensure_dir(parent)
        shutil.move(scratch, destination)
        if marker and not os.path.exists(os.path.join(parent, marker)):
            open(os.path.join(parent, marker), "a").close()

    proc.info("unpacked -> %s" % context.rel(destination))
    if not proc.DRY_RUN:
        normalised = _normalise_newlines(destination)
        if normalised:
            proc.info("normalised %d text file(s) to LF" % normalised)
        for name, count in _describe(destination):
            proc.info("  %-24s %d" % (name, count))
        if not os.path.isfile(os.path.join(destination, ".debug.xml")):
            proc.warn("this bundle has no .debug.xml, so `manifest regen` cannot "
                      "derive resource ids from it")
        else:
            proc.info("")
            proc.fix("next: nushaders.py manifest regen --platform %s" % platform)
    return context.EXIT_OK

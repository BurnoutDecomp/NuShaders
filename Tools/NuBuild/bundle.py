"""Bundle extract -> inject -> repack, via YAP. The shared half of repack_and_send.ps1.

The cycle is identical on both platforms; only the deploy step that follows it
differs, so this module knows nothing about devkits or game installs.

The .meta.yaml platform guard is the important part. A bundle records which
platform it was exported for (1 = BPR/PC, 2 = X360). Repacking an X360 bundle
with a BPR config produces a wrong-endian file the game rejects without a useful
error, so we refuse before writing anything.
"""

import os

from . import config, context, proc, toolchain


def meta_platform(extracted_dir):
    """The `platform:` value from an extracted bundle's .meta.yaml, or None.

    Deliberately a one-line scan rather than a YAML parse: this is the only
    field we need, and pulling in a YAML dependency for it is not worth it.
    """
    meta = os.path.join(extracted_dir, ".meta.yaml")
    if not os.path.isfile(meta):
        return None
    with open(meta, "r", encoding="utf-8", errors="replace") as handle:
        for line in handle:
            stripped = line.strip()
            if stripped.startswith("platform:"):
                value = stripped.split(":", 1)[1].strip()
                try:
                    return int(value, 0)
                except ValueError:
                    return None
    return None


def extract(cfg):
    yap = toolchain.require(toolchain.yap())
    source = cfg.resolved("og_bundle")
    target = cfg.resolved("extracted_dir")

    if not os.path.isfile(source):
        raise proc.StepError("stock bundle not found: %s" % source,
                             fix="point og_bundle at the game's SHADERS bundle")

    proc.info("extract %s -> %s" % (os.path.basename(source), context.rel(target)))
    proc.ensure_dir(target)
    # YAP prints a progress line per resource with no newlines; capture it and
    # let proc.run surface it only if the command actually fails.
    proc.run([yap, "e", source, target])

    expected = config.META_PLATFORM.get(cfg.platform)
    actual = meta_platform(target)
    if expected and actual is not None and actual != expected:
        raise proc.StepError(
            "platform mismatch: config says %s (bundle platform %d) but %s "
            "extracted as platform %d" % (cfg.platform, expected,
                                          os.path.basename(source), actual),
            fix="point og_bundle at the %s stock bundle" % cfg.platform,
        )
    return target


def inject(cfg):
    """Copy our packed resources over the extracted tree."""
    target = cfg.resolved("extracted_dir")
    spb_source = cfg.spb_source
    if not spb_source or not os.path.isdir(spb_source):
        raise proc.StepError(
            "no packed resources at %s" % context.rel(spb_source or "(unset)"),
            fix="run: nushaders.py build %s" % cfg.platform,
        )

    spb_dest = os.path.join(target, "ShaderProgramBuffer")
    count = proc.mirror_tree(spb_source, spb_dest)
    proc.info("injected %d ShaderProgramBuffer file(s)" % count)

    shader_source = cfg.shader_source
    if shader_source and os.path.isdir(shader_source):
        extra = proc.mirror_tree(shader_source, os.path.join(target, "Shader"))
        proc.info("injected %d Shader (0x32) file(s)" % extra)
    return count


def repack(cfg, out=None):
    yap = toolchain.require(toolchain.yap())
    source = cfg.resolved("extracted_dir")
    target = out or cfg.resolved("new_bundle")

    proc.ensure_dir(os.path.dirname(target))
    proc.info("repack %s -> %s" % (context.rel(source), context.rel(target)))
    proc.run([yap, "c", source, target])
    return target


def make(cfg, out=None):
    """The whole cycle. Returns the path of the repacked bundle."""
    proc.step("bundle %s" % cfg.platform)
    extract(cfg)
    inject(cfg)
    return repack(cfg, out)

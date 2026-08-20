"""NuShaders build system — module map.

Stdlib only, Python 3.11+. Run it via nushaders.py at the repo root.

    context     repo paths and the exit-code contract; the ONE place a path lives
    proc        process execution, --dry-run, STEP:/RESULT: output, StepError
    toolchain   Missing(what, fix) resolvers for fxc, xsd, YAP, nushaders.exe, xbcp
    doctor      per-role preflight: what is present, what is missing, how to fix it

    fx          .fx parsing: techniques, entry points, transitive includes
    manifest    the shader <-> resource-id map, generated and committed
    targets     platform and variant tables — the one declarative source
    cache       content-hash build cache
    graph       Action model and the threaded scheduler
    compile     builds the action list: fxc, then xsd, then the packer
    verify      byte-parity against the shipped resources in Reference/

    config      per-machine deploy config (Build/config/<hostname>.toml)
    bundle      YAP extract -> inject -> repack
    deploy      devkit (xbcp/xbreboot) and PC install, plus the autotest push

    tools       wrappers around the out-of-scope C#/C++ projects (owns neither)
    cli         argparse verb tree; the single catch site for StepError

Conventions worth keeping:

  Every miss names its fix. A message that only says what broke makes the reader
  go and read this source; `fix=` makes that unnecessary.

  Exit 2 means "this machine cannot run that", not "that is broken". A missing
  Xbox 360 XDK must never read as a failing shader, or people learn to ignore
  red output.

  Nothing here reaches for a third-party package. That is what lets someone with
  a bare Python install clone the repo and build, and it is worth more than any
  convenience a dependency would buy. The one place it cost something is the
  devkit reboot, which uses xbreboot.exe rather than the XDevkit COM object.
"""

#!/usr/bin/env python3
"""Entry point for the NuShaders build system.

Everything lives in Tools/NuBuild; this shim only fixes up sys.path and checks
the interpreter version so a bad Python gives a readable error instead of a
SyntaxError or a missing-tomllib traceback halfway through a build.

    python nushaders.py doctor all
    python nushaders.py build bpr
"""

import os
import sys

if sys.version_info < (3, 11):
    sys.stderr.write(
        "ERROR: the NuShaders build system needs Python 3.11+ (found %d.%d).\n"
        "  Fix: install a newer Python, or run it as `py -3.11 nushaders.py ...`\n"
        % (sys.version_info[0], sys.version_info[1])
    )
    raise SystemExit(1)

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "Tools"))

from NuBuild.cli import main  # noqa: E402

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

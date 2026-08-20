"""Convenience wrappers around build systems this project does NOT own.

Tools/ShaderApp (the C# NuShaders app) and Tools/Mod/SSRHook keep their own
MSBuild and CMake graphs. Nothing here models their dependencies, chooses their
outputs, or caches anything about them — these verbs exist only so a contributor
does not have to remember the exact csproj path or the CMake generator flags.

If a wrapper here ever grows a rule about *how* those projects build, it belongs
in those projects instead.
"""

import os

from . import context, proc, toolchain


def build_cli(configuration="Release"):
    dotnet = toolchain.require(toolchain.dotnet())
    proc.step("tools build-cli (%s)" % configuration)
    proc.run(
        [dotnet, "build", context.NUSHADERS_CSPROJ, "-c", configuration, "-v", "q", "--nologo"],
        capture=False,
    )
    toolchain.forget_nushaders()
    exe = toolchain.nushaders()
    if not exe:
        raise proc.StepError(
            "dotnet build succeeded but nushaders.exe was not where we look for it",
            fix="set NUSHADERS_EXE to the built exe path",
        )
    proc.info("nushaders.exe -> %s" % exe)
    return context.EXIT_OK


def build_ssrhook(configuration="Release"):
    cmake = toolchain.require(toolchain.cmake())
    if not os.path.isfile(os.path.join(context.SSRHOOK_DIR, "CMakeLists.txt")):
        raise proc.StepError(
            "no CMakeLists.txt in %s" % context.rel(context.SSRHOOK_DIR),
            code=context.EXIT_SKIP,
            fix="the SSRHook mod source is not present in this checkout",
        )
    build_dir = os.path.join(context.SSRHOOK_DIR, "build")
    proc.step("tools build-ssrhook (%s)" % configuration)
    proc.run([cmake, "-B", build_dir, "-S", context.SSRHOOK_DIR, "-A", "x64"], capture=False)
    proc.run([cmake, "--build", build_dir, "--config", configuration], capture=False)
    proc.info("output -> %s" % context.rel(os.path.join(build_dir, configuration)))
    return context.EXIT_OK


def test_cli():
    dotnet = toolchain.require(toolchain.dotnet())
    proc.step("tools test-cli")
    proc.run([dotnet, "test", context.NUSHADERS_SLN, "-v", "q", "--nologo"], capture=False)
    return context.EXIT_OK


def fetch_yap():
    proc.step("tools fetch-yap")
    found = toolchain.fetch_yap()
    if not found:
        raise proc.StepError("YAP still not resolvable after fetch", fix=found.fix)
    proc.info("YAP.exe -> %s" % found)
    return context.EXIT_OK

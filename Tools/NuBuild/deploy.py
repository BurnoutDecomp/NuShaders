"""Getting a repacked bundle onto the target, and pushing the autotest scripts.

Replaces the platform-specific halves of repack_and_send.ps1 and all of
deploy_autotest.ps1.

One deliberate change: repack_and_send.ps1 rebooted the devkit through the
XDevkit.XboxManager COM object. Driving COM from Python needs pywin32, and this
build system is stdlib-only on purpose. deploy_autotest.ps1 already rebooted the
same devkit with the xbreboot.exe CLI, so that is what both paths use now.

The COM call passed an explicit media directory that the CLI infers from the
executable path instead; deploy_autotest relied on that inference already.

While porting, one latent bug is fixed rather than carried over:
deploy_autotest.ps1 passed /X:<ip> to xbcp but not to xbreboot, so it rebooted
whichever console was default rather than the configured one. Invisible with a
single devkit on the desk, wrong with two. Every devkit call here is targeted.
"""

import os

from . import bundle, config, context, proc, toolchain


def _target_flag(cfg):
    return "/X:%s" % cfg.get("ip", "x360")


def _xbcp(cfg, source, dest):
    xbcp = toolchain.require(toolchain.xbcp())
    proc.run([xbcp, "/Y", _target_flag(cfg), source, dest], capture=False)


def _reboot(cfg, xex_path, extra_args=()):
    xbreboot = toolchain.require(toolchain.xbreboot())
    argv = [xbreboot, _target_flag(cfg), xex_path] + list(extra_args)
    proc.info("reboot %s %s" % (xex_path, " ".join(extra_args)))
    proc.run(argv, capture=False)


def _game_paths(cfg):
    game_path = str(cfg.get("game_path", "x360") or "").rstrip("\\/")
    xex = cfg.get("xex", "x360")
    return game_path, "%s\\%s" % (game_path, xex)


# --- platform deploys --------------------------------------------------------


def deploy_x360(cfg, bundle_path, launch=True):
    game_path, xex_path = _game_paths(cfg)
    dest = "%s\\SHADERS.bndl" % game_path

    proc.info("send %s -> %s" % (context.rel(bundle_path), dest))
    _xbcp(cfg, bundle_path, dest)

    if launch:
        _reboot(cfg, xex_path, cfg.launch_args)
    else:
        proc.info("bundle deployed (no reboot requested)")
    return context.EXIT_OK


def deploy_bpr(cfg, bundle_path, launch=True):
    dest = cfg.resolved("game_bundle", "bpr")
    backup = dest + ".orig"

    # Back the stock bundle up exactly once, so the install stays restorable
    # however many times we deploy over it.
    if os.path.isfile(dest) and not os.path.exists(backup):
        proc.copy_file(dest, backup)
        proc.info("backed up stock bundle -> %s" % os.path.basename(backup))

    proc.ensure_dir(os.path.dirname(dest))
    proc.copy_file(bundle_path, dest)
    proc.info("copied bundle -> %s" % dest)

    if launch:
        exe = cfg.resolved("game_exe", "bpr")
        proc.info("launch %s %s" % (os.path.basename(exe), " ".join(cfg.launch_args)))
        proc.spawn([exe] + cfg.launch_args, cwd=os.path.dirname(exe))
    return context.EXIT_OK


def restore_bpr(cfg):
    """Put the stock bundle back from the .orig we took on first deploy."""
    dest = cfg.resolved("game_bundle", "bpr")
    backup = dest + ".orig"
    proc.step("deploy restore")
    if not os.path.isfile(backup):
        raise proc.StepError("no backup at %s" % backup,
                             code=context.EXIT_SKIP,
                             fix="nothing to restore; the install was never overwritten")
    proc.copy_file(backup, dest)
    proc.info("restored %s" % dest)
    return context.EXIT_OK


# --- verbs -------------------------------------------------------------------


def deploy(platform=None, name=None, no_build=False, no_bundle=False, launch=True,
           jobs=None):
    cfg = config.load(name).require_valid()
    if platform and platform != cfg.platform:
        raise proc.StepError(
            "config %s targets %s, not %s" % (context.rel(cfg.path), cfg.platform, platform),
            fix="use a config whose platform matches, or: config set platform %s" % platform,
        )

    if not no_build:
        from . import compile as compile_mod

        code = compile_mod.build(cfg.platform, jobs=jobs)
        if code != context.EXIT_OK:
            raise proc.StepError(
                "build failed; not deploying",
                fix="fix the shaders, or pass --no-build to deploy what is already packed",
            )

    if no_bundle:
        bundle_path = cfg.resolved("new_bundle")
        if not os.path.isfile(bundle_path):
            raise proc.StepError("no bundle at %s" % context.rel(bundle_path),
                                 fix="drop --no-bundle so it gets built")
    else:
        bundle_path = bundle.make(cfg)

    proc.step("deploy %s" % cfg.platform)
    if cfg.platform == "x360":
        return deploy_x360(cfg, bundle_path, launch)
    return deploy_bpr(cfg, bundle_path, launch)


def autotest(name=None, script=None, reboot=True):
    """Push Build/autotest to the devkit. Replaces deploy_autotest.ps1.

    The Breaker build reads d:\\autotest.txt at boot; line 1 names a script under
    d:\\TESTINGSCRIPTS. On the devkit d:\\ is the configured game path, so this
    lets Lua be iterated without rebuilding any shaders.
    """
    cfg = config.load(name).require_valid()
    if cfg.platform != "x360":
        raise proc.StepError("autotest is an Xbox 360 devkit feature",
                             code=context.EXIT_SKIP,
                             fix="this config targets %s" % cfg.platform)

    src_dir = context.AUTOTEST_DIR
    autotest_txt = os.path.join(src_dir, "autotest.txt")
    scripts_dir = os.path.join(src_dir, "TESTINGSCRIPTS")
    for path in (autotest_txt, scripts_dir):
        if not os.path.exists(path):
            raise proc.StepError("missing %s" % context.rel(path),
                                 fix="the autotest harness lives in Build/autotest")

    game_path, xex_path = _game_paths(cfg)
    proc.step("autotest deploy -> %s" % game_path)

    if script:
        # Point autotest.txt at a different entry script without editing the
        # tracked file: write the override into the scratch area and send that.
        staged = os.path.join(context.CACHE_DIR, "autotest.txt")
        proc.ensure_dir(os.path.dirname(staged))
        if not proc.DRY_RUN:
            with open(staged, "w", encoding="ascii", newline="\r\n") as handle:
                handle.write(script + "\n")
        autotest_txt = staged
        proc.info("entry script: %s" % script)

    _xbcp(cfg, autotest_txt, "%s\\autotest.txt" % game_path)

    xbmkdir = toolchain.xbmkdir()
    if xbmkdir:
        # Already-exists is not an error worth stopping for.
        proc.run([xbmkdir, _target_flag(cfg), "%s\\TESTINGSCRIPTS" % game_path],
                 check=False)

    count = 0
    for entry in sorted(os.listdir(scripts_dir)):
        path = os.path.join(scripts_dir, entry)
        if os.path.isfile(path):
            _xbcp(cfg, path, "%s\\TESTINGSCRIPTS\\%s" % (game_path, entry))
            count += 1
    proc.info("sent %d test script(s)" % count)

    if reboot:
        _reboot(cfg, xex_path, cfg.launch_args or ["-skipvideos"])
    else:
        proc.info("files deployed (no reboot requested)")
    return context.EXIT_OK

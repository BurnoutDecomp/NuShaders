"""Process execution, output, and the error type — everything user-visible funnels here.

Output is deliberately plain: fixed prefixes, no color, no progress bars, no box
drawing. Builds get piped into logs and grepped far more often than they get
watched, and a spinner is worthless in both cases.

    STEP: <what is starting>
      <detail>
      WARNING: <non-fatal>
      ERROR: <fatal>
      Fix: <what the reader should do about it>
    RESULT: <verb> OK|FAIL|SKIP (1.2s)

Every miss names its fix. A message that says only what went wrong makes the
reader go read the build system's source; StepError(..., fix=...) makes that
unnecessary, so the fix argument is expected rather than optional in spirit.
"""

import os
import shutil
import subprocess
import sys
import threading

from . import context

# --- global switches, set once by cli.main from the parsed args --------------

DRY_RUN = False
VERBOSE = False

_write_lock = threading.Lock()


def configure(dry_run=False, verbose=False):
    global DRY_RUN, VERBOSE
    DRY_RUN = dry_run
    VERBOSE = verbose


# --- errors ------------------------------------------------------------------


class StepError(Exception):
    """A failure that should end the verb. Caught once, in cli.main.

    code is the process exit code: EXIT_FAIL for a real problem, EXIT_SKIP when
    the environment simply cannot run this (no XDK installed, no reference
    corpus checked out) and saying "FAIL" would train people to ignore it.
    """

    def __init__(self, message, code=context.EXIT_FAIL, fix=None):
        Exception.__init__(self, message)
        self.message = message
        self.code = code
        self.fix = fix


# --- output ------------------------------------------------------------------


def _write(line):
    with _write_lock:
        sys.stdout.write(line + "\n")
        sys.stdout.flush()


def step(message):
    _write("STEP: " + message)


def info(message):
    _write(("  " + message) if message else "")


def detail(message):
    """Verbose-only detail (full command lines, cache keys)."""
    if VERBOSE:
        _write("  " + message)


def warn(message):
    _write("  WARNING: " + message)


def error(message):
    _write("  ERROR: " + message)


def fix(message):
    _write("  Fix: " + message)


def result(verb, code, seconds):
    word = {context.EXIT_OK: "OK", context.EXIT_SKIP: "SKIP"}.get(code, "FAIL")
    _write("RESULT: %s %s (%.1fs)" % (verb, word, seconds))


def quote(arg):
    """Render one argv element for a human to copy-paste, host-appropriately."""
    arg = str(arg)
    if context.IS_WINDOWS:
        return '"%s"' % arg if (" " in arg or "\t" in arg) else arg
    import shlex

    return shlex.quote(arg)


def render(argv):
    return " ".join(quote(a) for a in argv)


# --- running -----------------------------------------------------------------


def run(argv, cwd=None, env=None, check=True, capture=True, read_only=False, timeout=None):
    """Run a command. Returns CompletedProcess.

    read_only=True runs even under --dry-run. Queries (git rev-parse, fxc
    /dumpbin, a version probe) must keep executing during a dry run or the
    preview it prints is a lie about what the real run would do.
    """
    argv = [str(a) for a in argv]
    detail("$ " + render(argv))

    if DRY_RUN and not read_only:
        _write("  DRY-RUN: " + render(argv))
        return subprocess.CompletedProcess(argv, 0, "", "")

    merged = None
    if env:
        merged = dict(os.environ)
        for k, v in env.items():
            if v is None:
                merged.pop(k, None)
            else:
                merged[k] = v

    try:
        done = subprocess.run(
            argv,
            cwd=cwd,
            env=merged,
            capture_output=capture,
            text=True,
            timeout=timeout,
        )
    except FileNotFoundError:
        raise StepError(
            "command not found: %s" % argv[0],
            fix="check the tool resolves — run: nushaders.py doctor all",
        )
    except subprocess.TimeoutExpired:
        raise StepError("timed out after %ss: %s" % (timeout, render(argv)))

    if check and done.returncode != 0:
        raise StepError(
            "command failed (exit %d): %s\n%s"
            % (done.returncode, render(argv), (done.stderr or done.stdout or "").strip())
        )
    return done


def spawn(argv, cwd=None):
    """Start a detached process and return immediately (game launch, emulator)."""
    argv = [str(a) for a in argv]
    if DRY_RUN:
        _write("  DRY-RUN: spawn " + render(argv))
        return None
    detail("$ spawn " + render(argv))
    kwargs = {"cwd": cwd}
    if context.IS_WINDOWS:
        kwargs["creationflags"] = subprocess.CREATE_NEW_CONSOLE  # type: ignore[attr-defined]
    else:
        kwargs["start_new_session"] = True
    return subprocess.Popen(argv, **kwargs)


# --- filesystem (dry-run aware) ---------------------------------------------


def ensure_dir(path):
    if DRY_RUN:
        return path
    os.makedirs(path, exist_ok=True)
    return path


def copy_file(src, dst):
    if DRY_RUN:
        _write("  DRY-RUN: copy %s -> %s" % (context.rel(src), context.rel(dst)))
        return
    os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
    shutil.copy2(src, dst)


def mirror_tree(src, dst):
    """Copy every file in src over dst, creating dst. Does not delete extras."""
    if DRY_RUN:
        _write("  DRY-RUN: mirror %s -> %s" % (context.rel(src), context.rel(dst)))
        return 0
    os.makedirs(dst, exist_ok=True)
    count = 0
    for name in sorted(os.listdir(src)):
        s = os.path.join(src, name)
        d = os.path.join(dst, name)
        if os.path.isdir(s):
            count += mirror_tree(s, d)
        else:
            shutil.copy2(s, d)
            count += 1
    return count


def remove_tree(path):
    if DRY_RUN:
        _write("  DRY-RUN: rm -r " + context.rel(path))
        return
    shutil.rmtree(path, ignore_errors=True)

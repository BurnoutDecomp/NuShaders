"""Action model and the threaded scheduler.

An Action is one independent chain: compile a stage, and (in resource mode) pack
it. Chains never depend on each other — a shader's VS does not need its PS, and
one .fx does not need another — so there is no dependency graph to walk and no
barrier between "compile everything" and "pack everything". Each worker runs a
whole chain end to end, which means a slow shader's pack overlaps a fast
shader's compile instead of waiting for the last compile to finish.

Threads rather than processes: every action's real work is a subprocess call to
fxc/xsd/nushaders, so the GIL is released for the duration and the pool costs
nothing to set up.
"""

import os
import threading
import traceback
from collections import namedtuple
from concurrent.futures import ThreadPoolExecutor

from . import context, proc

# id      stable identity for the cache (survives reordering)
# label   what the user sees
# key     content hash; None means "always run"
# outputs files the action produces, checked for existence on cache hits
# run     callable(scratch_dir) -> None, raising StepError on failure
Action = namedtuple("Action", "id label key outputs run")


class Stats(object):
    def __init__(self):
        self.built = 0
        self.cached = 0
        self.failed = 0
        self.failures = []
        self._lock = threading.Lock()

    def hit(self):
        with self._lock:
            self.cached += 1

    def ok(self):
        with self._lock:
            self.built += 1

    def fail(self, label, message):
        with self._lock:
            self.failed += 1
            self.failures.append((label, message))


def default_jobs():
    return max(1, (os.cpu_count() or 4))


def run_actions(actions, cache, jobs=None, force=False, scratch=None):
    """Execute actions concurrently, honouring the cache. Returns Stats.

    Failures are collected rather than raised: one shader that will not compile
    should not hide the state of the other 300. The caller decides the exit code.
    """
    jobs = jobs or default_jobs()
    stats = Stats()

    if scratch:
        proc.ensure_dir(scratch)

    def execute(action):
        if not force and action.key and cache.is_fresh(action.id, action.key):
            stats.hit()
            proc.detail("cached  %s" % action.label)
            return
        try:
            action.run(scratch)
        except proc.StepError as err:
            cache.forget(action.id)
            stats.fail(action.label, err.message)
            proc.info("FAIL  %s" % action.label)
            for line in err.message.splitlines()[:3]:
                proc.info("      %s" % line.strip())
            if err.fix:
                proc.fix(err.fix)
            return
        except Exception:  # a bug in the build system, not in the shader
            cache.forget(action.id)
            stats.fail(action.label, traceback.format_exc(limit=3))
            proc.info("ERROR %s (build system fault)" % action.label)
            proc.detail(traceback.format_exc())
            return

        missing = [o for o in action.outputs if not os.path.exists(o)]
        if missing and not proc.DRY_RUN:
            cache.forget(action.id)
            stats.fail(action.label,
                       "claimed outputs not produced: %s"
                       % ", ".join(context.rel(m) for m in missing))
            proc.info("FAIL  %s (no output)" % action.label)
            return

        if action.key:
            cache.record(action.id, action.key, action.outputs)
        stats.ok()
        proc.info("OK    %s" % action.label)

    if jobs == 1:
        for action in actions:
            execute(action)
    else:
        with ThreadPoolExecutor(max_workers=jobs) as pool:
            list(pool.map(execute, actions))

    return stats


def report(stats, total):
    proc.info("")
    proc.info("cached %d  built %d  failed %d  (of %d)"
              % (stats.cached, stats.built, stats.failed, total))
    if stats.failures:
        proc.info("")
        proc.info("failures:")
        for label, _message in stats.failures[:20]:
            proc.info("  %s" % label)
        if len(stats.failures) > 20:
            proc.info("  ... and %d more" % (len(stats.failures) - 20))
    return context.EXIT_FAIL if stats.failed else context.EXIT_OK

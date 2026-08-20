@echo off
REM Arg-forwarding shim so `nushaders build bpr` works from any shell on Windows.
py -3 "%~dp0nushaders.py" %*

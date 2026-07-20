@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0QudJP-Harmony-2.4.2.ps1" -Operation Install
set "QUDJP_EXIT_CODE=%ERRORLEVEL%"
echo.
pause
exit /b %QUDJP_EXIT_CODE%

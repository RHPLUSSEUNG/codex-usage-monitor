@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
set "INSTALL_EXIT=%ERRORLEVEL%"
if not "%INSTALL_EXIT%"=="0" (
  echo.
  echo Installation failed with exit code %INSTALL_EXIT%. Review the error messages above.
  pause
  exit /b %INSTALL_EXIT%
)
exit /b 0

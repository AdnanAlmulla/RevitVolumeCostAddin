@echo off
rem Double-click to install the add-in for Revit on this computer (see Install-Addin.ps1). Close Revit first.
rem -ExecutionPolicy Bypass applies to this one run only; it changes no Windows setting.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Addin.ps1"
echo.
pause

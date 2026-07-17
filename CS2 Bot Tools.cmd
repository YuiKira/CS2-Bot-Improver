@echo off
if not exist "%~dp0CS2 Bot Tools.exe" (
  echo [ERROR] CS2 Bot Tools.exe was not found. Extract the complete release package.
  pause
  exit /b 1
)
start "" "%~dp0CS2 Bot Tools.exe"

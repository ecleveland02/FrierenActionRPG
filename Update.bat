@echo off
REM Double-click this to pull the latest changes.
REM Save your scenes in Unity first; Unity overwrites files underneath a pull when it next saves.

cd /d "%~dp0"

echo Pulling latest from the working branch...
echo.
git pull origin claude/frieren-rpg-foundation-svvrqc

echo.
echo You are now on:
git log --oneline -1

echo.
echo If that failed with "local changes would be overwritten", run Update-Force.bat instead.
echo Now click into Unity and wait for the spinner in the bottom-right to finish,
echo otherwise the editor is still running the previous build.
echo.
pause

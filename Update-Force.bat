@echo off
REM Use this only when a normal pull refuses because of local changes.
REM It commits whatever Unity changed on its own, then pulls. Nothing is discarded.

cd /d "%~dp0"

echo Committing local changes made by Unity, then pulling...
echo.
git add -A
git commit -m "Local Unity changes"
git pull origin claude/frieren-rpg-foundation-svvrqc

echo.
echo You are now on:
git log --oneline -1

echo.
pause

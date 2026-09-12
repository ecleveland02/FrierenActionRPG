@echo off
REM Force this checkout to match the server exactly.
REM Use when a normal pull is not landing. CLOSE UNITY FIRST - an open editor holds files open
REM and is the usual reason a pull silently fails to apply.

cd /d "%~dp0"

echo Saving anything you have changed locally onto a backup branch first...
git branch backup-%RANDOM% 2>nul
git stash push -u -m "Resync stash" 2>nul

echo.
echo Fetching...
git fetch origin

echo.
echo Resetting to the server's version of the branch...
git reset --hard origin/claude/frieren-rpg-foundation-svvrqc

echo.
echo ============================================================
echo You are now on:
git log --oneline -1
echo ============================================================
echo.
echo Now open Unity. If the change still is not there, in Unity use
echo   Assets  -  Reimport All
echo and wait for it to finish.
echo.
pause

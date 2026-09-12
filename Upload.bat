@echo off
setlocal EnableDelayedExpansion
REM ---------------------------------------------------------------------------
REM Double-click to send everything you have changed up to GitHub.
REM
REM The order matters: your work is COMMITTED BEFORE ANYTHING IS PULLED, so a
REM failed merge can never lose it. If this script stops halfway, your changes
REM are already safe in a local commit.
REM ---------------------------------------------------------------------------

cd /d "%~dp0"
set BRANCH=claude/frieren-rpg-foundation-svvrqc

echo ===========================================================
echo  Save your scenes in Unity first with Ctrl+S.
echo  Unity keeps edits in memory until you save, and this
echo  script can only upload what is on disk.
echo ===========================================================
echo.
pause

echo.
echo --- What has changed --------------------------------------
git add -A
git status --short
echo.

echo --- Large files, which must go through Git LFS ------------
git lfs status 2>nul
echo.
echo If a big .wav / .fbx / .png shows up as an ordinary file instead of an
echo LFS object, press Ctrl+C now and say so. Committing a large binary
echo outside LFS is permanent without rewriting history.
echo.

git diff --cached --quiet
if not errorlevel 1 (
    echo Nothing to send - no local changes.
    echo.
    goto :report
)

set "MSG="
set /p MSG=Describe the change (Enter for a default): 
if "!MSG!"=="" set "MSG=Local changes from Unity"

echo.
echo --- Committing --------------------------------------------
git commit -m "!MSG!"
if errorlevel 1 (
    echo.
    echo The commit failed. Nothing was sent. Paste this window to Claude.
    pause
    exit /b 1
)

:pull
echo.
echo --- Pulling anything new from the server -------------------
git pull --no-rebase origin %BRANCH%
if errorlevel 1 (
    echo.
    echo The pull failed - but your work is committed locally, so nothing is lost.
    echo Close Unity and run Resync.bat, or paste this window to Claude.
    pause
    exit /b 1
)

echo.
echo --- Pushing ------------------------------------------------
git push -u origin %BRANCH%
if errorlevel 1 (
    echo Push failed. Waiting 5 seconds and trying once more...
    timeout /t 5 /nobreak >nul
    git push -u origin %BRANCH%
)
if errorlevel 1 (
    echo Push failed twice. Waiting 15 seconds and trying a third time...
    timeout /t 15 /nobreak >nul
    git push -u origin %BRANCH%
)
if errorlevel 1 (
    echo.
    echo Still failing. A first LFS upload of several hundred MB can time out;
    echo simply running Upload.bat again resumes where it stopped.
    pause
    exit /b 1
)

:report
echo.
echo ===========================================================
echo  DONE. Send these two lines to Claude or GPT so they know
echo  exactly what they are looking at:
echo ===========================================================
echo.
git log --oneline -1
findstr /C:"public const string Current" Assets\Scripts\Core\Debugging\BuildStamp.cs
echo.
pause

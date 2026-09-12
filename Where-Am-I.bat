@echo off
REM ---------------------------------------------------------------------------
REM Double-click and paste the output to Claude or GPT.
REM
REM This exists because "it is not working" and "you are looking at a different
REM build than I am" are indistinguishable from the outside, and this project
REM has already lost hours to the second one.
REM ---------------------------------------------------------------------------

cd /d "%~dp0"

echo ===========================================================
echo  Branch and commit
echo ===========================================================
git rev-parse --abbrev-ref HEAD
git log --oneline -1
echo.

echo ===========================================================
echo  Build stamp in the working tree
echo ===========================================================
findstr /C:"public const string Current" Assets\Scripts\Core\Debugging\BuildStamp.cs
echo.

echo ===========================================================
echo  Unpushed local changes
echo ===========================================================
git status --short
echo (nothing listed above means everything is committed)
echo.

echo ===========================================================
echo  Am I behind the server?
echo ===========================================================
git fetch -q origin claude/frieren-rpg-foundation-svvrqc
git log --oneline HEAD..origin/claude/frieren-rpg-foundation-svvrqc
echo (nothing listed above means you are up to date)
echo.
pause

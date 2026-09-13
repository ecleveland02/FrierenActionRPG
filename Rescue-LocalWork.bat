@echo off
REM Run this when a pull has failed with "you have unmerged files".
REM
REM That means a merge started, hit a conflict, and was left half-finished. Git refuses
REM every further pull until it is resolved. This script does not resolve it. It does the
REM safe part: it abandons the broken merge, and pushes your own commits to a branch of
REM their own on the server so they cannot be lost and so they can be looked at.
REM
REM Nothing here deletes a commit. The worst case is an extra branch on GitHub.

cd /d "%~dp0"
setlocal

echo ============================================================
echo  WHERE YOU ARE
echo ============================================================
git log --oneline -1
echo.

git rev-parse --verify -q MERGE_HEAD >nul 2>&1
if errorlevel 1 (
  echo No merge is in progress. If a pull is still failing, the cause is
  echo something else - read its output and send it on.
  echo.
  pause
  exit /b 0
)

echo A merge is in progress and these files are conflicted:
echo ------------------------------------------------------------
git diff --name-only --diff-filter=U
echo ------------------------------------------------------------
echo.
echo Step 1 of 3: abandoning the half-finished merge.
git merge --abort
if errorlevel 1 (
  echo.
  echo Could not abort. Send the message above on rather than trying anything else.
  pause
  exit /b 1
)
echo Done. Your own commits are untouched.
echo.

echo Step 2 of 3: making a local safety branch.
for /f "delims=" %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmm"') do set STAMP=%%d
git branch rescue-%STAMP%
echo Your work is also reachable as: rescue-%STAMP%
echo.

echo Step 3 of 3: pushing your commits to their own branch on GitHub.
git push -u origin HEAD:refs/heads/rescue-%STAMP%
if errorlevel 1 (
  echo.
  echo The push failed, but nothing is lost: your work is still here and on the
  echo local branch rescue-%STAMP%. Send the message above on.
  pause
  exit /b 1
)

echo.
echo ============================================================
echo  SAFE
echo ============================================================
echo Your commits are now on GitHub as the branch: rescue-%STAMP%
echo.
echo Send that branch name on. The two histories can then be merged
echo properly, with the conflicts resolved and the checkers run, and
echo the result pushed back to the working branch for you to pull.
echo.
echo Do not run Update.bat again until that is done.
echo.
pause

@echo off
REM Double-click this to pull the latest changes.
REM Save your scenes in Unity first; Unity overwrites files underneath a pull when it next saves.

cd /d "%~dp0"
setlocal

echo ============================================================
echo  BEFORE
echo ============================================================
git log --oneline -1
for /f "delims=" %%s in ('findstr /c:"Current =" "Assets\Scripts\Core\Debugging\BuildStamp.cs"') do echo  stamp:%%s
echo.

REM Local edits are the usual reason a pull does nothing. Unity rewrites assets it has open,
REM and git will not overwrite a modified file - so the pull refuses and the editor keeps
REM running yesterday's prefab with no obvious sign anything went wrong.
git diff --quiet
if errorlevel 1 (
  echo ------------------------------------------------------------
  echo  YOU HAVE LOCAL CHANGES. These can block the pull:
  echo ------------------------------------------------------------
  git diff --name-only
  echo.
)

echo Pulling latest...
echo.
git pull origin claude/frieren-rpg-foundation-svvrqc
set PULLFAILED=%errorlevel%

echo.
if not "%PULLFAILED%"=="0" (
  echo ############################################################
  echo #                                                          #
  echo #   THE PULL FAILED. YOU ARE STILL ON THE OLD VERSION.     #
  echo #                                                          #
  echo ############################################################
  echo.
  echo Anything you saw above about "local changes would be overwritten"
  echo names the files in the way. Your options:
  echo.
  echo   1. Close Unity, then run Update-Force.bat to throw those local
  echo      changes away and take the server's version.
  echo   2. If the local changes are yours and worth keeping, run
  echo      Upload.bat first to commit them, then run this again.
  echo   3. If the message mentions a data quota or bandwidth, this is
  echo      Git LFS, not your changes. Run Update-NoLFS.bat instead: the
  echo      code and scenes will come down, only art and audio will not.
  echo.
  pause
  exit /b 1
)

echo ============================================================
echo  AFTER
echo ============================================================
git log --oneline -1
for /f "delims=" %%s in ('findstr /c:"Current =" "Assets\Scripts\Core\Debugging\BuildStamp.cs"') do echo  stamp:%%s
echo.
echo Pull succeeded. Now click into Unity and wait for the spinner in the
echo bottom-right to finish, otherwise the editor is still running the
echo previous build. The stamp above is what the in-game overlay should show.
echo.
pause

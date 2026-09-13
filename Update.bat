@echo off
REM ---------------------------------------------------------------------------
REM This script pulls a new version of ITSELF. cmd.exe reads a batch file from
REM disk as it runs, by byte offset, so rewriting it mid-run makes cmd resume in
REM the middle of the new file and execute fragments of lines. That produced a
REM real failure once: 'hing' is not recognized as an internal or external
REM command, which is the tail of the word "nothing".
REM
REM So it copies itself somewhere git cannot reach and hands over to that copy.
REM Flat gotos rather than nested if/else blocks, because a parenthesised block
REM is parsed in one go and %errorlevel% inside one is the value from before the
REM block ran. The path is passed as "%~dp0." because %~dp0 ends in a backslash
REM and a backslash before a closing quote escapes it.
REM ---------------------------------------------------------------------------
if /i "%~1"=="--relaunched" goto :run

copy /y "%~f0" "%TEMP%\%~n0_running.bat" >nul
if errorlevel 1 (
  echo Could not copy this script to the temp folder, so it is running in place.
  echo If it stops partway through with a nonsense command, that is why: the pull
  echo overwrote the file while it was still being read.
  echo.
  goto :run
)
call "%TEMP%\%~n0_running.bat" --relaunched "%~dp0."
exit /b

:run
if /i "%~1"=="--relaunched" cd /d "%~2"
if /i not "%~1"=="--relaunched" cd /d "%~dp0"

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

REM A half-finished merge blocks every pull, and the error git gives for it looks nothing
REM like the cause. Catch it here and name the fix instead.
git rev-parse --verify -q MERGE_HEAD >nul 2>&1
if not errorlevel 1 (
  echo ############################################################
  echo #   A MERGE IS ALREADY IN PROGRESS AND UNRESOLVED.         #
  echo ############################################################
  echo.
  echo These files are conflicted:
  git diff --name-only --diff-filter=U
  echo.
  echo No pull can run until that is dealt with. Run Rescue-LocalWork.bat,
  echo which abandons the broken merge and pushes your own commits somewhere
  echo safe so nothing is lost.
  echo.
  pause
  exit /b 1
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

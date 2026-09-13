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

echo Pulling text only, skipping LFS downloads...
echo.
set GIT_LFS_SKIP_SMUDGE=1
git pull origin claude/frieren-rpg-foundation-svvrqc
set PULLFAILED=%errorlevel%
set GIT_LFS_SKIP_SMUDGE=

echo.
if not "%PULLFAILED%"=="0" (
  echo ############################################################
  echo #   THE PULL STILL FAILED. This is not an LFS problem.     #
  echo ############################################################
  echo.
  echo Read the git output above. If it mentions authentication or
  echo permission, your GitHub credentials need renewing.
  echo.
  pause
  exit /b 1
)

REM If the pull did bring down an LFS-tracked file, it landed as a small text pointer
REM rather than as the real asset, and Unity will fail to import it. Say so rather than
REM leaving a broken model or a silent 130-byte texture to be discovered later.
echo Checking whether any art or audio changed in this update...
git diff --name-only HEAD@{1} HEAD > "%TEMP%\frieren_changed.txt" 2>nul
findstr /i /r /c:"\.wav$" /c:"\.mp3$" /c:"\.ogg$" /c:"\.png$" /c:"\.jpg$" /c:"\.fbx$" /c:"\.blend$" /c:"\.psd$" /c:"\.tga$" /c:"\.exr$" "%TEMP%\frieren_changed.txt" >nul 2>&1
if not errorlevel 1 (
  echo.
  echo ------------------------------------------------------------
  echo  WARNING: this update changed art or audio files, and those
  echo  came down as placeholders rather than real assets because
  echo  LFS was skipped. Those specific files will be broken in
  echo  Unity until LFS access is restored. The files are listed in
  echo  %TEMP%\frieren_changed.txt
  echo ------------------------------------------------------------
) else (
  echo None. Everything in this update is text, so nothing is missing.
)

echo.
echo ============================================================
echo  AFTER
echo ============================================================
git log --oneline -1
for /f "delims=" %%s in ('findstr /c:"Current =" "Assets\Scripts\Core\Debugging\BuildStamp.cs"') do echo  stamp:%%s
echo.
echo Click into Unity and let the import spinner finish. The stamp above
echo is what the in-game overlay should show.
echo.
pause

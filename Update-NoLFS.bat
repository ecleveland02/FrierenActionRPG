@echo off
REM Pull the latest code and scene changes WITHOUT downloading any Git LFS content.
REM
REM Use this when a normal Update.bat fails with a message about a data quota, bandwidth,
REM or "This repository is over its data quota". The art and audio in this project live in
REM Git LFS, which on a free GitHub account allows 1 GB of storage and 1 GB of downloads
REM per month. This repository holds about 2.5 GB, so LFS is blocked until that is dealt
REM with. Code, scenes, prefabs and documentation are ordinary text and are not affected.

cd /d "%~dp0"
setlocal

echo ============================================================
echo  BEFORE
echo ============================================================
git log --oneline -1
for /f "delims=" %%s in ('findstr /c:"Current =" "Assets\Scripts\Core\Debugging\BuildStamp.cs"') do echo  stamp:%%s
echo.

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

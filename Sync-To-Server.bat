@echo off
REM Get this clone back to exactly what is on the server, whatever state it is in:
REM a half-finished merge, conflict markers in files, local edits Unity wrote.
REM
REM It refuses to throw away work. Before resetting anything it checks for commits
REM that exist here and not on the server, and pushes them to a rescue branch first.
REM Uncommitted edits to tracked files ARE discarded, so it lists them and asks.

if /i "%~1"=="--relaunched" goto :run
copy /y "%~f0" "%TEMP%\%~n0_running.bat" >nul
if errorlevel 1 goto :run
call "%TEMP%\%~n0_running.bat" --relaunched "%~dp0."
exit /b

:run
if /i "%~1"=="--relaunched" cd /d "%~2"
if /i not "%~1"=="--relaunched" cd /d "%~dp0"
setlocal
set BRANCH=claude/frieren-rpg-foundation-svvrqc

echo ============================================================
echo  BEFORE
echo ============================================================
git log --oneline -1
echo.

echo Fetching...
git fetch origin
if errorlevel 1 (
  echo.
  echo Could not reach the server. Nothing has been changed.
  pause
  exit /b 1
)
echo.

REM --- protect anything of yours the server has never seen --------------------
REM Flat gotos, not nested if-blocks. A parenthesised block is parsed in one go, so a
REM variable set inside one and read inside the same one expands to nothing.
for /f "delims=" %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmm"') do set STAMP=%%d

git rev-list --count origin/%BRANCH%..HEAD > "%TEMP%\frieren_ahead.txt" 2>nul
set /p AHEAD=<"%TEMP%\frieren_ahead.txt"
if "%AHEAD%"=="0" goto :checkdirty
if "%AHEAD%"=="" goto :checkdirty

echo ------------------------------------------------------------
echo  You have %AHEAD% commit(s) the server does not:
echo ------------------------------------------------------------
git log --oneline origin/%BRANCH%..HEAD
echo.
echo Pushing them to a rescue branch before touching anything.
git push origin HEAD:refs/heads/rescue-%STAMP%
if errorlevel 1 goto :pushfailed
echo Saved on GitHub as: rescue-%STAMP%
echo.
goto :checkdirty

:pushfailed
echo.
echo That push failed, so nothing will be reset. Your work is untouched.
pause
exit /b 1

:checkdirty
git status --porcelain > "%TEMP%\frieren_dirty.txt"
for /f %%c in ('find /c /v "" ^< "%TEMP%\frieren_dirty.txt"') do set DIRTY=%%c
if "%DIRTY%"=="0" goto :doreset

echo ------------------------------------------------------------
echo  These uncommitted changes WILL BE DISCARDED:
echo ------------------------------------------------------------
type "%TEMP%\frieren_dirty.txt"
echo ------------------------------------------------------------
echo.
choice /c YN /m "Discard them and match the server"
if errorlevel 2 goto :keptthem
echo.
goto :doreset

:keptthem
echo.
echo Nothing changed. Run Upload.bat if you want to keep them.
pause
exit /b 0

:doreset
echo Resetting to the server...
git reset --hard origin/%BRANCH%
if errorlevel 1 (
  echo Reset failed. Send the message above on.
  pause
  exit /b 1
)

echo.
echo ============================================================
echo  AFTER
echo ============================================================
git log --oneline -1
for /f "delims=" %%s in ('findstr /c:"Current =" "Assets\Scripts\Core\Debugging\BuildStamp.cs"') do echo  stamp:%%s
echo.
echo This clone now matches the server exactly. Open Unity and let the
echo import spinner finish; the overlay should show the stamp above.
echo.
pause

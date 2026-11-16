@echo off
setlocal EnableExtensions
cd /d "%~dp0" || (echo [ERROR] cannot cd to script directory & exit /b 1)

where git >nul 2>nul || (echo [ERROR] git not found in PATH & exit /b 1)
git rev-parse --is-inside-work-tree >nul 2>nul || (echo [ERROR] not a git repository & exit /b 1)

set "BRANCH=main"
set "REMOTE=backup"

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set "CURRENT=%%b"
if /i not "%CURRENT%"=="%BRANCH%" (
    echo [ERROR] current branch is "%CURRENT%", expected "%BRANCH%".
    echo         Run: git switch %BRANCH%
    goto :fail
)

git remote get-url %REMOTE% >nul 2>nul || (
    echo [ERROR] remote "%REMOTE%" is not configured.
    echo         Run: git remote add %REMOTE% D:/OProj/LMLocal-backup.git
    goto :fail
)

set "MSG=%~1"
if not defined MSG set "MSG=local: %date% %time%"

echo === staging changes ===
git add -A || goto :fail

git diff --cached --quiet
if errorlevel 1 (
    echo === commit: %MSG% ===
    git commit -m "%MSG%" || goto :fail
) else (
    echo [INFO] nothing to commit
)

echo === push %BRANCH% to origin ===
git push origin %BRANCH% || goto :fail

echo === push %BRANCH% to %REMOTE% ===
git push %REMOTE% %BRANCH% || goto :fail

echo.
echo [OK] commit and push finished
if not defined NO_PAUSE pause
exit /b 0

:fail
echo.
echo [ERROR] command failed, see output above
if not defined NO_PAUSE pause
exit /b 1

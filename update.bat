@echo off
setlocal EnableExtensions
cd /d "%~dp0" || (echo [ERROR] cannot cd to script directory & exit /b 1)

where git >nul 2>nul || (echo [ERROR] git not found in PATH & exit /b 1)
git rev-parse --is-inside-work-tree >nul 2>nul || (echo [ERROR] not a git repository & exit /b 1)

set "BRANCH=main"
set "REMOTE=backup"

git remote get-url origin >nul 2>nul || (echo [ERROR] remote "origin" is not configured & goto :fail)
git remote get-url %REMOTE% >nul 2>nul || (echo [ERROR] remote "%REMOTE%" is not configured & goto :fail)

for /f "delims=" %%b in ('git rev-parse --abbrev-ref HEAD') do set "CURRENT=%%b"
if /i not "%CURRENT%"=="%BRANCH%" (
    echo [ERROR] current branch is "%CURRENT%", expected "%BRANCH%".
    echo         Run: git switch %BRANCH%
    goto :fail
)

set "DIRTY="
for /f "delims=" %%i in ('git status --porcelain') do set "DIRTY=1"
if defined DIRTY (
    echo [ERROR] working tree has uncommitted changes.
    echo         Run commit-push.bat first, then run this script again.
    goto :fail
)

echo === fetch origin ===
git fetch origin --prune || goto :fail

echo === fast-forward %BRANCH% ===
git merge --ff-only origin/%BRANCH% || goto :fail

echo === update backup ===
git push %REMOTE% %BRANCH% || goto :fail

echo.
echo [OK] synced with origin/%BRANCH%
git log -1 --format="head: %%h %%s"
if not defined NO_PAUSE pause
exit /b 0

:fail
echo.
echo [ERROR] command failed, see output above
if not defined NO_PAUSE pause
exit /b 1

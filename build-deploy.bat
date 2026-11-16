@echo off
setlocal EnableExtensions
cd /d "%~dp0" || (echo [ERROR] cannot cd to script directory & exit /b 1)

set "PROJ=%~dp0LMLocal\LMLocal.csproj"
set "OUT=%~dp0LMLocal\bin\x64\Release"
set "DLL=%OUT%\LMLocal.dll"
set "PDB=%OUT%\LMLocal.pdb"

if not exist "%PROJ%" (
    echo [ERROR] project not found: %PROJ%
    goto :fail
)

tasklist /FI "IMAGENAME eq devenv.exe" 2>nul | "%SystemRoot%\System32\findstr.exe" /I "devenv.exe" >nul
if not errorlevel 1 (
    echo [ERROR] Visual Studio is running. Close all devenv.exe windows first.
    goto :fail
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
set "MSBUILD="
if exist "%VSWHERE%" (
    for /f "usebackq delims=" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VSSDK -find MSBuild\**\Bin\MSBuild.exe`) do (
        if not defined MSBUILD set "MSBUILD=%%i"
    )
)
if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not defined MSBUILD if exist "%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" (
    set "MSBUILD=%ProgramFiles%\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
)
if not defined MSBUILD (
    echo [ERROR] MSBuild with the VS SDK workload was not found.
    echo         Install "Visual Studio extension development" workload.
    goto :fail
)

echo === build Release x64 ===
"%MSBUILD%" "%PROJ%" -restore -p:Configuration=Release -p:Platform=x64 -v:m -nologo
if errorlevel 1 goto :fail

if not exist "%DLL%" (
    echo [ERROR] build output missing: %DLL%
    goto :fail
)

echo === locate installed extensions ===
set "FOUND="
for /f "usebackq delims=" %%d in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -LiteralPath ($env:LOCALAPPDATA + '\Microsoft\VisualStudio') -Recurse -Depth 4 -Filter LMLocal.dll -File -ErrorAction SilentlyContinue | ForEach-Object { $_.DirectoryName }"`) do (
    set "FOUND=1"
    echo deploy: %%d
    copy /Y "%DLL%" "%%d\" >nul || goto :fail
    if exist "%PDB%" copy /Y "%PDB%" "%%d\" >nul
    if exist "%OUT%\Resources" xcopy "%OUT%\Resources" "%%d\Resources\" /E /I /Y /Q >nul || goto :fail
)

if not defined FOUND (
    echo [ERROR] no installed LMLocal.dll found under %%LOCALAPPDATA%%\Microsoft\VisualStudio
    goto :fail
)

if /i "%~1"=="updateconfig" (
    echo === update Visual Studio configuration ===
    if exist "%VSWHERE%" (
        for /f "usebackq delims=" %%p in (`"%VSWHERE%" -all -products * -property installationPath`) do (
            if exist "%%p\Common7\IDE\devenv.exe" (
                echo update: %%p
                start "" /wait "%%p\Common7\IDE\devenv.exe" /updateconfiguration
            )
        )
    )
)

echo.
echo [OK] build and deploy finished
echo VSIX: %OUT%\LMLocal.vsix
echo Restart Visual Studio to load the new build.
if not defined NO_PAUSE pause
exit /b 0

:fail
echo.
echo [ERROR] build or deploy failed, see output above
if not defined NO_PAUSE pause
exit /b 1

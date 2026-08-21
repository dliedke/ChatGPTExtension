@echo off
REM =====================================================================
REM  Deploy the extension into the Visual Studio experimental hive (Exp).
REM
REM    deploy-exp.cmd            Build Debug and deploy to the Exp hive.
REM    deploy-exp.cmd -release   Build Release instead of Debug.
REM    deploy-exp.cmd -run       The above, then start devenv /rootsuffix Exp
REM                              with this solution loaded.
REM
REM  F5 and Ctrl+F5 from Visual Studio already deploy on their own, for
REM  Debug and Release alike (the csproj sets DeployExtension=true for
REM  both x64 configurations). This script is the command-line equivalent,
REM  and the way to recover when the Exp hive loses the extension: it
REM  retries after rebuilding the hive's extension cache, which is what
REM  "VSSDK1031 ... could not be found" on a first deploy needs.
REM
REM  Deploy ONLY through this script or F5/build. Never also install the
REM  VSIX into the hive with VSIXInstaller /rootSuffix:Exp - two copies of
REM  the same VSIX identity make VS fail to load the extension. If that
REM  happens, or a build stops updating, close every VS window and reset
REM  the hive:
REM    "%VSROOT%\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=18.0 /RootSuffix=Exp
REM
REM  The extension is x64 only, so the build always runs with Platform=x64.
REM =====================================================================

setlocal EnableDelayedExpansion

REM --- Arguments (order-independent) -----------------------------------
set "CONFIG=Debug"
set "RUNAFTER="
:parseargs
if "%~1"=="" goto parsed
if /i "%~1"=="-release" set "CONFIG=Release"
if /i "%~1"=="-debug"   set "CONFIG=Debug"
if /i "%~1"=="-run"     set "RUNAFTER=1"
shift
goto parseargs
:parsed

REM --- Tool locations, VS 2026 first then VS 2022 -----------------------
set "VSROOT=C:\Program Files\Microsoft Visual Studio\18\Enterprise"
if not exist "%VSROOT%\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "VSROOT=C:\Program Files\Microsoft Visual Studio\2022\Enterprise"
)

set "MSBUILD=%VSROOT%\MSBuild\Current\Bin\amd64\MSBuild.exe"
set "DEVENV=%VSROOT%\Common7\IDE\devenv.exe"
set "PROJ=%~dp0ChatGPTExtension.csproj"
set "SLN=%~dp0ChatGPTExtension.sln"

if not exist "%MSBUILD%" (
    echo [ERROR] MSBuild.exe not found.
    exit /b 1
)
if not exist "%DEVENV%" (
    echo [ERROR] devenv.exe not found.
    exit /b 1
)

REM  Deploying over a running experimental instance leaves it on the old
REM  binaries and can fail the copy outright.
tasklist /fi "imagename eq devenv.exe" | find /i "devenv.exe" >nul
if not errorlevel 1 (
    echo [WARN] Visual Studio is running. Close any /rootsuffix Exp instance
    echo        before deploying, or it will keep the previous build loaded.
)

echo Building %CONFIG%^|x64 and deploying to the Exp hive...
"%MSBUILD%" "%PROJ%" -t:Build -p:Configuration=%CONFIG% -p:Platform=x64 -p:DeployExtension=true -p:VSSDKTargetPlatformRegRootSuffix=Exp -v:minimal -nologo
if errorlevel 1 (
    echo.
    echo [INFO] Deploy failed. Rebuilding the Exp extension cache and retrying...
    "%DEVENV%" /rootsuffix Exp /updateconfiguration
    "%MSBUILD%" "%PROJ%" -t:Build -p:Configuration=%CONFIG% -p:Platform=x64 -p:DeployExtension=true -p:VSSDKTargetPlatformRegRootSuffix=Exp -v:minimal -nologo
    if errorlevel 1 (
        echo.
        echo [ERROR] Deploy to the Exp hive failed.
        exit /b 1
    )
)

echo.
echo [OK] Deployed to %LocalAppData%\Microsoft\VisualStudio\^<version^>Exp\Extensions\Daniel Carvalho Liedke

if defined RUNAFTER (
    echo Starting the experimental instance...
    start "" "%DEVENV%" /rootsuffix Exp "%SLN%"
)

endlocal
exit /b 0

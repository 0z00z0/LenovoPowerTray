@echo off
REM Builds LenPower.dll (x64): MIDL-generates the RPC client stub from pwrmgr.idl,
REM then compiles it together with lenpower.c, linking rpcrt4.
REM Requires VS Build Tools with the "Desktop development with C++" workload
REM (provides cl.exe, link.exe, and midl.exe).
setlocal enabledelayedexpansion
cd /d "%~dp0"

REM Locate a VC++ toolset. Prefer vswhere (handles Build Tools + stable VS), but fall
REM back to scanning known roots so prerelease/Insiders editions (e.g. VS 2026 "18")
REM that the bundled vswhere doesn't enumerate are still found.
set "VCVARS="

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -prerelease -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath 2^>nul`) do (
        if exist "%%i\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=%%i\VC\Auxiliary\Build\vcvars64.bat"
    )
)

if not defined VCVARS (
    for %%R in (
        "%ProgramFiles%\Microsoft Visual Studio\18\Insiders"
        "%ProgramFiles%\Microsoft Visual Studio\18\Preview"
        "%ProgramFiles%\Microsoft Visual Studio\2022\Enterprise"
        "%ProgramFiles%\Microsoft Visual Studio\2022\Professional"
        "%ProgramFiles%\Microsoft Visual Studio\2022\Community"
        "%ProgramFiles(x86)%\Microsoft Visual Studio\2022\BuildTools"
    ) do (
        if not defined VCVARS if exist "%%~R\VC\Auxiliary\Build\vcvars64.bat" set "VCVARS=%%~R\VC\Auxiliary\Build\vcvars64.bat"
    )
)

if not defined VCVARS (
    echo ERROR: No VC++ x64 toolset found. Add "Desktop development with C++" in the VS installer.
    exit /b 1
)

echo Using toolset: %VCVARS%
call "%VCVARS%" >nul
if errorlevel 1 ( echo ERROR: vcvars64 failed. & exit /b 1 )

echo === MIDL: generating RPC client stub ===
midl /nologo /env x64 /header pwrmgr_h.h /cstub pwrmgr_c.c /sstub nul /iid nul /proxy nul /dlldata nul pwrmgr.idl
if errorlevel 1 ( echo ERROR: midl failed. & exit /b 1 )

echo === CL: compiling LenPower.dll ===
cl /nologo /W3 /O2 /MT /LD lenpower.c pwrmgr_c.c /link rpcrt4.lib /OUT:LenPower.dll
if errorlevel 1 ( echo ERROR: cl failed. & exit /b 1 )

echo === Done: %CD%\LenPower.dll ===
endlocal

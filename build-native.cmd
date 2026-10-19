@echo off
setlocal
cd /d "%~dp0"
for /f "tokens=*" %%I in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath') do set "VSROOT=%%I"
if not defined VSROOT exit /b 20
call "%VSROOT%\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b 21
if not exist artifacts\native mkdir artifacts\native
cd artifacts\native
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /W4 /DUNICODE /D_UNICODE /I"..\..\Native" "..\..\Aurora.Bootstrap\Bootstrap.cpp" "..\..\Native\Common.cpp" "..\..\Native\Sha256.cpp" /Fe:Aurora.Bootstrap.exe /link /SUBSYSTEM:WINDOWS /INCREMENTAL:NO bcrypt.lib user32.lib gdi32.lib shell32.lib
if errorlevel 1 exit /b 22
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /W4 /DUNICODE /D_UNICODE /I"..\..\Native" /c "..\..\Aurora.Setup.Native\Setup.cpp" /Fo:Setup.obj
if errorlevel 1 exit /b 23
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /DUNICODE /D_UNICODE "..\..\Aurora.Setup.Native\Entry.cpp" /Fe:"Aurora Launcher.exe" /link /SUBSYSTEM:WINDOWS /INCREMENTAL:NO user32.lib
if errorlevel 1 exit /b 24
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /DUNICODE /D_UNICODE /DAURORA_EDITOR_ENTRY "..\..\Aurora.Setup.Native\Entry.cpp" /Fe:"Aurora Editor.exe" /link /SUBSYSTEM:WINDOWS /INCREMENTAL:NO user32.lib
if errorlevel 1 exit /b 25
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /DUNICODE /D_UNICODE /DAURORA_STEAM_ENTRY "..\..\Aurora.Setup.Native\Entry.cpp" /Fe:Aurora.Steam.exe /link /SUBSYSTEM:WINDOWS /INCREMENTAL:NO user32.lib
if errorlevel 1 exit /b 28
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /LD /I"..\..\Native" "..\..\Modules\FovChanger\FovChanger.cpp" "..\..\Native\Sha256.cpp" /Fe:fov.dll /link /INCREMENTAL:NO bcrypt.lib user32.lib
if errorlevel 1 exit /b 26
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT "..\..\Modules\FovChanger\ResolverTests.cpp" /Fe:FovResolverTests.exe
if errorlevel 1 exit /b 27
FovResolverTests.exe
if errorlevel 1 exit /b 29
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /DUNICODE /D_UNICODE /I"..\..\Native" "..\..\Aurora.Tests\PbrRuntimeDefaultsTests.cpp" /Fe:PbrRuntimeDefaultsTests.exe
if errorlevel 1 exit /b 30
PbrRuntimeDefaultsTests.exe
exit /b %errorlevel%


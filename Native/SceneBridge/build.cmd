@echo off
setlocal EnableDelayedExpansion
set "SCENE_SRC=%~dp0"
set "SCENE_OUT=%~dp0..\..\artifacts\native"
if not exist "%SCENE_OUT%" mkdir "%SCENE_OUT%"
if not defined VCToolsInstallDir (
 for /f "tokens=*" %%I in ('"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath') do set "SCENE_VS=%%I"
 call "!SCENE_VS!\VC\Auxiliary\Build\vcvars64.bat"
)
if not defined VCToolsInstallDir exit /b 40
pushd "%SCENE_OUT%"
cl /nologo /TC /O2 /MT /D_CRT_SECURE_NO_WARNINGS /c "%SCENE_SRC%vendor\ufbx\ufbx.c" /Fo:scene-ufbx.obj
if errorlevel 1 exit /b 41
cl /nologo /TC /O2 /MT /D_CRT_SECURE_NO_WARNINGS /c "%SCENE_SRC%vendor\ufbx-write\ufbx_write.c" /Fo:scene-ufbx-write.obj
if errorlevel 1 exit /b 42
cl /nologo /std:c++20 /utf-8 /EHsc /O2 /MT /D_CRT_SECURE_NO_WARNINGS "%SCENE_SRC%SceneBridge.cpp" scene-ufbx.obj scene-ufbx-write.obj /Fe:Aurora.SceneBridge.exe /link /INCREMENTAL:NO
if errorlevel 1 exit /b 43
popd
exit /b 0

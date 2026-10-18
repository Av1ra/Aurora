param([Parameter(Mandatory=$true)][string]$Output,[string]$Payload='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$vs=& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if(-not $vs){throw 'Visual Studio C++ tools missing'}
$out=[IO.Path]::GetFullPath($Output);New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($out)) -Force | Out-Null
$resource=Join-Path ([IO.Path]::GetDirectoryName($out)) ('setup-'+[guid]::NewGuid().ToString('N')+'.rc')
$source='1 24 "'+((Join-Path $root 'Aurora.Setup.Native/setup.manifest').Replace('\','/'))+'"'+[Environment]::NewLine
if($Payload){$descriptor=[IO.Path]::ChangeExtension($resource,".payload");[IO.File]::WriteAllText($descriptor,((Get-FileHash -LiteralPath $Payload -Algorithm SHA256).Hash.ToLowerInvariant()+"`n"+[IO.Path]::GetFileName($Payload)),[Text.UTF8Encoding]::new($false));$source+='102 RCDATA "'+($descriptor.Replace('\','/'))+'"'+[Environment]::NewLine}
[IO.File]::WriteAllText($resource,$source,[Text.UTF8Encoding]::new($false))
$res=[IO.Path]::ChangeExtension($resource,'.res');$command=[IO.Path]::ChangeExtension($resource,'.cmd')
$lines=@"
@echo off
call "$vs\VC\Auxiliary\Build\vcvars64.bat"
if errorlevel 1 exit /b 1
rc /nologo /fo "$res" "$resource"
if errorlevel 1 exit /b 2
link /nologo /SUBSYSTEM:WINDOWS /INCREMENTAL:NO /MANIFEST:NO /OUT:"$out" "$root\artifacts\native\Setup.obj" "$root\artifacts\native\Sha256.obj" "$res" bcrypt.lib comctl32.lib user32.lib gdi32.lib shell32.lib ole32.lib uuid.lib advapi32.lib
exit /b %errorlevel%
"@
[IO.File]::WriteAllText($command,$lines,[Text.Encoding]::ASCII)
& $command
if($LASTEXITCODE -ne 0){throw 'Native setup build failed'}
Remove-Item -LiteralPath $resource,$res,$command
if($Payload){Remove-Item -LiteralPath $descriptor}

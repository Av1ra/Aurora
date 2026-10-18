param([string]$Version='0.1.2',[string]$Python='python',[string]$PythonDependencyDirectory='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$buildRoot="$root/artifacts/build-$([guid]::NewGuid().ToString('N'))"
function Run([string]$program,[string[]]$arguments){& $program @arguments;if($LASTEXITCODE -ne 0){throw "Build failed: $program"}}
New-Item -ItemType Directory -Path "$buildRoot/main","$buildRoot/addon/Editor/Assets","$buildRoot/addon/Editor/Backend" -Force | Out-Null
Run 'dotnet' @('publish',"$root/Aurora.Launcher/Aurora.Launcher.csproj",'-c','Release','-p:Platform=x64',"-p:Version=$Version",'-p:DebugType=None','-p:DebugSymbols=false','-o',"$buildRoot/main")
Run 'dotnet' @('build',"$root/Aurora.Editor.Module/Aurora.Editor.Module.csproj",'-c','Release',"-p:Version=$Version",'-p:DebugType=None','-p:DebugSymbols=false','-o',"$buildRoot/module")
Run "$root/build-native.cmd" @()
& "$root/build-setup.ps1" -Output "$buildRoot/main/Aurora.Installer.exe"
Copy-Item -LiteralPath "$root/artifacts/native/Aurora.Bootstrap.exe","$root/artifacts/native/Aurora.ModelEditor.exe" -Destination "$buildRoot/main"
Copy-Item -LiteralPath "$root/README.md","$root/MODULES.md","$root/VALIDATION.md","$root/Packaging/legacy-files.tsv" -Destination "$buildRoot/main"
Rename-Item -LiteralPath "$buildRoot/main/legacy-files.tsv" -NewName retired-files.tsv
$backendArgs=@('--noconfirm','--onedir','--console','--name','Aurora.ModelBackend','--distpath',"$buildRoot/backend",'--workpath',"$buildRoot/backend-work",'--specpath',"$root/artifacts","$root/Backend/mask_backend.py")
if($PythonDependencyDirectory){Run $Python (@('-c','import sys;sys.path.insert(0,sys.argv[1]);from PyInstaller.__main__ import run;run(sys.argv[2:])',$PythonDependencyDirectory)+$backendArgs)}else{Run $Python (@('-m','PyInstaller')+$backendArgs)}
Copy-Item -LiteralPath "$buildRoot/module/Aurora.Editor.Module.dll" -Destination "$buildRoot/addon/Editor"
Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/viewport.html" -Destination "$buildRoot/addon/Editor/Assets"
Copy-Item -Path "$buildRoot/backend/Aurora.ModelBackend/*" -Destination "$buildRoot/addon/Editor/Backend" -Recurse
$editorRoot="$buildRoot/addon/Editor"
$files=@(Get-ChildItem -LiteralPath $editorRoot -Recurse -File | ForEach-Object {@{Path=$_.FullName.Substring($editorRoot.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$manifest=@{Schema=1;Id='aurora.editor';Name='Мастер моделей';Category='tools';Version=$Version;MinimumLauncher=$Version;Config='editor.ini';Files=$files;Options=@()}
[IO.File]::WriteAllText("$buildRoot/editor-manifest.json",($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','manifest',"$buildRoot/editor-manifest.json","$editorRoot/acode.lnchr")
$zip="$root/artifacts/Aurora-$Version-win-x64.zip"
$editorZip="$root/artifacts/Aurora-Editor-$Version-win-x64.zip"
foreach($item in @(@("$buildRoot/main",$zip),@("$buildRoot/addon",$editorZip))){if(Test-Path -LiteralPath $item[1]){throw 'Archive the previous local ZIP first.'};[IO.Compression.ZipFile]::CreateFromDirectory($item[0],$item[1],[IO.Compression.CompressionLevel]::Optimal,$false)}
Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$zip,"$root/artifacts/aurora-update.json")
Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$editorZip,"$root/artifacts/aurora-editor.json")
& "$root/build-setup.ps1" -Output "$root/artifacts/Aurora-Setup.exe" -Payload $zip
$lines=@($zip,$editorZip,"$root/artifacts/Aurora-Setup.exe")|ForEach-Object{(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_)}
[IO.File]::WriteAllLines("$root/artifacts/SHA256SUMS.txt",$lines,[Text.UTF8Encoding]::new($false))
Write-Output "Package source: $buildRoot"

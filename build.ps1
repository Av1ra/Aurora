param([string]$Version='0.1.1',[string]$Python='python',[string]$PythonDependencyDirectory='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$buildRoot="$root/artifacts/build-$([guid]::NewGuid().ToString('N'))"
function Run([string]$program,[string[]]$arguments){& $program @arguments;if($LASTEXITCODE -ne 0){throw "Build failed: $program"}}
New-Item -ItemType Directory -Path "$root/artifacts" -Force | Out-Null
Run 'dotnet' @('publish',"$root/Aurora.Launcher/Aurora.Launcher.csproj",'-c','Release','-p:Platform=x64',"-p:Version=$Version",'-o',"$buildRoot/launcher")
Run 'dotnet' @('publish',"$root/Aurora.Editor/Aurora.Editor.csproj",'-c','Release','-p:Platform=x64',"-p:Version=$Version",'-o',"$buildRoot/editor")
Run "$root/build-native.cmd" @()
Run 'dotnet' @('publish',"$root/Aurora.Installer/Aurora.Installer.csproj",'-c','Release','-o',"$buildRoot/updater",'-p:EmbedPayload=false')
$backendArgs=@('--noconfirm','--onedir','--console','--name','Aurora.ModelBackend','--distpath',"$buildRoot/backend",'--workpath',"$buildRoot/backend-work",'--specpath',"$root/artifacts","$root/Backend/mask_backend.py")
if($PythonDependencyDirectory){Run $Python (@('-c','import sys;sys.path.insert(0,sys.argv[1]);from PyInstaller.__main__ import run;run(sys.argv[2:])',$PythonDependencyDirectory)+$backendArgs)}else{Run $Python (@('-m','PyInstaller')+$backendArgs)}
$stage="$root/artifacts/package-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path "$stage/Editor/Backend" -Force | Out-Null
Copy-Item -Path "$buildRoot/launcher/*" -Destination $stage -Recurse
Copy-Item -LiteralPath "$root/artifacts/native/Aurora.Bootstrap.exe" -Destination $stage
Copy-Item -LiteralPath "$buildRoot/updater/Aurora.Installer.exe" -Destination $stage
Copy-Item -Path "$buildRoot/editor/*" -Destination "$stage/Editor" -Recurse -Force
Copy-Item -Path "$buildRoot/backend/Aurora.ModelBackend/*" -Destination "$stage/Editor/Backend" -Recurse -Force
Copy-Item -LiteralPath "$root/README.md","$root/MODULES.md","$root/VALIDATION.md" -Destination $stage
$zip="$root/artifacts/Aurora-$Version-win-x64.zip"
if(Test-Path -LiteralPath $zip){throw 'Choose a new version or archive the previous local ZIP first.'}
[IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$zip,"$root/artifacts/aurora-update.json")
Copy-Item -LiteralPath $zip -Destination "$root/artifacts/Aurora-payload.zip" -Force
Run 'dotnet' @('publish',"$root/Aurora.Installer/Aurora.Installer.csproj",'-c','Release','-p:EmbedPayload=true','-o',"$buildRoot/setup")
Copy-Item -LiteralPath "$buildRoot/setup/Aurora.Installer.exe" -Destination "$root/artifacts/Aurora-Setup.exe" -Force
$files=@($zip,"$root/artifacts/Aurora-Setup.exe")
$lines=$files|ForEach-Object{(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_)}
[IO.File]::WriteAllLines("$root/artifacts/SHA256SUMS.txt",$lines,[Text.UTF8Encoding]::new($false))
Write-Output "Built $zip"

param([ValidateSet('0.2')][string]$Version='0.2',[string]$Python='python',[string]$PythonDependencyDirectory='', [switch]$Development,[string]$NativeDist='')
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
if(!$Development){
 $publisherSecret=if($env:AURORA_SIGNING_KEY){$env:AURORA_SIGNING_KEY}else{Join-Path $env:LOCALAPPDATA 'Aurora/publisher/publisher.pk8'}
 if(!(Test-Path -LiteralPath $publisherSecret)){throw 'Production publisher key missing. Restore the existing key; do not generate a replacement.'}
 $signingIdentity=[Security.Cryptography.ECDsa]::Create()
 $trustedIdentity=[Security.Cryptography.ECDsa]::Create()
 try{
  $signingIdentity.ImportPkcs8PrivateKey([IO.File]::ReadAllBytes($publisherSecret),[ref]0)
  $trustedIdentity.ImportFromPem([IO.File]::ReadAllText("$root/Aurora.Launcher/Assets/publisher.pem"))
  if([Convert]::ToBase64String($signingIdentity.ExportSubjectPublicKeyInfo()) -ne [Convert]::ToBase64String($trustedIdentity.ExportSubjectPublicKeyInfo())){throw 'Signing key does not match the pinned production publisher. Build aborted.'}
 }finally{$signingIdentity.Dispose();$trustedIdentity.Dispose()}
}
$buildRoot="$root/artifacts/build-$([guid]::NewGuid().ToString('N'))"
$stage="$buildRoot/package"
$app="$stage/app"
$output="$buildRoot/output"
function Run([string]$program,[string[]]$arguments){& $program @arguments;if($LASTEXITCODE -ne 0){throw "Build failed: $program"}}
New-Item -ItemType Directory -Path $app,$output,"$buildRoot/addon/Editor/Assets","$buildRoot/addon/Editor/Backend" -Force | Out-Null
$previousSigningKey=$env:AURORA_SIGNING_KEY
try {
 if($Development){
  Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','dev-key',"$buildRoot/development.pk8","$buildRoot/development.pem")
  $env:AURORA_SIGNING_KEY="$buildRoot/development.pk8"
 }
 Run 'dotnet' @('publish',"$root/Aurora.Launcher/Aurora.Launcher.csproj",'-c','Release','-p:Platform=x64','-p:DebugType=None','-p:DebugSymbols=false','-o',$app)
 Run 'dotnet' @('build',"$root/Aurora.Editor.Module/Aurora.Editor.Module.csproj",'-c','Release','-p:DebugType=None','-p:DebugSymbols=false','-o',"$buildRoot/module")
 if($Development){Copy-Item "$buildRoot/development.pem" "$app/Assets/publisher.pem" -Force}
 Run "$root/build-native.cmd" @()
 & "$root/Prepare-Fov.ps1" -Dll "$root/artifacts/native/fov.dll" -Output "$app/Modules/aurora.fov"
 if($NativeDist){& "$root/Prepare-Graphics.ps1" -NativeDist $NativeDist -Output "$app/Modules/aurora.graphics" -Version $Version -MinimumLauncher '0.2.0'}
 & "$root/build-setup.ps1" -Output "$app/Aurora.Installer.exe"
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora.Bootstrap.exe" -Destination $app
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora.Steam.exe" -Destination $app
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora Launcher.exe","$root/artifacts/native/Aurora Editor.exe" -Destination $stage
 New-Item -ItemType Directory "$app/docs" -Force | Out-Null
 Copy-Item -LiteralPath "$root/docs/RELEASE_DESCRIPTION.md" -Destination "$app/docs/Readme.md"
 Copy-Item -LiteralPath "$root/Packaging/legacy-files.tsv" -Destination "$app/retired-files.tsv"
 $revision=(& git -C $root rev-parse HEAD).Trim()
 $buildInfo=@{Version=$Version;Commit=$revision;ModifiedSources=[bool](& git -C $root status --porcelain);DevelopmentSigning=[bool]$Development}
 [IO.File]::WriteAllText("$app/build-info.json",($buildInfo|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
 $backendArgs=@('--noconfirm','--onedir','--console','--name','Aurora.ModelBackend','--distpath',"$buildRoot/backend",'--workpath',"$buildRoot/backend-work",'--specpath',"$root/artifacts","$root/Backend/mask_backend.py")
 if($PythonDependencyDirectory){Run $Python (@('-c','import sys;sys.path.insert(0,sys.argv[1]);from PyInstaller.__main__ import run;run(sys.argv[2:])',$PythonDependencyDirectory)+$backendArgs)}else{Run $Python (@('-m','PyInstaller')+$backendArgs)}
 Copy-Item -LiteralPath "$buildRoot/module/Aurora.Editor.Module.dll" -Destination "$buildRoot/addon/Editor"
 New-Item -ItemType Directory "$buildRoot/addon/Editor/Exchange" -Force | Out-Null
 Get-ChildItem -LiteralPath "$root/Exchange" -File | Where-Object {$_.Extension -in @('.py','.md')} | Copy-Item -Destination "$buildRoot/addon/Editor/Exchange"
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora.SceneBridge.exe" -Destination "$buildRoot/addon/Editor"
 New-Item -ItemType Directory "$buildRoot/addon/Editor/ThirdParty" -Force | Out-Null
 Copy-Item "$root/Native/SceneBridge/vendor/sources.json" "$buildRoot/addon/Editor/ThirdParty/sources.json"
 Copy-Item "$root/Native/SceneBridge/vendor/ufbx/LICENSE" "$buildRoot/addon/Editor/ThirdParty/ufbx-LICENSE.txt"
 Copy-Item "$root/Native/SceneBridge/vendor/ufbx-write/LICENSE" "$buildRoot/addon/Editor/ThirdParty/ufbx-write-LICENSE.txt"
 Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/viewport.html" -Destination "$buildRoot/addon/Editor/Assets"
 Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/gizmo.js" -Destination "$buildRoot/addon/Editor/Assets"
 Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/previews.js" -Destination "$buildRoot/addon/Editor/Assets"
 Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/selection.js" -Destination "$buildRoot/addon/Editor/Assets"
 Copy-Item -Path "$buildRoot/backend/Aurora.ModelBackend/*" -Destination "$buildRoot/addon/Editor/Backend" -Recurse
 Run $Python @("$root/Packaging/prepare-logo.py","$root/Packaging/Branding/LogoAE.png","$buildRoot/logo.png")
 $brandExecutables=@(Get-ChildItem -LiteralPath $stage -Recurse -Filter '*.exe' -File | Where-Object {$_.Name -like 'Aurora*'} | Select-Object -ExpandProperty FullName)
 $brandExecutables+=@(Get-ChildItem -LiteralPath "$buildRoot/addon/Editor" -Recurse -Filter 'Aurora*.exe' -File | Select-Object -ExpandProperty FullName)
 & "$root/Packaging/embed-logo.ps1" -Logo "$buildRoot/logo.png" -Executables $brandExecutables
 $editorRoot="$buildRoot/addon/Editor"
 $files=@(Get-ChildItem -LiteralPath $editorRoot -Recurse -File | ForEach-Object {@{Path=$_.FullName.Substring($editorRoot.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
 $manifest=@{Schema=1;Id='aurora.editor';Name='Model Editor';Category='tools';Version=$Version;MinimumLauncher='0.2.0';Config='editor.ini';Files=$files;Options=@()}
 [IO.File]::WriteAllText("$buildRoot/editor-manifest.json",($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
 Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','manifest',"$buildRoot/editor-manifest.json","$editorRoot/acode.lnchr")
 # One ZIP for Aurora Launcher and Aurora Editor, sharing the same WinUI/.NET runtime.
 Copy-Item -LiteralPath "$buildRoot/addon/Editor" -Destination $app -Recurse
 Run $Python @("$root/Packaging/test-editor-startup.py","$app/Aurora.Launcher.exe")
 $zip="$output/Aurora-$Version-win-x64.zip"
 [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
 Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$zip,"$output/aurora-update.json")
 & "$root/build-setup.ps1" -Output "$output/Aurora-Setup.exe" -Payload $zip
 & "$root/Packaging/embed-logo.ps1" -Logo "$buildRoot/logo.png" -Executables @("$output/Aurora-Setup.exe")
 $lines=Get-ChildItem -LiteralPath $output -File | Where-Object {$_.Name -ne "SHA256SUMS.txt"} | ForEach-Object{(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+"  "+$_.Name}
 [IO.File]::WriteAllLines("$output/SHA256SUMS.txt",$lines,[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText("$root/artifacts/latest-build.txt",$buildRoot,[Text.UTF8Encoding]::new($false))
 Write-Output "Development package: $output (not published)"
} finally {
 $env:AURORA_SIGNING_KEY=$previousSigningKey
 if($Development -and (Test-Path -LiteralPath "$buildRoot/development.pk8")){Remove-Item -LiteralPath "$buildRoot/development.pk8"}
}


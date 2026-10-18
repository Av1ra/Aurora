param([ValidateSet('0.2')][string]$Version='0.2',[string]$Python='python',[string]$PythonDependencyDirectory='', [switch]$Development)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
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
 & "$root/build-setup.ps1" -Output "$app/Aurora.Installer.exe"
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora.Bootstrap.exe" -Destination $app
 Copy-Item -LiteralPath "$root/artifacts/native/Aurora.exe","$root/artifacts/native/Aurora.Editor.exe" -Destination $stage
 New-Item -ItemType Directory "$app/docs" -Force | Out-Null
 Copy-Item -LiteralPath "$root/docs/RELEASE_DESCRIPTION.md" -Destination "$app/docs/Readme.md"
 Copy-Item -LiteralPath "$root/Packaging/legacy-files.tsv" -Destination "$app/retired-files.tsv"
 $backendArgs=@('--noconfirm','--onedir','--console','--name','Aurora.ModelBackend','--distpath',"$buildRoot/backend",'--workpath',"$buildRoot/backend-work",'--specpath',"$root/artifacts","$root/Backend/mask_backend.py")
 if($PythonDependencyDirectory){Run $Python (@('-c','import sys;sys.path.insert(0,sys.argv[1]);from PyInstaller.__main__ import run;run(sys.argv[2:])',$PythonDependencyDirectory)+$backendArgs)}else{Run $Python (@('-m','PyInstaller')+$backendArgs)}
 Copy-Item -LiteralPath "$buildRoot/module/Aurora.Editor.Module.dll" -Destination "$buildRoot/addon/Editor"
 Copy-Item -LiteralPath "$root/Aurora.Editor/Assets/viewport.html" -Destination "$buildRoot/addon/Editor/Assets"
 Copy-Item -Path "$buildRoot/backend/Aurora.ModelBackend/*" -Destination "$buildRoot/addon/Editor/Backend" -Recurse
 $editorRoot="$buildRoot/addon/Editor"
 $files=@(Get-ChildItem -LiteralPath $editorRoot -Recurse -File | ForEach-Object {@{Path=$_.FullName.Substring($editorRoot.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
 $manifest=@{Schema=1;Id='aurora.editor';Name='Model Editor';Category='tools';Version=$Version;MinimumLauncher='0.2.0';Config='editor.ini';Files=$files;Options=@()}
 [IO.File]::WriteAllText("$buildRoot/editor-manifest.json",($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
 Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','manifest',"$buildRoot/editor-manifest.json","$editorRoot/acode.lnchr")
 $zip="$output/Aurora-$Version-win-x64.zip"
 $editorZip="$output/Aurora-Editor-$Version-win-x64.zip"
 [IO.Compression.ZipFile]::CreateFromDirectory($stage,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
 [IO.Compression.ZipFile]::CreateFromDirectory("$buildRoot/addon",$editorZip,[IO.Compression.CompressionLevel]::Optimal,$false)
 Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$zip,"$output/aurora-update.json")
 Run 'dotnet' @('run','--project',"$root/Aurora.Publisher/Aurora.Publisher.csproj",'--','release',$Version,$editorZip,"$output/aurora-editor.json")
 & "$root/build-setup.ps1" -Output "$output/Aurora-Setup.exe" -Payload $zip
 $lines=@($zip,$editorZip,"$output/Aurora-Setup.exe","$output/aurora-update.json","$output/aurora-editor.json")|ForEach-Object{(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_)}
 [IO.File]::WriteAllLines("$output/SHA256SUMS.txt",$lines,[Text.UTF8Encoding]::new($false))
 [IO.File]::WriteAllText("$root/artifacts/latest-build.txt",$buildRoot,[Text.UTF8Encoding]::new($false))
 Write-Output "Development package: $output (not published)"
} finally {
 $env:AURORA_SIGNING_KEY=$previousSigningKey
 if($Development -and (Test-Path -LiteralPath "$buildRoot/development.pk8")){Remove-Item -LiteralPath "$buildRoot/development.pk8"}
}

param([Parameter(Mandatory=$true)][string]$NativeDist,[Parameter(Mandatory=$true)][string]$Output,[string]$Version='0.1.0',[string]$MinimumLauncher='0.1.0')
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $Output){throw 'Choose a new module output directory.'}
New-Item -ItemType Directory -Path "$Output/Payload/mods" -Force | Out-Null
foreach($file in @('pbr.dll','m.dll')){Copy-Item -LiteralPath (Join-Path $NativeDist $file) -Destination "$Output/Payload/mods/$file"}
& dotnet run --project "$PSScriptRoot/Aurora.Publisher/Aurora.Publisher.csproj" -- pixel "$Output/Payload/mods/glass_default.dds"
if($LASTEXITCODE -ne 0){throw 'Glass asset generation failed'}
$files=Get-ChildItem -LiteralPath "$Output/Payload/mods" -File | ForEach-Object {@{Path='Payload/mods/'+$_.Name;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}}
$options=@(
 @{Id='pbr';Label='Включить PBR';Section='features';Key='enabled';Beta=$false;Default=$true},
 @{Id='vehicles';Label='PBR автомобилей';Section='features';Key='vehicles';Requires=@('pbr');Beta=$false;Default=$true},
 @{Id='models';Label='PBR зданий и объектов';Section='features';Key='models';Requires=@('pbr');Beta=$true;Default=$true},
 @{Id='roads';Label='PBR дорог';Section='features';Key='roads';Requires=@('pbr');Beta=$true;Default=$true},
 @{Id='shadows';Label='Дополнительные тени';Section='features';Key='shadows';Requires=@('pbr');Beta=$true;Default=$true},
 @{Id='glass';Label='Стекло';Section='features';Key='glass';Requires=@('pbr','vehicles');Beta=$true;Default=$true})
$manifest=@{Schema=1;Id='aurora.graphics';Name='Графика';Category='graphics';Version=$Version;MinimumLauncher=$MinimumLauncher;Config='pbr-runtime.ini';Files=@($files);Options=$options}
[IO.File]::WriteAllText("$Output/manifest.json",($manifest|ConvertTo-Json -Depth 10),[Text.UTF8Encoding]::new($false))
& dotnet run --project "$PSScriptRoot/Aurora.Publisher/Aurora.Publisher.csproj" -- manifest "$Output/manifest.json" "$Output/acode.lnchr"
if($LASTEXITCODE -ne 0){throw 'Module signature failed'}
Write-Output 'Signed effects-only Workshop payload prepared. Upload through the game Workshop tools; this script does not publish it.'

param([Parameter(Mandatory=$true)][string]$Dll,[Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
New-Item -ItemType Directory "$Output/Payload/mods/library/aurora.fov" -Force | Out-Null
Copy-Item -LiteralPath $Dll -Destination "$Output/Payload/mods/library/aurora.fov/fov.dll"
$hash=(Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest=@{Schema=2;Id='aurora.fov';Name='FOV Changer';Category='camera';Version='0.2';MinimumLauncher='0.2.0';Config='fov.ini';Placement='settings';Files=@(@{Path='Payload/mods/library/aurora.fov/fov.dll';Sha256=$hash});Options=@(
 @{Id='enabled';Label='FOV Changer';Section='camera';Key='enabled';Default=$true},
 @{Id='degrees';Label='Угол обзора';Section='camera';Key='degrees';Kind='slider';Minimum=20;Maximum=100;DefaultValue=45})}
[IO.File]::WriteAllText("$Output/manifest.json",($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
& dotnet run --project "$PSScriptRoot/Aurora.Publisher" -- manifest "$Output/manifest.json" "$Output/acode.lnchr"
if($LASTEXITCODE -ne 0){throw 'Module signing failed'}


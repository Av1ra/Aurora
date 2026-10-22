param(
 [Parameter(Mandatory=$true)][string]$Destination,
 [switch]$WithGraphics,
 [switch]$DryRun
)
$ErrorActionPreference='Stop'
$snapshot=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../snapshot.json') -Raw | ConvertFrom-Json
$target=[IO.Path]::GetFullPath($Destination)
$projects=@(@{Name='Aurora-runtime';Repository=$snapshot.Aurora.Repository;Revision=$snapshot.Aurora.InstalledSource})
if($WithGraphics){$projects+=@{Name='GameData';Repository=$snapshot.Graphics.Repository;Revision=$snapshot.Graphics.Revision}}
foreach($project in $projects){
 $path=Join-Path $target $project.Name
 if(Test-Path -LiteralPath $path){throw "Destination already exists: $path. Choose a new directory; nothing is overwritten."}
 Write-Output ("Clone {0} -> {1}; checkout --detach {2}" -f $project.Repository,$path,$project.Revision)
}
if($DryRun){Write-Output 'Dry run only; no directories, clones or installs created.';return}
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach($project in $projects){
 $path=Join-Path $target $project.Name
 # Installed Aurora revision predates the handoff LFS archive; avoid unnecessary binary download.
 & git clone --filter=blob:none --no-checkout $project.Repository $path
 if($LASTEXITCODE -ne 0){throw "Clone failed: $($project.Name)"}
 & git -C $path checkout --detach $project.Revision
 if($LASTEXITCODE -ne 0){throw "Checkout failed: $($project.Name)"}
}
Write-Output 'Source workspaces restored. No game files installed. Keep the handoff docs beside these exact historical checkouts.'

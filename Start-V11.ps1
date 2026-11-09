param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$repo=$PSScriptRoot
$source=Join-Path $repo 'publish/AIWorkAssistant-1.1'
$target=Join-Path $repo 'publish/AIWorkAssistant'
$manifest=Join-Path $source 'release.json'
if(-not(Test-Path -LiteralPath $manifest)){throw 'V1.1 release missing. Run publish-win-x64.ps1 -OutputFolder publish/AIWorkAssistant-1.1.'}
$release=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $target | Out-Null
$current=Join-Path $target 'release.json'
$upToDate=(Test-Path -LiteralPath $current) -and ((Get-Content -LiteralPath $current -Raw|ConvertFrom-Json).version -eq $release.version) -and ((Get-FileHash -LiteralPath (Join-Path $source 'AIWork.Web.dll')).Hash -eq (Get-FileHash -LiteralPath (Join-Path $target 'AIWork.Web.dll')).Hash)
foreach($asset in @('AIWork.Infrastructure.dll','wwwroot/js/site.js','wwwroot/js/dashboard.js','wwwroot/js/ai-entry.js','wwwroot/js/chat.js','wwwroot/js/markdown.js','wwwroot/css/site.css')){
 if(-not(Test-Path -LiteralPath (Join-Path $target $asset)) -or ((Get-FileHash -LiteralPath (Join-Path $source $asset)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $target $asset)).Hash)){$upToDate=$false}
}
if(-not $upToDate){
 $data=Join-Path $target 'Data';New-Item -ItemType Directory -Force $data | Out-Null
 $lock=$null
 try {
  try{$lock=[IO.File]::Open((Join-Path $data 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Ban cu van dang chay. Dong dung cua so AIWork bang Ctrl+C va chay lai Start.bat de CAP NHAT. Khong mo ban cu thay cho ban moi.'}
  $backup=Join-Path $data ('Backups/pre-deploy-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory -Force $backup | Out-Null
  foreach($name in @('AIWork.db','AIWork.db-wal','AIWork.db-shm')){if(Test-Path -LiteralPath (Join-Path $data $name)){Copy-Item -LiteralPath (Join-Path $data $name) -Destination $backup}}
  $code=Join-Path $backup 'previous-code';New-Item -ItemType Directory -Force $code | Out-Null
  Get-ChildItem -LiteralPath $target -Force | Where-Object {$_.Name -notin @('Data','appsettings.json')} | Copy-Item -Destination $code -Recurse
  Get-ChildItem -LiteralPath $source -Force | Where-Object {$_.Name -notin @('Data','release.json','appsettings.json')} | Copy-Item -Destination $target -Recurse -Force
  if(-not(Test-Path -LiteralPath (Join-Path $target 'appsettings.json'))){Copy-Item -LiteralPath (Join-Path $source 'appsettings.json') -Destination $target}
  # Manifest last: a failed copy is retried rather than marked installed.
  Copy-Item -LiteralPath $manifest -Destination $target -Force
  Write-Output ('Installed AIWork '+$release.version+' in the original folder. Existing Data/configuration preserved. Backup: '+$backup)
 } finally {if($lock){$lock.Dispose()}}
}
if(-not $PrepareOnly){& (Join-Path $target 'Start.ps1')}

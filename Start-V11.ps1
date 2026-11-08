param([switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$repo=$PSScriptRoot
$old=Join-Path $repo 'publish/AIWorkAssistant'
$next=Join-Path $repo 'publish/AIWorkAssistant-1.1'
if(-not(Test-Path -LiteralPath (Join-Path $next 'AIWork.Web.exe'))){throw 'Publish V1.1 not found. Run publish-win-x64.ps1 -OutputFolder publish/AIWorkAssistant-1.1 first.'}
$oldData=Join-Path $old 'Data';$nextData=Join-Path $next 'Data'
$oldLock=$null
if((Test-Path -LiteralPath $nextData) -and -not(Test-Path -LiteralPath (Join-Path $nextData "AIWork.db"))){throw 'Target Data exists without database; inspect before transfer.'}
try {
 if(Test-Path -LiteralPath $oldData){
  try{$oldLock=[IO.File]::Open((Join-Path $oldData 'instance.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}catch{throw 'Ban cu dang chay. Hay dong cua so AIWork cu (Ctrl+C), sau do chay lai Start.bat. Khong dung/xoa du lieu tu dong.'}
  if(-not(Test-Path -LiteralPath $nextData)){
   if(Test-Path -LiteralPath ($nextData+".staging")){throw 'Previous transfer incomplete; inspect Data.staging before retrying.'};$destination=$nextData+".staging";New-Item -ItemType Directory -Path $destination | Out-Null
   Get-ChildItem -LiteralPath $oldData -Force | Where-Object {$_.Name -notin @('instance.lock','AIWork.db-wal','AIWork.db-shm')} | Copy-Item -Destination $destination -Recurse
   # The application lock ensures the old process is stopped; preserve any uncheckpointed WAL too.
   foreach($name in @('AIWork.db-wal','AIWork.db-shm')){if(Test-Path -LiteralPath (Join-Path $oldData $name)){Copy-Item -LiteralPath (Join-Path $oldData $name) -Destination $destination}}
   if(Test-Path -LiteralPath (Join-Path $old 'appsettings.json')){Copy-Item -LiteralPath (Join-Path $old 'appsettings.json') -Destination $next -Force}
   if(-not([IO.Path]::GetFullPath($destination).StartsWith([IO.Path]::GetFullPath($next)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase))){throw 'Transfer target outside publish root.'}
   Move-Item -LiteralPath $destination -Destination $nextData;Write-Output 'Copied existing Data and configuration. Original folder preserved. Startup will back up before migration.'
  }
 }
} finally {if($oldLock){$oldLock.Dispose()}}
if(-not $PrepareOnly){& (Join-Path $next 'Start.ps1')}

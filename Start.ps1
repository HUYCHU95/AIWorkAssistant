$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
$exe=Join-Path $PSScriptRoot 'AIWork.Web.exe'
if(-not(Test-Path -LiteralPath $exe)){throw 'Executable not found. Run publish-win-x64.bat first, then Start.bat inside publish\AIWorkAssistant.'}
$port=5088
$config=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'appsettings.json') -Raw | ConvertFrom-Json
if($config.LocalPort){$port=[int]$config.LocalPort}
if($port -lt 1024 -or $port -gt 65535){throw 'Invalid LocalPort: use 1024..65535.'}
$url="http://127.0.0.1:$port/"
try {
 $running=Invoke-WebRequest ($url+'Account/Login') -UseBasicParsing -TimeoutSec 2
 if($running.StatusCode -eq 200 -and $running.Content -match 'AIWork'){
  Write-Output ('AIWork is already running. Opening '+$url)
  Start-Process $url
  return
 }
} catch {}
if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue){throw "Port $port is occupied by another service. Change LocalPort in appsettings.json."}
# Browser helper stays hidden; web app keeps this console for graceful Ctrl+C.
$helper=Start-Job -ArgumentList $port -ScriptBlock {
 param($p)
 for($attempt=0;$attempt -lt 60;$attempt++){
  try{$r=Invoke-WebRequest "http://127.0.0.1:$p/Account/Login" -UseBasicParsing -TimeoutSec 1;if($r.StatusCode -eq 200){Start-Process "http://127.0.0.1:$p/";return}}catch{}
  Start-Sleep -Milliseconds 500
 }
}
try { & $exe; if($LASTEXITCODE -ne 0){throw 'Startup failed. Check the console and Data\Logs\startup.log.'} }
finally{Stop-Job $helper -ErrorAction SilentlyContinue;Remove-Job $helper -Force -ErrorAction SilentlyContinue}

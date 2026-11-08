param([switch]$ForceRestore)
$ErrorActionPreference='Stop'
Set-Location -LiteralPath $PSScriptRoot
function Run-Dotnet([string[]]$CommandArgs){ & dotnet @CommandArgs; if($LASTEXITCODE -ne 0){throw ('dotnet '+($CommandArgs -join ' ')+' failed with exit '+$LASTEXITCODE)} }
$dependencyFiles=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'tests') -Recurse -Filter '*.csproj' | Sort-Object FullName
$dependencyText=($dependencyFiles | ForEach-Object { [BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($_.FullName))) }) -join ';'
$sha=[Security.Cryptography.SHA256]::Create();try{$fingerprint=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($dependencyText))).Replace('-','')}finally{$sha.Dispose()}
$artifacts=Join-Path $PSScriptRoot 'artifacts';New-Item -ItemType Directory -Force $artifacts | Out-Null
$stamp=Join-Path $artifacts 'restore-win-x64.sha256'
$needRestore=$ForceRestore -or -not(Test-Path -LiteralPath $stamp) -or -not(Test-Path -LiteralPath 'src/AIWork.Web/obj/project.assets.json') -or -not(Test-Path -LiteralPath 'tests/AIWork.Tests/obj/project.assets.json')
if(-not $needRestore){$needRestore=((Get-Content -LiteralPath $stamp -Raw).Trim() -ne $fingerprint)}
if($needRestore){Run-Dotnet @('restore','AIWorkAssistant.sln','-r','win-x64');Set-Content -LiteralPath $stamp -Value $fingerprint}
Run-Dotnet @('build','AIWorkAssistant.sln','-c','Release','--no-restore')
Run-Dotnet @('test','AIWorkAssistant.sln','-c','Release','--no-build','--no-restore','--logger','trx;LogFileName=v1-release.trx','--results-directory','artifacts/TestResults')
$output=Join-Path $PSScriptRoot 'publish/AIWorkAssistant';New-Item -ItemType Directory -Force $output | Out-Null
$config=Join-Path $output 'appsettings.json';$previousConfig=$null
if(Test-Path -LiteralPath $config){$previousConfig=[IO.File]::ReadAllBytes($config)}
try{Run-Dotnet @('publish','src/AIWork.Web/AIWork.Web.csproj','-c','Release','-r','win-x64','--self-contained','true','--no-restore','-o',$output)}finally{if($null -ne $previousConfig){[IO.File]::WriteAllBytes($config,$previousConfig)}}
foreach($name in @('Start.bat','Start.ps1','README.md','IMPLEMENTATION_REPORT_V1.md','TEST_REPORT_V1.md','V1_RELEASE_NOTES.md')){if(Test-Path -LiteralPath $name){Copy-Item -LiteralPath $name -Destination (Join-Path $output $name) -Force}}
$sample=Join-Path $output 'sample-data';New-Item -ItemType Directory -Force $sample | Out-Null;Copy-Item -LiteralPath 'sample-data/9_projects_nov_2026.xlsx' -Destination $sample -Force
Write-Output ('Ready: '+(Join-Path $output 'Start.bat')+'. Existing Data and appsettings preserved.')


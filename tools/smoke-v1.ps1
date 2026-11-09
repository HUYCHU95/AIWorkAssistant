param([switch]$KeepRunning,[string]$PublishFolder="publish/AIWorkAssistant")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Net.Http
$repo=Split-Path -Parent $PSScriptRoot
$fixture=Join-Path $repo ('artifacts/V1Smoke-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $fixture | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repo $PublishFolder) | Where-Object {$_.Name -ne 'Data'} | Copy-Item -Destination $fixture -Recurse
$configPath=Join-Path $fixture 'appsettings.json';$config=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json;$listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop();$config.LocalPort=$port;$config | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath
$base="http://127.0.0.1:$port"
$handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.UseProxy=$false;$handler.CookieContainer=[Net.CookieContainer]::new();$client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(30)
function Start-Fixture {Start-Process -FilePath (Join-Path $fixture 'AIWork.Web.exe') -WorkingDirectory $repo -WindowStyle Hidden -RedirectStandardOutput (Join-Path $fixture ('stdout-'+[guid]::NewGuid().ToString('N')+'.log')) -RedirectStandardError (Join-Path $fixture ('stderr-'+[guid]::NewGuid().ToString('N')+'.log')) -PassThru}
function Wait-Ready {for($attempt=0;$attempt -lt 100;$attempt++){if($process.HasExited){throw 'Fixture process exited; inspect stderr'};try{$r=$client.GetAsync($base+'/Account/Login').GetAwaiter().GetResult();if([int]$r.StatusCode -in @(200,302)){return}}catch{if($attempt -eq 0){Write-Output ('Readiness first error: '+$_.Exception.Message)}};Start-Sleep -Milliseconds 200};throw 'Fixture did not become ready'}
function Get-Page([string]$path){$r=$client.GetAsync($base+$path).GetAwaiter().GetResult();if([int]$r.StatusCode -ne 200){throw ('GET '+$path+' '+$r.StatusCode)};return $r.Content.ReadAsStringAsync().GetAwaiter().GetResult()}
function Field([string]$html,[string]$name){return [Net.WebUtility]::HtmlDecode([regex]::Match($html,('name="'+[regex]::Escape($name)+'"[^>]*value="([^"]*)"')).Groups[1].Value)}
function Post-Form([string]$path,[hashtable]$fields,[string]$tokenPage){$html=Get-Page $tokenPage;$fields['__RequestVerificationToken']=Field $html '__RequestVerificationToken';$dict=[Collections.Generic.Dictionary[string,string]]::new();foreach($key in $fields.Keys){$dict.Add($key,[string]$fields[$key])};$content=[Net.Http.FormUrlEncodedContent]::new($dict);return $client.PostAsync($base+$path,$content).GetAwaiter().GetResult()}
function Expect-Redirect($response){if([int]$response.StatusCode -ne 302){throw ('Expected redirect, got '+$response.StatusCode+' '+$response.Content.ReadAsStringAsync().GetAwaiter().GetResult())};return $response.Headers.Location.ToString()}
function Add-Text($multi,[string]$name,[string]$value){$multi.Add([Net.Http.StringContent]::new($value),$name)}
$process=Start-Fixture
try
{
    Wait-Ready
    $password='Smoke-only-'+[guid]::NewGuid().ToString('N');$user='v1-smoke'
    $null=Expect-Redirect (Post-Form '/Account/Setup' @{UserName=$user;Password=$password} '/Account/Setup')
    foreach($path in @('/','/Projects','/Tasks','/Tracking','/Sources','/Inbox','/Memory','/Chat','/Actions','/Planner','/Maintenance','/Profile','/Integrations')){$null=Get-Page $path}
    $importPage=Get-Page '/Import';$multi=[Net.Http.MultipartFormDataContent]::new();Add-Text $multi '__RequestVerificationToken' (Field $importPage '__RequestVerificationToken');$multi.Add([Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes((Join-Path $repo 'sample-data/9_projects_nov_2026.xlsx'))),'file','sample.xlsx');$preview=$client.PostAsync($base+'/Import/Preview',$multi).GetAwaiter().GetResult();if([int]$preview.StatusCode -ne 200){throw 'Import preview failed'};$previewHtml=$preview.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $dict=[Collections.Generic.Dictionary[string,string]]::new();$dict.Add('__RequestVerificationToken',(Field $previewHtml '__RequestVerificationToken'));$dict.Add('token',(Field $previewHtml 'token'));$null=Expect-Redirect ($client.PostAsync($base+'/Import/Confirm',[Net.Http.FormUrlEncodedContent]::new($dict)).GetAwaiter().GetResult())
    $projectCount=[regex]::Matches((Get-Page '/Projects'),'href="/Projects/Details/').Count;$taskCount=[regex]::Matches((Get-Page '/Tasks'),'href="/Tasks/Details/').Count;if($projectCount -ne 9 -or $taskCount -ne 60){throw 'Workbook counts do not match'}
    $page=Get-Page '/Inbox/Capture';$multi=[Net.Http.MultipartFormDataContent]::new();Add-Text $multi '__RequestVerificationToken' (Field $page '__RequestVerificationToken');Add-Text $multi 'Title' 'Smoke meeting';Add-Text $multi 'Kind' '2';Add-Text $multi 'Body' "1. Smoke action one`n2. Smoke action two`n3. Smoke action three";Add-Text $multi 'CaptureKey' ([guid]::NewGuid().ToString());Add-Text $multi 'IsConfidential' 'true';$multi.Add([Net.Http.ByteArrayContent]::new([Text.Encoding]::UTF8.GetBytes('portable attachment evidence')),'Files','evidence.txt');$inboxPath=Expect-Redirect ($client.PostAsync($base+'/Inbox/Capture',$multi).GetAwaiter().GetResult());$inboxId=$inboxPath.Split('/')[-1]
    $null=Expect-Redirect (Post-Form ('/Inbox/Extract/'+$inboxId) @{} $inboxPath)
    $actionsHtml=Get-Page '/Actions';$ids=[regex]::Matches($actionsHtml,'href="/Actions/Details/([a-fA-F0-9-]{36})"') | ForEach-Object {$_.Groups[1].Value};if(@($ids).Count -ne 3){throw 'Expected three action proposals'}
    $taskId=$null
    foreach($id in $ids){$actionPath='/Actions/Details/'+$id;$html=Get-Page $actionPath;$null=Expect-Redirect (Post-Form ('/Actions/Approve/'+$id) @{version=(Field $html 'version')} $actionPath);$html=Get-Page $actionPath;$taskId=[regex]::Match($html,'href="/Tasks/Details/([a-fA-F0-9-]{36})"').Groups[1].Value}
    $otherTask=[regex]::Matches((Get-Page '/Tasks'),'href="/Tasks/Details/([a-fA-F0-9-]{36})"') | ForEach-Object {$_.Groups[1].Value} | Where-Object {$_ -ne $taskId} | Select-Object -First 1
    $null=Expect-Redirect (Post-Form '/Dependencies/Add' @{taskId=$taskId;dependsOn=$otherTask} '/Dependencies')
    $dashboard=Get-Page '/Dashboard/Data' | ConvertFrom-Json;if(@($dashboard.dependencies).Count -lt 1){throw 'Dashboard dependency relationship missing'}
    if((Get-Page '/System/Version' | ConvertFrom-Json).version -ne '1.1.1'){throw 'Wrong release version'}
    $chatPath=Expect-Redirect (Post-Form '/Chat/Ask' @{text='Synthetic offline test, no company transmission'} '/');if((Get-Page $chatPath) -notmatch 'message-error'){throw 'Offline chat must be honest unavailable'}
    $backup=Post-Form '/Settings/Backup' @{} '/Settings';if([int]$backup.StatusCode -ne 200){throw 'Backup failed'};$backupPath=Join-Path $fixture 'smoke-backup.zip';[IO.File]::WriteAllBytes($backupPath,$backup.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult())
    $editPath='/Tasks/Edit/'+$taskId;$editHtml=Get-Page $editPath;$null=Expect-Redirect (Post-Form $editPath @{Id=$taskId;Title='Changed after backup';Version=(Field $editHtml 'Version');Status='0';Priority='1'} $editPath)
    $page=Get-Page '/Maintenance';$multi=[Net.Http.MultipartFormDataContent]::new();Add-Text $multi '__RequestVerificationToken' (Field $page '__RequestVerificationToken');$multi.Add([Net.Http.ByteArrayContent]::new([IO.File]::ReadAllBytes($backupPath)),'file','smoke-backup.zip');$restore=$client.PostAsync($base+'/Maintenance/Preview',$multi).GetAwaiter().GetResult();if([int]$restore.StatusCode -ne 200){throw 'Restore validation preview failed'};$restoreHtml=$restore.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $dict=[Collections.Generic.Dictionary[string,string]]::new();$dict.Add('__RequestVerificationToken',(Field $restoreHtml '__RequestVerificationToken'));$dict.Add('token',(Field $restoreHtml 'token'));$dict.Add('confirmation','PHUC HOI');$queued=$client.PostAsync($base+'/Maintenance/Confirm',[Net.Http.FormUrlEncodedContent]::new($dict)).GetAwaiter().GetResult();if([int]$queued.StatusCode -ne 200){throw 'Restore confirm failed'}
    if(-not $process.WaitForExit(20000)){throw 'App did not stop cleanly for offline restore'}
    $process=Start-Fixture;Wait-Ready
    $null=Expect-Redirect (Post-Form '/Account/Login' @{UserName=$user;Password=$password} '/Account/Login')
    $tasks=Get-Page '/Tasks';if($tasks.Contains('Changed after backup') -or [regex]::Matches($tasks,'href="/Tasks/Details/').Count -ne 63){throw 'Restored data mismatch'}
    if(-not(Get-ChildItem -LiteralPath (Join-Path $fixture 'Data/Backups') -Directory -Filter 'pre-restore-files-*')){throw 'Pre-restore originals missing'}
    @{UserName=$user;Password=$password;Fixture=$fixture;Port=$port;ProcessId=$process.Id} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $repo 'artifacts/v1-smoke-session.json')
    Write-Output "PASS: final EXE, auth, 13 screens, import 9/60, Inbox+attachment+3 reviewed tasks, honest offline chat, backup, queue stop, offline restore, old password preserved, 63 tasks restored. Fixture: $fixture"
}
finally{if(-not $KeepRunning -and $process -and -not $process.HasExited){$process.CloseMainWindow() | Out-Null;Start-Sleep -Milliseconds 200;if(-not $process.HasExited){Stop-Process -Id $process.Id}};$client.Dispose();$handler.Dispose()}

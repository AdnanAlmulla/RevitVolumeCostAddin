# Trial run of the update path: a <From> install laid out as Install-Addin.ps1 leaves it, in scratch folders; the real
# <From> uploader, with folder overrides, against a stand-in site (stub_site.py) offering <To>. Never touches the real
# folders or the registry. Needs both packages built (Build-Package.ps1, with -Version for the test one) and Python.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Updates\Test-UpdateEndToEnd.ps1 -RepositoryFolder . -From 0.7.2 -To 0.7.3
#
# Help's "Check for updates" (case 5) is checked only when <From> has it (0.7.2 on).
param([string]$RepositoryFolder, [string]$From = '0.7.0', [string]$To = '0.7.1', [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$scratchpad = $PSScriptRoot
$fromPattern = [regex]::Escape($From)
$toPattern = [regex]::Escape($To)
$failures = New-Object System.Collections.Generic.List[string]
function Check([bool]$condition, [string]$what) { if ($condition) { Write-Host "PASS $what" } else { Write-Host "FAIL $what"; $failures.Add($what) } }

function New-Install([string]$root) {
    $roaming = Join-Path $root 'Roaming'; $local = Join-Path $root 'Local'
    $package = Join-Path $RepositoryFolder "artifacts\Telltale-$From"
    # As Install-Addin.ps1 does, for Revit 2026 only (this PC's 2024 is left out on purpose: it must not be updated).
    $year = Join-Path $roaming 'Autodesk\Revit\Addins\2026'
    New-Item -ItemType Directory -Force -Path $year | Out-Null
    Copy-Item -Recurse (Join-Path $package '2026\Telltale') (Join-Path $year 'Telltale')
    Copy-Item (Join-Path $package '2026\Telltale.addin') $year
    New-Item -ItemType Directory -Force -Path (Join-Path $local 'Telltale\Uploader') | Out-Null
    Copy-Item -Recurse (Join-Path $package 'Uploader') (Join-Path $local "Telltale\Uploader\$From")
    New-Item -ItemType Directory -Force -Path (Join-Path $roaming 'Telltale') | Out-Null
    Set-Content (Join-Path $roaming 'Telltale\uploader.json') '{ "siteAddress": "http://127.0.0.1:5099", "sitePassword": "trial-password" }'
    return @{ Roaming = $roaming; Local = $local; Uploader = (Join-Path $local "Telltale\Uploader\$From\Telltale.Uploader.exe") }
}

function Invoke-UploaderOnce($install) {
    $process = Start-Process -FilePath $install.Uploader -ArgumentList '--once', '--local-app-data', "`"$($install.Local)`"", '--roaming-app-data', "`"$($install.Roaming)`"" -PassThru -Wait
    return $process.ExitCode
}

$scratch = Join-Path $env:TEMP ('telltale-update-' + [guid]::NewGuid().ToString('N'))
$zipTo = Join-Path $RepositoryFolder "artifacts\Telltale-$To.zip"

# Case 1: a good release.
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zipTo`"", '5099' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'good')
    Invoke-UploaderOnce $install | Out-Null
    $year = Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026'
    Check ((Get-Content -Raw (Join-Path $year 'Telltale.addin')) -match "Telltale\\$toPattern\\Telltale\.dll") "good: the 2026 manifest points at $To"
    Check (Test-Path (Join-Path $year "Telltale\$To\Telltale.dll")) "good: $To installed beside"
    Check (Test-Path (Join-Path $year "Telltale\$From\Telltale.dll")) "good: $From kept"
    Check (Test-Path (Join-Path $year "Telltale\$To\Telltale instructions.txt")) 'good: the instruction sheet came with it'
    Check (-not (Test-Path (Join-Path $install.Roaming 'Autodesk\Revit\Addins\2024'))) 'good: Revit 2024, not installed, left alone'
    Check (Test-Path (Join-Path $install.Local "Telltale\Uploader\$To\Telltale.Uploader.exe")) "good: uploader $To installed beside"
    Check (Test-Path (Join-Path $install.Local "Telltale\Uploader\$From\Telltale.Uploader.exe")) "good: uploader $From kept"
    $standardPath = Join-Path $install.Local 'Telltale\parameter-standard.json'
    Check ((Test-Path $standardPath) -and ((Get-Content -Raw $standardPath) -match 'a1b2c3d4e5f6')) 'good: the website standard saved'
    $log = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check ($log -match 'not started \(single pass\)') 'good: a single pass does not start the new uploader'
    Check (@(Get-ChildItem -Recurse $install.Roaming -Filter '*.partial').Count -eq 0) 'good: no half-written file left'
    # A second run finds nothing more to do.
    Invoke-UploaderOnce $install | Out-Null
    $log2 = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check (([regex]::Matches($log2, 'installed for Revit 2026')).Count -eq 1) 'good: a second run installs nothing again'
    Write-Host '--- uploader.log'; Write-Host $log2
}
finally { Stop-Process -Id $site.Id -Force }

# Case 2: a download that does not match its checksum installs nothing.
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zipTo`"", '5099', 'corrupt' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'corrupt')
    Invoke-UploaderOnce $install | Out-Null
    $year = Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026'
    Check ((Get-Content -Raw (Join-Path $year 'Telltale.addin')) -match "Telltale\\$fromPattern\\Telltale\.dll") "corrupt: the manifest still points at $From"
    Check (-not (Test-Path (Join-Path $year "Telltale\$To"))) "corrupt: nothing of $To installed"
    Check (-not (Test-Path (Join-Path $install.Local "Telltale\Updates\Telltale-$To.zip"))) 'corrupt: the bad download not kept'
    $log = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check ($log -match 'does not match its size and checksum') 'corrupt: logged'
}
finally { Stop-Process -Id $site.Id -Force }

# Case 4: a running uploader hands over to its new version: the old one ends, the new one runs from Uploader\$To.
# (Scratch folders: the registry is never touched.)
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zipTo`"", '5099' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'handover')
    $oldUploader = Start-Process -FilePath $install.Uploader -ArgumentList '--local-app-data', "`"$($install.Local)`"", '--roaming-app-data', "`"$($install.Roaming)`"" -PassThru
    $hasOldEnded = $oldUploader.WaitForExit(60000)
    Check $hasOldEnded "handover: the $From uploader ended"
    $newProgram = Join-Path $install.Local "Telltale\Uploader\$To\Telltale.Uploader.exe"
    Start-Sleep -Seconds 3
    $newUploaders = @(Get-Process -Name 'Telltale.Uploader' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $newProgram })
    Check ($newUploaders.Count -eq 1) "handover: one uploader runs, from Uploader\$To"
    $log = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check ($log -match "uploader $toPattern is running; this one \($fromPattern\) ends") 'handover: the old one logged the hand-over'
    Check ($log -match "Started $toPattern") 'handover: the new one started'
    $newUploaders | Stop-Process -Force
}
finally {
    Stop-Process -Id $site.Id -Force
    Get-Process -Name 'Telltale.Uploader' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$scratch*" } | Stop-Process -Force
}

# Case 5: Help's "Check for updates" (from 0.7.2): a running uploader, up to date at first, answers a request at once;
# once the site offers <To>, a second request installs it and is answered before the uploader hands over.
function Send-CheckRequest($install) {
    $requestId = [guid]::NewGuid().ToString('D')
    $requestPath = Join-Path $install.Local 'Telltale\update-check.request'
    Set-Content -Path "$requestPath.partial" -Value $requestId -NoNewline
    Move-Item -Force "$requestPath.partial" $requestPath
    $resultPath = Join-Path $install.Local 'Telltale\update-check.result'
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if ((Test-Path $resultPath) -and ((Get-Content -Raw $resultPath) -like "$requestId*")) {
            return @{ Lines = ((Get-Content -Raw $resultPath) -split "`n"); Seconds = 30 - ($deadline - (Get-Date)).TotalSeconds }
        }
        Start-Sleep -Milliseconds 250
    }
    return $null
}
if ([version]$From -ge [version]'0.7.2') {
    $zipFrom = Join-Path $RepositoryFolder "artifacts\Telltale-$From.zip"
    $site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zipFrom`"", '5099' -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    $install = New-Install (Join-Path $scratch 'ask')
    $runningUploader = Start-Process -FilePath $install.Uploader -ArgumentList '--local-app-data', "`"$($install.Local)`"", '--roaming-app-data', "`"$($install.Roaming)`"" -PassThru
    try {
        Start-Sleep -Seconds 4
        $answer = Send-CheckRequest $install
        Check ($null -ne $answer -and $answer.Lines[1] -eq 'UpToDate') "ask: up to date answered ($(if ($answer) { '{0:0.0} s: {1}' -f $answer.Seconds, $answer.Lines[2] } else { 'no answer' }))"
        Stop-Process -Id $site.Id -Force
        $site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zipTo`"", '5099' -PassThru -WindowStyle Hidden
        Start-Sleep -Seconds 2
        $answer = Send-CheckRequest $install
        Check ($null -ne $answer -and $answer.Lines[1] -eq 'Installed') "ask: a new version installed and answered ($(if ($answer) { '{0:0.0} s: {1}' -f $answer.Seconds, $answer.Lines[2] } else { 'no answer' }))"
        Check ((Get-Content -Raw (Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026\Telltale.addin')) -match "Telltale\\$toPattern\\Telltale\.dll") "ask: the manifest points at $To"
        Check ($runningUploader.WaitForExit(30000)) "ask: the $From uploader handed over and ended"
    }
    finally {
        Stop-Process -Id $site.Id -Force
        Get-Process -Name 'Telltale.Uploader' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$scratch*" } | Stop-Process -Force
    }
}

# Case 3: the site is down: nothing changes, and the uploader still ends its pass.
$install = New-Install (Join-Path $scratch 'down')
$exitCode = Invoke-UploaderOnce $install
Check ($exitCode -eq 0) 'down: the pass ends normally'
Check ((Get-Content -Raw (Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026\Telltale.addin')) -match $fromPattern) 'down: nothing changed'

Remove-Item -Recurse -Force $scratch
Write-Host "Failures: $($failures.Count)"
if ($failures.Count -gt 0) { exit 1 }

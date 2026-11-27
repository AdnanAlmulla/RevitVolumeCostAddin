# Trial run of the update path: a 0.7.0 install laid out as Install-Addin.ps1 leaves it, in scratch folders; the real
# 0.7.0 uploader, run once with folder overrides against a stand-in site offering 0.7.1. Never touches the real folders.
param([string]$RepositoryFolder, [string]$Python = 'python')
$ErrorActionPreference = 'Stop'
$scratchpad = $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]
function Check([bool]$condition, [string]$what) { if ($condition) { Write-Host "PASS $what" } else { Write-Host "FAIL $what"; $failures.Add($what) } }

function New-Install([string]$root) {
    $roaming = Join-Path $root 'Roaming'; $local = Join-Path $root 'Local'
    $package = Join-Path $RepositoryFolder 'artifacts\Telltale-0.7.0'
    # As Install-Addin.ps1 does, for Revit 2026 only (this PC's 2024 is left out on purpose: it must not be updated).
    $year = Join-Path $roaming 'Autodesk\Revit\Addins\2026'
    New-Item -ItemType Directory -Force -Path $year | Out-Null
    Copy-Item -Recurse (Join-Path $package '2026\Telltale') (Join-Path $year 'Telltale')
    Copy-Item (Join-Path $package '2026\Telltale.addin') $year
    New-Item -ItemType Directory -Force -Path (Join-Path $local 'Telltale\Uploader') | Out-Null
    Copy-Item -Recurse (Join-Path $package 'Uploader') (Join-Path $local 'Telltale\Uploader\0.7.0')
    New-Item -ItemType Directory -Force -Path (Join-Path $roaming 'Telltale') | Out-Null
    Set-Content (Join-Path $roaming 'Telltale\uploader.json') '{ "siteAddress": "http://127.0.0.1:5099", "sitePassword": "trial-password" }'
    return @{ Roaming = $roaming; Local = $local; Uploader = (Join-Path $local 'Telltale\Uploader\0.7.0\Telltale.Uploader.exe') }
}

function Invoke-UploaderOnce($install) {
    $process = Start-Process -FilePath $install.Uploader -ArgumentList '--once', '--local-app-data', "`"$($install.Local)`"", '--roaming-app-data', "`"$($install.Roaming)`"" -PassThru -Wait
    return $process.ExitCode
}

$scratch = Join-Path $env:TEMP ('telltale-update-' + [guid]::NewGuid().ToString('N'))
$zip071 = Join-Path $RepositoryFolder 'artifacts\Telltale-0.7.1.zip'

# Case 1: a good release.
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zip071`"", '5099' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'good')
    Invoke-UploaderOnce $install | Out-Null
    $year = Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026'
    Check ((Get-Content -Raw (Join-Path $year 'Telltale.addin')) -match 'Telltale\\0\.7\.1\\Telltale\.dll') 'good: the 2026 manifest points at 0.7.1'
    Check (Test-Path (Join-Path $year 'Telltale\0.7.1\Telltale.dll')) 'good: 0.7.1 installed beside'
    Check (Test-Path (Join-Path $year 'Telltale\0.7.0\Telltale.dll')) 'good: 0.7.0 kept'
    Check (Test-Path (Join-Path $year 'Telltale\0.7.1\Telltale instructions.txt')) 'good: the instruction sheet came with it'
    Check (-not (Test-Path (Join-Path $install.Roaming 'Autodesk\Revit\Addins\2024'))) 'good: Revit 2024, not installed, left alone'
    Check (Test-Path (Join-Path $install.Local 'Telltale\Uploader\0.7.1\Telltale.Uploader.exe')) 'good: uploader 0.7.1 installed beside'
    Check (Test-Path (Join-Path $install.Local 'Telltale\Uploader\0.7.0\Telltale.Uploader.exe')) 'good: uploader 0.7.0 kept'
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
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zip071`"", '5099', 'corrupt' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'corrupt')
    Invoke-UploaderOnce $install | Out-Null
    $year = Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026'
    Check ((Get-Content -Raw (Join-Path $year 'Telltale.addin')) -match 'Telltale\\0\.7\.0\\Telltale\.dll') 'corrupt: the manifest still points at 0.7.0'
    Check (-not (Test-Path (Join-Path $year 'Telltale\0.7.1'))) 'corrupt: nothing of 0.7.1 installed'
    Check (-not (Test-Path (Join-Path $install.Local 'Telltale\Updates\Telltale-0.7.1.zip'))) 'corrupt: the bad download not kept'
    $log = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check ($log -match 'does not match its size and checksum') 'corrupt: logged'
}
finally { Stop-Process -Id $site.Id -Force }

# Case 4: a running uploader hands over to its new version: the old one ends, the new one runs from Uploader\0.7.1.
# (Scratch folders: the registry is never touched.)
$site = Start-Process -FilePath $Python -ArgumentList "`"$scratchpad\stub_site.py`"", "`"$zip071`"", '5099' -PassThru -WindowStyle Hidden
Start-Sleep -Seconds 2
try {
    $install = New-Install (Join-Path $scratch 'handover')
    $oldUploader = Start-Process -FilePath $install.Uploader -ArgumentList '--local-app-data', "`"$($install.Local)`"", '--roaming-app-data', "`"$($install.Roaming)`"" -PassThru
    $hasOldEnded = $oldUploader.WaitForExit(60000)
    Check $hasOldEnded 'handover: the 0.7.0 uploader ended'
    $newProgram = Join-Path $install.Local 'Telltale\Uploader\0.7.1\Telltale.Uploader.exe'
    Start-Sleep -Seconds 3
    $newUploaders = @(Get-Process -Name 'Telltale.Uploader' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $newProgram })
    Check ($newUploaders.Count -eq 1) 'handover: one uploader runs, from Uploader\0.7.1'
    $log = Get-Content -Raw (Join-Path $install.Local 'Telltale\Logs\uploader.log')
    Check ($log -match 'uploader 0\.7\.1 is running; this one \(0\.7\.0\) ends') 'handover: the old one logged the hand-over'
    Check ($log -match 'Started 0\.7\.1') 'handover: the new one started'
    $newUploaders | Stop-Process -Force
}
finally {
    Stop-Process -Id $site.Id -Force
    Get-Process -Name 'Telltale.Uploader' -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$scratch*" } | Stop-Process -Force
}

# Case 3: the site is down: nothing changes, and the uploader still ends its pass.
$install = New-Install (Join-Path $scratch 'down')
$exitCode = Invoke-UploaderOnce $install
Check ($exitCode -eq 0) 'down: the pass ends normally'
Check ((Get-Content -Raw (Join-Path $install.Roaming 'Autodesk\Revit\Addins\2026\Telltale.addin')) -match '0\.7\.0') 'down: nothing changed'

Remove-Item -Recurse -Force $scratch
Write-Host "Failures: $($failures.Count)"
if ($failures.Count -gt 0) { exit 1 }

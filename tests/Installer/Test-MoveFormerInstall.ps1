# Checks installer/Move-FormerInstall.ps1 against scratch folders in %TEMP% seeded with a RevitVolumeCost 0.6.0 install.
# It never touches the real AppData or Startup folders. Run it after changing the migration:
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\Installer\Test-MoveFormerInstall.ps1
#
# Stopping the old uploader is not covered here (it looks for a running process by name).

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\installer\Move-FormerInstall.ps1')

$failures = New-Object System.Collections.Generic.List[string]
function Check([bool]$condition, [string]$what) { if ($condition) { Write-Host "PASS $what" } else { Write-Host "FAIL $what"; $failures.Add($what) } }

function New-OldLayout([string]$root) {
    $roaming = Join-Path $root 'Roaming'; $local = Join-Path $root 'Local'; $startup = Join-Path $root 'Startup'
    foreach ($year in '2024', '2026', '2023') {
        $yearFolder = Join-Path $roaming "Autodesk\Revit\Addins\$year"
        New-Item -ItemType Directory -Force -Path (Join-Path $yearFolder 'RevitVolumeCost') | Out-Null
        Set-Content (Join-Path $yearFolder 'RevitVolumeCost.addin') 'old manifest'
        Set-Content (Join-Path $yearFolder 'RevitVolumeCost\RevitVolumeCost.dll') 'old dll'
        Set-Content (Join-Path $yearFolder 'Other.addin') 'someone else'
    }
    New-Item -ItemType Directory -Force -Path (Join-Path $roaming 'RevitVolumeCost') | Out-Null
    Set-Content (Join-Path $roaming 'RevitVolumeCost\uploader.json') '{ "siteAddress": "http://host:5080", "sitePassword": "old-password" }'
    Set-Content (Join-Path $roaming 'RevitVolumeCost\addin.json') '{ "snapshotAfterSync": false }'
    foreach ($sub in 'Outbox\Sent', 'Outbox\Rejected', 'Logs', 'Uploader') { New-Item -ItemType Directory -Force -Path (Join-Path $local "RevitVolumeCost\$sub") | Out-Null }
    Set-Content (Join-Path $local 'RevitVolumeCost\Outbox\20261127-080000_a.snapshot.json.gz') 'unsent'
    Set-Content (Join-Path $local 'RevitVolumeCost\Outbox\20261127-080000_a.shapes.json.gz') 'unsent shapes'
    Set-Content (Join-Path $local 'RevitVolumeCost\Outbox\20261127-090000_b.snapshot.json.gz.partial') 'half'
    Set-Content (Join-Path $local 'RevitVolumeCost\Outbox\Sent\20261126-080000_c.snapshot.json.gz') 'sent'
    Set-Content (Join-Path $local 'RevitVolumeCost\Logs\uploader.log') 'old log'
    Set-Content (Join-Path $local 'RevitVolumeCost\Uploader\RevitVolumeCost.Uploader.exe') 'old exe'
    New-Item -ItemType Directory -Force -Path $startup | Out-Null
    Set-Content (Join-Path $startup 'RevitVolumeCost uploader.lnk') 'old shortcut'
    Set-Content (Join-Path $startup 'Unrelated.lnk') 'keep'
    return @{ Roaming = $roaming; Local = $local; Startup = $startup }
}

$scratch = Join-Path $env:TEMP ('telltale-migration-' + [guid]::NewGuid().ToString('N'))

# Case 1: a PC with 0.6.0 and nothing under the new name.
$folders = New-OldLayout (Join-Path $scratch 'case1')
Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder $folders.Roaming -LocalAppDataFolder $folders.Local -StartupFolder $folders.Startup
foreach ($year in '2024', '2026', '2023') {
    $yearFolder = Join-Path $folders.Roaming "Autodesk\Revit\Addins\$year"
    Check (-not (Test-Path (Join-Path $yearFolder 'RevitVolumeCost.addin'))) "case 1: old manifest gone ($year)"
    Check (-not (Test-Path (Join-Path $yearFolder 'RevitVolumeCost'))) "case 1: old add-in folder gone ($year)"
    Check (Test-Path (Join-Path $yearFolder 'Other.addin')) "case 1: another add-in left alone ($year)"
}
Check ((Get-Content -Raw (Join-Path $folders.Roaming 'Telltale\uploader.json')) -match 'old-password') 'case 1: uploader settings carried over'
Check (Test-Path (Join-Path $folders.Roaming 'Telltale\addin.json')) 'case 1: add-in settings carried over'
Check (-not (Test-Path (Join-Path $folders.Roaming 'RevitVolumeCost'))) 'case 1: old settings folder gone'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.snapshot.json.gz')) 'case 1: unsent snapshot in the new outbox'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.shapes.json.gz')) 'case 1: unsent shapes in the new outbox'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\Sent\20261126-080000_c.snapshot.json.gz')) 'case 1: sent history kept'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Logs\uploader.log')) 'case 1: logs kept'
Check (-not (Test-Path (Join-Path $folders.Local 'Telltale\Uploader'))) 'case 1: old uploader program not carried over'
Check (-not (Test-Path (Join-Path $folders.Local 'RevitVolumeCost'))) 'case 1: old local folder gone'
Check (-not (Test-Path (Join-Path $folders.Startup 'RevitVolumeCost uploader.lnk'))) 'case 1: old Startup shortcut gone'
Check (Test-Path (Join-Path $folders.Startup 'Unrelated.lnk')) 'case 1: other Startup shortcuts left alone'

# Running it again finds nothing to do and changes nothing.
Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder $folders.Roaming -LocalAppDataFolder $folders.Local -StartupFolder $folders.Startup
Check ((Get-Content -Raw (Join-Path $folders.Roaming 'Telltale\uploader.json')) -match 'old-password') 'case 1: a second run changes nothing'

# Case 2: the new name exists already (for example the uploader started once and wrote its default settings).
$folders = New-OldLayout (Join-Path $scratch 'case2')
New-Item -ItemType Directory -Force -Path (Join-Path $folders.Roaming 'Telltale'), (Join-Path $folders.Local 'Telltale\Outbox') | Out-Null
Set-Content (Join-Path $folders.Roaming 'Telltale\uploader.json') '{ "siteAddress": "http://new:5080", "sitePassword": "new-password" }'
Set-Content (Join-Path $folders.Local 'Telltale\Outbox\20261127-100000_d.snapshot.json.gz') 'new unsent'
Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder $folders.Roaming -LocalAppDataFolder $folders.Local -StartupFolder $folders.Startup
Check ((Get-Content -Raw (Join-Path $folders.Roaming 'Telltale\uploader.json')) -match 'new-password') 'case 2: existing settings never overwritten'
Check (Test-Path (Join-Path $folders.Roaming 'Telltale\addin.json')) 'case 2: a missing settings file carried over'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.snapshot.json.gz')) 'case 2: unsent snapshot moved'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.shapes.json.gz')) 'case 2: unsent shapes moved'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-100000_d.snapshot.json.gz')) 'case 2: the new outbox keeps its own'
Check (-not (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-090000_b.snapshot.json.gz.partial'))) 'case 2: a half-written file is not moved'
Check (-not (Test-Path (Join-Path $folders.Local 'RevitVolumeCost\Uploader'))) 'case 2: old uploader program removed'

# Case 3: -WhatIf changes nothing.
$folders = New-OldLayout (Join-Path $scratch 'case3')
Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder $folders.Roaming -LocalAppDataFolder $folders.Local -StartupFolder $folders.Startup -WhatIf
Check (Test-Path (Join-Path $folders.Roaming 'Autodesk\Revit\Addins\2026\RevitVolumeCost.addin')) 'case 3: -WhatIf leaves the old manifest'
Check (Test-Path (Join-Path $folders.Local 'RevitVolumeCost\Uploader')) 'case 3: -WhatIf leaves the old uploader'
Check (-not (Test-Path (Join-Path $folders.Roaming 'Telltale'))) 'case 3: -WhatIf moves no settings'

# Case 4: a fresh PC.
$fresh = Join-Path $scratch 'case4'
New-Item -ItemType Directory -Force -Path (Join-Path $fresh 'Roaming'), (Join-Path $fresh 'Local'), (Join-Path $fresh 'Startup') | Out-Null
Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder (Join-Path $fresh 'Roaming') -LocalAppDataFolder (Join-Path $fresh 'Local') -StartupFolder (Join-Path $fresh 'Startup')
Check (@(Get-ChildItem -Recurse $fresh -File).Count -eq 0) 'case 4: a fresh PC is left as it was'

# Case 5: the old folders can't be renamed (a file in each held open): the files that matter are moved one by one.
$folders = New-OldLayout (Join-Path $scratch 'case5')
$heldSettings = [System.IO.File]::Open((Join-Path $folders.Roaming 'RevitVolumeCost\addin.json'), 'Open', 'Read', 'Read')
$heldLog = [System.IO.File]::Open((Join-Path $folders.Local 'RevitVolumeCost\Logs\uploader.log'), 'Open', 'Read', 'Read')
try {
    Move-FormerInstall -ProductName 'Telltale' -RoamingAppDataFolder $folders.Roaming -LocalAppDataFolder $folders.Local -StartupFolder $folders.Startup
}
finally {
    $heldSettings.Dispose()
    $heldLog.Dispose()
}
Check ((Get-Content -Raw (Join-Path $folders.Roaming 'Telltale\uploader.json')) -match 'old-password') 'case 5: uploader settings moved although the folder could not be renamed'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.snapshot.json.gz')) 'case 5: unsent snapshot moved although the folder could not be renamed'
Check (Test-Path (Join-Path $folders.Local 'Telltale\Outbox\20261127-080000_a.shapes.json.gz')) 'case 5: unsent shapes moved although the folder could not be renamed'
Check (-not (Test-Path (Join-Path $folders.Roaming 'Autodesk\Revit\Addins\2026\RevitVolumeCost.addin'))) 'case 5: old manifest gone'

Remove-Item -Recurse -Force $scratch
Write-Host "Failures: $($failures.Count)"
if ($failures.Count -gt 0) { exit 1 }

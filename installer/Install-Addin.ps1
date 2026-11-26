# Installs the add-in for each Revit year it was built for and that this computer has.
#
# Start it with "Install.cmd" (double-click), with Revit closed. For each year folder beside this script
# (2024, 2026, ...) whose Revit is installed, it copies:
#     <year>\<product>.addin   to  %AppData%\Autodesk\Revit\Addins\<year>\
#     <year>\<product>\        to  %AppData%\Autodesk\Revit\Addins\<year>\<product>\
# replacing an earlier copy. Revit reads .addin files from that folder when it starts.
#
# The add-in is copied to this computer's own disk and never run from the network share: Windows reads a
# DLL piece by piece as it is used, and a failed read over the network closes the program with no message
# (seen with ETABS on 2026-11-23).
#
# "-WhatIf" prints what it would do and changes nothing.

[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'

$packageFolder = $PSScriptRoot
$addinsRootFolder = Join-Path $env:APPDATA 'Autodesk\Revit\Addins'

# Refuse while Revit is open: Revit holds the add-in's DLL open, so it cannot be replaced.
$runningRevit = Get-Process -Name 'Revit' -ErrorAction SilentlyContinue
if ($runningRevit) {
    Write-Host 'Revit is open. Close every Revit window, then run Install.cmd again.' -ForegroundColor Red
    exit 1
}

# A network AppData folder would put the DLL back on the network.
if ($env:APPDATA.StartsWith('\\')) {
    Write-Host "This computer's AppData folder is on the network ($env:APPDATA). Nothing was installed: ask Adnan." -ForegroundColor Red
    exit 1
}

$yearFolders = Get-ChildItem -Path $packageFolder -Directory | Where-Object { $_.Name -match '^\d{4}$' } | Sort-Object Name
if (-not $yearFolders) {
    Write-Host 'No Revit year folders (such as 2026) were found beside this script. Nothing was installed.' -ForegroundColor Red
    exit 1
}

$installedYearCount = 0
foreach ($yearFolder in $yearFolders) {
    $revitYear = $yearFolder.Name
    $revitProgram = Join-Path $env:ProgramFiles "Autodesk\Revit $revitYear\Revit.exe"
    if (-not (Test-Path $revitProgram)) {
        Write-Host "Revit $revitYear is not installed here: skipped."
        continue
    }

    $manifest = Get-ChildItem -Path $yearFolder.FullName -Filter '*.addin' | Select-Object -First 1
    $productName = [System.IO.Path]::GetFileNameWithoutExtension($manifest.Name)
    $sourceAddinFolder = Join-Path $yearFolder.FullName $productName
    $targetYearFolder = Join-Path $addinsRootFolder $revitYear
    $targetAddinFolder = Join-Path $targetYearFolder $productName

    if ($PSCmdlet.ShouldProcess($targetYearFolder, "Install $productName for Revit $revitYear")) {
        New-Item -ItemType Directory -Force -Path $targetYearFolder | Out-Null
        if (Test-Path $targetAddinFolder) {
            Remove-Item -Recurse -Force -Path $targetAddinFolder
        }
        Copy-Item -Recurse -Path $sourceAddinFolder -Destination $targetAddinFolder
        $targetManifest = Join-Path $targetYearFolder $manifest.Name
        Copy-Item -Force -Path $manifest.FullName -Destination $targetManifest
        # A package that arrived by Teams or email carries Windows' "downloaded from the internet" mark, and
        # copying keeps it. Revit 2024 (.NET Framework) can refuse to load a marked DLL, so remove the mark.
        Get-ChildItem -Recurse -File -Path $targetAddinFolder | Unblock-File
        Unblock-File -Path $targetManifest
        Write-Host "Revit ${revitYear}: installed in $targetYearFolder" -ForegroundColor Green
    }
    $installedYearCount++
}

if ($installedYearCount -eq 0) {
    Write-Host 'None of the Revit years this add-in was built for is installed here. Nothing was installed.' -ForegroundColor Red
    exit 1
}

# The uploader: sends the add-in's snapshots to the website. Copied to this computer's own disk, started now and at
# every Windows sign-in (a shortcut in the user's Startup folder), with no window. Its two settings, the website's
# address and the site password, come from Adnan and are kept in %AppData%\<product>\uploader.json, never in the package.
$sourceUploaderFolder = Join-Path $packageFolder 'Uploader'
$uploaderProgramName = "$productName.Uploader"
$targetUploaderFolder = Join-Path $env:LOCALAPPDATA "$productName\Uploader"
$uploaderProgram = Join-Path $targetUploaderFolder "$uploaderProgramName.exe"
$uploaderSettingsPath = Join-Path $env:APPDATA "$productName\uploader.json"
$startupShortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) "$productName uploader.lnk"
# The uploader is built for .NET 8, which Revit 2025 and 2026 install; a PC with only Revit 2024 may not have it.
$dotNet8Folders = Get-ChildItem -Path (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App') -Directory -Filter '8.*' -ErrorAction SilentlyContinue

if (-not (Test-Path $sourceUploaderFolder)) {
    Write-Host 'This package has no uploader: snapshots stay in the outbox for now.'
}
elseif (-not $dotNet8Folders) {
    Write-Host 'The uploader needs .NET 8, which is not on this computer: snapshots stay in the outbox. Tell Adnan.' -ForegroundColor Red
}
elseif ($PSCmdlet.ShouldProcess($targetUploaderFolder, 'Install the uploader and start it at sign-in')) {
    Get-Process -Name $uploaderProgramName -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
    if (Test-Path $targetUploaderFolder) {
        Remove-Item -Recurse -Force -Path $targetUploaderFolder
    }
    Copy-Item -Recurse -Path $sourceUploaderFolder -Destination $targetUploaderFolder
    Get-ChildItem -Recurse -File -Path $targetUploaderFolder | Unblock-File

    $uploaderSettings = $null
    if (Test-Path $uploaderSettingsPath) {
        $uploaderSettings = Get-Content -Raw -Path $uploaderSettingsPath | ConvertFrom-Json
    }
    $isAddressMissing = (-not $uploaderSettings) -or [string]::IsNullOrWhiteSpace($uploaderSettings.siteAddress) -or ($uploaderSettings.siteAddress -like '*localhost*')
    $isPasswordMissing = (-not $uploaderSettings) -or [string]::IsNullOrWhiteSpace($uploaderSettings.sitePassword)
    if ($isAddressMissing -or $isPasswordMissing) {
        Write-Host ''
        Write-Host 'The uploader needs the website address and the site password. Adnan gives you both.'
        $siteAddress = (Read-Host 'Website address (such as http://<computer name>:5080)').Trim().TrimEnd('/')
        $securePassword = Read-Host 'Site password' -AsSecureString
        $passwordPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        $sitePassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($passwordPointer)
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($passwordPointer)
        New-Item -ItemType Directory -Force -Path (Split-Path $uploaderSettingsPath) | Out-Null
        $newSettings = [ordered]@{ siteAddress = $siteAddress; sitePassword = $sitePassword } | ConvertTo-Json
        [IO.File]::WriteAllText($uploaderSettingsPath, $newSettings)
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($startupShortcutPath)
    $shortcut.TargetPath = $uploaderProgram
    $shortcut.WorkingDirectory = $targetUploaderFolder
    $shortcut.Save()
    Start-Process -FilePath $uploaderProgram -WorkingDirectory $targetUploaderFolder
    Write-Host "Uploader: installed in $targetUploaderFolder, running, and started at every sign-in." -ForegroundColor Green
    Write-Host "Its log: $env:LOCALAPPDATA\$productName\Logs\uploader.log"
}

Write-Host ''
Write-Host 'Done. Start Revit; if it asks about an unsigned add-in, choose "Load Once" or "Always Load".'

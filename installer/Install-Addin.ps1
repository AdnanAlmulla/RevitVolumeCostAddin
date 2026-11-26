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
Write-Host ''
Write-Host 'Done. Start Revit; if it asks about an unsigned add-in, choose "Load Once" or "Always Load".'

# Clears away a copy of the add-in installed under a former product name, and carries its settings and unsent
# snapshots over to the current name. Install-Addin.ps1 loads this file and runs it before installing; it ships beside
# it in the package.
#
# The product was renamed from "RevitVolumeCost" to "Telltale" in 0.7.0 (engineer, 2026-11-27). Every name the
# add-in used is built from the product name, so an old copy left in place would load a second add-in in Revit (two
# snapshots per sync), run a second uploader, and strand its unsent snapshots in the old outbox. The former names are
# written here as text: the one place outside Directory.Build.props where a product name appears, on purpose.
#
# For each former name, with Revit closed:
#   1. stops the old uploader and waits for it to end, and deletes its Startup shortcut;
#   2. deletes the old .addin manifest and add-in folder from every %AppData%\Autodesk\Revit\Addins\<year>\;
#   3. %AppData%\<old>\ (uploader.json with the site address and password, addin.json): renamed to the current name
#      when the current one does not exist yet; otherwise each settings file the current name lacks is moved across;
#   4. %LocalAppData%\<old>\ (Outbox with Sent and Rejected, Logs): renamed likewise, without the old uploader program;
#      otherwise each complete snapshot and shapes file still waiting in the old Outbox is moved to the current one.
# A file that already exists under the current name is never overwritten, so running the installer twice is safe.
# The old Documents\<old>\Raw dumps folder is left where it is: it holds the technician's own files.

$FORMER_PRODUCT_NAMES = @('RevitVolumeCost')

function Move-FormerInstall {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param(
        [Parameter(Mandatory = $true)][string]$ProductName,
        [Parameter(Mandatory = $true)][string]$RoamingAppDataFolder,
        [Parameter(Mandatory = $true)][string]$LocalAppDataFolder,
        [Parameter(Mandatory = $true)][string]$StartupFolder,
        [string[]]$FormerProductNames = $FORMER_PRODUCT_NAMES
    )

    $addinsRootFolder = Join-Path $RoamingAppDataFolder 'Autodesk\Revit\Addins'
    foreach ($formerName in $FormerProductNames) {
        if ($formerName -eq $ProductName) {
            continue
        }

        # 1. The old uploader: stopped before anything moves, since it holds its program and moves outbox files.
        $formerUploader = Get-Process -Name "$formerName.Uploader" -ErrorAction SilentlyContinue
        if ($formerUploader -and $PSCmdlet.ShouldProcess("$formerName.Uploader", 'Stop the former uploader')) {
            $formerUploader | Stop-Process -Force
            $formerUploader | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
            Write-Host "Stopped the former uploader, $formerName.Uploader"
        }
        Remove-PathIfPresent (Join-Path $StartupFolder "$formerName uploader.lnk")

        # 2. The old add-in, from every Revit year, whether or not this package or this PC has that Revit.
        if (Test-Path $addinsRootFolder) {
            foreach ($yearFolder in Get-ChildItem -Path $addinsRootFolder -Directory | Where-Object { $_.Name -match '^\d{4}$' }) {
                Remove-PathIfPresent (Join-Path $yearFolder.FullName "$formerName.addin")
                Remove-PathIfPresent (Join-Path $yearFolder.FullName $formerName)
            }
        }

        # 3. Settings.
        $formerSettingsFolder = Join-Path $RoamingAppDataFolder $formerName
        $settingsFolder = Join-Path $RoamingAppDataFolder $ProductName
        if (Test-Path $formerSettingsFolder) {
            if (-not (Test-Path $settingsFolder)) {
                Move-FolderTo $formerSettingsFolder $settingsFolder
            }
            else {
                foreach ($settingsFileName in 'uploader.json', 'addin.json') {
                    Move-FileIfAbsent (Join-Path $formerSettingsFolder $settingsFileName) (Join-Path $settingsFolder $settingsFileName)
                }
            }
        }

        # 4. Outbox and logs.
        $formerLocalFolder = Join-Path $LocalAppDataFolder $formerName
        $localFolder = Join-Path $LocalAppDataFolder $ProductName
        if (Test-Path $formerLocalFolder) {
            Remove-PathIfPresent (Join-Path $formerLocalFolder 'Uploader')
            if (-not (Test-Path $localFolder)) {
                Move-FolderTo $formerLocalFolder $localFolder
            }
            else {
                $formerOutbox = Join-Path $formerLocalFolder 'Outbox'
                $outbox = Join-Path $localFolder 'Outbox'
                if (Test-Path $formerOutbox) {
                    # Complete files only: a ".partial" file is one the add-in never finished writing.
                    $waitingFiles = Get-ChildItem -Path $formerOutbox -File | Where-Object { $_.Name -like '*.snapshot.json.gz' -or $_.Name -like '*.shapes.json.gz' }
                    if ($waitingFiles -and $PSCmdlet.ShouldProcess($outbox, 'Create the outbox')) {
                        New-Item -ItemType Directory -Force -Path $outbox | Out-Null
                    }
                    foreach ($waitingFile in $waitingFiles) {
                        Move-FileIfAbsent $waitingFile.FullName (Join-Path $outbox $waitingFile.Name)
                    }
                }
                Write-Host "The former $formerName folder stays at $formerLocalFolder (sent snapshots and logs); it can be deleted."
            }
        }
    }
}

function Remove-PathIfPresent {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$path)
    if ((Test-Path $path) -and $PSCmdlet.ShouldProcess($path, 'Delete')) {
        Remove-Item -Recurse -Force -Path $path
        Write-Host "Removed $path"
    }
}

function Move-FolderTo {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$fromFolder, [string]$toFolder)
    if ($PSCmdlet.ShouldProcess($fromFolder, "Rename to $toFolder")) {
        Move-Item -Path $fromFolder -Destination $toFolder
        Write-Host "Moved $fromFolder to $toFolder"
    }
}

function Move-FileIfAbsent {
    [CmdletBinding(SupportsShouldProcess = $true)]
    param([string]$fromPath, [string]$toPath)
    if ((Test-Path $fromPath) -and -not (Test-Path $toPath) -and $PSCmdlet.ShouldProcess($fromPath, "Move to $toPath")) {
        Move-Item -Path $fromPath -Destination $toPath
        Write-Host "Moved $fromPath to $toPath"
    }
}

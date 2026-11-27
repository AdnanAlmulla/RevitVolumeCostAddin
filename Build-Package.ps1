# Builds the hand-off package for the BIM technician: one folder and one .zip holding, for each Revit year,
# the add-in DLL and its .addin manifest, plus Install.cmd and the instruction sheet. No source goes in.
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File .\Build-Package.ps1
#
# It runs the tests first and stops if any fails. Output: artifacts\<product>-<version>\ and its .zip
# (artifacts\ is not committed).

$ErrorActionPreference = 'Stop'

$repositoryFolder = $PSScriptRoot
$addinProject = Join-Path $repositoryFolder 'src\Addin\Addin.csproj'
$testProject = Join-Path $repositoryFolder 'tests\Addin.Tests\Addin.Tests.csproj'
# The sheet for the current hand-off (v0.3: automatic snapshot after sync; v0.1 was raw-dump-instructions.md).
$instructionSheet = Join-Path $repositoryFolder 'docs\technician\sync-instructions.md'

# Revit year built by each build configuration (src/Addin/Addin.csproj).
$REVIT_YEAR_BY_CONFIGURATION = [ordered]@{ R24 = '2024'; R26 = '2026' }

function Invoke-Checked([string]$description, [scriptblock]$command) {
    Write-Host "== $description"
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$description failed (exit code $LASTEXITCODE)." }
}

$productName = (& dotnet msbuild $addinProject -getProperty:ProductName -p:Configuration=R26).Trim()
$version = (& dotnet msbuild $addinProject -getProperty:Version -p:Configuration=R26).Trim()

Invoke-Checked 'Tests' { dotnet test $testProject -nologo -v q }

$packageName = "$productName-$version"
$packageFolder = Join-Path $repositoryFolder "artifacts\$packageName"
if (Test-Path $packageFolder) { Remove-Item -Recurse -Force $packageFolder }
New-Item -ItemType Directory -Force -Path $packageFolder | Out-Null

foreach ($configuration in $REVIT_YEAR_BY_CONFIGURATION.Keys) {
    $revitYear = $REVIT_YEAR_BY_CONFIGURATION[$configuration]
    Invoke-Checked "Build for Revit $revitYear" { dotnet build $addinProject -c $configuration -nologo -v q }

    $buildFolder = Join-Path $repositoryFolder "src\Addin\bin\$configuration"
    $yearFolder = Join-Path $packageFolder $revitYear
    $addinFolder = Join-Path $yearFolder $productName
    New-Item -ItemType Directory -Force -Path $addinFolder | Out-Null
    Copy-Item (Join-Path $buildFolder "$productName.addin") $yearFolder
    # Only our own DLL (and its .pdb, for error line numbers): the add-in carries no third-party files.
    Copy-Item (Join-Path $buildFolder "$productName.dll") $addinFolder
    Copy-Item (Join-Path $buildFolder "$productName.pdb") $addinFolder
    # The instruction sheet beside the DLL, where the Help button opens it (AddinInfo.InstructionSheetPath).
    Copy-Item $instructionSheet (Join-Path $addinFolder "$productName instructions.txt")
    $unexpectedFiles = Get-ChildItem $buildFolder -File | Where-Object { $_.Extension -eq '.dll' -and $_.BaseName -ne $productName }
    if ($unexpectedFiles) { throw "The build output holds other DLLs ($($unexpectedFiles.Name -join ', ')); the add-in must carry none." }
}

# The uploader, one build for every Revit year (.NET 8, no third-party packages).
$uploaderProject = Join-Path $repositoryFolder 'src\Uploader\Uploader.csproj'
Invoke-Checked 'Build the uploader' { dotnet publish $uploaderProject -c Release -nologo -v q -o (Join-Path $packageFolder 'Uploader') }

Copy-Item (Join-Path $repositoryFolder 'installer\Install.cmd') $packageFolder
Copy-Item (Join-Path $repositoryFolder 'installer\Install-Addin.ps1') $packageFolder
Copy-Item (Join-Path $repositoryFolder 'installer\Move-FormerInstall.ps1') $packageFolder
# Shipped as .txt: many PCs have no program for .md files.
Copy-Item $instructionSheet (Join-Path $packageFolder 'READ ME FIRST.txt')

$zipPath = "$packageFolder.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $packageFolder '*') -DestinationPath $zipPath
Write-Host "== Package: $zipPath" -ForegroundColor Green

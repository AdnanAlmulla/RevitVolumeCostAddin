# Draws the add-in's ribbon icons as PNG files, from the outlines written below. The PNGs are committed and
# embedded in the add-in DLL (src/Addin/Resources/Icons), so this runs only when an icon changes:
#
#     powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Draw-RibbonIcons.ps1
#
# Each icon is drawn twice, for Revit's light and dark themes, at the two sizes Revit's ribbon shows: 32 x 32 px
# (large button) and 16 x 16 px (small button, Quick Access Toolbar). One flat colour per theme, no gradients
# (engineer, 2026-11-27: restrained icons that work in both themes). -Preview also writes a sheet of every icon on
# both ribbon backgrounds to the given file, to look at before committing.
#
# Needs Windows PowerShell (it draws with WPF, which Windows has).

param([string]$Preview)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$iconsFolder = Join-Path $PSScriptRoot '..\src\Addin\Resources\Icons'

# Outlines on a 32 x 32 grid, in Windows' path language (M move, L line, H/V horizontal/vertical line, A arc, Z close).
# "Stroke" outlines are drawn as lines; "Fill" outlines are filled.
$ICONS = [ordered]@{
    # A cloud with an arrow going up into it.
    'sync' = @{
        Stroke = 'M 12.5,25 H 9 A 5,5 0 0 1 8.6,15 A 7.5,7.5 0 0 1 23,12.5 A 6.25,6.25 0 0 1 23.75,25 H 19.5 M 16,28 V 15.5 M 12,19.5 L 16,15.5 L 20,19.5'
        Fill = ''
    }
    # A list of three rows, each a dot and a line: the parameter standard. (Boxes ran together at 16 px.)
    'parameters' = @{
        Stroke = 'M 13,8 H 27 M 13,16 H 27 M 13,24 H 23'
        Fill = 'M 7,5.5 A 2.5,2.5 0 1 1 6.99,5.5 Z M 7,13.5 A 2.5,2.5 0 1 1 6.99,13.5 Z M 7,21.5 A 2.5,2.5 0 1 1 6.99,21.5 Z'
    }
    # A page with a folded corner and three lines of text.
    'rawdump' = @{
        Stroke = 'M 8,4 H 19 L 25,10 V 28 H 8 Z M 19,4 V 10 H 25 M 12,15 H 21 M 12,19 H 21 M 12,23 H 18'
        Fill = ''
    }
    # A question mark in a circle.
    'help' = @{
        Stroke = 'M 16,4 A 12,12 0 1 1 15.99,4 Z M 12,12.75 A 4,4 0 1 1 18.1,16.1 C 16.6,17 16,17.8 16,19.5'
        Fill = 'M 16,21.6 A 1.6,1.6 0 1 1 15.99,21.6 Z'
    }
}

# One colour per theme, chosen against the ribbon's background. The ribbon colours are estimates, not measured: confirm
# them from the technician's screenshots of the ribbon in each theme (docs/technician/release-check.md steps 6 and 7).
$THEMES = [ordered]@{
    'light' = @{ Ink = '#3A4149'; Ribbon = '#F0F0F0' }
    'dark'  = @{ Ink = '#D9DDE2'; Ribbon = '#3B4048' }
}

# Line width on the 32 grid, per drawn size: thinner lines vanish at 16 px, so the small icon gets relatively thicker ones.
$STROKE_WIDTH_ON_GRID_BY_SIZE = @{ 32 = 2.0; 16 = 2.6 }
$GRID_SIZE = 32
$SCREEN_DPI = 96

function New-IconBitmap([hashtable]$icon, [string]$inkColour, [int]$sizePixels, [string]$backgroundColour) {
    $ink = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($inkColour))
    $pen = New-Object System.Windows.Media.Pen($ink, $STROKE_WIDTH_ON_GRID_BY_SIZE[$sizePixels])
    $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
    $scale = $sizePixels / $GRID_SIZE

    $visual = New-Object System.Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    if ($backgroundColour) {
        $background = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($backgroundColour))
        $context.DrawRectangle($background, $null, (New-Object System.Windows.Rect(0, 0, $sizePixels, $sizePixels)))
    }
    $context.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))
    $context.DrawGeometry($null, $pen, [System.Windows.Media.Geometry]::Parse($icon.Stroke))
    if ($icon.Fill) {
        $context.DrawGeometry($ink, $null, [System.Windows.Media.Geometry]::Parse($icon.Fill))
    }
    $context.Pop()
    $context.Close()

    $bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($sizePixels, $sizePixels, $SCREEN_DPI, $SCREEN_DPI, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    return $bitmap
}

function Save-Png($bitmap, [string]$path) {
    $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file = [System.IO.File]::Create($path)
    try { $encoder.Save($file) } finally { $file.Dispose() }
}

New-Item -ItemType Directory -Force -Path $iconsFolder | Out-Null
foreach ($iconName in $ICONS.Keys) {
    foreach ($themeName in $THEMES.Keys) {
        foreach ($sizePixels in 32, 16) {
            $bitmap = New-IconBitmap $ICONS[$iconName] $THEMES[$themeName].Ink $sizePixels $null
            $path = Join-Path $iconsFolder "$iconName-$themeName-$sizePixels.png"
            Save-Png $bitmap $path
            Write-Host "Wrote $path"
        }
    }
}

if ($Preview) {
    # Each icon at 32 and 16 px on each theme's ribbon, then the same enlarged 4 times to show the pixels.
    $cellPixels = 40
    $zoom = 4
    $columns = $ICONS.Count * 2
    $rows = $THEMES.Count
    $sheet = New-Object System.Windows.Media.DrawingVisual
    # Enlarged icons show their pixels as squares, not smoothed.
    [System.Windows.Media.RenderOptions]::SetBitmapScalingMode($sheet, [System.Windows.Media.BitmapScalingMode]::NearestNeighbor)
    $context = $sheet.RenderOpen()
    $rowIndex = 0
    foreach ($themeName in $THEMES.Keys) {
        $background = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString($THEMES[$themeName].Ribbon))
        $context.DrawRectangle($background, $null, (New-Object System.Windows.Rect(0, ($rowIndex * $cellPixels * $zoom), ($columns * $cellPixels * $zoom), ($cellPixels * $zoom))))
        $columnIndex = 0
        foreach ($iconName in $ICONS.Keys) {
            foreach ($sizePixels in 32, 16) {
                $bitmap = New-IconBitmap $ICONS[$iconName] $THEMES[$themeName].Ink $sizePixels $null
                $left = $columnIndex * $cellPixels * $zoom + 8
                $top = $rowIndex * $cellPixels * $zoom + 8
                $context.DrawImage($bitmap, (New-Object System.Windows.Rect($left, $top, $sizePixels, $sizePixels)))
                $context.DrawImage($bitmap, (New-Object System.Windows.Rect(($left + 40), ($top + 8), ($sizePixels * $zoom), ($sizePixels * $zoom))))
                $columnIndex++
            }
        }
        $rowIndex++
    }
    $context.Close()
    $sheetBitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(($columns * $cellPixels * $zoom), ($rows * $cellPixels * $zoom), $SCREEN_DPI, $SCREEN_DPI, [System.Windows.Media.PixelFormats]::Pbgra32)
    $sheetBitmap.Render($sheet)
    Save-Png $sheetBitmap $Preview
    Write-Host "Preview: $Preview"
}

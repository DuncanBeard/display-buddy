<#
.SYNOPSIS
    Regenerates every TaskBard icon and MSIX package image from assets/mark.txt.

.DESCRIPTION
    Run by hand after changing mark.txt or a palette below; never part of the
    build. Builds and publishes only use the committed outputs:

      TaskBard/TaskBard.ico           executable icon (canonical palette)
      TaskBard.Package/Images/*.png   MSIX package images (canonical palette)
      assets/tray/tray-*.ico          tray variants: light, dark, High Contrast

    The mark is scaled by whole pixels only (nearest neighbour, no
    anti-aliasing) and centred, so every output stays crisp. The same inputs
    give byte-identical outputs.

.EXAMPLE
    .\assets\generate.ps1
#>

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$packageImages = Join-Path $root 'TaskBard.Package\Images'
$trayDir = Join-Path $PSScriptRoot 'tray'

# Palette per variant; any grid cell without a colour stays transparent.
# 'o' is the outline generated around the filled pixels.
$palettes = @{
    canonical = @{ o = '#1B1B2F'; n = '#F4C430'; b = '#FFF4D6' }  # exe, Start menu and Store
    light     = @{ o = '#1B1B2F'; n = '#B3122E'; b = '#3A3F58' }  # light taskbar
    dark      = @{ o = '#0B0B14'; n = '#F4C430'; b = '#E6E6EE' }  # dark taskbar
    hc        = @{ o = '#000000'; n = '#FFFFFF'; b = '#FFFFFF' }  # High Contrast (black and white only)
}

$scales = 100, 125, 150, 200, 400
$appIconSizes = 16, 24, 32, 48, 64, 128, 256
$trayIconSizes = 16, 20, 24, 32, 40, 48
$appListTargetSizes = 16, 24, 32, 48, 256

# Read the fill grid and add the outline.
function Read-Mark {
    $rows = @(Get-Content (Join-Path $PSScriptRoot 'mark.txt') | Where-Object { $_ -and -not $_.StartsWith('#') })
    $size = $rows.Count
    foreach ($row in $rows) {
        if ($row.Length -ne $size) { throw "mark.txt must be a square grid; '$row' is not $size wide" }
    }

    $grid = New-Object 'char[,]' $size, $size
    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) { $grid[$y, $x] = $rows[$y][$x] }
    }

    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            if ($rows[$y][$x] -ne '.') { continue }
            :neighbours for ($dy = -1; $dy -le 1; $dy++) {
                for ($dx = -1; $dx -le 1; $dx++) {
                    $ny = $y + $dy; $nx = $x + $dx
                    if ($ny -ge 0 -and $ny -lt $size -and $nx -ge 0 -and $nx -lt $size -and $rows[$ny][$nx] -ne '.') {
                        $grid[$y, $x] = 'o'
                        break neighbours
                    }
                }
            }
        }
    }
    return , $grid
}

# Draw the mark centred on a transparent canvas at the largest whole-pixel
# scale that fits within $Fill of the shorter side.
function New-MarkBitmap {
    param([char[,]]$Grid, [hashtable]$Palette, [int]$Width, [int]$Height, [double]$Fill = 1.0)

    $cells = $Grid.GetLength(0)
    $scale = [Math]::Max(1, [Math]::Floor([Math]::Min($Width, $Height) * $Fill / $cells))
    $left = [Math]::Floor(($Width - $cells * $scale) / 2)
    $top = [Math]::Floor(($Height - $cells * $scale) / 2)

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        for ($y = 0; $y -lt $cells; $y++) {
            for ($x = 0; $x -lt $cells; $x++) {
                $hex = $Palette[[string]$Grid[$y, $x]]
                if (-not $hex) { continue }
                $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($hex))
                $graphics.FillRectangle($brush, [int]($left + $x * $scale), [int]($top + $y * $scale), [int]$scale, [int]$scale)
                $brush.Dispose()
            }
        }
    } finally {
        $graphics.Dispose()
    }
    return $bitmap
}

function Get-PngBytes([System.Drawing.Bitmap]$Bitmap) {
    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    } finally {
        $stream.Dispose()
    }
}

function Save-Png {
    param([char[,]]$Grid, [hashtable]$Palette, [string]$Path, [int]$Width, [int]$Height, [double]$Fill)
    $bitmap = New-MarkBitmap $Grid $Palette $Width $Height $Fill
    try {
        [System.IO.File]::WriteAllBytes($Path, (Get-PngBytes $bitmap))
    } finally {
        $bitmap.Dispose()
    }
}

# 32-bit BMP icon frame: BITMAPINFOHEADER, bottom-up BGRA rows, then a
# 1-bit AND mask that marks the transparent pixels.
function Get-DibBytes([System.Drawing.Bitmap]$Bitmap) {
    $size = $Bitmap.Width
    $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $pixels = New-Object byte[] ($size * $size * 4)
        for ($y = 0; $y -lt $size; $y++) {
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $pixels, $y * $size * 4, $size * 4)
        }
    } finally {
        $Bitmap.UnlockBits($data)
    }

    $maskStride = [int]([Math]::Ceiling($size / 32) * 4)
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([int]40); $writer.Write([int]$size); $writer.Write([int]($size * 2))
    $writer.Write([int16]1); $writer.Write([int16]32); $writer.Write([int]0)
    $writer.Write([int]($size * $size * 4)); $writer.Write([int]0); $writer.Write([int]0)
    $writer.Write([int]0); $writer.Write([int]0)
    for ($y = $size - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $size * 4, $size * 4) }
    for ($y = $size - 1; $y -ge 0; $y--) {
        $mask = New-Object byte[] $maskStride
        for ($x = 0; $x -lt $size; $x++) {
            if ($pixels[($y * $size + $x) * 4 + 3] -eq 0) { $mask[[Math]::Floor($x / 8)] = $mask[[Math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8)) }
        }
        $writer.Write($mask)
    }
    $writer.Flush()
    return , $stream.ToArray()
}

# Multi-size ICO. 256px frames are PNG-compressed, smaller ones are BMP for
# the widest compatibility (resource compiler, Explorer, shell notify icons).
function Save-Ico {
    param([char[,]]$Grid, [hashtable]$Palette, [string]$Path, [int[]]$Sizes)

    $frames = foreach ($size in $Sizes) {
        $bitmap = New-MarkBitmap $Grid $Palette $size $size 1.0
        try {
            , $(if ($size -ge 256) { Get-PngBytes $bitmap } else { Get-DibBytes $bitmap })
        } finally {
            $bitmap.Dispose()
        }
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $stream
    $writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$Sizes.Count)
    $offset = 6 + 16 * $Sizes.Count
    for ($i = 0; $i -lt $Sizes.Count; $i++) {
        $dim = if ($Sizes[$i] -ge 256) { 0 } else { $Sizes[$i] }
        $writer.Write([byte]$dim); $writer.Write([byte]$dim); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([int16]1); $writer.Write([int16]32)
        $writer.Write([int]$frames[$i].Length); $writer.Write([int]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $stream.ToArray())
    $stream.Dispose()
}

function Get-ScaledSize([int]$Size, [int]$Scale) {
    return [int][Math]::Round($Size * $Scale / 100, [MidpointRounding]::AwayFromZero)
}

$grid = Read-Mark
$canonical = $palettes.canonical

# MSIX package images. This folder holds only generated files, so clear it first.
Get-ChildItem $packageImages -Filter *.png -ErrorAction SilentlyContinue | Remove-Item
New-Item -ItemType Directory -Force $packageImages | Out-Null

# Name, scale-100 size, and how much of the shorter side the mark may fill.
$packageImageSlots = @(
    @{ Name = 'StoreLogo';         Width = 50;  Height = 50;  Fill = 1.0 },
    @{ Name = 'Square44x44Logo';   Width = 44;  Height = 44;  Fill = 1.0 },
    @{ Name = 'Square71x71Logo';   Width = 71;  Height = 71;  Fill = 0.66 },
    @{ Name = 'Square150x150Logo'; Width = 150; Height = 150; Fill = 0.66 },
    @{ Name = 'Wide310x150Logo';   Width = 310; Height = 150; Fill = 0.66 },
    @{ Name = 'Square310x310Logo'; Width = 310; Height = 310; Fill = 0.66 }
)
foreach ($slot in $packageImageSlots) {
    foreach ($scale in $scales) {
        $w = Get-ScaledSize $slot.Width $scale
        $h = Get-ScaledSize $slot.Height $scale
        Save-Png $grid $canonical (Join-Path $packageImages "$($slot.Name).scale-$scale.png") $w $h $slot.Fill
    }
}
foreach ($size in $appListTargetSizes) {
    Save-Png $grid $canonical (Join-Path $packageImages "Square44x44Logo.targetsize-$size.png") $size $size 1.0
    Save-Png $grid $canonical (Join-Path $packageImages "Square44x44Logo.targetsize-${size}_altform-unplated.png") $size $size 1.0
}

Save-Ico $grid $canonical (Join-Path $root 'TaskBard\TaskBard.ico') $appIconSizes

New-Item -ItemType Directory -Force $trayDir | Out-Null
foreach ($variant in 'light', 'dark', 'hc') {
    Save-Ico $grid $palettes[$variant] (Join-Path $trayDir "tray-$variant.ico") $trayIconSizes
}

Write-Host "Generated $((Get-ChildItem $packageImages -Filter *.png).Count) package images, TaskBard.ico and 3 tray icons."

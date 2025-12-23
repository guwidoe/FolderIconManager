# SVG to ICO Converter for Build Process
param(
    [string]$SvgPath,
    [string]$IcoPath
)

function Test-CommandExists {
    param($Command)
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

function Get-InkscapePath {
    # Check if inkscape is in PATH
    $cmd = Get-Command inkscape -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Path
    }
    
    # Check common installation locations
    $commonPaths = @(
        "C:\Program Files\Inkscape\bin\inkscape.exe",
        "C:\Program Files (x86)\Inkscape\bin\inkscape.exe",
        "$env:LOCALAPPDATA\Programs\Inkscape\bin\inkscape.exe"
    )
    
    foreach ($path in $commonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }
    
    return $null
}

function Convert-WithInkscape {
    param([string]$svgFile, [string]$icoFile, [string]$inkscapePath)
    
    Write-Host "Converting with Inkscape..."
    $tempDir = Join-Path $env:TEMP "svg2ico_$(Get-Random)"
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    
    try {
        $sizes = @(16, 32, 48, 256)
        $pngFiles = @()
        
        foreach ($size in $sizes) {
            $pngFile = Join-Path $tempDir "icon_$size.png"
            & $inkscapePath --export-type=png --export-filename="$pngFile" --export-width=$size --export-height=$size "$svgFile" 2>&1 | Out-Null
            
            if (Test-Path $pngFile) {
                $pngFiles += $pngFile
            }
        }
        
        if ($pngFiles.Count -eq 0) {
            return $false
        }
        
        # Combine PNGs into ICO using PowerShell
        Add-Type -AssemblyName System.Drawing
        
        $fs = [System.IO.File]::Create($icoFile)
        $writer = New-Object System.IO.BinaryWriter($fs)
        
        try {
            # ICO header
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$pngFiles.Count)
            
            # Read all PNG data
            $pngData = @()
            $pngSizes = @()
            foreach ($pngFile in $pngFiles) {
                $img = [System.Drawing.Image]::FromFile($pngFile)
                $pngSizes += $img.Width
                $img.Dispose()
                $pngData += ,[System.IO.File]::ReadAllBytes($pngFile)
            }
            
            # Calculate and write directory
            $dataOffset = 6 + (16 * $pngFiles.Count)
            for ($i = 0; $i -lt $pngFiles.Count; $i++) {
                $size = $pngSizes[$i]
                $sizeValue = if ($size -eq 256) { 0 } else { $size }
                
                $writer.Write([byte]$sizeValue)
                $writer.Write([byte]$sizeValue)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$pngData[$i].Length)
                $writer.Write([uint32]$dataOffset)
                $dataOffset = $dataOffset + $pngData[$i].Length
            }
            
            # Write PNG data
            foreach ($data in $pngData) {
                $writer.Write($data)
            }
            
            Write-Host "Successfully converted using Inkscape"
            return $true
        }
        finally {
            $writer.Close()
            $fs.Close()
        }
    }
    catch {
        Write-Error "Inkscape conversion failed: $_"
        return $false
    }
    finally {
        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Convert-WithImageMagick {
    param([string]$svgFile, [string]$icoFile)
    
    Write-Host "Converting with ImageMagick..."
    
    try {
        if (Test-CommandExists "magick") {
            & magick "$svgFile" -define icon:auto-resize=256,48,32,16 "$icoFile" 2>&1 | Out-Null
            if (Test-Path $icoFile) {
                Write-Host "Successfully converted using ImageMagick"
                return $true
            }
        }
        elseif (Test-CommandExists "convert") {
            & convert "$svgFile" -define icon:auto-resize=256,48,32,16 "$icoFile" 2>&1 | Out-Null
            if (Test-Path $icoFile) {
                Write-Host "Successfully converted using ImageMagick"
                return $true
            }
        }
        return $false
    }
    catch {
        return $false
    }
}

function Convert-WithDotNet {
    param([string]$svgFile, [string]$icoFile)
    
    Write-Host "Creating fallback icon using .NET..."
    
    try {
        Add-Type -AssemblyName System.Drawing
        
        # Default color from the SVG gradient
        $color = [System.Drawing.Color]::FromArgb(255, 79, 172, 254)
        $sizes = @(16, 32, 48, 256)
        
        # Create PNG data for each size
        $pngDataList = New-Object 'System.Collections.Generic.List[byte[]]'
        
        foreach ($size in $sizes) {
            $bitmap = New-Object System.Drawing.Bitmap($size, $size)
            $g = [System.Drawing.Graphics]::FromImage($bitmap)
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $g.Clear([System.Drawing.Color]::Transparent)
            
            # Draw simple folder icon
            $margin = [Math]::Max(2, [int]($size * 0.08))
            $width = $size - ($margin * 2)
            $height = [int]($size - ($margin * 2.5))
            $y = [int]($margin * 1.5)
            
            $brush = New-Object System.Drawing.SolidBrush($color)
            $g.FillRectangle($brush, $margin, $y, $width, $height)
            $brush.Dispose()
            $g.Dispose()
            
            # Save to PNG in memory
            $ms = New-Object System.IO.MemoryStream
            $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
            $bitmap.Dispose()
            
            $pngDataList.Add($ms.ToArray())
            $ms.Dispose()
        }
        
        # Write ICO file
        $fs = [System.IO.File]::Create($icoFile)
        $writer = New-Object System.IO.BinaryWriter($fs)
        
        try {
            # Header
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$sizes.Count)
            
            # Calculate offset
            [int]$offset = 6 + (16 * $sizes.Count)
            
            # Write directory
            for ($i = 0; $i -lt $sizes.Count; $i++) {
                $size = $sizes[$i]
                $sizeValue = if ($size -eq 256) { 0 } else { $size }
                $length = $pngDataList[$i].Length
                
                $writer.Write([byte]$sizeValue)
                $writer.Write([byte]$sizeValue)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$length)
                $writer.Write([uint32]$offset)
                $offset = $offset + $length
            }
            
            # Write image data
            for ($i = 0; $i -lt $pngDataList.Count; $i++) {
                $writer.Write($pngDataList[$i])
            }
            
            Write-Host "Created fallback icon (for best results, install Inkscape or ImageMagick)"
            return $true
        }
        finally {
            $writer.Close()
            $fs.Close()
        }
    }
    catch {
        Write-Error "Fallback conversion failed: $_"
        return $false
    }
}

# Main
if (-not (Test-Path $SvgPath)) {
    Write-Error "SVG file not found: $SvgPath"
    exit 1
}

$icoDir = Split-Path $IcoPath -Parent
if ($icoDir -and -not (Test-Path $icoDir)) {
    New-Item -ItemType Directory -Path $icoDir -Force | Out-Null
}

# Check if conversion needed
if (Test-Path $IcoPath) {
    $svgTime = (Get-Item $SvgPath).LastWriteTime
    $icoTime = (Get-Item $IcoPath).LastWriteTime
    if ($svgTime -le $icoTime) {
        Write-Host "ICO file is up to date."
        exit 0
    }
}

Write-Host "Converting AppIcon.svg to AppIcon.ico..."

# Try methods in order
$success = $false

# Method 1: Inkscape (best quality)
$inkscapePath = Get-InkscapePath
if ($inkscapePath) {
    Write-Host "Found Inkscape at: $inkscapePath"
    $success = Convert-WithInkscape -svgFile $SvgPath -icoFile $IcoPath -inkscapePath $inkscapePath
}

if (-not $success -and ((Test-CommandExists "magick") -or (Test-CommandExists "convert"))) {
    $success = Convert-WithImageMagick -svgFile $SvgPath -icoFile $IcoPath
}

if (-not $success) {
    Write-Warning "Neither Inkscape nor ImageMagick found. Using fallback method."
    Write-Warning "For best icon quality, install Inkscape (https://inkscape.org/) or ImageMagick (https://imagemagick.org/)"
    $success = Convert-WithDotNet -svgFile $SvgPath -icoFile $IcoPath
}

if (-not $success) {
    Write-Error "All conversion methods failed!"
    exit 1
}

exit 0

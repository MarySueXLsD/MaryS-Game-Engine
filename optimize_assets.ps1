# Asset Optimization Script for MaryS Game Engine
# This script optimizes PNG assets to reduce bundle size and improve loading performance

Write-Host "Starting asset optimization..." -ForegroundColor Green

# Function to optimize PNG files
function Optimize-PngFile {
    param (
        [string]$FilePath,
        [int]$MaxWidth = 64,
        [int]$MaxHeight = 64,
        [int]$Quality = 90
    )
    
    $originalSize = (Get-Item $FilePath).Length
    Write-Host "Optimizing: $FilePath (Original: $([Math]::Round($originalSize/1KB, 2)) KB)" -ForegroundColor Yellow
    
    # Create backup
    $backupPath = $FilePath + ".backup"
    if (-not (Test-Path $backupPath)) {
        Copy-Item $FilePath $backupPath
    }
    
    try {
        # Use ImageMagick if available, otherwise provide instructions
        if (Get-Command "magick" -ErrorAction SilentlyContinue) {
            # Resize and optimize with ImageMagick
            & magick $FilePath -resize "${MaxWidth}x${MaxHeight}>" -strip -quality $Quality $FilePath
            
            $newSize = (Get-Item $FilePath).Length
            $savings = $originalSize - $newSize
            $savingsPercent = [Math]::Round(($savings / $originalSize) * 100, 1)
            
            Write-Host "  Optimized: $([Math]::Round($newSize/1KB, 2)) KB (Saved: $([Math]::Round($savings/1KB, 2)) KB, $savingsPercent%)" -ForegroundColor Green
        } else {
            Write-Host "  ImageMagick not found. Please install ImageMagick for automatic optimization." -ForegroundColor Red
            Write-Host "  Download from: https://imagemagick.org/script/download.php" -ForegroundColor Red
        }
    } catch {
        Write-Host "  Error optimizing $FilePath`: $_" -ForegroundColor Red
        # Restore backup if optimization failed
        if (Test-Path $backupPath) {
            Copy-Item $backupPath $FilePath -Force
        }
    }
}

# Get all PNG files larger than 500KB
$largeFiles = Get-ChildItem -Path "Content" -Recurse -Filter "*.png" | Where-Object { $_.Length -gt 500KB }

Write-Host "Found $($largeFiles.Count) large PNG files to optimize" -ForegroundColor Cyan

foreach ($file in $largeFiles) {
    # Icon files should be small (64x64 max)
    if ($file.Name -like "*icon*" -or $file.Name -like "*logo*") {
        Optimize-PngFile -FilePath $file.FullName -MaxWidth 64 -MaxHeight 64 -Quality 85
    } else {
        # Other images can be larger but still optimized
        Optimize-PngFile -FilePath $file.FullName -MaxWidth 256 -MaxHeight 256 -Quality 90
    }
}

# Calculate total space saved
$originalTotal = 0
$optimizedTotal = 0

Get-ChildItem -Path "Content" -Recurse -Filter "*.png.backup" | ForEach-Object {
    $originalTotal += $_.Length
    $optimizedFile = $_.FullName -replace "\.backup$", ""
    if (Test-Path $optimizedFile) {
        $optimizedTotal += (Get-Item $optimizedFile).Length
    }
}

if ($originalTotal -gt 0) {
    $totalSavings = $originalTotal - $optimizedTotal
    $totalSavingsPercent = [Math]::Round(($totalSavings / $originalTotal) * 100, 1)
    
    Write-Host "`nOptimization Complete!" -ForegroundColor Green
    Write-Host "Original total size: $([Math]::Round($originalTotal/1MB, 2)) MB" -ForegroundColor Cyan
    Write-Host "Optimized total size: $([Math]::Round($optimizedTotal/1MB, 2)) MB" -ForegroundColor Cyan
    Write-Host "Total space saved: $([Math]::Round($totalSavings/1MB, 2)) MB ($totalSavingsPercent%)" -ForegroundColor Green
    
    Write-Host "`nTo restore original files if needed, run: Get-ChildItem -Recurse -Filter '*.backup' | ForEach-Object { Move-Item `$_.FullName (`$_.FullName -replace '\.backup`$', '') -Force }" -ForegroundColor Yellow
} else {
    Write-Host "`nNo files were optimized. Make sure ImageMagick is installed." -ForegroundColor Yellow
}

Write-Host "`nAsset optimization completed!" -ForegroundColor Green
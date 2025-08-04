# Optimized Build Script for MaryS Game Engine
# This script builds the project with all performance optimizations enabled

Write-Host "Starting optimized build process..." -ForegroundColor Green

# Check if dotnet is available
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "Error: .NET SDK not found. Please install .NET 8.0 SDK." -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
try {
    dotnet clean --configuration Release
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }
    Write-Host "Clean completed successfully." -ForegroundColor Green
} catch {
    Write-Host "Warning: Clean operation failed: $_" -ForegroundColor Yellow
}

# Restore packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
try {
    dotnet restore
    Write-Host "Package restore completed successfully." -ForegroundColor Green
} catch {
    Write-Host "Error: Package restore failed: $_" -ForegroundColor Red
    exit 1
}

# Build with optimizations
Write-Host "Building with performance optimizations..." -ForegroundColor Yellow
try {
    $buildArgs = @(
        "build"
        "--configuration", "Release"
        "--verbosity", "minimal"
        "/p:Optimize=true"
        "/p:TieredCompilation=true"
        "/p:TieredPGO=true"
        "/p:PublishReadyToRun=true"
    )
    
    & dotnet @buildArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Build completed successfully!" -ForegroundColor Green
    } else {
        Write-Host "Build failed with exit code: $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
} catch {
    Write-Host "Error during build: $_" -ForegroundColor Red
    exit 1
}

# Optional: Create optimized publish
Write-Host "`nCreating optimized publish build..." -ForegroundColor Yellow
try {
    $publishArgs = @(
        "publish"
        "--configuration", "Release"
        "--output", "publish"
        "--self-contained", "false"
        "/p:PublishReadyToRun=true"
        "/p:PublishTrimmed=true"
        "/p:TrimMode=partial"
        "/p:Optimize=true"
    )
    
    & dotnet @publishArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Publish completed successfully!" -ForegroundColor Green
        
        # Show build size
        if (Test-Path "publish") {
            $publishSize = (Get-ChildItem -Recurse "publish" | Measure-Object -Property Length -Sum).Sum
            Write-Host "Published build size: $([Math]::Round($publishSize/1MB, 2)) MB" -ForegroundColor Cyan
        }
    } else {
        Write-Host "Publish failed with exit code: $LASTEXITCODE" -ForegroundColor Red
    }
} catch {
    Write-Host "Warning: Publish operation failed: $_" -ForegroundColor Yellow
}

# Show performance recommendations
Write-Host "`n=== Performance Recommendations ===" -ForegroundColor Cyan
Write-Host "1. Run the asset optimization script: .\optimize_assets.ps1" -ForegroundColor White
Write-Host "2. Use Release configuration for best performance" -ForegroundColor White
Write-Host "3. Monitor performance with: dotnet-counters monitor --process-id <pid>" -ForegroundColor White
Write-Host "4. Profile with: dotnet-trace collect --process-id <pid>" -ForegroundColor White

Write-Host "`nOptimized build process completed!" -ForegroundColor Green
# Generates a 16px spritefont XML for every TTF in Fonts/TTF, placing the .spritefont in the corresponding folder under Fonts/SpriteFonts
# Updates Content/Content.mgcb to include all generated spritefonts

$ttfRoot = "Content/Fonts/TTF"
$spritefontRoot = "Content/Fonts/SpriteFonts"
$fontsRoot = "Content/Fonts"
$mainMgcb = "Content/Content.mgcb"

# Get absolute path for $ttfRoot
$ttfRootAbs = [System.IO.Path]::GetFullPath($ttfRoot)

# Template for the spritefont XML
function Get-SpriteFontXml {
    param(
        [string]$ttfRelativePath
    )
    return @"
<?xml version="1.0" encoding="utf-8"?>
<XnaContent xmlns:Graphics="Microsoft.Xna.Framework.Content.Pipeline.Graphics">
  <Asset Type="Graphics:FontDescription">
    <FontName>$ttfRelativePath</FontName>
    <Size>16</Size>
    <Spacing>0</Spacing>
    <UseKerning>true</UseKerning>
    <Style>Regular</Style>
    <CharacterRegions>
      <CharacterRegion>
        <Start>&#32;</Start>
        <End>&#126;</End>
      </CharacterRegion>
    </CharacterRegions>
  </Asset>
</XnaContent>
"@
}

# Function to create mgcb block for a spritefont
function Get-SpriteFontMgcbBlock {
    param(
        [string]$relativePath
    )
    return @"
#begin $relativePath
/importer:FontDescriptionImporter
/processor:FontDescriptionProcessor
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Compressed
/build:$relativePath
"@
}

# List to collect all generated spritefont relative paths
$spritefontRelativePaths = @()

# Find all TTF files recursively
Get-ChildItem -Path $ttfRoot -Recurse -Filter *.ttf | ForEach-Object {
    $ttfFullPath = [System.IO.Path]::GetFullPath($_.FullName)
    $relativeSubPath = $ttfFullPath.Substring($ttfRootAbs.Length).TrimStart('\','/')
    $ttfRelativePath = "../../TTF/" + $relativeSubPath -replace '\\','/'
    
    # Determine if this is a root-level TTF file or in a subfolder
    $isRootLevel = $relativeSubPath.IndexOf('\') -eq -1 -and $relativeSubPath.IndexOf('/') -eq -1
    
    if ($isRootLevel) {
        # For root-level TTF files, place spritefont in a subfolder named after the font
        $fontName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
        $spritefontSubdir = Join-Path $spritefontRoot $fontName
    } else {
        # For TTF files in subfolders, place spritefont in corresponding subfolder
        $spritefontSubdir = Join-Path $spritefontRoot ([System.IO.Path]::GetDirectoryName($relativeSubPath))
    }
    
    if (!(Test-Path $spritefontSubdir)) {
        New-Item -ItemType Directory -Path $spritefontSubdir | Out-Null
    }
    
    $spritefontName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name) + ".spritefont"
    $spritefontPath = Join-Path $spritefontSubdir $spritefontName
    $spritefontRelPath = "Fonts/SpriteFonts/" + ([System.IO.Path]::ChangeExtension($relativeSubPath.Replace('\', '/'), ".spritefont"))
    
    if (Test-Path $spritefontPath) {
        Write-Host "Skipped (already exists): $spritefontPath"
    } else {
        $xml = Get-SpriteFontXml $ttfRelativePath
        Set-Content -Path $spritefontPath -Value $xml -Encoding UTF8
        Write-Host "Generated: $spritefontPath"
    }
    $spritefontRelativePaths += $spritefontRelPath
}

# Update Content/Content.mgcb
if (Test-Path $mainMgcb) {
    $mgcbLines = Get-Content $mainMgcb
    
    # Remove all previous spritefont blocks
    $newLines = @()
    $inSpriteFontBlock = $false
    $existingSpriteFonts = @()
    
    foreach ($line in $mgcbLines) {
        if ($line -match "#begin Fonts/SpriteFonts/.+\.spritefont") {
            $inSpriteFontBlock = $true
            # Extract the spritefont path for duplicate checking
            $spritefontPath = $line -replace "#begin ", ""
            $existingSpriteFonts += $spritefontPath
            continue
        }
        if ($inSpriteFontBlock -and $line -match "^/build:Fonts/SpriteFonts/.+\.spritefont$") {
            $inSpriteFontBlock = $false
            continue
        }
        if (-not $inSpriteFontBlock) {
            $newLines += $line
        }
    }
    
    # Add comment section for spritefonts
    $newLines += ""

    $newLines += "# SpriteFont Files"
    $newLines += "# Generated automatically from TTF fonts"
    $newLines += ""
    
    # Append new spritefont blocks, checking for duplicates
    $addedCount = 0
    foreach ($relPath in $spritefontRelativePaths) {
        if ($existingSpriteFonts -notcontains $relPath) {
            $newLines += Get-SpriteFontMgcbBlock $relPath
            $addedCount++
        } else {
            Write-Host "Skipped duplicate in mgcb: $relPath"
        }
    }
    
    Set-Content -Path $mainMgcb -Value $newLines -Encoding UTF8
    Write-Host "Updated $mainMgcb with $addedCount new spritefont references."
} else {
    Write-Host "$mainMgcb not found!"
} 
# Any Uninstaller - Version Updater Script
param (
    [string]$NewVersion = "",
    [switch]$NoPause
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path

# File paths
$propsFile       = Join-Path $rootDir "source\Directory.Build.props"
$manifestFile    = Join-Path $rootDir "packaging\AppxManifest.xml"
$nbugInfoFile    = Join-Path $rootDir "source\NBug_custom\Properties\AssemblyInfo.cs"
$launcherRcFile  = Join-Path $rootDir "source\AnyU-launcher\AnyU-launcher.rc"
$aboutVmFile     = Join-Path $rootDir "source\AnyUninstaller.Avalonia\ViewModels\AboutViewModel.cs"
$settingsVmFile  = Join-Path $rootDir "source\AnyUninstaller.Avalonia\ViewModels\SettingsViewModel.cs"
$buildBatFile    = Join-Path $rootDir "build_packages.bat"
$buildPsFile     = Join-Path $rootDir "scripts\build_packages.ps1"
$readmeFile      = Join-Path $rootDir "README.md"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "                Any Uninstaller - Version Manager                       " -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

function Read-Utf8File($path) {
    if (Test-Path $path) {
        return [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    }
    return $null
}

function Write-Utf8File($path, $text) {
    [System.IO.File]::WriteAllText($path, $text, $utf8NoBom)
}

# Read current version from Directory.Build.props
$currentVersion = "Unknown"
if (Test-Path $propsFile) {
    $propsContent = Read-Utf8File $propsFile
    if ($propsContent -match '<Version>([^<]+)</Version>') {
        $currentVersion = $matches[1].Trim()
    }
}

Write-Host "Current App Version: $currentVersion" -ForegroundColor Yellow
Write-Host ""

$wasInteractive = $false

# Prompt for version if not supplied
if ([string]::IsNullOrWhiteSpace($NewVersion)) {
    $wasInteractive = $true
    $promptVersion = Read-Host "Enter new version (e.g. 1.3.0 or 2.0.0)"
    $NewVersion = $promptVersion.Trim()
}

if ([string]::IsNullOrWhiteSpace($NewVersion)) {
    Write-Host "[ERROR] No version specified. Aborted." -ForegroundColor Red
    if ($wasInteractive -and -not $NoPause) {
        Read-Host "Press Enter to exit..."
    }
    exit 1
}

# Clean input (strip leading 'v' or 'V')
$cleanVersion = $NewVersion.TrimStart('v', 'V').Trim()

# Validate format
if ($cleanVersion -notmatch '^\d+(\.\d+){1,3}$') {
    Write-Host "[ERROR] Invalid version format: '$NewVersion'." -ForegroundColor Red
    Write-Host "Expected format: X.Y or X.Y.Z or X.Y.Z.W (e.g. 1.3.0 or 1.3.0.0)" -ForegroundColor Red
    if ($wasInteractive -and -not $NoPause) {
        Read-Host "Press Enter to exit..."
    }
    exit 1
}

# Parse version parts
$parts = $cleanVersion.Split('.')
$major = [int]$parts[0]
$minor = if ($parts.Length -ge 2) { [int]$parts[1] } else { 0 }
$patch = if ($parts.Length -ge 3) { [int]$parts[2] } else { 0 }
$rev   = if ($parts.Length -ge 4) { [int]$parts[3] } else { 0 }

$semVer   = "$major.$minor.$patch"
$quadVer  = "$major.$minor.$patch.$rev"
$shortVer = "$major.$minor"
$commaVer = "$major,$minor,$patch,$rev"
$tagVer   = "v$semVer"

Write-Host "Applying New Version:" -ForegroundColor Green
Write-Host "  • Semantic Version (App):     $semVer" -ForegroundColor White
Write-Host "  • 4-Part Version (MSIX/Win):  $quadVer" -ForegroundColor White
Write-Host "  • Short Version (Assembly):   $shortVer" -ForegroundColor White
Write-Host "  • RC Comma Version:           $commaVer" -ForegroundColor White
Write-Host "  • Display Tag:                $tagVer" -ForegroundColor White
Write-Host ""
Write-Host "Updating files across codebase..." -ForegroundColor Cyan

$updatedCount = 0

function Update-FileContent($filePath, $pattern, $replacement, $description) {
    if (-not (Test-Path $filePath)) {
        Write-Host "  [!] Skipped (File not found): $filePath" -ForegroundColor DarkGray
        return
    }
    $content = Read-Utf8File $filePath
    if ($content -match $pattern) {
        $newContent = [regex]::Replace($content, $pattern, $replacement)
        if ($newContent -ne $content) {
            Write-Utf8File $filePath $newContent
            Write-Host "  [OK] Updated $description ($([System.IO.Path]::GetFileName($filePath)))" -ForegroundColor Green
            $script:updatedCount++
            return
        }
    }
    Write-Host "  [-] No change needed in $([System.IO.Path]::GetFileName($filePath))" -ForegroundColor DarkGray
}

# 1. Directory.Build.props (<Version>...</Version>)
Update-FileContent $propsFile `
    '(<Version>)[^<]+(</Version>)' `
    "`${1}$semVer`${2}" `
    "Master Project Props Version"

# 2. AppxManifest.xml (Version="...")
Update-FileContent $manifestFile `
    '(<Identity\s+[^>]*?Version=")[^"]+(")' `
    "`${1}$quadVer`${2}" `
    "MSIX Package Identity Version"

# 3. NBug_custom AssemblyInfo.cs
if (Test-Path $nbugInfoFile) {
    $nbugContent = Read-Utf8File $nbugInfoFile
    $nbugModified = [regex]::Replace($nbugContent, '\[assembly:\s*AssemblyVersion\("[^"]+"\)', "[assembly: AssemblyVersion(`"$shortVer`")")
    $nbugModified = [regex]::Replace($nbugModified, '\[assembly:\s*AssemblyFileVersion\("[^"]+"\)', "[assembly: AssemblyFileVersion(`"$quadVer`")")
    if ($nbugModified -ne $nbugContent) {
        Write-Utf8File $nbugInfoFile $nbugModified
        Write-Host "  [OK] Updated NBug AssemblyInfo.cs" -ForegroundColor Green
        $updatedCount++
    }
}

# 4. AnyU-launcher.rc
if (Test-Path $launcherRcFile) {
    $rcContent = Read-Utf8File $launcherRcFile
    $rcModified = [regex]::Replace($rcContent, 'FILEVERSION\s+[\d,]+', "FILEVERSION $commaVer")
    $rcModified = [regex]::Replace($rcContent, 'PRODUCTVERSION\s+[\d,]+', "PRODUCTVERSION $commaVer")
    $rcModified = [regex]::Replace($rcContent, 'VALUE "FileVersion",\s*"[^"]+"', "VALUE `"FileVersion`", `"$quadVer`"")
    $rcModified = [regex]::Replace($rcContent, 'VALUE "ProductVersion",\s*"[^"]+"', "VALUE `"ProductVersion`", `"$quadVer`"")
    if ($rcModified -ne $rcContent) {
        Write-Utf8File $launcherRcFile $rcModified
        Write-Host "  [OK] Updated AnyU-launcher Resource Script (.rc)" -ForegroundColor Green
        $updatedCount++
    }
}

# 5. AboutViewModel.cs
if (Test-Path $aboutVmFile) {
    $aboutContent = Read-Utf8File $aboutVmFile
    $aboutModified = [regex]::Replace($aboutContent, '(public\s+string\s+Version\s*=>\s*")[^"]+(";)', "`${1}$semVer`${2}")
    $aboutModified = [regex]::Replace($aboutContent, '(public\s+string\s+VersionDisplay\s*=>\s*")[^"]+(";)', "`${1}$tagVer`${2}")
    if ($aboutModified -ne $aboutContent) {
        Write-Utf8File $aboutVmFile $aboutModified
        Write-Host "  [OK] Updated AboutViewModel.cs" -ForegroundColor Green
        $updatedCount++
    }
}

# 6. SettingsViewModel.cs
Update-FileContent $settingsVmFile `
    '(public\s+string\s+AppVersion\s*=>\s*")[^"]+(";)' `
    "`${1}$semVer`${2}" `
    "SettingsViewModel.cs"

# 7. build_packages.bat
Update-FileContent $buildBatFile `
    '(Packaging Pipeline \(v)[^\)]+(\))' `
    "`${1}$semVer`${2}" `
    "build_packages.bat title banner"

# 8. scripts/build_packages.ps1
if (Test-Path $buildPsFile) {
    $buildPsContent = Read-Utf8File $buildPsFile
    $buildPsModified = [regex]::Replace($buildPsContent, '(Packaging Pipeline \(v)[^\)]+(\))', "`${1}$semVer`${2}")
    $buildPsModified = [regex]::Replace($buildPsModified, 'AnyUninstaller-v[\d\.]+-Portable', "AnyUninstaller-$tagVer-Portable")
    $buildPsModified = [regex]::Replace($buildPsModified, 'Saayan\.AnyUninstaller_[\d\.]+_x64\.msix', "Saayan.AnyUninstaller_${quadVer}_x64.msix")
    if ($buildPsModified -ne $buildPsContent) {
        Write-Utf8File $buildPsFile $buildPsModified
        Write-Host "  [OK] Updated scripts/build_packages.ps1" -ForegroundColor Green
        $updatedCount++
    }
}

# 9. README.md badge
Update-FileContent $readmeFile `
    '(\[!\[Version\]\(https://img\.shields\.io/badge/Version-v)[^-\)]+(-success\.svg\)\]\(CHANGELOG\.md\))' `
    "`${1}$semVer`${2}" `
    "README.md Version Badge"

Write-Host ""
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "  Version successfully changed everywhere to $semVer ($tagVer)!" -ForegroundColor Green
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host ""

if ($wasInteractive -and -not $NoPause) {
    Write-Host "Press any key to close this window..." -ForegroundColor DarkGray
    [void][System.Console]::ReadKey($true)
}

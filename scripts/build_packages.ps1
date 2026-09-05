# Any Uninstaller - Packaging & Build Pipeline
param (
    [string]$Configuration = "Release",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# Terminate any running instances of Any Uninstaller to release file locks
cmd.exe /c "taskkill /F /T /IM AnyUninstaller.exe 2>nul" | Out-Null
cmd.exe /c "taskkill /F /T /IM AnyUninstaller.Avalonia.exe 2>nul" | Out-Null
Get-Process -Name "*AnyUninstaller*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Resolve-Path (Join-Path $scriptDir "..")).Path
$distDir = Join-Path $rootDir "dist"
$appDir = Join-Path $distDir "app"
$exeDir = Join-Path $distDir "exe"
$portableDir = Join-Path $distDir "portable"
$msixDir = Join-Path $distDir "msix"

# Dynamic Version Resolution
if (-not $Version) {
    $propsPath = Join-Path $rootDir "source\Directory.Build.props"
    if (Test-Path $propsPath) {
        $propsRaw = Get-Content $propsPath -Raw
        if ($propsRaw -match '<Version>([^<]+)</Version>') {
            $Version = $matches[1].Trim()
        }
    }
    if (-not $Version) { $Version = "1.0.0" }
}

$cleanVer = $Version.TrimStart('v', 'V').Trim()
$parts = $cleanVer.Split('.')
$major = [int]$parts[0]
$minor = if ($parts.Length -ge 2) { [int]$parts[1] } else { 0 }
$patch = if ($parts.Length -ge 3) { [int]$parts[2] } else { 0 }
$rev   = if ($parts.Length -ge 4) { [int]$parts[3] } else { 0 }

$semVer  = "$major.$minor.$patch"
$quadVer = "$major.$minor.$patch.$rev"
$tagVer  = "v$semVer"

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "         Any Uninstaller - Packaging Pipeline ($tagVer)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "Root Directory:   $rootDir"
Write-Host "Output Directory: $distDir"
Write-Host "Package Version:  $semVer (MSIX: $quadVer)"
Write-Host ""

# 0. Clean & Prepare Directories
Write-Host "[1/5] Preparing output directories..." -ForegroundColor Yellow
if (Test-Path $distDir) {
    Remove-Item -Path $distDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $appDir | Out-Null
New-Item -ItemType Directory -Force -Path $exeDir | Out-Null
New-Item -ItemType Directory -Force -Path $portableDir | Out-Null
New-Item -ItemType Directory -Force -Path $msixDir | Out-Null

# Generate Assets
Write-Host "[2/5] Generating Store, Taskbar and Start Menu icon assets..." -ForegroundColor Yellow
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

$sourceLogo = Join-Path $rootDir "source\AnyUninstaller.Avalonia\Assets\logo.png"
$assetsDir = Join-Path $distDir "msix_assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

$srcImage = [System.Drawing.Image]::FromFile($sourceLogo)
function Resize-Image($targetPath, $width, $height, $bgHex, $customPad = 0) {
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    
    if ($bgHex) {
        $color = [System.Drawing.ColorTranslator]::FromHtml($bgHex)
        $brush = New-Object System.Drawing.SolidBrush $color
        $g.FillRectangle($brush, 0, 0, $width, $height)
        $brush.Dispose()
    } else {
        $g.Clear([System.Drawing.Color]::Transparent)
    }

    $pad = if ($customPad -ne $null) { $customPad } else { 0 }
    $destW = [Math]::Max(1, $width - ($pad * 2))
    $destH = [Math]::Max(1, $height - ($pad * 2))

    $scale = [Math]::Min($destW / $srcImage.Width, $destH / $srcImage.Height)
    $drawW = [int]($srcImage.Width * $scale)
    $drawH = [int]($srcImage.Height * $scale)
    $drawX = [int](($width - $drawW) / 2)
    $drawY = [int](($height - $drawH) / 2)

    $g.DrawImage($srcImage, $drawX, $drawY, $drawW, $drawH)
    $g.Dispose()
    $bmp.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# 1. Base Manifest Images
Resize-Image (Join-Path $assetsDir "StoreLogo.png") 50 50 $null 0
Resize-Image (Join-Path $assetsDir "Square44x44Logo.png") 44 44 $null 0
Resize-Image (Join-Path $assetsDir "Square150x150Logo.png") 150 150 $null 0
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.png") 310 150 $null 0
Resize-Image (Join-Path $assetsDir "SplashScreen.png") 620 300 "#151D24" 10

# 2. Square44x44Logo Scale Variants
Resize-Image (Join-Path $assetsDir "Square44x44Logo.scale-100.png") 44 44 $null 0
Resize-Image (Join-Path $assetsDir "Square44x44Logo.scale-125.png") 55 55 $null 0
Resize-Image (Join-Path $assetsDir "Square44x44Logo.scale-150.png") 66 66 $null 0
Resize-Image (Join-Path $assetsDir "Square44x44Logo.scale-200.png") 88 88 $null 0
Resize-Image (Join-Path $assetsDir "Square44x44Logo.scale-400.png") 176 176 $null 0

# 3. Square44x44Logo TargetSize & Unplated Variants (For Start Menu, Taskbar, Alt+Tab)
$targetSizes = @(16, 20, 24, 30, 32, 36, 40, 44, 48, 64, 72, 80, 96, 256)
foreach ($ts in $targetSizes) {
    Resize-Image (Join-Path $assetsDir "Square44x44Logo.targetsize-$ts.png") $ts $ts $null 0
    Resize-Image (Join-Path $assetsDir "Square44x44Logo.targetsize-${ts}_altform-unplated.png") $ts $ts $null 0
    Resize-Image (Join-Path $assetsDir "Square44x44Logo.targetsize-${ts}_altform-lightunplated.png") $ts $ts $null 0
}

# 4. Square150x150Logo Scale Variants
Resize-Image (Join-Path $assetsDir "Square150x150Logo.scale-100.png") 150 150 $null 0
Resize-Image (Join-Path $assetsDir "Square150x150Logo.scale-125.png") 188 188 $null 0
Resize-Image (Join-Path $assetsDir "Square150x150Logo.scale-150.png") 225 225 $null 0
Resize-Image (Join-Path $assetsDir "Square150x150Logo.scale-200.png") 300 300 $null 0
Resize-Image (Join-Path $assetsDir "Square150x150Logo.scale-400.png") 600 600 $null 0

# 5. StoreLogo Scale Variants
Resize-Image (Join-Path $assetsDir "StoreLogo.scale-100.png") 50 50 $null 0
Resize-Image (Join-Path $assetsDir "StoreLogo.scale-125.png") 63 63 $null 0
Resize-Image (Join-Path $assetsDir "StoreLogo.scale-150.png") 75 75 $null 0
Resize-Image (Join-Path $assetsDir "StoreLogo.scale-200.png") 100 100 $null 0
Resize-Image (Join-Path $assetsDir "StoreLogo.scale-400.png") 200 200 $null 0

# 6. Wide310x150Logo Scale Variants
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.scale-100.png") 310 150 $null 0
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.scale-125.png") 388 188 $null 0
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.scale-150.png") 465 225 $null 0
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.scale-200.png") 620 300 $null 0
Resize-Image (Join-Path $assetsDir "Wide310x150Logo.scale-400.png") 1240 600 $null 0

# 7. SplashScreen Scale Variants
Resize-Image (Join-Path $assetsDir "SplashScreen.scale-100.png") 620 300 "#151D24" 10
Resize-Image (Join-Path $assetsDir "SplashScreen.scale-125.png") 775 375 "#151D24" 12
Resize-Image (Join-Path $assetsDir "SplashScreen.scale-150.png") 930 450 "#151D24" 14
Resize-Image (Join-Path $assetsDir "SplashScreen.scale-200.png") 1240 600 "#151D24" 18
Resize-Image (Join-Path $assetsDir "SplashScreen.scale-400.png") 2480 1200 "#151D24" 30

$srcImage.Dispose()

# Step 1: Build the App (Self-contained so it works on any machine without .NET installed)
Write-Host "[3/5] Building self-contained application package (dist\app)..." -ForegroundColor Yellow
$projectPath = Join-Path $rootDir "source\AnyUninstaller.Avalonia\AnyUninstaller.Avalonia.csproj"
& dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for app" }

# Ensure AnyUninstaller.exe is the primary entry point
Copy-Item -Path "$appDir\AnyUninstaller.Avalonia.exe" -Destination "$appDir\AnyUninstaller.exe" -Force
Write-Host " -> App build complete in dist\app" -ForegroundColor Green

# Step 2: Build Standalone EXE
Write-Host "[4/5] Building Standalone EXE distribution (dist\exe)..." -ForegroundColor Yellow
& dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $exeDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for standalone exe" }
Copy-Item -Path "$exeDir\AnyUninstaller.Avalonia.exe" -Destination "$exeDir\AnyUninstaller.exe" -Force

# Publish helper tools as single-file self-contained executables so they run on any Windows 10/11 PC without .NET 8
$helperProjects = @(
    "source\StoreAppHelper\StoreAppHelper.csproj",
    "source\SteamHelper\SteamHelper.csproj",
    "source\OculusHelper\OculusHelper.csproj",
    "source\UniversalUninstaller\UniversalUninstaller.csproj",
    "source\UninstallerAutomatizer\UninstallerAutomatizer.csproj"
)
foreach ($hp in $helperProjects) {
    $fullHp = Join-Path $rootDir $hp
    & dotnet publish $fullHp -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $exeDir
}
Write-Host " -> Standalone EXE & self-contained helpers complete in dist\exe" -ForegroundColor Green

# Step 3: Build Portable Package & Zip
Write-Host "[5/5] Building Portable distribution (dist\portable)..." -ForegroundColor Yellow
$portableFolder = Join-Path $portableDir "AnyUninstaller-$tagVer-Portable"
Copy-Item -Path $appDir -Destination $portableFolder -Recurse -Force

# Add portable marker file so settings remain local
New-Item -ItemType File -Force -Path (Join-Path $portableFolder "portable.dat") | Out-Null
"{}" | Out-File -FilePath (Join-Path $portableFolder "AnyUninstaller_Settings.json") -Encoding UTF8 -Force

# Create Portable ZIP archive using ZipFile (fast & reliable)
$portableZip = Join-Path $portableDir "AnyUninstaller-$tagVer-Portable.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }
Start-Sleep -Milliseconds 500
[System.IO.Compression.ZipFile]::CreateFromDirectory($portableFolder, $portableZip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
Write-Host " -> Portable ZIP created at: $portableZip" -ForegroundColor Green

# Step 4: Build MSIX Package
Write-Host "[6/6] Packaging MSIX for Microsoft Store..." -ForegroundColor Yellow
$msixStaging = Join-Path $distDir "msix_staging"
New-Item -ItemType Directory -Force -Path $msixStaging | Out-Null

# Copy App files
Copy-Item -Path "$appDir\*" -Destination $msixStaging -Recurse -Force

# Copy Assets
$msixStagingAssets = Join-Path $msixStaging "Assets"
New-Item -ItemType Directory -Force -Path $msixStagingAssets | Out-Null
Copy-Item -Path "$assetsDir\*" -Destination $msixStagingAssets -Force

# Copy AppxManifest.xml
$manifestSrc = Join-Path $rootDir "packaging\AppxManifest.xml"
Copy-Item -Path $manifestSrc -Destination (Join-Path $msixStaging "AppxManifest.xml") -Force

# Locate makeappx.exe
$makeAppxPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\App Certification Kit\makeappx.exe"
)

$makeAppx = $null
foreach ($path in $makeAppxPaths) {
    if (Test-Path $path) {
        $makeAppx = $path
        break
    }
}

if (-not $makeAppx) {
    $found = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "makeappx.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $makeAppx = $found.FullName }
}

$outputMsix = Join-Path $msixDir "Saayan.AnyUninstaller_${quadVer}_x64.msix"
if (-not $makeAppx) {
    Write-Warning "makeappx.exe not found in Windows Kits. Please ensure Windows 10/11 SDK is installed."
} else {
    # Compile resources.pri with MakePri to officially register scale & unplated targetsize assets
    $makePri = Join-Path (Split-Path -Parent $makeAppx) "makepri.exe"
    if (-not (Test-Path $makePri)) {
        $foundPri = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter "makepri.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($foundPri) { $makePri = $foundPri.FullName }
    }
    if ($makePri -and (Test-Path $makePri)) {
        Write-Host "Compiling package resources with MakePri..." -ForegroundColor Yellow
        $priConfig = Join-Path $msixStaging "priconfig.xml"
        & $makePri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
        & $makePri new /pr $msixStaging /cf $priConfig /of (Join-Path $msixStaging "resources.pri") /o | Out-Null
        Remove-Item -Path $priConfig -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Using MakeAppx: $makeAppx"
    & $makeAppx pack /o /h SHA256 /d $msixStaging /p $outputMsix
    if ($LASTEXITCODE -eq 0) {
        Write-Host " -> MSIX Package created at: $outputMsix" -ForegroundColor Green
    } else {
        Write-Error "MakeAppx failed with exit code $LASTEXITCODE"
    }
}

# Clean staging directory
Remove-Item -Path $msixStaging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path $assetsDir -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "                     BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "Outputs:"
Write-Host "  1. App Directory:     $appDir" -ForegroundColor White
Write-Host "  2. Standalone EXE:    $exeDir\AnyUninstaller.exe" -ForegroundColor White
Write-Host "  3. Portable ZIP:      $portableZip" -ForegroundColor White
if ($outputMsix -and (Test-Path $outputMsix)) {
    Write-Host "  4. Store MSIX:        $outputMsix" -ForegroundColor White
}
Write-Host "========================================================================" -ForegroundColor Cyan

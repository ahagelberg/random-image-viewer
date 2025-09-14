# Simple script to build Random Image Viewer and create installer
Write-Host "Building Random Image Viewer Installer..." -ForegroundColor Green

# Step 1: Clean and build in Release mode
Write-Host "Step 1: Building application in Release mode..." -ForegroundColor Yellow
dotnet clean
dotnet build -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

# Step 2: Create installer directory
Write-Host "Step 2: Creating installer directory..." -ForegroundColor Yellow
if (!(Test-Path "installer")) {
    New-Item -ItemType Directory -Path "installer"
}

# Step 3: Find Inno Setup
Write-Host "Step 3: Looking for Inno Setup..." -ForegroundColor Yellow
$innoSetupPath = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe",
    "C:\Program Files\Inno Setup 5\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (!$innoSetupPath) {
    Write-Host "Inno Setup not found!" -ForegroundColor Red
    Write-Host "Please install Inno Setup from: https://jrsoftware.org/isinfo.php" -ForegroundColor Cyan
    Write-Host "Or run this script from the Inno Setup installation directory" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Alternative: You can also create a simple ZIP distribution instead of an installer." -ForegroundColor Yellow
    Write-Host "Would you like to create a ZIP distribution instead? (y/n): " -NoNewline -ForegroundColor Cyan
    $response = Read-Host
    if ($response -eq "y" -or $response -eq "Y") {
        Write-Host "Creating ZIP distribution..." -ForegroundColor Yellow
        $zipPath = ".\installer\RandomImageViewer-v1.2.0.zip"
        $sourcePath = ".\RandomImageViewer\bin\Release\net8.0-windows\*"
        
        if (Test-Path $sourcePath) {
            Compress-Archive -Path $sourcePath -DestinationPath $zipPath -Force
            Write-Host "SUCCESS! ZIP distribution created: $zipPath" -ForegroundColor Green
        } else {
            Write-Host "Build output not found at: $sourcePath" -ForegroundColor Red
        }
        Read-Host "Press Enter to exit"
        exit 0
    } else {
        Read-Host "Press Enter to exit"
        exit 1
    }
}

Write-Host "Found Inno Setup at: $innoSetupPath" -ForegroundColor Green

# Step 4: Create installer
Write-Host "Step 4: Creating installer..." -ForegroundColor Yellow
& $innoSetupPath "RandomImageViewer.iss"

if ($LASTEXITCODE -eq 0) {
    Write-Host "SUCCESS! Installer created successfully!" -ForegroundColor Green
    Write-Host "Installer location: installer\RandomImageViewer-Setup.exe" -ForegroundColor Cyan
} else {
    Write-Host "Installer creation failed!" -ForegroundColor Red
}

Read-Host "Press Enter to exit"
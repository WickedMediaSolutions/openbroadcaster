# OpenBroadcaster Docker Build Script for Windows
# Tests Linux version in Docker from Windows

# Configuration
$AppVersion = "4.4"
$AppName = "openbroadcaster"
$LinuxImage = "${AppName}:${AppVersion}-linux"
$DockerBuildContext = "."

Write-Host "========================================" -ForegroundColor Green
Write-Host "OpenBroadcaster Docker Linux Build"     -ForegroundColor Green
Write-Host "Version: $AppVersion"                   -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Check if Docker is installed
Write-Host "[*] Checking Docker installation..." -ForegroundColor Cyan
$docker = iex "docker --version 2>&1"
if ($LASTEXITCODE -ne 0) {
    Write-Host "[!] ERROR: Docker is not installed or not running" -ForegroundColor Red
    Write-Host "    Please install Docker Desktop and ensure it's running" -ForegroundColor Red
    exit 1
}
Write-Host "[+] Docker found: $docker" -ForegroundColor Green
Write-Host ""

# Check Docker daemon
Write-Host "[*] Checking Docker daemon..." -ForegroundColor Cyan
try {
    $null = iex "docker ps 2>&1"
    if ($LASTEXITCODE -ne 0) {
        throw "Docker daemon not responding"
    }
    Write-Host "[+] Docker daemon is running" -ForegroundColor Green
} catch {
    Write-Host "[!] ERROR: Docker daemon is not responding" -ForegroundColor Red
    Write-Host "    Please start Docker Desktop" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Build Linux image
Write-Host "[*] Building Linux Docker image: $LinuxImage" -ForegroundColor Cyan
Write-Host "    This will compile the .NET 8.0 application for Linux" -ForegroundColor Gray
Write-Host ""

$buildCmd = "docker build -f Dockerfile.linux -t $LinuxImage --build-arg VERSION=$AppVersion ."
Write-Host "$buildCmd" -ForegroundColor Yellow
Write-Host ""

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

try {
    iex $buildCmd
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] ERROR: Docker build failed" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[!] ERROR: $($_)" -ForegroundColor Red
    exit 1
}

$stopwatch.Stop()
Write-Host ""
Write-Host "[+] Build completed successfully in $($stopwatch.Elapsed.TotalSeconds) seconds" -ForegroundColor Green
Write-Host ""

# Verify image
Write-Host "[*] Verifying Docker image..." -ForegroundColor Cyan
$imageInfo = iex "docker images $AppName`*"
Write-Host $imageInfo
Write-Host ""

# Create volumes and directories
Write-Host "[*] Setting up volumes and directories..." -ForegroundColor Cyan
$configDir = "$(Get-Location)\config"
$logsDir = "$(Get-Location)\logs"
$dataDir = "$(Get-Location)\data"

foreach ($dir in @($configDir, $logsDir, $dataDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "[+] Created: $dir" -ForegroundColor Green
    } else {
        Write-Host "[+] Exists: $dir" -ForegroundColor Green
    }
}
Write-Host ""

# Summary and next steps
Write-Host "========================================" -ForegroundColor Green
Write-Host "Build Complete!"                       -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Image:" -ForegroundColor Yellow
Write-Host "  $LinuxImage"
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "  1. Run the application:" -ForegroundColor White
Write-Host "     .\scripts\docker-run-linux.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Or use Docker Compose:" -ForegroundColor White
Write-Host "     docker-compose up linux" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. To test in headless mode:" -ForegroundColor White
Write-Host "     docker run -it $LinuxImage" -ForegroundColor Gray
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "  Config Directory:  $configDir"
Write-Host "  Logs Directory:    $logsDir"
Write-Host "  Data Directory:    $dataDir"
Write-Host ""

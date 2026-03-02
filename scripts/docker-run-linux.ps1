# OpenBroadcaster Docker Run Script for Linux
# Runs the containerized Linux version from Windows

param(
    [switch]$Interactive = $false,
    [switch]$GUI = $false,
    [string]$DisplayNumber = ":0",
    [int]$OverlayPort = 9750
)

# Configuration
$AppVersion = "4.4"
$AppName = "openbroadcaster"
$LinuxImage = "${AppName}:${AppVersion}-linux"
$ContainerName = "${AppName}-linux-test"

Write-Host "========================================" -ForegroundColor Green
Write-Host "OpenBroadcaster Linux Container Runtime" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

# Check if image exists
Write-Host "[*] Checking if Docker image exists..." -ForegroundColor Cyan
$imageExists = iex "docker images -q $LinuxImage" 2>&1
if ([string]::IsNullOrWhiteSpace($imageExists)) {
    Write-Host "[!] ERROR: Docker image not found: $LinuxImage" -ForegroundColor Red
    Write-Host "    Please run: .\scripts\docker-build-linux.ps1" -ForegroundColor Red
    exit 1
}
Write-Host "[+] Image found: $LinuxImage" -ForegroundColor Green
Write-Host ""

# Remove existing container if running
Write-Host "[*] Checking for existing containers..." -ForegroundColor Cyan
$existingContainer = iex "docker ps -a -q -f name=$ContainerName" 2>&1
if (-not [string]::IsNullOrWhiteSpace($existingContainer)) {
    Write-Host "[*] Removing existing container..." -ForegroundColor Yellow
    iex "docker rm -f $ContainerName" | Out-Null
    Write-Host "[+] Removed existing container" -ForegroundColor Green
}
Write-Host ""

# Setup directories
$configDir = "$(Get-Location)\config"
$logsDir = "$(Get-Location)\logs"
$dataDir = "$(Get-Location)\data"

foreach ($dir in @($configDir, $logsDir, $dataDir)) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

# Build docker run command
$dockerRunCmd = @(
    "docker", "run"
    "-d"  # Detached mode
    "--name", $ContainerName
    "-p", "${OverlayPort}:9750"  # Overlay API port
    "-v", "${configDir}:/app/config"
    "-v", "${logsDir}:/app/logs"
    "-v", "${dataDir}:/app/data"
    "-e", "HOME=/app"
    "-e", "APPDATA=/app/config"
    "-e", "XDG_RUNTIME_DIR=/run/user/0"
)

# Interactive mode for testing/debugging
if ($Interactive) {
    Write-Host "[*] Starting in INTERACTIVE mode" -ForegroundColor Yellow
    $dockerRunCmd += @("-it", "--entrypoint", "/bin/bash")
} 
# GUI mode (requires X11 forwarding or VNC)
elseif ($GUI) {
    Write-Host "[*] Starting with GUI support (X11 forwarding)" -ForegroundColor Yellow
    $dockerRunCmd += @(
        "-e", "DISPLAY=$DisplayNumber"
        "-v", "/tmp/.X11-unix:/tmp/.X11-unix"
    )
}
# Default: headless mode
else {
    Write-Host "[*] Starting in HEADLESS mode" -ForegroundColor Yellow
    Write-Host "    The application will run with audio/API only, no GUI" -ForegroundColor Gray
}

$dockerRunCmd += $LinuxImage

# Run the container
Write-Host "[*] Starting Docker container..." -ForegroundColor Cyan
Write-Host ""

try {
    $command = $dockerRunCmd -join " "
    Write-Host "[CMD] $command" -ForegroundColor Gray
    Write-Host ""
    
    iex ($command)
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[!] ERROR: Failed to start container" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[!] ERROR: $($_)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Container Started Successfully!"         -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Container Information:" -ForegroundColor Cyan
Write-Host "  Name:              $ContainerName"
Write-Host "  Image:             $LinuxImage"
Write-Host "  Overlay API Port:  $OverlayPort"
Write-Host ""
Write-Host "Volume Mounts:" -ForegroundColor Cyan
Write-Host "  Config:  $configDir -> /app/config"
Write-Host "  Logs:    $logsDir -> /app/logs"
Write-Host "  Data:    $dataDir -> /app/data"
Write-Host ""
Write-Host "Useful Commands:" -ForegroundColor Cyan
Write-Host "  View logs:       docker logs -f $ContainerName"
Write-Host "  Shell access:    docker exec -it $ContainerName /bin/bash"
Write-Host "  Stop container:  docker stop $ContainerName"
Write-Host "  Remove container: docker rm $ContainerName"
Write-Host ""
if ($Interactive) {
    Write-Host "You are now in the container shell. To exit, type 'exit'" -ForegroundColor Yellow
    Write-Host ""
}

# Show initial logs
Write-Host "[*] Initial logs (tail -20):" -ForegroundColor Cyan
Write-Host ""
Start-Sleep -Milliseconds 1000
iex "docker logs --tail 20 $ContainerName"

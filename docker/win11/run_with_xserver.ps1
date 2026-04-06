# Script to run Diffracta Docker container with X server support
# Make sure XLaunch (VcXsrv) is running before executing this script

Write-Host "=== Diffracta Docker Runner ===" -ForegroundColor Green
Write-Host ""

# Check if XLaunch is running
$xlaunchRunning = Get-Process -Name "vcxsrv" -ErrorAction SilentlyContinue
if (-not $xlaunchRunning) {
    Write-Host "WARNING: XLaunch (VcXsrv) does not appear to be running!" -ForegroundColor Yellow
    Write-Host "Please start XLaunch with the following settings:" -ForegroundColor Yellow
    Write-Host "  1. Select 'Multiple windows'" -ForegroundColor Cyan
    Write-Host "  2. Select 'Start no client'" -ForegroundColor Cyan
    Write-Host "  3. CHECK 'Disable access control' (IMPORTANT!)" -ForegroundColor Cyan
    Write-Host "  4. Finish and start the server" -ForegroundColor Cyan
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/n)"
    if ($continue -ne "y") {
        exit 1
    }
}

# Get host IP addresses
Write-Host "Detecting network configuration" -ForegroundColor Yellow
$ipAddresses = (ipconfig | findstr IPv4) -replace '.*: ', ''
$mainIP = ($ipAddresses | Where-Object { $_ -like "192.168.*" -or $_ -like "10.*" } | Select-Object -First 1)

if (-not $mainIP) {
    $mainIP = $ipAddresses[0]
}

Write-Host "Found IP addresses: $($ipAddresses -join ', ')" -ForegroundColor Cyan
Write-Host "Using: $mainIP" -ForegroundColor Green
Write-Host ""

# Build the Docker image first
Write-Host "Building Docker image" -ForegroundColor Yellow
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
docker build -f "$scriptPath\Dockerfile" -t diffracta:latest ..

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host ""

# Try different network modes
Write-Host "Attempting to run container" -ForegroundColor Yellow
Write-Host ""

# Try different DISPLAY options
$displayOptions = @(
    @{ Name = "localhost"; Value = "localhost:0.0" },
    @{ Name = "host.docker.internal"; Value = "host.docker.internal:0.0" },
    @{ Name = "IP address"; Value = "$mainIP`:0.0" }
)

$success = $false

foreach ($displayOption in $displayOptions) {
    Write-Host "Trying with host network mode (DISPLAY=$($displayOption.Value))" -ForegroundColor Cyan
    
    docker run --rm `
        --network host `
        -e DISPLAY="$($displayOption.Value)" `
        -e LIBGL_ALWAYS_INDIRECT=1 `
        diffracta:latest
    
    $exitCode = $LASTEXITCODE
    
    if ($exitCode -eq 0) {
        $success = $true
        break
    }
    
    Write-Host "Failed with $($displayOption.Name) (exit code: $exitCode)" -ForegroundColor Yellow
    
    # Check for X11 authorization error
    if ($exitCode -eq 139 -or $exitCode -eq 1) {
        Write-Host ""
        Write-Host "=== X11 CONNECTION ERROR ===" -ForegroundColor Red
        Write-Host "The error 'Authorization required' means XLaunch is rejecting the connection." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "VERIFY XLaunch Configuration:" -ForegroundColor Cyan
        Write-Host "1. Right-click XLaunch icon in system tray" -ForegroundColor White
        Write-Host "2. Select 'Exit' to close it" -ForegroundColor White
        Write-Host "3. Start XLaunch from Start menu" -ForegroundColor White
        Write-Host "4. In the configuration wizard:" -ForegroundColor White
        Write-Host "   - Step 1: Select 'Multiple windows'" -ForegroundColor White
        Write-Host "   - Step 2: Select 'Start no client'" -ForegroundColor White
        Write-Host "   - Step 3: CHECK 'Disable access control' (CRITICAL!)" -ForegroundColor Yellow
        Write-Host "   - Step 4: Finish" -ForegroundColor White
        Write-Host ""
        Write-Host "5. Verify XLaunch icon appears in system tray" -ForegroundColor White
        Write-Host "6. Run this script again" -ForegroundColor White
        Write-Host ""
        
        # Ask if user wants to continue trying other options
        if ($displayOption -ne $displayOptions[-1]) {
            Write-Host "Trying next DISPLAY option..." -ForegroundColor Yellow
            Write-Host ""
        }
    }
}

if (-not $success) {
    Write-Host ""
    Write-Host "Trying bridge mode as final fallback..." -ForegroundColor Yellow
    Write-Host ""
    
    # Final fallback to bridge mode with localhost
    docker run --rm `
        -e DISPLAY="host.docker.internal:0.0" `
        -e LIBGL_ALWAYS_INDIRECT=1 `
        --add-host=host.docker.internal:host-gateway `
        diffracta:latest
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "=== ALL ATTEMPTS FAILED ===" -ForegroundColor Red
        Write-Host ""
        Write-Host "The application REST API is running at http://localhost:5000" -ForegroundColor Green
        Write-Host "But the GUI cannot connect to XLaunch." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Please ensure XLaunch is running with 'Disable access control' enabled." -ForegroundColor Yellow
        exit 1
    }
}


# Diffracta Application Launcher

$ErrorActionPreference = 'Stop'

# Set execution policy for this process (Windows only)
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
}

Write-Host "=== Diffracta ===" -ForegroundColor Green

# Function to cleanup on exit
function Cleanup {
    Write-Host "Cleaning up processes" -ForegroundColor Yellow
    # Kill any lingering dotnet processes for this project
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*Diffracta*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
}

# Register cleanup on script exit (suppress job output)
$null = Register-EngineEvent PowerShell.Exiting -Action { Cleanup }

# Check if .NET is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Yellow
} catch {
    Write-Host "Error: .NET SDK not found." -ForegroundColor Red
    exit 1
}

# Check if project exists
if (-not (Test-Path "src/App/Diffracta.csproj")) {
    Write-Host "Error: Project file not found." -ForegroundColor Red
    exit 1
}

# Clean up any existing Diffracta processes before starting
Write-Host "Cleaning up any existing Diffracta processes" -ForegroundColor Yellow
$existingProcesses = Get-Process -Name "Diffracta" -ErrorAction SilentlyContinue
if ($existingProcesses) {
    $existingProcesses | ForEach-Object {
        Write-Host "Stopping existing Diffracta process (PID: $($_.Id))" -ForegroundColor Yellow
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    # Wait for processes to fully terminate and release file locks
    # Windows needs a moment to release .exe and .pdb file handles
    Start-Sleep -Milliseconds 1500
    Write-Host "Cleanup complete" -ForegroundColor Green
}


try {
    # Restore packages to local cache first
    Write-Host "Restoring packages to local cache" -ForegroundColor Yellow
    dotnet restore src/App/Diffracta.csproj --packages ./cache

    Write-Host "Building project" -ForegroundColor Yellow
    dotnet build src/App/Diffracta.csproj
    
    # Run the application
    Write-Host "Launching application" -ForegroundColor Yellow
    dotnet watch run --project src/App/Diffracta.csproj
}
catch {
    Write-Host "Error running application: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "Application finished" -ForegroundColor Green

# Avalonia Video Synth Application Launcher
# Single script that handles everything with proper privileges

$ErrorActionPreference = 'Stop'


# Set execution policy for this process (Windows only)
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
}

Write-Host "=== Avalonia Video Synth (GLSL) ===" -ForegroundColor Green

# Function to cleanup on exit
function Cleanup {
    Write-Host "Cleaning up processes" -ForegroundColor Yellow
    # Kill any lingering dotnet processes for this project
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
        $_.CommandLine -like "*AvaloniaVideoSynth*"
    } | Stop-Process -Force -ErrorAction SilentlyContinue
}

# Register cleanup on script exit
Register-EngineEvent PowerShell.Exiting -Action { Cleanup }

# Check if .NET is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Yellow
} catch {
    Write-Host "Error: .NET SDK not found. Please run .\update.ps1 first to set up the environment." -ForegroundColor Red
    exit 1
}

# Check if project exists
if (-not (Test-Path "src/App/AvaloniaVideoSynth.csproj")) {
    Write-Host "Error: Project file not found. Please run this script from the project root directory." -ForegroundColor Red
    exit 1
}

# Ensure Shaders directory exists
$shadersDir = "src/App/Shaders"
if (-not (Test-Path $shadersDir)) {
    Write-Host "Creating Shaders directory: $shadersDir" -ForegroundColor Yellow
}

try {

    # Restore packages to local cache first
    Write-Host "Restoring packages to local cache" -ForegroundColor Yellow
    dotnet restore src/App/AvaloniaVideoSynth.csproj --packages ./cache

    Write-Host "Building project" -ForegroundColor Yellow
    dotnet build src/App/AvaloniaVideoSynth.csproj
    
    # Run the application
    Write-Host "Launching application" -ForegroundColor Yellow
    dotnet run --project src/App/AvaloniaVideoSynth.csproj
}
catch {
    Write-Host "Error running application: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Try running .\update.ps1 to set up the environment first." -ForegroundColor Yellow
    exit 1
}

Write-Host "Application finished" -ForegroundColor Green

# Avalonia Video Synth Application Launcher
# Single script that handles everything with proper privileges

$ErrorActionPreference = 'Stop'

# Create logs directory if it doesn't exist
if (-not (Test-Path "logs")) {
    New-Item -ItemType Directory -Path "logs" -Force | Out-Null
}

# Function to write timestamped logs
function Write-Log {
    param($Message, $Level = "INFO")
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $logEntry = "[$timestamp] [$Level] $Message"
    Write-Host $logEntry
    Add-Content -Path "logs/startup.log" -Value $logEntry
}

# Set execution policy for this process (Windows only)
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
}

Write-Log "=== Avalonia Video Synth (GLSL) ===" "INFO"

# Function to cleanup on exit
function Cleanup {
    Write-Log "Cleaning up processes" "INFO"
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
    Write-Log "Error: Project file not found. Please run this script from the project root directory." "ERROR"
    exit 1
}

# Ensure Shaders directory exists
$shadersDir = "src/App/Shaders"
if (-not (Test-Path $shadersDir)) {
    Write-Log "Creating Shaders directory: $shadersDir" "INFO"
}

try {
    # Restore packages first
    Write-Host "Restoring packages" -ForegroundColor Yellow
    dotnet restore src/App/AvaloniaVideoSynth.csproj

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

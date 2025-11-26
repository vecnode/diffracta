# Update and Setup Folder Script for Diffracta Application
# Uses local package cache in ./cache directory

Write-Host "=== Diffracta - Environment Setup ===" -ForegroundColor Green

# Create local cache directory if it doesn't exist
if (-not (Test-Path "cache")) {
    Write-Host "Creating local package cache directory" -ForegroundColor Yellow
    New-Item -ItemType Directory -Path "cache" -Force | Out-Null
    Write-Host "Local cache created: ./cache" -ForegroundColor Green
} else {
    Write-Host "Local cache directory exists: ./cache" -ForegroundColor Green
}

# Check if .NET is installed
Write-Host "Checking .NET installation" -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host ".NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host ".NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/download" -ForegroundColor Red
    Write-Host "After installing .NET, run this script again." -ForegroundColor Yellow
    exit 1
}

# Check if we're in the correct directory
if (-not (Test-Path "src/App/Diffracta.csproj")) {
    Write-Host "Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

Write-Host "`nChecking project dependencies" -ForegroundColor Yellow

# Restore NuGet packages to local cache
Write-Host "Restoring NuGet packages to local cache" -ForegroundColor Yellow
try {
    # First, download packages to local cache
    Write-Host "Downloading packages to ./cache directory" -ForegroundColor Cyan
    dotnet restore src/App/Diffracta.csproj --packages ./cache
    Write-Host "Packages restored to local cache successfully" -ForegroundColor Green
} catch {
    Write-Host "Failed to restore packages: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# List installed packages and extract package names
Write-Host "`nInstalled packages:" -ForegroundColor Yellow
$packageListOutput = $null
try {
    $packageListOutput = dotnet list src/App/Diffracta.csproj package
    $packageListOutput | Write-Host
} catch {
    Write-Host "Could not list packages: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Extract required packages from dotnet list output
Write-Host "`nChecking for required packages" -ForegroundColor Yellow
$requiredPackages = @()

if ($packageListOutput) {
    # Parse the output: lines starting with ">" contain package references
    # Format: "> PackageName    Version    Version"
    foreach ($line in $packageListOutput) {
        if ($line -match '^\s*>\s+(\S+)') {
            $packageName = $matches[1]
            if ($packageName -and $packageName -ne "Top-level") {
                $requiredPackages += $packageName
            }
        }
    }
}

if ($requiredPackages.Count -eq 0) {
    Write-Host "Warning: Could not extract package names from dotnet list output" -ForegroundColor Yellow
    Write-Host "Falling back to manual package check" -ForegroundColor Yellow
    # Fallback: check if packages exist in .csproj by trying to list them
    $fallbackPackages = @("Avalonia", "Avalonia.Desktop", "Avalonia.Themes.Fluent", "Avalonia.Diagnostics", "FFMpegCore", "Melanchall.DryWetMidi")
    foreach ($pkg in $fallbackPackages) {
        $found = $packageListOutput | Select-String -Pattern "^\s*>\s+$pkg\s"
        if ($found) {
            $requiredPackages += $pkg
        }
    }
}

if ($requiredPackages.Count -eq 0) {
    Write-Host "Error: No packages found. Please check your .csproj file." -ForegroundColor Red
    exit 1
}

Write-Host "Found $($requiredPackages.Count) package(s) in project:" -ForegroundColor Cyan
foreach ($package in $requiredPackages) {
    Write-Host "  - $package" -ForegroundColor Green
}

# Build the project
Write-Host "`nBuilding project" -ForegroundColor Yellow
try {
    dotnet build src/App/Diffracta.csproj --configuration Release
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Project built successfully" -ForegroundColor Green
    } else {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check the error messages above and fix any issues." -ForegroundColor Yellow
    exit 1
}

Write-Host "`n=== Environment Setup Complete ===" -ForegroundColor Green
Write-Host "Packages are now stored locally in: .\cache" -ForegroundColor Cyan
Write-Host "You can now run the application using: .\start.bat" -ForegroundColor Cyan
Write-Host "Or directly with: dotnet run --project src/App/Diffracta.csproj" -ForegroundColor Cyan



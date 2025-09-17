# Update and Setup Folder Script for Avalonia Video Synth Application
# Uses local package cache in ./cache directory

Write-Host "=== Avalonia Video Synth - Environment Setup ===" -ForegroundColor Green

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
if (-not (Test-Path "src/App/AvaloniaVideoSynth.csproj")) {
    Write-Host "Please run this script from the project root directory" -ForegroundColor Red
    exit 1
}

Write-Host "`nChecking project dependencies" -ForegroundColor Yellow

# Restore NuGet packages to local cache
Write-Host "Restoring NuGet packages to local cache" -ForegroundColor Yellow
try {
    # First, download packages to local cache
    Write-Host "Downloading packages to ./cache directory" -ForegroundColor Cyan
    dotnet restore src/App/AvaloniaVideoSynth.csproj --packages ./cache
    Write-Host "Packages restored to local cache successfully" -ForegroundColor Green
} catch {
    Write-Host "Failed to restore packages: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# List installed packages
Write-Host "`nInstalled packages:" -ForegroundColor Yellow
try {
    dotnet list src/App/AvaloniaVideoSynth.csproj package
} catch {
    Write-Host "Could not list packages: $($_.Exception.Message)" -ForegroundColor Red
}

# Check for required packages
Write-Host "`nChecking for required packages" -ForegroundColor Yellow
$requiredPackages = @(
    "Avalonia",
    "Avalonia.Desktop",
    "Avalonia.Themes.Fluent",
    "Avalonia.Diagnostics"
)

$missingPackages = @()
foreach ($package in $requiredPackages) {
    try {
        $result = dotnet list src/App/AvaloniaVideoSynth.csproj package | Select-String $package
        if ($result) {
            Write-Host "$package" -ForegroundColor Green
        } else {
            $missingPackages += $package
            Write-Host "$package (missing)" -ForegroundColor Red
        }
    } catch {
        $missingPackages += $package
        Write-Host "$package (check failed)" -ForegroundColor Red
    }
}

if ($missingPackages.Count -gt 0) {
    Write-Host "`nInstalling missing packages to local cache" -ForegroundColor Yellow
    foreach ($package in $missingPackages) {
        try {
            Write-Host "Installing $package to local cache" -ForegroundColor Yellow
            dotnet add src/App/AvaloniaVideoSynth.csproj package $package --packages ./cache
            Write-Host "$package installed to local cache" -ForegroundColor Green
        } catch {
            Write-Host "Failed to install $package`: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

# Build the project
Write-Host "`nBuilding project" -ForegroundColor Yellow
try {
    dotnet build src/App/AvaloniaVideoSynth.csproj --configuration Release
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Project built successfully" -ForegroundColor Green
    } else {
        Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
        Write-Host "Common issues:" -ForegroundColor Yellow
        Write-Host "- FluentTheme syntax errors (fixed in this version)" -ForegroundColor Cyan
        Write-Host "- OpenGL method signature mismatches (fixed in this version)" -ForegroundColor Cyan
        Write-Host "- Missing using statements (fixed in this version)" -ForegroundColor Cyan
        exit 1
    }
} catch {
    Write-Host "Build failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Please check the error messages above and fix any issues." -ForegroundColor Yellow
    exit 1
}

Write-Host "`n=== Environment Setup Complete ===" -ForegroundColor Green
Write-Host "Packages are now stored locally in: ./cache" -ForegroundColor Cyan
Write-Host "You can now run the application using: .\start.ps1" -ForegroundColor Cyan
Write-Host "Or directly with: dotnet run --project src/App/AvaloniaVideoSynth.csproj" -ForegroundColor Cyan


# Update and Setup Folder Script for Avalonia Video Synth Application
# not tested

Write-Host "=== Avalonia Video Synth - Environment Setup ===" -ForegroundColor Green

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

# Restore NuGet packages
Write-Host "Restoring NuGet packages" -ForegroundColor Yellow
try {
    dotnet restore src/App/AvaloniaVideoSynth.csproj
    Write-Host "Packages restored successfully" -ForegroundColor Green
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
    Write-Host "`nInstalling missing packages" -ForegroundColor Yellow
    foreach ($package in $missingPackages) {
        try {
            Write-Host "Installing $package" -ForegroundColor Yellow
            dotnet add src/App/AvaloniaVideoSynth.csproj package $package
            Write-Host "$package installed" -ForegroundColor Green
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
Write-Host "You can now run the application using: .\start.ps1" -ForegroundColor Cyan
Write-Host "Or directly with: dotnet run --project src/App/AvaloniaVideoSynth.csproj" -ForegroundColor Cyan

Write-Host "`n=== Avalonia OpenGL Shader Engine ===" -ForegroundColor Yellow
Write-Host "Avalonia provides cross-platform OpenGL functionality:" -ForegroundColor White
Write-Host "Windows: Uses native OpenGL drivers" -ForegroundColor Cyan
Write-Host "Linux: Uses Mesa/OpenGL drivers" -ForegroundColor Cyan
Write-Host "macOS: Uses Core OpenGL" -ForegroundColor Cyan
Write-Host "`nNote: Shaders are loaded from the Shaders/ folder and auto-reloaded on changes." -ForegroundColor Green

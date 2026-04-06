#!/usr/bin/env bash
# Diffracta - Environment Setup (Linux/macOS)
set -e

echo "=== Diffracta - Environment Setup ==="

# Create local cache directory if it doesn't exist
if [ ! -d "cache" ]; then
    echo "Creating local package cache directory"
    mkdir -p cache
    echo "Local cache created: ./cache"
else
    echo "Local cache directory exists: ./cache"
fi

# Check .NET SDK
echo "Checking .NET installation"
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found."
    echo "Install .NET 8: https://dotnet.microsoft.com/download"
    exit 1
fi
DOTNET_VER=$(dotnet --version)
echo ".NET SDK found: $DOTNET_VER"

# Check FFmpeg (required by FFMpegCore at runtime)
echo "Checking FFmpeg installation"
if ! command -v ffmpeg &> /dev/null; then
    echo "WARNING: ffmpeg not found in PATH."
    echo "  Ubuntu/Debian : sudo apt install ffmpeg"
    echo "  Fedora        : sudo dnf install ffmpeg"
    echo "  Arch          : sudo pacman -S ffmpeg"
    echo "  macOS (brew)  : brew install ffmpeg"
    echo "The app will build but video features will fail at runtime without ffmpeg."
else
    echo "FFmpeg found: $(ffmpeg -version 2>&1 | head -1)"
fi

# Check we're in the repo root
if [ ! -f "src/App/Diffracta.csproj" ]; then
    echo "ERROR: Please run this script from the project root directory."
    exit 1
fi

echo "Checking project dependencies"

# Restore NuGet packages to local cache
echo "Restoring NuGet packages to local cache"
dotnet restore src/App/Diffracta.csproj --packages ./cache
echo "Packages restored to local cache successfully"

# List installed packages
echo ""
echo "Installed packages:"
dotnet list src/App/Diffracta.csproj package || true

echo ""
echo "Note: ASP.NET Core is included as a FrameworkReference (not a PackageReference)"

# Build
echo ""
echo "Building project"
dotnet build src/App/Diffracta.csproj --configuration Release
echo "Project built successfully"

echo ""
echo "=== Environment Setup Complete ==="
echo "Packages stored locally in: ./cache"
echo "Run the application with: ./scripts/start_app.sh"
echo "Or directly with         : dotnet run --project src/App/Diffracta.csproj"

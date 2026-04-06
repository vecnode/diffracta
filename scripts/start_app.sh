#!/usr/bin/env bash
# AvaloniaGlslPipeline Application Launcher (Linux/macOS)
set -e

echo "=== AvaloniaGlslPipeline ==="

# Cleanup helper — kill any lingering AvaloniaGlslPipeline processes
cleanup() {
    echo "Cleaning up processes"
    pkill -f "AvaloniaGlslPipeline" 2>/dev/null || true
}
trap cleanup EXIT

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found."
    exit 1
fi
echo "Using .NET SDK: $(dotnet --version)"

# Check we're in the repo root
if [ ! -f "src/App/AvaloniaGlslPipeline.csproj" ]; then
    echo "ERROR: Project file not found. Run from the project root directory."
    exit 1
fi

# Kill any existing AvaloniaGlslPipeline instance
echo "Cleaning up any existing AvaloniaGlslPipeline processes"
pkill -f "AvaloniaGlslPipeline" 2>/dev/null && sleep 0.5 && echo "Cleanup complete" || echo "No existing processes found"

# Restore packages
echo "Restoring packages to local cache"
dotnet restore src/App/AvaloniaGlslPipeline.csproj --packages ./cache

# Build
echo "Building project"
dotnet build src/App/AvaloniaGlslPipeline.csproj

# Run
echo "Launching application"
dotnet watch run --project src/App/AvaloniaGlslPipeline.csproj

echo "Application finished"

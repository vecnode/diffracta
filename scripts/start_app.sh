#!/usr/bin/env bash
# Diffracta Application Launcher (Linux/macOS)
set -e

echo "=== Diffracta ==="

# Cleanup helper — kill any lingering Diffracta processes
cleanup() {
    echo "Cleaning up processes"
    pkill -f "Diffracta" 2>/dev/null || true
}
trap cleanup EXIT

# Check .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "ERROR: .NET SDK not found."
    exit 1
fi
echo "Using .NET SDK: $(dotnet --version)"

# Check we're in the repo root
if [ ! -f "src/App/Diffracta.csproj" ]; then
    echo "ERROR: Project file not found. Run from the project root directory."
    exit 1
fi

# Kill any existing Diffracta instance
echo "Cleaning up any existing Diffracta processes"
pkill -f "Diffracta" 2>/dev/null && sleep 0.5 && echo "Cleanup complete" || echo "No existing processes found"

# Restore packages
echo "Restoring packages to local cache"
dotnet restore src/App/Diffracta.csproj --packages ./cache

# Build
echo "Building project"
dotnet build src/App/Diffracta.csproj

# Run
echo "Launching application"
dotnet watch run --project src/App/Diffracta.csproj

echo "Application finished"

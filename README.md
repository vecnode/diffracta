# Diffracta

[Under heavy development]

This repository contains Diffracta, a desktop application for live video editing and cinema.  

### Build desktop app on Windows 11

```powershell
# Option 1: Automated Build (Recommended)
# First time setup
.\update.ps1
.\start.ps1
# Option 2: Direct .NET Commands
dotnet restore src/App/Diffracta.csproj
dotnet build src/App/Diffracta.csproj
dotnet run --project src/App/Diffracta.csproj

# Publish Release build for Windows x64
dotnet publish src/App/Diffracta.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ./publish/win-x64
# Publish as single executable
dotnet publish src/App/Diffracta.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish/win-x64-single

# Publish for Linux x64
dotnet publish src/App/Diffracta.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -o ./publish/linux-x64
# Publish for macOS (Intel)
dotnet publish src/App/Diffracta.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=false -o ./publish/osx-x64
# Publish for macOS (Apple Silicon)
dotnet publish src/App/Diffracta.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=false -o ./publish/osx-arm64
```

### Requirements

It is built using .NET and [Avalonia UI](https://avaloniaui.net/) 

```
[net8.0]: 
Packages in use:
•  Avalonia 11.3.5
•  Avalonia.Desktop 11.3.5
•  Avalonia.Diagnostics 11.3.5
•  Avalonia.Themes.Fluent 11.3.5
•  FFMpegCore 5.4.0
•  Melanchall.DryWetMidi 8.0.2

Included as FrameworkReference: 
•  ASP.NET Core (REST API)
```

### Docker on Windows 11/WSL

Prerequisites:
1. Install an X server on Windows (required for GUI display):
   - VcXsrv

2. Configure X server:
   - Start VcXsrv/X410/Xming
   - **Important**: Enable "Disable access control" or "Allow connections from network clients"
   - Display number is usually `:0` (default)

```sh
# Troubleshooting: If Docker Desktop does not detect a Hypervisor, run PowerShell as Administrator:
# Enable Windows Subsystem for Linux
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
# Enable Virtual Machine Platform (required for WSL2)
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
# Set WSL2 as default
wsl --set-default-version 2
# Restart your computer
Restart-Computer

# Navigate to docker folder
cd docker/
# Build the image (using parent directory as build context)
docker build -f Dockerfile -t diffracta:latest ..

# Run the container (EASIEST - use the helper script):
.\run_with_xserver.ps1

# OR manually run with your IP address:
# Get your Windows host IP
ipconfig | findstr IPv4
# Run with host network mode (best compatibility):
# Replace <YOUR_IP> with your actual IP address from the previous command
docker run --rm --network host -e DISPLAY='<YOUR_IP>:0.0' -e LIBGL_ALWAYS_INDIRECT=1 diffracta:latest
```

**Note**: Running GUI applications in Docker on Windows requires an X server. The application will start its REST API server, but the GUI window will only appear if X11 forwarding is properly configured.


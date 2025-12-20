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

```sh
# If Docker Desktop does not detect a Hypervisor and stops running run PowerShell as Administrator:
dism.exe /online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart
# Enable Virtual Machine Platform (required for WSL2)
dism.exe /online /enable-feature /featurename:VirtualMachinePlatform /all /norestart
# Set WSL2 as default
wsl --set-default-version 2
# Restart your computer
Restart-Computer

# Build the image and launch the app
# 1) Build the image
docker build -t diffracta:latest .
# 5) Run the container (prefer host.docker.internal; fall back to host IP)
docker run --rm -e DISPLAY='host.docker.internal:0.0' -e LIBGL_ALWAYS_INDIRECT=1 diffracta:latest
```


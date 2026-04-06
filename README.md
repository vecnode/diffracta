# Diffracta

[Under heavy development]

This repository contains a dotnet desktop application with Avalonia for GLSL rendering.  


### Reproduce

```sh
# Ubuntu
./scripts/setup_environment.sh   # first time
./scripts/start_app.sh           # to launch

# Windows 11
dotnet restore src/App/Diffracta.csproj
dotnet build src/App/Diffracta.csproj
dotnet run --project src/App/Diffracta.csproj

```



### Docker on Windows 11/WSL

Prerequisites:
1. Install an X server on Windows required for GUI display (VcXsrv)
2. Configure X server:
   - Start VcXsrv/X410/Xming
   - Important: Enable "Disable access control" or "Allow connections from network clients"
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
# Run with host network mode (best compatibility):
docker run --rm --network host -e DISPLAY='<YOUR_IP>:0.0' -e LIBGL_ALWAYS_INDIRECT=1 diffracta:latest
```

Running GUI applications in Docker on Windows requires an X server. The application will start its REST API server, but the GUI window will only appear if X11 forwarding is properly configured.

### REST API

- `GET /` - Get API information
- `GET /api/shader/list` - List all available shader files
- `GET /api/nodes` - Get all processing nodes state
- `POST /api/nodes/{slot}/active` - Set node active state (slot: 0-5)
- `POST /api/nodes/{slot}/value` - Set node value (slot: 0-5)
- `GET /api/state` - Get application state
- `POST /api/performance` - Set performance mode
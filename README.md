# AvaloniaGlslPipeline

AvaloniaGlslPipeline is a .NET desktop application built with Avalonia that loads and runs GLSL fragment shaders in real time.
It provides a shader preview workflow with a lightweight post-processing node pipeline for real-time visual iteration (some sliders).

![image_screenshot]()

### Reproduce

```sh
# Ubuntu
./scripts/setup_environment.sh   # first time
./scripts/start_app.sh           # to launch

# Windows 11
dotnet restore src/App/AvaloniaGlslPipeline.csproj
dotnet build src/App/AvaloniaGlslPipeline.csproj
dotnet run --project src/App/AvaloniaGlslPipeline.csproj

```


### Docker on Windows 11/WSL

```sh
# Navigate to docker folder
cd docker/
# Build the image (using parent directory as build context)
docker build -f Dockerfile -t diffracta:latest ..
# Run the container (EASIEST - use the helper script):
.\run_with_xserver.ps1
# Run with host network mode (best compatibility):
docker run --rm --network host -e DISPLAY='<YOUR_IP>:0.0' -e LIBGL_ALWAYS_INDIRECT=1 diffracta:latest
```


# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Common commands (PowerShell)

- Restore, build, run
```pwsh path=null start=null
# From repo root
dotnet restore src/App/Diffracta.csproj
dotnet build   src/App/Diffracta.csproj
dotnet run --project src/App/Diffracta.csproj
```

- Publish desktop app (Windows x64)
```pwsh path=null start=null
dotnet publish src/App/Diffracta.csproj -c Release -r win-x64
# Output: src/App/bin/Release/net8.0/win-x64/publish/
```

- Clean
```pwsh path=null start=null
dotnet clean src/App/Diffracta.csproj
```

Notes
- Tests: no test projects are present.
- Linters/formatters: no C# analyzers or formatting tools are configured in-repo.
- Requirements (from README): .NET 8 SDK, OpenGL 3.3 driver, Windows 11 tested.

## High-level architecture and development flow

- Project layout
  - Single Avalonia desktop app: `src/App/Diffracta.csproj` targeting `net8.0`.
  - Build config: `Directory.Build.props` stores NuGet packages under `cache/packages` in-repo.
  - Assets: `Images/**` embedded as Avalonia resources; app/icon files copied alongside the executable.

- App bootstrap and UI
  - Entry: `src/App/Program.cs` builds Avalonia app; `src/App/App.axaml(.cs)` loads global styles; `src/App/MainWindow.axaml(.cs)` is the main shell.
  - MainWindow composes:
    - Left sidebar for page navigation (Controls, Tools, Settings, Help).
    - Top-right OpenGL surface (`Graphics.ShaderSurface`).
    - Bottom-right tabs (e.g., Global info) and a toggleable logs panel.
    - Performance Mode: hides chrome, fullscreens the GL surface, restores with Esc.

- Shader engine (OpenGL via Avalonia)
  - `Graphics/GLLoader.cs`: dynamically resolves needed OpenGL functions from Avalonia’s `GlInterface` and exposes delegates/constants.
  - `Graphics/ShaderSurface.cs` (core rendering):
    - Uses a fullscreen triangle with an internal vertex shader.
    - Compiles a fragment shader (from file or fallback) and caches `u_time` and `u_resolution` uniforms.
    - Optional two-pass pipeline: renders main shader to an offscreen framebuffer, then applies a chain of post-process shaders (up to 5 slots) into dedicated FBO+texture pairs, finishing with a passthrough to the default framebuffer.
    - Built-in post-process slots (default files):
      - Slot 1 Saturation: expects `u_texture` (sampler2D), `u_saturation` (float), `u_resolution` (vec2).
      - Slot 2 Ping-pong delay: expects `u_texture` (sampler2D), `u_feedback` (sampler2D at texture unit 1), `u_delay_amount` (float), `u_resolution` (vec2); maintains a feedback texture across frames.
      - Slot 3 Barrel distortion: expects `u_texture` (sampler2D), `u_barrel_strength` (float), `u_resolution` (vec2).
    - Shader version normalization: source is converted to `#version 300 es` with `precision mediump float;` as needed.

- Shader files and hot-reload behavior
  - Main shader selection (Controls page) operates on the runtime shader directory: `<AppContext.BaseDirectory>/Shaders` (next to the running executable). `MainWindow` watches this directory for changes and refreshes the list.
  - Post-process shaders are loaded at startup from `Shaders/postprocess/` by filename convention inside `ShaderSurface.LoadPostProcessShaders()`.
    - Lookup order: first under `<BaseDirectory>/Shaders/postprocess/`, then falls back to `<CurrentDirectory>/Shaders/postprocess/`.
    - When running via `dotnet run`, the working directory is set to the project directory, so source files under `src/App/Shaders/postprocess/` are picked up without copying.
  - Build copy rules (csproj): only `Shaders/*.glsl` (top-level) are copied to output; `Shaders/postprocess/*.glsl` are not copied by default. For published/local runs where you rely on `<BaseDirectory>`, ensure post-process files exist there.

## Handy dev snippets (PowerShell)

- Create the runtime shader folder and copy a shader for live selection (while app is running)
```pwsh path=null start=null
$Out = "src/App/bin/Debug/net8.0/Shaders"
New-Item -ItemType Directory -Force -Path $Out | Out-Null
Copy-Item src/App/Shaders/001_organic_noise.glsl $Out
```

- Make post-process shaders available next to the exe (when not relying on CurrentDirectory fallback)
```pwsh path=null start=null
$Base = "src/App/bin/Debug/net8.0"
Copy-Item -Recurse src/App/Shaders/postprocess "$Base/Shaders/" -Force
```

## Docker (Windows with X11 forwarding)

- Build and run with VcXsrv (complete workflow)
```pwsh path=null start=null
# 1) Build the image
docker build -t diffracta:latest .

# 2) Ensure VcXsrv is installed
$vcxsrv = (Get-Command vcxsrv.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
if (-not $vcxsrv) { $vcxsrv = Get-ChildItem C:\Progra~1, C:\Progra~2 -Recurse -Filter vcxsrv.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
if (-not $vcxsrv) {
  winget install --id=VcXsrv.VcXsrv -e --silent 2>$null
  if ($LASTEXITCODE -ne 0) { winget install --id=marha.VcXsrv -e --silent }
  $vcxsrv = (Get-Command vcxsrv.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
  if (-not $vcxsrv) { $vcxsrv = Get-ChildItem C:\Progra~1, C:\Progra~2 -Recurse -Filter vcxsrv.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName }
}

# 3) Start X server (enable TCP, allow public access)
if ($vcxsrv) {
  Start-Process -FilePath $vcxsrv -ArgumentList ':0 -multiwindow -ac -clipboard -listen tcp -nowgl' -ErrorAction SilentlyContinue | Out-Null
} else {
  throw "No X server found. Install VcXsrv."
}

# 4) Wait for X to listen on :0 (TCP 6000)
$deadline = (Get-Date).AddSeconds(8)
do {
  Start-Sleep -Milliseconds 300
  $ok = (Test-NetConnection -ComputerName 'localhost' -Port 6000 -WarningAction SilentlyContinue).TcpTestSucceeded
} until ($ok -or (Get-Date) -gt $deadline)

# 5) Run the container
docker run --rm -e DISPLAY='host.docker.internal:0.0' -e LIBGL_ALWAYS_INDIRECT=1 diffracta:latest
```

# Diffracta (v/0.1)

This repository contains a template for Diffracta, a .NET GLSL application built with [Avalonia UI](https://avaloniaui.net/). The purpose is to build a live video editor for performance and (perhaps) neural rendering.

![Diffracta](media/20250921.png)

```powershell
# Option 1: PowerShell Scripts (Recommended)
# First time setup
.\update.ps1
.\start.ps1
# Option 2: Direct .NET Commands
dotnet restore src/App/Diffracta.csproj
dotnet build src/App/Diffracta.csproj
dotnet run --project src/App/Diffracta.csproj
```

## Requirements

- .NET 8.0 SDK or later
- OpenGL 3.3 compatible graphics driver
- Windows 11 (tested)

```
[net8.0]: 
   Top-level Package                    Requested    Resolved
   > Avalonia                           11.1.3       11.1.3
   > Avalonia.Desktop                   11.1.3       11.1.3
   > Avalonia.Diagnostics               11.1.3       11.1.3
   > Avalonia.Themes.Fluent             11.1.3       11.1.3
   > Microsoft.NET.ILLink.Tasks   (A)   [8.0.20, )   8.0.20

(A) : Auto-referenced package.
```

### Features

- Allows to load *.glsl shaders    
- Stacks shaders for post-processing  
- Has a Performance Mode (fullscreen, hides mouse)  

### Roadmap
 
- Divide the Log window to edit the current shader    
- Add a set of like 10 post-process shaders stacked that we can gate - e.g. barrel   
- Check the slider design  
  
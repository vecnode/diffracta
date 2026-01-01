ARG DOTNET_VERSION=8.0

########################
# Step 1: Build stage - Copy files and build the application
########################
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build

# Set working directory
WORKDIR /src

# Copy NuGet configuration files first (needed for restore)
COPY NuGet.config .
COPY Directory.Build.props .

# Copy only the project file first to leverage Docker layer caching for restore
COPY src/App/Diffracta.csproj App/

# Restore dependencies
# Override RestorePackagesPath to use default NuGet cache (ignore local cache)
# Also add nuget.org source explicitly in case NuGet.config has issues
RUN dotnet restore App/Diffracta.csproj \
    -p:RestorePackagesPath=/root/.nuget/packages \
    --source https://api.nuget.org/v3/index.json

# Copy assets folder (referenced by project as ../../assets/)
COPY assets/ /assets/

# Copy Shaders folder (now at src/Shaders)
COPY src/Shaders/ Shaders/

# Copy Media folder (now at src/Media)
COPY src/Media/ Media/

# Now copy the rest of the source (cache, bin, obj will be excluded via .dockerignore)
COPY src/App/ App/

# Build the application (restore again to ensure packages are available)
RUN dotnet build App/Diffracta.csproj -c Release -p:RestorePackagesPath=/root/.nuget/packages

# Publish the application for Linux (Docker containers run Linux)
# Force framework-dependent DLL output (no native apphost) so ENTRYPOINT 'dotnet Diffracta.dll' works
RUN dotnet publish App/Diffracta.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -p:UseAppHost=false \
    -p:RestorePackagesPath=/root/.nuget/packages \
    -o /app/publish

# Ensure shaders are copied to publish output (csproj should handle this, but verify/copy explicitly)
RUN if [ -d "Shaders" ]; then \
        cp -r Shaders /app/publish/Shaders || true; \
        echo "Shaders copied to publish output"; \
    else \
        echo "Warning: Shaders directory not found in source"; \
    fi

# Ensure media files are copied to publish output (csproj should handle this, but verify/copy explicitly)
RUN if [ -d "Media" ]; then \
        cp -r Media /app/publish/Media || true; \
        echo "Media copied to publish output"; \
    else \
        echo "Warning: Media directory not found in source"; \
    fi

########################
# Step 2: Runtime stage - Create final image with published app
########################
# Use aspnet runtime image to include ASP.NET Core framework (required by FrameworkReference)
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final

# Install dependencies for GUI (Avalonia/X11)
RUN apt-get update && \
    apt-get install -y \
    libx11-dev \
    libxrandr-dev \
    libgl1-mesa-dev \
    libglib2.0-0 \
    libfontconfig1 \
    libfreetype6 \
    libcairo2 \
    libice6 \
    libsm6 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published application from build stage
COPY --from=build /app/publish .

# Optional: Create non-root user for security
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Set entrypoint to run the application
ENTRYPOINT ["dotnet", "Diffracta.dll"]


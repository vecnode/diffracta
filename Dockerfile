# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=8.0

########################
# Step 1: Build stage - Copy files and build the application
########################
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build

# Set working directory
WORKDIR /src

# Copy only the project file first to leverage Docker layer caching for restore
COPY src/App/Diffracta.csproj src/App/

# Restore dependencies
RUN dotnet restore src/App/Diffracta.csproj

# Now copy the rest of the source (cache, bin, obj will be excluded via .dockerignore)
COPY . .

# Build the application
RUN dotnet build src/App/Diffracta.csproj -c Release --no-restore

# Publish the application for Linux (Docker containers run Linux)
# Force framework-dependent DLL output (no native apphost) so ENTRYPOINT 'dotnet Diffracta.dll' works
RUN dotnet publish src/App/Diffracta.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -p:UseAppHost=false \
    -o /app/publish \
    --no-restore

########################
# Step 2: Runtime stage - Create final image with published app
########################
FROM mcr.microsoft.com/dotnet/runtime:${DOTNET_VERSION} AS final

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

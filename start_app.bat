@echo off
REM Diffracta Application Launcher
REM Single script that handles everything with proper privileges

echo === Diffracta ===

REM Check if .NET is available
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo Error: .NET SDK not found. Please run setup_environment.bat first to set up the environment.
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo Using .NET SDK: %DOTNET_VERSION%

REM Check if project exists
if not exist "src\App\Diffracta.csproj" (
    echo Error: Project file not found. Please run this script from the project root directory.
    pause
    exit /b 1
)

REM Ensure Shaders directory exists
set SHADERS_DIR=src\App\Shaders
if not exist "%SHADERS_DIR%" (
    echo Creating Shaders directory: %SHADERS_DIR%
    mkdir "%SHADERS_DIR%"
)

REM Restore packages to local cache first
echo Restoring packages to local cache
dotnet restore src/App/Diffracta.csproj --packages ./cache
if %errorlevel% neq 0 (
    echo Error restoring packages
    pause
    exit /b 1
)

echo Building project
dotnet build src/App/Diffracta.csproj
if %errorlevel% neq 0 (
    echo Error building project
    pause
    exit /b 1
)

REM Run the application
echo Launching application
dotnet run --project src/App/Diffracta.csproj

echo Application finished
pause

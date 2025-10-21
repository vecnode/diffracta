@echo off
REM Update and Setup Folder Script for Diffracta Application
REM Uses local package cache in ./cache directory

echo === Diffracta - Environment Setup ===

REM Create local cache directory if it doesn't exist
if not exist "cache" (
    echo Creating local package cache directory
    mkdir cache
    echo Local cache created: ./cache
) else (
    echo Local cache directory exists: ./cache
)

REM Check if .NET is installed
echo Checking .NET installation
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo .NET SDK not found. Please install .NET 8.0 or later from https://dotnet.microsoft.com/download
    echo After installing .NET, run this script again.
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set DOTNET_VERSION=%%i
echo .NET SDK found: %DOTNET_VERSION%

REM Check if we're in the correct directory
if not exist "src\App\Diffracta.csproj" (
    echo Please run this script from the project root directory
    exit /b 1
)

echo.
echo Checking project dependencies

REM Restore NuGet packages to local cache
echo Restoring NuGet packages to local cache
echo Downloading packages to ./cache directory
dotnet restore src/App/Diffracta.csproj --packages ./cache
if %errorlevel% neq 0 (
    echo Failed to restore packages
    exit /b 1
)
echo Packages restored to local cache successfully

REM List installed packages
echo.
echo Installed packages:
dotnet list src/App/Diffracta.csproj package

REM Check for required packages
echo.
echo Checking for required packages
set REQUIRED_PACKAGES=Avalonia Avalonia.Desktop Avalonia.Themes.Fluent Avalonia.Diagnostics
set MISSING_PACKAGES=

for %%p in (%REQUIRED_PACKAGES%) do (
    dotnet list src/App/Diffracta.csproj package | findstr /i "%%p" >nul
    if %errorlevel% neq 0 (
        echo %%p (missing)
        set MISSING_PACKAGES=!MISSING_PACKAGES! %%p
    ) else (
        echo %%p
    )
)

REM Enable delayed variable expansion for the loop above
setlocal enabledelayedexpansion

REM Install missing packages if any
if not "!MISSING_PACKAGES!"=="" (
    echo.
    echo Installing missing packages to local cache
    for %%p in (!MISSING_PACKAGES!) do (
        echo Installing %%p to local cache
        dotnet add src/App/Diffracta.csproj package %%p
        if %errorlevel% neq 0 (
            echo Failed to install %%p
        ) else (
            echo %%p installed to local cache
        )
    )
)

REM Build the project
echo.
echo Building project
dotnet build src/App/Diffracta.csproj --configuration Release
if %errorlevel% neq 0 (
    echo Build failed
    echo Common issues:
    echo - FluentTheme syntax errors (fixed in this version)
    echo - OpenGL method signature mismatches (fixed in this version)
    echo - Missing using statements (fixed in this version)
    exit /b 1
)
echo Project built successfully

echo.
echo === Environment Setup Complete ===
echo Packages are now stored locally in: ./cache
echo You can now run the application using: start_app.bat
echo Or directly with: dotnet run --project src/App/Diffracta.csproj

pause

@echo off
REM Update and Setup Folder Script for Diffracta Application
REM Uses local package cache in ./cache directory
REM This batch file calls the PowerShell script

echo === Diffracta - Environment Setup ===
echo Calling PowerShell setup script

REM Check if PowerShell is available
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo PowerShell not found. Please install PowerShell or run setup_environment.ps1 directly.
    pause
    exit /b 1
)

REM Call the PowerShell script
powershell.exe -ExecutionPolicy Bypass -File "%~dp0setup_environment.ps1"

REM Check if PowerShell script succeeded
if %errorlevel% neq 0 (
    echo.
    echo Setup failed. See errors above.
    pause
    exit /b 1
)

pause

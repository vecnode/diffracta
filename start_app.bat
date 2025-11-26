@echo off
REM Diffracta Application Launcher
REM This batch file calls the PowerShell script

echo === Diffracta ===
echo Calling PowerShell launcher script

REM Check if PowerShell is available
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo PowerShell not found. Please install PowerShell or run start_app.ps1 directly.
    pause
    exit /b 1
)

REM Call the PowerShell script
powershell.exe -ExecutionPolicy Bypass -File "%~dp0start_app.ps1"

REM Check if PowerShell script succeeded
if %errorlevel% neq 0 (
    echo.
    echo Application launch failed. See errors above.
    pause
    exit /b 1
)

pause

@echo off
REM OpenBroadcaster Installer Build Script
REM Requires: Inno Setup 6.x installed

echo ========================================
echo  OpenBroadcaster Installer Builder
echo ========================================
echo.

REM Check for Inno Setup
set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
    set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
)
if not exist %ISCC% (
    echo ERROR: Inno Setup 6 not found!
    echo Please install from: https://jrsoftware.org/isdl.php
    pause
    exit /b 1
)

REM Build the application first
echo [1/3] Building OpenBroadcaster Avalonia in Release mode...
cd /d "%~dp0.."
dotnet publish OpenBroadcaster.Avalonia\OpenBroadcaster.Avalonia.csproj -c Release -r win-x64 --self-contained true -o "bin\Installer"
if errorlevel 1 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

REM Create output directory
echo [2/3] Preparing installer output directory...
if not exist "bin\InstallerOutput" mkdir "bin\InstallerOutput"

REM Run Inno Setup Compiler
echo [3/3] Building installer with Inno Setup...
cd /d "%~dp0"
%ISCC% OpenBroadcaster.iss
if errorlevel 1 (
    echo ERROR: Installer build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Build Complete!
echo  Installer: bin\InstallerOutput\
echo ========================================
pause

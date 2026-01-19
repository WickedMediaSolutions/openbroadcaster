# OpenBroadcaster Installer

This folder contains the Inno Setup script and build tools for creating the OpenBroadcaster Windows installer.

## Prerequisites

1. **Inno Setup 6.x** - Download from [jrsoftware.org](https://jrsoftware.org/isdl.php)
2. **.NET 8 SDK** - For building the application

## Building the Installer

### Option 1: Use the batch file (Recommended)

Simply run:
```batch
build-installer.bat
```

This will:
1. Build OpenBroadcaster in Release mode (self-contained)
2. Create the installer using Inno Setup
3. Output to `bin\InstallerOutput\`

### Option 2: Manual build

1. First, publish the application:
   ```powershell
   cd E:\openbroadcaster
   dotnet publish OpenBroadcaster.csproj -c Release -r win-x64 --self-contained true -o "bin\Installer"
   ```

2. Then compile the installer script:
   ```batch
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" OpenBroadcaster.iss
   ```

## Files

| File | Description |
|------|-------------|
| `OpenBroadcaster.iss` | Inno Setup script |
| `build-installer.bat` | Automated build script |

## Customization

### App Icon
Place your application icon at `Assets\app-icon.ico`. If no icon is provided, comment out the `SetupIconFile` line in the `.iss` file.

### Version Number
Update the version in `OpenBroadcaster.iss`:
```inno
#define MyAppVersion "1.0.0"
```

### Output Filename
The installer will be named: `OpenBroadcaster-{version}-Setup.exe`

## Installer Features

- Modern wizard style
- Desktop shortcut (optional)
- Start menu entries
- Automatic AppData folder creation
- Clean uninstaller
- LZMA2 compression (~40-50% size reduction)
- Per-user or all-users installation

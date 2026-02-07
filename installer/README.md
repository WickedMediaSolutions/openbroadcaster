# OpenBroadcaster Installer

This directory contains the Windows installer build scripts for **OpenBroadcaster 4.2**.

## What's New in Version 4.2

**Major Features:**
- ✅ Complete architectural modernization with dependency injection
- ✅ Enhanced security with DPAPI-encrypted OAuth tokens and credentials
- ✅ Structured logging framework with file-based logging
- ✅ LRU caching for album artwork (100-item limit)
- ✅ Comprehensive unit test coverage for core infrastructure
- ✅ Full Linux support (PulseAudio, JACK, ALSA)

**Security Enhancements:**
- OAuth tokens automatically encrypted at rest (Windows DPAPI)
- API passwords encrypted in settings
- Automatic migration from plain-text to encrypted format

## Requirements

**To Build the Installer:**
1. **Inno Setup 6.x** - Download from [jrsoftware.org](https://jrsoftware.org/isdl.php)
2. **.NET 8 SDK** - For building the application

**For End Users:**
- Windows 10 or later (64-bit)
- .NET 8.0 Desktop Runtime (bundled in self-contained mode)
- WASAPI-compatible audio device

## Building the Installer

### Important: Distribution & Licensing

The contents of this repository (source code, scripts, and documentation) are
licensed under the MIT license. **The compiled Windows installer that you
produce with this script is not free/open software and is not licensed for
public redistribution from this repository.**

Use the generated installer only for your own testing or according to the
separate terms under which you obtain an official installer.

### Quick Build (Recommended)

Simply run:
```batch
build-installer.bat
```

This will:
1. Build OpenBroadcaster.Avalonia in Release mode (self-contained)
2. Create the installer using Inno Setup
3. Output: `bin\InstallerOutput\OpenBroadcaster-4.2-Setup.exe`

### Manual Build

1. First, publish the application:
   ```batch
   cd ..
   dotnet publish OpenBroadcaster.Avalonia\OpenBroadcaster.Avalonia.csproj ^
     -c Release ^
     -r win-x64 ^
     --self-contained true ^
     -o "bin\Installer"
   ```

2. Then compile the installer script:
   ```batch
   cd installer
   "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" OpenBroadcaster.iss
   ```

## Files

| File | Description |
|------|-------------|
| `OpenBroadcaster.iss` | Inno Setup script |
| `build-installer.bat` | Automated build script |

## Customization

### Version Number
Update the version in two places:

1. **`OpenBroadcaster.iss`** (Line 5):
```inno
#define MyAppVersion "4.2"
```

2. **`OpenBroadcaster.Avalonia.csproj`**:
```xml
<Version>4.2.0</Version>
<AssemblyVersion>4.2.0.0</AssemblyVersion>
```

### App Icon
Place your application icon at `Assets\app-icon.ico`.

### Output Filename
The installer will be named: `OpenBroadcaster-4.2-Setup.exe`

## Installer Features

- **Modern wizard style** with enhanced UI
- **Desktop shortcut** (optional)
- **Start menu entries** with uninstaller
- **AppData folder structure** automatically created:
  - `%AppData%\OpenBroadcaster\` - Settings
  - `%AppData%\OpenBroadcaster\logs\` - Log files
  - `%AppData%\OpenBroadcaster\cache\` - Album artwork
  - `%AppData%\OpenBroadcaster\overlays\` - Custom overlays
- **File association** for `.obproj` project files
- **Clean uninstaller** (preserves user data)
- **LZMA2 Ultra64 compression** (~40-50% size reduction)
- **Per-user or all-users** installation
- **Self-contained .NET runtime** (no separate install needed)

## Distribution & Licensing

The contents of this repository (source code, scripts, and documentation) are
licensed under the **Creative Commons BY-NC 4.0** license.

**The compiled Windows installer is not licensed for public redistribution from this repository.**

Use the generated installer only for:
- Your own testing and development
- Distribution according to separate commercial terms
- Internal/private use

## Version History

- **4.2** (Feb 2026) - Architectural modernization, security enhancements, Linux support
- **3.1** - Initial Avalonia release
- **3.0** - Legacy WPF version

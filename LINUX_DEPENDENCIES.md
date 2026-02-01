# OpenBroadcaster Linux Dependencies

## Quick Setup

Run the automated setup script:
```bash
./setup-linux.sh
```

This will:
1. Detect your Linux distribution
2. Install all required libraries
3. Create necessary directories
4. Verify library installation

## Manual Setup

### Ubuntu/Debian
```bash
sudo apt-get update
sudo apt-get install -y \
    libmp3lame0 \
    libopus0 \
    libvorbis0a \
    libvorbisenc2 \
    libflac12 \
    libopenal1 \
    libssl3 \
    libx11-6 \
    libxrandr2 \
    libxcursor1 \
    libxinerama1 \
    libxi6 \
    libxext6 \
    libxkbcommon0 \
    fontconfig \
    fonts-dejavu \
    libpulse0 \
    libsndfile1
```

### Fedora/RHEL/CentOS
```bash
sudo dnf install -y \
    lame-libs \
    opus \
    libvorbis \
    libvorbisenc \
    flac-libs \
    openal-soft-libs \
    openssl \
    libX11 \
    libXrandr \
    libXcursor \
    libXinerama \
    libXi \
    libXext \
    libxkbcommon \
    fontconfig \
    dejavu-fonts \
    pulseaudio-libs \
    libsndfile
```

### Arch Linux
```bash
sudo pacman -S --noconfirm \
    libmp3lame \
    opus \
    libvorbis \
    flac \
    openal \
    openssl \
    libx11 \
    libxrandr \
    libxcursor \
    libxinerama \
    libxi \
    libxext \
    libxkbcommon \
    fontconfig \
    dejavu-fonts \
    libpulse \
    libsndfile
```

## Dependency Breakdown

### Audio Libraries
- **libmp3lame0** - MP3 encoding (required for encoders)
- **libopus** - Opus audio codec
- **libvorbis** - Vorbis audio codec
- **libflac** - FLAC audio codec
- **libopenal** - Audio output (OpenAL)
- **libpulse** - PulseAudio support (optional but recommended)

### UI Framework
- **libx11-6** - X11 display server
- **libxrandr2** - Monitor resolution support
- **libxcursor1** - Cursor support
- **libxinerama1** - Multi-monitor support
- **libxi6** - Input device support
- **libxext6** - X11 extensions
- **libxkbcommon0** - Keyboard handling

### Other
- **libssl3** - OpenSSL (encryption, HTTPS, SSL/TLS)
- **fontconfig** - Font configuration
- **fonts-dejavu** - Monospace fonts for UI
- **libsndfile1** - Sound file I/O

## Troubleshooting

### "libmp3lame.so not found"
This is the most common issue. The NAudio.Lame library needs to find the MP3 encoding library at runtime.

**Solution:**
```bash
# Verify it's installed
sudo apt-get install --reinstall libmp3lame0

# Check if it's in the standard library path
find /usr -name "libmp3lame*" 2>/dev/null
```

### "libopenal.so not found"
OpenAL is required for audio output on Linux.

**Solution:**
```bash
sudo apt-get install libopenal1
```

### Missing display libraries
If you get X11 errors, install:
```bash
sudo apt-get install libx11-6 libxrandr2 libxcursor1 libxinerama1 libxi6
```

### Library path issues

If libraries are installed but not found, you can add them to the library search path:

```bash
# Add to ~/.bashrc or ~/.zshrc
export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

# Or for the session
export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu
./run-openbroadcaster.sh
```

## Verification

To verify all dependencies are installed:

```bash
./setup-linux.sh
```

Or manually:
```bash
# Check for libmp3lame
find /usr -name "libmp3lame*.so*" 2>/dev/null | head -1

# Check for libopenal
find /usr -name "libopenal*.so*" 2>/dev/null | head -1

# Check for libvorbis
find /usr -name "libvorbis*.so*" 2>/dev/null | head -1
```

All three should return at least one result.

## Docker Installation (Alternative)

If you don't want to manage dependencies, you can use Docker:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm

# Install audio libraries
RUN apt-get update && apt-get install -y \
    libmp3lame0 \
    libopenal1 \
    libpulse0 \
    fonts-dejavu \
    libx11-6 \
    libxrandr2

# Copy app
COPY bin/linux-dist/OpenBroadcaster /app/

WORKDIR /app
ENTRYPOINT ["./OpenBroadcaster"]
```

## Notes

- **libmp3lame** is specifically required for encoder streaming
- Without audio libraries, the app will crash when trying to start encoders
- The app will still start without audio libraries, but audio features won't work
- PulseAudio support is optional but recommended for better audio device detection
- Fonts are optional but required for proper UI rendering

## Getting Help

If you're still experiencing issues:

1. Run: `./setup-linux.sh` to detect and fix problems
2. Check logs: `~/.local/share/OpenBroadcaster/logs/application.log`
3. Run encoder test: `./encoder-test.sh localhost 8000 /test`
4. Report with: OS info, output of `ldd ./OpenBroadcaster`, and error logs

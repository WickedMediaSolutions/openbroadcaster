#!/bin/bash
# OpenBroadcaster Linux Setup Script
# Installs all required dependencies and configures the system

set -e

echo "========================================="
echo "  OpenBroadcaster Linux Setup"
echo "========================================="
echo ""

# Detect OS
if [ -f /etc/os-release ]; then
    . /etc/os-release
    OS=$ID
    VERSION=$VERSION_ID
else
    echo "✗ Cannot detect OS. Please install dependencies manually."
    exit 1
fi

echo "Detected: $OS $VERSION"
echo ""

# Install dependencies based on OS
case $OS in
    debian|ubuntu)
        echo "[1/3] Installing Debian/Ubuntu dependencies..."
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
        echo "✓ Dependencies installed"
        ;;
    fedora|rhel|centos)
        echo "[1/3] Installing Fedora/RHEL/CentOS dependencies..."
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
        echo "✓ Dependencies installed"
        ;;
    arch)
        echo "[1/3] Installing Arch Linux dependencies..."
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
        echo "✓ Dependencies installed"
        ;;
    *)
        echo "✗ Unsupported OS: $OS"
        echo "Please install these packages manually:"
        echo "  - libmp3lame (or lame-libs)"
        echo "  - libopus"
        echo "  - libvorbis"
        echo "  - libflac"
        echo "  - libopenal"
        echo "  - OpenSSL"
        echo "  - X11 libraries"
        echo "  - PulseAudio"
        exit 1
        ;;
esac

echo ""
echo "[2/3] Configuring OpenBroadcaster..."

# Create config directories
mkdir -p ~/.local/share/OpenBroadcaster
mkdir -p ~/.config/OpenBroadcaster
mkdir -p ~/.local/share/OpenBroadcaster/logs

echo "✓ Configuration directories created"
echo ""

echo "[3/3] Verifying libraries..."

# Check for key audio libraries
MISSING=0

for lib in libmp3lame libopus libvorbis libFLAC libopenal; do
    if find /usr/lib -name "${lib}*.so*" 2>/dev/null | grep -q .; then
        echo "✓ Found: $lib"
    else
        echo "⚠ Warning: $lib not found in standard location"
    fi
done

echo ""

if [ $MISSING -eq 1 ]; then
    echo "⚠ Some libraries may be missing!"
    echo "Try running: sudo apt-get install libmp3lame0 libopenal1"
    exit 1
fi

echo "========================================="
echo "  Setup Complete! ✓"
echo "========================================="
echo ""
echo "You can now run OpenBroadcaster:"
echo "  dotnet run"
echo ""
echo "Or if installed system-wide:"
echo "  openbroadcaster"
echo ""
echo "Logs will be saved to:"
echo "  ~/.local/share/OpenBroadcaster/logs/"
echo ""

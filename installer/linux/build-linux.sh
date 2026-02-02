#!/bin/bash
# OpenBroadcaster Linux Build Script
# Creates a self-contained distributable package

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VERSION="1.3.0"
APP_NAME="OpenBroadcaster"
OUTPUT_DIR="$PROJECT_ROOT/bin/linux-dist"
PUBLISH_DIR="$OUTPUT_DIR/$APP_NAME"

echo "========================================"
echo "  OpenBroadcaster Linux Build"
echo "  Version: $VERSION"
echo "========================================"
echo ""

# Clean previous build
echo "[1/5] Cleaning previous build..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$PUBLISH_DIR"

# Build self-contained release
echo "[2/5] Publishing self-contained release..."
cd "$PROJECT_ROOT"
dotnet publish OpenBroadcaster.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -o "$PUBLISH_DIR"

# Copy overlay assets
echo "[3/5] Copying overlay assets..."
if [ -d "$PROJECT_ROOT/Overlay" ]; then
    cp -r "$PROJECT_ROOT/Overlay" "$PUBLISH_DIR/"
fi

# Create .desktop file
echo "[4/5] Creating desktop entry..."
cat > "$PUBLISH_DIR/openbroadcaster.desktop" << 'EOF'
[Desktop Entry]
Version=1.0
Type=Application
Name=OpenBroadcaster
Comment=Professional Radio Automation Software
Exec=OpenBroadcaster
Icon=openbroadcaster
Terminal=false
Categories=AudioVideo;Audio;
Keywords=radio;broadcasting;automation;streaming;
StartupWMClass=OpenBroadcaster
EOF

# Copy icon if exists
if [ -f "$PROJECT_ROOT/Assets/icon.png" ]; then
    cp "$PROJECT_ROOT/Assets/icon.png" "$PUBLISH_DIR/openbroadcaster.png"
elif [ -f "$PROJECT_ROOT/Assets/icon.ico" ]; then
    # Convert ico to png if ImageMagick is available
    if command -v convert &> /dev/null; then
        convert "$PROJECT_ROOT/Assets/icon.ico[0]" "$PUBLISH_DIR/openbroadcaster.png" 2>/dev/null || true
    fi
fi

# Create install script
echo "[5/5] Creating install script..."
cat > "$PUBLISH_DIR/install.sh" << 'INSTALL_EOF'
#!/bin/bash
# OpenBroadcaster Installation Script
set -e

INSTALL_DIR="/opt/openbroadcaster"
BIN_LINK="/usr/local/bin/openbroadcaster"
DESKTOP_FILE="/usr/share/applications/openbroadcaster.desktop"
ICON_DIR="/usr/share/icons/hicolor/256x256/apps"

# Check for root
if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (sudo ./install.sh)"
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Installing OpenBroadcaster..."

# Create install directory
mkdir -p "$INSTALL_DIR"
cp -r "$SCRIPT_DIR"/* "$INSTALL_DIR/"
rm -f "$INSTALL_DIR/install.sh"
rm -f "$INSTALL_DIR/uninstall.sh"

# Make executable
chmod +x "$INSTALL_DIR/OpenBroadcaster"

# Create symlink
ln -sf "$INSTALL_DIR/OpenBroadcaster" "$BIN_LINK"

# Install desktop file
if [ -f "$INSTALL_DIR/openbroadcaster.desktop" ]; then
    sed "s|Exec=OpenBroadcaster|Exec=$INSTALL_DIR/OpenBroadcaster|g" \
        "$INSTALL_DIR/openbroadcaster.desktop" > "$DESKTOP_FILE"
    
    # Update icon path
    if [ -f "$INSTALL_DIR/openbroadcaster.png" ]; then
        mkdir -p "$ICON_DIR"
        cp "$INSTALL_DIR/openbroadcaster.png" "$ICON_DIR/openbroadcaster.png"
    fi
    
    # Update desktop database
    if command -v update-desktop-database &> /dev/null; then
        update-desktop-database /usr/share/applications 2>/dev/null || true
    fi
fi

echo ""
echo "Installation complete!"
echo "Run 'openbroadcaster' or find it in your applications menu."
INSTALL_EOF

chmod +x "$PUBLISH_DIR/install.sh"

# Create uninstall script
cat > "$PUBLISH_DIR/uninstall.sh" << 'UNINSTALL_EOF'
#!/bin/bash
# OpenBroadcaster Uninstall Script
set -e

if [ "$EUID" -ne 0 ]; then
    echo "Please run as root (sudo ./uninstall.sh)"
    exit 1
fi

echo "Uninstalling OpenBroadcaster..."

rm -rf /opt/openbroadcaster
rm -f /usr/local/bin/openbroadcaster
rm -f /usr/share/applications/openbroadcaster.desktop
rm -f /usr/share/icons/hicolor/256x256/apps/openbroadcaster.png

if command -v update-desktop-database &> /dev/null; then
    update-desktop-database /usr/share/applications 2>/dev/null || true
fi

echo "OpenBroadcaster has been uninstalled."
UNINSTALL_EOF

chmod +x "$PUBLISH_DIR/uninstall.sh"

# Create tarball
echo ""
echo "Creating distribution archive..."
cd "$OUTPUT_DIR"
tar -czvf "OpenBroadcaster-$VERSION-linux-x64.tar.gz" "$APP_NAME"

echo ""
echo "========================================"
echo "  Build Complete!"
echo "========================================"
echo ""
echo "Output files:"
echo "  Directory: $PUBLISH_DIR"
echo "  Archive:   $OUTPUT_DIR/OpenBroadcaster-$VERSION-linux-x64.tar.gz"
echo ""
echo "To install:"
echo "  1. Extract: tar -xzf OpenBroadcaster-$VERSION-linux-x64.tar.gz"
echo "  2. Run:     cd OpenBroadcaster && sudo ./install.sh"
echo ""

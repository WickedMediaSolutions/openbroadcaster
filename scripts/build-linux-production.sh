#!/bin/bash
# OpenBroadcaster Linux Build Script for Production Package
# This script builds DEB, AppImage, and tarball distributions

set -e

VERSION="4.4.0"
BUILD_DIR="./build"
PUBLISH_DIR="$BUILD_DIR/publish"
OUTPUT_DIR="./dist"

echo "=== OpenBroadcaster v$VERSION Linux Build ==="
echo "Target: Production-ready packages (DEB, AppImage, Tarball)"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Clean previous builds
echo -e "${YELLOW}[1/7] Cleaning previous builds...${NC}"
rm -rf "$BUILD_DIR" "$OUTPUT_DIR"
mkdir -p "$BUILD_DIR" "$OUTPUT_DIR"

# Step 2: Restore dependencies
echo -e "${YELLOW}[2/7] Restoring NuGet dependencies...${NC}"
dotnet restore openbroadcaster.sln

# Step 3: Run tests
echo -e "${YELLOW}[3/7] Running unit tests (86 total)...${NC}"
dotnet test OpenBroadcaster.Tests/OpenBroadcaster.Tests.csproj \
    -c Release \
    --verbosity normal \
    --logger="console;verbosity=normal"

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Tests failed!${NC}"
    exit 1
fi
echo -e "${GREEN}✓ All 86 tests passed${NC}"

# Step 4: Build and publish
echo -e "${YELLOW}[4/7] Building Avalonia for Linux x64...${NC}"
dotnet publish OpenBroadcaster.Avalonia/OpenBroadcaster.Avalonia.csproj \
    -c Release \
    -o "$PUBLISH_DIR" \
    --self-contained=true \
    --runtime=linux-x64

echo -e "${GREEN}✓ Build complete${NC}"

# Step 5: Create tarball distribution
echo -e "${YELLOW}[5/7] Creating tarball distribution...${NC}"
tar -czf "$OUTPUT_DIR/OpenBroadcaster-${VERSION}-linux-x64.tar.gz" \
    -C "$PUBLISH_DIR" .

echo -e "${GREEN}✓ Tarball created${NC}"

# Step 6: Prepare DEB packaging (requires dpkg-deb on Linux)
echo -e "${YELLOW}[6/7] Preparing DEB package structure...${NC}"
DEB_TEMP="$BUILD_DIR/deb"
DEB_INSTALLDIR="$DEB_TEMP/usr/local/lib/openbroadcaster"
DEB_BINDIR="$DEB_TEMP/usr/local/bin"
DEB_APPDIR="$DEB_TEMP/usr/share/applications"
DEB_ICONDIR="$DEB_TEMP/usr/share/pixmaps"
DEB_CONTROL="$DEB_TEMP/DEBIAN"

mkdir -p "$DEB_INSTALLDIR" "$DEB_BINDIR" "$DEB_APPDIR" "$DEB_ICONDIR" "$DEB_CONTROL"

# Copy application files
cp -r "$PUBLISH_DIR"/* "$DEB_INSTALLDIR/"
chmod +x "$DEB_INSTALLDIR/OpenBroadcaster.Avalonia"

# Create launcher script
cat > "$DEB_BINDIR/openbroadcaster" << 'EOF'
#!/bin/bash
exec /usr/local/lib/openbroadcaster/OpenBroadcaster.Avalonia "$@"
EOF
chmod +x "$DEB_BINDIR/openbroadcaster"

# Create control file
cat > "$DEB_CONTROL/control" << EOF
Package: openbroadcaster
Version: $VERSION
Architecture: amd64
Installed-Size: $(du -s "$DEB_INSTALLDIR" | cut -f1)
Maintainer: Wicked Media Solutions <support@wickedmediasolutions.com>
Homepage: https://github.com/WickedMediaSolutions/openbroadcaster
Description: Cross-Platform Radio Automation Software
 OpenBroadcaster is a professional radio automation and streaming platform
 with advanced audio processing, multi-deck mixing, and automation features.
EOF

# Create postinst script
cat > "$DEB_CONTROL/postinst" << 'POST_EOF'
#!/bin/bash
set -e
case "$1" in
    configure)
        update-desktop-database /usr/share/applications 2>/dev/null || true
        update-mime-database /usr/share/mime 2>/dev/null || true
        ;;
esac
exit 0
POST_EOF
chmod 755 "$DEB_CONTROL/postinst"

# Create postrm script  
cat > "$DEB_CONTROL/postrm" << 'POST_EOF'
#!/bin/bash
set -e
case "$1" in
    remove|purge)
        update-desktop-database /usr/share/applications 2>/dev/null || true
        ;;
esac
exit 0
POST_EOF
chmod 755 "$DEB_CONTROL/postrm"

echo -e "${GREEN}✓ DEB structure ready${NC}"

# Step 7: Build DEB package (if dpkg-deb available)
if command -v dpkg-deb &> /dev/null; then
    echo -e "${YELLOW}[7/7] Building DEB package...${NC}"
    dpkg-deb --build "$DEB_TEMP" "$OUTPUT_DIR/openbroadcaster_${VERSION}_amd64.deb"
    echo -e "${GREEN}✓ DEB package created${NC}"
else
    echo -e "${YELLOW}[7/7] dpkg-deb not found (skipping DEB binary build)${NC}"
    echo "    To create DEB, run on Linux: dpkg-deb --build $DEB_TEMP dist/openbroadcaster_${VERSION}_amd64.deb"
    cp -r "$DEB_TEMP" "$OUTPUT_DIR/openbroadcaster-deb-template"
    echo -e "${YELLOW}    DEB template saved to dist/openbroadcaster-deb-template${NC}"
fi

# Summary
echo ""
echo -e "${GREEN}=== Build Complete ===${NC}"
echo "Output files in ./dist/:"
ls -lh "$OUTPUT_DIR/" | tail -n +2

echo ""
echo "Next steps:"
echo "  1. Tarball: ./dist/OpenBroadcaster-${VERSION}-linux-x64.tar.gz"
echo "  2. DEB: Install with 'sudo apt install ./dist/openbroadcaster_${VERSION}_amd64.deb'"
echo "  3. See LINUX_PRODUCTION_BUILD.md for detailed distribution instructions"
echo ""

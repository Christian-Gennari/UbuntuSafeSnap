#!/bin/bash
set -e

PROJECT_DIR="UbuntuSafeSnap"
BINARY_NAME="UbuntuSafeSnap"
PUBLISH_OUTPUT="$PROJECT_DIR/bin/Release/net10.0/linux-x64/publish/$BINARY_NAME"
DEST_DIR="${1:-$HOME/UbuntuSafeSnap}"

echo "Cleaning previous build..."
dotnet clean "$PROJECT_DIR" -c Release > /dev/null 2>&1

echo "Building self-contained executable..."
dotnet publish "$PROJECT_DIR" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true

mkdir -p "$DEST_DIR"
cp "$PUBLISH_OUTPUT" "$DEST_DIR/"
chmod +x "$DEST_DIR/$BINARY_NAME"

DISPLAY_DIR="${DEST_DIR/#$HOME\//~/}"

echo ""
echo "Binary copied to: $DISPLAY_DIR/$BINARY_NAME"
echo "Next steps:"
echo "  cd $DISPLAY_DIR"
echo "  ./UbuntuSafeSnap init"
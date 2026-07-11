#!/bin/bash

# TeknoParrotUI Desktop Integration Installer for Linux
#
# This script installs TeknoParrotUI with proper desktop integration
# (icon, launcher menu entry, etc.)
#
# Usage: ./install-desktop-entry.sh [APP_DIR]
#
# Example: ./install-desktop-entry.sh ~/.local/opt/TeknoParrotUi

set -e

APP_DIR="${1:-.}"
DESKTOP_DIR="${HOME}/.local/share/applications"
ICONS_DIR="${HOME}/.local/share/icons/hicolor/256x256/apps"

if [ ! -f "$APP_DIR/TeknoParrotUi" ]; then
    echo "Error: TeknoParrotUi executable not found in $APP_DIR"
    exit 1
fi

if [ ! -f "$APP_DIR/TeknoParrotUi.desktop" ]; then
    echo "Error: TeknoParrotUi.desktop file not found in $APP_DIR"
    exit 1
fi

if [ ! -f "$APP_DIR/teknoparrot.png" ]; then
    echo "Error: teknoparrot.png icon not found in $APP_DIR"
    exit 1
fi

echo "Installing TeknoParrotUI desktop integration..."

# Create directories if they don't exist
mkdir -p "$DESKTOP_DIR"
mkdir -p "$ICONS_DIR"

# Copy icon to standard location
echo "Installing icon to $ICONS_DIR..."
cp "$APP_DIR/teknoparrot.png" "$ICONS_DIR/teknoparrot.png"

# Update desktop file with correct exec path
DESKTOP_TEMP=$(mktemp)
sed "s|Exec=TeknoParrotUi|Exec=$APP_DIR/TeknoParrotUi|g" "$APP_DIR/TeknoParrotUi.desktop" > "$DESKTOP_TEMP"
sed -i "s|Icon=teknoparrot|Icon=$ICONS_DIR/teknoparrot|g" "$DESKTOP_TEMP"

# Install desktop file
echo "Installing desktop entry to $DESKTOP_DIR..."
cp "$DESKTOP_TEMP" "$DESKTOP_DIR/TeknoParrotUi.desktop"
chmod 644 "$DESKTOP_DIR/TeknoParrotUi.desktop"
rm "$DESKTOP_TEMP"

# Update desktop database
if command -v update-desktop-database &> /dev/null; then
    echo "Updating desktop database..."
    update-desktop-database "$DESKTOP_DIR" || true
fi

echo "✓ Desktop integration complete!"
echo "TeknoParrotUI should now appear in your application menu."
echo ""
echo "To launch directly:"
echo "  $APP_DIR/TeknoParrotUi"
echo ""
echo "To uninstall, remove:"
echo "  $DESKTOP_DIR/TeknoParrotUi.desktop"
echo "  $ICONS_DIR/teknoparrot.png"

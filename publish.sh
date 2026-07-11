#!/bin/bash

# TeknoParrotUI Linux Publisher
#
# Builds a distributable release of TeknoParrotUI (.NET 8, Avalonia) for Linux.
# Publishes TeknoParrotUi (Avalonia, linux-x64, framework-dependent) and
# ParrotPatcher into a single output folder.
# Users need the .NET 8 Desktop Runtime installed.
#
# Usage:
#   ./publish.sh [OUTPUT_DIR] [--zip]
#
# Examples:
#   ./publish.sh                              # outputs to ./publish/TeknoParrotUi
#   ./publish.sh ./dist/release --zip         # outputs to ./dist/release, creates zip

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR=""
CREATE_ZIP=false

# Parse arguments
for arg in "$@"; do
    if [[ "$arg" == "--zip" ]]; then
        CREATE_ZIP=true
    else
        OUTPUT_DIR="$arg"
    fi
done

# Set default output dir if not specified
OUTPUT_DIR="${OUTPUT_DIR:-${SCRIPT_DIR}/publish/TeknoParrotUi}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Error handler
error_exit() {
    echo -e "${RED}Error: $1${NC}" >&2
    exit 1
}

# Non-fatal error handler
warn() {
    echo -e "${RED}Warning: $1${NC}" >&2
}

# Cleanup old output
if [ -d "$OUTPUT_DIR" ]; then
    echo -e "${CYAN}Removing old publish directory...${NC}"
    rm -rf "$OUTPUT_DIR"
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Publish TeknoParrotUi.Avalonia
echo -e "${CYAN}Publishing TeknoParrotUi (Avalonia)...${NC}"
cd "$SCRIPT_DIR"
dotnet publish \
    "TeknoParrotUi.Avalonia/TeknoParrotUi.Avalonia.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$OUTPUT_DIR" \
    --nologo \
    || error_exit "TeknoParrotUi publish failed"

# Publish ParrotPatcher
echo -e "${CYAN}Publishing ParrotPatcher...${NC}"
dotnet publish \
    "ParrotPatcher/ParrotPatcher.csproj" \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o "$OUTPUT_DIR" \
    --nologo \
    || error_exit "ParrotPatcher publish failed"

# ---------------------------------------------------------------------------
# Move dependency assemblies into libs/ so the root folder stays clean.
# The deps.json files are rewritten so the .NET host resolves them from there.
# ---------------------------------------------------------------------------
echo -e "${CYAN}Moving dependencies into libs/...${NC}"
LIBS_DIR="$OUTPUT_DIR/libs"
mkdir -p "$LIBS_DIR"

# Files that must stay at the root (apphosts + their host config files)
declare -a KEEP_AT_ROOT=(
    "TeknoParrotUi"
    "TeknoParrotUi.dll"
    "TeknoParrotUi.runtimeconfig.json"
    "ParrotPatcher"
    "ParrotPatcher.dll"
    "ParrotPatcher.runtimeconfig.json"
)

# Check if file should stay at root
should_keep() {
    local file=$1
    for keep in "${KEEP_AT_ROOT[@]}"; do
        if [[ "$file" == "$keep" ]]; then
            return 0
        fi
    done
    return 1
}

MOVED_COUNT=0

# Move regular files (except those in KEEP_AT_ROOT)
for file in "$OUTPUT_DIR"/*; do
    if [ -f "$file" ]; then
        filename=$(basename "$file")
        if ! should_keep "$filename"; then
            mv "$file" "$LIBS_DIR/$filename" 2>/dev/null || warn "Failed to move $filename"
            ((MOVED_COUNT++)) || true
        fi
    fi
done

# Move translation satellite assemblies (fi-FI/, de-DE/, ar-SA/, etc.)
for dir in "$OUTPUT_DIR"/*; do
    if [ -d "$dir" ]; then
        dirname=$(basename "$dir")
        # Match language code pattern: xx-XX, xx-XXX, etc. (case-insensitive)
        if [[ $dirname =~ ^[a-zA-Z]{2}(-[a-zA-Z]{2,4})?$ ]]; then
            mv "$dir" "$LIBS_DIR/$dirname" 2>/dev/null || warn "Failed to move $dirname"
            ((MOVED_COUNT++)) || true
        fi
    fi
done

# Remove the deps.json manifests: without them the host probes the app folder
# and the in-app LibsResolver handles everything that lives in libs/
rm -f "$LIBS_DIR/TeknoParrotUi.deps.json" "$LIBS_DIR/ParrotPatcher.deps.json" 2>/dev/null || true

# No debug symbols in the distributable (the native Skia PDBs alone are 100+ MB)
find "$OUTPUT_DIR" -name "*.pdb" -delete 2>/dev/null || true

# RID-specific publishes flatten native libraries; drop any leftover runtimes tree
if [ -d "$OUTPUT_DIR/runtimes" ]; then
    rm -rf "$OUTPUT_DIR/runtimes" 2>/dev/null || true
fi

echo -e "${GREEN}Moved $MOVED_COUNT dependency file(s) into libs/${NC}"

# Copy Linux-specific assets (icon and desktop file)
echo -e "${CYAN}Adding Linux desktop integration...${NC}"
ASSETS_DIR="$SCRIPT_DIR/TeknoParrotUi.Avalonia/Assets"
if [ -f "$ASSETS_DIR/teknoparrot.png" ]; then
    cp "$ASSETS_DIR/teknoparrot.png" "$OUTPUT_DIR/teknoparrot.png" 2>/dev/null || true
fi
if [ -f "$ASSETS_DIR/TeknoParrotUi.desktop" ]; then
    cp "$ASSETS_DIR/TeknoParrotUi.desktop" "$OUTPUT_DIR/TeknoParrotUi.desktop" 2>/dev/null || true
fi

# Get version and size info
EXE_PATH="$OUTPUT_DIR/TeknoParrotUi"
if [ ! -f "$EXE_PATH" ]; then
    error_exit "TeknoParrotUi executable not found at $EXE_PATH"
fi

VERSION=$(file "$EXE_PATH" | grep -oP 'version [0-9.]+' | head -1 | awk '{print $2}')
if [ -z "$VERSION" ]; then
    VERSION="unknown"
fi

SIZE_BYTES=$(find "$OUTPUT_DIR" -type f -exec du -b {} + | awk '{sum += $1} END {print sum}')
SIZE_MB=$(awk "BEGIN {printf \"%.1f\", $SIZE_BYTES / 1024 / 1024}")

echo -e "${GREEN}Published TeknoParrotUi $VERSION to $OUTPUT_DIR (${SIZE_MB} MB)${NC}"

# Create zip archive if requested
if [ "$CREATE_ZIP" = true ]; then
    ZIP_NAME="TeknoParrotUi-${VERSION}-linux-x64.zip"
    ZIP_PATH="$(dirname "$OUTPUT_DIR")/$ZIP_NAME"
    
    if [ -f "$ZIP_PATH" ]; then
        rm "$ZIP_PATH"
    fi
    
    echo -e "${CYAN}Creating $ZIP_PATH...${NC}"
    cd "$OUTPUT_DIR"
    zip -r -q "$ZIP_PATH" .
    cd - > /dev/null
    
    ZIP_SIZE_BYTES=$(stat -c%s "$ZIP_PATH")
    ZIP_SIZE_MB=$(awk "BEGIN {printf \"%.1f\", $ZIP_SIZE_BYTES / 1024 / 1024}")
    
    echo -e "${GREEN}Created $ZIP_PATH (${ZIP_SIZE_MB} MB)${NC}"
fi

echo -e "${GREEN}✓ Publish complete${NC}"

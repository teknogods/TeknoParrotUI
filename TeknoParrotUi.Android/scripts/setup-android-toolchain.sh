#!/usr/bin/env bash
# Set up the complete user-space Android toolchain for TeknoParrotUi.Android.
# Everything installs into the user's home directory — NO sudo required.
# Idempotent: safe to re-run; skips components that are already present.
#
# Installs:
#   ~/.dotnet                          .NET 8 SDK + android workload
#   ~/android-toolchain/jdk-17.*       Microsoft OpenJDK 17 (Android tooling needs 17)
#   ~/android-toolchain/sdk            Android SDK (platform-34, build-tools, platform-tools)
#
# Optional: pass --with-emulator to also install the emulator + API 34 system image (~2 GB).
set -euo pipefail

TOOLCHAIN="$HOME/android-toolchain"
DOTNET_DIR="$HOME/.dotnet"
WITH_EMULATOR=0
[ "${1:-}" = "--with-emulator" ] && WITH_EMULATOR=1

echo "==> 1/4 .NET 8 SDK (user-local)"
if [ -x "$DOTNET_DIR/dotnet" ]; then
    echo "    already installed: $("$DOTNET_DIR/dotnet" --version)"
else
    curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_DIR"
fi
export DOTNET_ROOT="$DOTNET_DIR" PATH="$DOTNET_DIR:$PATH"

echo "==> 2/4 android workload"
if dotnet workload list | grep -q android; then
    echo "    already installed"
else
    dotnet workload install android --skip-sign-check
fi

echo "==> 3/4 Microsoft OpenJDK 17"
JDK_DIR=$(ls -d "$TOOLCHAIN"/jdk-17* 2>/dev/null | head -1 || true)
if [ -n "$JDK_DIR" ]; then
    echo "    already installed: $JDK_DIR"
else
    mkdir -p "$TOOLCHAIN"
    curl -sSL -o "$TOOLCHAIN/jdk17.tar.gz" https://aka.ms/download-jdk/microsoft-jdk-17-linux-x64.tar.gz
    tar xzf "$TOOLCHAIN/jdk17.tar.gz" -C "$TOOLCHAIN"
    rm "$TOOLCHAIN/jdk17.tar.gz"
    JDK_DIR=$(ls -d "$TOOLCHAIN"/jdk-17* | head -1)
fi

echo "==> 4/4 Android SDK (platform-34)"
export JAVA_HOME="$JDK_DIR"
SDK="$TOOLCHAIN/sdk"
if [ -d "$SDK/platforms/android-34" ]; then
    echo "    already installed"
else
    # The InstallAndroidDependencies MSBuild target provisions exactly what the project needs.
    REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
    dotnet build "$REPO_ROOT/TeknoParrotUi.Android" -t:InstallAndroidDependencies \
        -p:AndroidSdkDirectory="$SDK" \
        -p:JavaSdkDirectory="$JDK_DIR" \
        -p:AcceptAndroidSDKLicenses=True --nologo
fi

if [ "$WITH_EMULATOR" = 1 ]; then
    echo "==> optional: emulator + API 34 system image (~2 GB)"
    SDKM=$(ls "$SDK"/cmdline-tools/*/bin/sdkmanager | head -1)
    # IMPORTANT: sdkmanager asks hidden license questions — always pipe `yes`.
    yes | "$SDKM" --sdk_root="$SDK" --licenses > /dev/null || true
    yes | "$SDKM" --sdk_root="$SDK" "emulator" "system-images;android-34;google_apis;x86_64"
fi

echo
echo "Toolchain ready:"
echo "  DOTNET_ROOT=$DOTNET_DIR"
echo "  JAVA:       $JDK_DIR"
echo "  SDK:        $SDK"
echo
echo "Next: scripts/build-apk.sh"

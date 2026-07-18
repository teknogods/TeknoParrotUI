#!/usr/bin/env bash
# Set up the complete user-space Android toolchain for TeknoParrotUi.Android.
# Everything installs into the user's home directory — NO sudo required.
# Idempotent: safe to re-run; skips components that are already present.
#
# Installs:
#   ~/.dotnet                          .NET 8 + .NET 10 SDKs, Android workload
#   ~/android-toolchain/jdk-17.*       Microsoft OpenJDK 17 (Android tooling needs 17)
#   ~/android-toolchain/sdk            Android SDK (platform-34, build-tools, platform-tools)
#
# Optional: pass --with-emulator to also install the emulator + API 34 system image (~2 GB).
set -euo pipefail

TOOLCHAIN="$HOME/android-toolchain"
DOTNET_DIR="$HOME/.dotnet"
WITH_EMULATOR=0
[ "${1:-}" = "--with-emulator" ] && WITH_EMULATOR=1

echo "==> 1/5 .NET 8 + .NET 10 SDKs (user-local)"
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
if [ ! -x "$DOTNET_DIR/dotnet" ] || ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^8\.'; then
    bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$DOTNET_DIR"
else
    echo "    .NET 8 already installed"
fi
if ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^10\.'; then
    bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$DOTNET_DIR"
else
    echo "    .NET 10 already installed"
fi
export DOTNET_ROOT="$DOTNET_DIR" PATH="$DOTNET_DIR:$PATH"

install_android_workload() {
    local major="$1"
    local sdk_version
    local selector="$TOOLCHAIN/dotnet-${major}-select"
    sdk_version=$(dotnet --list-sdks | awk -v prefix="${major}." 'index($1, prefix) == 1 { version=$1 } END { print version }')
    [ -n "$sdk_version" ] || { echo "Could not resolve .NET $major SDK" >&2; exit 1; }
    mkdir -p "$selector"
    dotnet new globaljson --sdk-version "$sdk_version" --output "$selector" --force >/dev/null
    if (cd "$selector" && dotnet workload list | grep -q android); then
        echo "    .NET $major android workload already installed"
    else
        (cd "$selector" && dotnet workload install android --skip-sign-check)
    fi
}

echo "==> 2/5 Android workloads (.NET 8 harness + .NET 10 full UI)"
install_android_workload 8
install_android_workload 10

echo "==> 3/5 Microsoft OpenJDK 17"
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

echo "==> 4/5 Android SDK (platform-34)"
export JAVA_HOME="$JDK_DIR"
SDK="$TOOLCHAIN/sdk"
if [ -d "$SDK/platforms/android-34" ]; then
    echo "    already installed"
else
    # The InstallAndroidDependencies MSBuild target provisions exactly what the project needs.
    REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
    (cd "$TOOLCHAIN/dotnet-8-select" && \
        dotnet build "$REPO_ROOT/TeknoParrotUi.Android" -t:InstallAndroidDependencies \
            -p:AndroidSdkDirectory="$SDK" \
            -p:JavaSdkDirectory="$JDK_DIR" \
            -p:AcceptAndroidSDKLicenses=True --nologo)
fi

if [ "$WITH_EMULATOR" = 1 ]; then
    echo "==> 5/5 optional: emulator + API 34 system image (~2 GB)"
    SDKM=$(ls "$SDK"/cmdline-tools/*/bin/sdkmanager | head -1)
    # IMPORTANT: sdkmanager asks hidden license questions — always pipe `yes`.
    yes | "$SDKM" --sdk_root="$SDK" --licenses > /dev/null || true
    yes | "$SDKM" --sdk_root="$SDK" "emulator" "system-images;android-34;google_apis;x86_64"
fi

echo
echo "Toolchain ready:"
echo "  DOTNET_ROOT=$DOTNET_DIR"
echo "  .NET 8 SDK selector:  $TOOLCHAIN/dotnet-8-select"
echo "  .NET 10 SDK selector: $TOOLCHAIN/dotnet-10-select"
echo "  JAVA:       $JDK_DIR"
echo "  SDK:        $SDK"
echo
echo "Next: scripts/build-apk.sh"

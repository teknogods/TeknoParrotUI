#!/usr/bin/env bash
# Build the TeknoParrotUi.Android APK (debug-signed, installable via adb).
# Requires scripts/setup-android-toolchain.sh to have been run first.
set -euo pipefail

TOOLCHAIN="$HOME/android-toolchain"
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
JDK_DIR=$(ls -d "$TOOLCHAIN"/jdk-17* | head -1)
SDK="$TOOLCHAIN/sdk"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

# EmbedAssembliesIntoApk=true is REQUIRED for a plain `adb install`:
# without it, Debug builds use "Fast Deployment" (assemblies pushed separately
# by the IDE) and the app aborts at startup with "No assemblies found".
dotnet build "$REPO_ROOT/TeknoParrotUi.Android" -t:SignAndroidPackage \
    -p:EmbedAssembliesIntoApk=true \
    -p:AndroidSdkDirectory="$SDK" \
    -p:JavaSdkDirectory="$JDK_DIR" \
    --nologo "$@"

APK="$REPO_ROOT/TeknoParrotUi.Android/bin/Debug/net8.0-android/com.teknoparrot.inputtest-Signed.apk"
echo
echo "APK: $APK"
ls -lh "$APK"

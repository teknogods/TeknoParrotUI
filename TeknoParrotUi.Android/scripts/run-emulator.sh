#!/usr/bin/env bash
# Boot the Android emulator for TeknoParrot input testing.
# Creates the AVD on first run. Requires setup-android-toolchain.sh --with-emulator.
#
# Usage:
#   scripts/run-emulator.sh              # windowed (interactive testing)
#   scripts/run-emulator.sh --headless   # no window (CI / automated testing)
#
# The script waits until Android has fully booted, then returns
# (the emulator keeps running in the background; stop with: adb emu kill).
set -euo pipefail

TOOLCHAIN="$HOME/android-toolchain"
SDK="$TOOLCHAIN/sdk"
JDK_DIR=$(ls -d "$TOOLCHAIN"/jdk-17* | head -1)
ADB="$SDK/platform-tools/adb"
AVD_NAME="tp_test"
HEADLESS_ARGS=""
[ "${1:-}" = "--headless" ] && HEADLESS_ARGS="-no-window"

export ANDROID_HOME="$SDK" ANDROID_SDK_ROOT="$SDK" JAVA_HOME="$JDK_DIR"

# KVM makes the x86_64 image usable; warn if missing.
if [ ! -w /dev/kvm ]; then
    echo "WARNING: /dev/kvm not accessible — emulator will be very slow."
    echo "         Fix: sudo usermod -aG kvm \$USER  (then re-login)"
fi

# Create the AVD on first run.
if [ ! -d "$HOME/.android/avd/$AVD_NAME.avd" ]; then
    echo "==> Creating AVD '$AVD_NAME' (Pixel 5, API 34)"
    AVDM=$(ls "$SDK"/cmdline-tools/*/bin/avdmanager | head -1)
    echo no | "$AVDM" create avd -n "$AVD_NAME" \
        -k "system-images;android-34;google_apis;x86_64" --device "pixel_5"
fi

echo "==> Booting emulator ($([ -n "$HEADLESS_ARGS" ] && echo headless || echo windowed))"
nohup "$SDK/emulator/emulator" -avd "$AVD_NAME" \
    $HEADLESS_ARGS -no-audio -no-boot-anim -no-snapshot -no-metrics \
    -gpu swiftshader_indirect \
    > /tmp/tp-emulator.log 2>&1 &

echo "==> Waiting for boot (first boot can take a few minutes)..."
"$ADB" wait-for-device
for i in $(seq 1 60); do
    boot=$("$ADB" shell getprop sys.boot_completed 2>/dev/null | tr -d '\r' || true)
    [ "$boot" = "1" ] && break
    sleep 5
done

if [ "$boot" = "1" ]; then
    echo "==> Emulator booted: $("$ADB" devices | sed -n 2p)"
    echo "    Log: /tmp/tp-emulator.log"
    echo "    Stop with: $ADB emu kill"
else
    echo "ERROR: emulator did not boot in time — check /tmp/tp-emulator.log"
    exit 1
fi

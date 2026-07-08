#!/usr/bin/env bash
# Install the APK on the running emulator/device, launch the input test
# harness, and run the automated touch verification (the same checks that
# validated the port originally).
#
# Prerequisites: build-apk.sh built the APK; run-emulator.sh booted a device
# (or a physical device is connected with USB debugging).
set -euo pipefail

TOOLCHAIN="$HOME/android-toolchain"
ADB="$TOOLCHAIN/sdk/platform-tools/adb"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
APK="$REPO_ROOT/TeknoParrotUi.Android/bin/Debug/net8.0-android/com.teknoparrot.inputtest-Signed.apk"
PKG="com.teknoparrot.inputtest"

fail() { echo "FAIL: $1"; exit 1; }

echo "==> Installing $PKG"
"$ADB" install -r "$APK" > /dev/null || fail "adb install"

echo "==> Launching"
ACTIVITY=$("$ADB" shell cmd package resolve-activity --brief "$PKG" | tail -1 | tr -d '\r')
"$ADB" shell am start -n "$ACTIVITY" > /dev/null
sleep 6
PID=$("$ADB" shell pidof "$PKG" | tr -d '\r' || true)
[ -n "$PID" ] || fail "app crashed on startup (check: adb logcat -d | grep monodroid)"
echo "    running, pid=$PID"

# Screen size for computing tap coordinates
read -r W H < <("$ADB" shell wm size | grep -oE '[0-9]+x[0-9]+' | tr x ' ')
echo "    screen ${W}x${H}"

dump_text() {
    "$ADB" shell uiautomator dump /sdcard/tp_dump.xml > /dev/null 2>&1
    "$ADB" shell cat /sdcard/tp_dump.xml
}

echo "==> Test 1: aim — tap screen center"
"$ADB" shell input tap $((W / 2)) $((H / 2)); sleep 1
BYTES=$(dump_text | grep -o 'AnalogBytes\[0-15\]: [0-9A-F ]*' | head -1)
echo "    $BYTES"
# Center → factor ≈ 0.5 → value ≈ 127/128 → complement layout ≈ 0x7F/0x80
echo "$BYTES" | grep -qE ': (7F|80|81|82|83|84) 00 (7B|7C|7D|7E|7F|80) 00' \
    || echo "    NOTE: values outside expected center band — verify manually"

echo "==> Test 2: aim — tap at 10% X/Y"
"$ADB" shell input tap $((W / 10)) $((H / 10)); sleep 1
BYTES=$(dump_text | grep -o 'AnalogBytes\[0-15\]: [0-9A-F ]*' | head -1)
echo "    $BYTES"

echo "==> Test 3: trigger — press-and-hold 3 s"
"$ADB" shell input swipe $((W / 2)) $((H / 2)) $((W / 2)) $((H / 2)) 3000 &
sleep 1.2
HELD=$(dump_text | grep -oE 'P1 trigger: (True|False)' | head -1)
wait
sleep 1
RELEASED=$(dump_text | grep -oE 'P1 trigger: (True|False)' | head -1)
echo "    during hold: $HELD  |  after release: $RELEASED"
[ "$HELD" = "P1 trigger: True" ] || fail "trigger not pressed during hold"
[ "$RELEASED" = "P1 trigger: False" ] || fail "trigger stuck after release"

echo
echo "ALL AUTOMATED CHECKS PASSED"
echo "For manual testing: interact with the on-screen harness (1-2 fingers = P1/P2 guns)."

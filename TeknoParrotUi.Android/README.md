# TeknoParrotUi.Android — Build & Emulator Guide

Android head for the cross-platform input refactor (Phase 3). Contains
`AndroidTouchListener` (touch → light-gun input via the shared, oracle-verified
`GunAnalogMath`) and an on-device input test harness (`MainActivity`).

This project is **intentionally not in `TeknoParrotUI.sln`** so desktop builds
never require the Android workload.

> Games are x86 Windows binaries — this head delivers the *input stack* and its
> test harness, not game execution.

---

## Quick start (scripts)

```bash
# One-time toolchain setup (everything user-space, NO sudo).
# Add --with-emulator for the emulator + API 34 system image (~2 GB extra).
TeknoParrotUi.Android/scripts/setup-android-toolchain.sh --with-emulator

# Build the debug-signed APK
TeknoParrotUi.Android/scripts/build-apk.sh

# Boot the emulator (omit --headless for a visible window)
TeknoParrotUi.Android/scripts/run-emulator.sh --headless

# Install + launch + automated touch verification
TeknoParrotUi.Android/scripts/deploy-and-test.sh

# Stop the emulator when done
~/android-toolchain/sdk/platform-tools/adb emu kill
```

---

## What the toolchain setup installs (and where)

| Component | Location | Why |
|---|---|---|
| .NET 8 SDK (user-local) | `~/.dotnet` | distro/system dotnet is often root-owned; workloads need write access |
| `android` workload | inside `~/.dotnet` | net8.0-android target support |
| Microsoft OpenJDK 17 | `~/android-toolchain/jdk-17.*` | Android tooling requires JDK 17 (newer JDKs fail) |
| Android SDK platform-34 etc. | `~/android-toolchain/sdk` | provisioned by the `InstallAndroidDependencies` MSBuild target |
| Emulator + API 34 x86_64 image (optional) | `~/android-toolchain/sdk` | headless/windowed testing under KVM |

## Manual build (what the scripts do)

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
JDK=$(ls -d ~/android-toolchain/jdk-17* | head -1)

dotnet build TeknoParrotUi.Android -t:SignAndroidPackage \
    -p:EmbedAssembliesIntoApk=true \
    -p:AndroidSdkDirectory="$HOME/android-toolchain/sdk" \
    -p:JavaSdkDirectory="$JDK"
# → TeknoParrotUi.Android/bin/Debug/net8.0-android/com.teknoparrot.inputtest-Signed.apk
```

## Gotchas (learned the hard way — all handled by the scripts)

1. **`-p:EmbedAssembliesIntoApk=true` is mandatory** for plain `adb install`
   of Debug builds. Without it, .NET Android uses *Fast Deployment* (assemblies
   pushed separately by the IDE) and the app aborts at startup with
   `monodroid: No assemblies found ... Exiting`.
2. **Always pipe `yes |` into `sdkmanager`.** It asks license questions even
   mid-download; when its output is piped/filtered, the prompt is invisible
   and the process hangs forever.
3. **Don't pipe the emulator through `head`/`grep`** — SIGPIPE kills it.
   Redirect to a log file instead (the script uses `/tmp/tp-emulator.log`).
4. **KVM**: `/dev/kvm` must be writable (`sudo usermod -aG kvm $USER`) or the
   x86_64 image crawls.
5. The launcher activity's Java name is CRC-mangled
   (e.g. `crc648402895512007aa4.MainActivity`); resolve it with
   `adb shell cmd package resolve-activity --brief com.teknoparrot.inputtest`.

## Using the test harness

Launch the app: the screen shows live JVS state that a game would receive.

- **Touch** anywhere → P1 gun aim (`AnalogBytes[0..3]` update; standard
  RawInput complement layout, 0–255 default range)
- **Press/release** → P1 trigger (`Button1`)
- **Second finger** → P2 gun (slots `[4..7]`)

The `deploy-and-test.sh` script automates exactly this via `adb shell input`
and `uiautomator dump`, asserting:
- center tap ⇒ analog bytes ≈ `0x7F/0x80` (complemented)
- 10 % tap ⇒ predicted complement values in the correct X/Y slots
- press-and-hold ⇒ `P1 trigger: True`; release ⇒ `False`

## Architecture notes

- `AndroidTouchListener` implements `TeknoParrotUi.Common.InputListening.IInputListener`
  and `View.IOnTouchListener`. At app start it registers itself via
  `InputListenersManager.AndroidTouchListenerFactory` (Common has no Android
  references; the head injects the factory).
- At game launch, `InputListenersManager` selects it when the game has gun
  intent and the game's `InputProfile` has `AndroidTouch` enabled.
- Aim math is `GunAnalogMath` — shared with the Linux evdev listener and
  verified byte-identical to the Windows RawInput listener by
  `Tools/InputMethodAudit -- gun-math-test` (384 cases).

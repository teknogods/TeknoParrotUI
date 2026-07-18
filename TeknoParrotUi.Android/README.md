# TeknoParrotUi.Android — Build & Emulator Guide

Android development guide for the input harness and the full Avalonia TPUI +
managed Winlator companion. The production-shaped path can now import validated
games, provision the managed container/runtime, activate a subscription through
BudgieLoader, launch OpenParrot or TeknoParrot.dll, forward controls, and stop
the complete guest session without opening Winlator's main UI.

This project is **intentionally not in `TeknoParrotUI.sln`** so desktop builds
never require the Android workload.

The small `TeknoParrotUi.Android` project remains the emulator-friendly input
harness. The game-capable ARM64 application is
`TeknoParrotUi.Avalonia.Android`; it uses the signed
`com.teknoparrot.winlator` companion because the Windows games are x86/x64.

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
| .NET 8 + .NET 10 SDKs (user-local) | `~/.dotnet` | .NET 8 builds the input harness; .NET 10 builds the full Avalonia Android shell |
| `android` workloads | inside `~/.dotnet` | installed separately for both SDK feature bands through SDK-specific selectors |
| Microsoft OpenJDK 17 | `~/android-toolchain/jdk-17.*` | Android tooling requires JDK 17 (newer JDKs fail) |
| Android SDK platform-34 etc. | `~/android-toolchain/sdk` | provisioned by the `InstallAndroidDependencies` MSBuild target |
| Emulator + API 34 x86_64 image (optional) | `~/android-toolchain/sdk` | headless/windowed testing under KVM |

## Manual build (what the scripts do)

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH"
JDK=$(ls -d ~/android-toolchain/jdk-17* | head -1)
REPO_ROOT="$PWD"

(cd ~/android-toolchain/dotnet-8-select && dotnet build "$REPO_ROOT/TeknoParrotUi.Android" -t:SignAndroidPackage \
    -p:EmbedAssembliesIntoApk=true \
    -p:AndroidSdkDirectory="$HOME/android-toolchain/sdk" \
    -p:JavaSdkDirectory="$JDK")
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

## Playing and importing games from TPUI

Normal users need only the signed TPUI and TeknoParrot Winlator companion APKs.
The bridge-probe application is a development fixture and is not part of the
player workflow.

1. Put each game folder below
   `Download/TeknoParrotGames` on the Android device.
2. Open TPUI, choose **Library → Rom Scanner**, and select the
   `TeknoParrotGames` folder. Android remembers this document-tree grant.
3. Choose **Scan Launchable Games**, then **Import Found Games**. TPUI matches
   validated recipes, creates or updates the user profiles, and assigns the
   correct executable.
4. Return to **Library** and choose **Launch Game**. On the first launch only,
   Android asks whether Winlator may access the game files; choose **Allow**.
5. TPUI creates/updates the managed container, stages OpenParrot and bridge
   assets, and starts the game. There is no Winlator container/file-manager
   setup step. Use TPUI's **Force Quit Game** button or notification Stop action
   to end the whole managed guest session.

There are currently 24 validated x86 recipes. `3DCosplayMahjong`,
`RastanSaga`, `SR3` (Sega Rally 3 through OpenParrot), `PuzzleBobble`, and
`TetrisTheGrandMaster3TerrorInstinct` have booted on the Fold6. Puzzle Bobble
reached gameplay with FastIO controls; TGM3 renders through Zink and its JVS
buttons work. The 21-title `next_test` cohort has passed folder/executable
matching, launch-plan validation, controller-profile coverage, and
input-encoder tests and is being qualified on physical hardware. The cohort
covers FastIO, generic and game-specific JVS, ExBoard shared pages, Raw Thrills
shared pages, and the Frenzy Express, GRID, and GTI Club shared-page layouts.
All current recipes are 32-bit and use the managed x86/WoW64 guest path.

Recipe frame limits apply to both rendering families: DXVK receives its native
cap, while OpenGL-over-Zink uses Android's matching display mode plus Mesa
vblank/FIFO presentation. TGM3 has a dedicated editable three-button profile.

Adding another folder is automatic when that profile has a validated JSON
recipe in `TeknoParrotUi.Common/AndroidLaunchRecipes`. A new title still needs
one recipe because executable choice, architecture, loader arguments, working
directory, input transport, frame limit, and OpenParrot injection requirements
are game-specific. Recipes are data, not per-game Android code or per-game
APKs, and TPUI validates them before they can be imported or launched.

## Subscription activation on Android

Open **Subscription**, enter the normal TeknoParrot subscription serial, and
choose **Register**. TPUI binds to the signature-protected service in the
matching Winlator companion. If game-folder access has not been granted yet,
TPUI opens Winlator's narrow permission activity and resumes the same operation
after Allow. Activation is rejected while a game owns the managed container.

The v5 bridge runs `E:\TeknoParrotRuntime\TeknoParrot\BudgieLoader.exe` inside
the same managed Wine prefix used by every TPUI-launched game. BudgieLoader
writes the generated subscription activation there, so the game runtime can
consume it during initialization without an Android registry emulation layer or
a DLL change. The serial exists only in memory for the protected Binder call and
BudgieLoader command; TPUI never stores it in an Intent, preference, profile,
log, or status response. The generated registry value stays in Winlator's
app-private Wine prefix. **Deactivate** runs BudgieLoader in that same prefix
and verifies that the value is gone.

Production companion builds must package the core runtime as well as
OpenParrot:

```powershell
$env:TEKNOPARROT_OPENPARROT_WIN32 = 'C:\path\to\OpenParrotWin32'
$env:TEKNOPARROT_CORE_WIN32 = 'C:\path\to\TeknoParrot'
cd WinlatorFork\app
.\gradlew.bat :app:assembleDebug --no-daemon
```

`TEKNOPARROT_CORE_WIN32` must contain `BudgieLoader.exe` and
`TeknoParrot.dll`. Developer companions built without them continue to support
OpenParrot recipes, but the Subscription page reports activation as unavailable
and TeknoParrot.dll recipes remain gated.

## Full Winlator service lab from Windows

`run-winlator-service-lab.ps1` defaults to the API 34 x86_64 emulator and the
small Winlator service stub. When more than one ADB target is connected it
refuses to guess; pass the target serial explicitly.

Run the real ARM64 pair on a physical device with:

```powershell
cd TeknoParrotUi.Android\scripts
.\run-winlator-service-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -RuntimeIdentifier android-arm64 `
    -WinlatorVariant Full
```

This builds and update-installs the Debug TeknoParrot UI plus full Winlator
APK with `adb install --no-streaming -r`, preserving existing app data. It then
tests the signature-protected service, Winlator-owned TPJ1 page, authenticated
TPB1 pipe, 16 framed echoes, and 100 prepare/stop cycles. Use
`-SkipBuild -SkipInstall` to rerun the probe against matching APKs already on
the device. Full mode fails unless session shutdown has removed every test page
and per-session directory after the 100-cycle run. It also runs the official
16 KB ZIP-alignment check and audits every packaged ARM64 ELF `PT_LOAD` segment,
warning with the exact names of incompatible prebuilts without blocking a 4 KB
compatibility-device lab.

To run the controlled Windows guest integration gate instead, activate the
target Winlator container once and add `-GuestDiagnostic`:

```powershell
.\run-winlator-service-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -RuntimeIdentifier android-arm64 `
    -WinlatorVariant Full `
    -GuestDiagnostic `
    -ContainerId 1
```

This mode negotiates service protocol v5, sends a bounded versioned session
specification, and launches a fixed Debug-only batch through the bound Winlator
service. It verifies TPB1 authentication and exact bytes through real x64 and
x86/WoW64 Windows named pipes plus both directions of the Binder-created
4096-byte page. It uses neither `adb reverse` nor a file-manager coordinate tap.
The raw 256-bit token is never returned in `PreparedSession`. The diagnostic
page and its session link are intentionally retained for inspection; normal
service-fixture mode keeps its 100-cycle cleanup assertion.

The current diagnostic additionally requires a live wrong-token helper to be
rejected without `OKAY`. Each valid architecture then sends one randomized MiB
to Android, disconnects, reconnects through the same helper, and validates one
randomized MiB in the opposite direction. The staged fixtures pass the native
test below, and Fold6 session `<session-id>`
passed the complete packaged x64 and x86/WoW64 diagnostic with parent exit zero
and no remaining Wine, Box64, helper, or guest process.

The matching helpers can be rebuilt and tested natively before a device run:

```powershell
.\Tools\ProtonPipeHelper\build-winlator-bridge-guest.ps1
.\Tools\ProtonPipeHelper\test-tpb1-helper.ps1
```

The native test uses real Windows named pipes and loopback sockets for x64 and
x86. It checks fixed vectors, one randomized MiB in each direction, reconnects
on the same helper, verifies shared-page markers, and requires a wrong-token
connection to receive no `OKAY` acknowledgement. The helper uses overlapped
named-pipe I/O so sustained duplex traffic and shutdown do not depend on
simultaneous synchronous operations on one handle.

The first phone-independent control-forwarding slice can be tested separately:

```powershell
dotnet build .\Tools\InputMethodAudit\InputMethodAudit.csproj -c Debug
dotnet .\Tools\InputMethodAudit\bin\Debug\net8.0\InputMethodAudit.dll forwarded-input-test
```

This validates the C# TPI1 golden vector, strict framing, partial stream reads,
per-device sequences/state, and stuck-control release. The matching pure-Java
encoder and preallocated Winlator SPSC queue are exercised by
`Tools\WinlatorInputProtocolTest.java`; both languages emit identical bytes.
The separate signed Android probe authenticates TPB1 channel kind 2 and sends
six synthetic frames to the host service. On the Fold6 it verified button,
axis, absolute pointer, one intentional sequence gap, and focus-loss release.
Full Winlator uses a production prepared-session client with a bounded
preallocated queue, independent device sequences, TPB1 authentication,
overflow reset, and reconnect. Its session-gated, non-consuming observer is
wired into `XServerDisplayActivity` key, changed-axis, absolute-pointer,
input-device, focus, resume, pause, and destroy dispatch points. The append-only
Activity API negotiates the `PreparedActivityLaunch` feature, rejects
container/session/kind substitution and executable injection, then launches the
real, non-exported `XServerDisplayActivity` through one explicit-intent
boundary.

The full UI now routes all 24 validated x86 profiles through that handoff and
publishes the resulting control state through the recipe-selected FastIO, JVS,
or shared-page adapter. The TPUI foreground service owns the session so
Activity recreation, UI hibernation, or returning to the launcher does not
terminate Wine. The editable on-screen layouts and physical Android gamepads
share the same forwarded-input stream; unsupported profiles and undeclared
architectures remain fail-closed.

## Manual Windows guest pipe/shared-page lab

After the full companion APK and a test container are installed on an ARM64
device, run the real Wine/Box64 data-path fixture with:

```powershell
cd TeknoParrotUi.Android\scripts
.\run-winlator-guest-bridge-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -ContainerId 1
```

The script builds native x64 and x86 Windows peers with the Visual Studio C++
tools, stages both peers and both `pipehelper` architectures into Winlator's
private prefix, and verifies:

- exact bidirectional bytes through a real Windows named pipe;
- Android/rootfs page bytes reaching a Windows named mapping;
- Windows mapping writes returning to the Android/rootfs page;
- clean zero exits and explicit helper shutdown for x64 and x86/WoW64.

Use `-SkipBuild` when the guest EXEs already exist. The default
`-FileTapX`/`-FileTapY` values match the tested Fold6 file-manager grid. This
lower-level lab remains useful for isolating Wine/Box64 from Binder; the
`-GuestDiagnostic` service lab above is the primary integration gate.

# TeknoParrot Android bridge protocol

**Status:** service protocol v13/data protocol v1 implemented; Binder-controlled x64/x86 Windows guests pass authenticated named-pipe/shared-page stress, full Winlator sends queued TPI1 frames over the prepared session on ARM64 hardware, and recipe-selected Sega Rally, FastIO, and JVS host transports now build as of 2026-07-18
**Production target:** TeknoParrotUI Android host plus the signed TeknoParrot Winlator companion fork

This document records the protocol implemented by `TeknoParrotUi.AndroidBridge`,
the production-direction `GameSessionService`, the lab `ArcadeSessionService`,
the separate `com.teknoparrot.bridgeprobe` APK, and the pinned Winlator fork's
`TeknoParrotBridgeService`. The service labs prove
Android process isolation, Binder/AIDL, authenticated loopback I/O,
descriptor-backed shared memory, and the production service ownership
direction. The Android foreground service now owns the long-lived host side of
that session instead of an Avalonia Activity. The controlled Windows guest lab
joins those paths: TeknoParrotUI prepares and maps the Winlator-owned page, then
asks the bound service to launch native x64 and x86 fixtures through Wine/Box64.
The shared UI now selects that host through `IGameSession`/`GameSessionFactory`;
Windows and Linux continue to use the existing desktop `GameSession`. The first
Android converter accepts Rastan Saga and 3D Cosplay Mahjong, persists the
profile identity with the private session record, and lets a recreated UI
attach to the same service state without submitting a second launch request.
The existing helper carries real
named-pipe bytes, authenticates its session/pipe/token with TPB1, and mirrors the
page without a file-manager tap or ADB reverse.

The current lab is a compatibility fixture, not yet the complete Winlator API.
Production additions and ownership changes are listed at the end.

## 1. Participants and trust boundary

| Participant | Package | Current role |
| --- | --- | --- |
| Host | `com.teknoparrot.ui` | `GameSessionService` owns the production-direction session token, exact loopback listener, mapped Winlator page, heartbeat, and input state; `ArcadeSessionService` remains a lab fixture |
| Probe | `com.teknoparrot.bridgeprobe` | Acts as a future Winlator-side client in a separate Android process |
| Winlator companion | `com.teknoparrot.winlator` | Owns the production-direction service, session page, and controlled diagnostic Wine/Box64 execution |

Binding requires the signature-level permission
`com.teknoparrot.permission.BIND_BRIDGE`. The host and client APKs therefore
must be signed with the same development or release key. The service is also
bound by explicit package and class name; implicit discovery is not used.

The TCP listener binds only to `127.0.0.1` on an ephemeral port. Possession of
the port number is insufficient: every connection must prove the 256-bit
session token in its first frame.

## 2. Binder control plane

Service identity:

| Field | Value |
| --- | --- |
| Action | `com.teknoparrot.bridge.v1.BIND` |
| Package | `com.teknoparrot.ui` |
| Class | `com.teknoparrot.bridge.ArcadeSessionService` |
| Interface descriptor | `com.teknoparrot.bridge.v1.ITeknoParrotBridgeService` |

The lab AIDL methods are:

```aidl
int getProtocolVersion();
String prepareTestSession(String clientName);
String getSessionStatus(String sessionId);
void stopTestSession(String sessionId);
```

The production-direction fixture exposes this second service:

| Field | Value |
| --- | --- |
| Action | `com.teknoparrot.bridge.v1.WINLATOR_BIND` |
| Package | `com.teknoparrot.winlator` |
| Class | `com.winlator.teknoparrot.TeknoParrotBridgeService` |
| Interface descriptor | `com.teknoparrot.bridge.v1.ITeknoParrotWinlatorService` |

Its current compatibility and v13 contract is:

```aidl
int getProtocolVersion();
byte[] getCapabilities(int clientProtocolVersion);
byte[] prepareSession(in byte[] spec);
String launchPreparedGuestDiagnostic(String sessionId);
String prepareTestSession(String clientName);
String getSessionStatus(String sessionId);
String runPipeProbe(String sessionId, int port, String tokenHex);
String launchGuestBridgeDiagnostic(String sessionId, int containerId, int port);
String getGuestBridgeDiagnosticStatus(String sessionId);
void stopGuestBridgeDiagnostic(String sessionId);
void stopTestSession(String sessionId);
String runPreparedInputDiagnostic(String sessionId);
String launchPreparedInputActivityDiagnostic(String sessionId);
String launchPreparedActivity(in byte[] request);
```

Service protocol v13 is independent from TPB1/TPJ1/TPI1 data protocol v1. The .NET 10
Android AIDL generator currently emits invalid readers for custom parcelables
and even `Bundle`, so the versioned methods use bounded UTF-8 JSON byte
envelopes. Both sides reject empty/oversized envelopes, wrong field types,
unsupported versions, invalid IDs/tokens/ports/names/page sizes, and unknown
session or launch kinds. Typed Java and C# models sit above the byte transport.

The versioned `SessionSpec` contains:

| Field | Validation |
| --- | --- |
| `protocolVersion` | `1..13`; current client requests `13` |
| `clientName` | non-empty, maximum 80 characters |
| `requestedSessionId` | 16 bytes as 32 hexadecimal characters |
| `tokenHex` | 32 random bytes as 64 hexadecimal characters |
| `containerId` | positive selected Winlator container |
| `pipePort` | host-owned loopback port in `1..65535` |
| `pipeName64`, `pipeName32` | one exact production pair (`TeknoParrotPipe64`/`TeknoParrotPipe` or `TeknoParrot_JVS64`/`TeknoParrot_JVS`) or the diagnostic pair; maximum 128 UTF-8 bytes |
| `sharedPageBytes` | exactly 4096 |
| `sessionFlags` | `1` for the fixed Debug diagnostic or `2` for a production game session |

`PreparedSession` returns the effective immutable settings, feature flags, and
`state=ready`. It deliberately omits `tokenHex`; the host rejects a response
that contains it. Capabilities currently declare descriptor sharing, TPB1,
x64, x86/WoW64, controlled diagnostics, versioned sessions, forwarded input,
prepared Activity launch, display policy, and complete profile configuration.

`launchPreparedActivity` is the production-shaped Activity handoff. Its current
envelope is capped at 32768 UTF-8 bytes and starts with four common fields:

| Field | Validation |
| --- | --- |
| `protocolVersion` | exactly service protocol `13` |
| `sessionId` | 16 bytes as 32 hexadecimal characters; must name the active prepared session |
| `containerId` | positive and equal to the prepared container choice |
| `launchKind` | `forwarded-input-diagnostic` or `windows-executable` |

The diagnostic kind accepts exactly the four common fields. The Windows kind
requires exactly twelve additional fields:

| Field | Validation |
| --- | --- |
| `executable` | absolute scoped DOS path on `C:`, `D:`, `E:`, or `G:` ending in `.exe`; maximum 512 characters |
| `workingDirectory` | absolute scoped DOS directory on `C:`, `D:`, `E:`, or `G:`; maximum 512 characters |
| `arguments` | JSON string array, at most 32 entries and 512 characters per entry; quotes and control characters rejected |
| `libraryDirectory` | `null` or an absolute scoped DOS directory with the same validation |
| `controlsProfileId` | Winlator input-controls profile ID in `0..1000000`; validated production recipes provide a positive ID |
| `frameRateLimit` | DXVK D3D9/DXGI frame cap in `0..1000`; `0` leaves the container uncapped |
| `resolutionWidth`, `resolutionHeight` | both `0` to preserve the game INI, or a complete resolution from `320x240` through `8192x8192` |
| `debugLoggingEnabled` | required Boolean; `false` selects the low-overhead prepared-game path and `true` enables the bounded troubleshooting path |
| `compatibilityPreset` | exact allow-listed runtime/media compatibility policy or an empty string |
| `displayMode` | `centered`, `aspect-fit`, or `fullscreen`; centered is the safe default and keeps `Windowed=1` at native size, aspect-fit keeps `Windowed=1` but applies Winlator's experimental renderer scaling, and fullscreen writes `Windowed=0` |
| `profileConfigIni` | complete profile-generated `teknoparrot.ini`, non-empty and at most 16384 UTF-8 bytes; NUL and non-text control characters are rejected |

Unknown or missing fields are rejected. Winlator validates the request again
against its immutable prepared record, then one shared launcher builds an
explicit intent for the non-exported `XServerDisplayActivity`. It passes the
argument vector structurally to the launcher rather than accepting a shell
command. The compatibility method `launchPreparedInputActivityDiagnostic`
delegates to that same boundary. Neither envelope contains or returns the
session token. Capabilities advertise this boundary with `ScopedWindowsPath`.

The managed TeknoParrot container assigns `D:` to Android Downloads, keeps
`E:` inside Winlator's private application storage, and assigns `G:` only to
`/storage/emulated/0/TeknoParrotGames`. Ordinary Winlator containers do not
receive `G:` automatically, and TPUI rejects shared-storage game paths outside
Downloads and that dedicated library.

For prepared games, the per-profile diagnostic Boolean controls the entire
logging chain rather than Winlator's global preferences. Performance mode
discards guest stdout/stderr at the process boundary without creating reader
threads, disables Wine/DXVK/VKD3D/Mesa/Box64 diagnostics, suppresses routine
pipe/shared-page traces, and leaves fatal launch errors available. Diagnostic
mode restores the guest log reader, Winlator log menu, window-map traces, bridge
status, and arcade-protocol samples for user troubleshooting. Ordinary Winlator,
Windows, and Linux launches are unaffected.

`runPipeProbe(sessionId, port, tokenHex)` is the Android-only transport fixture.
Winlator creates the TPJ1 page in its
private session directory, publishes the guest heartbeat, connects explicitly
to `127.0.0.1`, authenticates as `TeknoParrot_WinlatorProbe`, and exchanges 16
framed messages with the TeknoParrotUI listener. The raw token is a test-only
argument and will move into a versioned session object without logging.

The older guest-diagnostic operations are Debug-only and intentionally cannot
accept an arbitrary executable. They validate the active session/container,
stage a fixed batch plus x64/x86 helpers and peers, expose the same app-private
page through a unique link inside the selected prefix, start Wine through
Winlator's `GuestProgramLauncherComponent`, report state/elapsed time/exit code,
and make stop idempotent. The prepared launch obtains its port, token, pipe
names, page, and container only from the validated immutable session.

`prepareTestSession` returns this temporary lab encoding:

```text
<32 lowercase/uppercase-neutral UUID hex>|<decimal TCP port>|<64 token hex>
```

The token is exactly 32 random bytes and the port is in `1..65535`. Production
code does not use this lab encoding. The foreground service currently keeps its
versioned recovery record in app-private preferences and never includes the raw
token in user-visible status or logs. Moving that secret to Android
Keystore-backed storage remains a release-hardening task.

### Shared-page descriptor transaction

The .NET 10 managed AIDL generator used by this project does not currently
generate the required framework-parcelable signature reliably. The lab sends
the descriptor with a reserved Binder transaction:

```text
code = FIRST_CALL_TRANSACTION + 32
request  = interface token, session UUID in N format
response = no-exception marker, one read/write file descriptor
```

The descriptor is duplicated by Binder and mapped independently in both
processes. Each recipient owns and closes its descriptor and mapping. Ordinary
AIDL method slots may grow without colliding with the reserved transaction.

## 3. TPB1 pipe transport

All multibyte handshake and frame-length integers use network byte order.

### Handshake

| Offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 4 | ASCII `TPB1` |
| 4 | 2 | protocol version, currently `1` |
| 6 | 2 | channel kind, `1` for named pipe or `2` for forwarded input |
| 8 | 16 | session UUID bytes in UUID N-format byte order |
| 24 | 32 | cryptographically random session token |
| 56 | 2 | UTF-8 channel-name byte length |
| 58 | N | UTF-8 channel name, maximum 128 bytes |

The Android-only echo lab accepts `TeknoParrot_BridgeProbe` for channel kind 1
and `TeknoParrot_ForwardedInput` for channel kind 2. The controlled Windows
fixture accepts only the architecture-specific pipe name declared in the
immutable prepared session. Session and token comparisons are constant time. A
successful handshake receives the four-byte ASCII acknowledgement `OKAY`;
failure closes the connection.

### Stream frames

The Android-only echo fixture uses this framed test sequence after
acknowledgement:

| Size | Meaning |
| ---: | --- |
| 4 | unsigned payload size in network byte order |
| N | opaque payload bytes |

That lab permits `1..65536` bytes and echoes each frame exactly. The protocol
handles partial reads and treats EOF in the middle of a frame as an error.
The authenticated Windows `pipehelper.exe` uses the same TPB1 prefix but then
forwards the opaque byte stream directly to `\\.\pipe\<declared-name>` without
adding frame lengths or interpreting JVS payload bytes.

## 4. TPJ1 shared page

The mapped page is 4096 bytes. All multibyte page fields are little-endian,
matching the Windows/x86 legacy consumer.

| Offset | Size | Owner | Meaning |
| ---: | ---: | --- | --- |
| 0 | 64 | TeknoParrot | Legacy `TeknoParrot_JvsState` bytes, layout unchanged |
| 64 | 4 | Host | ASCII `TPJ1` |
| 68 | 2 | Host | layout version, currently `1` |
| 70 | 2 | Host | header size, currently `128` |
| 72 | 4 | Host | total mapped size, currently `4096` |
| 76 | 4 | Host | host sequence |
| 80 | 4 | Guest/client | guest sequence |
| 84 | 8 | Host | monotonic timestamp in nanoseconds |
| 92 | 8 | Guest/client | monotonic timestamp in nanoseconds |
| 100 | 4 | Both by defined bits | state flags |
| 104 | 24 | Reserved | must remain zero in v1 |
| 128 | 256 | Future Winlator producer | normalized input snapshot |
| 384 | 512 | Future mailbox | output/force-feedback messages |
| 896 | 3200 | Reserved | future versioned regions |

Flags:

| Bit | Name | Meaning |
| ---: | --- | --- |
| 0 | `HostReady` | header and legacy region initialized |
| 1 | `PipeAuthenticated` | at least one TPB1 client authenticated |
| 2 | `GuestTouchedPage` | host observed a nonzero guest sequence |
| 3 | `Stopping` | session shutdown began |
| 4 | `Fault` | host recorded a transport fault |

The lab updates the host timestamp and sequence every 50 ms. The probe checks
the legacy 64-byte canary, waits for host sequence progress, writes
`0xC0DEC0DE` to the guest sequence, and verifies that the host observes it.

Production data structures need an explicit seqlock/double-buffer rule for any
multi-field snapshot. A writer publishes an odd sequence while mutating and an
even sequence after the complete snapshot; a reader retries if the two sampled
sequence values differ or are odd. The JVS sense byte at offset zero must be
published before the pipe reply that depends on it.

## 5. TPI1 forwarded-input frames

The control-forwarding slice now has matching C# and Java codecs. The signed
Android probe and host packages exercise the authenticated session socket with
synthetic frames on a physical Fold6. Full Winlator owns a prepared-session
client with a bounded preallocated queue, TPB1 authentication, reconnect, and
reset/resynchronization. A session-gated observer is wired into the real
`XServerDisplayActivity` key, joystick, pointer, device, and lifecycle dispatch
points without consuming normal Wine input. An append-only AIDL operation
launches that non-exported Activity with the prepared-session ID. The diagnostic
kind proves real key/touch dispatch and teardown without starting Wine; the
Windows-executable kind uses the same immutable handoff for actual games.
Production recipes feed this boundary directly. Their selected controls profile
is part of the immutable Activity request, so a driving game and an arcade game
can use different editable layouts without global preference collisions.

Every TPI1 integer is little-endian. A stream frame begins with this fixed
28-byte header:

| Offset | Size | Meaning |
| ---: | ---: | --- |
| 0 | 4 | ASCII `TPI1` |
| 4 | 2 | protocol version, currently `1` |
| 6 | 2 | frame type |
| 8 | 4 | payload length, at most 1024 bytes |
| 12 | 4 | wrapping sequence number |
| 16 | 8 | Android event time in monotonic nanoseconds |
| 24 | 4 | stable device ID |

The initial implemented payloads are fixed-size:

| Type | Size | Payload |
| --- | ---: | --- |
| `AXIS` | 8 | player, reserved zero, axis ID, signed Q15 value, unsigned Q15 flat value |
| `BUTTON` | 4 | player, pressed byte, logical binding ID |
| `POINTER_ABSOLUTE` | 16 | player, tool, Q16 x/y/pressure, pointer ID, buttons |
| `FOCUS` | 4 | focused byte plus three reserved zero bytes |
| `DEVICE_REMOVED` / `SUSPEND` | 0 | no payload |

`DEVICE_ADDED`, `KEY`, `POINTER_RELATIVE`, and `GAMEPAD_SNAPSHOT` retain their
type numbers but are deliberately rejected as unsupported until their complete
payloads and mapping rules are implemented. This prevents an incomplete decoder
from silently accepting controls it cannot release correctly.

Winlator's Java encoder writes into caller-owned buffers and performs no
steady-state allocation. `ForwardedInputQueue` preallocates a bounded SPSC ring;
a full ring is reported to the producer so it can request resynchronization
instead of silently dropping an edge. The .NET stream reader handles partial
reads using the header length, and the state boundary:

- tracks wrapping sequences independently per stable device;
- rejects duplicate/out-of-order frames;
- clears that device before applying a frame after a sequence gap;
- aggregates logical buttons across devices only on the JVS sampling thread;
- releases held controls on device removal, focus loss, suspend, malformed
  stream data, and EOF/socket loss.

The C# audit passes a byte-exact golden vector, strict malformed-frame cases,
partial reads, multi-device ownership, gaps, releases, and uint32 wrap. The Java
test emits the identical vector and validates Android mapping/math, the
preallocated queue, a real TPB1 loopback handshake, independent per-device
sequences, overflow/reset, and a forced reconnect. On the Fold6, session
`<session-id>` accepted six synthetic frames over TPB1
channel kind 2 and reported `inputFrames=6`, `inputGaps=1`, and button, axis,
pointer, and release observations. Full-Winlator prepared session
`<session-id>` then passed the production Java-client path
with `frames=6`, `queueRemaining=0`, `resync=1`, `dropped=0`, `hostFrames=8`,
`hostGaps=0`, and `release=1`. Its real-Activity diagnostic passed with
`frames=10`, `gaps=0`, `focus=6`, `coin=1`, `pointer=1`, `eofRelease=1`, and
four rejected immutable-request mutations (container, session, launch kind,
and an attempted executable field) before the session completed the x64/x86
Wine gate. Production SR3 launch and touch controls have since passed on the
Fold6. The v7 implementation assigns distinct physical Android controllers to
players 1–4, converts primary-stick or hat axes to arcade directions when a JVS
or FastIO recipe requires it, and retains per-game edited Winlator layouts.
Physical multi-controller coverage, routing/Wine suppression, and latency still
require on-device qualification.

## 6. Reproducing the emulator test on Windows

Prerequisites are the user-local .NET 10 Android workload, JDK 17, Android SDK,
emulator, and the API 34 Google APIs x86_64 image under
`%USERPROFILE%\android-toolchain`.

```powershell
cd TeknoParrotUi.Android\scripts
.\run-emulator.ps1 -Headless
.\run-bridge-lab.ps1
.\run-winlator-service-lab.ps1
```

The build must use `EmbedAssembliesIntoApk=true`; a Debug APK built for IDE
fast deployment cannot be launched after a plain `adb install` because its
managed assemblies live in the IDE deployment directory.

The automated probe passes only after it verifies all of the following:

1. signature-protected AIDL bind and protocol v1 negotiation;
2. descriptor transfer and two-process `mmap` access;
3. valid TPJ1 metadata and all 64 legacy canary bytes;
4. host heartbeat progression and host observation of the guest marker;
5. authenticated TPB1 handshake and 16 exact framed echoes;
6. clean session stop through the service;
7. production-direction Winlator binding and Winlator-owned page observation;
8. 100 consecutive Winlator prepare/stop cycles;
9. zero remaining per-session directories after the physical full-Winlator run.

The first role-reversed fixture pass exchanged 16 frames at 2.53 ms mean and
4.37 ms maximum round-trip latency. The first Winlator-owned fixture completed
16 frames in 108.51 ms with an 18.09 ms maximum frame and passed 100 lifecycle
cycles. These are functional emulator results, not Winlator or game-performance
benchmarks.

For a physical ARM64 device, build and install the Debug host together with the
full Winlator fork by selecting the device and mode explicitly:

```powershell
.\run-winlator-service-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -RuntimeIdentifier android-arm64 `
    -WinlatorVariant Full
```

After the matching APKs are already installed, repeat only the probe with
`-SkipBuild -SkipInstall`. The first Galaxy Z Fold6 pass completed all 16
frames in 17.14 ms with a 0.71 ms maximum frame; the scripted repeat completed
them in 15.71 ms with a 3.10 ms maximum frame. Both passed Winlator-owned page
observation and 100 lifecycle cycles. After fixing fixture cleanup, a further
run completed all 16 frames in 34.42 ms with a 3.91 ms maximum frame and left
zero per-session files or directories. These figures measure the Android
fixture only; no Wine guest was running.

## 7. Reproducing the Windows guest bridge tests

### Service-controlled test (current integration gate)

With both signed ARM64 APKs built and container 1 activated at least once in
Winlator, run:

```powershell
.\run-winlator-service-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -RuntimeIdentifier android-arm64 `
    -WinlatorVariant Full `
    -GuestDiagnostic `
    -ContainerId 1
```

Use `-SkipBuild` to install existing matching APKs, or
`-SkipBuild -SkipInstall` to probe an already installed pair. Guest mode is
ARM64/full-Winlator only. It binds directly to the signature-protected service,
uses an ordinary app-to-app loopback socket, and invokes Wine from the service;
it does not use `adb reverse`, coordinate input, or the Winlator file manager.

The authenticated Fold6 pass used session
`<session-id>` and required:

1. service v7/data v1 capability negotiation;
2. immutable `SessionSpec`/`PreparedSession` agreement with no token echo;
3. AIDL launch to return `state=running`;
4. x64 and x86/WoW64 helpers to authenticate TPB1 with the declared session, token, and architecture-specific pipe name;
5. both peers to exchange their exact deterministic named-pipe vectors;
6. both peers to observe the host's `0xA0..0xAF` prefix;
7. the host to observe the guest's `0xD0..0xDF` marker and final architecture byte;
8. the parent Wine/Box64 command to exit zero;
9. two consecutive stop calls to return a stable stopped state;
10. no Wine, Box64, helper, or guest process after completion.

The same rebuilt helper binaries also pass a production-shaped native Windows
test using real named pipes and loopback sockets for x64 and x86. Ten full
iterations per architecture passed the fixed vectors, one randomized MiB in
each direction, and a disconnect/reconnect on the same helper. Ten deliberately
wrong-token connections received no `OKAY` acknowledgement.

The volume test found that simultaneous synchronous `ReadFile`/`WriteFile` on
the helper's single duplex pipe handle was unreliable. The helper now opens the
pipe with `FILE_FLAG_OVERLAPPED`, uses separate overlapped events for connect,
read, and write, cancels pending I/O during teardown, and joins both forwarding
threads before closing the pipe. This retains TPB1 and the legacy raw stream;
the `TPS1`/`TPS2` stress records belong only to the diagnostic peers.

The current ARM64 host and Winlator APKs package this expanded path. The Android
host first rejects a live wrong token without sending `OKAY`, then accepts the
valid helper and runs both one-MiB directions across two connections for x64 and
x86. The five staged Winlator assets match byte-for-byte. Fold6 session
`<session-id>` passed the complete diagnostic for both
architectures, exited zero, and left no Wine, Box64, helper, or guest process.

The diagnostic page and its session-named Wine-prefix link are retained for
inspection in this mode. Normal fixture mode still exercises explicit session
cleanup. Production cleanup will be session/PID/target validated rather than a
Debug retention policy.

### Thin runtime-package transport

Distributable TPUI and Winlator APKs contain no TeknoParrot, OpenParrot,
ElfLoader2, CXBXR, or PCSX2X6 runtime payload. TPUI downloads each Android
runtime archive from the updater service only when its authoritative size and
`sha256:` digest are present. It verifies both, opens the private cached archive
read-only, and sends its file descriptor through Binder transaction
`FIRST_CALL_TRANSACTION + 33`.

The signature-protected Winlator service verifies the outer digest again,
validates `teknoparrot-package.json`, rejects duplicate, unlisted, escaping, or
package-external ZIP entries, hashes every extracted file, and replaces only
the roots owned by that component. Transaction `+34` returns Winlator's
private installed-package markers so TPUI does not trust stale local updater
state after companion data is cleared. Package installation is refused during
a game session or activation operation. See
`ANDROID_RUNTIME_PACKAGE_CONTRACT.md` for the wire-independent archive schema.

### Manual file-manager test (lower-level fallback)

The physical ARM64 fixture builds tiny native x64/x86 Windows peers, stages
them and the matching helpers into an existing private Winlator container,
creates a deterministic 64-byte backing page, and launches the batch through
Wine/Box64:

```powershell
.\run-winlator-guest-bridge-lab.ps1 `
    -DeviceSerial <adb-serial> `
    -ContainerId 1
```

Visual Studio C++ x86/x64 tools are required to rebuild the guest fixtures.
Pass `-SkipBuild` after `bridgeguest64.exe` and `bridgeguest32.exe` exist. The
default file-manager run-button coordinates match the tested Fold6 layout and
can be overridden with `-FileTapX`/`-FileTapY`. This remains useful for
isolating Wine from Binder, but it is no longer the primary integration gate.

The lab passes only when both architectures:

1. read the Android-owned `0xA0..0xAF` prefix through a Windows named mapping;
2. open the helper-created `\\.\pipe\TPWinlatorPipe*` endpoint;
3. exchange exact architecture-tagged request/response vectors;
4. write `0xD0..0xDF` through the named mapping and mirror it to the rootfs file;
5. return zero, publish `COMPLETE=1`, and leave no helper/guest/Wine/Box64 process.

The Fold6 run passed all five checks for x64 and x86/WoW64. This fixture uses
the helper's current raw stream and legacy 64-byte mirror. It does not claim
TPB1 authentication or the full TPJ1 protocol.

## 8. Next production expansion

The production-direction service, package signing, descriptor ownership, pipe
direction, foreground host ownership, Winlator-side prepared-session retention,
and safe argument-vector launch boundary are now implemented. Promotion still
requires:

1. Prove a live Fold6 game remains owned after the Avalonia Activity and UI
   process are destroyed, then reattaches to the saved session without a second
   guest launch. Exercise notification Stop, suspend/resume, and Android process
   recreation. Replace the byte envelope only when the .NET generator can safely
   deserialize custom parcelables.
2. Generalize the implemented `GameSessionFactory` route beyond SR3, Rastan Saga,
   and 3D Cosplay Mahjong. Replace the fixed developer container/runtime with
   discovered signed-companion settings, convert each profile's executable,
   test-mode, argument, helper, and library requirements to immutable scoped
   DOS paths, and add a formal observable state machine plus callbacks.
3. Add exact helper/game PID reporting and identity-checked cleanup. Add
   production timeouts/counters to the validated on-device wrong-token,
   reconnect, and one-MiB randomized path, then bind orphan recovery to the same
   session identity already used by TPB1, the page link, and the descriptor.
4. Qualify the implemented recipe-selected transports on hardware: SR3's
   15-byte report, Rastan's 64-byte FastIO report, and Cosplay Mahjong's
   bidirectional `TeknoParrot_JVS` command/reply stream plus shared-page sense
   byte. Exercise the new players 1–4 physical-controller assignment and edited
   arcade layout, then add relative pointer frames, viewport updates, and
   routing/Wine suppression.
5. Run the remaining stress gates before expanding Android game launch beyond
   the first developer cohort: repeated bind/stop, bad-token rejection, randomized pipe bytes,
   30-minute page coherence, Activity recreation, process death, and suspend.

The full ownership model, phases, gates, device matrix, and first-game cohort
remain in `ANDROID_WINLATOR_PORT_PLAN.md`.

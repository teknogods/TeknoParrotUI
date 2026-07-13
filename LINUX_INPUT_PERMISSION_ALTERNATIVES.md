# Linux Input Permissions Problem & Solutions

> **STATUS (2026-07-12): EXECUTED.** See "Implemented Solution" at the bottom.
> Shipped: udev `uaccess` rule + installer (`setup/`) **and** a zero-setup X11
> fallback (`X11FallbackInputListener`). SDL2/SDL3 migration was rejected —
> analysis kept below for the record. SDL2 stays as the gamepad backend on all
> platforms; Windows input is unchanged.

## Current Issue
- **Root cause:** Direct `/dev/input/event*` access via evdev requires:
  - User in `input` group (requires `sudo usermod -aG input $USER` + relogin), OR
  - Root access (not acceptable)
- **Problem:** Not sustainable — requires user intervention for permission escalation
- **Impact:** Keyboards always fail without group; some mice work via vendor udev ACLs (Razer yes, Logitech no)

---

## Solution Analysis

### ❌ Option 1: SDL3 (Won't Help)
**Verdict:** SDL3 uses identical Linux input stack to SDL2
- SDL3 still reads `/dev/input` directly on Linux (same evdev/libinput backend)
- No permission advantage over SDL2
- Adding SDL3 means maintaining two input libraries
- **Decision:** Skip this path

---

### ✅ Option 2: udev Rules (Professional, Recommended)
**Verdict:** Industry standard, no escalation required

#### How it works:
Create `/etc/udev/rules.d/99-input-devices.rules`:
```udev
# Grant non-root access to input devices (keyboards, mice, joysticks)
KERNEL=="event[0-9]*", SUBSYSTEM=="input", MODE="0660", GROUP="input"
KERNEL=="js[0-9]*", SUBSYSTEM=="input", MODE="0660", GROUP="input"
```

#### Advantages:
- ✅ No code changes needed — works with existing evdev code
- ✅ One-time system setup (not per-user)
- ✅ Transparent to app — no elevation dialogs
- ✅ Standard practice in arcade/gaming Linux setups
- ✅ Can be packaged with installer (e.g., `.deb`/`.rpm` scripts)

#### Disadvantages:
- ⚠️ Still requires `input` group membership OR sysadmin to set up rules
- ⚠️ Multi-user machines: all group members can read all input (keyboard snooping risk)
- ⚠️ Not suitable for shared/public machines without additional ACLs

#### Implementation:
- Keep current evdev code as-is
- Package setup script: `install-desktop-entry.sh` → create udev rules via installer
- Document in [LINUX_SETUP_GUIDE.md](LINUX_SETUP_GUIDE.md) + console warning

---

### ✅ Option 3: Migrate to libinput (Modern Stack)
**Verdict:** Long-term best solution, but requires significant refactor

#### How it works:
- Replace direct evdev P/Invoke with `libinput` C library (via P/Invoke)
- libinput handles device aggregation, event filtering, acceleration profiles
- Uses `systemd-logind` for seat/session management → better permission handling

#### Advantages:
- ✅ systemd-logind integration enables **non-root device access** on multi-user systems
- ✅ Modern standard (most Linux desktops use libinput)
- ✅ Automatic dead-zone, acceleration, profile support
- ✅ Better tablet/touchpad handling
- ✅ Future-proof (evdev being phased out)

#### Disadvantages:
- ❌ **Major refactor:** Replace ~600 lines EvdevInterop/EvdevMouseListener
- ❌ libinput API is more complex (requires event loop integration)
- ❌ Dependency: `libinput.so.10` (typically pre-installed on modern distros)
- ❌ Testing: need real hardware or complex VM setup
- ⚠️ Still won't work if user is locked out of `input` group AND no systemd-logind session

#### Example (pseudo-code):
```csharp
// libinput device enumeration
using Libinput;
var context = new LibinputContext();
foreach (var device in context.GetDevices(DeviceType.Mouse))
{
    device.Open(); // systemd-logind grants access transparently
    device.OnEvent += HandleInputEvent;
}
```

---

### ❌ Option 4: SDL2 for Everything (REJECTED — architecturally impossible)
**Verdict:** Doesn't work for a launcher — correction to the original analysis

SDL2 (and SDL3) deliver mouse/keyboard events **only to windows the SDL
process owns**. TeknoParrot's games run in a **separate Wine process** — SDL2
in the launcher would never see input aimed at the game window unless it read
`/dev/input` directly… which is exactly the permission problem being solved.
The original "Advantages" list below was wrong on this point.

<details>
<summary>Original (incorrect) analysis — kept for the record</summary>

#### How it works:
- **Current:** SDL2 for gamepad only; evdev for mouse/keyboard
- **Proposed:** Use SDL2 for mouse/keyboard/gun input too
- SDL2 has `SDL_GetMouseState()`, `SDL_GetKeyboardState()`, input events
- Already have SDL2-CS binding; just expand usage

#### Advantages:
- ✅ Single input library = simpler architecture
- ✅ SDL2 already vendored (no new deps) via `ppy.SDL2-CS`
- ✅ Cross-platform: same code path Windows/Linux/Mac
- ✅ SDL2 has fallback to keyboard `ioctl()` if `/dev/input` unavailable
- ✅ No new permissions required beyond gamepad
- ✅ Reduced testing surface (one backend instead of two)

#### Disadvantages:
- ⚠️ **SDL2 runs under your own UID → no escalation**, but:
  - Captured input only works inside game window (SDL2 design)
  - Raw/absolute input needs explicit device access (still hits /dev/input eventually)
  - No multi-seat support (one user per session)
- ⚠️ Gun/trackball games may need window focus to capture (vs fullscreen exclusive on Windows)
- ❌ Refactor: Rewrite EvdevMouseListener + keyboard capture to use SDL2 events

#### Implementation sketch:
```csharp
// Use SDL_PollEvent for mouse/keyboard during gameplay
SDL_Event sdlEvent;
while (SDL_PollEvent(out sdlEvent))
{
    if (sdlEvent.type == SDL_EventType.SDL_MOUSEMOTION)
        HandleMouseMotion(sdlEvent.motion);
    else if (sdlEvent.type == SDL_EventType.SDL_KEYDOWN)
        HandleKeyDown(EvdevKeyMap.ToKeys(sdlEvent.key.keysym.scancode));
}
```

</details>

---

### ✅ Option 7: X11 Polling Fallback (IMPLEMENTED)
**Verdict:** Zero-setup, permission-free — the actual answer to "not sustainable"

#### How it works:
Poll the X server (`XQueryPointer` for global cursor + button mask,
`XQueryKeymap` for keyboard state) at ~250 Hz. Reading the cursor position and
key bitmap requires **no permissions at all** — no root, no groups, no udev.
Wine games are always X11/XWayland clients, so this works on both X11 and
Wayland desktops precisely when the game window is focused.

#### Advantages:
- ✅ Works out of the box for every user — no setup step whatsoever
- ✅ Absolute cursor position is ideal for gun aim (matches what Wine shows the game)
- ✅ X keycode = evdev code + 8 → existing `EvdevKeyMap` reused, bindings stay compatible
- ✅ Tiny: libX11 P/Invoke, no new packages

#### Limitations (why evdev stays preferred when readable):
- ⚠️ Single pointer — no multi-gun (X merges all mice into one cursor)
- ⚠️ Three buttons via the XQueryPointer mask (left/middle/right)
- ⚠️ Dedicated light-gun hardware that doesn't drive the system cursor won't aim
- ⚠️ Polling (~250 Hz) instead of event-driven

---

### ❌ Option 5: Input Method Server / Daemon (Over-engineered)
**Verdict:** Too complex for this use case

- Run privileged daemon (e.g., systemd user service) that reads `/dev/input`
- App connects via socket/D-Bus to query input state
- **Disadvantages:**
  - Complex architecture (daemon + IPC)
  - Requires systemd (not all embedded/containers)
  - Difficult testing/debugging
  - Overkill for arcade emulator use case

---

### ❌ Option 6: Wayland Native Input (Incomplete Solution)
**Verdict:** Platform-specific, not universal

- Wayland has `zwp_pointer_v1`, `zwp_keyboard_v1` for captured input
- **Disadvantages:**
  - X11 systems (still majority of Linux) not supported
  - Requires specific Wayland compositor extensions
  - Reduces portability

---

## Implemented Solution (2026-07-12)

Two complementary layers — **zero-setup fallback + one-command proper fix** —
instead of the originally proposed SDL2 migration (see corrected Option 4):

### Layer 1: X11 fallback (Option 7) — automatic, no setup, never needs root
- `TeknoParrotUi.Common/InputListening/Mouse/X11Interop.cs` — libX11 + libXi P/Invoke.
- `TeknoParrotUi.Common/InputListening/Mouse/X11FallbackInputListener.cs`:
  - **Primary: XInput2 raw events** (2026-07-12 upgrade) — event-driven, per-
    physical-device button/key/motion, side buttons; **multi-gun works rootless
    on native X11** (XWayland merges pointers → single gun there). Aim via
    absolute cursor (single pointer) or per-device raw-delta canvas (multiple).
  - Last resort: XQueryPointer/XQueryKeymap polling when the server lacks XI2.
  - Same `GunAnalogMath` layouts and `RawInputButton` binding compatibility as evdev.
- `MappingDispatch` has **full parity** with InputListenerRawInput's switch
  (JVS board 2, extension boards, cards, TPSystem, relative directions, and the
  per-game special cases — PlayInput Test toggle, EADP, GSEVO, BattleGear,
  Haunted Museum). Analog*± and Rotary* remain owned by their state engines.
- Manager decides **per class**: if no evdev mouse is readable
  → X11 serves aim+buttons; if no evdev keyboard is readable → X11 serves key
  bindings. Evdev still runs for whatever *is* readable (vendor-ACL mice etc.),
  so the two never double-fire.
- Binding capture (`RawInputCaptureService`) gained the same fallback
  (`DevicePath = "X11"`), and the light-gun dropdown shows
  "System Pointer (X11)" when no evdev mice are readable.
- Verified: `x11-test` audit subcommand — automated warp/read-back self-test
  passed on a Wayland desktop (XWayland).

### Layer 2: udev `uaccess` rule — full evdev support with one command
- `setup/70-teknoparrot-input.rules` + `setup/install-udev-rules.sh`.
- Uses systemd-logind ACLs (`TAG+="uaccess"`): access is scoped to the active
  seat session (revoked on logout) — **better than the permanent `input` group**
  and no re-login required. Numbered ≤ 73 so `73-seat-late.rules` applies it.
- Needed for: multiple guns/mice, dedicated light-gun hardware, side buttons.

### Shared refactor
- `MappingDispatch` extracted from `EvdevMouseListener` so both Linux
  backends dispatch `InputMapping` identically.

### What was NOT done, deliberately
- **No SDL3** (no permission benefit — same evdev backend), so **Windows input
  is completely untouched** (SDL2 + RawInput, as before).
- No libinput migration (Option 3): re-evaluate only if X11-less Wayland-native
  game hosting ever becomes a target.

### Remaining follow-ups
- [ ] Play-test a gun game with evdev blocked (rename user out of `input`
      group or `chmod` a test node) to confirm end-to-end fallback in Wine
- [ ] Package hook: call `install-udev-rules.sh` from distro packages/installers

---

## Original Analysis (for the record)

### Recommended Path Forward (superseded by "Implemented Solution")

### Immediate (1-2 days)
**Use Option 2 (udev rules) + better documentation:**
1. ✅ Keep current evdev code as-is (no refactor)
2. 📝 Update [LINUX_SETUP_GUIDE.md](LINUX_SETUP_GUIDE.md):
   - Clear section: "Why do I need to add my user to the 'input' group?"
   - Explain permission model
   - Provide copy-paste commands
3. 📦 Create `/setup/udev-rules.sh` for packagers:
   ```bash
   sudo install -m 644 setup/99-input-devices.rules /etc/udev/rules.d/
   sudo udevadm control --reload
   ```
4. 🔧 Surface warnings in:
   - Launch console (if keyboard perms denied)
   - Controller Setup view
   - `evdev-test` subcommand

### Medium-term (1-2 weeks)
**Evaluate Option 4 (SDL2 for everything):**
1. Prototype: Use SDL2 for gun/mouse input alongside gamepad
2. Remove EvdevMouseListener; wire SDL2 events to gun analog handler
3. Test on real hardware (Linux + gun game)
4. **If successful:** Deprecate evdev code entirely
5. **Benefit:** Single input backend, better permission story

### Long-term (post-launch)
**Consider Option 3 (libinput) for next major version:**
- Only if users report systemd-logind access issues
- Requires dedicated refactor sprint + testing
- Provides forward-looking solution as evdev ages

---

## Decision Matrix

| Approach | Effort | Permission Better? | Notes |
|----------|--------|-------------------|-------|
| Status quo + docs | 1 day | No | Works now, needs user knowledge |
| udev rules | 1 day | Yes (professional) | **IMPLEMENTED** (uaccess variant) |
| SDL2 for all input | — | ❌ No | REJECTED: SDL only sees its own windows, games run in Wine process |
| X11 polling fallback | 1 day | ✅ Yes (zero perms) | **IMPLEMENTED** — automatic, single gun |
| libinput | 2-3 weeks | Yes (systemd-logind) | Future-proof, complex |
| Daemon / D-Bus | 2+ weeks | ✅ Maybe | Over-engineered |

---

## Action Items (all executed or superseded — see Implemented Solution)

- [x] **Phase 0:** Document in LINUX_SETUP_GUIDE.md + console warning (updated for fallback + udev rule)
- [x] **Phase 1:** `setup/install-udev-rules.sh` + `setup/70-teknoparrot-input.rules` (uaccess, not group-based)
- [x] **Phase 2 (improved):** X11 polling fallback instead of SDL2-only path (SDL2 can't see another process's window input)
- [ ] **Phase 3 (future):** Evaluate libinput if user feedback warrants


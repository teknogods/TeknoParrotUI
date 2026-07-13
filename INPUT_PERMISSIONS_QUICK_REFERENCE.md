# TeknoParrot Linux Input — Quick Reference

## Does it work on Steam Deck?
✅ **YES** (with caveats about latency)

| Scenario | Status | Latency | Setup |
|----------|--------|---------|-------|
| **Steam Deck with udev rule** | ✅ Full support | 2-5ms | `sudo ./setup/install-udev-rules.sh` (once in Desktop Mode) |
| **Steam Deck without rule** | ✅ Works | 10-20ms ⚠️ | Auto (X11 fallback) |
| **Linux X11 Desktop** | ✅ Full support | 5-10ms | udev rule recommended |
| **Linux Wayland Desktop** | ⚠️ Limited | 10-15ms | udev rule required (X11 fallback unavailable) |
| **Flatpak / Container** | ⚠️ Maybe | varies | Requires explicit `/dev/input` permissions |

---

## Three Permission Tiers (Pick One)

### 🥇 Tier 1: udev `uaccess` Rule (Best for most users)
```bash
sudo ./setup/install-udev-rules.sh
```
- ✅ Works on Steam Deck, all Linux desktops
- ✅ Best performance (2-5ms latency)
- ✅ Session-scoped (automatic cleanup on logout)
- ✅ No re-login needed
- ❌ Requires `sudo` (one-time, during setup)

**Recommended for:**
- Steam Deck users (run once in Desktop Mode, then use Game Mode)
- Multi-gun / dedicated light-gun hardware
- Any serious arcade gaming

---

### 🥈 Tier 2: X11 Fallback (Zero Setup)
- ✅ Works automatically, no setup needed
- ✅ Works on X11 desktops AND Wayland/XWayland
- ✅ No permissions required at all
- ⚠️ Slower latency (10-20ms on Wayland/XWayland)
- ❌ Single mouse/gun only (P1)
- ❌ Only 3 buttons (left/middle/right)

**Works for:**
- Casual gaming without dedicated light guns
- Steam Deck Game Mode without Desktop Mode access
- Testing without setup

**Does NOT work well for:**
- Fast-twitch light gun games (Virtua Cop, 2Spicy)
- Multi-gun setups
- Dedicated light-gun hardware (Sinden, AimTrak)

---

### 🥉 Tier 3: Input Group (Legacy, not recommended)
```bash
sudo usermod -aG input $USER
# Then log out and back in
```
- ⚠️ Requires re-login (session restart)
- ⚠️ Permanent grant (doesn't revoke on logout)
- ❌ Less secure on multi-user systems
- ✅ Works on systems without systemd-logind

---

## Platform Support Matrix

### ✅ Fully Supported
- Ubuntu 22.04+ with X11 or Wayland
- Fedora 38+ with X11 or Wayland
- Arch Linux with X11 or Wayland
- Steam Deck (SteamOS 3+)
- Linux Mint with X11 or Wayland
- Debian 12+ with X11 or Wayland
- Framework Laptop (Linux)
- Any systemd-logind + libX11 system

### ⚠️ Partial Support
- **Pure Wayland (no XWayland)**: Tier 1 only (udev rule needed)
  - X11 fallback unavailable (no X server to query)
  - ~10-15ms latency from udev
  - Rare in practice (Wine needs XWayland anyway)

- **Flatpak / Snap / Container**: Depends on sandbox policy
  - May need explicit `/dev/input` access in app metadata
  - Consult container documentation

- **SSH / Headless Systems**: X11 fallback unavailable
  - Tier 1 udev rule required for any input
  - Dedicated light-gun hardware won't work

### ❌ Not Supported
- Wayland without XWayland (theoretical; Wine won't run)
- Windows (not affected; SDL2 + RawInput unchanged)
- macOS (not affected; not a target platform)

---

## Latency Expectations

### Gun Games on Steam Deck

#### ✅ Playable (with udev rule)
- 2-5ms latency → responsive aiming
- Works for all gun games

#### ⚠️ Noticeable Lag (X11 fallback)
- 10-20ms latency → human-perceivable delay
- **Acceptable:** Golden Tee, WMMT series, House of Dead, driving games
- **Noticeable:** Virtua Cop, 2 Spicy, Gunslinger Stratos, rhythm light-gun games

### Desktop Linux

#### X11 Desktop
- Tier 1 (udev): 2-5ms ✅
- Tier 2 (X11): 5-10ms ✅

#### Wayland/XWayland
- Tier 1 (udev): 2-5ms ✅
- Tier 2 (X11): 10-15ms ⚠️

---

## Troubleshooting

### "Keyboard input not working"
1. Check: `dotnet run --project Tools/InputMethodAudit -- evdev-test`
   - If permission denied → install udev rule
2. Check: `dotnet run --project Tools/InputMethodAudit -- x11-test`
   - If fails → no X server available (Wayland + no XWayland)

### "Mouse/gun aim is slow or laggy"
1. Check which tier is active:
   - Launch console should say `X11 fallback` or `EvdevMouse` or `evdev` direct
2. If X11 fallback:
   - Install udev rule for better latency
   - Or use slower games (not fast light-guns)

### "Light gun hardware (Sinden, etc.) not detected"
1. Requires Tier 1 (udev rule) — X11 fallback can't bind specific devices
2. Install rule: `sudo ./setup/install-udev-rules.sh`
3. Restart game and bind device in Controller Setup

### "Permission denied on /dev/input/event*"
- **Option A** (recommended): `sudo ./setup/install-udev-rules.sh`
- **Option B**: `sudo usermod -aG input $USER` + re-login
- **Option C** (Steam Deck): Use Game Mode (X11 fallback auto-activates)

---

## Setup Instructions by Scenario

### 🎮 Steam Deck — Desktop Mode
```bash
# Download or clone TeknoParrot
cd TeknoParrotUI

# Install the udev rule (one-time)
sudo ./setup/install-udev-rules.sh

# Now launch from Game Mode — full evdev support!
```

### 🎮 Steam Deck — Game Mode Only (No Desktop Access)
- X11 fallback works automatically
- Single gun, 3 buttons, 10-20ms latency
- No setup needed

### 🐧 Linux Desktop (X11)
```bash
sudo ./setup/install-udev-rules.sh
# Then use TeknoParrot normally
```

### 🐧 Linux Desktop (Wayland)
```bash
# Tier 1 (recommended):
sudo ./setup/install-udev-rules.sh

# Tier 2 (no setup, but 10-15ms latency):
# Just run TeknoParrot — X11 fallback auto-activates
```

### 📦 Packagers (distro repos, snap, flatpak, etc.)
```bash
# Include in post-install:
udevadm control --reload
udevadm trigger --subsystem-match=input

# Or run the installer script:
./setup/install-udev-rules.sh
```

---

## Performance Tips

### For Best Latency
1. Install udev rule → 2-5ms (evdev direct)
2. Test with `evdev-test` to verify it's working
3. Play on X11 desktop if possible (vs Wayland)

### For Casual Gaming (Tier 2)
1. X11 fallback activates automatically
2. ~10-20ms latency is acceptable for slower games
3. No setup needed; test with `x11-test`

### For Multi-Gun or Dedicated Hardware
1. **Must use Tier 1** (udev rule)
2. X11 fallback only supports one pointer (the system cursor)
3. Dedicated light guns require direct `/dev/input` access

---

## Known Limitations

- ✅ Fixed: X11 fallback works on Wayland (XWayland polling)
- ✅ Fixed: udev rule on Steam Deck (systemd-logind always present)
- ⚠️ XWayland polling adds latency (Gamescope IPC overhead) — use udev rule for best performance
- ❌ Pure Wayland without XWayland: Tier 2 unavailable (use Tier 1 only)
- ❌ Trackball games: Still require direct evdev access (Tier 1)
- ❌ Multi-gun: Requires Tier 1 (udev rule)

---

## Testing

Verify your setup:

```bash
# Check which input methods work on this system:
dotnet run --project Tools/InputMethodAudit -- evdev-test       # Direct /dev/input access
dotnet run --project Tools/InputMethodAudit -- x11-test         # X11 fallback (permission-free)

# Also check that profiles and gun math are correct:
dotnet run --project Tools/InputMethodAudit -- gun-math-test
dotnet run --project Tools/InputMethodAudit -- profiles-test
```

---

## Questions?

See the detailed analysis: [STEAM_DECK_WAYLAND_COMPATIBILITY.md](STEAM_DECK_WAYLAND_COMPATIBILITY.md)


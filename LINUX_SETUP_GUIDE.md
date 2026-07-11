# TeknoParrotUI on Linux — Setup Guide for Test Users

This guide walks you through setting up TeknoParrotUI to run arcade games on Linux.

## System Requirements

- **OS:** Linux (tested on Ubuntu 22.04 LTS, Fedora 40, tested with Wayland and X11)
- **.NET:** 8.0 SDK or Runtime
- **Wine:** 11.0+ (system wine-staging recommended; GE-Proton available as opt-in)
- **GPU:** NVIDIA or AMD (Intel may work; tested on RTX 3090)
- **RAM:** 4GB minimum, 8GB recommended
- **Storage:** ~10GB for games + Wine prefix caching

## Prerequisites: Automatic First-Run Setup

When you launch TeknoParrotUI for the first time:

1. **Check Wine availability:**
   - The app looks for system wine: `/usr/bin/wine` or `/usr/local/bin/wine`
   - If not found, you'll see an error in the game-running console: `No wine binary found`

2. **Check SDL2 (for input/keyboard):**
   - Required for keyboard input binding
   - System packages: `libsdl2-2.0-0` (Ubuntu/Debian) or `SDL2` (Fedora)

3. **Game profiles load automatically** from the embedded metadata

## Manual Installation (if automatic fails)

### 1. Install Wine (Ubuntu/Debian)

```bash
# Add WineHQ repository
sudo dpkg --add-architecture i386
sudo mkdir -pm755 /etc/apt/keyrings
sudo wget -O /etc/apt/keyrings/winehq-archive.key https://dl.winehq.org/wine-builds/winehq.key
sudo wget -NP /etc/apt/keyrings/ https://dl.winehq.org/wine-builds/ubuntu/dists/$(lsb_release -cs)/winehq-$(lsb_release -cs).sources

# Install wine-staging
sudo apt update
sudo apt install -y wine-staging wine-staging-dev winetricks
```

### 2. Install Wine (Fedora/RHEL)

```bash
sudo dnf install -y wine winetricks
```

### 3. Install SDL2 & Graphics

```bash
# Ubuntu/Debian
sudo apt install -y libsdl2-2.0-0 libvulkan1 libgl1-mesa-glx libxkbcommon0

# Fedora
sudo dnf install -y SDL2 vulkan-loader mesa-libGL libxkbcommon
```

### 4. Install 32-bit Support (for 32-bit games)

```bash
# Ubuntu/Debian
sudo dpkg --add-architecture i386
sudo apt install -y wine32 libc6:i386

# Fedora (already installed with wine)
```

## Game Directory Setup

Games must be placed in a specific directory structure:

```
~/.local/share/TeknoParrotUI/
├── GameProfiles/        (auto-created by app)
├── prefixes/            (auto-created per game)
│   ├── TetrisTheGrandMaster3TerrorInstinct/
│   ├── SegaRally3/
│   └── ...
└── proton/              (optional: GE-Proton if you opt-in)
    └── GE-Proton11-1/
```

**Game ROM directories** (create these manually):

```
~/arcade/
├── Sega Rally 3/
│   └── Rally/
│       └── Rally.exe
├── Tetris The Grand Master 3 Terror Instinct/
│   └── tgm3_single/
│       └── game.exe
└── Trouble Witches AC/
    └── game.exe
```

Configure the game path in TeknoParrotUI UI:
- **Game Path:** point to the `.exe` (e.g., `/home/user/arcade/Sega Rally 3/Rally/Rally.exe`)
- **Game Folder:** leave empty (auto-detected)

## First-Run Checklist

- [ ] Wine installed and working: `wine --version`
- [ ] SDL2 installed: `pkg-config --modversion SDL2`
- [ ] Game `.exe` files accessible and readable
- [ ] TeknoParrotUI built: `dotnet build`
- [ ] Test with CLI mode first (see below)

## Testing & Troubleshooting

### CLI Mode (Headless — Recommended First Test)

Launch a game without the UI to isolate issues:

```bash
cd TeknoParrotUI/bin/x86/Debug
./TeknoParrotUi --profile=TetrisTheGrandMaster3TerrorInstinct.xml
```

Watch the console for:
- `[bridge ...] pipe 'TeknoParrot_JVS': GAME CONNECTED` ✅ — game is receiving JVS input
- `[bridge ...] game->TPUI X bytes` ✅ — data flowing from game
- `Launch error` ❌ — Wine/loader issue
- `I/O error` ❌ — JVS pipe failed to create in time

### UI Mode

```bash
cd TeknoParrotUI/bin/x86/Debug
./TeknoParrotUi
```

Navigate: **Select Game → Start Game**

Console shows the same diagnostics. Exit via **Back** button (graceful cleanup).

### Common Issues

**Issue:** `No wine binary found`
- **Fix:** `sudo apt install wine-staging` (or equivalent for your distro)

**Issue:** `Could not find kernel32.dll, status c0000135`
- **Fix:** 32-bit support missing → `sudo dpkg --add-architecture i386 && sudo apt install wine32`

**Issue:** Game boots but `I/O error` or no input response
- **Check:** Game running console shows `GAME CONNECTED`?
  - Yes → input bridge issue; try `TP_REMOTETHREAD=1 ./TeknoParrotUi` (set env var)
  - No → JVS pipe not created; check `/proc` for stale `pipehelper` processes (kill manually)

**Issue:** Game runs but window is tiny (640×480 on 4K screen)
- **Option 1:** Use game's built-in 4K patch (Sega Rally 3 / Tetris have them)
- **Option 2:** Experimental gamescope scaling: `TP_GAMESCOPE=1 ./TeknoParrotUi` (only from clean desktop session)

**Issue:** 2nd game launch hangs with "closing" spam in console
- **Fix:** Kill any stale processes first:
  ```bash
  pkill -f game.exe; pkill -f pipehelper; wineserver -k; sleep 1
  ```

## Advanced: Optional Proton GE

**Why:** Fixes rare float-exception crashes (TGM3 under system wine, TroubleWitches)

**Install:**
```bash
GE_VERSION="GE-Proton11-1"
GE_RELEASE="ge-proton11-1.tar.gz"
GE_URL="https://github.com/GloriousEggroll/proton-ge-custom/releases/download/${GE_VERSION}/${GE_RELEASE}"

mkdir -p ~/.local/share/TeknoParrotUI/proton
cd ~/.local/share/TeknoParrotUI/proton
wget "$GE_URL"
tar xzf "$GE_RELEASE"
rm "$GE_RELEASE"
```

**Enable per-game:** In TeknoParrotUI, set the game's `ProtonVersion` field to `GE-Proton11-1`

**Or system-wide (all games use it):** `TP_WINE=~/.local/share/TeknoParrotUI/proton/GE-Proton11-1/files/bin/wine ./TeknoParrotUi`

## Environment Variables (Advanced)

| Variable | Purpose | Example |
|----------|---------|---------|
| `TP_WINE` | Override wine binary | `TP_WINE=/usr/bin/wine ./TeknoParrotUi` |
| `TP_REMOTETHREAD=1` | Force remote-thread injection (fixes some float crashes) | `TP_REMOTETHREAD=1 ./TeknoParrotUi` |
| `TP_GAMESCOPE=1` | Enable fullscreen scaling (opt-in, environment-sensitive) | `TP_GAMESCOPE=1 ./TeknoParrotUi` |
| `TP_NO_GAMESCOPE` | Disable gamescope | `TP_NO_GAMESCOPE=1 ./TeknoParrotUi` |

## Reporting Issues

When reporting a problem, include:

1. **OS & Wine version:**
   ```bash
   lsb_release -a
   wine --version
   ```

2. **Game console output** (from the **Game Running** window or CLI):
   - Copy all `[bridge ...]` lines
   - Copy any `Launch error` or exception messages

3. **Stale process check:**
   ```bash
   pgrep -a -f "game.exe|pipehelper|wine"
   ```
   (If output is non-empty, kill them before retesting: `pkill -f game.exe; pkill -f pipehelper; wineserver -k`)

4. **GPU info:**
   ```bash
   glxinfo | grep "OpenGL version"
   # or for Wayland:
   vulkaninfo | grep "deviceName"
   ```

## Known Limitations

- **Wayland:** Works but may have input lag on some systems; X11 preferred for testing
- **Headless (remote SSH):** No display support; use CLI mode for validation only
- **4K scaling:** Game-side patches (SR3, Tetris) or `TP_GAMESCOPE=1` (experimental)
- **Networking/LAN:** Not yet implemented in Linux version
- **Force Feedback:** Not yet wired to Linux input system

## Next Steps

1. Build: `dotnet build TeknoParrotUI.sln -c Debug`
2. Test CLI: `./TeknoParrotUi --profile=SegaRally3.xml`
3. Launch UI: `./TeknoParrotUi`
4. Report feedback in Discord `#openparrot-dev`

---

**For developers:** See [CROSSPLATFORM_INPUT_REFACTOR_PLAN.md](CROSSPLATFORM_INPUT_REFACTOR_PLAN.md) and [PROTON_SUPPORT_PLAN.md](PROTON_SUPPORT_PLAN.md) for architecture details.

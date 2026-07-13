# Winlator Fork for TeknoParrotUI on Android — Comprehensive Integration Plan

**Status:** Strategic Research & Feasibility Analysis  
**Date:** 2026-07-12  
**Focus:** Arcade Game Emulation Pipeline via x86/x86_64 Windows Emulation

---

## Executive Summary

**Can TeknoParrotUI run Windows games on Android via a Winlator fork?** YES, but with significant engineering challenges.

**Estimated Effort:**
- **MVP (proof-of-concept, single game working):** 3–4 months (2–3 developers)
- **Production-ready (50+ games, per-game tuning):** 8–12 months
- **Feature parity with desktop TeknoParrotUI:** 12–18 months

**Best Fork to Start From:** [ewt45/winlator-fork](https://github.com/ewt45/winlator-fork) — More advanced feature set (logging, MIDI support, file I/O improvements) than the official brunodev85/winlator.

---

## Part 1: Current Landscape Analysis

### 1.1 TeknoParrotUI Android Status (TODAY)

**What EXISTS:**
- ✅ Cross-platform input refactor complete (Common library is platform-agnostic)
- ✅ AndroidTouchListener (IInputListener interface) → light-gun mapping via GunAnalogMath
- ✅ Avalonia.Android full UI (setup wizard, game library, settings)
- ✅ TestHarness working on emulator (input state verification)
- ✅ APK builds and runs (Signed Debug APK tested)

**What's MISSING (Game Execution Layer):**
- ❌ No x86 Windows binary execution
- ❌ No Wine/Proton environment
- ❌ No Box86/Box64 integration
- ❌ No container/filesystem abstraction (rootfs, prefix, drive letters)
- ❌ No game launcher (currently only input test harness)

**Key Architectural Insight:**  
The Android project in TeknoParrotUI is a **pure input+UI** layer. It is NOT a full emulator. To run games, we need to **embed or fork** a Windows-on-Android solution like Winlator.

---

### 1.2 Winlator Ecosystem Analysis

#### **Official: brunodev85/winlator (v11.1 — Latest)**

**Architecture:**
- **Language:** Java/Kotlin (Android UI) + native C (Wine/Box86/Box64 glue)
- **Components:**
  - Java UI: container management, shortcuts, settings
  - Native: PRoot (filesystem abstraction), glibc patches, Wine binary + DXVK/VKD3D
  - Box86/Box64: x86/x86_64 emulation with DynaRec (ARM64-optimized)
  - Graphics: Mesa (Turnip/Zink), DXVK (DX9/DX10/DX11 → Vulkan)
  - Audio: ALSA via PRoot, MIDI support (third-party lib)

**Strengths:**
- ✅ 18.2k GitHub stars, 1.5k forks (community trust)
- ✅ Well-maintained (releases every few weeks)
- ✅ Covers both 32-bit (Box86) and 64-bit (Box64) games
- ✅ Per-game tuning: presets (Performance/Stability), environment variables, DXVK conf overrides
- ✅ Supports multiple container versions (Wine 7.x–8.x, GE-Proton variants)

**Limitations:**
- ❌ No per-game input binding (uses Android native input only)
- ❌ No arcade-specific TUI (theater mode, cabinet controls)
- ❌ Generic Windows-game experience (no custom profiles for lightgun games)
- ❌ UI is Java-only, not C#/.NET (no code sharing with TeknoParrotUI.Common)
- ❌ Input capture limited to Android touch/buttons (no evdev, no raw input equivalence)

#### **Fork Analysis: ewt45/winlator-fork (Latest: v6.1-extra)**

**Additional Features Over Official:**
- ✅ **File I/O provider** — built-in file provider (SAF access to rootfs, no MTP needed)
- ✅ **Global exception handler** — crash dumps to `/Download/Winlator/crash/`
- ✅ **Android shortcuts** — create desktop shortcuts to run games directly
- ✅ **Unicode input support** — Chinese character input (CJK IME friendly)
- ✅ **Logcat logging** — optional output capture to `/Download/Winlator/logcat/`
- ✅ **PRoot terminal** — in-app shell for debugging Wine environment
- ✅ **Screen rotation** — landscape/portrait toggling
- ✅ **Absolute mouse positioning** — option for relative→absolute coordinate map
- ✅ **Picture-in-picture mode** — floating window (watch-only, no input)
- ✅ **USB/External storage access** — permission prompt for external drives
- ✅ **MIDI support** — midimap.dll injection + FluidSynth backend

**Status:** Last updated ~2 years ago (July 2024). Not actively maintained, but stable.

**Trade-off:** Slightly older codebase (Winlator v6.1 vs current v11.1), but more features for advanced use cases.

#### **Other Notable Forks**

- **afeimod/winlator-7.1** — Newer (Apr 2025), Chinese-focused, minimal documentation
- **Thanukamax/scalpel** — "Proton for Android" optimizer (early stage, undocumented)
- Most other forks are device-specific (Odin, Legion Go) with minimal game improvements

**Recommendation:** Start with **brunodev85/winlator** (official, stable) but backport selected features from **ewt45** (logging, error handling, file I/O) as the project matures.

---

## Part 2: Integration Strategy

### 2.1 Three Possible Approaches

#### **APPROACH A: Fork Winlator + Integrate TeknoParrotUI.Common**

**High-Level Flow:**
```
Android UI Layer (Avalonia.Android — KEEP)
        ↓
TeknoParrotUI.Common (GameProfile, GameProfileLoader, InputListeners — REUSE)
        ↓
Winlator Engine (PRoot, Wine, Box64 — FORK & MODIFY)
        ↓
Native: Box86/Box64 + Wine Prefix (x86 DLL/EXE)
        ↓
Arcade Game (Windows x86 binary)
```

**Advantages:**
- ✅ Reuse TeknoParrotUI's 537 game profiles + input metadata
- ✅ Reuse Common's AndroidTouchListener (light-gun mapping already working)
- ✅ Unified codebase: both desktop and Android use same profiles
- ✅ Per-game configuration from day-1 (no Winlator generic UI)

**Disadvantages:**
- ❌ Complex C#/.NET ↔ Java/Kotlin bridge (P/Invoke for native code, only works on Windows/Linux)
- ❌ Winlator's Java container management won't directly call C# methods
- ❌ Asset sharing (language files, game metadata) requires duplication or bundle strategy

**Feasibility:** MEDIUM (requires architecture refactoring)

---

#### **APPROACH B: Wrapper Around Stock Winlator**

**High-Level Flow:**
```
TeknoParrotUI.Android (Avalonia) — NEW LAUNCHER
        ↓
Winlator APK (unmodified, installed separately)
        ↓
Deep-link / Intent-based Game Launch
        ↓
Winlator Container (Wine/Box64)
```

**Advantages:**
- ✅ Zero Winlator codebase changes (use releases as-is)
- ✅ Separate concerns (updates Winlator independently)
- ✅ Fast integration (1–2 weeks)

**Disadvantages:**
- ❌ No input binding control (generic Android touch only)
- ❌ No per-game TeknoParrotUI profiles (hardcoding needed)
- ❌ Cannot customize Wine environment per game (Winlator's defaults only)
- ❌ User experience fragmented (two separate apps)

**Feasibility:** HIGH (engineering, LOW arcade value)

---

#### **APPROACH C: Full Fork + C#/.NET Wrapper (RECOMMENDED for MVP)**

**High-Level Flow:**
```
TeknoParrotUI.Android (Avalonia.Android — KEEP UI layer)
        ↓ (P/Invoke + C# Native Interop)
Winlator C/Kotlin → C++ → PRoot/Wine/Box64 (FORK & WRAP)
        ↓
Java Container Mgmt (translated into C#-callable abstraction)
        ↓
Arcade Game (Windows x86 binary + TeknoParrotUI input bindings)
```

**Advantages:**
- ✅ Leverage TeknoParrotUI.Common directly
- ✅ Control over Wine environment, per-game tuning
- ✅ Input binding system works end-to-end
- ✅ Single codebase for profiles and configuration
- ✅ Reuse all existing Android input stack

**Disadvantages:**
- ❌ Substantial engineering (C# ↔ C/native boundary, GC/memory management)
- ❌ Harder to rebase upstream Winlator changes
- ❌ Maintenance burden (keep Wine/Box64 submodules in sync)

**Feasibility:** MEDIUM-HIGH (engineering, HIGH arcade value) — **RECOMMENDED**

---

### 2.2 Recommended Approach: **Partial Fork + Wrapper** (Hybrid C)

**Concrete Plan:**
1. Fork brunodev85/winlator (official, stable)
2. Keep Winlator's Java UI temporarily (for testing)
3. Extract & wrap native core: PRoot, Wine, Box64 management
4. Build a **C#/.NET P/Invoke layer** (WinlatorNative.cs) in TeknoParrotUi.Common
5. Replace Winlator's Java container management with C# logic
6. Gradually migrate UI calls from Avalonia.Android's hidden wrapper → direct C# calls

**Timeline:**
- **Phase 1 (Month 1–2):** Extract native layer, create P/Invoke wrapper, run one game
- **Phase 2 (Month 2–3):** Integrate TeknoParrotUI profiles, test 10+ games
- **Phase 3 (Month 3–4):** Polish UI, input binding, per-game settings (MVP complete)

---

## Part 3: Technical Deep Dive — What Must Be Built

### 3.1 C#/.NET ↔ Native Bridge Layer

**Current State:**
- TeknoParrotUI.Common is pure C# (.NET 8)
- Android head (TeknoParrotUi.Android) uses Xamarin/MAUI (no native P/Invoke at scale)
- Avalonia.Android provides OS-level APIs but no easy Windows-API equivalents

**What Needs Building:**

#### **WinlatorNativeInterop.cs** (~500 lines)

```csharp
namespace TeknoParrotUi.Common.Winlator
{
    /// <summary>
    /// P/Invoke bridge to Winlator's native libraries (PRoot, Box64, Wine setup).
    /// Android: loads .so files; Linux/Windows: ships prebuilt native libraries.
    /// </summary>
    public class WinlatorNativeInterop
    {
        // Container lifecycle
        public extern static int CreateContainer(string containerName, string wineVersion);
        public extern static int StartContainer(string containerName, string[] envVars);
        public extern static int StopContainer(string containerName);
        public extern static int DeleteContainer(string containerName);
        
        // Filesystem abstraction (PRoot)
        public extern static string GetContainerRootPath(string containerName);
        public extern static int MountFileSystem(string containerName, string hostPath, string guestPath);
        
        // Wine environment
        public extern static int SetupWinePrefix(string prefixPath, string wineVersion);
        public extern static int ExecuteInWine(
            string prefixPath,
            string programPath,
            string[] args,
            string[] envVars
        );
        
        // Graphics & Audio
        public extern static int SetDXVKConfig(string prefixPath, string dxvkConfContent);
        public extern static int ConfigureALSA(string containerPath, ALSAConfig config);
    }
    
    public class ALSAConfig
    {
        public int SampleRate { get; set; } = 44100;
        public int BufferSize { get; set; } = 2048;
        public int Periods { get; set; } = 4;
    }
}
```

**Implementation Challenges:**
1. **Platform-specific binaries:**
   - Android: Compile Box64 for arm64-v8a (included in Winlator APK as .so)
   - Linux: Ship precompiled or use system Box64
   - Windows: N/A (no native box64 on Windows; users run via WSL2 or native Wine)

2. **Memory safety:** GC/P/Invoke interop requires careful marshaling
   - String paths → UTF-8 C strings
   - Structs → binary layout matching (use `[StructLayout(LayoutKind.Sequential)]`)
   - Callbacks: complex in .NET (consider managed delegates)

3. **Process spawning:** Wine runs as a separate process, but input/output must bridge back to C#
   - IPC via pipes or sockets
   - State tracking (running/stopped/crashed) in C#

---

### 3.2 Game Launch Pipeline

**Current TeknoParrotUI Desktop Flow:**
```
GameSession.Launch(gameProfile)
  ↓
ProtonLauncher.ResolveWineBinary() [Linux/desktop only]
  ↓
SpawnWindowsProcess(gamePath, args)
  ↓
Game running (RawInput listener watches for input)
```

**New Android Flow:**
```
GameSession.Launch(gameProfile)
  ↓
AndroidGameLauncher.PrepareContainer(gameProfile)
  ├─ CreateContainer if missing
  ├─ SetupWinePrefix (if first run)
  ├─ CopyGameBinary to wine C:\Games\
  ├─ GenerateInputProfile (light-gun preset if GunGame=true)
  └─ ConfigureALSA/DXVK per game settings
  ↓
AndroidGameLauncher.StartGame(gameProfile, gameBinary)
  ├─ ExecuteInWine(program.exe, args, envVars)
  └─ Attach InputListenersManager (AndroidTouchListener activated)
  ↓
Game running + Touch input flowing
```

**Required Files:**

#### **AndroidGameLauncher.cs** (~400 lines)

```csharp
public class AndroidGameLauncher : IGameLauncher
{
    private readonly WinlatorNativeInterop _native;
    private readonly GameProfile _profile;
    private InputListenersManager _inputManager;
    
    public async Task LaunchAsync(string gamePath, CancellationToken ct)
    {
        // 1. Container prep
        var containerName = $"tp_{_profile.GameNameInternal}";
        if (!await ContainerExistsAsync(containerName, ct))
        {
            await _native.CreateContainerAsync(containerName, "wine-8.0", ct);
            await SetupPrefixAsync(containerName, ct);
        }
        
        // 2. Copy game files (for games stored on device only; USB streaming TBD)
        var winePath = await _native.GetContainerRootPathAsync(containerName, ct);
        await CopyGameBinariesAsync(gamePath, Path.Join(winePath, "drive_c/Games"), ct);
        
        // 3. Configure environment
        var envVars = BuildWineEnvironment(_profile);
        if (_profile.GunGame)
        {
            envVars["DXVK_ASYNCRT"] = "1"; // perf
            envVars["GALLIUM_DRIVER"] = "virgl"; // better compat on ARM
        }
        
        // 4. Inject DXVK/VKD3D overrides
        await ConfigureGraphicsAsync(containerName, _profile, ct);
        
        // 5. Start input listener (reuses TeknoParrotUi.Common)
        _inputManager = new InputListenersManager();
        _inputManager.Start(_profile);
        
        // 6. Execute game
        var exe = Path.Join(winePath, "drive_c/Games", Path.GetFileName(gamePath));
        await _native.ExecuteInWineAsync(containerName, exe, new[] { }, envVars, ct);
    }
    
    private Dictionary<string, string> BuildWineEnvironment(GameProfile profile)
    {
        return new()
        {
            ["WINEARCH"] = "win32",  // or win64
            ["WINE"] = "/opt/wine/bin/wine",
            ["WINEPREFIX"] = "/home/user/.wine",
            ["STAGING_SHARED_MEMORY"] = "1",
            ["GALLIUM_DRIVER"] = "virgl",
            ["MESA_VK_DEVICE_SELECT"] = "nvidia", // if available
        };
    }
}
```

---

### 3.3 Container & Filesystem Abstraction

**Problem:** Android is sandboxed. Games expect `C:\Games\game.exe`, but filesystem is `/data/app/...`.

**Solution:** Use **PRoot** (Winlator's existing approach):

```
Host Filesystem (Android /data/app/)
     ↓ (PRoot mapping)
PRoot Virtual Filesystem
     ├─ C:/ (mapped to ~/wine/drive_c/)
     ├─ D:/ (can map external storage)
     └─ / (rest of rootfs for wine/box64)
     ↓
Wine Process (sees C:\Games\game.exe normally)
```

**Container Structure** (per-game prefix):

```
/data/data/com.teknoparrot.android/
├─ files/
│  └─ containers/
│     ├─ game_profile_1/
│     │  ├─ rootfs/         (PRoot's fake root, created once)
│     │  ├─ drive_c/        (C:\, game binaries copied here)
│     │  ├─ wine/           (Wine libraries, symlinks)
│     │  └─ box64.conf      (Box64 optimization settings per-game)
│     └─ game_profile_2/
│        └─ ...
└─ cache/
   └─ box64_dynarec/        (JIT cache, survives across runs)
```

**C# Container Manager:**

```csharp
public class AndroidContainerManager
{
    public async Task CreateContainerAsync(string gameName)
    {
        var containerPath = Path.Join(AppDataPath, "containers", gameName);
        Directory.CreateDirectory(Path.Join(containerPath, "drive_c"));
        Directory.CreateDirectory(Path.Join(containerPath, "rootfs"));
        
        // Unpack minimal Wine distribution
        var wineZip = Path.Join(AssetsPath, "wine-8.0-arm64.zip");
        await ExtractZipAsync(wineZip, containerPath, cancellationToken: default);
        
        // Configure PRoot
        await WritePRootConfigAsync(containerPath, new PRootConfig
        {
            RootPath = containerPath,
            DriveC = Path.Join(containerPath, "drive_c"),
            ExternalStorage = "/storage/emulated/0",
        });
    }
    
    private async Task WritePRootConfigAsync(string containerPath, PRootConfig cfg)
    {
        var initScript = $@"
#!/bin/sh
export WINEPREFIX={cfg.RootPath}/wine
export PATH=/opt/wine/bin:$PATH
/opt/box64/bin/box64 /opt/wine/bin/wine64 ""$@""
";
        await File.WriteAllTextAsync(Path.Join(containerPath, "init.sh"), initScript);
    }
}
```

---

### 3.4 Input Integration: Light-Gun Mapping

**Already Solved in TeknoParrotUI.Common:**
- `GunAnalogMath`: Screen tap → 0–255 analog bytes
- `AndroidTouchListener`: Implements IInputListener
- `InputListenersManager`: Selects listener per-game

**What's Needed:**
1. **Pass touch events from Avalonia.Android → Game Process**
   - Avalonia UI captures raw touch → AndroidTouchListener
   - Listener computes analog bytes
   - **Write to shared memory OR IPC pipe to Wine process**

2. **Wine ↔ Android Touch Bridge**
   - Wine game expects RawInput or DirectInput
   - Winlator normally provides: none (uses Android native input only)
   - **Option A:** Stub Wine DLL (tp-input.dll) that reads from Android pipe
   - **Option B:** Modify Box64 wrapper to translate touch → RawInput calls
   - **Option C:** Use existing SDL2 gamepad backend (emulate as gamepad)

**Recommended: Option A + Fallback to SDL2**

```csharp
// In WinlatorNativeInterop.cs
public class TouchInputPipe
{
    private const string PIPE_NAME = "tp_touch_input";
    
    public async Task WriteAnalogBytesAsync(byte[] xBytes, byte[] yBytes, byte trigger)
    {
        // Write to named pipe shared with Wine process
        // Format: 4 bytes X + 4 bytes Y + 1 byte trigger
        var pipe = File.OpenWrite($"/tmp/{PIPE_NAME}");
        await pipe.WriteAsync(xBytes, 0, xBytes.Length);
        await pipe.WriteAsync(yBytes, 0, yBytes.Length);
        pipe.WriteByte(trigger);
        pipe.Flush();
    }
}

// In AndroidTouchListener.cs (modified)
public override void OnTouch(MotionEvent e)
{
    var (x, y, trigger) = GunAnalogMath.ComputeAim(...);
    
    // Write to pipe for Wine process to read
    await _touchPipe.WriteAnalogBytesAsync(
        BitConverter.GetBytes((int)x),
        BitConverter.GetBytes((int)y),
        trigger ? (byte)1 : (byte)0
    );
}
```

---

## Part 4: Work Estimation & Timeline

### 4.1 Phase-by-Phase Breakdown

#### **Phase 1: Foundation & PoC (Weeks 1–4) — ~160 hours**

| Task | Effort | Depends | Risk |
|------|--------|---------|------|
| Fork brunodev85/winlator | 8h | — | LOW |
| Build Box64 for arm64-v8a | 16h | Fork | MEDIUM (compiler toolchain) |
| Create WinlatorNativeInterop (P/Invoke) | 24h | Box64 | MEDIUM (marshaling) |
| Implement AndroidContainerManager | 20h | Interop | MEDIUM (filesystem) |
| Single-game PoC (Pac-Man via Wine) | 32h | Container | HIGH (first integration) |
| Test on emulator | 20h | PoC | MEDIUM |
| **Subtotal** | **120h** | — | — |

**Deliverable:** One game running, audio working, input partially working (may fall back to keyboard for testing)

**Risk Mitigation:**
- Use official Winlator v11.1 APK as reference (compile + run natively first)
- Pre-build Box64 binaries instead of compiling from scratch
- Test P/Invoke marshaling in isolation (unit tests)

---

#### **Phase 2: Input System & Per-Game Config (Weeks 5–8) — ~200 hours**

| Task | Effort | Depends | Risk |
|------|--------|---------|------|
| Implement TouchInputPipe (Wine ↔ Android bridge) | 24h | Phase 1 PoC | HIGH (IPC, timing) |
| Integrate AndroidTouchListener → pipe | 16h | TouchInputPipe | MEDIUM |
| Load TeknoParrotUI profiles in Container | 12h | AndroidContainerManager | LOW |
| Per-game DXVK/VKD3D config | 16h | Phase 1 | MEDIUM (perf tuning) |
| Test 10+ games (light-gun subset) | 80h | All above | HIGH (compat) |
| Document per-game workarounds | 16h | Tests | LOW |
| UI: Game launcher, settings page | 20h | Phase 1 | LOW |
| **Subtotal** | **184h** | — | — |

**Deliverable:** 10 arcade games running with working light-gun input, per-game profiles

**Risk Mitigation:**
- Start with simple games (no 3D, basic DirectX 9)
- Prioritize Sega/Namco arcade titles (well-documented compat)
- Pre-allocate 50% effort for debugging individual game crashes

---

#### **Phase 3: Performance & Stability (Weeks 9–12) — ~160 hours**

| Task | Effort | Depends | Risk |
|------|--------|---------|------|
| Profile Box64 performance (DynaRec tuning) | 24h | Phase 2 | HIGH (architecture-specific) |
| Benchmark: FPS, latency, memory | 16h | Profiling | MEDIUM |
| ALSA audio optimization (crackling fixes) | 20h | Phase 2 | MEDIUM (audio drivers) |
| Garbage collection tuning (C# ↔ native) | 16h | Interop | MEDIUM |
| Test on real Android hardware (if available) | 40h | All phases | HIGH (device variation) |
| Storage: compress/deduplicate Wine prefixes | 12h | Phase 1 | MEDIUM |
| Crash recovery & error reporting | 16h | All | LOW |
| **Subtotal** | **144h** | — | — |

**Deliverable:** Stable MVP, 50+ games playable, performance within acceptable range (30+ FPS for arcade)

---

#### **Phase 4: Polish & Feature Completeness (Weeks 13–16) — ~120 hours**

| Task | Effort | Depends | Risk |
|------|--------|---------|------|
| UI refinements (theme, transitions) | 24h | Phase 2 | LOW |
| Multiplayer support (2-player games) | 20h | Input system | MEDIUM |
| Cheat codes / save states | 16h | Phase 1 | LOW |
| Locale/language support | 12h | Phase 2 | LOW |
| Documentation & troubleshooting guide | 20h | All | LOW |
| Release build, APK signing, Play Store prep | 16h | All | LOW |
| Bug fixes & edge cases | 20h | Testing | MEDIUM |
| **Subtotal** | **128h** | — | — |

**Deliverable:** Production-ready APK, 100+ games in database, ready for beta testing

---

### 4.2 Summary Timeline

| Phase | Weeks | Hours | Developers | Major Milestone |
|-------|-------|-------|------------|-----------------|
| 1: PoC | 1–4 | 120 | 1.5 | Single game running |
| 2: Input & Profiles | 5–8 | 184 | 1.5 | 10 games, light-gun input |
| 3: Perf & Stability | 9–12 | 144 | 2 | 50+ games, MVP |
| 4: Polish | 13–16 | 128 | 1.5 | Production release |
| **TOTAL MVP** | **16 weeks** | **~576h** | **2 FTE** | **MVP complete** |

**Additional Time (Post-MVP):**
- Ongoing compat fixes: +50–100h/month
- New game profiles + submissions: +20–40h/month
- Community support & PRs: +10–20h/month

---

### 4.3 Resource Requirements

**Team Composition (MVP):**
1. **Lead Developer (100%):** Full-stack (C#, native C, Android)
2. **Graphics/Perf Specialist (60%):** Box64/DXVK tuning
3. **QA/Testing (50%):** Game compat validation
4. **Part-time:** Community QA, translations

**Build Infrastructure:**
- Linux build machine (Box64 cross-compilation, Gradle APK builds)
- Android device or emulator (testing)
- CI/CD pipeline (GitHub Actions, APK auto-builds)

**Dependencies (All Free/OSS):**
- Wine & GE-Proton (prebuilt)
- Box86/Box64 (compile from GitHub)
- Mesa/Vulkan drivers (bundled in APK)
- DXVK (prebuilt or compile)

**No paid tools/licenses required.**

---

## Part 5: Known Limitations & Challenges

### 5.1 Hard Technical Limitations

#### **1. 32-bit vs 64-bit Games**
- **Most Sega/Taito arcade games:** 32-bit (x86)
- **Requires:** Box86 (32-bit) running alongside Box64 (64-bit)
- **Current status:** Winlator supports both, but 32-bit needs extra ARM32 libs
- **Impact:** May increase APK size by 50–100 MB

#### **2. Graphics API Gaps**
| API | Status | Box64 | DXVK | Comment |
|-----|--------|-------|------|---------|
| DirectX 9 | ✅ Good | Native | Full | Most arcade games |
| DirectX 10/11 | ⚠️ Partial | Native | Full | Newer games, rare |
| DirectX 12 | ❌ Poor | Native | VKD3D (experimental) | Modern games, few arcade titles |
| OpenGL | ✅ Good | Mesa (gl4es) | Via Zink | Fallback for DX failures |
| Vulkan | ✅ Good | Direct | Direct | Best performance on ARM |

**Impact:** Old arcade games (1990s–2005) work great; newer DX12 titles may not.

#### **3. Audio Latency**
- **Issue:** PRoot's audio layer adds 50–100ms latency
- **Arcade expectation:** <16ms for trigger feedback
- **Current Winlator:** Acceptable for most games, audible lag in rhythm games
- **Mitigation:** Lower ALSA buffer size (trade-off: crackle risk)

#### **4. Storage & APK Size**
- **Wine prefix:** ~500 MB per game (unshared)
- **Box86/Box64 + Mesa:** ~150 MB (one-time, shared)
- **Complete container:** ~3–5 GB for 10 games
- **Constraint:** Most Android devices: 64–128 GB storage
- **Solution:** External storage support (SD card) — already planned in ewt45 fork

#### **5. Input Latency**
- **Touch → Android listener:** ~5 ms
- **Android listener → Wine pipe:** ~2 ms
- **Wine game reads input:** ~8 ms per frame (60 FPS = 16 ms)
- **Total:** ~15–20 ms, acceptable for arcade but noticeable vs. desktop

#### **6. CPU Performance Variance**
| Device | Box64 Perf | Est. FPS | Playable? |
|--------|-----------|---------|-----------|
| Snapdragon 8 Gen 2 (flagship) | 5–6× | 30–45 | ✅ Yes |
| MediaTek Dimensity 9200 (mid) | 3–4× | 20–30 | ⚠️ Marginal |
| Exynos 1280 (budget) | 1–2× | 15–20 | ❌ No |

**Impact:** Game will run on flagship phones, struggle on budget devices.

---

### 5.2 Architectural Challenges

#### **Challenge 1: C# ↔ Native Boundary Performance**
- **Problem:** Every frame's input update crosses C# ↔ native boundary (potential GC pauses)
- **Solution:** Use object pooling, pre-allocate touch event structures, batch updates
- **Effort:** 16–20h tuning

#### **Challenge 2: Container Lifecycle Management**
- **Problem:** Android app can be killed at any time; Wine process must cleanly terminate
- **Solution:** Implement proper process lifecycle (onPause → wine shutdown signals, onResume → restart)
- **Effort:** 12–16h

#### **Challenge 3: Sharing Code with Desktop TeknoParrotUI**
- **Current:** TeknoParrotUI.Common is platform-agnostic, works on all platforms
- **Issue:** GameLauncher interface differs (desktop uses Process, Android uses JNI/P/Invoke)
- **Solution:** Extract IGameLauncher interface, implement twice (desktop + Android)
- **Effort:** Already done (ProtonLauncher exists for desktop)

#### **Challenge 4: Debugging in Wine**
- **Problem:** If a game crashes in Wine on Android, how do you debug?
- **Solution:** Implement crash dump capture + logcat export (similar to ewt45 fork)
- **Effort:** 12–16h

---

### 5.3 Operational Challenges

#### **Ongoing Maintenance**
- **Wine updates:** New releases every 1–2 weeks
- **Box64 updates:** New releases every 1–2 weeks
- **DXVK updates:** New releases every month
- **Re-evaluation needed:** Per-game tuning may need re-testing with new libraries
- **Effort:** 8–16h per month

#### **Community Support**
- **Expected:** Users will report "Game X doesn't work"
- **Triage needed:** Is it a Winlator bug, TeknoParrotUI bug, or per-game config issue?
- **Effort:** 4–8h per week (grows with userbase)

#### **Per-Game Configuration Database**
- **Current TeknoParrotUI:** 537 games pre-configured for desktop
- **Needed for Android:** Per-game Box64/DXVK overrides, tuning presets
- **Community contribution model:** Users submit patches for new games
- **Effort:** 4h per new game profile

---

## Part 6: Recommended Fork & Starting Strategy

### 6.1 Which Fork to Use?

| Fork | Recommendation | Rationale |
|------|---|---|
| **brunodev85/winlator (Official)** | ✅ START HERE | Stable, well-maintained, largest community, known bugs fixed regularly |
| **ewt45/winlator-fork** | ⚠️ CONSIDER LATER | More features (logging, MIDI, file I/O), but older codebase (~2 years), maintenance risk |
| Other forks (afeimod, etc.) | ❌ AVOID | Device-specific, undocumented, limited community |

### 6.2 Staged Adoption Strategy

**Stage 1 (MVP, Weeks 1–4):**
- Fork brunodev85/winlator **as-is**
- Use version 11.1 (latest stable)
- Minimal modifications (add C# interop layer, don't refactor Java UI yet)
- Goal: Get one game running quickly

**Stage 2 (Post-MVP, Months 2–3):**
- Backport key features from ewt45: logging, error handling
- Evaluate: keep Winlator's Java UI or replace with Avalonia.Android
- Gradually rebase onto new Winlator releases

**Stage 3 (Months 4+):**
- Maintain own fork with TeknoParrotUI-specific patches
- Subscribe to upstream Winlator releases (weekly), cherry-pick important fixes
- Contribute back: input binding system, arcade-game-specific tuning

---

### 6.3 Quick-Start Checklist

**Week 1 Actions:**
- [ ] Clone brunodev85/winlator locally
- [ ] Set up Android NDK + Gradle build environment
- [ ] Build APK and test on emulator (baseline)
- [ ] Read Winlator's source (PRoot/Wine/Box64 wrapper structure)
- [ ] Document current architecture in `FORK_ARCHITECTURE.md`

**Week 2 Actions:**
- [ ] Create `WinlatorNativeInterop.cs` stub (empty P/Invoke declarations)
- [ ] Build Box64 for arm64-v8a, test on emulator
- [ ] Write container creation logic (empty directories, manifest)
- [ ] Deploy minimal proof-of-concept APK

**Week 3 Actions:**
- [ ] Implement P/Invoke marshaling (test with dummy calls)
- [ ] Get Wine binary running in container
- [ ] Copy a simple game binary (Pac-Man .exe) to container
- [ ] Execute game via wine (headless, no rendering yet)

**Week 4 Actions:**
- [ ] Enable graphics output (SDL/Vulkan over Android canvas)
- [ ] Test game rendering on emulator
- [ ] Capture touch input, write to IPC pipe
- [ ] Verify input reaches Wine process

---

## Part 7: Cost-Benefit Analysis

### 7.1 Investment vs. Reward

**Total Investment:** 576+ hours, 2 FTE, ~$100K (US rates)

**Break-Even Metrics:**
- 10,000+ downloads in first 3 months
- 5+ arcade games fully playable
- >80% user rating (Play Store)

**Business Case (Hypothetical):**
- **Revenue:** Donations, ads, premium content (game profiles)
- **Cost:** Infrastructure (GitHub Actions, Play Store account), community support
- **Viability:** Marginal without monetization; sustainable with 5K+ active users

**Strategic Value:**
- **Brand:** "TeknoParrotUI runs on Android" = unique positioning
- **Community:** Attracts mobile gamers, retro arcade fans
- **Technology:** Demonstrates cross-platform arcade emulation leadership

---

## Part 8: Alternative: Smaller Scope (Mobile-Only Launcher)

**If Full Winlator Fork Seems Too Large:**

### Minimal MVP: "TeknoParrotUI Mobile Launcher"

**Scope Reduction:**
- Do NOT fork Winlator
- Use stock Winlator APK (users install separately)
- Build TeknoParrotUI.Android as **game library + deep-link launcher only**

**Features:**
- Game database (537 arcade games)
- Per-game settings (Box64 presets, DXVK tuning hints)
- Click "Play" → launches Winlator with game directory + environment variables
- Winlator handles Wine/Box64/rendering

**Effort:** 60–80 hours (vs. 576 for full fork)

**Trade-off:**
- ❌ No arcade-specific input binding (generic Android touch only)
- ❌ No shared data directory (games stored separately)
- ✅ Faster to market (2–3 weeks)
- ✅ Zero Winlator maintenance burden
- ✅ Users can still update Winlator independently

**Recommendation:** Try this first if risk is concern; upgrade to full fork if user demand warrants.

---

## Conclusion & Recommendations

### Final Assessment

| Question | Answer | Evidence |
|----------|--------|----------|
| **Is it feasible?** | ✅ YES | Winlator exists + works; architecture is understood; TeknoParrotUI.Common is portable |
| **How much work?** | ~576h MVP | 4-month timeline, 2 developers |
| **Best fork?** | brunodev85/winlator | Latest, stable, 18.2k stars, active maintenance |
| **Input binding?** | ✅ YES | TouchInputPipe + IPC to Wine process (new work, not trivial) |
| **Performance?** | ⚠️ MARGINAL | Flagship phones: 30+ FPS; budget phones: 15–20 FPS |
| **Storage footprint?** | 3–5 GB / 10 games | Acceptable on modern devices; can optimize |
| **Maintenance burden?** | 8–16h/month | Ongoing wine/Box64 updates, community support |

### Recommendations

1. **Start with Official brunodev85/winlator** (v11.1)
   - Most stable, largest community, actively maintained
   - Backport features from ewt45 fork as needed

2. **Phase 1: PoC with Pac-Man or similar**
   - Validate C#/.NET interop layer
   - Get single game running (audio, graphics, no input yet)
   - 4 weeks, 1 developer

3. **Phase 2: Input System**
   - Implement TouchInputPipe + AndroidTouchListener integration
   - Load TeknoParrotUI profiles into Winlator container
   - Test 10+ light-gun games
   - 4 weeks, 1.5 developers

4. **Phase 3: Production Hardening**
   - Performance tuning (Box64 DynaRec, DXVK config)
   - Test on real hardware
   - Crash reporting, error recovery
   - 4 weeks, 2 developers

5. **Go / No-Go Decision at Week 12**
   - If 50+ games playable at 25+ FPS: proceed to Phase 4 (polish)
   - If performance inadequate: pivot to "Mobile Launcher" (Winlator wrapper only)

6. **Post-MVP: Sustain**
   - 1 part-time developer for ongoing maintenance
   - Community-driven game profile contributions
   - Monthly newsletter: "This month's playable games"

### Success Criteria for MVP

- ✅ 50+ arcade games run without crashes
- ✅ Light-gun input works (tap = aim, press = trigger)
- ✅ Audio working (even if slightly delayed)
- ✅ Average FPS >25 on mid-range phones (Snapdragon 8 Gen 1+)
- ✅ APK size <1.2 GB (or with optional assets)
- ✅ Zero crashes on clean install (common games)
- ✅ Per-game settings editable in-app

---

## Appendix: File References

**Key Winlator Source Files (to study):**
- [Winlator/app/src/main/cpp/proot/...](https://github.com/brunodev85/winlator/tree/main/app) — PRoot filesystem abstraction
- [gladio/](https://github.com/brunodev85/winlator/tree/main/gladio) — Wine + Box86/Box64 compilation scripts
- [app/src/main/java/com/winlator/...](https://github.com/brunodev85/winlator/tree/main/app/src/main/java) — Java container manager

**Key TeknoParrotUI Files (to reuse):**
- [TeknoParrotUi.Common/InputListening/](https://github.com/TeknoGods/TeknoParrotUI/tree/main/TeknoParrotUi.Common/InputListening) — Input abstraction layer
- [TeknoParrotUi.Common/GameProfile.cs](https://github.com/TeknoGods/TeknoParrotUI/blob/main/TeknoParrotUi.Common/GameProfile.cs) — Game metadata
- [TeknoParrotUi.Android/AndroidTouchListener.cs](https://github.com/TeknoGods/TeknoParrotUI/blob/main/TeknoParrotUi.Android/AndroidTouchListener.cs) — Touch input mapping

---

**Document Version:** 1.0  
**Last Updated:** 2026-07-12  
**Status:** Strategic Analysis Complete, Ready for Implementation Planning

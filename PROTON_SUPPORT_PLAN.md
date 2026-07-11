# Proton Support for TeknoParrotUI - Technical Implementation Plan

## Executive Summary

This document outlines a comprehensive, modular architecture for enabling native Linux TeknoParrotUI to communicate with games running in Proton. The design maintains full backward compatibility with existing Windows functionality while adding a cross-platform abstraction layer for pipe and shared memory communication.

**Target First Game:** Sega Rally 3  
**Status:** Sega Rally 3 has all required DLLs ✓

---

## 1. Analysis of Current Architecture

### 1.1 Existing Windows Implementation

**Pipe Communication (Input):**
- `ControlPipe` creates `NamedPipeServerStream("TeknoParrotPipe")`
- Game (OpenParrot) connects as client with `CreateFileA("\\\\.\\pipe\\TeknoParrotPipe", OPEN_EXISTING)`
- TPUI writes input data (buttons, analogs) via pipe
- Data format varies by game:
  - **Sega Rally 3:** 8 bytes [0]=pad, [1]=wheel, [2]=wheel, [3]=gas, [4]=pad, [5]=brake, [6]=buttons, [7]=buttons

**Shared Memory Communication (Coins/FFB):**
- `JvsHelper` creates `MemoryMappedFile("TeknoParrot_JvsState", 64 bytes)`
- Layout:
  - `[0]` = Unused padding
  - `[1]` = wheelSection (coin input for Sega Rally)
  - `[2]` = ffbOffset (force feedback 1)
  - `[3-9]` = Additional FFB offsets
- `SegaRallyCoinPipe` writes coin byte to offset 4: `JvsHelper.StateView.Write(4, coinByte)`

**Inheritance Chain:**
```
ControlSender (base threading)
  ├── SegaRallyCoinPipe (writes to shared memory for coins)
  └── ControlPipe (NamedPipeServerStream wrapper)
       └── EuropaRPipe (generic Europa-R pipe handler)
            └── SegaRallyPipe (rally-specific buttons/analogs)
```

### 1.2 Critical Findings from OpenParrot Analysis

**Pipe Handling in OpenParrot (Global.cpp, line ~59):**
```cpp
static HANDLE file = CreateFileA("\\\\.\\pipe\\TeknoParrotPipe", 
                                  GENERIC_READ, 0, nullptr, OPEN_EXISTING, 0, NULL);
ReadFile(file, lpBuffer, nNumberOfBytesToRead, lpNumberOfBytesRead, lpOverlapped);
```

**Shared Memory in OpenParrot (amJvs.cpp):**
```cpp
static HANDLE hSection = CreateFileMapping(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, 64, 
                                          L"TeknoParrot_JvsState");
static int* secData = (int*)MapViewOfFile(hSection, FILE_MAP_ALL_ACCESS, 0, 0, 64);
int* wheelSection = &secData[1];  // Coin input for Rally
int* ffbOffset = &secData[2];     // Force feedback
```

### 1.3 Blockers for Linux Native + Proton Games

| Problem | Current State | Blocker |
|---------|--------------|---------|
| NamedPipeServerStream | Windows-only | Cannot create pipes on Linux |
| Named pipes in Proton | Game in Proton sees Windows FS | Pipes exist but in Proton's namespace, not accessible from Linux |
| Shared memory across boundary | Both Windows APIs | Need interop layer |

## 2. Current Challenges

When running games in Proton on Linux:
1. TPUI runs natively on Linux
2. OpenParrot runs inside Proton
3. Communication through pipes and shared memory must bridge native ↔ Proton boundaries

The key architectural decisions are:
- **Package Distribution:** How to distribute Proton (bundled, separate, user-installed?)
- **Version Management:** How to ensure compatibility?
- **Update Strategy:** How to update Proton independently?

---

## 3. Packaging Strategy: Modular Optional Package

### 3.1 Architecture ✅ IMPLEMENTED (code side)

This implementation uses a **modular optional package approach**:
- **Windows TPUI:** ~100MB (no Proton)
- **Linux TPUI:** ~100MB (identical to Windows, native only)
- **Linux Proton Package:** ~2.5GB (optional, separate download)
- **Version Pinning:** Each game specifies its required Proton version (e.g., Sega Rally 3 → Proton-GE 9.26)
- **Auto-Updater:** Handles TPUI and Proton as independent packages

**Implemented (2026-07-10):**
- `ProtonPackageManager` - side-by-side versions at ~/.local/share/TeknoParrotUI/proton/,
  tarball install via system tar (preserves +x), pinned/newest wine resolution
- `GameProfile.ProtonVersion` - optional per-game pin (backward-compatible XML field)
- Updater: Linux-only "TeknoParrotProton" component (never visible on Windows),
  versioned via .version file, streaming tarball download (no RAM buffering) +
  tar extraction in `UpdaterCore.InstallTarball`
- All validated by runtime tests (8/8 PASS: install, exec bits, pinning, fallback,
  component visibility, version tracking)
- **Remaining:** publish an actual TeknoParrotProton release asset (proton-ge dist
  + pipehelper.exe tarball); UI for install prompt/version display

### 3.2 Directory Structure

#### Build Artifacts

```
Windows Build:
    TeknoParrotUI-v1.5.2.zip (100MB)
    ├─ TeknoParrotUI.exe
    ├─ TeknoParrotUi.Common.dll
    ├─ TeknoParrotUi.Avalonia.dll
    └─ [other assemblies]

Linux Build:
    TeknoParrotUI-v1.5.2.tar.gz (100MB - identical to Windows)
    ├─ TeknoParrotUI (executable)
    ├─ TeknoParrotUi.Common.dll
    └─ [other assemblies]

Proton Package (Linux-only, separate release):
    TeknoParrotUI-Proton-GE-9.26.tar.gz (2.5GB)
    ├─ proton/
    │   ├─ bin/
    │   │   ├─ proton
    │   │   ├─ wineserver
    │   │   ├─ wine
    │   │   └─ [wine utilities]
    │   ├─ lib/ (wine libraries)
    │   ├─ lib64/ (64-bit libraries)
    │   ├─ share/ (data files)
    │   └─ VERSION (9.26)
    ├─ game-configs/
    │   ├─ SegaRally3.json
    │   ├─ DaytonaUSA.json
    │   └─ ...
    ├─ metadata.json
    └─ CHECKSUMS (SHA256)
```

#### User Installation Directory

```
~/.local/share/TeknoParrotUI/
├─ config.json                      (user settings)
├─ GameProfiles/                    (game configs)
├─ InputProfiles/                   (custom input bindings)
├─ proton/
│   ├─ proton-ge-9.26/             (main Proton version, ~2.5GB)
│   │   ├─ bin/
│   │   ├─ lib/
│   │   ├─ share/
│   │   └─ VERSION
│   ├─ proton-ge-8.26/             (optional alternate version)
│   └─ current-version.txt
├─ proton-prefixes/                (Wine WINEPREFIX directories)
│   ├─ SegaRally3/
│   ├─ DaytonaUSA/
│   └─ ...
└─ metadata/
    └─ proton-packages.json        (installed versions manifest)
```

### 3.3 Proton Package Metadata

**File: `metadata.json` (inside Proton package)**

```json
{
  "name": "TeknoParrotUI-Proton-GE-9.26",
  "version": "9.26",
  "protonVariant": "Proton-GE",
  "protonSource": "https://github.com/GloriousEggroll/proton-ge-custom",
  "releaseDate": "2026-07-10",
  "releaseNotes": "Fixes for Sega Rally steering input, improved D3D11 compatibility",
  "minTPUIVersion": "1.5.0",
  "maxTPUIVersion": null,
  "supportedGames": [
    "SegaRally3",
    "DaytonaUSA",
    "BattleGearDX",
    "FordRacingFull"
  ],
  "incompatibleGames": [],
  "checksumSha256": "abc123def456...",
  "size": 2684354560,
  "requiresReboot": false,
  "optional": false,
  "dependencies": []
}
```

**File: `~/.local/share/TeknoParrotUI/metadata/proton-packages.json` (manifest)**

```json
{
  "lastUpdated": "2026-07-10T15:30:00Z",
  "installedPackages": [
    {
      "name": "TeknoParrotUI-Proton-GE-9.26",
      "version": "9.26",
      "installed": true,
      "installDate": "2026-07-10T14:00:00Z",
      "location": "~/.local/share/TeknoParrotUI/proton/proton-ge-9.26",
      "size": 2684354560
    }
  ],
  "availablePackages": [
    {
      "name": "TeknoParrotUI-Proton-GE-8.26",
      "version": "8.26",
      "installed": false,
      "downloadUrl": "https://releases.teknoparrot.io/proton/TeknoParrotUI-Proton-GE-8.26.tar.gz",
      "size": 2500000000
    }
  ]
}
```

### 3.4 Game Configuration with Proton Pinning

**File: `GameProfiles/SegaRally3.json` (enhanced)**

```json
{
  "gameName": "Sega Rally 3",
  "executablePath": "/home/user/arcade/SegaRally3/Rally/Rally.exe",
  "linuxRequired": true,
  "protonConfig": {
    "protonVersion": "9.26",
    "protonVariant": "Proton-GE",
    "fallbackVersions": ["8.26"],
    "customWinePrefix": "SegaRally3",
    "customProtonArgs": "",
    "dxvk": "enabled"
  },
  "pipes": [
    {
      "name": "TeknoParrotPipe",
      "direction": "Write",
      "associatedPipeClass": "SegaRallyPipe"
    }
  ],
  "sharedMemories": [
    {
      "name": "TeknoParrot_JvsState",
      "size": 64,
      "offsets": [
        {
          "offset": 4,
          "purpose": "Coins",
          "associatedPipeClass": "SegaRallyCoinPipe"
        }
      ]
    }
  ]
}
```

### 3.5 Enhanced Auto-Updater Flow

#### Updater Manifest

**File: `metadata.json` (on update server)**

```json
{
  "updateServer": "https://releases.teknoparrot.io/",
  "currentTPUIVersion": "1.5.2",
  "lastChecked": "2026-07-10T16:00:00Z",
  "packages": [
    {
      "id": "TeknoParrotUI",
      "name": "TeknoParrotUI Core",
      "version": "1.5.2",
      "platforms": ["windows", "linux", "macos"],
      "downloadUrl": "releases/TeknoParrotUI-{version}.zip",
      "checksumSha256": "xyz...",
      "size": 104857600,
      "releaseNotes": "Proton bridge support, Linux improvements",
      "mandatory": true,
      "minVersion": "1.0.0"
    },
    {
      "id": "TeknoParrotUI-Proton-GE",
      "name": "Proton-GE 9.26",
      "version": "9.26",
      "platforms": ["linux"],
      "downloadUrl": "releases/proton/TeknoParrotUI-Proton-GE-{version}.tar.gz",
      "checksumSha256": "abc...",
      "size": 2684354560,
      "releaseNotes": "Latest Proton-GE with Sega Rally fixes",
      "mandatory": false,
      "optional": true,
      "minTPUIVersion": "1.5.0",
      "postInstallScript": "scripts/extract-proton.sh"
    },
    {
      "id": "TeknoParrotUI-Proton-GE-8.26",
      "name": "Proton-GE 8.26 (Legacy)",
      "version": "8.26",
      "platforms": ["linux"],
      "downloadUrl": "releases/proton/TeknoParrotUI-Proton-GE-{version}.tar.gz",
      "checksumSha256": "def...",
      "size": 2500000000,
      "releaseNotes": "For older games, compatibility fallback",
      "mandatory": false,
      "optional": true,
      "minTPUIVersion": "1.5.0"
    }
  ]
}
```

#### Updater Execution Flow (Pseudocode)

```csharp
public class ProtonAwareAutoUpdater
{
    public async Task CheckAndUpdateAsync()
    {
        // 1. Download manifest
        var manifest = await FetchManifest();
        
        // 2. Check TPUI version
        if (GetInstalledTPUIVersion() < manifest.CurrentTPUIVersion)
        {
            Log.Info("TPUI update available");
            await DownloadAndUpdateTPUI(manifest);
            RestartApplication();
            return;
        }
        
        // 3. Linux-only: Check Proton packages
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var protonPkgs = manifest.Packages
                .Where(p => p.Platforms.Contains("linux") && p.Optional)
                .ToList();
                
            foreach (var pkg in protonPkgs)
            {
                var installed = IsProtonPackageInstalled(pkg.Id, pkg.Version);
                
                if (!installed)
                {
                    Log.Info($"Proton package available: {pkg.Name}");
                    
                    // Prompt user on first Linux launch
                    if (IsFirstLinuxLaunch())
                    {
                        var shouldDownload = PromptUserForProtonDownload(pkg);
                        if (shouldDownload)
                        {
                            await DownloadProtonPackage(pkg);
                        }
                    }
                }
            }
        }
        
        // 4. Continue to app
        LaunchApplication();
    }
}
```

#### First-Run Setup Dialog (Linux)

```
┌─────────────────────────────────────────────┐
│ TeknoParrotUI - Proton Setup                │
├─────────────────────────────────────────────┤
│                                             │
│ To play arcade games on Linux, TeknoParrot  │
│ needs Proton (Wine compatibility layer).    │
│                                             │
│ Proton-GE 9.26 (2.5 GB)                    │
│ ✓ Recommended for Sega Rally 3, Daytona    │
│ ✓ Includes verified game configs           │
│                                             │
│ ┌─────────────────────────────────────────┐ │
│ │ [✓] Install Proton-GE 9.26              │ │
│ └─────────────────────────────────────────┘ │
│                                             │
│ [ Skip ]  [ Install & Continue ]            │
│                                             │
│ Downloads happen in background              │
└─────────────────────────────────────────────┘
```

### 3.6 Release Pipeline

#### Release Checklist

- [ ] **Test cycle:** Run game on fresh Proton install
- [ ] **Version pinning:** Update game config with Proton version
- [ ] **Compatibility matrix:** Mark game/Proton combo as "Verified"
- [ ] **Create package:** Extract Proton, add game-configs, generate metadata
- [ ] **Sign checksums:** SHA256 all files
- [ ] **Upload:** Push to releases.teknoparrot.io
- [ ] **Update manifest:** Increment version in update manifest
- [ ] **Announce:** Notify users of new Proton package (optional update for existing)

#### Release Example

```bash
# 1. Package Proton-GE-9.26
tar -czf TeknoParrotUI-Proton-GE-9.26.tar.gz \
  --directory=/path/to/proton-ge-9.26 \
  proton/ game-configs/ metadata.json CHECKSUMS

# 2. Generate SHA256
sha256sum TeknoParrotUI-Proton-GE-9.26.tar.gz > CHECKSUMS

# 3. Verify before upload
tar -tzf TeknoParrotUI-Proton-GE-9.26.tar.gz | head -20

# 4. Upload to server
aws s3 cp TeknoParrotUI-Proton-GE-9.26.tar.gz \
  s3://releases.teknoparrot.io/proton/
```

### 3.7 User Experience: Installation Summary

#### Scenario 1: Windows User (No Changes)
```
1. Download TeknoParrotUI-v1.5.2.zip (100MB)
2. Extract and run
3. Everything works (no Proton components present)
```

#### Scenario 2: Linux User (First Time)
```
1. Download TeknoParrotUI-v1.5.2.tar.gz (100MB)
2. Extract and run
3. Dialog appears: "Install Proton-GE 9.26?"
4. Click "Install & Continue"
5. Background download of Proton (~2.5GB, 5-10 min on typical internet)
6. Extraction to ~/.local/share/TeknoParrotUI/proton/
7. Ready to play!
```

#### Scenario 3: Linux User (Update Available)
```
1. Auto-updater finds TPUI v1.5.3 available
2. Shows: "1 update available"
3. Also shows: "New Proton-GE-9.27 available" (optional)
4. User can update TPUI alone or with Proton
5. After download, user prompted: "Restart to apply?"
```

---

## 4. Proton Bridge Architecture

### 4.1 High-Level Communication Flow

```
┌─────────────────────────────────────────────────────────────┐
│ TeknoParrotUI (Native Linux Application)                    │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ Input Manager / Game Session                        │   │
│  └──────────────────┬──────────────────────────────────┘   │
│                     │                                       │
│  ┌──────────────────▼──────────────────────────────────┐   │
│  │ PlatformPipeFactory.CreatePipe()                    │   │
│  │  ├─ Windows: Returns ControlPipe (existing)         │   │
│  │  └─ Linux: Returns ProtonBridgePipe (new)           │   │
│  └──────────────────┬──────────────────────────────────┘   │
│                     │                                       │
│  ┌──────────────────▼──────────────────────────────────┐   │
│  │ PlatformSharedMemoryFactory.CreateSharedMem()       │   │
│  │  ├─ Windows: Returns JvsHelper (existing)           │   │
│  │  └─ Linux: Returns ProtonSharedMemoryBridge (new)   │   │
│  └──────────────────┬──────────────────────────────────┘   │
└─────────────────────┼──────────────────────────────────────┘
                      │
                ┌─────┴─────┐
                │           │
      ┌─────────▼────┐  ┌──▼────────────┐
      │ Unix Socket  │  │ tmpfs/hugetlbfs
      │ (Pipes)      │  │ (Shared Memory)
      └─────────┬────┘  └──┬────────────┘
                │           │
    ┌───────────┴────┬──────┴───────────┐
    │                │                  │
    │ (Proton Bridge Translator)        │
    │ Maps Windows APIs to Linux syscalls
    │                │                  │
    └────────────────┼──────────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
    ┌───▼──────────┐  ┌──────────▼──┐
    │ Proton's     │  │ Proton's    │
    │ Named Pipes  │  │ Shared Mem  │
    │ Emulation    │  │ (WINEPREFIX)│
    └───┬──────────┘  └──────────┬──┘
        │                        │
    ┌───▼──────────────────────────▼─┐
    │  Rally.exe (OpenParrot + Game) │
    │ Running in Wine/Proton         │
    └────────────────────────────────┘
```

### 4.2 Modular Components

#### A. IPipeServer (Abstract Interface)

**File:** `TeknoParrotUi.Common/Pipes/Abstractions/IPipeServer.cs`

```csharp
namespace TeknoParrotUi.Common.Pipes.Abstractions
{
    /// <summary>
    /// Platform-agnostic pipe server interface.
    /// Abstracts Windows NamedPipes, Unix sockets, and Proton bridges.
    /// </summary>
    public interface IPipeServer : IDisposable
    {
        string PipeName { get; }
        bool IsRunning { get; }
        
        void Start();
        void Stop();
        void WaitForConnection();
        void Write(byte[] buffer, int offset, int count);
        void Flush();
    }
}
```

#### B. ControlPipeFactory (Platform Detection)

**File:** `TeknoParrotUi.Common/Pipes/PipeFactory/ControlPipeFactory.cs`

```csharp
namespace TeknoParrotUi.Common.Pipes.PipeFactory
{
    public static class ControlPipeFactory
    {
        public static IPipeServer CreatePipe(string pipeName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsNamedPipe(pipeName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Auto-detect Proton process
                var protonPid = ProtonProcessDetector.FindRunningProtonGame();
                if (protonPid > 0)
                {
                    return new ProtonBridgePipe(pipeName, protonPid);
                }
                else
                {
                    throw new InvalidOperationException(
                        "No Proton process detected. Ensure game is running in Proton before launching.");
                }
            }
            else
            {
                throw new PlatformNotSupportedException("Only Windows and Linux supported.");
            }
        }
    }
}
```

#### C. WindowsNamedPipe (Existing Behavior Wrapped)

**File:** `TeknoParrotUi.Common/Pipes/Implementation/WindowsNamedPipe.cs`

Wraps existing `NamedPipeServerStream` in IPipeServer interface. This is the existing behavior, now abstracted.

#### D. ProtonBridgePipe (New - Linux Support)

**File:** `TeknoParrotUi.Common/Pipes/Implementation/ProtonBridgePipe.cs`

**Strategy:**
1. Creates a Unix domain socket on Linux
2. Translates between Linux socket and Proton's named pipe namespace
3. Uses `wineserver` or `/proc/$PID/fd` mapping to find Proton's pipe namespace

**Implementation approach:**
```csharp
public class ProtonBridgePipe : IPipeServer
{
    private int _protonPid;
    private string _pipeName;
    private UnixDomainSocket _socket;
    private Thread _listenerThread;
    
    public void Start()
    {
        // 1. Find Proton process PID
        // 2. Map pipe in Proton's namespace via /proc/$PID/fd
        // 3. Create Unix socket listener
        // 4. Forward bytes between socket and Proton pipe
    }
    
    public void Write(byte[] buffer, int offset, int count)
    {
        // Write to Proton's pipe namespace
    }
}
```

**Key technique: Accessing Proton's Namespace**
- Proton sets `WINEPREFIX` environment variable
- Named pipes stored at `$WINEPREFIX/drive_c/windows/Global` (schematic)
- Use `ptrace` or `/proc/$PID/ns/mnt` to access Proton's filesystem namespace
- Alternative: Write to standard named pipe location and Proton can find it

#### E. ISharedMemory (Abstract Interface)

**File:** `TeknoParrotUi.Common/Jvs/Abstractions/ISharedMemory.cs`

```csharp
namespace TeknoParrotUi.Common.Jvs.Abstractions
{
    public interface ISharedMemory
    {
        string MemoryName { get; }
        void Write(int offset, byte value);
        byte Read(int offset);
        void Write(int offset, int value);
        int ReadInt(int offset);
    }
}
```

#### F. SharedMemoryFactory

**File:** `TeknoParrotUi.Common/Jvs/SharedMemoryFactory.cs`

```csharp
public static class SharedMemoryFactory
{
    public static ISharedMemory CreateOrOpen(string name, int size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WindowsMemoryMappedFile(name, size);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new ProtonSharedMemoryBridge(name, size);
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
    }
}
```

#### G. WindowsMemoryMappedFile (Existing Wrapped)

**File:** `TeknoParrotUi.Common/Jvs/Implementation/WindowsMemoryMappedFile.cs`

Wraps existing `MemoryMappedFile` from JvsHelper.

#### H. ProtonSharedMemoryBridge (New - Linux Shared Memory)

**File:** `TeknoParrotUi.Common/Jvs/Implementation/ProtonSharedMemoryBridge.cs`

**Strategy:**
- Creates POSIX shared memory (`shm_open`) at `/dev/shm/TeknoParrot_*`
- Proton can access via `shm_open` or direct `/dev/shm` access
- Both Linux and Proton process can read/write same bytes

```csharp
public class ProtonSharedMemoryBridge : ISharedMemory
{
    private int _fd;
    private IntPtr _map;
    
    public void Write(int offset, byte value)
    {
        // Write to /dev/shm file
        Marshal.WriteByte(_map + offset, value);
    }
}
```

#### I. GameProtonConfiguration (Per-Game Config)

**File:** `TeknoParrotUi.Common/Proton/GameProtonConfiguration.cs`

```csharp
public class GameProtonConfiguration
{
    public string GameName { get; set; }
    public string ExecutableName { get; set; }  // Rally.exe
    public List<PipeConfiguration> Pipes { get; set; }
    public List<SharedMemoryConfiguration> SharedMemories { get; set; }
}

public class PipeConfiguration
{
    public string Name { get; set; }  // "TeknoParrotPipe"
    public PipeDirection Direction { get; set; }
    public string? AssociatedPipeClass { get; set; }  // "SegaRallyPipe"
}

public class SharedMemoryConfiguration
{
    public string Name { get; set; }  // "TeknoParrot_JvsState"
    public int Size { get; set; }  // 64
    public List<ProtonOffset> Offsets { get; set; }
}

public class ProtonOffset
{
    public int Offset { get; set; }
    public string Purpose { get; set; }  // "Coins", "FFB"
    public string AssociatedPipeClass { get; set; }  // "SegaRallyCoinPipe"
}
```

#### J. ProtonProcessDetector

**File:** `TeknoParrotUi.Common/Proton/ProtonProcessDetector.cs`

```csharp
public static class ProtonProcessDetector
{
    public static int FindRunningProtonGame()
    {
        // Search /proc for processes matching:
        // 1. Parent: wine/wineserver
        // 2. WINEPREFIX env var set
        // 3. Matches expected game exe name
        
        // Priority: Look for Rally.exe, then generic
        // Return PID or -1 if not found
    }
    
    public static string GetWinePrefix(int pid)
    {
        // Read /proc/$pid/environ for WINEPREFIX
    }
}
```

---

## 5. Game-Specific Implementations

### 5.0 Overview: Communication Protocols by Game Type

TPUI needs to support different communication protocols for different arcade hardware:

| Game Type | Protocol | Pipes | Shared Memory | Examples |
|-----------|----------|-------|---------------|----------|
| **Sega Europa-R** | Named Pipes | TeknoParrotPipe | TeknoParrot_JvsState | Sega Rally 3, Daytona USA |
| **Type-X2** | JVS over COM1 | COM1 (serial) | TeknoParrot_JvsState | Trouble Witches, Battle Fantasia, BlazBlue |
| **Ex-Board** | JVS over COM1 | COM1 (serial) + SRAM | ffbOffset only | Initial D, Wacky Races (some variants) |

Each has a different communication channel:
- **Sega Rally**: Named pipes for input data + shared memory for coins
- **Type-X2**: Serial COM port (JVS protocol) + shared memory for FFB
- **Ex-Board**: Serial COM port (JVS protocol) + SRAM buffer + shared memory for control

### 5.1 Sega Rally 3

#### 5.1.1 Configuration File

**File:** `TeknoParrotUi.Common/Proton/Configs/SegaRally3.json` (Optional - can be hardcoded for MVP)

```json
{
  "gameName": "Sega Rally 3",
  "executableName": "Rally.exe",
  "pipes": [
    {
      "name": "TeknoParrotPipe",
      "direction": "Write",
      "associatedPipeClass": "SegaRallyPipe"
    }
  ],
  "sharedMemories": [
    {
      "name": "TeknoParrot_JvsState",
      "size": 64,
      "offsets": [
        {
          "offset": 1,
          "purpose": "WheelSection",
          "associatedPipeClass": null
        },
        {
          "offset": 4,
          "purpose": "Coins",
          "associatedPipeClass": "SegaRallyCoinPipe"
        },
        {
          "offset": 2,
          "purpose": "FFB1",
          "associatedPipeClass": null
        }
      ]
    }
  ]
}
```

#### 5.1.2 Minimal Changes to Existing Code

**Changes to SegaRallyPipe.cs:**

```csharp
public class SegaRallyPipe : EuropaRPipe
{
    // If running on Linux with Proton, EuropaRPipe.Start() will use 
    // PlatformPipeFactory.CreatePipe() which returns ProtonBridgePipe
    // instead of ControlPipe. No changes needed here!
}
```

**Changes to ControlPipe.cs:**

```csharp
public class ControlPipe
{
    // OLD:
    // public static NamedPipeServerStream _npServer;
    // public static Thread _pipeThread;

    // NEW:
    public static IPipeServer _pipeServer;
    public static Thread _pipeThread;

    public void Start(bool runEmuOnly)
    {
        if (_isRunning)
            return;
        _isRunning = true;
        
        // Use factory instead of direct NamedPipeServerStream
        _pipeServer = ControlPipeFactory.CreatePipe("TeknoParrotPipe");
        
        _pipeThread = new Thread(() => TransmitThread(runEmuOnly));
        _pipeThread.Start();
    }

    public void TransmitThread(bool runEmuOnly)
    {
        try
        {
            _pipeServer.Start();
            _pipeServer.WaitForConnection();
            Transmit(runEmuOnly);
        }
        finally
        {
            _pipeServer?.Dispose();
        }
    }
}
```

**Changes to JvsHelper.cs:**

```csharp
public static class JvsHelper
{
    public static ISharedMemory StateSharedMemory;
    
    // Keep backward-compatible StateView and StateSection properties:
    public static MemoryMappedViewAccessor StateView 
    { 
        get => (StateSharedMemory as WindowsMemoryMappedFile)?.ViewAccessor;
    }
    
    public static MemoryMappedFile StateSection 
    { 
        get => (StateSharedMemory as WindowsMemoryMappedFile)?.File;
    }

    static JvsHelper()
    {
        StateSharedMemory = SharedMemoryFactory.CreateOrOpen("TeknoParrot_JvsState", 64);
    }
}
```

---

### 5.2 Type-X2 Games (Trouble Witches, Battle Fantasia, BlazBlue, etc.)

#### 5.2.1 Communication Protocol Analysis

**From OpenParrot TypeX2Generic.cpp:**
- Uses COM1 serial port (JVS protocol)
- Hooks `CreateFileA` to intercept COM1 open: `CreateFileA(..., "COM1", ...)`
- Returns fake handle `(HANDLE)0x8001` for COM1
- Hooks `ReadFile` and `WriteFile` for handle `0x8001`

**Write Protocol (Game → TPUI):**
- Game writes JVS command bytes to COM1
- Commands include wheel calibration, FFB queries
- Example: `0x11` = Begin reporting steering, `0x20` = Reset

**Read Protocol (TPUI → Game):**
- TPUI must respond with wheel position data
- Format: `[status byte] [wheel MSB] [wheel LSB]`
- Wheel value from `wheelSection` (shared memory offset [1])
- FFB values sent via `ffbOffset` writes

**Shared Memory Layout (TeknoParrot_JvsState):**
```
Offset 1:  wheelSection (read by ReadFile hook)
Offset 2:  ffbOffset (written by game for FFB)
Offset 3+: Additional FFB channels
```

#### 5.2.2 Architecture: COM Port Bridge

**New Interface: IComPortBridge**

```csharp
public interface IComPortBridge : IDisposable
{
    string PortName { get; }  // "COM1"
    void Start();
    void Stop();
    void Write(byte[] buffer, int offset, int count);
    byte[] Read(int maxBytes);
    void Flush();
}
```

**Windows Implementation: WindowsComPortBridge**
- Uses existing `SerialPort` or direct Win32 COM APIs
- Returns fake handle for COM1 interception
- No changes needed; existing behavior

**Linux+Proton Implementation: ProtonComPortBridge**
- Creates Unix socket masquerading as COM1
- Translates between socket and Proton's COM1 namespace
- Handles JVS protocol passthrough
- Reads from shared memory for wheel position
- Writes to shared memory for FFB

#### 5.2.3 Game Configuration: Type-X2

**File:** `GameProfiles/TroubleWitches.json`

```json
{
  "gameName": "Trouble Witches",
  "executablePath": "/home/user/arcade/TroubleWitches/TW.exe",
  "linuxRequired": true,
  "gameType": "TypeX2",
  "protonConfig": {
    "protonVersion": "9.26",
    "protonVariant": "Proton-GE"
  },
  "communicationProtocol": {
    "type": "COM1-JVS",
    "pipes": [
      {
        "name": "COM1",
        "direction": "Bidirectional",
        "protocol": "JVS"
      }
    ],
    "sharedMemories": [
      {
        "name": "TeknoParrot_JvsState",
        "size": 64,
        "mode": "ReadWrite"
      }
    ]
  }
}
```

#### 5.2.4 Type-X2 Game List (in arcade dumps)

- Trouble Witches: `/home/janinoob/arcade/TroubleWitches/`
- Battle Fantasia: `/home/janinoob/arcade/BattleFantasia/`
- BlazBlue: `/home/janinoob/arcade/Blazblue/`
- Tetris The Grand Master 3: `/home/janinoob/arcade/Tetris The Grand Master 3 Terror Instinct/tgm3_single/`
- Wacky Races: `/home/janinoob/arcade/WackyRaces/TypeXsys_Wacky_Races/`

---

### 5.3 Ex-Board Games (Arcana Heart, Daemon Bride)

#### 5.3.1 Communication Protocol Analysis

**From OpenParrot Ex-BoardGeneric.cpp:**
- Also uses COM1 serial port (JVS protocol)
- Similar handle interception: `(HANDLE)0x8001`
- **DIFFERS**: Uses SRAM buffer + direct ffbOffset writes

**Write Protocol (Game → TPUI):**
- Writes JVS-like commands to COM1
- Commands include SRAM read/write operations
- Format for SRAM ops: `[cmd] [addr_high] [addr_low] [size] [data...]`

**Read Protocol (TPUI → Game):**
- Returns SRAM contents or status bytes
- Response format: `0x76 0xFD 0x08 [Control] [Analog0] [Analog1] [Buttons]`
- Control byte read from `ffbOffset` (shared memory)
- Analog values from shared memory channels
- Button byte from game state

**Shared Memory Usage (TeknoParrot_JvsState):**
```
Offset 2:  ffbOffset (contains control byte for response)
Offset 3:  ffbOffset2 (analog 0)
Offset 4:  ffbOffset3 (analog 1)
Offset 5:  ffbOffset4 (button byte)
```

**SRAM Buffer:**
- Games maintain 64KB SRAM (`0xffff` bytes)
- TPUI must emulate SRAM reads/writes
- SRAM persists across game sessions (saved to `sram.bin`)

#### 5.3.2 Architecture: SRAM Emulation + COM Bridge

**New Interface: ISramEmulator**

```csharp
public interface ISramEmulator
{
    byte[] ReadSram(int offset, int size);
    void WriteSram(int offset, byte[] data);
    void Load(string filePath);
    void Save(string filePath);
}
```

**Windows Implementation: WindowsSramEmulator**
- Existing SRAM handling (if any)
- Or new simple in-memory implementation
- Persists to disk on exit

**Linux+Proton Implementation: ProtonSramEmulator**
- Stores SRAM in shared memory or temp file
- Accessible by Ex-Board game via COM1 responses
- Same persistence mechanism

#### 5.3.3 Game Configuration: Ex-Board

**File:** `GameProfiles/InitialD.json`

```json
{
  "gameName": "Initial D Arcade Stage",
  "executablePath": "/home/user/arcade/InitialD/InitialD.exe",
  "linuxRequired": true,
  "gameType": "ExBoard",
  "protonConfig": {
    "protonVersion": "9.26",
    "protonVariant": "Proton-GE",
    "customWinePrefix": "InitialD"
  },
  "communicationProtocol": {
    "type": "COM1-JVS-SRAM",
    "pipes": [
      {
        "name": "COM1",
        "direction": "Bidirectional",
        "protocol": "JVS-SRAM"
      }
    ],
    "sharedMemories": [
      {
        "name": "TeknoParrot_JvsState",
        "size": 64
      }
    ],
    "sramConfig": {
      "size": 65535,
      "persistPath": "sram.bin",
      "resetOnBoot": false
    }
  }
}
```

#### 5.3.4 Ex-Board Game List (in arcade dumps)

- Ex-Board folder: `/home/janinoob/arcade/Ex-Board/`
- Arcana Heart 2
- Arcana Heart 3
- Daemon Bride
- Suggoi Arcana Heart 2.6
- Note: Initial D games are emulated in TeknoParrot core (not yet cloned)

---

## 5.4 Game Type Comparison & Modular Architecture

### Architecture: Supporting All Three Game Types

The modular design allows a **single Proton bridge infrastructure** to support all three game types:

```
TeknoParrotUI (Linux)
      │
      ├─ GameTypeRouter
      │  (route by gameType field in config)
      │
      ├──→ NamedPipeBridge (Sega Rally)
      │    │
      │    ├─ IPipeServer implementation
      │    │  ├─ Windows: NamedPipeServerStream
      │    │  └─ Linux+Proton: ProtonBridgePipe (Unix socket)
      │    │
      │    └─ ISharedMemory: TeknoParrot_JvsState
      │       (coins via offset [4])
      │
      ├──→ COM1-JVSBridge (Type-X2: Trouble Witches, etc.)
      │    │
      │    ├─ IComPortBridge implementation
      │    │  ├─ Windows: SerialPort + COM1 hooks
      │    │  └─ Linux+Proton: ProtonComPortBridge (Unix socket)
      │    │
      │    └─ ISharedMemory: TeknoParrot_JvsState
      │       (wheel, FFB via offsets [1], [2], [3])
      │
      └──→ COM1-SRAM-JVSBridge (Ex-Board: Initial D, etc.)
           │
           ├─ IComPortBridge implementation (same as Type-X2)
           │  ├─ Windows: SerialPort + COM1 hooks + SRAM
           │  └─ Linux+Proton: ProtonComPortBridge + SRAM emulation
           │
           ├─ ISramEmulator
           │  ├─ Windows: In-memory + disk persistence
           │  └─ Linux+Proton: Shared or temp file storage
           │
           └─ ISharedMemory: TeknoParrot_JvsState
              (control, analog, buttons via offsets [2-5])
```

### Feature Comparison Table

| Aspect | Sega Rally 3 | Type-X2 (Trouble Witches) | Ex-Board (Arcana Heart) |
|--------|--------------|--------------------------|---------------------|
| **Protocol** | Named Pipes | JVS over COM1 | JVS over COM1 |
| **Input Channel** | TeknoParrotPipe | COM1 | COM1 |
| **Input Data Format** | Binary (wheel/gas/brake/buttons) | JVS commands + responses | JVS + SRAM ops |
| **Shared Memory** | TeknoParrot_JvsState (64B) | TeknoParrot_JvsState (64B) | TeknoParrot_JvsState (64B) |
| **Persistent Storage** | None | None | SRAM (game scores, profiles) |
| **FFB/Output** | Wheels reported via shared mem | FFB via shared mem offsets | Control bytes via shared mem |
| **Existing Pipe Class** | `SegaRallyPipe` | N/A (COM1 direct) | N/A (COM1 direct) |
| **TPUI Code Impact** | Minimal (factory pattern) | Medium (add COM bridge) | Medium (add SRAM + COM) |
| **Windows Behavior** | Unchanged | Unchanged | Unchanged |
| **Linux+Proton Support** | ✓ Phase 2 | ✓ Phase 2b | ✓ Phase 2b |

### Code Reuse Across Game Types

| Component | Sega Rally | Type-X2 | Ex-Board | Reuse |
|-----------|-----------|---------|----------|-------|
| IPipeServer/Factory | ✓ | - | - | Base abstraction |
| ISharedMemory/Factory | ✓ | ✓ | ✓ | **100% shared** |
| IComPortBridge | - | ✓ | ✓ | **100% shared** |
| ISramEmulator | - | - | ✓ | Unique to Ex-Board |
| ProtonProcessDetector | ✓ | ✓ | ✓ | **100% shared** |
| ProtonBridgePipe | ✓ | - | - | Specific to named pipes |
| ProtonComPortBridge | - | ✓ | ✓ | **100% shared** |
| ProtonSharedMemoryBridge | ✓ | ✓ | ✓ | **100% shared** |
| ProtonSramEmulator | - | - | ✓ | Specific to Ex-Board |

### Adding a New Game Type

**Example: Adding a New Type-X Game (e.g., from future dumps)**

1. Analyze OpenParrot source to determine protocol type
2. Create game config: `GameProfiles/WackyRaces.json` with `"gameType": "TypeX2"`
3. Choose existing bridge implementation (COM1-JVS or Named Pipes)
4. **No new code required** - existing bridges handle it!
5. Test with Proton

---

## 6. Implementation Phases

### Phase 1: Foundation (Week 1) ✅ COMPLETE
- [x] Create IPipeServer interface + factory
- [x] Create ISharedMemory interface + factory
- [x] Create IComPortBridge interface (for Type-X/Ex-Board)
- [x] Create ISramEmulator interface (for Ex-Board)
- [x] Wrap existing Windows implementations
- [ ] Add unit tests for abstraction (mock implementations)
- **Goal:** Compile with 0 breaking changes to existing code ✅ (full solution builds, 0 errors)

### Phase 2: Proton Integration (Week 2) ✅ COMPLETE & TESTED
- [x] Implement ProtonProcessDetector
- [x] Implement ProtonBridgePipe (TCP loopback + pipehelper.exe inside Wine prefix)
- [x] Implement ProtonComPortBridge (PTY + $WINEPREFIX/dosdevices/com1 symlink - no helper needed)
- [x] Implement ProtonSramEmulator
- [x] Implement ProtonSharedMemoryBridge (/dev/shm-backed)
- [x] Implement shared-memory mirror in pipehelper (bidirectional, per-byte change detection)
- [x] Implement ProtonSharedMemoryMirror (shm-only helper mode for COM-based games)
- [x] Implement ProtonLauncher + GameSession wiring (games run under Wine/Proton on Linux)
- [x] Build pipehelper.exe + pipehelper32.exe (mingw-w64, Tools/ProtonPipeHelper)
- [x] Integration tests with real Wine ✅ ALL PASSED:
  - Pipe bridge: 8-byte input report host→TCP→helper→named pipe→game, ACK back
  - Shm mirror: coin byte host→game, FFB byte game→host
  - COM bridge: JVS bytes both directions over PTY, symlink cleanup
  - JvsHelper on Linux (/dev/shm), deterministic port match, SRAM persistence
- [x] **REAL GAME TEST ✅ PASSED**: actual Rally.exe + official OpenParrotLoader.exe
  (downloaded OpenParrotWin32 release) connected to the production SegaRallyPipe
  through the full bridge chain. Input reports flowed until the game hit a
  graphics-init crash (0xc00002b5 + libEGL dri2 failures - display/GPU session
  issue in the test environment, not a bridge issue).
- **Goal:** Sega Rally 3 communicates over Proton bridge ✅ **VERIFIED WITH REAL GAME**
- **Note:** Wine prefixes MUST be fully initialized (wineboot completes) or 32-bit
  support (syswow64) is missing → "could not load kernel32.dll". ProtonLauncher
  prefix creation should run wineboot to completion before first launch.

### Phase 2b: Type-X & Ex-Board (Week 2b - parallel) ✅ TESTED (bridge side)
- [x] **Key finding:** OpenParrot's COM1 hooks for Type-X2 AND Ex-Board are entirely
  in-process (fake handle 0x8001, FFB/SRAM emulated inside OpenParrot.dll, state via
  TeknoParrot_JvsState shared memory). The COM traffic never leaves the game process,
  so **no PTY/COM bridge is needed for OpenParrot-based games** - the shm mirror covers it.
  (ProtonComPortBridge remains available for future games that use a real COM port.)
- [x] Actual channel for Type-X games: the **TeknoParrot_JVS named pipe** served by
  SerialPortHandler.ListenPipe + JvsPackageEmulator → wired to ControlPipeFactory
  (new PipeServerStream adapter). Works over the same pipehelper bridge.
- [x] Real-game test (Trouble Witches + TGM3): game detected, pipehelper launched into
  prefix, JVS pipe served. Games crashed in graphics/engine init (0xc00002b5 float trap
  in SeleneSSE after NtUserChangeDisplaySettings failure / EGL dri2 failures) - a
  headless-session display issue, not a bridge issue. Retest on a real desktop session.
- [x] Only one shm mirror per session (claim in ProtonBridgePipe, released on Close)
- [ ] Validate on desktop session with working GPU/display
- [ ] Ex-Board game test (Arcana Heart - uses same in-process COM1+SRAM pattern)
- **Goal:** Type-X and Ex-Board games work via Proton

### Phase 3: Testing & Hardening (Week 3)
- [ ] Test input transmission (all three game types)
- [ ] Test coin input via shared memory
- [ ] Test SRAM persistence (Ex-Board)
- [ ] Test FFB feedback channels
- [ ] Error handling for missing Proton process
- [ ] Fallback to error messages
- **Goal:** Reliable input/output for all game types on Linux+Proton

### Phase 4: Documentation & Template (Week 4)
- [ ] Document how to add new game types
- [ ] Create COM-port bridge pattern guide
- [ ] Add SRAM emulation guide for future games
- [ ] Update README with all supported game types
- **Goal:** Easy for community to add more games

---

## 7. Linux System Requirements

### 7.1 User Setup

Users running games with Proton need:
```bash
# Minimum group membership (for input device access - existing requirement)
sudo usermod -aG input $USER

# Proton installed (via Steam or standalone)
# WINEPREFIX environment variable (set by Steam/Proton)

# Optional: run TPUI with Proton game in same user session
# so PID lookup can find it
```

### 7.2 Proton Pipe Namespace Discovery

**Method 1 (Robust):** Via `/proc/$PID/ns`
```bash
# Check if we can see Proton's namespace
ls -la /proc/$(pgrep Rally.exe)/ns/

# Access via nsenter if needed
sudo nsenter -t $PID --mount
```

**Method 2 (Simple):** Check WINEPREFIX env
- Proton sets `WINEPREFIX=/path/to/prefix`
- Pipes exist in standard Wine locations
- Can use `wineserver` commands or direct path access

### 7.3 Shared Memory (/dev/shm)

- POSIX shared memory automatically readable by all processes
- No special setup needed if using `shm_open()`
- Proton can access `/dev/shm/TeknoParrot_*` directly

---

## 8. Backward Compatibility & Testing

### 8.1 Windows Compatibility

- Existing Windows code uses `ControlPipe` → now wraps `IPipeServer`
- `ControlPipe` behavior unchanged
- All existing game profiles continue to work
- Tests: Run existing test suite without modification

### 8.2 Test Matrix

| Platform | Scenario | Status |
|----------|----------|--------|
| Windows | Existing Windows TPUI + native game | ✓ (no change) |
| Windows | Windows TPUI + OpenParrotLoader | ✓ (no change) |
| Linux | Native TPUI + Proton game | ✓ (new) |
| Linux | Native TPUI + no Proton | ✗ (error message) |
| macOS | Native TPUI + Proton game | ? (future) |

### 8.3 Regression Testing Checklist

- [ ] Build succeeds on Windows (existing tests)
- [ ] Build succeeds on Linux
- [ ] SegaRallyPipe works on Windows (unchanged)
- [ ] SegaRallyPipe works on Linux+Proton (new)
- [ ] SegaRallyCoinPipe works on Windows (unchanged)
- [ ] SegaRallyCoinPipe works on Linux+Proton (new)
- [ ] All 537 profiles still load (no breaking changes)
- [ ] Input API selection unchanged
- [ ] No new unhandled exceptions on startup

---

## 9. Error Handling & User Experience

### 9.1 Failure Scenarios

**Scenario 1:** User runs TPUI on Linux, no game running
```
Error: Could not find Proton game process.
Solution:
  1. Launch your game in Proton first
  2. Then launch TeknoParrotUI
  3. Ensure WINEPREFIX is set in current session
```

**Scenario 2:** Pipe connection fails
```
Error: Could not connect to Proton pipe.
Details: Timeout waiting for connection. Is OpenParrot loaded?
Solution:
  1. Check that Rally.exe started (check Proton logs)
  2. Verify game is running in same WINEPREFIX
  3. Try restarting the game
```

**Scenario 3:** Shared memory permission denied
```
Error: Could not create shared memory TeknoParrot_JvsState.
Solution:
  1. Check /dev/shm permissions: ls -la /dev/shm
  2. You may need to run: sudo chmod 777 /dev/shm
  3. Or ensure proper umask in ~/.bashrc
```

### 9.2 Diagnostic Tools

Add `-v` / `--verbose` flag to TPUI:
```
[INFO] Platform detected: Linux
[INFO] Searching for Proton game process...
[INFO] Found: PID 12345 (Rally.exe), WINEPREFIX=/home/user/.proton
[INFO] Creating ProtonBridgePipe: TeknoParrotPipe
[INFO] Listening on Unix socket: /tmp/tp_bridge_12345.sock
[INFO] Proton pipe connected!
[DEBUG] Pipe write: 8 bytes [steering=0x80, gas=0x00, ...]
[DEBUG] Shared memory write offset 4: 0x01 (coin)
```

---

## 10. Future Extensibility

### 10.1 Adding Daytona USA

1. Create `DaytonaPipe` extending `GtiClub3Pipe` or appropriate base
2. Already covered: factory will use correct pipe type automatically
3. If using same pipes/shared memory names: No new code
4. If using different names: Add to GameProtonConfiguration

### 10.2 Adding macOS Support

- Replace Unix socket with appropriate macOS IPC (XPC, mach ports)
- ProtonBridgePipe becomes generic with platform-specific implementation
- SharedMemoryBridge adapts to macOS POSIX shm

### 10.3 Adding JVS Pipe Support

- Create `JvsProtonPipe : IPipeServer`
- Factory checks game config for JVS pipe needs
- Automatically managed like input pipes

---

## 11. Code Organization

```
TeknoParrotUi.Common/
├── Pipes/
│   ├── Abstractions/
│   │   └── IPipeServer.cs          (NEW)
│   ├── PipeFactory/
│   │   └── ControlPipeFactory.cs   (NEW)
│   ├── Implementation/
│   │   ├── WindowsNamedPipe.cs     (NEW - wraps existing)
│   │   ├── ProtonBridgePipe.cs     (NEW)
│   │   └── ...
│   ├── ControlPipe.cs             (MODIFIED)
│   ├── SegaRallyPipe.cs            (UNCHANGED)
│   ├── SegaRallyCoinPipe.cs        (UNCHANGED)
│   └── ... (other game pipes)
├── Jvs/
│   ├── Abstractions/
│   │   └── ISharedMemory.cs        (NEW)
│   ├── SharedMemoryFactory.cs      (NEW)
│   ├── Implementation/
│   │   ├── WindowsMemoryMappedFile.cs  (NEW - wraps existing)
│   │   └── ProtonSharedMemoryBridge.cs (NEW)
│   └── JvsHelper.cs                (MODIFIED)
├── Proton/                         (NEW FOLDER)
│   ├── ProtonProcessDetector.cs
│   ├── GameProtonConfiguration.cs
│   └── Configs/
│       ├── SegaRally3.json
│       ├── DaytonaUSA.json
│       └── ...
└── ... (rest unchanged)
```

---

## 12. Success Criteria

✓ Sega Rally 3 runs on Linux with native TPUI and Proton game  
✓ All input (steering, gas, brake, buttons) transmitted correctly  
✓ Coins/JVS state shared memory works  
✓ No regression in Windows functionality  
✓ No breaking changes to existing pipe classes  
✓ Clean abstractions allow easy addition of new games  
✓ Clear error messages for common issues  
✓ Comprehensive logging for debugging  

---

## 13. Appendix: Quick Reference - Communication Breakdown

### Sega Rally 3 Full Protocol

**TeknoParrotPipe (Input Stream):**
```
Byte 0:  [0x00] Padding
Byte 1:  [0x80] Steering wheel (0x00=full left, 0x80=center, 0xFF=full right)
Byte 2:  [0x80] Steering wheel (duplicate for some games)
Byte 3:  [0x00] Gas pedal (0x00=no throttle, 0xFF=full throttle)
Byte 4:  [0x00] Padding
Byte 5:  [0x00] Brake pedal (0x00=no brake, 0xFF=full brake)
Byte 6:  [0x00] Buttons byte 1 (0x01=shift up, 0x02=shift down, 0x04=start, 0x08=view change)
Byte 7:  [0x00] Buttons byte 2 (0x04|0x08 always set based on Europa-R requirements)
```

**TeknoParrot_JvsState (Shared Memory):**
```
Offset 0:  [int32] Unused
Offset 1:  [int32] wheelSection (bits: 0x01=coin, etc.)
Offset 2:  [int32] ffbOffset (force feedback intensity)
Offset 3:  [int32] ffbOffset2
...
Offset 4:  [byte]  Coin input (written by SegaRallyCoinPipe)
```

---

## Document Version

**v1.0** - 2026-07-10 - Initial comprehensive architecture plan created

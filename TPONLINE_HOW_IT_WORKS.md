# How TeknoParrot Online (TPO2) Actually Works - Current Implementation

## Executive Summary

TeknoParrot Online is a **transparent launcher wrapper** that automates the game launch process. Users don't touch or configure loader parameters—everything is automatic based on:
- **Game Profile** (XML config with game-specific settings)
- **Room Info** (passed as environment variable)
- **Player Slot** (0 = host, 1+ = guest, auto-assigned by server)

---

## The Complete Launch Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                     TPOnline Game Launch                          │
└──────────────────────────────────────────────────────────────────┘

PHASE 1: Browser UI (teknoparrot.com:3333)
┌─────────────────────┐
│ Chrome/CefSharp     │
│ User joins room     │
│ Host clicks Launch  │
└──────────┬──────────┘
           │
           ├─ Server: Assign PlayerId (0=host, 1,2,3=guests)
           ├─ Server: Generate Unique Room ID (server-side only)
           └─ Server: Send LaunchGame signal to all players
           
PHASE 2: Browser Callback (JavaScript → C#)
┌────────────────────────────────────────────────────────┐
│ connection.on("LaunchGame", uniqueRoom, roomName)      │
│ {                                                       │
│   callbackObj.startGame(                               │
│     uniqueRoom,      // Server-generated ID            │
│     roomName,        // User room name                 │
│     gameId,          // Game XML name (e.g., "ID8")   │
│     playerId,        // 0=host, 1+=guest              │
│     playerName,      // Current username              │
│     playerCount      // Total player count            │
│   );                                                   │
│ }                                                       │
└─────────┬──────────────────────────────────────────────┘
          │
PHASE 3: Process Spawning (TPO2Callback.startGame)
┌───────────────────────────────────────────────────────────────┐
│ startGame() in UserLogin.xaml.cs:                              │
│                                                                 │
│ 1. Create ProcessStartInfo:                                    │
│    - Executable: TeknoParrotUi.exe (the main launcher)        │
│    - Args: --profile=ID8.xml --tponline                       │
│                                                                 │
│ 2. Set Environment Variable (AUTOMATIC - User doesn't set):   │
│    - TP_TPONLINE2="uniqueRoomId|playerId|playerName|count"   │
│                                                                 │
│ 3. Start Process:                                              │
│    Process.Start(info)                                         │
│                                                                 │
│ 4. Monitor Exit:                                               │
│    LauncherProcess.Exited += GameProcessExited event         │
└─────────┬──────────────────────────────────────────────────────┘
          │
PHASE 4: New Instance Loads Profile
┌──────────────────────────────────────────────────────────────┐
│ New TeknoParrotUi.exe instance:                               │
│                                                                │
│ 1. Command line args detected: --profile=ID8.xml --tponline  │
│                                                                │
│ 2. Load GameProfile:                                          │
│    - GameProfiles.xml (default settings)                     │
│    - ID8.xml (game-specific config)                          │
│    - UserProfiles/ID8.xml (user overrides, if any)           │
│                                                                │
│ 3. Read Environment Variable (AUTOMATIC):                     │
│    - TP_TPONLINE2 = "uniqueRoomId|0|PlayerName|2"           │
│    - Extract values (no user interaction needed)             │
│                                                                │
│ 4. Apply Game Settings:                                       │
│    - Windowed/Fullscreen                                     │
│    - Resolution                                               │
│    - Input API (XInput, DirectInput, RawInput)               │
│    - Graphics settings                                        │
│    - Network settings (if any)                               │
│    - Netplay settings (read from TP_TPONLINE2)              │
│                                                                │
│ 5. Skip UI, Go Direct to Game Launch:                        │
│    - MainWindow.xaml → GameRunning.xaml                      │
│    - No options dialog                                        │
│    - No profile selection                                    │
│    - Fully automated                                          │
└─────────┬──────────────────────────────────────────────────────┘
          │
PHASE 5: Game Loader Spawning (Fully Automatic)
┌──────────────────────────────────────────────────────────────┐
│ GameProcessManager.CreateGameProcess() is called:            │
│                                                                │
│ This is where loader parameters are set (USER NEVER SEES):   │
│                                                                │
│ 1. Build Command Line for Game Loader:                        │
│    - Select correct loader EXE (cxbxr-ldr.exe for Xbox)     │
│    - Determine DLL to use (loaded from profile)             │
│    - Build arguments string                                  │
│    - Game-specific extra args (resolution, etc.)            │
│                                                                │
│ 2. Set Environment Variables for Loader:                      │
│    - TP_DIRECTHOOK = 1/0 (from profile)                     │
│    - TP_REMOTETHREAD = 1/0 (from profile)                   │
│    - tp_msysType = "..." (from profile)                      │
│    - TP_LOGTOFILE = 1/0 (from ParrotData settings)          │
│    - TP_NETPLAY = info (MAYBE, if netplay needed)           │
│    - [Other emulator-specific vars]                          │
│                                                                │
│ 3. Spawn Loader Process:                                      │
│    ProcessStartInfo info = new(loader_exe, args)            │
│    info.EnvironmentVariables.AddRange(...)                  │
│    Process.Start(info)                                       │
│                                                                │
│ NOTE: User has NO CONTROL over these parameters              │
│       They are determined entirely by:                        │
│       - The loaded GameProfile                               │
│       - ParrotData global settings                           │
│       - The TP_TPONLINE2 environment variable               │
└─────────┬──────────────────────────────────────────────────────┘
          │
PHASE 6: Game Running
┌───────────────────────────────────────┐
│ Emulator executes XBE/game            │
│                                        │
│ CxbxR/Dolphin/ElfLdr handles:         │
│ - Game rendering                      │
│ - Input processing                    │
│ - Network/netplay (via loaded config) │
└────────────┬────────────────────────────┘
             │
             │ Player exits game
             │
PHASE 7: Cleanup
┌──────────────────────────────────────────┐
│ Game process exits                        │
│ ↓                                         │
│ LauncherProcess.Exited event fires       │
│ ↓                                         │
│ TPO2Callback.LauncherProcess_Exited()   │
│ ↓                                         │
│ OnGameProcessExited() → Browser event    │
│ ↓                                         │
│ Browser calls GameSessionEnded() to      │
│ notify server game is done               │
│ ↓                                         │
│ Server resets room IsGameLaunched flag   │
│ ↓                                         │
│ Lobby can launch another game            │
└──────────────────────────────────────────┘
```

---

## Key Points: What Users Control vs What's Automatic

### What Users CAN Control (via GameProfile in UI):

```
✅ Game Selection (dropdown)
✅ Room Name (text input, 8 chars max)
✅ Password (optional, text input)
✅ Input binding (which button = which action)
✅ Windowed/Fullscreen (UI checkbox/settings)
✅ Resolution (if configurable in profile)
✅ Graphics quality (if in profile)
```

### What Users CANNOT Control (Auto from TPOnline):

```
❌ PlayerId (0 = host, 1+ = guest) → Server assigns
❌ Unique Room ID → Server generates
❌ Loader type → Profile determines
❌ Loader DLL → Profile determines
❌ Loader EXE → Profile determines
❌ Loader command-line args → Built automatically
❌ Loader environment variables → Set automatically
❌ Game path → From profile
❌ Netplay parameters → From TP_TPONLINE2 variable
```

**User never opens a dialog to set loader parameters.**
**User never touches command line.**
**User never fiddles with environment variables.**

---

## The Environment Variable Bridge: TP_TPONLINE2

### What It Is:

A **single string** passed from the browser JavaScript callback to the spawned TeknoParrotUi.exe process.

### Format:

```
TP_TPONLINE2="uniqueRoomId|playerId|playerName|playerCount"

Example:
TP_TPONLINE2="MyRoomID8xAz|1|Player2|2"
```

### Components:

| Part | Example | Purpose | Set By |
|------|---------|---------|--------|
| uniqueRoomId | `MyRoomID8xAz` | Server-side session identifier | TPOnline Server |
| playerId | `1` | Player slot (0=host, 1+=guest) | TPOnline Server |
| playerName | `Player2` | Current player's username | TPOnline Server |
| playerCount | `2` | Total players in room | TPOnline Server |

### Where It's Used:

```csharp
// In GameProcessManager or equivalent:
// Read environment variable
string tpolineVar = Environment.GetEnvironmentVariable("TP_TPONLINE2");

if (!string.IsNullOrEmpty(tpolineVar))
{
    var parts = tpolineVar.Split('|');
    string sessionId = parts[0];      // "MyRoomID8xAz"
    int playerId = int.Parse(parts[1]); // 1
    string playerName = parts[2];     // "Player2"
    int playerCount = int.Parse(parts[3]); // 2
    
    // Pass to netplay configuration
    // Configure CxbxR/emulator with:
    // - Session ID for server connection
    // - Player slot assignment
    // - Expected peer count
}
```

---

## How It Works: Step-by-Step Example

### Scenario: Player 1 Hosts, Player 2 Joins, Both Launch

#### **Time T0: Player 1 at TPO Browser**

```
Browser (https://teknoparrot.com:3333)
├─ Game: "Initial D 8" selected
├─ Room: "ID8Test" entered
├─ Password: "secret1" entered
└─ Click: [Create Room]
```

#### **Server Creates Room**

```
Server (ChatHub.CreateRoom):
├─ Validate: No existing room named "ID8Test"
├─ Create: TpRoom { Name="ID8Test", GameName="ID8", ... }
├─ Add Player: TpUser { PlayerId=0, User=Player1, ConnectionId=conn1 }
├─ Broadcast: "UpdatePlayerCount" to room
└─ Status: Room exists, Player 1 is host (PlayerId=0)
```

#### **Time T1: Player 2 Joins**

```
Browser (Player 2):
├─ Sees room list: "ID8Test | Initial D 8 | 1/2"
├─ Click: [Join]
├─ Enter password: "secret1"
└─ Send: SendMessage(gameId="ID8", room="ID8Test", join=true, password="secret1")
```

#### **Server Adds Player 2**

```
Server (ChatHub.SendMessage with join=true):
├─ Validate: Room exists, not full, password correct
├─ Add Player: TpUser { PlayerId=1, User=Player2, ConnectionId=conn2 }
├─ Broadcast: "SetPlayerId" with (playerId=0, "Player1"), (playerId=1, "Player2")
├─ Broadcast: "UpdatePlayerCount" with "2/2"
└─ Status: Room full, ready to launch
```

#### **Time T2: Player 1 (Host) Clicks Launch**

```
Browser (Player 1):
├─ Click: [Launch Game] (only host sees this button)
└─ Send: LaunchGame("ID8Test", "ID8")
```

#### **Server Validates & Signals All Players**

```
Server (ChatHub.LaunchGame):
├─ Validate: Caller is host (PlayerId=0)
├─ Generate: uniqueRoom = "ID8Test_XyZ123" (server-side only)
├─ Check: Room not already launched
├─ Set: room.IsGameLaunched = true
├─ Broadcast: "LaunchGame" to ALL players in room:
│   - Param 1: uniqueRoom = "ID8Test_XyZ123"
│   - Param 2: roomName = "ID8Test"
└─ Status: Launch signal sent
```

#### **Player 1's Browser Receives Signal**

```
JavaScript (chat.js):
connection.on("LaunchGame", function(uniqueRoom, roomName) {
    // uniqueRoom = "ID8Test_XyZ123"
    // roomName = "ID8Test"
    // currentGameId = "ID8"
    // currentPlayerId = 0 (host)
    // playerName = "Player1"
    // playerCount = 2
    
    callbackObj.startGame(
        "ID8Test_XyZ123",  // uniqueRoom
        "ID8Test",         // roomName
        "ID8",             // gameId
        0,                 // playerId (ZERO = HOST)
        "Player1",         // playerName
        "2"                // playerCount
    );
});
```

#### **TPO2Callback.startGame() - Player 1**

```csharp
public void startGame(string uniqueRoom, string roomName, string gameId, 
                      string playerId, string playerName, string playerCount)
{
    // Check: Is game already running?
    if (LauncherProcess != null && !LauncherProcess.HasExited)
        return; // Already running
    
    // Create launch info
    var profileName = gameId + ".xml";  // "ID8.xml"
    var info = new ProcessStartInfo("TeknoParrotUi.exe", 
        $"--profile={profileName} --tponline");
    // Args: "--profile=ID8.xml --tponline"
    
    info.UseShellExecute = false;
    
    // Set the bridge variable
    info.EnvironmentVariables.Add("TP_TPONLINE2", 
        $"{uniqueRoom}|{playerId}|{playerName}|{playerCount}");
    // TP_TPONLINE2="ID8Test_XyZ123|0|Player1|2"
    
    // Spawn new process
    LauncherProcess = Process.Start(info);
    LauncherProcess.EnableRaisingEvents = true;
    LauncherProcess.Exited += LauncherProcess_Exited;
}
```

#### **New TeknoParrotUi.exe Instance - Player 1 (Loading Phase)**

```
Startup (App.xaml.cs / MainWindow.xaml.cs):
├─ Detect: Command line args "--profile=ID8.xml --tponline"
├─ Load: GameProfile ID8.xml
├─ Read: Environment.GetEnvironmentVariable("TP_TPONLINE2")
│        → "ID8Test_XyZ123|0|Player1|2"
│        → Parse: sessionId, playerId=0, playerName, playerCount
├─ Apply: All game settings from profile (windowed, resolution, etc.)
├─ Skip: Options dialog, profile selection, any user interaction
├─ Launch: GameRunning view
└─ Status: Ready to spawn game loader
```

#### **Loader Spawning - Player 1 (Automatic)**

```
GameProcessManager.CreateGameProcess():
├─ Load: ID8.xml contains:
│        - EmulationProfile: "CxbxReloaded"
│        - GamePath: "C:\Games\ID8\id8.xbe"
│        - CustomArguments: "-full" (if any)
│        - TP_DIRECTHOOK: true/false (auto)
│        - TP_REMOTETHREAD: true/false (auto)
├─ Build: Loader command line (user never types this)
│        Loader: cxbxr-ldr.exe
│        Args: [various, determined by profile]
├─ Set: Environment variables (all automatic)
│        TP_DIRECTHOOK=1
│        TP_REMOTETHREAD=0
│        tp_msysType=...
│        [other emulator-specific]
├─ Spawn: Process.Start(new ProcessStartInfo(loaderExe, args))
└─ Result: cxbxr-ldr.exe launches with full game config
```

#### **Game Executes**

```
CxbxR Emulator:
├─ Load: id8.xbe
├─ Check: Environment variable TP_TPONLINE2 (for netplay config)
├─ Configure: Netplay with:
│   - Session ID: "ID8Test_XyZ123"
│   - Player ID: 0 (host)
│   - Expected peers: 2
├─ Render: Game graphics
├─ Handle: Input (per user's configured bindings)
├─ Network: Sync with Player 2's instance
└─ User plays...
```

#### **At Same Time: Player 2's Browser Receives Signal**

```
JavaScript (Player 2):
connection.on("LaunchGame", function(uniqueRoom, roomName) {
    callbackObj.startGame(
        "ID8Test_XyZ123",  // Same uniqueRoom
        "ID8Test",         // Same roomName
        "ID8",             // Same game
        1,                 // playerId (ONE = GUEST)
        "Player2",         // Different playerName
        "2"                // Same playerCount
    );
});
```

#### **Loader Spawning - Player 2 (Automatic)**

```
TPO2Callback.startGame() → GameProcessManager.CreateGameProcess()
├─ Read: TP_TPONLINE2="ID8Test_XyZ123|1|Player2|2"
├─ Profile: Same ID8.xml loaded
├─ Build: Same loader command (cxbxr-ldr.exe)
├─ Env Vars: Same as Player 1 (profile-driven)
├─ Spawn: cxbxr-ldr.exe with netplay config
└─ Result: Player 2's game connects with:
           - Same session ID
           - Different player slot (1, not 0)
```

#### **Game Network Sync**

```
CxbxR Netplay (both instances):
├─ Player 1 (host):
│   - Listens on session "ID8Test_XyZ123"
│   - Slot 0 (host)
│   - Authoritative game state
├─ Player 2 (guest):
│   - Connects to session "ID8Test_XyZ123"
│   - Slot 1 (guest)
│   - Receives game state from host
└─ Sync: Both games run in lockstep
           - Player inputs synchronized
           - Game state deterministic
           - Racing happens in parallel
```

#### **Game Ends - Player 1 Exits**

```
CxbxR:
├─ User closes window or game finishes
├─ Process exits
└─ Windows: Sends WM_CLOSE event

TeknoParrotUi.exe (Launcher):
├─ Monitors: LauncherProcess.Exited event
├─ Fires: LauncherProcess_Exited()
├─ Calls: OnGameProcessExited()
└─ Sends: Browser event → JavaScript

Browser JavaScript:
├─ Detects: Game process exited
├─ Calls: connection.invoke("GameSessionEnded")
└─ Sends: Signal to server

Server (ChatHub.GameSessionEnded):
├─ Finds: Room associated with this player
├─ Resets: room.IsGameLaunched = false
├─ Allows: Room to launch another game
└─ Notifies: All players in room
```

---

## What This Architecture Achieves

### ✅ Zero Configuration by User

```
User only does:
1. Select game from dropdown
2. Enter room name (optional, auto-generated)
3. Enter password (optional)
4. Click [Create] or [Join]
5. Click [Launch]
6. Game plays
7. Exit game
→ DONE. No config files, no parameters, no command-line editing.
```

### ✅ Profile-Driven Automation

```
All technical details driven by XML profile:
├─ Which emulator? (CxbxR, ElfLdr, Dolphin, etc.)
├─ Which DLL/loader? (Downloaded with profile)
├─ Video settings? (Windowed mode, resolution, etc.)
├─ Network mode? (Direct, NAT, none)
├─ Graphics? (Hardware, software, fallback)
└─ Everything loaded from one .xml file
```

### ✅ Transparent Wrapper Pattern

```
TeknoParrotUi.exe acts as a wrapper:
│
├─ When launched with --tponline:
│  ├─ Reads TP_TPONLINE2 environment variable
│  ├─ Applies settings from game profile
│  ├─ Spawns game loader with correct parameters
│  └─ Monitors game process exit
│
├─ When launched normally:
│  ├─ Shows UI for manual profile/game selection
│  ├─ User configures settings
│  └─ Spawns game when ready
│
└─ In both cases:
   └─ Final game loader parameters are identical
```

### ✅ Single Source of Truth

```
Game behavior determined by:
1. Command-line args (--profile=, --tponline)
2. GameProfile XML (auto-loaded)
3. User profile overrides (input bindings, preferences)
4. TP_TPONLINE2 environment variable (netplay context)

Everything else is derived from these 4 inputs.
No hidden config files, no registry hacks, no magic.
```

---

## For the CLI/Launcher Plans

This architecture means:

### ✅ CLI integration is straightforward
```bash
TeknoParrotUi.exe --profile=ID8.xml --tponline --room=MyRoom --action=create
# Same flow as TPOnline browser → just uses CLI args instead
```

### ✅ Quick Game mode fits naturally
```
Find available room → Join → Get LaunchGame signal → Spawn with TP_TPONLINE2
Same exact process, just room selection automated
```

### ✅ Deep links work seamlessly
```
tponline://join?room=MyRoom&game=ID8 → Parse args → Set environment → Spawn
Or via CLI: --room=... args, which feeds into same system
```

### ✅ Discord bot can trigger games
```
Discord webhook → External system → Spawn TeknoParrotUi.exe with args
Same launching mechanism, just invoked from Discord instead of browser
```

---

## Summary: The Key Insight

**TeknoParrot Online is not a custom launcher that requires custom configuration.**

**It's a wrapper that:**
1. Receives minimal parameters (game ID, room name, password)
2. Reads a pre-configured XML profile
3. Automatically builds and spawns the correct loader with correct parameters
4. Passes multi-player context via a single environment variable
5. Monitors and reports game exit

**Users never fiddle with loader parameters because they don't need to.**
**The profile handles everything; the environment variable handles networking.**
**Pure automation.**


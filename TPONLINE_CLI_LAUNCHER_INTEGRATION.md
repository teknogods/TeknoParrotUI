# TeknoParrot Online - CLI & External Launcher Integration Plan

## Important: Single Official TPOnline Server Model

⚠️ **Infrastructure Note**: TeknoParrot Online is a **centralized, single-server system**.

**Server Separation:**
- **Main Website**: `teknoparrot.com` (downloads, news, profiles, etc.) - Separate server
- **TPOnline Service**: `teknoparrot.com:3333` (HTTPS, port 3333) - **This is the ONLY TPOnline lobby server**
- These are two different servers on different ports

**Hosting Model**:
- Self-hosted by TeknoParrot developers
- Custom servers: NOT supported or offered
- All players: Connect to `teknoparrot.com:3333` (port 3333)

The CLI integration assumes users always connect to the official `teknoparrot.com:3333` TPOnline service.
There is no `--server=` argument because custom servers are not available.

---

**Document Purpose**: Design specifications for making TPOnline accessible from command line and external game launchers (Launchbox, Mame, Hyperspin, GameHub, etc.) without requiring browser UI interaction.

**Current Problem**: 
- Users must navigate through browser lobby UI to create/join rooms
- External launchers cannot directly trigger TPO games
- No programmatic way to launch with specific room parameters
- Steep learning curve for casual users

---

## Part 1: Current Architecture Analysis

### Current Flow (Browser-Required)

```
User → External Launcher
    │
    ├─ Spawns TeknoParrotUi.exe (no args)
    │
    ├─ Loads MainWindow
    │
    ├─ User clicks "TPO Chat" tab
    │
    ├─ CefSharp browser loads https://teknoparrot.com:3333
    │
    ├─ User manually:
    │   ├─ Selects game from dropdown
    │   ├─ Enters room name (or clicks existing room)
    │   └─ Clicks "Create" or "Join"
    │
    ├─ Server broadcasts launch signal
    │
    └─ Game launches with multiplayer context
```

**Friction Points:**
1. Manual UI navigation required
2. No way to specify room name from CLI
3. No quick-join mechanism
4. Browser must fully load
5. No headless mode for batched launches
6. Cannot integrate with launcher libraries that expect CLI parameters

### What Exists Today

**TPO Infrastructure:**
- ✅ SignalR real-time communication
- ✅ Room management in memory
- ✅ Game profile database (GameProfiles.GameProfileList)
- ✅ Authentication via ASP.NET Core Identity
- ✅ TPO2Callback bridge (C# ↔ JavaScript)
- ✅ Environment variable passing to game (TP_TPONLINE2)

**Missing:**
- ❌ CLI argument parsing for room operations
- ❌ Headless/non-browser mode
- ❌ Direct SignalR client in TeknoParrotUi.exe
- ❌ Quick Game (auto-join/auto-launch) mode
- ❌ Configuration file for preset lobbies
- ❌ Status queries (room existence, player count)

---

## Part 2: Proposed Solutions

### Solution A: CLI Arguments (Minimum Viable Change)

**Add command-line parameters to TeknoParrotUi.exe:**

```bash
TeknoParrotUi.exe --tponline --game=ID8 --room=TestLobby --action=create --password=secret
TeknoParrotUi.exe --tponline --game=SR3 --room=MyRoom --action=join --password=pass123
TeknoParrotUi.exe --tponline --game=Quake --action=quick-join  # Auto-pick first available room
TeknoParrotUi.exe --tponline --game=WMMT6RR --action=watch     # Join as spectator (future)
```

**Advantages:**
- ✅ Minimal changes to existing codebase
- ✅ External launchers can construct command lines
- ✅ Works with browser (UI shown after launch)
- ✅ Backward compatible (no args = normal UI)

**Disadvantages:**
- ❌ Still loads browser (slow)
- ❌ Must wait for SignalR connection before game launches
- ❌ Complex to coordinate with launcher waiting

**Implementation Effort:** **LOW** (1-2 days)

**Code Changes:**
- Parse `args` in `MainWindow.xaml.cs`
- Store in static config
- JavaScript `chat.js` checks for auto-launch flags
- Auto-populate room fields and trigger join/create

---

### Solution B: Direct SignalR Client (Recommended for Headless)

**Create a dedicated CLI mode that connects directly to SignalR:**

```csharp
// New class: TeknoParrotOnlineClient.cs
public class TeknoParrotOnlineClient
{
    private HubConnection _connection;
    
    public async Task ConnectAsync(string username, string token)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl("https://teknoparrot.com:3333/chatHub", opts =>
            {
                opts.Headers.Add("Authorization", $"Bearer {token}");
            })
            .WithAutomaticReconnect()
            .Build();
        
        await _connection.StartAsync();
    }
    
    public async Task CreateRoomAsync(string roomName, string gameName, string password)
    {
        await _connection.InvokeAsync("SendMessage", 
            gameName, roomName, false, false, password, true);
    }
    
    public async Task JoinRoomAsync(string roomName, string gameName, string password)
    {
        await _connection.InvokeAsync("SendMessage", 
            gameName, roomName, true, false, password, false);
    }
    
    public async Task LaunchGameAsync(string roomName)
    {
        await _connection.InvokeAsync("LaunchGame", roomName, GetCurrentGameName());
    }
}
```

**Two execution modes:**

**1. Headless Console Mode** (for launchers that exec and wait)
```bash
TeknoParrotUi.exe --cli --game=ID8 --room=TestRoom --action=join --wait
# Connects to server, joins room, waits for LaunchGame signal, spawns game, exits
```

**2. GUI Mode** (current, but enhanced)
```bash
TeknoParrotUi.exe --tponline --game=ID8 --room=TestRoom --auto-launch
# Shows UI, but automatically populates and triggers join/create
```

**Advantages:**
- ✅ No browser overhead
- ✅ CLI launcher friendly (exec and wait pattern)
- ✅ Scalable for multiple concurrent instances
- ✅ Can be called from batch scripts
- ✅ Logging and status output

**Disadvantages:**
- ❌ Requires authentication token passing
- ❌ More complex implementation
- ❌ Still tied to game process lifecycle

**Implementation Effort:** **MEDIUM** (3-5 days)

---

### Solution C: Quick Game Mode (Best UX for Casual Users)

**"Quick Game" button/mode that automatically:**
1. Looks for existing rooms for that game
2. If none: creates new room with auto-generated name
3. Waits for opponent
4. Auto-launches when 2+ players ready
5. Returns to lobby after game ends

**User Experience:**

```
[Quick Game] button
    ↓
"Waiting for players..."
    ↓
"3 players joined!"
    ↓
"Launching in 5 seconds..."
    ↓
Game starts
```

**Implementation:**

**New SignalR methods:**
```csharp
// ChatHub.cs additions
public async Task FindOrCreateQuickGameRoom(string gameName)
{
    // Find first room for this game with space
    var availableRoom = UserHandler.ConnectionsOnRooms.Values
        .Where(r => r.GameName == gameName && !r.IsGameLaunched)
        .FirstOrDefault();
    
    if (availableRoom != null && availableRoom.Users.Count < availableRoom.MaxPlayers)
    {
        // Join existing room
        await SendMessage(gameName, availableRoom.Name, join: true, password: "");
    }
    else
    {
        // Create new room
        var newRoomName = GenerateQuickGameRoomName(gameName);
        await SendMessage(gameName, newRoomName, false, false, "", true);
    }
    
    // Notify client of quick game state
    await Clients.Caller.SendAsync("QuickGameJoined", availableRoom?.Name ?? newRoomName);
}

public async Task SetAutoLaunchThreshold(string roomName, int minPlayers)
{
    // When room reaches minPlayers, auto-launch
    if (room.Users.Count >= minPlayers)
    {
        await LaunchGame(roomName, game);
    }
}
```

**Advantages:**
- ✅ Best UX for casual players
- ✅ Reduces decision fatigue
- ✅ "One-click" gaming
- ✅ Works with existing browser UI

**Disadvantages:**
- ❌ Less control (can't pick specific room)
- ❌ May have to wait for players
- ❌ Auto-launch timing coordination difficult

**Implementation Effort:** **MEDIUM** (3-4 days)

---

### Solution D: Configuration File Presets (Power User Feature)

**YAML/JSON config file for preset rooms:**

```yaml
# %APPDATA%/TeknoParrot/TPOnline.yml
profiles:
  id8_casual:
    game: ID8
    room_prefix: ID8Casual
    password: null
    auto_find: true      # Auto-join existing room if available
    
  quake_tournament:
    game: Quake
    room_prefix: Tournament
    password: TourneyPass2024
    min_players: 4
    auto_launch: true
    
  sr3_friends:
    game: SR3
    room: FriendsOnly
    password: SecretCode
    auto_launch: true
```

**CLI usage:**
```bash
TeknoParrotUi.exe --profile=id8_casual
TeknoParrotUi.exe --profile=quake_tournament
```

**Or from launcher:**
```
LaunchBox: 
  Command: TeknoParrotUi.exe
  Arguments: --profile=%SystemVariable%  # Or passed from LaunchBox integration
```

**Advantages:**
- ✅ Users configure once, reuse many times
- ✅ Power users can manage complex setups
- ✅ Works with all UI modes
- ✅ Can version control configs

**Disadvantages:**
- ❌ Adds configuration complexity
- ❌ File management overhead

**Implementation Effort:** **LOW-MEDIUM** (2-3 days)

---

## Part 3: Recommended Implementation Roadmap

### Phase 1: CLI Arguments (v1.0) - **Weeks 1-2**

**Priority: HIGH** | **Complexity: LOW**

Add basic CLI argument parsing:

```csharp
// MainWindow.xaml.cs
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        
        string[] args = Environment.GetCommandLineArgs();
        var config = ParseTPOArgs(args);
        
        if (config.IsTPOEnabled)
        {
            // Set static config for UserLogin to read
            TPOConfig.GameId = config.GameId;
            TPOConfig.RoomName = config.RoomName;
            TPOConfig.Password = config.Password;
            TPOConfig.Action = config.Action; // "create", "join", "quick-join"
            
            // Show TPO tab immediately
            TPOTabItem.IsSelected = true;
        }
    }
}

// UserLogin.xaml.cs
private void RefreshBrowserToStart()
{
    if (TPOConfig.IsConfigured)
    {
        // JavaScript function to auto-populate and trigger action
        Browser?.ExecuteScriptAsync(
            $@"
            document.getElementById('HostGameName').value = '{TPOConfig.GameId}';
            
            switch('{TPOConfig.Action}')
            {{
                case 'create':
                    document.getElementById('HostRoomName').value = '{TPOConfig.RoomName}';
                    if('{TPOConfig.Password}' != '') 
                        document.getElementById('newLobbyPw').value = '{TPOConfig.Password}';
                    document.getElementById('createButton').click();
                    break;
                case 'join':
                    joinRoom('{TPOConfig.RoomName}', '{TPOConfig.Password}');
                    break;
                case 'quick-join':
                    connection.invoke('FindOrCreateQuickGameRoom', '{TPOConfig.GameId}');
                    break;
            }}
            "
        );
    }
}
```

**CLI Arguments Reference:**

| Argument | Format | Example | Purpose |
|----------|--------|---------|---------|
| `--tponline` | Flag | `--tponline` | Enable TPO mode |
| `--game` | Value | `--game=ID8` | Game XML name |
| `--room` | Value | `--room=TestLobby` | Room name (max 8 chars) |
| `--password` | Value | `--password=secret` | Room password |
| `--action` | Value | `--action=create\|join\|quick-join` | What to do |
| `--no-gui` | Flag | `--no-gui` | Hide UI after launch (future) |
| `--wait` | Flag | `--wait` | Don't exit until game ends |

**Example invocations:**
```bash
# Create a public room
TeknoParrotUi.exe --tponline --game=ID8 --room=MyRoom --action=create

# Join a password-protected room
TeknoParrotUi.exe --tponline --game=SR3 --room=Friends --password=pass1 --action=join

# Auto-join first available room for a game
TeknoParrotUi.exe --tponline --game=Quake --action=quick-join

# From external launcher with placeholders
TeknoParrotUi.exe --tponline --game=WMMT6RR --room=%USERNAME% --action=create
```

---

### Phase 2: Quick Game Mode (v1.1) - **Weeks 3-4**

**Priority: HIGH** | **Complexity: MEDIUM**

**Server-side (ChatHub.cs):**

Add new SignalR method:
```csharp
public async Task QuickGame(string gameName)
{
    if (Context.User == null) return;
    
    var user = await _userManager.GetUserAsync(Context.User);
    
    // Find room with space
    var room = UserHandler.ConnectionsOnRooms.Values
        .Where(r => r.GameName == gameName 
                 && !r.IsGameLaunched
                 && r.Users.Count < r.MaxPlayers)
        .FirstOrDefault();
    
    if (room == null)
    {
        // Create new room with auto-generated name
        var newRoomName = GenerateQuickGameRoomName(gameName);
        await CreateRoom(user, newRoomName, gameName, "");
        room = UserHandler.ConnectionsOnRooms[newRoomName];
    }
    else
    {
        // Join existing room
        await JoinRoom(user, room.Name, gameName, "");
    }
    
    // Check if we should auto-launch
    if (room.Users.Count >= 2) // Min 2 players
    {
        // Set a timer to auto-launch in 5 seconds if no one else joins
        _ = Task.Delay(5000).ContinueWith(async _ =>
        {
            if (room.Users.Count >= 2 && !room.IsGameLaunched)
            {
                await LaunchGame(room.Name, gameName);
            }
        });
    }
    
    await Clients.Caller.SendAsync("QuickGameReady", room.Name);
}

private string GenerateQuickGameRoomName(string gameName)
{
    string abbrev = gameName.Length > 4 ? gameName.Substring(0, 4) : gameName;
    string randomSuffix = new string(Enumerable.Range(0, 4)
        .Select(_ => "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"[new Random().Next(36)])
        .ToArray());
    
    return (abbrev + randomSuffix).Substring(0, 8);
}
```

**Client-side JavaScript:**
```javascript
// Add Quick Game button to UI
document.getElementById("quickGameButton").addEventListener("click", function() {
    const gameId = document.getElementById("HostGameName").value;
    document.getElementById("quickGameStatus").style.display = "block";
    
    connection.invoke("QuickGame", gameId).catch(err => console.error(err));
});

// Server notifies when ready
connection.on("QuickGameReady", function(roomName) {
    document.getElementById("quickGameStatus").innerHTML = 
        `<p>Joined: ${roomName}</p><p>Waiting for players...</p>`;
    
    // Show live player count
    connection.invoke("FetchLobbies"); // Refresh to show count
});
```

**UI Addition:**
```html
<!-- In Chat.cshtml -->
<button id="quickGameButton" class="btn btn-success btn-lg">⚡ Quick Game</button>

<div id="quickGameStatus" style="display: none; text-align: center;">
    <p id="quickGameMessage">Finding or creating room...</p>
    <div class="spinner-border" role="status">
        <span class="visually-hidden">Loading...</span>
    </div>
</div>
```

---

### Phase 3: Config File System (v1.2) - **Weeks 5-6**

**Priority: MEDIUM** | **Complexity: LOW**

**New class: TPOConfigManager.cs**
```csharp
public class TPOConfigManager
{
    public static readonly string ConfigPath = 
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "TeknoParrot", "TPOnline.json");
    
    public Dictionary<string, TPOProfile> Profiles { get; set; }
    
    public static TPOConfigManager Load()
    {
        if (!File.Exists(ConfigPath))
            return new TPOConfigManager { Profiles = new Dictionary<string, TPOProfile>() };
        
        var json = File.ReadAllText(ConfigPath);
        return JsonConvert.DeserializeObject<TPOConfigManager>(json);
    }
    
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}

public class TPOProfile
{
    public string GameId { get; set; }
    public string RoomNamePattern { get; set; } // Can contain placeholders
    public string Password { get; set; }
    public string Action { get; set; } // "create", "join", "quick-join"
    public bool AutoLaunch { get; set; }
    public int MinPlayers { get; set; } = 2;
}
```

**Default config file (~\AppData\Roaming\TeknoParrot\TPOnline.json):**
```json
{
  "profiles": {
    "id8_casual": {
      "gameId": "ID8",
      "roomNamePattern": "ID8Cas",
      "password": null,
      "action": "quick-join",
      "autoLaunch": true,
      "minPlayers": 2
    },
    "quake_tournament": {
      "gameId": "Quake",
      "roomNamePattern": "ToRnay",
      "password": "TournamentPass2024",
      "action": "create",
      "autoLaunch": false,
      "minPlayers": 4
    }
  }
}
```

**CLI usage:**
```bash
TeknoParrotUi.exe --profile=id8_casual
TeknoParrotUi.exe --profile=quake_tournament --wait
```

---

### Phase 4: Headless CLI Client (v2.0) - **Weeks 7-10** (Optional)

**Priority: MEDIUM** | **Complexity: HIGH**

**Create separate executable: `TPOnlineClient.exe`**

```csharp
// Program.cs for console app
static async Task Main(string[] args)
{
    var options = ParseArguments(args);
    
    using var client = new TeknoParrotOnlineClient();
    
    try
    {
        await client.AuthenticateAsync(options.Username, options.Token);
        
        switch (options.Action)
        {
            case "create":
                await client.CreateRoomAsync(options.RoomName, options.GameId, options.Password);
                Console.WriteLine($"Created room: {options.RoomName}");
                break;
                
            case "join":
                await client.JoinRoomAsync(options.RoomName, options.GameId, options.Password);
                Console.WriteLine($"Joined room: {options.RoomName}");
                break;
                
            case "quick-game":
                var foundRoom = await client.FindQuickGameAsync(options.GameId);
                Console.WriteLine($"Joined: {foundRoom}");
                break;
        }
        
        if (options.AutoLaunch)
        {
            Console.WriteLine("Waiting for launch signal...");
            var signal = await client.WaitForLaunchSignalAsync();
            
            Console.WriteLine($"Launching with params: {signal}");
            SpawnGameProcess(signal);
            
            await client.WaitForGameExitAsync();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}
```

**Usage:**
```bash
# From launcher that supports custom executables
TPOnlineClient.exe \
  --username=Player1 \
  --token=<auth_token> \
  --game=ID8 \
  --action=join \
  --room=MyRoom \
  --password=secret \
  --auto-launch \
  --wait

# Exit code: 0 = success, 1 = auth fail, 2 = room not found, 3 = game crashed
```

**Returns:** Exit code for launcher integration
- **0** = Game launched and exited normally
- **1** = Authentication failed
- **2** = Room not found or full
- **3** = Game crashed or hung
- **4** = Network error

---

## Part 4: External Launcher Integration Examples

### Launchbox / BigBox Integration

**Method 1: CLI Arguments (Phase 1)**

```
Game: Initial D 8 (TPO)
Emulator: TeknoParrot
Path to Application: C:\Games\TeknoParrot\TeknoParrotUi.exe

Command Line:
--tponline --game=ID8 --room=%ComputerName% --action=create --wait

Additional App:
(Same as above, but --action=quick-join for quick mode)
```

**Method 2: Profile Config (Phase 3)**

```
Game: Initial D 8 (TPO Quick)
Emulator: TeknoParrot
Path to Application: C:\Games\TeknoParrot\TeknoParrotUi.exe

Command Line:
--profile=id8_casual --wait
```

### MAME / Arcade Cabinets

**Script wrapper approach:**
```batch
@echo off
REM launch_tpo_game.bat
setlocal enabledelayedexpansion

set GAME=%1
set ROOM=%COMPUTERNAME%_%RANDOM%
set PASSWORD=%2

REM Map MAME game names to TPO game IDs
if "%GAME%"=="id8" set TPOGAME=ID8
if "%GAME%"=="sr3" set TPOGAME=SR3
if "%GAME%"=="quake" set TPOGAME=Quake

REM Launch TeknoParrot with parameters
start /wait TeknoParrotUi.exe ^
  --tponline ^
  --game=!TPOGAME! ^
  --room=%ROOM% ^
  --action=quick-join ^
  --wait

exit /b %ERRORLEVEL%
```

**MAME.ini mapping:**
```ini
[init]
teknoparrot "C:\launchers\launch_tpo_game.bat" "id8"
teknoparrot_sr3 "C:\launchers\launch_tpo_game.bat" "sr3"
```

### Hyperspin / GameHub

**Via XML config:**
```xml
<game name="Initial D 8">
  <description>Initial D Arcade Stage 8</description>
  <cloneof></cloneof>
  <year>2015</year>
  <manufacturer>Sega</manufacturer>
  <category>Racing</category>
  <image>initialD8.png</image>
  <executable>
    <game_executable>TeknoParrotUi.exe</game_executable>
    <additional_parameters>
      --tponline --game=ID8 --action=quick-join --wait
    </additional_parameters>
  </executable>
</game>
```

### Custom Launcher Script (PowerShell)

```powershell
# Launch-TPOGame.ps1
param(
    [string]$GameId = "ID8",
    [string]$RoomName = "$env:USERNAME-$(Get-Random -Max 9999)",
    [string]$Password = "",
    [switch]$QuickGame,
    [switch]$Wait
)

$tpoExe = "C:\Games\TeknoParrot\TeknoParrotUi.exe"
$action = $QuickGame ? "quick-join" : "join"

$args = @(
    "--tponline"
    "--game=$GameId"
    "--room=$RoomName"
    "--action=$action"
)

if ($Password) { $args += "--password=$Password" }
if ($Wait) { $args += "--wait" }

& $tpoExe @args

exit $LASTEXITCODE
```

**Usage:**
```powershell
# Quick join any ID8 room
.\Launch-TPOGame.ps1 -GameId ID8 -QuickGame -Wait

# Create private room
.\Launch-TPOGame.ps1 -GameId SR3 -RoomName MyRoom -Password Secret1 -Wait
```

---

## Part 5: Migration & Backward Compatibility

### Backward Compatibility Guarantee

**If no CLI args or config:**
```bash
TeknoParrotUi.exe
# → Shows UI as before
# → No TPO mode unless user navigates to Chat tab
```

**Existing browser-based workflow:**
```bash
# Still works 100%
# User manually creates/joins rooms
# No changes to experience
```

### Default Fallback

If `--tponline` args are invalid:
- Log warning to console/file
- Load normal UI
- User can manually retry

```csharp
try 
{
    config = ParseTPOArgs(args);
}
catch (Exception ex)
{
    Logger.LogWarning($"Invalid TPO args: {ex.Message}");
    config = new TPOConfig(); // Default (no automation)
}
```

---

## Part 6: File Structure Changes

### New Files to Create

```
TeknoParrotUi/
├── ViewModels/
│   └── TPOConfigManager.cs              (Phase 3)
│
├── Helpers/
│   └── TPOArgumentParser.cs             (Phase 1)
│   └── TPOConfig.cs                     (Phase 1)
│
└── Views/
    └── UserLogin.xaml.cs (MODIFIED)     (Phase 1)

TPOnlineClient/ (New Console App - Phase 4)
├── Program.cs
├── TeknoParrotOnlineClient.cs
└── CliArgumentParser.cs
```

### Configuration File

```
%APPDATA%/TeknoParrot/
└── TPOnline.json                        (Phase 3)
```

---

## Part 7: Server-Side Requirements

### New SignalR Methods (ChatHub.cs)

```csharp
// Phase 1: Minor changes (auto-populate existing methods)
// No new methods needed - use existing SendMessage

// Phase 2: Quick Game support
public async Task QuickGame(string gameName)
public async Task FindOrCreateQuickGameRoom(string gameName)

// Phase 4: Headless client support
public async Task AuthenticateClientAsync(string username, string token)
public async Task SetClientHeadlessMode(bool headless)
```

### Logging Additions

**For CLI mode, log to file:**
```csharp
// In ChatHub
private ILogger<ChatHub> _logger;

_logger.LogInformation($"[TPO-CLI] Room {room.Name} created by {user.UserName}");
_logger.LogInformation($"[TPO-CLI] Game launch signal sent to {room.Name}");
```

**Log location:**
```
%APPDATA%/TeknoParrot/Logs/TPOnline.log
```

---

## Part 8: Testing & Validation

### Unit Tests

```csharp
[TestFixture]
public class TPOArgumentParserTests
{
    [Test]
    public void ParseCreateCommand_ValidArgs_ReturnsCorrectConfig()
    {
        var args = new[] { "--tponline", "--game=ID8", "--room=Test", "--action=create" };
        var config = TPOArgumentParser.Parse(args);
        
        Assert.AreEqual("ID8", config.GameId);
        Assert.AreEqual("Test", config.RoomName);
        Assert.AreEqual("create", config.Action);
    }
    
    [Test]
    public void ParseJoinCommand_WithPassword_ReturnsPassword()
    {
        var args = new[] { "--tponline", "--game=SR3", "--room=Friends", 
                          "--password=secret", "--action=join" };
        var config = TPOArgumentParser.Parse(args);
        
        Assert.AreEqual("secret", config.Password);
    }
    
    [Test]
    public void ParseQuickGame_NoRoomArg_StaysEmpty()
    {
        var args = new[] { "--tponline", "--game=Quake", "--action=quick-join" };
        var config = TPOArgumentParser.Parse(args);
        
        Assert.IsEmpty(config.RoomName);
        Assert.AreEqual("quick-join", config.Action);
    }
}
```

### Integration Tests

```csharp
[TestFixture]
public class TPOClientIntegrationTests
{
    [Test]
    [Category("Integration")]
    public async Task CreateAndJoinRoom_FullFlow_GameLaunches()
    {
        // Host: Create room
        var hostClient = new TeknoParrotOnlineClient();
        await hostClient.AuthenticateAsync(testUser1);
        await hostClient.CreateRoomAsync("TestRoom", "ID8", "");
        
        // Guest: Join room
        var guestClient = new TeknoParrotOnlineClient();
        await guestClient.AuthenticateAsync(testUser2);
        await guestClient.JoinRoomAsync("TestRoom", "ID8", "");
        
        // Host: Launch game
        var launchSignal = await hostClient.LaunchGameAsync("TestRoom");
        
        Assert.IsNotNull(launchSignal);
        Assert.AreEqual(2, launchSignal.PlayerCount);
    }
}
```

### Manual Testing Checklist

- [ ] CLI args parsed correctly
- [ ] Room created with correct parameters
- [ ] Room joined successfully
- [ ] Quick Game finds existing room
- [ ] Quick Game creates new room if none exist
- [ ] Game launches with correct player ID
- [ ] Environment variable TP_TPONLINE2 contains correct data
- [ ] Config file loads and overrides CLI args
- [ ] Invalid args don't crash (fall back to UI)
- [ ] External launcher integration works (LaunchBox, MAME, etc.)
- [ ] Exit codes returned correctly (Phase 4)

---

## Part 9: Documentation for End Users

### CLI User Guide

**Quick Start:**

```bash
# Create public room
TeknoParrotUi.exe --tponline --game=ID8 --room=MyRoom --action=create

# Join existing room
TeknoParrotUi.exe --tponline --game=ID8 --room=MyRoom --action=join

# Auto-join any available room
TeknoParrotUi.exe --tponline --game=ID8 --action=quick-join
```

**For Launcher Users:**

Paste this into your launcher's command line field:
```
--tponline --game=%GAMEID% --room=%USERNAME% --action=create --wait
```

Replace `%GAMEID%` with:
- `ID8` = Initial D 8
- `SR3` = Sega Rally 3
- `Quake` = Quake Arcade
- `SRC` = Sega Racing Classic
- etc.

### Configuration Guide (Phase 3)

Create `~\AppData\Roaming\TeknoParrot\TPOnline.json`:

```json
{
  "profiles": {
    "my_game": {
      "gameId": "ID8",
      "roomNamePattern": "ID8Game",
      "password": null,
      "action": "quick-join",
      "autoLaunch": true,
      "minPlayers": 2
    }
  }
}
```

Then launch with:
```bash
TeknoParrotUi.exe --profile=my_game
```

---

## Part 10: Risk Assessment & Mitigation

### Risk 1: Authentication Token Exposure (Phase 4)
**Risk:** Passing auth tokens in command line visible in process list
**Mitigation:** 
- Use environment variable instead of argument
- Implement short-lived session tokens
- Never log full tokens

### Risk 2: Room Name Collisions
**Risk:** Two players create room with same name before sync
**Mitigation:**
- Server-side GUID appended invisibly
- Unique constraint in room lookup
- Existing: Already handled by ConcurrentDictionary

### Risk 3: Game Launch Timing
**Risk:** CLI caller gives up waiting; game launches late
**Mitigation:**
- Configurable timeout (default 30s)
- Return error code on timeout
- Launcher can retry

### Risk 4: Breaking UI Behavior
**Risk:** Auto-populate breaks manual room creation
**Mitigation:**
- Only activate if `--tponline` flag present
- Clear config after use
- Existing UI unchanged if no args

---

## Part 11: Success Metrics

### Adoption Goals

| Metric | Target | Timeline |
|--------|--------|----------|
| CLI invocations per day | 100+ | Month 2 |
| LaunchBox integration count | 50+ | Month 3 |
| Headless client downloads | 1000+ | Month 6 |
| Quick Game usage rate | 40% of UI sessions | Month 3 |

### Quality Metrics

| Metric | Target |
|--------|--------|
| Failed launch rate | < 5% |
| Average join time | < 5 seconds |
| Process exit code correctness | 99%+ |
| Documentation coverage | 100% |

---

## Summary & Recommendation

### Recommended Approach: **Hybrid Strategy (A + C + B)**

**Phase 1 (v1.0)**: Implement CLI Arguments
- **Effort:** 1-2 weeks
- **Impact:** 70% of external launcher use cases solved
- **User Value:** IMMEDIATE - Users can launch from launchers

**Phase 2 (v1.1)**: Add Quick Game Mode  
- **Effort:** 1-2 weeks
- **Impact:** Best UX for casual play
- **User Value:** SIGNIFICANT - One-click gaming

**Phase 3 (v1.2)**: Config File System
- **Effort:** 1 week
- **Impact:** Power user enablement
- **User Value:** MODERATE - Saves configuration

**Phase 4 (v2.0)**: Headless CLI Client
- **Effort:** 3-4 weeks
- **Impact:** Advanced integration scenarios
- **User Value:** NICE-TO-HAVE - Enterprise/tournament use

### Why This Order?

1. **Phase 1** requires minimal changes, delivers immediate value
2. **Phase 2** enhances Phase 1 with great UX
3. **Phase 3** builds on phases 1-2 for power users
4. **Phase 4** is optional, serves edge cases only

### Backward Compatibility
✅ **Fully maintained** - Zero breaking changes to existing UI workflows

### User Communication
- **Blog post** announcing CLI support
- **LaunchBox wiki** integration guide  
- **Discord guide** for quick-game feature
- **YouTube tutorial** for config file setup

---

## Conclusion

TeknoParrot Online can evolve from a browser-first experience to a **launcher-friendly, CLI-accessible platform** without breaking existing functionality. The phased approach allows users to benefit incrementally while minimizing implementation risk.

**By Phase 1**, casual players using external launchers will experience a dramatically improved workflow. **By Phase 2**, the best UX comes with one-click Quick Game. **By Phase 3**, power users have full control via configuration.

The architecture is sound; it's primarily a **UX integration challenge**, not a fundamental redesign.


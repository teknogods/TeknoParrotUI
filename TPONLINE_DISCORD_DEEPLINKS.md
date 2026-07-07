# TeknoParrot Online - Deep Linking & Discord Integration Plan

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
- **Web Fallback**: All deep links point to `teknoparrot.com:3333/join/...` (the TPOnline service on port 3333)
- **Discord Bot**: Deployed once, connects to the official `teknoparrot.com:3333` service

There is no need for server selection in deep links because there is only one official TPOnline server (on port 3333).

---

**Document Purpose**: Design specifications for enabling sharable links that allow players to join TPOnline lobbies directly from Discord, web browsers, and other platforms.

**User Problem Statement**:
> "Player A is waiting for a quick match. Player B sees a Discord message: 'Player A is waiting in ID8 lobby' with a clickable link. Player B clicks it → TeknoParrotUI opens → Auto-joins Player A's room → Game starts."

Currently this is impossible. This document enables it.

---

## Overview: Why Deep Linking Matters

### Current Experience
```
Player A (Host): "Anyone want to play ID8?"
Player B (Viewer): Manually:
  1. Open TeknoParrot
  2. Click TPO Chat tab
  3. Wait for browser to load
  4. Select ID8 from game dropdown
  5. Search room list for Player A's room
  6. Click "Join"
  7. Enter password (if any)
  → Click "Launch"
  
Time to play: 2-3 minutes
Friction: EXTREME
```

### Improved Experience (With Deep Links)
```
Player A (Host): Posts Discord message with link
Player B (Viewer): Clicks link
  → TeknoParrotUI opens directly in room
  → Auto-joins with one click
  → Waits for host to launch
  
Time to play: 15-30 seconds
Friction: MINIMAL
```

---

## Part 1: Deep Linking Architecture

### Option A: Custom Protocol Handler (tponline://)

**URL Format:**
```
tponline://action?param1=value1&param2=value2

Examples:
tponline://join?room=MyRoom&game=ID8
tponline://join?room=MyRoom&game=ID8&password=secret123
tponline://quick-game?game=SR3
tponline://spectate?room=Tournament&game=Quake
tponline://profile?player=Player1
```

**Windows Protocol Registration (Registry):**
```
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.tponline\UserChoice
HKEY_CLASSES_ROOT\tponline\
  → (Default) = "URL:TeknoParrot Online Protocol"
  → URL Protocol = "" (empty)
  → shell\open\command = "C:\Games\TeknoParrot\TeknoParrotUi.exe --link=%1"
```

**Implementation in TeknoParrotUi.exe:**
```csharp
// MainWindow.xaml.cs
public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        
        string[] args = Environment.GetCommandLineArgs();
        
        // Check for deep link
        var linkArg = args.FirstOrDefault(a => a.StartsWith("--link="));
        if (linkArg != null)
        {
            string url = linkArg.Substring("--link=".Length);
            HandleDeepLink(Uri.UnescapeDataString(url));
        }
        else if (args.Contains("--tponline"))
        {
            // CLI args (from Phase 1)
            HandleCLIArgs(args);
        }
    }
    
    private void HandleDeepLink(string link)
    {
        // Parse tponline://action?param=value&...
        var uri = new Uri(link);
        string action = uri.Host; // "join", "quick-game", etc.
        var query = HttpUtility.ParseQueryString(uri.Query);
        
        TPOConfig.Action = action;
        TPOConfig.GameId = query["game"];
        TPOConfig.RoomName = query["room"];
        TPOConfig.Password = query["password"];
        TPOConfig.SpectatorMode = query.Get("mode") == "spectate";
        
        // Show TPO tab and auto-launch
        TPOTabItem.IsSelected = true;
    }
}
```

**Advantages:**
- ✅ Native Windows integration
- ✅ Works offline (no server lookup)
- ✅ Fast (direct invocation)
- ✅ Standard protocol (like `steam://`, `discord://`)
- ✅ Can be embedded in Discord messages

**Disadvantages:**
- ❌ Windows-only (no mobile)
- ❌ Need to register on first install
- ❌ User sees "Open with..." dialog first time
- ❌ Requires local installation

---

### Option B: Web-Based Links (HTTPS)

**URL Format:**
```
https://teknoparrot.com/join/{roomId}
https://teknoparrot.com/quick-game?game=ID8
https://teknoparrot.com/invite/{token}

Parameters:
- token: Encoded room + auth info
- room: Room name (human-readable)
- game: Game ID
- password: Room password (encrypted in URL)
```

**Server-Side (ASP.NET Core):**
```csharp
// Controllers/LinkController.cs
[ApiController]
[Route("api/[controller]")]
public class LinkController : ControllerBase
{
    private readonly UserManager<TeknoParrotUser> _userManager;
    private readonly ChatHub _chatHub;
    
    [HttpGet("{roomId}")]
    public async Task<IActionResult> JoinRoom(string roomId)
    {
        // Validate room exists
        var room = UserHandler.ConnectionsOnRooms.Values
            .FirstOrDefault(r => r.Name == roomId);
        
        if (room == null)
            return NotFound("Room not found");
        
        // Generate a join token (JWT with room info)
        var token = GenerateJoinToken(room);
        
        // Redirect to app with deep link or fallback to web
        return Redirect($"tponline://join?room={roomId}&game={room.GameName}&token={token}");
    }
    
    [HttpGet("quick-game/{gameId}")]
    public async Task<IActionResult> QuickGame(string gameId)
    {
        // Validate game exists
        var game = GameProfiles.GameProfileList
            .FirstOrDefault(g => g.GameXmlName == gameId);
        
        if (game == null)
            return BadRequest("Game not found");
        
        return Redirect($"tponline://quick-game?game={gameId}");
    }
}
```

**Advantages:**
- ✅ Platform-agnostic (works on mobile)
- ✅ Server can validate room before redirect
- ✅ Can fall back to web UI if app not installed
- ✅ Analytics/tracking possible
- ✅ URL shareable on all platforms

**Disadvantages:**
- ❌ Requires redirect/hop
- ❌ More server processing
- ❌ Mobile needs custom app (or PWA)

---

### Option C: Hybrid (Recommended)

**Both protocols, server-side verification:**

```
Discord Bot Posts:
┌─────────────────────────────────────┐
│ 🎮 Room: MyRoom - Initial D 8       │
│ Players: 1/2 👥                     │
│ Host: Player1                       │
│                                     │
│ [🔗 Join Room]                      │
│ (Click to join)                     │
└─────────────────────────────────────┘

On Click:
├─ If desktop: tponline://join?room=MyRoom&game=ID8
├─ If mobile: https://teknoparrot.com/join/MyRoom?game=ID8
└─ If web: Open web UI with room pre-selected
```

**Implementation:**
```csharp
// Utility to generate both link types
public class LinkGenerator
{
    public static string GenerateAppLink(TpRoom room)
    {
        return $"tponline://join?room={room.Name}&game={room.GameName}";
    }
    
    public static string GenerateWebLink(TpRoom room)
    {
        return $"https://teknoparrot.com/invite/{EncodeRoom(room)}";
    }
    
    public static string GenerateDiscordButton(TpRoom room)
    {
        // Return markdown or embed that Discord understands
        return $"[Join {room.Name}]({GenerateWebLink(room)})";
    }
}
```

---

## Part 2: Discord Integration

### Discord Bot Features

**Use Case 1: Manual Share Command**
```
User: !tpo share
Bot: Posts embed with join link for user's current room
```

**Use Case 2: Auto-Post on Room Create**
```
Server logic:
1. Room created on TPO server
2. Bot receives webhook notification
3. Bot posts #tponline channel with room info
4. Auto-updates player count
5. Deletes when room closes
```

**Use Case 3: Interactive Buttons**
```
┌──────────────────────────────┐
│ 🎮 Waiting for Players       │
│ Room: Tournament             │
│ Game: Quake Arcade           │
│ Players: 1/4                 │
│                              │
│ [✅ Join] [👁 Spectate]      │
│ (Click buttons to act)       │
└──────────────────────────────┘
```

### Discord Bot Implementation

**Discord.Net C# Bot:**

```csharp
// DiscordBot/Modules/TPOModule.cs
public class TPOModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("share", "Share your current room on Discord")]
    public async Task ShareRoom()
    {
        // Get user's current room
        var user = Context.User;
        var tpUser = GetTPOUser(user.Id);
        
        if (tpUser?.CurrentRoom == null)
        {
            await RespondAsync("You're not in a TPO room!", ephemeral: true);
            return;
        }
        
        var room = tpUser.CurrentRoom;
        
        var embed = new EmbedBuilder()
            .WithTitle($"🎮 {room.FullGameName}")
            .WithDescription($"**Room:** {room.Name}")
            .AddField("Players", $"{room.Users.Count}/{room.MaxPlayers}", inline: true)
            .AddField("Host", room.Users.First(u => u.PlayerId == 0).User.UserName, inline: true)
            .WithColor(Color.Blue)
            .WithThumbnailUrl(GetGameThumbnail(room.GameName))
            .WithTimestamp(DateTime.UtcNow)
            .WithFooter("Click button below to join!")
            .Build();
        
        var button = new ButtonBuilder()
            .WithLabel($"Join {room.Name}")
            .WithStyle(ButtonStyle.Link)
            .WithUrl($"https://teknoparrot.com/join/{room.Name}?game={room.GameName}")
            .Build();
        
        var component = new ComponentBuilder()
            .WithButton(button)
            .Build();
        
        await RespondAsync(embed: embed, components: component);
    }
    
    [SlashCommand("waiting", "Show all rooms waiting for players")]
    public async Task ShowWaitingRooms()
    {
        var waitingRooms = UserHandler.ConnectionsOnRooms.Values
            .Where(r => !r.IsGameLaunched && r.Users.Count < r.MaxPlayers)
            .OrderBy(r => r.Users.Count)
            .ToList();
        
        if (waitingRooms.Count == 0)
        {
            await RespondAsync("No rooms waiting for players right now.", ephemeral: true);
            return;
        }
        
        var embed = new EmbedBuilder()
            .WithTitle("⏳ Rooms Waiting for Players")
            .WithColor(Color.Green);
        
        foreach (var room in waitingRooms.Take(10))
        {
            embed.AddField(
                $"{room.FullGameName}",
                $"Room: **{room.Name}** | Players: {room.Users.Count}/{room.MaxPlayers}\n" +
                $"[Click to join](https://teknoparrot.com/join/{room.Name}?game={room.GameName})",
                inline: false
            );
        }
        
        await RespondAsync(embed: embed.Build());
    }
    
    [SlashCommand("quick-match", "Find or create a quick match")]
    public async Task QuickMatch(string gameId)
    {
        var game = GameProfiles.GameProfileList
            .FirstOrDefault(g => g.GameXmlName == gameId);
        
        if (game == null)
        {
            await RespondAsync($"Game '{gameId}' not found.", ephemeral: true);
            return;
        }
        
        var button = new ButtonBuilder()
            .WithLabel($"🚀 Quick Match: {game.Name}")
            .WithStyle(ButtonStyle.Link)
            .WithUrl($"https://teknoparrot.com/quick-game?game={gameId}")
            .Build();
        
        var component = new ComponentBuilder()
            .WithButton(button)
            .Build();
        
        await RespondAsync(components: component);
    }
}
```

### Webhook-Based Auto-Posting

**Server posts to Discord webhook when room created:**

```csharp
// ChatHub.cs - In CreateRoom method
private async Task NotifyDiscordRoomCreated(TpRoom room, TeknoParrotUser host)
{
    using var client = new HttpClient();
    
    var embed = new
    {
        title = room.FullGameName,
        description = $"Room: **{room.Name}**",
        color = 3447003, // Blurple
        fields = new object[]
        {
            new { name = "Host", value = host.UserName, inline = true },
            new { name = "Players", value = $"1/{room.MaxPlayers}", inline = true },
            new { name = "Password", value = string.IsNullOrEmpty(room.Password) ? "None" : "Yes (Protected)", inline = true }
        },
        thumbnail = new { url = GetGameThumbnailUrl(room.GameName) },
        footer = new { text = "Click button below to join this room" }
    };
    
    var payload = new
    {
        username = "TeknoParrot Online",
        avatar_url = "https://teknoparrot.com/img/icon.png",
        embeds = new object[] { embed },
        components = new object[]
        {
            new
            {
                type = 1, // Action row
                components = new object[]
                {
                    new
                    {
                        type = 2, // Button
                        label = "Join Room",
                        style = 5, // Link button
                        url = $"https://teknoparrot.com/join/{room.Name}?game={room.GameName}"
                    }
                }
            }
        }
    };
    
    await client.PostAsJsonAsync(
        Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL"),
        payload
    );
}
```

**Discord Webhook Environment Variable:**
```
DISCORD_WEBHOOK_URL=https://discordapp.com/api/webhooks/...
```

---

## Part 3: Link URL Structure

### Scheme 1: Direct Join

```
tponline://join
  ?room=RoomName        [required]
  &game=GameId          [required]
  &password=secret      [optional]
  &player=PlayerName    [optional, auto-filled with username]
  &token=JWT            [optional, for auth verification]

Example:
tponline://join?room=MyRoom&game=ID8&password=secret123
```

### Scheme 2: Quick Game

```
tponline://quick-game
  ?game=GameId          [required]
  &token=JWT            [optional]

Example:
tponline://quick-game?game=SR3
```

### Scheme 3: Spectate (Future)

```
tponline://spectate
  ?room=RoomName        [required]
  &game=GameId          [required]

Example:
tponline://spectate?room=MyRoom&game=Quake
```

### Scheme 4: Profile

```
tponline://profile
  ?player=PlayerName    [required]

Example:
tponline://profile?player=Player1
```

### Web Fallback

```
https://teknoparrot.com/join/{roomName}?game={gameId}
https://teknoparrot.com/quick-game?game={gameId}
https://teknoparrot.com/invite/{encodedToken}
```

---

## Part 4: Security Considerations

### Token-Based Authentication

**Generate short-lived JWT for links:**

```csharp
public class JwtLinkToken
{
    public string RoomName { get; set; }
    public string GameId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string HostId { get; set; }  // Room creator's user ID
    
    // Optional: Password hash (encrypted link contains password)
    public string PasswordHash { get; set; }
}

public static string GenerateLinkToken(TpRoom room)
{
    var claims = new[]
    {
        new Claim("room", room.Name),
        new Claim("game", room.GameName),
        new Claim("host", room.Users.First(u => u.PlayerId == 0).User.Id),
        new Claim("exp", DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds().ToString())
    };
    
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    
    var token = new JwtSecurityToken(
        issuer: "teknoparrot.com",
        audience: "tponline",
        claims: claims,
        expires: DateTime.UtcNow.AddHours(2),
        signingCredentials: creds
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}

public static bool ValidateLinkToken(string token, out JwtLinkToken linkToken)
{
    try
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        
        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = true,
            ValidIssuer = "teknoparrot.com",
            ValidateAudience = true,
            ValidAudience = "tponline"
        }, out SecurityToken validatedToken);
        
        var jwtToken = (JwtSecurityToken)validatedToken;
        
        linkToken = new JwtLinkToken
        {
            RoomName = jwtToken.Claims.FirstOrDefault(c => c.Type == "room")?.Value,
            GameId = jwtToken.Claims.FirstOrDefault(c => c.Type == "game")?.Value,
            ExpiresAt = jwtToken.ValidTo,
            HostId = jwtToken.Claims.FirstOrDefault(c => c.Type == "host")?.Value
        };
        
        return true;
    }
    catch
    {
        linkToken = null;
        return false;
    }
}
```

### Password Protection

**Encrypted passwords in links:**
```csharp
// Encrypt room password for link
public static string EncryptPasswordForLink(string password)
{
    var encryptedBytes = ProtectedData.Protect(
        Encoding.UTF8.GetBytes(password),
        null,
        DataProtectionScope.CurrentUser
    );
    
    return Convert.ToBase64String(encryptedBytes);
}

// Decrypt when joining via link
public static string DecryptPasswordFromLink(string encrypted)
{
    var encryptedBytes = Convert.FromBase64String(encrypted);
    var decryptedBytes = ProtectedData.Unprotect(
        encryptedBytes,
        null,
        DataProtectionScope.CurrentUser
    );
    
    return Encoding.UTF8.GetString(decryptedBytes);
}
```

### Security Rules

1. **Token Expiration**: Links expire after 2 hours
2. **Room Validation**: Token must match actual room on server
3. **Rate Limiting**: Max 10 links generated per user per hour
4. **Revocation**: Host can revoke room link at any time
5. **Password Hashing**: Passwords encrypted in links, never plain-text
6. **HTTPS Only**: Web links must use HTTPS

---

## Part 5: Implementation Phases

### Phase 1: Basic Deep Linking (v1.3) - **Weeks 1-2**

**Windows Protocol Handler Only**

```csharp
// MainWindow.xaml.cs - Add deep link handler
private void HandleDeepLink(string link)
{
    var uri = new Uri(link);
    string action = uri.Host;
    var query = HttpUtility.ParseQueryString(uri.Query);
    
    TPOConfig.GameId = query["game"];
    TPOConfig.RoomName = query["room"];
    TPOConfig.Password = query["password"];
    TPOConfig.IsDeepLink = true;
    
    TPOTabItem.IsSelected = true;
}
```

**Discord-ready, but manual sharing**
- Users can manually create links
- Share via Discord manually
- Links work when clicked on Windows PC

### Phase 2: Discord Bot Commands (v1.4) - **Weeks 3-4**

```
/share - Posts current room as embed with link
/waiting - Shows all rooms waiting for players
/quick-match <game> - Quick match button
```

**Features:**
- ✅ Discord bot slash commands
- ✅ Interactive join buttons
- ✅ Room status embeds
- ✅ Requires manual invocation

### Phase 3: Web Fallback & Auto-Posting (v1.5) - **Weeks 5-6**

**Server-side:**
- Web fallback page (https://teknoparrot.com/join/...)
- Webhook auto-posting on room create
- Room status updates in Discord

**Features:**
- ✅ Mobile-friendly join links
- ✅ Auto-post rooms to Discord
- ✅ Live player count updates
- ✅ Web UI with pre-filled room

### Phase 4: Advanced Features (v2.0) - **Optional**

```
- Spectator mode links
- Invite codes (share without exposing password)
- Analytics (track which links are clicked)
- Leaderboard embeds
- Tournament bracket links
```

---

## Part 6: User Experience Flows

### Discord User Perspective (Phase 1)

```
1️⃣ Player A is in TPO room "TestID8"
2️⃣ Clicks "Share" button in UI (auto-generates link)
3️⃣ Link copied to clipboard: tponline://join?room=TestID8&game=ID8
4️⃣ Pastes in Discord: "Join my room! tponline://join?room=TestID8&game=ID8"
5️⃣ Player B clicks link
6️⃣ TeknoParrotUI opens, auto-navigates to TestID8 room
7️⃣ Auto-joins (if no password) or prompts for password
8️⃣ Game launches when host clicks Launch
```

### Discord with Bot (Phase 2)

```
1️⃣ Player A in TPO room "TestID8"
2️⃣ Clicks "Share on Discord" button
3️⃣ UI shows: "Choose Discord channel..."
4️⃣ Selects #gaming
5️⃣ Bot posts embed with join button
6️⃣ Player B clicks [Join Room] button
7️⃣ Redirected to tponline://join link
8️⃣ TeknoParrotUI opens, auto-joins
```

### Mobile User (Phase 3)

```
1️⃣ Player A shares Discord link from mobile
2️⃣ Player B on mobile clicks link
3️⃣ Opens browser: https://teknoparrot.com/join/TestID8?game=ID8
4️⃣ Web page shows room info
5️⃣ Button: [Open in App] (if installed) or [Download App]
6️⃣ If installed: Opens app, auto-joins
7️⃣ If not installed: Shows web UI to enter game code
```

---

## Part 7: Share Button in UI

### New UI Component (UserLogin.xaml)

```html
<!-- In room view, add share section -->
<div class="share-section">
    <div class="share-buttons">
        <button id="copyLinkButton" class="btn btn-info">
            📋 Copy Room Link
        </button>
        <button id="shareDiscordButton" class="btn btn-purple">
            🔗 Share to Discord
        </button>
        <button id="shareInviteButton" class="btn btn-secondary">
            📧 Invite Friends
        </button>
    </div>
    
    <div id="linkCopyStatus" style="display:none;">
        ✅ Link copied! Share it now!
    </div>
</div>
```

### JavaScript Implementation

```javascript
document.getElementById("copyLinkButton").addEventListener("click", function() {
    const roomName = document.getElementById("currentRoom").value;
    const gameId = currentGameId;
    const password = getUserRoomPassword();
    
    let link = `tponline://join?room=${roomName}&game=${gameId}`;
    if (password) link += `&password=${password}`;
    
    navigator.clipboard.writeText(link).then(() => {
        showCopyStatus("Link copied! Share on Discord!");
    });
});

document.getElementById("shareDiscordButton").addEventListener("click", function() {
    const roomName = document.getElementById("currentRoom").value;
    const gameId = currentGameId;
    const playerCount = document.getElementById("currentPlayers").value;
    
    const link = `tponline://join?room=${roomName}&game=${gameId}`;
    
    // Open Discord with pre-filled message
    const discordUrl = `discord://channels/@me`;
    window.open(discordUrl);
    
    // Also copy to clipboard as fallback
    navigator.clipboard.writeText(
        `🎮 Join my room!\n${link}\n\nPlayers: ${playerCount}`
    );
};
```

---

## Part 8: Windows Protocol Registration

### Auto-Registration on First Install

```csharp
// Installer or MainWindow startup
public static void RegisterProtocol()
{
    try
    {
        string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
        
        using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\tponline"))
        {
            key.SetValue("", "URL:TeknoParrot Online Protocol");
            key.SetValue("URL Protocol", "");
            
            using (var cmdKey = key.CreateSubKey("shell\\open\\command"))
            {
                cmdKey.SetValue("", $"\"{appPath}\" --link=%1");
            }
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"Failed to register tponline:// protocol: {ex.Message}");
    }
}
```

**Call in MainWindow constructor:**
```csharp
public MainWindow()
{
    InitializeComponent();
    
    // Register protocol on first run or each start
    if (!IsProtocolRegistered())
    {
        RegisterProtocol();
    }
    
    // Handle link
    var args = Environment.GetCommandLineArgs();
    var linkArg = args.FirstOrDefault(a => a.StartsWith("tponline://"));
    if (linkArg != null)
    {
        HandleDeepLink(linkArg);
    }
}
```

---

## Part 9: Example Discord Bot Setup

### Prerequisites

```bash
# Install Discord.Net
dotnet add package Discord.Net
dotnet add package Discord.Net.WebSocket
```

### Bot Configuration

```json
// appsettings.json
{
  "discord": {
    "token": "YOUR_BOT_TOKEN",
    "prefix": "!",
    "webhook_url": "https://discordapp.com/api/webhooks/...",
    "channel_id": "123456789"
  }
}
```

### Full Bot Startup

```csharp
// Program.cs
var client = new DiscordSocketClient();
var interactionService = new InteractionService(client);

client.Log += LogAsync;
interactionService.Log += LogAsync;

client.Ready += async () =>
{
    await interactionService.RegisterCommandsGloballyAsync();
    Console.WriteLine("Bot is ready!");
};

client.InteractionCreated += async interaction =>
{
    var ctx = new SocketInteractionContext(client, interaction);
    await interactionService.ExecuteCommandAsync(ctx, null);
};

await client.LoginAsync(TokenType.Bot, botToken);
await client.StartAsync();

await Task.Delay(Timeout.Infinite);
```

---

## Part 10: Testing Checklist

### Unit Tests

```csharp
[TestFixture]
public class DeepLinkTests
{
    [Test]
    public void ParseJoinLink_ValidUrl_ExtractsCorrectly()
    {
        string link = "tponline://join?room=TestRoom&game=ID8&password=secret";
        var config = DeepLinkParser.Parse(link);
        
        Assert.AreEqual("TestRoom", config.RoomName);
        Assert.AreEqual("ID8", config.GameId);
        Assert.AreEqual("secret", config.Password);
    }
    
    [Test]
    public void GenerateLinkToken_ValidRoom_CreatesToken()
    {
        var room = CreateTestRoom();
        var token = LinkTokenGenerator.Generate(room);
        
        Assert.IsNotEmpty(token);
        Assert.IsTrue(LinkTokenValidator.Validate(token));
    }
    
    [Test]
    public void LinkToken_Expired_FailsValidation()
    {
        var token = GenerateExpiredToken();
        
        Assert.IsFalse(LinkTokenValidator.Validate(token));
    }
}
```

### Integration Tests

```csharp
[TestFixture]
[Category("Integration")]
public class DiscordBotIntegrationTests
{
    [Test]
    public async Task ShareCommand_InValidRoom_PostsEmbed()
    {
        var bot = new TestDiscordBot();
        var user = CreateTestUser();
        var room = CreateTestRoom(user);
        
        var result = await bot.ExecuteCommand("share", user);
        
        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.EmbedPosted);
        Assert.IsTrue(result.Embed.Contains("TestRoom"));
    }
}
```

### Manual Testing Checklist

- [ ] Protocol registers on install
- [ ] Deep link opens TeknoParrotUI
- [ ] Room and game auto-populate
- [ ] Room validation works (non-existent room)
- [ ] Password-protected rooms prompt for password
- [ ] Token expiration prevents joining old links
- [ ] Discord bot commands work
- [ ] Embeds display correctly in Discord
- [ ] Join buttons work
- [ ] Web fallback page displays
- [ ] Mobile deep links handle gracefully
- [ ] Discord auto-posting webhook works
- [ ] Link expires after 2 hours
- [ ] Share button copies to clipboard
- [ ] Share to Discord opens Discord

---

## Part 11: Rollout Strategy

### Phase 1 (v1.3): Internal Testing
- Alpha testers in Discord server
- Test protocol registration
- Test link generation
- Gather feedback

### Phase 2 (v1.4): Discord Bot Beta
- Deploy bot to test server
- Users test /share, /waiting commands
- Refine embed design
- Optimize button behavior

### Phase 3 (v1.5): Public Release
- Announce feature in Discord
- Publish blog post
- Create tutorial video
- Share examples in #gaming channel

---

## Part 12: Discord Bot Hosting

### Option A: Self-Hosted (On Same Server as TPO)

```
TPOnlineService
├── Existing ASP.NET Core app
└── Add Discord.Net library
    ├── Bot token in appsettings
    ├── Slash commands registered
    └── Listens for interactions
```

**Pros:** ✅ Simple, single deployment
**Cons:** ❌ ASP.NET Core app used for two things

### Option B: Separate Bot Service

```
Separate Console App (.NET)
├── Discord.Net client
├── Webhooks to TPOnlineService
├── Separate deployment
└── Can scale independently
```

**Pros:** ✅ Cleaner separation, scales independently
**Cons:** ❌ Two apps to maintain

### Option C: Cloud-Hosted (Recommended)

```
Azure Functions / AWS Lambda
├── Serverless Discord bot
├── Only pay for invocations
├── Auto-scales
└── Low maintenance
```

**Pros:** ✅ Cheap, scales automatically, no servers
**Cons:** ❌ Cold start latency

---

## Part 13: Success Metrics

### Adoption Goals

| Metric | Target | Timeline |
|--------|--------|----------|
| Links shared per day | 50+ | Month 2 |
| Links clicked per day | 100+ | Month 2 |
| Discord bot joins | 100+ servers | Month 3 |
| Room creation increase | +30% | Month 2 |
| Average time to play | -50% (2-3 min → ~30 sec) | Month 2 |

### Quality Metrics

| Metric | Target |
|--------|--------|
| Link validity | 99%+ work on click |
| Protocol registration success | 99%+ |
| Discord bot uptime | 99.9% |
| Token validation accuracy | 100% |
| Mobile fallback success | 95%+ |

---

## Part 14: Example Discord Embeds

### Embed 1: Simple Room Share

```
┌────────────────────────────────┐
│ 🎮 Initial D Arcade Stage 8    │
│                                │
│ Room: MyRoom                   │
│ Players: 1/2 👥               │
│ Host: Player1                  │
│                                │
│ [Join Room]                    │
│ (Click to join)                │
└────────────────────────────────┘
```

### Embed 2: Tournament Bracket

```
┌────────────────────────────────┐
│ 🏆 Quake Tournament             │
│                                │
│ Room: Tournament               │
│ Players: 3/4 👥               │
│ Spots Left: 1                  │
│                                │
│ Waiting for 4th player...      │
│ [Join Tournament] [Spectate]   │
└────────────────────────────────┘
```

### Embed 3: Quick Match

```
┌────────────────────────────────┐
│ ⚡ Quick Match Available        │
│                                │
│ Game: Sega Rally 3             │
│ Rooms Waiting: 3               │
│                                │
│ [🚀 Quick Match]               │
│ Find a game instantly!         │
└────────────────────────────────┘
```

---

## Conclusion

**Deep linking + Discord integration transforms TPO from browser-only to socially-shareable.**

### Key Benefits:

✅ **Frictionless sharing**: Copy/paste link, instant room access
✅ **Discord-native**: Room info in Discord, one click to play
✅ **Cross-platform**: Works on desktop, mobile, web
✅ **Social discovery**: Public rooms visible in Discord channel
✅ **Discord bot**: Slash commands for power users
✅ **Backward compatible**: Existing UI unchanged

### Recommended Implementation:

**Phase 1 (v1.3)**: Basic deep linking (1-2 weeks)
→ Users can share links manually via Discord

**Phase 2 (v1.4)**: Discord bot commands (1-2 weeks)  
→ /share, /waiting, /quick-match slash commands

**Phase 3 (v1.5)**: Auto-posting & web fallback (1-2 weeks)
→ Automatic room posts, mobile support

**Total Timeline**: 4-6 weeks for full implementation

This is the **most impactful feature** for onboarding new players and increasing session frequency.


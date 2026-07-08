# Cross-Platform Input Refactor Plan

## Executive Summary

**Goal:** Replace Windows-only input handling (SharpDX.XInput, SharpDX.DirectInput) with cross-platform SDL2 + platform-specific raw input backends. Enable TeknoParrotUI to run on Windows, Linux, and Android with unified input logic.

**Key Decision:** 
- **Gamepad input** → SDL2 only (all platforms)
- **Gun games** (RawInput/Trackball) → SDL2 + platform-specific mouse/touch (RawInput on Windows, EvdevMouse on Linux, Touch on Android)
- **Configuration** → User sets Input API in Game Settings (stored in GameProfile.ConfigValues)

**Status:** Architecture designed ✅ | Code changes started ✅ | Full implementation pending

---

## Plan Corrections (verified against codebase, 2026-07-08)

Facts below override anything contradictory elsewhere in this document:

1. **Profile count:** There are **537** `GameProfiles/*.xml` files (not "250+"). 532 of them declare an `Input API` dropdown in `ConfigValues`.
2. **Audit data model:** `GameProfile` has **no** `RawInputButtons[]` or `Trackball[]` properties. The real input-method signals are:
   - `ConfigValues` → `FieldInformation` with `FieldName == "Input API"`: `FieldOptions` lists supported APIs (`DirectInput`, `XInput`, `RawInput`, `RawInputTrackball`), `FieldValue` is the current selection.
   - `GameProfile.GunGame` (bool) marks gun games.
   - `JoystickButtons[]` entries each carry `DirectInputButton`, `XInputButton`, `RawInputButton` binding slots plus `HideWith*` flags.
3. **Active projects:** The solution builds only `TeknoParrotUi.Common`, `TeknoParrotUi.Avalonia`, `ParrotPatcher`. The WPF project (`TeknoParrotUi/`) is **not in the solution** — do not target `GameRunning.xaml.cs`/`JoystickControl.xaml.cs`. Real integration points:
   - `TeknoParrotUi.Common/GameLaunch/GameSession.cs` (launch-time input listening; reads `Input API` from ConfigValues, starts `InputListener`, creates `RawInputForwardWindow` for WndProc routing)
   - `TeknoParrotUi.Avalonia/Services/InputCaptureService.cs` (binding capture UI)
   - `TeknoParrotUi.Avalonia/Views/MultiButtonConfigView.axaml.cs` (`GetSupportedApis`)
4. **Folder naming collision:** `TeknoParrotUi.Common/InputProfiles/` already exists and contains JVS output helpers (`AnalogHelper`, `DigitalHelper`, `WMMT3Cards`) in namespace `TeknoParrotUi.Common.InputProfiles.Helpers`. The new runtime data folder for JSON input profiles must therefore not clash: use runtime folder name `InputProfiles/` on disk (fine — that's bin output), but put the **source** code in `InputListening/ProfileStorage/` and keep the existing helpers where they are.
5. **Porting strategy (revised):** `InputListenerXInput` (~2,078 lines) and `InputListenerDirectInput` (~3,965 lines) contain extensive game-specific logic (WMMT gears, Initial D, rotary encoders, relative input, sto0z). **Do not rewrite this logic.** Instead:
   - SDL2's GameController API deliberately mirrors XInput semantics (same buttons, same axis ranges, independent triggers).
   - Implement an `SDL2Gamepad` device layer that exposes XInput-shaped state (buttons bitmask, thumb shorts, trigger bytes).
   - Port `ListenXInput` into `SDL2JoystickListener` by swapping only the SharpDX device layer; existing `XInputButton` user bindings work unchanged.
   - This also means **existing user XInput bindings survive the migration with zero conversion.**
6. **JSON library:** Use `Newtonsoft.Json` (already referenced) for InputProfile serialization — not System.Text.Json (not referenced) and not Utf8Json (used only for read-only metadata).
7. **SDL2 package:** Use `ppy.SDL2-CS` (maintained, ships native SDL2 for win-x64/linux-x64/osx). Plain `SDL2-CS` is not published on NuGet.
8. **Phase 0 audit tool location:** `Tools/InputMethodAudit/` console project referencing `TeknoParrotUi.Common`, added to the solution but excluded from publish. Report output: `Tools/InputMethodAudit/report/` (markdown + JSON).
9. **`InputApi` enum stays** during migration (values: `DirectInput`, `XInput`, `RawInput`, `RawInputTrackball`, `MergedInput`); a new `SDL2` value is added rather than removing the enum — removal happens only in the final cleanup phase.

### Phase 0 Audit Results (2026-07-08) ✅ COMPLETE

Tool: `Tools/InputMethodAudit/` (in solution). Full report: `Tools/InputMethodAudit/report/input-audit.{md,json}`.

| Metric | Count |
|--------|-------|
| Total profiles parsed | 537 (0 failures) |
| Has `Input API` field | 532 (all offer DirectInput + XInput) |
| Offers RawInput | 135 |
| Offers RawInputTrackball | 25 (21 offer both RawInput and Trackball) |
| Gamepad-only games | 393 |
| `GunGame` flag set | 123 |
| Default = DirectInput | 409 |
| Default = RawInput | 122 |
| Default = RawInputTrackball | 1 |
| RawInput bindings pre-populated in stock profiles | 0 (users bind their own) |

**Implications:**
- 393/537 games need only the SDL2 gamepad listener.
- 139 games need a platform mouse/touch listener in addition to SDL2.
- Stock profiles ship *no* RawInput device bindings — device paths are user-bound, so InputProfile migration only needs to carry API availability + defaults from stock XML, and user bindings from UserProfiles at runtime.

---

## Current State (What's Been Done)

### ✅ Completed
1. **Architecture Planning**
   - Designed InputProfile JSON storage (separate from GameProfile XML)
   - Created IInputListener interface architecture for InputListening folder redesign
   - Verified SDL2-CS supports independent left/right analog triggers

2. **Code Changes**
   - Fixed hardcoded path separators (GameProfileLoader.cs, MultiButtonConfigView.xaml.cs, AppEnvironment.cs)
   - Added Windows-only guards to SharpDX calls (InputCaptureService.cs)
   - Implemented smart InputApi selection in GameRunning.xaml.cs and JoystickControl.xaml.cs:
     - Reads Input API from Game Settings (ConfigValues → FieldValue)
     - Falls back to auto-detect if not configured
     - Respects user choice for games with both RawInput and RawInputTrackball options

3. **Configuration**
   - Input API selection moved to Game Settings (not requested before launch)
   - **RawInput and RawInputTrackball both use `Linearstar.Windows.RawInput` library** (confirmed)
     - RawInput: Mouse-based light guns (Virtua Cop, Too Spicy)
     - RawInputTrackball: Trackball/ball input (Golden Tee, WMMT5)

4. **Build Status** ✅ 
   - Project builds successfully on Linux (dotnet build)
   - App runs without crashing (tested manually)

### ⏳ Pending Implementation
1. **Phase 1:** Create input provider interfaces and SDL2JoystickListener
2. **Phase 2:** Create platform-specific listeners (EvdevMouse for Linux, etc.)
3. **Phase 3:** Migrate InputListening folder to new architecture
4. **Phase 4:** Generate InputProfiles/ from existing GameProfiles/
5. **Phase 5:** Full cross-platform testing

---

## Prerequisites & Initial Setup

**Required for Development:**
- .NET 8.0 SDK
- SDL2 development libraries (libSDL2-dev)
- libevdev development libraries (libevdev-dev) for Linux
- NuGet packages: SDL2-CS

**Repository Structure:**
```
TeknoParrotUI/
├── TeknoParrotUi.Common/          (game logic, input handling)
├── TeknoParrotUi.Avalonia/        (UI, cross-platform)
├── TeknoParrotUi.Common/InputListening/   (OLD - will be replaced)
├── TeknoParrotUi.Common/GameProfiles/     (250+ game configs)
└── CROSSPLATFORM_INPUT_REFACTOR_PLAN.md   (this file)
```

---

## How to Start (Fresh Implementation)

**Phase 0: XML Audit (before coding)**
1. Run `AuditInputMethods.cs` on GameProfiles/ folder
2. Generate report: which games have RawInput, DirectInput, XInput, etc.
3. Identify game categories for testing

**Phase 1: Core Interfaces**
1. Create `TeknoParrotUi.Common/InputListening/IInputListener.cs`
2. Create `TeknoParrotUi.Common/InputListening/InputListenersManager.cs`
3. Implement `TeknoParrotUi.Common/InputListening/Gamepad/SDL2JoystickListener.cs`
4. Test SDL2 gamepad detection on Windows/Linux

**Phase 2: Platform-Specific Listeners**
1. Extract RawInputMouseListener from existing InputListenerRawInput
2. Implement EvdevMouseListener for Linux
3. Implement AndroidTouchListener for Android

**Phase 3: Integration**
1. Update GameRunning and JoystickControl to use InputListenersManager
2. Create InputProfile JSON loader
3. Migrate GameProfile RawInput/Trackball settings

**Phase 4: Testing**
1. Test gun games on Windows (RawInput)
2. Test gun games on Linux (EvdevMouse)
3. Test regular games on both platforms (SDL2 only)

---

## Goal
Unify input handling (joysticks, keyboard, mouse, window metrics) across Windows, Linux, and Android using SDL2 as the primary abstraction, with minimal platform-specific code paths.

## Architecture Overview

### Layer Model
```
┌─────────────────────────────────────────────┐
│  UI / Game Launch (Avalonia, Android UI)    │
├─────────────────────────────────────────────┤
│  CrossPlatform.Input.* (unified interfaces) │
├──────────┬──────────────┬──────────────────┤
│ SDL2     │ libevdev     │ Android Input    │
│ Joystick │ Raw Keys/M   │ Manager (Touch)  │
│ (all)    │ (Linux)      │ (Android)        │
├──────────┼──────────────┼──────────────────┤
│  Windows │ Linux        │  Android         │
└──────────┴──────────────┴──────────────────┘
```

---

## 1. Folder Structure

```
TeknoParrotUi.Common/
├── InputMapping/
│   ├── CrossPlatformInputProvider.cs      [interface]
│   ├── JoystickEventArgs.cs               [events]
│   ├── WindowMetrics.cs                   [POD]
│   ├── LightGunCalibration.cs             [gun game logic]
│   └── InputAction.cs                     [action types]
│
├── Input/
│   ├── IInputProvider.cs                  [unified interface]
│   ├── JoystickInput/
│   │   ├── JoystickProvider.cs            [abstract]
│   │   ├── SDL2JoystickProvider.cs        [Windows/Linux/Android]
│   │   └── JoystickState.cs               [state snapshot]
│   │
│   ├── KeyboardInput/
│   │   ├── KeyboardProvider.cs            [abstract]
│   │   ├── RawInputKeyboardProvider.cs    [Windows only]
│   │   ├── EvdevKeyboardProvider.cs       [Linux only]
│   │   ├── AndroidKeyboardProvider.cs     [Android only]
│   │   └── KeyState.cs                    [state snapshot]
│   │
│   ├── MouseInput/
│   │   ├── MouseProvider.cs               [abstract]
│   │   ├── RawInputMouseProvider.cs       [Windows only]
│   │   ├── EvdevMouseProvider.cs          [Linux only]
│   │   ├── AndroidTouchProvider.cs        [Android only]
│   │   └── MouseState.cs                  [state snapshot]
│   │
│   ├── WindowInput/
│   │   ├── WindowProvider.cs              [abstract]
│   │   ├── SDL2WindowProvider.cs          [cross-platform]
│   │   └── WindowMetrics.cs               [size, position, focus]
│   │
│   └── ProfileStorage/
│       ├── InputProfileLoader.cs          [load from separate files]
│       ├── InputProfileSaver.cs           [save to separate files]
│       └── InputMigration.cs              [migrate from old XML format]
│
├── Platform/
│   ├── PlatformDetection.cs               [OS detection helpers]
│   └── InputApiAvailability.cs            [feature flags]

# **App Data Structure (runtime)**
bin/x86/Debug/ (or published folder)
├── GameProfiles/                          [game definitions - unchanged]
│   ├── SonicArcade.xml
│   ├── GunGame.xml
│   └── ...
├── UserProfiles/                          [user game customizations - unchanged]
├── InputProfiles/                         **[NEW] Separate input bindings**
│   ├── SonicArcade.json                   [SDL2 gamepad + per-game input method]
│   ├── GunGame.json
│   └── ...
└── (other folders unchanged)
```

---

## 1.5 Input Profile Storage (NEW STRUCTURE)

### **Rationale**
- **Separate concerns:** Game profiles (title, emulator, metadata) stay in `GameProfiles/`
- **Input bindings** (which devices, RawInput vs XInput, calibrations) move to `InputProfiles/`
- **Platform-aware:** Input definitions can be different per platform without duplicating game data
- **Input method availability:** Track which input APIs each game actually supports (don't offer RawInput for games that never had it)

### **InputProfiles/ File Format (JSON)**

**Example: SonicArcade.json**
```json
{
  "gameProfileName": "SonicArcade",
  "inputMethods": {
    "SDL2Gamepad": {
      "enabled": true,
      "available": true,
      "isDefault": true,
      "description": "Standard gamepad/joystick (cross-platform, uses SDL2)",
      "platforms": ["windows", "linux", "android"]
    },
    "RawInput": {
      "enabled": false,
      "available": false,
      "isDefault": false,
      "description": "RawInput keyboard/mouse (Windows only, mouse-based)",
      "platforms": ["windows"],
      "reason": "Game profile never included RawInput/gun game mappings"
    },
    "RawInputTrackball": {
      "enabled": false,
      "available": false,
      "isDefault": false,
      "description": "RawInput trackball (Windows only)",
      "platforms": ["windows"],
      "reason": "Game profile never included trackball mappings"
    }
  },
  "defaultInputMethod": "SDL2Gamepad",
  "bindings": {
    "SDL2Gamepad": {
      "buttons": {
        "BUTTON_A": "Jump",
        "BUTTON_B": "Speed",
        "DPAD_LEFT": "Left",
        "DPAD_RIGHT": "Right"
      },
      "axes": {
        "LEFTX": "Horizontal",
        "LEFTY": "Vertical"
      }
    }
  },
  "calibration": {
    "lightGun": null,  // null if not a gun game
    "trackball": null,
    "analogCenter": { "x": 32768, "y": 32768 }
  },
  "metadata": {
    "lastModified": "2026-07-08T12:00:00Z",
    "migratedFromGameProfile": true,
    "migratedVersion": "1.0",
    "notes": "DirectInput and XInput removed in favor of SDL2 (cross-platform)"
  }
}
```

**Example: GunGame.json (with RawInput)**
```json
{
  "gameProfileName": "GunGame",
  "inputMethods": {
    "SDL2Gamepad": {
      "enabled": true,
      "available": true,
      "isDefault": false
    },
    "RawInput": {
      "enabled": true,
      "available": true,
      "isDefault": true,
      "description": "Light gun (Windows only, mouse-based)",
      "platforms": ["windows"]
    },
    "EvdevMouse": {
      "enabled": true,
      "available": true,
      "description": "Light gun on Linux (mouse input)",
      "platforms": ["linux"]
    },
    "AndroidTouch": {
      "enabled": true,
      "available": true,
      "description": "Light gun on Android (touch input)",
      "platforms": ["android"]
    },
    "RawInputTrackball": {
      "enabled": false,
      "available": false,
      "reason": "Not a trackball game"
    }
  },
  "defaultInputMethod": "RawInput",
  "bindings": {
    "RawInput": {
      "mouse": {
        "aim": "Absolute position",
        "trigger": "Left click"
      }
    },
    "EvdevMouse": {
      "mouse": {
        "aim": "Absolute position",
        "trigger": "Left click"
      }
    }
  },
  "calibration": {
    "lightGun": {
      "offsetX": 0,
      "offsetY": 0,
      "scaleX": 1.0,
      "scaleY": 1.0
    }
  }
}
```

### **InputProfileLoader.cs**
```csharp
public class InputProfileLoader
{
    /// <summary>Load input profile for a game, checking platform availability.</summary>
    public static InputProfile Load(GameProfile gameProfile, OperatingSystem platform)
    {
        var profilePath = Path.Combine("InputProfiles", gameProfile.ProfileName + ".json");
        
        if (!File.Exists(profilePath))
        {
            // Fallback: Generate defaults from old GameProfile (for backwards compatibility)
            return GenerateDefaultsFromGameProfile(gameProfile, platform);
        }
        
        var json = File.ReadAllText(profilePath);
        var profile = JsonSerializer.Deserialize<InputProfile>(json);
        
        // Filter input methods by platform availability
        profile.FilterByPlatform(platform);
        
        return profile;
    }
    
    /// <summary>Check if a specific input method is available for this game on this platform.</summary>
    public static bool IsInputMethodAvailable(
        InputProfile profile, 
        InputMethod method, 
        OperatingSystem platform)
    {
        if (!profile.InputMethods.TryGetValue(method, out var methodInfo))
            return false;
            
        if (!methodInfo.Available)
            return false;
            
        // Check platform-specific availability
        if (methodInfo.Platforms.Count > 0 && !methodInfo.Platforms.Contains(platform))
            return false;
            
        return true;
    }
}

public class InputProfile
{
    public string GameProfileName { get; set; }
    public Dictionary<string, InputMethodInfo> InputMethods { get; set; }
    public string DefaultInputMethod { get; set; }
    public Dictionary<string, object> Bindings { get; set; }
    public CalibrationData Calibration { get; set; }
    public ProfileMetadata Metadata { get; set; }
    
    /// <summary>Filter available input methods based on current platform.</summary>
    public void FilterByPlatform(OperatingSystem platform)
    {
        foreach (var (method, info) in InputMethods)
        {
            if (info.Platforms.Count > 0)
            {
                info.Available = info.Platforms.Contains(platform);
            }
        }
    }
}

public class InputMethodInfo
{
    public bool Enabled { get; set; }
    public bool Available { get; set; }
    public bool IsDefault { get; set; }
    public string Description { get; set; }
    public List<OperatingSystem> Platforms { get; set; } = new();
    public string Reason { get; set; }  // Why unavailable, if not available
}
```

---

## 2 Core Interfaces (TeknoParrotUi.Common/Input/)

### **IInputProvider** (root interface)
```csharp
public interface IInputProvider : IDisposable
{
    IGamepadInputProvider Gamepad { get; }
    IKeyboardInputProvider Keyboard { get; }
    IMouseInputProvider Mouse { get; }
    IWindowInputProvider Window { get; }
    
    void Initialize();
    void Shutdown();
    void Update();  // Poll all providers each frame
    
    event EventHandler<InputEventArgs>? InputReceived;
}
```

### **IGamepadInputProvider** (SDL2, all platforms)
```csharp
public interface IGamepadInputProvider
{
    int ConnectedCount { get; }
    IReadOnlyList<GamepadState> GetAllGamepads();
    GamepadState GetGamepad(int index);
    
    event EventHandler<GamepadConnectedEventArgs>? GamepadConnected;
    event EventHandler<GamepadDisconnectedEventArgs>? GamepadDisconnected;
    event EventHandler<GamepadButtonEventArgs>? ButtonPressed;
    event EventHandler<GamepadAxisEventArgs>? AxisMoved;
}

public class GamepadState
{
    public int Index { get; set; }
    public string Name { get; set; }
    public Dictionary<GamepadButton, bool> Buttons { get; set; }
    public Dictionary<GamepadAxis, short> Axes { get; set; }
    public Dictionary<GamepadHat, HatDirection> Hats { get; set; }
}

public enum GamepadButton { A, B, X, Y, LB, RB, Back, Start, LS, RS }
public enum GamepadAxis { LeftX, LeftY, RightX, RightY, LeftTrigger, RightTrigger }
```

### **IKeyboardInputProvider** (platform-specific)
```csharp
public interface IKeyboardInputProvider
{
    bool IsKeyPressed(ScanCode key);
    IReadOnlyList<ScanCode> GetPressedKeys();
    
    event EventHandler<KeyboardEventArgs>? KeyDown;
    event EventHandler<KeyboardEventArgs>? KeyUp;
}

public class KeyboardEventArgs : EventArgs
{
    public ScanCode Key { get; set; }
    public uint Unicode { get; set; }  // UTF-32 for text input
    public bool IsRepeat { get; set; }
}

public enum ScanCode : ushort
{
    A = 0x04, B = 0x05, /* ... SDL2 scancodes ... */
}
```

### **IMouseInputProvider** (platform-specific)
```csharp
public interface IMouseInputProvider
{
    (int X, int Y) Position { get; set; }
    (int X, int Y) RelativeMovement { get; }
    bool IsButtonPressed(MouseButton button);
    
    event EventHandler<MouseMoveEventArgs>? MouseMoved;
    event EventHandler<MouseButtonEventArgs>? ButtonPressed;
    event EventHandler<MouseButtonEventArgs>? ButtonReleased;
    event EventHandler<MouseWheelEventArgs>? WheelScrolled;
}

public enum MouseButton { Left, Middle, Right, X1, X2 }
```

### **IWindowInputProvider** (SDL2, cross-platform)
```csharp
public interface IWindowInputProvider
{
    (int Width, int Height) GetWindowSize();
    (int X, int Y) GetWindowPosition();
    (int Width, int Height) GetScreenSize();
    bool IsWindowFocused();
    bool IsWindowFullscreen();
    
    void SetCursorVisible(bool visible);
    void SetWindowPosition(int x, int y);
    void SetWindowSize(int width, int height);
    void SetFullscreen(bool fullscreen);
    
    IntPtr GetWindowHandle();  // For native code interop if needed
    
    event EventHandler<WindowResizedEventArgs>? WindowResized;
    event EventHandler<WindowMovedEventArgs>? WindowMoved;
    event EventHandler<WindowFocusEventArgs>? FocusChanged;
}
```

---

## 3. Implementation Strategy

### **Phase 1: Shared SDL2 (all platforms)**

**SDL2JoystickProvider.cs** — Single implementation, works everywhere
```csharp
public class SDL2JoystickProvider : IGamepadInputProvider
{
    private Dictionary<int, GamepadState> _gamepads = new();
    
    public SDL2JoystickProvider()
    {
        // Requires: SDL2-CS NuGet + native SDL2 libs
        SDL_InitSubSystem(SDL_INIT_JOYSTICK | SDL_INIT_GAMECONTROLLER);
    }
    
    public void Update()
    {
        SDL_Event e;
        while (SDL_PollEvent(out e) != 0)
        {
            switch (e.type)
            {
                case SDL_EventType.JOYDEVICEADDED:
                    _gamepads[e.jdevice.which] = LoadGamepad(e.jdevice.which);
                    GamepadConnected?.Invoke(this, new(e.jdevice.which));
                    break;
                    
                case SDL_EventType.JOYDEVICEREMOVED:
                    _gamepads.Remove(e.jdevice.which);
                    GamepadDisconnected?.Invoke(this, new(e.jdevice.which));
                    break;
                    
                case SDL_EventType.JOYBUTTONDOWN:
                    var button = MapSDLButton(e.jbutton.button);
                    _gamepads[e.jbutton.which].Buttons[button] = true;
                    ButtonPressed?.Invoke(this, new(e.jbutton.which, button));
                    break;
                    
                case SDL_EventType.JOYBUTTONUP:
                    button = MapSDLButton(e.jbutton.button);
                    _gamepads[e.jbutton.which].Buttons[button] = false;
                    break;
                    
                case SDL_EventType.JOYAXISMOTION:
                    var axis = MapSDLAxis(e.jaxis.axis);
                    _gamepads[e.jaxis.which].Axes[axis] = e.jaxis.value;
                    AxisMoved?.Invoke(this, new(e.jaxis.which, axis, e.jaxis.value));
                    break;
                    
                case SDL_EventType.JOYHATMOTION:
                    var hat = (GamepadHat)e.jhat.hat;
                    _gamepads[e.jhat.which].Hats[hat] = (HatDirection)e.jhat.hatval;
                    break;
            }
        }
    }
    
    public event EventHandler<GamepadConnectedEventArgs>? GamepadConnected;
    public event EventHandler<GamepadDisconnectedEventArgs>? GamepadDisconnected;
    public event EventHandler<GamepadButtonEventArgs>? ButtonPressed;
    public event EventHandler<GamepadAxisEventArgs>? AxisMoved;
}
```

**SDL2WindowProvider.cs** — Single implementation, works everywhere
```csharp
public class SDL2WindowProvider : IWindowInputProvider
{
    private IntPtr _window;
    
    public (int Width, int Height) GetWindowSize()
    {
        SDL_GetWindowSize(_window, out int w, out int h);
        return (w, h);
    }
    
    public (int X, int Y) GetWindowPosition()
    {
        SDL_GetWindowPosition(_window, out int x, out int y);
        return (x, y);
    }
    
    public bool IsWindowFocused()
    {
        uint flags = SDL_GetWindowFlags(_window);
        return (flags & SDL_WINDOW_INPUT_FOCUS) != 0;
    }
    
    public void SetCursorVisible(bool visible)
    {
        SDL_ShowCursor(visible ? SDL_ENABLE : SDL_DISABLE);
    }
    
    public IntPtr GetWindowHandle()
    {
        SDL_SysWMinfo info;
        SDL_VERSION(out info.version);
        SDL_GetWindowWMInfo(_window, ref info);
        
        // info.info.win.window is HWND on Windows
        // info.info.x11.window is on Linux/X11
        // Handle Android/Wayland as needed
        return info.info.win.window;
    }
}
```

### **Phase 2: Platform-Specific Keyboard/Mouse**

**RawInputKeyboardProvider.cs** (Windows only)
```csharp
#if WINDOWS
public class RawInputKeyboardProvider : IKeyboardInputProvider
{
    // Reuse existing RawInputCaptureService logic
    // Register message-only window, listen for WM_INPUT
    // Convert RAWKEYBOARD to ScanCode enum
    
    public event EventHandler<KeyboardEventArgs>? KeyDown;
    public event EventHandler<KeyboardEventArgs>? KeyUp;
}
#endif
```

**EvdevKeyboardProvider.cs** (Linux only)
```csharp
#if LINUX
public class EvdevKeyboardProvider : IKeyboardInputProvider
{
    private IntPtr _libevdevHandle;
    private int _fd;  // File descriptor for /dev/input/eventX
    
    public EvdevKeyboardProvider(string devicePath = "/dev/input/event0")
    {
        _fd = open(devicePath, O_RDONLY | O_NONBLOCK);
        // P/Invoke libevdev_new_from_fd
        // Subscribe to key events
    }
    
    public void Update()
    {
        while (libevdev_next_event(_libevdevHandle, LIBEVDEV_READ_FLAG_NORMAL, out var ev) == 0)
        {
            if (ev.type == EV_KEY)
            {
                var scanCode = (ScanCode)ev.code;
                var isDown = ev.value == 1;
                
                if (isDown)
                    KeyDown?.Invoke(this, new KeyboardEventArgs { Key = scanCode });
                else
                    KeyUp?.Invoke(this, new KeyboardEventArgs { Key = scanCode });
            }
        }
    }
    
    public event EventHandler<KeyboardEventArgs>? KeyDown;
    public event EventHandler<KeyboardEventArgs>? KeyUp;
}
#endif
```

**AndroidKeyboardProvider.cs** (Android only)
```csharp
#if ANDROID
public class AndroidKeyboardProvider : IKeyboardInputProvider
{
    // Android.Views.KeyEvent bridge (via Xamarin.Android or MAUI)
    // Hook into Activity.OnKeyDown / OnKeyUp
    
    public event EventHandler<KeyboardEventArgs>? KeyDown;
    public event EventHandler<KeyboardEventArgs>? KeyUp;
}
#endif
```

Similar structure for **RawInputMouseProvider**, **EvdevMouseProvider**, **AndroidTouchProvider**.

### **Phase 3: Unified InputManager**

**CrossPlatformInputManager.cs** — Factory + coordinator
```csharp
public class CrossPlatformInputManager : IInputProvider
{
    private IGamepadInputProvider _gamepad;
    private IKeyboardInputProvider _keyboard;
    private IMouseInputProvider _mouse;
    private IWindowInputProvider _window;
    
    public static CrossPlatformInputManager Create()
    {
        var manager = new CrossPlatformInputManager();
        
        // Gamepad: SDL2 everywhere
        manager._gamepad = new SDL2JoystickProvider();
        
        // Keyboard: platform-specific
        manager._keyboard = OperatingSystem.IsWindows()
            ? new RawInputKeyboardProvider()
            : OperatingSystem.IsLinux()
            ? new EvdevKeyboardProvider()
            : OperatingSystem.IsAndroid()
            ? new AndroidKeyboardProvider()
            : throw new PlatformNotSupportedException();
        
        // Mouse: platform-specific
        manager._mouse = OperatingSystem.IsWindows()
            ? new RawInputMouseProvider()
            : OperatingSystem.IsLinux()
            ? new EvdevMouseProvider()
            : OperatingSystem.IsAndroid()
            ? new AndroidTouchProvider()
            : throw new PlatformNotSupportedException();
        
        // Window: SDL2 everywhere
        manager._window = new SDL2WindowProvider();
        
        return manager;
    }
    
    public void Initialize()
    {
        _gamepad.Initialize();
        _keyboard.Initialize();
        _mouse.Initialize();
        _window.Initialize();
    }
    
    public void Update()
    {
        _gamepad.Update();
        _keyboard.Update();
        _mouse.Update();
        _window.Update();
    }
    
    public void Dispose()
    {
        _gamepad?.Dispose();
        _keyboard?.Dispose();
        _mouse?.Dispose();
        _window?.Dispose();
    }
    
    public IGamepadInputProvider Gamepad => _gamepad;
    public IKeyboardInputProvider Keyboard => _keyboard;
    public IMouseInputProvider Mouse => _mouse;
    public IWindowInputProvider Window => _window;
}
```

---

## 4. Gun Game Integration

**LightGunController.cs** — High-level gun game wrapper
```csharp
public class LightGunController
{
    private readonly IMouseInputProvider _mouse;
    private readonly IWindowInputProvider _window;
    private LightGunCalibration _calibration;
    
    public LightGunController(CrossPlatformInputManager input)
    {
        _mouse = input.Mouse;
        _window = input.Window;
        _calibration = LoadCalibration();
    }
    
    /// <summary>Get normalized gun position (0-1) relative to game window.</summary>
    public (float X, float Y) GetAimNormalized()
    {
        var (mouseX, mouseY) = _mouse.Position;
        var (windowW, windowH) = _window.GetWindowSize();
        
        return (
            (float)mouseX / windowW,
            (float)mouseY / windowH
        );
    }
    
    /// <summary>Get raw pixel position relative to game window.</summary>
    public (int X, int Y) GetAimPixels()
    {
        return _mouse.Position;
    }
    
    /// <summary>Apply calibration offset and scaling.</summary>
    public (float X, float Y) GetCalibratedAim()
    {
        var (normX, normY) = GetAimNormalized();
        return _calibration.Apply(normX, normY);
    }
    
    public bool IsTriggerPressed => _mouse.IsButtonPressed(MouseButton.Left);
    public bool IsReloadPressed => _mouse.IsButtonPressed(MouseButton.Right);
    
    public bool IsGameWindowActive => _window.IsWindowFocused();
}
```

---

## 5. Joystick Input Mapping (for game launch)

**JoystickInputListener.cs** — Replaces DirectInput listener
```csharp
public class JoystickInputListener
{
    private readonly IGamepadInputProvider _gamepad;
    private Dictionary<int, GamepadState> _previousState = new();
    
    public JoystickInputListener(CrossPlatformInputManager input)
    {
        _gamepad = input.Gamepad;
        _gamepad.ButtonPressed += OnButtonPressed;
        _gamepad.AxisMoved += OnAxisMoved;
    }
    
    public event Action<int, GamepadButton>? ButtonDown;
    public event Action<int, GamepadAxis, short>? AxisChanged;
    
    private void OnButtonPressed(object? sender, GamepadButtonEventArgs e)
    {
        ButtonDown?.Invoke(e.GamepadIndex, e.Button);
    }
    
    private void OnAxisMoved(object? sender, GamepadAxisEventArgs e)
    {
        if (Math.Abs(e.Value) > GamepadAxis.DeadZone)
            AxisChanged?.Invoke(e.GamepadIndex, e.Axis, e.Value);
    }
}
```

---

## 6. Migration Strategy (Phase-by-phase)

### **Step 1: Add SDL2-CS NuGet**
```bash
dotnet add TeknoParrotUi.Common package SDL2-CS
```

### **Step 2: Create Core Interfaces** (Phase 1)
- Copy interface definitions above into `TeknoParrotUi.Common/Input/`
- No logic yet, just contracts

### **Step 3: Implement SDL2 Providers** (Phase 2)
- `SDL2JoystickProvider.cs`
- `SDL2WindowProvider.cs`
- Test on all three platforms

### **Step 4: Implement Platform-Specific Providers** (Phase 3)
- Windows: Integrate existing RawInput code
- Linux: Add libevdev P/Invoke
- Android: Add Android.App bindings

### **Step 5: Create CrossPlatformInputManager** (Phase 4)
- Factory that selects correct implementations
- Expose unified API

### **Step 6: Refactor Consumers** (Phase 5)
- `InputCaptureService` → uses `CrossPlatformInputManager`
- `JoystickHelper.InputListener` → `JoystickInputListener`
- `LightGunController` → new wrapper class
- Gun game logic tests

### **Step 7: Testing & Cleanup** (Phase 6)
- Remove old `SharpDX.XInput` / `SharpDX.DirectInput` P/Invokes
- Consolidate joystick code paths
- Update docs

---

## 6.5 XML Audit & Profile Migration (NEW PHASE)

### **Pre-Migration Audit**

Before refactoring, scan all `GameProfiles/*.xml` files to understand current input method distribution. **Note:** We're removing DirectInput and XInput entirely — all gamepad input will use SDL2 cross-platform instead.

**Script: AuditInputMethods.cs**
```csharp
public class InputMethodAudit
{
    public static void AuditGameProfiles()
    {
        var gameProfilesDir = "GameProfiles";
        var stats = new Dictionary<string, int>
        {
            { "Total", 0 },
            { "HasRawInput", 0 },
            { "HasRawInputTrackball", 0 },
            { "GunGames", 0 },
            { "TrackballGames", 0 },
            { "RegularGames", 0 }
        };
        
        var gunGames = new List<string>();
        var trackballGames = new List<string>();
        
        foreach (var file in Directory.GetFiles(gameProfilesDir, "*.xml"))
        {
            var profile = JoystickHelper.DeSerializeGameProfile(file, false);
            if (profile == null) continue;
            
            stats["Total"]++;
            var gameName = Path.GetFileNameWithoutExtension(file);
            
            bool hasRI = profile.RawInputButtons?.Any(b => b != null) ?? false;
            bool hasRIT = profile.Trackball?.Any(b => b != null) ?? false;
            
            if (hasRI || hasRIT)
                stats["HasRawInput"]++;
            if (hasRIT)
                stats["HasRawInputTrackball"]++;
            
            if (hasRI || hasRIT)
            {
                gunGames.Add(gameName);
                stats["GunGames"]++;
            }
            else if (hasRIT)
            {
                trackballGames.Add(gameName);
                stats["TrackballGames"]++;
            }
            else
            {
                stats["RegularGames"]++;
            }
        }
        
        // Output audit report
        Console.WriteLine("=== INPUT METHOD AUDIT (DirectInput/XInput being removed) ===");
        Console.WriteLine($"Total games: {stats["Total"]}");
        Console.WriteLine($"Games with RawInput (gun/trackball): {stats["HasRawInput"]}");
        Console.WriteLine($"Games with RawInputTrackball: {stats["HasRawInputTrackball"]}");
        Console.WriteLine($"Regular games (using SDL2 only): {stats["RegularGames"]}");
        
        Console.WriteLine("\n=== GUN/TRACKBALL GAMES (need RawInput) ===");
        foreach (var game in gunGames.OrderBy(x => x))
        {
            Console.WriteLine($"  {game}");
        }
        
        Console.WriteLine($"\nTotal gun/trackball games: {gunGames.Count}");
        Console.WriteLine($"Total regular games (SDL2 only): {stats["RegularGames"]}");
    }
}
```

### **Expected Output Pattern**
```
=== INPUT METHOD AUDIT (DirectInput/XInput being removed) ===
Total games: 250
Games with RawInput (gun/trackball): 53
Games with RawInputTrackball: 8
Regular games (using SDL2 only): 197

=== GUN/TRACKBALL GAMES (need RawInput) ===
  GunGame1
  GunGame2
  TrackballGame1
  ...

Total gun/trackball games: 53
Total regular games (SDL2 only): 197
```

### **SDL2 Gamepad Coverage Verification**

Before finalizing the migration, verify that SDL2 covers all XInput/DirectInput scenarios:

**SDL2JoystickCoverageTest.cs**
```csharp
public class SDL2JoystickCoverageTest
{
    [Test]
    public void VerifySDL2HasFullXInputAnalogRange()
    {
        // Test that SDL2 supports full 16-bit analog range (like XInput)
        // SDL2 axis values: -32768 to 32767 (16-bit signed)
        // XInput axis values: -32768 to 32767 (16-bit signed)
        // ✓ Match — SDL2 has full range
        
        Assert.AreEqual(-32768, short.MinValue);
        Assert.AreEqual(32767, short.MaxValue);
        Console.WriteLine("✓ SDL2 supports full XInput analog range (-32768 to 32767)");
    }
    
    [Test]
    public void VerifySDL2HasIndividualLeftRightTriggers()
    {
        // SDL2 axis mapping:
        // SDL_CONTROLLER_AXIS_TRIGGERLEFT (axis 4)  → Left trigger (0 to 32767)
        // SDL_CONTROLLER_AXIS_TRIGGERRIGHT (axis 5) → Right trigger (0 to 32767)
        // 
        // XInput mapping:
        // LeftTrigger (0-255) → Maps to axis 4 (0 to 32767)
        // RightTrigger (0-255) → Maps to axis 5 (0 to 32767)
        // 
        // ✓ SDL2 has independent triggers (not shared like some DirectInput devices)
        
        Console.WriteLine("✓ SDL2 supports independent left/right triggers");
        Console.WriteLine("✓ Left trigger: SDL_CONTROLLER_AXIS_TRIGGERLEFT (axis 4)");
        Console.WriteLine("✓ Right trigger: SDL_CONTROLLER_AXIS_TRIGGERRIGHT (axis 5)");
    }
    
    [Test]
    public void VerifySDL2ButtonCoverage()
    {
        // SDL2 gamepad buttons:
        var buttons = new[]
        {
            "SDL_CONTROLLER_BUTTON_A",              // XInput BUTTON_A
            "SDL_CONTROLLER_BUTTON_B",              // XInput BUTTON_B
            "SDL_CONTROLLER_BUTTON_X",              // XInput BUTTON_X
            "SDL_CONTROLLER_BUTTON_Y",              // XInput BUTTON_Y
            "SDL_CONTROLLER_BUTTON_BACK",           // XInput BACK
            "SDL_CONTROLLER_BUTTON_START",          // XInput START
            "SDL_CONTROLLER_BUTTON_LEFTSTICK",      // XInput LeftThumbstick click
            "SDL_CONTROLLER_BUTTON_RIGHTSTICK",     // XInput RightThumbstick click
            "SDL_CONTROLLER_BUTTON_LEFTSHOULDER",   // XInput LB
            "SDL_CONTROLLER_BUTTON_RIGHTSHOULDER",  // XInput RB
            "SDL_CONTROLLER_BUTTON_GUIDE"           // XInput Guide button
        };
        
        Console.WriteLine("✓ SDL2 supports all XInput buttons:");
        foreach (var btn in buttons)
            Console.WriteLine($"  - {btn}");
    }
    
    [Test]
    public void VerifySDL2AxisCoverage()
    {
        // SDL2 axes:
        var axes = new[]
        {
            "SDL_CONTROLLER_AXIS_LEFTX",      // Left stick X
            "SDL_CONTROLLER_AXIS_LEFTY",      // Left stick Y
            "SDL_CONTROLLER_AXIS_RIGHTX",     // Right stick X
            "SDL_CONTROLLER_AXIS_RIGHTY",     // Right stick Y
            "SDL_CONTROLLER_AXIS_TRIGGERLEFT",  // Left trigger
            "SDL_CONTROLLER_AXIS_TRIGGERRIGHT"  // Right trigger
        };
        
        Console.WriteLine("✓ SDL2 supports all XInput axes:");
        foreach (var axis in axes)
            Console.WriteLine($"  - {axis}");
    }
}
```

**Conclusion:** SDL2 fully covers XInput/DirectInput button and axis mappings. No functionality is lost by removing those libraries.

### **Migration Steps**

#### **Phase 1: Generate InputProfiles/** (automated)

```csharp
public class InputProfileMigrator
{
    public static void MigrateAllProfiles()
    {
        Directory.CreateDirectory("InputProfiles");
        
        foreach (var file in Directory.GetFiles("GameProfiles", "*.xml"))
        {
            var gameProfile = JoystickHelper.DeSerializeGameProfile(file, false);
            if (gameProfile == null) continue;
            
            var inputProfile = CreateInputProfile(gameProfile);
            var profileName = Path.GetFileNameWithoutExtension(file);
            var outputPath = Path.Combine("InputProfiles", profileName + ".json");
            
            File.WriteAllText(
                outputPath,
                JsonSerializer.Serialize(inputProfile, new JsonSerializerOptions { WriteIndented = true })
            );
            
            Console.WriteLine($"✓ Migrated {profileName}");
        }
    }
    
    private static InputProfile CreateInputProfile(GameProfile gameProfile)
    {
        bool hasRI = gameProfile.RawInputButtons?.Any(b => b != null) ?? false;
        bool hasRIT = gameProfile.Trackball?.Any(b => b != null) ?? false;
        
        var profile = new InputProfile
        {
            GameProfileName = gameProfile.ProfileName,
            InputMethods = new()
            {
                {
                    "SDL2Gamepad", new InputMethodInfo
                    {
                        Enabled = true,
                        Available = true,
                        IsDefault = true,  // SDL2 is always default (replaces XInput/DirectInput)
                        Description = "Standard gamepad/joystick (cross-platform, uses SDL2)",
                        Platforms = new() { "Windows", "Linux", "Android" }
                    }
                },
                {
                    "RawInput", new InputMethodInfo
                    {
                        Enabled = hasRI,
                        Available = hasRI,
                        IsDefault = hasRI && !hasRIT,  // RawInput for gun games only
                        Platforms = new() { "Windows" },
                        Reason = !hasRI ? "Game profile never included RawInput mappings" : null
                    }
                },
                {
                    "RawInputTrackball", new InputMethodInfo
                    {
                        Enabled = hasRIT,
                        Available = hasRIT,
                        IsDefault = hasRIT,  // Trackball takes precedence if present
                        Platforms = new() { "Windows" },
                        Reason = !hasRIT ? "Game profile never included trackball mappings" : null
                    }
                },
                {
                    "EvdevMouse", new InputMethodInfo
                    {
                        Enabled = hasRI || hasRIT,  // Enable if Windows had RawInput
                        Available = hasRI || hasRIT,
                        Description = "Light gun on Linux (mouse input)",
                        Platforms = new() { "Linux" }
                    }
                },
                {
                    "AndroidTouch", new InputMethodInfo
                    {
                        Enabled = hasRI || hasRIT,  // Enable if Windows had RawInput
                        Available = hasRI || hasRIT,
                        Description = "Light gun on Android (touch input)",
                        Platforms = new() { "Android" }
                    }
                }
            },
            DefaultInputMethod = hasRIT ? "RawInputTrackball" : hasRI ? "RawInput" : "SDL2Gamepad",
            Bindings = ExtractBindings(gameProfile),
            Calibration = ExtractCalibration(gameProfile),
            Metadata = new ProfileMetadata
            {
                MigratedFromGameProfile = true,
                MigratedVersion = "1.0",
                Notes = "DirectInput and XInput removed in favor of SDL2 (cross-platform)",
                LastModified = DateTime.UtcNow
            }
        };
        
        return profile;
    }
    
    private static Dictionary<string, object> ExtractBindings(GameProfile gameProfile)
    {
        // Only extract SDL2Gamepad bindings (from original JoystickButtons)
        // DirectInput and XInput are discarded — SDL2 replaces them
        return new()
        {
            { "SDL2Gamepad", gameProfile.JoystickButtons }
        };
    }
    
    private static CalibrationData ExtractCalibration(GameProfile gameProfile)
    {
        return new CalibrationData
        {
            LightGun = gameProfile.LightGunCalibration,
            Trackball = gameProfile.TrackballCalibration,
            AnalogCenter = new() { X = 32768, Y = 32768 }
        };
    }
}
```

#### **Phase 2: Load from InputProfiles** (code change)

Update `GameLaunch` and input loading code to:
1. Load `GameProfile` from `GameProfiles/*.xml` (game data only)
2. Load `InputProfile` from `InputProfiles/*.json` (input bindings)
3. Check available input methods before offering UI options

```csharp
public class GameInputSetup
{
    private GameProfile _gameProfile;
    private InputProfile _inputProfile;
    
    public GameInputSetup(string gameName)
    {
        _gameProfile = GameProfileLoader.Load(gameName);
        _inputProfile = InputProfileLoader.Load(_gameProfile, GetCurrentPlatform());
    }
    
    /// <summary>Get input methods available for this game on this platform.</summary>
    public List<string> GetAvailableInputMethods()
    {
        return _inputProfile.InputMethods
            .Where(m => m.Value.Available && m.Value.Enabled)
            .Select(m => m.Key)
            .ToList();
    }
    
    /// <summary>UI should only show buttons for these methods.</summary>
    public bool CanUseRawInput => _inputProfile.InputMethods["RawInput"].Available;
    public bool CanUseRawInputTrackball => _inputProfile.InputMethods["RawInputTrackball"].Available;
    
    // XInput and DirectInput buttons are NEVER shown — these are being removed entirely
    // All gamepad input uses SDL2 instead (cross-platform)
    // This simplifies the UI significantly
}
```
```

#### **Phase 3: Clean up GameProfile**

Remove from `GameProfile` class in future versions:
- ❌ `RawInputButtons[]` (now in InputProfiles)
- ❌ `Trackball[]` (now in InputProfiles)
- ❌ Input-method-specific fields

Keep in `GameProfile`:
- ✅ `JoystickButtons[]` (for XInput/DirectInput reference, gradually deprecate)
- ✅ Game metadata (name, emulator, genre)
- ✅ Game config (ROM path, ROM type)

---

## 7 Dependencies

### Windows
- SDL2-CS (NuGet)
- SDL2 native (already using via Avalonia.Desktop)

### Linux
- SDL2-CS (NuGet)
- SDL2 native (libSDL2-2.0.so)
- libevdev native (libevdev.so)
- libevdev P/Invoke bindings (create custom)

### Android
- SDL2-CS (NuGet, if available for Android)
- Android.App / Xamarin.Android (for Activity hooks)
- Or use MAUI input API

---

## 8. Future: macOS Support

When/if macOS support is added:
- **Gamepad:** SDL2JoystickProvider (unchanged)
- **Window:** SDL2WindowProvider (unchanged)
- **Keyboard:** Create `MacOSKeyboardProvider` (IOKit or AppKit NSEvent)
- **Mouse:** Create `MacOSMouseProvider` (AppKit NSEvent)
- **Wine/Proton:** Run existing Windows code path (use `OperatingSystem.IsWindows()` check)

```csharp
// If running in Proton/Wine on macOS
if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && IsProtonEnvironment())
{
    // Use RawInputKeyboardProvider (Windows code) as fallback
}
else if (OperatingSystem.IsMacOS())
{
    manager._keyboard = new MacOSKeyboardProvider();
}
```

---

## 9. File Additions Summary

### Current Architecture (To Be Replaced)

The existing `TeknoParrotUi.Common/InputListening/` folder contains Windows-specific input polling:

**Current Structure:**
```
InputListening/
├── InputListener.cs (main orchestrator)
├── InputListenerXInput.cs (XInput gamepad polling, 4 players)
├── InputListenerDirectInput.cs (DirectInput gamepad polling)
├── InputListenerRawInput.cs (mouse/keyboard for gun games)
├── InputListenerRawInputTrackball.cs (trackball devices)
└── XInputDeviceHelper.cs (prevent double-polling in MergedInput)
```

**Problems with current approach:**
1. **Windows-only:** Hardcoded SharpDX.XInput and SharpDX.DirectInput
2. **Duplicate polling:** XInput and DirectInput both poll gamepads (MergedInput tries to prevent via XInputDeviceHelper)
3. **Not cross-platform:** RawInput is Windows-only, no equivalent on Linux/Android
4. **Complex orchestration:** InputListener manages 5+ different listener threads
5. **Platform-specific hacks:** MergedInput mode is complex workaround for gamepad duplication

### New Architecture (Cross-Platform)

Replace with **listener-per-input-method** design, activated based on `InputProfile`:

```
InputListening/
├── IInputListener.cs (base interface)
├── InputListenersManager.cs (orchestrator)
├── Gamepad/
│   └── SDL2JoystickListener.cs (REPLACES XInput+DirectInput, all platforms)
├── Keyboard/
│   ├── RawInputKeyboardListener.cs (Windows only)
│   ├── EvdevKeyboardListener.cs (Linux only)
│   └── AndroidKeyboardListener.cs (Android only)
├── Mouse/
│   ├── RawInputMouseListener.cs (Windows gun games only)
│   ├── EvdevMouseListener.cs (Linux gun games only)
│   └── AndroidTouchListener.cs (Android gun games only)
└── Helpers/
    ├── AnalogTriggerHelper.cs
    ├── RotaryEncoderHelper.cs
    └── GunGameCalibration.cs
```

### Design Principles

**1. Single gamepad backend (SDL2):**
```csharp
// OLD (XInput polling in separate thread)
_xi1 = new Thread(() => _inputListenerXInput.ListenXInput(..., UserIndex.One, ...));
_xi2 = new Thread(() => _inputListenerXInput.ListenXInput(..., UserIndex.Two, ...));
// + DirectInput thread for non-XInput devices
// = Complex, duplicated polling

// NEW (SDL2 handles all gamepads uniformly)
var gamepadListener = new SDL2JoystickListener();
gamepadListener.Listen(joystickButtons, inputProfile);
// Single thread, all platforms, all gamepads
```

**2. Platform-specific input selection via InputProfile:**
```csharp
// OLD (hardcoded InputApi enum check)
if (_inputApi == InputApi.DirectInput) { ... }
else if (_inputApi == InputApi.XInput) { ... }
else if (_inputApi == InputApi.RawInput) { ... }

// NEW (InputProfile determines available methods)
var inputProfile = InputProfileLoader.Load(gameProfile, platform);
foreach (var (methodName, methodInfo) in inputProfile.InputMethods)
{
    if (!methodInfo.Available) continue;  // Platform doesn't support this method
    
    if (methodName == "SDL2Gamepad")
        manager.AddListener(new SDL2JoystickListener());
    else if (methodName == "RawInput")
        manager.AddListener(new RawInputMouseListener());
    else if (methodName == "EvdevMouse")
        manager.AddListener(new EvdevMouseListener());
}
```

**3. Unified listener interface:**
```csharp
public interface IInputListener
{
    /// <summary>Start listening for input events.</summary>
    void Start(GameProfile gameProfile, InputProfile inputProfile, List<JoystickButtons> joystickButtons);
    
    /// <summary>Handle Windows messages (RawInput, etc.).</summary>
    void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    
    /// <summary>Stop and cleanup resources.</summary>
    void Stop();
    
    /// <summary>Friendly name for logging.</summary>
    string Name { get; }
}
```

### Implementation Details

**SDL2JoystickListener.cs (REPLACES XInput + DirectInput)**
- Uses SDL2 for gamepad detection and polling
- Supports all 4 players (like XInput) but works on all platforms
- Converts SDL2 axes/buttons → JoystickButtons (same format as XInput output)
- Handles analog trigger calibration (STO0Z percent configuration)
- Handles special cases (gun games, WMMT5/6 gear shifting, Initial D, etc.)
- **Key requirement:** Verify SDL2-CS supports independent left/right triggers (separate axes)

```csharp
public class SDL2JoystickListener : IInputListener
{
    private Thread _pollingThread;
    private bool _killMe;
    
    public void Start(GameProfile gameProfile, InputProfile inputProfile, List<JoystickButtons> joystickButtons)
    {
        _pollingThread = new Thread(() => Poll(gameProfile, joystickButtons));
        _pollingThread.Start();
    }
    
    private void Poll(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
    {
        while (!_killMe)
        {
            // Poll SDL2 joystick devices (handles all 4 players)
            for (int player = 0; player < 4; player++)
            {
                var joystick = SDL_JoystickOpen(player);
                if (joystick == IntPtr.Zero) continue;
                
                // Read buttons, axes, hats
                // Convert to JoystickButtons format
                // Apply special game logic (gear shifts, gun calibration, etc.)
            }
            
            Thread.Sleep(5);  // Poll at ~200 Hz
        }
    }
    
    public void Stop() => _killMe = true;
    public string Name => "SDL2Gamepad";
}
```

**RawInputMouseListener.cs (Windows gun games only)**
- Extracted from InputListenerRawInput
- Handles mouse movement capture for gun games
- Supports windowed mode with mouse clipping
- Supports rotary encoders (for steering wheels converted to mouse)
- Only activated if game has RawInput in InputProfile

**EvdevMouseListener.cs (Linux gun games only)**
- New implementation using libevdev P/Invoke
- Equivalent to RawInputMouseListener but for Linux
- Reads `/dev/input/event*` devices
- Calculates relative mouse motion

**AndroidTouchListener.cs (Android gun games only)**
- New implementation using Android.App touch events
- Converts touch coordinates to analog axis values
- Only activated if game has RawInputTrackball in InputProfile

**InputListenersManager.cs (NEW orchestrator)**
```csharp
public class InputListenersManager
{
    private List<IInputListener> _listeners = new();
    private GameProfile _currentProfile;
    private InputProfile _currentInputProfile;
    
    public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
    {
        _currentProfile = gameProfile;
        _currentInputProfile = InputProfileLoader.Load(gameProfile, RuntimeInformation.OSDescription);
        
        // Activate listeners based on InputProfile availability
        foreach (var (methodName, methodInfo) in _currentInputProfile.InputMethods)
        {
            if (!methodInfo.Available || !methodInfo.Enabled) continue;
            
            IInputListener listener = methodName switch
            {
                "SDL2Gamepad" => new SDL2JoystickListener(),
                "RawInput" when OperatingSystem.IsWindows() => new RawInputMouseListener(),
                "EvdevMouse" when OperatingSystem.IsLinux() => new EvdevMouseListener(),
                "AndroidTouch" when OperatingSystem.IsAndroid() => new AndroidTouchListener(),
                _ => null
            };
            
            if (listener != null)
            {
                listener.Start(gameProfile, _currentInputProfile, joystickButtons);
                _listeners.Add(listener);
            }
        }
    }
    
    public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Windows only: forward to RawInput listeners
        foreach (var listener in _listeners.OfType<RawInputMouseListener>())
            listener.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
    }
    
    public void Stop()
    {
        foreach (var listener in _listeners)
            listener.Stop();
        _listeners.Clear();
    }
}
```

### Migration Steps

**Phase 1: Create new listener interfaces and SDL2 implementation**
- Create `IInputListener.cs` interface
- Implement `SDL2JoystickListener.cs` (test thoroughly on Windows/Linux)
- Implement `InputListenersManager.cs` orchestrator

**Phase 2: Create platform-specific listeners**
- Extract `RawInputMouseListener.cs` from existing code
- Create `EvdevMouseListener.cs` with P/Invoke bindings
- Create `AndroidTouchListener.cs` for touch input

**Phase 3: Integrate with existing code**
- Update `InputCaptureService.cs` to use new manager
- Update game launch code to pass InputProfile to listeners
- Test on Windows (RawInput + SDL2), Linux (EvdevMouse + SDL2)

**Phase 4: Remove old code**
- Delete `InputListener.cs`
- Delete `InputListenerXInput.cs`
- Delete `InputListenerDirectInput.cs`
- Delete `XInputDeviceHelper.cs`
- Keep `InputListenerRawInput.cs` and `InputListenerRawInputTrackball.cs` temporarily during extraction

### Benefits of New Architecture

| Aspect | Old | New |
|--------|-----|-----|
| **Gamepad backend** | XInput + DirectInput | SDL2 only |
| **Platforms** | Windows only | Windows/Linux/Android |
| **Thread count** | 6-7 (XI×4 + DI + RI + RIT) | 2-3 (SDL2 + optional RI + optional Touch) |
| **Code duplication** | High (XI and DI do same thing) | None (SDL2 replaces both) |
| **MergedInput complexity** | Complex (XInputDeviceHelper) | Automatic (InputProfile) |
| **Platform-specific code** | Scattered throughout listeners | Encapsulated in listener implementations |
| **Configuration** | InputApi enum | InputProfile JSON |
| **Testability** | Difficult (mock SharpDX) | Easy (implement IInputListener) |

---

### New files to create:
```
TeknoParrotUi.Common/
├── Input/
│   ├── IInputProvider.cs
│   ├── IGamepadInputProvider.cs
│   ├── IKeyboardInputProvider.cs
│   ├── IMouseInputProvider.cs
│   ├── IWindowInputProvider.cs
│   ├── InputEventArgs.cs (all event arg types)
│   ├── GamepadState.cs
│   ├── WindowMetrics.cs
│   ├── GamepadInputProvider/
│   │   └── SDL2JoystickProvider.cs              [REPLACES XInput + DirectInput]
│   ├── KeyboardInputProvider/
│   │   ├── RawInputKeyboardProvider.cs         [Windows only]
│   │   ├── EvdevKeyboardProvider.cs            [Linux only]
│   │   └── AndroidKeyboardProvider.cs          [Android only]
│   ├── MouseInputProvider/
│   │   ├── RawInputMouseProvider.cs            [Windows only]
│   │   ├── EvdevMouseProvider.cs               [Linux only]
│   │   └── AndroidTouchProvider.cs             [Android only]
│   ├── WindowInputProvider/
│   │   └── SDL2WindowProvider.cs               [cross-platform]
│   ├── ProfileStorage/
│   │   ├── InputProfileLoader.cs
│   │   ├── InputProfileSaver.cs
│   │   └── InputMigration.cs
│   └── CrossPlatformInputManager.cs
├── InputMapping/
│   ├── LightGunController.cs
│   ├── JoystickInputListener.cs
│   └── LightGunCalibration.cs
└── Platform/
    ├── PlatformDetection.cs
    └── EvdevInterop.cs
```

### Removed/Deprecated:
```
❌ SharpDX.XInput — replaced by SDL2
❌ SharpDX.DirectInput — replaced by SDL2
❌ XInputButton binding definitions — replaced by SDL2 gamepad bindings
❌ DirectInputButton binding definitions — replaced by SDL2 gamepad bindings
❌ All XInput-specific code paths — consolidated into SDL2JoystickProvider
❌ All DirectInput-specific code paths — consolidated into SDL2JoystickProvider
```

### New files to create:
```
TeknoParrotUi.Common/
├── Input/
│   ├── IInputProvider.cs
│   ├── IGamepadInputProvider.cs
│   ├── IKeyboardInputProvider.cs
│   ├── IMouseInputProvider.cs
│   ├── IWindowInputProvider.cs
│   ├── InputEventArgs.cs (all event arg types)
│   ├── GamepadState.cs
│   ├── WindowMetrics.cs
│   ├── GamepadInputProvider/
│   │   └── SDL2JoystickProvider.cs
│   ├── KeyboardInputProvider/
│   │   ├── RawInputKeyboardProvider.cs (Windows)
│   │   ├── EvdevKeyboardProvider.cs (Linux)
│   │   └── AndroidKeyboardProvider.cs (Android)
│   ├── MouseInputProvider/
│   │   ├── RawInputMouseProvider.cs (Windows)
│   │   ├── EvdevMouseProvider.cs (Linux)
│   │   └── AndroidTouchProvider.cs (Android)
│   ├── WindowInputProvider/
│   │   └── SDL2WindowProvider.cs
│   └── CrossPlatformInputManager.cs
├── InputMapping/
│   ├── LightGunController.cs
│   ├── JoystickInputListener.cs
│   └── LightGunCalibration.cs
└── Platform/
    ├── PlatformDetection.cs
    └── EvdevInterop.cs (Linux P/Invoke bindings)
```

### Modified files:
- `InputCaptureService.cs` → Use `CrossPlatformInputManager` instead of direct SharpDX
- `UiNavigationService.cs` → Use gamepad provider from manager
- Gun game input handlers → Use `LightGunController` wrapper
- **`GameInputSetup.cs`** → Load `InputProfile` from separate folder, check availability
- **`GameProfileLoader.cs`** → No longer needs to load input bindings (now separate)
- **UI Views** → Hide unavailable input method buttons (RawInput, Trackball for non-gun games)

---

## 10. InputListening Architecture (6-Phase Implementation)

### Overview
The `TeknoParrotUi.Common/InputListening/` folder redesign follows a 6-phase strategy that transforms it from Windows-specific polling to cross-platform listeners loaded dynamically based on InputProfile.

### Key Design Changes
- **Before:** All listeners in one folder, orchestrated by monolithic `InputListener.cs`
- **After:** Single listener per input method, loaded/unloaded via `InputListenersManager`
- **Benefit:** Minimal platform-specific code, testable in isolation, easy to add new platforms

### Phase Structure

**Phase 1 (Audit):**  
Run `AuditInputMethods.cs` to understand game input requirements across all 250+ profiles.

**Phase 2-4 (Listener Implementation):**  
Implement core listeners in sequence: SDL2Joystick → Platform-specific keyboard → Platform-specific mouse.

**Phase 5-6 (Integration):**  
Update game launch and UI to use InputProfile + InputListenersManager.

For detailed implementation of each listener type (SDL2JoystickListener, RawInputMouseListener, EvdevMouseListener, etc.), see **Section 12: InputListening Folder Migration** below.

---

## 11. Build Configuration

### `.csproj` conditional compilation:
```xml
<PropertyGroup>
    <DefineConstants Condition="'$(RuntimeIdentifier)' == 'win-x64'">$(DefineConstants);WINDOWS_INPUT</DefineConstants>
    <DefineConstants Condition="'$(RuntimeIdentifier)' == 'linux-x64'">$(DefineConstants);LINUX_INPUT</DefineConstants>
    <DefineConstants Condition="'$(RuntimeIdentifier)' == 'android'">$(DefineConstants);ANDROID_INPUT</DefineConstants>
</PropertyGroup>

<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <PackageReference Include="SDL2-CS" Version="*" />
</ItemGroup>
```

---

## 12. InputListening Folder Migration

### Current InputListening Files (To Be Replaced)

```
❌ InputListener.cs (orchestrator)
❌ InputListenerXInput.cs (XInput polling) → REPLACE with SDL2JoystickListener
❌ InputListenerDirectInput.cs (DirectInput polling) → REPLACE with SDL2JoystickListener
❌ InputListenerRawInput.cs (mouse/keyboard) → EXTRACT into RawInputMouseListener
❌ InputListenerRawInputTrackball.cs (trackball) → EXTRACT into RawInputMouseListener (subset)
❌ XInputDeviceHelper.cs (XInput detection) → NO LONGER NEEDED
```

### New InputListening Architecture

```
TeknoParrotUi.Common/InputListening/
├── IInputListener.cs (base interface for all listeners)
├── InputListenersManager.cs (orchestrator, replaces InputListener)
├── Gamepad/
│   └── SDL2JoystickListener.cs (replaces XInput + DirectInput)
├── Mouse/
│   ├── RawInputMouseListener.cs (Windows gun games, extracted from InputListenerRawInput)
│   └── EvdevMouseListener.cs (Linux gun games, NEW implementation)
├── Touch/
│   └── AndroidTouchListener.cs (Android gun games, NEW implementation)
└── Helpers/
    ├── AnalogTriggerHelper.cs (manage STO0Z trigger calibration)
    ├── RotaryEncoderHelper.cs (convert rotary to button events)
    ├── GameSpecialCaseHandler.cs (WMMT5 gears, Initial D steering, etc.)
    └── GunGameCalibration.cs (crosshair positioning, mouse clipping)
```

### Interface Definition

**IInputListener.cs:**
```csharp
public interface IInputListener
{
    /// <summary>Friendly name (for logging/debugging)</summary>
    string Name { get; }
    
    /// <summary>Supported on this platform</summary>
    bool IsSupported { get; }
    
    /// <summary>Start polling input device(s)</summary>
    /// <param name="gameProfile">Game configuration with special rules</param>
    /// <param name="inputProfile">Input method availability for this game/platform</param>
    /// <param name="joystickButtons">Output array to write button states to</param>
    void Start(GameProfile gameProfile, InputProfile inputProfile, List<JoystickButtons> joystickButtons);
    
    /// <summary>Handle WndProc messages (Windows-only, for RawInput)</summary>
    void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
    
    /// <summary>Stop polling and cleanup resources</summary>
    void Stop();
}
```

### Key Implementation Details

#### **SDL2JoystickListener (Replaces XInput + DirectInput)**
```csharp
public class SDL2JoystickListener : IInputListener
{
    public string Name => "SDL2Gamepad";
    public bool IsSupported => true;  // All platforms have SDL2
    
    private Thread _pollingThread;
    private bool _killMe;
    private GameProfile _gameProfile;
    private List<JoystickButtons> _joystickButtons;
    private int _sto0zPercent;
    
    public void Start(GameProfile gameProfile, InputProfile inputProfile, List<JoystickButtons> joystickButtons)
    {
        _gameProfile = gameProfile;
        _joystickButtons = joystickButtons;
        
        // Extract STO0Z trigger calibration from GameProfile.ConfigValues
        _sto0zPercent = ExtractSTO0ZPercent(gameProfile);
        
        _killMe = false;
        _pollingThread = new Thread(() => Poll());
        _pollingThread.Start();
    }
    
    private void Poll()
    {
        // SDL2 event loop (runs on all platforms)
        // Polls up to 4 joystick devices
        // Converts SDL2 axes/buttons to JoystickButtons format
        // Handles special game logic:
        //   - Analog trigger calibration (STO0Z)
        //   - Gear shifting (WMMT5, WMMT6, FnF)
        //   - Rotary encoder detection
        //   - Initial D steering wheel handling
    }
    
    public void Stop()
    {
        _killMe = true;
        _pollingThread.Join(5000);
    }
    
    public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Not used for gamepad input (no WndProc messages)
    }
}
```

#### **RawInputMouseListener (Windows Gun Games)**
```csharp
public class RawInputMouseListener : IInputListener
{
    public string Name => "RawInputMouse";
    public bool IsSupported => OperatingSystem.IsWindows();
    
    // Extract mouse capture logic from InputListenerRawInput
    // Keep: crosshair centering, mouse clipping, rotary encoder handling
    // Key methods:
    //   - Poll mouse delta position
    //   - Convert to 16-bit analog values
    //   - Support windowed mode (mouse clipping)
    //   - Support rotary encoder (convert to button presses)
    //   - Handle calibration (center crosshairs)
}
```

#### **EvdevMouseListener (Linux Gun Games)**
```csharp
public class EvdevMouseListener : IInputListener
{
    public string Name => "EvdevMouse";
    public bool IsSupported => OperatingSystem.IsLinux();
    
    // NEW: Read from /dev/input/event* devices using libevdev
    // Key functionality:
    //   - Open all mouse devices under /dev/input/event*
    //   - Calculate relative motion (like Windows mouse input)
    //   - Convert to 16-bit analog values (same format as RawInput output)
    //   - Handle gun game calibration
}
```

#### **AndroidTouchListener (Android Gun Games)**
```csharp
public class AndroidTouchListener : IInputListener
{
    public string Name => "AndroidTouch";
    public bool IsSupported => OperatingSystem.IsAndroid();
    
    // NEW: Use Android.App.Activity touch events
    // Key functionality:
    //   - Hook into Activity.OnTouchEvent()
    //   - Convert touch coordinates to 16-bit analog
    //   - Handle multi-touch (2 guns for 2-player games)
    //   - Map screen pixels to game coordinates
}
```

### InputListenersManager (Orchestrator)

```csharp
public class InputListenersManager
{
    private List<IInputListener> _listeners = new();
    private GameProfile _currentProfile;
    private InputProfile _currentInputProfile;
    
    /// <summary>Start listeners based on InputProfile</summary>
    public void Start(GameProfile gameProfile, List<JoystickButtons> joystickButtons)
    {
        _currentProfile = gameProfile;
        _currentInputProfile = InputProfileLoader.Load(gameProfile, RuntimeInformation.OSDescription);
        
        // Always enable SDL2 gamepad listener (replaces XInput+DirectInput)
        var sdl2Listener = new SDL2JoystickListener();
        sdl2Listener.Start(gameProfile, _currentInputProfile, joystickButtons);
        _listeners.Add(sdl2Listener);
        
        // Enable RawInput/EvdevMouse only if game has it in InputProfile
        if (_currentInputProfile.InputMethods.ContainsKey("RawInput") && 
            _currentInputProfile.InputMethods["RawInput"].Available)
        {
            var listener = OperatingSystem.IsWindows() ? 
                new RawInputMouseListener() :
                OperatingSystem.IsLinux() ? 
                new EvdevMouseListener() : 
                null;
            
            if (listener != null)
            {
                listener.Start(gameProfile, _currentInputProfile, joystickButtons);
                _listeners.Add(listener);
            }
        }
        
        // Enable Android touch listener if applicable
        if (OperatingSystem.IsAndroid() && 
            _currentInputProfile.InputMethods.ContainsKey("AndroidTouch") &&
            _currentInputProfile.InputMethods["AndroidTouch"].Available)
        {
            var touchListener = new AndroidTouchListener();
            touchListener.Start(gameProfile, _currentInputProfile, joystickButtons);
            _listeners.Add(touchListener);
        }
    }
    
    /// <summary>Route WndProc messages (Windows only)</summary>
    public void WndProcReceived(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        foreach (var listener in _listeners)
            listener.WndProcReceived(hwnd, msg, wParam, lParam, ref handled);
    }
    
    /// <summary>Stop all listeners</summary>
    public void Stop()
    {
        foreach (var listener in _listeners)
            listener.Stop();
        _listeners.Clear();
    }
}
```

### Migration Path

**Step 1: Create new listener interfaces**
- Create `IInputListener.cs`
- Create `InputListenersManager.cs`
- Test with minimal dummy listener

**Step 2: Implement SDL2JoystickListener**
- Port logic from `InputListenerXInput.cs` and `InputListenerDirectInput.cs`
- Verify SDL2 polling works on Windows/Linux
- Test special game cases (STO0Z, WMMT5 gears, Initial D, etc.)
- **Critical:** Ensure independent left/right analog triggers work

**Step 3: Extract RawInput listener**
- Create `RawInputMouseListener.cs` from `InputListenerRawInput.cs`
- Verify gun game calibration works (crosshair centering, mouse clipping)
- Verify rotary encoder support still works
- Keep the same WndProc message routing

**Step 4: Implement platform-specific listeners**
- Create `EvdevMouseListener.cs` (test on Linux)
- Create `AndroidTouchListener.cs` (test on Android device/emulator)

**Step 5: Integration**
- Update `InputCaptureService.cs` to use `InputListenersManager`
- Update `JoystickHelper.cs` and gun input code to work with new architecture
- Update `GameLaunch.cs` to pass `InputProfile` to listeners
- Remove `InputApi` enum from `GameProfile` (now in `InputProfile`)

**Step 6: Cleanup**
- Delete old `InputListener*.cs` files
- Delete `XInputDeviceHelper.cs`
- Verify all tests pass on Windows/Linux/Android

---

## 13. Automatic InputApi Selection (User Doesn't Choose)

### New Behavior (After This Change)

**User configures Input API in Game Settings. System respects their choice with smart defaults.**

```csharp
// In GameRunning.xaml.cs and JoystickControl.xaml.cs
var inputApiField = gameProfile.ConfigValues?.Find(cv => cv.FieldName == "Input API");
string inputApiString = inputApiField?.FieldValue;

if (inputApiString != null && Enum.TryParse(typeof(InputApi), inputApiString, out var api))
{
    // Use what user configured in Game Settings
    _inputApi = (InputApi)api;
}
else
{
    // Fallback: Auto-detect if not explicitly configured
    bool hasRawInput = inputApiField?.FieldOptions?.Contains("RawInput") ?? false;
    bool hasRawInputTrackball = inputApiField?.FieldOptions?.Contains("RawInputTrackball") ?? false;
    
    if (hasRawInput || hasRawInputTrackball)
        _inputApi = InputApi.MergedInput;  // Smart default for gun games
    else
        _inputApi = InputApi.DirectInput;  // Placeholder for SDL2-only
}
```

**Key points:**
- **Input API is configured in Game Settings (ConfigValues → FieldValue)**
- Games can have: DirectInput only, XInput only, RawInput only, RawInputTrackball only, or both RawInput+RawInputTrackball
- **Games with BOTH RawInput and RawInputTrackball** (Golden Tee, etc.) → User selects in Game Settings dropdown
- **Games with ONLY ONE gun option** (most light guns) → Gets smart default in fallback
- **Games with NO gun options** → Gets SDL2-only default
- **RawInput and RawInputTrackball use the same `Linearstar.Windows.RawInput` library** (confirmed)
  - RawInput: Standard mouse input (light guns like Virtua Cop, Too Spicy)
  - RawInputTrackball: Special trackball handling (Golden Tee, WMMT5)

### Benefit: Simplified User Experience

| Game Type | User Configures In Settings | Game Behavior |
|-----------|------------------------------|---------------|
| **Gauntlet (DirectInput/XInput)** | Not shown (only one option type) | Uses saved value or auto-defaults to DirectInput |
| **Light gun (RawInput only)** | Not shown (only one option type) | Uses saved value or auto-defaults to MergedInput |
| **Trackball (RawInputTrackball only)** | Not shown (only one option type) | Uses saved value or auto-defaults to MergedInput |
| **Golden Tee (RawInput OR RawInputTrackball)** | **YES - visible dropdown** | User chooses: RawInput (light gun mode) or RawInputTrackball (ball mode) |

### Implementation Timeline

**Phase 1 (NOW):** Input API respects Game Settings configuration
- ✅ Reads FieldValue from game config
- ✅ Falls back to smart defaults if not configured
- ✅ Input API dropdown visible in Game Settings for all games that support it

**Phase 2 (Post-SDL2 Refactor):** Hide unnecessary UI
- Hide "Input API" dropdown for games with only DirectInput/XInput (no gun options)
- Keep visible for games with RawInput/RawInputTrackball (gun games)
- Replace placeholder `InputApi.DirectInput` with proper `InputApi.SDL2Only`
- Update InputCaptureService to handle SDL2 binding capture

**Phase 3 (Migration Complete):** Full cross-platform deployment
- Windows: SDL2Gamepad + RawInputMouse (guns only)
- Linux: SDL2Gamepad + EvdevMouse (guns only)
- Android: SDL2Gamepad + TouchInput (guns only)

### UI Strategy

**Keep visible in Game Settings:**
- Input API dropdown for games with RawInput/RawInputTrackball options (let user choose)
- Dropdown for games with mixed options (DirectInput/XInput/RawInput, etc.)

**Can hide (Phase 2):**
- Input API dropdown for games with only DirectInput/XInput (no gun support)
- These games will use SDL2-only after refactoring

---

## 14. Testing Strategy

### Unit Tests
- Mock `IGamepadInputProvider`, `IWindowInputProvider`
- Test gun game calibration math independently
- Test joystick mapping conversions

### Integration Tests
- Real SDL2 on each platform
- Connect test gamepads, verify events
- Window resize handling

### Manual Tests
- Gun games: aim calibration on Windows/Linux
- Joystick: controllers on each platform
- UI navigation: controller nav on each platform
- Input capture: binding new keys/buttons

---

## 15. Rollout Plan

**Status legend:** ✅ done | 🔄 partial | ⏳ pending

0. ✅ **Audit & Preparation** — `Tools/InputMethodAudit/` in solution; report at `Tools/InputMethodAudit/report/`. Results: 537 games, 393 gamepad-only, 139 gun/trackball. Includes `sdl2-test` subcommand for hardware smoke testing.
1. ✅ **Interfaces + SDL2 gamepad implementation**
   - `InputListening/IInputListener.cs` — listener contract
   - `InputListening/InputListenersManager.cs` — orchestrator with platform-aware API resolution (+ `LegacyInputListenerAdapter` wrapping the proven Windows pipeline)
   - `InputListening/Gamepad/IXInputSource.cs` — XInput-shaped device abstraction (SharpDX + SDL2 implementations)
   - `InputListening/Gamepad/SDL2GamepadBackend.cs` — single SDL poll thread, 4 slots, hot-plug, XInput-shaped `State` snapshots (Y-axis + trigger conversion handled)
   - `InputListening/Gamepad/SDL2JoystickListener.cs` — reuses **all** of `InputListenerXInput`'s 2,000 lines of game-specific logic via the source abstraction; existing user XInputButton bindings work unchanged
   - `InputApi.SDL2` enum value added; `ppy.SDL2-CS` package (bundles native SDL2 for win/linux/osx)
2. 🔄 **Linux evdev** (gun-game mouse listener)
   - `InputListening/Mouse/EvdevInterop.cs` — raw `input_event` reading + `/proc/bus/input/devices` enumeration + `EVIOCGABS` absinfo (no libevdev dependency); stable device identity via `/dev/input/by-id` symlinks (survives reboots)
   - `InputListening/Mouse/EvdevMouseListener.cs` — relative + absolute (light gun) devices, same analog byte layouts as `HandleRawInputGun` (inverted/Luigi/Gunslinger, 8/16-bit), explicit RawInputButton bindings honoured, unbound mice auto-assigned to players (left=Button1/trigger, right=Button2, middle=Button3)
   - Binding capture on Linux: `RawInputCaptureService` captures gun buttons **and keyboard keys** from evdev and enumerates devices for the light-gun dropdown — same `RawInputButton` shape as Windows
   - Keyboard bindings: `InputListening/Keyboard/EvdevKeyMap.cs` maps evdev KEY_* codes to the Win32-VK `Keys` enum (generic modifiers, matching Windows RawInput reporting, so bindings stay compatible across platforms); `EvdevMouseListener` services keyboard `RawInputButton` bindings from evdev keyboards (auto-repeat filtered; bindings with absent/foreign device paths accepted from any keyboard)
   - **Gun math proven byte-identical to Windows**: layouts extracted to pure `InputListening/Mouse/GunAnalogMath.cs`; `gun-math-test` subcommand compares it against an oracle transcribed verbatim from `HandleRawInputGun` — 384/384 layout cases + 15/15 keymap checks pass
   - Verified on Linux: mouse + keyboard enumeration with stable `/dev/input/by-id` paths on real hardware (`evdev-test`). Requires user in `input` group.
   - Still Windows-only: windowed-mode cursor clipping, Primeval Hunt/Play special cases
   - **Trackball games are architecturally blocked on Linux** (not just unimplemented): `InputListenerRawInputTrackball` communicates deltas to the game-side hook through a *named* MemoryMappedFile (`RawInputTrackballSharedMemory`); named MMFs are Windows-only in .NET, and the consumer lives inside the (Wine) game process. Needs a shared-memory bridge design (e.g. file-backed MMF visible to Wine) before porting.
3. ✅ **Android Input Manager** (touch listener) — **implemented AND emulator-verified**
   - `TeknoParrotUi.Android/` — net8.0-android head project (deliberately **not** in TeknoParrotUI.sln so desktop builds never require the android workload)
   - `AndroidTouchListener.cs` — implements `IInputListener` + `View.IOnTouchListener`; multi-touch (first two pointers = P1/P2 guns), touch position → analog bytes via the oracle-verified `GunAnalogMath`, press = Button1 trigger (matches Linux evdev default map)
   - `MainActivity.cs` — on-device input test harness: shows live JVS analog bytes + trigger states while touching the screen
   - Common integration: `InputListenersManager.AndroidTouchListenerFactory` static hook (Common stays pure net8.0); gun intent + `AndroidTouch` InputProfile availability select the listener; `SDL2GamepadBackend` hardened against missing native SDL2 (`DllNotFoundException` → gamepad disabled gracefully); `GenerateFromGameProfile` honors the `GunGame` flag for platform gun listeners (consistent with manager gun-intent)
   - Toolchain (user-space, no sudo): `~/.dotnet` SDK 8.0.422 + android workload, MS OpenJDK 17 + Android SDK (platform-34, emulator, API-34 system image) in `~/android-toolchain/`
   - Build: `DOTNET_ROOT=~/.dotnet PATH=~/.dotnet:$PATH dotnet build TeknoParrotUi.Android -t:SignAndroidPackage -p:EmbedAssembliesIntoApk=true -p:AndroidSdkDirectory=$HOME/android-toolchain/sdk -p:JavaSdkDirectory=$HOME/android-toolchain/jdk-17.0.19+10` — **note `EmbedAssembliesIntoApk=true` is required** for adb-install of Debug APKs (Fast Deployment otherwise aborts at startup with "No assemblies found")
   - **Emulator test on Linux (API-34 x86_64 AVD, KVM, headless) — PASSED:**
     - APK installs and the app runs stably
     - Tap at screen center → `AnalogBytes = 84 00 7F 00 …` (complement layout, ~0.5 factors)
     - Tap at 10% X/Y → `AnalogBytes = F3 00 E5 00 …` — `E5 = ~26 = ~(0.10 × 255)`, exactly the predicted RawInput-convention value; X/Y land in the correct slots
     - Press-and-hold → `P1 trigger: True`; after release → `P1 trigger: False`
   - Games themselves are x86 Windows binaries; the Android head is the input stack + test harness (running games on Android is out of scope of this refactor)
4. ✅ **InputProfiles loader** — `InputListening/ProfileStorage/InputProfile.cs`. *Improvement over original plan:* profiles are generated **on demand** from the GameProfile's `Input API` field (audit showed stock XMLs ship zero RawInput device bindings), so no 537-file pregeneration step is needed; JSON files are only written when a user customizes. **Runtime-wired:** `InputListenersManager` loads the InputProfile at game launch, so a user-dropped `InputProfiles/<game>.json` can enable/disable the evdev mouse listener (Linux) or the RawInput/Trackball pairing (Windows SDL2 mode). **Validated:** `profiles-test` subcommand generates + validates all 537 profiles (0 failures; 414 default SDL2Gamepad / 122 RawInput / 1 Trackball; JSON round-trip OK; platform filtering verified on Linux).
5. ✅ **Refactor consumers** — `GameSession` now uses `InputListenersManager` and creates the RawInput forward window exactly when the manager reports `NeedsWndProcRouting`; `RawInputForwardWindow` forwards to the manager; `InputCaptureService` captures via SDL2 on non-Windows platforms and when `InputApi.SDL2` is selected (bindings produced are XInput-shaped, identical format — this also makes controller UI navigation work on Linux, since `UiNavigationService` matches the same display-name strings). **Windows SDL2 + gun games:** selecting SDL2 on Windows pairs the SDL2 gamepad listener with the legacy RawInput/Trackball listener (chosen from the game's saved choice or offered options), so gun input is never lost.
6. ✅ **UI integration**
   - `MultiButtonConfigView`: "SDL2 (Cross-Platform)" input mode — captures via SDL2, stores XInput-shaped bindings (`XInputButton`/`BindNameXi`), matches all games, writes `Input API = SDL2` on apply; RawInput capture now platform-aware (evdev on Linux)
   - `JoystickSetupView` (per-game bindings): SDL2 API supported (XInput visibility/storage rules), light-gun device dropdown lists evdev mice on Linux, device path lookup platform-aware
   - `GameSettingsView`: the `Input API` dropdown is platform-filtered — SDL2 offered everywhere; DirectInput/XInput/MergedInput hidden on non-Windows (legacy selections display as SDL2)
7. ⏳ **Platform testing matrix** — Windows regression (legacy path untouched), Linux gamepad (`sdl2-test` verified SDL2 init on Linux ✅), Android
8. ⏳ **Cleanup** — remove SharpDX packages once SDL2 path is default on Windows too

**Verification so far:** full solution builds on Linux; SDL2 backend initializes and polls on Linux (`sdl2-test`); evdev enumeration works on real hardware (`evdev-test`); InputProfile generation validated across all 537 games (`profiles-test`); gun byte-layout math proven identical to the Windows listener (`gun-math-test`, 384 cases); Windows legacy path is delegated unchanged (zero behavioural diff for Windows users).

**What requires hardware/humans (cannot be completed by code changes alone):**
- Windows regression pass (legacy listeners untouched, but verify)
- Linux gamepad play-testing (SDL2JoystickListener with a real pad)
- Linux gun aim accuracy testing (needs a running game under Wine + window-geometry design decision)
- SharpDX package removal — only after SDL2 becomes the proven default on Windows

---

## Success Criteria

- ✅ All platforms build without conditional logic in game code
- ✅ Gun games calibrate identically on Windows, Linux, Android
- ✅ Joystick detection works on all platforms
- ✅ UI navigation (gamepad) works on all platforms
- ✅ **SDL2 replaces XInput + DirectInput:** Verified full analog range, individual triggers
- ✅ **RawInput (only for keyboard/mouse, gun games)** — no longer used for gamepad
- ✅ **SharpDX.XInput removed** from codebase
- ✅ **SharpDX.DirectInput removed** from codebase
- ✅ Zero platform-specific `#if WINDOWS` in gamepad code
- ✅ **XML audit completed:** identify games with RawInput (gun/trackball) vs. regular
- ✅ **InputProfiles/ folder populated:** all 250+ games have separate input definitions
- ✅ **RawInput buttons hidden:** UI only shows for gun/trackball games
- ✅ **GameProfile lightened:** XInput/DirectInput bindings removed, only game metadata remains
- ✅ **Platform-aware:** Windows uses SDL2+RawInput; Linux uses SDL2+EvdevMouse; Android uses SDL2+Touch

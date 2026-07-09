# Cross-Platform Input — Testing Checklist

Companion to CROSSPLATFORM_INPUT_REFACTOR_PLAN.md (§15 rollout).
Mark items: `[x]` works, `[!]` broken (add notes below the item).

**Automated suites (run these first, both platforms):**

```bash
dotnet run --project Tools/InputMethodAudit -- gun-math-test   # 384 layout cases + keymap
dotnet run --project Tools/InputMethodAudit -- profiles-test   # InputProfile generation, 537 games
dotnet run --project Tools/InputMethodAudit -- sdl2-test       # live SDL2 gamepad state (15 s)
dotnet run --project Tools/InputMethodAudit -- evdev-test      # Linux only: device enum + event stream (10 s)
```

---

## Windows — SDL2 gamepad (the only gamepad path, 2026-07-09)

XInput/DirectInput listeners and SharpDX are **removed**. Every gamepad API
selection (including legacy XInput/DirectInput saves) runs the SDL2 listener,
which reads the same XInputButton bindings. DirectInput-only bindings are dead
— the launch console warns and asks for a rebind.

### Gamepad (SDL2)
- [ ] Game with legacy `Input API = XInput` save: SDL2 listener starts (console: "gamepads via SDL2"), existing bindings work in-game
- [ ] Game with legacy `Input API = DirectInput` save + only DI bindings: console warns "only has old DirectInput bindings", rebind in Controller Setup fixes it
- [ ] Fresh SDL2 bindings work in-game (buttons, sticks, independent triggers)
- [ ] Hot-plug: connect pad after game launch (SDL2 respawner picks it up ≤ 5 s)
- [ ] sto0z driving hack + Stooz percent still applies (wheel games)
- [ ] Game-specific logic: WMMT5/6 gear shifting, Initial D steering, MKDX test-button toggle, rotary encoder games

### Gun games (RawInput — kept, uses RawInput.Sharp)
- [ ] `Input API = RawInput` light gun game (e.g. 2Spicy, Virtua Cop): aim tracks mouse, trigger/buttons fire, crosshair centered at boot; SDL2 gamepad listener runs alongside
- [ ] Windowed mode: cursor clipping to game window; Ctrl releases clip
- [ ] `Input API = RawInputTrackball` (Golden Tee): trackball deltas reach the game (named MMF path)
- [ ] `MergedInput`: SDL2 gamepad + RawInput mouse/keyboard simultaneously
- [ ] Two-mouse / two-gun setup: per-device bindings route to correct player
- [ ] Keyboard RawInput bindings (Test/Service/coin keys) work
- [ ] Inverted-axis game (`InvertedMouseAxis`), Luigi's Mansion, Gunslinger Stratos 3 layouts
- [ ] 16-bit analog gun game (`Use16BitAnalog`)

### UI
- [ ] Game Settings Input API dropdown offers SDL2 (+ RawInput flavours where the game supports them); no DirectInput/XInput entries
- [ ] Multi-game button config modes: Merged Input (Gamepad + Gun) / SDL2 Gamepad / RawInput
- [ ] Per-game bindings view (`JoystickSetupView`) captures via SDL2 for every gamepad selection
- [ ] Controller UI navigation still works (SDL2 capture)

## Linux

**Prerequisite (keyboard + most mice/guns): user must be in the `input` group:**
`sudo usermod -aG input $USER` then log out/in. Without it keyboards are always
unreadable; some mice still work through vendor udev ACLs (e.g. Razer), which
makes it look like "mouse works but keyboard doesn't". The launch console,
Controller Setup and `evdev-test` all surface this with an explicit warning now.

### Gamepad (SDL2 — the only gamepad path)
- [ ] `sdl2-test` shows connected pad state (buttons/axes/triggers move)
- [ ] Games with any legacy API selection transparently use SDL2 (log: "not available on this platform, using SDL2")
- [ ] Binding capture works in UI (all modes fall back to SDL2 capture)
- [ ] Bindings persist and match in-game behaviour
- [ ] Hot-plug pad after launch
- [ ] 4 pads simultaneously (slot assignment stable)
- [ ] sto0z + game-specific logic (WMMT gears, Initial D...) — same code path as XInput, verify once in-game
- [ ] Controller UI navigation works
- [ ] App runs without pad connected (no crash, no SDL errors)

### Gun games (evdev)
- [ ] User is in `input` group (`groups | grep input`) — required for /dev/input access
- [ ] `evdev-test` lists mice with stable `/dev/input/by-id` paths and streams motion/button events
- [ ] Gun game launch starts EvdevMouse listener alongside SDL2 (RawInput/Merged/Trackball selection or GunGame flag)
- [ ] Unbound mouse defaults: first mouse = P1, aim moves analogs, left=Button1/trigger, right=Button2, middle=Button3
- [ ] Explicit device binding: light-gun dropdown lists evdev mice; binding survives reboot (by-id path)
- [ ] Two mice = two guns (P1/P2 device assignment)
- [ ] Absolute-position device (Sinden/Gun4IR-style, or graphics tablet as stand-in): absolute aim maps via absinfo range
- [ ] Keyboard bindings (Test/Service/coin) via evdev keyboard; auto-repeat does not spam
- [ ] Gun button capture in UI (RawInput mode) captures evdev mouse buttons and keyboard keys; Escape cancels
- [ ] Inverted-axis / Luigi / Gunslinger layout games (math is oracle-verified; spot-check one in-game)
- [ ] User InputProfile override: drop `InputProfiles/<game>.json` with `EvdevMouse.Enabled=false` → listener not started

### Known Linux limitations (expected failures — do not file as bugs)
- Windowed-mode cursor clipping (design pending; fullscreen path only)
- Trackball games (blocked: named-MMF bridge to the game-side hook)
- Primeval Hunt split-screen / Play canvas special cases

## Android (emulator or device)

See [TeknoParrotUi.Android/README.md](TeknoParrotUi.Android/README.md); automated: `scripts/deploy-and-test.sh`.
- [ ] Harness launches, shows live analog bytes
- [ ] Touch aim: center ≈ `7F/80`; corners map correctly (complement layout)
- [ ] Trigger True while held, False on release
- [ ] Two-finger = P1+P2 simultaneously
- [ ] Rotation/resize keeps normalization correct

## Cross-platform binding compatibility
- [ ] Bindings captured on Windows (XInput mode) work on Linux under SDL2 (same XInputButton storage)
- [ ] Bindings captured on Linux (SDL2) work on Windows in XInput and SDL2 modes
- [ ] UserProfiles XML round-trips across platforms (no serializer differences)

## Sign-off gates
- [x] SharpDX removal — **done 2026-07-09**: SDL2 is the only gamepad backend on all platforms; RawInput (Windows) / evdev (Linux) / touch (Android) serve gun games

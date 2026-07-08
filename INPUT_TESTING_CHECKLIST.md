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

## Windows — regression (legacy path must be byte-identical)

The legacy pipeline (`InputListener` + XInput/DirectInput/RawInput listeners)
is delegated **unchanged** for all legacy API selections. These tests confirm
nothing regressed via the new `InputListenersManager` wrapper.

### Gamepad (legacy APIs)
- [ ] Game with `Input API = XInput`: all 4 pads poll, bindings work in-game
- [ ] Game with `Input API = DirectInput`: pad + keyboard bindings work
- [ ] `MergedInput`: XInput pad and DirectInput device work simultaneously, no double-input from XInput pads via DirectInput
- [ ] Hot-plug: connect pad after game launch (XInput respawner picks it up ≤ 5 s)
- [ ] sto0z driving hack + Stooz percent still applies (wheel games)
- [ ] Game-specific logic: WMMT5/6 gear shifting, Initial D steering, MKDX test-button toggle, rotary encoder games

### Gun games (legacy RawInput)
- [ ] `Input API = RawInput` light gun game (e.g. 2Spicy, Virtua Cop): aim tracks mouse, trigger/buttons fire, crosshair centered at boot
- [ ] Windowed mode: cursor clipping to game window; Ctrl releases clip
- [ ] `Input API = RawInputTrackball` (Golden Tee): trackball deltas reach the game (named MMF path)
- [ ] Two-mouse / two-gun setup: per-device bindings route to correct player
- [ ] Keyboard RawInput bindings (Test/Service/coin keys) work
- [ ] Inverted-axis game (`InvertedMouseAxis`), Luigi's Mansion, Gunslinger Stratos 3 layouts
- [ ] 16-bit analog gun game (`Use16BitAnalog`)

### New on Windows: SDL2 mode
- [ ] Select `Input API = SDL2` in Game Settings (dropdown shows SDL2 on every game)
- [ ] SDL2 gamepad: bindings captured in SDL2 mode work in-game; existing XInput bindings work unchanged under SDL2
- [ ] Independent left/right triggers + full stick ranges under SDL2
- [ ] Hot-plug under SDL2 (respawner, ≤ 5 s)
- [ ] **SDL2 + gun game**: RawInput mouse listener auto-pairs (aim + trigger still work while pads run through SDL2); WndProc forward window created
- [ ] SDL2 + trackball game: trackball listener auto-pairs (saved `RawInputTrackball` choice respected)
- [ ] Multi-game button config: "SDL2 (Cross-Platform)" mode captures and applies; sets `Input API = SDL2` on apply
- [ ] Per-game bindings view (`JoystickSetupView`) works in SDL2 mode (XInput-shaped storage)
- [ ] Controller UI navigation still works (MergedInput capture)

## Linux

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
- [ ] All Windows regression items pass → SDL2 may become the recommended default on Windows
- [ ] Windows SDL2 default proven stable for a release cycle → begin SharpDX removal (final cleanup phase)

# TeknoParrot Avalonia — User Testing Checklist

Mark items as tested: `[x]` works, `[!]` broken (add notes below the item).

## Startup & First Run
- [ ] Privacy policy dialog appears on first run; Accept continues, Quit exits
- [ ] Setup wizard appears on first run (Welcome → DAT/XML → Scan → Controls → Account → Serial → Complete)
- [ ] Wizard DAT/XML step: browse for file, download link opens, Skip works
- [ ] Wizard opens Game Scanner / Button Config / Account / Subscription and returns to wizard on Back
- [ ] Wizard Finish → never shows again on next launch
- [ ] App remembers window state; "Start in fullscreen" option works
- [ ] Exe/window/taskbar icon shows correctly
- [ ] Title bar shows version number (AppVeyor-patched builds)
- [ ] Header badge shows "⭐ Subscribed" vs "Free" correctly

## Library
- [ ] Installed games list with icons (icons download on demand, honors Download icons setting)
- [ ] Category dropdown (All, Installed, Not Installed, Subscription, System 246/256, System 357, Triforce, genres)
- [ ] Search filter
- [ ] Selected game is remembered when navigating away and back
- [ ] Metadata panel: emulator + arch with homepage link, platform, release year, wheel rotation, supported versions, TPO version, general issues
- [ ] GPU compatibility line (NVIDIA/AMD/Intel) with issue tooltips
- [ ] Add Game: full catalog, genre filter, "✓ Added" markers, add → opens Game Settings
- [ ] Remove Game (with confirmation)
- [ ] Game Scanner: scan folder against DAT, adds recognized games
- [ ] Verify Files against DAT

## Game Launching (native pipeline — previously classic exe)
- [ ] Loader-based games launch (OpenParrot x86/x64, TeknoParrot core, Lindbergh, ElfLdr2, N2, Konami)
- [ ] Test Menu launch (games with separate test mode)
- [ ] Game Running view shows console output; Force Quit works
- [ ] After game exits, returns to library with same game selected
- [ ] Dolphin/Triforce games launch (ini configured, fullscreen/windowed)
- [ ] Play! games launch (config.xml written)
- [ ] RPCS3 games launch (config.yml, GUI settings, HDD serial fix)
- [ ] PCSX2 games launch (PCSX2.ini, AVX2 variant option)
- [ ] Cxbx-Reloaded games launch (BIOS check, region patch, child process tracked)
- [ ] SegaTools IDZ launches (segatools.ini generated, minime/amdaemon/ServerBox boot)
- [ ] Per-game hacks still work (EXVS/MKDX kills, Tekken 7 INI, DPI flags, OpenSSL fix...)
- [ ] CLI: `TeknoParrotUi.exe --profile=X.xml` launches game directly and exits after
- [ ] CLI: `--profile=X.xml --test` opens test menu
- [ ] CLI: `--emuonly` runs emulation layer only (developers), window stays open
- [ ] CLI: `--startMinimized`

## Controls
- [ ] Per-game control setup: XInput capture (buttons, sticks, triggers)
- [ ] DirectInput capture (controllers AND keyboards, POV hats, axes)
- [ ] RawInput capture (per-device keyboard/mouse buttons, Escape cancels)
- [ ] MergedInput capture (XInput + DirectInput + RawInput mice, no double-detection)
- [ ] Lightgun/trackball device dropdown lists real devices (AimTrak/Sinden/DolphinBar named correctly)
- [ ] "Windows Mouse Cursor" / "None" / "Unknown Device" options save correctly
- [ ] Unplugged device stays selectable in dropdown
- [ ] Saved bindings actually work in-game (all APIs)
- [ ] Multi-Game Button Config: input mode switch (Merged/DI/XI/RI)
- [ ] MGBC: game category filter, search, select all/none, selection survives filtering
- [ ] MGBC: bind once → Apply to Selected Games (only supported APIs written, Input API set)
- [ ] MGBC: named profiles save/load/delete (compatible with old UserProfiles\Profiles)
- [ ] MGBC: Copy From Game, Reset to Default, unsaved-changes prompts
- [ ] MGBC: lightgun device dropdowns

## Game Settings
- [ ] Config values editor (dropdowns, checkboxes, text)
- [ ] Game executable path picker (and second exe where applicable)
- [ ] Monitor selection editor
- [ ] Settings persist after save

## TeknoParrot Online
- [ ] TPO page loads in the internal browser (Online nav)
- [ ] Login works and persists
- [ ] Create/join room; game launches via site button (second instance with TP_TPONLINE2)
- [ ] Game exit notifies the site (room state resets, no ghost lobby)
- [ ] External links (Discord invites) open in system browser
- [ ] `tponline://` deep links open the app into the room (Discord invite flow)
- [ ] TPO CLI: `--tponline --game=X --room=Y --action=create|join`
- [ ] Login-bounce notice appears when joining while logged out

## Updates
- [ ] Check for Updates lists all components with local/online versions
- [ ] Update single component (both folderOverride ones like OpenSegaAPI and plain ones like OpenParrotWin32/x64)
- [ ] Update All
- [ ] UI self-update via ParrotPatcher (cache zip → patcher extracts after exit → restarts UI)
- [ ] ParrotPatcher log/output readable; cleans cache; restarts UI

## Account & Subscription
- [ ] OAuth login via system browser (Account page), token persists across restarts
- [ ] Logout
- [ ] Subscription: serial key masked with 👁 reveal toggle when registered
- [ ] Register serial (BudgieLoader output shown); Deactivate
- [ ] "N subscription game(s) available" opens the game list prompt
- [ ] Header badge updates after register/deactivate

## Settings
- [ ] Hotkeys (exit/pause/score keys) capture and work in-game
- [ ] Language selection persists (translations themselves TBD)
- [ ] DAT file picker
- [ ] StoOz driving hack + percent
- [ ] ElfLdr2 network adapter dropdown
- [ ] Dolphin hide-GUI toggle
- [ ] Vanguard warning toggle
- [ ] Settings save/persist

## UI Options (new)
- [ ] F11 / Alt+Enter fullscreen toggle
- [ ] Start-in-fullscreen option
- [ ] Controller UI navigation: bind Up/Down/Left/Right/Confirm/Back/Toggle Fullscreen via merged input
- [ ] Navigation works across all pages (lists scroll, buttons click, combos open)
- [ ] Navigation suspends while binding controls (no capture conflicts)

## Mods & Misc
- [ ] Mods browser lists and downloads mods
- [ ] UI usable at 640x480
- [ ] UI navigation via hamburger/sidebar on all pages
- [ ] No stutter/GPU issues on low-end machines

## Distribution
- [ ] Published build: clean root (exe + libs\ folder), app runs from fresh unzip
- [ ] ParrotPatcher runs from published layout
- [ ] Works on machine WITHOUT .NET 8 installed → clear error/prompt to install .NET 8 Desktop Runtime
- [ ] Works on machine without WebView2 runtime (TPO shows install hint, rest of app fine)
- [ ] AppVeyor build produces working artifact with patched version number

## Linux (experimental)
- [ ] UI starts under Linux (X11/Wayland)
- [ ] Library/profiles/settings readable and writable
- [ ] TPO browser with WPE WebKit libs installed
- [ ] ParrotPatcher runs

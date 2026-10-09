# Type Zero validation

Build the TPUI solution in Debug/x86, then run `tests/TypeZero/run.ps1`.
The contract check isolates its shared page and covers publication, lease revocation,
restart, neutral controls, train notch selection, Stunt steering, revision routing,
argument quoting and seven XML profiles / nine game revisions. It also resolves the
five Type Zero revisions and seven TPOnline room/view IDs using isolated stock/user profiles,
checks saved controls and offline settings remain intact, and rejects offline-only
games. The older Battle Gear 2 conversion disables the v2.04-only rumble and
wheel force-feedback hooks for that launch, preserving the saved offline setup.

The TTZIN publisher uses the managed memory-mapped accessor with aligned UInt32
sequence stores and explicit memory barriers. Common builds with unsafe blocks
disabled. Contracts cover interrupted publication recovery, sequence wrap and
50,000 updates racing a reader that rejects incomplete publications.

Power Shovel v2.07J and Raizin Ping Pong v2.01O use two-player shared-cabinet
sessions. Both participants bind their local Player 1 controls; TeknoTZero maps
the joining player's controls to Player 2. Power Shovel's two local levers are
both part of Player 1. TPOnline automatically locks the game to two-player mode,
so the one-player choice is disabled. Offline selection is unchanged.
Use the same emulator build, sound mode and rendering settings on both peers.
The host supplies temporary cabinet state; neither player's local save is changed.
Local pause, reset, save/load and fast-forward are unavailable during the session.
These rooms require the emulator's shared-cabinet UDP implementation; adding the
UI profiles alone does not make older emulator builds compatible.

All seven profiles launch Vulkan with 1-8x internal resolution, widescreen,
stereo SBS/OpenXR and custom/Lottes CRT options. Contracts check renderer
migration, invalid option rejection and Battle Gear 2 v2.04 wheel/rumble routing,
including per-effect gains and spring mode.

For a live integration check, create a fresh directory with the built emulator
package under `TeknoTZero/` and a copy of initialized Battle Gear saves under
`TeknoTZero/nvram/batlgear/`. Run `run-live.ps1` with `-StagedUiRoot`, `-RomRoot`
and `-ChdRoot`. Do not run another game or benchmark at the same time.

The live test uses the production launcher and TTZIN writer, injects coin/Start
and accelerator controls, and checks that the emulator recorded those inputs.
It uses persistence-safe mode and a new `live-result/` directory. Captures must
be reviewed separately to confirm the game reached a race. This exercises the
launcher/writer boundary; physical device binding and WPF interaction still need
their own acceptance.
# Raizin switch-test scenario

`run-live.ps1 -Game raizpin` uses an isolated staged emulator and Raizin save
seed to exercise native Switch Test through the production launcher and writer.
It records six sensor axes, both players' Start/Select, endpoint/intermediate
values and return to neutral. Inspect all four captures after it completes;
input-recording assertions alone are not visual acceptance. The default game
remains `batlgear`. This does not calibrate a physical paddle or complete a rally.

## Densha coin-input scenario

`run-live.ps1 -Game dendego3` holds each of two coin presses for 90 guest frames,
then sends Start and a power-notch input. Use an isolated initialized train save
and review `diagnostics.log` for finite board coin pulses and the captures for
successful game startup. The input journal itself records the original held
controls, before the emulator's pulse shaping.

## Personal widescreen room modes

`pwrshovl_personal_tz` and `raizpin_personal_tz` resolve to the existing base
profiles with temporary `Player View=personal` and `Widescreen=16:9` overrides.
The shared room IDs explicitly select `shared`. Both modes retain saved controls,
media paths and internal resolution. Tests cover both player IDs, saved-profile
isolation, supported revisions and rejection of unsupported personal-view IDs.
The emulator keeps menus shared and selects each player's reprojected camera
during two-player gameplay; UDP handoff and input ownership stay unchanged.

Local TPO contracts cover Power Shovel and Raizin's two seats and both view
choices, a custom broker endpoint, invalid room settings, service-handoff
precedence and unchanged offline aspect preferences. Local TPO requires a
running compatible UDP broker. Type Zero shared-cabinet rollback is automatic;
Battle Gear continues to use its native link protocol.

Launches contain only `--section.key=value` arguments and the game identifier.
No global/game INIs are required or created. Run with `-ExportArguments DIR` to
export all nine revision command lines, then pass DIR to the emulator
`command_line_tests` binary to validate them against its compiled schema.

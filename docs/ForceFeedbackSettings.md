# Force Feedback Settings

Open Game Settings, then Settings beside Force Feedback Device. Save settings persists the page; Back returns to Game Settings and discards unsaved feedback edits. Controller Settings has no feedback button.

The XML schema is reusable across emulators. Put `SettingsPage` = `ForceFeedback` on fields that belong on this page. `EnabledBy` names a Bool field on the same page; it becomes an inline Enabled checkbox. Keep device selectors in Game Settings. A device selector and at least one page field are required to show the Settings button. Player 2/3 device selectors open the same page.

All effect enables default to `1`, all strength sliders to `100`, with a range of 0 through 100. Existing overall strength and device field names remain unchanged so profile upgrades preserve saved selections and values. No inversion checkbox is added.

## Supported effects

| Emulator | Game effects exposed |
| --- | --- |
| TeknoModel2 | Existing per-game constant, spring, friction and sine controls |
| TeknoModel1 | Virtua Racing / Virtua Formula: spring, constant, friction |
| TeknoVegas, TeknoGClub, TeknoVUnit, TeknoCobra | Steering torque through constant force |
| TeknoViper, TeknoHornet, TeknoHNG64, TeknoZeus | Constant steering force on driving games; recoil on supported gun games |

Gun games have independent recoil enables/strengths for each supported player. HNG64 retains its existing per-player overall strengths. Other gun games retain their shared overall slider.

Spring defaults to the wheel's native Spring effect. **Spring using Constant Force** recreates the spring requested by the game's board from steering position. Neither mode adds an always-on spring. Model 1 also preserves game-requested uncentering. Constant-force gain does not disable spring emulation. The old saved label `Constant Spring` remains accepted.

Games that supply only a combined steering torque have no separate spring, friction, or sine commands to adjust, so their menus expose only overall and constant strength. Unsupported Model 1 games do not get invented effects.

## Launcher options

`ForceFeedbackArguments` emits only declared effects supported by its caller. The checkbox maps to gain zero without erasing its saved slider value. New per-effect flags use percentages: `--ffb-constant-gain`, `--ffb-spring-gain`, `--ffb-friction-gain`, `--ffb-recoil-gain`. Recoil players 2/3 use `-p2` / `-p3` suffixes. Only supported channels are emitted per emulator. Model 1 spring mode uses `--ffb-spring-mode spring|constant`. Model 2 retains its sine strength/period options.

Existing overall-gain conventions remain unchanged, including Vegas's 0..1 `--ffb-gain` and Zeus's `--ffb-strength`. New effect gains default to full strength if omitted. Gain scales output after gamepad cue classification; Vegas and Zeus retain their existing overall response/clipping and apply the new constant-effect slider afterward.

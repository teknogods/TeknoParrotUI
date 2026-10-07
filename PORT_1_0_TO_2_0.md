# Committed 1.0 port into 2.0

Source: sibling TeknoParrotUI_old, master revision
`bf5a7b67ef238677b0df3a0c5cafd08d37eac072`.
The live remote master reference was checked and matches this revision.
Previous source baseline: `6657cff1`.

Only committed source changes are included. The source working tree is untouched.
Uncommitted webcam/language experiments, Steam presence, TpOnline credential
experiments, and their profile changes are excluded. Historical profile-backups
are archived snapshots rather than runtime changes.

| Source changes | 2.0 integration |
| --- | --- |
| 1,382 changed/new profiles, 1,409 metadata files, 1,035 new games | Committed catalog imported; all 2,791 changed catalog files compared structurally with the pinned source, zero mismatches |
| HDrive, Magic, Model 3, MVS, CPS, System 32 | Emulator types, native launchers, cabinet pipes, test menu handling, output routing, package discovery and patcher entries |
| Arcade revision families | Library grouping, Add Game duplicate prevention, settings selection and exact TPO room revision resolution; 2.0 bindings and platform preferences preserved |
| Namco revisions and cabinet input | Revision choices, force feedback, renamed-control migration, independent cabinet axes, player 3/4 contacts, test toggles and native LAN arguments |
| Input fixes and new mappings | SDL3 mapper, keyboard/raw mouse mapping, high-resolution gun axes, rotary/trackball publication, player 3 trackball setup and chat/system buttons; Linux host adapters updated |
| Model 1/2/3 and other native settings | Updated launcher arguments for networking, video, scaling, VR, draw distance, force feedback and score submission |
| Initial D Online | Shared identity/API helpers, old-loader migration, unified network policy, registration prompts, friends codes, LAN presence/consent/firewall flow, per-launch environment and live/exit status |
| Kizuna Online | Separate cabinet credentials, API registration/use here/regeneration/removal, manual entry, rank display, and registration requirement before library launch |
| Golden Tee 2020 and GT on TP | Catalog, account PCB/card auto-fill, tool launcher and updater component |
| CPS media preparation | Asynchronous TPO bridge, progress, cancellation, timeout, diagnostics and offline launch progress |
| News history | History endpoint, dated article selection, older/newer navigation and debug testing in the shared Windows/Linux/Android view |
| UI and updater changes | Genre/platform filtering, terminal launch action, GPU status details, account cards/rank display, selected updates/select all, 30-second cache and removal of post-update changelog |
| Translations and troubleshooting | Committed resource changes merged by key; online secrets and cabinet identifiers masked in reports |

The WPF presentation code is implemented through Avalonia controls and shared
services. Retired DirectInput code is represented by the existing SDL3 mapper;
the old WPF frontend and its obsolete dependencies are not restored.

## Verification

- Windows Release solution build.
- Android ARM64 Release build (generated AIDL binding warnings; no errors).
- Linux x64 publish.
- All 1,967 stock XML profiles loaded; 1,890 visible in Release.
- PortParityTests: 17 Namco online/manual launches and 38 room revisions.
- 287 MVS, 641 CPS and 67 System 32 profiles: cabinet, media, operator menu,
  online modes, publication flags, input and presentation checks.
- 23,794 digital bindings through the actual SDL3 mapper into the cabinet bridge.
- 99 Model 1/2/3, HDrive, Magic and Vegas launcher configurations.
- CPS preparation pipe drainage, timeout, owned-process cancellation and diagnostics.
- 20 news parsing/transport tests and 25 Avalonia integration checks.
- Existing SDL3 lifecycle/compatibility, profile migration and gun math checks.

These are build and regression checks. Physical gameplay, live online-account
mutations and Linux device behavior are separate runtime acceptance tests.
No changes have been pushed.

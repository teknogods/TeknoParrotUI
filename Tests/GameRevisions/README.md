# Arcade revision checks

Build the Release/x86 Common assembly, then run:

```powershell
.\Tests\GameRevisions\run.ps1
```

Use `-CommonAssembly PATH` for an isolated build. These checks need a Release
assembly because they verify normal developer-profile filtering as well as the
explicit `TPUI_SHOW_DEVONLY_PROFILES` override. No emulator, ROMs, networking or
real user profile writes are used.

The suite checks all 995 stock variants and their 275 authoritative families,
including exact input mappings, axes, operator settings, metadata and eligibility.
It also checks revision-specific DIP defaults, edited bindings and media paths
through repeated switches, existing saved child profiles, migrated root profiles,
invalid selections, root Test capability, clone-only libraries, and exact online
room selection when only a family profile is configured. Repeated launch-profile
construction must be idempotent and detached from saved/edited objects.

The helper keeps in-memory edits for each revision while a settings object lives.
It never writes or deletes profiles. The existing settings UI remains responsible
for explicit saving; existing separately saved variant files are read as fallbacks
and remain untouched. Cross-revision fallback shares host settings and compatible
bindings, while stock hardware/DIP defaults remain specific to the selected game.

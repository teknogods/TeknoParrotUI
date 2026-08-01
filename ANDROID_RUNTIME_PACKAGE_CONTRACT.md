# TeknoParrot Android delivery contract

TeknoParrotUI and its Winlator companion are thin APKs. They must not embed
OpenParrot, TeknoParrot, ElfLoader2, CXBXR, or PCSX2X6 runtime payloads.
The first public Android release installs the two APK companions separately and
downloads OpenParrot from the existing OpenParrot releases.

## Updater service fields

The platform-specific update endpoint keeps the existing component/release
envelope. Every release asset used for Android installation must provide:

- `name`: immutable asset filename.
- `browser_download_url`: HTTPS URL.
- `size`: authoritative byte length, greater than zero.
- `digest`: `sha256:` followed by exactly 64 hexadecimal characters.

Android refuses an asset if a required field is missing, the response length
differs from `size`, or the downloaded digest differs from `digest`.

## First-release components and assets

| Updater key/tag | Delivery | Exact asset contract |
|---|---|---|
| `TeknoParrotUI-android` | TPUI APK from `teknogods/TeknoParrotUI` | `TeknoParrotUi-*-android-arm64.apk` |
| `winlator` | Winlator APK from `ReaverTeknoGods/winlator` | `TeknoParrotWinlator-*-android-arm64.apk` |
| `pcsx2x6-android` | PCSX2X6 APK from `ReaverTeknoGods/pcsx2x6` | `pcsx2x6-*-android-arm64.apk` |
| `teknodolphin-android` | TeknoDolphin APK from `ReaverTeknoGods/CrediarDolphin` | `teknodolphin-*-android-arm64.apk` |
| `OpenParrotWin32` | Shared OpenParrot x86 archive | `OpenParrotWin32.zip` |
| `OpenParrotx64` | Shared OpenParrot x64 archive | `OpenParrotx64.zip` |

The OpenParrot assets are exactly the same ZIP files used by Windows and Linux.
There is no `*-android.zip`, no republished Android OpenParrot package, and no
second set of OpenParrot binaries. Exact-name matching prevents similarly named
or stale release artifacts from being selected.

The release `name` remains the four-part component version used by the current
updater comparison logic. TeknoParrot, ElfLoader2, and CXBXR delivery is
intentionally deferred beyond the OpenParrot-first release.

## Private Android installation envelope

The standard OpenParrot ZIPs are flat archives. Windows and Linux extract them
with the existing desktop updater. On Android, TPUI first downloads and verifies
that same published ZIP, then converts it inside TPUI's private cache into the
manifest/payload envelope required by Winlator:

```text
teknoparrot-package.json
payload/OpenParrotWin32/OpenParrot.dll
payload/OpenParrotWin32/OpenParrotLoader.exe
...
```

The x64 archive uses `payload/OpenParrotWin64/`. This conversion is local
installation plumbing; the generated envelope is never published or committed.
The source ZIP must remain flat, contain unique safe filenames, and contain the
expected core and loader:

| Package id | Required files | Winlator runtime root |
|---|---|---|
| `OpenParrotWin32` | `OpenParrot.dll`, `OpenParrotLoader.exe` | `OpenParrotWin32/` |
| `OpenParrotx64` | `OpenParrot64.dll`, `OpenParrotLoader64.exe` | `OpenParrotWin64/` |

Manifest schema version 1:

```json
{
  "schemaVersion": 1,
  "packageId": "OpenParrotWin32",
  "platform": "android",
  "version": "1.0.0.123",
  "files": [
    {
      "path": "OpenParrotWin32/OpenParrot.dll",
      "size": 123456,
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
    }
  ]
}
```

Every non-directory payload entry appears exactly once in `files`, and every
listed file exists exactly once. Backslashes, nesting in the shared source ZIP,
absolute paths, parent segments, duplicates, links, unlisted files, and files
outside the package-owned root are rejected.

`Tools/New-RuntimePackage.ps1` remains available for future runtime modules that
may require a native manifest package. It must not be used to republish the two
OpenParrot ZIPs.

## Android installation boundary

TPUI:

1. downloads the exact shared release asset into private cache;
2. verifies its authoritative GitHub/service size and SHA256;
3. validates and locally adapts the flat ZIP;
4. hashes the generated installation envelope; and
5. sends a read-only file descriptor and envelope digest to the
   signature-protected Winlator service.

Winlator verifies the transferred envelope digest and then validates the
manifest identity, version, roots, file list, sizes, and per-file hashes. It
extracts only into private staging, swaps only roots owned by that package with
rollback on failure, and records the installed version in private storage.
Installation is refused while a game or activation operation is active.

PCSX2X6, TeknoDolphin, and Winlator remain separate APK updates and are never
copied into another APK. TeknoParrotUI and its companions use the same
production signing certificate because their Android bridges are protected by
a signature permission.

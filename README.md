# TeknoParrotUI

Open Source JVS / other I/O emulator with Windows and Linux frontends. The experimental Android ARM build now includes a Winlator-backed launch path for the first validated x86 game profiles; broader profile conversion, full JVS publication, and lifecycle qualification are still in progress. Works in collaboration with [TeknoParrot](https://teknoparrot.com) and [OpenParrot](https://github.com/teknogods/OpenParrot).

[TeknoParrot Discord](https://discord.gg/kmWgGDe), development discussion is in the ``#openparrot-dev`` channel.

## Importing 1.0 button bindings

In 2.0, open **Settings → Input → Import 1.0 Button Bindings** and select the 1.0 installation folder or its `UserProfiles` folder. The importer matches games by XML filename and buttons by input mapping, then writes missing bindings to `InputBindings/<game>.json`. Existing 2.0 bindings and game settings are preserved.

XInput bindings carry over to SDL's normalized controller layout. To convert DirectInput bindings, connect the controller and choose its current SDL device for each legacy DirectInput GUID. Verify button and axis order after importing, since different drivers can enumerate physical controls differently. Windows RawInput mouse paths only carry over on Windows; keyboard keys can also move to Linux. Android's Winlator controls editor uses a separate layout and is not populated by this importer.

## Notes for contributors

When adding a new GameProfile, create a metadata file and fill in as much details as possible.

If possible, also add the game's icon to the [Icons](https://github.com/teknogods/TeknoParrotUIThumbnails/tree/master/Icons) repository.

When updating a GameProfile, increment the ``GameProfileRevision``, otherwise existing user profiles will not be updated, forcing users to delete and readd the game.

Do not commit any GameProfile/Descriptions changes to the ``TeknoParrotUi.Common.csproj`` file. The files will be added automatically when the project is reloaded.

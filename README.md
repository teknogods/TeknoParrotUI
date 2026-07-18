# TeknoParrotUI

Open Source JVS / other I/O emulator with Windows and Linux frontends. The experimental Android ARM build now includes a Winlator-backed launch path for the first validated x86 game profiles; broader profile conversion, full JVS publication, and lifecycle qualification are still in progress. Works in collaboration with [TeknoParrot](https://teknoparrot.com) and [OpenParrot](https://github.com/teknogods/OpenParrot).

[TeknoParrot Discord](https://discord.gg/kmWgGDe), development discussion is in the ``#openparrot-dev`` channel.

## Notes for contributors

When adding a new GameProfile, create a metadata file and fill in as much details as possible.

If possible, also add the game's icon to the [Icons](https://github.com/teknogods/TeknoParrotUIThumbnails/tree/master/Icons) repository.

When updating a GameProfile, increment the ``GameProfileRevision``, otherwise existing user profiles will not be updated, forcing users to delete and readd the game.

Do not commit any GameProfile/Descriptions changes to the ``TeknoParrotUi.Common.csproj`` file. The files will be added automatically when the project is reloaded.

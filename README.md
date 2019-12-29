# TeknoParrotUI

Open Source JVS / Other I/O emulator for Windows. Works in collaboration with [TeknoParrot](https://teknoparrot.com) and [OpenParrot](https://github.com/teknogods/OpenParrot).

TeknoParrot Discord, development discussion is in the ``#openparrot-dev`` channel.
https://discord.gg/kmWgGDe

## Dependencies

[discord-rpc-win](https://github.com/discordapp/discord-rpc/releases/download/v3.4.0/discord-rpc-win.zip)

Extract ``discord-rpc.dll`` (``win32-dynamic\bin\discord-rpc.dll``) into TeknoParrotUI's bin folder

## Notes for contributors

When adding a new GameProfile, create a description file and fill in as much details as possible (
If possible, also add the game's icon to the [Icons](https://github.com/teknogods/TeknoParrotUIThumbnails/tree/master/Icons) repository.
When updating a GameProfile, increment the ``GameProfileRevision``.

Do not commit any GameProfile/Descriptions changes to the ``TeknoParrotUi.Common.csproj`` file. The files will be added automatically when the project is reloaded.

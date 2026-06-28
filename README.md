# RiftStrap

An open-source Roblox launcher. Fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), rebuilt with a new UI and extended feature set.

<!-- screenshot -->

## Features

**Multi-Instance** -- Run multiple Roblox clients simultaneously.

**FPS Cap** -- Set or remove Roblox's FPS cap from the launcher, no external tools needed.

**FastFlag Profiles** -- Save, load, and swap named FastFlag presets. Import/export as JSON.

**Custom Themes** -- Install or create visual themes that change cursors, textures, fonts, and sounds. Includes a built-in theme editor.

**Session Tracking** -- Track playtime, server history, and activity across sessions.

**Server Browser** -- Browse public servers, filter by player count, join by server ID.

**Account Switcher** -- Manage multiple Roblox accounts with encrypted cookie storage (DPAPI, Windows user-scoped).

**Performance Optimizer** -- Auto-detect hardware and generate optimal FastFlags. Real-time overlay for FPS, CPU, RAM, and ping.

**Game-Specific Profiles** -- Automatically switch FastFlags and themes per game on join.

---

## Building

Requires .NET 8 SDK and Windows 10 or later.

```
git clone --recurse-submodules https://github.com/N3XT3R1337/RiftStrap
cd RiftStrap
dotnet build RiftStrap.sln -c Release
```

Output: `RiftStrap/bin/Release/net8.0-windows/RiftStrap.exe` (framework-dependent — for development only).

## Publishing a release (self-contained single exe)

The installer copies a single executable into the install folder, so a release build must be a
self-contained single-file publish (otherwise the installed copy is missing its dependencies and
will not start):

```
dotnet publish RiftStrap/RiftStrap.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true
```

Output: `RiftStrap/bin/Release/net8.0-windows/win-x64/publish/RiftStrap.exe` — this is the exe to
distribute and install; it embeds the .NET runtime and all dependencies.

---

## Ecosystem

RiftStrap is the free launcher in the Rift ecosystem. For full-featured account management, see [Rift Manager](https://getrift.ru).

- Website: [getrift.ru](https://getrift.ru)
- Discord: [discord.gg/QbYNHhDRvt](https://discord.gg/QbYNHhDRvt)

## License

MIT License. See [LICENSE](LICENSE).

This project is a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer. Original license preserved in [LICENSE.Bloxstrap](LICENSE.Bloxstrap).

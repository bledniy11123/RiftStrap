# RiftStrap

An open-source Roblox launcher. Fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap), rebuilt with a new UI and extended feature set.

<!-- screenshot -->

## Features

**Multi-Instance** -- Run multiple Roblox clients simultaneously.

**FPS Unlocker** -- Remove the 60 FPS cap without external tools.

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
git clone --recurse-submodules https://github.com/n3xt3r/riftstrap
cd riftstrap
dotnet build RiftStrap.sln -c Release
```

Output: `RiftStrap/bin/Release/net8.0-windows/RiftStrap.exe`

---

## Ecosystem

RiftStrap is the free launcher in the Rift ecosystem. For full-featured account management, see [Rift Manager](https://getrift.ru).

- Website: [getrift.ru](https://getrift.ru)
- Discord: [discord.gg/QbYNHhDRvt](https://discord.gg/QbYNHhDRvt)

## License

MIT License. See [LICENSE](LICENSE).

This project is a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) by pizzaboxer. Original license preserved in [LICENSE.Bloxstrap](LICENSE.Bloxstrap).

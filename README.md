# Milady Bruisers Mod Loader

A mod loader for **Bruisers 2D Boxing** that lets several mods run at the same time.

Most game mods ship as a patch for one exact version of the game file (`data.win`), so two mods
can't both be installed. Milady mods are different: each mod is a folder of **instructions** ("add this
code after the career screen's update event"). The loader keeps an untouched copy of your game file and
rebuilds it with every mod you've switched on, in an order you control, and tells you if two mods clash.

## For players

1. Download the latest `MiladyLoader-….zip` from [Releases](../../releases) (or the latest build under
   [Actions](../../actions)).
2. Open your game folder: Steam → right-click Bruisers 2D Boxing → Manage → Browse local files.
3. Unzip so the `MiladyLoader` folder sits **inside** the game folder.
4. Put mods in `MiladyLoader/mods/` - each mod is a folder with a `mod.json` in it.
5. Double-click `MiladyLoader.exe`. The launcher window lists your mods - tick them on or off, change
   the order, then press **PLAY**.

PLAY only rebuilds the game file when something changed (a mod added, removed, switched, reordered or
updated, or the game itself updated), then starts the game through Steam. You can launch the game from
the loader every time, or from Steam as usual - the mods stay in until you change something.

The window also has **Play without mods** (one session with the plain game), **Open mods folder**,
and **Restore original game**.

Advanced: the same things work from a terminal.

| Command | What it does |
|---|---|
| `MiladyLoader play` | Rebuild if needed, then start the game |
| `MiladyLoader build` | Rebuild the game with all switched-on mods |
| `MiladyLoader check` | Show the load order and any conflicts without changing anything |
| `MiladyLoader list` | List installed mods |
| `MiladyLoader enable <id>` / `disable <id>` | Switch a mod on or off |
| `MiladyLoader move <id> <n>` | Change load order (1 = applied first) |
| `MiladyLoader restore` | Go back to the game without loader mods |

**Mods that use their own installer** (for example The Undercard): install those first. The loader
treats whatever is in `data.win` as the base and adds its mods on top. The launcher lists it as
"installed separately". If that other installer runs again later, just press PLAY in the loader afterwards.

**Uninstalling:** press *Restore original game* in the launcher, or use Steam's *Verify integrity of game files*.

Every push is built automatically by GitHub Actions; tagged versions (`v0.1.0`, …) become Releases.

## License

GPL-3.0 - see [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md). Not affiliated with the developers of
Bruisers 2D Boxing. This project never distributes the game's own files.

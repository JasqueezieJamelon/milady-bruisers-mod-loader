# Design

## Goal

Any number of mods - including mods whose authors never coordinated - can be installed together,
removed cleanly, and survive game updates, with clashes reported before the player starts the game.

## How a build works

1. **Base file.** On first run the loader copies `data.win` to `MiladyLoader/backup/base.win`.
   `state.json` stores its SHA-256 and the hash of the last file the loader wrote.
2. **Spotting outside changes.** If `data.win` matches neither hash, something else changed it - a
   Steam update, file verification, or a non-loader mod's own installer. That file becomes the new base
   (the old one is kept as `base-previous.win`). This is how installer-based mods such as The Undercard
   end up *underneath* loader mods instead of being wiped out.
   The loader also recognises known installer-based mods underneath (currently The Undercard, via the
   `bruisers_mods_installed.txt` it writes, whose `patchedHash` must match the base). Recognised ones are
   listed in the report and counted in the main-menu label. Add new detectors in `ExternalMods.cs`.
3. **Mods.** Every folder in `mods/` with a valid `mod.json` is read. `loadorder.json` keeps the
   player's order and on/off switches; new mods are added at the end, switched on.
4. **Ordering.** `incompatibleWith` drops the later of two clashing mods; `loadAfter` moves a mod after
   the mods it depends on; otherwise the player's order is used.
5. **Analysis.** Before anything is written, the loader reports:
   - two mods replacing the same code entry (conflict - last one wins),
   - a replace that wipes out an earlier mod's append (conflict),
   - find-and-replace on code another mod also changes (warning),
   - the same `global.` variable written by two mods (warning).
6. **Build.** `base.win` is loaded with UndertaleModLib; each mod's patches are applied with
   `CodeImportGroup`. Each mod's code is wrapped in `// [Milady] <id> BEGIN/END` comments.
   If any mod fails to compile, the build stops and `data.win` is left untouched.
7. **Write.** The result goes to a temp file first, then replaces `data.win`. The new hash is saved.

Removing or disabling a mod is just another rebuild from the base - nothing piles up in the game file.

## The launcher window (0.2)

Double-clicking `MiladyLoader.exe` opens a small window (WinForms); with a command it behaves as the
command-line tool. The window reuses the same `Builder`, so it shows exactly what a build would do.

- **PLAY** skips the rebuild when nothing changed. After each build the loader saves a fingerprint
  (loader version + base hash + every switched-on mod in order, with a hash of every file in its folder).
  PLAY rebuilds only if `data.win` isn't the loader's last output, or the fingerprint differs.
- The game is started through Steam (`steam://rungameid/<id>`, the id read from the `appmanifest_*.acf`
  whose `installdir` matches the game folder), falling back to running the exe directly.
- The window re-reads the mods folder whenever it regains focus, so dropping a mod in and switching back
  is enough.

## Planned for 0.3 - shared hooks

Even with `append`, two mods drawing on the same screen each recompile the same game event. The hook
system removes that:

- On every build the loader inserts **one** small dispatcher into key game events (game start, the
  career screen's step/draw events, save and load).
- Mods don't patch those events. They register callbacks, e.g. `milady_on("career_draw_gui", my_fn)`.
- The dispatcher calls each mod's callbacks in load order, inside a try/catch, so one broken mod
  can't crash the others, and reports which mod failed.
- Shared services on top: per-mod settings and save data (`milady_save_set("my-mod", key, value)`),
  a key registry that flags two mods using the same button on the same screen, and a loader version
  check mods can call.

## Planned later

- Drag-to-reorder in the launcher.
- Optional `.csx` support for advanced mods that need to add sprites, sounds or rooms.

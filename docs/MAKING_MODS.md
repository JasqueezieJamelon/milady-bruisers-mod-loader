# Making a mod for Milady Bruisers Mod Loader

A Milady mod is a folder containing a `mod.json` and the GML code it adds. You never ship the game's
`data.win` or a binary patch - the loader applies your code to whatever version of the game the player
has, together with their other mods.

```
my-cool-mod/
  mod.json
  code/
    career_end_step.gml
    my_new_script.gml
```

## mod.json

```jsonc
{
  "id": "my-cool-mod",            // lowercase letters, numbers, dashes. Must be unique. Never change it.
  "name": "My Cool Mod",
  "version": "1.2.0",
  "author": "you",
  "description": "What it does, in one sentence.",
  "loaderVersion": "0.1",         // lowest loader version your mod needs
  "testedGameHashes": [],         // optional: SHA-256 of the game data.win you tested on
  "loadAfter": [],                // optional: mod ids that must be applied before yours
  "incompatibleWith": [],         // optional: mod ids yours can't be used with
  "patches": [
    { "target": "gml_Object_oCareer_Step_2", "mode": "append",  "file": "code/career_end_step.gml" },
    { "target": "gml_GlobalScript_mycoolmod_helpers", "mode": "create", "file": "code/my_new_script.gml" },
    { "target": "gml_Object_oSomething_Create_0", "mode": "findreplace", "find": "speed = 4", "replace": "speed = 6" }
  ]
}
```

`target` is a code entry name exactly as UndertaleModTool shows it, e.g. `gml_Object_<object>_<Event>_<n>`,
`gml_Script_<name>` or `gml_GlobalScript_<name>`. Object events that don't exist yet are created
automatically (e.g. `gml_Object_oCareer_Step_1` = Begin Step).

### Modes - pick the gentlest one that works

| Mode | What it does | Plays well with other mods? |
|---|---|---|
| `append` | Adds your code after the existing code | **Yes** - every mod's appended code runs, in load order |
| `prepend` | Adds your code before the existing code | **Yes** |
| `create` | Makes a brand-new code entry; fails if it already exists | **Yes** - use your own unique names |
| `findreplace` | Replaces exact text in the decompiled code | Risky - fails if another mod already changed that text |
| `replace` | Throws away the existing code and uses yours | **No** - wipes out other mods' changes to that entry |

Use `append`/`prepend`/`create` whenever you can. The loader's conflict report flags every `replace`
and `findreplace` that overlaps with another mod.

> Note: `append`, `prepend` and `findreplace` decompile and recompile the whole target code entry.
> For very large entries (like `gml_Object_oCareer_Step_0`) prefer adding a new event instead
> (Begin Step `_Step_1`, End Step `_Step_2`, Draw GUI End `_Draw_75`, …) - it's faster and safer.

## Rules that keep mods compatible

1. **Prefix your names.** Every global variable, new script and new object you add starts with a short
   prefix from your mod id: `global.mycoolmod_count`, not `global.count`. The loader warns when two mods
   write the same `global.` name.
2. **Don't remove game code** unless your mod's whole purpose is to change it - then say so in your
   description and use `incompatibleWith` for mods you know you clash with.
3. **Restore draw state.** If you change the font, colour, alpha or alignment, set them back afterwards.
4. **Saving data.** If you add your variable names to `global.saveVarsGame`, the game saves and loads
   them with the player's save. Prefixed names only. (A shared settings/save API is planned for 0.2.)
5. **Controls.** Check another mod isn't already using the same button on the same screen. (A shared
   key registry is planned for 0.2.)

## Testing

1. Put your folder in `MiladyLoader/mods/`.
2. Run `MiladyLoader check` - it validates your `mod.json` and shows conflicts without touching the game.
3. Run `MiladyLoader` to build, then play. Compile errors in your GML are shown in the report with
   your mod's name, and nothing is written when a mod fails.

## Converting an UndertaleModTool script mod

If your mod is a `.csx` script that calls `QueueReplace` / `QueueAppend` on code entries, move each
GML string into its own `.gml` file and list it in `patches` with the same target and mode.
Prefer switching `QueueReplace` on existing game events to `append`.

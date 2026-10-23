using System.Text;
using System.Text.RegularExpressions;
using UndertaleModLib;
using UndertaleModLib.Compiler;

namespace MiladyLoader;

/// <summary>Collects everything the player should know about a build.</summary>
public sealed class Report
{
    public readonly List<string> Info = new();
    public readonly List<string> Warnings = new();
    public readonly List<string> Conflicts = new();
    public readonly List<string> Errors = new();

    public string Render()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{LoaderInfo.Name} {LoaderInfo.Version} - {DateTime.Now:yyyy-MM-dd HH:mm}");
        void Section(string title, List<string> lines)
        {
            if (lines.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(title);
            foreach (var l in lines) sb.AppendLine("  - " + l);
        }
        Section("ERRORS", Errors);
        Section("CONFLICTS (two mods change the same thing - the later mod in the load order wins)", Conflicts);
        Section("WARNINGS", Warnings);
        Section("INFO", Info);
        return sb.ToString();
    }
}

public sealed class Builder(Workspace ws)
{
    static readonly Regex GlobalWrite = new(@"\bglobal\.([A-Za-z_]\w*)\s*(?:=(?!=)|\+=|-=|\*=|/=|\+\+|--)", RegexOptions.Compiled);

    /// <summary>
    /// Makes sure backup/base.win holds the game file the loader should build on.
    /// If data.win was changed by something other than the loader (Steam update, another mod's
    /// installer), that new file becomes the base, so non-loader mods are kept underneath loader mods.
    /// A changed data.win that already has loader mods in it is never used (see AdoptionProblem); when
    /// restoring, the saved base is put back instead.
    /// </summary>
    public LoaderState SyncBase(Report r, bool restoring = false)
    {
        var state = ws.LoadState();
        string cur = Workspace.Sha256(ws.DataWin);
        bool firstRun = !File.Exists(ws.BaseWin) || string.IsNullOrEmpty(state.BaseHash);
        bool adopted = false;

        // State files from before baseExternalMods existed: record it now, before a restore clears lastBuiltMods.
        if (!firstRun) state.BaseExternalMods ??= BaseExternalIds(state);

        if (AdoptionProblem(state, cur) is string problem)
        {
            if (!restoring || firstRun) throw new LoaderException(problem);
            r.Warnings.Add("data.win was changed by something other than the loader, but it already had loader mods inside it, " +
                           "so it was ignored and the last clean base was put back.");
        }
        else if (firstRun)
        {
            File.Copy(ws.DataWin, ws.BaseWin, true);
            state.BaseHash = cur;
            state.LastBuiltHash = "";
            state.BaseExternalMods = ExternalIds(cur);
            adopted = true;
            r.Info.Add("First run: saved your current game file as the base to build on (MiladyLoader/backup/base.win).");
        }
        else if (cur != state.BaseHash && cur != state.LastBuiltHash)
        {
            var changes = ExternalChanges(state, cur);
            string prev = Path.Combine(ws.BackupDir, "base-previous.win");
            File.Copy(ws.BaseWin, prev, true);
            File.Copy(ws.DataWin, ws.BaseWin, true);
            state.BaseHash = cur;
            state.LastBuiltHash = "";
            state.BaseExternalMods = ExternalIds(cur);
            adopted = true;
            if (changes.Count == 0)
                r.Warnings.Add("data.win changed since the loader last touched it (a game update, Steam file verification, " +
                               "or another mod's own installer). Using it as the new base. The old base was kept as backup/base-previous.win.");
            else
            {
                foreach (var (text, removed) in changes) (removed ? r.Warnings : r.Info).Add(text + ".");
                r.Info.Add("Your loader mods are added on top of the new game file. The previous one was kept as backup/base-previous.win.");
            }
        }

        if (!adopted && Workspace.Sha256(ws.BaseWin) != state.BaseHash)
            throw new LoaderException("backup/base.win doesn't match what the loader saved earlier. Use Steam's " +
                                      "\"Verify integrity of game files\", then run the loader again to start fresh.");

        ws.SaveState(state);
        return state;
    }

    /// <summary>
    /// Explains the problem when the file the loader would take as its new base already has loader mods in it -
    /// for example another mod's installer patched the loader's build instead of a clean game file. Building on
    /// that would add every loader mod a second time. Null when there's no problem.
    /// </summary>
    public string? AdoptionProblem() => AdoptionProblem(ws.LoadState(), Workspace.Sha256(ws.DataWin));

    string? AdoptionProblem(LoaderState state, string cur)
    {
        bool firstRun = !File.Exists(ws.BaseWin) || string.IsNullOrEmpty(state.BaseHash);
        bool willAdopt = firstRun || (cur != state.BaseHash && cur != state.LastBuiltHash);
        if (!willAdopt || !Workspace.ContainsText(ws.DataWin, LoaderInfo.MenuLabelPrefix)) return null;
        return firstRun
            ? "Your game file already has loader mods inside it, but the loader has no clean copy saved to build on. " +
              "Building on it would add your mods twice, so the loader won't use it. To fix it: use Steam's \"Verify integrity " +
              "of game files\", reinstall any mods that have their own installer (like The Undercard), then press PLAY."
            : "data.win was changed by something other than the loader (probably a mod's own installer, like The Undercard), " +
              "but the new file already has loader mods inside it. Building on it would add your mods twice, so the loader won't use it. " +
              "To fix it: press Restore, run the other mod's installer again, then press PLAY.";
    }

    /// <summary>Mods installed with their own installers that are part of the given file, as "Name@Version".</summary>
    List<string> ExternalIds(string hash) => ExternalMods.Detect(ws, hash).Select(x => $"{x.Name}@{x.Version}").ToList();

    /// <summary>
    /// The mods with their own installers in the saved base. Older state files don't record it, so fall back
    /// to the last build's list, then to detecting them in the base (only works until their installer runs again).
    /// </summary>
    List<string> BaseExternalIds(LoaderState state)
    {
        if (state.BaseExternalMods != null) return state.BaseExternalMods;
        var fromBuild = state.LastBuiltMods.Where(s => s.StartsWith("external:")).Select(s => s["external:".Length..]).ToList();
        return fromBuild.Count > 0 ? fromBuild : ExternalIds(state.BaseHash);
    }

    /// <summary>
    /// How the mods installed with their own installers differ between the saved base and data.win, in plain
    /// words (e.g. "The Undercard was updated from A to B"). Removed = that mod is gone. Empty when neither has any.
    /// </summary>
    List<(string Text, bool Removed)> ExternalChanges(LoaderState state, string cur)
    {
        var before = BaseExternalIds(state)
                     .Select(s => s.LastIndexOf('@') is int at and >= 0 ? (Name: s[..at], Version: s[(at + 1)..]) : (Name: s, Version: "?"))
                     .ToList();
        var after = ExternalMods.Detect(ws, cur);
        var changes = new List<(string, bool)>();
        foreach (var x in after)
        {
            var old = before.FirstOrDefault(b => b.Name == x.Name);
            changes.Add((old.Name == null ? $"{x.Name} {x.Version} was installed"
                       : old.Version != x.Version ? $"{x.Name} was updated from {old.Version} to {x.Version}"
                       : $"{x.Name} {x.Version} was reinstalled", false));
        }
        foreach (var b in before.Where(b => after.All(x => x.Name != b.Name)))
            changes.Add(($"{b.Name} is no longer part of the game file (a game update, a Steam file check, or it was uninstalled)", true));
        return changes;
    }

    /// <summary>Finds installed mods and returns the enabled ones in final load order.</summary>
    public List<ModManifest> ResolveMods(Report r, bool persist = true)
    {
        var found = Discover(r);

        // Keep the player's order; new mods go to the end, switched on. Forget mods that were removed.
        var order = ws.LoadOrder().Where(e => found.ContainsKey(e.Id)).ToList();
        foreach (var id in found.Keys.Where(id => order.All(e => e.Id != id)))
        {
            order.Add(new LoadOrderEntry { Id = id, Enabled = true });
            r.Info.Add($"New mod found: {found[id].Name} ({id}) - switched on and added to the end of the load order.");
        }
        if (persist) ws.SaveLoadOrder(order);
        return Order(order.Where(e => e.Enabled).Select(e => found[e.Id]).ToList(), r);
    }

    /// <summary>Reads every mod folder in mods/.</summary>
    public Dictionary<string, ModManifest> Discover(Report r)
    {
        var found = new Dictionary<string, ModManifest>();
        foreach (var dir in Directory.GetDirectories(ws.ModsDir).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var m = ModManifest.Load(dir, r.Errors);
            if (m is null) continue;
            if (found.TryGetValue(m.Id, out var existing))
            {
                r.Errors.Add($"Two mod folders use the id \"{m.Id}\" ({Path.GetFileName(existing.Folder)} and {Path.GetFileName(dir)}) - skipped the second.");
                continue;
            }
            found[m.Id] = m;
        }
        return found;
    }

    /// <summary>Applies incompatibilities and loadAfter to the player's order of enabled mods.</summary>
    static List<ModManifest> Order(List<ModManifest> enabled, Report r)
    {
        // Incompatibilities: keep the earlier mod, drop the later one.
        foreach (var m in enabled.ToList())
        {
            var clash = enabled.FirstOrDefault(o => o != m && (m.IncompatibleWith.Contains(o.Id) || o.IncompatibleWith.Contains(m.Id))
                                                   && enabled.IndexOf(o) < enabled.IndexOf(m));
            if (clash != null)
            {
                r.Errors.Add($"{m.Name} can't be used with {clash.Name} - {m.Name} was left out. Switch one of them off.");
                enabled.Remove(m);
            }
        }

        // loadAfter: move mods after the mods they depend on, otherwise keep the player's order.
        var result = new List<ModManifest>();
        var pending = new List<ModManifest>(enabled);
        while (pending.Count > 0)
        {
            var next = pending.FirstOrDefault(m => m.LoadAfter.All(dep => pending.All(p => p.Id != dep)));
            if (next == null)
            {
                r.Warnings.Add("Some mods' \"loadAfter\" settings form a loop (" + string.Join(", ", pending.Select(p => p.Id)) +
                               "); using the load order as listed for those.");
                result.AddRange(pending);
                break;
            }
            result.Add(next);
            pending.Remove(next);
        }
        return result;
    }

    /// <summary>Warns about places where two mods touch the same code or the same global variables.</summary>
    public void Analyze(List<ModManifest> mods, Report r)
    {
        var byTarget = new Dictionary<string, List<(ModManifest mod, string mode)>>(StringComparer.Ordinal);
        foreach (var m in mods)
            foreach (var p in m.Patches)
            {
                if (!byTarget.TryGetValue(p.Target, out var list)) byTarget[p.Target] = list = new();
                list.Add((m, p.Mode));
            }

        foreach (var (target, list) in byTarget)
        {
            var mods2 = list.Select(x => x.mod).Distinct().ToList();
            if (mods2.Count < 2) continue;
            var replacers = list.Where(x => x.mode is "replace" or "create").Select(x => x.mod).Distinct().ToList();
            string names = string.Join(", ", mods2.Select(x => x.Name));
            if (replacers.Count > 1)
                r.Conflicts.Add($"{target}: replaced by {string.Join(" and ", replacers.Select(x => x.Name))}. Only {replacers.Last().Name}'s version survives.");
            else if (replacers.Count == 1)
            {
                var rep = replacers[0];
                var before = list.TakeWhile(x => x.mod != rep).Select(x => x.mod.Name).Distinct().ToList();
                if (before.Count > 0)
                    r.Conflicts.Add($"{target}: {rep.Name} replaces it after {string.Join(", ", before)} changed it, so their changes there are lost.");
                else
                    r.Warnings.Add($"{target}: {rep.Name} replaces it and {names} also add to it - check they still work together.");
            }
            else if (list.Any(x => x.mode == "findreplace"))
                r.Warnings.Add($"{target}: edited by {names}, including a find-and-replace. If it fails, the mods may be clashing there.");
            else
                r.Info.Add($"{target}: {names} all add code here (fine - each runs in load order).");
        }

        var writers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var m in mods)
            foreach (var p in m.Patches.Where(p => p.File != null))
                foreach (Match g in GlobalWrite.Matches(m.ReadPatchFile(p)))
                {
                    if (!writers.TryGetValue(g.Groups[1].Value, out var set)) writers[g.Groups[1].Value] = set = new();
                    set.Add(m.Name);
                }
        foreach (var (name, set) in writers.Where(w => w.Value.Count > 1))
            r.Warnings.Add($"global.{name} is set by {string.Join(" and ", set)}. If they mean different things, they'll overwrite each other.");
    }

    /// <summary>Rebuilds data.win = base + enabled mods. Writes nothing if any mod fails.</summary>
    public bool Build(Report r, Action<string> progress)
    {
        if (Workspace.GameIsRunning())
            throw new LoaderException("Bruisers 2D Boxing is running. Close the game, then run the loader again.");

        var state = SyncBase(r);
        var mods = ResolveMods(r);
        var external = DescribeBase(state.BaseHash, r);
        Analyze(mods, r);

        progress("Reading the base game file...");
        UndertaleData data;
        using (var fs = File.OpenRead(ws.BaseWin))
            data = UndertaleIO.Read(fs, (w, important) => { if (important) r.Warnings.Add("Game file: " + w); });

        foreach (var m in mods)
        {
            progress($"Applying {m.Name} {m.Version}...");
            try
            {
                Apply(data, m);
                r.Info.Add($"Applied {m.Name} {m.Version} ({m.Patches.Count} change{(m.Patches.Count == 1 ? "" : "s")}).");
            }
            catch (Exception e)
            {
                r.Errors.Add($"{m.Name} failed to apply, so nothing was changed. Reason: {e.Message}");
                return false;
            }
            if (m.TestedGameHashes.Count > 0 && !m.TestedGameHashes.Contains(state.BaseHash, StringComparer.OrdinalIgnoreCase))
            {
                if (external.Count > 0)
                    r.Info.Add($"{m.Name} was tested on the plain game, not together with {string.Join(" and ", external.Select(x => x.Name))}. If something breaks, try switching it off.");
                else
                    r.Warnings.Add($"{m.Name} wasn't tested on this exact game version. If something breaks, check for an update to the mod.");
            }
        }

        progress("Adding the loader's main-menu label...");
        try { AddMenuLabel(data, mods.Count + external.Count); }
        catch (Exception e) { r.Warnings.Add("Couldn't add the main-menu label (the game may have changed): " + e.Message); }

        progress("Saving the game file...");
        string tmp = ws.DataWin + ".milady-tmp";
        using (var fs = File.Create(tmp))
            UndertaleIO.Write(fs, data);
        File.Move(tmp, ws.DataWin, overwrite: true);

        state.LastBuiltHash = Workspace.Sha256(ws.DataWin);
        state.LastBuiltMods = mods.Select(m => $"{m.Id}@{m.Version}")
                                  .Concat(external.Select(x => $"external:{x.Name}@{x.Version}")).ToList();
        state.LastBuiltAt = DateTime.Now.ToString("s");
        state.LastBuiltFingerprint = Fingerprint(state.BaseHash, mods);
        ws.SaveState(state);
        if (mods.Count == 0) r.Info.Add("No mods are switched on, so the game is back to its base version.");
        return true;
    }

    /// <summary>
    /// A hash of everything a build depends on: loader version, base file, and each switched-on mod
    /// (in order) with every file in its folder. Same fingerprint + untouched data.win = nothing to do.
    /// </summary>
    public static string Fingerprint(string baseHash, List<ModManifest> mods)
    {
        var sb = new StringBuilder();
        sb.Append(LoaderInfo.Version).Append('|').Append(baseHash);
        foreach (var m in mods)
        {
            sb.Append('|').Append(m.Id).Append('@').Append(m.Version);
            foreach (var f in Directory.GetFiles(m.Folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
                sb.Append(';').Append(Path.GetRelativePath(m.Folder, f).Replace('\\', '/')).Append('=').Append(Workspace.Sha256(f));
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>The base the next build will use: the saved base, or data.win if something else changed it since.</summary>
    public string ProspectiveBaseHash()
    {
        var state = ws.LoadState();
        string cur = Workspace.Sha256(ws.DataWin);
        if (string.IsNullOrEmpty(state.BaseHash) || (cur != state.BaseHash && cur != state.LastBuiltHash)) return cur;
        return state.BaseHash;
    }

    /// <summary>
    /// True when PLAY has to rebuild first: first run, a mod/toggle/order change, a mod file edited,
    /// a loader update, or data.win changed by something else (game update, restore, another installer).
    /// </summary>
    public bool NeedsBuild(out string reason)
    {
        var state = ws.LoadState();
        if (string.IsNullOrEmpty(state.BaseHash) || !File.Exists(ws.BaseWin)) { reason = "first run"; return true; }
        string cur = Workspace.Sha256(ws.DataWin);
        if (string.IsNullOrEmpty(state.LastBuiltHash) || cur != state.LastBuiltHash)
        {
            var changes = cur != state.BaseHash ? ExternalChanges(state, cur) : new();
            reason = changes.Count > 0 ? string.Join("; ", changes.Select(c => c.Text)) : "the game file isn't the loader's last build";
            return true;
        }
        var mods = ResolveMods(new Report(), persist: false);
        if (Fingerprint(state.BaseHash, mods) != state.LastBuiltFingerprint) { reason = "mods changed"; return true; }
        reason = "up to date";
        return false;
    }

    /// <summary>Reports mods that are already part of the base file (installed with their own installers).</summary>
    public List<ExternalMod> DescribeBase(string baseHash, Report r)
    {
        var external = ExternalMods.Detect(ws, baseHash);
        foreach (var x in external)
            r.Info.Add($"Installed separately: {x.Name} {x.Version}" + (x.Parts.Count > 0 ? $" ({x.Parts.Count} parts: {string.Join(", ", x.Parts)})" : "") +
                       ". Loader mods are added on top of it.");
        if (external.Count == 0 && !ExternalMods.KnownVanillaHashes.Contains(baseHash))
            r.Info.Add("Your base game file isn't a version the loader recognises - either a game update or a mod installed some other way. Loader mods are added on top of it.");
        return external;
    }

    static void Apply(UndertaleData data, ModManifest m)
    {
        var group = new CodeImportGroup(data) { ThrowOnNoOpFindReplace = true };
        foreach (var p in m.Patches)
        {
            string Wrapped() => $"{LoaderInfo.BeginMarker(m.Id)}\n{m.ReadPatchFile(p)}\n{LoaderInfo.EndMarker(m.Id)}";
            switch (p.Mode)
            {
                case "append": group.QueueAppend(p.Target, Wrapped()); break;
                case "prepend": group.QueuePrepend(p.Target, Wrapped()); break;
                case "replace": group.QueueReplace(p.Target, Wrapped()); break;
                case "create":
                    if (data.Code.ByName(p.Target) != null)
                        throw new LoaderException($"\"{p.Target}\" already exists (mode \"create\" only makes new code). Use \"append\" or \"replace\".");
                    group.QueueReplace(p.Target, Wrapped());
                    break;
                case "findreplace": group.QueueFindReplace(p.Target, p.Find!, p.Replace!); break;
            }
        }
        group.Import();
    }

    /// <summary>
    /// Shows "MILADY LOADER x.y.z - N MODS" on the main menu only, on the same line as the game's own
    /// version text (bottom-right), just to its left. oMenu's state is "menu" on the main menu.
    /// </summary>
    static void AddMenuLabel(UndertaleData data, int modCount)
    {
        // A new Draw End event on the menu object: draws in the same space as the menu, without
        // recompiling the game's own (large) menu draw code. Other mods appending here is fine too.
        const string target = "gml_Object_oMenu_Draw_73";
        if (data.GameObjects.ByName("oMenu") == null) throw new LoaderException("main menu object not found");
        string label = $"{LoaderInfo.MenuLabelPrefix}{LoaderInfo.Version} -{modCount} MOD{(modCount == 1 ? "" : "S")}";
        string gml = $$"""
            {{LoaderInfo.BeginMarker("milady-loader")}}
            if (state == "menu" && hideMenu != 1)
            {
                // Same line, font and alignment as the game's own version text, just to its left.
                draw_set_font(fPixel2);
                var _gap = string_width(Loc("UI_Menu_Version")) + 12;
                draw_set_halign(fa_right);
                draw_set_valign(fa_middle);
                var _lx = 764 - _gap;
                var _ly = (428 + global.GUIHeight) / 2;
                // Dark outline so it reads over bright parts of the background.
                draw_set_colour(c_black);
                for (var _ox = -1; _ox <= 1; _ox++)
                    for (var _oy = -1; _oy <= 1; _oy++)
                        if (_ox != 0 || _oy != 0) draw_text(_lx + _ox, _ly + _oy, "{{label}}");
                draw_set_colour(make_colour_rgb(255, 90, 200));
                draw_text(_lx, _ly, "{{label}}");
                draw_set_halign(fa_left);
                draw_set_valign(fa_top);
                draw_set_colour(c_white);
            }
            {{LoaderInfo.EndMarker("milady-loader")}}
            """;
        var group = new CodeImportGroup(data);
        group.QueueAppend(target, gml);
        group.Import();
    }

    /// <summary>Puts the base game file back, with no loader mods.</summary>
    public void Restore(Report r)
    {
        if (Workspace.GameIsRunning())
            throw new LoaderException("Bruisers 2D Boxing is running. Close the game, then run the loader again.");
        var state = SyncBase(r, restoring: true);
        File.Copy(ws.BaseWin, ws.DataWin, true);
        state.LastBuiltHash = "";
        state.LastBuiltMods.Clear();
        ws.SaveState(state);
        r.Info.Add("Restored the base game file. Loader mods are off until you run the loader again.");
    }
}

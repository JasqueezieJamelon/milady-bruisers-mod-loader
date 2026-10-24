namespace MiladyLoader;

/// <summary>
/// A mod that was installed by its own installer (not through the loader) and is part of the base file.
/// Missing = its installer still says it's installed, but it isn't in the game file any more (Steam replaced
/// data.win with a game update or file check), so its installer has to run again.
/// </summary>
public sealed record ExternalMod(string Name, string Version, IReadOnlyList<string> Parts, bool Missing = false);

/// <summary>
/// Recognises mods that patch data.win with their own installer, so the loader can list and count them.
/// A detector only reports a mod when it can prove that mod produced the current base file.
/// To support another installer-based mod, add a detector here.
/// </summary>
public static class ExternalMods
{
    /// <summary>SHA-256 hashes of unmodified Steam releases of the game that the loader knows about.</summary>
    public static readonly HashSet<string> KnownVanillaHashes = new(StringComparer.OrdinalIgnoreCase)
    {
        "35C10B8C95FF14CA8844FAEC5BF29300E566EA4D3F1124BE96AAA884F23612B2", // Early Access 0.7
    };

    public static List<ExternalMod> Detect(Workspace ws, string baseHash)
    {
        var found = new List<ExternalMod>();
        var undercard = DetectUndercard(ws.GameDir, baseHash);
        if (undercard is { Missing: false }) found.Add(undercard);
        return found;
    }

    /// <summary>Mods whose installer says they're installed, but that aren't part of the given base file.</summary>
    public static List<ExternalMod> DetectMissing(Workspace ws, string baseHash)
    {
        var found = new List<ExternalMod>();
        var undercard = DetectUndercard(ws.GameDir, baseHash);
        if (undercard is { Missing: true }) found.Add(undercard);
        return found;
    }

    /// <summary>
    /// The Undercard (bruisersmod.co.uk) writes bruisers_mods_installed.txt next to the game, including
    /// the hash of the data.win it produced ("patchedHash") and the list of parts it installed ("mods").
    /// Its uninstaller deletes that file, so a file whose hash doesn't match means Steam replaced data.win.
    /// </summary>
    static ExternalMod? DetectUndercard(string gameDir, string baseHash)
    {
        string path = Path.Combine(gameDir, "bruisers_mods_installed.txt");
        if (!File.Exists(path)) return null;
        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq > 0) kv[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        if (!kv.TryGetValue("patchedHash", out var patched)) return null;
        var parts = kv.TryGetValue("mods", out var mods)
            ? mods.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : Array.Empty<string>();
        return new ExternalMod("The Undercard", kv.GetValueOrDefault("pack", "?"), parts,
                               Missing: !patched.Equals(baseHash, StringComparison.OrdinalIgnoreCase));
    }
}

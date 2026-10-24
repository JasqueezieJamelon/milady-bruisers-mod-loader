using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiladyLoader;

/// <summary>What the loader remembers between runs (MiladyLoader/state.json).</summary>
public sealed class LoaderState
{
    /// <summary>SHA-256 of the saved base game file (backup/base.win).</summary>
    [JsonPropertyName("baseHash")] public string BaseHash { get; set; } = "";

    /// <summary>SHA-256 of the data.win the loader last wrote, so it can tell its own output apart.</summary>
    [JsonPropertyName("lastBuiltHash")] public string LastBuiltHash { get; set; } = "";

    [JsonPropertyName("lastBuiltMods")] public List<string> LastBuiltMods { get; set; } = new();
    [JsonPropertyName("lastBuiltAt")] public string LastBuiltAt { get; set; } = "";

    /// <summary>Fingerprint of everything that went into the last build (see Builder.Fingerprint).</summary>
    [JsonPropertyName("lastBuiltFingerprint")] public string LastBuiltFingerprint { get; set; } = "";

    /// <summary>
    /// Mods installed with their own installers that are part of the base, as "Name@Version", so the loader can
    /// say what changed when the base is replaced. Null in state files written before this was added.
    /// </summary>
    [JsonPropertyName("baseExternalMods")] public List<string>? BaseExternalMods { get; set; }

    /// <summary>Steam build id when the base came straight from Steam (a game update or file check), otherwise null.</summary>
    [JsonPropertyName("baseSteamBuild")] public string? BaseSteamBuild { get; set; }
}

/// <summary>One line of MiladyLoader/loadorder.json.</summary>
public sealed class LoadOrderEntry
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
}

/// <summary>
/// Folder layout:
///   &lt;game folder&gt;/data.win               the file the game runs
///   &lt;game folder&gt;/MiladyLoader/           this program
///       mods/&lt;any folder&gt;/mod.json       installed mods
///       loadorder.json                    which mods are on, and in what order
///       backup/base.win                   untouched copy the loader rebuilds from
///       state.json                        hashes so the loader can spot game updates
///       logs/last-build.txt               report from the last rebuild
/// </summary>
public sealed class Workspace
{
    public string GameDir { get; }
    public string LoaderDir { get; }
    public string DataWin => Path.Combine(GameDir, "data.win");
    public string ModsDir => Path.Combine(LoaderDir, "mods");
    public string BackupDir => Path.Combine(LoaderDir, "backup");
    public string BaseWin => Path.Combine(BackupDir, "base.win");
    public string StateFile => Path.Combine(LoaderDir, "state.json");
    public string LoadOrderFile => Path.Combine(LoaderDir, "loadorder.json");
    public string LogsDir => Path.Combine(LoaderDir, "logs");

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    Workspace(string gameDir, string loaderDir)
    {
        GameDir = gameDir;
        LoaderDir = loaderDir;
        Directory.CreateDirectory(ModsDir);
        Directory.CreateDirectory(BackupDir);
        Directory.CreateDirectory(LogsDir);
    }

    /// <summary>
    /// Finds the game. Order: --game argument, the folder above the loader (normal install),
    /// the loader's own folder, then Steam's library folders.
    /// </summary>
    public static Workspace Locate(string? gameArg)
    {
        string exeDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(gameArg)) candidates.Add(gameArg);
        var parent = Directory.GetParent(exeDir)?.FullName;
        if (parent != null) candidates.Add(parent);
        candidates.Add(exeDir);
        candidates.AddRange(SteamGameFolders());

        foreach (var c in candidates)
        {
            if (File.Exists(Path.Combine(c, "data.win")) && File.Exists(Path.Combine(c, LoaderInfo.GameExe)))
            {
                // Keep the loader's files next to the exe if it lives inside the game folder,
                // otherwise in a MiladyLoader folder inside the game folder.
                bool exeInsideGame = Path.GetFullPath(exeDir).StartsWith(Path.GetFullPath(c), StringComparison.OrdinalIgnoreCase);
                string loaderDir = exeInsideGame ? exeDir : Path.Combine(c, "MiladyLoader");
                return new Workspace(Path.GetFullPath(c), loaderDir);
            }
        }
        throw new LoaderException(
            $"Couldn't find {LoaderInfo.GameFolderName}. Put the MiladyLoader folder inside the game folder " +
            "(Steam > right-click the game > Manage > Browse local files), or run: MiladyLoader --game \"<game folder>\"");
    }

    static IEnumerable<string> SteamGameFolders()
    {
        var roots = new List<string>
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
        };
        foreach (var root in roots.ToList())
        {
            string vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (var line in File.ReadLines(vdf))
            {
                var t = line.Trim();
                if (!t.StartsWith("\"path\"")) continue;
                var parts = t.Split('"', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3) roots.Add(parts[^1].Replace(@"\\", @"\"));
            }
        }
        return roots.Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(r => Path.Combine(r, "steamapps", "common", LoaderInfo.GameFolderName));
    }

    public LoaderState LoadState() =>
        File.Exists(StateFile) ? JsonSerializer.Deserialize<LoaderState>(File.ReadAllText(StateFile), Json) ?? new() : new();

    public void SaveState(LoaderState s) => File.WriteAllText(StateFile, JsonSerializer.Serialize(s, Json));

    public List<LoadOrderEntry> LoadOrder() =>
        File.Exists(LoadOrderFile) ? JsonSerializer.Deserialize<List<LoadOrderEntry>>(File.ReadAllText(LoadOrderFile), Json) ?? new() : new();

    public void SaveLoadOrder(List<LoadOrderEntry> order) => File.WriteAllText(LoadOrderFile, JsonSerializer.Serialize(order, Json));

    static readonly Dictionary<string, (long len, DateTime time, string hash)> HashCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>SHA-256 of a file. Remembered until the file's size or modified time changes, so the window can re-check cheaply.</summary>
    public static string Sha256(string path)
    {
        var fi = new FileInfo(Path.GetFullPath(path));
        lock (HashCache)
            if (HashCache.TryGetValue(fi.FullName, out var c) && c.len == fi.Length && c.time == fi.LastWriteTimeUtc) return c.hash;
        string hash;
        using (var fs = File.OpenRead(fi.FullName))
            hash = Convert.ToHexString(SHA256.HashData(fs));
        lock (HashCache) HashCache[fi.FullName] = (fi.Length, fi.LastWriteTimeUtc, hash);
        return hash;
    }

    static readonly Dictionary<(string path, string text), (long len, DateTime time, bool found)> TextCache = new();

    /// <summary>True if the file contains the given text anywhere. Remembered like Sha256.</summary>
    public static bool ContainsText(string path, string text)
    {
        var fi = new FileInfo(Path.GetFullPath(path));
        var key = (fi.FullName.ToUpperInvariant(), text);
        lock (TextCache)
            if (TextCache.TryGetValue(key, out var c) && c.len == fi.Length && c.time == fi.LastWriteTimeUtc) return c.found;

        // Read in 1 MB chunks, carrying the end of each chunk over so a match across two chunks isn't missed.
        byte[] needle = Encoding.UTF8.GetBytes(text);
        byte[] buf = new byte[1 << 20];
        int carry = 0;
        bool found = false;
        using (var fs = File.OpenRead(fi.FullName))
        {
            int n;
            while (!found && (n = fs.Read(buf, carry, buf.Length - carry)) > 0)
            {
                int len = carry + n;
                found = buf.AsSpan(0, len).IndexOf(needle) >= 0;
                carry = Math.Min(needle.Length - 1, len);
                Buffer.BlockCopy(buf, len - carry, buf, 0, carry);
            }
        }
        lock (TextCache) TextCache[key] = (fi.Length, fi.LastWriteTimeUtc, found);
        return found;
    }

    public static bool GameIsRunning() =>
        System.Diagnostics.Process.GetProcessesByName(Path.GetFileNameWithoutExtension(LoaderInfo.GameExe)).Length > 0;
}

public sealed class LoaderException(string message) : Exception(message);

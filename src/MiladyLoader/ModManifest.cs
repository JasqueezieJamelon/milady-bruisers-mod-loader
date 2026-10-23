using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MiladyLoader;

/// <summary>One code change a mod wants to make (one entry of "patches" in mod.json).</summary>
public sealed class ModPatch
{
    /// <summary>Code entry to change, e.g. "gml_Object_oCareer_Step_2".</summary>
    [JsonPropertyName("target")] public string Target { get; set; } = "";

    /// <summary>append | prepend | replace | create | findreplace</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "append";

    /// <summary>GML file inside the mod folder (append / prepend / replace / create).</summary>
    [JsonPropertyName("file")] public string? File { get; set; }

    /// <summary>Text to find (findreplace).</summary>
    [JsonPropertyName("find")] public string? Find { get; set; }

    /// <summary>Replacement text (findreplace).</summary>
    [JsonPropertyName("replace")] public string? Replace { get; set; }
}

/// <summary>Contents of a mod's mod.json.</summary>
public sealed class ModManifest
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";

    /// <summary>Lowest loader version this mod needs, e.g. "0.1".</summary>
    [JsonPropertyName("loaderVersion")] public string LoaderVersion { get; set; } = "0.1";

    /// <summary>Optional: SHA-256 hashes of the vanilla game data.win this mod was tested on.</summary>
    [JsonPropertyName("testedGameHashes")] public List<string> TestedGameHashes { get; set; } = new();

    /// <summary>Optional: ids of mods that must be applied before this one, if they are installed.</summary>
    [JsonPropertyName("loadAfter")] public List<string> LoadAfter { get; set; } = new();

    /// <summary>Optional: ids of mods this one can't be used with.</summary>
    [JsonPropertyName("incompatibleWith")] public List<string> IncompatibleWith { get; set; } = new();

    [JsonPropertyName("patches")] public List<ModPatch> Patches { get; set; } = new();

    /// <summary>Folder the mod was loaded from (not part of the json).</summary>
    [JsonIgnore] public string Folder { get; set; } = "";

    public static readonly string[] Modes = { "append", "prepend", "replace", "create", "findreplace" };
    static readonly Regex IdPattern = new("^[a-z0-9][a-z0-9-]{1,47}$");

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Reads and validates a mod folder. Returns null and fills errors if it's unusable.</summary>
    public static ModManifest? Load(string folder, List<string> errors)
    {
        string path = Path.Combine(folder, "mod.json");
        string label = Path.GetFileName(folder);
        if (!System.IO.File.Exists(path))
        {
            errors.Add($"{label}: no mod.json - skipped.");
            return null;
        }

        ModManifest? m;
        try { m = JsonSerializer.Deserialize<ModManifest>(System.IO.File.ReadAllText(path), JsonOptions); }
        catch (JsonException e)
        {
            errors.Add($"{label}: mod.json is not valid JSON ({e.Message}).");
            return null;
        }
        if (m is null) { errors.Add($"{label}: mod.json is empty."); return null; }
        m.Folder = folder;

        var problems = new List<string>();
        if (!IdPattern.IsMatch(m.Id))
            problems.Add("\"id\" must be 2-48 characters of lowercase letters, numbers and dashes (e.g. \"my-cool-mod\")");
        if (string.IsNullOrWhiteSpace(m.Name)) problems.Add("\"name\" is missing");
        if (string.IsNullOrWhiteSpace(m.Version)) problems.Add("\"version\" is missing");
        if (m.Patches.Count == 0) problems.Add("\"patches\" is empty");
        if (CompareVersions(m.LoaderVersion, LoaderInfo.Version) > 0)
            problems.Add($"needs loader {m.LoaderVersion} or newer (this is {LoaderInfo.Version})");

        for (int i = 0; i < m.Patches.Count; i++)
        {
            var p = m.Patches[i];
            string where = $"patch #{i + 1}";
            p.Mode = p.Mode.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(p.Target)) problems.Add($"{where}: \"target\" is missing");
            if (!Modes.Contains(p.Mode)) problems.Add($"{where}: unknown mode \"{p.Mode}\" (use {string.Join(", ", Modes)})");
            if (p.Mode == "findreplace")
            {
                if (string.IsNullOrEmpty(p.Find)) problems.Add($"{where}: findreplace needs \"find\"");
                if (p.Replace is null) problems.Add($"{where}: findreplace needs \"replace\"");
            }
            else if (Modes.Contains(p.Mode))
            {
                if (string.IsNullOrWhiteSpace(p.File)) problems.Add($"{where}: \"file\" is missing");
                else if (!System.IO.File.Exists(m.ResolveFile(p.File))) problems.Add($"{where}: file \"{p.File}\" not found");
            }
        }

        if (problems.Count > 0)
        {
            errors.Add($"{label}: " + string.Join("; ", problems) + ".");
            return null;
        }
        return m;
    }

    /// <summary>Full path of a file inside the mod; refuses paths that escape the mod folder.</summary>
    public string ResolveFile(string relative)
    {
        string full = Path.GetFullPath(Path.Combine(Folder, relative));
        string root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{Id}: file path \"{relative}\" points outside the mod folder.");
        return full;
    }

    public string ReadPatchFile(ModPatch p) => System.IO.File.ReadAllText(ResolveFile(p.File!));

    /// <summary>Compares "1.2.3"-style versions. Missing parts count as 0.</summary>
    public static int CompareVersions(string a, string b)
    {
        int[] Parse(string v) => v.Split('.', '-').Select(s => int.TryParse(s, out int n) ? n : 0).ToArray();
        var x = Parse(a); var y = Parse(b);
        for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            int c = (i < x.Length ? x[i] : 0).CompareTo(i < y.Length ? y[i] : 0);
            if (c != 0) return c;
        }
        return 0;
    }
}

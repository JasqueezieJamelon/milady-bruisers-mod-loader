using System.Diagnostics;

namespace MiladyLoader;

/// <summary>Starts the game - through Steam when possible, so the overlay, playtime and cloud saves work.</summary>
public static class GameLauncher
{
    /// <summary>
    /// Reads the Steam app id from the appmanifest_*.acf next to the game's install folder
    /// (steamapps/common/&lt;game&gt; -> steamapps/appmanifest_&lt;id&gt;.acf whose "installdir" matches).
    /// </summary>
    public static string? FindSteamAppId(Workspace ws) => ReadSteamManifest(ws)?.GetValueOrDefault("appid");

    /// <summary>
    /// When Steam last updated the game and the build it installed, from the same appmanifest
    /// ("LastUpdated", "buildid"). Null if the game isn't a Steam install or the manifest can't be read.
    /// </summary>
    public static (DateTime Updated, string Build)? FindSteamUpdate(Workspace ws)
    {
        var m = ReadSteamManifest(ws);
        if (m == null || !long.TryParse(m.GetValueOrDefault("LastUpdated"), out long secs) || secs <= 0) return null;
        return (DateTimeOffset.FromUnixTimeSeconds(secs).LocalDateTime, m.GetValueOrDefault("buildid", "?"));
    }

    /// <summary>The top-level "key" "value" pairs of the appmanifest_*.acf whose "installdir" is the game folder.</summary>
    static Dictionary<string, string>? ReadSteamManifest(Workspace ws)
    {
        try
        {
            var common = Directory.GetParent(ws.GameDir);
            var steamapps = common?.Parent;
            if (steamapps == null || !common!.Name.Equals("common", StringComparison.OrdinalIgnoreCase)) return null;
            string installDir = Path.GetFileName(ws.GameDir);
            foreach (var acf in Directory.GetFiles(steamapps.FullName, "appmanifest_*.acf"))
            {
                var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var line in File.ReadLines(acf))
                {
                    var parts = line.Trim().Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Where(p => p.Length > 0).ToArray();
                    if (parts.Length >= 2) kv.TryAdd(parts[0], parts[1]);
                }
                if (kv.TryGetValue("installdir", out var dir) && dir.Equals(installDir, StringComparison.OrdinalIgnoreCase)) return kv;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }

    /// <summary>Launches the game. Returns a short description of how, for the status line.</summary>
    public static string Launch(Workspace ws)
    {
        var appId = FindSteamAppId(ws);
        if (appId != null)
        {
            Process.Start(new ProcessStartInfo($"steam://rungameid/{appId}") { UseShellExecute = true });
            return "Starting the game through Steam...";
        }
        string exe = Path.Combine(ws.GameDir, LoaderInfo.GameExe);
        Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = ws.GameDir, UseShellExecute = true });
        return "Starting the game...";
    }
}

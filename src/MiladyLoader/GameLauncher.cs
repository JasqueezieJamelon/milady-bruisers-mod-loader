using System.Diagnostics;

namespace MiladyLoader;

/// <summary>Starts the game - through Steam when possible, so the overlay, playtime and cloud saves work.</summary>
public static class GameLauncher
{
    /// <summary>
    /// Reads the Steam app id from the appmanifest_*.acf next to the game's install folder
    /// (steamapps/common/&lt;game&gt; -> steamapps/appmanifest_&lt;id&gt;.acf whose "installdir" matches).
    /// </summary>
    public static string? FindSteamAppId(Workspace ws)
    {
        try
        {
            var common = Directory.GetParent(ws.GameDir);
            var steamapps = common?.Parent;
            if (steamapps == null || !common!.Name.Equals("common", StringComparison.OrdinalIgnoreCase)) return null;
            string installDir = Path.GetFileName(ws.GameDir);
            foreach (var acf in Directory.GetFiles(steamapps.FullName, "appmanifest_*.acf"))
            {
                string? id = null, dir = null;
                foreach (var line in File.ReadLines(acf))
                {
                    var parts = line.Trim().Split('"', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Where(p => p.Length > 0).ToArray();
                    if (parts.Length < 2) continue;
                    if (parts[0].Equals("appid", StringComparison.OrdinalIgnoreCase)) id = parts[1];
                    else if (parts[0].Equals("installdir", StringComparison.OrdinalIgnoreCase)) dir = parts[1];
                }
                if (id != null && dir != null && dir.Equals(installDir, StringComparison.OrdinalIgnoreCase)) return id;
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

using System.Runtime.InteropServices;

namespace MiladyLoader;

public static class Program
{
    const string Usage = """
        Usage: MiladyLoader [command] [--game "<game folder>"]

        With no command, the launcher window opens.

        Commands:
          build              Rebuild the game with every switched-on mod, in load order.
          check              Show what a build would do, including conflicts, without changing anything.
          list               Show installed mods and the load order.
          enable <mod-id>    Switch a mod on.
          disable <mod-id>   Switch a mod off.
          move <mod-id> <n>  Move a mod to position n in the load order (1 = applied first).
          restore            Put the base game file back with no loader mods.
          play               Rebuild if anything changed, then start the game.

        Mods go in MiladyLoader/mods/<mod folder>/ (each with a mod.json).
        """;

    [DllImport("kernel32.dll")] static extern bool AttachConsole(int processId);

    [STAThread]
    public static int Main(string[] args)
    {
        // Double-clicked (or only --game given): open the launcher window.
        if (args.Length == 0 || (args.Length == 2 && args[0] == "--game"))
            return Gui.MainForm.Run(args.Length == 2 ? args[1] : null);

        // Run from a terminal with a command: print to that terminal.
        if (OperatingSystem.IsWindows()) AttachConsole(-1);
        Console.WriteLine();
        return RunCli(args);
    }

    static int RunCli(string[] args)
    {
        var rest = new List<string>();
        string? game = null;
        bool noPause = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--game" && i + 1 < args.Length) game = args[++i];
            else if (args[i] == "--no-pause") noPause = true;
            else if (args[i] is "-h" or "--help" or "help") { Console.WriteLine(Usage); return 0; }
            else rest.Add(args[i]);
        }
        string command = rest.Count > 0 ? rest[0].ToLowerInvariant() : "check";

        Console.WriteLine($"{LoaderInfo.Name} {LoaderInfo.Version}");
        int code;
        try
        {
            var ws = Workspace.Locate(game);
            Console.WriteLine($"Game folder: {ws.GameDir}");
            code = Run(ws, command, rest.Skip(1).ToList());
        }
        catch (LoaderException e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine(e.Message);
            Console.ResetColor();
            code = 1;
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine("Unexpected error - nothing was changed unless the report above says otherwise.");
            Console.WriteLine(e);
            Console.ResetColor();
            code = 2;
        }

        if (noPause) { } // kept so old shortcuts with --no-pause still work
        return code;
    }

    static int Run(Workspace ws, string command, List<string> a)
    {
        var builder = new Builder(ws);
        var r = new Report();
        switch (command)
        {
            case "build":
            {
                bool ok = builder.Build(r, msg => Console.WriteLine(msg));
                Finish(ws, r);
                Console.WriteLine(ok ? "\nDone. Launch the game from Steam." : "\nBuild failed - your game file was not changed.");
                return ok ? 0 : 1;
            }
            case "play":
            {
                if (builder.NeedsBuild(out string reason))
                {
                    Console.WriteLine($"Rebuilding ({reason})...");
                    bool ok = builder.Build(r, msg => Console.WriteLine(msg));
                    Finish(ws, r);
                    if (!ok) { Console.WriteLine("\nBuild failed - your game file was not changed."); return 1; }
                }
                else Console.WriteLine("Up to date - no rebuild needed.");
                Console.WriteLine(GameLauncher.Launch(ws));
                return 0;
            }
            case "check":
            {
                var mods = builder.ResolveMods(r, persist: false);
                var st = ws.LoadState();
                if (!string.IsNullOrEmpty(st.BaseHash)) builder.DescribeBase(st.BaseHash, r);
                if (builder.AdoptionProblem() is string problem) r.Errors.Add(problem);
                builder.Analyze(mods, r);
                r.Info.Insert(0, "Load order: " + (mods.Count == 0 ? "(no mods switched on)" : string.Join(" > ", mods.Select(m => $"{m.Name} {m.Version}"))));
                Finish(ws, r, save: false);
                return r.Errors.Count == 0 ? 0 : 1;
            }
            case "list":
            {
                builder.ResolveMods(r);
                var found = builder.Discover(new Report());
                var order = ws.LoadOrder();
                Console.WriteLine();
                if (order.Count == 0) Console.WriteLine("No mods installed. Put mod folders in: " + ws.ModsDir);
                int n = 1;
                foreach (var e in order)
                {
                    var m = found[e.Id];
                    Console.WriteLine($"{n++,3}. [{(e.Enabled ? "on " : "off")}] {m.Name} {m.Version}  ({e.Id})  by {m.Author}");
                }
                if (r.Errors.Count > 0) Finish(ws, r, save: false);
                return 0;
            }
            case "enable":
            case "disable":
            case "move":
            {
                if (a.Count == 0) throw new LoaderException($"Which mod? Example: MiladyLoader {command} my-mod-id");
                builder.ResolveMods(r);
                var order = ws.LoadOrder();
                var entry = order.FirstOrDefault(e => e.Id == a[0])
                            ?? throw new LoaderException($"No installed mod has the id \"{a[0]}\". Run \"MiladyLoader list\" to see ids.");
                if (command == "move")
                {
                    if (a.Count < 2 || !int.TryParse(a[1], out int pos)) throw new LoaderException("Example: MiladyLoader move my-mod-id 1");
                    order.Remove(entry);
                    order.Insert(Math.Clamp(pos - 1, 0, order.Count), entry);
                }
                else entry.Enabled = command == "enable";
                ws.SaveLoadOrder(order);
                Console.WriteLine($"Saved. Run the loader (build) to apply the change.");
                return 0;
            }
            case "restore":
                builder.Restore(r);
                Finish(ws, r);
                return 0;
            default:
                Console.WriteLine(Usage);
                return 1;
        }
    }

    static void Finish(Workspace ws, Report r, bool save = true)
    {
        string text = r.Render();
        Console.WriteLine();
        Console.WriteLine(text);
        if (save) File.WriteAllText(Path.Combine(ws.LogsDir, "last-build.txt"), text);
    }
}

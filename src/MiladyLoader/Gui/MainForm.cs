using System.Diagnostics;

namespace MiladyLoader.Gui;

/// <summary>
/// The launcher window: shows installed mods (switch on/off, reorder), what a build would do,
/// and a PLAY button that rebuilds only when something changed and then starts the game.
/// </summary>
sealed class MainForm : Form
{
    readonly Workspace ws;
    readonly Builder builder;
    List<ModRow> rows = new();
    bool busy, closing;
    /// <summary>Keeps a result (failed build, restore) on screen until the player does something else.</summary>
    bool holdStatus;

    readonly ModList list = new() { Dock = DockStyle.Fill };
    readonly PillButton upButton = new() { Text = "Move up" };
    readonly PillButton downButton = new() { Text = "Move down" };
    readonly PillButton restoreButton = new() { Text = "Restore original game" };
    readonly PillButton folderButton = new() { Text = "Open mods folder" };
    readonly PillButton vanillaButton = new() { Text = "Play without mods" };
    readonly PillButton playButton = new() { Text = "PLAY", Filled = true, Font = Theme.Play };
    readonly Label detailName = new() { AutoSize = true, Font = Theme.BodyBold, ForeColor = Theme.Text, Margin = new Padding(0) };
    readonly Label detailMeta = new() { AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0, 2, 0, 6) };
    readonly Label detailText = new() { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0) };
    readonly RichTextBox check = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.Panel,
        ForeColor = Theme.Text, Font = Theme.Small, DetectUrls = false, ScrollBars = RichTextBoxScrollBars.Vertical,
        Cursor = Cursors.Default, TabStop = false,
    };
    readonly Label status = new() { AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0) };
    readonly Label statusSub = new() { AutoSize = true, ForeColor = Theme.Muted, Font = Theme.Small, Margin = new Padding(0, 3, 0, 0) };

    public MainForm(Workspace ws)
    {
        this.ws = ws;
        builder = new Builder(ws);

        Text = LoaderInfo.Name;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(880, 580);
        MinimumSize = new Size(740, 500);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Back;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        try { Icon = Theme.AppIcon() ?? Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath); } catch { }

        Controls.Add(BuildLayout());

        list.ToggleRequested += Toggle;
        list.SelectedIndexChanged += (_, _) => ShowDetails();
        upButton.Click += (_, _) => MoveSelected(-1);
        downButton.Click += (_, _) => MoveSelected(+1);
        folderButton.Click += (_, _) => OpenFolder(ws.ModsDir);
        restoreButton.Click += (_, _) => RestoreClicked();
        vanillaButton.Click += async (_, _) => await PlayAsync(withMods: false);
        playButton.Click += async (_, _) => await PlayAsync(withMods: true);
        Activated += (_, _) => { if (!busy && !holdStatus) Reload(); };
        AcceptButton = playButton;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(this);
    }

    // ---------- layout ----------

    Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(20, 16, 20, 16) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(Header(), 0, 0);
        root.Controls.Add(Body(), 0, 1);
        root.Controls.Add(Footer(), 0, 2);
        return root;
    }

    Control Header()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 0, 0, 12) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Logo to the left of the title, about the height of the title + subtitle.
        t.Controls.Add(new SmoothPicture { Image = Theme.Logo, Size = new Size(48, 48), Margin = new Padding(0, 0, 12, 0), Anchor = AnchorStyles.Left }, 0, 0);

        var left = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
        var title = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        title.Controls.Add(new Label { Text = "MILADY", AutoSize = true, Font = Theme.Title, ForeColor = Theme.Pink, Margin = new Padding(0) });
        title.Controls.Add(new Label { Text = "BRUISERS MOD LOADER", AutoSize = true, Font = Theme.Title, ForeColor = Theme.Text, Margin = new Padding(0) });
        left.Controls.Add(title);
        left.Controls.Add(new Label
        {
            Text = $"v{LoaderInfo.Version}   ·   {ws.GameDir}", AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted,
            Margin = new Padding(2, 2, 0, 0), AutoEllipsis = true,
        });
        left.Anchor = AnchorStyles.Left;
        t.Controls.Add(left, 1, 0);

        folderButton.Size = new Size(150, 34);
        folderButton.Anchor = AnchorStyles.Right;
        t.Controls.Add(folderButton, 2, 0);
        return t;
    }

    Control Body()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        // Left: the mod list.
        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = new Padding(0, 0, 10, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        left.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        left.Controls.Add(SectionTitle("MODS", "top of the list is applied first"), 0, 0);
        left.Controls.Add(Card(list, new Padding(0, 6, 0, 6)), 0, 1);

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 10, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        foreach (var b in new[] { upButton, downButton }) { b.Size = new Size(104, 32); b.Margin = new Padding(0, 0, 8, 0); }
        restoreButton.Size = new Size(176, 32);
        restoreButton.Margin = new Padding(0);
        buttons.Controls.Add(upButton, 0, 0);
        buttons.Controls.Add(downButton, 1, 0);
        buttons.Controls.Add(restoreButton, 3, 0);
        left.Controls.Add(buttons, 0, 2);
        t.Controls.Add(left, 0, 0);

        // Right: details of the selected mod, and the build check.
        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = new Padding(10, 0, 0, 0) };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(SectionTitle("DETAILS", null), 0, 0);

        var details = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
        details.Controls.AddRange(new Control[] { detailName, detailMeta, detailText });
        var detailCard = Card(details, new Padding(14, 12, 14, 12));
        details.Dock = DockStyle.Top;
        detailCard.AutoSize = true;
        detailCard.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        detailCard.Margin = new Padding(0, 6, 0, 14);
        detailCard.Dock = DockStyle.Top;
        detailCard.Resize += (_, _) =>
        {
            int w = Math.Max(100, detailCard.ClientSize.Width - detailCard.Padding.Horizontal);
            foreach (var l in new[] { detailName, detailMeta, detailText }) l.MaximumSize = new Size(w, 0);
        };
        right.Controls.Add(detailCard, 0, 1);

        right.Controls.Add(SectionTitle("BUILD CHECK", null), 0, 2);
        right.Controls.Add(Card(check, new Padding(12, 10, 8, 10)), 0, 3);
        t.Controls.Add(right, 1, 0);
        return t;
    }

    Control Footer()
    {
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 16, 0, 0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var s = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0) };
        s.Controls.Add(status);
        s.Controls.Add(statusSub);
        t.Controls.Add(s, 0, 0);
        t.Resize += (_, _) =>
        {
            int w = Math.Max(120, t.ClientSize.Width - 360);
            status.MaximumSize = statusSub.MaximumSize = new Size(w, 0);
        };

        vanillaButton.Size = new Size(150, 48);
        vanillaButton.Margin = new Padding(0, 0, 10, 0);
        vanillaButton.Anchor = AnchorStyles.Right;
        playButton.Size = new Size(176, 48);
        playButton.Margin = new Padding(0);
        playButton.Anchor = AnchorStyles.Right;
        t.Controls.Add(vanillaButton, 1, 0);
        t.Controls.Add(playButton, 2, 0);
        return t;
    }

    static Control SectionTitle(string title, string? hint)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        f.Controls.Add(new Label { Text = title, AutoSize = true, Font = Theme.Section, ForeColor = Theme.Pink, Margin = new Padding(2, 0, 8, 0) });
        if (hint != null)
            f.Controls.Add(new Label { Text = hint, AutoSize = true, Font = Theme.Small, ForeColor = Theme.Muted, Margin = new Padding(0) });
        return f;
    }

    /// <summary>A rounded panel-coloured card around a control.</summary>
    static Panel Card(Control inner, Padding padding)
    {
        var card = new CardPanel { Dock = DockStyle.Fill, Padding = padding, Margin = new Padding(0, 6, 0, 0) };
        card.Controls.Add(inner);
        inner.Dock = DockStyle.Fill;
        return card;
    }

    sealed class CardPanel : Panel
    {
        public CardPanel()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Panel;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent?.BackColor ?? Theme.Back);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = Theme.Rounded(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), LogicalToDeviceUnits(10));
            using var fill = new SolidBrush(Theme.Panel);
            using var pen = new Pen(Theme.Border, 1f);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }
    }

    // ---------- data ----------

    /// <summary>Re-reads mods, load order and the game file, and refreshes everything on screen.</summary>
    void Reload(string? selectId = null)
    {
        selectId ??= (list.SelectedItem as ModRow)?.Mod?.Id ?? (list.SelectedItem as ModRow)?.External?.Name;
        var r = new Report();
        bool needs = false;
        string reason = "";
        try
        {
            var mods = builder.ResolveMods(r, persist: true);
            var found = builder.Discover(new Report());
            var order = ws.LoadOrder();
            string baseHash = builder.ProspectiveBaseHash();
            var external = builder.DescribeBase(baseHash, r, warnMissing: false).Concat(builder.MissingExternal(baseHash)).ToList();
            if (builder.AdoptionProblem() is string problem) r.Errors.Add(problem);
            builder.Analyze(mods, r);
            needs = builder.NeedsBuild(out reason);

            rows = order.Where(e => found.ContainsKey(e.Id))
                        .Select((e, i) => new ModRow { Mod = found[e.Id], Enabled = e.Enabled, Position = i + 1 })
                        .Concat(external.Select(x => new ModRow { External = x }))
                        .ToList();
            r.Info.Insert(0, "Load order: " + (mods.Count == 0 ? "(no mods switched on)" : string.Join(" > ", mods.Select(m => m.Name))));
        }
        catch (Exception e)
        {
            r.Errors.Add(e.Message);
        }

        list.BeginUpdate();
        list.Items.Clear();
        foreach (var row in rows) list.Items.Add(row);
        int sel = rows.FindIndex(x => (x.Mod?.Id ?? x.External?.Name) == selectId);
        list.SelectedIndex = sel >= 0 ? sel : (rows.Count > 0 ? 0 : -1);
        list.EndUpdate();
        ShowDetails();
        ShowCheck(r, needs, reason);

        int on = rows.Count(x => x.Enabled && !x.IsExternal), ext = rows.Count(x => x.IsExternal && !x.External!.Missing);
        SetStatus(rows.Count == 0 ? "No mods installed yet." : $"{on + ext} mod{(on + ext == 1 ? "" : "s")} will be loaded.",
                  rows.Count == 0 ? "Put a mod's folder in the mods folder, then come back to this window." :
                  rows.Any(x => x.IsExternal && x.External!.Missing) ? "A mod needs reinstalling - see the build check." :
                  needs ? "PLAY rebuilds the game file first (takes a few seconds), then starts the game." : "Everything is up to date - PLAY starts the game right away.");
    }

    void ShowDetails()
    {
        if (list.SelectedItem is not ModRow row)
        {
            detailName.Text = "No mod selected";
            detailMeta.Text = "";
            detailText.Text = "Mods go in: " + ws.ModsDir;
            upButton.Enabled = downButton.Enabled = false;
            return;
        }
        detailName.Text = row.Name;
        if (row.IsExternal && row.External!.Missing)
        {
            detailMeta.Text = $"v{row.Version}  ·  needs reinstalling";
            detailText.Text = $"Steam replaced the game file (usually a game update), which removed {row.Name}.\n\n" +
                              $"To get it back: close the game, run {row.Name}'s installer and click Install. If it offers an " +
                              "update, take it - the new game version may need one. Then press PLAY here.\n\n" +
                              "Until then, PLAY starts the game without it.";
        }
        else if (row.IsExternal)
        {
            detailMeta.Text = $"v{row.Version}  ·  installed with its own installer";
            detailText.Text = "This mod is already part of your game file, so the loader builds on top of it. " +
                              "To remove it, use its own uninstaller (or Steam's \"Verify integrity of game files\")." +
                              (row.External!.Parts.Count > 0 ? "\n\nIncludes: " + string.Join(", ", row.External.Parts) : "");
        }
        else
        {
            var m = row.Mod!;
            detailMeta.Text = string.Join("  ·  ", new[] { "v" + m.Version, string.IsNullOrWhiteSpace(m.Author) ? null : "by " + m.Author, row.Enabled ? "on" : "off" }
                                                   .Where(s => s != null));
            detailText.Text = (string.IsNullOrWhiteSpace(m.Description) ? "No description." : m.Description) +
                              $"\n\nFolder: mods\\{Path.GetFileName(m.Folder)}";
        }
        int loaderRows = rows.Count(x => !x.IsExternal);
        upButton.Enabled = !busy && !row.IsExternal && row.Position > 1;
        downButton.Enabled = !busy && !row.IsExternal && row.Position < loaderRows;
    }

    void ShowCheck(Report r, bool needs, string reason)
    {
        check.Clear();
        void Line(string text, Color c, Font? f = null)
        {
            check.SelectionStart = check.TextLength;
            check.SelectionColor = c;
            check.SelectionFont = f ?? Theme.Body;
            check.AppendText(text + "\n");
        }
        void Gap() => Line("", Theme.Muted, Theme.Small);

        // At a glance: one headline and a sentence or two. The full report is in logs/last-build.txt.
        var missing = rows.Where(x => x.IsExternal && x.External!.Missing).Select(x => x.Name).ToList();
        string names = string.Join(" and ", missing);
        if (r.Errors.Count > 0)
        {
            Line("Can't build", Theme.Bad, Theme.Heading);
            foreach (var x in r.Errors) { Gap(); Line(x, Theme.Text); }
        }
        else if (missing.Count > 0)
        {
            Line($"{names} need{(missing.Count == 1 ? "s" : "")} reinstalling", Theme.Warn, Theme.Heading);
            Gap();
            Line($"A Steam update removed {(missing.Count == 1 ? "it" : "them")} from the game.", Theme.Text);
            Line($"Run {names}'s installer, click Install, then come back here.", Theme.Text);
        }
        else
        {
            Line("Ready to play", Theme.Good, Theme.Heading);
            Gap();
            if (!needs) Line("Everything is up to date.", Theme.Text);
            else
            {
                Line("PLAY will rebuild first.", Theme.Text);
                Line(char.ToUpper(reason[0]) + reason[1..] + ".", Theme.Muted);
            }
        }
        if (r.Errors.Count == 0)
        {
            foreach (var x in r.Conflicts) { Gap(); Line(x, Theme.Conflict); }
            foreach (var x in r.Warnings) { Gap(); Line(x, Theme.Warn); }
        }
        check.SelectionStart = 0;
        check.ScrollToCaret();
    }

    void SetStatus(string main, string sub = "", Color? color = null)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => SetStatus(main, sub, color))); return; }
        status.Text = main;
        status.ForeColor = color ?? Theme.Text;
        statusSub.Text = sub;
    }

    void SetBusy(bool on)
    {
        busy = on;
        foreach (var b in new Control[] { playButton, vanillaButton, restoreButton, list }) b.Enabled = !on;
        UseWaitCursor = on;
        ShowDetails();
    }

    // ---------- actions ----------

    void Toggle(ModRow row)
    {
        if (busy || row.IsExternal) return;
        holdStatus = false;
        var order = ws.LoadOrder();
        var e = order.FirstOrDefault(x => x.Id == row.Mod!.Id);
        if (e == null) return;
        e.Enabled = !e.Enabled;
        ws.SaveLoadOrder(order);
        Reload(row.Mod!.Id);
    }

    void MoveSelected(int delta)
    {
        if (busy || list.SelectedItem is not ModRow row || row.IsExternal) return;
        holdStatus = false;
        var order = ws.LoadOrder();
        int i = order.FindIndex(x => x.Id == row.Mod!.Id), j = i + delta;
        if (i < 0 || j < 0 || j >= order.Count) return;
        (order[i], order[j]) = (order[j], order[i]);
        ws.SaveLoadOrder(order);
        Reload(row.Mod!.Id);
        list.Focus();
    }

    async Task PlayAsync(bool withMods)
    {
        if (busy) return;
        var missing = rows.Where(x => x.IsExternal && x.External!.Missing).Select(x => x.Name).ToList();
        if (withMods && missing.Count > 0)
        {
            string names = string.Join(" and ", missing);
            var answer = MessageBox.Show(this,
                $"{names} isn't in the game right now - a Steam update removed it.\n\n" +
                $"Play without it anyway?\n\n(To get it back: run {names}'s installer and click Install.)",
                "Play without " + names, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;
        }
        holdStatus = false;
        SetBusy(true);
        Report? report = null;
        try
        {
            bool ok = await Task.Run(() =>
            {
                if (Workspace.GameIsRunning())
                    throw new LoaderException("The game is already running. Close it first, then press PLAY.");
                report = new Report();
                if (!withMods)
                {
                    SetStatus("Turning mods off for this session...");
                    builder.Restore(report);
                }
                else if (builder.NeedsBuild(out _))
                {
                    SetStatus("Rebuilding the game file...", "This takes a few seconds.");
                    bool built = builder.Build(report, msg => SetStatus(msg, "This takes a few seconds."));
                    File.WriteAllText(Path.Combine(ws.LogsDir, "last-build.txt"), report.Render());
                    return built;
                }
                return true;
            });

            if (!ok)
            {
                ShowCheck(report!, false, "");
                SetStatus("Build failed - your game file wasn't changed.", "See the build check on the right for which mod failed.", Theme.Bad);
                holdStatus = true;
                return;
            }

            string how = GameLauncher.Launch(ws);
            SetStatus(how, withMods ? "Have fun!" : "Mods are off this time. Press PLAY next time to turn them back on.", Theme.Good);
            closing = true;
            await Task.Delay(1800);
            Close();
        }
        catch (LoaderException e)
        {
            SetStatus(e.Message, "", Theme.Bad);
            holdStatus = true;
        }
        catch (Exception e)
        {
            SetStatus("Something went wrong: " + e.Message, "Details were saved to MiladyLoader\\logs\\last-error.txt.", Theme.Bad);
            holdStatus = true;
            try { File.WriteAllText(Path.Combine(ws.LogsDir, "last-error.txt"), e.ToString()); } catch { }
        }
        finally
        {
            if (!closing) SetBusy(false);
        }
    }

    void RestoreClicked()
    {
        if (busy) return;
        var answer = MessageBox.Show(this,
            "Put the game file back the way it was before the loader added mods?\n\n" +
            "Your mods stay installed - press PLAY any time to add them back. " +
            "Mods installed with their own installer (like The Undercard) are not removed.",
            "Restore original game", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (answer != DialogResult.OK) return;
        try
        {
            var r = new Report();
            builder.Restore(r);
            File.WriteAllText(Path.Combine(ws.LogsDir, "last-build.txt"), r.Render());
            Reload();
            SetStatus("Original game restored.", "Your mods are still installed - press PLAY to use them again.", Theme.Good);
            holdStatus = true;
        }
        catch (Exception e) { SetStatus(e.Message, "", Theme.Bad); holdStatus = true; }
    }

    static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    // ---------- startup ----------

    /// <summary>Finds the game (asking for the folder if needed) and runs the window.</summary>
    public static int Run(string? gameArg)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Workspace? ws = null;
        string? folder = gameArg;
        while (ws == null)
        {
            try { ws = Workspace.Locate(folder); }
            catch (LoaderException e)
            {
                var answer = MessageBox.Show(e.Message + "\n\nChoose the game folder now?", LoaderInfo.Name,
                                             MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (answer != DialogResult.OK) return 1;
                using var dlg = new FolderBrowserDialog { Description = $"Select the {LoaderInfo.GameFolderName} folder (the one with data.win)", UseDescriptionForTitle = true };
                if (dlg.ShowDialog() != DialogResult.OK) return 1;
                folder = dlg.SelectedPath;
            }
        }
        Application.Run(new MainForm(ws));
        return 0;
    }
}

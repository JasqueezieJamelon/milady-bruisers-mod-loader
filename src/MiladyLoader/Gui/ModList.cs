using System.Drawing.Drawing2D;

namespace MiladyLoader.Gui;

/// <summary>One row in the mod list: a loader mod (can be switched on/off and moved) or a mod installed separately.</summary>
sealed class ModRow
{
    public ModManifest? Mod;
    public ExternalMod? External;
    public bool Enabled;
    public int Position;
    public bool IsExternal => External != null;
    public string Name => Mod?.Name ?? External!.Name;
    public string Version => Mod?.Version ?? External!.Version;
}

/// <summary>Owner-drawn list with a checkbox, name, version and author on each row.</summary>
sealed class ModList : ListBox
{
    /// <summary>Raised when the player clicks a row's checkbox, double-clicks it, or presses Space.</summary>
    public event Action<ModRow>? ToggleRequested;

    public ModList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        BackColor = Theme.Panel;
        ForeColor = Theme.Text;
        IntegralHeight = false;
        Font = Theme.Body;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = LogicalToDeviceUnits(52);
    }

    int Px(int v) => LogicalToDeviceUnits(v);
    Rectangle CheckRect(Rectangle row) => new(row.X + Px(14), row.Y + (row.Height - Px(20)) / 2, Px(20), Px(20));

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var b = e.Bounds;
        using (var bg = new SolidBrush(Theme.Panel)) g.FillRectangle(bg, b);
        if (e.Index < 0 || e.Index >= Items.Count) return;
        var row = (ModRow)Items[e.Index];
        bool selected = (e.State & DrawItemState.Selected) != 0;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var card = new RectangleF(b.X + Px(6), b.Y + Px(3), b.Width - Px(12), b.Height - Px(6));
        using (var path = Theme.Rounded(card, Px(8)))
        using (var fill = new SolidBrush(selected ? Theme.RowSelected : Theme.Row))
        {
            g.FillPath(fill, path);
            if (selected) { using var pen = new Pen(Theme.Pink, 1.2f); g.DrawPath(pen, path); }
        }

        var cb = CheckRect(b);
        if (row.IsExternal)
        {
            // A small lock-ish badge instead of a checkbox: these are part of the game file already.
            using var pen = new Pen(Theme.Muted, 1.4f);
            using var p = Theme.Rounded(new RectangleF(cb.X + Px(3), cb.Y + Px(8), cb.Width - Px(6), cb.Height - Px(9)), Px(2));
            g.DrawPath(pen, p);
            g.DrawArc(pen, cb.X + Px(6), cb.Y + Px(2), cb.Width - Px(12), Px(12), 180, 180);
        }
        else
        {
            using var p = Theme.Rounded(cb, Px(5));
            if (row.Enabled)
            {
                using var fill = new SolidBrush(Theme.Pink);
                g.FillPath(fill, p);
                using var tick = new Pen(Theme.OnPink, Px(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                g.DrawLines(tick, new[]
                {
                    new PointF(cb.X + cb.Width * 0.26f, cb.Y + cb.Height * 0.52f),
                    new PointF(cb.X + cb.Width * 0.44f, cb.Y + cb.Height * 0.70f),
                    new PointF(cb.X + cb.Width * 0.76f, cb.Y + cb.Height * 0.32f),
                });
            }
            else
            {
                using var pen = new Pen(Theme.Muted, 1.4f);
                g.DrawPath(pen, p);
            }
        }

        int textX = cb.Right + Px(14);
        var nameColor = row.IsExternal || row.Enabled ? Theme.Text : Theme.Muted;
        var nameSize = TextRenderer.MeasureText(g, row.Name, Theme.BodyBold, Size.Empty, TextFormatFlags.NoPadding);
        int rightEdge = b.Right - Px(16);
        int line1 = b.Y + Px(9);
        TextRenderer.DrawText(g, row.Name, Theme.BodyBold, new Rectangle(textX, line1, rightEdge - textX - Px(60), nameSize.Height),
            nameColor, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        int vx = Math.Min(textX + nameSize.Width + Px(6), rightEdge - Px(60));
        if (!string.IsNullOrEmpty(row.Version))
            TextRenderer.DrawText(g, "v" + row.Version, Theme.Small, new Point(vx, line1 + Px(2)), Theme.Muted, TextFormatFlags.NoPadding);

        bool missing = row.IsExternal && row.External!.Missing;
        string sub = missing ? "Removed by a game update - run its installer again"
                   : row.IsExternal ? "Installed separately - loader mods go on top of it"
                   : (string.IsNullOrWhiteSpace(row.Mod!.Author) ? "" : "by " + row.Mod.Author) + (row.Enabled ? "" : (string.IsNullOrWhiteSpace(row.Mod.Author) ? "" : "  ·  ") + "off");
        TextRenderer.DrawText(g, sub, Theme.Small, new Rectangle(textX, line1 + Px(20), rightEdge - textX - Px(70), Px(16)),
            missing ? Theme.Warn : Theme.Muted, TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

        string tag = missing ? "MISSING" : row.IsExternal ? "BASE" : "#" + row.Position;
        var tagSize = TextRenderer.MeasureText(g, tag, Theme.Section, Size.Empty, TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, tag, Theme.Section, new Point(rightEdge - tagSize.Width, b.Y + (b.Height - tagSize.Height) / 2),
            missing ? Theme.Warn : row.IsExternal ? Theme.Muted : (row.Enabled ? Theme.Pink : Theme.Muted), TextFormatFlags.NoPadding);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int i = IndexFromPoint(e.Location);
        if (i < 0 || e.Button != MouseButtons.Left) return;
        var rect = GetItemRectangle(i);
        if (e.X < CheckRect(rect).Right + Px(8)) ToggleRequested?.Invoke((ModRow)Items[i]);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        int i = IndexFromPoint(e.Location);
        if (i >= 0 && e.X >= CheckRect(GetItemRectangle(i)).Right + Px(8)) ToggleRequested?.Invoke((ModRow)Items[i]);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space && SelectedItem is ModRow row) { ToggleRequested?.Invoke(row); e.Handled = true; }
    }
}

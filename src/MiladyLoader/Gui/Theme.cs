using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MiladyLoader.Gui;

/// <summary>Colours and small drawing helpers for the launcher window.</summary>
static class Theme
{
    public static readonly Color Back = Color.FromArgb(21, 18, 26);
    public static readonly Color Panel = Color.FromArgb(31, 26, 38);
    public static readonly Color Row = Color.FromArgb(38, 32, 46);
    public static readonly Color RowSelected = Color.FromArgb(52, 41, 62);
    public static readonly Color Border = Color.FromArgb(60, 51, 70);
    public static readonly Color Text = Color.FromArgb(237, 231, 242);
    public static readonly Color Muted = Color.FromArgb(154, 144, 166);
    public static readonly Color Pink = Color.FromArgb(255, 90, 200);
    public static readonly Color PinkHover = Color.FromArgb(255, 125, 212);
    public static readonly Color PinkDown = Color.FromArgb(217, 67, 159);
    public static readonly Color OnPink = Color.FromArgb(26, 15, 31);
    public static readonly Color Good = Color.FromArgb(110, 231, 168);
    public static readonly Color Warn = Color.FromArgb(255, 200, 87);
    public static readonly Color Conflict = Color.FromArgb(255, 140, 66);
    public static readonly Color Bad = Color.FromArgb(255, 95, 95);

    public const string FontName = "Segoe UI";
    public static readonly Font Body = new(FontName, 9.75f);
    public static readonly Font BodyBold = new(FontName, 9.75f, FontStyle.Bold);
    public static readonly Font Small = new(FontName, 8.5f);
    public static readonly Font Heading = new(FontName, 12f, FontStyle.Bold);
    public static readonly Font Section = new(FontName, 8.25f, FontStyle.Bold);
    public static readonly Font Title = new(FontName, 15f, FontStyle.Bold);
    public static readonly Font Play = new(FontName, 14f, FontStyle.Bold);

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Stream? Resource(string name) => typeof(Theme).Assembly.GetManifestResourceStream("MiladyLoader." + name);

    /// <summary>The loader's logo (256 px, transparent corners), or null if missing.</summary>
    public static readonly Image? Logo = Resource("logo.png") is { } s ? Image.FromStream(s) : null;

    /// <summary>The app icon with every size (16-256 px), so the title bar and taskbar get a sharp one.</summary>
    public static Icon? AppIcon()
    {
        using var s = Resource("milady.ico");
        return s == null ? null : new Icon(s);
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Dark title bar on Windows 10 (20H1+) and 11. Does nothing elsewhere.</summary>
    public static void DarkTitleBar(Form f)
    {
        try { int on = 1; DwmSetWindowAttribute(f.Handle, 20, ref on, sizeof(int)); } catch { }
    }
}

/// <summary>A flat, rounded button: filled (PLAY) or outlined (everything else).</summary>
sealed class PillButton : Button
{
    [System.ComponentModel.DefaultValue(false)]
    public bool Filled { get; set; }
    bool hover, down;

    public PillButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Font = Theme.Body;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Theme.Back);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Rounded(r, Math.Min(10, Height / 2f - 1));
        Color text;
        if (Filled)
        {
            var fill = !Enabled ? Theme.Border : down ? Theme.PinkDown : hover ? Theme.PinkHover : Theme.Pink;
            using var b = new SolidBrush(fill);
            g.FillPath(b, path);
            text = Enabled ? Theme.OnPink : Theme.Muted;
        }
        else
        {
            if (hover && Enabled) { using var b = new SolidBrush(Theme.Row); g.FillPath(b, path); }
            using var pen = new Pen(Enabled && hover ? Theme.Pink : Theme.Border, 1.2f);
            g.DrawPath(pen, path);
            text = Enabled ? Theme.Text : Theme.Muted;
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        if (Focused && ShowFocusCues)
        {
            using var pen = new Pen(Filled ? Theme.OnPink : Theme.Pink, 1f) { DashStyle = DashStyle.Dot };
            var fr = RectangleF.Inflate(r, -3, -3);
            using var fp = Theme.Rounded(fr, Math.Min(8, fr.Height / 2f - 1));
            g.DrawPath(pen, fp);
        }
    }
}

/// <summary>Draws an image scaled smoothly (high-quality downscale from a large source, so it stays sharp).</summary>
sealed class SmoothPicture : Control
{
    public Image? Image { get; init; }

    public SmoothPicture()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Image == null) return;
        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.CompositingQuality = CompositingQuality.HighQuality;
        int side = Math.Min(Width, Height);
        g.DrawImage(Image, new Rectangle((Width - side) / 2, (Height - side) / 2, side, side));
    }
}

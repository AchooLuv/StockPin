namespace StockPin.Core;

/// <summary>
/// A colour palette. The six hex values in each theme come from the product spec;
/// translucent surface / hover / divider values are derived from them.
/// </summary>
public sealed class ThemePalette
{
    public ThemePalette(
        string name,
        string note,
        bool isDark,
        string bgTop,
        string bgBottom,
        string border,
        string text,
        string muted,
        string accent,
        string accentSoft,
        string surface,
        string hover,
        string divider,
        string chipBg,
        string chipText,
        string windowBg,
        string cardBg,
        string inputBg,
        string inputBorder,
        string switchOff,
        string scrollThumb,
        string up,
        string down,
        string flat)
    {
        Name = name;
        Note = note;
        IsDark = isDark;
        BgTop = bgTop;
        BgBottom = bgBottom;
        Border = border;
        Text = text;
        Muted = muted;
        Accent = accent;
        AccentSoft = accentSoft;
        Surface = surface;
        Hover = hover;
        Divider = divider;
        ChipBg = chipBg;
        ChipText = chipText;
        WindowBg = windowBg;
        CardBg = cardBg;
        InputBg = inputBg;
        InputBorder = inputBorder;
        SwitchOff = switchOff;
        ScrollThumb = scrollThumb;
        Up = up;
        Down = down;
        Flat = flat;

        Preview = new LinearGradientBrush(Opaque(bgTop), Opaque(bgBottom), new Point(0, 0), new Point(1, 1));
        Preview.Freeze();
        PreviewText = Solid(text);
        PreviewAccent = Solid(accent);
        PreviewSwatches = new[] { Solid(accentSoft), Solid(accent), Solid(muted) };
    }

    public string Name { get; }
    public string Note { get; }
    public bool IsDark { get; }

    public string BgTop { get; }
    public string BgBottom { get; }
    public string Border { get; }
    public string Text { get; }
    public string Muted { get; }
    public string Accent { get; }
    public string AccentSoft { get; }
    public string Surface { get; }
    public string Hover { get; }
    public string Divider { get; }
    public string ChipBg { get; }
    public string ChipText { get; }
    public string WindowBg { get; }
    public string CardBg { get; }
    public string InputBg { get; }
    public string InputBorder { get; }
    public string SwitchOff { get; }
    public string ScrollThumb { get; }
    public string Up { get; }
    public string Down { get; }
    public string Flat { get; }

    public LinearGradientBrush Preview { get; }
    public SolidColorBrush PreviewText { get; }
    public SolidColorBrush PreviewAccent { get; }
    public SolidColorBrush[] PreviewSwatches { get; }

    internal static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex)!;
    internal static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush(Parse(hex));
        brush.Freeze();
        return brush;
    }
    internal static Color Opaque(string hex)
    {
        var color = Parse(hex);
        color.A = 255;
        return color;
    }
}

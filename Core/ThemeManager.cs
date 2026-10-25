namespace StockPin.Core;

using System.Windows.Media.Effects;

/// <summary>Owns the four palettes and swaps them into the application resource dictionary.</summary>
public static class ThemeManager
{
    private static ResourceDictionary? _themeDictionary;

    public static IReadOnlyList<ThemePalette> Themes { get; } = new[]
    {
        // 主题一：蓝调（深色）
        new ThemePalette(
            index: 0,
            name: "蓝调",
            note: "深色 · 静谧",
            isDark: true,
            bgTop: "#F21E2D5C",
            bgBottom: "#F2151F42",
            border: "#804863A8",
            text: "#FFEDF1FE",
            muted: "#FFA9B8E8",
            accent: "#FFB1C3F4",
            accentSoft: "#FF8998CD",
            surface: "#14B1C3F4",
            hover: "#26B1C3F4",
            divider: "#408998CD",
            chipBg: "#408998CD",
            chipText: "#FFC3D0F1",
            windowBg: "#FF19254F",
            cardBg: "#FF1F2E60",
            inputBg: "#FF101A3C",
            inputBorder: "#994863A8",
            switchOff: "#FF3A4777",
            scrollThumb: "#8099ACD9",
            up: "#FFFF6B74",
            down: "#FF3ECF8E",
            flat: "#FF8F9BC4"),

        // 主题二：深海（深色，默认）
        new ThemePalette(
            index: 1,
            name: "深海",
            note: "深色 · 沉静",
            isDark: true,
            bgTop: "#F213252D",
            bgBottom: "#F20A1418",
            border: "#8043525B",
            text: "#FFE2EDF0",
            muted: "#FF85AEBF",
            accent: "#FF6D8C9B",
            accentSoft: "#FF557182",
            surface: "#146D8C9B",
            hover: "#246D8C9B",
            divider: "#5543525B",
            chipBg: "#5543525B",
            chipText: "#FFA9C6D1",
            windowBg: "#FF0F1D23",
            cardBg: "#FF16262D",
            inputBg: "#FF0A1519",
            inputBorder: "#9943525B",
            switchOff: "#FF2C3D45",
            scrollThumb: "#806D8C9B",
            up: "#FFF2666E",
            down: "#FF45C39A",
            flat: "#FF7D97A3"),

        // 主题三：暮紫（深色）
        new ThemePalette(
            index: 2,
            name: "暮紫",
            note: "深色 · 温柔",
            isDark: true,
            bgTop: "#F2271A33",
            bgBottom: "#F21B1024",
            border: "#80604884",
            text: "#FFF2EBF7",
            muted: "#FFB89FBC",
            accent: "#FFB7A5D0",
            accentSoft: "#FF9372A0",
            surface: "#14B7A5D0",
            hover: "#24B7A5D0",
            divider: "#55604884",
            chipBg: "#4D9372A0",
            chipText: "#FFD7C6E4",
            windowBg: "#FF211530",
            cardBg: "#FF2B1C3B",
            inputBg: "#FF170E1F",
            inputBorder: "#99604884",
            switchOff: "#FF3D2B4E",
            scrollThumb: "#80B7A5D0",
            up: "#FFEE6A76",
            down: "#FF45C08F",
            flat: "#FF9C86A8"),

        // 主题四：雾灰（浅色）
        new ThemePalette(
            index: 3,
            name: "雾灰",
            note: "浅色 · 柔和",
            isDark: false,
            bgTop: "#F2EFF2F7",
            bgBottom: "#F0E2E6EF",
            border: "#A6CDD2DC",
            text: "#FF3E3A3A",
            muted: "#FF7C8BA8",
            accent: "#FF788FB2",
            accentSoft: "#FFAFBBD1",
            surface: "#A6FFFFFF",
            hover: "#B8FFFFFF",
            divider: "#66CDD2DC",
            chipBg: "#33788FB2",
            chipText: "#FF5B6E8C",
            windowBg: "#FFF4F5F9",
            cardBg: "#FFFFFFFF",
            inputBg: "#FFFFFFFF",
            inputBorder: "#FFD5D9E2",
            switchOff: "#FFC9CFDA",
            scrollThumb: "#80788FB2",
            up: "#FFD9454F",
            down: "#FF1E9C6B",
            flat: "#FF8B8B93")
    };

    public static int Current { get; private set; }

    public static ThemePalette CurrentPalette => Themes[Math.Clamp(Current, 0, Themes.Count - 1)];

    public static void Apply(int index, ResourceDictionary target)
    {
        Current = Math.Clamp(index, 0, Themes.Count - 1);
        var p = CurrentPalette;
        var d = new ResourceDictionary
        {
            ["WindowBg"] = ThemePalette.Solid(p.WindowBg),
            ["CardBg"] = ThemePalette.Solid(p.CardBg),
            ["PanelBorder"] = ThemePalette.Solid(p.Border),
            ["Divider"] = ThemePalette.Solid(p.Divider),
            ["TextPrimary"] = ThemePalette.Solid(p.Text),
            ["TextMuted"] = ThemePalette.Solid(p.Muted),
            ["Accent"] = ThemePalette.Solid(p.Accent),
            ["AccentSoft"] = ThemePalette.Solid(p.AccentSoft),
            ["Surface"] = ThemePalette.Solid(p.Surface),
            ["RowHover"] = ThemePalette.Solid(p.Hover),
            ["ChipBg"] = ThemePalette.Solid(p.ChipBg),
            ["ChipText"] = ThemePalette.Solid(p.ChipText),
            ["InputBg"] = ThemePalette.Solid(p.InputBg),
            ["InputBorder"] = ThemePalette.Solid(p.InputBorder),
            ["SwitchOff"] = ThemePalette.Solid(p.SwitchOff),
            ["ScrollThumb"] = ThemePalette.Solid(p.ScrollThumb),
            ["Up"] = ThemePalette.Solid(p.Up),
            ["Down"] = ThemePalette.Solid(p.Down),
            ["Flat"] = ThemePalette.Solid(p.Flat),
            ["OnAccent"] = ThemePalette.Solid(p.IsDark ? p.WindowBg : "#FFFFFFFF"),
            ["ButtonBg"] = ThemePalette.Solid(p.IsDark ? p.Accent : p.Text),
            ["ButtonText"] = ThemePalette.Solid(p.IsDark ? p.WindowBg : "#FFFFFFFF"),
            ["TextShadow"] = BuildShadow(p)
        };

        if (_themeDictionary != null)
        {
            int at = target.MergedDictionaries.IndexOf(_themeDictionary);
            if (at >= 0) target.MergedDictionaries[at] = d;
            else target.MergedDictionaries.Insert(0, d);
        }
        else
        {
            target.MergedDictionaries.Insert(0, d);
        }

        _themeDictionary = d;
    }

    /// <summary>Soft halo so text stays readable on any wallpaper (light text → dark halo, and vice versa).</summary>
    private static DropShadowEffect BuildShadow(ThemePalette p)
    {
        var shadow = new DropShadowEffect
        {
            BlurRadius = 2.5,
            ShadowDepth = 0,
            Direction = 0,
            Opacity = 0.8,
            Color = p.IsDark ? Colors.Black : Colors.White
        };
        shadow.Freeze();
        return shadow;
    }
}

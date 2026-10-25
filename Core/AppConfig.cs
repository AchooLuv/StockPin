namespace StockPin.Core;

/// <summary>Serialized shape of one watch-list item.</summary>
public sealed class StockConfig
{
    public string SecId { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Market { get; set; } = MarketKind.A;
    public int Qty { get; set; }
    public double Cost { get; set; }
}

public static class MarketKind
{
    public const string A = "A";
    public const string HK = "HK";
    public const string US = "US";

    public static string Tag(string market) => market switch
    {
        HK => "港",
        US => "美",
        _ => "A"
    };

    public static string Label(string market) => market switch
    {
        HK => "港股",
        US => "美股",
        _ => "A股"
    };

    /// <summary>Maps an Eastmoney secid prefix (f13) to our market kind.</summary>
    public static string FromSecId(string secId)
    {
        int dot = secId.IndexOf('.');
        var prefix = dot > 0 ? secId[..dot] : "";
        return prefix switch
        {
            "116" => HK,
            "105" or "106" or "107" => US,
            _ => A
        };
    }
}

public sealed class AppConfig
{
    public List<StockConfig> Stocks { get; set; } = new();

    /// <summary>Index into ThemeManager.Themes: 蓝调 / 深海 / 暮紫 / 雾灰. 深海 is the default.</summary>
    public int Theme { get; set; } = 1;

    /// <summary>2 = current theme order. Older configs have no such field and are migrated on load.</summary>
    public int ThemeOrder { get; set; } = 2;

    public int IntervalSec { get; set; } = 3;

    /// <summary>Panel plate opacity in percent: 0 = pure text, 100 = solid card.</summary>
    public int Backdrop { get; set; } = 70;

    public bool Locked { get; set; }
    public bool AutoStart { get; set; }
    public bool ShowCode { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? SettingsLeft { get; set; }
    public double? SettingsTop { get; set; }
}

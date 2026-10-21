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
    public int Theme { get; set; }
    public double Opacity { get; set; } = 0.96;
    public int IntervalSec { get; set; } = 5;
    public bool TopMost { get; set; } = true;
    public bool Locked { get; set; }
    public bool AutoStart { get; set; }
    public bool ShowCode { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? SettingsLeft { get; set; }
    public double? SettingsTop { get; set; }
}

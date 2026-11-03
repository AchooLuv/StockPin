namespace StockPin.Core;

/// <summary>Maps a stock to its market board and the indices shown in the header.</summary>
public static class BoardCatalog
{
    public const string ShanghaiMain = "上证指数";
    public const string ShenzhenMain = "深证成指";
    public const string ChiNext = "创业板指";
    public const string Star = "科创50";
    public const string Beijing = "北证50";
    public const string HongKong = "港股";
    public const string UnitedStates = "美股";

    public static string Name(string market, string code)
    {
        if (market == MarketKind.HK) return HongKong;
        if (market == MarketKind.US) return UnitedStates;

        if (code.Length == 6)
        {
            if (code[0] == '6' && code[1] == '0') return ShanghaiMain;
            if (code[0] == '0' && code[1] == '0') return ShenzhenMain;
            if (code[0] == '3' && code[1] == '0') return ChiNext;
            if (code[0] == '6' && code[1] == '8') return Star;
            if (code[0] == '4' || code[0] == '8') return Beijing;
            if (code[0] == '9' && code[1] == '2') return Beijing;
        }

        return ShanghaiMain;
    }

    /// <summary>Only A-share boards are split into industry sectors.</summary>
    public static bool HasSectors(string board) => board is
        ShanghaiMain or ShenzhenMain or ChiNext or Star or Beijing;

    /// <summary>Indices displayed next to the board (Tencent secid).</summary>
    public static IReadOnlyList<(string Name, string SecId)> Indices(string board) => board switch
    {
        ShanghaiMain => new[] { ("上证指数", "1.000001") },
        ShenzhenMain => new[] { ("深证成指", "0.399001") },
        ChiNext => new[] { ("创业板指", "0.399006") },
        Star => new[] { ("科创50", "1.000688") },
        Beijing => new[] { ("北证50", "0.899050") },
        HongKong => new[] { ("恒生指数", "116.HSI") },
        UnitedStates => new[] { ("纳指", "105.IXIC"), ("道指", "105.DJI") },
        _ => Array.Empty<(string, string)>()
    };
}

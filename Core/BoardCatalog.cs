namespace StockPin.Core;

/// <summary>Maps a stock to its listing board and that board's index.</summary>
public static class BoardCatalog
{
    public const string ShanghaiMain = "沪市主板";
    public const string ShenzhenMain = "深市主板";
    public const string ChiNext = "创业板";
    public const string Star = "科创板";
    public const string Beijing = "北交所";
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

    /// <summary>Index used for the board header (Tencent secid).</summary>
    public static string? IndexSecId(string board) => board switch
    {
        ShanghaiMain => "1.000001",   // 上证指数
        ShenzhenMain => "0.399001",   // 深证成指
        ChiNext => "0.399006",        // 创业板指
        Star => "1.000688",           // 科创50
        Beijing => "0.899050",        // 北证50
        HongKong => "116.HSI",        // 恒生指数
        UnitedStates => "105.SPY",    // 标普 500 ETF，作为美股大盘代理
        _ => null
    };
}

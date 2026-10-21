namespace StockPin.Services;

/// <summary>Local trading-session clock for A-shares, Hong Kong and US markets.</summary>
public static class MarketClock
{
    private static readonly TimeZoneInfo Shanghai = Find("Asia/Shanghai", "China Standard Time");
    private static readonly TimeZoneInfo HongKong = Find("Asia/Hong_Kong", "China Standard Time");
    private static readonly TimeZoneInfo NewYork = Find("America/New_York", "Eastern Standard Time");

    /// <summary>Benchmark symbol used as a heartbeat to tell a real session from a public holiday.</summary>
    public static string ProbeSecId(string market) => market switch
    {
        MarketKind.HK => "100.HSI",
        MarketKind.US => "100.SPX",
        _ => "1.000001"
    };

    public static bool IsOpen(string market, DateTimeOffset now)
        => MinutesSinceSessionStart(market, now) >= 0;

    /// <summary>
    /// Open when the local session is running AND the benchmark quote is fresh.
    /// The freshness check keeps holiday / early-close days grey; the grace period
    /// avoids flickering right at the opening bell, and a missing heartbeat never greys.
    /// </summary>
    public static bool IsTrading(string market, DateTimeOffset now, DateTimeOffset? heartbeat)
    {
        int since = MinutesSinceSessionStart(market, now);
        if (since < 0) return false;
        if (since <= 15) return true;
        if (heartbeat == null) return true;
        return (now - heartbeat.Value).TotalMinutes < 30;
    }

    /// <summary>Minutes since the current session segment started, or -1 when closed.</summary>
    public static int MinutesSinceSessionStart(string market, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Zone(market));
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return -1;

        int minutes = local.Hour * 60 + local.Minute;
        return market switch
        {
            MarketKind.A => minutes is >= 570 and < 690 ? minutes - 570
                : minutes is >= 780 and < 900 ? minutes - 780 : -1,
            MarketKind.HK => minutes is >= 570 and < 720 ? minutes - 570
                : minutes is >= 780 and < 960 ? minutes - 780 : -1,
            _ => minutes is >= 570 and < 960 ? minutes - 570 : -1
        };
    }

    private static TimeZoneInfo Zone(string market) => market switch
    {
        MarketKind.HK => HongKong,
        MarketKind.US => NewYork,
        _ => Shanghai
    };

    private static TimeZoneInfo Find(string iana, string windows)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(iana);
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(windows);
            }
            catch
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}

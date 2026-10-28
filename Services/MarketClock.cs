namespace StockPin.Services;

public enum SessionPhase
{
    PreOpen,
    Premarket,
    Trading,
    Lunch,
    PostClose,
    OffDay
}

/// <summary>Local trading-session clock for A-shares, Hong Kong and US markets.</summary>
public static class MarketClock
{
    private static readonly TimeZoneInfo Shanghai = Find("Asia/Shanghai", "China Standard Time");
    private static readonly TimeZoneInfo HongKong = Find("Asia/Hong_Kong", "China Standard Time");
    private static readonly TimeZoneInfo NewYork = Find("America/New_York", "Eastern Standard Time");

    /// <summary>Benchmark symbol used as a heartbeat to tell a real session from a public holiday.</summary>
    public static string ProbeSecId(string market) => market switch
    {
        MarketKind.HK => "116.00700",  // hk00700 (Tencent) — updated throughout the HK session
        MarketKind.US => "105.AAPL",   // usAAPL
        _ => "1.000001"                // sh000001
    };

    public static SessionPhase Phase(string market, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Zone(market));
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return SessionPhase.OffDay;

        int minutes = local.Hour * 60 + local.Minute;
        return market switch
        {
            MarketKind.A => minutes < 570 ? SessionPhase.PreOpen
                : minutes < 690 ? SessionPhase.Trading
                : minutes < 780 ? SessionPhase.Lunch
                : minutes < 900 ? SessionPhase.Trading
                : SessionPhase.PostClose,
            MarketKind.HK => minutes < 570 ? SessionPhase.PreOpen
                : minutes < 720 ? SessionPhase.Trading
                : minutes < 780 ? SessionPhase.Lunch
                : minutes < 960 ? SessionPhase.Trading
                : SessionPhase.PostClose,
            // US pre-market runs 04:00–09:30 ET (04:00 = 240 minutes).
            _ => minutes < 240 ? SessionPhase.PreOpen
                : minutes < 570 ? SessionPhase.Premarket
                : minutes < 960 ? SessionPhase.Trading
                : SessionPhase.PostClose
        };
    }

    /// <summary>Short label shown after the stock name, empty while the market is live.</summary>
    public static string StatusText(SessionPhase phase, bool trading) => trading ? "" : phase switch
    {
        SessionPhase.PreOpen => "未开盘",
        SessionPhase.Premarket => "盘前",
        SessionPhase.Lunch => "午间休市",
        SessionPhase.PostClose => "已收盘",
        _ => "休市"
    };

    /// <summary>
    /// Whether this market is worth polling right now: during the session, in the
    /// 30 minutes before the open, and for the first 15 minutes of a session segment.
    /// Closed markets and public holidays are skipped.
    /// </summary>
    public static bool ShouldPoll(string market, DateTimeOffset now, DateTimeOffset? heartbeat)
    {
        var phase = Phase(market, now);
        if (phase == SessionPhase.Premarket) return true;   // pre-market quotes update all morning
        if (phase == SessionPhase.PreOpen) return MinutesUntilOpen(market, now) <= 30;
        if (phase != SessionPhase.Trading) return false;

        int since = MinutesSinceSessionStart(market, now);
        return (since is >= 0 and <= 15) || IsTrading(market, now, heartbeat);
    }

    /// <summary>Minutes until the 09:30 open; int.MaxValue on weekends.</summary>
    public static int MinutesUntilOpen(string market, DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, Zone(market));
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return int.MaxValue;

        int minutes = local.Hour * 60 + local.Minute;
        return minutes < 570 ? 570 - minutes : 0;
    }

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

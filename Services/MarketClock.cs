namespace StockPin.Services;

/// <summary>Local trading-session clock for A-shares, Hong Kong and US markets.</summary>
public static class MarketClock
{
    private static readonly TimeZoneInfo Shanghai = Find("Asia/Shanghai", "China Standard Time");
    private static readonly TimeZoneInfo HongKong = Find("Asia/Hong_Kong", "China Standard Time");
    private static readonly TimeZoneInfo NewYork = Find("America/New_York", "Eastern Standard Time");

    public static bool IsOpen(string market, DateTimeOffset now)
    {
        var zone = market switch
        {
            MarketKind.HK => HongKong,
            MarketKind.US => NewYork,
            _ => Shanghai
        };

        var local = TimeZoneInfo.ConvertTime(now, zone);
        if (local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;

        int minutes = local.Hour * 60 + local.Minute;
        return market switch
        {
            MarketKind.A => (minutes >= 570 && minutes < 690) || (minutes >= 780 && minutes < 900),
            MarketKind.HK => (minutes >= 570 && minutes < 720) || (minutes >= 780 && minutes < 960),
            _ => minutes >= 570 && minutes < 960
        };
    }


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

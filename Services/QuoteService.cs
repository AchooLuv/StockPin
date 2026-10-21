namespace StockPin.Services;

/// <summary>
/// Free quote access through Eastmoney's public HTTP endpoints (no key, no scraping).
/// One batched request per refresh keeps the app light.
/// </summary>
public sealed class QuoteService : IDisposable
{
    private const string BatchUrl = "https://push2.eastmoney.com/api/qt/ulist.np/get";
    private const string SearchUrl = "https://searchapi.eastmoney.com/api/suggest/get";
    private const string SearchToken = "D43BF722C8E33BDC906FB84D85E326E8";
    private const string Fields = "f2,f3,f4,f12,f13,f14,f15,f16,f17,f18,f124";

    private readonly HttpClient _http;

    public QuoteService()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(6),
            AutomaticDecompression = DecompressionMethods.All
        };

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Referrer = new Uri("https://quote.eastmoney.com/");
    }

    public async Task<Dictionary<string, Quote>> FetchQuotesAsync(IEnumerable<string> secIds, CancellationToken ct)
    {
        var list = secIds.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, Quote>(StringComparer.Ordinal);
        if (list.Count == 0) return result;

        var url = $"{BatchUrl}?fltt=2&secids={string.Join(",", list)}&fields={Fields}&_={Epoch()}";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        if (doc == null) return result;

        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return result;
        if (!data.TryGetProperty("diff", out var diff) || diff.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in diff.EnumerateArray())
        {
            var quote = ParseQuote(item);
            if (quote != null) result[quote.SecId] = quote;
        }

        return result;
    }

    /// <summary>USD/CNH and HKD/CNH, used to convert a mixed portfolio to CNY.</summary>
    public async Task<(double Usd, double Hkd)> FetchRatesAsync(CancellationToken ct)
    {
        try
        {
            var url = $"{BatchUrl}?fltt=2&secids=133.USDCNH,133.HKDCNH&fields=f2,f12&_={Epoch()}";
            using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
            if (doc == null) return (0, 0);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return (0, 0);
            if (!data.TryGetProperty("diff", out var diff) || diff.ValueKind != JsonValueKind.Array)
                return (0, 0);

            double usd = 0, hkd = 0;
            foreach (var item in diff.EnumerateArray())
            {
                var code = Str(item, "f12");
                var value = Num(item, "f2");
                if (code == "USDCNH") usd = value;
                else if (code == "HKDCNH") hkd = value;
            }

            return (usd, hkd);
        }
        catch (Exception ex)
        {
            Log.Warn("fx fetch failed: " + ex.Message);
            return (0, 0);
        }
    }

    /// <summary>Resolves a code / name (601872, 00700, FRO, 招商轮船) into tradable symbols.</summary>
    public async Task<List<StockSearchResult>> SearchAsync(string keyword, CancellationToken ct)
    {
        var results = new List<StockSearchResult>();
        var input = (keyword ?? "").Trim();
        if (input.Length == 0) return results;

        var url = $"{SearchUrl}?input={Uri.EscapeDataString(input)}&type=14&token={SearchToken}&count=10&_={Epoch()}";
        using var doc = await GetJsonAsync(url, ct).ConfigureAwait(false);
        if (doc == null) return results;

        if (!doc.RootElement.TryGetProperty("QuotationCodeTable", out var table) ||
            table.ValueKind != JsonValueKind.Object) return results;
        if (!table.TryGetProperty("Data", out var data) || data.ValueKind != JsonValueKind.Array) return results;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in data.EnumerateArray())
        {
            var typeName = Str(item, "SecurityTypeName");
            if (typeName is not ("沪A" or "深A" or "京A" or "港股" or "美股")) continue;

            var secId = Str(item, "QuoteID");
            var code = Str(item, "Code");
            var name = Str(item, "Name");
            if (secId.Length < 3 || code.Length == 0 || !seen.Add(secId)) continue;

            results.Add(new StockSearchResult(secId, code, name, MarketKind.FromSecId(secId), typeName));
            if (results.Count >= 8) break;
        }

        // exact code match first
        var exact = results.FirstOrDefault(r => string.Equals(r.Code, input, StringComparison.OrdinalIgnoreCase));
        if (exact != null && results[0] != exact)
        {
            results.Remove(exact);
            results.Insert(0, exact);
        }

        return results;
    }

    public void Dispose() => _http.Dispose();

    // ---------------------------------------------------------------- helpers

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken ct)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            if (bytes.Length == 0) return null;
            return JsonDocument.Parse(bytes);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warn("quote request failed: " + ex.Message);
            return null;
        }
    }

    private static Quote? ParseQuote(JsonElement item)
    {
        var code = Str(item, "f12");
        var market = (int)Num(item, "f13");
        if (code.Length == 0) return null;

        var secId = market.ToString(CultureInfo.InvariantCulture) + "." + code;
        var price = Num(item, "f2");
        var prevClose = Num(item, "f18");
        var changePct = Num(item, "f3");
        var changeAmt = Num(item, "f4");
        var ts = Num(item, "f124");

        if (price <= 0) return null;

        if (Math.Abs(changePct) < 0.00001 && prevClose > 0 && price > 0)
            changePct = (price - prevClose) / prevClose * 100;

        DateTimeOffset updated = DateTimeOffset.Now;
        if (ts > 1_000_000_000)
        {
            try
            {
                updated = DateTimeOffset.FromUnixTimeSeconds((long)ts);
            }
            catch
            {
                // keep now
            }
        }

        return new Quote(
            secId,
            code,
            Str(item, "f14"),
            price,
            prevClose,
            changePct,
            changeAmt,
            Num(item, "f17"),
            Num(item, "f15"),
            Num(item, "f16"),
            updated,
            true);
    }

    private static string Str(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Number => value.ToString(),
            _ => ""
        };
    }

    private static double Num(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value)) return 0;
        switch (value.ValueKind)
        {
            case JsonValueKind.Number:
                return value.TryGetDouble(out var d) ? d : 0;
            case JsonValueKind.String:
                return double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                    ? parsed
                    : 0;
            default:
                return 0;
        }
    }

    private static long Epoch() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

namespace StockPin.Services;

/// <summary>
/// Free quotes via Tencent's public batch endpoint (A / HK / US in one request),
/// FX via Sina, symbol search via Eastmoney. No key, no scraping.
/// </summary>
public sealed class QuoteService : IDisposable
{
    private const string TencentUrl = "https://qt.gtimg.cn/q=";
    private const string SinaUrl = "https://hq.sinajs.cn/list=";
    private const string SearchUrl = "https://searchapi.eastmoney.com/api/suggest/get";
    private const string SearchToken = "D43BF722C8E33BDC906FB84D85E326E8";

    private static readonly TimeZoneInfo NewYork = FindZone("America/New_York", "Eastern Standard Time");
    private static Encoding _gbk = Encoding.UTF8;

    private readonly HttpClient _http;

    static QuoteService()
    {
        // Tencent / Sina return GBK; .NET Core needs the code pages provider for that.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _gbk = Encoding.GetEncoding("GB18030");
    }

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
    }

    public async Task<Dictionary<string, Quote>> FetchQuotesAsync(IEnumerable<string> secIds, CancellationToken ct)
    {
        var result = new Dictionary<string, Quote>(StringComparer.Ordinal);
        var symbolToSecId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secId in secIds)
        {
            if (string.IsNullOrWhiteSpace(secId)) continue;
            var symbol = ToTencentSymbol(secId);
            if (symbol == null || symbolToSecId.ContainsKey(symbol)) continue;
            symbolToSecId[symbol] = secId;
        }

        if (symbolToSecId.Count == 0) return result;

        var url = TencentUrl + string.Join(",", symbolToSecId.Keys) + "&_=" + Epoch();
        var text = await GetTextAsync(url, "https://gu.qq.com/", ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text)) return result;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("v_", StringComparison.Ordinal)) continue;

            int equals = line.IndexOf('=');
            if (equals <= 2) continue;

            var symbol = line[2..equals];
            if (!symbolToSecId.TryGetValue(symbol, out var secId)) continue;

            var payload = line[(equals + 1)..].Trim().TrimEnd(';').Trim('"');
            var fields = payload.Split('~');
            var quote = ParseTencentQuote(secId, fields);
            if (quote != null) result[secId] = quote;
        }

        return result;
    }

    /// <summary>
    /// US pre-market quotes from Sina. Tencent's US quote only carries the last regular
    /// close, while Sina exposes the 04:00–09:30 ET pre-market price in fields 22–24.
    /// </summary>
    public async Task<Dictionary<string, Quote>> FetchUsPreMarketAsync(IEnumerable<string> secIds, CancellationToken ct)
    {
        var result = new Dictionary<string, Quote>(StringComparer.Ordinal);
        var symbolToSecId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var secId in secIds)
        {
            var symbol = ToSinaUsSymbol(secId);
            if (symbol == null || symbolToSecId.ContainsKey(symbol)) continue;
            symbolToSecId[symbol] = secId;
        }

        if (symbolToSecId.Count == 0) return result;

        var text = await GetTextAsync(SinaUrl + string.Join(",", symbolToSecId.Keys),
            "https://finance.sina.com.cn", ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(text)) return result;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            int marker = line.IndexOf("hq_str_", StringComparison.Ordinal);
            if (marker < 0) continue;

            int equals = line.IndexOf('=', marker);
            if (equals < 0) continue;

            var symbol = line[(marker + 7)..equals];
            if (!symbolToSecId.TryGetValue(symbol, out var secId)) continue;

            var payload = line[(equals + 1)..].Trim().TrimEnd(';').Trim('"');
            if (payload.Length == 0) continue;

            var quote = ParseSinaUsPreMarket(secId, payload.Split(','));
            if (quote != null) result[secId] = quote;
        }

        return result;
    }

    /// <summary>USD/CNH and HKD/CNH, used to convert a mixed portfolio to CNY.</summary>
    public async Task<(double Usd, double Hkd)> FetchRatesAsync(CancellationToken ct)
    {
        try
        {
            var text = await GetTextAsync(SinaUrl + "fx_susdcnh,fx_shkdcnh", "https://finance.sina.com.cn", ct)
                .ConfigureAwait(false);
            if (string.IsNullOrEmpty(text)) return (0, 0);

            double usd = 0, hkd = 0;
            foreach (var line in text.Split('\n'))
            {
                if (line.Contains("hq_str_fx_susdcnh=", StringComparison.Ordinal)) usd = ParseSinaFx(line);
                else if (line.Contains("hq_str_fx_shkdcnh=", StringComparison.Ordinal)) hkd = ParseSinaFx(line);
            }

            return (usd, hkd);
        }
        catch (Exception ex)
        {
            Log.Warn("fx fetch failed: " + Describe(ex));
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

    private async Task<string?> GetTextAsync(string url, string referer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri(referer);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return bytes.Length == 0 ? null : _gbk.GetString(bytes);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warn("quote request failed: " + Describe(ex));
            return null;
        }
    }

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
            Log.Warn("search request failed: " + Describe(ex));
            return null;
        }
    }

    private static Quote? ParseTencentQuote(string secId, string[] f)
    {
        // 1 name · 2 code · 3 price · 4 prev close · 5 open · 30 time
        // 31 change · 32 change % · 33 high · 34 low
        if (f.Length < 35) return null;

        var price = ParseNum(f[3]);
        if (price <= 0) return null;

        var market = MarketKind.FromSecId(secId);
        var prevClose = ParseNum(f[4]);
        var changePct = ParseNum(f[32]);
        var changeAmt = ParseNum(f[31]);

        if (Math.Abs(changePct) < 0.00001 && prevClose > 0)
            changePct = (price - prevClose) / prevClose * 100;

        int dot = secId.IndexOf('.');
        var code = dot >= 0 ? secId[(dot + 1)..] : f[2];

        return new Quote(
            secId,
            code,
            f[1].Trim(),
            price,
            prevClose,
            changePct,
            changeAmt,
            ParseNum(f[5]),
            ParseNum(f[33]),
            ParseNum(f[34]),
            ParseTime(f[30], market),
            true);
    }

    private static string? ToTencentSymbol(string secId)
    {
        int dot = secId.IndexOf('.');
        if (dot <= 0 || dot == secId.Length - 1) return null;

        var prefix = secId[..dot];
        var code = secId[(dot + 1)..];

        return prefix switch
        {
            "1" => "sh" + code,
            "0" => code.Length == 6 && (code[0] == '4' || code[0] == '8') ? "bj" + code : "sz" + code,
            "116" => "hk" + (code.All(char.IsDigit) ? code.PadLeft(5, '0') : code),
            "105" or "106" or "107" => "us" + code,
            _ => null
        };
    }

    private static string? ToSinaUsSymbol(string secId)
    {
        int dot = secId.IndexOf('.');
        if (dot <= 0 || dot == secId.Length - 1) return null;

        var prefix = secId[..dot];
        if (prefix is not ("105" or "106" or "107")) return null;

        return "gb_" + secId[(dot + 1)..].ToLowerInvariant();
    }

    private static Quote? ParseSinaUsPreMarket(string secId, string[] f)
    {
        // 1 regular price · 2 regular % · 3 update time · 4 regular change
        // 21 pre-market price · 22 pre-market % · 23 pre-market change · 24 pre-market time
        // 26 previous close
        if (f.Length < 27) return null;

        double prePrice = ParseNum(f[21]);
        double price = prePrice > 0 ? prePrice : ParseNum(f[1]);
        if (price <= 0) return null;

        double prevClose = ParseNum(f[26]);
        if (prevClose <= 0) prevClose = ParseNum(f[1]) - ParseNum(f[4]);

        double changePct = prePrice > 0 ? ParseNum(f[22]) : ParseNum(f[2]);
        double changeAmt = prePrice > 0 ? ParseNum(f[23]) : ParseNum(f[4]);
        if (Math.Abs(changePct) < 0.00001 && prevClose > 0)
            changePct = (price - prevClose) / prevClose * 100;

        DateTimeOffset updated = DateTimeOffset.Now;
        if (DateTime.TryParseExact(f[3].Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var beijing))
            updated = new DateTimeOffset(beijing, TimeSpan.FromHours(8));

        int dot = secId.IndexOf('.');
        var code = dot >= 0 ? secId[(dot + 1)..] : "";

        // Name left empty so the Chinese name saved when the stock was added is kept.
        return new Quote(secId, code, "", price, prevClose, changePct, changeAmt, 0, 0, 0, updated, true);
    }

    private static DateTimeOffset ParseTime(string raw, string market)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return DateTimeOffset.Now;

        if (market == MarketKind.US)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var us))
            {
                var unspecified = DateTime.SpecifyKind(us, DateTimeKind.Unspecified);
                return new DateTimeOffset(unspecified, NewYork.GetUtcOffset(unspecified));
            }
        }
        else if (market == MarketKind.HK)
        {
            if (DateTime.TryParseExact(raw, "yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var hk))
                return new DateTimeOffset(hk, TimeSpan.FromHours(8));
        }
        else if (DateTime.TryParseExact(raw, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                     DateTimeStyles.None, out var a))
        {
            return new DateTimeOffset(a, TimeSpan.FromHours(8));
        }

        return DateTimeOffset.Now;
    }

    private static double ParseSinaFx(string line)
    {
        int first = line.IndexOf('"');
        int last = line.LastIndexOf('"');
        if (first < 0 || last <= first) return 0;

        var parts = line[(first + 1)..last].Split(',');
        // 8 = current price, 1 = bid
        if (parts.Length > 8 && ParseNum(parts[8]) > 0) return ParseNum(parts[8]);
        if (parts.Length > 1 && ParseNum(parts[1]) > 0) return ParseNum(parts[1]);
        return 0;
    }

    private static double ParseNum(string? text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string Describe(Exception ex)
    {
        var builder = new StringBuilder(ex.Message);
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            builder.Append(" | ").Append(inner.Message);
        return builder.ToString();
    }

    private static TimeZoneInfo FindZone(string iana, string windows)
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

    private static long Epoch() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}

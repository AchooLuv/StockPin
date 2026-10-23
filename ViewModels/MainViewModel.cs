namespace StockPin.ViewModels;

/// <summary>Panel state: watch list, quotes, simulated portfolio and polling.</summary>
public sealed class MainViewModel : ObservableObject
{
    private readonly AppConfig _config;
    private readonly QuoteService _quotes;
    private readonly DispatcherTimer _timer;
    private bool _busy;
    private bool _paused;
    private string _statusText = "";
    private bool _hasError;
    private string _lastUpdateText = "";
    private int _intervalSec;
    private bool _showCode;

    private readonly Dictionary<string, DateTimeOffset> _heartbeats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _prevActive = new(StringComparer.Ordinal);
    private bool _forceAll = true;
    private bool _anyTrading;
    private bool _anyWarmup;
    private bool _anyLunch;
    private int _failStreak;

    private double _usdRate = 7.10;
    private double _hkdRate = 0.92;

    private bool _hasPortfolio;
    private string _portfolioValueText = "—";
    private string _totalPlText = "—";
    private string _todayPlText = "—";
    private string _todayPctText = "—";
    private string _totalPlPctText = "";
    private string _totalPlDirection = "Flat";
    private string _todayPctDirection = "Flat";
    private string _fxTooltip = "";

    public MainViewModel(AppConfig config, QuoteService quotes, bool seedDefaults)
    {
        _config = config;
        _quotes = quotes;
        _intervalSec = config.IntervalSec;
        _showCode = config.ShowCode;

        foreach (var item in config.Stocks)
        {
            if (string.IsNullOrWhiteSpace(item.SecId)) continue;
            AddEntry(new StockEntry(item.SecId, item.Code, item.Name, item.Market, item.Qty, item.Cost));
        }

        if (seedDefaults && Stocks.Count == 0)
        {
            AddEntry(new StockEntry("1.601872", "601872", "招商轮船", MarketKind.A));
            AddEntry(new StockEntry("116.00700", "00700", "腾讯控股", MarketKind.HK));
            AddEntry(new StockEntry("106.FRO", "FRO", "Frontline plc", MarketKind.US));
            Persist();
        }

        Stocks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasStocks));
        };

        RecalcPortfolio();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_intervalSec) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
    }

    public ObservableCollection<StockEntry> Stocks { get; } = new();

    public bool IsEmpty => Stocks.Count == 0;
    public bool HasStocks => Stocks.Count > 0;

    public bool HasPortfolio { get => _hasPortfolio; private set => Set(ref _hasPortfolio, value); }
    public string PortfolioValueText { get => _portfolioValueText; private set => Set(ref _portfolioValueText, value); }
    public string TotalPlText { get => _totalPlText; private set => Set(ref _totalPlText, value); }
    public string TodayPlText { get => _todayPlText; private set => Set(ref _todayPlText, value); }
    public string TodayPctText { get => _todayPctText; private set => Set(ref _todayPctText, value); }
    public string TotalPlPctText { get => _totalPlPctText; private set => Set(ref _totalPlPctText, value); }
    public string TotalPlDirection { get => _totalPlDirection; private set => Set(ref _totalPlDirection, value); }
    public string TodayPctDirection { get => _todayPctDirection; private set => Set(ref _todayPctDirection, value); }
    public string FxTooltip { get => _fxTooltip; private set => Set(ref _fxTooltip, value); }

    public bool HasError { get => _hasError; private set => Set(ref _hasError, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string LastUpdateText { get => _lastUpdateText; private set => Set(ref _lastUpdateText, value); }

    public bool ShowCode
    {
        get => _showCode;
        set => Set(ref _showCode, value);
    }

    public int IntervalSec
    {
        get => _intervalSec;
        set
        {
            if (Set(ref _intervalSec, value)) UpdateTimerInterval();
        }
    }

    // ------------------------------------------------------------------ stocks

    public bool AddStock(StockSearchResult result)
    {
        if (Stocks.Any(s => string.Equals(s.SecId, result.SecId, StringComparison.OrdinalIgnoreCase)))
            return false;

        var entry = new StockEntry(result.SecId, result.Code, result.Name, result.Market);
        AddEntry(entry);
        Persist();
        RecalcPortfolio();
        UpdateTimerInterval();
        _forceAll = true;
        _ = RefreshAsync();
        return true;
    }

    public void RemoveStock(StockEntry entry)
    {
        if (!Stocks.Remove(entry)) return;
        entry.PropertyChanged -= EntryChanged;
        Persist();
        RecalcPortfolio();
        UpdateTimerInterval();
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasStocks));
    }

    public void MoveStock(StockEntry entry, int toIndex)
    {
        int from = Stocks.IndexOf(entry);
        if (from < 0) return;
        toIndex = Math.Clamp(toIndex, 0, Stocks.Count - 1);
        if (from == toIndex) return;
        Stocks.Move(from, toIndex);
        Persist();
    }

    public void MoveStockBy(StockEntry entry, int delta)
    {
        int from = Stocks.IndexOf(entry);
        if (from < 0) return;
        int to = Math.Clamp(from + delta, 0, Stocks.Count - 1);
        if (from == to) return;
        Stocks.Move(from, to);
        Persist();
    }

    public Task RefreshNowAsync()
    {
        _forceAll = true;
        return RefreshAsync();
    }

    /// <summary>Design-preview data used by <c>--demo</c> / <c>--screenshot</c>.</summary>
    public void ApplyDemoData()
    {
        var now = DateTimeOffset.Now;
        var samples = new Dictionary<string, (double Price, double Prev, double Open, double High, double Low)>
        {
            ["1.601872"] = (17.61, 17.58, 17.60, 17.72, 17.55),
            ["116.00700"] = (393.80, 410.40, 408.20, 409.00, 391.50),
            ["106.FRO"] = (50.32, 50.72, 50.24, 51.32, 49.03),
            ["105.AAPL"] = (339.50, 343.00, 342.10, 344.20, 338.60),
            ["0.300750"] = (268.40, 264.10, 265.00, 270.20, 263.50)
        };

        foreach (var entry in Stocks)
        {
            if (!samples.TryGetValue(entry.SecId, out var s)) continue;
            double pct = s.Prev > 0 ? (s.Price - s.Prev) / s.Prev * 100 : 0;
            string session = MarketClock.StatusText(MarketClock.Phase(entry.Market, now), false);
            entry.ApplyQuote(new Quote(entry.SecId, entry.Code, entry.Name, s.Price, s.Prev, pct,
                s.Price - s.Prev, s.Open, s.High, s.Low, now, true), false, session);
        }

        RecalcPortfolio();
        LastUpdateText = $"行情更新于 {DateTime.Now:HH:mm:ss}";
    }

    // ------------------------------------------------------------------ polling

    public void SetPaused(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        if (paused)
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
            _ = RefreshAsync();
        }
    }

    public async Task RefreshAsync()
    {
        if (_busy) return;
        if (Stocks.Count == 0)
        {
            _anyTrading = _anyWarmup = _anyLunch = false;
            _failStreak = 0;
            UpdateTimerInterval();
            RecalcPortfolio();
            return;
        }

        _busy = true;
        try
        {
            var now = DateTimeOffset.Now;
            var markets = Stocks.Select(s => s.Market).Distinct().ToList();
            var phases = new Dictionary<string, SessionPhase>(StringComparer.Ordinal);
            var active = new Dictionary<string, bool>(StringComparer.Ordinal);
            var warmup = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var market in markets)
            {
                var phase = MarketClock.Phase(market, now);
                phases[market] = phase;

                bool isActive = MarketClock.ShouldPoll(market, now,
                    _heartbeats.TryGetValue(market, out var hb) ? hb : null);
                bool isWarmup = isActive && phase == SessionPhase.PreOpen;

                active[market] = isActive;
                warmup[market] = isWarmup;
            }

            _anyTrading = markets.Any(m => active[m] && !warmup[m]);
            _anyWarmup = markets.Any(m => warmup[m]);
            _anyLunch = markets.Any(m => phases[m] == SessionPhase.Lunch);

            bool force = _forceAll;
            _forceAll = false;

            // Closed markets are skipped, except for one final refresh at the close transition.
            var fetchSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var market in markets)
            {
                bool wasActive = _prevActive.TryGetValue(market, out var prev) && prev;
                if (force || active[market] || wasActive) fetchSet.Add(market);
            }

            var stockIds = Stocks.Where(s => fetchSet.Contains(s.Market)).Select(s => s.SecId).ToList();
            var probeIds = markets.Select(MarketClock.ProbeSecId)
                .Where(p => !stockIds.Contains(p, StringComparer.Ordinal))
                .ToList();

            // Benchmarks are fetched on every tick, even when idle, so openings and holidays
            // are noticed quickly without polling every closed stock.
            var quotes = await _quotes.FetchQuotesAsync(stockIds.Concat(probeIds), CancellationToken.None)
                .ConfigureAwait(true);

            if (quotes.Count == 0)
            {
                _failStreak = Math.Min(_failStreak + 1, 6);
                SetStatus("行情连接异常，正在重试…");
                return;
            }

            _failStreak = 0;

            foreach (var market in markets)
            {
                if (quotes.TryGetValue(MarketClock.ProbeSecId(market), out var probe) && probe.HasData)
                    _heartbeats[market] = probe.UpdatedAt;
            }

            if (fetchSet.Count > 0 && Stocks.Any(s => s.Qty > 0 && s.Market != MarketKind.A))
            {
                var (usd, hkd) = await _quotes.FetchRatesAsync(CancellationToken.None).ConfigureAwait(true);
                if (usd > 0) _usdRate = usd;
                if (hkd > 0) _hkdRate = hkd;
            }

            bool namesChanged = false;

            foreach (var entry in Stocks)
            {
                DateTimeOffset? hb = _heartbeats.TryGetValue(entry.Market, out var stamp) ? stamp : null;
                bool trading = MarketClock.IsTrading(entry.Market, now, hb);
                string session = MarketClock.StatusText(phases[entry.Market], trading);

                if (fetchSet.Contains(entry.Market))
                {
                    if (quotes.TryGetValue(entry.SecId, out var quote))
                    {
                        if (!string.IsNullOrWhiteSpace(quote.Name) && quote.Name != entry.Name) namesChanged = true;
                        entry.ApplyQuote(quote, trading, session);
                    }
                    else
                    {
                        entry.MarkClosed(session);
                    }
                }
                else
                {
                    // Market is closed: keep the last price, only update the session label.
                    entry.SetSession(trading, session);
                }
            }

            foreach (var market in markets) _prevActive[market] = active[market];

            if (namesChanged) Persist();

            RecalcPortfolio();
            SetStatus("");
            LastUpdateText = $"行情更新于 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _failStreak = Math.Min(_failStreak + 1, 6);
            Log.Warn("refresh failed: " + Describe(ex));
            SetStatus("行情连接异常，正在重试…");
        }
        finally
        {
            _busy = false;
            UpdateTimerInterval();
        }
    }

    private void UpdateTimerInterval()
    {
        // Trading markets: user interval. Pre-open warm-up: 10s. Lunch: 30s. Otherwise idle at 60s.
        int seconds;
        if (_anyTrading) seconds = Math.Max(1, _intervalSec);
        else if (_anyWarmup) seconds = Math.Max(10, _intervalSec);
        else if (_anyLunch) seconds = 30;
        else seconds = 60;

        // Back off after failures instead of hammering a struggling endpoint at 1s.
        if (_failStreak > 0)
        {
            int backoff = _failStreak switch { 1 => 5, 2 => 10, 3 => 20, _ => 60 };
            seconds = Math.Max(seconds, backoff);
        }

        var interval = TimeSpan.FromSeconds(seconds);
        if (_timer.Interval != interval) _timer.Interval = interval;
        if (!_paused && !_timer.IsEnabled && Stocks.Count > 0) _timer.Start();
    }

    private void SetStatus(string text)
    {
        StatusText = text;
        HasError = text.Length > 0;
    }

    private static string Describe(Exception ex)
    {
        var text = ex.Message;
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
            text += " | " + inner.Message;
        return text;
    }

    // ------------------------------------------------------------------ portfolio

    private void RecalcPortfolio()
    {
        double marketValue = 0;
        double costValue = 0;
        double todayPl = 0;
        bool any = false;

        foreach (var s in Stocks)
        {
            // A holding only counts when both quantity and cost are filled in.
            if (s.Qty <= 0 || s.Cost <= 0) continue;
            any = true;
            if (!s.HasQuote) continue;

            double fx = s.Market switch
            {
                MarketKind.HK => _hkdRate,
                MarketKind.US => _usdRate,
                _ => 1
            };

            marketValue += s.Qty * s.Price * fx;
            costValue += s.Qty * s.Cost * fx;
            todayPl += s.Qty * (s.Price - s.PrevClose) * fx;
        }

        if (!any)
        {
            HasPortfolio = false;
            return;
        }

        HasPortfolio = true;

        double totalPl = marketValue - costValue;
        double todayBase = marketValue - todayPl;
        double todayPct = todayBase > 0 ? todayPl / todayBase * 100 : 0;
        double totalPct = costValue > 0 ? totalPl / costValue * 100 : 0;

        PortfolioValueText = Money(marketValue);
        TotalPlText = SignedMoney(totalPl);
        TodayPlText = SignedMoney(todayPl);
        TodayPctText = "(" + SignedPct(todayPct) + ")";
        TotalPlPctText = "(" + SignedPct(totalPct) + ")";
        TotalPlDirection = Dir(totalPl).ToString();
        TodayPctDirection = Dir(todayPl).ToString();
        FxTooltip = $"总盈亏 {TotalPlText} {TotalPlPctText}\n今日 {TodayPlText} {TodayPctText}\n" +
                    $"按实时汇率折算人民币\nUSD/CNH {_usdRate:F4}   HKD/CNH {_hkdRate:F4}";
    }

    private static string SignedPct(double pct)
        => (pct > 0 ? "+" : "") + pct.ToString("F2", CultureInfo.InvariantCulture) + "%";

    private static PriceDirection Dir(double value) =>
        value > 0.005 ? PriceDirection.Up : value < -0.005 ? PriceDirection.Down : PriceDirection.Flat;

    private static string Money(double value)
        => "¥" + value.ToString("N2", CultureInfo.InvariantCulture);

    private static string SignedMoney(double value)
        => (value < 0 ? "-¥" : value > 0 ? "+¥" : "¥") + Math.Abs(value).ToString("N2", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ plumbing

    private void AddEntry(StockEntry entry)
    {
        entry.PropertyChanged += EntryChanged;
        Stocks.Add(entry);
    }

    private void EntryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StockEntry.Qty) or nameof(StockEntry.Cost)
            or nameof(StockEntry.QtyText) or nameof(StockEntry.CostText))
        {
            RecalcPortfolio();
            Persist();
        }
    }

    private void Persist()
    {
        _config.Stocks = Stocks.Select(s => new StockConfig
        {
            SecId = s.SecId,
            Code = s.Code,
            Name = s.Name,
            Market = s.Market,
            Qty = s.Qty,
            Cost = s.Cost
        }).ToList();

        ConfigService.ScheduleSave(_config);
    }
}

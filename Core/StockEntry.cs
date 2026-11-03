namespace StockPin.Core;

public enum PriceDirection
{
    Flat,
    Up,
    Down
}

/// <summary>One row of the panel: identity, latest quote and simulated position.</summary>
public sealed class StockEntry : ObservableObject
{
    private string _name;
    private double _price;
    private double _prevClose;
    private double _changePct;
    private double _changeAmt;
    private double _open;
    private double _high;
    private double _low;
    private bool _hasQuote;
    private bool _isOpen;
    private DateTimeOffset _updatedAt;
    private int _qty;
    private double _cost;
    private string _sessionText = "";
    private string _qtyText = "";
    private string _costText = "";
    private string _sector = "";
    private double? _mtdBase;
    private double? _ytdBase;

    public StockEntry(string secId, string code, string name, string market, int qty = 0, double cost = 0,
        string sector = "")
    {
        SecId = secId;
        Code = code;
        Market = market;
        _name = name;
        _qty = qty;
        _cost = cost;
        _sector = sector ?? "";
        _qtyText = qty > 0 ? qty.ToString(CultureInfo.InvariantCulture) : "";
        _costText = cost > 0 ? cost.ToString("0.###", CultureInfo.InvariantCulture) : "";
    }

    public string SecId { get; }
    public string Code { get; }
    public string Market { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value)) OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(_name) ? Code : _name;
    public string TagText => MarketKind.Tag(Market);
    public string MarketLabel => MarketKind.Label(Market);

    /// <summary>Industry / board name, fetched from Eastmoney and cached in the config.</summary>
    public string Sector
    {
        get => _sector;
        set
        {
            var text = value ?? "";
            if (!Set(ref _sector, text)) return;
            OnPropertyChanged(nameof(SectorName));
            OnPropertyChanged(nameof(HasSector));
            OnPropertyChanged(nameof(ShowSector));
        }
    }

    public string SectorName => string.IsNullOrWhiteSpace(_sector) ? "未分类" : _sector;
    public bool HasSector => _sector.Length > 0;
    public bool ShowSector => Market == MarketKind.A && _sector.Length > 0;

    public string BoardName => BoardCatalog.Name(Market, Code);

    public double Price { get => _price; private set => Set(ref _price, value); }
    public double PrevClose { get => _prevClose; private set => Set(ref _prevClose, value); }
    public double ChangePct { get => _changePct; private set => Set(ref _changePct, value); }
    public double ChangeAmt { get => _changeAmt; private set => Set(ref _changeAmt, value); }
    public double Open { get => _open; private set => Set(ref _open, value); }
    public double High { get => _high; private set => Set(ref _high, value); }
    public double Low { get => _low; private set => Set(ref _low, value); }
    public bool HasQuote { get => _hasQuote; private set => Set(ref _hasQuote, value); }
    public bool IsOpen { get => _isOpen; private set => Set(ref _isOpen, value); }
    public DateTimeOffset UpdatedAt { get => _updatedAt; private set => Set(ref _updatedAt, value); }

    /// <summary>"未开盘" / "已收盘" / "午间休市" / "休市" shown after the name; empty while trading.</summary>
    public string SessionText => _sessionText;
    public bool HasSessionText => _sessionText.Length > 0;

    public int Qty
    {
        get => _qty;
        set
        {
            if (Set(ref _qty, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(HasPosition));
                OnPropertyChanged(nameof(PositionQtyText));
            }
        }
    }

    public double Cost
    {
        get => _cost;
        set
        {
            if (Set(ref _cost, Math.Max(0, value)))
                OnPropertyChanged(nameof(PositionCostText));
        }
    }

    /// <summary>
    /// Live-edited text. The raw string is kept so the caret never jumps, and the
    /// numeric value is parsed on every keystroke so the panel updates immediately.
    /// </summary>
    public string QtyText
    {
        get => _qtyText;
        set
        {
            var text = value ?? "";
            if (_qtyText == text) return;
            _qtyText = text;

            var trimmed = text.Trim().Replace(",", "").Replace(" ", "");
            if (trimmed.Length == 0)
            {
                Qty = 0;
            }
            else if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) && qty >= 0)
            {
                Qty = qty;
            }
        }
    }

    public string CostText
    {
        get => _costText;
        set
        {
            var text = value ?? "";
            if (_costText == text) return;
            _costText = text;

            var trimmed = text.Trim().Replace(",", "").Replace(" ", "");
            if (trimmed.Length == 0)
            {
                Cost = 0;
            }
            else if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) && cost >= 0)
            {
                Cost = cost;
            }
        }
    }

    public PriceDirection Direction =>
        !_hasQuote ? PriceDirection.Flat
        : _changePct > 0.0001 ? PriceDirection.Up
        : _changePct < -0.0001 ? PriceDirection.Down
        : PriceDirection.Flat;

    /// <summary>String form used by XAML data triggers.</summary>
    public string DirectionText => Direction.ToString();

    private int Decimals => Market switch
    {
        MarketKind.A => 2,
        MarketKind.HK => _price < 1 && _price > 0 ? 3 : 2,
        _ => 2
    };

    public string PriceText => _hasQuote ? Format(Price, Decimals) : "—";

    public string ChangePctText => !_hasQuote
        ? "—"
        : (_changePct > 0 ? "+" : "") + _changePct.ToString("F2", CultureInfo.InvariantCulture) + "%";

    public string ChangeAmtText => !_hasQuote
        ? "—"
        : (_changeAmt > 0 ? "+" : "") + Format(_changeAmt, Decimals);

    public string PrevCloseText => _hasQuote ? Format(_prevClose, Decimals) : "—";
    public string OpenText => _open > 0 ? Format(_open, Decimals) : "—";
    public string HighText => _high > 0 ? Format(_high, Decimals) : "—";
    public string LowText => _low > 0 ? Format(_low, Decimals) : "—";

    /// <summary>"交易中" / "已收盘" / "盘前" … shown in the tooltip.</summary>
    public string StateText => _isOpen ? "交易中" : _sessionText.Length > 0 ? _sessionText : "休市";

    public string UpdatedText => _hasQuote ? "更新于 " + _updatedAt.ToLocalTime().ToString("HH:mm:ss") : "";

    public bool HasPosition => _qty > 0;
    public string PositionQtyText => _qty.ToString("N0", CultureInfo.InvariantCulture);
    public string PositionCostText => Format(_cost, Decimals);

    public string MtdPctText => PeriodPctText(_mtdBase);
    public string YtdPctText => PeriodPctText(_ytdBase);
    public string MtdDirectionText => PeriodDirection(_mtdBase);
    public string YtdDirectionText => PeriodDirection(_ytdBase);

    /// <summary>Base closes for month-to-date / year-to-date, fetched once per session.</summary>
    public void SetPeriodBases(double? mtdBase, double? ytdBase)
    {
        _mtdBase = mtdBase;
        _ytdBase = ytdBase;
        OnPropertyChanged(nameof(MtdPctText));
        OnPropertyChanged(nameof(YtdPctText));
        OnPropertyChanged(nameof(MtdDirectionText));
        OnPropertyChanged(nameof(YtdDirectionText));
    }

    private string PeriodPctText(double? basePrice)
    {
        if (!_hasQuote || basePrice is not > 0) return "—";
        double pct = (_price - basePrice.Value) / basePrice.Value * 100;
        return (pct > 0 ? "+" : "") + pct.ToString("F2", CultureInfo.InvariantCulture) + "%";
    }

    private string PeriodDirection(double? basePrice)
    {
        if (!_hasQuote || basePrice is not > 0) return "Flat";
        double pct = (_price - basePrice.Value) / basePrice.Value * 100;
        return pct > 0.0001 ? "Up" : pct < -0.0001 ? "Down" : "Flat";
    }

    /// <summary>Applies a fresh quote. Raises a blanket notification (cheap: ~10 rows).</summary>
    public void ApplyQuote(Quote quote, bool isOpen, string sessionText)
    {
        if (!string.IsNullOrWhiteSpace(quote.Name)) _name = quote.Name;
        _price = quote.Price;
        _prevClose = quote.PrevClose;
        _changePct = quote.ChangePct;
        _changeAmt = quote.ChangeAmt;
        _open = quote.Open;
        _high = quote.High;
        _low = quote.Low;
        _hasQuote = quote.HasData;
        _updatedAt = quote.UpdatedAt;
        _isOpen = isOpen;
        _sessionText = sessionText;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Keeps the last price but refreshes the session label (market closed or no data).</summary>
    public void MarkClosed(string sessionText)
    {
        _isOpen = false;
        _sessionText = sessionText;
        OnPropertyChanged(string.Empty);
    }

    /// <summary>Updates only the session state; used when a market is not being polled.</summary>
    public void SetSession(bool isOpen, string sessionText)
    {
        if (_isOpen == isOpen && _sessionText == sessionText) return;
        _isOpen = isOpen;
        _sessionText = sessionText;
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(SessionText));
        OnPropertyChanged(nameof(HasSessionText));
    }

    private static string Format(double value, int decimals)
        => value.ToString("N" + decimals, CultureInfo.InvariantCulture);
}

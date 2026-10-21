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

    public StockEntry(string secId, string code, string name, string market, int qty = 0, double cost = 0)
    {
        SecId = secId;
        Code = code;
        Market = market;
        _name = name;
        _qty = qty;
        _cost = cost;
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
                OnPropertyChanged(nameof(QtyText));
                OnPropertyChanged(nameof(HasPosition));
            }
        }
    }

    public double Cost
    {
        get => _cost;
        set
        {
            if (Set(ref _cost, Math.Max(0, value))) OnPropertyChanged(nameof(CostText));
        }
    }

    public bool HasPosition => _qty > 0;

    /// <summary>Text shown in the settings list; empty means "no position".</summary>
    public string QtyText
    {
        get => _qty == 0 ? "" : _qty.ToString(CultureInfo.InvariantCulture);
        set
        {
            var text = (value ?? "").Trim().Replace(",", "").Replace(" ", "");
            if (text.Length == 0)
            {
                if (_qty != 0)
                {
                    _qty = 0;
                    OnPropertyChanged(nameof(Qty));
                    OnPropertyChanged(nameof(QtyText));
                    OnPropertyChanged(nameof(HasPosition));
                }
                else
                {
                    OnPropertyChanged(nameof(QtyText));
                }
                return;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty) && qty >= 0)
            {
                if (_qty != qty)
                {
                    _qty = qty;
                    OnPropertyChanged(nameof(Qty));
                    OnPropertyChanged(nameof(HasPosition));
                }
            }

            OnPropertyChanged(nameof(QtyText));
        }
    }

    public string CostText
    {
        get => _cost <= 0 ? "" : _cost.ToString("0.###", CultureInfo.InvariantCulture);
        set
        {
            var text = (value ?? "").Trim().Replace(",", "").Replace(" ", "");
            if (text.Length == 0)
            {
                _cost = 0;
            }
            else if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var cost) && cost >= 0)
            {
                _cost = cost;
            }
            OnPropertyChanged(nameof(Cost));
            OnPropertyChanged(nameof(CostText));
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

    public string DetailText
    {
        get
        {
            if (!_hasQuote)
                return $"{MarketLabel} · {Code}\n暂无行情数据";
            var local = _updatedAt.ToLocalTime();
            return string.Join("\n",
                $"{MarketLabel} · {Code}",
                $"昨收 {Format(_prevClose, Decimals)}    今开 {Format(_open, Decimals)}",
                $"最高 {Format(_high, Decimals)}    最低 {Format(_low, Decimals)}",
                $"{(IsOpen ? "交易中" : "休市")} · 更新于 {local:MM-dd HH:mm:ss}");
        }
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

    private static string Format(double value, int decimals)
        => value.ToString("N" + decimals, CultureInfo.InvariantCulture);
}

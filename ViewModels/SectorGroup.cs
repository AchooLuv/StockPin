namespace StockPin.ViewModels;

/// <summary>One sector header in the panel: name plus its board change.</summary>
public sealed class SectorGroup : ObservableObject
{
    private double _changePct;
    private bool _hasChange;

    public SectorGroup(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<StockEntry> Stocks { get; } = new();

    public string ChangePctText => _hasChange ? Format(_changePct) : "—";

    public string DirectionText =>
        !_hasChange || Math.Abs(_changePct) < 0.0001 ? "Flat" : _changePct > 0 ? "Up" : "Down";

    public void SetChange(double pct)
    {
        if (_hasChange && Math.Abs(_changePct - pct) < 0.0001) return;
        _hasChange = true;
        _changePct = pct;
        OnPropertyChanged(nameof(ChangePctText));
        OnPropertyChanged(nameof(DirectionText));
    }

    public void ClearChange()
    {
        if (!_hasChange) return;
        _hasChange = false;
        _changePct = 0;
        OnPropertyChanged(nameof(ChangePctText));
        OnPropertyChanged(nameof(DirectionText));
    }

    private static string Format(double pct)
    {
        string sign = pct > 0.0001 ? "+" : "";
        return sign + pct.ToString("0.00", CultureInfo.InvariantCulture) + "%";
    }
}

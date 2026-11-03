namespace StockPin.ViewModels;

/// <summary>An index shown in the board header (name + points + change).</summary>
public sealed class BoardIndex : ChangeGroup
{
    private double _price;
    private bool _hasPrice;

    public BoardIndex(string name, string secId)
    {
        Name = name;
        SecId = secId;
    }

    public string Name { get; }
    public string SecId { get; }

    public string PriceText => _hasPrice ? _price.ToString("N2", CultureInfo.InvariantCulture) : "";

    public bool HasPrice => _hasPrice;

    public void SetPrice(double price)
    {
        if (_hasPrice && Math.Abs(_price - price) < 0.000001) return;
        _price = price;
        _hasPrice = true;
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(HasPrice));
    }

    public void ClearPrice()
    {
        if (!_hasPrice) return;
        _hasPrice = false;
        _price = 0;
        OnPropertyChanged(nameof(PriceText));
        OnPropertyChanged(nameof(HasPrice));
    }
}

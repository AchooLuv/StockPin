namespace StockPin.ViewModels;

/// <summary>Listing board section: 沪市主板 / 深市主板 / 创业板 / 科创板 / 北交所 / 港股 / 美股.</summary>
public sealed class BoardGroup : ChangeGroup
{
    private double _price;
    private bool _hasPrice;

    public BoardGroup(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<SectorGroup> Sectors { get; } = new();

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

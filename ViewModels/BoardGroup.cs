namespace StockPin.ViewModels;

/// <summary>Listing board section: 沪市主板 / 深市主板 / 创业板 / 科创板 / 北交所 / 港股 / 美股.</summary>
public sealed class BoardGroup : ChangeGroup
{
    public BoardGroup(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<SectorGroup> Sectors { get; } = new();
}

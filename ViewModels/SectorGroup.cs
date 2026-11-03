namespace StockPin.ViewModels;

/// <summary>One sector inside a board.</summary>
public sealed class SectorGroup : ChangeGroup
{
    public SectorGroup(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<StockEntry> Stocks { get; } = new();
}

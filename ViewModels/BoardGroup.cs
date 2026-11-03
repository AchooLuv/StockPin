namespace StockPin.ViewModels;

/// <summary>One board section: header indices, A-share sectors, or flat HK/US stocks.</summary>
public sealed class BoardGroup : ObservableObject
{
    public BoardGroup(string name) => Name = name;

    public string Name { get; }
    public ObservableCollection<BoardIndex> Indices { get; } = new();
    public ObservableCollection<SectorGroup> Sectors { get; } = new();
    public ObservableCollection<StockEntry> Stocks { get; } = new();
}

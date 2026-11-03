namespace StockPin.ViewModels;

using System.Windows;

/// <summary>One flat row group: an A-share sector, or a whole HK/US board.</summary>
public sealed class DisplayGroup
{
    public DisplayGroup(BoardGroup board, SectorGroup? sector, IReadOnlyList<StockEntry> stocks, bool showBoard)
    {
        Board = board;
        Sector = sector;
        Stocks = stocks;
        ShowBoard = showBoard;
    }

    public BoardGroup Board { get; }
    public SectorGroup? Sector { get; }
    public IReadOnlyList<StockEntry> Stocks { get; }
    public bool ShowBoard { get; }
    public bool ShowSector => Sector != null;

    public Thickness TopMargin => ShowBoard ? new Thickness(0, 4, 0, 0) : new Thickness(0, 1, 0, 0);
}

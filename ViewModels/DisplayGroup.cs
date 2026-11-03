namespace StockPin.ViewModels;

using System.Windows;

/// <summary>One flat row group in the panel: a sector, plus its board on the first sector.</summary>
public sealed class DisplayGroup
{
    public DisplayGroup(BoardGroup board, SectorGroup sector, bool showBoard)
    {
        Board = board;
        Sector = sector;
        ShowBoard = showBoard;
    }

    public BoardGroup Board { get; }
    public SectorGroup Sector { get; }
    public bool ShowBoard { get; }

    public Thickness TopMargin => ShowBoard ? new Thickness(0, 4, 0, 0) : new Thickness(0, 1, 0, 0);
}

namespace StockPin.Core;

public sealed record Quote(
    string SecId,
    string Code,
    string Name,
    double Price,
    double PrevClose,
    double ChangePct,
    double ChangeAmt,
    double Open,
    double High,
    double Low,
    DateTimeOffset UpdatedAt,
    bool HasData);

public sealed record StockSearchResult(string SecId, string Code, string Name, string Market, string TypeName)
{
    public string Tag => MarketKind.Tag(Market);
}

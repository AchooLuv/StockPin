namespace StockPin.Core;

/// <summary>Built-in commodity futures supported by the quote-only futures strip.</summary>
public static class FuturesCatalog
{
    public static readonly IReadOnlyList<(string SecId, string Code, string Name)> Items = new[]
    {
        ("hf_OIL", "OIL", "布伦特原油"),
        ("hf_CL", "CL", "WTI原油"),
        ("nf_SC0", "SC0", "上海原油")
    };

    public static bool Contains(string secId)
        => Items.Any(i => string.Equals(i.SecId, secId, StringComparison.OrdinalIgnoreCase));

    public static (string SecId, string Code, string Name) Find(string secId)
        => Items.First(i => string.Equals(i.SecId, secId, StringComparison.OrdinalIgnoreCase));
}

namespace WeByte.Search.Core;

/// <summary>Esito paginato di una ricerca.</summary>
public sealed class SearchResult<T>
{
    public IReadOnlyList<T> Items { get; }

    public SearchResult(IReadOnlyList<T> items)
    {
        Items = items;
    }
}

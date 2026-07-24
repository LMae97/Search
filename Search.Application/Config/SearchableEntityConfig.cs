using Search.Application.Querying;
using Search.Application.Querying.Dynamic;

namespace Search.Application.Config;

public interface ISearchableEntityConfig
{
    SearchEntity SearchEntity { get; }
    IReadOnlyList<string> DefaultProjection { get; }
    IReadOnlyList<SortField> DefaultSort { get; }

    /// <summary>
    /// Come tradurre il free-text (<c>SearchRequest.Search</c>). Default: OR di contains (universale).
    /// Le entità che vogliono Atlas fanno override con <see cref="FreeTextSearch.Atlas"/>.
    /// </summary>
    FreeTextSearch FreeText => FreeTextSearch.OrContains;
}
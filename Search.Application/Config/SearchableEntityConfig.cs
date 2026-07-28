using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

namespace Search.Application.Config;

public interface ISearchableEntityConfig
{
    SearchEntity SearchEntity { get; }
    IReadOnlyList<string> HiddenProjection { get; }
    IReadOnlyList<string> DefaultProjection { get; }
    IReadOnlyList<SortField> DefaultSort { get; }
    string IdField { get; }
    public FilterNode? AuthFilters(IVisibilityFilters? filters);

    /// <summary>
    /// Come tradurre il free-text (<c>SearchRequest.Search</c>). Default: OR di contains (universale).
    /// Le entità che vogliono Atlas fanno override con <see cref="FreeTextSearch.Atlas"/>.
    /// </summary>
    FreeTextSearch FreeText => FreeTextSearch.OrContains;
}
using Search.Core;
using Search.Core.Dynamic;
using Search.Core.Filters;

namespace Search.Application.Config;

/// <summary>
/// Ricerca "a livello di prodotto": stessa collection/stessi campi di <see cref="ContractEntityConfig"/> (i
/// path <c>contractData._products.*</c> sono identici prima o dopo l'unwind — cambia solo la cardinalità del
/// match), quindi tutto è delegato a un'istanza di quella config. L'unica differenza reale è
/// <see cref="MongoUnwindPath"/>: ogni prodotto di un contratto diventa una riga indipendente, invece di un
/// array dentro la riga-contratto. Equivalente al vecchio contesto <c>ContractProducts</c>.
/// </summary>
public sealed class ContractProductEntityConfig : ISearchableEntityConfig
{
    private readonly ContractEntityConfig _contract = new();

    public SearchEntity SearchEntity => _contract.SearchEntity;
    public IReadOnlyList<string> DefaultProjection => _contract.DefaultProjection;
    public IReadOnlyList<SortField> DefaultSort => _contract.DefaultSort;
    public string IdField => _contract.IdField;
    public IReadOnlyList<string> HiddenProjection => _contract.HiddenProjection;
    public FilterNode? AuthFilters(IVisibilityFilters? filters) => _contract.AuthFilters(filters);
    public string? MongoAtlasIndex => _contract.MongoAtlasIndex;
    public string? MongoUnwindPath => "contractData._products";
}

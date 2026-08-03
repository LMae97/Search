using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Search;
using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Mongo;

namespace WeByte.Search.Infrastructure.Mongo;

/// <summary>
/// Handler di ricerca per lo store <b>Mongo</b>: serve tutte le entità documentali, risolvendo mappa e
/// collection per nome a runtime. Delega a <see cref="MongoSearchExecutor{TDocument}"/> (filtro → BsonDocument,
/// proiezione/sort/paginazione). La pipeline comune (permessi/potatura/validazione) è nella base.
/// </summary>
public sealed class MongoSearchHandler(
    DbBackedSearchMapProvider maps,
    IMongoCollectionProvider collections,
    ILogger<MongoSearchHandler> logger) : SearchHandlerBase(maps)
{
    public override StoreKind Store => StoreKind.Mongo;

    /// <summary>Nome convenzionale del campo tenant; se presente nella mappa, la ricerca è confinata al suo valore.</summary>
    private const string TenantField = "spaceId";

    protected override SearchResult<IReadOnlyDictionary<string, object?>> Execute(ISearchableEntityConfig config, IEntitySearchMap map, SearchRequest request, Guid spaceId)
    {
        var collection = collections.GetCollection(config.SearchEntity.Name);
        var scoped = ApplyTenantScope(map, request, spaceId);

        // L'indice Atlas viene dalla config d'entità; per le entità OrContains resta inutilizzato (SearchWithCount è già
        // stato espanso in filtro a monte, quindi il piano non produrrà alcuno stage $search).
        var atlasIndex = config.MongoAtlasIndex;
        var executor = new MongoSearchExecutor<BsonDocument>(map, atlasIndex, config.MongoUnwindPath);
        var plan = executor.BuildPlan(scoped);

        logger.LogInformation(
            "Mongo query su '{Collection}':\nsearch: {Search}\nunwind: {Unwind}\nfilter: {Filter}\nsort: {Sort}\nprojection: {Projection}\nskip {Skip}, limit {Limit}",
            collection.CollectionNamespace.CollectionName,
            plan.SearchStage?.ToJson() ?? "(nessuno)",
            plan.UnwindStage?.ToJson() ?? "(nessuno)",
            plan.Filter.ToJson(),
            plan.Sort?.ToJson() ?? "(rilevanza/default)",
            plan.Projection.ToJson(),
            plan.Skip,
            plan.Limit);

        return executor.Execute(collection, plan, scoped.Page);
    }

    protected override long ExecuteCount(ISearchableEntityConfig config, IEntitySearchMap map, SearchRequest request, Guid spaceId, long? upTo)
    {
        var collection = collections.GetCollection(config.SearchEntity.Name);
        var scoped = ApplyTenantScope(map, request, spaceId);

        var executor = new MongoSearchExecutor<BsonDocument>(map, config.MongoAtlasIndex, config.MongoUnwindPath);

        return executor.Count(collection, scoped, upTo);
    }

    // Scoping tenant server-side (l'equivalente Mongo del @space iniettato nel SQL): se l'entità ha un campo
    // "spaceId", lo forziamo dal caller in AND al filtro utente. Gira DOPO la validazione, quindi non viene
    // potato dal sanitizer e il client non può ampliare la visibilità oltre il proprio tenant.
    private static SearchRequest ApplyTenantScope(IEntitySearchMap map, SearchRequest request, Guid spaceId)
    {
        if (!map.TryGetField(TenantField, out _))
            return request;

        var tenant = Filter.Eq(TenantField, spaceId);
        FilterNode combined = request.Filter is null ? tenant : Filter.And(request.Filter, tenant);

        return new SearchRequest
        {
            FullTextSearch = request.FullTextSearch,
            Filter = combined,
            Projection = request.Projection,
            Sort = request.Sort,
            Page = request.Page
        };
    }
}

using WeByte.Search.Application.Config;
using WeByte.Search.Application.Dtos;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Export;
using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Tests;

/// <summary>
/// <see cref="SearchExporter.Decide"/> non scrive nulla e non esegue query sui dati: usa
/// <see cref="ISearchService.Count"/> per il numero di righe, mai <see cref="ISearchService.SearchWithCount"/>, e lo
/// chiama con un tetto (<c>maxSyncRows + SafetyMargin</c>) così lo store non conta oltre il necessario.
/// </summary>
public class SearchExporterDecideTests
{
    // ISearchService finto: SearchWithCount lancia, a riprova che Decide non lo chiami mai (nessuna query sui dati
    // per prendere una decisione che dipende solo dal conteggio). Count registra request E tetto ricevuti,
    // e risponde con un valore scelto dal test — a riprova che chi implementa Count per davvero (SQL/Mongo)
    // può restituire un conteggio capped, non necessariamente il totale esatto.
    private sealed class FakeSearchService : ISearchService
    {
        public long CountToReturn { get; set; }
        public List<(SearchRequest Request, long? UpTo)> CountCalls { get; } = [];

        public long Count(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? upTo = null)
        {
            CountCalls.Add((request, upTo));
            return CountToReturn;
        }

        public SearchResponseDto SearchWithCount(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? maxPageSize = 20, long? maxTotalCount = 1000)
            => throw new InvalidOperationException("Decide non deve eseguire una ricerca sui dati, solo un conteggio.");

        public SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? maxPageSize = 20)
            => throw new InvalidOperationException("Decide non deve eseguire una ricerca sui dati, solo un conteggio.");
    }

    private sealed class FakeEntityConfig : ISearchableEntityConfig
    {
        public SearchEntity SearchEntity => SearchEntity.RelationalRaw("fake");
        public IReadOnlyList<string> HiddenProjection => [];
        public IReadOnlyList<string> DefaultProjection => ["a", "b", "c", "d"]; // 4 colonne di default
        public IReadOnlyList<SortField> DefaultSort => [];
        public string IdField => "a";
        public FilterNode? AuthFilters(IVisibilityFilters? filters) => null;
    }

    // Le colonne si risolvono dalla mappa effettiva (SearchExporter.ResolveColumns): i campi "a".."d" della
    // FakeEntityConfig devono esistere davvero, altrimenti il sanitizer li scarterebbe tutti e il conteggio
    // colonne sarebbe sempre zero.
    private static SearchExporter Exporter(FakeSearchService search)
    {
        var provider = new InMemorySearchFieldDefinitionProvider();
        foreach (var name in new[] { "a", "b", "c", "d" })
            provider.Add(TestSupport.Def("fake", name, FieldKind.String, name));

        return new SearchExporter(new DbBackedSearchMapProvider(provider), search);
    }

    private static SearchCaller Caller() => new(Guid.NewGuid(), new HashSet<Guid>());

    [Fact]
    public void Decide_counts_instead_of_running_a_search()
    {
        var search = new FakeSearchService { CountToReturn = 5 };
        var request = new SearchRequest { Page = new PageRequest(1, 500) };

        // Se Decide chiamasse SearchWithCount invece di Count, FakeSearchService.SearchWithCount lancerebbe: il test
        // fallirebbe con quell'eccezione invece di verificare l'esito.
        Exporter(search).Decide(new FakeEntityConfig(), request, Caller());

        var (counted, _) = Assert.Single(search.CountCalls);
        Assert.Same(request, counted); // passata così com'è: Count non ha bisogno di riscrivere la pagina
    }

    /*
    [Fact]
    public void Decide_caps_the_count_at_max_sync_rows_plus_the_safety_margin()
    {
        var search = new FakeSearchService { CountToReturn = 5 };
        var limit = new ExportSizeLimit { MaxSyncCells = 1000, SafetyMargin = 10 }; // 4 colonne ⇒ soglia 250

        Exporter(search).Decide(new FakeEntityConfig(), new SearchRequest(), Caller(), limit);

        var (_, upTo) = Assert.Single(search.CountCalls);
        Assert.Equal(260, upTo); // 250 + 10, non "conta tutto"
    }
    */

    [Fact]
    public void Within_the_threshold_is_sync()
    {
        var search = new FakeSearchService { CountToReturn = 100 };
        var limit = new ExportSizeLimit { MaxSyncCells = 1000 }; // 4 colonne ⇒ soglia 250 righe

        var decision = Exporter(search).Decide(new FakeEntityConfig(), new SearchRequest(), Caller(), limit);

        Assert.IsType<ExportDecision.Sync>(decision);
    }

    [Fact]
    public void Past_the_threshold_is_deferred_with_whatever_the_store_returned()
    {
        var search = new FakeSearchService { CountToReturn = 999 };
        var limit = new ExportSizeLimit { MaxSyncCells = 1000 }; // 4 colonne ⇒ soglia 250 righe

        var decision = Exporter(search).Decide(new FakeEntityConfig(), new SearchRequest(), Caller(), limit);

        var deferred = Assert.IsType<ExportDecision.TooLargeForSync>(decision);
        Assert.Equal(999, deferred.RowCount);
        Assert.Equal(250, deferred.MaxSyncRows);
    }

    [Fact]
    public void Explicit_projection_count_is_used_instead_of_the_config_default()
    {
        var search = new FakeSearchService { CountToReturn = 300 };
        var limit = new ExportSizeLimit { MaxSyncCells = 1000 };

        // Il client chiede 2 colonne, non le 4 di default: soglia 500 righe, non 250 ⇒ resta sincrono.
        var request = new SearchRequest { Projection = ["a", "b"] };

        var decision = Exporter(search).Decide(new FakeEntityConfig(), request, Caller(), limit);

        Assert.IsType<ExportDecision.Sync>(decision);
    }

    [Fact]
    public void No_limit_given_falls_back_to_the_default()
    {
        var search = new FakeSearchService { CountToReturn = 1 };

        // Con ExportSizeLimit.Default (200_000 celle) qualunque piccolo risultato resta sincrono.
        var decision = Exporter(search).Decide(new FakeEntityConfig(), new SearchRequest(), Caller());

        Assert.IsType<ExportDecision.Sync>(decision);
    }
}

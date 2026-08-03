using System.Text;
using WeByte.Search.Application.Config;
using WeByte.Search.Application.Dtos;
using WeByte.Search.Application.Querying.Authorization;
using WeByte.Search.Application.Querying.Dynamic;
using WeByte.Search.Application.Export;
using WeByte.Search.Core;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;
using WeByte.Search.Export;
using WeByte.Search.Application.Search;

namespace WeByte.Search.Tests;

/// <summary>
/// <see cref="SearchExporter.Write"/> deve ripulire dai risultati i campi non esportabili PRIMA di scriverli
/// nel file: i nascosti (es. "id", che la ricerca include per la griglia — l'FE lo usa come chiave di riga
/// senza mostrarlo come colonna) e i <see cref="FieldKind.Custom"/> (bottoni/immagini della UI, es. "btn" —
/// un pulsante "impersonifica" non ha senso in un CSV). Entrambi restano invece nella risposta della
/// ricerca normale, dove servono davvero.
/// </summary>
public class SearchExporterWriteTests
{
    private const string Entity = "fake";
    private const string ButtonValue = "IMPERSONATE";
    private static readonly Guid HiddenId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    // ISearchService finto: restituisce righe che includono ANCHE il campo nascosto, esattamente come fa la
    // pipeline vera (ProjectionResolver unisce sempre HiddenProjection alla proiezione). Registra ogni
    // chiamata a SearchWithCount, per verificare che Write ne faccia una sola (requisito: mai paginare a batch, per
    // non rischiare duplicati/righe saltate se la tabella cambia fra una query e l'altra).
    private sealed class FakeSearchService : ISearchService
    {
        public List<SearchRequest> SearchCalls { get; } = [];

        public SearchResponseDto Search(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? maxPageSize = 20)
            => SearchWithCount(config, request, caller, maxPageSize);

        public SearchResponseDto SearchWithCount(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? maxPageSize = 20, long? maxTotalCount = 1000)
        {
            SearchCalls.Add(request);

            var rows = new List<IReadOnlyDictionary<string, object?>>
            {
                new Dictionary<string, object?> { ["id"] = HiddenId, ["name"] = "Acea", ["btn"] = ButtonValue }
            };

            return new SearchResponseDto
            {
                Header =
                [
                    new HeaderDto { Id = "1", Key = "id", Label = "Id", Type = "Guid", Visible = false },
                    new HeaderDto { Id = "2", Key = "name", Label = "Nome", Type = "String", Visible = true }
                ],
                Body = rows,
                ResultCount = 1
            };
        }

        public long Count(ISearchableEntityConfig config, SearchRequest request, SearchCaller caller, long? upTo = null)
            => throw new InvalidOperationException("Write non deve contare, solo cercare.");
    }

    private sealed class FakeEntityConfig : ISearchableEntityConfig
    {
        public SearchEntity SearchEntity => SearchEntity.RelationalRaw(Entity);
        public IReadOnlyList<string> HiddenProjection => ["id"];
        public IReadOnlyList<string> DefaultProjection => ["id", "name", "btn"];
        public IReadOnlyList<SortField> DefaultSort => [];
        public string IdField => "id";
        public FilterNode? AuthFilters(IVisibilityFilters? filters) => null;
    }

    private static SearchExporter Exporter(FakeSearchService search)
    {
        var provider = new InMemorySearchFieldDefinitionProvider();
        provider.Add(TestSupport.Def(Entity, "id", FieldKind.Guid, "id", isHidden: true));
        provider.Add(TestSupport.Def(Entity, "name", FieldKind.String, "name"));
        provider.Add(TestSupport.Def(Entity, "btn", FieldKind.Custom, "btn"));

        return new SearchExporter(new DbBackedSearchMapProvider(provider), search);
    }

    [Fact]
    public void Hidden_fields_are_stripped_from_the_written_rows()
    {
        using var stream = new MemoryStream();
        Exporter(new FakeSearchService()).Write(stream, new CsvTabularWriterFactory(), new FakeEntityConfig(), new SearchRequest(), Caller());

        var csv = new UTF8Encoding(false).GetString(stream.ToArray());

        // Una sola colonna scritta (né il nascosto né il Custom diventano mai una colonna). Le colonne dello
        // scrittore vengono da SearchExporter.ResolveColumns (mappa effettiva), non dall'Header finto della
        // fake ricerca: label di default = nome del campo, "name", non quello impostato nell'Header.
        Assert.Equal("name\r\nAcea\r\n", csv.TrimStart('﻿'));
        // ...e nemmeno il valore del campo nascosto finisce nel file per qualche altra via.
        Assert.DoesNotContain(HiddenId.ToString(), csv);
    }

    [Fact]
    public void Custom_kind_fields_are_excluded_from_the_export_like_a_ui_button_should_be()
    {
        using var stream = new MemoryStream();
        Exporter(new FakeSearchService()).Write(stream, new CsvTabularWriterFactory(), new FakeEntityConfig(), new SearchRequest(), Caller());

        var csv = new UTF8Encoding(false).GetString(stream.ToArray());

        Assert.DoesNotContain("btn", csv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ButtonValue, csv);
    }

    [Fact]
    public void Write_executes_exactly_one_search_never_paginated()
    {
        var search = new FakeSearchService();
        using var stream = new MemoryStream();

        Exporter(search).Write(stream, new CsvTabularWriterFactory(), new FakeEntityConfig(), new SearchRequest(), Caller());

        // Requisito di progetto: un'unica query, mai un round-trip per batch — evita duplicati/righe saltate
        // se la tabella cambia fra una query e l'altra.
        var call = Assert.Single(search.SearchCalls);
        Assert.Equal(1, call.Page.Number);
    }

    [Fact]
    public void Without_a_row_limit_the_single_query_asks_for_everything()
    {
        var search = new FakeSearchService();
        using var stream = new MemoryStream();

        Exporter(search).Write(stream, new CsvTabularWriterFactory(), new FakeEntityConfig(), new SearchRequest(), Caller());

        // Nessun ExportPagingOptions.MaxRows ⇒ la dimensione richiesta è "il più grande possibile", non un
        // batch arbitrario: non esiste un valore di PageRequest.Size che significhi "senza limite".
        Assert.Equal(int.MaxValue, Assert.Single(search.SearchCalls).Page.Size);
    }

    [Fact]
    public void A_row_limit_becomes_the_size_of_the_single_query()
    {
        var search = new FakeSearchService();
        using var stream = new MemoryStream();

        Exporter(search).Write(
            stream, new CsvTabularWriterFactory(), new FakeEntityConfig(), new SearchRequest(), Caller(),
            new ExportPagingOptions { MaxRows = 500 });

        Assert.Equal(500, Assert.Single(search.SearchCalls).Page.Size);
    }

    private static SearchCaller Caller() => new(Guid.NewGuid(), new HashSet<Guid>());
}

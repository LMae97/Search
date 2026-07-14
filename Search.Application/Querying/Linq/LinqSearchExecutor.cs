using System.Linq.Expressions;
using Search.Application.Querying.Metadata;
using Search.Domain.Common;

namespace Search.Application.Querying.Linq;

/// <summary>
/// Esegue una <see cref="SearchRequest"/> su un <see cref="IQueryable{T}"/> (EF Core o
/// LINQ-to-Objects): filtro, ordinamento, conteggio, paginazione e <b>proiezione spinta nella query</b>.
/// <para>
/// La proiezione costruisce un <c>Select</c> dinamico verso <c>object[]</c> con i soli campi richiesti:
/// così EF seleziona solo quelle colonne (niente <c>SELECT *</c>) e traduce i campi collezione (es. i tag)
/// in subquery — invece di materializzare l'intera entità e proiettare in memoria (che, sotto EF, non
/// caricherebbe le navigation e restituirebbe collezioni vuote).
/// </para>
/// </summary>
public sealed class LinqSearchExecutor<TEntity>
{
    private readonly IEntitySearchMap _map;
    private readonly LinqFilterTranslator<TEntity> _translator;

    public LinqSearchExecutor(IEntitySearchMap map)
    {
        _map = map;
        _translator = new LinqFilterTranslator<TEntity>(map);
    }

    public SearchResult<IReadOnlyDictionary<string, object?>> Execute(IQueryable<TEntity> source, SearchRequest request)
    {
        var query = source;

        if (request.Filter is not null)
            query = query.Where(_translator.Translate(request.Filter));

        query = ApplySort(query, request.Sort);

        var total = query.LongCount();

        var (projection, fieldNames) = BuildProjection(request.Projection);

        // La proiezione è spinta nella query: EF emette SELECT <solo colonne richieste> (+ subquery collezioni).
        var rows = query
            .Skip(request.Page.Skip)
            .Take(request.Page.Size)
            .Select(projection)
            .ToList();

        var items = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        foreach (var row in rows)
        {
            var record = new Dictionary<string, object?>(fieldNames.Count, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < fieldNames.Count; i++)
                record[fieldNames[i]] = row[i];
            items.Add(record);
        }

        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }

    private IQueryable<TEntity> ApplySort(IQueryable<TEntity> query, IReadOnlyList<SortField> sorts)
    {
        if (sorts.Count == 0)
        {
            query = ApplyDefaultSort(query, isFirst: true);
            return query;
        }

        IOrderedQueryable<TEntity>? ordered = null;
        for (var i = 0; i < sorts.Count; i++)
        {
            var sort = sorts[i];
            if (!_map.TryGetField(sort.Field, out var field))
                throw new InvalidOperationException($"Ordinamento su campo non mappato '{sort.Field}'.");

            var ascending = sort.Direction == SortDirection.Ascending;
            var methodName = i == 0
                ? (ascending ? "OrderBy" : "OrderByDescending")
                : (ascending ? "ThenBy" : "ThenByDescending");

            var selector = field.Selector
                ?? throw new NotSupportedException($"Ordinamento non supportato sul campo dinamico '{sort.Field}' via LINQ.");
            var method = typeof(Queryable).GetMethods()
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), selector.ReturnType);

            var currentSource = i == 0 ? query : ordered!;
            ordered = (IOrderedQueryable<TEntity>)method.Invoke(null, new object[] { currentSource, selector })!;
        }

        return ApplyDefaultSort(ordered, isFirst: false);
    }

    private IOrderedQueryable<TEntity> ApplyDefaultSort(IQueryable<TEntity> query, bool isFirst)
    {
        //Se id è presente nella mappa, lo usiamo come ordinamento di default (altrimenti usiamo la data di creazione, altrimenti comunichiamo errore se isFirst è true).
        //Se TEntity è un AggregateRoot, allora id è sempre presente, altrimenti non possiamo fare assunzioni.
        if (_map.TryGetField("id", out var idField))
        {
            var selector = idField.Selector
                ?? throw new NotSupportedException($"Ordinamento non supportato sul campo dinamico 'id' via LINQ.");
            var methodName = isFirst ? "OrderBy" : "ThenBy";
            var method = typeof(Queryable).GetMethods()
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), selector.ReturnType);
            return (IOrderedQueryable<TEntity>)method.Invoke(null, new object[] { query, selector })!;
        }
        else if (_map.TryGetField("createdAt", out var createdAtField))
        {
            var selector = createdAtField.Selector
                ?? throw new NotSupportedException($"Ordinamento non supportato sul campo dinamico 'createdAt' via LINQ.");
            var methodName = isFirst ? "OrderBy" : "ThenBy";
            var method = typeof(Queryable).GetMethods()
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), selector.ReturnType);
            return (IOrderedQueryable<TEntity>)method.Invoke(null, new object[] { query, selector })!;
        }
        else if (isFirst)
        {
            throw new InvalidOperationException("Nessun ordinamento specificato e nessun ordinamento di default disponibile (né 'Id' né 'CreatedAt').");
        }
        else
        {
            return (IOrderedQueryable<TEntity>)query;
        }
    }

    /// <summary>
    /// Costruisce <c>x =&gt; new object[] { x.A, x.B.C, x.Tags.Select(t =&gt; t.Name).ToList(), ... }</c>
    /// con i soli campi richiesti (o quelli visibili di default se la proiezione è vuota).
    /// </summary>
    private (Expression<Func<TEntity, object[]>> Selector, IReadOnlyList<string> Names) BuildProjection(IReadOnlyList<string> projection)
    {
        var names = projection.Count == 0
            ? _map.Fields.Values.Where(field => field.VisibleByDefault).Select(field => field.Name).ToList()
            : projection.ToList();

        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var elements = new List<Expression>(names.Count);

        foreach (var name in names)
        {
            if (!_map.TryGetField(name, out var field))
                throw new InvalidOperationException($"Proiezione su campo non mappato '{name}'.");
            var selector = field.Selector
                ?? throw new NotSupportedException($"Il campo dinamico '{name}' non è proiettabile via LINQ.");

            Expression body = ParameterReplacer.Rebase(selector, parameter);

            // Campo array: materializziamo (EF → subquery; in memoria → ToList). Altrimenti EF non saprebbe
            // shaparlo dentro l'array e, col vecchio approccio, la navigation restava vuota.
            if (field.IsArray)
            {
                var toList = typeof(Enumerable).GetMethod(nameof(Enumerable.ToList))!.MakeGenericMethod(field.ClrType);
                body = Expression.Call(toList, body);
            }

            elements.Add(Expression.Convert(body, typeof(object)));
        }

        var array = Expression.NewArrayInit(typeof(object), elements);
        return (Expression.Lambda<Func<TEntity, object[]>>(array, parameter), names);
    }
}

using System.Linq.Expressions;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Linq;

/// <summary>
/// Esegue una <see cref="SearchRequest"/> su un <see cref="IQueryable{T}"/> (EF Core o
/// LINQ-to-Objects): applica filtro, ordinamento, conteggio totale, paginazione e proiezione
/// dinamica verso dizionari (nome campo → valore). La proiezione a dizionario realizza il
/// requisito "ogni campo proiettabile" senza dover definire un DTO per ogni combinazione.
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

        var page = query
            .Skip(request.Page.Skip)
            .Take(request.Page.Size)
            .ToList();

        var items = Project(page, request.Projection);
        return new SearchResult<IReadOnlyDictionary<string, object?>>(items, total, request.Page.Number, request.Page.Size);
    }

    private IQueryable<TEntity> ApplySort(IQueryable<TEntity> query, IReadOnlyList<SortField> sorts)
    {
        if (sorts.Count == 0)
            return query;

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

            var method = typeof(Queryable).GetMethods()
                .Single(m => m.Name == methodName && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(TEntity), field.Selector.ReturnType);

            var currentSource = i == 0 ? query : ordered!;
            ordered = (IOrderedQueryable<TEntity>)method.Invoke(null, new object[] { currentSource, field.Selector })!;
        }

        return ordered!;
    }

    private IReadOnlyList<IReadOnlyDictionary<string, object?>> Project(
        IReadOnlyList<TEntity> items,
        IReadOnlyList<string> projection)
    {
        var fieldNames = projection.Count == 0 ? _map.Fields.Keys.ToList() : projection.ToList();

        var getters = new List<(string Name, Func<TEntity, object?> Getter)>(fieldNames.Count);
        foreach (var name in fieldNames)
        {
            if (!_map.TryGetField(name, out var field))
                throw new InvalidOperationException($"Proiezione su campo non mappato '{name}'.");
            getters.Add((field.Name, CompileGetter(field.Selector)));
        }

        var result = new List<IReadOnlyDictionary<string, object?>>(items.Count);
        foreach (var item in items)
        {
            var row = new Dictionary<string, object?>(getters.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, getter) in getters)
                row[name] = getter(item);
            result.Add(row);
        }

        return result;
    }

    private static Func<TEntity, object?> CompileGetter(LambdaExpression selector)
    {
        var parameter = selector.Parameters[0];
        Expression body = selector.Body;
        if (body.Type.IsValueType)
            body = Expression.Convert(body, typeof(object));
        return Expression.Lambda<Func<TEntity, object?>>(body, parameter).Compile();
    }
}

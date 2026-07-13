using System.Linq.Expressions;
using System.Reflection;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Ricostruisce un selettore (<c>x =&gt; x.Seg1.Seg2...</c>) da una property-path testuale (es. "Price.Amount")
/// e dal tipo dell'entità. È il ponte per le definizioni <b>a DB</b> dei campi relazionali: la stringa salvata
/// torna a essere un'<see cref="Expression"/> eseguibile (EF/LINQ).
/// <para>
/// Gestisce le catene scalari e le <b>collezioni</b>: se un segmento è una collezione e restano segmenti,
/// emette un <c>Select</c> sull'elemento (es. "Tags.Name" → <c>x.Tags.Select(t =&gt; t.Name)</c>, tipico del
/// molti-a-molti). La validità del path è verificata al caricamento (fail-fast su segmento inesistente).
/// </para>
/// </summary>
public static class PropertyPathSelectorFactory
{
    public static LambdaExpression Build(Type entityType, string propertyPath)
    {
        if (string.IsNullOrWhiteSpace(propertyPath))
            throw new ArgumentException("Property path vuoto.", nameof(propertyPath));

        var parameter = Expression.Parameter(entityType, "x");
        var body = BuildAccessor(parameter, entityType, Split(propertyPath), propertyPath);
        return Expression.Lambda(body, parameter);
    }

    /// <summary>Se <paramref name="type"/> è una collezione (non <see cref="string"/>), restituisce il tipo elemento.</summary>
    public static Type? GetEnumerableElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            return type.GetGenericArguments()[0];
        var enumerable = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }

    private static string[] Split(string path)
        => path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Expression BuildAccessor(Expression source, Type currentType, string[] segments, string fullPath)
    {
        var expression = source;
        var type = currentType;

        for (var i = 0; i < segments.Length; i++)
        {
            var property = type.GetProperty(segments[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Property '{segments[i]}' non trovata su '{type.Name}' (path '{fullPath}').");

            expression = Expression.Property(expression, property);
            type = property.PropertyType;

            var elementType = GetEnumerableElementType(type);
            if (elementType is not null && i < segments.Length - 1)
            {
                // Collezione + path residuo → source.Select(e => <residuo applicato a e>).
                var remaining = segments[(i + 1)..];
                var elementParameter = Expression.Parameter(elementType, "e");
                var elementBody = BuildAccessor(elementParameter, elementType, remaining, fullPath);
                var selectLambda = Expression.Lambda(elementBody, elementParameter);
                var select = SelectMethod.MakeGenericMethod(elementType, elementBody.Type);
                return Expression.Call(select, expression, selectLambda);
            }
        }

        return expression;
    }

    // Enumerable.Select<TSource,TResult>(IEnumerable<TSource>, Func<TSource,TResult>) — non l'overload indicizzato.
    private static readonly MethodInfo SelectMethod = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Select)
                     && m.GetParameters().Length == 2
                     && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2);
}

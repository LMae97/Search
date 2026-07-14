using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Linq;

/// <summary>
/// Traduce un albero di filtri in <c>Expression&lt;Func&lt;TEntity,bool&gt;&gt;</c>.
/// La stessa Expression è consumata da LINQ-to-Objects (test in memoria) e da EF Core
/// (che la traduce in SQL). È il translator per il mondo PostgresEF/relazionale.
/// Presuppone che la richiesta sia già stata validata dal <c>SearchRequestValidator</c>.
/// </summary>
public sealed class LinqFilterTranslator<TEntity>
{
    private readonly IEntitySearchMap _map;

    public LinqFilterTranslator(IEntitySearchMap map) => _map = map;

    public Expression<Func<TEntity, bool>> Translate(FilterNode node)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var body = Build(node, parameter);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private Expression Build(FilterNode node, ParameterExpression parameter) => node switch
    {
        LogicalFilterNode logical => BuildLogical(logical, parameter),
        ComparisonFilterNode comparison => BuildComparison(comparison, parameter),
        _ => throw new NotSupportedException($"Nodo di filtro non supportato: {node.GetType().Name}.")
    };

    private Expression BuildLogical(LogicalFilterNode node, ParameterExpression parameter)
    {
        if (node.Operator == LogicalOperator.Not)
            return Expression.Not(Build(node.Children[0], parameter));

        var children = node.Children.Select(child => Build(child, parameter)).ToList();
        Func<Expression, Expression, Expression> combine = node.Operator == LogicalOperator.And
            ? (a, b) => Expression.AndAlso(a, b)
            : (a, b) => Expression.OrElse(a, b);

        // AND/OR hanno sempre >= 1 figlio (garantito da validazione/sanitizer): nessun seed.
        return children.Aggregate(combine);
    }

    private Expression BuildComparison(ComparisonFilterNode node, ParameterExpression parameter)
    {
        if (!_map.TryGetField(node.Field, out var field))
            throw new InvalidOperationException($"Campo '{node.Field}' non mappato (validare prima di tradurre).");

        var selector = field.Selector
            ?? throw new NotSupportedException($"Il campo dinamico '{field.Name}' non è supportato dal translator LINQ.");
        var member = ParameterReplacer.Rebase(selector, parameter);

        return field.IsArray
            ? BuildArray(node, field, member)
            : BuildScalar(node, field, member);
    }

    // --- Scalari ---

    private static Expression BuildScalar(ComparisonFilterNode node, FieldDescriptor field, Expression member)
    {
        switch (node.Operator)
        {
            case FilterOperator.IsNull:
                return NullCheck(member, isNull: true);
            case FilterOperator.IsNotNull:
                return NullCheck(member, isNull: false);

            case FilterOperator.Equals:
                return Expression.Equal(member, Constant(node.SingleValue, field, member.Type));
            case FilterOperator.NotEquals:
                return Expression.NotEqual(member, Constant(node.SingleValue, field, member.Type));

            case FilterOperator.GreaterThan:
                return Expression.GreaterThan(member, Constant(node.SingleValue, field, member.Type));
            case FilterOperator.GreaterThanOrEqual:
                return Expression.GreaterThanOrEqual(member, Constant(node.SingleValue, field, member.Type));
            case FilterOperator.LessThan:
                return Expression.LessThan(member, Constant(node.SingleValue, field, member.Type));
            case FilterOperator.LessThanOrEqual:
                return Expression.LessThanOrEqual(member, Constant(node.SingleValue, field, member.Type));

            case FilterOperator.Between:
                return Expression.AndAlso(
                    Expression.GreaterThanOrEqual(member, Constant(node.Values[0], field, member.Type)),
                    Expression.LessThanOrEqual(member, Constant(node.Values[1], field, member.Type)));

            case FilterOperator.In:
                return BuildIn(member, field, node.Values, negate: false);
            case FilterOperator.NotIn:
                return BuildIn(member, field, node.Values, negate: true);

            case FilterOperator.Contains:
                return StringCall(member, StringContains, node.SingleValue);
            case FilterOperator.NotContains:
                return Expression.Not(StringCall(member, StringContains, node.SingleValue));
            case FilterOperator.StartsWith:
                return StringCall(member, StringStartsWith, node.SingleValue);
            case FilterOperator.EndsWith:
                return StringCall(member, StringEndsWith, node.SingleValue);

            default:
                throw new NotSupportedException($"Operatore {node.Operator} non valido sul campo scalare '{field.Name}'.");
        }
    }

    private static Expression Constant(object? rawValue, FieldDescriptor field, Type memberType)
    {
        var coerced = ValueCoercion.Coerce(rawValue, field.ClrType);
        if (coerced is null)
            return Expression.Constant(null, memberType);

        Expression constant = Expression.Constant(coerced);
        if (constant.Type != memberType)
            constant = Expression.Convert(constant, memberType);
        return constant;
    }

    private static Expression NullCheck(Expression member, bool isNull)
    {
        var type = member.Type;
        var isNullable = !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;
        if (!isNullable)
            return Expression.Constant(!isNull); // un valore non-nullable non è mai null

        var nullConstant = Expression.Constant(null, type);
        return isNull ? Expression.Equal(member, nullConstant) : Expression.NotEqual(member, nullConstant);
    }

    private static Expression StringCall(Expression member, MethodInfo method, object? value)
    {
        // Case-insensitive, coerente con Mongo (regex "i"): abbassiamo entrambi i lati.
        // Usiamo ToLower() (che EF traduce in LOWER(...)) e non StringComparison, che EF NON traduce.
        // (Nota: LOWER lato DB dipende dalla collation; per termini ASCII è equivalente all'invariant.)
        var argument = Expression.Constant(value?.ToString()?.ToLowerInvariant() ?? string.Empty, typeof(string));
        // Protegge da NullReferenceException quando la stringa è null (es. Description nullable).
        var notNull = Expression.NotEqual(member, Expression.Constant(null, member.Type));
        var memberLowered = Expression.Call(member, StringToLower);
        return Expression.AndAlso(notNull, Expression.Call(memberLowered, method, argument));
    }

    private static Expression BuildIn(Expression member, FieldDescriptor field, IReadOnlyList<object?> values, bool negate)
    {
        var elementType = member.Type;
        var list = CreateTypedList(values, field.ClrType, elementType);
        var contains = EnumerableContains(elementType);
        var call = Expression.Call(contains, Expression.Constant(list, list.GetType()), member);
        return negate ? Expression.Not(call) : call;
    }

    // --- Array/collezioni ---

    private static Expression BuildArray(ComparisonFilterNode node, FieldDescriptor field, Expression member)
    {
        var element = field.ClrType;

        switch (node.Operator)
        {
            case FilterOperator.ArrayIsEmpty:
                return Expression.Not(AnyNoPredicate(member, element));
            case FilterOperator.ArrayNotEmpty:
                return AnyNoPredicate(member, element);

            case FilterOperator.ArrayContains:
                return Expression.Call(
                    EnumerableContains(element),
                    member,
                    Expression.Constant(ValueCoercion.Coerce(node.SingleValue, element), element));

            case FilterOperator.ArrayContainsAny:
            {
                // member.Any(e => values.Contains(e))
                var values = CreateTypedList(node.Values, element, element);
                var e = Expression.Parameter(element, "e");
                var predicate = Expression.Lambda(
                    Expression.Call(EnumerableContains(element), Expression.Constant(values, values.GetType()), e), e);
                return Expression.Call(EnumerableMethod("Any", 2).MakeGenericMethod(element), member, predicate);
            }

            case FilterOperator.ArrayContainsAll:
            {
                // values.All(v => member.Contains(v))
                var values = CreateTypedList(node.Values, element, element);
                var v = Expression.Parameter(element, "v");
                var predicate = Expression.Lambda(
                    Expression.Call(EnumerableContains(element), member, v), v);
                return Expression.Call(
                    EnumerableMethod("All", 2).MakeGenericMethod(element),
                    Expression.Constant(values, values.GetType()),
                    predicate);
            }

            default:
                throw new NotSupportedException($"Operatore {node.Operator} non valido sul campo array '{field.Name}'.");
        }
    }

    private static Expression AnyNoPredicate(Expression source, Type element)
        => Expression.Call(EnumerableMethod("Any", 1).MakeGenericMethod(element), source);

    // --- Helper di reflection ---

    private static IList CreateTypedList(IReadOnlyList<object?> values, Type coerceTo, Type listElementType)
    {
        var listType = typeof(List<>).MakeGenericType(listElementType);
        var list = (IList)Activator.CreateInstance(listType)!;
        foreach (var value in values)
            list.Add(ValueCoercion.Coerce(value, coerceTo));
        return list;
    }

    private static readonly MethodInfo StringToLower =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    private static readonly MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), new[] { typeof(string) })!;

    private static readonly MethodInfo StringStartsWith =
        typeof(string).GetMethod(nameof(string.StartsWith), new[] { typeof(string) })!;

    private static readonly MethodInfo StringEndsWith =
        typeof(string).GetMethod(nameof(string.EndsWith), new[] { typeof(string) })!;

    private static MethodInfo EnumerableMethod(string name, int parameterCount)
        => typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == name && m.GetParameters().Length == parameterCount);

    private static MethodInfo EnumerableContains(Type element)
        => EnumerableMethod("Contains", 2).MakeGenericMethod(element);
}

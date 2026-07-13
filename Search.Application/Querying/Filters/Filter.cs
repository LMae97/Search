namespace Search.Application.Querying.Filters;

/// <summary>
/// Factory fluente per costruire alberi di filtri in modo leggibile nel codice/test:
/// <code>Filter.And(Filter.Gte("price", 10), Filter.ArrayContainsAny("tags", "sale"))</code>
/// </summary>
public static class Filter
{
    public static ComparisonFilterNode Eq(string field, object? value) => new(field, FilterOperator.Equals, value);
    public static ComparisonFilterNode Ne(string field, object? value) => new(field, FilterOperator.NotEquals, value);
    public static ComparisonFilterNode Gt(string field, object? value) => new(field, FilterOperator.GreaterThan, value);
    public static ComparisonFilterNode Gte(string field, object? value) => new(field, FilterOperator.GreaterThanOrEqual, value);
    public static ComparisonFilterNode Lt(string field, object? value) => new(field, FilterOperator.LessThan, value);
    public static ComparisonFilterNode Lte(string field, object? value) => new(field, FilterOperator.LessThanOrEqual, value);
    public static ComparisonFilterNode Between(string field, object? low, object? high) => new(field, FilterOperator.Between, low, high);
    public static ComparisonFilterNode In(string field, params object?[] values) => new(field, FilterOperator.In, values);
    public static ComparisonFilterNode NotIn(string field, params object?[] values) => new(field, FilterOperator.NotIn, values);
    public static ComparisonFilterNode Contains(string field, object? value) => new(field, FilterOperator.Contains, value);
    public static ComparisonFilterNode NotContains(string field, object? value) => new(field, FilterOperator.NotContains, value);
    public static ComparisonFilterNode StartsWith(string field, object? value) => new(field, FilterOperator.StartsWith, value);
    public static ComparisonFilterNode EndsWith(string field, object? value) => new(field, FilterOperator.EndsWith, value);
    public static ComparisonFilterNode IsNull(string field) => new(field, FilterOperator.IsNull);
    public static ComparisonFilterNode IsNotNull(string field) => new(field, FilterOperator.IsNotNull);
    public static ComparisonFilterNode ArrayContains(string field, object? value) => new(field, FilterOperator.ArrayContains, value);
    public static ComparisonFilterNode ArrayContainsAny(string field, params object?[] values) => new(field, FilterOperator.ArrayContainsAny, values);
    public static ComparisonFilterNode ArrayContainsAll(string field, params object?[] values) => new(field, FilterOperator.ArrayContainsAll, values);
    public static ComparisonFilterNode ArrayIsEmpty(string field) => new(field, FilterOperator.ArrayIsEmpty);
    public static ComparisonFilterNode ArrayNotEmpty(string field) => new(field, FilterOperator.ArrayNotEmpty);

    public static LogicalFilterNode And(params FilterNode[] children) => LogicalFilterNode.And(children);
    public static LogicalFilterNode Or(params FilterNode[] children) => LogicalFilterNode.Or(children);
    public static FilterNode Not(FilterNode child) => LogicalFilterNode.Not(child);
}

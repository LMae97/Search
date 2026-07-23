using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;
using Search.Infrastructure.Sql;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>
/// Copertura esaustiva del translator SQL: matrice tipo × operatore. I valori utente diventano SEMPRE
/// parametri @pN (mai interpolati) e vengono coerciati al ClrType del campo.
/// </summary>
public sealed class SqlFilterTranslatorTests
{
    private const string E = "e";

    private static readonly IReadOnlyDictionary<string, SqlM2MJoin> M2M =
        new Dictionary<string, SqlM2MJoin>(StringComparer.OrdinalIgnoreCase)
        {
            ["tags"] = new SqlM2MJoin("FROM \"ET\" AS \"et\" JOIN \"Tags\" AS \"t\" ON \"t\".\"Id\" = \"et\".\"TagId\" WHERE \"e\".\"Id\" = \"et\".\"EId\"")
        };

    private static SqlFilterTranslator T() => new(Map(
        SearchEntity.RelationalRaw(E), 
        Caller(),
        Def(E, "str", FieldKind.String, "\"e\".\"Str\""),
        Def(E, "num", FieldKind.Integer, "\"e\".\"Num\""),
        Def(E, "price", FieldKind.Decimal, "\"e\".\"Price\""),
        Def(E, "flag", FieldKind.Boolean, "\"e\".\"Flag\""),
        Def(E, "when", FieldKind.DateTime, "\"e\".\"When\""),
        Def(E, "gid", FieldKind.Guid, "\"e\".\"Gid\""),
        Def(E, "kind", FieldKind.Enum, "\"e\".\"Kind\""),
        Def(E, "tags", FieldKind.String, "\"t\".\"Name\"", isArray: true),
        Def(E, "jtags", FieldKind.String, "\"e\".\"Data\" -> 'tags'", isArray: true, json: true)),
        M2M);

    private static SqlFilter Tr(FilterNode node) => T().Translate(node);

    // ---------------------------------------------------------------- confronti scalari

    [Theory]
    [InlineData(FilterOperator.Equals, "=")]
    [InlineData(FilterOperator.NotEquals, "<>")]
    [InlineData(FilterOperator.GreaterThan, ">")]
    [InlineData(FilterOperator.GreaterThanOrEqual, ">=")]
    [InlineData(FilterOperator.LessThan, "<")]
    [InlineData(FilterOperator.LessThanOrEqual, "<=")]
    public void Scalar_comparisons_map_to_the_right_operator(FilterOperator op, string symbol)
    {
        var sql = Tr(new ComparisonFilterNode("num", op, 5));

        Assert.Equal($"\"e\".\"Num\" {symbol} @p0", sql.Sql);
        Assert.Equal(5L, sql.Parameters[0]);
    }

    // ---------------------------------------------------------------- null

    [Fact]
    public void Equals_null_and_is_null_both_emit_is_null_without_parameter()
    {
        Assert.Equal("\"e\".\"Str\" IS NULL", Tr(Filter.Eq("str", null)).Sql);
        Assert.Empty(Tr(Filter.Eq("str", null)).Parameters);
        Assert.Equal("\"e\".\"Str\" IS NULL", Tr(Filter.IsNull("str")).Sql);
    }

    [Fact]
    public void NotEquals_null_and_is_not_null_both_emit_is_not_null()
    {
        Assert.Equal("\"e\".\"Str\" IS NOT NULL", Tr(Filter.Ne("str", null)).Sql);
        Assert.Equal("\"e\".\"Str\" IS NOT NULL", Tr(Filter.IsNotNull("str")).Sql);
    }

    // ---------------------------------------------------------------- between / in

    [Fact]
    public void Between_emits_two_parameters()
    {
        var sql = Tr(Filter.Between("price", 10, 20));

        Assert.Equal("\"e\".\"Price\" BETWEEN @p0 AND @p1", sql.Sql);
        Assert.Equal(new object?[] { 10m, 20m }, sql.Parameters);
    }

    [Fact]
    public void In_and_not_in_expand_to_parameter_lists()
    {
        Assert.Equal("\"e\".\"Kind\" IN (@p0, @p1)", Tr(Filter.In("kind", "A", "B")).Sql);
        Assert.Equal("\"e\".\"Kind\" NOT IN (@p0, @p1)", Tr(Filter.NotIn("kind", "A", "B")).Sql);
    }

    // ---------------------------------------------------------------- like family

    [Theory]
    [InlineData(FilterOperator.Contains, "%ab%")]
    [InlineData(FilterOperator.StartsWith, "ab%")]
    [InlineData(FilterOperator.EndsWith, "%ab")]
    public void Like_family_is_case_insensitive_with_escape(FilterOperator op, string expectedTerm)
    {
        var sql = Tr(new ComparisonFilterNode("str", op, "Ab"));

        Assert.Equal("lower(\"e\".\"Str\") LIKE @p0 ESCAPE '\\'", sql.Sql);
        Assert.Equal(expectedTerm, sql.Parameters[0]);
    }

    [Fact]
    public void NotContains_wraps_the_like_in_a_negation()
    {
        var sql = Tr(Filter.NotContains("str", "ab"));

        Assert.Equal("NOT (lower(\"e\".\"Str\") LIKE @p0 ESCAPE '\\')", sql.Sql);
        Assert.Equal("%ab%", sql.Parameters[0]);
    }

    [Fact]
    public void Like_term_escapes_wildcards_and_backslash()
    {
        // Un termine con jolly va cercato ALLA LETTERA: %, _ e \ vengono neutralizzati.
        var sql = Tr(Filter.Contains("str", "50%_\\x"));

        Assert.Equal("%50\\%\\_\\\\x%", sql.Parameters[0]);
    }

    // ---------------------------------------------------------------- coercizione per tipo

    [Fact]
    public void Integer_value_is_coerced_to_long()
    {
        var p = Tr(Filter.Eq("num", "42")).Parameters[0];
        Assert.IsType<long>(p);
        Assert.Equal(42L, p);
    }

    [Fact]
    public void Decimal_value_is_coerced_to_decimal()
    {
        var p = Tr(Filter.Gte("price", "1.5")).Parameters[0];
        Assert.IsType<decimal>(p);
        Assert.Equal(1.5m, p);
    }

    [Fact]
    public void Boolean_value_is_coerced_to_bool()
    {
        Assert.Equal(true, Tr(Filter.Eq("flag", true)).Parameters[0]);
        Assert.Equal(false, Tr(Filter.Eq("flag", "false")).Parameters[0]);
    }

    [Fact]
    public void Guid_value_is_coerced_to_guid()
    {
        var id = "d48cf423-9db0-4458-936c-5ce7e4ff25b9";
        var p = Tr(Filter.Eq("gid", id)).Parameters[0];
        Assert.IsType<Guid>(p);
        Assert.Equal(Guid.Parse(id), p);
    }

    [Fact]
    public void DateTime_value_is_coerced_to_datetime()
    {
        var p = Tr(Filter.Gte("when", "2026-01-15T00:00:00Z")).Parameters[0];
        var dt = Assert.IsType<DateTime>(p);
        Assert.Equal(new DateTime(2026, 1, 15), dt.Date);
    }

    [Fact]
    public void Enum_value_stays_a_string()
    {
        var p = Tr(Filter.Eq("kind", "Active")).Parameters[0];
        Assert.Equal("Active", p);
    }

    // ---------------------------------------------------------------- array M2M (EXISTS su join)

    [Fact]
    public void Array_contains_emits_correlated_exists()
    {
        var sql = Tr(Filter.ArrayContains("tags", "x"));

        Assert.Contains("EXISTS", sql.Sql);
        Assert.Contains("JOIN \"Tags\" AS \"t\"", sql.Sql);
        Assert.Contains("\"t\".\"Name\" = @p0", sql.Sql);
        Assert.Equal(new object?[] { "x" }, sql.Parameters);
    }

    [Fact]
    public void Array_contains_any_uses_in_inside_exists()
    {
        var sql = Tr(Filter.ArrayContainsAny("tags", "x", "y"));

        Assert.Contains("EXISTS", sql.Sql);
        Assert.Contains("\"t\".\"Name\" IN (@p0, @p1)", sql.Sql);
        Assert.Equal(2, sql.Parameters.Count);
    }

    [Fact]
    public void Array_contains_all_ands_one_exists_per_value()
    {
        var sql = Tr(Filter.ArrayContainsAll("tags", "x", "y"));

        Assert.Equal(2, CountOccurrences(sql.Sql, "EXISTS"));
        Assert.Contains(" AND ", sql.Sql);
        Assert.Equal(2, sql.Parameters.Count);
    }

    [Fact]
    public void Array_is_empty_negates_exists_and_not_empty_does_not()
    {
        Assert.StartsWith("NOT ", Tr(Filter.ArrayIsEmpty("tags")).Sql.TrimStart());
        Assert.DoesNotContain("NOT ", Tr(Filter.ArrayNotEmpty("tags")).Sql);
        Assert.Contains("EXISTS", Tr(Filter.ArrayNotEmpty("tags")).Sql);
    }

    // ---------------------------------------------------------------- array JSON (jsonb)

    [Fact]
    public void Json_array_contains_unnests_the_jsonb_column()
    {
        var sql = Tr(Filter.ArrayContains("jtags", "x"));

        Assert.Contains("jsonb_array_elements_text", sql.Sql);
        Assert.Contains("\"e\".\"Data\" -> 'tags'", sql.Sql);
        Assert.Contains("elem = @p0", sql.Sql);
    }

    [Fact]
    public void Json_array_contains_any_uses_in_on_the_unnested_elements()
    {
        var sql = Tr(Filter.ArrayContainsAny("jtags", "x", "y"));

        Assert.Contains("jsonb_array_elements_text", sql.Sql);
        Assert.Contains("elem IN (@p0, @p1)", sql.Sql);
    }

    // ---------------------------------------------------------------- logica annidata

    [Fact]
    public void And_or_not_and_the_shared_parameter_counter()
    {
        var sql = Tr(Filter.And(
            Filter.Or(Filter.Eq("str", "a"), Filter.Eq("str", "b")),
            Filter.Not(Filter.Gt("num", 5))));

        Assert.Equal(
            "((\"e\".\"Str\" = @p0 OR \"e\".\"Str\" = @p1) AND NOT (\"e\".\"Num\" > @p2))",
            sql.Sql);
        Assert.Equal(3, sql.Parameters.Count); // contatore condiviso, senza collisioni
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}

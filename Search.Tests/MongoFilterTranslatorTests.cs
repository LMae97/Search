using MongoDB.Bson;
using Search.Application.Querying.Dynamic;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;
using Search.Infrastructure.Mongo;
using static Search.Tests.TestSupport;

namespace Search.Tests;

/// <summary>
/// Copertura esaustiva del translator Mongo: matrice tipo × operatore. La coercizione del valore
/// determina il BsonType (String/Int64/Decimal128/Boolean/DateTime/ObjectId), poi ogni operatore
/// diventa il documento/operatore Mongo corrispondente.
/// </summary>
public sealed class MongoFilterTranslatorTests
{
    private const string E = "m";

    private static MongoFilterTranslator<BsonDocument> T() => new(Map(
        SearchEntity.Document(E), Caller(),
        Def(E, "str", FieldKind.String, "str"),
        Def(E, "num", FieldKind.Integer, "num"),
        Def(E, "price", FieldKind.Decimal, "price"),
        Def(E, "flag", FieldKind.Boolean, "flag"),
        Def(E, "when", FieldKind.DateTime, "when"),
        Def(E, "gid", FieldKind.Guid, "gid"),
        Def(E, "kind", FieldKind.Enum, "kind"),
        Def(E, "oid", FieldKind.ObjectId, "_id"),
        Def(E, "tags", FieldKind.String, "tags", isArray: true)));

    private static BsonDocument D(FilterNode node) => T().BuildFilterDocument(node);

    // ---------------------------------------------------------------- confronti scalari

    [Fact]
    public void Equals_is_a_plain_field_value()
    {
        var doc = D(Filter.Eq("str", "Acme"));
        Assert.Equal("Acme", doc["str"].AsString); // { str: "Acme" }, senza $eq
    }

    [Theory]
    [InlineData(FilterOperator.NotEquals, "$ne")]
    [InlineData(FilterOperator.GreaterThan, "$gt")]
    [InlineData(FilterOperator.GreaterThanOrEqual, "$gte")]
    [InlineData(FilterOperator.LessThan, "$lt")]
    [InlineData(FilterOperator.LessThanOrEqual, "$lte")]
    public void Scalar_comparisons_use_the_matching_operator(FilterOperator op, string mongoOp)
    {
        var doc = D(new ComparisonFilterNode("num", op, 5));
        Assert.True(doc["num"].AsBsonDocument.Contains(mongoOp));
    }

    [Fact]
    public void Between_produces_gte_and_lte()
    {
        var inner = D(Filter.Between("price", 10, 20))["price"].AsBsonDocument;
        Assert.True(inner.Contains("$gte"));
        Assert.True(inner.Contains("$lte"));
    }

    [Fact]
    public void In_and_not_in_use_dollar_in_nin()
    {
        Assert.Equal(2, D(Filter.In("kind", "a", "b"))["kind"].AsBsonDocument["$in"].AsBsonArray.Count);
        Assert.Equal(2, D(Filter.NotIn("kind", "a", "b"))["kind"].AsBsonDocument["$nin"].AsBsonArray.Count);
    }

    // ---------------------------------------------------------------- null

    [Fact]
    public void IsNull_and_is_not_null()
    {
        Assert.True(D(Filter.IsNull("str"))["str"].AsBsonDocument["$eq"].IsBsonNull);
        Assert.True(D(Filter.IsNotNull("str"))["str"].AsBsonDocument["$ne"].IsBsonNull);
    }

    // ---------------------------------------------------------------- like family (regex)

    [Theory]
    [InlineData(FilterOperator.Contains, "ab")]
    [InlineData(FilterOperator.StartsWith, "^ab")]
    [InlineData(FilterOperator.EndsWith, "ab$")]
    public void Like_family_becomes_case_insensitive_regex(FilterOperator op, string expectedPattern)
    {
        var regex = D(new ComparisonFilterNode("str", op, "ab"))["str"].AsBsonRegularExpression;

        Assert.Equal("i", regex.Options);
        Assert.Contains(expectedPattern, regex.Pattern);
    }

    [Fact]
    public void NotContains_negates_the_regex()
    {
        var doc = D(Filter.NotContains("str", "ab"));
        Assert.True(doc["str"].AsBsonDocument.Contains("$not"));
    }

    // ---------------------------------------------------------------- coercizione tipo → BsonType

    [Fact]
    public void String_becomes_bson_string()
        => Assert.Equal(BsonType.String, D(Filter.Eq("str", "x"))["str"].BsonType);

    [Fact]
    public void Integer_becomes_bson_int64()
        => Assert.Equal(BsonType.Int64, D(Filter.Eq("num", 5))["num"].BsonType);

    [Fact]
    public void Decimal_becomes_bson_decimal128()
        => Assert.Equal(BsonType.Decimal128, D(Filter.Eq("price", "1.5"))["price"].BsonType);

    [Fact]
    public void Boolean_becomes_bson_boolean()
        => Assert.Equal(BsonType.Boolean, D(Filter.Eq("flag", true))["flag"].BsonType);

    [Fact]
    public void DateTime_becomes_bson_datetime()
        => Assert.Equal(BsonType.DateTime, D(Filter.Eq("when", "2026-01-15T10:30:00Z"))["when"].BsonType);

    [Fact]
    public void Guid_becomes_bson_string()
        => Assert.Equal(BsonType.String, D(Filter.Eq("gid", "d48cf423-9db0-4458-936c-5ce7e4ff25b9"))["gid"].BsonType);

    [Fact]
    public void Enum_becomes_bson_string()
        => Assert.Equal(BsonType.String, D(Filter.Eq("kind", "Active"))["kind"].BsonType);

    [Fact]
    public void ObjectId_becomes_bson_object_id()
    {
        var doc = D(Filter.Eq("oid", "6754248f43ad677b83600fad"));
        Assert.Equal(BsonType.ObjectId, doc["_id"].BsonType);
        Assert.Equal("6754248f43ad677b83600fad", doc["_id"].AsObjectId.ToString());
    }

    [Fact]
    public void Invalid_object_id_is_a_client_error()
        => Assert.Throws<ArgumentException>(() => D(Filter.Eq("oid", "not-an-oid")));

    // ---------------------------------------------------------------- array

    [Fact]
    public void Array_contains_matches_an_element_by_equality()
        => Assert.Equal("x", D(Filter.ArrayContains("tags", "x"))["tags"].AsString);

    [Fact]
    public void Array_contains_any_uses_dollar_in()
        => Assert.Equal(2, D(Filter.ArrayContainsAny("tags", "x", "y"))["tags"].AsBsonDocument["$in"].AsBsonArray.Count);

    [Fact]
    public void Array_contains_all_uses_dollar_all()
        => Assert.Equal(2, D(Filter.ArrayContainsAll("tags", "x", "y"))["tags"].AsBsonDocument["$all"].AsBsonArray.Count);

    [Fact]
    public void Array_is_empty_uses_size_zero()
        => Assert.Equal(0, D(Filter.ArrayIsEmpty("tags"))["tags"].AsBsonDocument["$size"].AsInt32);

    [Fact]
    public void Array_not_empty_negates_size_zero()
        => Assert.True(D(Filter.ArrayNotEmpty("tags"))["tags"].AsBsonDocument.Contains("$not"));

    // ---------------------------------------------------------------- logica

    [Fact]
    public void And_or_not_map_to_and_or_nor()
    {
        Assert.True(D(Filter.And(Filter.Eq("str", "a"), Filter.Eq("num", 1))).Contains("$and"));
        Assert.True(D(Filter.Or(Filter.Eq("str", "a"), Filter.Eq("num", 1))).Contains("$or"));
        Assert.True(D(Filter.Not(Filter.Eq("str", "a"))).Contains("$nor"));
    }

    [Fact]
    public void Nested_logical_keeps_the_structure()
    {
        var doc = D(Filter.And(
            Filter.Or(Filter.Eq("str", "a"), Filter.Eq("str", "b")),
            Filter.Gte("num", 5)));

        var and = doc["$and"].AsBsonArray;
        Assert.Equal(2, and.Count);
        Assert.True(and[0].AsBsonDocument.Contains("$or"));
    }
}

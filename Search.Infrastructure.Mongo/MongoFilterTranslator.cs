using MongoDB.Bson;
using MongoDB.Driver;
using Search.Application.Querying.Filters;
using Search.Application.Querying.Linq;
using Search.Application.Querying.Metadata;

namespace Search.Infrastructure.Mongo;

/// <summary>
/// Traduce lo <b>stesso</b> albero di filtri in una query MongoDB (<see cref="FilterDefinition{TDocument}"/>).
/// Riusa contratto, registry e coercizione dei valori dell'Application: cambia solo il "backend".
/// Presuppone che la richiesta sia già stata validata dal <c>SearchRequestValidator</c>.
/// </summary>
public sealed class MongoFilterTranslator<TDocument>
{
    private readonly IEntitySearchMap _map;

    public MongoFilterTranslator(IEntitySearchMap map) => _map = map;

    /// <summary>API di produzione: da usare con <c>collection.Find(filter)</c>.</summary>
    public FilterDefinition<TDocument> Translate(FilterNode node) => BuildFilterDocument(node);

    /// <summary>Come <see cref="Translate"/> ma espone il <see cref="BsonDocument"/> grezzo (utile per test/ispezione).</summary>
    public BsonDocument BuildFilterDocument(FilterNode node) => Build(node);

    /**
     * Build( And )                                           → BuildLogical
     *  ├─ Build( In status ["Paid","Shipped"] )              → BuildComparison
     *  │     path="status"; Op("$in", Array())
     *  │     └─► { "status": { "$in": ["Paid","Shipped"] } }
     *  ├─ Build( Gte total 50 )                              → BuildComparison
     *  │     path="totalAmount.amount"; Op("$gte", Value(50m))
     *  │     └─► { "totalAmount.amount": { "$gte": NumberDecimal("50") } }
     *  └─ Build( Or )                                        → BuildLogical
     *      ├─ Build( Contains customerEmail "example.com" )  → BuildComparison
     *      │     path="customer.email"; Regex(Contains:…)
     *      │     └─► { "customer.email": /example\.com/i }
     *      └─ Build( ArrayContainsAny tags ["priority"] )    → BuildComparison
     *            path="tags"; Op("$in", Array())
     *            └─► { "tags": { "$in": ["priority"] } }
     *            
     * Risultato finale (i pezzi annidati):
     * { "$and": [
     *     { "status": { "$in": ["Paid","Shipped"] } },
     *     { "totalAmount.amount": { "$gte": NumberDecimal("50") } },
     *     { "$or": [
     *         { "customer.email": /example\.com/i },
     *         { "tags": { "$in": ["priority"] } }
     *     ]}
     * ]}
     */
    private BsonDocument Build(FilterNode node) => node switch
    {
        LogicalFilterNode logical => BuildLogical(logical),
        ComparisonFilterNode comparison => BuildComparison(comparison),
        _ => throw new NotSupportedException($"Nodo di filtro non supportato: {node.GetType().Name}.")
    };

    /**
     * Input: un nodo And/Or/Not con i suoi figli.
     * Output: { "$and": [...] }, { "$or": [...] } o { "$nor": [...] }.
     * 
     * Nota: $nor con un solo elemento = NOT dell'intera espressione (a differenza di $not, 
     * che opera solo a livello di singolo campo/operatore).
     */
    private BsonDocument BuildLogical(LogicalFilterNode node)
    {
        var children = new BsonArray(node.Children.Select(Build));
        return node.Operator switch
        {
            LogicalOperator.And => new BsonDocument("$and", children),
            LogicalOperator.Or => new BsonDocument("$or", children),
            LogicalOperator.Not => new BsonDocument("$nor", children),
            _ => throw new NotSupportedException($"Operatore logico non supportato: {node.Operator}.")
        };
    }

    /**
     * Input:  { Field="total", Operator=GreaterThanOrEqual, Values=[50] }.
     * Output: { "totalAmount.amount": { "$gte": NumberDecimal("50") } }.
     * 
     * Helper     | Cosa fa                                            | Esempio
     * -----------|--------------------------------------------------- |---------------------------------------------------------
     * Value(raw) | Coercizione al tipo del campo + conversione a BSON | Value(50) → BsonDecimal128(50)
     * Array()    | Applica Value() a tutti i valori                   | ["Paid", "Shipped"] → BsonArray di 2 BsonString
     * Op(op, v)  | Impacchetta { path: { op: v } }                    | Op("$gte", …) → { "totalAmount.amount": { "$gte": … } }
     */
    private BsonDocument BuildComparison(ComparisonFilterNode node)
    {
        if (!_map.TryGetField(node.Field, out var field))
            throw new InvalidOperationException($"Campo '{node.Field}' non mappato (validare prima di tradurre).");

        var path = MongoFieldPath.From(field.Selector);

        BsonValue Value(object? raw) => ToBson(ValueCoercion.Coerce(raw, field.ClrType));
        BsonArray Array() => new(node.Values.Select(Value));
        BsonDocument Op(string @operator, BsonValue value) => new(path, new BsonDocument(@operator, value));

        return node.Operator switch
        {
            FilterOperator.Equals => new BsonDocument(path, Value(node.SingleValue)),
            FilterOperator.NotEquals => Op("$ne", Value(node.SingleValue)),

            FilterOperator.GreaterThan => Op("$gt", Value(node.SingleValue)),
            FilterOperator.GreaterThanOrEqual => Op("$gte", Value(node.SingleValue)),
            FilterOperator.LessThan => Op("$lt", Value(node.SingleValue)),
            FilterOperator.LessThanOrEqual => Op("$lte", Value(node.SingleValue)),
            FilterOperator.Between => new BsonDocument(path,
                new BsonDocument { { "$gte", Value(node.Values[0]) }, { "$lte", Value(node.Values[1]) } }),

            FilterOperator.In => Op("$in", Array()),
            FilterOperator.NotIn => Op("$nin", Array()),

            // Contains/StartsWith/EndsWith via regex: case-insensitive di default ("i").
            // È qui che la scelta sulla case-sensitivity diventa esplicita per Mongo.
            FilterOperator.Contains => new BsonDocument(path, Regex(Contains: node.SingleValue)),
            FilterOperator.NotContains => new BsonDocument(path, new BsonDocument("$not", Regex(Contains: node.SingleValue))),
            FilterOperator.StartsWith => new BsonDocument(path, Regex(Prefix: node.SingleValue)),
            FilterOperator.EndsWith => new BsonDocument(path, Regex(Suffix: node.SingleValue)),

            FilterOperator.IsNull => Op("$eq", BsonNull.Value),
            FilterOperator.IsNotNull => Op("$ne", BsonNull.Value),

            // Su un campo array, l'uguaglianza semplice matcha se un elemento è uguale al valore.
            FilterOperator.ArrayContains => new BsonDocument(path, Value(node.SingleValue)),
            FilterOperator.ArrayContainsAny => Op("$in", Array()),
            FilterOperator.ArrayContainsAll => Op("$all", Array()),
            FilterOperator.ArrayIsEmpty => Op("$size", 0),
            FilterOperator.ArrayNotEmpty => new BsonDocument(path, new BsonDocument("$not", new BsonDocument("$size", 0))),

            _ => throw new NotSupportedException($"Operatore {node.Operator} non supportato per Mongo.")
        };
    }

    /**
     * System.Text.RegularExpressions.Regex.Escape(...) mette il backslash davanti ai caratteri speciali, 
     * così un . cerca un punto letterale e non "qualsiasi carattere".
     */
    private static BsonRegularExpression Regex(object? Contains = null, object? Prefix = null, object? Suffix = null)
    {
        if (Contains is not null)
            return new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(Contains.ToString()!), "i");
        if (Prefix is not null)
            return new BsonRegularExpression("^" + System.Text.RegularExpressions.Regex.Escape(Prefix.ToString()!), "i");
        return new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(Suffix!.ToString()!) + "$", "i");
    }

    /**
     * Input: un valore CLR già coerciato (string, decimal, enum, DateTimeOffset…). 
     * Output: il corrispondente tipo BSON.
     * "Paid"                    → BsonString("Paid")
     * 50m                       → BsonDecimal128(50)
     * OrderStatus.Paid (enum)   → BsonString("Paid")     ← assunzione: enum salvato come stringa
     * DateTimeOffset            → BsonDateTime(UTC)
     * null                      → BsonNull.Value
     */
    private static BsonValue ToBson(object? value) => value switch
    {
        null => BsonNull.Value,
        string s => new BsonString(s),
        bool b => BsonBoolean.Create(b),
        int i => new BsonInt32(i),
        long l => new BsonInt64(l),
        decimal d => new BsonDecimal128(d),
        double db => new BsonDouble(db),
        float f => new BsonDouble(f),
        Guid g => new BsonString(g.ToString()),
        DateTimeOffset dto => new BsonDateTime(dto.UtcDateTime),
        DateTime dt => new BsonDateTime(dt),
        Enum e => new BsonString(e.ToString()),
        _ => BsonValue.Create(value)
    };
}

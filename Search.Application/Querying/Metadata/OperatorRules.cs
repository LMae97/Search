using Search.Application.Querying.Filters;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Regole tipo→operatore: dato il <see cref="FieldKind"/> e l'essere array, quali operatori
/// hanno senso di default. Centralizzando qui la regola, la coerenza è garantita per tutte le entità.
/// Un mapping può comunque restringere/ampliare esplicitamente per un singolo campo.
/// </summary>
public static class OperatorRules
{
    public static IReadOnlySet<FilterOperator> DefaultFor(FieldKind kind, bool isArray)
    {
        if (isArray)
        {
            return new HashSet<FilterOperator>
            {
                FilterOperator.ArrayContains,
                FilterOperator.ArrayContainsAny,
                FilterOperator.ArrayContainsAll,
                FilterOperator.ArrayIsEmpty,
                FilterOperator.ArrayNotEmpty
            };
        }

        // Comune agli scalari: uguaglianza, insiemi, nullità.
        var operators = new HashSet<FilterOperator>
        {
            FilterOperator.Equals,
            FilterOperator.NotEquals,
            FilterOperator.In,
            FilterOperator.NotIn,
            FilterOperator.IsNull,
            FilterOperator.IsNotNull
        };

        switch (kind)
        {
            case FieldKind.String:
                operators.UnionWith(new[]
                {
                    FilterOperator.Contains,
                    FilterOperator.NotContains,
                    FilterOperator.StartsWith,
                    FilterOperator.EndsWith
                });
                break;

            case FieldKind.Integer:
            case FieldKind.Decimal:
            case FieldKind.DateTime:
                operators.UnionWith(new[]
                {
                    FilterOperator.GreaterThan,
                    FilterOperator.GreaterThanOrEqual,
                    FilterOperator.LessThan,
                    FilterOperator.LessThanOrEqual,
                    FilterOperator.Between
                });
                break;

            case FieldKind.Boolean:
                // Solo uguaglianza/nullità: gt/contains/in non hanno senso su un bool.
                operators.RemoveWhere(op => op is FilterOperator.In or FilterOperator.NotIn);
                break;

            case FieldKind.Guid:
            case FieldKind.Enum:
            case FieldKind.ObjectId:
                // Uguaglianza + insiemi + nullità: già inclusi.
                break;
        }

        return operators;
    }
}

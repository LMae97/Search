using Search.Application.Querying.Filters;
using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Validation;

/// <summary>
/// Valida una <see cref="SearchRequest"/> contro la mappa dell'entità, PRIMA della traduzione.
/// È la barriera di sicurezza: nessun campo/operatore non dichiarato arriva mai al DB.
/// </summary>
public sealed class SearchRequestValidator
{
    private readonly IEntitySearchMap _map;

    public SearchRequestValidator(IEntitySearchMap map) => _map = map;

    /// <summary>Lancia <see cref="SearchValidationException"/> se la richiesta non è valida.</summary>
    public void Validate(SearchRequest request)
    {
        var errors = new List<string>();

        if (request.Filter is not null)
            ValidateNode(request.Filter, errors);

        foreach (var field in request.Projection)
            if (!_map.TryGetField(field, out _))
                errors.Add($"Proiezione: campo sconosciuto '{field}'.");

        foreach (var sort in request.Sort)
        {
            if (!_map.TryGetField(sort.Field, out var descriptor))
                errors.Add($"Ordinamento: campo sconosciuto '{sort.Field}'.");
            else if (descriptor.IsArray)
                errors.Add($"Ordinamento: non ammesso su campo array '{sort.Field}'.");
        }

        if (request.Page.Number < 1)
            errors.Add("Paginazione: il numero di pagina deve essere >= 1.");
        if (request.Page.Size is < 1 or > 200)
            errors.Add("Paginazione: la dimensione pagina deve essere tra 1 e 200.");

        if (errors.Count > 0)
            throw new SearchValidationException(errors);
    }

    private void ValidateNode(FilterNode node, List<string> errors)
    {
        switch (node)
        {
            case LogicalFilterNode logical:
                if (logical.Operator == LogicalOperator.Not && logical.Children.Count != 1)
                    errors.Add("NOT richiede esattamente un figlio.");
                if (logical.Operator != LogicalOperator.Not && logical.Children.Count == 0)
                    errors.Add($"{logical.Operator} richiede almeno un figlio.");
                foreach (var child in logical.Children)
                    ValidateNode(child, errors);
                break;

            case ComparisonFilterNode comparison:
                ValidateComparison(comparison, errors);
                break;

            default:
                errors.Add($"Nodo di filtro non supportato: {node.GetType().Name}.");
                break;
        }
    }

    private void ValidateComparison(ComparisonFilterNode node, List<string> errors)
    {
        if (!_map.TryGetField(node.Field, out var field))
        {
            errors.Add($"Campo sconosciuto '{node.Field}'.");
            return;
        }

        if (!field.Supports(node.Operator))
        {
            var arrayMark = field.IsArray ? "[]" : string.Empty;
            errors.Add($"Operatore {node.Operator} non ammesso per '{node.Field}' (tipo {field.Kind}{arrayMark}).");
            return;
        }

        var arity = ExpectedArity(node.Operator);
        if (!arity.IsSatisfiedBy(node.Values.Count))
            errors.Add($"Operatore {node.Operator} su '{node.Field}': attesi {arity.Describe()} valori, ricevuti {node.Values.Count}.");
    }

    private static Arity ExpectedArity(FilterOperator @operator) => @operator switch
    {
        FilterOperator.IsNull or FilterOperator.IsNotNull
            or FilterOperator.ArrayIsEmpty or FilterOperator.ArrayNotEmpty => Arity.Exactly(0),
        FilterOperator.Between => Arity.Exactly(2),
        FilterOperator.In or FilterOperator.NotIn
            or FilterOperator.ArrayContainsAny or FilterOperator.ArrayContainsAll => Arity.AtLeast(1),
        _ => Arity.Exactly(1)
    };

    private readonly record struct Arity(int Min, int? Max)
    {
        public static Arity Exactly(int n) => new(n, n);
        public static Arity AtLeast(int n) => new(n, null);

        public bool IsSatisfiedBy(int count) => count >= Min && (Max is null || count <= Max);

        public string Describe() => Max switch
        {
            null => $">= {Min}",
            _ when Min == Max => $"{Min}",
            _ => $"{Min}..{Max}"
        };
    }
}

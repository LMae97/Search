namespace Search.Application.Querying.Filters;

/// <summary>
/// Nodo dell'albero di filtri. È la rappresentazione interna, store-agnostic:
/// il DTO JSON che arriva dal FE viene deserializzato in quest'albero, che poi
/// ogni translator trasforma nel linguaggio del proprio store.
/// </summary>
public abstract class FilterNode;

/// <summary>Operatore logico che combina altri nodi.</summary>
public enum LogicalOperator
{
    And,
    Or,
    Not
}

/// <summary>Nodo composito: AND/OR su N figli, NOT su un figlio.</summary>
public sealed class LogicalFilterNode : FilterNode
{
    public LogicalOperator Operator { get; }
    public IReadOnlyList<FilterNode> Children { get; }

    // Privato di proposito: l'unico modo per costruire un nodo è passare dalle factory qui sotto.
    // Così è impossibile creare un NOT con 2 figli (Not accetta un solo child) o un operatore incoerente.
    private LogicalFilterNode(LogicalOperator @operator, IReadOnlyList<FilterNode> children)
    {
        Operator = @operator;
        Children = children;
    }

    public static LogicalFilterNode And(params FilterNode[] children) => new(LogicalOperator.And, children);
    public static LogicalFilterNode Or(params FilterNode[] children) => new(LogicalOperator.Or, children);
    public static LogicalFilterNode Not(FilterNode child) => new(LogicalOperator.Not, new[] { child });
}

/// <summary>Nodo foglia: un confronto su un singolo campo.</summary>
public sealed class ComparisonFilterNode : FilterNode
{
    public string Field { get; }
    public FilterOperator Operator { get; }

    /// <summary>
    /// Valori dell'operatore. La cardinalità dipende dall'operatore
    /// (0 per IsNull, 1 per Equals, 2 per Between, N per In). La validazione la verifica.
    /// </summary>
    public IReadOnlyList<object?> Values { get; }

    public ComparisonFilterNode(string field, FilterOperator op, params object?[] values)
    {
        Field = field;
        Operator = op;
        Values = values ?? [];
    }

    /// <summary>Primo valore, per gli operatori mono-valore.</summary>
    public object? SingleValue => Values.Count > 0 ? Values[0] : null;
}

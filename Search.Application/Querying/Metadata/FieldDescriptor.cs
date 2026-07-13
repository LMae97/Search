using System.Linq.Expressions;
using Search.Application.Querying.Filters;

namespace Search.Application.Querying.Metadata;

/// <summary>
/// Descrittore di un campo ricercabile: nome pubblico, categoria di tipo, se è array,
/// il tipo CLR (dello scalare o dell'elemento se array), il selettore verso la proprietà
/// e l'insieme di operatori ammessi.
/// </summary>
public sealed class FieldDescriptor
{
    /// <summary>Nome pubblico esposto al FE (parte del contratto, es. "price").</summary>
    public string Name { get; }

    public FieldKind Kind { get; }

    public bool IsArray { get; }

    /// <summary>Tipo CLR dello scalare; se <see cref="IsArray"/>, tipo dell'elemento. Nullable già scartato.</summary>
    public Type ClrType { get; }

    /// <summary>
    /// Selettore verso la proprietà: TEntity -> valore scalare oppure TEntity -> IEnumerable<Element>.
    /// È il ponte tra il nome pubblico del campo e la proprietà reale dell'entità:
    /// nessuna stringa "grezza" viene mai utilizzata direttamente verso il database.
    /// </summary>
    public LambdaExpression Selector { get; }

    public IReadOnlySet<FilterOperator> AllowedOperators { get; }

    public FieldDescriptor(
        string name,
        FieldKind kind,
        bool isArray,
        Type clrType,
        LambdaExpression selector,
        IReadOnlySet<FilterOperator> allowedOperators)
    {
        Name = name;
        Kind = kind;
        IsArray = isArray;
        ClrType = clrType;
        Selector = selector;
        AllowedOperators = allowedOperators;
    }

    public bool Supports(FilterOperator op) => AllowedOperators.Contains(op);
}

using System.Linq.Expressions;

namespace Search.Infrastructure.Mongo;

/// <summary>
/// Ricava il path del campo nel documento Mongo dal selettore del registry:
/// <c>o => o.Customer.Email</c> ⟶ <c>"customer.email"</c>.
/// Assume la convenzione camelCase; in un progetto reale rispetteremmo le convenzioni
/// registrate nel driver (BsonClassMap / attributi <c>[BsonElement]</c>), eventualmente
/// permettendo un override esplicito del path in fase di mappatura.
/// </summary>
internal static class MongoFieldPath
{
    /**
     * Scopo: trasformare un LambdaExpression nella stringa-path che Mongo usa per 
     * indirizzare un campo.
     * 
     * Input: un selettore, es. o => o.Customer.Email.
     * Output: "customer.email".
     * 
     * passo   | expression è…    | azione                        | segments
     * inizio  | Member(Email)    |	—                             |	[]
     * iter 1  | Member(Email)	  | add "email", scendi           |	["email"]
     * iter 2  | Member(Customer) | add "customer", scendi        |	["email","customer"]
     * iter 3  | Parameter(o)	  | non è Member → esci dal while |	["email","customer"]
     * check   | Parameter(o)	  | è ParameterExpression ✓	      | 
     * reverse |	              | inverti	                      | ["customer","email"]
     * join    |	              | "customer.email"              |	
     */
    public static string From(LambdaExpression selector)
    {
        var segments = new List<string>();
        var expression = Unwrap(selector.Body);

        while (expression is MemberExpression member)
        {
            segments.Add(CamelCase(member.Member.Name));
            expression = Unwrap(member.Expression);
        }

        if (expression is not ParameterExpression)
            throw new NotSupportedException($"Selettore non traducibile in un path Mongo: {selector}.");

        segments.Reverse();
        return string.Join('.', segments);
    }

    /**
     * Rimuove le conversioni implicite (es. IReadOnlyCollection<T> -> IEnumerable<T> nei campi array).
     * 
     * Input: un'espressione che potrebbe essere avvolta in uno o più cast.
     * Output: la stessa espressione senza i cast.
     * 
     * Perché serve. Nei campi array il selettore o => o.Tags ha tipo di ritorno IEnumerable<string>, 
     * ma Tags è dichiarato IReadOnlyCollection<string>
     */
    private static Expression? Unwrap(Expression? expression)
    {
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            expression = unary.Operand;
        return expression;
    }

    private static string CamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}

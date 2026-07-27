using System.Linq.Expressions;

namespace Search.Application.Querying.Linq;

/// <summary>
/// Sostituisce il parametro di una lambda con un'altra espressione. Serve a "ribasare" i
/// selettori dei campi (ognuno ha il proprio parametro <c>p</c>) sul parametro unico <c>x</c>
/// dell'albero di predicati che stiamo costruendo.
/// </summary>
internal sealed class ParameterReplacer : ExpressionVisitor
{
    private readonly ParameterExpression _from;
    private readonly Expression _to;

    private ParameterReplacer(ParameterExpression from, Expression to)
    {
        _from = from;
        _to = to;
    }

    protected override Expression VisitParameter(ParameterExpression node)
        => node == _from ? _to : base.VisitParameter(node);

    /// <summary>Riscrive il corpo di <paramref name="lambda"/> usando <paramref name="parameter"/>.</summary>
    public static Expression Rebase(LambdaExpression lambda, ParameterExpression parameter)
        => new ParameterReplacer(lambda.Parameters[0], parameter).Visit(lambda.Body);
}

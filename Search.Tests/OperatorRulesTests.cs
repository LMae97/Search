using WeByte.Search.Core.Filters;
using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Tests;

public class OperatorRulesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Custom_fields_allow_no_operator_at_all(bool isArray)
    {
        // Un pulsante/immagine della UI non è un dato filtrabile: nemmeno Equals o IsNull hanno senso
        // ("filtra dove il bottone è nullo" non significa niente), a prescindere da isArray.
        var operators = OperatorRules.DefaultFor(FieldKind.Custom, isArray);

        Assert.Empty(operators);
    }

    [Fact]
    public void String_fields_still_get_the_usual_operators()
    {
        // Regressione: l'early-return per Custom non deve intaccare gli altri kind.
        var operators = OperatorRules.DefaultFor(FieldKind.String, isArray: false);

        Assert.Contains(FilterOperator.Contains, operators);
        Assert.Contains(FilterOperator.Equals, operators);
    }
}

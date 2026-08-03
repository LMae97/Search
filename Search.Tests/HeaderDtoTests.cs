using WeByte.Search.Application.Dtos;
using WeByte.Search.Core.Dynamic;
using WeByte.Search.Core.Metadata;
using static WeByte.Search.Tests.TestSupport;

namespace WeByte.Search.Tests;

/// <summary>
/// <see cref="HeaderDto.FromDescriptor"/>: per un campo <see cref="FieldKind.Custom"/> il FE deve ricevere
/// in <c>Type</c> il widget specifico (es. "BtnImpersonateUser"), non la generica etichetta "Custom" — è
/// così che sceglie quale componente renderizzare, esattamente come faceva il vecchio sistema con un Guid
/// per tipo di pulsante.
/// </summary>
public class HeaderDtoTests
{
    private const string E = "fake";

    private static FieldDescriptor Field(SearchFieldDefinition def)
        => Map(SearchEntity.RelationalRaw(E), Caller(), def).Fields[def.Name];

    [Fact]
    public void Custom_field_reports_its_specific_widget_type()
    {
        var field = Field(Def(E, "btnImpersonateUser", FieldKind.Custom, "id", customType: "BtnImpersonateUser"));

        var header = HeaderDto.FromDescriptor(field);

        Assert.Equal("BtnImpersonateUser", header.Type);
    }

    [Fact]
    public void Custom_field_without_a_specific_type_falls_back_to_Custom()
    {
        var field = Field(Def(E, "btn", FieldKind.Custom, "id")); // nessun customType impostato

        var header = HeaderDto.FromDescriptor(field);

        Assert.Equal("Custom", header.Type);
    }

    [Fact]
    public void Non_custom_fields_ignore_custom_type_and_report_their_own_kind()
    {
        // CustomType impostato per errore su un campo non-Custom: non deve mai comparire al posto del Kind vero.
        var field = Field(Def(E, "name", FieldKind.String, "name", customType: "BtnImpersonateUser"));

        var header = HeaderDto.FromDescriptor(field);

        Assert.Equal("String", header.Type);
    }

    [Fact]
    public void Regular_kinds_still_report_their_own_name()
    {
        var field = Field(Def(E, "price", FieldKind.Decimal, "price"));

        Assert.Equal("Decimal", HeaderDto.FromDescriptor(field).Type);
    }
}

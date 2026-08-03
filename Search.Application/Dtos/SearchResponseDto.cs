using WeByte.Search.Core.Metadata;

namespace WeByte.Search.Application.Dtos;

public class SearchResponseDto
{
    public List<HeaderDto> Header { get; init; } = [];
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Body { get; init; } = [];
    public long? ResultCount { get; init; }
}

public class HeaderDto
{
    public string Id { get; init; }
    public string Key { get; init; }
    public string Label { get; init; }
    public string Type { get; init; }
    public bool Visible { get; init; }

    public static HeaderDto FromDescriptor(FieldDescriptor field) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Key = field.Name,
        Label = field.Label,
        Type = AdaptType(field.Kind, field.IsArray, field.CustomType),
        Visible = !field.IsHidden
    };

    // Per un campo Custom, il FE deve sapere QUALE widget (bottone/immagine), non solo "è custom": se
    // CustomType è valorizzato (es. "BtnImpersonateUser") sostituisce il nome del Kind. Fallback su "Custom"
    // per i campi Custom senza CustomType impostato — non deve mai risultare null nell'header.
    private static string AdaptType(FieldKind kind, bool isArray, string? customType)
    {
        var name = kind == FieldKind.Custom ? customType ?? kind.ToString() : kind.ToString();
        return isArray ? name + "[]" : name;
    }
}

public class SearchResponseAdapter
{
    public static SearchResponseDto ToSearchResponseDto(
        IEnumerable<FieldDescriptor> projection,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> result,
        long? count)
    {
        return new SearchResponseDto
        {
            Header = [.. projection.Select(x => HeaderDto.FromDescriptor(x))],
            Body = result,
            ResultCount = count
        };
    }
}
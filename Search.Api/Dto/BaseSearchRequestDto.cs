namespace WeByte.Search.Api.Dto;

/// <summary>
/// Contratto base della richiesta di ricerca lato FE, comune a ogni entità. Le entità specifiche
/// (es. contratti, utenti) ereditano da questa classe aggiungendo le proprie proprietà tipizzate
/// (vedi <c>SearchContractRequestDto</c>), che un adapter dedicato traduce in filtri aggiuntivi.
/// </summary>
public class BaseSearchRequestDto
{
    public string? Search { get; set; }
    public OptionsDto? Options { get; set; }
}

public class OptionsDto
{
    public int? Page { get; set; } = null;
    public int? PageSize { get; set; } = null;

    /// <summary>AND di OR: la lista esterna è in AND, ogni lista interna è in OR.</summary>
    public List<List<FilterDto>>? Filters { get; set; } = null;
    public List<string>? Columns { get; set; } = null;
    public List<SortingDto>? SortBy { get; set; } = null;
}

public class FilterDto
{
    /// <summary>Solo per il FE (es. per identificare la riga nella UI): il backend lo ignora.</summary>
    public string? Key { get; set; }
    public string Field { get; set; } = "";

    /// <summary>
    /// Valore del filtro. Per gli operatori multi-valore (in/nin/between/containsAny/containsAll) è una
    /// stringa con i valori separati da virgola — stessa convenzione del vecchio traduttore, che univa
    /// più valori con <c>Aggregate((a,b) =&gt; a + "," + b)</c>. Se il FE reale userà un array JSON invece
    /// di una stringa CSV, l'unico punto da cambiare è <see cref="BaseSearchRequestDtoAdapter"/>.
    /// </summary>
    public string? Value { get; set; } = null;
    public string Operation { get; set; } = "";
}

public class SortingDto
{
    public string Field { get; set; } = "";

    /// <summary>"asc"/"desc" (case-insensitive); qualunque altro valore è trattato come "asc".</summary>
    public string Direction { get; set; } = "asc";
}

/// <summary>Range di date (solo giorno, senza ora) per i filtri "da/a" tipici della UI.</summary>
public sealed record DateOnlyRangeDto(DateOnly? From, DateOnly? To);

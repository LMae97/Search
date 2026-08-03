using System.Linq.Expressions;
using WeByte.Search.Core.Filters;

namespace WeByte.Search.Core.Metadata;

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

    public bool JsonColumn { get; }

    public bool IsArray { get; }

    /// <summary>Tipo CLR dello scalare; se <see cref="IsArray"/>, tipo dell'elemento. Nullable già scartato.</summary>
    public Type ClrType { get; }

    /// <summary>
    /// Selettore verso la proprietà: TEntity -> valore scalare oppure TEntity -> IEnumerable&lt;Element&gt;.
    /// È il ponte tra nome pubblico e proprietà reale (niente stringhe grezze verso il DB).
    /// <b>Null per i campi dinamici</b>, che non hanno una proprietà CLR e usano <see cref="StoragePath"/>.
    /// </summary>
    public LambdaExpression? Selector { get; init; }

    /// <summary>
    /// Path esplicito nello store per i campi <b>dinamici</b> (es. "attributes.zonaConsegna" su Mongo).
    /// Null per i campi statici, dove il path si deriva dal <see cref="Selector"/>.
    /// </summary>
    public string? StoragePath { get; init; }

    public string ResponseId { get; }

    public IReadOnlySet<FilterOperator> AllowedOperators { get; }

    // --- Metadati di presentazione / autorizzazione (decorano il campo tecnico) ---

    /// <summary>Etichetta per il FE. Default = <see cref="Name"/>.</summary>
    public string Label { get; } = string.Empty;

    /// <summary>Sezione/gruppo per la UI (opzionale).</summary>
    public string? Section { get;}

    /// <summary>Se il campo è mostrato come fallback in tabella. È presentazione, NON autorizzazione.</summary>
    public int? DefaultOrder { get; }

    /// <summary>Se il campo è nascosto (non proiettabile). È presentazione, NON autorizzazione.</summary>
    public bool IsHidden { get; }

    /// <summary>
    /// Permesso richiesto per vedere/filtrare/ordinare il campo. Null = nessun permesso richiesto.
    /// Se l'utente non lo possiede, il campo sparisce dalla <c>EffectiveSearchMap</c>.
    /// </summary>
    public Guid? RequiredPermissionId { get; }

    public string? LinkReferencePath { get; }
    public string? LinkReferenceEntityId { get; }

    /// <summary>
    /// Path di un secondo valore da comporre insieme a <see cref="StoragePath"/> in un'unica proiezione
    /// <c>{ value, &lt;SecondaryResponseKey&gt; }</c> — stesso principio del composto <c>{value,label}</c>
    /// di <see cref="FieldKind.Link"/>, ma con una chiave arbitraria invece di "label". Serve ai campi
    /// <see cref="FieldKind.Custom"/> che portano un dato di corredo insieme al valore principale (es. il
    /// vecchio pulsante "scarica tutti gli allegati", che tornava anche se il contratto fosse già stampato).
    /// Null = proiezione singola, il caso comune.
    /// </summary>
    public string? SecondaryStoragePath { get; init; }

    /// <summary>Nome della chiave del valore secondario nell'oggetto proiettato (es. "isPrinted").</summary>
    public string? SecondaryResponseKey { get; init; }

    /// <summary>
    /// Identificatore del widget specifico per un campo <see cref="FieldKind.Custom"/> (es.
    /// "BtnImpersonateUser", "BtnDuplicateProduct", "Image") — quello che il FE legge in
    /// <c>HeaderDto.Type</c> per scegliere quale componente renderizzare. Non è un <see cref="FieldKind"/>
    /// a sé (il motore tratta tutti i Custom allo stesso modo: zero operatori, mai esportabili — vedi
    /// <see cref="FieldKind.Custom"/>), è solo l'etichetta che il vecchio sistema portava con un Guid per
    /// tipo di pulsante. Null per ogni campo non-Custom.
    /// </summary>
    public string? CustomType { get; init; }

    /// <summary>
    /// Se il campo partecipa alla ricerca full-text libera (<c>SearchRequest.Search</c>). È metadato
    /// store-agnostic: ogni store decide come usarlo (Mongo → path di un <c>$search</c> Atlas; SQL → colonna
    /// di un <c>ILIKE</c>). Non c'entra con gli operatori del filtro puntuale.
    /// </summary>
    public bool IsSearchable { get; init; }

    private FieldDescriptor(
        string name,
        string responseId,
        FieldKind kind,
        bool isArray,
        Type clrType,
        bool jsonColumn,
        string? linkReferencePath,
        string? linkReferenceEntityId,
        string? label,
        string? section,
        int? defaultOrder,
        bool isHidden,
        Guid? requiredPermissionId,
        IReadOnlySet<FilterOperator> allowedOperators)
    {
        Name = name;
        ResponseId = responseId;
        Kind = kind;
        IsArray = isArray;
        ClrType = clrType;
        JsonColumn = jsonColumn;
        LinkReferencePath = linkReferencePath;
        LinkReferenceEntityId = linkReferenceEntityId;
        Label = label ?? name;
        Section = section;
        DefaultOrder = defaultOrder;
        IsHidden = isHidden;
        RequiredPermissionId = requiredPermissionId;
        AllowedOperators = allowedOperators;
    }

    public static FieldDescriptor BuildPathBased(
        string storagePath,
        string name,
        string responseId,
        FieldKind kind,
        bool isArray,
        Type clrType,
        bool jsonColumn,
        string? linkReferencePath,
        string? linkReferenceEntityId,
        string? label,
        string? section,
        int? defaultOrder,
        bool isHidden,
        Guid? requiredPermissionId,
        IReadOnlySet<FilterOperator> allowedOperators,
        bool isSearchable = false,
        string? secondaryStoragePath = null,
        string? secondaryResponseKey = null,
        string? customType = null)
    {
        return new FieldDescriptor(name, responseId, kind, isArray, clrType, jsonColumn,
            linkReferencePath, linkReferenceEntityId, label, section,
            defaultOrder, isHidden, requiredPermissionId, allowedOperators)
        {
            StoragePath = storagePath,
            IsSearchable = isSearchable,
            SecondaryStoragePath = secondaryStoragePath,
            SecondaryResponseKey = secondaryResponseKey,
            CustomType = customType
        };
    }

    public static FieldDescriptor BuildSelectorBased(
        LambdaExpression selector,
        string name,
        string responseId,
        FieldKind kind,
        bool isArray,
        Type clrType,
        bool jsonColumn,
        string? linkReferencePath,
        string? linkReferenceEntityId,
        string? label,
        string? section,
        int? defaultOrder,
        bool isHidden,
        Guid? requiredPermissionId,
        IReadOnlySet<FilterOperator> allowedOperators,
        bool isSearchable = false)
    {
        return new FieldDescriptor(name, responseId, kind, isArray, clrType, jsonColumn,
            linkReferencePath, linkReferenceEntityId, label, section,
            defaultOrder, isHidden, requiredPermissionId, allowedOperators)
        {
            Selector = selector,
            IsSearchable = isSearchable
        };
    }

    public bool Supports(FilterOperator op) => AllowedOperators.Contains(op);
}

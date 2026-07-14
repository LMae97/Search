namespace Search.Api.Contracts;

// --- Value object condivisi ---
public sealed record DimensionsDto(decimal LengthMm, decimal WidthMm, decimal HeightMm);
public sealed record MoneyDto(decimal Amount, string Currency);

// --- Richieste ---
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    Guid BrandId,
    decimal Price,
    string Currency,
    string? Description = null,
    string? Category = null,
    DimensionsDto? Dimensions = null,
    decimal? WeightInGrams = null,
    string[]? Tags = null);

/// <summary>Aggiornamento delle info generiche (PUT = sostituzione). Lo stato NON è qui.</summary>
public sealed record UpdateProductDetailsRequest(
    string Name,
    decimal Price,
    string Currency,
    string? Description = null,
    string? Category = null,
    DimensionsDto? Dimensions = null,
    decimal? WeightInGrams = null);

/// <summary>Lo stato si cambia per <b>azione</b> (rispetta le invarianti), non impostando un valore libero.</summary>
public enum ProductStatusAction
{
    Publish,
    Discontinue
}

public sealed record ChangeProductStatusRequest(ProductStatusAction Action);

// --- Risposte ---
public sealed record ProductListItemDto(Guid Id, string Code, string Label);

public sealed record ProductDetailsDto(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid BrandId,
    MoneyDto Price,
    int Stock,
    string Status,
    string? Category,
    DimensionsDto? Dimensions,
    decimal? WeightInGrams,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Barcodes,
    AuditDto Audit);

public sealed record AuditDto(
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedBy,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    string? DeletedBy);

using Microsoft.AspNetCore.Mvc;
using Search.Api.Contracts;
using Search.Application.Catalog;
using Search.Application.Querying;
using Search.Application.Querying.Authorization;
using Search.Application.Querying.Dynamic;
using Search.Domain.Catalog.Products;
using Search.Domain.Catalog.Products.ValueObjects;
using Search.Domain.Catalog.Tags;
using Search.Domain.Common.ValueObjects;

namespace Search.Api.Controllers;

[ApiController]
[Route("products")]
public sealed class ProductsController : ControllerBase
{
    // Utente di audit fittizio finché non c'è l'autenticazione reale.
    private const string AuditUser = "api@we-byte.it";

    private readonly IProductRepository _repository;
    private readonly ISearchService _search;

    public ProductsController(IProductRepository repository, ISearchService search)
    {
        _repository = repository;
        _search = search;
    }

    /// <summary>Crea un prodotto (accetta dimensioni e tag).</summary>
    [HttpPost]
    public IActionResult Create(CreateProductRequest request)
    {
        var product = Product.Create(
            Sku.Create(request.Sku),
            request.Name,
            request.BrandId,
            Money.Of(request.Price, request.Currency),
            request.Description,
            request.Category,
            ProductMappings.ToDomain(request.Dimensions),
            request.WeightInGrams);

        foreach (var tag in request.Tags ?? [])
            product.AddTag(Tag.Create(tag));

        product.ApplyCreationAudit(AuditUser, DateTimeOffset.UtcNow);
        _repository.Add(product);

        return CreatedAtAction(nameof(Details), new { id = product.Id }, ProductMappings.ToDetails(product));
    }

    /// <summary>Aggiorna le info generiche (PUT = sostituzione). Lo stato NON si tocca qui.</summary>
    [HttpPut("{id:guid}/details")]
    public IActionResult UpdateDetails(Guid id, UpdateProductDetailsRequest request)
    {
        var product = _repository.Get(id);
        if (product is null)
            return NotFound();

        product.Rename(request.Name);
        product.ChangePrice(Money.Of(request.Price, request.Currency));
        product.UpdateDetails(request.Description, request.Category, ProductMappings.ToDomain(request.Dimensions), request.WeightInGrams);
        product.ApplyModificationAudit(AuditUser, DateTimeOffset.UtcNow);
        _repository.Save();

        return Ok(ProductMappings.ToDetails(product));
    }

    /// <summary>Cambia lo stato per <b>azione</b> (Publish/Discontinue): rispetta le invarianti del dominio.</summary>
    [HttpPost("{id:guid}/status")]
    public IActionResult ChangeStatus(Guid id, ChangeProductStatusRequest request)
    {
        var product = _repository.Get(id);
        if (product is null)
            return NotFound();

        switch (request.Action)
        {
            case ProductStatusAction.Publish:
                product.Publish();
                break;
            case ProductStatusAction.Discontinue:
                product.Discontinue();
                break;
        }

        product.ApplyModificationAudit(AuditUser, DateTimeOffset.UtcNow);
        _repository.Save();
        return Ok(ProductMappings.ToDetails(product));
    }

    /// <summary>Elenco compatto: <c>[{ id, code, label }]</c>.</summary>
    [HttpGet]
    public IEnumerable<ProductListItemDto> List()
        => _repository.List().Select(ProductMappings.ToListItem);

    /// <summary>Dettaglio completo: stato + metadati di audit/soft-delete.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
        => _repository.Get(id) is { } product
            ? Ok(ProductMappings.ToDetails(product))
            : NotFound();

    /// <summary>Ricerca: albero di filtri + proiezione/sort/paginazione.</summary>
    [HttpPost("search")]
    public IActionResult Search(SearchRequest request)
    {
        // NB: senza autenticazione reale usiamo un caller "di servizio" (tenant demo + permessi pieni).
        //     Con l'auth, il caller verrà derivato dall'utente corrente (spaceId + permessi).
        var caller = new SearchCaller(
            SimulatedFieldDefinitionDatabase.DemoSpace,
            new HashSet<Guid> { SearchPermissions.ViewPrice, SearchPermissions.ViewAudit });

        // Il controller non sa quale store gira sotto: chiede la ricerca su "product" al facade e basta.
        var result = _search.Search("product", request, caller);

        return Ok(new { result.Items, result.TotalCount, result.PageNumber, result.PageSize });
    }
}

using Search.Application.Querying.Metadata;

namespace Search.Application.Querying.Dynamic;

/// <summary>
/// SEED che simula il contenuto della tabella delle definizioni dei campi (in attesa del provider su DB).
/// <b>Non</b> è un provider: produce un <see cref="InMemorySearchFieldDefinitionProvider"/> già popolato.
/// Così esiste una sola implementazione dell'interfaccia (quella in memoria) e il seed resta puro dato,
/// domani rimpiazzabile da un <c>EfSearchFieldDefinitionProvider</c> senza toccare il resto.
/// </summary>
public static class SimulatedFieldDefinitionDatabase
{
    /// <summary>Tenant di demo a cui è associato il campo dinamico "deliveryZone".</summary>
    public static readonly Guid DemoSpace = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    /// <summary>Crea un provider in memoria già popolato con le definizioni di demo.</summary>
    public static InMemorySearchFieldDefinitionProvider Create()
    {
        var rows = new InMemorySearchFieldDefinitionProvider();

        // === product (store relazionale): Path = property-path CLR ===
        /**
        rows.Add(new SearchFieldDefinition("product", "id", FieldKind.Guid, false, "Id"));
        rows.Add(new SearchFieldDefinition("product", "sku", FieldKind.String, false, "Sku.Value", Label: "SKU"));
        rows.Add(new SearchFieldDefinition("product", "name", FieldKind.String, false, "Name"));
        rows.Add(new SearchFieldDefinition("product", "description", FieldKind.String, false, "Description"));
        rows.Add(new SearchFieldDefinition("product", "brandId", FieldKind.Guid, false, "BrandId", Label: "Marca"));
        rows.Add(new SearchFieldDefinition("product", "price", FieldKind.Decimal, false, "Price.Amount",
            Label: "Prezzo", Section: "Economici", RequiredPermissionId: SearchPermissions.ViewPrice));
        rows.Add(new SearchFieldDefinition("product", "stockQuantity", FieldKind.Integer, false, "StockQuantity",
            Label: "Quantità in stock", Section: "Economici"));
        rows.Add(new SearchFieldDefinition("product", "status", FieldKind.Enum, false, "Status"));
        rows.Add(new SearchFieldDefinition("product", "category", FieldKind.String, false, "Category"));
        rows.Add(new SearchFieldDefinition("product", "weightInGrams", FieldKind.Decimal, false, "WeightInGrams",
            Label: "Peso (g)", Section: "Fisici"));
        // dimensioni fisiche (value object Dimensions)
        rows.Add(new SearchFieldDefinition("product", "lengthMm", FieldKind.Decimal, false, "Dimensions.LengthMm",
            Label: "Lunghezza (mm)", Section: "Fisici"));
        rows.Add(new SearchFieldDefinition("product", "widthMm", FieldKind.Decimal, false, "Dimensions.WidthMm",
            Label: "Larghezza (mm)", Section: "Fisici"));
        rows.Add(new SearchFieldDefinition("product", "heightMm", FieldKind.Decimal, false, "Dimensions.HeightMm",
            Label: "Altezza (mm)", Section: "Fisici"));
        // collezione molti-a-molti → il path "Tags.Name" verrà ricostruito in x.Tags.Select(t => t.Name)
        rows.Add(new SearchFieldDefinition("product", "tags", FieldKind.String, true, "Tags.Name", Label: "Tag"));
        rows.Add(new SearchFieldDefinition("product", "tagIds", FieldKind.Guid, true, "Tags.Id"));
        */
        rows.Add(new SearchFieldDefinition("product", "id", FieldKind.Guid, false, "\"product\".\"Id\""));
        //sku: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "name", FieldKind.String, false, "\"product\".\"Name\""));
        rows.Add(new SearchFieldDefinition("product", "description", FieldKind.String, false, "\"product\".\"Description\""));
        rows.Add(new SearchFieldDefinition("product", "brandId", FieldKind.Guid, false, "\"product\".\"BrandId\"", Label: "Marca"));
        // brandName: colonna di Brands; il LEFT JOIN sull'alias "brand" vive nel From dello schema SQL
        // (CatalogSqlSchemaProvider), quindi qui è un semplice riferimento di colonna.
        rows.Add(new SearchFieldDefinition("product", "brandName", FieldKind.String, false, "\"brand\".\"Name\"", Label: "Marca"));
        //price: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "stockQuantity", FieldKind.Integer, false, "\"product\".\"StockQuantity\"", Label: "Quantità in stock", Section: "Economici"));
        rows.Add(new SearchFieldDefinition("product", "status", FieldKind.Enum, false, "\"product\".\"Status\""));
        rows.Add(new SearchFieldDefinition("product", "category", FieldKind.String, false, "\"product\".\"Category\""));
        rows.Add(new SearchFieldDefinition("product", "priceAmount", FieldKind.Decimal, false, "CONCAT(\"product\".\"PriceAmount\", ' ', \"product\".\"PriceCurrency\")", Label: "Prezzo", Section: "Economici"));
        //weightInGrams: aspettiamo
        //dimensioni fisiche: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "tags", FieldKind.String, true, "\"tag\".\"Name\"", Label: "Tag"));
        rows.Add(new SearchFieldDefinition("product", "tagIds", FieldKind.Guid, true, "\"tag\".\"Id\""));

        // === order (store documentale): Path = path del documento Mongo ===
        rows.Add(new SearchFieldDefinition("order", "status", FieldKind.Enum, false, "status"));
        rows.Add(new SearchFieldDefinition("order", "total", FieldKind.Decimal, false, "totalAmount.amount"));
        rows.Add(new SearchFieldDefinition("order", "customerEmail", FieldKind.String, false, "customer.email"));

        // === campo dinamico, solo per il tenant DemoSpace ===
        rows.Add(new SearchFieldDefinition("order", "deliveryZone", FieldKind.String, false, "attributes.deliveryZone",
            Label: "Zona di consegna", Section: "Logistica", SpaceId: DemoSpace));

        rows.Add(new SearchFieldDefinition("brand", "id", FieldKind.Guid, false, "\"brand\".\"Id\""));
        rows.Add(new SearchFieldDefinition("brand", "code", FieldKind.String, false, "\"brand\".\"Code\""));
        rows.Add(new SearchFieldDefinition("brand", "description", FieldKind.String, false, "\"brand\".\"Description\""));
        rows.Add(new SearchFieldDefinition("brand", "countryOfOrigin", FieldKind.String, false, "\"brand\".\"CountryOfOrigin\""));
        rows.Add(new SearchFieldDefinition("brand", "website", FieldKind.String, false, "\"brand\".\"Website\""));
        rows.Add(new SearchFieldDefinition("brand", "logoUrl", FieldKind.String, false, "\"brand\".\"LogoUrl\""));
        rows.Add(new SearchFieldDefinition("brand", "tags", FieldKind.String, true, "\"tag\".\"Name\"", Label: "Tag"));
        rows.Add(new SearchFieldDefinition("brand", "tagIds", FieldKind.Guid, true, "\"tag\".\"Id\""));

        // === brand: campi su una colonna JSONB "Data" (Postgres). Scalare via estrazione (#>>, col cast se
        // tipizzato); array via il path dell'array (l'unnest per il filtro sta nella config SQL, non qui). ===
        rows.Add(new SearchFieldDefinition("brand", "dataCity", FieldKind.String, false,
            "\"brand\".\"Data\" #>> '{address,city}'", Label: "Città (JSON)", Section: "JSON"));
        rows.Add(new SearchFieldDefinition("brand", "dataScore", FieldKind.Decimal, false,
            "(\"brand\".\"Data\" #>> '{metrics,score}')::numeric", Label: "Score (JSON)", Section: "JSON"));
        rows.Add(new SearchFieldDefinition("brand", "dataTags", FieldKind.String, true,
            "\"brand\".\"Data\" -> 'tags'", Label: "Tag (JSON)", Section: "JSON"));




        rows.Add(new SearchFieldDefinition("customer", "id", FieldKind.Guid, false, "customer.\"Id\""));
        rows.Add(new SearchFieldDefinition("customer", "firstName", FieldKind.String, false, "customer.\"FirstName\""));
        rows.Add(new SearchFieldDefinition("customer", "lastName", FieldKind.String, false, "customer.\"LastName\""));
        rows.Add(new SearchFieldDefinition("customer", "email", FieldKind.String, false, "customer.\"Email\""));
        rows.Add(new SearchFieldDefinition("customer", "legalStreet", FieldKind.String, false, "customer.\"Legal\" ->> 'Street'"));
        rows.Add(new SearchFieldDefinition("customer", "landline", FieldKind.String, true, "customer.\"Landline\""));

        return rows;
    }
}

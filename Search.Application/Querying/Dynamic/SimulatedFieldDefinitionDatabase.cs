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
    public static readonly Guid DemoSpace = Guid.Parse("4ae7781f-28a9-4070-b545-dfeb854c8764");

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

        /*
        rows.Add(new SearchFieldDefinition("product", "id", FieldKind.Guid, false, false, "\"product\".\"Id\""));
        //sku: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "name", FieldKind.String, false, false, "\"product\".\"Name\""));
        rows.Add(new SearchFieldDefinition("product", "description", FieldKind.String, false, false, "\"product\".\"Description\""));
        rows.Add(new SearchFieldDefinition("product", "brandId", FieldKind.Guid, false, false, "\"product\".\"BrandId\"", Label: "Marca"));
        // brandName: colonna di Brands; il LEFT JOIN sull'alias "brand" vive nel From dello schema SQL
        // (CatalogSqlSchemaProvider), quindi qui è un semplice riferimento di colonna.
        rows.Add(new SearchFieldDefinition("product", "brandName", FieldKind.String, false, false, "\"brand\".\"Name\"", Label: "Marca"));
        //price: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "stockQuantity", FieldKind.Integer, false, false, "\"product\".\"StockQuantity\"", Label: "Quantità in stock", Section: "Economici"));
        rows.Add(new SearchFieldDefinition("product", "status", FieldKind.Enum, false, false, "\"product\".\"Status\""));
        rows.Add(new SearchFieldDefinition("product", "category", FieldKind.String, false, false, "\"product\".\"Category\""));
        rows.Add(new SearchFieldDefinition("product", "priceAmount", FieldKind.Decimal, false, false, "CONCAT(\"product\".\"PriceAmount\", ' ', \"product\".\"PriceCurrency\")", Label: "Prezzo", Section: "Economici"));
        //weightInGrams: aspettiamo
        //dimensioni fisiche: aspettiamo
        rows.Add(new SearchFieldDefinition("product", "tags", FieldKind.String, true, false, "\"tag\".\"Name\"", Label: "Tag"));
        rows.Add(new SearchFieldDefinition("product", "tagIds", FieldKind.Guid, true, false, "\"tag\".\"Id\""));

        // === order (store documentale): Path = path del documento Mongo ===
        rows.Add(new SearchFieldDefinition("order", "status", FieldKind.Enum, false, false, "status"));
        rows.Add(new SearchFieldDefinition("order", "total", FieldKind.Decimal, false, false, "totalAmount.amount"));
        rows.Add(new SearchFieldDefinition("order", "customerEmail", FieldKind.String, false, false, "customer.email"));

        // === campo dinamico, solo per il tenant DemoSpace ===
        rows.Add(new SearchFieldDefinition("order", "deliveryZone", FieldKind.String, false, false, "attributes.deliveryZone",
            Label: "Zona di consegna", Section: "Logistica", SpaceId: DemoSpace));

        rows.Add(new SearchFieldDefinition("brand", "id", FieldKind.Guid, false, false, "\"brand\".\"Id\""));
        rows.Add(new SearchFieldDefinition("brand", "code", FieldKind.String, false, false, "\"brand\".\"Code\""));
        rows.Add(new SearchFieldDefinition("brand", "description", FieldKind.String, false, false, "\"brand\".\"Description\""));
        rows.Add(new SearchFieldDefinition("brand", "countryOfOrigin", FieldKind.String, false, false, "\"brand\".\"CountryOfOrigin\""));
        rows.Add(new SearchFieldDefinition("brand", "website", FieldKind.String, false, false, "\"brand\".\"Website\""));
        rows.Add(new SearchFieldDefinition("brand", "logoUrl", FieldKind.String, false, false, "\"brand\".\"LogoUrl\""));
        rows.Add(new SearchFieldDefinition("brand", "tags", FieldKind.String, true, false, "\"tag\".\"Name\"", Label: "Tag"));
        rows.Add(new SearchFieldDefinition("brand", "tagIds", FieldKind.Guid, true, false, "\"tag\".\"Id\""));

        // === brand: campi su una colonna JSONB "Data" (Postgres). Scalare via estrazione (#>>, col cast se
        // tipizzato); array via il path dell'array (l'unnest per il filtro sta nella config SQL, non qui). ===
        rows.Add(new SearchFieldDefinition("brand", "dataCity", FieldKind.String, true, false,
            "\"brand\".\"Data\" #>> '{address,city}'", Label: "Città (JSON)", Section: "JSON"));
        rows.Add(new SearchFieldDefinition("brand", "dataScore", FieldKind.Decimal, true, false,
            "(\"brand\".\"Data\" #>> '{metrics,score}')::numeric", Label: "Score (JSON)", Section: "JSON"));
        rows.Add(new SearchFieldDefinition("brand", "dataTags", FieldKind.String, true, true,
            "\"brand\".\"Data\" -> 'tags'", Label: "Tag (JSON)", Section: "JSON"));
        */

        rows.Add(new SearchFieldDefinition("customer", "id", FieldKind.Guid, false, false, "customer.\"Id\""));
        rows.Add(new SearchFieldDefinition("customer", "businessType", FieldKind.String, false, false, "customer.\"BusinessType\""));
        rows.Add(new SearchFieldDefinition("customer", "legalDoor", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Door'"));
        rows.Add(new SearchFieldDefinition("customer", "phone", FieldKind.String, false, false, "customer.\"Phone\""));
        rows.Add(new SearchFieldDefinition("customer", "businessCode", FieldKind.String, false, false, "customer.\"BusinessCode\""));
        rows.Add(new SearchFieldDefinition("customer", "email", FieldKind.String, false, false, "customer.\"Email\""));
        rows.Add(new SearchFieldDefinition("customer", "businessName", FieldKind.String, false, false, "customer.\"BusinessName\""));
        rows.Add(new SearchFieldDefinition("customer", "legalStairs", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Stairs'"));
        rows.Add(new SearchFieldDefinition("customer", "vatNumber", FieldKind.String, false, false, "customer.\"VatNumber\""));
        rows.Add(new SearchFieldDefinition("customer", "sector", FieldKind.String, false, false, "customer.\"Sector\""));
        rows.Add(new SearchFieldDefinition("customer", "legalCountry", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Country'"));
        rows.Add(new SearchFieldDefinition("customer", "birthDate", FieldKind.DateTime, false, false, "customer.\"BirthDate\""));
        rows.Add(new SearchFieldDefinition("customer", "legalFloor", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Floor'"));
        rows.Add(new SearchFieldDefinition("customer", "legalStreet", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Street'"));
        rows.Add(new SearchFieldDefinition("customer", "legalStreetNumber", FieldKind.String, true, false, "customer.\"Legal\" ->> 'StreetNumber'"));
        rows.Add(new SearchFieldDefinition("customer", "birthProvince", FieldKind.String, false, false, "customer.\"BirthProvince\""));
        rows.Add(new SearchFieldDefinition("customer", "lastName", FieldKind.String, false, false, "customer.\"LastName\""));
        rows.Add(new SearchFieldDefinition("customer", "website", FieldKind.String, false, false, "customer.\"Website\""));
        rows.Add(new SearchFieldDefinition("customer", "legalState", FieldKind.String, true, false, "customer.\"Legal\" ->> 'State'"));
        rows.Add(new SearchFieldDefinition("customer", "legalCity", FieldKind.String, true, false, "customer.\"Legal\" ->> 'City'"));
        rows.Add(new SearchFieldDefinition("customer", "birthPlace", FieldKind.String, false, false, "customer.\"BirthPlace\""));
        rows.Add(new SearchFieldDefinition("customer", "birthCountry", FieldKind.String, false, false, "customer.\"BirthCountry\""));
        rows.Add(new SearchFieldDefinition("customer", "legalZip", FieldKind.String, true, false, "customer.\"Legal\" ->> 'Zip'"));
        rows.Add(new SearchFieldDefinition("customer", "sdiCode", FieldKind.String, true, false, "customer.\"Legal\" ->> 'SdiCode'"));
        rows.Add(new SearchFieldDefinition("customer", "businessFiscalCode", FieldKind.String, false, false, "customer.\"BusinessFiscalCode\""));
        rows.Add(new SearchFieldDefinition("customer", "tagIds", FieldKind.String, false, true, "tag.\"Id\""));
        rows.Add(new SearchFieldDefinition("customer", "businessActivity", FieldKind.String, false, false, "customer.\"BusinessActivity\""));
        rows.Add(new SearchFieldDefinition("customer", "fax", FieldKind.String, false, false, "customer.\"Fax\""));
        rows.Add(new SearchFieldDefinition("customer", "pec", FieldKind.String, false, false, "customer.\"Pec\""));
        rows.Add(new SearchFieldDefinition("customer", "firstName", FieldKind.String, false, false, "customer.\"FirstName\""));
        rows.Add(new SearchFieldDefinition("customer", "sex", FieldKind.String, false, false, "customer.\"Sex\""));
        rows.Add(new SearchFieldDefinition("customer", "tagNames", FieldKind.String, false, true, "tag.\"Name\""));
        rows.Add(new SearchFieldDefinition("customer", "type", FieldKind.String, false, false, "customer.\"Type\""));
        rows.Add(new SearchFieldDefinition("customer", "fiscalCode", FieldKind.String, false, false, "customer.\"FiscalCode\""));
        rows.Add(new SearchFieldDefinition("customer", "externalId", FieldKind.String, false, false, "customer.\"ExternalId\""));
        rows.Add(new SearchFieldDefinition("customer", "landline", FieldKind.String, true, true, "customer.\"Landline\""));
        rows.Add(new SearchFieldDefinition("customer", "mobile", FieldKind.String, true, true, "customer.\"Mobile\""));
        rows.Add(new SearchFieldDefinition("customer", "createdById", FieldKind.Guid, false, false, "customer.\"CreatedById\""));
        rows.Add(new SearchFieldDefinition("customer", "createdByName", FieldKind.String, false, false, "\"utenteCreatore\".\"Username\""));
        rows.Add(new SearchFieldDefinition("customer", "updatedById", FieldKind.Guid, false, false, "customer.\"UpdatedById\""));
        rows.Add(new SearchFieldDefinition("customer", "updatedByName", FieldKind.String, false, false, "\"utenteModificatore\".\"Username\""));
        rows.Add(new SearchFieldDefinition("customer", "updatedAt", FieldKind.DateTime, false, false, "customer.\"UpdatedAt\""));
        rows.Add(new SearchFieldDefinition("customer", "createdAt", FieldKind.DateTime, false, false, "customer.\"CreatedAt\""));
        rows.Add(new SearchFieldDefinition("customer", "spaceId", FieldKind.Guid, false, false, "customer.\"SpaceId\""));

        rows.Add(new SearchFieldDefinition("utente", "id", FieldKind.Guid, false, false, "utente.\"Id\""));
        rows.Add(new SearchFieldDefinition("utente", "username", FieldKind.String, false, false, "utente.\"Username\""));
        rows.Add(new SearchFieldDefinition("utente", "email", FieldKind.String, false, false, "utente.\"Email\""));
        rows.Add(new SearchFieldDefinition("utente", "name", FieldKind.String, false, false, "utente.\"Name\" || ' ' || utente.\"LastName\""));
        rows.Add(new SearchFieldDefinition("utente", "workProfile", FieldKind.String, false, true, "brand.\"Name\" || ' (' || workprofile.\"Name\" || ')'"));
        rows.Add(new SearchFieldDefinition("utente", "accountEmail", FieldKind.String, false, false, "account.\"Email\""));

        rows.Add(new SearchFieldDefinition("workProfile", "id", FieldKind.Guid, false, false, "workProfile.\"Id\""));
        rows.Add(new SearchFieldDefinition("workProfile", "name", FieldKind.String, false, false, "workProfile.\"Name\""));
        rows.Add(new SearchFieldDefinition("workProfile", "brandId", FieldKind.Guid, false, false, "workProfile.\"BrandId\""));
        rows.Add(new SearchFieldDefinition("workProfile", "brandName", FieldKind.String, false, false, "brand.\"Name\""));
        rows.Add(new SearchFieldDefinition("workProfile", "userIds", FieldKind.Guid, false, true, "utente.\"Id\""));
        rows.Add(new SearchFieldDefinition("workProfile", "userNames", FieldKind.String, false, true, "utente.\"Username\""));
        rows.Add(new SearchFieldDefinition("workProfile", "spaceId", FieldKind.Guid, false, false, "workProfile.\"SpaceId\""));

        // === compensationPlan (store documentale Mongo): Path = path del documento (dot-notation) ===
        rows.Add(new SearchFieldDefinition("compensationPlan", "id", FieldKind.ObjectId, false, false, "_id"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "createdAt", FieldKind.DateTime, false, false, "createdAt"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "updatedAt", FieldKind.DateTime, false, false, "updatedAt"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "createdByValue", FieldKind.String, false, false, "createdBy.value", Label: "Creato da (id)"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "createdByLabel", FieldKind.String, false, false, "createdBy.label", Label: "Creato da"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "updatedByValue", FieldKind.String, false, false, "updatedBy.value", Label: "Modificato da (id)"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "updatedByLabel", FieldKind.String, false, false, "updatedBy.label", Label: "Modificato da"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "name", FieldKind.String, false, false, "name"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "description", FieldKind.String, false, false, "description"));
        rows.Add(new SearchFieldDefinition("compensationPlan", "spaceId", FieldKind.String, false, false, "spaceId"));

        return rows;
    }
}

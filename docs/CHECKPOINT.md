# Checkpoint — Progetto "Search" (BE)

> Ultimo aggiornamento: 2026-07-13

> **DECISIONE (2026-07-13):** si procede verso definizioni dei campi **a DB** (approccio data-driven,
> ritenuto più diretto per l'applicazione). Le fette 1-2 (motore + autorizzazione + campi dinamici,
> in memoria) restano la base. Prossimo lavoro: portare le definizioni su DB. Nodo aperto da decidere:
> come gestire il *binding* per le entità relazionali (path-string+reflection vs overlay vs Dynamic LINQ).
> Vedi sezione "Campi a DB" in fondo.

## Obiettivo
Backend per schermate di ricerca su più entità del sistema. Il FE deve poter:
- **proiettare qualunque campo** dell'entità,
- applicare **filtri** per campo, coerenti con il **tipo del dato** e con il fatto che sia **array** o meno
  (equals, not equals, contains, gt/lt, between, in, array contains any/all, ...),
- comporre **filtri complessi** con **and / or / not** anche annidati.

## Sorgenti dati
- **PostgreSQL** → `Brand`, `Product` (contesto *Catalog*).
- **MongoDB** → `Order` (contesto *Ordering*).

## Stato

### STEP 1 — Modelli di dominio ✅ (fatto e verificato)
Progetto `Search.Domain` (puro, nessuna dipendenza EF/Mongo). Build a 0 warning, demo eseguita.
- `Common/`: `Entity` (uguaglianza per identità), `AuditableEntity` (audit: created/lastModified + soft delete),
  `AggregateRoot` (+ domain events), `ValueObject` (uguaglianza strutturale), `DomainException`/`DomainGuard`,
  `Money`.
- `Catalog/`: `Brand`, `Product` (+ `Sku`, `Dimensions`, `ProductStatus`), eventi. `Barcodes` = array scalare. `Tags` = **molti-a-molti** con l'entità `Tag` (aggregato a sé, `Catalog/Tags/Tag.cs`), skip navigation EF; in ricerca proiettata su `tags` (nomi) e `tagIds`.
- `Ordering/`: `Order` (+ `OrderLine`, `Address`, `CustomerInfo`, `OrderStatus`), eventi. Array: `Lines`.
- Decisioni chiave: modello ricco (setter privati + factory + metodi); audit via hook invocati dall'infra
  (`ApplyCreationAudit`/`ApplyModificationAudit`); soft delete come metodo di dominio; aggregati separati
  (Product referenzia Brand per Id); `OrderLine` con SKU come snapshot string (niente accoppiamento tra contesti).
- Fuori scope volutamente: optimistic concurrency, mapping EF/Mongo, repository, `IClock`/`IUserContext`.

### STEP 2 — Motore di ricerca 🔜 (in corso)
**Direzione scelta: Opzione 1 — motore custom sottile.** (Opzione 2, confronto multi-approccio
custom vs Gridify vs HotChocolate vs OData, rimandata: la faremo dopo aver visto cosa esce dalla 1.)

Progetto `Search.Application` (puro, referenzia Domain). Build + demo OK.

Blocchi:
1. ✅ **Contratto** `Querying/Filters`: `FilterNode` → `LogicalFilterNode{and|or|not}` + `ComparisonFilterNode{field,op,values}`; `FilterOperator`; factory `Filter`. `SearchRequest`/`SearchResult`/`SortField`/`PageRequest`.
2. ✅ **Metadata registry** `Querying/Metadata`: `FieldDescriptor`, `EntitySearchMap<T>` (MapField/MapArray con inferenza tipo→`FieldKind`), `OperatorRules.DefaultFor(kind,isArray)`, `IEntitySearchMap`. Esempio: `Maps/ProductSearchMap`.
3. ✅ **Validazione** `Querying/Validation`: `SearchRequestValidator` (campo/operatore/arità/sort/paginazione) → `SearchValidationException` (422).
4. ✅ **Translator LINQ/Expression** `Querying/Linq`: `LinqFilterTranslator<T>` (visitor su albero → `Expression<Func<T,bool>>`, con `ParameterReplacer` per ribasare i selettori e `ValueCoercion`). `LinqSearchExecutor<T>` (filtro+sort+proiezione dinamica a dizionario+paginazione).
   ✅ **Translator Mongo** in `Search.Infrastructure.Mongo` (MongoDB.Driver 3.10.0): `MongoFilterTranslator<T>` (stesso albero → `BsonDocument`/`FilterDefinition<T>`) + `MongoFieldPath` (path del documento ricavato dal selettore). `OrderSearchMap` in Application. Demo: stesso filtro → match LINQ in memoria **e** query Mongo equivalente.
5. 🔜/parziale **Proiezione** dinamica a dizionario fatta; paginazione offset fatta; **keyset/cursor** per Mongo da fare.

Note translator Mongo (trade-off da conoscere): il `BsonDocument` è costruito a mano (leggibile e testabile) ma **assume convenzioni di serializzazione** (camelCase per i path, enum come stringa, decimal come Decimal128, Guid come stringa). In produzione devono combaciare col BsonClassMap registrato; alternativa "a prova di convenzione": costruire il `FilterDefinition` col builder tipizzato del driver usando i selettori. NOT reso con `$nor` (non `$not`, che opera solo su singolo campo). Contains/StartsWith/EndsWith via `$regex` con opzione `i` (case-insensitive).

### Layer metadati + autorizzazione per campo — fetta 1 ✅ (fatto e verificato)
In `Search.Application/Querying/Authorization`:
- `FieldDescriptor` esteso con `Label`, `Section`, `VisibleByDefault`, `RequiredPermissionId`; `MapField`/`MapArray` con parametri opzionali per popolarli.
- `SearchCaller(SpaceId, Permissions)`; `SearchPermissions` (placeholder Guid).
- `EffectiveSearchMap(source, caller)`: mappa filtrata per permesso → "niente permesso ≡ campo assente".
- `SearchRequestSanitizer`: **pota** (non rifiuta) filtro/sort/proiezione sui campi assenti. Semantica: togliere da AND allarga, da OR restringe, NOT/AND/OR svuotati spariscono. Proiezione vuota ⇒ campi `VisibleByDefault`.
- Enforcement dei 3 requisiti (vedere/filtrare/ordinare) da un unico meccanismo (map effettiva) + riuso di validator/executor. Demo: Manager (con ViewPrice) 2 match con colonna price; Clerk (senza) 3 match (AND allargato) senza price.

### Fetta 2 — campi dinamici per tenant ✅ (fatto e verificato)
In `Search.Application/Querying/Dynamic` + `Querying/Metadata/MaterializedSearchMap`:
- `FieldDescriptor`: `Selector` ora nullable + `StoragePath` (path esplicito per i dinamici, es. `attributes.deliveryZone`).
- `DynamicFieldDefinition` (per `SpaceId`+entità), `IDynamicFieldProvider` (+`InMemory`), `DynamicFieldFactory` (definizione → descriptor).
- `SearchMapProvider.GetEffectiveMap(entity, caller)` = statici (codice) + dinamici (per spaceId) → merge (`MaterializedSearchMap`) → filtro permessi (`EffectiveSearchMap`).
- Translator Mongo usa `StoragePath` per i dinamici; i translator/executor LINQ lanciano un errore chiaro sui dinamici (sono store-Mongo, niente selettore CLR).
- Demo: tenant con `deliveryZone` → query `{... "attributes.deliveryZone": "Nord"}`; altro tenant → campo assente, potato.
- **NB:** questi file dynamic-only sono stati poi **consolidati** nel sistema unificato data-driven (vedi "Campi a DB"): concetti invariati, `DynamicFieldDefinition`→`SearchFieldDefinition`, `SearchMapProvider`→`DbBackedSearchMapProvider`.

## Proiezione push-down ✅ (fatto e verificato)
`LinqSearchExecutor` ora costruisce un `Select` dinamico verso `object[]` con i **soli campi richiesti** (per i campi array: `.ToList()` → EF li traduce in subquery/LEFT JOIN). Prima materializzava l'entità intera e proiettava in memoria (over-fetch + le navigation collection tornavano **vuote** sotto EF). Verificato su SQLite: `SELECT` ridotto alle sole colonne richieste + i `tags` proiettati via LEFT JOIN e **popolati**. Compatibile con l'esecuzione in memoria.

**`MongoSearchExecutor<TDocument>` ✅ (fatto e verificato)** — gemello documentale in `Search.Infrastructure.Mongo`: filtro (via `MongoFilterTranslator`) + **projection document** dinamico (solo i path richiesti, `_id:0`) + sort + skip/limit + `CountDocuments` → stesso `SearchResult<dizionario>`. `BuildPlan(request)` espone la query senza eseguirla. Verificato **su mongod reale** (EphemeralMongo, pacchetto sul console `Search`) su documenti schemaless col bag `attributes`: filtro+proiezione+sort corretti, campo dinamico `deliveryZone` incluso. Simmetria completa LINQ/EF ↔ Mongo.

## EF Core / SQLite — spike SQL ✅ (fatto e verificato)
Progetto `Search.Infrastructure.Sql` (EFCore.Sqlite 10.0.9): `CatalogDbContext` (Product + Tag), `EfProductRepository` (+`ToSql` via `ToQueryString`), factory `SqliteCatalog.CreateInMemory()`. Mapping dominio ricco: `Sku` via converter, `Money`/`Dimensions` owned, `Status` enum→stringa, **Tags M2M skip navigation** (`HasMany().WithMany()`), **soft-delete** query filter globale, `DomainEvents`/`Barcodes` ignorati. Lo **stesso motore** (definizioni DB → Expression) gira su `IQueryable` EF → SQL reale: il filtro M2M diventa **`EXISTS`** sulla giunzione (niente moltiplicazione righe), il `contains` diventa `lower(...)`, soft-delete applicato. Passaggio a **Postgres = una riga** (`UseSqlite`→`UseNpgsql`). NB: warning NuGet NU1903 su `SQLitePCLRaw` (dep transitiva SQLite) — sparisce con Npgsql. Spike guidato dal console `Search`.

## Campi a DB (fase decisa 2026-07-13)
Obiettivo: le definizioni dei campi (statici + dinamici) vivono a DB, come nel vecchio progetto (`SearchField`).
Nodo tecnico centrale: **il binding non si serializza** (non puoi salvare una lambda). Risoluzioni:
- **Mongo/document**: si salva il path (`customer.email`, `attributes.zonaConsegna`) → il translator Mongo lo usa
  direttamente. **Già supportato** (StoragePath, fetta 2). Per gli ordini è quasi gratis.
- **Relazionale/EF**: si salva una property-path (`Price.Amount`) e si **ricostruisce l'Expression via reflection**
  al load (perde la type-safety compile-time → validare il path al caricamento). Caso M2M/collezioni (`Tags.Name`)
  = ricostruzione di un `Select(...)` → più complesso. Alternativa: Dynamic LINQ (come vecchio progetto).
Sicurezza: la tabella diventa la whitelist → limitare la scrittura, validare i path al load, mai concatenare in SQL.

**Binding scelto (2026-07-13): path-string + rebuild Expression via reflection.** ✅ Factory fatta e verificata:
- `Querying/Metadata/FieldKindResolver` (estratto da EntitySearchMap, riusabile).
- `Querying/Metadata/PropertyPathSelectorFactory.Build(entityType, "Price.Amount")` → `LambdaExpression` (valida il path, fail-fast). Demo: mappa prodotto costruita da sole stringhe → stesso risultato della mappa in codice. Gestisce catene scalari (anche annidate in value object) + enum. **Non ancora**: proiezioni su collezioni (`Tags.Name` → `Select`).

**Sistema unificato data-driven ✅ (fatto e verificato).** In `Querying/Dynamic` (i 5 file "dynamic-only" della fetta 2 sono stati rimossi e sostituiti — un solo sistema):
- `StoreKind`, `SearchEntity` (+`Relational<T>`/`Document`), `SearchEntityRegistry`.
- `SearchFieldDefinition` (record unico: EntityName, Name, Kind, IsArray, Path, Label, Section, Visible, RequiredPermissionId, SpaceId?); `ISearchFieldDefinitionProvider` (+`InMemory`).
- `SearchFieldDefinitionResolver`: relazionale → `PropertyPathSelectorFactory` (Expression, tipo/operatori dal tipo reale); documentale → `StoragePath`.
- `DbBackedSearchMapProvider.GetEffectiveMap(entity, caller)` = definizioni (globali+tenant) → descrittori → merge → filtro permessi. Downstream invariato.
- Helper condivisi in `Querying/Metadata`: `FieldKindResolver`, `PropertyPathSelectorFactory`.
- Demo verificata: product (relazionale, in memoria) + order (documentale, query Mongo) + campo dinamico per tenant + pruning per altro tenant.

✅ **(c) collezioni/M2M da path** — `PropertyPathSelectorFactory` ora gestisce le collezioni: `"Tags.Name"` → `x.Tags.Select(t => t.Name)` (+`GetEnumerableElementType`); il resolver relazionale ricava tipo/operatori dall'elemento per i campi array. Verificato: filtro `tags containsAny` su prodotti con `Tag` M2M.
✅ **DB simulato** — le definizioni sono in `Search/SimulatedFieldDefinitionDatabase.cs` (righe pre-caricate, `ISearchFieldDefinitionProvider`); il seeding è uscito dalla demo. Rimpiazzabile con un provider EF senza toccare il resto.

### Pulizia (2026-07-13) ✅
Consolidato su **un solo percorso DB-driven**. Rimossi (non più necessari): `EntitySearchMap<T>`, `Maps/ProductSearchMap`, `Maps/OrderSearchMap`, `Search.Infrastructure.Mongo/MongoFieldPath` (+ cartella `Maps`). Semplificati: `EffectiveSearchMap` ora è una **factory statica** `For(entity, fields, caller)` che riusa `MaterializedSearchMap` (niente più dizionario duplicato); `MongoFilterTranslator` usa solo `StoragePath` (niente fallback selettore); `LinqFilterTranslator.BuildLogical` senza costante seed. `Program.cs` riscritto: solo demo DB-driven (product relazionale con manager/clerk + order documentale con tenant). NB: i riferimenti a quei file nelle sezioni precedenti sono storici.

### API layer — progetto `Search.Api` ✅ (fatto e verificato via HTTP)
ASP.NET Core con **controller** (`Controllers/ProductsController`, net10) — porta fissa **http://localhost:5080** (launchSettings). File **`Search.Api/products.http`** con tutte le chiamate (create→details/update/status concatenati per id, ricerche, casi 422/404). `IProductRepository` (Application/Catalog) + `InMemoryProductRepository` (Api). Rotte prodotti:
- `POST /products` (create; accetta dimensioni + tag) · `PUT /products/{id}/details` (info generiche, **semantica PUT = sostituzione**: campi omessi vengono azzerati) · `POST /products/{id}/status` (**per azione**: Publish/Discontinue, rispetta le invarianti) · `GET /products` → `[{id, code, label}]` · `GET /products/{id}` → dettaglio completo (stato + audit/soft-delete) · `POST /products/search`.
- **DTO/JSON layer**: `FilterNodeJsonConverter` (System.Text.Json) deserializza l'albero polimorfico dal FE (`and/or/not` + `field/op/value(s)`, operatori con alias `eq/gte/containsAny…`). Enum come stringhe.
- `Product.Create` esteso con `dimensions`/`weightInGrams` (scelta di design).
- Eccezioni dominio/validazione → 422/400. Search verificata: filtro complesso + proiezione/sort/paginazione, campo M2M `tags`, e 422 su operatore incoerente.
- **Stub noti:** il `SearchCaller` della rotta search è "di servizio" (tenant demo + permessi pieni) — l'auth reale (caller dall'utente) è da fare. `launchSettings.json` fissa le porte (http ~52887).

Prossimo: (e) **scoping dati per `spaceId`** sui risultati; auth reale → caller dall'utente; endpoint `GET /products/fields` (metadati per la UI); persistenza EF (rimandata).

**Nota di design — ricerca/proiezione su campo di un altro aggregato (es. `brandName`).** Non serve per forza una navigation property. 4 opzioni: (A) navigation `Product.Brand` (join to-one, sicuro come cardinalità, ma rompe il confine d'aggregato; solo Postgres); (B) snapshot denormalizzato `Product.BrandName` (nessun join, da sincronizzare su rename via evento — coerente con `CustomerInfo` sull'ordine); (C) read model `ProductSearchView` (brandName colonna piatta, write model puro); (D) resolve-by-id (nome→id→`brandId IN (...)` + stitch). Raccomandazione: se la ricerca è read-model → C/B; navigation solo come concessione pragmatica per la lettura. Decisione rimandata (tenuta come nota).

Aperti da discutere: layer DTO/JSON ✅ (fatto: `FilterNodeJsonConverter` nell'API), **case-insensitivity** ✅ (fatto: `Contains`/`NotContains`/`StartsWith`/`EndsWith` case-insensitive — LINQ `LOWER()` su entrambi i lati traducibile da EF, Mongo regex `i`; `Equals`/`In` restano case-sensitive), campi array di sotto-documenti (`lines.sku`) — aperto, test d'integrazione su Mongo/EF reali — aperto.

## Note / preferenze
- Focus dichiarato: **leggibilità e manutenibilità**.
- L'utente (mid, 3 anni) vuole spiegazioni con il *perché* delle scelte (taglio da mentoring).
- L'utente è interessato a **scrivere SQL** più avanti → quando agganceremo EF Core mostreremo l'SQL generato
  dall'`Expression` e valuteremo query scritte a mano dove conviene.

## Comandi utili
```bash
dotnet build Search.slnx
dotnet run --project Search/Search.csproj
```

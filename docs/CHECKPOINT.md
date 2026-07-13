# Checkpoint — Progetto "Search" (BE)

> Ultimo aggiornamento: 2026-07-10

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

Prossime fette STEP 2: (3) endpoint `GET /search/{entity}/fields` + `BaseSearchResponseDto`/`ColumnsHeaderDto`; (4) persistenza definizioni dinamiche + overlay config (DB). Poi: scoping dati per `spaceId` sulle query; wiring EF Core reale.

**Nota di design — ricerca/proiezione su campo di un altro aggregato (es. `brandName`).** Non serve per forza una navigation property. 4 opzioni: (A) navigation `Product.Brand` (join to-one, sicuro come cardinalità, ma rompe il confine d'aggregato; solo Postgres); (B) snapshot denormalizzato `Product.BrandName` (nessun join, da sincronizzare su rename via evento — coerente con `CustomerInfo` sull'ordine); (C) read model `ProductSearchView` (brandName colonna piatta, write model puro); (D) resolve-by-id (nome→id→`brandId IN (...)` + stitch). Raccomandazione: se la ricerca è read-model → C/B; navigation solo come concessione pragmatica per la lettura. Decisione rimandata (tenuta come nota).

Aperti da discutere: layer DTO/JSON (deserializzazione polimorfica `System.Text.Json`), **case-insensitivity** su Postgres (`ILIKE`/`citext`) coerente con Mongo, campi array di sotto-documenti (`lines.sku`), test d'integrazione su Mongo reale.

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

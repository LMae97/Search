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
- `Catalog/`: `Brand`, `Product` (+ `Sku`, `Dimensions`, `ProductStatus`), eventi. Campi array: `Tags`, `Barcodes`.
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

Aperti da discutere: layer DTO/JSON (deserializzazione polimorfica `System.Text.Json`), **case-insensitivity** su Postgres (`ILIKE`/`citext`) coerente con Mongo, campi array di sotto-documenti (`lines.sku`), endpoint API, test d'integrazione su Mongo reale.

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

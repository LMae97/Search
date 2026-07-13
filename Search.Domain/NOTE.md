# Decisioni architetturali principali

## Le decisioni che contano (e il perché)

| Scelta | Perché |
|--------|---------|
| **Setter privati + factory `Create()` + metodi** | Modello ricco, non anemico. È impossibile costruire un'entità in uno stato invalido: le invarianti risiedono nel dominio, non sono disperse nei service. |
| **Audit tramite `ApplyCreationAudit()` / `ApplyModificationAudit()`** | Sono hook che verranno invocati da un interceptor d'infrastruttura (`EF SaveChangesInterceptor` o wrapper del repository Mongo), leggendo utente da `IUserContext` e timestamp da `IClock`. Il dominio rimane completamente testabile e non conosce il concetto di "chi" o "quando". |
| **Soft delete come metodo di dominio (`MarkAsDeleted()` / `Restore()`)** | Eliminare un'entità è una decisione di business, non un side-effect automatico. Il filtro che nasconde gli elementi eliminati appartiene invece all'infrastruttura (`HasQueryFilter` in EF Core o filtro globale del repository). |
| **`Product` referenzia `Brand` tramite `BrandId`, non con una navigation property** | `Product` e `Brand` sono aggregati distinti: devono mantenere confini transazionali separati ed evitare caricamenti a cascata involontari. |
| **`OrderLine.Sku` è una `string` (snapshot), non il Value Object `Sku` del Catalogo** | Il bounded context **Ordering** non deve dipendere da **Catalog**. L'ordine deve congelare SKU, nome e prezzo del prodotto al momento dell'acquisto. |
| **Value Object (`Money`, `Address`, `Sku`, …)** | Incapsulano validazione e comportamento. Ad esempio, `Money.Add()` impedisce la somma di importi con valute differenti. Diventano tipi ricchi, riutilizzabili e semanticamente corretti. |
| **`DateTimeOffset` in UTC, non `DateTime`** | Evita ambiguità legate ai fusi orari. È la regola di riferimento per tutti i timestamp persistiti. |

---

# Scelte volutamente rimandate

I seguenti aspetti sono stati volutamente esclusi da questa fase perché appartengono all'infrastruttura e non al dominio, ma sono già previsti come step successivi.

- **Optimistic Concurrency**
  - PostgreSQL: `xmin`
  - MongoDB: campo `Version`
  - È una responsabilità del mapping d'infrastruttura, non del dominio.

- **Persistence e Infrastructure**
  - Mapping Entity Framework Core
  - Mapping MongoDB
  - Repository
  - `IClock`
  - `IUserContext`

Questi elementi verranno implementati negli step successivi dell'architettura.
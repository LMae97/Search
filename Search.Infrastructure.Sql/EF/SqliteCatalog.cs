using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Search.Infrastructure.Sql.EF;

/// <summary>
/// Crea un <see cref="CatalogDbContext"/> su SQLite <b>in-memory</b> (spike). Per passare a PostgresEF
/// basta sostituire <c>UseSqlite</c> con <c>UseNpgsql(connectionString)</c>: il resto non cambia.
/// </summary>
public static class SqliteCatalog
{
    public static CatalogDbContext CreateInMemory()
    {
        // La DB in-memory di SQLite vive finché la connessione resta aperta: la teniamo aperta.
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseSqlite(connection)
            // Logga in console SOLO l'SQL eseguito. Usa .LogTo(Console.WriteLine) senza filtro per TUTTO.
            .LogTo(Console.WriteLine, new[] { RelationalEventId.CommandExecuted })
            // Mostra anche i valori dei parametri: comodo in dev, da NON usare in produzione (dati sensibili nei log).
            .EnableSensitiveDataLogging()
            .Options;

        var context = new CatalogDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

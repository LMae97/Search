using System.Data.Common;
using Npgsql;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Crea connessioni ADO.NET verso il DB del catalogo, per l'esecuzione della ricerca SQL grezza
/// (<see cref="SqlSearchExecutor"/>). Astratta così l'executor resta agnostico dal provider concreto.
/// </summary>
public interface ICatalogConnectionFactory
{
    DbConnection Create();
}

/// <summary>Implementazione Postgres (Npgsql).</summary>
public sealed class NpgsqlCatalogConnectionFactory(string connectionString) : ICatalogConnectionFactory
{
    public DbConnection Create() => new NpgsqlConnection(connectionString);
}

using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Esegue un <see cref="SqlQueryPlan"/> parametrizzato su una qualsiasi connessione ADO.NET
/// (<see cref="System.Data.Common.DbConnection"/>): in produzione Postgres/Npgsql. Nessun ORM, nessun
/// modello: le righe tornano come dizionari campo→valore, coerenti con gli altri executor.
/// <para>
/// Se gli viene passato un <see cref="ILogger"/> logga l'SQL e i parametri di ogni comando (come il
/// command-logging di EF). Il livello si regola per categoria in appsettings (<c>Search.Infrastructure.Sql.SqlSearchExecutor</c>).
/// </para>
/// </summary>
public sealed class SqlSearchExecutor()
{
    /// <summary>Esegue la query dati e mappa ogni riga in un dizionario per nome-campo (l'alias del SELECT).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(DbConnection connection, SqlQueryPlan plan)
    {
        using var command = CreateCommand(connection, plan);
        using var reader = command.ExecuteReader();

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (reader.Read())
        {
            var record = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                record[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(record);
        }
        return rows;
    }

    /// <summary>Esegue la query di conteggio (<c>SELECT COUNT(*)</c>).</summary>
    public long Count(DbConnection connection, SqlQueryPlan countPlan)
    {
        using var command = CreateCommand(connection, countPlan);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    // I valori sono SEMPRE legati come parametri: mai concatenati nel testo (niente SQL injection).
    private DbCommand CreateCommand(DbConnection connection, SqlQueryPlan plan)
    {
        var command = connection.CreateCommand();
        command.CommandText = plan.Sql;
        foreach (var (name, value) in plan.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        // NB: logga anche i VALORI dei parametri (comodo in dev; in prod valuta un livello più alto o di
        //     oscurarli, come fa EF con EnableSensitiveDataLogging).
        //logger?.LogInformation("SqlSearchExecutor esegue:\n{Sql}\n-- parametri: {Parameters}",
        //    plan.Sql, FormatParameters(plan.Parameters));

        return command;
    }

    private static string FormatParameters(IReadOnlyDictionary<string, object?> parameters)
        => parameters.Count == 0
            ? "(nessuno)"
            : string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value ?? "NULL"}"));
}

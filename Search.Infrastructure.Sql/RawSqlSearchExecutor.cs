using System.Data.Common;

namespace Search.Infrastructure.Sql;

/// <summary>
/// Esegue una <see cref="SqlQuery"/> parametrizzata su una qualsiasi connessione ADO.NET
/// (SQLite nello spike, Postgres/Npgsql in produzione). Nessun ORM, nessun modello: le righe tornano
/// come dizionari campo→valore, coerenti con gli altri executor.
/// </summary>
public sealed class RawSqlSearchExecutor
{
    /// <summary>Esegue la query dati e mappa ogni riga in un dizionario per nome-campo (l'alias del SELECT).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Query(DbConnection connection, SqlQuery query)
    {
        using var command = CreateCommand(connection, query);
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
    public long Count(DbConnection connection, SqlQuery countQuery)
    {
        using var command = CreateCommand(connection, countQuery);
        return Convert.ToInt64(command.ExecuteScalar());
    }

    // I valori sono SEMPRE legati come parametri: mai concatenati nel testo (niente SQL injection).
    private static DbCommand CreateCommand(DbConnection connection, SqlQuery query)
    {
        var command = connection.CreateCommand();
        command.CommandText = query.Sql;
        foreach (var (name, value) in query.Parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }
}

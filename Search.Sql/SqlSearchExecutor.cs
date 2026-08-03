using System.Data.Common;
using System.Text.Json;

namespace WeByte.Search.Sql;

/// <summary>
/// Esegue un <see cref="SqlQueryPlan"/> parametrizzato su una qualsiasi connessione ADO.NET
/// (<see cref="System.Data.Common.DbConnection"/>): in produzione Postgres/Npgsql. Nessun ORM, nessun
/// modello: le righe tornano come dizionari campo→valore, coerenti con gli altri executor.
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
                record[reader.GetName(i)] = ReadValue(reader, i);
            rows.Add(record);
        }
        return rows;
    }

    // Npgsql legge le colonne json/jsonb (es. i campi Link, vedi json_build_object nel SELECT) come stringa
    // grezza: se la lasciassimo tale, il serializzatore a valle la vedrebbe come stringa e la re-escaperebbe
    // invece di emettere l'oggetto annidato. La ripariamo in un grafo "nudo" (Dictionary/List/primitivi):
    // un JsonElement boxato come object si serializza a volte per riflessione sulle sue proprietà interne
    // (es. ValueKind) invece che col suo contenuto, mentre Dictionary/List sono gestiti bene da qualsiasi
    // serializzatore JSON.
    private static object? ReadValue(DbDataReader reader, int i)
    {
        if (reader.IsDBNull(i)) return null;

        var value = reader.GetValue(i);
        if (value is string json && IsJsonColumn(reader.GetDataTypeName(i)))
            return Unwrap(JsonDocument.Parse(json).RootElement);

        return value;
    }

    private static object? Unwrap(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Unwrap(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Unwrap).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private static bool IsJsonColumn(string dataTypeName) =>
        dataTypeName.Equals("json", StringComparison.OrdinalIgnoreCase) ||
        dataTypeName.Equals("jsonb", StringComparison.OrdinalIgnoreCase);

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

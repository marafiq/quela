#if SQL_SERVER_CLIENT
using Microsoft.Data.SqlClient;
#endif

namespace Quela.Generator;

/// <summary>
/// Reads schema metadata from SQL Server database.
/// </summary>
public class SchemaReader
{
    private readonly string _connectionString;
    private readonly string? _schemaFilter;
    private readonly bool _includeViews;

    public SchemaReader(string connectionString, string? schemaFilter = null, bool includeViews = false)
    {
        _connectionString = connectionString;
        _schemaFilter = schemaFilter;
        _includeViews = includeViews;
    }

#if SQL_SERVER_CLIENT
    /// <summary>
    /// Reads all tables and their columns from the database.
    /// </summary>
    public async Task<List<TableInfo>> GetTablesAsync()
    {
        var tables = new Dictionary<string, TableInfo>();

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Query for tables (and optionally views)
        var tableQuery = @"
            SELECT
                TABLE_SCHEMA,
                TABLE_NAME,
                TABLE_TYPE
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE IN ('BASE TABLE'" + (_includeViews ? ", 'VIEW'" : "") + @")
            " + (_schemaFilter != null ? "AND TABLE_SCHEMA = @schema" : "") + @"
            ORDER BY TABLE_SCHEMA, TABLE_NAME";

        await using (var cmd = new SqlCommand(tableQuery, connection))
        {
            if (_schemaFilter != null)
                cmd.Parameters.AddWithValue("@schema", _schemaFilter);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var name = reader.GetString(1);

                var key = $"{schema}.{name}";
                tables[key] = new TableInfo
                {
                    Schema = schema,
                    Name = name,
                    Columns = new List<ColumnInfo>()
                };
            }
        }

        // Query for columns
        var columnQuery = @"
            SELECT
                c.TABLE_SCHEMA,
                c.TABLE_NAME,
                c.COLUMN_NAME,
                c.DATA_TYPE,
                c.IS_NULLABLE,
                c.CHARACTER_MAXIMUM_LENGTH,
                c.NUMERIC_PRECISION,
                c.NUMERIC_SCALE,
                c.ORDINAL_POSITION
            FROM INFORMATION_SCHEMA.COLUMNS c
            INNER JOIN INFORMATION_SCHEMA.TABLES t
                ON c.TABLE_SCHEMA = t.TABLE_SCHEMA
                AND c.TABLE_NAME = t.TABLE_NAME
            WHERE t.TABLE_TYPE IN ('BASE TABLE'" + (_includeViews ? ", 'VIEW'" : "") + @")
            " + (_schemaFilter != null ? "AND c.TABLE_SCHEMA = @schema" : "") + @"
            ORDER BY c.TABLE_SCHEMA, c.TABLE_NAME, c.ORDINAL_POSITION";

        await using (var cmd = new SqlCommand(columnQuery, connection))
        {
            if (_schemaFilter != null)
                cmd.Parameters.AddWithValue("@schema", _schemaFilter);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var tableName = reader.GetString(1);
                var key = $"{schema}.{tableName}";

                if (tables.TryGetValue(key, out var table))
                {
                    var column = new ColumnInfo
                    {
                        Name = reader.GetString(2),
                        SqlType = reader.GetString(3),
                        IsNullable = reader.GetString(4) == "YES",
                        MaxLength = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        Precision = reader.IsDBNull(6) ? null : (int)reader.GetByte(6),
                        Scale = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                    };
                    table.Columns.Add(column);
                }
            }
        }

        return tables.Values.ToList();
    }

    /// <summary>
    /// Tests the database connection.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the database name from the connection.
    /// </summary>
    public async Task<string> GetDatabaseNameAsync()
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection.Database;
    }
#else
    /// <summary>
    /// Reads all tables and their columns from the database.
    /// Note: SQL Server client not available - returns empty list.
    /// </summary>
    public Task<List<TableInfo>> GetTablesAsync()
    {
        Console.WriteLine("Note: SQL Server client not available (offline build).");
        return Task.FromResult(new List<TableInfo>());
    }

    /// <summary>
    /// Tests the database connection.
    /// Note: SQL Server client not available - always returns false.
    /// </summary>
    public Task<bool> TestConnectionAsync()
    {
        return Task.FromResult(false);
    }

    /// <summary>
    /// Gets the database name from the connection.
    /// Note: SQL Server client not available - returns empty string.
    /// </summary>
    public Task<string> GetDatabaseNameAsync()
    {
        return Task.FromResult(string.Empty);
    }
#endif
}

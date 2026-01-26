using System.Data;
using Dapper;

namespace Quela;

/// <summary>
/// The final output of a query builder - contains SQL and parameters ready for execution.
/// </summary>
public class SqlQuery
{
    public string Sql { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public SqlQuery(string sql, Dictionary<string, object?> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Dapper Execution Methods
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes the query and returns all rows.
    /// </summary>
    public async Task<IEnumerable<T>> QueryAsync<T>(IDbConnection connection)
        => await connection.QueryAsync<T>(Sql, Parameters);

    /// <summary>
    /// Executes the query and returns the first row or default.
    /// </summary>
    public async Task<T?> QueryFirstOrDefaultAsync<T>(IDbConnection connection)
        => await connection.QueryFirstOrDefaultAsync<T>(Sql, Parameters);

    /// <summary>
    /// Executes the query and returns exactly one row.
    /// </summary>
    public async Task<T> QuerySingleAsync<T>(IDbConnection connection)
        => await connection.QuerySingleAsync<T>(Sql, Parameters);

    /// <summary>
    /// Executes the query and returns the number of affected rows.
    /// </summary>
    public async Task<int> ExecuteAsync(IDbConnection connection)
        => await connection.ExecuteAsync(Sql, Parameters);

    /// <summary>
    /// Executes the query and returns a scalar value.
    /// </summary>
    public async Task<T?> ExecuteScalarAsync<T>(IDbConnection connection)
        => await connection.ExecuteScalarAsync<T>(Sql, Parameters);
}

using System.Data;

namespace Quela;

/// <summary>
/// Non-generic base interface for SQL query operations.
/// Used by subqueries where the result type doesn't matter.
/// </summary>
public interface IQuery
{
    /// <summary>
    /// Builds the final SQL query with parameters.
    /// </summary>
    SqlQuery Build();

    /// <summary>
    /// Returns just the SQL string.
    /// </summary>
    string ToSql();
}

/// <summary>
/// Generic query interface with typed execution methods.
/// </summary>
/// <typeparam name="T">The result type for query execution.</typeparam>
public interface IQuery<T> : IQuery
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Dapper Execution
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes the query and returns all rows.
    /// </summary>
    Task<IEnumerable<T>> QueryAsync(IDbConnection connection);

    /// <summary>
    /// Executes the query and returns the first row or default.
    /// </summary>
    Task<T?> FirstOrDefaultAsync(IDbConnection connection);

    /// <summary>
    /// Executes the query and returns exactly one row.
    /// </summary>
    Task<T> SingleAsync(IDbConnection connection);

    // ═══════════════════════════════════════════════════════════════════════════
    // Pagination (SQL Server)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds OFFSET clause. Requires ORDER BY.
    /// </summary>
    IQuery<T> Offset(int rows);

    /// <summary>
    /// Adds OFFSET and FETCH clauses. Requires ORDER BY.
    /// </summary>
    IQuery<T> OffsetFetch(int offset, int fetch);

    /// <summary>
    /// Adds FETCH clause with OFFSET 0.
    /// </summary>
    IQuery<T> Fetch(int rows);

    // ═══════════════════════════════════════════════════════════════════════════
    // Set Operations
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Combines with another query using UNION.
    /// </summary>
    IOrderBy<T> Union(IQuery<T> other);

    /// <summary>
    /// Combines with another query using UNION ALL.
    /// </summary>
    IOrderBy<T> UnionAll(IQuery<T> other);

    /// <summary>
    /// Combines with another query using INTERSECT.
    /// </summary>
    IOrderBy<T> Intersect(IQuery<T> other);

    /// <summary>
    /// Combines with another query using EXCEPT.
    /// </summary>
    IOrderBy<T> Except(IQuery<T> other);
}

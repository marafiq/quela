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

    // ═══════════════════════════════════════════════════════════════════════════
    // FOR JSON / FOR XML Output
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds FOR JSON AUTO clause for automatic JSON output.
    /// </summary>
    IQuery<string> ForJsonAuto(bool includeNullValues = false, string? root = null);

    /// <summary>
    /// Adds FOR JSON PATH clause for JSON output with explicit path mapping.
    /// </summary>
    IQuery<string> ForJsonPath(bool includeNullValues = false, string? root = null, bool withoutArrayWrapper = false);

    /// <summary>
    /// Adds FOR XML AUTO clause for automatic XML output.
    /// </summary>
    IQuery<string> ForXmlAuto(bool elements = false, string? root = null);

    /// <summary>
    /// Adds FOR XML PATH clause for XML output with explicit path mapping.
    /// </summary>
    IQuery<string> ForXmlPath(string? elementName = null, string? root = null);

    /// <summary>
    /// Adds FOR XML RAW clause for simple XML output.
    /// </summary>
    IQuery<string> ForXmlRaw(string? elementName = null, string? root = null, bool elements = false);

    // ═══════════════════════════════════════════════════════════════════════════
    // OPTION Clause (Query Hints)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Adds OPTION clause with query hints.
    /// </summary>
    IQuery<T> Option(params QueryHint[] hints);

    /// <summary>
    /// Adds OPTION (RECOMPILE) hint.
    /// </summary>
    IQuery<T> OptionRecompile();

    /// <summary>
    /// Adds OPTION (MAXDOP n) hint.
    /// </summary>
    IQuery<T> OptionMaxDop(int maxDegreeOfParallelism);

    /// <summary>
    /// Adds OPTION (OPTIMIZE FOR (@param = value)) hint.
    /// </summary>
    IQuery<T> OptionOptimizeFor(string parameterName, object value);

    /// <summary>
    /// Adds OPTION (OPTIMIZE FOR UNKNOWN) hint.
    /// </summary>
    IQuery<T> OptionOptimizeForUnknown();

    /// <summary>
    /// Adds OPTION (FAST n) hint.
    /// </summary>
    IQuery<T> OptionFast(int rows);

    /// <summary>
    /// Adds OPTION (FORCE ORDER) hint.
    /// </summary>
    IQuery<T> OptionForceOrder();

    /// <summary>
    /// Adds OPTION (HASH JOIN) hint.
    /// </summary>
    IQuery<T> OptionHashJoin();

    /// <summary>
    /// Adds OPTION (LOOP JOIN) hint.
    /// </summary>
    IQuery<T> OptionLoopJoin();

    /// <summary>
    /// Adds OPTION (MERGE JOIN) hint.
    /// </summary>
    IQuery<T> OptionMergeJoin();

    // Note: Dapper execution methods (QueryAsync, FirstOrDefaultAsync, etc.)
    // are available when Dapper package is added via extension methods.
}

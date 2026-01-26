namespace Quela.Dml;

/// <summary>
/// Base interface for all DML (Data Manipulation Language) statements.
/// Includes INSERT, UPDATE, DELETE, and MERGE operations.
/// </summary>
public interface IDmlStatement
{
    /// <summary>
    /// Builds the final SQL statement with parameters.
    /// </summary>
    DmlResult Build();

    /// <summary>
    /// Returns just the SQL string.
    /// </summary>
    string ToSql();
}

/// <summary>
/// The final output of a DML builder - contains SQL and parameters ready for execution.
/// </summary>
public class DmlResult
{
    public string Sql { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public DmlResult(string sql, Dictionary<string, object?> parameters)
    {
        Sql = sql;
        Parameters = parameters;
    }

    public DmlResult(string sql) : this(sql, new Dictionary<string, object?>())
    {
    }

    /// <summary>
    /// Deconstructs the result into SQL and parameters.
    /// </summary>
    public void Deconstruct(out string sql, out IReadOnlyDictionary<string, object?> parameters)
    {
        sql = Sql;
        parameters = Parameters;
    }

    // Note: Dapper execution methods available when Dapper package is added:
    // ExecuteAsync, ExecuteScalarAsync<T>, etc.
}

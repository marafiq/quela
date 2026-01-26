using System.Data;

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

    // Note: Dapper execution methods are available when Dapper package is added:
    // QueryAsync<T>, QueryFirstOrDefaultAsync<T>, ExecuteAsync, etc.
    // Add PackageReference Include="Dapper" to enable these methods.
}

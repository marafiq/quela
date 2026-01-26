namespace Quela;

/// <summary>
/// Marker interface for types that can appear in a SELECT clause.
/// </summary>
public interface ISelectable
{
    string ToSql();
}

/// <summary>
/// Marker interface for types that can appear in a GROUP BY clause.
/// </summary>
public interface IGroupable
{
    string ToSql();
}

/// <summary>
/// Marker interface for types that can appear in an ORDER BY clause.
/// </summary>
public interface IOrderable
{
    string ToSql();
}

/// <summary>
/// Marker interface for column types (used for generic constraints).
/// </summary>
public interface IColumn : ISelectable, IGroupable
{
    string TableName { get; }
    string ColumnName { get; }
    string FullName { get; }
}

/// <summary>
/// Interface for selectables that contain a subquery with parameters.
/// </summary>
public interface ISubquerySelectable : ISelectable
{
    /// <summary>
    /// Gets the SQL, parameters, and optional alias from the subquery.
    /// </summary>
    (string Sql, Dictionary<string, object?> Parameters, string? Alias) GetSubquerySql();
}

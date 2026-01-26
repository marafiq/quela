namespace Quela;

/// <summary>
/// Simple alias for subqueries when typed columns aren't needed.
/// </summary>
/// <example>
/// <code>
/// var sub = new Alias("sub");
/// Sql.From(Orders)
///    .CrossApply(subquery, sub.Name)
///    .Select(Orders.Id, Sql.Col&lt;decimal&gt;("sub.Total"));
/// </code>
/// </example>
public class Alias
{
    public string Name { get; }

    public Alias(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a column reference for this alias.
    /// </summary>
    public Column<T> Col<T>(string columnName) => new(Name, columnName);

    /// <summary>
    /// Creates a typed alias inline with columns.
    /// </summary>
    public static InlineAlias Create(string name, params (string Column, Type Type)[] columns)
        => new(name, columns);
}

/// <summary>
/// Inline alias with typed column definitions.
/// </summary>
public class InlineAlias
{
    public string Name { get; }
    private readonly Dictionary<string, Type> _columns;

    public InlineAlias(string name, (string Column, Type Type)[] columns)
    {
        Name = name;
        _columns = columns.ToDictionary(c => c.Column, c => c.Type);
    }

    public Column<T> Col<T>(string columnName)
    {
        if (_columns.TryGetValue(columnName, out var type) && type != typeof(T))
            throw new InvalidOperationException($"Column '{columnName}' is defined as {type.Name}, not {typeof(T).Name}");
        return new Column<T>(Name, columnName);
    }
}

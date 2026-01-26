namespace Quela;

/// <summary>
/// Represents a database table.
/// </summary>
public class Table
{
    public string Name { get; }
    public string? Schema { get; }
    public string? Alias { get; private set; }
    internal bool IsCte { get; init; }

    public Table(string name, string? schema = null, bool isCte = false)
    {
        Name = name;
        Schema = schema;
        IsCte = isCte;
    }

    /// <summary>
    /// Creates a copy of this table with an alias.
    /// </summary>
    public Table As(string alias)
    {
        return new Table(Name, Schema, IsCte) { Alias = alias };
    }

    /// <summary>
    /// The fully qualified name with schema: [Schema].[Name] or [Name]
    /// </summary>
    public string QualifiedName => IsCte
        ? $"[{Name}]"
        : Schema != null
            ? $"[{Schema}].[{Name}]"
            : $"[{Name}]";

    /// <summary>
    /// The reference name used in queries (alias if set, otherwise table name).
    /// </summary>
    public string Reference => Alias ?? Name;

    /// <summary>
    /// Creates a column reference for this table.
    /// </summary>
    public Column<T> Col<T>(string columnName) => new(Reference, columnName);

    /// <summary>
    /// Generates SQL for FROM clause.
    /// </summary>
    public string ToSql() => Alias != null
        ? $"{QualifiedName} AS [{Alias}]"
        : QualifiedName;

    /// <summary>
    /// Represents all columns from this table (t.*).
    /// </summary>
    public TableStar All => new(Reference);
}

/// <summary>
/// Represents table.* in a SELECT clause.
/// </summary>
public class TableStar : ISelectable
{
    private readonly string _tableReference;

    public TableStar(string tableReference)
    {
        _tableReference = tableReference;
    }

    public string ToSql() => $"[{_tableReference}].*";
}

/// <summary>
/// Represents SELECT * (all columns from all tables).
/// </summary>
public class Star : ISelectable
{
    public static readonly Star Instance = new();
    private Star() { }

    public string ToSql() => "*";
}

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
    internal string? TableHint { get; private set; }

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
        return new Table(Name, Schema, IsCte) { Alias = alias, TableHint = TableHint };
    }

    /// <summary>
    /// Creates a copy of this table with a table hint (e.g., NOLOCK, READUNCOMMITTED).
    /// </summary>
    public Table WithHint(TableHints hint)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hint.ToSql() };
    }

    /// <summary>
    /// Creates a copy of this table with multiple table hints.
    /// </summary>
    public Table WithHints(params TableHints[] hints)
    {
        var hintSql = string.Join(", ", hints.Select(h => h.ToSql()));
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hintSql };
    }

    /// <summary>
    /// Creates a copy of this table with an index hint.
    /// </summary>
    public Table WithIndex(string indexName)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = $"INDEX({indexName})" };
    }

    /// <summary>
    /// Creates a copy of this table with a raw hint string.
    /// </summary>
    public Table WithRawHint(string hint)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hint };
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
    public string ToSql()
    {
        var baseSql = Alias != null
            ? $"{QualifiedName} AS [{Alias}]"
            : QualifiedName;

        return TableHint != null
            ? $"{baseSql} WITH ({TableHint})"
            : baseSql;
    }

    /// <summary>
    /// Represents all columns from this table (t.*).
    /// </summary>
    public TableStar All => new(Reference);
}

/// <summary>
/// SQL Server table hints for locking and optimization.
/// </summary>
public enum TableHints
{
    /// <summary>Does not take shared locks (dirty reads allowed).</summary>
    NoLock,
    /// <summary>Same as NOLOCK.</summary>
    ReadUncommitted,
    /// <summary>Takes shared locks until transaction completes.</summary>
    HoldLock,
    /// <summary>Same as HOLDLOCK.</summary>
    Serializable,
    /// <summary>Uses row-level locks.</summary>
    RowLock,
    /// <summary>Uses page-level locks.</summary>
    PageLock,
    /// <summary>Uses table-level lock.</summary>
    TabLock,
    /// <summary>Uses table-level exclusive lock.</summary>
    TabLockX,
    /// <summary>Takes update locks (prevents others from updating).</summary>
    UpdLock,
    /// <summary>Takes exclusive locks.</summary>
    XLock,
    /// <summary>Does not wait if lock cannot be obtained.</summary>
    NoWait,
    /// <summary>Skip locked rows.</summary>
    ReadPast,
    /// <summary>Only reads committed data.</summary>
    ReadCommitted,
    /// <summary>Uses row versioning.</summary>
    ReadCommittedLock,
    /// <summary>Uses repeatable read isolation.</summary>
    RepeatableRead,
    /// <summary>Uses snapshot isolation.</summary>
    Snapshot,
    /// <summary>Forces index seek operation.</summary>
    ForceSeek,
    /// <summary>Forces index scan operation.</summary>
    ForceScan,
    /// <summary>Do not expand indexed views.</summary>
    NoExpand
}

/// <summary>
/// Extension methods for TableHints.
/// </summary>
public static class TableHintsExtensions
{
    public static string ToSql(this TableHints hint) => hint switch
    {
        TableHints.NoLock => "NOLOCK",
        TableHints.ReadUncommitted => "READUNCOMMITTED",
        TableHints.HoldLock => "HOLDLOCK",
        TableHints.Serializable => "SERIALIZABLE",
        TableHints.RowLock => "ROWLOCK",
        TableHints.PageLock => "PAGLOCK",
        TableHints.TabLock => "TABLOCK",
        TableHints.TabLockX => "TABLOCKX",
        TableHints.UpdLock => "UPDLOCK",
        TableHints.XLock => "XLOCK",
        TableHints.NoWait => "NOWAIT",
        TableHints.ReadPast => "READPAST",
        TableHints.ReadCommitted => "READCOMMITTED",
        TableHints.ReadCommittedLock => "READCOMMITTEDLOCK",
        TableHints.RepeatableRead => "REPEATABLEREAD",
        TableHints.Snapshot => "SNAPSHOT",
        TableHints.ForceSeek => "FORCESEEK",
        TableHints.ForceScan => "FORCESCAN",
        TableHints.NoExpand => "NOEXPAND",
        _ => throw new ArgumentOutOfRangeException(nameof(hint))
    };
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

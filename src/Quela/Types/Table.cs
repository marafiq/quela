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
    internal string? TemporalClause { get; private set; }

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
        return new Table(Name, Schema, IsCte) { Alias = alias, TableHint = TableHint, TemporalClause = TemporalClause };
    }

    /// <summary>
    /// Creates a copy of this table with a table hint (e.g., NOLOCK, READUNCOMMITTED).
    /// </summary>
    public Table WithHint(TableHints hint)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hint.ToSql(), TemporalClause = TemporalClause };
    }

    /// <summary>
    /// Creates a copy of this table with multiple table hints.
    /// </summary>
    public Table WithHints(params TableHints[] hints)
    {
        var hintSql = string.Join(", ", hints.Select(h => h.ToSql()));
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hintSql, TemporalClause = TemporalClause };
    }

    /// <summary>
    /// Creates a copy of this table with an index hint.
    /// </summary>
    public Table WithIndex(string indexName)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = $"INDEX({indexName})", TemporalClause = TemporalClause };
    }

    /// <summary>
    /// Creates a copy of this table with a raw hint string.
    /// </summary>
    public Table WithRawHint(string hint)
    {
        return new Table(Name, Schema, IsCte) { Alias = Alias, TableHint = hint, TemporalClause = TemporalClause };
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Temporal Table Support (FOR SYSTEM_TIME)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Queries the temporal table AS OF a specific point in time.
    /// </summary>
    public Table ForSystemTimeAsOf(DateTime dateTime)
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = $"FOR SYSTEM_TIME AS OF '{dateTime:yyyy-MM-dd HH:mm:ss.fffffff}'"
        };
    }

    /// <summary>
    /// Queries the temporal table AS OF a specific point in time using a parameter.
    /// </summary>
    public Table ForSystemTimeAsOf(string parameterExpression)
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = $"FOR SYSTEM_TIME AS OF {parameterExpression}"
        };
    }

    /// <summary>
    /// Queries all rows that were active FROM start_time TO end_time.
    /// </summary>
    public Table ForSystemTimeFromTo(DateTime startTime, DateTime endTime)
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = $"FOR SYSTEM_TIME FROM '{startTime:yyyy-MM-dd HH:mm:ss.fffffff}' TO '{endTime:yyyy-MM-dd HH:mm:ss.fffffff}'"
        };
    }

    /// <summary>
    /// Queries all rows that were active BETWEEN start_time AND end_time.
    /// </summary>
    public Table ForSystemTimeBetween(DateTime startTime, DateTime endTime)
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = $"FOR SYSTEM_TIME BETWEEN '{startTime:yyyy-MM-dd HH:mm:ss.fffffff}' AND '{endTime:yyyy-MM-dd HH:mm:ss.fffffff}'"
        };
    }

    /// <summary>
    /// Queries all rows CONTAINED IN the specified time range.
    /// </summary>
    public Table ForSystemTimeContainedIn(DateTime startTime, DateTime endTime)
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = $"FOR SYSTEM_TIME CONTAINED IN ('{startTime:yyyy-MM-dd HH:mm:ss.fffffff}', '{endTime:yyyy-MM-dd HH:mm:ss.fffffff}')"
        };
    }

    /// <summary>
    /// Queries all versions of rows (current and historical).
    /// </summary>
    public Table ForSystemTimeAll()
    {
        return new Table(Name, Schema, IsCte)
        {
            Alias = Alias,
            TableHint = TableHint,
            TemporalClause = "FOR SYSTEM_TIME ALL"
        };
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
        var sb = new System.Text.StringBuilder(QualifiedName);

        // Add temporal clause (FOR SYSTEM_TIME) immediately after table name
        if (TemporalClause != null)
        {
            sb.Append(' ');
            sb.Append(TemporalClause);
        }

        // Add alias
        if (Alias != null)
        {
            sb.Append(" AS [");
            sb.Append(Alias);
            sb.Append(']');
        }

        // Add table hints
        if (TableHint != null)
        {
            sb.Append(" WITH (");
            sb.Append(TableHint);
            sb.Append(')');
        }

        return sb.ToString();
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

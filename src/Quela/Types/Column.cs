namespace Quela;

/// <summary>
/// Type-safe column reference with compile-time type checking.
/// </summary>
/// <typeparam name="T">The CLR type of the column value.</typeparam>
public class Column<T> : IColumn, ISelectable, IGroupable, IOrderable
{
    public string TableName { get; }
    public string ColumnName { get; }

    public Column(string tableName, string columnName)
    {
        TableName = tableName;
        ColumnName = columnName;
    }

    /// <summary>
    /// The fully qualified column name: [Table].[Column]
    /// </summary>
    public string FullName => $"[{TableName}].[{ColumnName}]";

    /// <summary>
    /// Creates an aliased column for SELECT.
    /// </summary>
    public AliasedColumn<T> As(string alias) => new(this, alias);

    // ═══════════════════════════════════════════════════════════════════════════
    // Comparison Operators - Column to Value
    // ═══════════════════════════════════════════════════════════════════════════

    public static Condition operator ==(Column<T> col, T? value)
    {
        if (value is null)
            return new Condition($"{col.FullName} IS NULL");
        return new Condition($"{col.FullName} = @p", value);
    }

    public static Condition operator !=(Column<T> col, T? value)
    {
        if (value is null)
            return new Condition($"{col.FullName} IS NOT NULL");
        return new Condition($"{col.FullName} <> @p", value);
    }

    public static Condition operator >(Column<T> col, T value) =>
        new($"{col.FullName} > @p", value!);

    public static Condition operator <(Column<T> col, T value) =>
        new($"{col.FullName} < @p", value!);

    public static Condition operator >=(Column<T> col, T value) =>
        new($"{col.FullName} >= @p", value!);

    public static Condition operator <=(Column<T> col, T value) =>
        new($"{col.FullName} <= @p", value!);

    // ═══════════════════════════════════════════════════════════════════════════
    // Comparison Operators - Column to Column
    // ═══════════════════════════════════════════════════════════════════════════

    public static Condition operator ==(Column<T> left, Column<T> right) =>
        new($"{left.FullName} = {right.FullName}");

    public static Condition operator !=(Column<T> left, Column<T> right) =>
        new($"{left.FullName} <> {right.FullName}");

    // ═══════════════════════════════════════════════════════════════════════════
    // NULL Checks
    // ═══════════════════════════════════════════════════════════════════════════

    public Condition IsNull() => new($"{FullName} IS NULL");
    public Condition IsNotNull() => new($"{FullName} IS NOT NULL");

    // ═══════════════════════════════════════════════════════════════════════════
    // Ordering
    // ═══════════════════════════════════════════════════════════════════════════

    public OrderedColumn Asc() => new(FullName, ascending: true);
    public OrderedColumn Desc() => new(FullName, ascending: false);

    // ═══════════════════════════════════════════════════════════════════════════
    // IN / NOT IN
    // ═══════════════════════════════════════════════════════════════════════════

    public Condition In(params T[] values)
    {
        if (values.Length == 0)
            return new Condition("1 = 0"); // Always false for empty IN
        return new Condition($"{FullName} IN @p", values);
    }

    public Condition In(IEnumerable<T> values) => In(values.ToArray());

    public Condition NotIn(params T[] values)
    {
        if (values.Length == 0)
            return new Condition("1 = 1"); // Always true for empty NOT IN
        return new Condition($"{FullName} NOT IN @p", values);
    }

    public Condition NotIn(IEnumerable<T> values) => NotIn(values.ToArray());

    // ═══════════════════════════════════════════════════════════════════════════
    // ISelectable / IGroupable / IOrderable
    // ═══════════════════════════════════════════════════════════════════════════

    public string ToSql() => FullName;

    // ═══════════════════════════════════════════════════════════════════════════
    // Required overrides (for operator == / !=)
    // ═══════════════════════════════════════════════════════════════════════════

    public override bool Equals(object? obj)
    {
        if (obj is Column<T> other)
            return TableName == other.TableName && ColumnName == other.ColumnName;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(TableName, ColumnName);
}

/// <summary>
/// A column with an alias for SELECT output.
/// </summary>
public class AliasedColumn<T> : ISelectable
{
    private readonly Column<T> _column;
    private readonly string _alias;

    public AliasedColumn(Column<T> column, string alias)
    {
        _column = column;
        _alias = alias;
    }

    public string ToSql() => $"{_column.FullName} AS [{_alias}]";
}

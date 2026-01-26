namespace Quela;

/// <summary>
/// Window functions (ROW_NUMBER, RANK, LAG, LEAD, etc.)
/// </summary>
public static class Window
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Ranking Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>ROW_NUMBER()</summary>
    public static WindowFunction<int> RowNumber() => new("ROW_NUMBER()");

    /// <summary>RANK()</summary>
    public static WindowFunction<int> Rank() => new("RANK()");

    /// <summary>DENSE_RANK()</summary>
    public static WindowFunction<int> DenseRank() => new("DENSE_RANK()");

    /// <summary>NTILE(buckets)</summary>
    public static WindowFunction<int> Ntile(int buckets) => new($"NTILE({buckets})");

    /// <summary>PERCENT_RANK()</summary>
    public static WindowFunction<double> PercentRank() => new("PERCENT_RANK()");

    /// <summary>CUME_DIST()</summary>
    public static WindowFunction<double> CumeDist() => new("CUME_DIST()");

    // ═══════════════════════════════════════════════════════════════════════════
    // Value Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>LAG(column)</summary>
    public static WindowFunction<T> Lag<T>(Column<T> col) => new($"LAG({col.FullName})");

    /// <summary>LAG(column, offset)</summary>
    public static WindowFunction<T> Lag<T>(Column<T> col, int offset)
        => new($"LAG({col.FullName}, {offset})");

    /// <summary>LAG(column, offset, default)</summary>
    public static WindowFunction<T> Lag<T>(Column<T> col, int offset, T defaultValue)
        => new($"LAG({col.FullName}, {offset}, {FormatValue(defaultValue)})");

    /// <summary>LEAD(column)</summary>
    public static WindowFunction<T> Lead<T>(Column<T> col) => new($"LEAD({col.FullName})");

    /// <summary>LEAD(column, offset)</summary>
    public static WindowFunction<T> Lead<T>(Column<T> col, int offset)
        => new($"LEAD({col.FullName}, {offset})");

    /// <summary>LEAD(column, offset, default)</summary>
    public static WindowFunction<T> Lead<T>(Column<T> col, int offset, T defaultValue)
        => new($"LEAD({col.FullName}, {offset}, {FormatValue(defaultValue)})");

    /// <summary>FIRST_VALUE(column)</summary>
    public static WindowFunction<T> FirstValue<T>(Column<T> col)
        => new($"FIRST_VALUE({col.FullName})");

    /// <summary>LAST_VALUE(column)</summary>
    public static WindowFunction<T> LastValue<T>(Column<T> col)
        => new($"LAST_VALUE({col.FullName})");

    /// <summary>NTH_VALUE(column, n) - SQL Server 2022+</summary>
    public static WindowFunction<T> NthValue<T>(Column<T> col, int n)
        => new($"NTH_VALUE({col.FullName}, {n})");

    // ═══════════════════════════════════════════════════════════════════════════
    // Aggregate Window Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>COUNT(*) OVER</summary>
    public static WindowFunction<int> Count() => new("COUNT(*)");

    /// <summary>COUNT(column) OVER</summary>
    public static WindowFunction<int> Count<T>(Column<T> col) => new($"COUNT({col.FullName})");

    /// <summary>SUM(column) OVER</summary>
    public static WindowFunction<T> Sum<T>(Column<T> col) where T : struct
        => new($"SUM({col.FullName})");

    /// <summary>AVG(column) OVER</summary>
    public static WindowFunction<decimal> Avg<T>(Column<T> col) where T : struct
        => new($"AVG({col.FullName})");

    /// <summary>MIN(column) OVER</summary>
    public static WindowFunction<T> Min<T>(Column<T> col)
        => new($"MIN({col.FullName})");

    /// <summary>MAX(column) OVER</summary>
    public static WindowFunction<T> Max<T>(Column<T> col)
        => new($"MAX({col.FullName})");

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper
    // ═══════════════════════════════════════════════════════════════════════════

    private static string FormatValue<T>(T value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            _ => value.ToString()!
        };
    }
}

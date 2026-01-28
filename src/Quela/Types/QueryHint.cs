namespace Quela;

/// <summary>
/// Represents a query-level hint for the OPTION clause.
/// </summary>
public class QueryHint
{
    private readonly string _hint;

    private QueryHint(string hint)
    {
        _hint = hint;
    }

    /// <summary>
    /// Creates a custom query hint.
    /// </summary>
    public static QueryHint Raw(string hint) => new(hint);

    /// <summary>
    /// RECOMPILE - Forces recompilation of the query plan.
    /// </summary>
    public static QueryHint Recompile => new("RECOMPILE");

    /// <summary>
    /// FORCE ORDER - Preserves join order as written in the query.
    /// </summary>
    public static QueryHint ForceOrder => new("FORCE ORDER");

    /// <summary>
    /// HASH JOIN - Forces hash join algorithm.
    /// </summary>
    public static QueryHint HashJoin => new("HASH JOIN");

    /// <summary>
    /// LOOP JOIN - Forces nested loop join algorithm.
    /// </summary>
    public static QueryHint LoopJoin => new("LOOP JOIN");

    /// <summary>
    /// MERGE JOIN - Forces merge join algorithm.
    /// </summary>
    public static QueryHint MergeJoin => new("MERGE JOIN");

    /// <summary>
    /// CONCAT UNION - Forces CONCAT algorithm for UNION.
    /// </summary>
    public static QueryHint ConcatUnion => new("CONCAT UNION");

    /// <summary>
    /// HASH UNION - Forces HASH algorithm for UNION.
    /// </summary>
    public static QueryHint HashUnion => new("HASH UNION");

    /// <summary>
    /// MERGE UNION - Forces MERGE algorithm for UNION.
    /// </summary>
    public static QueryHint MergeUnion => new("MERGE UNION");

    /// <summary>
    /// HASH GROUP - Forces HASH algorithm for GROUP BY.
    /// </summary>
    public static QueryHint HashGroup => new("HASH GROUP");

    /// <summary>
    /// ORDER GROUP - Forces ORDER algorithm for GROUP BY.
    /// </summary>
    public static QueryHint OrderGroup => new("ORDER GROUP");

    /// <summary>
    /// EXPAND VIEWS - Expands indexed views.
    /// </summary>
    public static QueryHint ExpandViews => new("EXPAND VIEWS");

    /// <summary>
    /// KEEP PLAN - Relaxes the estimated recompile threshold.
    /// </summary>
    public static QueryHint KeepPlan => new("KEEP PLAN");

    /// <summary>
    /// KEEPFIXED PLAN - Forces the optimizer not to recompile.
    /// </summary>
    public static QueryHint KeepFixedPlan => new("KEEPFIXED PLAN");

    /// <summary>
    /// NO_PERFORMANCE_SPOOL - Prevents spool operator in plans.
    /// </summary>
    public static QueryHint NoPerformanceSpool => new("NO_PERFORMANCE_SPOOL");

    /// <summary>
    /// OPTIMIZE FOR UNKNOWN - Optimizes for unknown parameter values.
    /// </summary>
    public static QueryHint OptimizeForUnknown => new("OPTIMIZE FOR UNKNOWN");

    /// <summary>
    /// PARAMETERIZATION SIMPLE - Forces simple parameterization.
    /// </summary>
    public static QueryHint ParameterizationSimple => new("PARAMETERIZATION SIMPLE");

    /// <summary>
    /// PARAMETERIZATION FORCED - Forces forced parameterization.
    /// </summary>
    public static QueryHint ParameterizationForced => new("PARAMETERIZATION FORCED");

    /// <summary>
    /// ROBUST PLAN - Forces maximum potential row size.
    /// </summary>
    public static QueryHint RobustPlan => new("ROBUST PLAN");

    /// <summary>
    /// MAXDOP n - Sets maximum degree of parallelism.
    /// </summary>
    public static QueryHint MaxDop(int maxDegreeOfParallelism) => new($"MAXDOP {maxDegreeOfParallelism}");

    /// <summary>
    /// FAST n - Optimizes for fast first n rows.
    /// </summary>
    public static QueryHint Fast(int rows) => new($"FAST {rows}");

    /// <summary>
    /// MAXRECURSION n - Sets maximum recursion level for CTEs.
    /// </summary>
    public static QueryHint MaxRecursion(int maxRecursion) => new($"MAXRECURSION {maxRecursion}");

    /// <summary>
    /// OPTIMIZE FOR (@param = value) - Optimizes for specific parameter value.
    /// </summary>
    public static QueryHint OptimizeFor(string parameterName, object value)
    {
        var formattedValue = value switch
        {
            string s => $"N'{s.Replace("'", "''")}'",
            int i => i.ToString(),
            long l => l.ToString(),
            decimal d => d.ToString(),
            double db => db.ToString(),
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
        return new($"OPTIMIZE FOR ({parameterName} = {formattedValue})");
    }

    /// <summary>
    /// USE HINT ('hint_name') - Specifies a query processor hint.
    /// </summary>
    public static QueryHint UseHint(string hintName) => new($"USE HINT('{hintName}')");

    /// <summary>
    /// TABLE HINT (table, hints) - Applies table hints via OPTION clause.
    /// </summary>
    public static QueryHint TableHint(string tableName, params TableHints[] hints)
    {
        var hintList = string.Join(", ", hints.Select(h => h.ToSql()));
        return new($"TABLE HINT({tableName}, {hintList})");
    }

    public string ToSql() => _hint;

    public override string ToString() => _hint;
}

namespace Quela;

/// <summary>
/// Interface after FROM - can JOIN or skip to WHERE/SELECT.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IFrom<T> : IWhere<T>
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Table Joins
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// INNER JOIN (same as InnerJoin).
    /// </summary>
    IJoin<T> Join(Table table);

    /// <summary>
    /// INNER JOIN.
    /// </summary>
    IJoin<T> InnerJoin(Table table);

    /// <summary>
    /// LEFT OUTER JOIN.
    /// </summary>
    IJoin<T> LeftJoin(Table table);

    /// <summary>
    /// LEFT OUTER JOIN (explicit).
    /// </summary>
    IJoin<T> LeftOuterJoin(Table table);

    /// <summary>
    /// RIGHT OUTER JOIN.
    /// </summary>
    IJoin<T> RightJoin(Table table);

    /// <summary>
    /// RIGHT OUTER JOIN (explicit).
    /// </summary>
    IJoin<T> RightOuterJoin(Table table);

    /// <summary>
    /// FULL OUTER JOIN.
    /// </summary>
    IJoin<T> FullJoin(Table table);

    /// <summary>
    /// FULL OUTER JOIN (explicit).
    /// </summary>
    IJoin<T> FullOuterJoin(Table table);

    /// <summary>
    /// CROSS JOIN (no ON clause).
    /// </summary>
    IFrom<T> CrossJoin(Table table);

    // ═══════════════════════════════════════════════════════════════════════════
    // Subquery Joins
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// INNER JOIN with subquery.
    /// </summary>
    IJoin<T> Join(IQuery subquery, string alias);

    /// <summary>
    /// LEFT JOIN with subquery.
    /// </summary>
    IJoin<T> LeftJoin(IQuery subquery, string alias);

    /// <summary>
    /// RIGHT JOIN with subquery.
    /// </summary>
    IJoin<T> RightJoin(IQuery subquery, string alias);

    // ═══════════════════════════════════════════════════════════════════════════
    // APPLY (SQL Server)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CROSS APPLY with string alias.
    /// </summary>
    IFrom<T> CrossApply(IQuery subquery, string alias);

    /// <summary>
    /// OUTER APPLY with string alias.
    /// </summary>
    IFrom<T> OuterApply(IQuery subquery, string alias);

    /// <summary>
    /// CROSS APPLY with typed alias.
    /// </summary>
    IFrom<T> CrossApply<TAlias>(IQuery subquery, TAlias alias) where TAlias : TypedAlias;

    /// <summary>
    /// OUTER APPLY with typed alias.
    /// </summary>
    IFrom<T> OuterApply<TAlias>(IQuery subquery, TAlias alias) where TAlias : TypedAlias;

    /// <summary>
    /// CROSS APPLY with a table-valued function (e.g., OPENJSON, STRING_SPLIT).
    /// </summary>
    IFrom<T> CrossApply(TableValuedFunction tvf, string alias);

    /// <summary>
    /// OUTER APPLY with a table-valued function (e.g., OPENJSON, STRING_SPLIT).
    /// </summary>
    IFrom<T> OuterApply(TableValuedFunction tvf, string alias);

    // ═══════════════════════════════════════════════════════════════════════════
    // PIVOT / UNPIVOT (SQL Server)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies a PIVOT operation to transform row values into columns.
    /// </summary>
    /// <param name="aggregate">The aggregate function (e.g., Fn.Sum(column)).</param>
    /// <param name="forColumn">The column whose values become column headers.</param>
    /// <param name="inValues">The values to pivot into columns.</param>
    /// <param name="alias">The alias for the pivoted result.</param>
    IFrom<T> Pivot(ISelectable aggregate, IColumn forColumn, object[] inValues, string alias);

    /// <summary>
    /// Applies a PIVOT operation using string column names.
    /// </summary>
    /// <param name="aggregateFunction">The aggregate function name (e.g., "SUM").</param>
    /// <param name="valueColumn">The column to aggregate.</param>
    /// <param name="forColumn">The column whose values become column headers.</param>
    /// <param name="inValues">The values to pivot into columns.</param>
    /// <param name="alias">The alias for the pivoted result.</param>
    IFrom<T> Pivot(string aggregateFunction, string valueColumn, string forColumn, object[] inValues, string alias);

    /// <summary>
    /// Applies an UNPIVOT operation to transform columns into row values.
    /// </summary>
    /// <param name="valueColumn">The name for the new value column.</param>
    /// <param name="nameColumn">The name for the new column that will contain the original column names.</param>
    /// <param name="sourceColumns">The columns to unpivot.</param>
    /// <param name="alias">The alias for the unpivoted result.</param>
    IFrom<T> Unpivot(string valueColumn, string nameColumn, string[] sourceColumns, string alias);

    /// <summary>
    /// Applies an UNPIVOT operation using column references.
    /// </summary>
    /// <param name="valueColumn">The name for the new value column.</param>
    /// <param name="nameColumn">The name for the new column that will contain the original column names.</param>
    /// <param name="sourceColumns">The columns to unpivot.</param>
    /// <param name="alias">The alias for the unpivoted result.</param>
    IFrom<T> Unpivot(string valueColumn, string nameColumn, IColumn[] sourceColumns, string alias);
}

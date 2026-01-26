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
}

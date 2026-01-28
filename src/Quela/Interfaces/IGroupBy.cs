namespace Quela;

/// <summary>
/// Interface for GROUP BY clause.
/// Can add GROUP BY or skip to ORDER BY/SELECT.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IGroupBy<T> : IOrderBy<T>
{
    /// <summary>
    /// Adds GROUP BY clause with specified columns.
    /// </summary>
    IHaving<T> GroupBy(params IGroupable[] columns);

    /// <summary>
    /// Adds GROUP BY with ROLLUP for hierarchical subtotals.
    /// </summary>
    IHaving<T> GroupByRollup(params IGroupable[] columns);

    /// <summary>
    /// Adds GROUP BY with CUBE for all combinations of subtotals.
    /// </summary>
    IHaving<T> GroupByCube(params IGroupable[] columns);

    /// <summary>
    /// Adds GROUP BY with GROUPING SETS for custom grouping combinations.
    /// </summary>
    IHaving<T> GroupBySets(params IGroupable[][] sets);
}

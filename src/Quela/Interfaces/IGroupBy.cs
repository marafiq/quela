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
}

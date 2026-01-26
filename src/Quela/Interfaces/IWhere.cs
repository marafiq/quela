namespace Quela;

/// <summary>
/// Interface for WHERE clause.
/// Can add WHERE or skip to GROUP BY/SELECT.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IWhere<T> : IGroupBy<T>
{
    /// <summary>
    /// Adds WHERE clause with specified condition.
    /// </summary>
    ICondition<T> Where(Condition condition);

    /// <summary>
    /// Adds WHERE EXISTS subquery.
    /// </summary>
    ICondition<T> WhereExists(IQuery subquery);

    /// <summary>
    /// Adds WHERE NOT EXISTS subquery.
    /// </summary>
    ICondition<T> WhereNotExists(IQuery subquery);
}

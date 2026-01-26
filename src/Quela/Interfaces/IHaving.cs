namespace Quela;

/// <summary>
/// Interface for HAVING clause.
/// Available after GROUP BY.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IHaving<T> : IOrderBy<T>
{
    /// <summary>
    /// Adds HAVING clause to filter grouped results.
    /// </summary>
    IOrderBy<T> Having(Condition condition);
}

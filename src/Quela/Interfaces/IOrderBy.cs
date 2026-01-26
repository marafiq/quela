namespace Quela;

/// <summary>
/// Interface for ORDER BY clause.
/// Can add ORDER BY or skip directly to SELECT.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface IOrderBy<T> : ISelect<T>
{
    /// <summary>
    /// Adds ORDER BY clause with specified columns.
    /// </summary>
    ISelect<T> OrderBy(params IOrderable[] columns);
}

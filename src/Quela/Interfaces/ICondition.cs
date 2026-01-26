namespace Quela;

/// <summary>
/// Interface for adding AND/OR conditions after WHERE.
/// </summary>
/// <typeparam name="T">The result type.</typeparam>
public interface ICondition<T> : IGroupBy<T>
{
    /// <summary>
    /// Adds AND condition.
    /// </summary>
    ICondition<T> And(Condition condition);

    /// <summary>
    /// Adds OR condition (grouped in parentheses).
    /// </summary>
    ICondition<T> Or(Condition condition);

    /// <summary>
    /// Adds AND NOT condition.
    /// </summary>
    ICondition<T> AndNot(Condition condition);

    /// <summary>
    /// Adds OR NOT condition.
    /// </summary>
    ICondition<T> OrNot(Condition condition);

    /// <summary>
    /// Adds AND EXISTS subquery.
    /// </summary>
    ICondition<T> AndExists(IQuery subquery);

    /// <summary>
    /// Adds OR EXISTS subquery.
    /// </summary>
    ICondition<T> OrExists(IQuery subquery);

    /// <summary>
    /// Adds AND NOT EXISTS subquery.
    /// </summary>
    ICondition<T> AndNotExists(IQuery subquery);

    /// <summary>
    /// Adds OR NOT EXISTS subquery.
    /// </summary>
    ICondition<T> OrNotExists(IQuery subquery);
}

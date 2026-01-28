namespace Quela;

/// <summary>
/// Interface for the SELECT clause - comes last in the query chain.
/// At this point, all tables and aliases are known.
/// </summary>
/// <typeparam name="T">The default result type.</typeparam>
public interface ISelect<T>
{
    /// <summary>
    /// Selects specific columns.
    /// </summary>
    IQuery<T> Select(params ISelectable[] columns);

    /// <summary>
    /// Selects specific columns with a different result type.
    /// </summary>
    IQuery<TResult> Select<TResult>(params ISelectable[] columns);

    /// <summary>
    /// Selects columns with DISTINCT.
    /// </summary>
    IQuery<T> SelectDistinct(params ISelectable[] columns);

    /// <summary>
    /// Selects TOP N rows.
    /// </summary>
    IQuery<T> SelectTop(int count, params ISelectable[] columns);

    /// <summary>
    /// Selects TOP N rows WITH TIES (includes ties based on ORDER BY).
    /// </summary>
    IQuery<T> SelectTopWithTies(int count, params ISelectable[] columns);

    /// <summary>
    /// Selects TOP N PERCENT rows.
    /// </summary>
    IQuery<T> SelectTopPercent(int percent, params ISelectable[] columns);

    /// <summary>
    /// Selects TOP N PERCENT rows WITH TIES.
    /// </summary>
    IQuery<T> SelectTopPercentWithTies(int percent, params ISelectable[] columns);

    /// <summary>
    /// Selects all columns (*).
    /// </summary>
    IQuery<T> SelectAll();
}

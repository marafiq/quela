namespace Quela;

/// <summary>
/// Interface for CTE (Common Table Expression) support.
/// </summary>
public interface ICte
{
    /// <summary>
    /// Adds another CTE.
    /// </summary>
    ICte With(string name, Func<IQuery> queryBuilder);

    /// <summary>
    /// Adds a recursive CTE.
    /// </summary>
    ICte WithRecursive(string name, Func<IQuery> anchor, Func<IQuery> recursive);

    /// <summary>
    /// Starts the main query FROM clause.
    /// </summary>
    IFrom<Row> From(Table table);

    /// <summary>
    /// Starts the main query FROM clause with multiple tables.
    /// </summary>
    IFrom<Row> From(params Table[] tables);
}

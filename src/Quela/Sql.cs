using Quela.Internal;

namespace Quela;

/// <summary>
/// Entry point for building SQL queries.
/// All queries start with Sql.From() or Sql.With().
/// </summary>
public static class Sql
{
    // ═══════════════════════════════════════════════════════════════════════════
    // FROM - Primary entry point
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a query with FROM clause.
    /// </summary>
    public static IFrom<Row> From(Table table)
        => new QueryBuilder<Row>().From(table);

    /// <summary>
    /// Starts a query with multiple tables in FROM clause (implicit cross join).
    /// </summary>
    public static IFrom<Row> From(params Table[] tables)
        => new QueryBuilder<Row>().From(tables);

    /// <summary>
    /// Starts a query with a subquery in FROM clause.
    /// </summary>
    public static IFrom<Row> From(IQuery subquery, string alias)
        => new QueryBuilder<Row>().From(subquery, alias);

    // ═══════════════════════════════════════════════════════════════════════════
    // CTE Support
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts a query with a CTE (Common Table Expression).
    /// </summary>
    public static ICte With(string name, Func<IFrom<Row>> queryBuilder)
        => new CteBuilder(name, queryBuilder);

    /// <summary>
    /// Starts a query with a recursive CTE.
    /// </summary>
    public static ICte WithRecursive(string name, Func<IFrom<Row>> anchor, Func<IFrom<Row>> recursive)
        => new CteBuilder(name, anchor, recursive);

    /// <summary>
    /// Creates a table reference to a CTE for use in FROM clause.
    /// </summary>
    public static Table Cte(string name) => new(name, schema: null, isCte: true);

    // ═══════════════════════════════════════════════════════════════════════════
    // Utilities
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an untyped column reference (for CTEs, dynamic SQL).
    /// </summary>
    public static Column<T> Col<T>(string qualifiedName)
    {
        var parts = qualifiedName.Split('.');
        return parts.Length == 2
            ? new Column<T>(parts[0], parts[1])
            : new Column<T>("", parts[0]);
    }

    /// <summary>
    /// Creates a literal value for SELECT.
    /// </summary>
    public static Literal<T> Literal<T>(T value) => new(value);

    /// <summary>
    /// Escape hatch for complex SQL expressions.
    /// </summary>
    public static RawSql<T> Raw<T>(FormattableString sql) => new(sql);

    /// <summary>
    /// Represents SELECT * (all columns).
    /// </summary>
    public static Star All => Star.Instance;

    // ═══════════════════════════════════════════════════════════════════════════
    // EXISTS Conditions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates an EXISTS condition for use in WHERE clause.
    /// </summary>
    public static Condition Exists(IQuery subquery)
    {
        return new Condition($"EXISTS ({subquery.ToSql()})");
    }

    /// <summary>
    /// Creates a NOT EXISTS condition for use in WHERE clause.
    /// </summary>
    public static Condition NotExists(IQuery subquery)
    {
        return new Condition($"NOT EXISTS ({subquery.ToSql()})");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ANY / ALL / SOME for subquery comparisons
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a subquery wrapper for ANY comparison.
    /// </summary>
    public static SubqueryComparison<T> Any<T>(IQuery subquery)
        => new(subquery, "ANY");

    /// <summary>
    /// Creates a subquery wrapper for ALL comparison.
    /// </summary>
    public static SubqueryComparison<T> All<T>(IQuery subquery)
        => new(subquery, "ALL");

    /// <summary>
    /// Creates a subquery wrapper for SOME comparison.
    /// </summary>
    public static SubqueryComparison<T> Some<T>(IQuery subquery)
        => new(subquery, "SOME");
}

/// <summary>
/// Wrapper for subquery comparisons (ANY, ALL, SOME).
/// </summary>
public class SubqueryComparison<T>
{
    private readonly IQuery _subquery;
    private readonly string _operator;

    public SubqueryComparison(IQuery subquery, string @operator)
    {
        _subquery = subquery;
        _operator = @operator;
    }

    public static Condition operator >(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} > {sub._operator} ({sub._subquery.ToSql()})");

    public static Condition operator <(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} < {sub._operator} ({sub._subquery.ToSql()})");

    public static Condition operator >=(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} >= {sub._operator} ({sub._subquery.ToSql()})");

    public static Condition operator <=(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} <= {sub._operator} ({sub._subquery.ToSql()})");

    public static Condition operator ==(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} = {sub._operator} ({sub._subquery.ToSql()})");

    public static Condition operator !=(Column<T> col, SubqueryComparison<T> sub)
        => new($"{col.FullName} <> {sub._operator} ({sub._subquery.ToSql()})");

    public override bool Equals(object? obj) => obj is SubqueryComparison<T> other && _subquery == other._subquery;
    public override int GetHashCode() => _subquery.GetHashCode();
}

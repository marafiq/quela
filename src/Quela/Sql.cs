using Quela.Dml;
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
    public static ICte With(string name, Func<IQuery> queryBuilder)
        => new CteBuilder(name, queryBuilder);

    /// <summary>
    /// Starts a query with a recursive CTE.
    /// </summary>
    public static ICte WithRecursive(string name, Func<IQuery> anchor, Func<IQuery> recursive)
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
        var result = subquery.Build();
        var inheritedParams = new Dictionary<string, object?>(result.Parameters);
        return new Condition($"EXISTS ({result.Sql})", inheritedParams);
    }

    /// <summary>
    /// Creates a NOT EXISTS condition for use in WHERE clause.
    /// </summary>
    public static Condition NotExists(IQuery subquery)
    {
        var result = subquery.Build();
        var inheritedParams = new Dictionary<string, object?>(result.Parameters);
        return new Condition($"NOT EXISTS ({result.Sql})", inheritedParams);
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
    /// Creates a subquery wrapper for ALL comparison (col > ALL (subquery)).
    /// </summary>
    public static SubqueryComparison<T> AllOf<T>(IQuery subquery)
        => new(subquery, "ALL");

    /// <summary>
    /// Creates a subquery wrapper for SOME comparison.
    /// </summary>
    public static SubqueryComparison<T> Some<T>(IQuery subquery)
        => new(subquery, "SOME");

    // ═══════════════════════════════════════════════════════════════════════════
    // DML Operations (INSERT, UPDATE, DELETE, MERGE)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Starts an INSERT statement.
    /// </summary>
    public static IInsertInto InsertInto(Table table)
        => new InsertBuilder(table);

    /// <summary>
    /// Starts an UPDATE statement.
    /// </summary>
    public static IUpdate Update(Table table)
        => new UpdateBuilder(table);

    /// <summary>
    /// Starts a DELETE statement.
    /// </summary>
    public static IDelete DeleteFrom(Table table)
        => new DeleteBuilder(table);

    /// <summary>
    /// Starts a MERGE statement (SQL Server upsert).
    /// </summary>
    public static IMergeInto MergeInto(Table table)
        => new MergeBuilder(table);
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

    private static Condition CreateCondition(Column<T> col, SubqueryComparison<T> sub, string op)
    {
        var result = sub._subquery.Build();
        var inheritedParams = new Dictionary<string, object?>(result.Parameters);
        return new Condition($"{col.FullName} {op} {sub._operator} ({result.Sql})", inheritedParams);
    }

    public static Condition operator >(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, ">");

    public static Condition operator <(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, "<");

    public static Condition operator >=(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, ">=");

    public static Condition operator <=(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, "<=");

    public static Condition operator ==(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, "=");

    public static Condition operator !=(Column<T> col, SubqueryComparison<T> sub)
        => CreateCondition(col, sub, "<>");

    public override bool Equals(object? obj) => obj is SubqueryComparison<T> other && _subquery == other._subquery;
    public override int GetHashCode() => _subquery.GetHashCode();
}

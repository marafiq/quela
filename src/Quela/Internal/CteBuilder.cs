namespace Quela.Internal;

/// <summary>
/// Builder for CTE (Common Table Expression) queries.
/// </summary>
internal class CteBuilder : ICte
{
    private readonly List<string> _ctes = new();

    public CteBuilder(string name, Func<IFrom<Row>> queryBuilder)
    {
        var query = queryBuilder();
        var sql = query.ToSql();
        _ctes.Add($"[{name}] AS ({sql})");
    }

    public CteBuilder(string name, Func<IFrom<Row>> anchor, Func<IFrom<Row>> recursive)
    {
        var anchorQuery = anchor();
        var recursiveQuery = recursive();
        var anchorSql = anchorQuery.ToSql();
        var recursiveSql = recursiveQuery.ToSql();
        _ctes.Add($"[{name}] AS ({anchorSql} UNION ALL {recursiveSql})");
    }

    public ICte With(string name, Func<IFrom<Row>> queryBuilder)
    {
        var query = queryBuilder();
        var sql = query.ToSql();
        _ctes.Add($"[{name}] AS ({sql})");
        return this;
    }

    public ICte WithRecursive(string name, Func<IFrom<Row>> anchor, Func<IFrom<Row>> recursive)
    {
        var anchorQuery = anchor();
        var recursiveQuery = recursive();
        var anchorSql = anchorQuery.ToSql();
        var recursiveSql = recursiveQuery.ToSql();
        _ctes.Add($"[{name}] AS ({anchorSql} UNION ALL {recursiveSql})");
        return this;
    }

    public IFrom<Row> From(Table table)
    {
        var builder = new QueryBuilder<Row>();
        foreach (var cte in _ctes)
            builder.AddCte(cte);
        return builder.From(table);
    }

    public IFrom<Row> From(params Table[] tables)
    {
        var builder = new QueryBuilder<Row>();
        foreach (var cte in _ctes)
            builder.AddCte(cte);
        return builder.From(tables);
    }
}

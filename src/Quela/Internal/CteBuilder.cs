namespace Quela.Internal;

/// <summary>
/// Builder for CTE (Common Table Expression) queries.
/// </summary>
internal class CteBuilder : ICte
{
    private readonly List<string> _ctes = new();
    private readonly Dictionary<string, object?> _cteParams = new();
    private int _paramIndex;

    public CteBuilder(string name, Func<IQuery> queryBuilder)
    {
        var query = queryBuilder();
        var sql = IncorporateQuery(query);
        _ctes.Add($"[{name}] AS ({sql})");
    }

    public CteBuilder(string name, Func<IQuery> anchor, Func<IQuery> recursive)
    {
        var anchorQuery = anchor();
        var recursiveQuery = recursive();
        var anchorSql = IncorporateQuery(anchorQuery);
        var recursiveSql = IncorporateQuery(recursiveQuery);
        _ctes.Add($"[{name}] AS ({anchorSql} UNION ALL {recursiveSql})");
    }

    public ICte With(string name, Func<IQuery> queryBuilder)
    {
        var query = queryBuilder();
        var sql = IncorporateQuery(query);
        _ctes.Add($"[{name}] AS ({sql})");
        return this;
    }

    public ICte WithRecursive(string name, Func<IQuery> anchor, Func<IQuery> recursive)
    {
        var anchorQuery = anchor();
        var recursiveQuery = recursive();
        var anchorSql = IncorporateQuery(anchorQuery);
        var recursiveSql = IncorporateQuery(recursiveQuery);
        _ctes.Add($"[{name}] AS ({anchorSql} UNION ALL {recursiveSql})");
        return this;
    }

    public IFrom<Row> From(Table table)
    {
        var builder = new QueryBuilder<Row>();
        foreach (var cte in _ctes)
            builder.AddCte(cte);
        builder.InjectParameters(_cteParams, _paramIndex);
        return builder.From(table);
    }

    public IFrom<Row> From(params Table[] tables)
    {
        var builder = new QueryBuilder<Row>();
        foreach (var cte in _ctes)
            builder.AddCte(cte);
        builder.InjectParameters(_cteParams, _paramIndex);
        return builder.From(tables);
    }

    private string IncorporateQuery(IQuery query)
    {
        var result = query.Build();
        var sql = result.Sql;

        // Use two-pass approach to avoid overlapping replacements
        var paramList = result.Parameters.Keys
            .Where(k => k.StartsWith("@p"))
            .Select(k => (Key: k, Num: int.TryParse(k.Substring(2), out var n) ? n : -1))
            .OrderByDescending(x => x.Num)
            .ToList();

        // First pass: replace with temporary placeholders
        var tempPrefix = $"__temp_{Guid.NewGuid():N}_";
        foreach (var (oldKey, _) in paramList)
        {
            sql = sql.Replace(oldKey, tempPrefix + oldKey);
        }

        // Second pass: replace temp placeholders with new names (lowest numbers first for correct ordering)
        foreach (var (oldKey, _) in paramList.OrderBy(x => x.Num))
        {
            var newKey = $"@p{_paramIndex++}";
            _cteParams[newKey] = result.Parameters[oldKey];
            sql = sql.Replace(tempPrefix + oldKey, newKey);
        }

        return sql;
    }
}

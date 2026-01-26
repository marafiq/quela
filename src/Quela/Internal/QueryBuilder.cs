using System.Text;

namespace Quela.Internal;

/// <summary>
/// Internal query builder that implements ALL grammar interfaces.
/// Uses a single mutable builder pattern.
/// </summary>
internal class QueryBuilder<T> :
    IFrom<T>,
    IJoin<T>,
    IWhere<T>,
    ICondition<T>,
    IGroupBy<T>,
    IHaving<T>,
    IOrderBy<T>,
    ISelect<T>,
    IQuery<T>
{
    // ═══════════════════════════════════════════════════════════════════════════
    // State
    // ═══════════════════════════════════════════════════════════════════════════

    private readonly List<string> _ctes = new();
    private readonly List<string> _from = new();
    private readonly List<string> _joins = new();
    private readonly List<Condition> _where = new();
    private readonly List<string> _groupBy = new();
    private Condition? _having;
    private readonly List<string> _orderBy = new();
    private readonly List<string> _select = new();
    private int? _offset;
    private int? _fetch;
    private bool _distinct;
    private int? _top;

    private readonly List<(IQuery Query, string Operation)> _setOperations = new();

    private readonly Dictionary<string, object?> _params = new();
    private int _paramIndex;

    // ═══════════════════════════════════════════════════════════════════════════
    // CTE Support (called by CteBuilder)
    // ═══════════════════════════════════════════════════════════════════════════

    internal void AddCte(string cteSql)
    {
        _ctes.Add(cteSql);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FROM
    // ═══════════════════════════════════════════════════════════════════════════

    internal IFrom<T> From(Table table)
    {
        _from.Add(table.ToSql());
        return this;
    }

    internal IFrom<T> From(params Table[] tables)
    {
        _from.AddRange(tables.Select(t => t.ToSql()));
        return this;
    }

    internal IFrom<T> From(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        var subParams = subquery.Build().Parameters;
        MergeParameters(subParams);
        _from.Add($"({subSql}) AS [{alias}]");
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // JOIN
    // ═══════════════════════════════════════════════════════════════════════════

    public IJoin<T> Join(Table table)
    {
        _joins.Add($"INNER JOIN {table.ToSql()}");
        return this;
    }

    public IJoin<T> InnerJoin(Table table) => Join(table);

    public IJoin<T> LeftJoin(Table table)
    {
        _joins.Add($"LEFT OUTER JOIN {table.ToSql()}");
        return this;
    }

    public IJoin<T> LeftOuterJoin(Table table) => LeftJoin(table);

    public IJoin<T> RightJoin(Table table)
    {
        _joins.Add($"RIGHT OUTER JOIN {table.ToSql()}");
        return this;
    }

    public IJoin<T> RightOuterJoin(Table table) => RightJoin(table);

    public IJoin<T> FullJoin(Table table)
    {
        _joins.Add($"FULL OUTER JOIN {table.ToSql()}");
        return this;
    }

    public IJoin<T> FullOuterJoin(Table table) => FullJoin(table);

    public IFrom<T> CrossJoin(Table table)
    {
        _joins.Add($"CROSS JOIN {table.ToSql()}");
        return this;
    }

    public IJoin<T> Join(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _joins.Add($"INNER JOIN ({subSql}) AS [{alias}]");
        return this;
    }

    public IJoin<T> LeftJoin(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _joins.Add($"LEFT OUTER JOIN ({subSql}) AS [{alias}]");
        return this;
    }

    public IJoin<T> RightJoin(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _joins.Add($"RIGHT OUTER JOIN ({subSql}) AS [{alias}]");
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ON
    // ═══════════════════════════════════════════════════════════════════════════

    public IFrom<T> On(Condition condition)
    {
        var (sql, nextIndex) = condition.ToSql(_paramIndex, _params);
        _paramIndex = nextIndex;
        _joins[^1] += $" ON {sql}";
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // APPLY
    // ═══════════════════════════════════════════════════════════════════════════

    public IFrom<T> CrossApply(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _joins.Add($"CROSS APPLY ({subSql}) AS [{alias}]");
        return this;
    }

    public IFrom<T> OuterApply(IQuery subquery, string alias)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _joins.Add($"OUTER APPLY ({subSql}) AS [{alias}]");
        return this;
    }

    public IFrom<T> CrossApply<TAlias>(IQuery subquery, TAlias alias) where TAlias : TypedAlias
    {
        return CrossApply(subquery, alias.AliasName);
    }

    public IFrom<T> OuterApply<TAlias>(IQuery subquery, TAlias alias) where TAlias : TypedAlias
    {
        return OuterApply(subquery, alias.AliasName);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // WHERE
    // ═══════════════════════════════════════════════════════════════════════════

    public ICondition<T> Where(Condition condition)
    {
        _where.Add(condition);
        return this;
    }

    public ICondition<T> WhereExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _where.Add(new Condition($"EXISTS ({subSql})"));
        return this;
    }

    public ICondition<T> WhereNotExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        _where.Add(new Condition($"NOT EXISTS ({subSql})"));
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AND / OR
    // ═══════════════════════════════════════════════════════════════════════════

    public ICondition<T> And(Condition condition)
    {
        if (_where.Count > 0)
        {
            var last = _where[^1];
            _where[^1] = last.And(condition);
        }
        else
        {
            _where.Add(condition);
        }
        return this;
    }

    public ICondition<T> Or(Condition condition)
    {
        if (_where.Count > 0)
        {
            var last = _where[^1];
            _where[^1] = last.Or(condition);
        }
        else
        {
            _where.Add(condition);
        }
        return this;
    }

    public ICondition<T> AndNot(Condition condition)
    {
        return And(condition.Not());
    }

    public ICondition<T> OrNot(Condition condition)
    {
        return Or(condition.Not());
    }

    public ICondition<T> AndExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        return And(new Condition($"EXISTS ({subSql})"));
    }

    public ICondition<T> OrExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        return Or(new Condition($"EXISTS ({subSql})"));
    }

    public ICondition<T> AndNotExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        return And(new Condition($"NOT EXISTS ({subSql})"));
    }

    public ICondition<T> OrNotExists(IQuery subquery)
    {
        var subSql = subquery.ToSql();
        MergeParameters(subquery.Build().Parameters);
        return Or(new Condition($"NOT EXISTS ({subSql})"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GROUP BY
    // ═══════════════════════════════════════════════════════════════════════════

    public IHaving<T> GroupBy(params IGroupable[] columns)
    {
        _groupBy.AddRange(columns.Select(c => c.ToSql()));
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // HAVING
    // ═══════════════════════════════════════════════════════════════════════════

    public IOrderBy<T> Having(Condition condition)
    {
        _having = condition;
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ORDER BY
    // ═══════════════════════════════════════════════════════════════════════════

    public ISelect<T> OrderBy(params IOrderable[] columns)
    {
        _orderBy.AddRange(columns.Select(c => c.ToSql()));
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELECT
    // ═══════════════════════════════════════════════════════════════════════════

    public IQuery<T> Select(params ISelectable[] columns)
    {
        _select.AddRange(columns.Select(c => c.ToSql()));
        return this;
    }

    public IQuery<TResult> Select<TResult>(params ISelectable[] columns)
    {
        _select.AddRange(columns.Select(c => c.ToSql()));
        // Return a new builder with the same state but different type
        return new QueryBuilder<TResult>(this);
    }

    public IQuery<T> SelectDistinct(params ISelectable[] columns)
    {
        _distinct = true;
        return Select(columns);
    }

    public IQuery<T> SelectTop(int count, params ISelectable[] columns)
    {
        _top = count;
        return Select(columns);
    }

    public IQuery<T> SelectAll()
    {
        _select.Add("*");
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OFFSET / FETCH
    // ═══════════════════════════════════════════════════════════════════════════

    public IQuery<T> Offset(int rows)
    {
        _offset = rows;
        return this;
    }

    public IQuery<T> OffsetFetch(int offset, int fetch)
    {
        _offset = offset;
        _fetch = fetch;
        return this;
    }

    public IQuery<T> Fetch(int rows)
    {
        _offset = 0;
        _fetch = rows;
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Set Operations
    // ═══════════════════════════════════════════════════════════════════════════

    public IOrderBy<T> Union(IQuery<T> other)
    {
        _setOperations.Add((other, "UNION"));
        return this;
    }

    public IOrderBy<T> UnionAll(IQuery<T> other)
    {
        _setOperations.Add((other, "UNION ALL"));
        return this;
    }

    public IOrderBy<T> Intersect(IQuery<T> other)
    {
        _setOperations.Add((other, "INTERSECT"));
        return this;
    }

    public IOrderBy<T> Except(IQuery<T> other)
    {
        _setOperations.Add((other, "EXCEPT"));
        return this;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // BUILD
    // ═══════════════════════════════════════════════════════════════════════════

    public SqlQuery Build()
    {
        var sql = new StringBuilder();

        // CTEs
        if (_ctes.Count > 0)
        {
            sql.Append("WITH ");
            sql.Append(string.Join(", ", _ctes));
            sql.Append(' ');
        }

        // SELECT
        sql.Append("SELECT ");
        if (_distinct) sql.Append("DISTINCT ");
        if (_top.HasValue) sql.Append($"TOP ({_top}) ");
        sql.Append(_select.Count > 0 ? string.Join(", ", _select) : "*");

        // FROM
        if (_from.Count > 0)
        {
            sql.Append(" FROM ");
            sql.Append(string.Join(", ", _from));
        }

        // JOINs
        foreach (var join in _joins)
            sql.Append($" {join}");

        // WHERE
        if (_where.Count > 0)
        {
            sql.Append(" WHERE ");
            var whereParts = new List<string>();
            foreach (var cond in _where)
            {
                var (condSql, nextIndex) = cond.ToSql(_paramIndex, _params);
                _paramIndex = nextIndex;
                whereParts.Add(condSql);
            }
            sql.Append(string.Join(" AND ", whereParts));
        }

        // GROUP BY
        if (_groupBy.Count > 0)
        {
            sql.Append(" GROUP BY ");
            sql.Append(string.Join(", ", _groupBy));
        }

        // HAVING
        if (_having != null)
        {
            var (havingSql, nextIndex) = _having.ToSql(_paramIndex, _params);
            _paramIndex = nextIndex;
            sql.Append($" HAVING {havingSql}");
        }

        // Set Operations
        foreach (var (query, operation) in _setOperations)
        {
            sql.Append($" {operation} ");
            sql.Append(query.ToSql());
            MergeParameters(query.Build().Parameters);
        }

        // ORDER BY
        if (_orderBy.Count > 0)
        {
            sql.Append(" ORDER BY ");
            sql.Append(string.Join(", ", _orderBy));
        }

        // OFFSET FETCH
        if (_offset.HasValue)
        {
            sql.Append($" OFFSET {_offset} ROWS");
            if (_fetch.HasValue)
                sql.Append($" FETCH NEXT {_fetch} ROWS ONLY");
        }

        return new SqlQuery(sql.ToString(), _params);
    }

    public string ToSql() => Build().Sql;

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════════════════════

    private void MergeParameters(IReadOnlyDictionary<string, object?> other)
    {
        foreach (var kvp in other)
        {
            var newKey = $"@p{_paramIndex++}";
            _params[newKey] = kvp.Value;
        }
    }

    // Copy constructor for type conversion
    internal QueryBuilder(QueryBuilder<T> other)
    {
        _ctes = other._ctes;
        _from = other._from;
        _joins = other._joins;
        _where = other._where;
        _groupBy = other._groupBy;
        _having = other._having;
        _orderBy = other._orderBy;
        _select = other._select;
        _offset = other._offset;
        _fetch = other._fetch;
        _distinct = other._distinct;
        _top = other._top;
        _setOperations = other._setOperations;
        _params = other._params;
        _paramIndex = other._paramIndex;
    }

    // Generic copy constructor for type changes
    private QueryBuilder(object other)
    {
        var t = other.GetType();
        _ctes = (List<string>)t.GetField("_ctes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _from = (List<string>)t.GetField("_from", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _joins = (List<string>)t.GetField("_joins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _where = (List<Condition>)t.GetField("_where", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _groupBy = (List<string>)t.GetField("_groupBy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _having = (Condition?)t.GetField("_having", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other);
        _orderBy = (List<string>)t.GetField("_orderBy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _select = (List<string>)t.GetField("_select", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _offset = (int?)t.GetField("_offset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other);
        _fetch = (int?)t.GetField("_fetch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other);
        _distinct = (bool)t.GetField("_distinct", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _top = (int?)t.GetField("_top", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other);
        _setOperations = (List<(IQuery, string)>)t.GetField("_setOperations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _params = (Dictionary<string, object?>)t.GetField("_params", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
        _paramIndex = (int)t.GetField("_paramIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(other)!;
    }

    public QueryBuilder() { }
}

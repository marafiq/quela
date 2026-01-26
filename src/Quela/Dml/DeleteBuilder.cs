using System.Text;

namespace Quela.Dml;

/// <summary>
/// Fluent builder for DELETE statements.
/// </summary>
public interface IDelete
{
    /// <summary>
    /// Add WHERE clause.
    /// </summary>
    IDeleteWhere Where(Condition condition);

    /// <summary>
    /// Delete using a subquery (DELETE FROM table WHERE id IN (SELECT ...)).
    /// </summary>
    IDeleteWhere WhereIn<T>(Column<T> column, IQuery subquery);

    /// <summary>
    /// Delete where EXISTS.
    /// </summary>
    IDeleteWhere WhereExists(IQuery subquery);

    /// <summary>
    /// Join for correlated delete (SQL Server syntax).
    /// </summary>
    IDeleteFrom From(Table table);
}

/// <summary>
/// Delete with FROM clause for correlated deletes.
/// </summary>
public interface IDeleteFrom : IDelete
{
    /// <summary>
    /// Join another table.
    /// </summary>
    IDeleteJoin Join(Table table);

    /// <summary>
    /// Left join another table.
    /// </summary>
    IDeleteJoin LeftJoin(Table table);
}

/// <summary>
/// After JOIN, specify ON condition.
/// </summary>
public interface IDeleteJoin
{
    /// <summary>
    /// Specify join condition.
    /// </summary>
    IDeleteFrom On(Condition condition);
}

/// <summary>
/// After WHERE, ready to execute.
/// </summary>
public interface IDeleteWhere : IDmlStatement
{
    /// <summary>
    /// Add AND condition.
    /// </summary>
    IDeleteWhere And(Condition condition);

    /// <summary>
    /// Add OR condition.
    /// </summary>
    IDeleteWhere Or(Condition condition);

    /// <summary>
    /// Return deleted values (OUTPUT clause).
    /// </summary>
    IDeleteOutput Output(params IColumn[] columns);
}

/// <summary>
/// After OUTPUT clause.
/// </summary>
public interface IDeleteOutput : IDmlStatement
{
}

/// <summary>
/// Builds DELETE statements.
/// </summary>
public class DeleteBuilder : IDelete, IDeleteFrom, IDeleteJoin, IDeleteWhere, IDeleteOutput
{
    private readonly Table _table;
    private readonly List<Condition> _where = new();
    private Table? _fromTable;
    private readonly List<(string JoinType, Table Table, Condition On)> _joins = new();
    private string? _pendingJoinType;
    private Table? _pendingJoinTable;
    private readonly List<IColumn> _outputColumns = new();
    private readonly Dictionary<string, object?> _params = new();
    private int _paramIndex;

    internal DeleteBuilder(Table table)
    {
        _table = table;
    }

    public IDeleteWhere Where(Condition condition)
    {
        _where.Add(condition);
        return this;
    }

    public IDeleteWhere WhereIn<T>(Column<T> column, IQuery subquery)
    {
        var subSql = IncorporateSubquery(subquery);
        _where.Add(new Condition($"{column.FullName} IN ({subSql})"));
        return this;
    }

    public IDeleteWhere WhereExists(IQuery subquery)
    {
        var subSql = IncorporateSubquery(subquery);
        _where.Add(new Condition($"EXISTS ({subSql})"));
        return this;
    }

    private string IncorporateSubquery(IQuery subquery)
    {
        var result = subquery.Build();
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
            _params[newKey] = result.Parameters[oldKey];
            sql = sql.Replace(tempPrefix + oldKey, newKey);
        }

        return sql;
    }

    public IDeleteWhere And(Condition condition)
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

    public IDeleteWhere Or(Condition condition)
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

    public IDeleteFrom From(Table table)
    {
        _fromTable = table;
        return this;
    }

    public IDeleteJoin Join(Table table)
    {
        _pendingJoinType = "INNER JOIN";
        _pendingJoinTable = table;
        return this;
    }

    public IDeleteJoin LeftJoin(Table table)
    {
        _pendingJoinType = "LEFT JOIN";
        _pendingJoinTable = table;
        return this;
    }

    public IDeleteFrom On(Condition condition)
    {
        if (_pendingJoinTable != null && _pendingJoinType != null)
        {
            _joins.Add((_pendingJoinType, _pendingJoinTable, condition));
            _pendingJoinType = null;
            _pendingJoinTable = null;
        }
        return this;
    }

    public IDeleteOutput Output(params IColumn[] columns)
    {
        _outputColumns.AddRange(columns);
        return this;
    }

    public DmlResult Build()
    {
        var sql = new StringBuilder();
        sql.Append($"DELETE FROM {_table.ToSql()}");

        // OUTPUT clause
        if (_outputColumns.Count > 0)
        {
            sql.Append(" OUTPUT ");
            var outputCols = _outputColumns.Select(c => $"DELETED.[{c.ColumnName}]");
            sql.Append(string.Join(", ", outputCols));
        }

        // FROM clause for correlated deletes (SQL Server syntax)
        if (_fromTable != null)
        {
            // SQL Server uses: DELETE t FROM Table t JOIN OtherTable o ON ...
            sql.Clear();
            sql.Append($"DELETE {_table.Alias ?? _table.Name}");

            if (_outputColumns.Count > 0)
            {
                sql.Append(" OUTPUT ");
                var outputCols = _outputColumns.Select(c => $"DELETED.[{c.ColumnName}]");
                sql.Append(string.Join(", ", outputCols));
            }

            sql.Append($" FROM {_table.ToSql()}");
            if (_fromTable.Name != _table.Name)
            {
                sql.Append($", {_fromTable.ToSql()}");
            }
            foreach (var (joinType, table, onCondition) in _joins)
            {
                var (onSql, nextIndex) = onCondition.ToSql(_paramIndex, _params);
                _paramIndex = nextIndex;
                sql.Append($" {joinType} {table.ToSql()} ON {onSql}");
            }
        }

        // WHERE clause
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

        return new DmlResult(sql.ToString(), _params);
    }

    public string ToSql() => Build().Sql;
}

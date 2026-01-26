using System.Text;

namespace Quela.Dml;

/// <summary>
/// Fluent builder for UPDATE statements.
/// </summary>
public interface IUpdate
{
    /// <summary>
    /// Sets a column to a value.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, T? value);

    /// <summary>
    /// Sets a column to another column's value.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, Column<T> sourceColumn);

    /// <summary>
    /// Sets a column to the result of an expression.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, ISelectable expression);
}

/// <summary>
/// After SET, can add more SETs or WHERE clause.
/// </summary>
public interface IUpdateSet : IDmlStatement
{
    /// <summary>
    /// Sets another column.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, T? value);

    /// <summary>
    /// Sets a column to another column's value.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, Column<T> sourceColumn);

    /// <summary>
    /// Sets a column to the result of an expression.
    /// </summary>
    IUpdateSet Set<T>(Column<T> column, ISelectable expression);

    /// <summary>
    /// Add WHERE clause.
    /// </summary>
    IUpdateWhere Where(Condition condition);

    /// <summary>
    /// Join another table for correlated update.
    /// </summary>
    IUpdateFrom From(Table table);
}

/// <summary>
/// Update with FROM clause for correlated updates.
/// </summary>
public interface IUpdateFrom : IUpdateSet
{
    /// <summary>
    /// Join another table.
    /// </summary>
    IUpdateJoin Join(Table table);

    /// <summary>
    /// Left join another table.
    /// </summary>
    IUpdateJoin LeftJoin(Table table);
}

/// <summary>
/// After JOIN, specify ON condition.
/// </summary>
public interface IUpdateJoin
{
    /// <summary>
    /// Specify join condition.
    /// </summary>
    IUpdateFrom On(Condition condition);
}

/// <summary>
/// After WHERE, ready to execute.
/// </summary>
public interface IUpdateWhere : IDmlStatement
{
    /// <summary>
    /// Add AND condition.
    /// </summary>
    IUpdateWhere And(Condition condition);

    /// <summary>
    /// Add OR condition.
    /// </summary>
    IUpdateWhere Or(Condition condition);

    /// <summary>
    /// Return updated values (OUTPUT clause).
    /// </summary>
    IUpdateOutput Output(params IColumn[] columns);
}

/// <summary>
/// After OUTPUT clause.
/// </summary>
public interface IUpdateOutput : IDmlStatement
{
}

/// <summary>
/// Builds UPDATE statements.
/// </summary>
public class UpdateBuilder : IUpdate, IUpdateSet, IUpdateFrom, IUpdateJoin, IUpdateWhere, IUpdateOutput
{
    private readonly Table _table;
    private readonly List<(string Column, string Sql)> _sets = new();
    private readonly List<Condition> _where = new();
    private Table? _fromTable;
    private readonly List<(string JoinType, Table Table, Condition On)> _joins = new();
    private string? _pendingJoinType;
    private Table? _pendingJoinTable;
    private readonly List<IColumn> _outputColumns = new();
    private readonly Dictionary<string, object?> _params = new();
    private int _paramIndex;

    internal UpdateBuilder(Table table)
    {
        _table = table;
    }

    public IUpdateSet Set<T>(Column<T> column, T? value)
    {
        if (value is null)
        {
            _sets.Add((column.ColumnName, "NULL"));
        }
        else
        {
            var paramName = $"@p{_paramIndex++}";
            _params[paramName] = value;
            _sets.Add((column.ColumnName, paramName));
        }
        return this;
    }

    public IUpdateSet Set<T>(Column<T> column, Column<T> sourceColumn)
    {
        _sets.Add((column.ColumnName, sourceColumn.FullName));
        return this;
    }

    public IUpdateSet Set<T>(Column<T> column, ISelectable expression)
    {
        _sets.Add((column.ColumnName, expression.ToSql()));
        return this;
    }

    public IUpdateWhere Where(Condition condition)
    {
        _where.Add(condition);
        return this;
    }

    public IUpdateWhere And(Condition condition)
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

    public IUpdateWhere Or(Condition condition)
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

    public IUpdateFrom From(Table table)
    {
        _fromTable = table;
        return this;
    }

    public IUpdateJoin Join(Table table)
    {
        _pendingJoinType = "INNER JOIN";
        _pendingJoinTable = table;
        return this;
    }

    public IUpdateJoin LeftJoin(Table table)
    {
        _pendingJoinType = "LEFT JOIN";
        _pendingJoinTable = table;
        return this;
    }

    public IUpdateFrom On(Condition condition)
    {
        if (_pendingJoinTable != null && _pendingJoinType != null)
        {
            _joins.Add((_pendingJoinType, _pendingJoinTable, condition));
            _pendingJoinType = null;
            _pendingJoinTable = null;
        }
        return this;
    }

    public IUpdateOutput Output(params IColumn[] columns)
    {
        _outputColumns.AddRange(columns);
        return this;
    }

    public DmlResult Build()
    {
        var sql = new StringBuilder();
        sql.Append($"UPDATE {_table.ToSql()}");

        // SET clause
        sql.Append(" SET ");
        sql.Append(string.Join(", ", _sets.Select(s => $"[{s.Column}] = {s.Sql}")));

        // OUTPUT clause (before FROM in SQL Server)
        if (_outputColumns.Count > 0)
        {
            sql.Append(" OUTPUT ");
            var outputCols = new List<string>();
            foreach (var col in _outputColumns)
            {
                outputCols.Add($"INSERTED.[{col.ColumnName}]");
            }
            sql.Append(string.Join(", ", outputCols));
        }

        // FROM clause for correlated updates
        if (_fromTable != null)
        {
            sql.Append($" FROM {_fromTable.ToSql()}");
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

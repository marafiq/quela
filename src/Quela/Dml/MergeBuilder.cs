using System.Text;

namespace Quela.Dml;

/// <summary>
/// Fluent builder for MERGE statements (SQL Server upsert).
/// </summary>
public interface IMergeInto
{
    /// <summary>
    /// Specify the source table.
    /// </summary>
    IMergeUsing Using(Table sourceTable);

    /// <summary>
    /// Specify a source subquery.
    /// </summary>
    IMergeUsing Using(IQuery sourceQuery, string alias);

    /// <summary>
    /// Specify source values directly (for single-row upserts).
    /// </summary>
    IMergeUsingValues UsingValues();
}

/// <summary>
/// After USING, specify the join condition.
/// </summary>
public interface IMergeUsing
{
    /// <summary>
    /// Specify the ON condition for matching.
    /// </summary>
    IMergeOn On(Condition condition);
}

/// <summary>
/// For specifying values directly.
/// </summary>
public interface IMergeUsingValues
{
    /// <summary>
    /// Set a column value for the source.
    /// </summary>
    IMergeUsingValues Set<T>(Column<T> column, T value);

    /// <summary>
    /// Set a column to NULL in the source.
    /// </summary>
    IMergeUsingValues SetNull(IColumn column);

    /// <summary>
    /// Specify the ON condition after all values are set.
    /// </summary>
    IMergeOn On(Condition condition);
}

/// <summary>
/// After ON, specify WHEN MATCHED/NOT MATCHED actions.
/// </summary>
public interface IMergeOn
{
    /// <summary>
    /// Action when target row matches source.
    /// </summary>
    IMergeWhenMatched WhenMatched();

    /// <summary>
    /// Action when target row matches source and condition is true.
    /// </summary>
    IMergeWhenMatched WhenMatchedAnd(Condition condition);

    /// <summary>
    /// Action when source row has no match in target.
    /// </summary>
    IMergeWhenNotMatched WhenNotMatchedByTarget();

    /// <summary>
    /// Action when target row has no match in source.
    /// </summary>
    IMergeWhenNotMatchedBySource WhenNotMatchedBySource();
}

/// <summary>
/// Specify action for matched rows.
/// </summary>
public interface IMergeWhenMatched
{
    /// <summary>
    /// Update matched rows.
    /// </summary>
    IMergeUpdate ThenUpdate();

    /// <summary>
    /// Delete matched rows.
    /// </summary>
    IMergeThen ThenDelete();
}

/// <summary>
/// Specify action for non-matched rows (insert).
/// </summary>
public interface IMergeWhenNotMatched
{
    /// <summary>
    /// Insert non-matched rows.
    /// </summary>
    IMergeInsert ThenInsert(params IColumn[] columns);
}

/// <summary>
/// Specify action for rows not matched by source (delete or update).
/// </summary>
public interface IMergeWhenNotMatchedBySource
{
    /// <summary>
    /// Delete rows not in source.
    /// </summary>
    IMergeThen ThenDelete();

    /// <summary>
    /// Update rows not in source.
    /// </summary>
    IMergeUpdate ThenUpdate();
}

/// <summary>
/// Specify UPDATE SET values.
/// </summary>
public interface IMergeUpdate
{
    /// <summary>
    /// Set a column to a value.
    /// </summary>
    IMergeUpdate Set<T>(Column<T> column, T value);

    /// <summary>
    /// Set a column to NULL.
    /// </summary>
    IMergeUpdate SetNull(IColumn column);

    /// <summary>
    /// Set a column to the source column value.
    /// </summary>
    IMergeUpdate Set<T>(Column<T> column, Column<T> sourceColumn);

    /// <summary>
    /// Add another WHEN clause.
    /// </summary>
    IMergeOn And();

    /// <summary>
    /// Finish the MERGE.
    /// </summary>
    IMergeFinal Build();
}

/// <summary>
/// Specify INSERT VALUES.
/// </summary>
public interface IMergeInsert
{
    /// <summary>
    /// Specify values to insert from source.
    /// </summary>
    IMergeThen Values(params ISelectable[] values);

    /// <summary>
    /// Use same columns from source.
    /// </summary>
    IMergeThen ValuesFromSource();
}

/// <summary>
/// After a THEN clause, can add more conditions or finish.
/// </summary>
public interface IMergeThen
{
    /// <summary>
    /// Add another WHEN clause.
    /// </summary>
    IMergeOn And();

    /// <summary>
    /// Finish and get the statement.
    /// </summary>
    IMergeFinal Build();
}

/// <summary>
/// Final MERGE ready to execute.
/// </summary>
public interface IMergeFinal : IDmlStatement
{
    /// <summary>
    /// Add OUTPUT clause.
    /// </summary>
    IMergeFinal Output(params IColumn[] columns);
}

/// <summary>
/// Builds MERGE statements.
/// </summary>
public class MergeBuilder :
    IMergeInto, IMergeUsing, IMergeUsingValues, IMergeOn,
    IMergeWhenMatched, IMergeWhenNotMatched, IMergeWhenNotMatchedBySource,
    IMergeUpdate, IMergeInsert, IMergeThen, IMergeFinal
{
    private readonly Table _targetTable;
    private Table? _sourceTable;
    private IQuery? _sourceQuery;
    private string? _sourceAlias;
    private readonly List<(IColumn Column, string Value)> _sourceValues = new();
    private Condition? _onCondition;
    private readonly List<MergeAction> _actions = new();
    private MergeAction? _currentAction;
    private readonly List<IColumn> _outputColumns = new();
    private readonly Dictionary<string, object?> _params = new();
    private int _paramIndex;

    private enum MergeActionType { Update, Delete, Insert }
    private enum MergeMatchType { Matched, NotMatchedByTarget, NotMatchedBySource }

    private class MergeAction
    {
        public MergeMatchType MatchType { get; set; }
        public Condition? AndCondition { get; set; }
        public MergeActionType ActionType { get; set; }
        public List<(string Column, string Value)> Sets { get; } = new();
        public List<IColumn> InsertColumns { get; } = new();
        public List<string> InsertValues { get; } = new();
        public bool UseSourceValues { get; set; }
    }

    internal MergeBuilder(Table targetTable)
    {
        _targetTable = targetTable;
    }

    public IMergeUsing Using(Table sourceTable)
    {
        _sourceTable = sourceTable;
        return this;
    }

    public IMergeUsing Using(IQuery sourceQuery, string alias)
    {
        _sourceQuery = sourceQuery;
        _sourceAlias = alias;
        return this;
    }

    public IMergeUsingValues UsingValues()
    {
        return this;
    }

    // IMergeUsingValues implementation
    IMergeUsingValues IMergeUsingValues.Set<T>(Column<T> column, T value)
    {
        var paramName = $"@p{_paramIndex++}";
        _params[paramName] = value;
        _sourceValues.Add((column, paramName));
        return this;
    }

    IMergeUsingValues IMergeUsingValues.SetNull(IColumn column)
    {
        _sourceValues.Add((column, "NULL"));
        return this;
    }

    public IMergeOn On(Condition condition)
    {
        _onCondition = condition;
        return this;
    }

    public IMergeWhenMatched WhenMatched()
    {
        _currentAction = new MergeAction { MatchType = MergeMatchType.Matched };
        return this;
    }

    public IMergeWhenMatched WhenMatchedAnd(Condition condition)
    {
        _currentAction = new MergeAction { MatchType = MergeMatchType.Matched, AndCondition = condition };
        return this;
    }

    public IMergeWhenNotMatched WhenNotMatchedByTarget()
    {
        _currentAction = new MergeAction { MatchType = MergeMatchType.NotMatchedByTarget };
        return this;
    }

    public IMergeWhenNotMatchedBySource WhenNotMatchedBySource()
    {
        _currentAction = new MergeAction { MatchType = MergeMatchType.NotMatchedBySource };
        return this;
    }

    public IMergeUpdate ThenUpdate()
    {
        if (_currentAction != null)
        {
            _currentAction.ActionType = MergeActionType.Update;
        }
        return this;
    }

    public IMergeThen ThenDelete()
    {
        if (_currentAction != null)
        {
            _currentAction.ActionType = MergeActionType.Delete;
            _actions.Add(_currentAction);
            _currentAction = null;
        }
        return this;
    }

    public IMergeInsert ThenInsert(params IColumn[] columns)
    {
        if (_currentAction != null)
        {
            _currentAction.ActionType = MergeActionType.Insert;
            _currentAction.InsertColumns.AddRange(columns);
        }
        return this;
    }

    // IMergeUpdate implementation
    IMergeUpdate IMergeUpdate.Set<T>(Column<T> column, T value)
    {
        if (_currentAction != null)
        {
            var paramName = $"@p{_paramIndex++}";
            _params[paramName] = value;
            _currentAction.Sets.Add((column.ColumnName, paramName));
        }
        return this;
    }

    IMergeUpdate IMergeUpdate.SetNull(IColumn column)
    {
        if (_currentAction != null)
        {
            _currentAction.Sets.Add((column.ColumnName, "NULL"));
        }
        return this;
    }

    IMergeUpdate IMergeUpdate.Set<T>(Column<T> column, Column<T> sourceColumn)
    {
        if (_currentAction != null)
        {
            _currentAction.Sets.Add((column.ColumnName, $"SOURCE.[{sourceColumn.ColumnName}]"));
        }
        return this;
    }

    IMergeOn IMergeUpdate.And()
    {
        if (_currentAction != null)
        {
            _actions.Add(_currentAction);
            _currentAction = null;
        }
        return this;
    }

    IMergeFinal IMergeUpdate.Build()
    {
        if (_currentAction != null)
        {
            _actions.Add(_currentAction);
            _currentAction = null;
        }
        return this;
    }

    public IMergeThen Values(params ISelectable[] values)
    {
        if (_currentAction != null)
        {
            _currentAction.InsertValues.AddRange(values.Select(v => v.ToSql()));
            _actions.Add(_currentAction);
            _currentAction = null;
        }
        return this;
    }

    public IMergeThen ValuesFromSource()
    {
        if (_currentAction != null)
        {
            _currentAction.UseSourceValues = true;
            _actions.Add(_currentAction);
            _currentAction = null;
        }
        return this;
    }

    IMergeOn IMergeThen.And()
    {
        return this;
    }

    IMergeFinal IMergeThen.Build()
    {
        return this;
    }

    public IMergeFinal Output(params IColumn[] columns)
    {
        _outputColumns.AddRange(columns);
        return this;
    }

    public DmlResult Build()
    {
        var sql = new StringBuilder();

        // MERGE INTO target
        sql.Append($"MERGE INTO {_targetTable.ToSql()} AS TARGET");

        // USING source
        sql.Append(" USING ");
        if (_sourceQuery != null)
        {
            var subSql = IncorporateSubquery(_sourceQuery);
            sql.Append($"({subSql}) AS [{_sourceAlias}]");
        }
        else if (_sourceTable != null)
        {
            sql.Append($"{_sourceTable.ToSql()} AS SOURCE");
        }
        else if (_sourceValues.Count > 0)
        {
            sql.Append("(VALUES (");
            sql.Append(string.Join(", ", _sourceValues.Select(v => v.Value)));
            sql.Append(")) AS SOURCE (");
            sql.Append(string.Join(", ", _sourceValues.Select(v => $"[{v.Column.ColumnName}]")));
            sql.Append(")");
        }

        // ON condition
        if (_onCondition != null)
        {
            var (onSql, nextIndex) = _onCondition.ToSql(_paramIndex, _params);
            _paramIndex = nextIndex;
            sql.Append($" ON {onSql}");
        }

        // WHEN clauses
        foreach (var action in _actions)
        {
            switch (action.MatchType)
            {
                case MergeMatchType.Matched:
                    sql.Append(" WHEN MATCHED");
                    if (action.AndCondition != null)
                    {
                        var (andSql, nextIndex) = action.AndCondition.ToSql(_paramIndex, _params);
                        _paramIndex = nextIndex;
                        sql.Append($" AND {andSql}");
                    }
                    break;
                case MergeMatchType.NotMatchedByTarget:
                    sql.Append(" WHEN NOT MATCHED BY TARGET");
                    break;
                case MergeMatchType.NotMatchedBySource:
                    sql.Append(" WHEN NOT MATCHED BY SOURCE");
                    break;
            }

            sql.Append(" THEN ");

            switch (action.ActionType)
            {
                case MergeActionType.Update:
                    sql.Append("UPDATE SET ");
                    sql.Append(string.Join(", ", action.Sets.Select(s => $"[{s.Column}] = {s.Value}")));
                    break;
                case MergeActionType.Delete:
                    sql.Append("DELETE");
                    break;
                case MergeActionType.Insert:
                    sql.Append("INSERT (");
                    sql.Append(string.Join(", ", action.InsertColumns.Select(c => $"[{c.ColumnName}]")));
                    sql.Append(") VALUES (");
                    if (action.UseSourceValues)
                    {
                        sql.Append(string.Join(", ", action.InsertColumns.Select(c => $"SOURCE.[{c.ColumnName}]")));
                    }
                    else
                    {
                        sql.Append(string.Join(", ", action.InsertValues));
                    }
                    sql.Append(")");
                    break;
            }
        }

        // OUTPUT clause
        if (_outputColumns.Count > 0)
        {
            sql.Append(" OUTPUT ");
            var outputParts = new List<string>();
            foreach (var col in _outputColumns)
            {
                outputParts.Add($"INSERTED.[{col.ColumnName}]");
            }
            sql.Append(string.Join(", ", outputParts));
        }

        // MERGE must end with semicolon
        sql.Append(";");

        return new DmlResult(sql.ToString(), _params);
    }

    public string ToSql() => Build().Sql;

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
}

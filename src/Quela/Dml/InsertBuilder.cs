using System.Text;

namespace Quela.Dml;

/// <summary>
/// Fluent builder for INSERT statements.
/// </summary>
public interface IInsertInto
{
    /// <summary>
    /// Specifies columns to insert.
    /// </summary>
    IInsertColumns Columns(params IColumn[] columns);

    /// <summary>
    /// Insert all columns from a SELECT query.
    /// </summary>
    IInsertSelect Select(IQuery query);
}

/// <summary>
/// After specifying columns, provide values.
/// </summary>
public interface IInsertColumns
{
    /// <summary>
    /// Add a row of values.
    /// </summary>
    IInsertValues Values(params object?[] values);

    /// <summary>
    /// Add multiple rows of values (bulk insert).
    /// </summary>
    IInsertValues BulkValues(IEnumerable<object?[]> rows);

    /// <summary>
    /// Insert from a SELECT query.
    /// </summary>
    IInsertSelect Select(IQuery query);
}

/// <summary>
/// After VALUES, can add more rows or execute.
/// </summary>
public interface IInsertValues : IDmlStatement
{
    /// <summary>
    /// Add another row of values.
    /// </summary>
    IInsertValues Values(params object?[] values);

    /// <summary>
    /// Return inserted values (OUTPUT clause).
    /// </summary>
    IInsertOutput Output(params IColumn[] columns);
}

/// <summary>
/// After SELECT, ready to execute.
/// </summary>
public interface IInsertSelect : IDmlStatement
{
    /// <summary>
    /// Return inserted values (OUTPUT clause).
    /// </summary>
    IInsertOutput Output(params IColumn[] columns);
}

/// <summary>
/// After OUTPUT clause, ready to execute.
/// </summary>
public interface IInsertOutput : IDmlStatement
{
}

/// <summary>
/// Builds INSERT statements.
/// </summary>
public class InsertBuilder : IInsertInto, IInsertColumns, IInsertValues, IInsertSelect, IInsertOutput
{
    private readonly Table _table;
    private readonly List<IColumn> _columns = new();
    private readonly List<object?[]> _rows = new();
    private IQuery? _selectQuery;
    private readonly List<IColumn> _outputColumns = new();
    private readonly Dictionary<string, object?> _params = new();
    private int _paramIndex;

    internal InsertBuilder(Table table)
    {
        _table = table;
    }

    public IInsertColumns Columns(params IColumn[] columns)
    {
        _columns.AddRange(columns);
        return this;
    }

    public IInsertValues Values(params object?[] values)
    {
        if (values.Length != _columns.Count && _columns.Count > 0)
            throw new ArgumentException($"Expected {_columns.Count} values, got {values.Length}");
        _rows.Add(values);
        return this;
    }

    public IInsertValues BulkValues(IEnumerable<object?[]> rows)
    {
        foreach (var row in rows)
        {
            if (row.Length != _columns.Count && _columns.Count > 0)
                throw new ArgumentException($"Expected {_columns.Count} values per row");
            _rows.Add(row);
        }
        return this;
    }

    public IInsertSelect Select(IQuery query)
    {
        _selectQuery = query;
        return this;
    }

    public IInsertOutput Output(params IColumn[] columns)
    {
        _outputColumns.AddRange(columns);
        return this;
    }

    public DmlResult Build()
    {
        var sql = new StringBuilder();
        sql.Append($"INSERT INTO {_table.ToSql()}");

        // Column list
        if (_columns.Count > 0)
        {
            sql.Append(" (");
            sql.Append(string.Join(", ", _columns.Select(c => $"[{c.ColumnName}]")));
            sql.Append(")");
        }

        // OUTPUT clause (before VALUES/SELECT in SQL Server)
        if (_outputColumns.Count > 0)
        {
            sql.Append(" OUTPUT ");
            sql.Append(string.Join(", ", _outputColumns.Select(c => $"INSERTED.[{c.ColumnName}]")));
        }

        // VALUES or SELECT
        if (_selectQuery != null)
        {
            sql.Append(" ");
            var selectResult = _selectQuery.Build();
            var selectSql = selectResult.Sql;

            // Remap parameter names to avoid collisions using two-pass approach
            var paramList = selectResult.Parameters.Keys
                .Where(k => k.StartsWith("@p"))
                .Select(k => (Key: k, Num: int.TryParse(k.Substring(2), out var n) ? n : -1))
                .OrderByDescending(x => x.Num)
                .ToList();

            // First pass: replace with temporary placeholders (highest numbers first to avoid substring issues)
            var tempPrefix = $"__temp_{Guid.NewGuid():N}_";
            foreach (var (oldKey, _) in paramList)
            {
                selectSql = selectSql.Replace(oldKey, tempPrefix + oldKey);
            }

            // Second pass: replace temp placeholders with new names (lowest numbers first for correct ordering)
            foreach (var (oldKey, _) in paramList.OrderBy(x => x.Num))
            {
                var newKey = $"@p{_paramIndex++}";
                _params[newKey] = selectResult.Parameters[oldKey];
                selectSql = selectSql.Replace(tempPrefix + oldKey, newKey);
            }
            sql.Append(selectSql);
        }
        else if (_rows.Count > 0)
        {
            sql.Append(" VALUES ");
            var rowSqls = new List<string>();
            foreach (var row in _rows)
            {
                var valueSqls = new List<string>();
                foreach (var value in row)
                {
                    if (value is null)
                    {
                        valueSqls.Add("NULL");
                    }
                    else
                    {
                        var paramName = $"@p{_paramIndex++}";
                        _params[paramName] = value;
                        valueSqls.Add(paramName);
                    }
                }
                rowSqls.Add($"({string.Join(", ", valueSqls)})");
            }
            sql.Append(string.Join(", ", rowSqls));
        }
        else
        {
            sql.Append(" DEFAULT VALUES");
        }

        return new DmlResult(sql.ToString(), _params);
    }

    public string ToSql() => Build().Sql;
}

/// <summary>
/// Typed INSERT builder for compile-time column checking.
/// </summary>
public class InsertBuilder<TTable> : IInsertInto, IInsertColumns, IInsertValues, IInsertSelect, IInsertOutput
    where TTable : class
{
    private readonly InsertBuilder _inner;

    internal InsertBuilder(Table table)
    {
        _inner = new InsertBuilder(table);
    }

    public IInsertColumns Columns(params IColumn[] columns) => _inner.Columns(columns);
    public IInsertValues Values(params object?[] values) => _inner.Values(values);
    public IInsertValues BulkValues(IEnumerable<object?[]> rows) => _inner.BulkValues(rows);
    public IInsertSelect Select(IQuery query) => _inner.Select(query);
    public IInsertOutput Output(params IColumn[] columns) => _inner.Output(columns);
    public DmlResult Build() => _inner.Build();
    public string ToSql() => _inner.ToSql();
}

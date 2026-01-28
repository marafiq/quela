namespace Quela;

/// <summary>
/// Represents a table-valued function that can be used with CROSS APPLY / OUTER APPLY.
/// </summary>
public class TableValuedFunction
{
    private readonly string _sql;

    private TableValuedFunction(string sql)
    {
        _sql = sql;
    }

    /// <summary>
    /// Creates a raw table-valued function expression.
    /// </summary>
    public static TableValuedFunction Raw(string sql) => new(sql);

    /// <summary>
    /// OPENJSON with default schema (key, value, type columns).
    /// </summary>
    public static TableValuedFunction OpenJson(IColumn jsonColumn)
        => new($"OPENJSON({((ISelectable)jsonColumn).ToSql()})");

    /// <summary>
    /// OPENJSON with a JSON path.
    /// </summary>
    public static TableValuedFunction OpenJson(IColumn jsonColumn, string path)
        => new($"OPENJSON({((ISelectable)jsonColumn).ToSql()}, N'{path}')");

    /// <summary>
    /// OPENJSON with default schema using a string expression.
    /// </summary>
    public static TableValuedFunction OpenJson(string jsonExpression)
        => new($"OPENJSON({jsonExpression})");

    /// <summary>
    /// OPENJSON with explicit schema using WITH clause.
    /// </summary>
    public static OpenJsonBuilder OpenJsonWith(IColumn jsonColumn)
        => new(((ISelectable)jsonColumn).ToSql(), null);

    /// <summary>
    /// OPENJSON with explicit schema using WITH clause and JSON path.
    /// </summary>
    public static OpenJsonBuilder OpenJsonWith(IColumn jsonColumn, string path)
        => new(((ISelectable)jsonColumn).ToSql(), path);

    /// <summary>
    /// OPENJSON with explicit schema using WITH clause from string expression.
    /// </summary>
    public static OpenJsonBuilder OpenJsonWith(string jsonExpression)
        => new(jsonExpression, null);

    /// <summary>
    /// STRING_SPLIT table-valued function.
    /// </summary>
    public static TableValuedFunction StringSplit(IColumn column, string separator)
        => new($"STRING_SPLIT({((ISelectable)column).ToSql()}, N'{separator}')");

    /// <summary>
    /// STRING_SPLIT table-valued function with ordinal (SQL Server 2022+).
    /// </summary>
    public static TableValuedFunction StringSplitWithOrdinal(IColumn column, string separator)
        => new($"STRING_SPLIT({((ISelectable)column).ToSql()}, N'{separator}', 1)");

    /// <summary>
    /// STRING_SPLIT table-valued function from string expression.
    /// </summary>
    public static TableValuedFunction StringSplit(string expression, string separator)
        => new($"STRING_SPLIT({expression}, N'{separator}')");

    /// <summary>
    /// GENERATE_SERIES table-valued function (SQL Server 2022+).
    /// </summary>
    public static TableValuedFunction GenerateSeries(int start, int stop)
        => new($"GENERATE_SERIES({start}, {stop})");

    /// <summary>
    /// GENERATE_SERIES table-valued function with step (SQL Server 2022+).
    /// </summary>
    public static TableValuedFunction GenerateSeries(int start, int stop, int step)
        => new($"GENERATE_SERIES({start}, {stop}, {step})");

    public string ToSql() => _sql;

    public override string ToString() => _sql;
}

/// <summary>
/// Builder for OPENJSON WITH clause.
/// </summary>
public class OpenJsonBuilder
{
    private readonly string _jsonExpression;
    private readonly string? _path;
    private readonly List<OpenJsonColumn> _columns = new();

    internal OpenJsonBuilder(string jsonExpression, string? path)
    {
        _jsonExpression = jsonExpression;
        _path = path;
    }

    /// <summary>
    /// Adds a column to the WITH schema.
    /// </summary>
    public OpenJsonBuilder Column(string name, string sqlType)
    {
        _columns.Add(new OpenJsonColumn(name, sqlType, null, false));
        return this;
    }

    /// <summary>
    /// Adds a column with a JSON path to the WITH schema.
    /// </summary>
    public OpenJsonBuilder Column(string name, string sqlType, string jsonPath)
    {
        _columns.Add(new OpenJsonColumn(name, sqlType, jsonPath, false));
        return this;
    }

    /// <summary>
    /// Adds a column with AS JSON modifier for nested JSON.
    /// </summary>
    public OpenJsonBuilder ColumnAsJson(string name, string jsonPath)
    {
        _columns.Add(new OpenJsonColumn(name, "NVARCHAR(MAX)", jsonPath, true));
        return this;
    }

    /// <summary>
    /// Builds the OPENJSON WITH expression.
    /// </summary>
    public TableValuedFunction Build()
    {
        var pathClause = _path != null ? $", N'{_path}'" : "";
        var withClause = string.Join(", ", _columns.Select(c => c.ToSql()));
        return TableValuedFunction.Raw($"OPENJSON({_jsonExpression}{pathClause}) WITH ({withClause})");
    }

    private record OpenJsonColumn(string Name, string SqlType, string? JsonPath, bool AsJson)
    {
        public string ToSql()
        {
            var pathPart = JsonPath != null ? $" '{JsonPath}'" : "";
            var asJsonPart = AsJson ? " AS JSON" : "";
            return $"[{Name}] {SqlType}{pathPart}{asJsonPart}";
        }
    }
}

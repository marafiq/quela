namespace Quela;

/// <summary>
/// SQL functions - aggregates, string, date, math, etc.
/// </summary>
public static class Fn
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Aggregate Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>COUNT(*)</summary>
    public static Aggregate<int> Count() => new("COUNT(*)");

    /// <summary>COUNT(column)</summary>
    public static Aggregate<int> Count<T>(Column<T> col) => new($"COUNT({col.FullName})");

    /// <summary>COUNT(DISTINCT column)</summary>
    public static Aggregate<int> CountDistinct<T>(Column<T> col) => new($"COUNT(DISTINCT {col.FullName})");

    /// <summary>SUM(column)</summary>
    public static Aggregate<T> Sum<T>(Column<T> col) where T : struct => new($"SUM({col.FullName})");

    /// <summary>SUM(DISTINCT column)</summary>
    public static Aggregate<T> SumDistinct<T>(Column<T> col) where T : struct => new($"SUM(DISTINCT {col.FullName})");

    /// <summary>AVG(column)</summary>
    public static Aggregate<decimal> Avg<T>(Column<T> col) where T : struct => new($"AVG({col.FullName})");

    /// <summary>AVG(DISTINCT column)</summary>
    public static Aggregate<decimal> AvgDistinct<T>(Column<T> col) where T : struct => new($"AVG(DISTINCT {col.FullName})");

    /// <summary>MIN(column)</summary>
    public static Aggregate<T> Min<T>(Column<T> col) => new($"MIN({col.FullName})");

    /// <summary>MAX(column)</summary>
    public static Aggregate<T> Max<T>(Column<T> col) => new($"MAX({col.FullName})");

    /// <summary>STDEV(column)</summary>
    public static Aggregate<double> StDev<T>(Column<T> col) where T : struct => new($"STDEV({col.FullName})");

    /// <summary>STDEVP(column)</summary>
    public static Aggregate<double> StDevP<T>(Column<T> col) where T : struct => new($"STDEVP({col.FullName})");

    /// <summary>VAR(column)</summary>
    public static Aggregate<double> Var<T>(Column<T> col) where T : struct => new($"VAR({col.FullName})");

    /// <summary>VARP(column)</summary>
    public static Aggregate<double> VarP<T>(Column<T> col) where T : struct => new($"VARP({col.FullName})");

    /// <summary>STRING_AGG(column, separator)</summary>
    public static StringAggBuilder StringAgg(Column<string> col, string separator)
        => new(col.FullName, separator);

    /// <summary>CHECKSUM_AGG(column)</summary>
    public static Aggregate<int> ChecksumAgg<T>(Column<T> col) where T : struct => new($"CHECKSUM_AGG({col.FullName})");

    /// <summary>APPROX_COUNT_DISTINCT(column) - SQL Server 2019+</summary>
    public static Aggregate<long> ApproxCountDistinct<T>(Column<T> col) => new($"APPROX_COUNT_DISTINCT({col.FullName})");

    // ═══════════════════════════════════════════════════════════════════════════
    // NULL Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>ISNULL(column, replacement)</summary>
    public static Expression<T> IsNull<T>(Column<T> col, T replacement)
        => new($"ISNULL({col.FullName}, {FormatValue(replacement)})");

    /// <summary>COALESCE(columns...)</summary>
    public static Expression<T> Coalesce<T>(params Column<T>[] columns)
        => new($"COALESCE({string.Join(", ", columns.Select(c => c.FullName))})");

    /// <summary>NULLIF(column, value)</summary>
    public static Expression<T?> NullIf<T>(Column<T> col, T value)
        => new($"NULLIF({col.FullName}, {FormatValue(value)})");

    // ═══════════════════════════════════════════════════════════════════════════
    // String Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>CONCAT(values...)</summary>
    public static Expression<string> Concat(params ISelectable[] values)
        => new($"CONCAT({string.Join(", ", values.Select(v => v.ToSql()))})");

    /// <summary>CONCAT_WS(separator, values...)</summary>
    public static Expression<string> ConcatWs(string separator, params ISelectable[] values)
        => new($"CONCAT_WS(N'{separator}', {string.Join(", ", values.Select(v => v.ToSql()))})");

    /// <summary>LEFT(string, length)</summary>
    public static Expression<string> Left(Column<string> col, int length)
        => new($"LEFT({col.FullName}, {length})");

    /// <summary>RIGHT(string, length)</summary>
    public static Expression<string> Right(Column<string> col, int length)
        => new($"RIGHT({col.FullName}, {length})");

    /// <summary>SUBSTRING(string, start, length)</summary>
    public static Expression<string> Substring(Column<string> col, int start, int length)
        => new($"SUBSTRING({col.FullName}, {start}, {length})");

    /// <summary>LEN(string)</summary>
    public static Expression<int> Len(Column<string> col)
        => new($"LEN({col.FullName})");

    /// <summary>DATALENGTH(expression)</summary>
    public static Expression<int> DataLength<T>(Column<T> col)
        => new($"DATALENGTH({col.FullName})");

    /// <summary>UPPER(string)</summary>
    public static Expression<string> Upper(Column<string> col)
        => new($"UPPER({col.FullName})");

    /// <summary>LOWER(string)</summary>
    public static Expression<string> Lower(Column<string> col)
        => new($"LOWER({col.FullName})");

    /// <summary>LTRIM(string)</summary>
    public static Expression<string> LTrim(Column<string> col)
        => new($"LTRIM({col.FullName})");

    /// <summary>RTRIM(string)</summary>
    public static Expression<string> RTrim(Column<string> col)
        => new($"RTRIM({col.FullName})");

    /// <summary>TRIM(string)</summary>
    public static Expression<string> Trim(Column<string> col)
        => new($"TRIM({col.FullName})");

    /// <summary>REPLACE(string, old, new)</summary>
    public static Expression<string> Replace(Column<string> col, string oldValue, string newValue)
        => new($"REPLACE({col.FullName}, N'{oldValue}', N'{newValue}')");

    /// <summary>STUFF(string, start, length, replacement)</summary>
    public static Expression<string> Stuff(Column<string> col, int start, int length, string replacement)
        => new($"STUFF({col.FullName}, {start}, {length}, N'{replacement}')");

    /// <summary>CHARINDEX(substring, string)</summary>
    public static Expression<int> CharIndex(string substring, Column<string> col)
        => new($"CHARINDEX(N'{substring}', {col.FullName})");

    /// <summary>PATINDEX(pattern, string)</summary>
    public static Expression<int> PatIndex(string pattern, Column<string> col)
        => new($"PATINDEX(N'{pattern}', {col.FullName})");

    /// <summary>REVERSE(string)</summary>
    public static Expression<string> Reverse(Column<string> col)
        => new($"REVERSE({col.FullName})");

    /// <summary>REPLICATE(string, count)</summary>
    public static Expression<string> Replicate(Column<string> col, int count)
        => new($"REPLICATE({col.FullName}, {count})");

    /// <summary>SPACE(count)</summary>
    public static Expression<string> Space(int count)
        => new($"SPACE({count})");

    /// <summary>FORMAT(value, format, culture)</summary>
    public static Expression<string> Format<T>(Column<T> col, string format, string? culture = null)
        => culture != null
            ? new($"FORMAT({col.FullName}, N'{format}', N'{culture}')")
            : new($"FORMAT({col.FullName}, N'{format}')");

    // ═══════════════════════════════════════════════════════════════════════════
    // Date/Time Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>GETDATE()</summary>
    public static Expression<DateTime> GetDate() => new("GETDATE()");

    /// <summary>GETUTCDATE()</summary>
    public static Expression<DateTime> GetUtcDate() => new("GETUTCDATE()");

    /// <summary>SYSDATETIME()</summary>
    public static Expression<DateTime> SysDateTime() => new("SYSDATETIME()");

    /// <summary>SYSUTCDATETIME()</summary>
    public static Expression<DateTime> SysUtcDateTime() => new("SYSUTCDATETIME()");

    /// <summary>SYSDATETIMEOFFSET()</summary>
    public static Expression<DateTimeOffset> SysDateTimeOffset() => new("SYSDATETIMEOFFSET()");

    /// <summary>CURRENT_TIMESTAMP</summary>
    public static Expression<DateTime> CurrentTimestamp() => new("CURRENT_TIMESTAMP");

    /// <summary>DATEADD(datepart, number, date)</summary>
    public static Expression<DateTime> DateAdd(DatePart datePart, int number, Column<DateTime> col)
        => new($"DATEADD({datePart.ToSql()}, {number}, {col.FullName})");

    /// <summary>DATEDIFF(datepart, startdate, enddate)</summary>
    public static Expression<int> DateDiff(DatePart datePart, Column<DateTime> startDate, Column<DateTime> endDate)
        => new($"DATEDIFF({datePart.ToSql()}, {startDate.FullName}, {endDate.FullName})");

    /// <summary>DATEDIFF_BIG(datepart, startdate, enddate)</summary>
    public static Expression<long> DateDiffBig(DatePart datePart, Column<DateTime> startDate, Column<DateTime> endDate)
        => new($"DATEDIFF_BIG({datePart.ToSql()}, {startDate.FullName}, {endDate.FullName})");

    /// <summary>DATEPART(datepart, date)</summary>
    public static Expression<int> DatePartFn(DatePart datePart, Column<DateTime> col)
        => new($"DATEPART({datePart.ToSql()}, {col.FullName})");

    /// <summary>DATENAME(datepart, date)</summary>
    public static Expression<string> DateName(DatePart datePart, Column<DateTime> col)
        => new($"DATENAME({datePart.ToSql()}, {col.FullName})");

    /// <summary>YEAR(date)</summary>
    public static Expression<int> Year(Column<DateTime> col) => new($"YEAR({col.FullName})");

    /// <summary>MONTH(date)</summary>
    public static Expression<int> Month(Column<DateTime> col) => new($"MONTH({col.FullName})");

    /// <summary>DAY(date)</summary>
    public static Expression<int> Day(Column<DateTime> col) => new($"DAY({col.FullName})");

    /// <summary>DATEFROMPARTS(year, month, day)</summary>
    public static Expression<DateTime> DateFromParts(int year, int month, int day)
        => new($"DATEFROMPARTS({year}, {month}, {day})");

    /// <summary>EOMONTH(date)</summary>
    public static Expression<DateTime> EOMonth(Column<DateTime> col)
        => new($"EOMONTH({col.FullName})");

    /// <summary>EOMONTH(date, months)</summary>
    public static Expression<DateTime> EOMonth(Column<DateTime> col, int months)
        => new($"EOMONTH({col.FullName}, {months})");

    /// <summary>ISDATE(expression)</summary>
    public static Expression<int> IsDate(Column<string> col)
        => new($"ISDATE({col.FullName})");

    // ═══════════════════════════════════════════════════════════════════════════
    // Math Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>ABS(value)</summary>
    public static Expression<T> Abs<T>(Column<T> col) where T : struct
        => new($"ABS({col.FullName})");

    /// <summary>CEILING(value)</summary>
    public static Expression<T> Ceiling<T>(Column<T> col) where T : struct
        => new($"CEILING({col.FullName})");

    /// <summary>FLOOR(value)</summary>
    public static Expression<T> Floor<T>(Column<T> col) where T : struct
        => new($"FLOOR({col.FullName})");

    /// <summary>ROUND(value, precision)</summary>
    public static Expression<T> Round<T>(Column<T> col, int precision) where T : struct
        => new($"ROUND({col.FullName}, {precision})");

    /// <summary>POWER(value, exponent)</summary>
    public static Expression<double> Power<T>(Column<T> col, double exponent) where T : struct
        => new($"POWER({col.FullName}, {exponent})");

    /// <summary>SQRT(value)</summary>
    public static Expression<double> Sqrt<T>(Column<T> col) where T : struct
        => new($"SQRT({col.FullName})");

    /// <summary>SQUARE(value)</summary>
    public static Expression<double> Square<T>(Column<T> col) where T : struct
        => new($"SQUARE({col.FullName})");

    /// <summary>SIGN(value)</summary>
    public static Expression<int> Sign<T>(Column<T> col) where T : struct
        => new($"SIGN({col.FullName})");

    /// <summary>LOG(value)</summary>
    public static Expression<double> Log<T>(Column<T> col) where T : struct
        => new($"LOG({col.FullName})");

    /// <summary>LOG10(value)</summary>
    public static Expression<double> Log10<T>(Column<T> col) where T : struct
        => new($"LOG10({col.FullName})");

    /// <summary>EXP(value)</summary>
    public static Expression<double> Exp<T>(Column<T> col) where T : struct
        => new($"EXP({col.FullName})");

    /// <summary>PI()</summary>
    public static Expression<double> Pi() => new("PI()");

    /// <summary>RAND()</summary>
    public static Expression<double> Rand() => new("RAND()");

    /// <summary>RAND(seed)</summary>
    public static Expression<double> Rand(int seed) => new($"RAND({seed})");

    // ═══════════════════════════════════════════════════════════════════════════
    // Logical Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>IIF(condition, trueValue, falseValue)</summary>
    public static Expression<T> Iif<T>(Condition condition, T trueValue, T falseValue)
        => new($"IIF({condition.Template}, {FormatValue(trueValue)}, {FormatValue(falseValue)})");

    /// <summary>CHOOSE(index, values...)</summary>
    public static Expression<T> Choose<T>(int index, params T[] values)
        => new($"CHOOSE({index}, {string.Join(", ", values.Select(FormatValue))})");

    /// <summary>GREATEST(values...) - SQL Server 2022+</summary>
    public static Expression<T> Greatest<T>(params Column<T>[] columns)
        => new($"GREATEST({string.Join(", ", columns.Select(c => c.FullName))})");

    /// <summary>LEAST(values...) - SQL Server 2022+</summary>
    public static Expression<T> Least<T>(params Column<T>[] columns)
        => new($"LEAST({string.Join(", ", columns.Select(c => c.FullName))})");

    /// <summary>Starts a CASE expression.</summary>
    public static CaseBuilder Case() => new();

    /// <summary>Starts a simple CASE expression.</summary>
    public static SimpleCaseBuilder<T> Case<T>(Column<T> col) => new(col.FullName);

    // ═══════════════════════════════════════════════════════════════════════════
    // JSON Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>JSON_VALUE(expression, path)</summary>
    public static Expression<string> JsonValue(Column<string> col, string path)
        => new($"JSON_VALUE({col.FullName}, N'{path}')");

    /// <summary>JSON_QUERY(expression, path)</summary>
    public static Expression<string> JsonQuery(Column<string> col, string path)
        => new($"JSON_QUERY({col.FullName}, N'{path}')");

    /// <summary>ISJSON(expression)</summary>
    public static Expression<int> IsJson(Column<string> col)
        => new($"ISJSON({col.FullName})");

    // ═══════════════════════════════════════════════════════════════════════════
    // System Functions
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>NEWID()</summary>
    public static Expression<Guid> NewId() => new("NEWID()");

    /// <summary>NEWSEQUENTIALID()</summary>
    public static Expression<Guid> NewSequentialId() => new("NEWSEQUENTIALID()");

    /// <summary>@@ROWCOUNT</summary>
    public static Expression<int> RowCount() => new("@@ROWCOUNT");

    /// <summary>@@IDENTITY</summary>
    public static Expression<decimal> Identity() => new("@@IDENTITY");

    /// <summary>SCOPE_IDENTITY()</summary>
    public static Expression<decimal> ScopeIdentity() => new("SCOPE_IDENTITY()");

    // ═══════════════════════════════════════════════════════════════════════════
    // Helper
    // ═══════════════════════════════════════════════════════════════════════════

    private static string FormatValue<T>(T value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-ddTHH:mm:ss.fff}'",
            _ => value.ToString()!
        };
    }
}

/// <summary>
/// Represents a SQL expression with a specific result type.
/// </summary>
public class Expression<T> : ISelectable, IGroupable, IOrderable
{
    private readonly string _expression;

    public Expression(string expression)
    {
        _expression = expression;
    }

    public AliasedExpression<T> As(string alias) => new(_expression, alias);

    public OrderedColumn Asc() => new(_expression, ascending: true);
    public OrderedColumn Desc() => new(_expression, ascending: false);

    public string ToSql() => _expression;
}

/// <summary>
/// An expression with an alias.
/// </summary>
public class AliasedExpression<T> : ISelectable
{
    private readonly string _expression;
    private readonly string _alias;

    public AliasedExpression(string expression, string alias)
    {
        _expression = expression;
        _alias = alias;
    }

    public string ToSql() => $"{_expression} AS [{_alias}]";
}

/// <summary>
/// Builder for STRING_AGG with WITHIN GROUP support.
/// </summary>
public class StringAggBuilder
{
    private readonly string _column;
    private readonly string _separator;

    public StringAggBuilder(string column, string separator)
    {
        _column = column;
        _separator = separator;
    }

    public Aggregate<string> WithinGroup(Action<WithinGroupBuilder> configure)
    {
        var builder = new WithinGroupBuilder();
        configure(builder);
        return new Aggregate<string>($"STRING_AGG({_column}, N'{_separator}') WITHIN GROUP ({builder.Build()})");
    }

    public Aggregate<string> Build() => new($"STRING_AGG({_column}, N'{_separator}')");

    public static implicit operator Aggregate<string>(StringAggBuilder builder) => builder.Build();
}

public class WithinGroupBuilder
{
    private readonly List<string> _orderBy = new();

    public WithinGroupBuilder OrderBy(params IOrderable[] columns)
    {
        _orderBy.AddRange(columns.Select(c => c.ToSql()));
        return this;
    }

    public string Build() => $"ORDER BY {string.Join(", ", _orderBy)}";
}

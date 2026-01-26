namespace Quela;

/// <summary>
/// Static entry point for CASE expressions.
/// </summary>
public static class Case
{
    /// <summary>
    /// Starts a searched CASE expression: CASE WHEN condition THEN result...
    /// </summary>
    public static CaseBuilder When(Condition condition, object result)
    {
        var builder = new CaseBuilder();
        return builder.When(condition, result);
    }

    /// <summary>
    /// Starts a simple CASE expression: CASE expression WHEN value THEN result...
    /// </summary>
    public static SimpleCaseBuilder<T> On<T>(Column<T> column)
    {
        return new SimpleCaseBuilder<T>(column.FullName);
    }
}

/// <summary>
/// Builder for CASE WHEN ... THEN ... ELSE ... END expressions.
/// </summary>
public class CaseBuilder
{
    private readonly List<(string When, string Then)> _cases = new();
    private string? _else;

    /// <summary>
    /// Adds a WHEN condition with its result.
    /// </summary>
    public CaseBuilder When(Condition condition, object result)
    {
        _cases.Add((condition.Template, FormatValue(result)));
        return this;
    }

    /// <summary>
    /// Adds the ELSE clause.
    /// </summary>
    public CaseExpression<T> Else<T>(T value)
    {
        _else = FormatValue(value);
        return new CaseExpression<T>(Build());
    }

    /// <summary>
    /// Ends the CASE without ELSE (returns NULL when no match).
    /// </summary>
    public CaseExpression<T> End<T>()
    {
        return new CaseExpression<T>(Build());
    }

    /// <summary>
    /// Ends the CASE without ELSE (returns NULL when no match).
    /// </summary>
    public CaseExpression<object> End()
    {
        return new CaseExpression<object>(Build());
    }

    private string Build()
    {
        var cases = string.Join(" ", _cases.Select(c => $"WHEN {c.When} THEN {c.Then}"));
        var elseClause = _else != null ? $" ELSE {_else}" : "";
        return $"CASE {cases}{elseClause} END";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            ISelectable sel => sel.ToSql(),
            _ => value.ToString()!
        };
    }
}

/// <summary>
/// Builder for simple CASE expression (CASE column WHEN value THEN ...).
/// </summary>
public class SimpleCaseBuilder<TInput>
{
    private readonly string _expression;
    private readonly List<(string When, string Then)> _cases = new();
    private string? _else;

    public SimpleCaseBuilder(string expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Adds a WHEN value with its result.
    /// </summary>
    public SimpleCaseBuilder<TInput> When(TInput value, object result)
    {
        _cases.Add((FormatValue(value), FormatValue(result)));
        return this;
    }

    /// <summary>
    /// Adds the ELSE clause.
    /// </summary>
    public CaseExpression<T> Else<T>(T value)
    {
        _else = FormatValue(value);
        return new CaseExpression<T>(Build());
    }

    /// <summary>
    /// Ends the CASE without ELSE.
    /// </summary>
    public CaseExpression<T> End<T>()
    {
        return new CaseExpression<T>(Build());
    }

    private string Build()
    {
        var cases = string.Join(" ", _cases.Select(c => $"WHEN {c.When} THEN {c.Then}"));
        var elseClause = _else != null ? $" ELSE {_else}" : "";
        return $"CASE {_expression} {cases}{elseClause} END";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            ISelectable sel => sel.ToSql(),
            _ => value.ToString()!
        };
    }
}

/// <summary>
/// A completed CASE expression that can be selected.
/// </summary>
public class CaseExpression<T> : ISelectable, IOrderable
{
    private readonly string _expression;

    public CaseExpression(string expression)
    {
        _expression = expression;
    }

    public AliasedCaseExpression<T> As(string alias) => new(_expression, alias);

    public OrderedColumn Asc() => new(_expression, ascending: true);
    public OrderedColumn Desc() => new(_expression, ascending: false);

    public string ToSql() => _expression;
}

/// <summary>
/// A CASE expression with an alias.
/// </summary>
public class AliasedCaseExpression<T> : ISelectable
{
    private readonly string _expression;
    private readonly string _alias;

    public AliasedCaseExpression(string expression, string alias)
    {
        _expression = expression;
        _alias = alias;
    }

    public string ToSql() => $"{_expression} AS [{_alias}]";
}

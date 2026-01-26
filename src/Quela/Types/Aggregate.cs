namespace Quela;

/// <summary>
/// Represents an aggregate function result (COUNT, SUM, AVG, etc.).
/// </summary>
/// <typeparam name="T">The result type of the aggregate.</typeparam>
public class Aggregate<T> : ISelectable
{
    private readonly string _expression;

    public Aggregate(string expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Aliases the aggregate for SELECT output.
    /// </summary>
    public AliasedAggregate<T> As(string alias) => new(_expression, alias);

    /// <summary>
    /// Aliases the aggregate using a typed column (enforces type match).
    /// </summary>
    public AliasedAggregate<T> As(Column<T> aliasColumn) => new(_expression, aliasColumn.ColumnName);

    /// <summary>
    /// Converts this aggregate to a window function with OVER clause.
    /// </summary>
    public WindowAggregate<T> Over() => new(_expression);

    /// <summary>
    /// Converts this aggregate to a window function with OVER clause configuration.
    /// </summary>
    public WindowedExpression<T> Over(Action<OverClauseBuilder> configure)
    {
        var builder = new OverClauseBuilder();
        configure(builder);
        return new WindowedExpression<T>(_expression, builder.Build());
    }

    // Comparison operators for HAVING clause
    public static Condition operator >(Aggregate<T> agg, T value) =>
        new($"{agg._expression} > @p", value!);

    public static Condition operator <(Aggregate<T> agg, T value) =>
        new($"{agg._expression} < @p", value!);

    public static Condition operator >=(Aggregate<T> agg, T value) =>
        new($"{agg._expression} >= @p", value!);

    public static Condition operator <=(Aggregate<T> agg, T value) =>
        new($"{agg._expression} <= @p", value!);

    public static Condition operator ==(Aggregate<T> agg, T value) =>
        new($"{agg._expression} = @p", value!);

    public static Condition operator !=(Aggregate<T> agg, T value) =>
        new($"{agg._expression} <> @p", value!);

    public OrderedColumn Asc() => new(_expression, ascending: true);
    public OrderedColumn Desc() => new(_expression, ascending: false);

    public string ToSql() => _expression;

    public override bool Equals(object? obj) =>
        obj is Aggregate<T> other && _expression == other._expression;

    public override int GetHashCode() => _expression.GetHashCode();
}

/// <summary>
/// An aggregate with an alias for SELECT output.
/// </summary>
public class AliasedAggregate<T> : ISelectable
{
    private readonly string _expression;
    private readonly string _alias;

    public AliasedAggregate(string expression, string alias)
    {
        _expression = expression;
        _alias = alias;
    }

    public string ToSql() => $"{_expression} AS [{_alias}]";
}

/// <summary>
/// An aggregate that can have an OVER clause added.
/// </summary>
public class WindowAggregate<T> : ISelectable
{
    private readonly string _expression;

    public WindowAggregate(string expression)
    {
        _expression = expression;
    }

    /// <summary>
    /// Adds an OVER clause with configuration.
    /// </summary>
    public WindowedExpression<T> Configure(Action<OverClauseBuilder> configure)
    {
        var builder = new OverClauseBuilder();
        configure(builder);
        return new WindowedExpression<T>(_expression, builder.Build());
    }

    public AliasedWindowedExpression<T> As(string alias) =>
        new($"{_expression} OVER ()", alias);

    public string ToSql() => $"{_expression} OVER ()";
}

namespace Quela;

/// <summary>
/// Represents a window function (ROW_NUMBER, RANK, LAG, etc.).
/// </summary>
/// <typeparam name="T">The result type of the window function.</typeparam>
public class WindowFunction<T> : ISelectable
{
    private readonly string _function;

    public WindowFunction(string function)
    {
        _function = function;
    }

    /// <summary>
    /// Configures the OVER clause for this window function.
    /// </summary>
    public WindowedExpression<T> Over(Action<OverClauseBuilder> configure)
    {
        var builder = new OverClauseBuilder();
        configure(builder);
        return new WindowedExpression<T>(_function, builder.Build());
    }

    public string ToSql() => $"{_function} OVER ()";
}

/// <summary>
/// A window function or aggregate with a configured OVER clause.
/// </summary>
public class WindowedExpression<T> : ISelectable
{
    private readonly string _expression;
    private readonly string _overClause;

    public WindowedExpression(string expression, string overClause)
    {
        _expression = expression;
        _overClause = overClause;
    }

    /// <summary>
    /// Aliases the windowed expression for SELECT output.
    /// </summary>
    public AliasedWindowedExpression<T> As(string alias) =>
        new($"{_expression} OVER ({_overClause})", alias);

    public string ToSql() => $"{_expression} OVER ({_overClause})";
}

/// <summary>
/// A windowed expression with an alias.
/// </summary>
public class AliasedWindowedExpression<T> : ISelectable
{
    private readonly string _expression;
    private readonly string _alias;

    public AliasedWindowedExpression(string expression, string alias)
    {
        _expression = expression;
        _alias = alias;
    }

    public string ToSql() => $"{_expression} AS [{_alias}]";
}

/// <summary>
/// Builder for constructing OVER clauses.
/// </summary>
public class OverClauseBuilder
{
    private readonly List<string> _partitionBy = new();
    private readonly List<string> _orderBy = new();
    private string? _frameClause;

    /// <summary>
    /// Adds PARTITION BY columns.
    /// </summary>
    public OverClauseBuilder PartitionBy(params IColumn[] columns)
    {
        _partitionBy.AddRange(columns.Select(c => c.FullName));
        return this;
    }

    /// <summary>
    /// Adds ORDER BY columns.
    /// </summary>
    public OverClauseBuilder OrderBy(params IOrderable[] columns)
    {
        _orderBy.AddRange(columns.Select(c => c.ToSql()));
        return this;
    }

    /// <summary>
    /// Starts a ROWS frame specification.
    /// </summary>
    public FrameBuilder Rows() => new(this, "ROWS");

    /// <summary>
    /// Starts a RANGE frame specification.
    /// </summary>
    public FrameBuilder Range() => new(this, "RANGE");

    internal void SetFrame(string frame)
    {
        _frameClause = frame;
    }

    public string Build()
    {
        var parts = new List<string>();

        if (_partitionBy.Count > 0)
            parts.Add($"PARTITION BY {string.Join(", ", _partitionBy)}");

        if (_orderBy.Count > 0)
            parts.Add($"ORDER BY {string.Join(", ", _orderBy)}");

        if (_frameClause != null)
            parts.Add(_frameClause);

        return string.Join(" ", parts);
    }
}

/// <summary>
/// Builder for window frame specifications (ROWS/RANGE).
/// </summary>
public class FrameBuilder
{
    private readonly OverClauseBuilder _parent;
    private readonly string _frameType;

    public FrameBuilder(OverClauseBuilder parent, string frameType)
    {
        _parent = parent;
        _frameType = frameType;
    }

    public FrameBoundBuilder Unbounded() => new(_parent, _frameType, "UNBOUNDED");
    public FrameBoundBuilder CurrentRow() => new(_parent, _frameType, "CURRENT ROW", isComplete: true);
    public FrameBoundBuilder Value(int n) => new(_parent, _frameType, n.ToString());

    public BetweenFrameBuilder Between() => new(_parent, _frameType);
}

/// <summary>
/// Builder for frame bound (PRECEDING/FOLLOWING).
/// </summary>
public class FrameBoundBuilder
{
    private readonly OverClauseBuilder _parent;
    private readonly string _frameType;
    private readonly string _value;
    private readonly bool _isComplete;

    public FrameBoundBuilder(OverClauseBuilder parent, string frameType, string value, bool isComplete = false)
    {
        _parent = parent;
        _frameType = frameType;
        _value = value;
        _isComplete = isComplete;

        if (isComplete)
        {
            _parent.SetFrame($"{_frameType} {_value}");
        }
    }

    public OverClauseBuilder Preceding()
    {
        _parent.SetFrame($"{_frameType} {_value} PRECEDING");
        return _parent;
    }

    public OverClauseBuilder Following()
    {
        _parent.SetFrame($"{_frameType} {_value} FOLLOWING");
        return _parent;
    }
}

/// <summary>
/// Builder for BETWEEN frame specifications.
/// </summary>
public class BetweenFrameBuilder
{
    private readonly OverClauseBuilder _parent;
    private readonly string _frameType;
    private string? _startBound;

    public BetweenFrameBuilder(OverClauseBuilder parent, string frameType)
    {
        _parent = parent;
        _frameType = frameType;
    }

    public BetweenFrameStartBuilder Unbounded() => new(this, "UNBOUNDED");
    public BetweenFrameStartBuilder CurrentRow() => new(this, "CURRENT ROW", isComplete: true);
    public BetweenFrameStartBuilder Value(int n) => new(this, n.ToString());

    internal void SetStart(string bound)
    {
        _startBound = bound;
    }

    internal void Complete(string endBound)
    {
        _parent.SetFrame($"{_frameType} BETWEEN {_startBound} AND {endBound}");
    }
}

public class BetweenFrameStartBuilder
{
    private readonly BetweenFrameBuilder _parent;
    private readonly string _value;
    private readonly bool _isComplete;

    public BetweenFrameStartBuilder(BetweenFrameBuilder parent, string value, bool isComplete = false)
    {
        _parent = parent;
        _value = value;
        _isComplete = isComplete;

        if (isComplete)
            _parent.SetStart(value);
    }

    public BetweenFrameAndBuilder Preceding()
    {
        _parent.SetStart($"{_value} PRECEDING");
        return new BetweenFrameAndBuilder(_parent);
    }

    public BetweenFrameAndBuilder Following()
    {
        _parent.SetStart($"{_value} FOLLOWING");
        return new BetweenFrameAndBuilder(_parent);
    }

    public BetweenFrameAndBuilder And() => new(_parent);
}

public class BetweenFrameAndBuilder
{
    private readonly BetweenFrameBuilder _parent;

    public BetweenFrameAndBuilder(BetweenFrameBuilder parent)
    {
        _parent = parent;
    }

    public BetweenFrameEndBuilder Unbounded() => new(_parent, "UNBOUNDED");
    public BetweenFrameEndBuilder CurrentRow() => new(_parent, "CURRENT ROW", isComplete: true);
    public BetweenFrameEndBuilder Value(int n) => new(_parent, n.ToString());

    public BetweenFrameEndBuilder And() => new(_parent, "");
}

public class BetweenFrameEndBuilder
{
    private readonly BetweenFrameBuilder _parent;
    private readonly string _value;

    public BetweenFrameEndBuilder(BetweenFrameBuilder parent, string value, bool isComplete = false)
    {
        _parent = parent;
        _value = value;

        if (isComplete)
            _parent.Complete(value);
    }

    public OverClauseBuilder Preceding()
    {
        _parent.Complete($"{_value} PRECEDING");
        return GetParentOverBuilder();
    }

    public OverClauseBuilder Following()
    {
        _parent.Complete($"{_value} FOLLOWING");
        return GetParentOverBuilder();
    }

    private OverClauseBuilder GetParentOverBuilder()
    {
        // We need to return the parent OverClauseBuilder
        // This is a bit of a hack, but it works
        var field = typeof(BetweenFrameBuilder).GetField("_parent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (OverClauseBuilder)field!.GetValue(_parent)!;
    }
}

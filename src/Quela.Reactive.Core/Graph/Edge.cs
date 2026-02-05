using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Graph;

/// <summary>
/// Represents an edge (dependency) in the reactive graph.
/// Edges define the flow of data and control between nodes.
/// </summary>
public sealed record Edge
{
    /// <summary>
    /// Source node (upstream) of the edge.
    /// </summary>
    public NodeId Source { get; init; }

    /// <summary>
    /// Target node (downstream) of the edge.
    /// </summary>
    public NodeId Target { get; init; }

    /// <summary>
    /// Type of relationship this edge represents.
    /// </summary>
    public EdgeType Type { get; init; }

    /// <summary>
    /// Metadata for edge configuration.
    /// </summary>
    public EdgeMetadata Metadata { get; init; } = EdgeMetadata.Default;

    public Edge(NodeId source, NodeId target, EdgeType type = EdgeType.Data)
    {
        Source = source;
        Target = target;
        Type = type;
    }

    /// <summary>
    /// Creates a data dependency edge.
    /// </summary>
    public static Edge Data(NodeId source, NodeId target) =>
        new(source, target, EdgeType.Data);

    /// <summary>
    /// Creates a trigger edge (event-based activation).
    /// </summary>
    public static Edge Trigger(NodeId source, NodeId target) =>
        new(source, target, EdgeType.Trigger);

    /// <summary>
    /// Creates a conditional edge (may or may not be active).
    /// </summary>
    public static Edge Conditional(NodeId source, NodeId target, bool initiallyActive = true) =>
        new(source, target, EdgeType.Conditional) with
        {
            Metadata = new EdgeMetadata { IsActive = initiallyActive }
        };

    /// <summary>
    /// Creates a temporal edge (time-based dependency).
    /// </summary>
    public static Edge Temporal(NodeId source, NodeId target, TimeSpan delay) =>
        new(source, target, EdgeType.Temporal) with
        {
            Metadata = new EdgeMetadata { Delay = delay }
        };
}

/// <summary>
/// Type of relationship between nodes.
/// </summary>
public enum EdgeType
{
    /// <summary>
    /// Data dependency - target needs source's value.
    /// </summary>
    Data,

    /// <summary>
    /// Trigger dependency - target activates when source fires.
    /// </summary>
    Trigger,

    /// <summary>
    /// Conditional dependency - may be active or inactive.
    /// </summary>
    Conditional,

    /// <summary>
    /// Temporal dependency - introduces a time delay.
    /// </summary>
    Temporal,

    /// <summary>
    /// Validation dependency - target validates source.
    /// </summary>
    Validation,

    /// <summary>
    /// Cancellation dependency - source can cancel target.
    /// </summary>
    Cancellation
}

/// <summary>
/// Metadata for edge configuration.
/// </summary>
public sealed record EdgeMetadata
{
    /// <summary>
    /// Debounce delay before propagating changes.
    /// </summary>
    public TimeSpan? Debounce { get; init; }

    /// <summary>
    /// Throttle interval for rate limiting.
    /// </summary>
    public TimeSpan? Throttle { get; init; }

    /// <summary>
    /// Time delay before propagating (for temporal edges).
    /// </summary>
    public TimeSpan? Delay { get; init; }

    /// <summary>
    /// Transform function to apply when propagating (boxing for flexibility).
    /// </summary>
    public Func<object?, object?>? Transform { get; init; }

    /// <summary>
    /// Filter function to determine if propagation should occur.
    /// </summary>
    public Func<object?, bool>? Filter { get; init; }

    /// <summary>
    /// Whether this edge is currently active (for conditional edges).
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Priority for edge evaluation order.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// Custom tags for categorization.
    /// </summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();

    public static EdgeMetadata Default => new();

    /// <summary>
    /// Creates metadata with debounce configuration.
    /// </summary>
    public static EdgeMetadata WithDebounce(TimeSpan delay) =>
        new() { Debounce = delay };

    /// <summary>
    /// Creates metadata with throttle configuration.
    /// </summary>
    public static EdgeMetadata WithThrottle(TimeSpan interval) =>
        new() { Throttle = interval };
}

/// <summary>
/// Builder for creating edges with fluent configuration.
/// </summary>
public sealed class EdgeBuilder
{
    private readonly NodeId _source;
    private readonly NodeId _target;
    private EdgeType _type = EdgeType.Data;
    private EdgeMetadata _metadata = EdgeMetadata.Default;

    public EdgeBuilder(NodeId source, NodeId target)
    {
        _source = source;
        _target = target;
    }

    public EdgeBuilder AsData()
    {
        _type = EdgeType.Data;
        return this;
    }

    public EdgeBuilder AsTrigger()
    {
        _type = EdgeType.Trigger;
        return this;
    }

    public EdgeBuilder AsConditional(bool initiallyActive = true)
    {
        _type = EdgeType.Conditional;
        _metadata = _metadata with { IsActive = initiallyActive };
        return this;
    }

    public EdgeBuilder AsTemporal(TimeSpan delay)
    {
        _type = EdgeType.Temporal;
        _metadata = _metadata with { Delay = delay };
        return this;
    }

    public EdgeBuilder WithDebounce(TimeSpan delay)
    {
        _metadata = _metadata with { Debounce = delay };
        return this;
    }

    public EdgeBuilder WithThrottle(TimeSpan interval)
    {
        _metadata = _metadata with { Throttle = interval };
        return this;
    }

    public EdgeBuilder WithTransform(Func<object?, object?> transform)
    {
        _metadata = _metadata with { Transform = transform };
        return this;
    }

    public EdgeBuilder WithFilter(Func<object?, bool> filter)
    {
        _metadata = _metadata with { Filter = filter };
        return this;
    }

    public EdgeBuilder WithPriority(int priority)
    {
        _metadata = _metadata with { Priority = priority };
        return this;
    }

    public Edge Build() => new(_source, _target, _type) { Metadata = _metadata };
}

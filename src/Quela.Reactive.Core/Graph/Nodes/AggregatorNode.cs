using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Execution;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Aggregates multiple source values into a single result.
/// Implements fan-in patterns with configurable completion strategies.
/// </summary>
public sealed class AggregatorNode<TSource, TResult> : IStatefulNode<TResult>, IExecutableNode
{
    private TResult? _cachedValue;
    private bool _isDirty = true;
    private readonly HashSet<NodeId> _sources;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Aggregator;
    public Type ValueType => typeof(TResult);
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// Source nodes to aggregate from.
    /// </summary>
    public IReadOnlySet<NodeId> Sources => _sources;

    /// <summary>
    /// Aggregation function that combines source values.
    /// </summary>
    public Func<IReadOnlyList<TSource>, TResult> AggregateFunction { get; }

    /// <summary>
    /// Strategy for when to complete aggregation.
    /// </summary>
    public AggregationStrategy Strategy { get; }

    /// <summary>
    /// Minimum number of sources required (for WaitN strategy).
    /// </summary>
    public int MinimumSources { get; }

    /// <summary>
    /// Timeout for waiting on sources.
    /// </summary>
    public TimeSpan? Timeout { get; }

    public TResult Value => _cachedValue!;

    public bool IsDirty => _isDirty;

    public bool IsParallelizable => true;

    public int ExecutionCost => 1;

    public IReadOnlySet<NodeId> Dependencies => _sources;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public AggregatorNode(
        NodeId id,
        string name,
        IEnumerable<NodeId> sources,
        Func<IReadOnlyList<TSource>, TResult> aggregateFunction,
        AggregationStrategy strategy = AggregationStrategy.WaitAll,
        int minimumSources = 0,
        TimeSpan? timeout = null,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        _sources = new HashSet<NodeId>(sources);
        AggregateFunction = aggregateFunction;
        Strategy = strategy;
        MinimumSources = minimumSources > 0 ? minimumSources : _sources.Count;
        Timeout = timeout;
        Metadata = metadata ?? NodeMetadata.Default;
    }

    public object? GetValue() => _cachedValue;

    internal void SetCachedValue(TResult value)
    {
        _cachedValue = value;
        _isDirty = false;
    }

    internal void MarkDirty()
    {
        _isDirty = true;
    }

    internal void AddDependent(NodeId dependent)
    {
        _dependents.Add(dependent);
    }

    internal void RemoveDependent(NodeId dependent)
    {
        _dependents.Remove(dependent);
    }

    internal void Invalidate()
    {
        _isDirty = true;
        _cachedValue = default;
    }
}

/// <summary>
/// Strategy for aggregation completion.
/// </summary>
public enum AggregationStrategy
{
    /// <summary>
    /// Wait for all sources to complete before aggregating.
    /// </summary>
    WaitAll,

    /// <summary>
    /// Complete as soon as any source completes.
    /// </summary>
    WaitAny,

    /// <summary>
    /// Wait for N sources to complete.
    /// </summary>
    WaitN,

    /// <summary>
    /// Wait for first successful source (skip errors).
    /// </summary>
    FirstSuccess,

    /// <summary>
    /// Collect results as they arrive (streaming).
    /// </summary>
    Streaming
}

/// <summary>
/// Builder for creating aggregator nodes with fluent configuration.
/// </summary>
public sealed class AggregatorNodeBuilder<TSource, TResult>
{
    private readonly NodeId _id;
    private readonly string _name;
    private readonly List<NodeId> _sources = new();
    private Func<IReadOnlyList<TSource>, TResult>? _aggregateFunction;
    private AggregationStrategy _strategy = AggregationStrategy.WaitAll;
    private int _minimumSources;
    private TimeSpan? _timeout;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public AggregatorNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public AggregatorNodeBuilder<TSource, TResult> FromSources(params NodeId[] sources)
    {
        _sources.AddRange(sources);
        return this;
    }

    public AggregatorNodeBuilder<TSource, TResult> Aggregate(Func<IReadOnlyList<TSource>, TResult> function)
    {
        _aggregateFunction = function;
        return this;
    }

    public AggregatorNodeBuilder<TSource, TResult> WithStrategy(AggregationStrategy strategy)
    {
        _strategy = strategy;
        return this;
    }

    public AggregatorNodeBuilder<TSource, TResult> WaitForAtLeast(int count)
    {
        _strategy = AggregationStrategy.WaitN;
        _minimumSources = count;
        return this;
    }

    public AggregatorNodeBuilder<TSource, TResult> WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public AggregatorNodeBuilder<TSource, TResult> WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public AggregatorNode<TSource, TResult> Build()
    {
        if (_sources.Count == 0)
            throw new InvalidOperationException("At least one source must be specified");

        if (_aggregateFunction == null)
            throw new InvalidOperationException("Aggregate function must be specified");

        return new AggregatorNode<TSource, TResult>(
            _id,
            _name,
            _sources,
            _aggregateFunction,
            _strategy,
            _minimumSources,
            _timeout,
            _metadata);
    }
}

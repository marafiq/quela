using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Execution;
using Quela.Reactive.Core.Effects;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a side effect node that performs I/O operations.
/// Effect nodes are carefully managed for retries, cancellation, and idempotency.
/// </summary>
public sealed class EffectNode : INode, IExecutableNode
{
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Effect;
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// The effect execution function.
    /// </summary>
    public Func<IEffectContext, Task<EffectResult>> EffectFunction { get; }

    /// <summary>
    /// Retry policy for this effect.
    /// </summary>
    public RetryPolicy RetryPolicy { get; }

    /// <summary>
    /// Timeout for effect execution.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Optional idempotency key generator for deduplication.
    /// </summary>
    public Func<IDependencyReader, string>? IdempotencyKeyGenerator { get; }

    /// <summary>
    /// Cancellation scope identifier for grouped cancellation.
    /// </summary>
    public string? CancellationScope { get; }

    /// <summary>
    /// Type of result this effect produces.
    /// </summary>
    public Type ResultType { get; }

    public bool IsParallelizable { get; }

    public int ExecutionCost { get; }

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public EffectNode(
        NodeId id,
        string name,
        Func<IEffectContext, Task<EffectResult>> effectFunction,
        IEnumerable<NodeId> dependencies,
        Type resultType,
        RetryPolicy? retryPolicy = null,
        TimeSpan? timeout = null,
        Func<IDependencyReader, string>? idempotencyKeyGenerator = null,
        string? cancellationScope = null,
        bool isParallelizable = true,
        int executionCost = 10,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        EffectFunction = effectFunction;
        _dependencies = new HashSet<NodeId>(dependencies);
        ResultType = resultType;
        RetryPolicy = retryPolicy ?? RetryPolicy.Default;
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        IdempotencyKeyGenerator = idempotencyKeyGenerator;
        CancellationScope = cancellationScope;
        IsParallelizable = isParallelizable;
        ExecutionCost = executionCost;
        Metadata = metadata ?? NodeMetadata.Default;
    }

    internal void AddDependent(NodeId dependent)
    {
        _dependents.Add(dependent);
    }

    internal void RemoveDependent(NodeId dependent)
    {
        _dependents.Remove(dependent);
    }
}

/// <summary>
/// Strongly-typed effect node for better compile-time checking.
/// </summary>
public sealed class EffectNode<TResult> : INode, IExecutableNode
{
    private readonly EffectNode _inner;

    public NodeId Id => _inner.Id;
    public string Name => _inner.Name;
    public NodeType Type => _inner.Type;
    public NodeMetadata Metadata => _inner.Metadata;
    public IReadOnlySet<NodeId> Dependencies => _inner.Dependencies;
    public IReadOnlySet<NodeId> Dependents => _inner.Dependents;
    public bool IsParallelizable => _inner.IsParallelizable;
    public int ExecutionCost => _inner.ExecutionCost;

    public Func<IEffectContext, Task<EffectResult<TResult>>> TypedEffectFunction { get; }

    public EffectNode(
        NodeId id,
        string name,
        Func<IEffectContext, Task<EffectResult<TResult>>> effectFunction,
        IEnumerable<NodeId> dependencies,
        RetryPolicy? retryPolicy = null,
        TimeSpan? timeout = null,
        Func<IDependencyReader, string>? idempotencyKeyGenerator = null,
        string? cancellationScope = null,
        bool isParallelizable = true,
        int executionCost = 10,
        NodeMetadata? metadata = null)
    {
        TypedEffectFunction = effectFunction;

        _inner = new EffectNode(
            id,
            name,
            async ctx => await effectFunction(ctx),
            dependencies,
            typeof(TResult),
            retryPolicy,
            timeout,
            idempotencyKeyGenerator,
            cancellationScope,
            isParallelizable,
            executionCost,
            metadata);
    }

    internal EffectNode Inner => _inner;
}

/// <summary>
/// Builder for creating effect nodes with fluent configuration.
/// </summary>
public sealed class EffectNodeBuilder<TResult>
{
    private readonly NodeId _id;
    private readonly string _name;
    private Func<IEffectContext, Task<EffectResult<TResult>>>? _effectFunction;
    private readonly List<NodeId> _dependencies = new();
    private RetryPolicy _retryPolicy = RetryPolicy.Default;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private Func<IDependencyReader, string>? _idempotencyKeyGenerator;
    private string? _cancellationScope;
    private bool _isParallelizable = true;
    private int _executionCost = 10;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public EffectNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public EffectNodeBuilder<TResult> Execute(Func<IEffectContext, Task<EffectResult<TResult>>> function)
    {
        _effectFunction = function;
        return this;
    }

    public EffectNodeBuilder<TResult> DependsOn(NodeId dependency)
    {
        _dependencies.Add(dependency);
        return this;
    }

    public EffectNodeBuilder<TResult> DependsOn(params NodeId[] dependencies)
    {
        _dependencies.AddRange(dependencies);
        return this;
    }

    public EffectNodeBuilder<TResult> WithRetryPolicy(RetryPolicy policy)
    {
        _retryPolicy = policy;
        return this;
    }

    public EffectNodeBuilder<TResult> WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public EffectNodeBuilder<TResult> WithIdempotencyKey(Func<IDependencyReader, string> keyGenerator)
    {
        _idempotencyKeyGenerator = keyGenerator;
        return this;
    }

    public EffectNodeBuilder<TResult> InCancellationScope(string scope)
    {
        _cancellationScope = scope;
        return this;
    }

    public EffectNodeBuilder<TResult> Sequential()
    {
        _isParallelizable = false;
        return this;
    }

    public EffectNodeBuilder<TResult> WithCost(int cost)
    {
        _executionCost = cost;
        return this;
    }

    public EffectNodeBuilder<TResult> WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public EffectNode<TResult> Build()
    {
        if (_effectFunction == null)
            throw new InvalidOperationException("Effect function must be specified");

        return new EffectNode<TResult>(
            _id,
            _name,
            _effectFunction,
            _dependencies,
            _retryPolicy,
            _timeout,
            _idempotencyKeyGenerator,
            _cancellationScope,
            _isParallelizable,
            _executionCost,
            _metadata);
    }
}

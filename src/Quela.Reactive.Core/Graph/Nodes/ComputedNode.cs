using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Execution;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a computed value that derives from its dependencies.
/// Computed nodes are pure functions that transform input values.
/// </summary>
public sealed class ComputedNode<T> : IStatefulNode<T>, IExecutableNode
{
    private T? _cachedValue;
    private bool _isDirty = true;
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Computed;
    public Type ValueType => typeof(T);
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// Pure computation function that takes a dependency context and produces a value.
    /// </summary>
    public Func<IDependencyReader, T> ComputeFunction { get; }

    public T Value => _cachedValue!;

    public bool IsDirty => _isDirty;

    public bool IsParallelizable => true;

    public int ExecutionCost { get; }

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public ComputedNode(
        NodeId id,
        string name,
        Func<IDependencyReader, T> computeFunction,
        IEnumerable<NodeId> dependencies,
        int executionCost = 1,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        ComputeFunction = computeFunction;
        _dependencies = new HashSet<NodeId>(dependencies);
        ExecutionCost = executionCost;
        Metadata = metadata ?? NodeMetadata.Default;
    }

    public object? GetValue() => _cachedValue;

    internal void SetCachedValue(T value)
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
/// Builder for creating computed nodes with fluent configuration.
/// </summary>
public sealed class ComputedNodeBuilder<T>
{
    private readonly NodeId _id;
    private readonly string _name;
    private Func<IDependencyReader, T>? _computeFunction;
    private readonly List<NodeId> _dependencies = new();
    private int _executionCost = 1;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public ComputedNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public ComputedNodeBuilder<T> Compute(Func<IDependencyReader, T> function)
    {
        _computeFunction = function;
        return this;
    }

    public ComputedNodeBuilder<T> DependsOn(NodeId dependency)
    {
        _dependencies.Add(dependency);
        return this;
    }

    public ComputedNodeBuilder<T> DependsOn(params NodeId[] dependencies)
    {
        _dependencies.AddRange(dependencies);
        return this;
    }

    public ComputedNodeBuilder<T> WithCost(int cost)
    {
        _executionCost = cost;
        return this;
    }

    public ComputedNodeBuilder<T> WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public ComputedNode<T> Build()
    {
        if (_computeFunction == null)
            throw new InvalidOperationException("Compute function must be specified");

        return new ComputedNode<T>(
            _id,
            _name,
            _computeFunction,
            _dependencies,
            _executionCost,
            _metadata);
    }
}

using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Execution;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Routes execution based on a condition.
/// Enables dynamic graph behavior through conditional branching.
/// </summary>
public sealed class ConditionalNode : INode, IExecutableNode
{
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Conditional;
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// The condition function that determines the branch.
    /// </summary>
    public Func<IDependencyReader, bool> Condition { get; }

    /// <summary>
    /// Nodes to activate when condition is true.
    /// </summary>
    public IReadOnlyList<NodeId> TrueBranch { get; }

    /// <summary>
    /// Nodes to activate when condition is false.
    /// </summary>
    public IReadOnlyList<NodeId> FalseBranch { get; }

    public bool IsParallelizable => true;

    public int ExecutionCost => 1;

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public ConditionalNode(
        NodeId id,
        string name,
        Func<IDependencyReader, bool> condition,
        IEnumerable<NodeId> dependencies,
        IEnumerable<NodeId> trueBranch,
        IEnumerable<NodeId> falseBranch,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        Condition = condition;
        _dependencies = new HashSet<NodeId>(dependencies);
        TrueBranch = trueBranch.ToList();
        FalseBranch = falseBranch.ToList();
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

    /// <summary>
    /// Evaluates the condition and returns the active branch.
    /// </summary>
    public IReadOnlyList<NodeId> EvaluateBranch(IDependencyReader reader)
    {
        return Condition(reader) ? TrueBranch : FalseBranch;
    }
}

/// <summary>
/// Multi-way conditional node for switch-like behavior.
/// </summary>
public sealed class SwitchNode<TKey> : INode, IExecutableNode where TKey : notnull
{
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Conditional;
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// Function to extract the switch key.
    /// </summary>
    public Func<IDependencyReader, TKey> KeySelector { get; }

    /// <summary>
    /// Mapping from keys to branch nodes.
    /// </summary>
    public IReadOnlyDictionary<TKey, IReadOnlyList<NodeId>> Branches { get; }

    /// <summary>
    /// Default branch when no key matches.
    /// </summary>
    public IReadOnlyList<NodeId> DefaultBranch { get; }

    public bool IsParallelizable => true;

    public int ExecutionCost => 1;

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public SwitchNode(
        NodeId id,
        string name,
        Func<IDependencyReader, TKey> keySelector,
        IEnumerable<NodeId> dependencies,
        IReadOnlyDictionary<TKey, IReadOnlyList<NodeId>> branches,
        IReadOnlyList<NodeId>? defaultBranch = null,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        KeySelector = keySelector;
        _dependencies = new HashSet<NodeId>(dependencies);
        Branches = branches;
        DefaultBranch = defaultBranch ?? Array.Empty<NodeId>();
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

    /// <summary>
    /// Evaluates the key and returns the active branch.
    /// </summary>
    public IReadOnlyList<NodeId> EvaluateBranch(IDependencyReader reader)
    {
        var key = KeySelector(reader);
        return Branches.TryGetValue(key, out var branch) ? branch : DefaultBranch;
    }
}

/// <summary>
/// Builder for creating conditional nodes with fluent configuration.
/// </summary>
public sealed class ConditionalNodeBuilder
{
    private readonly NodeId _id;
    private readonly string _name;
    private Func<IDependencyReader, bool>? _condition;
    private readonly List<NodeId> _dependencies = new();
    private readonly List<NodeId> _trueBranch = new();
    private readonly List<NodeId> _falseBranch = new();
    private NodeMetadata _metadata = NodeMetadata.Default;

    public ConditionalNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public ConditionalNodeBuilder When(Func<IDependencyReader, bool> condition)
    {
        _condition = condition;
        return this;
    }

    public ConditionalNodeBuilder DependsOn(NodeId dependency)
    {
        _dependencies.Add(dependency);
        return this;
    }

    public ConditionalNodeBuilder ThenActivate(params NodeId[] nodes)
    {
        _trueBranch.AddRange(nodes);
        return this;
    }

    public ConditionalNodeBuilder ElseActivate(params NodeId[] nodes)
    {
        _falseBranch.AddRange(nodes);
        return this;
    }

    public ConditionalNodeBuilder WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public ConditionalNode Build()
    {
        if (_condition == null)
            throw new InvalidOperationException("Condition must be specified");

        return new ConditionalNode(
            _id,
            _name,
            _condition,
            _dependencies,
            _trueBranch,
            _falseBranch,
            _metadata);
    }
}

using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Execution;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a nested partial with its own sub-graph.
/// Enables composition and reuse of orchestration logic.
/// </summary>
public sealed class PartialNode : INode, IExecutableNode
{
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Partial;
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// Unique identifier for this partial type.
    /// </summary>
    public string PartialId { get; }

    /// <summary>
    /// The sub-graph for this partial.
    /// </summary>
    public ReactiveGraph SubGraph { get; }

    /// <summary>
    /// Mappings from parent graph nodes to sub-graph input nodes.
    /// </summary>
    public IReadOnlyDictionary<NodeId, NodeId> InputMappings { get; }

    /// <summary>
    /// Mappings from sub-graph output nodes to parent graph nodes.
    /// </summary>
    public IReadOnlyDictionary<NodeId, NodeId> OutputMappings { get; }

    /// <summary>
    /// Template for rendering this partial.
    /// </summary>
    public string? Template { get; }

    /// <summary>
    /// Target element for inserting this partial.
    /// </summary>
    public string? InsertionTarget { get; }

    /// <summary>
    /// Position for inserting relative to target.
    /// </summary>
    public InsertionPosition InsertionPosition { get; }

    public bool IsParallelizable => true;

    public int ExecutionCost { get; }

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public PartialNode(
        NodeId id,
        string name,
        string partialId,
        ReactiveGraph subGraph,
        IReadOnlyDictionary<NodeId, NodeId> inputMappings,
        IReadOnlyDictionary<NodeId, NodeId> outputMappings,
        string? template = null,
        string? insertionTarget = null,
        InsertionPosition insertionPosition = InsertionPosition.Replace,
        int executionCost = 5,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        PartialId = partialId;
        SubGraph = subGraph;
        InputMappings = inputMappings;
        OutputMappings = outputMappings;
        Template = template;
        InsertionTarget = insertionTarget;
        InsertionPosition = insertionPosition;
        ExecutionCost = executionCost;
        Metadata = metadata ?? NodeMetadata.Default;

        _dependencies = new HashSet<NodeId>(inputMappings.Keys);
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
/// Position for inserting a partial relative to its target.
/// </summary>
public enum InsertionPosition
{
    /// <summary>
    /// Replace the target element.
    /// </summary>
    Replace,

    /// <summary>
    /// Insert before the target element.
    /// </summary>
    Before,

    /// <summary>
    /// Insert after the target element.
    /// </summary>
    After,

    /// <summary>
    /// Insert as first child of the target element.
    /// </summary>
    Prepend,

    /// <summary>
    /// Insert as last child of the target element.
    /// </summary>
    Append
}

/// <summary>
/// Builder for creating partial nodes with fluent configuration.
/// </summary>
public sealed class PartialNodeBuilder
{
    private readonly NodeId _id;
    private readonly string _name;
    private string? _partialId;
    private ReactiveGraph? _subGraph;
    private readonly Dictionary<NodeId, NodeId> _inputMappings = new();
    private readonly Dictionary<NodeId, NodeId> _outputMappings = new();
    private string? _template;
    private string? _insertionTarget;
    private InsertionPosition _insertionPosition = InsertionPosition.Replace;
    private int _executionCost = 5;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public PartialNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public PartialNodeBuilder WithPartialId(string partialId)
    {
        _partialId = partialId;
        return this;
    }

    public PartialNodeBuilder WithSubGraph(ReactiveGraph subGraph)
    {
        _subGraph = subGraph;
        return this;
    }

    public PartialNodeBuilder MapInput(NodeId parentNode, NodeId subGraphNode)
    {
        _inputMappings[parentNode] = subGraphNode;
        return this;
    }

    public PartialNodeBuilder MapOutput(NodeId subGraphNode, NodeId parentNode)
    {
        _outputMappings[subGraphNode] = parentNode;
        return this;
    }

    public PartialNodeBuilder WithTemplate(string template)
    {
        _template = template;
        return this;
    }

    public PartialNodeBuilder InsertAt(string target, InsertionPosition position = InsertionPosition.Replace)
    {
        _insertionTarget = target;
        _insertionPosition = position;
        return this;
    }

    public PartialNodeBuilder WithCost(int cost)
    {
        _executionCost = cost;
        return this;
    }

    public PartialNodeBuilder WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public PartialNode Build()
    {
        if (string.IsNullOrWhiteSpace(_partialId))
            throw new InvalidOperationException("Partial ID must be specified");

        if (_subGraph == null)
            throw new InvalidOperationException("Sub-graph must be specified");

        return new PartialNode(
            _id,
            _name,
            _partialId,
            _subGraph,
            _inputMappings,
            _outputMappings,
            _template,
            _insertionTarget,
            _insertionPosition,
            _executionCost,
            _metadata);
    }
}

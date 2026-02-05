using System.Collections.Immutable;
using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Graph;

/// <summary>
/// Immutable reactive graph that defines the orchestration structure.
/// The graph is compiled once and reused across all sessions.
/// </summary>
public sealed class ReactiveGraph
{
    private readonly ImmutableDictionary<NodeId, INode> _nodes;
    private readonly ImmutableDictionary<NodeId, ImmutableHashSet<Edge>> _outgoingEdges;
    private readonly ImmutableDictionary<NodeId, ImmutableHashSet<Edge>> _incomingEdges;
    private readonly ImmutableList<NodeId> _topologicalOrder;

    /// <summary>
    /// Unique identifier for this graph.
    /// </summary>
    public OrchestrationId Id { get; }

    /// <summary>
    /// Human-readable name for this orchestration.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Version of this graph for cache invalidation.
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// All nodes in the graph.
    /// </summary>
    public IReadOnlyDictionary<NodeId, INode> Nodes => _nodes;

    /// <summary>
    /// Root nodes (nodes with no dependencies).
    /// </summary>
    public IReadOnlyList<NodeId> RootNodes { get; }

    /// <summary>
    /// Leaf nodes (nodes with no dependents).
    /// </summary>
    public IReadOnlyList<NodeId> LeafNodes { get; }

    /// <summary>
    /// Nodes in topological order (dependencies before dependents).
    /// </summary>
    public IReadOnlyList<NodeId> TopologicalOrder => _topologicalOrder;

    /// <summary>
    /// Total number of nodes.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Total number of edges.
    /// </summary>
    public int EdgeCount { get; }

    /// <summary>
    /// Metadata for the graph.
    /// </summary>
    public GraphMetadata Metadata { get; }

    internal ReactiveGraph(
        OrchestrationId id,
        string name,
        string version,
        ImmutableDictionary<NodeId, INode> nodes,
        ImmutableDictionary<NodeId, ImmutableHashSet<Edge>> outgoingEdges,
        ImmutableDictionary<NodeId, ImmutableHashSet<Edge>> incomingEdges,
        ImmutableList<NodeId> topologicalOrder,
        GraphMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        Version = version;
        _nodes = nodes;
        _outgoingEdges = outgoingEdges;
        _incomingEdges = incomingEdges;
        _topologicalOrder = topologicalOrder;
        Metadata = metadata ?? GraphMetadata.Default;

        RootNodes = _nodes.Keys
            .Where(id => !_incomingEdges.ContainsKey(id) || _incomingEdges[id].IsEmpty)
            .ToList();

        LeafNodes = _nodes.Keys
            .Where(id => !_outgoingEdges.ContainsKey(id) || _outgoingEdges[id].IsEmpty)
            .ToList();

        EdgeCount = _outgoingEdges.Values.Sum(e => e.Count);
    }

    /// <summary>
    /// Gets a node by ID.
    /// </summary>
    public INode? GetNode(NodeId id) =>
        _nodes.TryGetValue(id, out var node) ? node : null;

    /// <summary>
    /// Gets a strongly-typed node by ID.
    /// </summary>
    public TNode? GetNode<TNode>(NodeId id) where TNode : class, INode =>
        GetNode(id) as TNode;

    /// <summary>
    /// Gets outgoing edges from a node.
    /// </summary>
    public IReadOnlySet<Edge> GetOutgoingEdges(NodeId id) =>
        _outgoingEdges.TryGetValue(id, out var edges) ? edges : ImmutableHashSet<Edge>.Empty;

    /// <summary>
    /// Gets incoming edges to a node.
    /// </summary>
    public IReadOnlySet<Edge> GetIncomingEdges(NodeId id) =>
        _incomingEdges.TryGetValue(id, out var edges) ? edges : ImmutableHashSet<Edge>.Empty;

    /// <summary>
    /// Gets all direct dependents of a node.
    /// </summary>
    public IEnumerable<NodeId> GetDependents(NodeId id) =>
        GetOutgoingEdges(id).Select(e => e.Target);

    /// <summary>
    /// Gets all direct dependencies of a node.
    /// </summary>
    public IEnumerable<NodeId> GetDependencies(NodeId id) =>
        GetIncomingEdges(id).Select(e => e.Source);

    /// <summary>
    /// Gets all transitive dependents of a node.
    /// </summary>
    public IEnumerable<NodeId> GetTransitiveDependents(NodeId id)
    {
        var visited = new HashSet<NodeId>();
        var queue = new Queue<NodeId>();

        foreach (var dependent in GetDependents(id))
            queue.Enqueue(dependent);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Add(current))
            {
                yield return current;
                foreach (var dependent in GetDependents(current))
                    queue.Enqueue(dependent);
            }
        }
    }

    /// <summary>
    /// Gets all transitive dependencies of a node.
    /// </summary>
    public IEnumerable<NodeId> GetTransitiveDependencies(NodeId id)
    {
        var visited = new HashSet<NodeId>();
        var queue = new Queue<NodeId>();

        foreach (var dependency in GetDependencies(id))
            queue.Enqueue(dependency);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (visited.Add(current))
            {
                yield return current;
                foreach (var dependency in GetDependencies(current))
                    queue.Enqueue(dependency);
            }
        }
    }

    /// <summary>
    /// Gets nodes by type.
    /// </summary>
    public IEnumerable<TNode> GetNodesByType<TNode>() where TNode : class, INode =>
        _nodes.Values.OfType<TNode>();

    /// <summary>
    /// Gets nodes by node type enum.
    /// </summary>
    public IEnumerable<INode> GetNodesByType(NodeType type) =>
        _nodes.Values.Where(n => n.Type == type);

    /// <summary>
    /// Checks if the graph contains a node.
    /// </summary>
    public bool ContainsNode(NodeId id) => _nodes.ContainsKey(id);

    /// <summary>
    /// Checks if there's a path from source to target.
    /// </summary>
    public bool HasPath(NodeId source, NodeId target)
    {
        var visited = new HashSet<NodeId>();
        var queue = new Queue<NodeId>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == target)
                return true;

            if (visited.Add(current))
            {
                foreach (var dependent in GetDependents(current))
                    queue.Enqueue(dependent);
            }
        }

        return false;
    }

    /// <summary>
    /// Finds nodes matching a predicate.
    /// </summary>
    public IEnumerable<INode> FindNodes(Func<INode, bool> predicate) =>
        _nodes.Values.Where(predicate);

    /// <summary>
    /// Creates a subgraph containing only the specified nodes.
    /// </summary>
    public ReactiveGraph CreateSubgraph(IEnumerable<NodeId> nodeIds)
    {
        var nodeSet = nodeIds.ToHashSet();
        var filteredNodes = _nodes.Where(kvp => nodeSet.Contains(kvp.Key))
            .ToImmutableDictionary();

        var filteredOutgoing = _outgoingEdges
            .Where(kvp => nodeSet.Contains(kvp.Key))
            .ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Where(e => nodeSet.Contains(e.Target)).ToImmutableHashSet());

        var filteredIncoming = _incomingEdges
            .Where(kvp => nodeSet.Contains(kvp.Key))
            .ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Where(e => nodeSet.Contains(e.Source)).ToImmutableHashSet());

        var filteredTopo = _topologicalOrder.Where(id => nodeSet.Contains(id)).ToImmutableList();

        return new ReactiveGraph(
            Id,
            $"{Name}_subgraph",
            Version,
            filteredNodes,
            filteredOutgoing,
            filteredIncoming,
            filteredTopo,
            Metadata);
    }
}

/// <summary>
/// Metadata for graph configuration.
/// </summary>
public sealed record GraphMetadata
{
    /// <summary>
    /// Maximum concurrent effect executions.
    /// </summary>
    public int MaxConcurrentEffects { get; init; } = 10;

    /// <summary>
    /// Default timeout for effects.
    /// </summary>
    public TimeSpan DefaultEffectTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to enable detailed tracing.
    /// </summary>
    public bool EnableTracing { get; init; }

    /// <summary>
    /// Custom tags for categorization.
    /// </summary>
    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>();

    /// <summary>
    /// Custom data for extensibility.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomData { get; init; } =
        new Dictionary<string, object>();

    public static GraphMetadata Default => new();
}

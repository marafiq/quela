using System.Collections.Immutable;
using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Graph;

/// <summary>
/// Builder for constructing reactive graphs with validation.
/// Provides a mutable interface for graph construction before compilation.
/// </summary>
public sealed class ReactiveGraphBuilder
{
    private readonly Dictionary<NodeId, INode> _nodes = new();
    private readonly Dictionary<NodeId, HashSet<Edge>> _outgoingEdges = new();
    private readonly Dictionary<NodeId, HashSet<Edge>> _incomingEdges = new();

    public OrchestrationId Id { get; }
    public string Name { get; }
    public string Version { get; }
    private GraphMetadata _metadata = GraphMetadata.Default;

    public ReactiveGraphBuilder(OrchestrationId id, string name, string? version = null)
    {
        Id = id;
        Name = name;
        Version = version ?? "1.0.0";
    }

    public ReactiveGraphBuilder(string id, string name, string? version = null)
        : this(OrchestrationId.Create(id), name, version)
    {
    }

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public ReactiveGraphBuilder AddNode(INode node)
    {
        if (_nodes.ContainsKey(node.Id))
            throw new InvalidOperationException($"Node with ID '{node.Id}' already exists");

        _nodes[node.Id] = node;

        // Add edges for declared dependencies
        foreach (var depId in node.Dependencies)
        {
            AddEdge(Edge.Data(depId, node.Id));
        }

        return this;
    }

    /// <summary>
    /// Adds multiple nodes to the graph.
    /// </summary>
    public ReactiveGraphBuilder AddNodes(params INode[] nodes)
    {
        foreach (var node in nodes)
            AddNode(node);
        return this;
    }

    /// <summary>
    /// Adds an edge to the graph.
    /// </summary>
    public ReactiveGraphBuilder AddEdge(Edge edge)
    {
        if (!_outgoingEdges.TryGetValue(edge.Source, out var outgoing))
        {
            outgoing = new HashSet<Edge>();
            _outgoingEdges[edge.Source] = outgoing;
        }
        outgoing.Add(edge);

        if (!_incomingEdges.TryGetValue(edge.Target, out var incoming))
        {
            incoming = new HashSet<Edge>();
            _incomingEdges[edge.Target] = incoming;
        }
        incoming.Add(edge);

        return this;
    }

    /// <summary>
    /// Adds multiple edges to the graph.
    /// </summary>
    public ReactiveGraphBuilder AddEdges(params Edge[] edges)
    {
        foreach (var edge in edges)
            AddEdge(edge);
        return this;
    }

    /// <summary>
    /// Connects two nodes with a data dependency.
    /// </summary>
    public ReactiveGraphBuilder Connect(NodeId source, NodeId target)
    {
        return AddEdge(Edge.Data(source, target));
    }

    /// <summary>
    /// Sets the graph metadata.
    /// </summary>
    public ReactiveGraphBuilder WithMetadata(GraphMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Validates the graph for correctness.
    /// </summary>
    public GraphValidationResult Validate()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check for missing dependencies
        foreach (var edge in _outgoingEdges.Values.SelectMany(e => e))
        {
            if (!_nodes.ContainsKey(edge.Source))
                errors.Add($"Edge references non-existent source node: {edge.Source}");
            if (!_nodes.ContainsKey(edge.Target))
                errors.Add($"Edge references non-existent target node: {edge.Target}");
        }

        // Check for cycles
        if (HasCycle(out var cycleNodes))
        {
            errors.Add($"Graph contains cycle involving nodes: {string.Join(" -> ", cycleNodes)}");
        }

        // Check for unreachable nodes
        var reachable = GetReachableNodes();
        var unreachable = _nodes.Keys.Where(id => !reachable.Contains(id)).ToList();
        if (unreachable.Count > 0)
        {
            warnings.Add($"Unreachable nodes detected: {string.Join(", ", unreachable)}");
        }

        return new GraphValidationResult(
            errors.Count == 0,
            errors,
            warnings);
    }

    /// <summary>
    /// Builds the immutable reactive graph.
    /// </summary>
    public ReactiveGraph Build()
    {
        var validation = Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Graph validation failed: {string.Join("; ", validation.Errors)}");
        }

        var topologicalOrder = ComputeTopologicalOrder();

        return new ReactiveGraph(
            Id,
            Name,
            Version,
            _nodes.ToImmutableDictionary(),
            _outgoingEdges.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToImmutableHashSet()),
            _incomingEdges.ToImmutableDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToImmutableHashSet()),
            topologicalOrder.ToImmutableList(),
            _metadata);
    }

    private bool HasCycle(out List<NodeId> cycleNodes)
    {
        cycleNodes = new List<NodeId>();
        var visited = new HashSet<NodeId>();
        var recursionStack = new HashSet<NodeId>();
        var path = new List<NodeId>();

        foreach (var nodeId in _nodes.Keys)
        {
            if (HasCycleDfs(nodeId, visited, recursionStack, path))
            {
                // Find the cycle start
                var cycleStart = path.Last();
                var cycleStartIndex = path.IndexOf(cycleStart);
                cycleNodes = path.Skip(cycleStartIndex).ToList();
                return true;
            }
        }

        return false;
    }

    private bool HasCycleDfs(NodeId nodeId, HashSet<NodeId> visited, HashSet<NodeId> recursionStack, List<NodeId> path)
    {
        if (recursionStack.Contains(nodeId))
        {
            path.Add(nodeId);
            return true;
        }

        if (visited.Contains(nodeId))
            return false;

        visited.Add(nodeId);
        recursionStack.Add(nodeId);
        path.Add(nodeId);

        if (_outgoingEdges.TryGetValue(nodeId, out var edges))
        {
            foreach (var edge in edges)
            {
                if (HasCycleDfs(edge.Target, visited, recursionStack, path))
                    return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(nodeId);
        return false;
    }

    private HashSet<NodeId> GetReachableNodes()
    {
        var reachable = new HashSet<NodeId>();
        var queue = new Queue<NodeId>();

        // Start from root nodes (no incoming edges)
        foreach (var nodeId in _nodes.Keys)
        {
            if (!_incomingEdges.ContainsKey(nodeId) || _incomingEdges[nodeId].Count == 0)
            {
                queue.Enqueue(nodeId);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (reachable.Add(current))
            {
                if (_outgoingEdges.TryGetValue(current, out var edges))
                {
                    foreach (var edge in edges)
                        queue.Enqueue(edge.Target);
                }
            }
        }

        return reachable;
    }

    private List<NodeId> ComputeTopologicalOrder()
    {
        var result = new List<NodeId>();
        var visited = new HashSet<NodeId>();
        var temp = new HashSet<NodeId>();

        void Visit(NodeId nodeId)
        {
            if (visited.Contains(nodeId))
                return;
            if (temp.Contains(nodeId))
                throw new InvalidOperationException("Cycle detected during topological sort");

            temp.Add(nodeId);

            if (_incomingEdges.TryGetValue(nodeId, out var edges))
            {
                foreach (var edge in edges)
                    Visit(edge.Source);
            }

            temp.Remove(nodeId);
            visited.Add(nodeId);
            result.Add(nodeId);
        }

        foreach (var nodeId in _nodes.Keys)
            Visit(nodeId);

        return result;
    }
}

/// <summary>
/// Result of graph validation.
/// </summary>
public sealed record GraphValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);

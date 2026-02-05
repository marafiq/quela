using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.DSL.Compiler;

/// <summary>
/// Compiles orchestration definitions to optimized reactive graphs.
/// </summary>
public sealed class GraphCompiler
{
    private readonly GraphCompilerOptions _options;

    public GraphCompiler(GraphCompilerOptions? options = null)
    {
        _options = options ?? new GraphCompilerOptions();
    }

    /// <summary>
    /// Compiles a graph with optimization passes.
    /// </summary>
    public CompiledGraph Compile(ReactiveGraph graph)
    {
        var analysis = AnalyzeGraph(graph);
        var optimizedGraph = _options.EnableOptimizations
            ? OptimizeGraph(graph, analysis)
            : graph;

        return new CompiledGraph(
            optimizedGraph,
            analysis,
            GenerateMetadata(optimizedGraph, analysis));
    }

    /// <summary>
    /// Analyzes the graph for optimization opportunities.
    /// </summary>
    public GraphAnalysis AnalyzeGraph(ReactiveGraph graph)
    {
        var analysis = new GraphAnalysis();

        // Identify hot paths (nodes with many dependents)
        foreach (var (nodeId, node) in graph.Nodes)
        {
            var dependentCount = graph.GetTransitiveDependents(nodeId).Count();
            if (dependentCount > _options.HotPathThreshold)
            {
                analysis.HotPaths.Add(nodeId);
            }
        }

        // Identify parallelization opportunities
        foreach (var nodeId in graph.TopologicalOrder)
        {
            var node = graph.GetNode(nodeId);
            if (node is IExecutableNode execNode && execNode.IsParallelizable)
            {
                var deps = graph.GetDependencies(nodeId).ToList();
                var parallelSiblings = deps
                    .SelectMany(d => graph.GetDependents(d))
                    .Where(s => s != nodeId)
                    .Where(s => graph.GetNode(s) is IExecutableNode e && e.IsParallelizable)
                    .ToList();

                if (parallelSiblings.Count > 0)
                {
                    analysis.ParallelizationOpportunities[nodeId] = parallelSiblings;
                }
            }
        }

        // Identify dead nodes (unreachable from any trigger)
        var triggerNodes = graph.GetNodesByType(NodeType.Trigger).Select(n => n.Id).ToHashSet();
        var reachableFromTriggers = new HashSet<NodeId>();

        foreach (var triggerId in triggerNodes)
        {
            reachableFromTriggers.Add(triggerId);
            foreach (var dep in graph.GetTransitiveDependents(triggerId))
            {
                reachableFromTriggers.Add(dep);
            }
        }

        // Value nodes are always reachable (they're inputs)
        foreach (var node in graph.GetNodesByType(NodeType.Value))
        {
            reachableFromTriggers.Add(node.Id);
            foreach (var dep in graph.GetTransitiveDependents(node.Id))
            {
                reachableFromTriggers.Add(dep);
            }
        }

        analysis.DeadNodes = graph.Nodes.Keys
            .Where(id => !reachableFromTriggers.Contains(id))
            .ToHashSet();

        // Compute critical path
        analysis.CriticalPath = ComputeCriticalPath(graph);

        // Identify effect chains
        analysis.EffectChains = IdentifyEffectChains(graph);

        return analysis;
    }

    private ReactiveGraph OptimizeGraph(ReactiveGraph graph, GraphAnalysis analysis)
    {
        // For now, return the original graph
        // Future optimizations could include:
        // - Dead code elimination
        // - Effect batching
        // - Computation memoization hints
        return graph;
    }

    private List<NodeId> ComputeCriticalPath(ReactiveGraph graph)
    {
        // Find the path with maximum total execution cost
        var costs = new Dictionary<NodeId, (int Cost, NodeId? Predecessor)>();

        foreach (var nodeId in graph.TopologicalOrder)
        {
            var node = graph.GetNode(nodeId);
            var nodeCost = node is IExecutableNode exec ? exec.ExecutionCost : 1;

            var maxPredCost = 0;
            NodeId? maxPred = null;

            foreach (var depId in graph.GetDependencies(nodeId))
            {
                if (costs.TryGetValue(depId, out var depCost) && depCost.Cost > maxPredCost)
                {
                    maxPredCost = depCost.Cost;
                    maxPred = depId;
                }
            }

            costs[nodeId] = (maxPredCost + nodeCost, maxPred);
        }

        // Find the node with maximum cost (end of critical path)
        var maxCostNode = costs.MaxBy(kvp => kvp.Value.Cost).Key;

        // Reconstruct path
        var path = new List<NodeId>();
        var current = (NodeId?)maxCostNode;

        while (current.HasValue)
        {
            path.Insert(0, current.Value);
            current = costs[current.Value].Predecessor;
        }

        return path;
    }

    private List<List<NodeId>> IdentifyEffectChains(ReactiveGraph graph)
    {
        var chains = new List<List<NodeId>>();
        var visited = new HashSet<NodeId>();

        foreach (var node in graph.GetNodesByType(NodeType.Effect))
        {
            if (visited.Contains(node.Id))
                continue;

            var chain = new List<NodeId> { node.Id };
            visited.Add(node.Id);

            // Follow effect dependencies
            var current = node.Id;
            while (true)
            {
                var nextEffects = graph.GetDependents(current)
                    .Where(d => graph.GetNode(d)?.Type == NodeType.Effect)
                    .Where(d => !visited.Contains(d))
                    .ToList();

                if (nextEffects.Count == 0)
                    break;

                // Take the first unvisited effect dependent
                current = nextEffects.First();
                chain.Add(current);
                visited.Add(current);
            }

            if (chain.Count > 1)
            {
                chains.Add(chain);
            }
        }

        return chains;
    }

    private CompiledGraphMetadata GenerateMetadata(ReactiveGraph graph, GraphAnalysis analysis)
    {
        return new CompiledGraphMetadata
        {
            TotalNodes = graph.NodeCount,
            TotalEdges = graph.EdgeCount,
            MaxDepth = ComputeMaxDepth(graph),
            HotPathCount = analysis.HotPaths.Count,
            DeadNodeCount = analysis.DeadNodes.Count,
            EffectChainCount = analysis.EffectChains.Count,
            CriticalPathLength = analysis.CriticalPath.Count,
            CompiledAt = DateTimeOffset.UtcNow
        };
    }

    private int ComputeMaxDepth(ReactiveGraph graph)
    {
        var depths = new Dictionary<NodeId, int>();

        foreach (var nodeId in graph.TopologicalOrder)
        {
            var maxDepDepth = graph.GetDependencies(nodeId)
                .Select(d => depths.TryGetValue(d, out var depth) ? depth : 0)
                .DefaultIfEmpty(0)
                .Max();

            depths[nodeId] = maxDepDepth + 1;
        }

        return depths.Values.DefaultIfEmpty(0).Max();
    }
}

/// <summary>
/// Analysis results for a graph.
/// </summary>
public sealed class GraphAnalysis
{
    public HashSet<NodeId> HotPaths { get; } = new();
    public Dictionary<NodeId, List<NodeId>> ParallelizationOpportunities { get; } = new();
    public HashSet<NodeId> DeadNodes { get; set; } = new();
    public List<NodeId> CriticalPath { get; set; } = new();
    public List<List<NodeId>> EffectChains { get; set; } = new();
}

/// <summary>
/// Compiled graph with analysis and metadata.
/// </summary>
public sealed record CompiledGraph(
    ReactiveGraph Graph,
    GraphAnalysis Analysis,
    CompiledGraphMetadata Metadata);

/// <summary>
/// Metadata about a compiled graph.
/// </summary>
public sealed record CompiledGraphMetadata
{
    public int TotalNodes { get; init; }
    public int TotalEdges { get; init; }
    public int MaxDepth { get; init; }
    public int HotPathCount { get; init; }
    public int DeadNodeCount { get; init; }
    public int EffectChainCount { get; init; }
    public int CriticalPathLength { get; init; }
    public DateTimeOffset CompiledAt { get; init; }
}

/// <summary>
/// Options for the graph compiler.
/// </summary>
public sealed record GraphCompilerOptions
{
    public bool EnableOptimizations { get; init; } = true;
    public int HotPathThreshold { get; init; } = 5;
    public bool GenerateDebugInfo { get; init; } = false;
}

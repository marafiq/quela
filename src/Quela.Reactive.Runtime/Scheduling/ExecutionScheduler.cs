using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Runtime.State;

namespace Quela.Reactive.Runtime.Scheduling;

/// <summary>
/// Schedules and coordinates node execution in topological order.
/// Implements wave-based parallel execution.
/// </summary>
public sealed class ExecutionScheduler
{
    private readonly ILogger<ExecutionScheduler> _logger;
    private readonly SchedulerOptions _options;

    public ExecutionScheduler(
        ILogger<ExecutionScheduler> logger,
        SchedulerOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new SchedulerOptions();
    }

    /// <summary>
    /// Computes execution waves from dirty nodes.
    /// Nodes in the same wave can execute in parallel.
    /// </summary>
    public ExecutionPlan ComputeExecutionPlan(
        ReactiveGraph graph,
        IReadOnlySet<NodeId> dirtyNodes)
    {
        if (dirtyNodes.Count == 0)
            return ExecutionPlan.Empty;

        // Find all nodes that need to be executed (dirty + transitive dependents)
        var nodesToExecute = ComputeNodesToExecute(graph, dirtyNodes);

        // Build waves based on topological order
        var waves = BuildExecutionWaves(graph, nodesToExecute);

        _logger.LogDebug(
            "Computed execution plan: {NodeCount} nodes in {WaveCount} waves",
            nodesToExecute.Count, waves.Count);

        return new ExecutionPlan(waves, nodesToExecute);
    }

    private HashSet<NodeId> ComputeNodesToExecute(
        ReactiveGraph graph,
        IReadOnlySet<NodeId> dirtyNodes)
    {
        var result = new HashSet<NodeId>(dirtyNodes);

        // Add all transitive dependents
        foreach (var dirtyNode in dirtyNodes)
        {
            foreach (var dependent in graph.GetTransitiveDependents(dirtyNode))
            {
                result.Add(dependent);
            }
        }

        return result;
    }

    private List<ExecutionWave> BuildExecutionWaves(
        ReactiveGraph graph,
        HashSet<NodeId> nodesToExecute)
    {
        var waves = new List<ExecutionWave>();
        var executed = new HashSet<NodeId>();
        var remaining = new HashSet<NodeId>(nodesToExecute);

        while (remaining.Count > 0)
        {
            // Find nodes whose dependencies are all satisfied
            var waveNodes = new List<NodeId>();

            foreach (var nodeId in remaining)
            {
                var node = graph.GetNode(nodeId);
                if (node == null) continue;

                var dependencies = node.Dependencies.Where(nodesToExecute.Contains);
                if (dependencies.All(executed.Contains))
                {
                    waveNodes.Add(nodeId);
                }
            }

            if (waveNodes.Count == 0 && remaining.Count > 0)
            {
                // This shouldn't happen with a valid DAG
                throw new InvalidOperationException(
                    "Unable to make progress - possible cycle in graph");
            }

            // Sort wave nodes by priority
            var sortedWaveNodes = waveNodes
                .Select(id => (Id: id, Node: graph.GetNode(id)!))
                .OrderByDescending(x => x.Node.Metadata.Priority)
                .Select(x => x.Id)
                .ToList();

            waves.Add(new ExecutionWave(waves.Count, sortedWaveNodes));

            foreach (var nodeId in waveNodes)
            {
                executed.Add(nodeId);
                remaining.Remove(nodeId);
            }
        }

        return waves;
    }

    /// <summary>
    /// Executes a plan using the provided executor.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        ReactiveGraph graph,
        SessionState sessionState,
        Func<NodeId, INode, Task<NodeExecutionResult>> executor,
        CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentDictionary<NodeId, NodeExecutionResult>();
        var failedNodes = new List<NodeId>();

        foreach (var wave in plan.Waves)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var waveResults = await ExecuteWaveAsync(
                wave,
                graph,
                sessionState,
                executor,
                cancellationToken);

            foreach (var (nodeId, result) in waveResults)
            {
                results[nodeId] = result;
                if (!result.IsSuccess)
                {
                    failedNodes.Add(nodeId);
                }
            }

            // If any nodes failed, we might want to stop
            if (failedNodes.Count > 0 && _options.StopOnFirstError)
            {
                _logger.LogDebug(
                    "Stopping execution due to failure in node {NodeId}",
                    failedNodes.First());
                break;
            }
        }

        return new ExecutionResult(
            results,
            failedNodes.Count == 0,
            failedNodes);
    }

    private async Task<Dictionary<NodeId, NodeExecutionResult>> ExecuteWaveAsync(
        ExecutionWave wave,
        ReactiveGraph graph,
        SessionState sessionState,
        Func<NodeId, INode, Task<NodeExecutionResult>> executor,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<NodeId, NodeExecutionResult>();

        if (_options.EnableParallelExecution && wave.Nodes.Count > 1)
        {
            // Execute parallelizable nodes in parallel
            var parallelizableNodes = wave.Nodes
                .Select(id => (Id: id, Node: graph.GetNode(id)))
                .Where(x => x.Node is IExecutableNode exec && exec.IsParallelizable)
                .ToList();

            var sequentialNodes = wave.Nodes
                .Except(parallelizableNodes.Select(x => x.Id))
                .ToList();

            // Execute parallel nodes
            if (parallelizableNodes.Count > 0)
            {
                var semaphore = new SemaphoreSlim(_options.MaxParallelNodes);
                var tasks = parallelizableNodes.Select(async x =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await executor(x.Id, x.Node!);
                        return (x.Id, Result: result);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                var parallelResults = await Task.WhenAll(tasks);
                foreach (var (nodeId, result) in parallelResults)
                {
                    results[nodeId] = result;
                }
            }

            // Execute sequential nodes
            foreach (var nodeId in sequentialNodes)
            {
                var node = graph.GetNode(nodeId);
                if (node != null)
                {
                    results[nodeId] = await executor(nodeId, node);
                }
            }
        }
        else
        {
            // Execute all nodes sequentially
            foreach (var nodeId in wave.Nodes)
            {
                var node = graph.GetNode(nodeId);
                if (node != null)
                {
                    results[nodeId] = await executor(nodeId, node);
                }
            }
        }

        return results;
    }
}

/// <summary>
/// A plan for executing nodes in waves.
/// </summary>
public sealed record ExecutionPlan(
    IReadOnlyList<ExecutionWave> Waves,
    IReadOnlySet<NodeId> NodesToExecute)
{
    public static ExecutionPlan Empty => new(
        Array.Empty<ExecutionWave>(),
        new HashSet<NodeId>());

    public bool IsEmpty => Waves.Count == 0;
}

/// <summary>
/// A wave of nodes that can execute in parallel.
/// </summary>
public sealed record ExecutionWave(
    int Index,
    IReadOnlyList<NodeId> Nodes);

/// <summary>
/// Result of executing a single node.
/// </summary>
public sealed record NodeExecutionResult(
    NodeId NodeId,
    bool IsSuccess,
    object? Value = null,
    Exception? Error = null,
    TimeSpan Duration = default);

/// <summary>
/// Result of executing a plan.
/// </summary>
public sealed record ExecutionResult(
    IReadOnlyDictionary<NodeId, NodeExecutionResult> NodeResults,
    bool IsSuccess,
    IReadOnlyList<NodeId> FailedNodes);

/// <summary>
/// Options for the scheduler.
/// </summary>
public sealed record SchedulerOptions
{
    public bool EnableParallelExecution { get; init; } = true;
    public int MaxParallelNodes { get; init; } = 10;
    public bool StopOnFirstError { get; init; } = false;
}

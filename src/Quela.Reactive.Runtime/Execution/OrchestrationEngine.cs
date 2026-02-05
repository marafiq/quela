using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Graph.Nodes;
using Quela.Reactive.Core.Execution;
using Quela.Reactive.Core.Transactions;
using Quela.Reactive.Core.Patches;
using Quela.Reactive.Core.Payloads;
using Quela.Reactive.Core.Validation;
using Quela.Reactive.Runtime.State;
using Quela.Reactive.Runtime.Transactions;
using Quela.Reactive.Runtime.Scheduling;
using Quela.Reactive.Runtime.Effects;
using Quela.Reactive.Runtime.Patches;

namespace Quela.Reactive.Runtime.Execution;

/// <summary>
/// Main orchestration engine that coordinates execution.
/// </summary>
public sealed class OrchestrationEngine
{
    private readonly TransactionManager _transactionManager;
    private readonly ExecutionScheduler _scheduler;
    private readonly EffectRuntime _effectRuntime;
    private readonly PatchGenerator _patchGenerator;
    private readonly DebounceManager _debounceManager;
    private readonly ILogger<OrchestrationEngine> _logger;
    private readonly OrchestrationEngineOptions _options;

    public OrchestrationEngine(
        TransactionManager transactionManager,
        ExecutionScheduler scheduler,
        EffectRuntime effectRuntime,
        PatchGenerator patchGenerator,
        DebounceManager debounceManager,
        ILogger<OrchestrationEngine> logger,
        OrchestrationEngineOptions? options = null)
    {
        _transactionManager = transactionManager;
        _scheduler = scheduler;
        _effectRuntime = effectRuntime;
        _patchGenerator = patchGenerator;
        _debounceManager = debounceManager;
        _logger = logger;
        _options = options ?? new OrchestrationEngineOptions();
    }

    /// <summary>
    /// Processes a changeset and returns the response payload.
    /// </summary>
    public async Task<ResponsePayload> ProcessChangesetAsync(
        ChangesetPayload changeset,
        SessionState sessionState,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var transactionId = TransactionId.Parse(changeset.TransactionId);
        var orchestrationId = OrchestrationId.Create(changeset.OrchestrationId);

        _logger.LogDebug(
            "Processing changeset {TransactionId} with {ChangeCount} changes and {ActionCount} actions",
            transactionId, changeset.Changes.Count, changeset.Actions.Count);

        // Begin transaction
        var txContext = await _transactionManager.BeginTransactionAsync(
            sessionState.SessionId,
            orchestrationId,
            sessionState,
            _options.DefaultIsolationLevel,
            cancellationToken);

        try
        {
            // Apply field changes
            foreach (var change in changeset.Changes)
            {
                await ApplyFieldChangeAsync(change, sessionState, txContext);
            }

            // Process triggers from actions
            foreach (var action in changeset.Actions)
            {
                await ProcessActionAsync(action, sessionState, txContext);
            }

            // Execute the graph
            var executionResult = await ExecuteGraphAsync(
                sessionState,
                txContext,
                cancellationToken);

            if (!executionResult.IsSuccess)
            {
                await _transactionManager.AbortAsync(
                    txContext.Transaction.Id,
                    new TransactionError("ExecutionFailed", "Graph execution failed"));

                return CreateErrorResponse(
                    transactionId,
                    executionResult.FailedNodes,
                    sessionState);
            }

            // Generate patches
            var patches = _patchGenerator.GeneratePatches(
                sessionState,
                executionResult.NodeResults.Keys.ToHashSet());

            // Commit transaction
            await _transactionManager.CommitAsync(txContext.Transaction.Id);

            sw.Stop();
            _logger.LogDebug(
                "Processed changeset {TransactionId} in {Duration}ms",
                transactionId, sw.ElapsedMilliseconds);

            return new ResponsePayload
            {
                TransactionId = transactionId.ToString(),
                Status = ResponseStatus.Committed,
                Sequence = sessionState.SequenceGenerator.Current.Value,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Patches = patches,
                AvailableActions = GetAvailableActions(sessionState)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing changeset {TransactionId}", transactionId);

            await _transactionManager.AbortAsync(
                txContext.Transaction.Id,
                new TransactionError("Exception", ex.Message, ex));

            return new ResponsePayload
            {
                TransactionId = transactionId.ToString(),
                Status = ResponseStatus.Aborted,
                Errors = new[]
                {
                    new ResponseError
                    {
                        Code = "InternalError",
                        Message = _options.ExposeDetailedErrors ? ex.Message : "An error occurred"
                    }
                }
            };
        }
    }

    private async Task ApplyFieldChangeAsync(
        FieldChange change,
        SessionState sessionState,
        TransactionContext txContext)
    {
        var nodeId = NodeId.Create(change.NodeId);
        var node = sessionState.Graph.GetNode(nodeId);

        if (node == null)
        {
            _logger.LogWarning("Field change for unknown node: {NodeId}", change.NodeId);
            return;
        }

        // Check for debouncing
        if (node.Metadata.DebounceDelay.HasValue)
        {
            var shouldProcess = await _debounceManager.DebounceAsync(
                nodeId,
                node.Metadata.DebounceDelay.Value);

            if (!shouldProcess)
                return;
        }

        // Record the change
        var oldValue = sessionState.GetNodeState(nodeId).Value;
        txContext.RecordOperation(
            OperationType.ValueChange,
            nodeId,
            oldValue,
            change.Value);

        // Update the value
        sessionState.SetValue(nodeId, change.Value);

        await Task.CompletedTask;
    }

    private async Task ProcessActionAsync(
        ActionRequest action,
        SessionState sessionState,
        TransactionContext txContext)
    {
        var triggerId = NodeId.Create(action.ActionId);
        var triggerNode = sessionState.Graph.GetNode<TriggerNode>(triggerId);

        if (triggerNode == null)
        {
            _logger.LogWarning("Action for unknown trigger: {ActionId}", action.ActionId);
            return;
        }

        // Check throttling
        if (triggerNode.ThrottleInterval.HasValue)
        {
            if (!_debounceManager.TryThrottle(triggerId, triggerNode.ThrottleInterval.Value))
            {
                _logger.LogDebug("Trigger {TriggerId} throttled", triggerId);
                return;
            }
        }

        // Check required valid nodes
        foreach (var requiredNodeId in triggerNode.RequiredValidNodes)
        {
            var nodeState = sessionState.GetNodeState(requiredNodeId);
            if (!nodeState.ValidationResult.IsValid)
            {
                _logger.LogDebug(
                    "Trigger {TriggerId} blocked by invalid node {NodeId}",
                    triggerId, requiredNodeId);
                return;
            }
        }

        // Mark trigger dependents as dirty
        foreach (var dependentId in sessionState.Graph.GetDependents(triggerId))
        {
            sessionState.MarkDirty(dependentId);
        }

        await Task.CompletedTask;
    }

    private async Task<ExecutionResult> ExecuteGraphAsync(
        SessionState sessionState,
        TransactionContext txContext,
        CancellationToken cancellationToken)
    {
        var dirtyNodes = sessionState.GetDirtyNodes();
        if (dirtyNodes.Count == 0)
        {
            return new ExecutionResult(
                new Dictionary<NodeId, NodeExecutionResult>(),
                true,
                Array.Empty<NodeId>());
        }

        var plan = _scheduler.ComputeExecutionPlan(sessionState.Graph, dirtyNodes);

        return await _scheduler.ExecuteAsync(
            plan,
            sessionState.Graph,
            sessionState,
            async (nodeId, node) => await ExecuteNodeAsync(
                nodeId,
                node,
                sessionState,
                txContext,
                cancellationToken),
            cancellationToken);
    }

    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        NodeId nodeId,
        INode node,
        SessionState sessionState,
        TransactionContext txContext,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var nodeState = sessionState.GetNodeState(nodeId);
            nodeState.BeginCompute();

            object? result = null;

            switch (node)
            {
                case IStatefulNode<object> valueNode when node.Type == NodeType.Value:
                    // Value nodes just validate
                    result = nodeState.Value;
                    break;

                case ComputedNode<object> computedNode:
                    var reader = new DependencyReaderImpl(sessionState, txContext.Transaction.Id);
                    result = computedNode.ComputeFunction(reader);
                    sessionState.SetValue(nodeId, result);
                    break;

                case EffectNode effectNode:
                    var effectContext = new EffectExecutionContext(
                        sessionState,
                        txContext.Transaction.Id);

                    var effectResult = await _effectRuntime.ExecuteAsync(
                        effectNode,
                        effectContext,
                        cancellationToken);

                    if (!effectResult.IsSuccess)
                    {
                        return new NodeExecutionResult(
                            nodeId,
                            false,
                            Error: effectResult.Error?.Exception ?? new Exception(effectResult.Error?.Message));
                    }

                    result = effectResult.Value;
                    sessionState.SetValue(nodeId, result);
                    break;

                case ConditionalNode conditionalNode:
                    var conditionReader = new DependencyReaderImpl(sessionState, txContext.Transaction.Id);
                    var activeBranch = conditionalNode.EvaluateBranch(conditionReader);

                    // Mark active branch nodes as dirty
                    foreach (var branchNodeId in activeBranch)
                    {
                        sessionState.MarkDirty(branchNodeId);
                    }
                    break;

                default:
                    // Handle other node types as needed
                    result = nodeState.Value;
                    break;
            }

            nodeState.EndCompute();
            sessionState.MarkClean(nodeId);

            sw.Stop();
            return new NodeExecutionResult(nodeId, true, result, Duration: sw.Elapsed);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error executing node {NodeId}", nodeId);
            return new NodeExecutionResult(nodeId, false, Error: ex, Duration: sw.Elapsed);
        }
    }

    private ResponsePayload CreateErrorResponse(
        TransactionId transactionId,
        IReadOnlyList<NodeId> failedNodes,
        SessionState sessionState)
    {
        var errors = failedNodes.Select(nodeId => new ResponseError
        {
            Code = "NodeExecutionFailed",
            Message = $"Execution failed for node {nodeId}",
            NodeId = nodeId.Value
        }).ToList();

        return new ResponsePayload
        {
            TransactionId = transactionId.ToString(),
            Status = ResponseStatus.Aborted,
            Errors = errors
        };
    }

    private IReadOnlyList<string> GetAvailableActions(SessionState sessionState)
    {
        return sessionState.Graph
            .GetNodesByType<TriggerNode>()
            .Where(trigger =>
            {
                // Check if all required nodes are valid
                foreach (var requiredNodeId in trigger.RequiredValidNodes)
                {
                    var nodeState = sessionState.GetNodeState(requiredNodeId);
                    if (!nodeState.ValidationResult.IsValid)
                        return false;
                }
                return true;
            })
            .Select(trigger => trigger.Id.Value)
            .ToList();
    }
}

/// <summary>
/// Implementation of IDependencyReader.
/// </summary>
internal sealed class DependencyReaderImpl : IDependencyReader
{
    private readonly SessionState _sessionState;

    public TransactionId TransactionId { get; }
    public SessionId SessionId => _sessionState.SessionId;

    public DependencyReaderImpl(SessionState sessionState, TransactionId transactionId)
    {
        _sessionState = sessionState;
        TransactionId = transactionId;
    }

    public T Get<T>(NodeId nodeId)
    {
        var value = _sessionState.GetNodeState(nodeId).Value;
        if (value is T typedValue)
            return typedValue;
        throw new InvalidOperationException(
            $"Node {nodeId} has value of type {value?.GetType().Name ?? "null"}, expected {typeof(T).Name}");
    }

    public bool TryGet<T>(NodeId nodeId, out T? value)
    {
        var raw = _sessionState.GetNodeState(nodeId).Value;
        if (raw is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }

    public T GetOrDefault<T>(NodeId nodeId, T defaultValue = default!)
    {
        return TryGet<T>(nodeId, out var value) ? value! : defaultValue;
    }

    public object? GetRaw(NodeId nodeId)
    {
        return _sessionState.GetNodeState(nodeId).Value;
    }

    public bool HasValue(NodeId nodeId)
    {
        return _sessionState.GetNodeState(nodeId).Value != null;
    }

    public bool NodeExists(NodeId nodeId)
    {
        return _sessionState.Graph.ContainsNode(nodeId);
    }
}

/// <summary>
/// Options for the orchestration engine.
/// </summary>
public sealed record OrchestrationEngineOptions
{
    public IsolationLevel DefaultIsolationLevel { get; init; } = IsolationLevel.ReadCommitted;
    public bool ExposeDetailedErrors { get; init; } = false;
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
}

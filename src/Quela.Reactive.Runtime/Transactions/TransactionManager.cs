using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Transactions;
using Quela.Reactive.Runtime.State;

namespace Quela.Reactive.Runtime.Transactions;

/// <summary>
/// Manages transaction lifecycle and isolation.
/// </summary>
public sealed class TransactionManager
{
    private readonly ConcurrentDictionary<TransactionId, TransactionContext> _activeTransactions = new();
    private readonly ConcurrentDictionary<SessionId, SemaphoreSlim> _sessionLocks = new();
    private readonly ILogger<TransactionManager> _logger;
    private readonly TransactionOptions _options;

    public TransactionManager(
        ILogger<TransactionManager> logger,
        TransactionOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new TransactionOptions();
    }

    /// <summary>
    /// Begins a new transaction.
    /// </summary>
    public async Task<TransactionContext> BeginTransactionAsync(
        SessionId sessionId,
        OrchestrationId orchestrationId,
        SessionState sessionState,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var transactionId = TransactionId.Create();

        // Get session lock for serializable isolation
        if (isolationLevel == IsolationLevel.Serializable)
        {
            var sessionLock = _sessionLocks.GetOrAdd(sessionId, _ => new SemaphoreSlim(1, 1));
            await sessionLock.WaitAsync(cancellationToken);
        }

        var transaction = new Transaction(
            transactionId,
            sessionId,
            orchestrationId,
            isolationLevel,
            timeout: _options.DefaultTimeout);

        // Create snapshot for isolation
        var snapshot = isolationLevel switch
        {
            IsolationLevel.Snapshot or IsolationLevel.Serializable => sessionState.CreateSnapshot(),
            _ => null
        };

        var context = new TransactionContext(
            transaction,
            sessionState,
            snapshot,
            _logger);

        if (!_activeTransactions.TryAdd(transactionId, context))
        {
            throw new InvalidOperationException($"Transaction {transactionId} already exists");
        }

        transaction.Start();
        transaction.SequenceNumber = sessionState.SequenceGenerator.Next();

        _logger.LogDebug(
            "Started transaction {TransactionId} for session {SessionId}",
            transactionId, sessionId);

        return context;
    }

    /// <summary>
    /// Commits a transaction.
    /// </summary>
    public async Task CommitAsync(TransactionId transactionId)
    {
        if (!_activeTransactions.TryRemove(transactionId, out var context))
        {
            throw new InvalidOperationException($"Transaction {transactionId} not found");
        }

        try
        {
            context.Transaction.BeginCommit();

            // Apply any deferred operations
            await context.ApplyDeferredOperationsAsync();

            context.Transaction.Commit();

            _logger.LogDebug(
                "Committed transaction {TransactionId} ({OperationCount} operations)",
                transactionId, context.Transaction.Operations.Count);
        }
        finally
        {
            ReleaseLocks(context);
        }
    }

    /// <summary>
    /// Aborts a transaction and rolls back changes.
    /// </summary>
    public async Task AbortAsync(TransactionId transactionId, TransactionError? error = null)
    {
        if (!_activeTransactions.TryRemove(transactionId, out var context))
        {
            _logger.LogWarning("Attempted to abort non-existent transaction {TransactionId}", transactionId);
            return;
        }

        try
        {
            // Restore snapshot if available
            if (context.Snapshot != null)
            {
                context.SessionState.RestoreSnapshot(context.Snapshot);
            }

            context.Transaction.Abort(error);

            _logger.LogDebug(
                "Aborted transaction {TransactionId}: {Error}",
                transactionId, error?.Message ?? "No error");
        }
        finally
        {
            ReleaseLocks(context);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets an active transaction context.
    /// </summary>
    public TransactionContext? GetTransaction(TransactionId transactionId)
    {
        return _activeTransactions.TryGetValue(transactionId, out var context) ? context : null;
    }

    /// <summary>
    /// Checks for timed out transactions.
    /// </summary>
    public async Task CleanupTimedOutTransactionsAsync()
    {
        var timedOut = _activeTransactions
            .Where(kvp => kvp.Value.Transaction.IsTimedOut)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var txId in timedOut)
        {
            _logger.LogWarning("Transaction {TransactionId} timed out", txId);
            await AbortAsync(txId, new TransactionError("Timeout", "Transaction timed out"));
        }
    }

    private void ReleaseLocks(TransactionContext context)
    {
        if (context.Transaction.IsolationLevel == IsolationLevel.Serializable)
        {
            if (_sessionLocks.TryGetValue(context.Transaction.SessionId, out var sessionLock))
            {
                sessionLock.Release();
            }
        }
    }
}

/// <summary>
/// Context for an active transaction.
/// </summary>
public sealed class TransactionContext : IDisposable
{
    private readonly List<Func<Task>> _deferredOperations = new();
    private readonly ILogger _logger;
    private bool _disposed;

    public Transaction Transaction { get; }
    public SessionState SessionState { get; }
    public StateSnapshot? Snapshot { get; }

    internal TransactionContext(
        Transaction transaction,
        SessionState sessionState,
        StateSnapshot? snapshot,
        ILogger logger)
    {
        Transaction = transaction;
        SessionState = sessionState;
        Snapshot = snapshot;
        _logger = logger;
    }

    /// <summary>
    /// Records an operation in the transaction log.
    /// </summary>
    public void RecordOperation(
        OperationType type,
        NodeId nodeId,
        object? oldValue,
        object? newValue)
    {
        var operation = new TransactionOperation(
            type,
            nodeId,
            oldValue,
            newValue,
            DateTimeOffset.UtcNow,
            SessionState.SequenceGenerator.Next());

        Transaction.RecordOperation(operation);
    }

    /// <summary>
    /// Defers an operation to commit time.
    /// </summary>
    public void DeferOperation(Func<Task> operation)
    {
        _deferredOperations.Add(operation);
    }

    /// <summary>
    /// Applies all deferred operations.
    /// </summary>
    internal async Task ApplyDeferredOperationsAsync()
    {
        foreach (var operation in _deferredOperations)
        {
            await operation();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// Options for transaction behavior.
/// </summary>
public sealed record TransactionOptions
{
    public TimeSpan DefaultTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaxConcurrentTransactions { get; init; } = 100;
    public bool EnableTransactionLogging { get; init; } = true;
}

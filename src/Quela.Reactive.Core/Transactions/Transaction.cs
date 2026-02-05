using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Transactions;

/// <summary>
/// Represents a transaction in the orchestration runtime.
/// Provides ACID-like guarantees for state changes.
/// </summary>
public sealed class Transaction
{
    private readonly List<TransactionOperation> _operations = new();
    private readonly object _lock = new();
    private TransactionState _state = TransactionState.Pending;

    /// <summary>
    /// Unique identifier for this transaction.
    /// </summary>
    public TransactionId Id { get; }

    /// <summary>
    /// Session this transaction belongs to.
    /// </summary>
    public SessionId SessionId { get; }

    /// <summary>
    /// Orchestration being executed.
    /// </summary>
    public OrchestrationId OrchestrationId { get; }

    /// <summary>
    /// When the transaction was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// When the transaction started executing.
    /// </summary>
    public DateTimeOffset? StartedAt { get; private set; }

    /// <summary>
    /// When the transaction completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Current state of the transaction.
    /// </summary>
    public TransactionState State
    {
        get { lock (_lock) return _state; }
    }

    /// <summary>
    /// Isolation level for this transaction.
    /// </summary>
    public IsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Operations performed in this transaction.
    /// </summary>
    public IReadOnlyList<TransactionOperation> Operations
    {
        get { lock (_lock) return _operations.ToList(); }
    }

    /// <summary>
    /// Parent transaction ID (for nested transactions).
    /// </summary>
    public TransactionId? ParentTransactionId { get; }

    /// <summary>
    /// Sequence number for ordering.
    /// </summary>
    public SequenceNumber SequenceNumber { get; internal set; }

    /// <summary>
    /// Timeout for this transaction.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Error if transaction failed.
    /// </summary>
    public TransactionError? Error { get; private set; }

    public Transaction(
        TransactionId id,
        SessionId sessionId,
        OrchestrationId orchestrationId,
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        TransactionId? parentTransactionId = null,
        TimeSpan? timeout = null)
    {
        Id = id;
        SessionId = sessionId;
        OrchestrationId = orchestrationId;
        IsolationLevel = isolationLevel;
        ParentTransactionId = parentTransactionId;
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Records an operation in the transaction log.
    /// </summary>
    internal void RecordOperation(TransactionOperation operation)
    {
        lock (_lock)
        {
            if (_state != TransactionState.Running)
                throw new InvalidOperationException(
                    $"Cannot record operation in transaction with state {_state}");
            _operations.Add(operation);
        }
    }

    /// <summary>
    /// Transitions the transaction to running state.
    /// </summary>
    internal void Start()
    {
        lock (_lock)
        {
            if (_state != TransactionState.Pending)
                throw new InvalidOperationException(
                    $"Cannot start transaction in state {_state}");
            _state = TransactionState.Running;
            StartedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Transitions to committing state.
    /// </summary>
    internal void BeginCommit()
    {
        lock (_lock)
        {
            if (_state != TransactionState.Running)
                throw new InvalidOperationException(
                    $"Cannot begin commit for transaction in state {_state}");
            _state = TransactionState.Committing;
        }
    }

    /// <summary>
    /// Completes the transaction successfully.
    /// </summary>
    internal void Commit()
    {
        lock (_lock)
        {
            if (_state != TransactionState.Committing)
                throw new InvalidOperationException(
                    $"Cannot commit transaction in state {_state}");
            _state = TransactionState.Committed;
            CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Aborts the transaction.
    /// </summary>
    internal void Abort(TransactionError? error = null)
    {
        lock (_lock)
        {
            if (_state == TransactionState.Committed)
                throw new InvalidOperationException("Cannot abort committed transaction");
            _state = TransactionState.Aborted;
            Error = error;
            CompletedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Checks if the transaction has timed out.
    /// </summary>
    public bool IsTimedOut =>
        StartedAt.HasValue && DateTimeOffset.UtcNow - StartedAt.Value > Timeout;

    /// <summary>
    /// Gets the duration of the transaction.
    /// </summary>
    public TimeSpan? Duration =>
        CompletedAt.HasValue && StartedAt.HasValue
            ? CompletedAt.Value - StartedAt.Value
            : null;
}

/// <summary>
/// State of a transaction.
/// </summary>
public enum TransactionState
{
    Pending,
    Running,
    Committing,
    Committed,
    Aborted
}

/// <summary>
/// Isolation level for transactions.
/// </summary>
public enum IsolationLevel
{
    /// <summary>
    /// See committed values from other transactions.
    /// </summary>
    ReadCommitted,

    /// <summary>
    /// See a consistent snapshot from transaction start.
    /// </summary>
    Snapshot,

    /// <summary>
    /// Full isolation between concurrent transactions.
    /// </summary>
    Serializable
}

/// <summary>
/// An operation recorded in the transaction log.
/// </summary>
public sealed record TransactionOperation(
    OperationType Type,
    NodeId NodeId,
    object? OldValue,
    object? NewValue,
    DateTimeOffset Timestamp,
    SequenceNumber SequenceNumber);

/// <summary>
/// Type of operation in a transaction.
/// </summary>
public enum OperationType
{
    ValueChange,
    EffectStart,
    EffectComplete,
    EffectFailed,
    ValidationChange,
    NodeActivation,
    NodeDeactivation
}

/// <summary>
/// Error information for failed transactions.
/// </summary>
public sealed record TransactionError(
    string Code,
    string Message,
    Exception? Exception = null,
    NodeId? FailedNodeId = null);

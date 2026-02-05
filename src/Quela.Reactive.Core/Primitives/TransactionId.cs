namespace Quela.Reactive.Core.Primitives;

/// <summary>
/// Strongly-typed identifier for transactions.
/// Provides correlation across the entire transaction lifecycle.
/// </summary>
public readonly record struct TransactionId : IComparable<TransactionId>
{
    private readonly Guid _value;

    public TransactionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Transaction ID cannot be empty", nameof(value));
        _value = value;
    }

    public Guid Value => _value;

    public static TransactionId Create() => new(Guid.NewGuid());

    public static TransactionId Parse(string value) => new(Guid.Parse(value));

    public static implicit operator Guid(TransactionId id) => id._value;

    public static explicit operator TransactionId(Guid value) => new(value);

    public int CompareTo(TransactionId other) => _value.CompareTo(other._value);

    public override string ToString() => _value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for sessions (user interaction contexts).
/// </summary>
public readonly record struct SessionId
{
    private readonly Guid _value;

    public SessionId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Session ID cannot be empty", nameof(value));
        _value = value;
    }

    public Guid Value => _value;

    public static SessionId Create() => new(Guid.NewGuid());

    public static SessionId Parse(string value) => new(Guid.Parse(value));

    public static implicit operator Guid(SessionId id) => id._value;

    public override string ToString() => _value.ToString("N");
}

/// <summary>
/// Strongly-typed identifier for orchestrations (graph definitions).
/// </summary>
public readonly record struct OrchestrationId
{
    private readonly string _value;

    public OrchestrationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public string Value => _value;

    public static OrchestrationId Create(string value) => new(value);

    public static implicit operator string(OrchestrationId id) => id._value;

    public override string ToString() => _value;
}

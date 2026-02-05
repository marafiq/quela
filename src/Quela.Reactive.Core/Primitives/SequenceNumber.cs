namespace Quela.Reactive.Core.Primitives;

/// <summary>
/// Monotonically increasing sequence number for ordering operations.
/// Used for causal ordering and conflict detection.
/// </summary>
public readonly record struct SequenceNumber : IComparable<SequenceNumber>
{
    private readonly long _value;

    public SequenceNumber(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        _value = value;
    }

    public long Value => _value;

    public static SequenceNumber Zero => new(0);

    public static SequenceNumber Initial => new(1);

    public SequenceNumber Next() => new(_value + 1);

    public SequenceNumber Advance(int count) => new(_value + count);

    public static implicit operator long(SequenceNumber seq) => seq._value;

    public static explicit operator SequenceNumber(long value) => new(value);

    public int CompareTo(SequenceNumber other) => _value.CompareTo(other._value);

    public static bool operator <(SequenceNumber left, SequenceNumber right) =>
        left._value < right._value;

    public static bool operator >(SequenceNumber left, SequenceNumber right) =>
        left._value > right._value;

    public static bool operator <=(SequenceNumber left, SequenceNumber right) =>
        left._value <= right._value;

    public static bool operator >=(SequenceNumber left, SequenceNumber right) =>
        left._value >= right._value;

    public override string ToString() => _value.ToString();
}

/// <summary>
/// Thread-safe sequence number generator.
/// </summary>
public sealed class SequenceGenerator
{
    private long _current;

    public SequenceGenerator(long initial = 0)
    {
        _current = initial;
    }

    public SequenceNumber Next() => new(Interlocked.Increment(ref _current));

    public SequenceNumber Current => new(Interlocked.Read(ref _current));

    public void Reset(long value = 0) => Interlocked.Exchange(ref _current, value);
}

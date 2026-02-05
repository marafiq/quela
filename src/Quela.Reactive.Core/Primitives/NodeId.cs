namespace Quela.Reactive.Core.Primitives;

/// <summary>
/// Strongly-typed identifier for nodes in the reactive graph.
/// Provides type safety and efficient hashing for graph operations.
/// </summary>
public readonly record struct NodeId : IComparable<NodeId>
{
    private readonly string _value;

    public NodeId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        _value = value;
    }

    public string Value => _value;

    public static NodeId Create(string value) => new(value);

    public static NodeId Generate() => new(Guid.NewGuid().ToString("N"));

    public static implicit operator string(NodeId id) => id._value;

    public static explicit operator NodeId(string value) => new(value);

    public int CompareTo(NodeId other) =>
        string.Compare(_value, other._value, StringComparison.Ordinal);

    public override string ToString() => _value;
}

/// <summary>
/// Represents a path through nested partials to a specific node.
/// </summary>
public readonly record struct NodePath
{
    public IReadOnlyList<string> Segments { get; }
    public NodeId NodeId { get; }

    public NodePath(IReadOnlyList<string> segments, NodeId nodeId)
    {
        Segments = segments;
        NodeId = nodeId;
    }

    public static NodePath Root(NodeId nodeId) => new(Array.Empty<string>(), nodeId);

    public NodePath Descend(string partialId) =>
        new([.. Segments, partialId], NodeId);

    public override string ToString() =>
        Segments.Count == 0 ? NodeId.ToString() : $"{string.Join("/", Segments)}/{NodeId}";
}

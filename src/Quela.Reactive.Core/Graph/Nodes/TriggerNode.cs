using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a trigger node that initiates downstream computations.
/// Triggers are activated by external events (button clicks, form submissions).
/// </summary>
public sealed class TriggerNode : INode
{
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Trigger;
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// Event type that activates this trigger.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// Nodes that must be valid before this trigger can fire.
    /// </summary>
    public IReadOnlyList<NodeId> RequiredValidNodes { get; }

    /// <summary>
    /// Whether this trigger can fire multiple times concurrently.
    /// </summary>
    public bool AllowConcurrent { get; }

    /// <summary>
    /// Debounce delay for repeated activations.
    /// </summary>
    public TimeSpan? DebounceDelay { get; }

    /// <summary>
    /// Throttle interval for rate limiting.
    /// </summary>
    public TimeSpan? ThrottleInterval { get; }

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public TriggerNode(
        NodeId id,
        string name,
        string eventType,
        IEnumerable<NodeId>? dependencies = null,
        IEnumerable<NodeId>? requiredValidNodes = null,
        bool allowConcurrent = false,
        TimeSpan? debounceDelay = null,
        TimeSpan? throttleInterval = null,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        EventType = eventType;
        _dependencies = new HashSet<NodeId>(dependencies ?? Enumerable.Empty<NodeId>());
        RequiredValidNodes = requiredValidNodes?.ToList() ?? new List<NodeId>();
        AllowConcurrent = allowConcurrent;
        DebounceDelay = debounceDelay;
        ThrottleInterval = throttleInterval;
        Metadata = metadata ?? NodeMetadata.Default;
    }

    internal void AddDependent(NodeId dependent)
    {
        _dependents.Add(dependent);
    }

    internal void RemoveDependent(NodeId dependent)
    {
        _dependents.Remove(dependent);
    }
}

/// <summary>
/// Builder for creating trigger nodes with fluent configuration.
/// </summary>
public sealed class TriggerNodeBuilder
{
    private readonly NodeId _id;
    private readonly string _name;
    private string _eventType = "click";
    private readonly List<NodeId> _dependencies = new();
    private readonly List<NodeId> _requiredValidNodes = new();
    private bool _allowConcurrent;
    private TimeSpan? _debounceDelay;
    private TimeSpan? _throttleInterval;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public TriggerNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public TriggerNodeBuilder OnEvent(string eventType)
    {
        _eventType = eventType;
        return this;
    }

    public TriggerNodeBuilder DependsOn(params NodeId[] dependencies)
    {
        _dependencies.AddRange(dependencies);
        return this;
    }

    public TriggerNodeBuilder RequiresValid(params NodeId[] nodes)
    {
        _requiredValidNodes.AddRange(nodes);
        return this;
    }

    public TriggerNodeBuilder AllowConcurrent(bool allow = true)
    {
        _allowConcurrent = allow;
        return this;
    }

    public TriggerNodeBuilder WithDebounce(TimeSpan delay)
    {
        _debounceDelay = delay;
        return this;
    }

    public TriggerNodeBuilder WithThrottle(TimeSpan interval)
    {
        _throttleInterval = interval;
        return this;
    }

    public TriggerNodeBuilder WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public TriggerNode Build()
    {
        return new TriggerNode(
            _id,
            _name,
            _eventType,
            _dependencies,
            _requiredValidNodes,
            _allowConcurrent,
            _debounceDelay,
            _throttleInterval,
            _metadata);
    }
}

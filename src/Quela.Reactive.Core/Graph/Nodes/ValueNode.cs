using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Validation;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a mutable value node in the reactive graph.
/// Value nodes are the input points for external data (form fields, user input).
/// </summary>
public sealed class ValueNode<T> : IStatefulNode<T>
{
    private T _value;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Value;
    public Type ValueType => typeof(T);
    public NodeMetadata Metadata { get; }

    public T Value => _value;
    public T DefaultValue { get; }

    /// <summary>
    /// Validation rules applied to this value.
    /// </summary>
    public IReadOnlyList<IValidationRule<T>> ValidationRules { get; }

    /// <summary>
    /// Current validation state.
    /// </summary>
    public ValidationState ValidationState { get; private set; } = ValidationState.Pending;

    /// <summary>
    /// Value nodes have no dependencies (they are leaf inputs).
    /// </summary>
    public IReadOnlySet<NodeId> Dependencies => EmptyDependencies.Instance;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public ValueNode(
        NodeId id,
        string name,
        T defaultValue,
        IReadOnlyList<IValidationRule<T>>? validationRules = null,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        DefaultValue = defaultValue;
        _value = defaultValue;
        ValidationRules = validationRules ?? Array.Empty<IValidationRule<T>>();
        Metadata = metadata ?? NodeMetadata.Default;
    }

    public object? GetValue() => _value;

    internal void SetValue(T value)
    {
        _value = value;
    }

    internal void SetValidationState(ValidationState state)
    {
        ValidationState = state;
    }

    internal void AddDependent(NodeId dependent)
    {
        _dependents.Add(dependent);
    }

    internal void RemoveDependent(NodeId dependent)
    {
        _dependents.Remove(dependent);
    }

    public void Reset()
    {
        _value = DefaultValue;
        ValidationState = ValidationState.Pending;
    }
}

/// <summary>
/// Empty set singleton for nodes without dependencies.
/// </summary>
internal static class EmptyDependencies
{
    public static readonly IReadOnlySet<NodeId> Instance = new HashSet<NodeId>();
}

/// <summary>
/// Builder for creating value nodes with fluent configuration.
/// </summary>
public sealed class ValueNodeBuilder<T>
{
    private readonly NodeId _id;
    private readonly string _name;
    private T _defaultValue = default!;
    private readonly List<IValidationRule<T>> _validationRules = new();
    private NodeMetadata _metadata = NodeMetadata.Default;

    public ValueNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public ValueNodeBuilder<T> WithDefault(T value)
    {
        _defaultValue = value;
        return this;
    }

    public ValueNodeBuilder<T> WithValidation(IValidationRule<T> rule)
    {
        _validationRules.Add(rule);
        return this;
    }

    public ValueNodeBuilder<T> WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public ValueNodeBuilder<T> WithTargetSelector(string selector)
    {
        _metadata = _metadata with { TargetSelector = selector };
        return this;
    }

    public ValueNodeBuilder<T> WithDebounce(TimeSpan delay)
    {
        _metadata = _metadata with { DebounceDelay = delay };
        return this;
    }

    public ValueNode<T> Build() => new(_id, _name, _defaultValue, _validationRules, _metadata);
}

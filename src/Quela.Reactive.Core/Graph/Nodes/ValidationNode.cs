using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Validation;
using Quela.Reactive.Core.Execution;

namespace Quela.Reactive.Core.Graph.Nodes;

/// <summary>
/// Represents a validation node that validates data and produces validation results.
/// Separates validation logic from value nodes for complex validation scenarios.
/// </summary>
public sealed class ValidationNode<T> : IStatefulNode<ValidationResult>, IExecutableNode
{
    private ValidationResult _cachedResult = ValidationResult.Pending;
    private bool _isDirty = true;
    private readonly HashSet<NodeId> _dependencies;
    private readonly HashSet<NodeId> _dependents = new();

    public NodeId Id { get; }
    public string Name { get; }
    public NodeType Type => NodeType.Validation;
    public Type ValueType => typeof(ValidationResult);
    public NodeMetadata Metadata { get; }

    /// <summary>
    /// The node being validated.
    /// </summary>
    public NodeId TargetNode { get; }

    /// <summary>
    /// Validation function that produces a result.
    /// </summary>
    public Func<IDependencyReader, T, ValidationResult> ValidateFunction { get; }

    /// <summary>
    /// Whether this validation can be performed asynchronously.
    /// </summary>
    public bool IsAsync { get; }

    /// <summary>
    /// Async validation function for server-side validation.
    /// </summary>
    public Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>>? AsyncValidateFunction { get; }

    /// <summary>
    /// Debounce delay before running validation.
    /// </summary>
    public TimeSpan? DebounceDelay { get; }

    public ValidationResult Value => _cachedResult;

    public bool IsDirty => _isDirty;

    public bool IsParallelizable => true;

    public int ExecutionCost => IsAsync ? 5 : 1;

    public IReadOnlySet<NodeId> Dependencies => _dependencies;

    public IReadOnlySet<NodeId> Dependents => _dependents;

    public ValidationNode(
        NodeId id,
        string name,
        NodeId targetNode,
        Func<IDependencyReader, T, ValidationResult> validateFunction,
        IEnumerable<NodeId>? additionalDependencies = null,
        Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>>? asyncValidateFunction = null,
        TimeSpan? debounceDelay = null,
        NodeMetadata? metadata = null)
    {
        Id = id;
        Name = name;
        TargetNode = targetNode;
        ValidateFunction = validateFunction;
        AsyncValidateFunction = asyncValidateFunction;
        IsAsync = asyncValidateFunction != null;
        DebounceDelay = debounceDelay;
        Metadata = metadata ?? NodeMetadata.Default;

        _dependencies = new HashSet<NodeId> { targetNode };
        if (additionalDependencies != null)
        {
            foreach (var dep in additionalDependencies)
                _dependencies.Add(dep);
        }
    }

    public object? GetValue() => _cachedResult;

    internal void SetCachedResult(ValidationResult result)
    {
        _cachedResult = result;
        _isDirty = false;
    }

    internal void MarkDirty()
    {
        _isDirty = true;
    }

    internal void AddDependent(NodeId dependent)
    {
        _dependents.Add(dependent);
    }

    internal void RemoveDependent(NodeId dependent)
    {
        _dependents.Remove(dependent);
    }

    internal void Invalidate()
    {
        _isDirty = true;
        _cachedResult = ValidationResult.Pending;
    }
}

/// <summary>
/// Builder for creating validation nodes with fluent configuration.
/// </summary>
public sealed class ValidationNodeBuilder<T>
{
    private readonly NodeId _id;
    private readonly string _name;
    private NodeId _targetNode;
    private Func<IDependencyReader, T, ValidationResult>? _validateFunction;
    private readonly List<NodeId> _additionalDependencies = new();
    private Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>>? _asyncValidateFunction;
    private TimeSpan? _debounceDelay;
    private NodeMetadata _metadata = NodeMetadata.Default;

    public ValidationNodeBuilder(NodeId id, string name)
    {
        _id = id;
        _name = name;
    }

    public ValidationNodeBuilder<T> ForNode(NodeId targetNode)
    {
        _targetNode = targetNode;
        return this;
    }

    public ValidationNodeBuilder<T> Validate(Func<IDependencyReader, T, ValidationResult> function)
    {
        _validateFunction = function;
        return this;
    }

    public ValidationNodeBuilder<T> ValidateSimple(Func<T, ValidationResult> function)
    {
        _validateFunction = (_, value) => function(value);
        return this;
    }

    public ValidationNodeBuilder<T> ValidateAsync(
        Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>> function)
    {
        _asyncValidateFunction = function;
        return this;
    }

    public ValidationNodeBuilder<T> DependsOn(params NodeId[] dependencies)
    {
        _additionalDependencies.AddRange(dependencies);
        return this;
    }

    public ValidationNodeBuilder<T> WithDebounce(TimeSpan delay)
    {
        _debounceDelay = delay;
        return this;
    }

    public ValidationNodeBuilder<T> WithMetadata(NodeMetadata metadata)
    {
        _metadata = metadata;
        return this;
    }

    public ValidationNode<T> Build()
    {
        if (_targetNode == default)
            throw new InvalidOperationException("Target node must be specified");

        if (_validateFunction == null)
            throw new InvalidOperationException("Validate function must be specified");

        return new ValidationNode<T>(
            _id,
            _name,
            _targetNode,
            _validateFunction,
            _additionalDependencies,
            _asyncValidateFunction,
            _debounceDelay,
            _metadata);
    }
}

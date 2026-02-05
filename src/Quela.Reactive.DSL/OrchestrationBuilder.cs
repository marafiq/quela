using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Core.Graph.Nodes;
using Quela.Reactive.Core.Validation;
using Quela.Reactive.Core.Execution;
using Quela.Reactive.Core.Effects;

namespace Quela.Reactive.DSL;

/// <summary>
/// Fluent DSL for building reactive orchestrations.
/// </summary>
public sealed class OrchestrationBuilder
{
    private readonly ReactiveGraphBuilder _graphBuilder;
    private readonly Dictionary<string, INode> _namedNodes = new();

    public OrchestrationBuilder(string orchestrationId, string name, string? version = null)
    {
        _graphBuilder = new ReactiveGraphBuilder(orchestrationId, name, version);
    }

    /// <summary>
    /// Starts building an orchestration.
    /// </summary>
    public static OrchestrationBuilder Create(string id, string name, string? version = null) =>
        new(id, name, version);

    /// <summary>
    /// Defines a value field (input).
    /// </summary>
    public ValueFieldBuilder<T> Field<T>(string name) =>
        new(this, name);

    /// <summary>
    /// Defines a computed value.
    /// </summary>
    public ComputedFieldBuilder<T> Computed<T>(string name) =>
        new(this, name);

    /// <summary>
    /// Defines an effect (side effect operation).
    /// </summary>
    public EffectBuilder<T> Effect<T>(string name) =>
        new(this, name);

    /// <summary>
    /// Defines a validation node.
    /// </summary>
    public ValidationBuilder<T> Validation<T>(string name) =>
        new(this, name);

    /// <summary>
    /// Defines a conditional branch.
    /// </summary>
    public ConditionalBuilder When(string name) =>
        new(this, name);

    /// <summary>
    /// Defines a trigger (action).
    /// </summary>
    public TriggerBuilder Trigger(string name) =>
        new(this, name);

    /// <summary>
    /// Defines an aggregator node.
    /// </summary>
    public AggregatorBuilder<TSource, TResult> Aggregate<TSource, TResult>(string name) =>
        new(this, name);

    /// <summary>
    /// Defines a partial (nested sub-graph).
    /// </summary>
    public PartialBuilder Partial(string name) =>
        new(this, name);

    /// <summary>
    /// Gets a reference to a named node.
    /// </summary>
    public NodeRef<T> Ref<T>(string name) => new(name);

    /// <summary>
    /// Gets a generic reference to a named node.
    /// </summary>
    public NodeRef Ref(string name) => new(name);

    /// <summary>
    /// Sets graph metadata.
    /// </summary>
    public OrchestrationBuilder WithMetadata(GraphMetadata metadata)
    {
        _graphBuilder.WithMetadata(metadata);
        return this;
    }

    /// <summary>
    /// Compiles the orchestration into a reactive graph.
    /// </summary>
    public ReactiveGraph Build()
    {
        return _graphBuilder.Build();
    }

    internal void RegisterNode(string name, INode node)
    {
        _namedNodes[name] = node;
        _graphBuilder.AddNode(node);
    }

    internal NodeId ResolveNodeId(string name)
    {
        if (_namedNodes.TryGetValue(name, out var node))
            return node.Id;
        return NodeId.Create(name);
    }

    internal INode? GetNode(string name)
    {
        return _namedNodes.TryGetValue(name, out var node) ? node : null;
    }
}

/// <summary>
/// Reference to a node by name.
/// </summary>
public readonly record struct NodeRef(string Name)
{
    public NodeId ToNodeId() => NodeId.Create(Name);
}

/// <summary>
/// Typed reference to a node by name.
/// </summary>
public readonly record struct NodeRef<T>(string Name)
{
    public NodeId ToNodeId() => NodeId.Create(Name);
}

/// <summary>
/// Builder for value fields.
/// </summary>
public sealed class ValueFieldBuilder<T>
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private T _defaultValue = default!;
    private readonly List<IValidationRule<T>> _validationRules = new();
    private string? _targetSelector;
    private TimeSpan? _debounce;
    private bool _isRequired;

    internal ValueFieldBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public ValueFieldBuilder<T> Default(T value)
    {
        _defaultValue = value;
        return this;
    }

    public ValueFieldBuilder<T> Required(string? message = null)
    {
        _isRequired = true;
        if (typeof(T) == typeof(string))
        {
            _validationRules.Add((IValidationRule<T>)(object)ValidationRules.Required(message));
        }
        return this;
    }

    public ValueFieldBuilder<T> Validate(IValidationRule<T> rule)
    {
        _validationRules.Add(rule);
        return this;
    }

    public ValueFieldBuilder<T> Validate(string ruleName, string errorMessage, Func<T, bool> predicate)
    {
        _validationRules.Add(new PredicateRule<T>(ruleName, errorMessage, predicate));
        return this;
    }

    public ValueFieldBuilder<T> BindTo(string selector)
    {
        _targetSelector = selector;
        return this;
    }

    public ValueFieldBuilder<T> Debounce(TimeSpan delay)
    {
        _debounce = delay;
        return this;
    }

    public ValueFieldBuilder<T> Debounce(int milliseconds)
    {
        _debounce = TimeSpan.FromMilliseconds(milliseconds);
        return this;
    }

    public OrchestrationBuilder Add()
    {
        var nodeId = NodeId.Create(_name);
        var metadata = new NodeMetadata
        {
            TargetSelector = _targetSelector,
            DebounceDelay = _debounce
        };

        var node = new ValueNode<T>(
            nodeId,
            _name,
            _defaultValue,
            _validationRules,
            metadata);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for computed fields.
/// </summary>
public sealed class ComputedFieldBuilder<T>
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private Func<IDependencyReader, T>? _computeFunction;
    private readonly List<string> _dependencies = new();
    private string? _targetSelector;

    internal ComputedFieldBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public ComputedFieldBuilder<T> DependsOn(params string[] nodeNames)
    {
        _dependencies.AddRange(nodeNames);
        return this;
    }

    public ComputedFieldBuilder<T> DependsOn(params NodeRef[] refs)
    {
        _dependencies.AddRange(refs.Select(r => r.Name));
        return this;
    }

    public ComputedFieldBuilder<T> Compute(Func<IDependencyReader, T> function)
    {
        _computeFunction = function;
        return this;
    }

    public ComputedFieldBuilder<T> BindTo(string selector)
    {
        _targetSelector = selector;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (_computeFunction == null)
            throw new InvalidOperationException("Compute function must be specified");

        var nodeId = NodeId.Create(_name);
        var metadata = new NodeMetadata { TargetSelector = _targetSelector };
        var dependencyIds = _dependencies.Select(d => _parent.ResolveNodeId(d));

        var node = new ComputedNode<T>(
            nodeId,
            _name,
            _computeFunction,
            dependencyIds,
            metadata: metadata);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for effects.
/// </summary>
public sealed class EffectBuilder<TResult>
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private Func<IEffectContext, Task<EffectResult<TResult>>>? _effectFunction;
    private readonly List<string> _dependencies = new();
    private RetryPolicy _retryPolicy = RetryPolicy.Default;
    private TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private Func<IDependencyReader, string>? _idempotencyKey;
    private string? _cancellationScope;
    private bool _isParallelizable = true;

    internal EffectBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public EffectBuilder<TResult> DependsOn(params string[] nodeNames)
    {
        _dependencies.AddRange(nodeNames);
        return this;
    }

    public EffectBuilder<TResult> Execute(Func<IEffectContext, Task<EffectResult<TResult>>> function)
    {
        _effectFunction = function;
        return this;
    }

    public EffectBuilder<TResult> ExecuteAsync(Func<IEffectContext, Task<TResult>> function)
    {
        _effectFunction = async ctx =>
        {
            var result = await function(ctx);
            return EffectResult<TResult>.Success(result);
        };
        return this;
    }

    public EffectBuilder<TResult> WithRetry(int maxRetries = 3)
    {
        _retryPolicy = _retryPolicy with { MaxRetries = maxRetries };
        return this;
    }

    public EffectBuilder<TResult> WithRetry(RetryPolicy policy)
    {
        _retryPolicy = policy;
        return this;
    }

    public EffectBuilder<TResult> WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public EffectBuilder<TResult> WithTimeout(int seconds)
    {
        _timeout = TimeSpan.FromSeconds(seconds);
        return this;
    }

    public EffectBuilder<TResult> WithIdempotencyKey(Func<IDependencyReader, string> keyGenerator)
    {
        _idempotencyKey = keyGenerator;
        return this;
    }

    public EffectBuilder<TResult> InCancellationScope(string scope)
    {
        _cancellationScope = scope;
        return this;
    }

    public EffectBuilder<TResult> Sequential()
    {
        _isParallelizable = false;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (_effectFunction == null)
            throw new InvalidOperationException("Effect function must be specified");

        var nodeId = NodeId.Create(_name);
        var dependencyIds = _dependencies.Select(d => _parent.ResolveNodeId(d));

        var node = new EffectNode<TResult>(
            nodeId,
            _name,
            _effectFunction,
            dependencyIds,
            _retryPolicy,
            _timeout,
            _idempotencyKey,
            _cancellationScope,
            _isParallelizable);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for validation nodes.
/// </summary>
public sealed class ValidationBuilder<T>
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private string? _targetNode;
    private Func<IDependencyReader, T, ValidationResult>? _validateFunction;
    private Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>>? _asyncValidateFunction;
    private readonly List<string> _additionalDependencies = new();
    private TimeSpan? _debounce;

    internal ValidationBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public ValidationBuilder<T> For(string nodeName)
    {
        _targetNode = nodeName;
        return this;
    }

    public ValidationBuilder<T> For(NodeRef<T> nodeRef)
    {
        _targetNode = nodeRef.Name;
        return this;
    }

    public ValidationBuilder<T> DependsOn(params string[] nodeNames)
    {
        _additionalDependencies.AddRange(nodeNames);
        return this;
    }

    public ValidationBuilder<T> Validate(Func<T, ValidationResult> function)
    {
        _validateFunction = (_, value) => function(value);
        return this;
    }

    public ValidationBuilder<T> Validate(Func<IDependencyReader, T, ValidationResult> function)
    {
        _validateFunction = function;
        return this;
    }

    public ValidationBuilder<T> ValidateAsync(Func<T, CancellationToken, Task<ValidationResult>> function)
    {
        _asyncValidateFunction = (_, value, ct) => function(value, ct);
        return this;
    }

    public ValidationBuilder<T> ValidateAsync(
        Func<IDependencyReader, T, CancellationToken, Task<ValidationResult>> function)
    {
        _asyncValidateFunction = function;
        return this;
    }

    public ValidationBuilder<T> Debounce(TimeSpan delay)
    {
        _debounce = delay;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (string.IsNullOrEmpty(_targetNode))
            throw new InvalidOperationException("Target node must be specified");

        if (_validateFunction == null)
            throw new InvalidOperationException("Validate function must be specified");

        var nodeId = NodeId.Create(_name);
        var targetNodeId = _parent.ResolveNodeId(_targetNode);
        var additionalDeps = _additionalDependencies.Select(d => _parent.ResolveNodeId(d));

        var node = new ValidationNode<T>(
            nodeId,
            _name,
            targetNodeId,
            _validateFunction,
            additionalDeps,
            _asyncValidateFunction,
            _debounce);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for conditional nodes.
/// </summary>
public sealed class ConditionalBuilder
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private Func<IDependencyReader, bool>? _condition;
    private readonly List<string> _dependencies = new();
    private readonly List<string> _trueBranch = new();
    private readonly List<string> _falseBranch = new();

    internal ConditionalBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public ConditionalBuilder DependsOn(params string[] nodeNames)
    {
        _dependencies.AddRange(nodeNames);
        return this;
    }

    public ConditionalBuilder Condition(Func<IDependencyReader, bool> condition)
    {
        _condition = condition;
        return this;
    }

    public ConditionalBuilder ThenActivate(params string[] nodeNames)
    {
        _trueBranch.AddRange(nodeNames);
        return this;
    }

    public ConditionalBuilder ElseActivate(params string[] nodeNames)
    {
        _falseBranch.AddRange(nodeNames);
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (_condition == null)
            throw new InvalidOperationException("Condition must be specified");

        var nodeId = NodeId.Create(_name);
        var dependencyIds = _dependencies.Select(d => _parent.ResolveNodeId(d));
        var trueIds = _trueBranch.Select(n => _parent.ResolveNodeId(n));
        var falseIds = _falseBranch.Select(n => _parent.ResolveNodeId(n));

        var node = new ConditionalNode(
            nodeId,
            _name,
            _condition,
            dependencyIds,
            trueIds,
            falseIds);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for trigger nodes.
/// </summary>
public sealed class TriggerBuilder
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private string _eventType = "click";
    private readonly List<string> _dependencies = new();
    private readonly List<string> _requiredValidNodes = new();
    private bool _allowConcurrent;
    private TimeSpan? _debounce;
    private TimeSpan? _throttle;
    private string? _targetSelector;

    internal TriggerBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public TriggerBuilder OnEvent(string eventType)
    {
        _eventType = eventType;
        return this;
    }

    public TriggerBuilder OnClick() => OnEvent("click");
    public TriggerBuilder OnSubmit() => OnEvent("submit");
    public TriggerBuilder OnChange() => OnEvent("change");

    public TriggerBuilder DependsOn(params string[] nodeNames)
    {
        _dependencies.AddRange(nodeNames);
        return this;
    }

    public TriggerBuilder RequiresValid(params string[] nodeNames)
    {
        _requiredValidNodes.AddRange(nodeNames);
        return this;
    }

    public TriggerBuilder AllowConcurrent(bool allow = true)
    {
        _allowConcurrent = allow;
        return this;
    }

    public TriggerBuilder Debounce(TimeSpan delay)
    {
        _debounce = delay;
        return this;
    }

    public TriggerBuilder Throttle(TimeSpan interval)
    {
        _throttle = interval;
        return this;
    }

    public TriggerBuilder BindTo(string selector)
    {
        _targetSelector = selector;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        var nodeId = NodeId.Create(_name);
        var dependencyIds = _dependencies.Select(d => _parent.ResolveNodeId(d));
        var requiredIds = _requiredValidNodes.Select(n => _parent.ResolveNodeId(n));
        var metadata = new NodeMetadata { TargetSelector = _targetSelector };

        var node = new TriggerNode(
            nodeId,
            _name,
            _eventType,
            dependencyIds,
            requiredIds,
            _allowConcurrent,
            _debounce,
            _throttle,
            metadata);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for aggregator nodes.
/// </summary>
public sealed class AggregatorBuilder<TSource, TResult>
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private readonly List<string> _sources = new();
    private Func<IReadOnlyList<TSource>, TResult>? _aggregateFunction;
    private AggregationStrategy _strategy = AggregationStrategy.WaitAll;
    private int _minimumSources;
    private TimeSpan? _timeout;

    internal AggregatorBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public AggregatorBuilder<TSource, TResult> FromSources(params string[] nodeNames)
    {
        _sources.AddRange(nodeNames);
        return this;
    }

    public AggregatorBuilder<TSource, TResult> Aggregate(Func<IReadOnlyList<TSource>, TResult> function)
    {
        _aggregateFunction = function;
        return this;
    }

    public AggregatorBuilder<TSource, TResult> WaitAll()
    {
        _strategy = AggregationStrategy.WaitAll;
        return this;
    }

    public AggregatorBuilder<TSource, TResult> WaitAny()
    {
        _strategy = AggregationStrategy.WaitAny;
        return this;
    }

    public AggregatorBuilder<TSource, TResult> WaitForAtLeast(int count)
    {
        _strategy = AggregationStrategy.WaitN;
        _minimumSources = count;
        return this;
    }

    public AggregatorBuilder<TSource, TResult> WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (_sources.Count == 0)
            throw new InvalidOperationException("At least one source must be specified");

        if (_aggregateFunction == null)
            throw new InvalidOperationException("Aggregate function must be specified");

        var nodeId = NodeId.Create(_name);
        var sourceIds = _sources.Select(s => _parent.ResolveNodeId(s));

        var node = new AggregatorNode<TSource, TResult>(
            nodeId,
            _name,
            sourceIds,
            _aggregateFunction,
            _strategy,
            _minimumSources,
            _timeout);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

/// <summary>
/// Builder for partial nodes.
/// </summary>
public sealed class PartialBuilder
{
    private readonly OrchestrationBuilder _parent;
    private readonly string _name;
    private string? _partialId;
    private ReactiveGraph? _subGraph;
    private readonly Dictionary<string, string> _inputMappings = new();
    private readonly Dictionary<string, string> _outputMappings = new();
    private string? _template;
    private string? _insertionTarget;
    private InsertionPosition _insertionPosition = InsertionPosition.Replace;

    internal PartialBuilder(OrchestrationBuilder parent, string name)
    {
        _parent = parent;
        _name = name;
    }

    public PartialBuilder WithPartialId(string partialId)
    {
        _partialId = partialId;
        return this;
    }

    public PartialBuilder WithSubGraph(ReactiveGraph subGraph)
    {
        _subGraph = subGraph;
        return this;
    }

    public PartialBuilder WithSubGraph(Func<OrchestrationBuilder, ReactiveGraph> buildFunc)
    {
        var subBuilder = OrchestrationBuilder.Create($"{_name}_sub", $"{_name}_sub");
        _subGraph = buildFunc(subBuilder);
        return this;
    }

    public PartialBuilder MapInput(string parentNode, string subGraphNode)
    {
        _inputMappings[parentNode] = subGraphNode;
        return this;
    }

    public PartialBuilder MapOutput(string subGraphNode, string parentNode)
    {
        _outputMappings[subGraphNode] = parentNode;
        return this;
    }

    public PartialBuilder WithTemplate(string template)
    {
        _template = template;
        return this;
    }

    public PartialBuilder InsertAt(string target, InsertionPosition position = InsertionPosition.Replace)
    {
        _insertionTarget = target;
        _insertionPosition = position;
        return this;
    }

    public OrchestrationBuilder Add()
    {
        if (string.IsNullOrWhiteSpace(_partialId))
            _partialId = _name;

        if (_subGraph == null)
            throw new InvalidOperationException("Sub-graph must be specified");

        var nodeId = NodeId.Create(_name);
        var inputIds = _inputMappings.ToDictionary(
            kvp => _parent.ResolveNodeId(kvp.Key),
            kvp => NodeId.Create(kvp.Value));
        var outputIds = _outputMappings.ToDictionary(
            kvp => NodeId.Create(kvp.Key),
            kvp => _parent.ResolveNodeId(kvp.Value));

        var node = new PartialNode(
            nodeId,
            _name,
            _partialId,
            _subGraph,
            inputIds,
            outputIds,
            _template,
            _insertionTarget,
            _insertionPosition);

        _parent.RegisterNode(_name, node);
        return _parent;
    }
}

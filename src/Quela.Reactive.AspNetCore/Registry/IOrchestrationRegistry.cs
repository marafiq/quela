using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.DSL.Compiler;

namespace Quela.Reactive.AspNetCore;

/// <summary>
/// Registry for compiled orchestrations.
/// </summary>
public interface IOrchestrationRegistry
{
    /// <summary>
    /// Registers an orchestration.
    /// </summary>
    void Register(ReactiveGraph graph);

    /// <summary>
    /// Gets an orchestration by ID.
    /// </summary>
    ReactiveGraph? Get(OrchestrationId id);

    /// <summary>
    /// Gets a compiled orchestration by ID.
    /// </summary>
    CompiledGraph? GetCompiled(OrchestrationId id);

    /// <summary>
    /// Gets all registered orchestration IDs.
    /// </summary>
    IEnumerable<OrchestrationId> GetAllIds();

    /// <summary>
    /// Checks if an orchestration is registered.
    /// </summary>
    bool Contains(OrchestrationId id);
}

/// <summary>
/// In-memory orchestration registry.
/// </summary>
public sealed class OrchestrationRegistry : IOrchestrationRegistry
{
    private readonly Dictionary<OrchestrationId, CompiledGraph> _orchestrations = new();
    private readonly GraphCompiler _compiler;
    private readonly object _lock = new();

    public OrchestrationRegistry(
        GraphCompiler compiler,
        IEnumerable<OrchestrationRegistration> registrations)
    {
        _compiler = compiler;

        // Register all orchestrations from DI
        foreach (var registration in registrations)
        {
            Register(registration.Graph);
        }
    }

    public void Register(ReactiveGraph graph)
    {
        var compiled = _compiler.Compile(graph);

        lock (_lock)
        {
            _orchestrations[graph.Id] = compiled;
        }
    }

    public ReactiveGraph? Get(OrchestrationId id)
    {
        lock (_lock)
        {
            return _orchestrations.TryGetValue(id, out var compiled)
                ? compiled.Graph
                : null;
        }
    }

    public CompiledGraph? GetCompiled(OrchestrationId id)
    {
        lock (_lock)
        {
            return _orchestrations.TryGetValue(id, out var compiled)
                ? compiled
                : null;
        }
    }

    public IEnumerable<OrchestrationId> GetAllIds()
    {
        lock (_lock)
        {
            return _orchestrations.Keys.ToList();
        }
    }

    public bool Contains(OrchestrationId id)
    {
        lock (_lock)
        {
            return _orchestrations.ContainsKey(id);
        }
    }
}

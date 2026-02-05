using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quela.Reactive.Core.Execution;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Runtime.Execution;
using Quela.Reactive.Runtime.Transactions;
using Quela.Reactive.Runtime.Scheduling;
using Quela.Reactive.Runtime.Effects;
using Quela.Reactive.Runtime.Patches;
using Quela.Reactive.DSL.Compiler;

namespace Quela.Reactive.AspNetCore;

/// <summary>
/// Extension methods for registering Quela services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Quela reactive orchestration services to the service collection.
    /// </summary>
    public static IServiceCollection AddQuelaReactive(
        this IServiceCollection services,
        Action<QuelaOptions>? configure = null)
    {
        var options = new QuelaOptions();
        configure?.Invoke(options);

        // Register options
        services.AddSingleton(options);
        services.AddSingleton(options.TransactionOptions);
        services.AddSingleton(options.SchedulerOptions);
        services.AddSingleton(options.EffectRuntimeOptions);
        services.AddSingleton(options.PatchGeneratorOptions);
        services.AddSingleton(options.EngineOptions);

        // Register core services
        services.AddSingleton<TransactionManager>();
        services.AddSingleton<ExecutionScheduler>();
        services.AddSingleton<EffectRuntime>();
        services.AddSingleton<PatchGenerator>();
        services.AddSingleton<DebounceManager>();
        services.AddSingleton<OrchestrationEngine>();
        services.AddSingleton<GraphCompiler>();

        // Register session management
        services.AddSingleton<ISessionManager, InMemorySessionManager>();

        // Register orchestration registry
        services.AddSingleton<IOrchestrationRegistry, OrchestrationRegistry>();

        // Register HTTP client factory if not already registered
        services.TryAddSingleton<IHttpClientFactory>(new DefaultHttpClientFactory());

        return services;
    }

    /// <summary>
    /// Registers an orchestration with the registry.
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        ReactiveGraph graph)
    {
        services.AddSingleton(new OrchestrationRegistration(graph));
        return services;
    }

    /// <summary>
    /// Registers an orchestration using a builder function.
    /// </summary>
    public static IServiceCollection AddOrchestration(
        this IServiceCollection services,
        string id,
        string name,
        Func<DSL.OrchestrationBuilder, ReactiveGraph> buildFunc)
    {
        var builder = DSL.OrchestrationBuilder.Create(id, name);
        var graph = buildFunc(builder);
        return services.AddOrchestration(graph);
    }
}

/// <summary>
/// Options for Quela configuration.
/// </summary>
public sealed class QuelaOptions
{
    public TransactionOptions TransactionOptions { get; set; } = new();
    public SchedulerOptions SchedulerOptions { get; set; } = new();
    public EffectRuntimeOptions EffectRuntimeOptions { get; set; } = new();
    public PatchGeneratorOptions PatchGeneratorOptions { get; set; } = new();
    public OrchestrationEngineOptions EngineOptions { get; set; } = new();
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int MaxSessionsPerUser { get; set; } = 10;
}

/// <summary>
/// Registration for an orchestration.
/// </summary>
public sealed record OrchestrationRegistration(ReactiveGraph Graph);

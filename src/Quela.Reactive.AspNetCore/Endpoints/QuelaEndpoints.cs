using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Payloads;
using Quela.Reactive.Runtime.Execution;

namespace Quela.Reactive.AspNetCore;

/// <summary>
/// Extension methods for mapping Quela endpoints.
/// </summary>
public static class QuelaEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Maps Quela orchestration endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapQuelaEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/quela")
    {
        var group = endpoints.MapGroup(prefix);

        // Create session endpoint
        group.MapPost("/sessions", CreateSessionAsync)
            .WithName("CreateQuelaSession")
            .WithDescription("Creates a new orchestration session");

        // Process changeset endpoint
        group.MapPost("/sessions/{sessionId}/changesets", ProcessChangesetAsync)
            .WithName("ProcessQuelaChangeset")
            .WithDescription("Processes a changeset and returns patches");

        // Get session state endpoint
        group.MapGet("/sessions/{sessionId}", GetSessionStateAsync)
            .WithName("GetQuelaSession")
            .WithDescription("Gets the current state of a session");

        // Delete session endpoint
        group.MapDelete("/sessions/{sessionId}", DeleteSessionAsync)
            .WithName("DeleteQuelaSession")
            .WithDescription("Deletes a session");

        // List orchestrations endpoint
        group.MapGet("/orchestrations", ListOrchestrationsAsync)
            .WithName("ListQuelaOrchestrations")
            .WithDescription("Lists all registered orchestrations");

        // Get orchestration info endpoint
        group.MapGet("/orchestrations/{orchestrationId}", GetOrchestrationInfoAsync)
            .WithName("GetQuelaOrchestration")
            .WithDescription("Gets information about an orchestration");

        return endpoints;
    }

    private static async Task<IResult> CreateSessionAsync(
        HttpContext context,
        ISessionManager sessionManager,
        IOrchestrationRegistry registry)
    {
        var request = await JsonSerializer.DeserializeAsync<CreateSessionRequest>(
            context.Request.Body,
            JsonOptions);

        if (request == null || string.IsNullOrEmpty(request.OrchestrationId))
        {
            return Results.BadRequest(new { error = "OrchestrationId is required" });
        }

        var orchestrationId = OrchestrationId.Create(request.OrchestrationId);
        var graph = registry.Get(orchestrationId);

        if (graph == null)
        {
            return Results.NotFound(new { error = $"Orchestration '{request.OrchestrationId}' not found" });
        }

        var session = await sessionManager.CreateSessionAsync(
            orchestrationId,
            graph,
            context.RequestAborted);

        return Results.Ok(new CreateSessionResponse
        {
            SessionId = session.SessionId.ToString(),
            OrchestrationId = orchestrationId.Value,
            CreatedAt = session.CreatedAt.ToUnixTimeMilliseconds()
        });
    }

    private static async Task<IResult> ProcessChangesetAsync(
        HttpContext context,
        string sessionId,
        ISessionManager sessionManager,
        OrchestrationEngine engine)
    {
        var session = await sessionManager.GetSessionAsync(
            SessionId.Parse(sessionId),
            context.RequestAborted);

        if (session == null)
        {
            return Results.NotFound(new { error = "Session not found" });
        }

        var changeset = await JsonSerializer.DeserializeAsync<ChangesetPayload>(
            context.Request.Body,
            JsonOptions);

        if (changeset == null)
        {
            return Results.BadRequest(new { error = "Invalid changeset payload" });
        }

        var response = await engine.ProcessChangesetAsync(
            changeset,
            session,
            context.RequestAborted);

        return Results.Ok(response);
    }

    private static async Task<IResult> GetSessionStateAsync(
        HttpContext context,
        string sessionId,
        ISessionManager sessionManager)
    {
        var session = await sessionManager.GetSessionAsync(
            SessionId.Parse(sessionId),
            context.RequestAborted);

        if (session == null)
        {
            return Results.NotFound(new { error = "Session not found" });
        }

        var state = new SessionStateResponse
        {
            SessionId = session.SessionId.ToString(),
            OrchestrationId = session.Graph.Id.Value,
            CreatedAt = session.CreatedAt.ToUnixTimeMilliseconds(),
            LastActivityAt = session.LastActivityAt.ToUnixTimeMilliseconds(),
            CurrentSequence = session.SequenceGenerator.Current.Value
        };

        return Results.Ok(state);
    }

    private static async Task<IResult> DeleteSessionAsync(
        HttpContext context,
        string sessionId,
        ISessionManager sessionManager)
    {
        await sessionManager.RemoveSessionAsync(
            SessionId.Parse(sessionId),
            context.RequestAborted);

        return Results.NoContent();
    }

    private static Task<IResult> ListOrchestrationsAsync(
        IOrchestrationRegistry registry)
    {
        var orchestrations = registry.GetAllIds()
            .Select(id =>
            {
                var graph = registry.Get(id);
                return new OrchestrationInfo
                {
                    Id = id.Value,
                    Name = graph?.Name ?? "",
                    Version = graph?.Version ?? "",
                    NodeCount = graph?.NodeCount ?? 0
                };
            })
            .ToList();

        return Task.FromResult(Results.Ok(orchestrations));
    }

    private static Task<IResult> GetOrchestrationInfoAsync(
        string orchestrationId,
        IOrchestrationRegistry registry)
    {
        var id = OrchestrationId.Create(orchestrationId);
        var compiled = registry.GetCompiled(id);

        if (compiled == null)
        {
            return Task.FromResult(Results.NotFound(new { error = "Orchestration not found" }));
        }

        var graph = compiled.Graph;
        var info = new OrchestrationDetailInfo
        {
            Id = id.Value,
            Name = graph.Name,
            Version = graph.Version,
            NodeCount = graph.NodeCount,
            EdgeCount = graph.EdgeCount,
            Metadata = compiled.Metadata
        };

        return Task.FromResult(Results.Ok(info));
    }
}

// Request/Response DTOs

public sealed record CreateSessionRequest
{
    public string OrchestrationId { get; init; } = "";
    public Dictionary<string, object>? InitialValues { get; init; }
}

public sealed record CreateSessionResponse
{
    public string SessionId { get; init; } = "";
    public string OrchestrationId { get; init; } = "";
    public long CreatedAt { get; init; }
}

public sealed record SessionStateResponse
{
    public string SessionId { get; init; } = "";
    public string OrchestrationId { get; init; } = "";
    public long CreatedAt { get; init; }
    public long LastActivityAt { get; init; }
    public long CurrentSequence { get; init; }
}

public sealed record OrchestrationInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public int NodeCount { get; init; }
}

public sealed record OrchestrationDetailInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public int NodeCount { get; init; }
    public int EdgeCount { get; init; }
    public object? Metadata { get; init; }
}

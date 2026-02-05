// Quela Reactive Orchestration - Standalone Demo
// This is a self-contained demo that doesn't require external packages

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// In-memory state management
var orchestrations = new ConcurrentDictionary<string, Orchestration>();
var sessions = new ConcurrentDictionary<string, Session>();

// Register the contact form orchestration
var contactForm = CreateContactFormOrchestration();
orchestrations[contactForm.Id] = contactForm;

app.UseDefaultFiles();
app.UseStaticFiles();

// Health check
app.MapGet("/api/health", () => new { status = "ok", timestamp = DateTime.UtcNow });

// List orchestrations
app.MapGet("/api/quela/orchestrations", () =>
    orchestrations.Values.Select(o => new
    {
        id = o.Id,
        name = o.Name,
        version = o.Version,
        nodeCount = o.Nodes.Count
    }));

// Get orchestration details
app.MapGet("/api/debug/orchestration/{id}", (string id) =>
{
    if (!orchestrations.TryGetValue(id, out var orchestration))
        return Results.NotFound(new { error = "Orchestration not found" });

    return Results.Ok(new
    {
        id = orchestration.Id,
        name = orchestration.Name,
        version = orchestration.Version,
        nodeCount = orchestration.Nodes.Count,
        edgeCount = orchestration.Edges.Count,
        nodes = orchestration.Nodes.Select(n => new
        {
            id = n.Id,
            name = n.Name,
            type = n.Type.ToString(),
            dependencies = n.Dependencies.ToList()
        }).ToList(),
        topologicalOrder = orchestration.TopologicalOrder,
        analysis = new
        {
            maxDepth = orchestration.MaxDepth,
            criticalPathLength = orchestration.TopologicalOrder.Count
        }
    });
});

// Create session
app.MapPost("/api/quela/sessions", async (HttpContext ctx) =>
{
    var request = await ctx.Request.ReadFromJsonAsync<CreateSessionRequest>();

    if (request == null || string.IsNullOrEmpty(request.OrchestrationId))
        return Results.BadRequest(new { error = "OrchestrationId is required" });

    if (!orchestrations.TryGetValue(request.OrchestrationId, out var orchestration))
        return Results.NotFound(new { error = "Orchestration not found" });

    var sessionId = Guid.NewGuid().ToString("N");
    var session = new Session
    {
        Id = sessionId,
        OrchestrationId = orchestration.Id,
        CreatedAt = DateTimeOffset.UtcNow,
        State = orchestration.Nodes.ToDictionary(n => n.Id, n => n.DefaultValue),
        Sequence = 1
    };

    sessions[sessionId] = session;

    Console.WriteLine($"[Session Created] {sessionId} for orchestration: {orchestration.Name}");

    return Results.Ok(new
    {
        sessionId = sessionId,
        orchestrationId = orchestration.Id,
        createdAt = session.CreatedAt.ToUnixTimeMilliseconds()
    });
});

// Process changeset
app.MapPost("/api/quela/sessions/{sessionId}/changesets", async (string sessionId, HttpContext ctx) =>
{
    if (!sessions.TryGetValue(sessionId, out var session))
        return Results.NotFound(new { error = "Session not found" });

    if (!orchestrations.TryGetValue(session.OrchestrationId, out var orchestration))
        return Results.NotFound(new { error = "Orchestration not found" });

    var changeset = await ctx.Request.ReadFromJsonAsync<Changeset>();
    if (changeset == null)
        return Results.BadRequest(new { error = "Invalid changeset" });

    session.LastActivityAt = DateTimeOffset.UtcNow;

    Console.WriteLine($"[Changeset] Session {sessionId}: {changeset.Changes?.Count ?? 0} changes, {changeset.Actions?.Count ?? 0} actions");

    var patches = new List<UIPatch>();
    var effects = new List<EffectResult>();
    var dirtyNodes = new HashSet<string>();

    // Apply changes
    foreach (var change in changeset.Changes ?? Enumerable.Empty<FieldChange>())
    {
        Console.WriteLine($"  [Change] {change.NodeId} = {change.Value}");
        session.State[change.NodeId] = change.Value;
        dirtyNodes.Add(change.NodeId);

        // Mark dependents as dirty
        foreach (var node in orchestration.Nodes.Where(n => n.Dependencies.Contains(change.NodeId)))
        {
            dirtyNodes.Add(node.Id);
        }
    }

    // Execute nodes in topological order
    foreach (var nodeId in orchestration.TopologicalOrder.Where(id => dirtyNodes.Contains(id) || orchestration.Nodes.First(n => n.Id == id).Type == NodeType.Computed))
    {
        var node = orchestration.Nodes.First(n => n.Id == nodeId);

        if (node.Type == NodeType.Computed && node.ComputeFn != null)
        {
            var result = node.ComputeFn(session.State);
            session.State[nodeId] = result;
            Console.WriteLine($"  [Computed] {nodeId} = {result}");

            if (!string.IsNullOrEmpty(node.TargetSelector))
            {
                patches.Add(new UIPatch
                {
                    Target = node.TargetSelector,
                    Operation = "SetValue",
                    Value = result
                });
            }
        }

        // Generate validation patches
        if (node.ValidationFn != null)
        {
            var value = session.State.GetValueOrDefault(nodeId);
            var (isValid, errorMessage) = node.ValidationFn(value);

            patches.Add(new UIPatch
            {
                Target = node.TargetSelector ?? $"#{nodeId}",
                Operation = "SetValid",
                Value = new { valid = isValid, errorMessage = errorMessage }
            });
        }
    }

    // Process actions
    foreach (var action in changeset.Actions ?? Enumerable.Empty<ActionRequest>())
    {
        Console.WriteLine($"  [Action] {action.ActionId}");

        if (action.ActionId == "submit")
        {
            // Execute submit effect
            var effectNode = orchestration.Nodes.FirstOrDefault(n => n.Id == "submitForm");
            if (effectNode?.EffectFn != null)
            {
                Console.WriteLine("  [Effect] Executing submitForm...");
                await Task.Delay(1000); // Simulate API delay

                var name = session.State.GetValueOrDefault("name")?.ToString() ?? "";
                var email = session.State.GetValueOrDefault("email")?.ToString() ?? "";

                var effectResult = new EffectResult
                {
                    EffectId = "submitForm",
                    Status = "Completed",
                    Result = new
                    {
                        Success = true,
                        Message = $"Thank you {name}! Your message has been sent.",
                        TicketId = $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}"
                    }
                };

                effects.Add(effectResult);
                Console.WriteLine($"  [Effect] submitForm completed: {effectResult.Result}");
            }
        }
    }

    // Determine available actions
    var isFormValid = session.State.GetValueOrDefault("isFormValid") is true;
    var availableActions = isFormValid ? new[] { "submit" } : Array.Empty<string>();

    session.Sequence++;

    var response = new
    {
        transactionId = changeset.TransactionId,
        status = "Committed",
        sequence = session.Sequence,
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        patches = patches,
        effects = effects,
        availableActions = availableActions
    };

    Console.WriteLine($"  [Response] {patches.Count} patches, {effects.Count} effects");

    return Results.Ok(response);
});

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     QUELA REACTIVE ORCHESTRATION RUNTIME - SANDBOX DEMO      ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Server starting on http://localhost:5000                    ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("║  Available endpoints:                                        ║");
Console.WriteLine("║    GET  /                          - Web UI                  ║");
Console.WriteLine("║    GET  /api/health                - Health check            ║");
Console.WriteLine("║    GET  /api/quela/orchestrations  - List orchestrations     ║");
Console.WriteLine("║    POST /api/quela/sessions        - Create session          ║");
Console.WriteLine("║    POST /api/quela/sessions/{id}/changesets - Process changes║");
Console.WriteLine("║    GET  /api/debug/orchestration/{id} - Debug view           ║");
Console.WriteLine("║                                                              ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine();

app.Run("http://0.0.0.0:5000");

// ================ ORCHESTRATION DEFINITIONS ================

static Orchestration CreateContactFormOrchestration()
{
    var nodes = new List<Node>
    {
        // Value nodes (form fields)
        new Node
        {
            Id = "name",
            Name = "Name Field",
            Type = NodeType.Value,
            DefaultValue = "",
            TargetSelector = "#name",
            ValidationFn = (value) =>
            {
                var str = value?.ToString() ?? "";
                if (string.IsNullOrEmpty(str)) return (false, "Name is required");
                if (str.Length < 2) return (false, "Name must be at least 2 characters");
                return (true, null);
            }
        },
        new Node
        {
            Id = "email",
            Name = "Email Field",
            Type = NodeType.Value,
            DefaultValue = "",
            TargetSelector = "#email",
            ValidationFn = (value) =>
            {
                var str = value?.ToString() ?? "";
                if (string.IsNullOrEmpty(str)) return (false, "Email is required");
                if (!str.Contains("@")) return (false, "Please enter a valid email address");
                return (true, null);
            }
        },
        new Node
        {
            Id = "subject",
            Name = "Subject Field",
            Type = NodeType.Value,
            DefaultValue = "",
            TargetSelector = "#subject",
            ValidationFn = (value) =>
            {
                var str = value?.ToString() ?? "";
                if (string.IsNullOrEmpty(str)) return (false, "Subject is required");
                if (str.Length < 5) return (false, "Subject must be at least 5 characters");
                return (true, null);
            }
        },
        new Node
        {
            Id = "message",
            Name = "Message Field",
            Type = NodeType.Value,
            DefaultValue = "",
            TargetSelector = "#message",
            ValidationFn = (value) =>
            {
                var str = value?.ToString() ?? "";
                if (string.IsNullOrEmpty(str)) return (false, "Message is required");
                if (str.Length < 10) return (false, "Message must be at least 10 characters");
                return (true, null);
            }
        },

        // Computed nodes
        new Node
        {
            Id = "messageLength",
            Name = "Message Length",
            Type = NodeType.Computed,
            Dependencies = new[] { "message" },
            TargetSelector = "#message-length",
            ComputeFn = (state) =>
            {
                var msg = state.GetValueOrDefault("message")?.ToString() ?? "";
                return msg.Length;
            }
        },
        new Node
        {
            Id = "isFormValid",
            Name = "Form Validity",
            Type = NodeType.Computed,
            Dependencies = new[] { "name", "email", "subject", "message" },
            TargetSelector = "#submit-btn",
            ComputeFn = (state) =>
            {
                var name = state.GetValueOrDefault("name")?.ToString() ?? "";
                var email = state.GetValueOrDefault("email")?.ToString() ?? "";
                var subject = state.GetValueOrDefault("subject")?.ToString() ?? "";
                var message = state.GetValueOrDefault("message")?.ToString() ?? "";

                return !string.IsNullOrEmpty(name) && name.Length >= 2 &&
                       !string.IsNullOrEmpty(email) && email.Contains("@") &&
                       !string.IsNullOrEmpty(subject) && subject.Length >= 5 &&
                       !string.IsNullOrEmpty(message) && message.Length >= 10;
            }
        },

        // Trigger node
        new Node
        {
            Id = "submit",
            Name = "Submit Trigger",
            Type = NodeType.Trigger,
            Dependencies = new[] { "isFormValid" },
            TargetSelector = "#submit-btn"
        },

        // Effect node
        new Node
        {
            Id = "submitForm",
            Name = "Submit Form Effect",
            Type = NodeType.Effect,
            Dependencies = new[] { "submit", "name", "email", "subject", "message" },
            EffectFn = async (state, ct) =>
            {
                await Task.Delay(1000, ct);
                return new
                {
                    Success = true,
                    Message = "Form submitted!",
                    TicketId = $"TKT-{DateTime.UtcNow:yyyyMMddHHmmss}"
                };
            }
        }
    };

    // Build edges from dependencies
    var edges = nodes
        .SelectMany(n => n.Dependencies.Select(d => new Edge { Source = d, Target = n.Id }))
        .ToList();

    // Compute topological order
    var topoOrder = ComputeTopologicalOrder(nodes);

    return new Orchestration
    {
        Id = "contact-form",
        Name = "Contact Form",
        Version = "1.0.0",
        Nodes = nodes,
        Edges = edges,
        TopologicalOrder = topoOrder,
        MaxDepth = ComputeMaxDepth(nodes)
    };
}

static List<string> ComputeTopologicalOrder(List<Node> nodes)
{
    var result = new List<string>();
    var visited = new HashSet<string>();
    var temp = new HashSet<string>();

    void Visit(string nodeId)
    {
        if (visited.Contains(nodeId)) return;
        if (temp.Contains(nodeId)) return; // Cycle detection

        temp.Add(nodeId);

        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node != null)
        {
            foreach (var dep in node.Dependencies)
                Visit(dep);
        }

        temp.Remove(nodeId);
        visited.Add(nodeId);
        result.Add(nodeId);
    }

    foreach (var node in nodes)
        Visit(node.Id);

    return result;
}

static int ComputeMaxDepth(List<Node> nodes)
{
    var depths = new Dictionary<string, int>();

    int GetDepth(string nodeId)
    {
        if (depths.TryGetValue(nodeId, out var d)) return d;

        var node = nodes.FirstOrDefault(n => n.Id == nodeId);
        if (node == null || node.Dependencies.Length == 0)
        {
            depths[nodeId] = 1;
            return 1;
        }

        var maxDepth = node.Dependencies.Max(dep => GetDepth(dep));
        depths[nodeId] = maxDepth + 1;
        return maxDepth + 1;
    }

    return nodes.Max(n => GetDepth(n.Id));
}

// ================ DATA MODELS ================

record CreateSessionRequest
{
    public string OrchestrationId { get; init; } = "";
}

record Changeset
{
    public string TransactionId { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string OrchestrationId { get; init; } = "";
    public long Timestamp { get; init; }
    public long LastSequence { get; init; }
    public List<FieldChange>? Changes { get; init; }
    public List<ActionRequest>? Actions { get; init; }
}

record FieldChange
{
    public string NodeId { get; init; } = "";
    public object? Value { get; init; }
    public string? Source { get; init; }
}

record ActionRequest
{
    public string ActionId { get; init; } = "";
    public object? Payload { get; init; }
}

record UIPatch
{
    [JsonPropertyName("target")]
    public string Target { get; init; } = "";

    [JsonPropertyName("operation")]
    public string Operation { get; init; } = "";

    [JsonPropertyName("value")]
    public object? Value { get; init; }
}

record EffectResult
{
    [JsonPropertyName("effectId")]
    public string EffectId { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";

    [JsonPropertyName("result")]
    public object? Result { get; init; }
}

class Session
{
    public string Id { get; init; } = "";
    public string OrchestrationId { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastActivityAt { get; set; }
    public Dictionary<string, object?> State { get; init; } = new();
    public long Sequence { get; set; }
}

class Orchestration
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public List<Node> Nodes { get; init; } = new();
    public List<Edge> Edges { get; init; } = new();
    public List<string> TopologicalOrder { get; init; } = new();
    public int MaxDepth { get; init; }
}

class Node
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public NodeType Type { get; init; }
    public object? DefaultValue { get; init; }
    public string[] Dependencies { get; init; } = Array.Empty<string>();
    public string? TargetSelector { get; init; }
    public Func<Dictionary<string, object?>, object?>? ComputeFn { get; init; }
    public Func<object?, (bool isValid, string? errorMessage)>? ValidationFn { get; init; }
    public Func<Dictionary<string, object?>, CancellationToken, Task<object>>? EffectFn { get; init; }
}

class Edge
{
    public string Source { get; init; } = "";
    public string Target { get; init; } = "";
}

enum NodeType
{
    Value,
    Computed,
    Trigger,
    Effect,
    Conditional,
    Aggregator,
    Validation,
    Partial
}

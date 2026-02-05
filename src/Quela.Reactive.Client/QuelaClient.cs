using System.Net.Http.Json;
using System.Text.Json;
using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Payloads;

namespace Quela.Reactive.Client;

/// <summary>
/// Client for interacting with Quela orchestration endpoints.
/// </summary>
public sealed class QuelaClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public QuelaClient(HttpClient httpClient, string baseUrl = "/api/quela")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// Creates a new session for an orchestration.
    /// </summary>
    public async Task<SessionInfo> CreateSessionAsync(
        string orchestrationId,
        Dictionary<string, object>? initialValues = null,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            orchestrationId,
            initialValues
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/sessions",
            request,
            _jsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SessionInfo>(
            _jsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Failed to parse session response");
    }

    /// <summary>
    /// Sends a changeset and receives patches.
    /// </summary>
    public async Task<ResponsePayload> SendChangesetAsync(
        string sessionId,
        ChangesetPayload changeset,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/sessions/{sessionId}/changesets",
            changeset,
            _jsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ResponsePayload>(
            _jsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Failed to parse response");
    }

    /// <summary>
    /// Gets the current session state.
    /// </summary>
    public async Task<SessionState> GetSessionStateAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/sessions/{sessionId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SessionState>(
            _jsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Failed to parse session state");
    }

    /// <summary>
    /// Deletes a session.
    /// </summary>
    public async Task DeleteSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(
            $"{_baseUrl}/sessions/{sessionId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Lists all available orchestrations.
    /// </summary>
    public async Task<IReadOnlyList<OrchestrationInfo>> ListOrchestrationsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{_baseUrl}/orchestrations",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<OrchestrationInfo>>(
            _jsonOptions,
            cancellationToken);

        return result ?? new List<OrchestrationInfo>();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Don't dispose HttpClient - it's managed externally
    }

    // DTOs
    public record SessionInfo(string SessionId, string OrchestrationId, long CreatedAt);
    public record SessionState(string SessionId, string OrchestrationId, long CreatedAt, long LastActivityAt, long CurrentSequence);
    public record OrchestrationInfo(string Id, string Name, string Version, int NodeCount);
}

/// <summary>
/// Builder for creating changesets.
/// </summary>
public sealed class ChangesetBuilder
{
    private readonly string _sessionId;
    private readonly string _orchestrationId;
    private readonly List<FieldChange> _changes = new();
    private readonly List<ActionRequest> _actions = new();
    private long _lastSequence;

    public ChangesetBuilder(string sessionId, string orchestrationId)
    {
        _sessionId = sessionId;
        _orchestrationId = orchestrationId;
    }

    public ChangesetBuilder WithLastSequence(long sequence)
    {
        _lastSequence = sequence;
        return this;
    }

    public ChangesetBuilder SetField(string nodeId, object? value, ChangeSource source = ChangeSource.Input)
    {
        _changes.Add(new FieldChange
        {
            NodeId = nodeId,
            Value = value,
            Source = source,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        return this;
    }

    public ChangesetBuilder TriggerAction(string actionId, object? payload = null)
    {
        _actions.Add(new ActionRequest
        {
            ActionId = actionId,
            Payload = payload,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        return this;
    }

    public ChangesetPayload Build()
    {
        return new ChangesetPayload
        {
            TransactionId = Guid.NewGuid().ToString("N"),
            SessionId = _sessionId,
            OrchestrationId = _orchestrationId,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastSequence = _lastSequence,
            Changes = _changes,
            Actions = _actions
        };
    }
}

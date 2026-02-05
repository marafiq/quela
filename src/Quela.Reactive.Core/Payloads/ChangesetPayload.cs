using System.Text.Json.Serialization;
using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Payloads;

/// <summary>
/// Payload sent from client to server containing changes and actions.
/// </summary>
public sealed record ChangesetPayload
{
    /// <summary>
    /// Client-generated transaction ID for correlation.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = "";

    /// <summary>
    /// Session identifier.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; init; } = "";

    /// <summary>
    /// Orchestration being interacted with.
    /// </summary>
    [JsonPropertyName("orchestrationId")]
    public string OrchestrationId { get; init; } = "";

    /// <summary>
    /// Client timestamp for latency calculation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Last known sequence number for ordering.
    /// </summary>
    [JsonPropertyName("lastSequence")]
    public long LastSequence { get; init; }

    /// <summary>
    /// Value changes from the client.
    /// </summary>
    [JsonPropertyName("changes")]
    public IReadOnlyList<FieldChange> Changes { get; init; } = Array.Empty<FieldChange>();

    /// <summary>
    /// Actions triggered by the client.
    /// </summary>
    [JsonPropertyName("actions")]
    public IReadOnlyList<ActionRequest> Actions { get; init; } = Array.Empty<ActionRequest>();

    /// <summary>
    /// Metadata for debugging and analytics.
    /// </summary>
    [JsonPropertyName("metadata")]
    public ChangesetMetadata? Metadata { get; init; }
}

/// <summary>
/// A field value change from the client.
/// </summary>
public sealed record FieldChange
{
    /// <summary>
    /// Node ID of the changed field.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string NodeId { get; init; } = "";

    /// <summary>
    /// The new value.
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    /// <summary>
    /// Source of the change.
    /// </summary>
    [JsonPropertyName("source")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ChangeSource Source { get; init; }

    /// <summary>
    /// Event that triggered the change.
    /// </summary>
    [JsonPropertyName("triggeredBy")]
    public string? TriggeredBy { get; init; }

    /// <summary>
    /// Timestamp when the change occurred on the client.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}

/// <summary>
/// Source of a field change.
/// </summary>
public enum ChangeSource
{
    /// <summary>User typed in an input field.</summary>
    Input,

    /// <summary>User selected an option.</summary>
    Selection,

    /// <summary>User checked/unchecked a checkbox.</summary>
    Toggle,

    /// <summary>Change came from JavaScript.</summary>
    Script,

    /// <summary>Change came from initialization.</summary>
    Init,

    /// <summary>Change came from paste operation.</summary>
    Paste,

    /// <summary>Change came from autocomplete.</summary>
    Autocomplete
}

/// <summary>
/// An action request from the client.
/// </summary>
public sealed record ActionRequest
{
    /// <summary>
    /// ID of the action (corresponds to trigger node).
    /// </summary>
    [JsonPropertyName("actionId")]
    public string ActionId { get; init; } = "";

    /// <summary>
    /// Optional payload for the action.
    /// </summary>
    [JsonPropertyName("payload")]
    public object? Payload { get; init; }

    /// <summary>
    /// Timestamp when the action was triggered.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }
}

/// <summary>
/// Metadata about the changeset.
/// </summary>
public sealed record ChangesetMetadata
{
    /// <summary>
    /// User agent string.
    /// </summary>
    [JsonPropertyName("userAgent")]
    public string? UserAgent { get; init; }

    /// <summary>
    /// Viewport width.
    /// </summary>
    [JsonPropertyName("viewportWidth")]
    public int? ViewportWidth { get; init; }

    /// <summary>
    /// Viewport height.
    /// </summary>
    [JsonPropertyName("viewportHeight")]
    public int? ViewportHeight { get; init; }

    /// <summary>
    /// Client timezone offset in minutes.
    /// </summary>
    [JsonPropertyName("timezoneOffset")]
    public int? TimezoneOffset { get; init; }

    /// <summary>
    /// Custom tracking data.
    /// </summary>
    [JsonPropertyName("customData")]
    public IReadOnlyDictionary<string, object>? CustomData { get; init; }
}

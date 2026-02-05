using System.Text.Json.Serialization;
using Quela.Reactive.Core.Patches;

namespace Quela.Reactive.Core.Payloads;

/// <summary>
/// Payload sent from server to client containing patches and status.
/// </summary>
public sealed record ResponsePayload
{
    /// <summary>
    /// Transaction ID for correlation.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = "";

    /// <summary>
    /// Status of the transaction.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResponseStatus Status { get; init; }

    /// <summary>
    /// Sequence number for ordering patches.
    /// </summary>
    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    /// <summary>
    /// Server timestamp.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// UI patches to apply.
    /// </summary>
    [JsonPropertyName("patches")]
    public IReadOnlyList<UIPatch> Patches { get; init; } = Array.Empty<UIPatch>();

    /// <summary>
    /// Effect execution status.
    /// </summary>
    [JsonPropertyName("effects")]
    public IReadOnlyList<EffectStatus> Effects { get; init; } = Array.Empty<EffectStatus>();

    /// <summary>
    /// Available actions the user can take.
    /// </summary>
    [JsonPropertyName("availableActions")]
    public IReadOnlyList<string> AvailableActions { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Errors if the transaction failed.
    /// </summary>
    [JsonPropertyName("errors")]
    public IReadOnlyList<ResponseError> Errors { get; init; } = Array.Empty<ResponseError>();

    /// <summary>
    /// Redirect URL if navigation is needed.
    /// </summary>
    [JsonPropertyName("redirect")]
    public RedirectInfo? Redirect { get; init; }

    /// <summary>
    /// Data to expose to client JavaScript.
    /// </summary>
    [JsonPropertyName("data")]
    public IReadOnlyDictionary<string, object>? Data { get; init; }
}

/// <summary>
/// Status of the response.
/// </summary>
public enum ResponseStatus
{
    /// <summary>Transaction committed successfully.</summary>
    Committed,

    /// <summary>Transaction is still processing.</summary>
    Processing,

    /// <summary>Transaction was aborted due to error.</summary>
    Aborted,

    /// <summary>Transaction timed out.</summary>
    TimedOut,

    /// <summary>Validation failed.</summary>
    ValidationFailed,

    /// <summary>Conflict with another transaction.</summary>
    Conflict
}

/// <summary>
/// Status of an effect execution.
/// </summary>
public sealed record EffectStatus
{
    /// <summary>
    /// Effect node ID.
    /// </summary>
    [JsonPropertyName("effectId")]
    public string EffectId { get; init; } = "";

    /// <summary>
    /// Current status of the effect.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EffectExecutionStatus Status { get; init; }

    /// <summary>
    /// Result of the effect if completed.
    /// </summary>
    [JsonPropertyName("result")]
    public object? Result { get; init; }

    /// <summary>
    /// Error if the effect failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>
    /// Duration of the effect in milliseconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public long? Duration { get; init; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    [JsonPropertyName("retryCount")]
    public int RetryCount { get; init; }
}

/// <summary>
/// Status of effect execution.
/// </summary>
public enum EffectExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Retrying
}

/// <summary>
/// Error information in the response.
/// </summary>
public sealed record ResponseError
{
    /// <summary>
    /// Error code.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    /// <summary>
    /// Error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = "";

    /// <summary>
    /// Node that caused the error.
    /// </summary>
    [JsonPropertyName("nodeId")]
    public string? NodeId { get; init; }

    /// <summary>
    /// Field path for validation errors.
    /// </summary>
    [JsonPropertyName("field")]
    public string? Field { get; init; }

    /// <summary>
    /// Additional error details.
    /// </summary>
    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, object>? Details { get; init; }
}

/// <summary>
/// Information for client-side redirect.
/// </summary>
public sealed record RedirectInfo
{
    /// <summary>
    /// URL to redirect to.
    /// </summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    /// <summary>
    /// Whether to use a full page navigation.
    /// </summary>
    [JsonPropertyName("fullPage")]
    public bool FullPage { get; init; }

    /// <summary>
    /// Delay before redirecting in milliseconds.
    /// </summary>
    [JsonPropertyName("delay")]
    public int? Delay { get; init; }

    /// <summary>
    /// Whether to open in a new tab.
    /// </summary>
    [JsonPropertyName("newTab")]
    public bool NewTab { get; init; }
}

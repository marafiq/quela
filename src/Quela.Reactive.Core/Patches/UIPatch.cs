using Quela.Reactive.Core.Primitives;
using System.Text.Json.Serialization;

namespace Quela.Reactive.Core.Patches;

/// <summary>
/// Represents a UI patch operation.
/// Patches are minimal, declarative updates to the UI.
/// </summary>
public sealed record UIPatch
{
    /// <summary>
    /// CSS selector or element ID for the target element.
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; init; } = "";

    /// <summary>
    /// The operation to perform.
    /// </summary>
    [JsonPropertyName("operation")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PatchOperation Operation { get; init; }

    /// <summary>
    /// The value for the operation (type depends on operation).
    /// </summary>
    [JsonPropertyName("value")]
    public object? Value { get; init; }

    /// <summary>
    /// Priority for patch ordering (higher = sooner).
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; }

    /// <summary>
    /// Optional key for patch deduplication.
    /// </summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>
    /// Whether this patch should be animated.
    /// </summary>
    [JsonPropertyName("animate")]
    public bool Animate { get; init; }

    /// <summary>
    /// Animation duration in milliseconds.
    /// </summary>
    [JsonPropertyName("animationDuration")]
    public int? AnimationDuration { get; init; }

    // Factory methods for common operations

    public static UIPatch SetValue(string target, object? value) =>
        new() { Target = target, Operation = PatchOperation.SetValue, Value = value };

    public static UIPatch SetText(string target, string text) =>
        new() { Target = target, Operation = PatchOperation.SetText, Value = text };

    public static UIPatch SetHtml(string target, string html) =>
        new() { Target = target, Operation = PatchOperation.SetHtml, Value = html };

    public static UIPatch SetVisible(string target, bool visible) =>
        new() { Target = target, Operation = PatchOperation.SetVisible, Value = visible };

    public static UIPatch SetEnabled(string target, bool enabled) =>
        new() { Target = target, Operation = PatchOperation.SetEnabled, Value = enabled };

    public static UIPatch SetValid(string target, bool valid, string? errorMessage = null) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.SetValid,
            Value = new { valid, errorMessage }
        };

    public static UIPatch AddClass(string target, string className) =>
        new() { Target = target, Operation = PatchOperation.AddClass, Value = className };

    public static UIPatch RemoveClass(string target, string className) =>
        new() { Target = target, Operation = PatchOperation.RemoveClass, Value = className };

    public static UIPatch ToggleClass(string target, string className, bool? force = null) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.ToggleClass,
            Value = new { className, force }
        };

    public static UIPatch SetAttribute(string target, string name, string? value) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.SetAttribute,
            Value = new { name, value }
        };

    public static UIPatch RemoveAttribute(string target, string name) =>
        new() { Target = target, Operation = PatchOperation.RemoveAttribute, Value = name };

    public static UIPatch SetStyle(string target, string property, string value) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.SetStyle,
            Value = new { property, value }
        };

    public static UIPatch InsertPartial(string target, string html, InsertPosition position = InsertPosition.Replace) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.InsertPartial,
            Value = new { html, position = position.ToString().ToLowerInvariant() }
        };

    public static UIPatch RemoveElement(string target) =>
        new() { Target = target, Operation = PatchOperation.RemoveElement };

    public static UIPatch Focus(string target) =>
        new() { Target = target, Operation = PatchOperation.Focus };

    public static UIPatch Blur(string target) =>
        new() { Target = target, Operation = PatchOperation.Blur };

    public static UIPatch ScrollIntoView(string target) =>
        new() { Target = target, Operation = PatchOperation.ScrollIntoView };

    public static UIPatch TriggerEvent(string target, string eventName, object? detail = null) =>
        new()
        {
            Target = target,
            Operation = PatchOperation.TriggerEvent,
            Value = new { eventName, detail }
        };
}

/// <summary>
/// Types of patch operations.
/// </summary>
public enum PatchOperation
{
    /// <summary>Set the value of an input/select element.</summary>
    SetValue,

    /// <summary>Set the text content of an element.</summary>
    SetText,

    /// <summary>Set the inner HTML of an element.</summary>
    SetHtml,

    /// <summary>Toggle visibility of an element.</summary>
    SetVisible,

    /// <summary>Toggle enabled/disabled state.</summary>
    SetEnabled,

    /// <summary>Set validation state with optional error message.</summary>
    SetValid,

    /// <summary>Add a CSS class to an element.</summary>
    AddClass,

    /// <summary>Remove a CSS class from an element.</summary>
    RemoveClass,

    /// <summary>Toggle a CSS class on an element.</summary>
    ToggleClass,

    /// <summary>Set an attribute on an element.</summary>
    SetAttribute,

    /// <summary>Remove an attribute from an element.</summary>
    RemoveAttribute,

    /// <summary>Set a CSS style property.</summary>
    SetStyle,

    /// <summary>Insert partial HTML content.</summary>
    InsertPartial,

    /// <summary>Remove an element from the DOM.</summary>
    RemoveElement,

    /// <summary>Replace an element with new content.</summary>
    ReplaceElement,

    /// <summary>Move an element to a new location.</summary>
    MoveElement,

    /// <summary>Focus an element.</summary>
    Focus,

    /// <summary>Remove focus from an element.</summary>
    Blur,

    /// <summary>Scroll element into view.</summary>
    ScrollIntoView,

    /// <summary>Trigger a custom event.</summary>
    TriggerEvent,

    /// <summary>Execute a JavaScript expression.</summary>
    ExecuteScript
}

/// <summary>
/// Position for inserting content relative to a target.
/// </summary>
public enum InsertPosition
{
    Replace,
    BeforeBegin,
    AfterBegin,
    BeforeEnd,
    AfterEnd
}

/// <summary>
/// A batch of patches for a single transaction.
/// </summary>
public sealed record PatchBatch
{
    /// <summary>
    /// Transaction ID for correlation.
    /// </summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = "";

    /// <summary>
    /// Sequence number for ordering.
    /// </summary>
    [JsonPropertyName("sequence")]
    public long Sequence { get; init; }

    /// <summary>
    /// Timestamp of patch generation.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// The patches in this batch.
    /// </summary>
    [JsonPropertyName("patches")]
    public IReadOnlyList<UIPatch> Patches { get; init; } = Array.Empty<UIPatch>();

    /// <summary>
    /// Whether this is the final batch for the transaction.
    /// </summary>
    [JsonPropertyName("isFinal")]
    public bool IsFinal { get; init; }

    /// <summary>
    /// Error information if the transaction failed.
    /// </summary>
    [JsonPropertyName("error")]
    public PatchError? Error { get; init; }
}

/// <summary>
/// Error information in a patch batch.
/// </summary>
public sealed record PatchError(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("nodeId")] string? NodeId = null);

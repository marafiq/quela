using Quela.Reactive.Core.Primitives;

namespace Quela.Reactive.Core.Execution;

/// <summary>
/// Provides read-only access to dependency values during computation.
/// Used by computed nodes and conditional nodes to access their dependencies.
/// </summary>
public interface IDependencyReader
{
    /// <summary>
    /// Gets the value of a dependency node.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="nodeId">The ID of the dependency node.</param>
    /// <returns>The current value of the node.</returns>
    /// <exception cref="InvalidOperationException">If the node doesn't exist or type mismatch.</exception>
    T Get<T>(NodeId nodeId);

    /// <summary>
    /// Tries to get the value of a dependency node.
    /// </summary>
    bool TryGet<T>(NodeId nodeId, out T? value);

    /// <summary>
    /// Gets the value of a dependency node or a default.
    /// </summary>
    T GetOrDefault<T>(NodeId nodeId, T defaultValue = default!);

    /// <summary>
    /// Gets the raw (boxed) value of a dependency node.
    /// </summary>
    object? GetRaw(NodeId nodeId);

    /// <summary>
    /// Checks if a node has a value.
    /// </summary>
    bool HasValue(NodeId nodeId);

    /// <summary>
    /// Checks if a node exists in the graph.
    /// </summary>
    bool NodeExists(NodeId nodeId);

    /// <summary>
    /// Gets the current transaction ID.
    /// </summary>
    TransactionId TransactionId { get; }

    /// <summary>
    /// Gets the current session ID.
    /// </summary>
    SessionId SessionId { get; }
}

/// <summary>
/// Extended dependency reader with mutation capabilities.
/// Used internally by the execution engine.
/// </summary>
public interface IDependencyWriter : IDependencyReader
{
    /// <summary>
    /// Sets the value of a node.
    /// </summary>
    void Set<T>(NodeId nodeId, T value);

    /// <summary>
    /// Sets the raw (boxed) value of a node.
    /// </summary>
    void SetRaw(NodeId nodeId, object? value);

    /// <summary>
    /// Marks a node as dirty (needing recomputation).
    /// </summary>
    void MarkDirty(NodeId nodeId);

    /// <summary>
    /// Invalidates a node and its transitive dependents.
    /// </summary>
    void Invalidate(NodeId nodeId);
}

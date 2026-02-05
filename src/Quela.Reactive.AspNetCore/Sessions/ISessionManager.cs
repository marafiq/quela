using Quela.Reactive.Core.Primitives;
using Quela.Reactive.Core.Graph;
using Quela.Reactive.Runtime.State;

namespace Quela.Reactive.AspNetCore;

/// <summary>
/// Manages session state for orchestrations.
/// </summary>
public interface ISessionManager
{
    /// <summary>
    /// Creates a new session for an orchestration.
    /// </summary>
    Task<SessionState> CreateSessionAsync(
        OrchestrationId orchestrationId,
        ReactiveGraph graph,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an existing session.
    /// </summary>
    Task<SessionState?> GetSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a session.
    /// </summary>
    Task RemoveSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all sessions for an orchestration.
    /// </summary>
    Task<IReadOnlyList<SessionState>> GetSessionsForOrchestrationAsync(
        OrchestrationId orchestrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up expired sessions.
    /// </summary>
    Task CleanupExpiredSessionsAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory session manager for development and single-server deployments.
/// </summary>
public sealed class InMemorySessionManager : ISessionManager, IDisposable
{
    private readonly Dictionary<SessionId, SessionState> _sessions = new();
    private readonly Dictionary<OrchestrationId, List<SessionId>> _orchestrationSessions = new();
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Timer _cleanupTimer;
    private readonly TimeSpan _sessionTimeout;
    private bool _disposed;

    public InMemorySessionManager(QuelaOptions? options = null)
    {
        _sessionTimeout = options?.SessionTimeout ?? TimeSpan.FromMinutes(30);
        _cleanupTimer = new Timer(
            _ => _ = CleanupExpiredSessionsAsync(_sessionTimeout),
            null,
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1));
    }

    public Task<SessionState> CreateSessionAsync(
        OrchestrationId orchestrationId,
        ReactiveGraph graph,
        CancellationToken cancellationToken = default)
    {
        var sessionId = SessionId.Create();
        var session = new SessionState(sessionId, graph);

        _lock.EnterWriteLock();
        try
        {
            _sessions[sessionId] = session;

            if (!_orchestrationSessions.TryGetValue(orchestrationId, out var sessions))
            {
                sessions = new List<SessionId>();
                _orchestrationSessions[orchestrationId] = sessions;
            }
            sessions.Add(sessionId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.FromResult(session);
    }

    public Task<SessionState?> GetSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Touch();
                return Task.FromResult<SessionState?>(session);
            }
            return Task.FromResult<SessionState?>(null);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task RemoveSessionAsync(
        SessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                _sessions.Remove(sessionId);
                session.CancelAllScopes();

                // Remove from orchestration mapping
                foreach (var sessions in _orchestrationSessions.Values)
                {
                    sessions.Remove(sessionId);
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionState>> GetSessionsForOrchestrationAsync(
        OrchestrationId orchestrationId,
        CancellationToken cancellationToken = default)
    {
        _lock.EnterReadLock();
        try
        {
            if (_orchestrationSessions.TryGetValue(orchestrationId, out var sessionIds))
            {
                var sessions = sessionIds
                    .Select(id => _sessions.TryGetValue(id, out var s) ? s : null)
                    .Where(s => s != null)
                    .Cast<SessionState>()
                    .ToList();

                return Task.FromResult<IReadOnlyList<SessionState>>(sessions);
            }

            return Task.FromResult<IReadOnlyList<SessionState>>(Array.Empty<SessionState>());
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public Task CleanupExpiredSessionsAsync(
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - maxAge;

        _lock.EnterWriteLock();
        try
        {
            var expiredSessions = _sessions
                .Where(kvp => kvp.Value.LastActivityAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                if (_sessions.TryGetValue(sessionId, out var session))
                {
                    session.CancelAllScopes();
                    _sessions.Remove(sessionId);
                }

                foreach (var sessions in _orchestrationSessions.Values)
                {
                    sessions.Remove(sessionId);
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        _lock.Dispose();
    }
}

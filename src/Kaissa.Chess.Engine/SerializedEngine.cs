using System.Threading;

namespace Kaissa.Chess.Engine;

/// <summary>
/// Wraps an <see cref="IChessEngine"/> so that searches never overlap. A UCI process handles one
/// search at a time; when a single long-lived engine is shared (e.g. an eval bar that re-evaluates on
/// every move, or rapid hint requests), concurrent <see cref="AnalyzeAsync"/> calls would interleave
/// position/go commands and corrupt results. This serializes them behind a single gate.
/// </summary>
public sealed class SerializedEngine : IChessEngine
{
    private readonly IChessEngine _inner;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SerializedEngine(IChessEngine inner) => _inner = inner;

    public Task StartAsync(CancellationToken cancellationToken = default) => _inner.StartAsync(cancellationToken);
    public Task<EngineIdentification> HandshakeAsync(CancellationToken cancellationToken = default) => _inner.HandshakeAsync(cancellationToken);
    public Task<bool> IsReadyAsync(CancellationToken cancellationToken = default) => _inner.IsReadyAsync(cancellationToken);
    public Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default) => _inner.SetOptionAsync(name, value, cancellationToken);
    public Task ConfigureStrengthAsync(StrengthSettings strength, CancellationToken cancellationToken = default) => _inner.ConfigureStrengthAsync(strength, cancellationToken);
    public Task NewGameAsync(CancellationToken cancellationToken = default) => _inner.NewGameAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken = default) => _inner.StopAsync(cancellationToken);

    public async Task<SearchResult> AnalyzeAsync(string position, SearchLimits limits, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _inner.AnalyzeAsync(position, limits, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}

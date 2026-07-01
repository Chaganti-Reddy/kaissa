namespace Kaissa.Chess.Engine;

/// <summary>
/// A chess engine the rest of the application talks to. This is the single seam behind which
/// any UCI engine (Stockfish today) lives, whether in-process, out-of-process, or remote.
/// </summary>
public interface IChessEngine : IAsyncDisposable
{
    /// <summary>Starts the engine so it is ready to receive commands.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Performs the UCI handshake and returns the engine's identity and options.</summary>
    Task<EngineIdentification> HandshakeAsync(CancellationToken cancellationToken = default);

    /// <summary>Blocks until the engine reports it is ready to search.</summary>
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);

    /// <summary>Sets a single engine option by name.</summary>
    Task SetOptionAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>Applies a strength cap (or full strength) to the engine.</summary>
    Task ConfigureStrengthAsync(StrengthSettings strength, CancellationToken cancellationToken = default);

    /// <summary>Signals the start of a new game so the engine can reset its state.</summary>
    Task NewGameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches a position and returns the best move and principal variations.
    /// <paramref name="position"/> is either <c>"startpos"</c> or a FEN string.
    /// </summary>
    Task<SearchResult> AnalyzeAsync(string position, SearchLimits limits, CancellationToken cancellationToken = default);

    /// <summary>Asks the engine to stop the current search as soon as possible.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

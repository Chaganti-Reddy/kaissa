using System.Threading.Channels;

namespace Kaissa.Chess.Engine;

/// <summary>
/// Carries UCI text lines to and from an engine. Abstracting the transport lets the protocol
/// logic in <see cref="UciChessEngine"/> be tested against an in-memory engine, while the real
/// engine runs out-of-process behind <see cref="ProcessUciTransport"/>.
/// </summary>
public interface IUciTransport : IAsyncDisposable
{
    /// <summary>Lines emitted by the engine, in order.</summary>
    ChannelReader<string> Output { get; }

    /// <summary>Starts the transport (e.g. spawns the engine process).</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends one command line to the engine.</summary>
    ValueTask SendLineAsync(string line, CancellationToken cancellationToken = default);
}

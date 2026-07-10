using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace Kaissa.Chess.Engine;

/// <summary>
/// Runs a UCI engine as a child process and exchanges lines over its standard input/output.
/// The process boundary keeps the (GPL) engine isolated and swappable; see docs/architecture.md.
/// </summary>
public sealed class ProcessUciTransport : IUciTransport
{
    private readonly string _executablePath;
    private readonly string? _workingDirectory;
    private readonly Channel<string> _output =
        Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private Process? _process;
    private Task? _pump;

    public ProcessUciTransport(string executablePath, string? workingDirectory = null)
    {
        _executablePath = executablePath;
        _workingDirectory = workingDirectory;
    }

    public ChannelReader<string> Output => _output.Reader;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
            throw new InvalidOperationException("Transport already started.");

        if (!File.Exists(_executablePath))
            throw new FileNotFoundException("Engine executable not found.", _executablePath);

        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            WorkingDirectory = _workingDirectory
                ?? Path.GetDirectoryName(Path.GetFullPath(_executablePath))!,
        };

        _process = new Process { StartInfo = startInfo };
        _process.Start();
        _pump = Task.Run(() => PumpOutputAsync(_process.StandardOutput), CancellationToken.None);
        // Drain stderr too: it is redirected, so if the engine ever writes enough to it and no one reads,
        // its OS pipe buffer fills and the engine blocks (a classic redirect deadlock). We discard it.
        _ = Task.Run(() => DrainAsync(_process.StandardError), CancellationToken.None);
        return Task.CompletedTask;
    }

    private static async Task DrainAsync(StreamReader reader)
    {
        try { while (await reader.ReadLineAsync().ConfigureAwait(false) is not null) { } }
        catch { /* stream closed on teardown */ }
    }

    private async Task PumpOutputAsync(StreamReader reader)
    {
        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
                await _output.Writer.WriteAsync(line).ConfigureAwait(false);
        }
        finally
        {
            _output.Writer.TryComplete();
        }
    }

    public async ValueTask SendLineAsync(string line, CancellationToken cancellationToken = default)
    {
        var process = _process ?? throw new InvalidOperationException("Transport not started.");
        cancellationToken.ThrowIfCancellationRequested();
        // Use the overloads available on both net9 and .NET Standard 2.1 (Unity).
        await process.StandardInput.WriteLineAsync(line).ConfigureAwait(false);
        await process.StandardInput.FlushAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                try
                {
                    await _process.StandardInput.WriteLineAsync("quit").ConfigureAwait(false);
                    await _process.StandardInput.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // Engine may have already closed its input; fall through to a hard stop.
                }

                if (!_process.WaitForExit(1000))
                    _process.Kill();
            }
        }
        catch (InvalidOperationException)
        {
            // Process already gone.
        }
        finally
        {
            if (_pump is not null)
            {
                try { await _pump.ConfigureAwait(false); }
                catch { /* pump completion is best-effort during teardown */ }
            }

            _process.Dispose();
            _process = null;
        }
    }
}

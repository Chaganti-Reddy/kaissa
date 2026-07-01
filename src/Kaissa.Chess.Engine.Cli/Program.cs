using Kaissa.Chess.Engine;

// Spike 1 harness: prove we can drive a real UCI engine (Stockfish) from C#.
//
// Usage:
//   dotnet run --project src/Kaissa.Chess.Engine.Cli -- <path-to-engine>
//   or set KAISSA_STOCKFISH_PATH and run with no arguments.

string? enginePath = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");

if (string.IsNullOrWhiteSpace(enginePath))
{
    Console.Error.WriteLine("No engine path given. Pass it as an argument or set KAISSA_STOCKFISH_PATH.");
    return 1;
}

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var ct = cts.Token;

// Focused mode: `-- <engine> <fen>` analyzes one position and prints "bestmove <uci> <score>".
// Used to validate authored training positions against the engine.
if (args.Length >= 2)
{
    await using var probe = UciChessEngine.LaunchProcess(enginePath);
    await probe.HandshakeAsync(ct);
    await probe.NewGameAsync(ct);
    var r = await probe.AnalyzeAsync(args[1], SearchLimits.ToDepth(20), ct);
    Console.WriteLine($"bestmove {r.BestMove} {r.Evaluation}");
    return 0;
}

await using var engine = UciChessEngine.LaunchProcess(enginePath);

var id = await engine.HandshakeAsync(ct);
Console.WriteLine($"Engine    : {id.Name}");
Console.WriteLine($"Author    : {id.Author}");
Console.WriteLine($"Options   : {id.Options.Count} advertised");
Console.WriteLine($"Elo cap   : {(id.SupportsElo ? "supported" : "not supported")}");
Console.WriteLine();

await engine.NewGameAsync(ct);

// Full strength: top three candidate moves from the starting position.
await engine.ConfigureStrengthAsync(StrengthSettings.Full, ct);
var analysis = await engine.AnalyzeAsync("startpos", SearchLimits.ToDepth(18, multiPv: 3), ct);

Console.WriteLine("Full strength, start position, depth 18, MultiPV 3:");
Console.WriteLine($"  best move : {analysis.BestMove}");
foreach (var line in analysis.Lines)
    Console.WriteLine($"  pv{line.MultiPvIndex} [{line.Score}] d{line.Depth}: {string.Join(' ', line.Moves.Take(6))}");
Console.WriteLine();

// Capped strength: a beginner-level opponent should be reachable when the engine supports it.
if (id.SupportsElo)
{
    await engine.ConfigureStrengthAsync(StrengthSettings.FromElo(1350), ct);
    var capped = await engine.AnalyzeAsync("startpos", SearchLimits.ForTime(TimeSpan.FromMilliseconds(300)), ct);
    Console.WriteLine($"Capped to ~1350 Elo, 300ms: best move {capped.BestMove} [{capped.Evaluation}]");
}

Console.WriteLine();
Console.WriteLine("Spike 1 OK: handshake, options, strength cap, and search all work over UCI.");
return 0;

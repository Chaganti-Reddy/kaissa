using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>A named opening line: a sequence of moves (UCI) from the starting position.</summary>
public sealed record OpeningLine(string Id, string Name, IReadOnlyList<string> Moves);

/// <summary>A few common openings to learn by playing the line, move by move.</summary>
public static class OpeningLibrary
{
    public static IReadOnlyList<OpeningLine> All { get; } = new[]
    {
        new OpeningLine("italian", "Italian Game", new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4" }),
        new OpeningLine("ruy_lopez", "Ruy Lopez", new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1b5" }),
        new OpeningLine("sicilian", "Sicilian Defence", new[] { "e2e4", "c7c5", "g1f3", "d7d6", "d2d4" }),
        new OpeningLine("queens_gambit", "Queen's Gambit", new[] { "d2d4", "d7d5", "c2c4" }),
        new OpeningLine("london", "London System", new[] { "d2d4", "d7d5", "g1f3", "g8f6", "c1f4" }),
    };

    public static OpeningLine? ById(string id) => All.FirstOrDefault(o => o.Id == id);
}

/// <summary>
/// Drills an opening line: the player enters each book move in order. A correct move advances the
/// line; the trainer tracks the resulting position so a client can render it.
/// </summary>
public sealed class OpeningTrainer
{
    private readonly OpeningLine _line;
    private readonly ChessGame _game = ChessGame.Start();
    private int _ply;

    public OpeningTrainer(OpeningLine line) => _line = line;

    public string Fen => _game.Fen;
    public bool IsComplete => _ply >= _line.Moves.Count;
    public string? ExpectedMove => IsComplete ? null : _line.Moves[_ply];

    /// <summary>Plays a move (SAN or UCI). True and advances if it is the next book move.</summary>
    public bool Play(string move)
    {
        if (IsComplete)
            return false;

        var expected = _line.Moves[_ply];
        var uci = _game.ResolveToUci(move);
        if (uci is null || !string.Equals(uci, expected, StringComparison.OrdinalIgnoreCase))
            return false;

        _game.TryMakeMove(expected);
        _ply++;
        return true;
    }
}

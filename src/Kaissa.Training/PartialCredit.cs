using System.Collections.Generic;
using System.Linq;
using Kaissa.Chess.Rules;

namespace Kaissa.Training;

/// <summary>Grade of a puzzle answer: fully correct, partially credited, or wrong.</summary>
public enum PuzzleGrade { Wrong, Partial, Correct }

/// <summary>
/// Adds CT-ART-style partial credit: a move that is not the puzzle's best move but still grabs at
/// least as much material is credited as Partial rather than hard-failed, which reads better for
/// learners who find a winning idea that is not the sharpest. This is an offline HEURISTIC (material
/// captured on the move, read from the board) - it has no engine and cannot know that a bigger capture
/// is actually sound, so it is deliberately conservative and only applies when the intended solution is
/// itself a capture. The engine-backed version (evaluate the resulting position) is a later upgrade.
/// </summary>
public static class PartialCredit
{
    public static PuzzleGrade Assess(string fen, string playedUci, IReadOnlyList<string> solutions)
    {
        if (string.IsNullOrWhiteSpace(playedUci)) return PuzzleGrade.Wrong;
        if (solutions != null && solutions.Any(s => string.Equals(s, playedUci, System.StringComparison.OrdinalIgnoreCase)))
            return PuzzleGrade.Correct;

        ChessGame game;
        try { game = ChessGame.FromFen(fen); }
        catch { return PuzzleGrade.Wrong; }

        // Must be a legal move to earn anything.
        if (!game.LegalUciMoves().Any(m => string.Equals(m, playedUci, System.StringComparison.OrdinalIgnoreCase)))
            return PuzzleGrade.Wrong;

        int solutionGain = solutions is { Count: > 0 } ? CaptureValue(fen, solutions[0]) : 0;
        if (solutionGain <= 0) return PuzzleGrade.Wrong; // solution is not about winning material

        int playedGain = CaptureValue(fen, playedUci);
        return playedGain >= solutionGain ? PuzzleGrade.Partial : PuzzleGrade.Wrong;
    }

    // Value of the piece captured by a UCI move (0 if it captures nothing), read from the FEN board.
    private static int CaptureValue(string fen, string uci)
    {
        if (string.IsNullOrEmpty(uci) || uci.Length < 4) return 0;
        var grid = ParseGrid(fen);
        int tf = uci[2] - 'a', tr = uci[3] - '1';
        if (tf is < 0 or > 7 || tr is < 0 or > 7) return 0;
        return Value(grid[tf, tr]);
    }

    private static int Value(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0,
    };

    private static char[,] ParseGrid(string fen)
    {
        var g = new char[8, 8];
        var ranks = fen.Split(' ')[0].Split('/');
        for (int i = 0; i < 8 && i < ranks.Length; i++)
        {
            int rank = 7 - i, file = 0;
            foreach (char c in ranks[i])
            {
                if (char.IsDigit(c)) file += c - '0';
                else { if (file < 8) g[file, rank] = c; file++; }
            }
        }
        return g;
    }
}

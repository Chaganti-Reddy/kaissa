using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>Tactical motif of a move, to the extent it can be recognised reliably.</summary>
public enum Motif
{
    Checkmate,
    Fork,
    DiscoveredAttack,
    Skewer,
    Pin,
    HangingPiece,
    Unclassified,
}

/// <summary>
/// Recognises the motif of a move from the board. Detects the cases that can be judged precisely —
/// checkmate, a fork (the moved piece attacks two or more valuable enemy pieces), and winning an
/// undefended piece. Pins, skewers, and discovered attacks need deeper analysis and fall back to
/// <see cref="Motif.Unclassified"/> rather than guess.
/// </summary>
public static class MotifClassifier
{
    public static Motif Classify(string fenBefore, string uciMove)
    {
        var game = ChessGame.FromFen(fenBefore);
        if (!game.TryMakeMove(uciMove))
            return Motif.Unclassified;

        if (game.IsCheckmate)
            return Motif.Checkmate;

        var before = new AttackBoard(fenBefore);
        var after = new AttackBoard(game.Fen);

        var to = AttackBoard.Square(uciMove.Substring(2, 2));
        char moved = after.PieceAt(to.file, to.rank);
        if (moved == '\0')
            return Motif.Unclassified;
        bool moverWhite = after.IsWhite(moved);

        if (CountForkTargets(after, to, moverWhite) >= 2)
            return Motif.Fork;

        var from = AttackBoard.Square(uciMove.Substring(0, 2));
        if (IsDiscoveredAttack(after, from, to, moverWhite))
            return Motif.DiscoveredAttack;

        if (PinOrSkewer(after, to, moved, moverWhite) is { } lineMotif)
            return lineMotif;

        if (IsWinningUndefendedPiece(before, to, moverWhite))
            return Motif.HangingPiece;

        return Motif.Unclassified;
    }

    private static Motif? PinOrSkewer(AttackBoard board, (int file, int rank) from, char slider, bool moverWhite)
    {
        foreach (var (df, dr) in AttackBoard.SliderDirections(slider))
        {
            var line = board.RayPieces(from.file, from.rank, df, dr).Take(2).ToList();
            if (line.Count < 2)
                continue;

            var (_, _, front) = line[0];
            var (_, _, back) = line[1];
            // Both pieces beyond the slider must be the opponent's for a pin/skewer.
            if (board.IsWhite(front) == moverWhite || board.IsWhite(back) == moverWhite)
                continue;

            int frontValue = Value(front);
            int backValue = Value(back);

            if (backValue > frontValue)
                return Motif.Pin; // lesser piece pinned against a more valuable one behind it
            if (frontValue > backValue && frontValue >= 5)
                return Motif.Skewer; // valuable piece in front, lesser exposed behind
        }

        return null;
    }

    private static bool IsDiscoveredAttack(AttackBoard board, (int file, int rank) origin, (int file, int rank) moved, bool moverWhite)
    {
        // A friendly slider, other than the piece that just moved, now attacks an enemy king or
        // queen along a line that runs through the vacated origin square (i.e. it was blocked by
        // the moved piece a moment ago).
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            char piece = board.PieceAt(f, r);
            if (piece == '\0' || board.IsWhite(piece) != moverWhite)
                continue;
            if (f == moved.file && r == moved.rank)
                continue; // the piece that moved is not the revealer

            foreach (var (df, dr) in AttackBoard.SliderDirections(piece))
            {
                bool passedOrigin = false;
                foreach (var (sf, sr, sp) in board.RaySquares(f, r, df, dr))
                {
                    if (sf == origin.file && sr == origin.rank)
                        passedOrigin = true;
                    if (sp == '\0')
                        continue;

                    // First piece along this ray.
                    if (passedOrigin && board.IsWhite(sp) != moverWhite && char.ToUpperInvariant(sp) is 'K' or 'Q')
                        return true;
                    break;
                }
            }
        }

        return false;
    }

    private static int Value(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => 1,
        'N' => 3,
        'B' => 3,
        'R' => 5,
        'Q' => 9,
        'K' => 100,
        _ => 0,
    };

    private static int CountForkTargets(AttackBoard board, (int file, int rank) from, bool moverWhite)
    {
        int targets = 0;
        foreach (var (f, r) in board.AttacksFrom(from.file, from.rank))
        {
            char piece = board.PieceAt(f, r);
            if (piece == '\0' || board.IsWhite(piece) == moverWhite)
                continue;
            if (char.ToUpperInvariant(piece) is 'N' or 'B' or 'R' or 'Q' or 'K')
                targets++;
        }

        return targets;
    }

    private static bool IsWinningUndefendedPiece(AttackBoard before, (int file, int rank) to, bool moverWhite)
    {
        char captured = before.PieceAt(to.file, to.rank);
        if (captured == '\0' || before.IsWhite(captured) == moverWhite)
            return false; // not a capture of an enemy piece

        // Undefended if the opponent does not attack the square (approximate: ignores x-rays).
        return !before.IsAttackedBy(to.file, to.rank, byWhite: !moverWhite);
    }
}

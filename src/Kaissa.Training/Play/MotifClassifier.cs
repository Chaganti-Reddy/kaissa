using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>Tactical motif of a move, to the extent it can be recognised reliably.</summary>
public enum Motif
{
    Checkmate,
    Fork,
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

        if (IsWinningUndefendedPiece(before, to, moverWhite))
            return Motif.HangingPiece;

        return Motif.Unclassified;
    }

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

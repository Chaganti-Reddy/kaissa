namespace Kaissa.Training.Play;

/// <summary>
/// A lightweight board built from the piece-placement field of a FEN, with just enough to answer
/// "what does the piece on this square attack" and "is this square attacked by a side". Used by
/// <see cref="MotifClassifier"/> to recognise tactical motifs. Independent of the rules library on
/// purpose: it is small, pure, and easy to test.
/// </summary>
public sealed class AttackBoard
{
    private static readonly (int df, int dr)[] KnightHops =
        { (1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2) };
    private static readonly (int df, int dr)[] KingSteps =
        { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };
    private static readonly (int df, int dr)[] BishopDirs = { (1, 1), (1, -1), (-1, 1), (-1, -1) };
    private static readonly (int df, int dr)[] RookDirs = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private readonly char[,] _pieces = new char[8, 8]; // [file, rank]; '\0' = empty

    /// <param name="fen">A full FEN or just its piece-placement field.</param>
    public AttackBoard(string fen)
    {
        var placement = fen.Split(' ')[0];
        var rows = placement.Split('/'); // rows[0] = rank 8
        for (int i = 0; i < rows.Length && i < 8; i++)
        {
            int rank = 7 - i;
            int file = 0;
            foreach (var ch in rows[i])
            {
                if (char.IsDigit(ch))
                    file += ch - '0';
                else if (file < 8)
                    _pieces[file++, rank] = ch;
            }
        }
    }

    public char PieceAt(int file, int rank) => _pieces[file, rank];

    public bool IsWhite(char piece) => char.IsUpper(piece);

    /// <summary>Squares attacked by the piece standing on (file, rank). Empty if the square is empty.</summary>
    public IEnumerable<(int file, int rank)> AttacksFrom(int file, int rank)
    {
        char piece = _pieces[file, rank];
        if (piece == '\0')
            yield break;

        bool white = IsWhite(piece);
        switch (char.ToUpperInvariant(piece))
        {
            case 'P':
                int dr = white ? 1 : -1;
                foreach (var df in new[] { -1, 1 })
                    if (OnBoard(file + df, rank + dr))
                        yield return (file + df, rank + dr);
                break;

            case 'N':
                foreach (var (df, d) in KnightHops)
                    if (OnBoard(file + df, rank + d))
                        yield return (file + df, rank + d);
                break;

            case 'K':
                foreach (var (df, d) in KingSteps)
                    if (OnBoard(file + df, rank + d))
                        yield return (file + df, rank + d);
                break;

            case 'B':
                foreach (var sq in Slide(file, rank, BishopDirs)) yield return sq;
                break;
            case 'R':
                foreach (var sq in Slide(file, rank, RookDirs)) yield return sq;
                break;
            case 'Q':
                foreach (var sq in Slide(file, rank, BishopDirs)) yield return sq;
                foreach (var sq in Slide(file, rank, RookDirs)) yield return sq;
                break;
        }
    }

    /// <summary>True if any piece of the given colour attacks (file, rank).</summary>
    public bool IsAttackedBy(int file, int rank, bool byWhite)
    {
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            char piece = _pieces[f, r];
            if (piece == '\0' || IsWhite(piece) != byWhite)
                continue;
            foreach (var (af, ar) in AttacksFrom(f, r))
                if (af == file && ar == rank)
                    return true;
        }

        return false;
    }

    /// <summary>Every square, in order, walking from (file, rank) along a direction (empties included, as '\0').</summary>
    public IEnumerable<(int file, int rank, char piece)> RaySquares(int file, int rank, int df, int dr)
    {
        int f = file + df, r = rank + dr;
        while (OnBoard(f, r))
        {
            yield return (f, r, _pieces[f, r]);
            f += df;
            r += dr;
        }
    }

    /// <summary>The pieces met, in order, walking from (file, rank) along a direction (empties skipped).</summary>
    public IEnumerable<(int file, int rank, char piece)> RayPieces(int file, int rank, int df, int dr)
    {
        int f = file + df, r = rank + dr;
        while (OnBoard(f, r))
        {
            char piece = _pieces[f, r];
            if (piece != '\0')
                yield return (f, r, piece);
            f += df;
            r += dr;
        }
    }

    /// <summary>Sliding directions for a piece letter (bishop/rook/queen); empty for others.</summary>
    public static IReadOnlyList<(int df, int dr)> SliderDirections(char piece) => char.ToUpperInvariant(piece) switch
    {
        'B' => BishopDirs,
        'R' => RookDirs,
        'Q' => BishopDirs.Concat(RookDirs).ToArray(),
        _ => Array.Empty<(int, int)>(),
    };

    private IEnumerable<(int, int)> Slide(int file, int rank, (int df, int dr)[] dirs)
    {
        foreach (var (df, dr) in dirs)
        {
            int f = file + df, r = rank + dr;
            while (OnBoard(f, r))
            {
                yield return (f, r);
                if (_pieces[f, r] != '\0')
                    break; // stop at the first blocker (which is attacked)
                f += df;
                r += dr;
            }
        }
    }

    private static bool OnBoard(int file, int rank) => file is >= 0 and < 8 && rank is >= 0 and < 8;

    /// <summary>Parses a UCI square like "e4" into (file, rank) with a=0, rank1=0.</summary>
    public static (int file, int rank) Square(string uciSquare) =>
        (uciSquare[0] - 'a', uciSquare[1] - '1');
}

namespace Kaissa.Training.Api;

/// <summary>An occupied square, e.g. ("e1", 'K'). Piece is FEN letter (upper = white).</summary>
public readonly record struct BoardSquare(string Square, char Piece);

/// <summary>
/// A renderer-friendly view of a position: which pieces sit on which squares, and whose turn it is.
/// Lets a UI (Unity or otherwise) draw the board without knowing anything about FEN or the engine.
/// </summary>
public sealed record BoardView(IReadOnlyList<BoardSquare> Pieces, bool WhiteToMove, string Fen)
{
    public static BoardView FromFen(string fen)
    {
        var parts = fen.Split(' ');
        var rows = parts[0].Split('/'); // rows[0] = rank 8
        var pieces = new List<BoardSquare>();

        for (int i = 0; i < rows.Length && i < 8; i++)
        {
            int rank = 8 - i;
            int file = 0;
            foreach (var ch in rows[i])
            {
                if (char.IsDigit(ch))
                {
                    file += ch - '0';
                }
                else if (file < 8)
                {
                    var square = $"{(char)('a' + file)}{rank}";
                    pieces.Add(new BoardSquare(square, ch));
                    file++;
                }
            }
        }

        bool whiteToMove = parts.Length < 2 || parts[1] != "b";
        return new BoardView(pieces, whiteToMove, fen);
    }
}

/// <summary>A position presented to the player, with everything a UI needs to show it.</summary>
public sealed record TrainingCard(
    string PatternId,
    string PatternName,
    string PatternDescription,
    BoardView Board,
    string Prompt,
    int PuzzleRating,
    double PlayerRating,
    IReadOnlyList<string> LegalMoves);

/// <summary>The outcome of answering a card.</summary>
public sealed record AnswerResult(
    bool Correct,
    string Grade,
    IReadOnlyList<string> Solutions,
    int NextReviewInDays,
    double PlayerRating,
    double PlayerRatingChange);

/// <summary>Headline stats for an insights screen.</summary>
public sealed record PlayerStats(
    double Rating,
    int TotalAttempts,
    int TotalCorrect,
    double Accuracy,
    int PatternsSeen,
    int CurrentStreak,
    int BestStreak,
    IReadOnlyList<double> RatingHistory);

/// <summary>One row of the player's progress across a pattern.</summary>
public sealed record ProgressRow(
    string PatternId,
    string PatternName,
    bool Seen,
    int Reps,
    int Lapses,
    double StabilityDays);

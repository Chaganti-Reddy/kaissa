using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;

namespace Kaissa.Training.Play;

/// <summary>How one move stacked up against the engine's best.</summary>
public sealed record MoveAssessment(
    int Ply,
    Side Side,
    string Fen,
    string PlayedMove,
    string PlayedMoveSan,
    string BestMove,
    string BestMoveSan,
    int CentipawnLoss,
    MoveQuality Quality,
    int BestEvalCp,
    int PlayedEvalCp);

/// <summary>
/// Reviews a finished game with the engine at full strength, producing a per-move assessment for both
/// sides. Uses two candidate lines so it can tell an only-good move (Great) from an ordinary best one,
/// checks opening theory for Book moves, and material swings for sacrifices (Brilliant). Mistakes feed
/// the practice generator, so playing a game becomes training.
/// </summary>
public sealed class GameAnalyzer
{
    private readonly IChessEngine _engine;
    private readonly int _depth;
    private readonly OpeningBook? _book;

    public GameAnalyzer(IChessEngine engine, int depth = 14, OpeningBook? book = null)
    {
        _engine = engine;
        _depth = depth;
        _book = book;
    }

    /// <summary>Assesses every move in the game (both sides), oldest first.</summary>
    public async Task<IReadOnlyList<MoveAssessment>> AnalyzeAsync(
        string startFen, IReadOnlyList<string> moves, Side playerSide, CancellationToken cancellationToken = default)
    {
        await _engine.ConfigureStrengthAsync(StrengthSettings.Full, cancellationToken).ConfigureAwait(false);

        var assessments = new List<MoveAssessment>();
        var game = ChessGame.FromFen(startFen);
        int ply = 0;

        foreach (var move in moves)
        {
            assessments.Add(await AssessAsync(game.Fen, move, ply, game.SideToMove, cancellationToken).ConfigureAwait(false));
            game.TryMakeMove(move);
            ply++;
        }

        return assessments;
    }

    private async Task<MoveAssessment> AssessAsync(string fen, string playedMove, int ply, Side sideToMove, CancellationToken ct)
    {
        var before = await _engine.AnalyzeAsync(fen, SearchLimits.ToDepth(_depth, 2), ct).ConfigureAwait(false);
        int bestCp = before.Evaluation is { } bestEval ? MoveClassifier.ToCentipawns(bestEval) : 0;
        // Gap to the second-best line: a large gap means the best move was the only good one.
        int secondCp = before.Lines.Count > 1 ? MoveClassifier.ToCentipawns(before.Lines[1].Score) : bestCp - 1000;
        int secondGap = Math.Max(0, bestCp - secondCp);

        var sanBoard = ChessGame.FromFen(fen);
        string bestSan = sanBoard.SanForUci(before.BestMove) ?? before.BestMove;
        string playedSan = ChessGame.FromFen(fen).SanForUci(playedMove) ?? playedMove;

        var probe = ChessGame.FromFen(fen);
        probe.TryMakeMove(playedMove);

        int playedCp;
        bool isSacrifice = false;
        if (probe.IsGameOver)
        {
            playedCp = probe.Result switch
            {
                GameResult.Draw => 0,
                GameResult.WhiteWins => sideToMove == Side.White ? 100_000 : -100_000,
                GameResult.BlackWins => sideToMove == Side.White ? -100_000 : 100_000,
                _ => 0,
            };
        }
        else
        {
            var after = await _engine.AnalyzeAsync(probe.Fen, SearchLimits.ToDepth(_depth), ct).ConfigureAwait(false);
            // After the move it is the opponent to move, so negate to the mover's perspective.
            playedCp = after.Evaluation is { } afterEval ? -MoveClassifier.ToCentipawns(afterEval) : 0;
            isSacrifice = IsSacrifice(fen, probe.Fen, after.BestMove, sideToMove);
        }

        int loss = Math.Max(0, bestCp - playedCp);
        bool isBook = _book != null
            && _book.Continuations(fen).Any(c => string.Equals(c.Uci, playedMove, StringComparison.OrdinalIgnoreCase));

        var quality = MoveClassifier.Classify(new MoveJudgement(loss, bestCp, playedCp, secondGap, isBook, isSacrifice));
        return new MoveAssessment(ply, sideToMove, fen, playedMove, playedSan, before.BestMove, bestSan, loss, quality, bestCp, playedCp);
    }

    // A sacrifice: after the move and the opponent's best reply, the mover is down at least a minor
    // piece of material versus before. Conservative on purpose (a real, un-recaptured give-up).
    private static bool IsSacrifice(string fenBefore, string fenAfterMove, string opponentReply, Side moverSide)
    {
        int before = Material(fenBefore, moverSide);
        var g = ChessGame.FromFen(fenAfterMove);
        if (!string.IsNullOrEmpty(opponentReply)) g.TryMakeMove(opponentReply);
        int after = Material(g.Fen, moverSide);
        return before - after >= 2;
    }

    private static int Material(string fen, Side side)
    {
        int total = 0;
        bool wantWhite = side == Side.White;
        foreach (char c in fen.Split(' ')[0])
        {
            if (!char.IsLetter(c)) continue;
            bool isWhite = char.IsUpper(c);
            if (isWhite != wantWhite) continue;
            total += char.ToUpperInvariant(c) switch
            {
                'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, _ => 0,
            };
        }
        return total;
    }
}

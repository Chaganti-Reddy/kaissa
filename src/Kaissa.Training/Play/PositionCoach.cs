using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training.Play;

/// <summary>One engine suggestion handed to the coach: a move in SAN and its already-formatted score.</summary>
public sealed record CoachLine(string San, string ScoreText);

/// <summary>A board arrow the coach wants drawn, in UCI square coordinates (e.g. from "c4" to "f7").</summary>
public sealed record CoachArrow(string From, string To);

/// <summary>
/// A whole-position explanation in five plain-language categories, mirroring the way a coaching tool
/// breaks a position down: what the opponent threatens, the best moves, the plans the position calls
/// for, the role each piece is playing, and the named concepts on the board. Every line is templated
/// text - no engine prose, no network, no LLM - so it stays free and offline.
/// </summary>
public sealed record PositionExplanation(
    IReadOnlyList<string> Threats,
    IReadOnlyList<string> BestMoves,
    IReadOnlyList<string> Plans,
    IReadOnlyList<string> PieceRoles,
    IReadOnlyList<string> Concepts,
    IReadOnlyList<CoachArrow> ThreatArrows,
    IReadOnlyList<CoachArrow> RoleArrows);

/// <summary>
/// Builds a <see cref="PositionExplanation"/> from a FEN (and the engine's top lines, for the best-move
/// list). The tactical and positional reads come from a pure board scan (<see cref="AttackBoard"/>), so
/// the coach works with or without an engine attached; without lines the best-move list is simply empty.
/// The reads are deliberately simple and honest - useful pointers for a learner, not a full evaluation.
/// </summary>
public static class PositionCoach
{
    private const int MaxItems = 6;

    public static PositionExplanation Explain(string fen, IReadOnlyList<CoachLine>? engineLines = null)
    {
        var board = new AttackBoard(fen);
        bool whiteToMove = SideToMove(fen);

        return new PositionExplanation(
            Threats(board, whiteToMove),
            BestMoves(engineLines),
            Plans(board, whiteToMove),
            PieceRoles(board, whiteToMove),
            Concepts(board, whiteToMove),
            ThreatArrows(board, whiteToMove),
            RoleArrows(board, whiteToMove));
    }

    // Red arrows: each enemy attacker pointing at a friendly piece it threatens (undefended or cheaper).
    private static IReadOnlyList<CoachArrow> ThreatArrows(AttackBoard board, bool weAreWhite)
    {
        var arrows = new List<CoachArrow>();
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != weAreWhite || char.ToUpperInvariant(piece) == 'K') continue;
            if (!board.IsAttackedBy(f, r, byWhite: !weAreWhite)) continue;
            bool defended = board.IsAttackedBy(f, r, byWhite: weAreWhite);
            var attacker = CheapestAttackerSquare(board, f, r, byWhite: !weAreWhite);
            if (attacker is { } a && (!defended || Value(board.PieceAt(a.f, a.r)) < Value(piece)))
                arrows.Add(new CoachArrow(Sq(a.f, a.r), Sq(f, r)));
        }
        return Cap(arrows);
    }

    // Blue arrows: each developed friendly piece pointing at the most valuable enemy piece it bears on.
    private static IReadOnlyList<CoachArrow> RoleArrows(AttackBoard board, bool weAreWhite)
    {
        var arrows = new List<(int order, CoachArrow arrow)>();
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != weAreWhite) continue;
            if (char.ToUpperInvariant(piece) is 'P' or 'K') continue;

            int bestVal = 0; (int f, int r)? target = null;
            foreach (var (af, ar) in board.AttacksFrom(f, r))
            {
                char t = board.PieceAt(af, ar);
                if (t == '\0' || board.IsWhite(t) == weAreWhite) continue;
                if (Value(t) > bestVal) { bestVal = Value(t); target = (af, ar); }
            }
            if (target is { } tg)
                arrows.Add((Value(piece), new CoachArrow(Sq(f, r), Sq(tg.f, tg.r))));
        }
        return Cap(arrows.OrderByDescending(x => x.order).Select(x => x.arrow).ToList());
    }

    private static bool SideToMove(string fen)
    {
        var parts = fen.Split(' ');
        return parts.Length < 2 || parts[1] == "w";
    }

    // What the side NOT to move is threatening against the side to move: attacks on undefended or
    // higher-value friendly pieces (the ones that cost material if ignored).
    private static IReadOnlyList<string> Threats(AttackBoard board, bool weAreWhite)
    {
        var threats = new List<string>();
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != weAreWhite) continue;          // only our pieces can be threatened
            if (char.ToUpperInvariant(piece) == 'K') continue;         // check handling is the engine's job
            if (!board.IsAttackedBy(f, r, byWhite: !weAreWhite)) continue;

            bool defended = board.IsAttackedBy(f, r, byWhite: weAreWhite);
            int attackerVal = CheapestAttacker(board, f, r, byWhite: !weAreWhite);
            if (!defended || attackerVal < Value(piece))
                threats.Add($"Your {Name(piece)} on {Sq(f, r)} is under attack{(defended ? "" : " and undefended")}.");
        }

        if (threats.Count == 0)
            threats.Add("No immediate material threats against you.");
        return Cap(threats);
    }

    private static IReadOnlyList<string> BestMoves(IReadOnlyList<CoachLine>? lines)
    {
        if (lines == null || lines.Count == 0)
            return new[] { "Attach the engine to see the best moves here." };
        return lines.Take(MaxItems)
            .Select((l, i) => $"{i + 1}. {l.San}   {l.ScoreText}")
            .ToList();
    }

    // Templated plans from a few reliably-detectable features.
    private static IReadOnlyList<string> Plans(AttackBoard board, bool weAreWhite)
    {
        var plans = new List<string>();

        foreach (int file in OpenAndHalfOpenFiles(board, weAreWhite))
        {
            bool open = !HasPawnOnFile(board, file, white: !weAreWhite);
            plans.Add($"Use the {FileName(file)}-file ({(open ? "open" : "half-open")}) with a rook.");
        }

        foreach (var (f, r) in Outposts(board, weAreWhite).Take(2))
            plans.Add($"{Sq(f, r)} is an outpost - a knight there is hard to dislodge.");

        if (KingOnHome(board, weAreWhite))
            plans.Add("Get the king to safety - castle before opening the centre.");

        if (plans.Count == 0)
            plans.Add("Improve your least active piece and keep developing.");
        return Cap(plans);
    }

    // The job each of our pieces is doing: what it attacks and what it defends.
    private static IReadOnlyList<string> PieceRoles(AttackBoard board, bool weAreWhite)
    {
        var roles = new List<(int order, string text)>();
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != weAreWhite) continue;
            char up = char.ToUpperInvariant(piece);
            if (up is 'P' or 'K') continue; // focus on the developed pieces

            var attacks = new List<string>();
            var defends = new List<string>();
            foreach (var (af, ar) in board.AttacksFrom(f, r))
            {
                char t = board.PieceAt(af, ar);
                if (t == '\0') continue;
                if (board.IsWhite(t) == weAreWhite) defends.Add(Name(t));
                else attacks.Add(Name(t));
            }

            string detail = attacks.Count > 0 ? $"attacks the {Join(attacks)}"
                : defends.Count > 0 ? $"defends the {Join(defends)}"
                : "is not yet doing much";
            roles.Add((Value(piece), $"{Name(piece)} on {Sq(f, r)} {detail}."));
        }

        var ordered = roles.OrderByDescending(x => x.order).Select(x => x.text).ToList();
        if (ordered.Count == 0)
            ordered.Add("Only pawns and the king are on the board.");
        return Cap(ordered);
    }

    private static IReadOnlyList<string> Concepts(AttackBoard board, bool weAreWhite)
    {
        var concepts = new List<string>();

        if (CountPiece(board, 'B', weAreWhite) >= 2)
            concepts.Add("You have the bishop pair.");

        var isolated = IsolatedPawn(board, weAreWhite);
        if (isolated is { } iso)
            concepts.Add($"Isolated pawn on {Sq(iso.f, iso.r)} - a long-term weakness to watch.");

        var doubledFile = DoubledPawnFile(board, weAreWhite);
        if (doubledFile is { } df)
            concepts.Add($"Doubled pawns on the {FileName(df)}-file.");

        foreach (int file in OpenAndHalfOpenFiles(board, weAreWhite).Where(fl => !HasPawnOnFile(board, fl, white: !weAreWhite)))
        {
            concepts.Add($"The {FileName(file)}-file is open.");
            break;
        }

        if (Outposts(board, weAreWhite).Any())
            concepts.Add("An outpost square is available for a knight.");

        if (KingOnHome(board, weAreWhite))
            concepts.Add("Your king is still in the centre.");

        if (concepts.Count == 0)
            concepts.Add("A balanced position with no sharp structural features.");
        return Cap(concepts);
    }

    // -- board helpers -------------------------------------------------------

    private static IEnumerable<(int f, int r, char piece)> Pieces(AttackBoard board)
    {
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            char p = board.PieceAt(f, r);
            if (p != '\0') yield return (f, r, p);
        }
    }

    private static int CheapestAttacker(AttackBoard board, int file, int rank, bool byWhite)
    {
        int best = int.MaxValue;
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != byWhite) continue;
            if (board.AttacksFrom(f, r).Any(sq => sq.file == file && sq.rank == rank))
                best = System.Math.Min(best, Value(piece));
        }
        return best;
    }

    private static (int f, int r)? CheapestAttackerSquare(AttackBoard board, int file, int rank, bool byWhite)
    {
        int best = int.MaxValue; (int f, int r)? square = null;
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (board.IsWhite(piece) != byWhite) continue;
            if (board.AttacksFrom(f, r).Any(sq => sq.file == file && sq.rank == rank) && Value(piece) < best)
            {
                best = Value(piece);
                square = (f, r);
            }
        }
        return square;
    }

    private static IEnumerable<int> OpenAndHalfOpenFiles(AttackBoard board, bool weAreWhite)
    {
        for (int file = 0; file < 8; file++)
            if (!HasPawnOnFile(board, file, white: weAreWhite)) // no friendly pawn -> (half-)open for us
                yield return file;
    }

    private static bool HasPawnOnFile(AttackBoard board, int file, bool white)
    {
        for (int r = 0; r < 8; r++)
        {
            char p = board.PieceAt(file, r);
            if (p != '\0' && char.ToUpperInvariant(p) == 'P' && board.IsWhite(p) == white) return true;
        }
        return false;
    }

    // Squares on our side's 4th-6th ranks, guarded by a friendly pawn and unreachable by any enemy pawn.
    private static IEnumerable<(int f, int r)> Outposts(AttackBoard board, bool weAreWhite)
    {
        int lo = weAreWhite ? 3 : 2, hi = weAreWhite ? 5 : 4; // ranks 4-6 (white) / 3-5 (black), 0-indexed
        for (int f = 0; f < 8; f++)
        for (int r = lo; r <= hi; r++)
        {
            if (board.PieceAt(f, r) != '\0') continue;
            if (!DefendedByOwnPawn(board, f, r, weAreWhite)) continue;
            if (AttackableByEnemyPawn(board, f, r, weAreWhite)) continue;
            yield return (f, r);
        }
    }

    private static bool DefendedByOwnPawn(AttackBoard board, int file, int rank, bool weAreWhite)
    {
        int dr = weAreWhite ? -1 : 1; // a defending pawn sits one rank behind
        foreach (int df in new[] { -1, 1 })
        {
            int f = file + df, r = rank + dr;
            if (f is < 0 or > 7 || r is < 0 or > 7) continue;
            char p = board.PieceAt(f, r);
            if (p != '\0' && char.ToUpperInvariant(p) == 'P' && board.IsWhite(p) == weAreWhite) return true;
        }
        return false;
    }

    private static bool AttackableByEnemyPawn(AttackBoard board, int file, int rank, bool weAreWhite)
    {
        // An enemy pawn on an adjacent file, anywhere ahead of this square, could be pushed to hit it.
        for (int f = file - 1; f <= file + 1; f += 2)
        {
            if (f is < 0 or > 7) continue;
            for (int r = 0; r < 8; r++)
            {
                char p = board.PieceAt(f, r);
                if (p == '\0' || char.ToUpperInvariant(p) != 'P' || board.IsWhite(p) == weAreWhite) continue;
                bool ahead = weAreWhite ? r > rank : r < rank;
                if (ahead) return true;
            }
        }
        return false;
    }

    private static bool KingOnHome(AttackBoard board, bool weAreWhite)
    {
        int rank = weAreWhite ? 0 : 7;
        char k = board.PieceAt(4, rank); // e1 / e8
        return k != '\0' && char.ToUpperInvariant(k) == 'K' && board.IsWhite(k) == weAreWhite;
    }

    private static int CountPiece(AttackBoard board, char kind, bool white) =>
        Pieces(board).Count(p => char.ToUpperInvariant(p.piece) == kind && board.IsWhite(p.piece) == white);

    private static (int f, int r)? IsolatedPawn(AttackBoard board, bool white)
    {
        foreach (var (f, r, piece) in Pieces(board))
        {
            if (char.ToUpperInvariant(piece) != 'P' || board.IsWhite(piece) != white) continue;
            bool neighbour = (f > 0 && HasPawnOnFile(board, f - 1, white)) || (f < 7 && HasPawnOnFile(board, f + 1, white));
            if (!neighbour) return (f, r);
        }
        return null;
    }

    private static int? DoubledPawnFile(AttackBoard board, bool white)
    {
        for (int file = 0; file < 8; file++)
        {
            int count = 0;
            for (int r = 0; r < 8; r++)
            {
                char p = board.PieceAt(file, r);
                if (p != '\0' && char.ToUpperInvariant(p) == 'P' && board.IsWhite(p) == white) count++;
            }
            if (count >= 2) return file;
        }
        return null;
    }

    // -- formatting ----------------------------------------------------------

    private static int Value(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => 1, 'N' => 3, 'B' => 3, 'R' => 5, 'Q' => 9, 'K' => 100, _ => 0,
    };

    private static string Name(char piece) => char.ToUpperInvariant(piece) switch
    {
        'P' => "pawn", 'N' => "knight", 'B' => "bishop", 'R' => "rook", 'Q' => "queen", 'K' => "king", _ => "piece",
    };

    private static string Sq(int file, int rank) => $"{(char)('a' + file)}{rank + 1}";
    private static string FileName(int file) => $"{(char)('a' + file)}";

    private static string Join(IReadOnlyList<string> items)
    {
        var distinct = items.Distinct().ToList();
        return distinct.Count switch
        {
            0 => "",
            1 => distinct[0],
            2 => $"{distinct[0]} and {distinct[1]}",
            _ => string.Join(", ", distinct.Take(distinct.Count - 1)) + $" and {distinct[^1]}",
        };
    }

    private static IReadOnlyList<string> Cap(List<string> items) =>
        items.Count > MaxItems ? items.Take(MaxItems).ToList() : items;

    private static IReadOnlyList<CoachArrow> Cap(List<CoachArrow> items) =>
        items.Count > MaxItems ? items.Take(MaxItems).ToList() : items;
}

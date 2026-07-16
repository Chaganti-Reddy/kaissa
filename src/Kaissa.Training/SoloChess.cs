using System;
using System.Collections.Generic;
using Kaissa.Training.Play;

namespace Kaissa.Training;

/// <summary>One Solo Chess capture: the piece on <see cref="From"/> takes the piece on <see cref="To"/>.</summary>
public sealed record SoloMove(string From, string To);

/// <summary>
/// Solo Chess (the chess.com single-player puzzle): every move must capture, no piece may capture more
/// than twice, and a king can never be captured - so if one is present it is forced to be the last piece.
/// Clear the board down to a single piece to win. Movement is standard chess (geometry + slider blockers),
/// reusing <see cref="AttackBoard"/>. Positions are generated to be solvable by a DFS filter.
/// </summary>
public sealed class SoloChess
{
    private readonly char[,] _b = new char[8, 8];   // [file, rank], '\0' = empty
    private readonly int[,] _cap = new int[8, 8];    // captures made by the piece now on this square

    public SoloChess(string placement)
    {
        var rows = placement.Split(' ')[0].Split('/');
        for (int i = 0; i < rows.Length && i < 8; i++)
        {
            int rank = 7 - i, file = 0;
            foreach (var ch in rows[i])
            {
                if (char.IsDigit(ch)) file += ch - '0';
                else if (file < 8) _b[file++, rank] = ch;
            }
        }
    }

    private SoloChess(char[,] b, int[,] cap)
    {
        Array.Copy(b, _b, b.Length);
        Array.Copy(cap, _cap, cap.Length);
    }

    public SoloChess Clone() => new(_b, _cap);

    public int PieceCount
    {
        get
        {
            int n = 0;
            for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) if (_b[f, r] != '\0') n++;
            return n;
        }
    }

    public bool Solved => PieceCount == 1;

    public char PieceAt(string sq) { var (f, r) = Sq(sq); return OnBoard(f, r) ? _b[f, r] : '\0'; }
    public int CapturesAt(string sq) { var (f, r) = Sq(sq); return OnBoard(f, r) ? _cap[f, r] : 0; }

    /// <summary>FEN piece-placement of the current board (side/rights are irrelevant here).</summary>
    public string Placement()
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                char c = _b[f, r];
                if (c == '\0') { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(c);
            }
            if (empty > 0) sb.Append(empty);
            if (r > 0) sb.Append('/');
        }
        return sb.ToString();
    }

    /// <summary>Placement for display: pieces that have already captured twice (and so can no longer
    /// move) are lower-cased, so the board can draw them "turned" like the online version does.</summary>
    public string DisplayPlacement()
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                char c = _b[f, r];
                if (c == '\0') { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(_cap[f, r] >= 2 ? char.ToLowerInvariant(c) : c);
            }
            if (empty > 0) sb.Append(empty);
            if (r > 0) sb.Append('/');
        }
        return sb.ToString();
    }

    /// <summary>Every legal capture available now: a piece that has moved fewer than twice taking a
    /// non-king piece it geometrically attacks (sliders respect blockers via AttackBoard).</summary>
    public IReadOnlyList<SoloMove> LegalMoves()
    {
        var moves = new List<SoloMove>();
        var ab = new AttackBoard(Placement());
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            if (_b[f, r] == '\0' || _cap[f, r] >= 2) continue; // empty, or already captured twice (stuck)
            foreach (var (tf, tr) in ab.AttacksFrom(f, r))
            {
                char target = _b[tf, tr];
                if (target == '\0' || char.ToUpperInvariant(target) == 'K') continue; // must capture; kings are safe
                moves.Add(new SoloMove(Name(f, r), Name(tf, tr)));
            }
        }
        return moves;
    }

    /// <summary>Applies a capture if it is legal now; returns false (unchanged) otherwise.</summary>
    public bool TryApply(SoloMove m)
    {
        var (ff, fr) = Sq(m.From);
        var (tf, tr) = Sq(m.To);
        if (!OnBoard(ff, fr) || !OnBoard(tf, tr)) return false;
        bool legal = false;
        foreach (var lm in LegalMoves())
            if (lm.From == m.From && lm.To == m.To) { legal = true; break; }
        if (!legal) return false;

        char piece = _b[ff, fr];
        int cap = _cap[ff, fr] + 1;
        _b[ff, fr] = '\0'; _cap[ff, fr] = 0;
        _b[tf, tr] = piece; _cap[tf, tr] = cap; // the mover replaces the captured piece, carrying its count
        return true;
    }

    /// <summary>Whether the position can be cleared to a single piece under the Solo Chess rules.</summary>
    public bool IsSolvable()
    {
        int budget = 40000;
        return Search(this, ref budget);
    }

    private static bool Search(SoloChess s, ref int budget)
    {
        if (s.PieceCount <= 1) return true;
        if (--budget <= 0) return false;
        foreach (var m in s.LegalMoves())
        {
            var next = s.Clone();
            next.TryApply(m);
            if (Search(next, ref budget)) return true;
        }
        return false;
    }

    // -- generation ----------------------------------------------------------

    private static readonly char[] Types = { 'Q', 'R', 'B', 'N', 'P' };

    /// <summary>A solvable Solo Chess board with the given piece count (optionally including a king that
    /// must end last). Returns null if none was found within the attempt budget for that count.</summary>
    public static string? Generate(int pieces, bool withKing, int seed)
    {
        pieces = Math.Clamp(pieces, 2, 16);
        var rng = new Random(seed);
        for (int attempt = 0; attempt < 600; attempt++)
        {
            var board = new char[8, 8];
            var squares = new List<(int f, int r)>();
            for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) squares.Add((f, r));
            Shuffle(squares, rng);

            int placed = 0;
            for (int i = 0; i < squares.Count && placed < pieces; i++)
            {
                var (f, r) = squares[i];
                char type;
                if (withKing && placed == 0) type = 'K';
                else
                {
                    type = Types[rng.Next(Types.Length)];
                    if (type == 'P' && (r == 0 || r == 7)) continue; // a pawn on the last rank can never move
                }
                board[f, r] = type;
                placed++;
            }
            if (placed < pieces) continue;

            var solo = new SoloChess(FromArray(board));
            if (solo.IsSolvable()) return solo.Placement();
        }
        return null;
    }

    private static string FromArray(char[,] b) => new SoloChess(PlacementOf(b)).Placement();

    private static string PlacementOf(char[,] b)
    {
        var sb = new System.Text.StringBuilder();
        for (int r = 7; r >= 0; r--)
        {
            int empty = 0;
            for (int f = 0; f < 8; f++)
            {
                char c = b[f, r];
                if (c == '\0') { empty++; continue; }
                if (empty > 0) { sb.Append(empty); empty = 0; }
                sb.Append(c);
            }
            if (empty > 0) sb.Append(empty);
            if (r > 0) sb.Append('/');
        }
        return sb.ToString();
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static (int f, int r) Sq(string s) => (s[0] - 'a', s[1] - '1');
    private static string Name(int f, int r) => $"{(char)('a' + f)}{(char)('1' + r)}";
    private static bool OnBoard(int f, int r) => f is >= 0 and < 8 && r is >= 0 and < 8;
}

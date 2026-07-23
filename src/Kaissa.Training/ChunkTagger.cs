using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>Which kind of pattern a chunk is.</summary>
public enum ChunkCategory { PawnStructure, PiecePlacement, KingSafety }

/// <summary>
/// A named structural chunk found in a position - the recurring configuration a stronger player sees as
/// one unit (Chase-Simon / Gobet-Simon). Tagging positions by chunk lets the trainer organise puzzles,
/// drills and spaced-repetition items around chunk recognition, which is the app's whole premise.
/// </summary>
public sealed record Chunk(string Name, ChunkCategory Category, bool White);

/// <summary>
/// Reads a FEN and reports the structural chunks present, per side. Deliberately simple and honest -
/// it detects reliably-identifiable configurations (pawn structure, a few piece placements, king
/// safety) from the board alone, no engine. Pure and deterministic, so it is unit-tested.
/// </summary>
public static class ChunkTagger
{
    public const string IsolatedPawn = "Isolated pawn";
    public const string DoubledPawns = "Doubled pawns";
    public const string PassedPawn = "Passed pawn";
    public const string BishopPair = "Bishop pair";
    public const string RookOpenFile = "Rook on an open file";
    public const string KnightOutpost = "Knight outpost";
    public const string Fianchetto = "Fianchettoed bishop";
    public const string CastledKingside = "Castled kingside";
    public const string CastledQueenside = "Castled queenside";
    public const string KingInCentre = "King in the centre";

    public static IReadOnlyList<Chunk> Tag(string fen)
    {
        var grid = Parse(fen); // grid[file, rank], rank 0 = rank 1; '\0' = empty
        var chunks = new List<Chunk>();

        foreach (bool white in new[] { true, false })
        {
            AddPawnChunks(grid, white, chunks);
            AddPieceChunks(grid, white, chunks);
            AddKingChunks(grid, white, chunks);
        }
        return chunks;
    }

    private static void AddPawnChunks(char[,] g, bool white, List<Chunk> chunks)
    {
        char pawn = white ? 'P' : 'p';
        var filesWithPawns = new List<int>();
        for (int f = 0; f < 8; f++)
        {
            int count = 0;
            for (int r = 0; r < 8; r++) if (g[f, r] == pawn) count++;
            if (count > 0) filesWithPawns.Add(f);
            if (count >= 2) chunks.Add(new Chunk(DoubledPawns, ChunkCategory.PawnStructure, white));
        }

        foreach (int f in filesWithPawns)
        {
            bool neighbour = filesWithPawns.Contains(f - 1) || filesWithPawns.Contains(f + 1);
            if (!neighbour) chunks.Add(new Chunk(IsolatedPawn, ChunkCategory.PawnStructure, white));
        }

        // Passed pawn: no enemy pawn on the same or an adjacent file, anywhere ahead of it.
        char enemyPawn = white ? 'p' : 'P';
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            if (g[f, r] != pawn) continue;
            bool blocked = false;
            for (int ef = f - 1; ef <= f + 1 && !blocked; ef++)
            {
                if (ef < 0 || ef > 7) continue;
                for (int er = 0; er < 8; er++)
                    if (g[ef, er] == enemyPawn && (white ? er > r : er < r)) { blocked = true; break; }
            }
            if (!blocked) chunks.Add(new Chunk(PassedPawn, ChunkCategory.PawnStructure, white));
        }
    }

    private static void AddPieceChunks(char[,] g, bool white, List<Chunk> chunks)
    {
        char bishop = white ? 'B' : 'b', rook = white ? 'R' : 'r', knight = white ? 'N' : 'n';
        char pawn = white ? 'P' : 'p', enemyPawn = white ? 'p' : 'P';

        int bishops = Count(g, bishop);
        if (bishops >= 2) chunks.Add(new Chunk(BishopPair, ChunkCategory.PiecePlacement, white));

        // Rook on an open file (no pawns of either colour on that file).
        for (int f = 0; f < 8; f++)
        {
            bool hasRook = false, hasPawn = false;
            for (int r = 0; r < 8; r++)
            {
                if (g[f, r] == rook) hasRook = true;
                if (g[f, r] == 'P' || g[f, r] == 'p') hasPawn = true;
            }
            if (hasRook && !hasPawn) chunks.Add(new Chunk(RookOpenFile, ChunkCategory.PiecePlacement, white));
        }

        // Fianchettoed bishop on its long-diagonal square, with the knight-pawn advanced.
        foreach (var (bf, br, pf, pr) in white
                     ? new[] { (6, 1, 6, 2), (1, 1, 1, 2) }   // Bg2 with g3 / Bb2 with b3
                     : new[] { (6, 6, 6, 5), (1, 6, 1, 5) })  // Bg7 with g6 / Bb7 with b6
        {
            if (g[bf, br] == bishop && g[pf, pr] == pawn)
                chunks.Add(new Chunk(Fianchetto, ChunkCategory.PiecePlacement, white));
        }

        // Knight outpost: a knight in the enemy half, guarded by a friendly pawn, unreachable by an enemy pawn.
        for (int f = 0; f < 8; f++)
        for (int r = 0; r < 8; r++)
        {
            if (g[f, r] != knight) continue;
            bool inEnemyHalf = white ? r >= 4 : r <= 3;
            if (!inEnemyHalf) continue;
            if (!DefendedByOwnPawn(g, f, r, white, pawn)) continue;
            if (AttackableByEnemyPawn(g, f, r, white, enemyPawn)) continue;
            chunks.Add(new Chunk(KnightOutpost, ChunkCategory.PiecePlacement, white));
        }
    }

    private static void AddKingChunks(char[,] g, bool white, List<Chunk> chunks)
    {
        char king = white ? 'K' : 'k';
        int homeRank = white ? 0 : 7;
        for (int f = 0; f < 8; f++)
        {
            if (g[f, homeRank] != king) continue;
            if (f >= 6) chunks.Add(new Chunk(CastledKingside, ChunkCategory.KingSafety, white));
            else if (f <= 2) chunks.Add(new Chunk(CastledQueenside, ChunkCategory.KingSafety, white));
            else if (f is 3 or 4) chunks.Add(new Chunk(KingInCentre, ChunkCategory.KingSafety, white));
        }
    }

    private static bool DefendedByOwnPawn(char[,] g, int f, int r, bool white, char pawn)
    {
        int dr = white ? -1 : 1;
        foreach (int df in new[] { -1, 1 })
        {
            int nf = f + df, nr = r + dr;
            if (nf is < 0 or > 7 || nr is < 0 or > 7) continue;
            if (g[nf, nr] == pawn) return true;
        }
        return false;
    }

    private static bool AttackableByEnemyPawn(char[,] g, int f, int r, bool white, char enemyPawn)
    {
        for (int nf = f - 1; nf <= f + 1; nf += 2)
        {
            if (nf is < 0 or > 7) continue;
            for (int nr = 0; nr < 8; nr++)
            {
                if (g[nf, nr] != enemyPawn) continue;
                bool ahead = white ? nr > r : nr < r;
                if (ahead) return true;
            }
        }
        return false;
    }

    private static int Count(char[,] g, char piece)
    {
        int n = 0;
        for (int f = 0; f < 8; f++) for (int r = 0; r < 8; r++) if (g[f, r] == piece) n++;
        return n;
    }

    private static char[,] Parse(string fen)
    {
        var g = new char[8, 8];
        var placement = fen.Split(' ')[0];
        var ranks = placement.Split('/'); // ranks[0] = rank 8
        for (int i = 0; i < 8 && i < ranks.Length; i++)
        {
            int rank = 7 - i; // FEN top row is rank 8
            int file = 0;
            foreach (char c in ranks[i])
            {
                if (char.IsDigit(c)) file += c - '0';
                else { if (file < 8) g[file, rank] = c; file++; }
            }
        }
        return g;
    }
}

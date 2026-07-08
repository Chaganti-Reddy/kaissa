namespace Kaissa.Training.Play;

/// <summary>The broad phase a position belongs to, for reporting accuracy by phase.</summary>
public enum GamePhase
{
    Opening,
    Middlegame,
    Endgame,
}

/// <summary>
/// Classifies a position into opening / middlegame / endgame. A light heuristic: few pieces left is
/// an endgame regardless of move number; otherwise the first several moves are the opening and the
/// rest the middlegame. Good enough to report where a player's accuracy holds up or breaks down.
/// </summary>
public static class GamePhaseClassifier
{
    // Non-king material summed over both sides at the start is 78; queens-off with light forces is low.
    private const int EndgameMaterial = 20;
    private const int OpeningPly = 16; // first 8 full moves

    public static GamePhase Classify(string fen, int ply)
    {
        if (NonKingMaterial(fen) <= EndgameMaterial)
            return GamePhase.Endgame;
        return ply < OpeningPly ? GamePhase.Opening : GamePhase.Middlegame;
    }

    private static int NonKingMaterial(string fen)
    {
        int total = 0;
        foreach (char c in fen.Split(' ')[0])
        {
            total += char.ToUpperInvariant(c) switch
            {
                'P' => 1,
                'N' => 3,
                'B' => 3,
                'R' => 5,
                'Q' => 9,
                _ => 0, // digits, '/', kings
            };
        }
        return total;
    }
}

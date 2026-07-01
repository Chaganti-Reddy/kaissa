namespace Kaissa.Training;

/// <summary>A named endgame position to learn by playing it out against the engine.</summary>
public sealed record EndgamePosition(string Id, string Name, string Fen, string Goal);

/// <summary>
/// A small catalogue of instructive endgames. The trainer starts a game from one of these FENs
/// (via KaissaGame's fen parameter) so the player plays it out against the bot.
/// </summary>
public static class EndgameLibrary
{
    public static IReadOnlyList<EndgamePosition> All { get; } = new[]
    {
        new EndgamePosition("kq_v_k", "Queen and king mate",
            "8/8/8/4k3/8/8/2Q5/4K3 w - - 0 1", "Checkmate the lone king with your queen and king."),
        new EndgamePosition("kr_v_k", "Rook and king mate",
            "8/8/8/4k3/8/8/8/R3K3 w - - 0 1", "Drive the king to the edge and mate with the rook."),
        new EndgamePosition("kp_opposition", "King and pawn: the opposition",
            "8/8/8/4k3/8/4K3/4P3/8 w - - 0 1", "Use the opposition to escort the pawn to promotion."),
        new EndgamePosition("promotion", "Pawn promotion",
            "8/4P3/4k3/8/8/8/8/4K3 w - - 0 1", "Promote the pawn to a queen."),
    };

    public static EndgamePosition? ById(string id) => All.FirstOrDefault(p => p.Id == id);
}

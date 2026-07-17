using System.Collections.Generic;
using System.Linq;

namespace Kaissa.Training;

/// <summary>A cosmetic unlock: a board theme or piece-set "slot" bought with earned coins. Cosmetic only -
/// nothing here affects playing strength or gates training.</summary>
public sealed record CosmeticItem(string Id, string Name, string Kind, int Cost);

/// <summary>
/// The earned-cosmetic layer: coins accrue from play (never money), and can unlock cosmetic items. All
/// pure - the client persists the coin balance and the owned set; this defines the earn formula, the
/// catalogue, and purchase validation. Money never buys strength; coins never buy strength either.
/// </summary>
public static class CosmeticShop
{
    /// <summary>Lifetime coins earned from the player's activity. Monotonic in every input.</summary>
    public static int CoinsEarned(QuestSnapshot s) =>
        s.PuzzlesSolved * 1
        + s.GamesWon * 5
        + s.BotsBeaten * 10
        + s.DayStreak * 2
        + s.SoloBest * 3
        + s.MemoryBest * 2
        + s.VisualizationBest * 4;

    public static IReadOnlyList<CosmeticItem> Catalog { get; } = new[]
    {
        new CosmeticItem("board_midnight", "Midnight board", "board", 100),
        new CosmeticItem("board_coral", "Coral board", "board", 150),
        new CosmeticItem("pieces_gold", "Gold piece tint", "pieces", 200),
        new CosmeticItem("pieces_marble", "Marble piece tint", "pieces", 300),
        new CosmeticItem("board_tournament", "Tournament board", "board", 500),
    };

    public static CosmeticItem? ById(string id) => Catalog.FirstOrDefault(c => c.Id == id);

    /// <summary>Whether the given balance can buy an item the player does not already own.</summary>
    public static bool CanAfford(int balance, IReadOnlyCollection<string> owned, string itemId)
    {
        var item = ById(itemId);
        return item != null && !(owned?.Contains(itemId) ?? false) && balance >= item.Cost;
    }

    /// <summary>Attempts a purchase; on success returns the coins spent so the caller can deduct them.</summary>
    public static bool TryBuy(int balance, IReadOnlyCollection<string> owned, string itemId, out int cost)
    {
        cost = 0;
        var item = ById(itemId);
        if (item == null || (owned?.Contains(itemId) ?? false) || balance < item.Cost) return false;
        cost = item.Cost;
        return true;
    }
}

using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class CosmeticShopTests
{
    [Fact]
    public void Coins_are_earned_from_activity_and_are_monotonic()
    {
        var none = new QuestSnapshot(0, 0, 0, 0, 0, 0, 0, 0);
        var some = new QuestSnapshot(50, 3, 0, 4, 2, 8, 3, 6);
        Assert.Equal(0, CosmeticShop.CoinsEarned(none));
        Assert.True(CosmeticShop.CoinsEarned(some) > 0);
        var more = some with { GamesWon = 10 };
        Assert.True(CosmeticShop.CoinsEarned(more) > CosmeticShop.CoinsEarned(some));
    }

    [Fact]
    public void Catalog_has_distinct_ids_and_positive_costs()
    {
        var ids = CosmeticShop.Catalog.Select(c => c.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(CosmeticShop.Catalog, c => Assert.True(c.Cost > 0));
    }

    [Fact]
    public void Purchase_requires_affordable_and_unowned()
    {
        var owned = new[] { "board_midnight" };
        Assert.False(CosmeticShop.TryBuy(1000, owned, "board_midnight", out _)); // already owned
        Assert.False(CosmeticShop.TryBuy(50, owned, "board_coral", out _));       // too poor (cost 150)
        Assert.True(CosmeticShop.TryBuy(150, owned, "board_coral", out int cost));
        Assert.Equal(150, cost);
        Assert.False(CosmeticShop.TryBuy(1000, owned, "nope", out _));            // no such item
    }

    [Fact]
    public void CanAfford_matches_TryBuy()
    {
        Assert.True(CosmeticShop.CanAfford(200, System.Array.Empty<string>(), "pieces_gold"));
        Assert.False(CosmeticShop.CanAfford(199, System.Array.Empty<string>(), "pieces_gold"));
    }
}

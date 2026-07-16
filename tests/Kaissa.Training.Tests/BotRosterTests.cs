using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class BotRosterTests
{
    private static readonly string[] Archetypes = { "Hunter", "Guardian", "Savage", "Observer", "Mediator" };

    [Fact]
    public void Ladder_holds_every_bot_ordered_by_rating()
    {
        Assert.Equal(BotRoster.All.Count + BotRoster.Maia.Count, BotRoster.Ladder.Count);
        for (int i = 1; i < BotRoster.Ladder.Count; i++)
            Assert.True(BotRoster.Ladder[i].Elo >= BotRoster.Ladder[i - 1].Elo);
    }

    [Fact]
    public void Every_bot_has_a_known_archetype_and_a_style_line()
    {
        foreach (var b in BotRoster.Ladder)
        {
            Assert.Contains(b.Archetype, Archetypes);
            Assert.False(string.IsNullOrWhiteSpace(b.Style));
            Assert.False(string.IsNullOrWhiteSpace(b.Name));
        }
    }

    [Fact]
    public void Personas_have_distinct_names()
    {
        var names = BotRoster.Ladder.Select(b => b.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}

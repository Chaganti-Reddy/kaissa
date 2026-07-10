using System.Linq;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class OpeningBookTests
{
    private static readonly OpeningBook Book = OpeningBook.LoadDefault();

    [Fact]
    public void Loads_a_large_named_set()
    {
        Assert.True(Book.All.Count > 3000, $"only {Book.All.Count} openings");
    }

    [Fact]
    public void Start_position_offers_the_main_first_moves()
    {
        var cont = Book.Continuations(ChessGame.StartFen);
        var ucis = cont.Select(c => c.Uci).ToList();
        Assert.Contains("e2e4", ucis);
        Assert.Contains("d2d4", ucis);
        // Each continuation should carry a SAN.
        Assert.All(cont, c => Assert.False(string.IsNullOrEmpty(c.San)));
    }

    [Fact]
    public void Names_a_known_position_the_italian_game()
    {
        var g = ChessGame.Start();
        foreach (var m in new[] { "e2e4", "e7e5", "g1f3", "b8c6", "f1c4" })
            Assert.True(g.TryMakeMove(m));

        var named = Book.Name(g.Fen);
        Assert.NotNull(named);
        Assert.Contains("Italian", named!.Name);
    }

    [Fact]
    public void Continuation_carries_the_resulting_opening_name()
    {
        var g = ChessGame.Start();
        foreach (var m in new[] { "e2e4", "e7e5", "g1f3", "b8c6" })
            g.TryMakeMove(m);

        var cont = Book.Continuations(g.Fen);
        // f1c4 -> Italian Game is a book continuation here.
        var italian = cont.FirstOrDefault(c => c.Uci == "f1c4");
        Assert.NotNull(italian);
        Assert.NotNull(italian!.Name);
        Assert.Contains("Italian", italian.Name!);
    }

    [Fact]
    public void Grouping_splits_by_first_move()
    {
        var groups = Book.Grouped();
        Assert.Equal(3, groups.Count);
        Assert.Equal("1. e4", groups[0].Group);
        Assert.True(groups[0].Entries.Count > 100);
        Assert.All(groups[0].Entries, e => Assert.Equal("e2e4", e.Uci[0]));
    }

    [Fact]
    public void Unknown_position_has_no_name()
    {
        // A random non-book position.
        var g = ChessGame.Start();
        foreach (var m in new[] { "a2a4", "a7a5", "h2h4", "h7h5" })
            g.TryMakeMove(m);
        Assert.Null(Book.Name(g.Fen));
    }
}

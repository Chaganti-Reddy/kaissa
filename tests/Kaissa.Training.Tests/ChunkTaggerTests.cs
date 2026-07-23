using System.Linq;
using Kaissa.Training;
using Xunit;

namespace Kaissa.Training.Tests;

public class ChunkTaggerTests
{
    private static bool Has(string fen, string name, bool white) =>
        ChunkTagger.Tag(fen).Any(c => c.Name == name && c.White == white);

    [Fact]
    public void Detects_an_isolated_and_passed_pawn()
    {
        const string fen = "4k3/8/8/8/3P4/8/8/4K3 w - - 0 1"; // lone white d-pawn
        Assert.True(Has(fen, ChunkTagger.IsolatedPawn, white: true));
        Assert.True(Has(fen, ChunkTagger.PassedPawn, white: true));
    }

    [Fact]
    public void Detects_doubled_pawns()
    {
        Assert.True(Has("4k3/8/8/3P4/3P4/8/8/4K3 w - - 0 1", ChunkTagger.DoubledPawns, white: true));
    }

    [Fact]
    public void A_pawn_with_an_enemy_pawn_ahead_is_not_passed()
    {
        // White d4 pawn, black e5 pawn on an adjacent file ahead of it -> not passed.
        const string fen = "4k3/8/8/4p3/3P4/8/8/4K3 w - - 0 1";
        Assert.False(Has(fen, ChunkTagger.PassedPawn, white: true));
    }

    [Fact]
    public void Detects_the_bishop_pair()
    {
        Assert.True(Has("4k3/8/8/8/8/8/8/2B1KB2 w - - 0 1", ChunkTagger.BishopPair, white: true));
    }

    [Fact]
    public void Detects_a_fianchettoed_bishop()
    {
        Assert.True(Has("4k3/8/8/8/8/6P1/6B1/4K3 w - - 0 1", ChunkTagger.Fianchetto, white: true));
    }

    [Fact]
    public void Detects_a_rook_on_an_open_file()
    {
        Assert.True(Has("4k3/8/8/8/8/8/8/R3K3 w - - 0 1", ChunkTagger.RookOpenFile, white: true));
    }

    [Fact]
    public void Detects_a_knight_outpost()
    {
        Assert.True(Has("4k3/8/8/3N4/2P5/8/8/4K3 w - - 0 1", ChunkTagger.KnightOutpost, white: true));
    }

    [Fact]
    public void Detects_king_safety_states()
    {
        Assert.True(Has("4k3/8/8/8/8/8/8/6K1 w - - 0 1", ChunkTagger.CastledKingside, white: true));
        Assert.True(Has("4k3/8/8/8/8/8/8/2K5 w - - 0 1", ChunkTagger.CastledQueenside, white: true));
        Assert.True(Has("4k3/8/8/8/8/8/8/4K3 w - - 0 1", ChunkTagger.KingInCentre, white: true));
    }

    [Fact]
    public void Tags_the_opening_position_sanely()
    {
        // Start position: no isolated/doubled/passed pawns, both kings in the centre.
        const string start = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        var chunks = ChunkTagger.Tag(start);
        Assert.DoesNotContain(chunks, c => c.Name == ChunkTagger.IsolatedPawn);
        Assert.DoesNotContain(chunks, c => c.Name == ChunkTagger.PassedPawn);
        Assert.Contains(chunks, c => c.Name == ChunkTagger.KingInCentre && c.White);
        Assert.Contains(chunks, c => c.Name == ChunkTagger.KingInCentre && !c.White);
    }
}

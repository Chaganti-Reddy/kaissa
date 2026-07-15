using System.Linq;
using Kaissa.Training.Play;
using Xunit;

namespace Kaissa.Training.Tests;

public class PositionCoachTests
{
    private const string Start = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void Every_category_is_present_and_non_empty()
    {
        var e = PositionCoach.Explain(Start);
        Assert.NotEmpty(e.Threats);
        Assert.NotEmpty(e.BestMoves);
        Assert.NotEmpty(e.Plans);
        Assert.NotEmpty(e.PieceRoles);
        Assert.NotEmpty(e.Concepts);
    }

    [Fact]
    public void Start_position_has_no_material_threats_and_king_on_home()
    {
        var e = PositionCoach.Explain(Start);
        Assert.Contains(e.Threats, t => t.Contains("No immediate material threats"));
        Assert.Contains(e.Concepts, c => c.Contains("king is still in the centre"));
    }

    [Fact]
    public void Best_moves_prompt_for_the_engine_when_no_lines_given()
    {
        var e = PositionCoach.Explain(Start);
        Assert.Contains(e.BestMoves, m => m.Contains("Attach the engine"));
    }

    [Fact]
    public void Best_moves_list_the_supplied_engine_lines_in_order()
    {
        var lines = new[]
        {
            new CoachLine("e4", "+0.3"),
            new CoachLine("d4", "+0.2"),
            new CoachLine("Nf3", "+0.1"),
        };
        var e = PositionCoach.Explain(Start, lines);
        Assert.Equal(3, e.BestMoves.Count);
        Assert.StartsWith("1. e4", e.BestMoves[0]);
        Assert.StartsWith("3. Nf3", e.BestMoves[2]);
    }

    [Fact]
    public void An_undefended_attacked_piece_is_reported_as_a_threat()
    {
        // Black to move; white queen on h5 attacks the undefended pawn on f7 (and eyes mate ideas).
        // Simpler, unambiguous: white rook on e7 attacks the black bishop on e2? Use a clear hang:
        // Black knight on d4 is attacked by a white pawn on e3 and undefended. Black to move.
        const string fen = "rnbqkb1r/pppppppp/8/8/3n4/4P3/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var e = PositionCoach.Explain(fen);
        Assert.Contains(e.Threats, t => t.Contains("knight on d4") && t.Contains("under attack"));
    }

    [Fact]
    public void Piece_roles_describe_attacks_and_defences()
    {
        // White bishop on b5 attacks the knight on c6 (Ruy Lopez shape), white to move.
        const string fen = "r1bqkbnr/pppp1ppp/2n5/1B2p3/4P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 0 1";
        var e = PositionCoach.Explain(fen);
        Assert.Contains(e.PieceRoles, r => r.StartsWith("bishop on b5") && r.Contains("attacks"));
    }

    [Fact]
    public void Results_are_capped()
    {
        var e = PositionCoach.Explain(Start);
        Assert.All(new[] { e.Threats, e.BestMoves, e.Plans, e.PieceRoles, e.Concepts },
            list => Assert.True(list.Count <= 6));
    }
}

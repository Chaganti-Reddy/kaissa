using System.Linq;

namespace Kaissa.Training;

/// <summary>
/// A few built-in annotated studies, authored as PGN with comments and parsed by <see cref="PgnStudy"/>.
/// Content is data, not code, so more chapters (or user-imported PGN) can be added without touching the
/// reader. Every mainline here is a legal game from the start; the tests replay them to prove it.
/// </summary>
public static class StudyLibrary
{
    public static IReadOnlyList<string> Pgns { get; } = new[]
    {
        "[Event \"Italian Game: main ideas\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 {Take the centre and open lines for the bishop and queen.} " +
        "e5 2. Nf3 {Develop with a threat - the e5 pawn is now attacked.} " +
        "Nc6 {Defends e5 and develops a piece.} " +
        "3. Bc4 {The Italian bishop, aiming at the f7 square.} " +
        "Bc5 {A natural, symmetrical reply.} " +
        "4. c3 {Prepares d4 to build a broad centre.} Nf6 " +
        "5. d4 {Strike in the centre while ahead in development.} exd4 " +
        "6. cxd4 Bb4+ 7. Nc3 {Block the check and keep developing.} *",

        "[Event \"Ruy Lopez: the Spanish\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 e5 2. Nf3 Nc6 3. Bb5 {The Spanish bishop pressures the knight that guards e5.} " +
        "a6 4. Ba4 Nf6 5. O-O {Castle early and connect the rooks.} " +
        "Be7 6. Re1 {Defend e4 and prepare c3 then d4.} " +
        "b5 7. Bb3 d6 8. c3 O-O {A calm, classical main line.} *",

        "[Event \"Legal's Mate: a classic trap\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 e5 2. Nf3 Nc6 3. Bc4 d6 4. Nc3 " +
        "Bg4 {Pinning the knight - but here the pin is an illusion.} " +
        "5. Nxe5 {The point: a temporary queen sacrifice.} " +
        "Bxd1 6. Bxf7+ Ke7 7. Nd5# {Checkmate - three minor pieces do the work.} *",

        "[Event \"Sicilian Defence: the Najdorf\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 c5 {The Sicilian - Black fights for the centre asymmetrically.} " +
        "2. Nf3 d6 3. d4 cxd4 4. Nxd4 Nf6 5. Nc3 " +
        "a6 {The Najdorf: a waiting move that takes b5 from White's pieces and prepares ...e5 or ...b5.} *",

        "[Event \"French Defence: the Winawer\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 e6 {The French - solid, with a plan to strike the centre with ...d5.} " +
        "2. d4 d5 3. Nc3 " +
        "Bb4 {The Winawer: pin the knight and pressure e4.} " +
        "4. e5 {White claims space and closes the centre.} c5 5. a3 Bxc3+ 6. bxc3 {a sharp, imbalanced middlegame} *",

        "[Event \"Caro-Kann Defence\"]\n[White \"Study\"]\n[Black \"Study\"]\n\n" +
        "1. e4 c6 {The Caro-Kann - like the French, but the light-squared bishop stays free.} " +
        "2. d4 d5 3. Nc3 dxe4 4. Nxe4 " +
        "Bf5 {Developing the bishop outside the pawn chain before ...e6.} " +
        "5. Ng3 Bg6 6. h4 h6 {the main-line tussle over the bishop} *",
    };

    /// <summary>The parsed chapters, ready to step through.</summary>
    public static IReadOnlyList<StudyChapter> Chapters => Pgns.Select(PgnStudy.Parse).ToList();
}

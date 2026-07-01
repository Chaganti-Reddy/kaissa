using System.Diagnostics;
using Kaissa.Chess.Engine;
using Kaissa.Chess.Rules;
using Kaissa.Training;
using Kaissa.Training.Play;

// Phase 1 headless training loop, playable in a console.
//   dotnet run --project src/Kaissa.Training.Cli               # interactive
//   dotnet run --project src/Kaissa.Training.Cli -- --simulate # auto-run a diligent learner

const string progressPath = "kaissa-progress.json";
const string practicePath = "kaissa-practice.json";

var library = ScenarioLibrary.LoadDefault();

// Fold in practice positions saved from the player's own games, so they get scheduled too.
var savedPractice = PlayerPracticeStore.Load(practicePath);
if (savedPractice.Count > 0)
    library.Add(GamePractice.Pattern, savedPractice.Scenarios);

if (args.Contains("--simulate"))
{
    Simulate(library);
    return 0;
}

if (args.Contains("--play"))
    return await PlayGame(progressPath);

RunInteractive(library, progressPath);
return 0;

async Task<int> PlayGame(string savePath)
{
    var enginePath = Environment.GetEnvironmentVariable("KAISSA_STOCKFISH_PATH");
    if (string.IsNullOrWhiteSpace(enginePath) || !File.Exists(enginePath))
    {
        Console.Error.WriteLine("Set KAISSA_STOCKFISH_PATH to a Stockfish binary to play a game.");
        return 1;
    }

    var model = File.Exists(savePath) ? SkillModel.FromJson(File.ReadAllText(savePath)) : new SkillModel();

    await using var engine = UciChessEngine.LaunchProcess(enginePath);
    await engine.HandshakeAsync();
    await engine.NewGameAsync();

    var opponent = new AdaptiveOpponent(engine, TimeSpan.FromMilliseconds(200));
    var game = new GameSession(engine, Side.White, model.RatingEstimate, opponent: opponent);

    Console.WriteLine($"You are White. Opponent Elo: {game.OpponentElo} (your rating {model.RatingEstimate:0}).");
    Console.WriteLine("Enter moves as SAN or UCI. Commands: 'resign', 'quit'.\n");

    while (!game.IsGameOver)
    {
        PrintBoard(game.Fen);

        if (game.SideToMove == Side.White)
        {
            Console.Write("Your move: ");
            var input = Console.ReadLine()?.Trim() ?? "quit";

            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(savePath, model.ToJson());
                Console.WriteLine("Saved. Game abandoned.");
                return 0;
            }

            if (input.Equals("resign", StringComparison.OrdinalIgnoreCase))
            {
                model.RatingEstimate = RatingEstimator.Update(model.RatingEstimate, game.OpponentElo, 0.0);
                Console.WriteLine($"You resigned. New rating: {model.RatingEstimate:0}");
                File.WriteAllText(savePath, model.ToJson());
                return 0;
            }

            if (!game.TryPlayerMove(input))
            {
                Console.WriteLine("Illegal move, try again.\n");
                continue;
            }
        }
        else
        {
            var move = await game.EngineReplyAsync();
            Console.WriteLine($"Bot plays {move}\n");
        }
    }

    PrintBoard(game.Fen);
    game.FinalizeRating();
    model.RatingEstimate = game.PlayerRating;
    Console.WriteLine($"Game over: {game.Result}. Your new rating: {model.RatingEstimate:0}\n");

    // Review the game: the engine grades your moves and your mistakes become practice.
    var analyzer = new GameAnalyzer(engine, depth: 12);
    var assessments = await analyzer.AnalyzeAsync(game.StartFen, game.MoveHistory, game.PlayerSide);
    var mistakes = assessments.Where(a => a.Quality > MoveQuality.Inaccuracy).ToList();

    Console.WriteLine($"Review: {assessments.Count} of your moves analysed, {mistakes.Count} mistake(s)/blunder(s).");
    foreach (var m in mistakes)
        Console.WriteLine($"  move {m.Ply / 2 + 1}: you played {m.PlayedMove}, best was {m.BestMove} ({m.Quality}, -{m.CentipawnLoss}cp)");

    var practice = GamePractice.FromAssessments(assessments);
    if (practice.Count > 0)
    {
        var store = PlayerPracticeStore.Load(practicePath);
        store.AddRange(practice);
        store.Save(practicePath);
        Console.WriteLine($"\n{practice.Count} position(s) saved to your practice queue ({store.Count} total). They'll show up in training.");
    }

    File.WriteAllText(savePath, model.ToJson());
    return 0;
}

static void RunInteractive(ScenarioLibrary library, string progressPath)
{
    var model = File.Exists(progressPath) ? SkillModel.FromJson(File.ReadAllText(progressPath)) : new SkillModel();
    var session = new TrainingSession(library, model, new SystemClock());

    Console.WriteLine("Kaissa — training loop (Phase 1 prototype)");
    Console.WriteLine("Type a move as SAN (e.g. Ra8) or UCI (e.g. a1a8). Commands: 'solution', 'quit'.\n");

    while (true)
    {
        var scenario = session.Next();
        if (scenario is null)
        {
            Console.WriteLine("No content available.");
            return;
        }

        var pattern = library.Describe(scenario.Pattern);
        Console.WriteLine($"Your rating: {model.RatingEstimate:0}");
        Console.WriteLine($"Pattern: {pattern.Name} — {pattern.Description}");
        Console.WriteLine($"{scenario.Prompt}  (puzzle rating {scenario.Rating})");
        PrintBoard(scenario.Fen);

        Console.Write("Your move: ");
        var stopwatch = Stopwatch.StartNew();
        var input = Console.ReadLine()?.Trim() ?? "quit";
        stopwatch.Stop();

        if (input.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllText(progressPath, model.ToJson());
            Console.WriteLine($"Progress saved to {progressPath}. See you next time.");
            return;
        }

        if (input.Equals("solution", StringComparison.OrdinalIgnoreCase))
            input = scenario.Solutions[0];

        var outcome = session.Submit(input, stopwatch.Elapsed);

        Console.WriteLine(outcome.Correct
            ? $"Correct ({outcome.Rating}). Next review in {outcome.IntervalDays} day(s)."
            : $"Not quite. A solution was: {string.Join(", ", scenario.Solutions)}. It will come back soon.");
        Console.WriteLine($"  pattern strength (stability): {outcome.Stability:0.0} days\n");

        PrintProgress(library, model);
        Console.WriteLine(new string('-', 60));
    }
}

static void Simulate(ScenarioLibrary library)
{
    var model = new SkillModel();
    var clock = new ManualClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    var session = new TrainingSession(library, model, clock);

    Console.WriteLine("Simulating a diligent learner (always correct, studying every few days)...\n");

    const int items = 40;
    for (int i = 0; i < items; i++)
    {
        var scenario = session.Next();
        if (scenario is null)
            break;
        session.Submit(scenario.Solutions[0], TimeSpan.FromSeconds(6));
        clock.AdvanceDays(3);
    }

    Console.WriteLine($"After {items} study items over ~{items * 3} days:\n");
    PrintProgress(library, model);
}

static void PrintProgress(ScenarioLibrary library, SkillModel model)
{
    Console.WriteLine($"Player rating estimate: {model.RatingEstimate:0}");
    Console.WriteLine("Progress:");
    foreach (var pattern in library.Patterns)
    {
        var name = library.Describe(pattern).Name;
        if (!model.Has(pattern))
        {
            Console.WriteLine($"  {name,-22} not yet seen");
            continue;
        }

        var card = model.GetOrCreate(pattern);
        var stability = card.State?.Stability ?? 0;
        Console.WriteLine($"  {name,-22} reps {card.Reps,2}  strength {stability,6:0.0}d  lapses {card.Lapses}");
    }
}

static void PrintBoard(string fen)
{
    var placement = fen.Split(' ')[0];
    var ranks = placement.Split('/');

    Console.WriteLine();
    for (int r = 0; r < ranks.Length; r++)
    {
        Console.Write($"  {8 - r} ");
        foreach (var ch in ranks[r])
        {
            if (char.IsDigit(ch))
                Console.Write(new string('.', ch - '0').Replace(".", ". "));
            else
                Console.Write($"{ch} ");
        }
        Console.WriteLine();
    }

    Console.WriteLine("    a b c d e f g h");
    Console.WriteLine();
}

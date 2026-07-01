# Kaissa.Training

The headless training loop. This is where the three cores come together into something that
teaches: rules (`Kaissa.Chess.Rules`) check moves, the scheduler (`Kaissa.Learning`) decides
what to practise and when, and content (`ScenarioLibrary`) supplies positions. No UI and no engine
dependency in the loop itself, so the learning experience can be proven before any 3D work — the
sequencing in the project plan.

## Pieces

- `PatternId` / `Pattern` — a learnable pattern (e.g. "checkmate.back_rank").
- `Scenario` / `ScenarioLibrary` — positions tagged with the pattern they train, loaded from
  embedded JSON content (`Content/scenarios.json`).
- `SkillModel` / `PatternCard` — the player's per-pattern FSRS state, serialisable to JSON.
- `GradeExtractor` — derives an FSRS grade from the move played and how long it took. The player
  is never asked to rate themselves.
- `SessionPlanner` — chooses the next *pattern*: due reviews first, then a new pattern, then the
  weakest.
- `RatingEstimator` — maintains a live estimate of the player's strength on the puzzle-rating
  (Elo) scale, updated from each attempt against the puzzle's own rating.
- `DifficultyController` — chooses which *scenario* of a pattern to serve, aiming just above the
  player's rating (desirable difficulty). The seam where a future ML selector plugs in.
- `TrainingSession` — the loop: `Next()` picks a pattern and a level-matched scenario,
  `Submit(move, thinkingTime)` grades it, updates the schedule, and adjusts the player rating.
- `Play/AdaptiveOpponent` + `Play/GameSession` — play a full game against the engine capped to
  the player's level; the result updates the player's rating. The other half of "play, not study".
- `Play/MoveClassifier` + `Play/GameAnalyzer` — grade each of the player's moves against the
  engine's best (best/good/inaccuracy/mistake/blunder) from the evaluation swing.
- `Play/AttackBoard` + `Play/MotifClassifier` — recognise a move's motif from the board
  (checkmate, fork, winning an undefended piece; pins/skewers fall back to unclassified).
- `Play/GamePractice` — turn the mistakes from a game into practice scenarios (the missed move is
  the solution), routing each to the pattern for its motif so a missed fork trains the *fork*
  pattern. `Play/PlayerPracticeStore` persists them and `ScenarioLibrary.Add` folds them into the
  training library, so a player's own blunders come back as spaced, scheduled practice under the
  right skill. This is where playing and learning meet.

## Using it from a UI (the Unity client)

`Api/KaissaTrainer` is the single entry point a UI drives. It speaks only in plain DTOs
(`TrainingCard`, `AnswerResult`, `ProgressRow`, `BoardView`) — no engine or rules types cross the
boundary — so the Unity client can render and drive the loop without depending on the internals.

```csharp
var trainer = KaissaTrainer.CreateDefault(savedProgressJson);
TrainingCard? card = trainer.NextCard();          // pattern, prompt, board view, ratings
AnswerResult result = trainer.Answer(move, think); // correct?, grade, next review, rating change
string save = trainer.ExportProgress();            // persist however the platform prefers
```

## Try it

```powershell
# Auto-run a diligent learner and print progress
dotnet run --project src/Kaissa.Training.Cli -- --simulate

# Play the loop yourself (SAN or UCI moves; 'solution' reveals, 'quit' saves and exits)
dotnet run --project src/Kaissa.Training.Cli

# Play a full game against the adaptive bot (needs KAISSA_STOCKFISH_PATH set)
dotnet run --project src/Kaissa.Training.Cli -- --play
```

## Content status

v0 content is a small set of engine-verified mate-in-one positions across four patterns — enough
to prove the loop. The `ScenarioSoundnessTests` (run when a Stockfish path is set) assert that the
engine's best move matches each authored solution, so wrong content fails the build. Broader
content (forks, pins, skewers, endgames) will come from importing the openly-licensed Lichess
puzzle database, which is themed and pre-validated, rather than by hand-authoring positions.

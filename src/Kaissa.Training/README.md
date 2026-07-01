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
- `SessionPlanner` — chooses the next pattern: due reviews first, then a new pattern, then the
  weakest. The seam where a future adaptive/ML selector plugs in.
- `TrainingSession` — the loop: `Next()` picks a scenario, `Submit(move, thinkingTime)` grades it
  and updates the schedule.

## Try it

```powershell
# Auto-run a diligent learner and print progress
dotnet run --project src/Kaissa.Training.Cli -- --simulate

# Play the loop yourself (SAN or UCI moves; 'solution' reveals, 'quit' saves and exits)
dotnet run --project src/Kaissa.Training.Cli
```

## Content status

v0 content is a small set of engine-verified mate-in-one positions across four patterns — enough
to prove the loop. The `ScenarioSoundnessTests` (run when a Stockfish path is set) assert that the
engine's best move matches each authored solution, so wrong content fails the build. Broader
content (forks, pins, skewers, endgames) will come from importing the openly-licensed Lichess
puzzle database, which is themed and pre-validated, rather than by hand-authoring positions.

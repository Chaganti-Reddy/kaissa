# Kaissa.Learning

The learning core. Pure C#, no Unity and no engine dependency, so it can be unit tested on its
own and, later, run on a server. This is where the training logic that makes Kaissa different
lives (see `docs/learning-engine.md`).

## Contents

- `FsrsScheduler` — a faithful port of the FSRS-6 scheduling math (initial state, retrievability,
  difficulty update, recall/forget/short-term stability, next interval). It answers: given a
  pattern's memory state, how long since it was last practised, and how the last attempt went,
  what is the new memory state and when should it be practised again?
- `FsrsParameters` — the 21 FSRS-6 weights (published defaults), target retention, and interval
  cap. Weights can later be fitted per player without changing the scheduler.
- `MemoryState`, `Rating`, `ReviewResult` — the small value types the scheduler works with.

In Kaissa a "review" is not a flashcard; the `Rating` is derived from in-game play, not chosen by
the user. The scheduler math is the same regardless of where the grade comes from.

## Scope

This is the FSRS scheduling core only. Anki-style multi-step learning/relearning queues and
interval fuzzing are deliberately left out; Kaissa schedules patterns, not cards, and does not
need them. Parameter fitting from a player's own history is a later phase.

## Verified properties (see tests)

- The interval at 0.9 target retention equals the rounded stability.
- Recall probability after exactly `stability` days equals the target retention.
- Successful, well-spaced reviews grow stability and interval; lapses shrink stability; failing a
  pattern makes it harder than acing it.

## Running

```powershell
dotnet test tests/Kaissa.Learning.Tests
```

# Architecture

## Principles

- Local-first. The core experience runs offline with no backend dependency. Cloud features are additive and come later.
- The learning core is plain C# with no Unity references, so it can be unit-tested on its own and moved to a server later if needed.
- The chess engine sits behind a single interface and a process boundary, so it can be swapped, mocked, or run remotely.
- Content (patterns, scenarios, curricula) is data, not code, loaded through one path. This keeps content addable without releases and leaves room for generated content later.

## Layers

```
Unity client (C#)
  Presentation   3D board, input, animation, UI
  Game layer     match/session flow, scenario running, rules glue
  Learning core  skill model, scheduler, difficulty, grading   (no Unity deps)
  Core services  chess rules, UCI client, persistence
        |
        | UCI over stdio (separate process)
        v
  Stockfish (GPLv3)
```

### Presentation

Rendering and interaction only. Uses the Universal Render Pipeline so one project scales from mobile to desktop. Input (touch, mouse, controller) is behind one abstraction. UI is kept separate from the 3D scene. Presentation renders state and emits intents; it does not own game state.

### Game layer

Drives session flow as a state machine (menu, setup, play, review, schedule update). A session may be a full game, an endgame drill, a tactic, or a run of positions, all expressed as scenario data run by a scenario runner. Translates player intents into legal moves via the rules service and queries the engine via the UCI client.

### Learning core

The training logic. No Unity dependencies. Components:

- `SkillModel` — per-player state, one FSRS card per pattern, plus a live rating estimate.
- `Scheduler` — FSRS wrapper; determines which patterns are due or fading and what to practice.
- `DifficultyController` — sets opponent strength and biases content selection to keep the player in a productive difficulty range.
- `GradeExtractor` — converts in-game behavior into a review grade, so the player is never asked to self-rate.

### Core services

- `ChessRules` — legal move generation, board state, FEN/PGN, check/mate/draw detection. Implementation choice (existing library vs. minimal bitboard) is an open item.
- `UciClient` — spawns and manages the Stockfish process, sends UCI commands, parses evaluations and best moves, and caps strength for adaptive opponents. Asynchronous and cancellable.
- `Persistence` — local storage of the skill model, history, and settings, with a versioned schema. SQLite is the current preference.

### Backend (later)

Deferred. When added, it provides accounts and cross-device sync, and a data pipeline for future machine-learning work. It will be AGPLv3. To keep that path open, the learning core avoids Unity dependencies and structured events are logged from the start.

## Data flow

A move against a computer opponent:

1. The player moves a piece; presentation emits a move intent.
2. The game layer validates it against the rules service and applies it to the board state.
3. The game layer requests a reply from the UCI client at the current adaptive strength.
4. The grade extractor observes the exchange and updates the relevant pattern cards.
5. Presentation animates the result and updates the display.

Session start:

1. The scheduler selects patterns that are due, fading, or newly introduced.
2. The difficulty controller sets opponent strength and the position pool.
3. The scenario runner assembles the session from content data.

## Decisions

| Area              | Choice                                  | State     |
|-------------------|-----------------------------------------|-----------|
| Engine/runtime    | Unity + URP                             | decided   |
| Chess engine      | Stockfish via UCI, separate process     | decided   |
| Scheduler         | FSRS                                    | decided   |
| Move generation   | library vs. own bitboard                | open      |
| Local storage     | SQLite (leaning)                        | tentative |
| Backend           | none initially; AGPLv3 when added       | deferred  |
| License           | GPLv3                                   | decided   |

## Constraints to preserve

- The learning core keeps no Unity dependency.
- All engine access goes through one `IChessEngine` interface.
- Content is loaded through a single path.
- Learning events are recorded as structured records from the start.

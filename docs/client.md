# Client and UI design

This is the plan for the Unity client: the 3D game the player actually touches. The headless core
(learning, rules, engine, play/review) is built and tested; this document describes how the game
is layered on top of it, and how to set up to build it. No client code exists yet — this is the
spec to build from.

## Goals

- A game, not a utility: a real 3D board, motion, sound, and feedback that make practice feel like
  play (see `docs/vision.md`).
- The client is a thin presentation layer. All learning logic stays in the core, reached through
  one façade (`Kaissa.Training.Api.KaissaTrainer`). The client renders and collects input; it does
  not reimplement any rules, scheduling, or grading.
- Cross-platform (desktop first, mobile next) from one project.

## Prerequisites and setup

- **Unity Hub** (free) and a **Unity LTS Editor** (Unity 6 LTS) installed through the Hub.
- Build support modules: Windows/macOS/Linux to start; Android next; iOS requires a Mac.
- **Unity Personal** license (free; appropriate for a free, open-source project).
- The repository's shared libraries are consumed by the Unity project (see compatibility below).

## Runtime compatibility (must resolve before wiring Unity)

Unity's scripting runtime targets **.NET Standard 2.1**, not .NET 9. The shared libraries therefore
need a Unity-consumable build:

- Multi-target the core libraries (`Kaissa.Learning`, `Kaissa.Chess.Rules`, `Kaissa.Training`,
  `Kaissa.Chess.Engine`) to `netstandard2.1;net9.0`. Keep `net9.0` for the CLI and tests; add
  `netstandard2.1` for Unity. Verify no net9-only APIs are used on the netstandard path.
- `Gera.Chess` targets net8.0; confirm it loads under Unity's runtime, or replace it behind the
  existing `ChessGame` seam with a netstandard-compatible move generator if needed.
- The core must remain free of `System.Text.Json` features unavailable in netstandard2.1 (or bundle
  the NuGet package into Unity).

The engine is a separate process over UCI. That works on desktop. On **iOS**, processes cannot be
spawned, so the engine must be embedded as a native plugin (or Stockfish compiled for the platform
and called in-process). This is deferred; the `IChessEngine` seam already isolates it. A
human-like engine (e.g. Maia) can also slot in here later.

## Architecture

```
Unity client (MonoBehaviour / UI)
  Scenes + input + 3D board rendering + animation + audio
        |
        v
  KaissaTrainer facade  (plain DTOs: TrainingCard, AnswerResult, ProgressRow, BoardView)
        |
        v
  Core (learning, rules, engine, play/review)  — unchanged, shared as netstandard2.1
```

- One thin adapter (`KaissaBindings`) in the Unity project holds a `KaissaTrainer` and exposes
  simple methods/events for the UI. Persistence uses `ExportProgress()` saved via Unity's
  `Application.persistentDataPath`.
- Play-vs-bot and post-game review use `Kaissa.Training.Api.KaissaGame`, the async façade that
  mirrors `KaissaTrainer` (start a game, `PlayAsync` a move to get the bot's reply, `ReviewAsync`
  for mistakes + generated practice). Keep it off the mobile path until the engine is embedded.

## Screens (first version)

1. **Home** — start training, play a game, view progress. Minimal, calm.
2. **Training** — the core screen:
   - 3D board rendered from `TrainingCard.Board` (a `BoardView`).
   - Prompt and pattern name shown lightly (not lecture-y).
   - Player makes a move by tapping/dragging a piece; the move is turned into a UCI string and
     passed to `KaissaTrainer.Answer`.
   - Feedback: correct/incorrect with subtle motion and sound; on a miss, reveal the solution.
     Never a "you are being graded" screen — the scheduling is invisible.
   - Advance to `NextCard`.
3. **Play** — a full game vs the adaptive bot (desktop first). After the game, a quiet review
   surfaces mistakes; they are added to practice.
4. **Progress** — an earned, motivating map of pattern mastery from `Progress()`. Not a chore list.

## Board rendering and input

- Board and pieces are 3D assets under the Universal Render Pipeline. One prefab per piece type,
  instantiated from `BoardView.Pieces` (square + FEN letter).
- Input abstraction handles touch, mouse, and controller behind one interface (per
  `docs/architecture.md`). A drag from square A to B yields the UCI move `"a2a4"`; promotion adds a
  piece letter. The move string goes straight to the façade, which validates it.
- Legal-move hints (optional): the client can ask the core for legal moves to highlight; keep this
  a toggle so it does not become a crutch.

## Interaction flow (training)

```
NextCard() -> render board + prompt
   -> player drags a piece -> build UCI move
   -> Answer(move, thinkingTime)
   -> show feedback (correct / reveal), play motion + sound
   -> NextCard()
```

`thinkingTime` is measured on the client from card shown to move made.

## Visual direction

- Distinctive and intentional, not a default template. A physical, tactile board; warm, focused
  lighting; motion that rewards a correct find. Sound design that makes a solved pattern feel good.
- Restraint over clutter — the incumbent apps are busy; this should feel like a calm, premium game.
- Accessibility from the start: colour-blind-safe piece and square cues, scalable text, and a
  reduced-motion option.

## MVP vertical slice (the first build target)

- One polished Training screen driving `KaissaTrainer` end to end on desktop.
- 3D board with real game feel (animation, sound, feedback), rendered from `BoardView`.
- Local persistence of progress via `ExportProgress()`.
- Runs on desktop and one mobile device to prove cross-platform early (engine can be desktop-only
  at this stage; puzzles do not require the engine at runtime).

Breadth (multiple session types, play-vs-bot on mobile, cosmetics) comes after the slice proves the
game feel.

## Open items

- ~~Multi-target the core to netstandard2.1.~~ Done: `Kaissa.Learning`, `Kaissa.Chess.Engine`,
  `Kaissa.Chess.Rules`, and `Kaissa.Training` build for `netstandard2.1;net9.0`. Records/`init`/
  `required` are polyfilled for netstandard2.1 (`build/IsExternalInit.cs`).
- ~~Confirm or replace `Gera.Chess` under Unity's runtime.~~ Done: its MIT source is vendored and
  compiled for the netstandard2.1 build (net9 still uses the package). See
  `src/Kaissa.Chess.Rules/Vendor/Gera/`.
- Engine on mobile: embed Stockfish as a native plugin (and evaluate a human-like engine).
- Art direction and asset pipeline (board, pieces, environment, audio).

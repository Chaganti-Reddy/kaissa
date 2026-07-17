# Roadmap

Where the project stands and what is planned. Everything that makes you a stronger player is free and
always will be. For the current feature set see [`features.md`](features.md); for release notes see
[`../CHANGELOG.md`](../CHANGELOG.md). This is a living document, not a promise of dates.

## Shipped (through v2.0.0)

The core is a single adaptive, spaced training loop that runs through every mode.

- Puzzles: rated adaptive, by theme, by difficulty, weakness-targeted, daily, and a "from your games" feed;
  Puzzle Blitz (3/5-minute, Survival, Streak, combo).
- Play: adaptive Stockfish and human-like Maia opponents on a rating ladder of characters, human move
  timing and clocks, and an optional "decode as you play" position coach.
- Openings: explorer, a browsable book of named openings, and a repertoire drill with per-move recall
  levels and structural chunk tags.
- Endgames: a drill trainer against the engine across the standard instructive positions.
- Analysis and review: a full analysis board, a ten-class post-game review with both-player accuracy, and
  a five-tab plain-language position coach (also on the review board).
- Progression: a six-axis improvement dashboard, per-pattern mastery, achievements, XP and tiers, and a
  daily streak that counts every kind of practice.
- Drills: board vision, coordinates, memory reconstruction, captures and threats, blindfold visualization,
  and Solo Chess.
- Onboarding calibration, settings, and a 2D or 3D board throughout.

## In progress (v3.0.0)

Code complete and tested; client build pending.

- Guess the Move over bundled public-domain master games.
- A weekly study plan generated from your weakest areas.
- Additional endgames (bishop-and-knight mate, queen versus rook).

## Planned (near-term, no new infrastructure)

Buildable in the pure-C# core and the existing client.

- More insights: additional breakdowns on the Stats page from data already collected.
- Accessibility: shape-based (not colour-only) move and check highlights; deeper keyboard navigation.
- Analysis: hover an engine line to preview the resulting position without changing the board.
- Dual puzzle rating: separate untimed and timed ratings, as some trainers do.
- A custom problem-set builder: filter by rating, theme, and your own mistake history.
- Named practice modes generated from your play: time management, intuition, defence, checkmate-pattern
  flashcards, opening-error fixing. Several are the same solve loop with a different filter or timer.
- More guided lessons and studies authored as PGN with commentary.
- A quest and rank curriculum, and an earned-cosmetic layer (cosmetics only; never strength).

## Deferred (needs infrastructure, data, or a platform)

Wanted, but each depends on work beyond the current single-player, offline desktop scope.

- Multiplayer training (puzzle races, head-to-head): needs a server and matchmaking.
- Mobile builds: the engine runs as a UCI subprocess, which iOS forbids; needs a WASM or embedded-library
  path behind the existing `IChessEngine` seam.
- A full opening explorer over a large games database (masters and all players).
- Syzygy endgame tablebases: bundle up to five pieces for offline perfect play; probe the online seven-piece
  API when connected.
- Maia-2 as a single skill-conditioned opponent, run in-core via ONNX rather than as nine weight files.
- Crowd-sourced position analysis: needs a server and conflicts with the offline-first design.
- Localization and community translation.

## Principles

- Free forever; optional spending is cosmetics and convenience only.
- The learning core stays plain C# with no engine or platform dependencies, so it is testable and portable.
- Engines (Stockfish, lc0/Maia) stay behind one `IChessEngine` seam over UCI.
- Live online play is out of scope until there is a server and a player base to justify it.

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
- Studies: annotated master lines authored as PGN, stepped through with a note on each move.
- Named practice drills generated from the shared content: Time Trainer, Intuition, Defender, Advantage
  Capitalization, Blunder Preventer, Checkmate Patterns, Opening Improver.
- Puzzle Storm: a running clock with a difficulty ramp and a combo that adds time.
- Expeditions: campaigns to master an opening by beating a bot from it repeatedly.
- Analysis: hover an engine line to preview it without changing the board; keyboard move entry.
- An earned-cosmetic shop (cosmetics only; coins come from play and never buy strength).
- Accessibility: shape markers on highlighted squares (ring for check, bracket for last move) so
  highlights do not rely on colour alone.
- Chunk tagging: a core routine that labels a position by its structural chunks (pawn structure,
  piece placement, king safety) - groundwork for organising training around chunk recognition.
- A weekly study plan generated from your weakest areas.
- Additional endgames (bishop-and-knight mate, queen versus rook).

## Planned (near-term, no new infrastructure)

Buildable in the pure-C# core and the existing client.

- More insights: additional breakdowns on the Stats page from data already collected.
- Accessibility: deeper keyboard navigation and a screen-reader-friendly linear board mode.
- The bot ladder's shrinking tutor-hint budget wired into Play (the ladder itself is already the
  opponent picker; the per-rung hint budget lives in the core and is not yet surfaced).
- Per-move partial credit on puzzles for a winning-but-not-best move (needs an engine evaluation).
- More guided lessons and studies authored as PGN with commentary.
- Surfacing the spaced-repetition and grading core that is built and tested but not yet on screen:
  per-move scheduling (Chessable-style), chunk-based scheduling keyed to the chunk tagger, and
  partial credit for a winning-but-not-best puzzle move.

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

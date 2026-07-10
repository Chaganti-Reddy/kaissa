# Features

Kaissa's feature set and roadmap. Everything that makes you a stronger player is free — there is no paid tier, and there never will be. The through-line is the adaptive, spaced, implicit-learning loop that runs across every mode rather than being bolted on as a separate library of lessons.

These are standard chess-training and study capabilities; Kaissa's implementations, content, and assets are its own or come from open, public-domain sources (credited in `THIRD-PARTY-NOTICES.md`).

Status: `[x]` built (core + client) · `[~]` core built, client UI pending · `[ ]` planned.

## Puzzles / tactics

- [x] Multi-move puzzles: play the whole solution line; the opponent's replies play automatically — `PuzzleSession`
- [x] Large bundled set — 51,000 positions across all rating bands, with theme tags
- [x] Rated adaptive mode: Glicko-style rating with spaced repetition per pattern — the core loop
- [x] Custom practice by theme and by difficulty band — on-page mode picker
- [x] Daily puzzle — `DailyPuzzle`
- [x] Hint, Solution (played out), Retry, Next, and analyze-this-position
- [x] Theme chips, side-to-move indicator, puzzle and player ratings, timer, streak
- [x] Weakness report: drill the weakest motif, chosen by the skill model — `WeaknessReport`

## Puzzle Blitz

- [x] 3-minute, 5-minute, and Survival modes
- [x] Three strikes ends the run; difficulty ramps each solve — `RushSession`
- [x] Countdown/count-up timer, strike indicator, score, in-run streak, per-mode personal best

## Play

- [x] Play against an adaptive bot (engine capped to your level) — `KaissaGame`
- [x] Bot roster / fixed-strength opponents; bot-speed setting — `BotRoster`
- [x] Time controls (untimed / 3 / 5 / 10 / 15|10), clocks, flag detection; takeback, resign, rematch, flip
- [x] Move list with click-to-review, captured-piece trays, live evaluation bar

## Analysis and review

- [x] Post-game review: mistakes, best reply, severity, centipawn loss, accuracy by phase — `GameAnalyzer`
- [x] Mistakes routed automatically to spaced practice, tagged by motif — `GamePractice`
- [x] Analysis board: step and branch any line, engine evaluation and best line — `AnalysisSession` / `KaissaAnalysis`
- [ ] Natural-language move explanations
- [ ] Cross-game accuracy / insights dashboard

## Openings

- [x] Explorer: play any moves; the position is named (ECO + opening) and its book continuations are listed with the opening each leads to — `OpeningBook`
- [x] Browse and learn: 3,790 named openings grouped by first move, searchable, with mainline stepping
- [x] Repertoire drill: recall your own book moves, scheduled per decision — `RepertoireSession`
- [x] Analyze or open any position in the analysis board
- [ ] Per-move popularity and result statistics (needs a games database, not yet shipped)

## Endgames

- [x] Drill trainer: play instructive endgames against the engine toward a goal (win / draw / promote) with a pass/fail result — `EndgameLibrary` / `DrillEvaluator`
- [x] Categories (Checkmates, King & Pawn, Queen); Hint, Retry, Next, Flip
- [x] Play from any position against the engine — `KaissaGame(fen)`
- [ ] Expand the set (Lucena, Philidor, minor-piece and rook endings, KBN mate)

## Learn

- [~] Pattern library browser: browse each motif, see an example, drill it — `ScenarioLibrary`
- [ ] Structured lessons and courses with guided interactive positions and progress tracking

## Vision and coordinates

- [x] Board-vision trainer (light/dark square drill) — `VisionSession`
- [x] Coordinate trainer (find the named square) — `CoordinateSession`

## Progression and motivation

- [x] Player rating with history, streaks, and accuracy — `KaissaTrainer.GetStats()`
- [x] Per-pattern mastery map derived from the spaced-repetition schedule — the pattern library made visible — `PuzzleProgression`
- [x] Hybrid XP and named tiers (Wood through Grandmaster) alongside the rating — `PuzzleProgression`
- [x] Day streak; patterns-due count — `KaissaStreak`
- [ ] Goals, reminders, and leagues

## Platform and UX

- [x] Onboarding and rating calibration — `CalibrationSession`
- [x] Settings: sound, move input, move hints, auto-queen, bot speed, board flip, board theme, piece style, coordinates, evaluation bar, reset
- [x] Shared board on every screen: drag or click, legal-move dots, highlights, sounds, premove, glide, promotion picker; a 2D or 3D board (toggle)
- [x] Right-click annotations (arrows and square highlights) on both boards
- [x] Consistent nav-rail / board / panel layout; springy hover and press feedback on clickable elements
- [x] Fast content load: opening and puzzle indexes are precomputed offline, parsed once per run, and warmed on a background thread at launch, so pages open near-instantly
- [ ] Cloud sync; mobile (embedded engine for iOS)

## Scope

Kaissa is a single-player, offline trainer. Live online play — matchmaking, head-to-head puzzle races, tournaments, clubs, chat, spectating, and global leaderboards — needs a server and a player population and is deliberately not part of the project. Everything that improves your own play is.

## Principles

- Everything that makes you stronger is free. Optional spending is cosmetics and convenience only.
- The adaptive, spaced, implicit-learning loop runs through every mode, not bolted on.
- Your own games and mistakes feed your training automatically.

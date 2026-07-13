# Features

Kaissa's feature set and roadmap. Everything that makes you a stronger player is free - there is no paid tier, and there never will be. The through-line is the adaptive, spaced, implicit-learning loop that runs across every mode rather than being bolted on as a separate library of lessons.

These are standard chess-training and study capabilities; Kaissa's implementations, content, and assets are its own or come from open, public-domain sources (credited in `THIRD-PARTY-NOTICES.md`).

Status: `[x]` built (core + client) - `[~]` core built, client UI pending - `[ ]` planned.

## Puzzles / tactics

- [x] Multi-move puzzles: play the whole solution line; the opponent's replies play automatically - `PuzzleSession`
- [x] Large bundled set - 52,500 positions spanning the full rating range (about 400 to 3100, i.e. beginner through grandmaster), with theme tags
- [x] Difficulty bands from Beginner to Grandmaster; the hardest puzzles (2600+) are included, not capped
- [x] Rated adaptive mode: Glicko-style rating with spaced repetition per pattern - the core loop
- [x] Custom practice by theme and by difficulty band - on-page mode picker
- [x] Daily puzzle - `DailyPuzzle`
- [x] Hint, Solution (played out), Retry, Next, and analyze-this-position
- [x] Theme chips, side-to-move indicator, puzzle and player ratings, timer, streak
- [x] Weakness report: drill the weakest motif, chosen by the skill model - `WeaknessReport`

## Puzzle Blitz

- [x] 3-minute, 5-minute, and Survival modes
- [x] Three strikes ends the run; difficulty ramps each solve - `RushSession`
- [x] Countdown/count-up timer, strike indicator, score, in-run streak, per-mode personal best

## Play

- [x] Play against an adaptive bot (engine capped to your level) - `KaissaGame`
- [x] Bot roster / fixed-strength opponents; bot-speed setting - `BotRoster`
- [x] Time controls (untimed / 3 / 5 / 10 / 15|10), clocks, flag detection; takeback, resign, rematch, flip
- [x] Move list with click-to-review, captured-piece trays, live evaluation bar

## Analysis and review

- [x] Post-game review: mistakes, best reply, severity, centipawn loss, accuracy by phase - `GameAnalyzer`
- [x] Mistakes routed automatically to spaced practice, tagged by motif - `GamePractice`
- [x] Analysis board: play and branch any line with an evaluation bar, several engine lines at once (click one to play it out), a best-move arrow, an optional threat arrow, a clickable move list, the opening name, load-a-FEN and copy FEN/PGN, and play-vs-computer from the position - `AnalysisSession` / `KaissaAnalysis`
- [ ] Drag-piece board editor (set up a position by hand; FEN paste covers this today)
- [ ] Natural-language move explanations
- [ ] Cross-game accuracy / insights dashboard

## Openings

- [x] Explorer: play any moves; the position is named (ECO + opening) and its book continuations are listed with the opening each leads to - `OpeningBook`
- [x] Browse and learn: 3,790 named openings grouped by first move, searchable, with mainline stepping
- [x] Repertoire drill: recall your own book moves, scheduled per decision - `RepertoireSession`
- [x] Analyze or open any position in the analysis board
- [ ] Per-move popularity and result statistics (needs a games database, not yet shipped)

## Endgames

- [x] Drill trainer: play instructive endgames against the engine toward a goal (win / draw / promote) with a pass/fail result - `EndgameLibrary` / `DrillEvaluator`
- [x] Categories (Checkmates, King & Pawn, Queen); Hint, Retry, Next, Flip
- [x] Play from any position against the engine - `KaissaGame(fen)`
- [ ] Expand the set (Lucena, Philidor, minor-piece and rook endings, KBN mate)

## Learn

- [x] Guided lessons: each teaches a motif in plain terms, then drills it on real positions you solve on the board, with feedback and completion tracking - `LessonLibrary` / `LessonSession`
- [x] Lessons grouped by topic (Tactics, Checkmates); hint, restart, flip, and per-lesson progress saved
- [ ] Longer courses and a wider lesson catalogue

## Vision and coordinates

- [x] Board Vision: timed 30-second light/dark square drill with score, best, and L/D keys - `VisionSession`
- [x] Coordinates: timed 30-second find-the-square drill; pick your side (orientation) and hide the labels to train recall; correct flashes green, a miss reveals the right square - `CoordinateSession`

## Progression and motivation

- [x] Insights dashboard: headline stat tiles, a rating-over-time chart, tier/XP progression, puzzle / Puzzle Blitz / play summaries, and the pattern-mastery map, with a one-tap drill of the weakest motif
- [x] Player rating with history, streaks, and accuracy - `KaissaTrainer.GetStats()`
- [x] Per-pattern mastery map derived from the spaced-repetition schedule - the pattern library made visible - `PuzzleProgression`
- [x] Hybrid XP and named tiers (Wood through Grandmaster) alongside the rating - `PuzzleProgression`
- [x] Day streak; patterns-due count - `KaissaStreak`
- [ ] Goals, reminders, and leagues

## Platform and UX

- [x] Onboarding and rating calibration: an intro screen, a short adaptive run with a progress bar, side-to-move badge and per-answer feedback, and a rating reveal with a level descriptor that seeds your starting rating - `CalibrationSession`
- [x] Settings: a live board preview, clickable board-theme swatches, and a picker for 14 flat-board piece sets, with grouped toggles for sound, move input, move hints, auto-queen, bot speed, board flip, board style (2D/3D), piece style, coordinates, evaluation bar, display mode, and a confirm-guarded progress reset
- [x] Shared board on every screen: drag or click, legal-move dots, highlights, sounds, premove, glide, promotion picker; a 2D or 3D board (toggle)
- [x] Board feel: capture pop, ease-out glide, piece lift on grab, and a solve/win celebration flourish; sampled sound with selectable themes (Sfx, Piano, NES, Futuristic, Classic)
- [x] Right-click annotations (arrows and square highlights) on both boards
- [x] Home dashboard: a snapshot (rating, tier, day streak, patterns due) and cards that launch every mode, with a first-run calibration prompt
- [x] Consistent nav-rail / board / panel layout; springy hover and press feedback on clickable elements; fade transitions between screens
- [x] Fast content load: opening and puzzle indexes are precomputed offline, parsed once per run, and warmed on a background thread at launch, so pages open near-instantly
- [ ] Cloud sync; mobile (embedded engine for iOS)

## Scope

Kaissa is a single-player, offline trainer. Live online play - matchmaking, head-to-head puzzle races, tournaments, clubs, chat, spectating, and global leaderboards - needs a server and a player population and is deliberately not part of the project. Everything that improves your own play is.

## Principles

- Everything that makes you stronger is free. Optional spending is cosmetics and convenience only.
- The adaptive, spaced, implicit-learning loop runs through every mode, not bolted on.
- Your own games and mistakes feed your training automatically.

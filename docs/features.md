# Features

Kaissa's feature set and roadmap. Everything that makes you a stronger player is free - there is no paid tier, and there never will be. The through-line is the adaptive, spaced, implicit-learning loop that runs across every mode rather than being bolted on as a separate library of lessons.

These are standard chess-training and study capabilities; Kaissa's implementations, content, and assets are its own or come from open, public-domain sources (credited in `THIRD-PARTY-NOTICES.md`).

Status: `[x]` built (core + client) - `[~]` core built, client UI pending - `[ ]` planned.

## Puzzles / tactics

- [x] Multi-move puzzles: play the whole solution line; the opponent's replies play automatically - `PuzzleSession`
- [x] Large bundled set - 52,500 positions spanning the full rating range (about 400 to 3100, i.e. beginner through grandmaster), with theme tags
- [x] Difficulty bands from Beginner to Grandmaster; the hardest puzzles (2600+) are included, not capped
- [x] Rated adaptive mode: Glicko-style rating with spaced repetition per pattern - the core loop
- [x] Custom practice by theme, by difficulty band, and a Weakness generator that builds a tailored set from your weakest patterns - on-page mode picker
- [x] From your games: drill the exact positions where you went wrong in your own games, served back as puzzles - `KaissaPractice`
- [x] Daily puzzle - `DailyPuzzle`
- [x] Hint, Solution (played out), Retry, Next, and analyze-this-position
- [x] Theme chips, side-to-move indicator, puzzle and player ratings, timer, streak
- [x] Weakness report: drill the weakest motif, chosen by the skill model - `WeaknessReport`

## Puzzle Blitz

- [x] 3-minute, 5-minute, Survival, and Streak modes (Streak: untimed, one wrong ends it, one skip allowed)
- [x] Three strikes ends the timed runs; difficulty ramps each solve; the timed modes reward consecutive solves with combo bonus time - `RushSession`
- [x] Countdown/count-up timer, strike indicator, score, in-run streak, per-mode personal best

## Puzzle Storm

- [x] A running clock with a difficulty ramp; a combo of consecutive solves tops the clock up at each milestone - `StormScoring`
- [x] A miss costs time and resets the combo, but the run continues (unlike Puzzle Blitz's three strikes); best run and best combo kept

## Drills

- [x] Seven named practice modes generated from the shared puzzle content, each a filter over the same library so they grow with it - `DrillFactory`
- [x] Time Trainer, Intuition, Defender, Advantage Capitalization, Checkmate Patterns, Opening Improver (solve on the board); Blunder Preventer (pick the stronger of two candidate moves)

## Play

- [x] Play against an adaptive bot (engine capped to your level) - `KaissaGame`
- [x] Human-like opponents (Maia): neural-network bots (Maia 1100-1900) that play like a human of that rating, on Leela Chess Zero at one node - `MaiaOpponent`
- [x] Bot ladder: the Stockfish and Maia opponents are characters with a rating and a playing style (Hunter / Guardian / Savage / Observer / Mediator), ordered weakest to strongest; beating one marks it, and the picker tracks how far you have climbed - `BotRoster`
- [x] Human move timing: the bot spends time like a person - quick in book and on recaptures, longer in sharp positions - and in a timed game it spends that time from its own clock and can get into time trouble - `MoveTimeModel`
- [x] Bot-speed setting; time controls (untimed / 3 / 5 / 10 / 15|10), clocks, flag detection; takeback, resign, rematch, flip; keyboard move entry
- [x] Decode as you play: an optional coach panel reads the current position (threats, plans, piece roles, concepts) each move, without ever handing you the move - `PositionCoach`
- [x] Move list with click-to-review, captured-piece trays, live evaluation bar

## Analysis and review

- [x] Post-game review: ten move classes (Brilliant to Blunder, with Book, Great and Miss), accuracy for both players, opening and how far it stayed in book, key turning points, a performance-rating estimate, and per-move commentary - `GameAnalyzer` / `MoveClassifier` / `MoveCommentary`
- [x] Mistakes routed automatically to spaced practice, tagged by motif; drill them back as a "From your games" puzzle feed - `GamePractice`
- [x] Position coach: a five-tab plain-language read - Threats, Best moves, Plans, Piece roles, Concepts - templated from the position and the engine's lines (no cloud, no language model). Also draws the read on the board: green best move, red threats, blue piece roles. Works on the analysis board and while stepping through a game review - `PositionCoach`
- [x] Analysis board: play and branch any line with an evaluation bar, several engine lines at once (click one to play it out), a clickable move list, the opening name, load-a-FEN and copy FEN/PGN, and play-vs-computer from the position - `AnalysisSession` / `KaissaAnalysis`
- [x] Hover an engine line to preview its first moves on the board without playing them; type a move in algebraic or long notation to play it - `MoveEntry`
- [x] Studies: step through an annotated master line move by move with a note on each move; lessons authored as PGN, so more can be added as data - `PgnStudy` / `StudyLibrary`
- [x] Drag-piece board editor (2D): piece palette, click-to-place, eraser, side-to-move and castling toggles, clear/reset, apply to load; FEN paste also works

## Openings

- [x] Explorer: play any moves; the position is named (ECO + opening) and its book continuations are listed with the opening each leads to - `OpeningBook`
- [x] Browse and learn: 3,790 named openings grouped by first move, searchable, with mainline stepping
- [x] Repertoire drill: recall your own book moves, scheduled per decision, each move on a Chessable-style recall ladder (Level 0-8, "next in ...") and tagged with the pawn-structure chunk it belongs to - `RepertoireSession` / `SrLevel`
- [x] Analyze or open any position in the analysis board
- [ ] Per-move popularity and result statistics (needs a games database, not yet shipped)

## Endgames

- [x] Drill trainer: play instructive endgames against the engine toward a goal (win / draw / promote) with a pass/fail result - `EndgameLibrary` / `DrillEvaluator`
- [x] Categories (Checkmates including the bishop-and-knight mate, King & Pawn, Minor Piece, Rook including Lucena, Queen including queen-vs-rook); Hint, Retry, Next, Flip
- [x] Play from any position against the engine - `KaissaGame(fen)`
- [ ] Deeper theory positions (Philidor defence, more rook endings) and DTZ-perfect tablebase play

## Learn

- [x] Guided lessons: each teaches a motif in plain terms, then drills it on real positions you solve on the board, with feedback and completion tracking - `LessonLibrary` / `LessonSession`
- [x] Lessons grouped by topic (Tactics, Checkmates); hint, restart, flip, and per-lesson progress saved
- [ ] Longer courses and a wider lesson catalogue

## Vision, memory and pattern drills

- [x] Board Vision: timed 30-second light/dark square drill with score, best, and L/D keys - `VisionSession`
- [x] Coordinates: timed 30-second find-the-square drill; pick your side and hide the labels to train recall - `CoordinateSession`
- [x] Memory: a position is shown briefly, the board clears, and you rebuild it from memory by stamping pieces; each solve adds a piece and shortens the look - `MemoryController`
- [x] Captures & Threats: a 30-second drill - click every enemy piece the side to move can capture, then submit; trains the instant "what is hanging" read
- [x] Visualization: solve a short tactic while the pieces are faded, fading further each solve (50% to fully blind); the board and its coordinates stay, only the pieces fade - `Board2D.SetPieceOpacity`
- [x] Solo Chess: the single-player capture puzzle - every move must capture, no piece captures more than twice, a king can never be taken, clear the board to one piece; generated to always be solvable - `SoloChess`
- [x] Guess the Move: play a famous out-of-copyright game as one side and predict each move, scored against the master's move - `MasterGames` / `GuessMoveSession`
- [x] Tactics found vs missed: your games are scanned for forks, pins, mates and hanging pieces, and the insights dashboard shows how many you took versus let slip

## Progression and motivation

- [x] Insights dashboard: headline stat tiles, a rating-over-time chart, tier/XP progression, per-mode summaries, move-quality mix, accuracy by phase, tactics found-vs-missed, and the pattern-mastery map, with a one-tap drill of the weakest motif
- [x] Improvement areas: six sides of your play (Tactics, Endgame, Advantage capitalization, Resourcefulness, Time management, Opening) each scored 0-100 from your own games against a rating-indexed baseline, with a plain-language read and a link to the matching drill - `WeaknessDashboard`
- [x] Achievements: milestone badges earned from your own play (first win, climbing the bot ladder, puzzle streaks and centuries, reaching levels in the drills) - `KaissaAchievements`
- [x] Weekly study plan: a short, prioritized list generated from your weakest axes and patterns, worst gap first, each pointing at a drill - `StudyPlan`
- [x] Player rating with history, streaks, and accuracy - `KaissaTrainer.GetStats()`
- [x] Per-pattern mastery map derived from the spaced-repetition schedule - the pattern library made visible - `PuzzleProgression`
- [x] Hybrid XP and named tiers (Wood through Grandmaster) alongside the rating - `PuzzleProgression`
- [x] Daily streak that counts every kind of practice - play, drills, Puzzle Blitz, Solo Chess - not only puzzles - `KaissaStreak`
- [x] Quests and a rank ladder (Pawn to King) driven by your own progress snapshot - `QuestBoard`
- [x] Shop: coins earned from play unlock cosmetic board and piece tints; cosmetics only, never strength or gated training - `CosmeticShop`
- [ ] Goals, reminders, and leagues

## Platform and UX

- [x] Onboarding and rating calibration: an intro screen, a short adaptive run with a progress bar, side-to-move badge and per-answer feedback, and a rating reveal with a level descriptor that seeds your starting rating - `CalibrationSession`
- [x] Settings: a live board preview, clickable board-theme swatches (including a high-contrast, colour-blind-safe theme), and a picker for 14 flat-board piece sets, with grouped toggles for sound, move input, move hints, auto-queen, premove, bot speed, board flip, board style (2D/3D), piece style, coordinates, evaluation bar, display mode, and a confirm-guarded progress reset
- [x] Shared board on every screen: drag or click, legal-move dots, highlights, sounds, premove, glide, promotion picker; a 2D or 3D board (toggle)
- [x] Board feel: capture pop, ease-out glide, piece lift on grab, and a solve/win celebration flourish; sampled sound with selectable themes (Sfx, Piano, NES, Futuristic, Classic)
- [x] Right-click annotations (arrows and square highlights) on both boards
- [x] Home dashboard: a snapshot (rating, tier, day streak, patterns due) and cards that launch every mode, with a first-run calibration prompt
- [x] Consistent nav-rail / board / panel layout; springy hover and press feedback on clickable elements; fade transitions between screens; a branded startup animation
- [x] Fast content load: opening and puzzle indexes are precomputed offline, parsed once per run, and warmed on a background thread at launch, so pages open near-instantly
- [ ] Cloud sync; mobile (embedded engine for iOS)

## Scope

Kaissa is a single-player, offline trainer. Live online play - matchmaking, head-to-head puzzle races, tournaments, clubs, chat, spectating, and global leaderboards - needs a server and a player population and is deliberately not part of the project. Everything that improves your own play is.

## Principles

- Everything that makes you stronger is free. Optional spending is cosmetics and convenience only.
- The adaptive, spaced, implicit-learning loop runs through every mode, not bolted on.
- Your own games and mistakes feed your training automatically.

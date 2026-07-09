# Feature roadmap — parity and edge vs the incumbents

Goal: match or beat the big platforms on everything except live online play (which we deliberately
do not chase — see `docs/vision.md`). Live human-vs-human matchmaking is out of scope; everything
else is fair game, and our differentiator is the adaptive, spaced, implicit-learning core that
threads through all of it.

Legend: ✅ built (core + client) · 🎨 core built, client UI still pending · ⏳ planned · 🚫 out of scope

## Training and puzzles

- ✅ Adaptive spaced puzzle trainer (FSRS; each pattern drilled at its own rating) — *our core edge*
- ✅ Puzzle Blitz (lives, escalating difficulty) — `RushSession`
- ✅ Daily puzzle (deterministic by date, marked done until tomorrow) — `DailyPuzzle`
- ✅ Themed practice — pick any pattern to drill from the menu (Practice) — `ThemedSession`
- 🎨 Puzzle by rating range / custom sets — `ScenarioLibrary.ByRatingRange` + `PuzzleSetSession`
- ✅ "Weakness report" → practice the weakest motif from Stats (uses the skill model; beyond chess.com) — `WeaknessReport`

## Playing

- ✅ Play vs adaptive bot (Stockfish capped to your level) — `KaissaGame`
- ✅ Bot personalities / fixed-Elo opponents, with a bot-speed setting — `BotRoster` + `KaissaGame` fixed Elo
- ✅ Play from a position / play out endgames vs engine — `KaissaGame(fen)` + `EndgameLibrary`
- 🚫 Live online multiplayer (out of scope by design)

## Analysis and improvement

- ✅ Post-game review (mistake list, best reply, severity, centipawn loss, accuracy % by phase) with move-by-move walkthrough — `GameAnalyzer` / `AccuracyModel`
- ✅ Mistakes → spaced practice, tagged by motif — *fusion, beyond chess.com* — `GamePractice`
- ✅ Position/line analysis (engine eval + best line) — `KaissaAnalysis`
- ⏳ Accuracy / insights per game

## Learning content

- ✅ Endgame trainer (K+Q, K+R, K+P opposition, promotion) — pick and play out vs engine — `EndgameLibrary`
- ✅ Opening repertoire trainer — recall your own moves, spaced-repetition scheduled per decision,
  deviations flagged — `OpeningRepertoire` / `RepertoireSession`
- ✅ Pattern library browser (Learn) — browse each motif, see an example position, drill it — `ScenarioLibrary`

## Stats and motivation

- ✅ Progress / mastery map (per-pattern) — `KaissaTrainer.Progress()`
- ✅ Player stats + rating history, streaks, accuracy — `KaissaTrainer.GetStats()`
- ✅ Day streak on the menu; patterns-due count surfaced — `KaissaStreak`
- ⏳ Goals / reminders

## Vision and drills

- ✅ Board-vision trainer (light/dark square drill) — `BoardVision` / `VisionSession`
- ✅ Coordinate trainer (find the named square) — `Coordinates` / `CoordinateSession`

## Platform / meta

- ✅ Onboarding + rating calibration — `CalibrationSession`
- ✅ Settings (sound, move input, move hints, auto-queen, bot speed, board flip, board theme, piece style, coordinates, reset) — `KaissaSettings` + Settings screen
- ✅ Shared board interaction on every screen — drag or click, legal-move dots, highlights, sounds, premove, glide animation, promotion picker
- ✅ F1 controls help overlay, on every screen
- ✅ Launches maximized with a windowed/fullscreen toggle; the UI scales with window size
- ✅ UI Toolkit interface: nav-rail/board/panel layout on every screen; 2D or 3D board (toggle)
- ✅ Analysis board — explore positions, step/branch, engine eval + best line — `AnalysisSession` / `KaissaAnalysis`
- ✅ Cosmetic board themes (eight) + piece style (modelled/procedural); more sets = drop-in. Never pay-to-win.
- ⏳ Cloud sync (v1.5), ML weakness prediction + generated content (v2)
- ⏳ Mobile (embed engine for iOS)

## Principles that keep us ahead

- Everything that makes you stronger is free (money buys cosmetics/convenience only).
- The adaptive/spaced/implicit-learning loop runs *through* every mode, not bolted on.
- Your own games and mistakes feed your training automatically.

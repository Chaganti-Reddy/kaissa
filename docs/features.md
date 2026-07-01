# Feature roadmap — parity and edge vs the incumbents

Goal: match or beat the big platforms on everything except live online play (which we deliberately
do not chase — see `docs/vision.md`). Live human-vs-human matchmaking is out of scope; everything
else is fair game, and our differentiator is the adaptive, spaced, implicit-learning core that
threads through all of it.

Legend: ✅ built (core) · 🎨 needs client UI · ⏳ planned · 🚫 out of scope

## Training and puzzles

- ✅🎨 Adaptive spaced puzzle trainer (FSRS, per-pattern, difficulty-matched) — *our core edge*
- ✅🎨 Puzzle Rush (timed / lives, escalating difficulty) — `RushSession`
- ✅ Daily puzzle (deterministic by date) — `DailyPuzzle`
- ✅🎨 Themed practice (drill one pattern on demand) — `ThemedSession`
- ✅🎨 Puzzle by rating range / custom sets — `ScenarioLibrary.ByRatingRange` + `PuzzleSetSession`
- ✅🎨 "Weakness report" → auto-generated practice set (uses the skill model; beyond chess.com) — `WeaknessReport`

## Playing

- ✅🎨 Play vs adaptive bot (Stockfish capped to your level) — `KaissaGame`
- ✅🎨 Bot personalities / fixed-Elo opponents — `BotRoster` + `KaissaGame` fixed Elo
- ✅🎨 Play from a position / play out endgames vs engine — `KaissaGame(fen)` + `EndgameLibrary`
- 🚫 Live online multiplayer (out of scope by design)

## Analysis and improvement

- ✅🎨 Post-game review (best move, mistake grading) — `GameAnalyzer`
- ✅🎨 Mistakes → spaced practice, tagged by motif — *fusion, beyond chess.com* — `GamePractice`
- ✅🎨 Position/line analysis (engine eval + best line) — `KaissaAnalysis`
- ⏳ Accuracy / insights per game

## Learning content

- ✅🎨 Endgame trainer (K+Q, K+R, K+P opposition, promotion) — play out vs engine — `EndgameLibrary`
- ✅🎨 Opening trainer (learn lines move by move) — `OpeningLibrary` / `OpeningTrainer`
- ⏳ Pattern library browser (see/learn each motif)

## Stats and motivation

- ✅🎨 Progress / mastery map (per-pattern) — `KaissaTrainer.Progress()`
- ✅🎨 Player stats + rating history, streaks, accuracy — `KaissaTrainer.GetStats()`
- ⏳ Goals / daily streak / reminders

## Vision and drills

- ✅🎨 Board-vision trainer (light/dark square drill) — `BoardVision` / `VisionSession`
- ✅🎨 Coordinate trainer (find the named square) — `Coordinates` / `CoordinateSession`

## Platform / meta

- ⏳ Onboarding + rating calibration
- ⏳ Settings (themes, sound, difficulty, accessibility)
- ⏳ Cosmetic piece/board themes (free + optional cosmetics; never pay-to-win)
- ⏳ Cloud sync (v1.5), ML weakness prediction + generated content (v2)
- ⏳ Mobile (embed engine for iOS)

## Principles that keep us ahead

- Everything that makes you stronger is free (money buys cosmetics/convenience only).
- The adaptive/spaced/implicit-learning loop runs *through* every mode, not bolted on.
- Your own games and mistakes feed your training automatically.

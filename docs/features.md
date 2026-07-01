# Feature roadmap — parity and edge vs the incumbents

Goal: match or beat the big platforms on everything except live online play (which we deliberately
do not chase — see `docs/vision.md`). Live human-vs-human matchmaking is out of scope; everything
else is fair game, and our differentiator is the adaptive, spaced, implicit-learning core that
threads through all of it.

Legend: ✅ built (core) · 🎨 needs client UI · ⏳ planned · 🚫 out of scope

## Training and puzzles

- ✅🎨 Adaptive spaced puzzle trainer (FSRS, per-pattern, difficulty-matched) — *our core edge*
- ✅🎨 Puzzle Rush (timed / lives, escalating difficulty) — `RushSession`
- ⏳ Daily puzzle (deterministic by date)
- ⏳ Themed practice (drill one pattern/theme on demand)
- ⏳ Puzzle by rating range / custom sets
- ⏳ "Weakness report" → auto-generated practice set (uses the skill model; beyond chess.com)

## Playing

- ✅🎨 Play vs adaptive bot (Stockfish capped to your level) — `KaissaGame`
- ⏳ Bot personalities / fixed-Elo opponents
- ⏳ Play from a position / scenario, play out endgames vs engine
- 🚫 Live online multiplayer (out of scope by design)

## Analysis and improvement

- ✅🎨 Post-game review (best move, mistake grading) — `GameAnalyzer`
- ✅🎨 Mistakes → spaced practice, tagged by motif — *fusion, beyond chess.com* — `GamePractice`
- ⏳ Full analysis board (free exploration + engine eval)
- ⏳ Accuracy / insights per game

## Learning content

- ⏳ Endgame trainer (Lucena, Philidor, K+P, etc.) — content + play-out mode
- ⏳ Opening trainer (learn lines by spaced repetition, not rote)
- ⏳ Pattern library browser (see/learn each motif)

## Stats and motivation

- ⏳ Progress / mastery map (per-pattern) — `KaissaTrainer.Progress()` exists; needs a screen
- ⏳ Player stats + rating history, streaks, accuracy
- ⏳ Goals / daily streak / reminders

## Vision and drills

- ⏳ Board-vision trainer (name the square, square colour, knight paths)
- ⏳ Coordinate trainer

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

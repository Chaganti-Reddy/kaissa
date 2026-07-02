# Changelog

## v0.1 (alpha) — 2026-07-02

First alpha of Kaissa, a free, open-source 3D chess training game.

Training and improvement:
- Adaptive, spaced puzzle training (FSRS) over 3,750 verified puzzles across 15 patterns
- Puzzle Rush, daily puzzle, themed practice, rating-range / custom sets, weakness report
- Post-game review that turns mistakes into motif-tagged spaced practice; position analysis
- Rating calibration, stats and insights, progress map, board-vision and coordinate drills

Playing:
- Play against an adaptive bot (Stockfish capped to your level) and fixed-Elo personalities
- Play from a position and instructive endgames; openings trainer

Client (desktop):
- Unity/URP app covering every mode, with real Staunton piece models, board themes, HDRI
  reflections, legal-move hints, board orientation, and a settings screen
- Progress persists locally; calibration seeds a starting rating

Foundation:
- Pure-C# core, 130 tests, GPLv3, offline-first; CI on every push

Not in this release: mobile builds, real-time online play (out of scope by design), and the full
UX/art polish pass.

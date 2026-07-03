# Changelog

## v0.1.4 (alpha) — 2026-07-03

- Top the king with a cross finial so it is no longer mistaken for the queen (both models otherwise
  have a crown).
- Keep the window title bar dark, including when the window loses focus, instead of reverting to a
  white inactive bar.
- The installer now behaves like an in-place update when a version is already installed: it skips
  the welcome, directory, and ready pages, keeps the previous install location, and closes a running
  Kaissa automatically. First-time installs are unchanged. The version is also stamped into the
  installer and executable resources.

## v0.1.3 (alpha) — 2026-07-03

- Fix inconsistent, abnormally sized pieces. Every piece now runs through one path that scales it to
  a consistent Staunton height, seats its base on the board, centres it on the square, turns knights
  to face up the board, and applies one clean ivory/obsidian material. Used by all board screens.
- Add an editor helper (Kaissa > preview) that renders the piece set to an image for tuning.

## v0.1.2 (alpha) — 2026-07-03

- Restore the HDRI skybox and reflections in player builds. The panoramic skybox shader is only
  referenced from code, so the build stripped it; it is now registered in Always Included Shaders.
  A small editor helper (Kaissa > Ensure Always-Included Shaders) keeps the list correct.

## v0.1.1 (alpha) — 2026-07-03

- Fix a crash that left the training screen blank in player builds: a code-only skybox shader was
  stripped from the build, so applying the HDRI environment threw. It now falls back to the coded
  lighting when the shader is absent.
- Launch windowed and add a Quit button and Esc-to-quit on the main menu, so the app no longer
  traps you in fullscreen.
- Set the company and product name to Kaissa (fixes the window title and the save-file location)
  and add an application icon.
- Ship a Windows installer (Start-menu and desktop shortcuts, uninstaller) alongside the portable
  zip, and drop the debug-symbol folder from the release.

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

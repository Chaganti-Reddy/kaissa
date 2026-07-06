# Changelog

## v0.1.8 (alpha) — 2026-07-03

- Settings add move input (drag or click / click only) and auto-queen, alongside the existing sound,
  board flip, board theme, and piece style options.
- The board theme now applies to every screen. Play, Training, and Puzzle Rush previously always used
  the brown board; they now honor the chosen theme like the Openings and Endgame screens.

## v0.1.7 (alpha) — 2026-07-03

- Puzzle Rush and Openings now use the same interaction layer as Play and Training: they reject
  illegal moves (snap-back with an error sound) and gain drag-to-move, legal-move dots,
  selected/last-move/check highlights, and distinct sounds; their pieces are normalized too. Every
  move-making screen now shares this interaction (Endgames play through the Play screen).

## v0.1.6 (alpha) — 2026-07-03

- Training no longer accepts illegal moves. It now uses the same interaction layer as Play, which
  validates moves against the position: an illegal move (for example a pawn pushed straight into an
  enemy pawn) snaps back with an error sound instead of being played and graded "wrong." Training also
  gains drag-to-move, legal-move dots, selected/last-move/check highlights, and the distinct sounds.
- Installer upgrades are now near-silent: the shortcut is created without a "Select Additional Tasks"
  prompt, so installing over an existing version shows only the progress and finished pages (no
  welcome, directory, tasks, or ready pages). The Windows UAC prompt and SmartScreen warning are
  operating-system prompts for unsigned apps and are not part of the installer.

## v0.1.5 (alpha) — 2026-07-03

- New board interaction on Play vs bot, built as a reusable layer for all board screens:
  - Drag a piece to move, or click-to-move — both work together.
  - Legal-move dots on empty squares and markers on capturable pieces, shown when a piece is picked
    up; illegal drops snap back.
  - Selected-square, last-move, and check (red king) highlights; a hover highlight while dragging.
  - A promotion picker (queen/rook/bishop/knight), with an auto-queen setting.
  - Distinct sounds for move, capture, castle, check, promotion, and illegal moves.
- Play now normalizes its pieces like the other screens (consistent Staunton sizing and seating).

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

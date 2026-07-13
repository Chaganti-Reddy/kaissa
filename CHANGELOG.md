# Changelog

## v0.2.1 (alpha) - in progress

A page-by-page rebuild and expansion of the training and study screens, each done for both the 2D and 3D board.

- Puzzles: puzzles are now multi-move - you play the whole solution line and the opponent's replies play automatically. The bundled set was regenerated to 52,500 positions spanning the full rating range (about 400 to 3100, beginner through grandmaster) with full theme tags; the hardest puzzles are included rather than capped. The solve page gained hint, solution, retry, next, analyze, a side-to-move indicator, theme chips, puzzle and player ratings, a timer, and an on-page mode picker (rated, by theme, by difficulty from Beginner to Grandmaster).
- A hybrid progression layer was added: XP and named tiers alongside the rating, plus a per-pattern mastery map derived from the spaced-repetition schedule.
- Puzzle Blitz: rebuilt as Puzzle Rush - 3-minute, 5-minute, and Survival modes, a countdown timer, a three-strike indicator, difficulty ramp, and per-mode personal bests.
- Openings: rebuilt into an explorer (play moves; the position is named with its ECO and book continuations, each showing the opening it leads to), a browse/learn mode over 3,790 named openings grouped by first move with search and mainline stepping, and the repertoire drill.
- Endgames: rebuilt into a drill trainer - play instructive endgames against the engine with a goal (win, draw, or promote) and a pass/fail result, grouped by category, with hint, retry, next, flip.
- Board Vision and Coordinates: rebuilt into timed 30-second drills with a score, a per-run best, and start/game-over screens. Board Vision asks light-or-dark (L/D keys); Coordinates shows a square to click, lets you pick your side and hide the labels to train recall, flashes green on a hit and reveals the right square on a miss.
- Stats: rebuilt into an Insights dashboard - headline stat tiles, a rating-over-time chart, tier and XP progression, puzzle / Puzzle Blitz / play summaries, and the per-pattern mastery map, with a one-tap drill of your weakest motif.
- Analysis: rebuilt into a full analysis board - an evaluation bar, several engine lines at once (click one to play it), a best-move arrow and an optional threat arrow, a clickable move list, the opening name, load-a-FEN and copy FEN/PGN, and play-vs-computer from the current position. Works on the 2D or 3D board.
- Learn: rebuilt into a guided lesson trainer. Each lesson explains a motif in plain terms, then has you solve it on a series of real positions with feedback and commentary; lessons are grouped by topic, completion is saved, and there are hint, restart, and flip controls.
- Performance: opening and puzzle indexes are precomputed offline and shipped, parsed once per run, and warmed on a background thread at launch, so pages open near-instantly. The chess engine is now launched once at startup and shared across every screen (a play engine and a full-strength analysis engine), so opening Play, Endgames, or the analysis board no longer spawns a new engine or shows a "starting engine" wait.

## v0.2.0 (alpha) - 2026-07-09

Interface redesign. Every screen was rebuilt in Unity UI Toolkit with a consistent layout: a left navigation rail, a board area, and a right panel - the conventions most chess players already know.

- New flat 2D board (squares, coordinates, legal-move dots, last-move and check highlights, click-to-move). A Board setting switches between the 2D board and the existing 3D board on every playing and training screen.
- New Analysis board: explore any position, step forward and back through your line, branch into variations, and see the engine's evaluation, best move, and line for the current position.
- Home, Play, Puzzles, Openings, Learn, Endgames, Board Vision, Coordinates, Stats, Settings and calibration were all reworked into the new layout.
- Puzzle Rush is now called Puzzle Blitz.
- The window launches maximized, with a windowed/fullscreen toggle in Settings.

## v0.1.23 (alpha) - 2026-07-08

- Openings is now a spaced-repetition repertoire trainer. You recall only your own moves; each is scheduled for review on its own, and a wrong move shows the book move and comes back sooner. This replaces the single-line walkthrough.
- Training adapts per pattern: each motif is drilled at its own difficulty instead of one overall level, so weak motifs get easier positions and strong ones get harder.
- Play vs bot: the post-game review reports your accuracy as a percentage, broken down by opening, middlegame, and endgame.
- Your rating now updates and is saved after a game against the bot, so play and puzzles share one rating. Mistakes from your games are now saved and scheduled as practice - previously the "added to practice" count had no effect.
- Missed double checks and back-rank or smothered mates in your games are recognised and routed to the matching pattern for practice.

## v0.1.22 (alpha) - 2026-07-08

- New Learn screen: a pattern library. Browse every motif, read what it trains, see an example position on the board, then drill it. Replaces the previous Practice picker (this is a superset).
- The app now launches maximized instead of a fixed small window, and Settings has a Display toggle between a maximized window and borderless fullscreen.
- The interface scales with the window size, so screens stay proportional when maximized, in fullscreen, or resized, instead of clustering at a fixed pixel size.

## v0.1.21 (alpha) - 2026-07-08

- Openings: the screen now lists the opening lines and you pick which one to train, instead of always loading the first.
- Custom practice: a new Practice entry on the menu lets you pick any pattern and drill it on demand (themed mode), rather than only the adaptive loop or your weakest motif.
- Press F1 on any screen for a controls cheat-sheet (move, flip, hint, review, resign, and the per-screen keys).

## v0.1.20 (alpha) - 2026-07-07

- The bot's move now glides across the board (with a capture pop and sound) instead of teleporting, so you can actually see what it played.
- New "Bot speed" setting (Fast / Normal / Slow, default Normal) controls how long the bot takes to move, independent of its strength.

## v0.1.19 (alpha) - 2026-07-07

- Post-game review: when a game ends (or you resign), the side panel lists your mistakes - the move played, the engine's best reply, its severity, and the centipawn loss.
- Step through the finished game with <- / -> : each position is shown with the move and any mistake flagged, so you can see exactly where it turned.

## v0.1.18 (alpha) - 2026-07-07

- Four more board themes: Marble, Coral, Ice, Midnight.
- Play's move list now uses standard algebraic notation (e4, Nf3, O-O, exd5) instead of long UCI.
- In Puzzle Rush, using a hint no longer scores or extends the streak (it also doesn't cost a life), matching training - a hint is a nudge, not a free point.

## v0.1.17 (alpha) - 2026-07-07

- Play vs bot shows a move list (numbered moves) and the material balance.
- Press F to flip the board view in Play, Training, and Puzzle Rush.
- New Coordinates on/off setting for the a-h / 1-8 board labels.

## v0.1.16 (alpha) - 2026-07-07

- Rank/file coordinates (a-h, 1-8) now show on every board - Play, Training, and Puzzle Rush previously had none.
- The menu marks the Daily Puzzle as done once you've solved today's.

## v0.1.15 (alpha) - 2026-07-07

- The menu shows your progress at a glance: a day streak and how many patterns are due for review.
- Stats screen now shows your rating trend since you started, your strongest and needs-work patterns, and a Practice button that drills your weakest motif.
- A consecutive-days training streak that counts every day you train.

## v0.1.14 (alpha) - 2026-07-07

- New "Move hints" setting: legal-move dots and the hover preview can be turned off to train recall. State cues (selected square, last move, check) always stay.
- Using a hint in Training now counts as a lapse - no rating credit, and the pattern is resurfaced soon. A hint is a nudge, not a free solve.
- After a correct training solve, the position's motif is explained in one line, turning each solve into a short lesson.

## v0.1.13 (alpha) - 2026-07-07

- Hover a piece to preview its legal moves without clicking.
- Hint (press H) in Training and Puzzle Rush highlights the best move's from-square.
- Takeback (press U) in Play vs bot: undoes your last move and the bot's reply.
- Training shows a session summary on Esc - puzzles solved, accuracy, and rating change - with keep-training / back-to-menu.
- Daily Puzzle on the menu: the day's puzzle, solvable once and marked done until tomorrow.

## v0.1.12 (alpha) - 2026-07-07

- First-run welcome: on first launch the menu shows a one-time prompt to find your level (calibrate) or skip, so the puzzles and the adaptive bot start matched to you instead of a cold default.
- Play vs bot: press N for a new game (re-pick opponent without leaving), and R to resign - a resigned game is still reviewed into practice.
- Snappier interaction feel: quicker piece glide, lower drag lift (less floaty), faster capture pop.
- Calibration (find your level) now uses the same board interaction as every other screen - drag or click with glide and legal-move validation - instead of the old floating-piece input.

## v0.1.11 (alpha) - 2026-07-07

- Play vs bot starts with an opponent picker: Adaptive (matches your rating) or a fixed-strength bot - Rookie (1350), Casual (1500), Club Player (1800), Expert (2100), Master (2500). Stronger bots think longer, which also gives premove a real window. Endgames go straight to the adaptive opponent.

## v0.1.10 (alpha) - 2026-07-07

- Click-to-move no longer lifts the piece off the board. Selecting a piece now just highlights its square (the piece stays put) and the second click glides it to the target; only dragging lifts a piece. Fixes the "floating piece jumps to where I click" feel.
- Fix the board's light squares blowing out to white on the side facing the key light. Tiles are now matte (no mirror hotspot), bloom only catches genuine highlights (higher threshold, lower intensity), and Neutral tonemapping compresses bright values so the board is evenly lit.

## v0.1.9 (alpha) - 2026-07-03

- Pieces glide to their square instead of snapping, and a captured piece pops (scales out) as it is taken. Applies to every board screen.
- Premove on Play vs bot: queue a move during the bot's turn (the squares are highlighted). It plays automatically when it becomes your move, or is discarded if it is no longer legal.

## v0.1.8 (alpha) - 2026-07-03

- Settings add move input (drag or click / click only) and auto-queen, alongside the existing sound, board flip, board theme, and piece style options.
- The board theme now applies to every screen. Play, Training, and Puzzle Rush previously always used the brown board; they now honor the chosen theme like the Openings and Endgame screens.

## v0.1.7 (alpha) - 2026-07-03

- Puzzle Rush and Openings now use the same interaction layer as Play and Training: they reject illegal moves (snap-back with an error sound) and gain drag-to-move, legal-move dots, selected/last-move/check highlights, and distinct sounds; their pieces are normalized too. Every move-making screen now shares this interaction (Endgames play through the Play screen).

## v0.1.6 (alpha) - 2026-07-03

- Training no longer accepts illegal moves. It now uses the same interaction layer as Play, which validates moves against the position: an illegal move (for example a pawn pushed straight into an enemy pawn) snaps back with an error sound instead of being played and graded "wrong." Training also gains drag-to-move, legal-move dots, selected/last-move/check highlights, and the distinct sounds.
- Installer upgrades are now near-silent: the shortcut is created without a "Select Additional Tasks" prompt, so installing over an existing version shows only the progress and finished pages (no welcome, directory, tasks, or ready pages). The Windows UAC prompt and SmartScreen warning are operating-system prompts for unsigned apps and are not part of the installer.

## v0.1.5 (alpha) - 2026-07-03

- New board interaction on Play vs bot, built as a reusable layer for all board screens:
  - Drag a piece to move, or click-to-move - both work together.
  - Legal-move dots on empty squares and markers on capturable pieces, shown when a piece is picked up; illegal drops snap back.
  - Selected-square, last-move, and check (red king) highlights; a hover highlight while dragging.
  - A promotion picker (queen/rook/bishop/knight), with an auto-queen setting.
  - Distinct sounds for move, capture, castle, check, promotion, and illegal moves.
- Play now normalizes its pieces like the other screens (consistent Staunton sizing and seating).

## v0.1.4 (alpha) - 2026-07-03

- Top the king with a cross finial so it is no longer mistaken for the queen (both models otherwise have a crown).
- Keep the window title bar dark, including when the window loses focus, instead of reverting to a white inactive bar.
- The installer now behaves like an in-place update when a version is already installed: it skips the welcome, directory, and ready pages, keeps the previous install location, and closes a running Kaissa automatically. First-time installs are unchanged. The version is also stamped into the installer and executable resources.

## v0.1.3 (alpha) - 2026-07-03

- Fix inconsistent, abnormally sized pieces. Every piece now runs through one path that scales it to a consistent Staunton height, seats its base on the board, centres it on the square, turns knights to face up the board, and applies one clean ivory/obsidian material. Used by all board screens.
- Add an editor helper (Kaissa > preview) that renders the piece set to an image for tuning.

## v0.1.2 (alpha) - 2026-07-03

- Restore the HDRI skybox and reflections in player builds. The panoramic skybox shader is only referenced from code, so the build stripped it; it is now registered in Always Included Shaders. A small editor helper (Kaissa > Ensure Always-Included Shaders) keeps the list correct.

## v0.1.1 (alpha) - 2026-07-03

- Fix a crash that left the training screen blank in player builds: a code-only skybox shader was stripped from the build, so applying the HDRI environment threw. It now falls back to the coded lighting when the shader is absent.
- Launch windowed and add a Quit button and Esc-to-quit on the main menu, so the app no longer traps you in fullscreen.
- Set the company and product name to Kaissa (fixes the window title and the save-file location) and add an application icon.
- Ship a Windows installer (Start-menu and desktop shortcuts, uninstaller) alongside the portable zip, and drop the debug-symbol folder from the release.

## v0.1 (alpha) - 2026-07-02

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
- Unity/URP app covering every mode, with real Staunton piece models, board themes, HDRI reflections, legal-move hints, board orientation, and a settings screen
- Progress persists locally; calibration seeds a starting rating

Foundation:
- Pure-C# core, 130 tests, GPLv3, offline-first; CI on every push

Not in this release: mobile builds, real-time online play (out of scope by design), and the full UX/art polish pass.

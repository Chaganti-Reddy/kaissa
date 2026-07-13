# Mobile plan

Desktop runs Stockfish as a separate process over UCI. Mobile is different, and the engine is the only real obstacle - the rest of the app (Unity + the netstandard2.1 core) already builds for Android and iOS. Everything engine-specific is behind `IChessEngine`, so mobile support is a new implementation of that seam, not a rewrite.

## The engine problem

- **Android** can spawn processes: a Stockfish binary built for the target ABI (arm64-v8a, etc.) can be shipped in the APK, copied to the app's internal storage on first run, made executable, and driven with the existing `ProcessUciTransport`. This is the lower-effort path and reuses almost everything.
- **iOS** does not allow spawning processes. Stockfish must be compiled as a static library and called **in-process**: a small native plugin exposes "send UCI line" / "read UCI line" functions, and a new `IUciTransport` (an in-process transport) bridges to it via P/Invoke. The existing `UciChessEngine` protocol logic sits on top unchanged.

## Plan

1. Add a native build of Stockfish per platform (Android: executable per ABI; iOS: static lib).
2. Android first: reuse `ProcessUciTransport`, pointing at the extracted binary. Fastest to a working mobile build.
3. iOS: add an `InProcessUciTransport` backed by a native plugin wrapping the Stockfish library; `UciChessEngine` uses it exactly like the process transport.
4. Consider a human-like engine (e.g. Maia, a Leela-based net) as an alternative `IChessEngine` for more natural low-Elo play; it slots into the same seam.

The training loop needs no engine at runtime (puzzles are graded offline), so training, rush, vision, and stats work on mobile immediately. Play-vs-bot, endgames, and analysis wait on the embedded engine.

## Interface on mobile

The desktop layout is a three-column shell: a left navigation rail, a centered board, and a right panel (move list, themes, mastery, book moves, and so on). That does not fit a phone in portrait, so the interface reflows by screen size rather than being redrawn. The client is built in Unity UI Toolkit with flexbox layout and size-relative units, and the panel already scales with the window, so the work is adding breakpoints and a few mobile-specific containers, not a second UI.

Breakpoints:

- Phone portrait: a single column. The board sits at the top at full width; the primary controls sit directly beneath it; the side-panel content (move list, themes, mastery, book lines) moves into a collapsible bottom sheet or a small tab strip below the controls. Navigation becomes a bottom tab bar.
- Phone landscape and small tablets: a two-column split - board on one side, panel on the other - without the nav rail, which stays a bottom bar.
- Tablet and desktop: the existing three-column shell.

Navigation: the left rail collapses to a bottom tab bar with the primary destinations (Home, Play, Puzzles, Learn) and a "More" entry that opens the rest (Puzzle Blitz, Openings, Endgames, Board Vision, Coordinates, Analysis, Stats, Settings). This keeps the most-used modes one tap away and matches the platform convention.

Board and input: the board is always square, sized to the smaller of the available width and height so it never overflows. Touch already works - the board uses pointer events, so tap-to-move and drag both function - but tap targets (controls, list rows, the promotion picker) are enlarged to a comfortable minimum, and the promotion picker and other overlays become full-width sheets rather than small centered dialogs. Coordinate labels can be turned off to reclaim space. The flat 2D board is the default on phones; the 3D board remains available but is heavier.

Ergonomics and platform fit: respect the safe area (notches, rounded corners, the home indicator) so nothing is clipped or unreachable; support portrait as the primary orientation with landscape optional; keep text legible with the existing size-relative scaling.

Performance on mobile: default to the 2D board, drop the heavy 3D post-processing and HDRI reflections that the desktop 3D board uses, and cap engine threads (see the engine section) so play and analysis stay responsive without draining the battery.

Status: the input layer, the core, and size-relative scaling already work on a touch screen; the breakpoint layouts (board-first portrait, bottom tab bar, collapsible panel sheets, enlarged targets) are designed here but not yet built. They are a client-only task and do not touch the core or the training loop.

## Licensing note (important for iOS)

Stockfish is GPLv3, and Kaissa is GPLv3. Distributing GPL software through the Apple App Store is contentious: the App Store terms impose usage restrictions that conflict with the GPL (the VLC case is the well-known precedent). Options to resolve before an iOS App Store release:

- Distribute the iOS build outside the App Store where the platform allows it, or
- Reach the engine over the network (a server component, which would be AGPLv3), avoiding shipping GPL binaries in the app, at the cost of requiring connectivity for play/analysis, or
- Ship engine-dependent modes only where licensing is clean; keep the offline training loop everywhere.

Android (and desktop) do not have this conflict. This does not affect the core or the training loop - only the engine-dependent modes on iOS.

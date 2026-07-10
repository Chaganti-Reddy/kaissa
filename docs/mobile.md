# Mobile plan

Desktop runs Stockfish as a separate process over UCI. Mobile is different, and the engine is the only real obstacle — the rest of the app (Unity + the netstandard2.1 core) already builds for Android and iOS. Everything engine-specific is behind `IChessEngine`, so mobile support is a new implementation of that seam, not a rewrite.

## The engine problem

- **Android** can spawn processes: a Stockfish binary built for the target ABI (arm64-v8a, etc.) can be shipped in the APK, copied to the app's internal storage on first run, made executable, and driven with the existing `ProcessUciTransport`. This is the lower-effort path and reuses almost everything.
- **iOS** does not allow spawning processes. Stockfish must be compiled as a static library and called **in-process**: a small native plugin exposes "send UCI line" / "read UCI line" functions, and a new `IUciTransport` (an in-process transport) bridges to it via P/Invoke. The existing `UciChessEngine` protocol logic sits on top unchanged.

## Plan

1. Add a native build of Stockfish per platform (Android: executable per ABI; iOS: static lib).
2. Android first: reuse `ProcessUciTransport`, pointing at the extracted binary. Fastest to a working mobile build.
3. iOS: add an `InProcessUciTransport` backed by a native plugin wrapping the Stockfish library; `UciChessEngine` uses it exactly like the process transport.
4. Consider a human-like engine (e.g. Maia, a Leela-based net) as an alternative `IChessEngine` for more natural low-Elo play; it slots into the same seam.

The training loop needs no engine at runtime (puzzles are graded offline), so training, rush, vision, and stats work on mobile immediately. Play-vs-bot, endgames, and analysis wait on the embedded engine.

## Licensing note (important for iOS)

Stockfish is GPLv3, and Kaissa is GPLv3. Distributing GPL software through the Apple App Store is contentious: the App Store terms impose usage restrictions that conflict with the GPL (the VLC case is the well-known precedent). Options to resolve before an iOS App Store release:

- Distribute the iOS build outside the App Store where the platform allows it, or
- Reach the engine over the network (a server component, which would be AGPLv3), avoiding shipping GPL binaries in the app, at the cost of requiring connectivity for play/analysis, or
- Ship engine-dependent modes only where licensing is clean; keep the offline training loop everywhere.

Android (and desktop) do not have this conflict. This does not affect the core or the training loop — only the engine-dependent modes on iOS.

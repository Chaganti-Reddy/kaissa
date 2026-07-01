# Kaissa.Chess.Engine

The engine integration layer. Everything the rest of the app needs from a chess engine sits
behind `IChessEngine`; the only implementation today is `UciChessEngine`, which speaks the UCI
protocol to Stockfish.

## Design

- `IChessEngine` — the single seam the application depends on (handshake, options, strength,
  search). Nothing else in the codebase talks to an engine directly.
- `UciChessEngine` — UCI protocol logic. Contains no process or platform code, so it is unit
  tested against an in-memory transport.
- `IUciTransport` — carries UCI lines to and from the engine. `ProcessUciTransport` runs the
  engine as a separate child process; tests use a scripted in-memory transport.

Running the engine out-of-process keeps the GPLv3 binary isolated and swappable, allows moving it
to a server later, and avoids native-linking friction on mobile. See `docs/architecture.md`.

## Getting the engine binary

Stockfish is GPLv3 and is not committed here. Fetch it (into the git-ignored `third_party/`):

```powershell
./scripts/fetch-stockfish.ps1
$env:KAISSA_STOCKFISH_PATH = 'D:\Git\chess\third_party\stockfish\stockfish\stockfish-windows-x86-64.exe'
```

## Running

```powershell
# Unit tests (no engine needed) plus integration tests (run when KAISSA_STOCKFISH_PATH is set)
dotnet test

# Drive a real engine end to end
dotnet run --project src/Kaissa.Chess.Engine.Cli -- $env:KAISSA_STOCKFISH_PATH
```

## Status

Spike 1 (docs roadmap, Phase 0). Verified against Stockfish 18: handshake, option discovery,
Elo-limited play, and MultiPV search with evaluations all work over UCI.

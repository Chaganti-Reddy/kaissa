# Kaissa.Chess.Rules

Chess rules: legal move generation, move application, FEN/PGN, and check/mate/draw detection.

`ChessGame` is the only public type callers use. It wraps the MIT-licensed
[Gera.Chess](https://github.com/Geras1mleo/Chess) library so that dependency never leaks into the
rest of the codebase and can be replaced without touching callers. This is the same "hide it
behind our own seam" approach used for the engine.

## Why a library rather than our own move generator

Correct chess rules are deceptively hard (en passant, castling rights, promotion, SAN
disambiguation, threefold repetition, the fifty-move rule, insufficient material). Gera.Chess is
a mature, MIT-licensed implementation that covers all of it, which is the right trade for a solo
project. If profiling or a feature need ever justifies it, `ChessGame` can be reimplemented on a
bitboard engine behind the same API.

## API

```csharp
var game = ChessGame.Start();                 // or ChessGame.FromFen(fen)
IReadOnlyList<string> san = game.LegalMoves(); // "e4", "Nf3", "O-O"
IReadOnlyList<string> uci = game.LegalUciMoves(); // "e2e4", "e7e8q"

game.TryMakeMove("e4");     // accepts SAN or UCI; false if illegal
game.TryMakeMove("e2e4");

game.SideToMove;   // Side.White / Side.Black
game.IsCheck; game.IsCheckmate; game.IsStalemate; game.IsDraw; game.IsGameOver;
game.Result;       // GameResult.Ongoing / WhiteWins / BlackWins / Draw
game.Fen; game.Pgn;
```

UCI move strings bridge directly to the engine layer (`Kaissa.Chess.Engine`): an engine's best
move comes back as UCI and can be applied with `TryMakeMove`.

## Notes

All draw types (insufficient material, threefold repetition, fifty-move) are enabled via the
library's automatic end-game rules, so games detect draws as they occur.

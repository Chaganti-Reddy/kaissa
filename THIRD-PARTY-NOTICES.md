# Third-party notices

Kaissa is GPLv3. It uses the following third-party components. Their licenses are compatible with
that use; each is credited here as required.

## Engine

- **Stockfish** — GPLv3. https://stockfishchess.org
  The chess engine, run as a separate process over UCI. Not committed to this repo; fetched by
  `scripts/fetch-stockfish.ps1`. Any binary distribution must include the corresponding source.

## Libraries

- **Gera.Chess** by Sviatoslav Harasymchuk — MIT. https://github.com/Geras1mleo/Chess
  Chess rules (move generation, FEN/PGN). Used as a NuGet package (net9) and vendored as source for
  the Unity netstandard2.1 build (see `src/Kaissa.Chess.Rules/Vendor/Gera/`).
- **FSRS** (Free Spaced Repetition Scheduler) — MIT. https://github.com/open-spaced-repetition
  The scheduling algorithm; reimplemented in `Kaissa.Learning` from the published spec.

## Content

- **Lichess puzzle database** — CC0 1.0 (public domain). https://database.lichess.org/#puzzles
  Source for the bundled training puzzles (`src/Kaissa.Training/Content/scenarios.json`), imported
  and reshaped by `tools/ContentImporter`.

## Client assets

- **Poly Haven HDRI** (`client/Assets/Resources/env.hdr`) — CC0 1.0. https://polyhaven.com
  Environment lighting and reflections.
- **Inter** by Rasmus Andersson (`client/Assets/Resources/Inter-Regular.ttf`) — SIL Open Font
  License 1.1. https://rsms.me/inter/
  User-interface font.

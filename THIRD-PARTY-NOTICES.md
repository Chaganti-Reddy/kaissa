# Third-party notices

Kaissa is GPLv3. It uses the following third-party components. Their licenses are compatible with that use; each is credited here as required.

## Engine

- **Stockfish** — GPLv3. https://stockfishchess.org The chess engine, run as a separate process over UCI. Not committed to this repo; fetched by `scripts/fetch-stockfish.ps1`. Any binary distribution must include the corresponding source.

## Libraries

- **Gera.Chess** by Sviatoslav Harasymchuk — MIT. https://github.com/Geras1mleo/Chess Chess rules (move generation, FEN/PGN). Used as a NuGet package (net9) and vendored as source for the Unity netstandard2.1 build (see `src/Kaissa.Chess.Rules/Vendor/Gera/`).
- **FSRS** (Free Spaced Repetition Scheduler) — MIT. https://github.com/open-spaced-repetition The scheduling algorithm; reimplemented in `Kaissa.Learning` from the published spec.

## Content

- **Lichess puzzle database** — CC0 1.0 (public domain). https://database.lichess.org/#puzzles Source for the bundled training puzzles (`src/Kaissa.Training/Content/scenarios.json`), imported and reshaped by `tools/ContentImporter`.
- **Lichess chess-openings** — CC0 1.0 (public domain). https://github.com/lichess-org/chess-openings Source for the bundled opening book (`src/Kaissa.Training/Content/openings.json`): ECO codes, names, and lines, converted from SAN to UCI and indexed by `tools/OpeningImporter`.

## Client assets

- **Poly Haven HDRI** (`client/Assets/Resources/env.hdr`) — CC0 1.0. https://polyhaven.com Environment lighting and reflections.
- **Inter** by Rasmus Andersson (`client/Assets/Resources/Inter-Regular.ttf`) — SIL Open Font License 1.1. https://rsms.me/inter/ User-interface font.
- **Staunton chess piece models** by clarkerubber (`client/Assets/Resources/Pieces/*.obj`) — MIT. https://github.com/clarkerubber/Staunton-Pieces Converted from the source STL to OBJ (rotated, scaled, seated) by `tools/StlToObj`.

### 2D piece sets

The flat-board piece art (`client/Assets/Resources/Pieces2D/<set>/*.png`) is rasterized from the SVG sets distributed with Lichess (https://github.com/lichess-org/lila, `public/piece/`). Only sets under licenses compatible with GPLv3 are bundled; Lichess's non-free and non-commercial (CC BY-NC-SA) sets are deliberately excluded. Each set's author and license, as listed in Lichess's `COPYING.md`:

- **cburnett** by Colin M.L. Burnett — GPLv2+.
- **merida** by Armando Hernandez Marroquin — GPLv2+.
- **chessnut** by Alexis Luengas — Apache 2.0. https://github.com/LexLuengas/chessnut-pieces
- **fantasy**, **spatial**, **celtic** by Maurizio Monge — MIT. https://github.com/maurimo/chess-art
- **pixel** by therealqtpi — AGPLv3+.
- **rhosgfx** by RhosGFX — CC0 1.0. https://rhosgfx.itch.io/
- **pirouetti** by pirouetti — AGPLv3+.
- **shapes** by flugsio — CC BY-SA 4.0. https://github.com/flugsio/chess_shapes
- **letter** by usolando — AGPLv3+.
- **kiwen-suwi** by neverRare — CC BY 4.0. https://github.com/neverRare
- **mpchess** by Maxime Chupin — GPLv3+. https://github.com/chupinmaxime
- **mono** by Thibault Duplessis and Colin M.L. Burnett — GPLv2+. Distributed as six single-colour glyphs; recoloured into light and dark variants before rasterizing.

### Sound sets

The board sound sets (`client/Assets/Resources/Sounds/<set>/*.ogg`) are from Lichess (`public/sound/`), by Enigmahack — AGPLv3+. https://github.com/Enigmahack Bundled sets: sfx, piano, nes, futuristic. Only the license-clean sets are included; Lichess's other sound sets (standard, robot, woodland, lisp, etc.) are not redistributable here and are excluded. A few events in each set (Checkmate, Confirmation) are symlinks to another clip within the free sets and were materialized on import; the Error event linked to a non-free set and was dropped in favour of the built-in procedural cue.

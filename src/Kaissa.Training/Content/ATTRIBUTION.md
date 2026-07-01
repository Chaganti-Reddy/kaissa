# Content attribution

The training scenarios in `scenarios.json` are derived from the **Lichess puzzle database**,
which is released into the public domain under **Creative Commons CC0 1.0**.

- Source: https://database.lichess.org/#puzzles
- License: CC0 1.0 Universal (public domain dedication)

Each scenario id (`lichess-<PuzzleId>`) references the original puzzle. The positions are imported
and reshaped by `tools/ContentImporter`: the puzzle's setup move is applied so the scenario begins
at the solver's turn, and the solver's first move is recorded as the accepted solution.

Thanks to Lichess and its community for making this data freely available.

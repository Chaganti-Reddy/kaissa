# Contributing

Thanks for your interest in Kaissa. Contributions are welcome: code, practice content, art, docs, translations, and playtesting.

The project is in an early, pre-code stage, so the most useful contributions right now are discussion and design review rather than large pull requests. Please open an issue before starting significant work.

## Before you start

Read the design documents in [`docs/`](docs/). Changes are expected to fit the direction set there: an adaptive training loop rather than explicit study, a local-first client, a learning core that has no Unity dependency, and the chess engine kept behind a single interface. Proposals that work against these will be discussed rather than merged as-is.

## Licensing of contributions

By contributing you agree that your contribution is licensed under GPLv3 (and AGPLv3 for any server components added later). Contributed art and content must be under a compatible license.

## A constraint that will not change

The project is free, and the features that make a player stronger will not be placed behind a paywall. Contributions that add such gating will not be accepted.

## Where help is most valuable early

- Building the taxonomy of teachable patterns and authoring practice scenarios.
- The Stockfish/UCI, FSRS, and chess-rules prototypes.
- 3D assets (boards, piece sets, environments) under compatible licenses.

## Working agreements (once code exists)

- Keep the learning core as plain C# with no Unity references, so it stays unit-testable.
- Add tests for changes to the learning core and core services.
- Prefer small, reviewable pull requests, each described by an issue.

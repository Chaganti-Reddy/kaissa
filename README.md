# Kaissa

A free, open-source 3D chess training game.

Kaissa is not another place to play chess online. It is a game whose purpose is to make you a
stronger player. It does this by building your chess pattern recognition through adaptive,
spaced practice that is embedded in gameplay rather than presented as lessons to memorize.

The name is a placeholder and may change.

## Status

Alpha. The learning core is complete and tested, and there is a playable Unity client on desktop covering training, puzzle rush, daily puzzle, themed/custom practice, play-vs-bot, endgames, openings, post-game review, stats, board-vision and coordinate drills, and calibration. Visuals are functional and still being polished; a real modelled piece set and full UX pass are pending. See [`docs/`](docs/).

## Build and run

Headless core (no Unity needed) — run the training loop or drive the engine from the terminal:

```
dotnet test Kaissa.sln                                   # run the test suite
dotnet run --project src/Kaissa.Training.Cli -- --simulate   # simulate the learning loop
```

The 3D client (Unity 6 LTS):

```
./scripts/fetch-stockfish.ps1        # get the engine (GPLv3, not committed)
./scripts/build-unity-plugins.ps1    # build the core into the client and stage the engine
```

Then open `client/` in Unity Hub and press Play. See [`client/README.md`](client/README.md) for
details, and [`docs/client.md`](docs/client.md) for the design and how to make it look premium on a
zero budget.

## Why it exists

Getting better at chess usually means explicit study: memorizing opening lines, grinding puzzle sets, reading theory. It works, but it feels like homework and most improvers give up. Research on chess expertise (Chase & Simon and later work on chunking and template theory) shows that strong players mostly *recognize* good moves from a large store of learned patterns rather than calculating from scratch. That pattern store is built through exposure and practice, not memorization. Kaissa treats improvement as a training problem. You play; the system tracks which patterns you
have encountered and how well you retain them, and shapes upcoming positions and opponents to practice the ones that need work, at a difficulty where learning is fastest. There are no flashcards and no "memorize this line" screens.

## How it relates to existing sites

chess.com and Lichess are excellent places to play and study. Lichess in particular shows that a free, open-source chess project can be first-class, and it is the model this project looks up to. Both are primarily 2D play platforms where learning is a separate library of lessons and puzzles. Kaissa is narrower and different: a 3D game built around an adaptive training loop. It does not try to replace either.

## Technology

- Unity (Universal Render Pipeline), targeting Windows/macOS/Linux and mobile from one codebase.
- Stockfish as the chess engine, run as a separate process over the UCI protocol.
- FSRS (Free Spaced Repetition Scheduler) for scheduling practice.
- The learning core is plain C# with no Unity dependencies, so it can be tested on its own.

Details in [`docs/architecture.md`](docs/architecture.md).

## License

GPLv3. See [`LICENSE`](LICENSE). Networked server components, if added later, will be AGPLv3.
The project is free to use and always will be; see [`docs/vision.md`](docs/vision.md) for how it
is meant to stay sustainable without charging for the parts that make you a better player.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md).

## Documentation

- [`docs/vision.md`](docs/vision.md) — goals, audience, scope, sustainability
- [`docs/research.md`](docs/research.md) — the research and prior art the design rests on
- [`docs/architecture.md`](docs/architecture.md) — system structure and data flow
- [`docs/learning-engine.md`](docs/learning-engine.md) — how the training loop works
- [`docs/client.md`](docs/client.md) — the Unity client and UI design, and how to set up to build it
- [`docs/features.md`](docs/features.md) — feature parity map vs the incumbents
- [`docs/mobile.md`](docs/mobile.md) — plan for the engine on Android/iOS
- [`docs/release.md`](docs/release.md) — building a desktop build and cutting a release
- [`CHANGELOG.md`](CHANGELOG.md) — release notes

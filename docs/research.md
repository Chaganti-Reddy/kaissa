# Research and prior art

Background for the design decisions, with sources. This is a working document and will grow as open questions are resolved.

## Chess expertise and pattern recognition

Chase and Simon's *Perception in Chess* (1973) found that strong players reconstruct real game positions far better than weaker players, but their advantage largely disappears for random positions. The explanation is that experts store a large number of meaningful patterns ("chunks") in long-term memory and perceive positions in terms of them. Later work by Gobet and Simon extended this into template theory, which adds larger, schematic structures and connects low-level perception to planning. Estimates of the number of chunks/templates held by a master run into the tens of thousands.

There is ongoing debate about the exact mechanism (for example the "experience recognition" critique of strict chunk boundaries), but the general finding — that expertise rests on a large, acquired store of recognizable patterns — is well established and is the basis for treating improvement as pattern acquisition.

Sources:

- Chase & Simon, *Perception in Chess*, Cognitive Psychology (1973); revisited in <https://pubmed.ncbi.nlm.nih.gov/9709441/>
- Gobet & Simon, expert memory theories: <https://pubmed.ncbi.nlm.nih.gov/9677761/>
- Experience-recognition critique: <https://www.sciencedirect.com/science/article/abs/pii/S0732118X09000403>

## Spaced repetition

Practice is scheduled with a spaced-repetition algorithm. Two options were considered:

- SM-2 (SuperMemo, 1987): simple and long-established, but applies the same scheduling curve to every learner.
- FSRS (Free Spaced Repetition Scheduler): fits a model to review history and predicts recall per item. Published benchmarks report fewer reviews than SM-2 for the same retention, and better recall prediction. It is MIT-licensed with implementations in several languages including C#. Anki adopted it as the default in late 2023.

FSRS was chosen. Note that in Kaissa an item is a chess pattern, not a flashcard, and the review grade is derived from in-game performance rather than a self-rating.

Sources:

- FSRS project and benchmarks: <https://github.com/open-spaced-repetition/fsrs-optimizer>
- Comparison with SM-2: <https://deepwiki.com/open-spaced-repetition/fsrs-optimizer/7.3-comparison-with-sm-2>

## Chess engine and licensing

Stockfish is the strongest available open-source engine and is licensed under GPLv3. Because Kaissa is itself GPLv3, there is no licensing conflict; the two are compatible. The one hard requirement is that any binary distribution must include the corresponding source (or a pointer to the exact commit built) and the license text. The ChessBase dispute (2021, settled) is the cautionary case, and it concerned a proprietary product shipping Stockfish without meeting these obligations.

Stockfish is run as a separate process over the UCI protocol. With an open-source project the reason is no longer legal isolation; it is that a process boundary keeps the engine swappable and mockable, allows moving it to a server later, and avoids native-linking friction on mobile. Bundling Stockfish on mobile is feasible; several iOS and Android apps do so.

Sources:

- Stockfish license: <https://stockfishchess.org/about/>
- ChessBase case analysis: <https://fossa.com/blog/stockfish-vs-chessbase-gpl-v3/>

## Existing products

chess.com is a large, mature commercial platform with a broad feature set covering online play, content, and study, and a very large user base. Its learning tools are delivered mainly as structured lessons and puzzle sets.

Lichess is a free, open-source, donation-funded platform that is widely respected and is a direct inspiration for this project. It focuses on fast, lightweight 2D play and study.

Both are primarily play-and-study platforms. Kaissa's focus is narrower and different: a 3D game built around an adaptive, per-player training loop. It is not intended to compete with either on online play, and it benefits from the standards they have set.

## Open questions

- Choice of C# FSRS implementation versus porting the reference core.
- Legal move generation and FEN/PGN handling: use an existing C# library (for example ChessDotNet or the Gera chess library) or write a minimal bitboard implementation.
- Building the taxonomy of teachable patterns for the first release.
- Mobile Stockfish build pipeline and store requirements.

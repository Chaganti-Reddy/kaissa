# The learning engine

This describes how practice is chosen, delivered, and graded. It is the part of the project that justifies its existence, so the design is written out in some detail.

## Unit of learning: the pattern

The system does not track "openings" or "lessons". It tracks patterns - the smallest reusable units a strong player recognizes. Examples:

- Tactical motifs: fork, pin, skewer, discovered attack, back-rank mate, deflection.
- Positional motifs: isolated queen pawn play, knight outposts, minority attack, bad bishops.
- Endgame patterns: Lucena, Philidor, opposition, key squares, king-and-pawn races.
- Opening ideas rather than lines: control the center, develop before attacking, castle early.

Each pattern is one item in the spaced-repetition scheduler, with its own memory state. The first release targets a curated set of a few hundred patterns; the initial taxonomy is an open task.

## The loop

For each pattern:

- Exposure: the player meets a position where the pattern is the right idea, inside a game, scenario, or run. They may or may not find it.
- Retrieval: later, a different position needing the same pattern appears. Finding it counts as a successful retrieval and strengthens the pattern. Varying the surface while keeping the underlying idea encourages transfer rather than memorization of specific positions.
- Spacing: the scheduler sets when the pattern should next be practiced, per player. Patterns drifting toward being forgotten are brought back into upcoming sessions.

None of this is presented as study. There are no flashcards and no explicit "memorize this" prompts.

## Implicit grading

Spaced-repetition systems normally ask the learner to rate each item. Kaissa derives the grade from play instead:

- Whether the player made the pattern's move, or a top engine move sharing the same idea.
- Time taken relative to the player's own baseline, as a proxy for fluency.
- Whether the move was found unaided or after a hint.
- Whether the resulting advantage was converted.

These signals map to a review grade for the scheduler. The player simply plays.

## Adaptive difficulty

Learning is fastest when the player succeeds often but not always. Two controls maintain that:

- Opponent strength, by capping Stockfish (via UCI strength limiting, depth, or added noise) to sit near the player's level and adjust to recent results.
- Content selection, biased toward patterns the player is currently learning or is due to revisit, and away from patterns already mastered or far beyond current level.

Both draw on the skill model. A live rating estimate (Glicko-2 style) tracks overall level from results.

## Data model (sketch)

Illustrative, not final. Plain C#, no Unity.

```csharp
readonly record struct PatternId(string Value);   // e.g. "tactic.fork.knight"

class PatternCard {                 // one per (player, pattern)
    PatternId Id;
    double Stability;               // scheduler memory-strength state
    double Difficulty;              // scheduler difficulty state
    DateTime DueUtc;
    int Reps;
    int Lapses;
    double MasteryEstimate;         // 0..1, for selection and UI
}

class SkillModel {
    Dictionary<PatternId, PatternCard> Cards;
    double RatingEstimate;
    IEnumerable<PatternId> DueOrFading(DateTime now);
    IEnumerable<PatternId> Frontier();   // partially learned, best to practice next
}

interface IScheduler {
    ReviewOutcome Grade(PatternCard card, Grade grade, DateTime now);
}
```

## Tagging positions with patterns

The system needs to know which patterns a position exercises.

- Authored content carries pattern tags in its data.
- In live games, patterns are detected during play, starting with simple heuristics and engine evaluation swings (for example, a move that wins material via a fork), and deepening over time.
- Later, a model may tag positions and generate new practice positions for a target pattern. This is why content is loaded as data through one path.

## Rollout

- First release: the scheduler, a few hundred patterns, implicit grading from authored scenarios and basic live tactic detection, adaptive opponent strength, and frontier-based selection.
- Later: cross-device sync and richer live detection.
- Further out: models for weakness prediction and generated practice content.

## Evaluation

The design should be tested against its own claim. The intended experiment compares improvement over several weeks between players using Kaissa and players spending equivalent time on conventional puzzle practice, measured by puzzle rating and game results, along with retention and engagement. The system logs the structured data needed for this from the start.

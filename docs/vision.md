# Vision

## The problem

People who want to improve at chess are told to study: memorize openings, drill tactics, read
theory. This is effective but it feels like work, retention is poor when it is done passively,
and most improvers lose motivation. The main tools are strong play platforms with learning
features attached as a library of lessons and puzzles the player has to consciously work through.

## The idea

Chess strength is largely perceptual. Chase and Simon's work in the 1970s, and later chunking
and template theories, describe how strong players hold a large number of learned board patterns
in memory and recognize good moves through them rather than calculating everything. That store of
patterns is acquired through practice and exposure over time.

Kaissa is built on the position that this acquisition can be made faster and less tedious by
treating it as a training problem inside a game, rather than a study problem outside one. The
player plays; the system decides what to practice and how hard to make it, based on what the
player has seen and how well they retain it.

## Scope

In scope for the first versions:

- Single-player play against adaptive computer opponents.
- Authored practice scenarios (tactics, endgames, positional motifs).
- An adaptive training loop that selects content and opponent strength per player.
- 3D presentation on desktop and mobile.

Not in scope initially:

- Online matchmaking, ratings ladders, or real-time multiplayer. These are what the existing
  platforms do well, and competing there is not the point.
- Opening databases or analysis tooling aimed at titled players.

## Audience

The first audience is adult improvers, roughly 600–1600 rating, who have tried conventional study
and did not stick with it. Later work may address younger players and coaches (structured
curricula) and stronger players (deeper pattern libraries).

## Sustainability

Kaissa is free and open-source under GPLv3, and the parts that make a player stronger will never
be paywalled. The project still needs to be sustainable, and the intended model follows
donation-funded open projects such as Lichess:

- Voluntary donations and recurring patronage.
- Optional cosmetic content (board and piece themes, environments) that does not affect play or
  learning.
- Optional paid hosting for cloud sync, where the same functionality remains available offline
  and self-hosted for free.

The project is not built to be sold or to enclose its own commons.

## What success looks like

A player who uses Kaissa for a short daily session should improve at a measurable rate — by
puzzle rating and by real-game results — at least as fast as they would with equivalent time
spent on conventional puzzle practice, and should be more likely to keep doing it. Demonstrating
that is the point of the project; the design should be instrumented to test it (see
[`learning-engine.md`](learning-engine.md)).

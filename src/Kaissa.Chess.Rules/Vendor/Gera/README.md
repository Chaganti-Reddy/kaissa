# Vendored: Gera.Chess

This folder contains the source of [Gera.Chess](https://github.com/Geras1mleo/Chess) by
Sviatoslav Harasymchuk, licensed under the MIT License (see `LICENSE.md` here).

## Why it is vendored

The `Gera.Chess` NuGet package targets .NET 8 only, which Unity's .NET Standard 2.1 runtime cannot
consume. For the `netstandard2.1` build of `Kaissa.Chess.Rules` (the Unity build) we compile this
source directly; the `net9.0` build (CLI and tests) still uses the NuGet package. Both are the same
version, kept behind our own `ChessGame` wrapper.

## Local patches

Four call sites were adjusted for .NET Standard 2.1 compatibility (no behaviour change):

- `Types/Move.cs`, `Builders/FenBoardBuilder.cs`, `Builders/SanBuilder.cs`: iterate `Match.Groups`
  directly instead of `GroupCollection.Values` (that property is not on netstandard2.1).
- `Builders/SanBuilder.cs`: call `Trim` on a `ReadOnlySpan<char>` rather than a `Span<char>`.

Re-vendoring from upstream means re-applying these four edits.

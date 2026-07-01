# Kaissa client (Unity)

The 3D game client. It is a thin presentation layer over the shared core: it renders positions and
collects input, and drives everything through the `KaissaTrainer` / `KaissaGame` façades. No
learning logic lives here.

- Engine: Unity 6 LTS, Universal Render Pipeline.
- Runtime: .NET Standard 2.1 (the core is multi-targeted for this).

## Opening the project

1. Build the core into the client's plugin folder:

   ```powershell
   ./scripts/build-unity-plugins.ps1
   ```

   This publishes the shared libraries for netstandard2.1 and copies them (plus the JSON/Channels
   dependencies Unity does not ship) into `Assets/Plugins/Kaissa`.

2. Open `client/` in Unity Hub (Unity 6 LTS).

## Current state

MVP vertical slice: `Assets/Scripts/KaissaBoardController.cs` loads the bundled puzzles through
`KaissaTrainer`, renders the position with placeholder primitives, and lets you play a move
(click a piece, then a target square). The move is graded, the rating updates, and the next card is
dealt. Real piece art, on-screen UI, and the play/review screens come next (see `docs/client.md`).

## Notes

- Unity-generated folders (`Library/`, `Temp/`, `Logs/`, generated `.csproj`/`.sln`) are git-ignored.
- The core DLLs under `Assets/Plugins/Kaissa` are committed so the project opens without a build
  step; re-run `build-unity-plugins.ps1` after changing the core.

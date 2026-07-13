# Building and releasing

## Desktop build (Unity)

1. Fetch the engine and stage the core into the client:
   ```powershell
   ./scripts/fetch-stockfish.ps1
   ./scripts/build-unity-plugins.ps1
   ```
2. Open `client/` in Unity 6 LTS.
3. **File -> Build Profiles -> Scene List** must contain, with `Menu` first: `Menu, SampleScene, Play, Rush, Stats, Endgame, Opening, Vision, Coordinate, Calibrate, Settings`.
4. Select the **Windows** platform -> **Build** -> choose an output folder.
5. The build bundles Stockfish (StreamingAssets), the puzzle content, and piece models, so it runs fully offline. Distribute the output folder (or zip it).

macOS/Linux builds work the same from their platforms; mobile is not ready yet (see [`mobile.md`](mobile.md)).

## Publishing the repository

- `LICENSE` (GPLv3), `THIRD-PARTY-NOTICES.md`, `README.md`, and `CONTRIBUTING.md` are present and accurate.
- Push to a public GitHub repository; CI (`.github/workflows/ci.yml`) runs the tests on every push.
- Tag the release:
  ```
  git tag v0.1
  git push --tags
  ```

## Automated releases (self-hosted runner)

Unity removed manual activation of free Personal licenses, so hosted CI runners cannot activate one. The free workaround is a **self-hosted GitHub Actions runner** on a machine where the Unity editor is already installed and activated (your dev PC). `.github/workflows/release.yml` runs there on a version tag: it stages the engine + core DLLs, builds the Windows player with the local editor, and attaches the zip to a GitHub Release.

One-time setup:

1. Install the runner: repo **Settings -> Actions -> Runners -> New self-hosted runner -> Windows**, then run the shown `config`/`run` commands on your PC (in any folder). Optionally install it as a service (`svc.cmd install`, `svc.cmd start`) so it is always available.
2. The runner machine needs: the Unity editor (activated), the .NET SDK, and PowerShell. Confirm the editor path in `release.yml` (`env.UNITY`) matches your installed version.
3. Push a tag: `git tag v0.1 && git push origin v0.1`. The build runs on your PC and publishes the release.

Alternatives if you don't want a self-hosted runner: Unity Pro with a serial (game-ci on hosted runners), Unity Build Automation, or just build locally and upload the zip (see above).

## Release checklist

- [ ] `dotnet test Kaissa.sln` green (also enforced by CI)
- [ ] Client builds and runs on desktop, all scenes in Build Profiles
- [ ] README, CHANGELOG, and third-party notices accurate
- [ ] A desktop build zipped and attached to the tagged release

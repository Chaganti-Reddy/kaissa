# Building and releasing

## Desktop build (Unity)

1. Fetch the engine and stage the core into the client:
   ```powershell
   ./scripts/fetch-stockfish.ps1
   ./scripts/build-unity-plugins.ps1
   ```
2. Open `client/` in Unity 6 LTS.
3. **File → Build Profiles → Scene List** must contain, with `Menu` first:
   `Menu, SampleScene, Play, Rush, Stats, Endgame, Opening, Vision, Coordinate, Calibrate, Settings`.
4. Select the **Windows** platform → **Build** → choose an output folder.
5. The build bundles Stockfish (StreamingAssets), the puzzle content, and piece models, so it runs
   fully offline. Distribute the output folder (or zip it).

macOS/Linux builds work the same from their platforms; mobile is not ready yet (see
[`mobile.md`](mobile.md)).

## Publishing the repository

- `LICENSE` (GPLv3), `THIRD-PARTY-NOTICES.md`, `README.md`, and `CONTRIBUTING.md` are present and
  accurate.
- Push to a public GitHub repository; CI (`.github/workflows/ci.yml`) runs the tests on every push.
- Tag the release:
  ```
  git tag v0.1
  git push --tags
  ```

## Automated releases (CI)

`.github/workflows/release.yml` builds the Windows app and attaches it to a GitHub Release when a
version tag is pushed. It uses [game-ci](https://game.ci) and needs a Unity license stored as a
repository secret (one-time):

1. Generate an activation request file, then convert it to a license:
   - Use the `game-ci/unity-request-activation-file` action once (or run the editor with
     `-createManualActivationFile`), download the `.alf`.
   - Upload the `.alf` at <https://license.unity3d.com/manual>, download the resulting `.ulf`.
2. In the GitHub repo: **Settings → Secrets and variables → Actions → New secret**:
   - `UNITY_LICENSE` = the full contents of the `.ulf` file (Unity Personal is free and enough).
3. Push a tag: `git tag v0.1 && git push origin v0.1`. The workflow builds and publishes the zip.

Notes: game-ci must have an editor image for this project's Unity version (`6000.5.1f1`); the
engine is fetched during the build (not committed). The first release can also be done manually
(build locally, upload the zip) while the license secret is set up.

## Release checklist

- [ ] `dotnet test Kaissa.sln` green (also enforced by CI)
- [ ] Client builds and runs on desktop, all scenes in Build Profiles
- [ ] README, CHANGELOG, and third-party notices accurate
- [ ] A desktop build zipped and attached to the tagged release

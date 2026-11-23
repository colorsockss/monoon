# BRUTAL / optimisation / editor-structure phase — rules shared by every implementation agent

Repository: /Users/paare/codex_project/brutal_engine (MoNoon 2D engine, C#/.NET 10, Avalonia 11.3 editor, SDL3 player via BRUTAL).
You run in your own git worktree. Check `git log -1` first: your branch must contain `b524375` (wip/11-integration = chapter 11 + main docs). If
`git merge-base --is-ancestor b524375 HEAD` fails and you have changed nothing yet, run `git reset --hard b524375`.

## Read first
- `AGENTS.md` (project rules — they override defaults), `docs/brutal.md`, the coordinator audit notes named in your brief,
  `docs/manual/build.md` (local regression), and the manual page(s) for the area you touch.

## Hard rules
- Runtime boundary: Engine.Core / Engine.Desktop / Engine.Player and game scripts never reference the editor or build tools.
  Release and Native AOT outputs contain no SkiaSharp, Avalonia, Engine.Editor or Engine.Authoring files. Editor-only code
  (importers, gizmos, Inspector) stays out of game builds.
- Shipped games draw with BRUTAL's SDL3 renderer. SkiaSharp only in the editor/build tools for previews and pre-baked sheets.
- BRUTAL first (user's standing rule): if what you build overlaps a feature in `docs/brutal.md`, use the BRUTAL package, even
  when that replaces an existing MoNoon implementation and means rebuilding samples and baselines. Downloading `Ahwoo.BRUTAL.*`
  packages in the same version series as the existing `Ahwoo.BRUTAL.Sdl` reference is approved. Any other download (other
  NuGet packages, fonts, tools, installers) is NOT approved: stop and say so in your report. If you deliberately do not use
  BRUTAL for something it covers, write the reason in the results README.
- Editor UI: reuse the existing grid columns, spacing and design tokens (`engine/src/Engine.Editor/EditorStyles.axaml`,
  `EditorTokens.cs`, icons in `EditorIcons.axaml`). Tool windows are `EditorPanel`s hosted by `DockHost`. Labels, inputs and
  buttons stay on the existing baselines at 1280/1600/1920px and with long names; selection/drag highlight never shifts
  neighbours.
- Do not edit `docs/roadmap.md`. Do not touch `.test-env/` and do not start the Windows/Linux VMs (the coordinator does x64
  later; Windows is deferred entirely until the user has a Windows machine — building win-x64 outputs on this Mac is fine).
  Never push. Never commit on main or on wip/11-integration.
- Timing benchmarks: a QEMU Linux guest may be running on this Mac (`pgrep -f qemu-system`). Alternate baseline/candidate
  runs so load hits both equally, record in the results whether qemu was running, and do not claim a win unless the medians
  differ by more than the combined ranges. Determinism gates (exact hashes/counts) must match regardless of load.
- Never run two `dotnet build`s in the same checkout at once (other agents work in parallel in other worktrees; that is fine).

## Build and test (macOS arm64)
- `engine/scripts/build-players.sh`, `./monoon test`, `engine/scripts/build-editor-mac.sh`.
- Probe builds: `dotnet build engine/tests/<Harness> -c Release -m:1 -p:UsedAvaloniaProducts=`.
- BEFORE the first editor build, copy
  `/Users/paare/codex_project/brutal_engine/engine/.artifacts/native/osx-arm64/libSDL3.dylib` to the same relative path in your
  worktree. Without it `build-scene-sdl-mac.sh` downloads the SDL3 source tarball from GitHub, which is not approved.
- Every command that opens an editor or player window MUST be wrapped:
  `sh /private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/gui-lock.sh <command...>`
  It serialises GUI runs across agents and keeps the display awake. `Avalonia.Native was not able to start the RenderTimer
  ... -6661` means the display is asleep/locked: report it, do not work around it in code. If the lock dir is older than
  60 minutes and no MoNoon/MoNoonPlayer process exists, you may remove it.
- Harness pattern (copy an existing one, e.g. `engine/tests/KeyRoom.Rebinding` or `engine/tests/KeyRoom.PrefabStructure`):
  `engine/tests/<Sample>.<Feature>/` with `verify.py` (prepare/play for Release & Native AOT), `editor.py` + a StartupHook
  probe project for Scene Play, and a README. Results go in `engine/tests/results/<feature>/README.md`, written in Korean,
  with OS/arch, target and build type, first exception, reproduction input, and logs or captures.
- Each feature is checked in Scene Play, Release and Native AOT on this Mac unless the roadmap row says otherwise, plus the
  forbidden-files check on the build outputs. Existing regressions you could affect must still pass (`./monoon test` always).

## Finish
- Update the relevant `docs/manual/*.md` page (Korean, same style as the surrounding text).
- Commit everything on your worktree branch: `git add -A && git commit -m "wip: <package> <short>"` (never on main, never push).
- Final report (Korean, concise): files changed, decisions (especially BRUTAL use and anything skipped), a pass/fail table per
  check and build type, first exception of any failure, and what was not verified.

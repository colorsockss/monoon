---
name: run-harness
description: Run one or more MoNoon test harnesses under engine/tests (Scene Play probe, Release or Native AOT replay) through engine/scripts/run-harness.py, which reads engine/tests/harnesses.json and applies the GUI lock, and summarise the result. Use to check a feature or after changing code a harness covers.
argument-hint: "<Harness id, e.g. KeyRoom.Rebinding> [scene-play|release|native-aot|headless]"
---

Harness: `$ARGUMENTS[0]`, mode: `$ARGUMENTS[1]` (every mode the manifest lists when empty).

1. `python3 engine/scripts/run-harness.py $ARGUMENTS[0] --plan` shows the modes and needs. Unknown id: `--list`.
2. Run it with results in a new scratchpad folder, never in the repo:
   `python3 engine/scripts/run-harness.py $ARGUMENTS[0] [--mode $ARGUMENTS[1]] --out <scratchpad>/harness-<id>`.
   Add `--build-editor` when Scene Play runs and `editor/MoNoon.app` is missing or older than the editor sources. Missing
   `player-templates/`: run `sh engine/scripts/bootstrap-worktree.sh` first. The runner wraps window steps in the GUI lock itself.
3. Report the table it prints (harness, modes, exit, seconds). On failure give `failed_step` and `first_error` from
   `<out>/summary.json` and the log path (`<out>/<id>/run.log`, plus `error.txt`/`editor.log`/`player.log` under `<out>/<id>`).
   Exit code 3 from an editor step is the probe's window guard; 2 from the runner is a missing prerequisite.

Do not edit code or baselines in this skill. A harness the manifest does not run correctly is fixed in `engine/tests/harnesses.json`
(check with `--check`). Rules: docs/manual/agent-workflow.md#하네스-실행기

---
name: regress
description: Run a MoNoon regression tier - one Engine.Checks group, the fast tier (engine checks plus the headless modes of the harnesses the branch touches) or the full local set before handing work back. Use after a change and before committing.
argument-hint: "check <name> | fast [base] | full [base]"
---

Tier: $ARGUMENTS. `base` defaults to the branch's integration branch from the brief (for example `wip/next1`); results go to a new
scratchpad folder (`--out`).

- `check <name>`: `dotnet run --project engine/tests/Engine.Checks -c Release -- "$PWD" --only <name>`
  (names: `dotnet run … -- "$PWD" --list` or docs/manual/agent-workflow.md#enginechecks---only).
- `fast`: `python3 engine/scripts/run-harness.py --tier fast --changed <base> --out <dir>`: `./monoon test` (or
  `./monoon test --tier fast` when `player-templates/` is missing), then the Release, Native AOT and headless modes of every
  harness the diff reaches directly. No window opens.
- `full`: `python3 engine/scripts/run-harness.py --tier full --changed <base> --out <dir>` (direct and broad matches, every
  mode, Scene Play under the GUI lock), then `./monoon ci`. Without `--changed` it runs every harness.

First look at the selection with `--plan`. Report one line per step from the runner's table: harness or command, modes, PASS/FAIL,
seconds, and `first_error` for failures. Never run two `dotnet build`s in this checkout at once.

#!/bin/sh
# Usage: engine/scripts/gui-lock.sh <command...>
# Runs one editor/player window session at a time across parallel agents and worktrees, and keeps the display awake meanwhile
# (a sleeping display stops Avalonia's render timer: "RenderTimer ... -6661"). docs/manual/agent-workflow.md#gui-잠금
# The lock is <main checkout>/.monoon-cache/gui.lock, shared by every worktree of this repository.
if common=$(git -C "$(dirname "$0")" rev-parse --path-format=absolute --git-common-dir 2>/dev/null); then
  dir=$(dirname "$common")/.monoon-cache
else
  dir=${TMPDIR:-/tmp}
fi
mkdir -p "$dir"
L=$dir/gui.lock

# A lock is stale when its owner shell is gone, or when it is older than 60 minutes and no editor or game process runs.
stale() {
  owner=$(cat "$L/owner" 2>/dev/null) || return 1
  pid=${owner%% *}
  if [ -n "$pid" ] && ! kill -0 "$pid" 2>/dev/null; then return 0; fi
  since=$(stat -f %m "$L" 2>/dev/null || stat -c %Y "$L" 2>/dev/null) || return 1
  [ $(( $(date +%s) - since )) -gt 3600 ] && ! pgrep -x MoNoon >/dev/null && ! pgrep -x MoNoonPlayer >/dev/null
}

waiting=
until mkdir "$L" 2>/dev/null; do
  if stale; then
    # shortcut: two waiters can both judge the same lock stale; the owner re-check below narrows that window, it does not close it.
    if [ "$(cat "$L/owner" 2>/dev/null)" = "$owner" ]; then
      echo "gui-lock: removing stale lock ($owner)" >&2
      rm -rf "$L"
      continue
    fi
  fi
  if [ -z "$waiting" ]; then echo "gui-lock: waiting for $(cat "$L/owner" 2>/dev/null)" >&2; waiting=1; fi
  sleep 5
done
echo "$$ $(date +%s) $*" > "$L/owner"
( while :; do caffeinate -u -t 1; sleep 15; done ) & awake=$!
trap 'kill $awake 2>/dev/null; rm -rf "$L"' EXIT
trap 'exit 130' INT TERM
"$@"

#!/bin/sh
# Usage: gui-lock.sh <command...>
# Runs one editor/player GUI session at a time across parallel agents and keeps the display awake meanwhile.
L=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/gui.lock
until mkdir "$L" 2>/dev/null; do sleep 5; done
echo "$$ $(date +%s) $*" > "$L/owner"
( while :; do caffeinate -u -t 1; sleep 15; done ) & awake=$!
trap 'kill $awake 2>/dev/null; rm -rf "$L"' EXIT INT TERM
"$@"

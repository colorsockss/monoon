#!/bin/sh
root=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/basics
. /private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/lib.sh
pgrep -fl qemu-system > $root/qemu.txt || echo "no qemu" > $root/qemu.txt
step build-players engine/scripts/build-players.sh
step monoon-test ./monoon test
step build-editor engine/scripts/build-editor-mac.sh
echo overall=$status; exit $status

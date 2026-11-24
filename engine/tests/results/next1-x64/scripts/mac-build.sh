#!/bin/sh
# Mac side of the wip/next1 Linux x64 regression: linux-x64 Release packages built on this Mac (15.7 trimmed ReadyToRun,
# 15.11 library exclusion, tar.gz layout) with a copy of the CLI (a concurrent run-harness may rebuild the worktree's CLI).
set -eux
S=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/n1x64
W=/Users/paare/codex_project/brutal_engine/.worktrees/x64reg
cd $W
rm -rf $S/cli $S/mac; mkdir -p $S/mac/src
cp -R engine/.artifacts/bin/Engine.Cli/release $S/cli
export MONOON_PLAYERS_DIR=$W/player-templates
rsync -a --exclude .monoon samples/KeyRoom samples/InventoryLab samples/SpriteLab $S/mac/src/
echo 'icon: fb750913-c06a-50c4-beae-cb27d559af91' >> $S/mac/src/KeyRoom/GameSettings  # as in the chapter-11 and phase1 runs
python3 engine/tests/PlatformerLab.Joints/verify.py prepare samples/PlatformerLab $S/mac/src/PlatformerLab
for g in KeyRoom InventoryLab SpriteLab PlatformerLab; do
  dotnet $S/cli/MoNoon.Cli.dll build $S/mac/src/$g release --target linux-x64 --profile distribution --package on --output $S/mac/out-$g
  cp $(find $S/mac/out-$g -name "$g-linux-x64.tar.gz") $S/mac/$g-linux-x64.tar.gz
done
ls -l $S/mac/*.tar.gz
echo MAC-BUILD-DONE

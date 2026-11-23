#!/bin/sh
# usage: perf-build.sh <tree> <out>   perf + size builds of one tree with its own templates (cwd-independent)
set -eu
T=$1; O=$2
cd "$T"; rm -rf "$O"; mkdir -p "$O"
./monoon build samples/CollisionLab release --profile development --output "$O/collision"
./monoon build samples/CollisionLab release --profile distribution --output "$O/collision"
python3 engine/tests/CollisionLab.SplitLoading/replay.py prepare "$O/Split" 2>/dev/null || true
for g in BarrageLab KeyRoom PlatformerLab InventoryLab; do
  ./monoon build samples/$g release --profile distribution --output "$O/$g"
  ./monoon build samples/$g native-aot --profile distribution --output "$O/$g"
done
echo BUILT "$O"

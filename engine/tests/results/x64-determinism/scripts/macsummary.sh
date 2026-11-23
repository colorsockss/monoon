#!/bin/sh
# Summarise the Mac gate run (macgates.sh output) against the committed Mac records. Usage: macsummary.sh <gate dir> <results dir>
D=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/x64det
M=$1; R=$2
echo "# Mac arm64 (macOS 27.0.1, Apple M4 Max), fix applied: trace vs the committed Mac record (tsvdiff: sha256 prefixes, rows, differing rows)"
for f in release aot scene-play; do echo "PlatformerLab.Joints $f: $(python3 $D/tsvdiff.py $M/PlatformerLab.Joints/$f/trace.tsv $R/physics-box2d/osx-arm64/joints-release.tsv)"; done
for f in release aot scene-play; do echo "PlatformerLab.Paths $f: $(python3 $D/tsvdiff.py $M/PlatformerLab.Paths/$f/trace.tsv $R/lines-paths/osx-arm64/paths-trace.tsv)"; done
for f in release aot scene-play; do echo "PlatformerLab.TileCollision $f: $(shasum -a 256 $M/PlatformerLab.TileCollision/$f/trace.tsv | cut -c1-16) (record f30ed686, headerless)"; done
for f in release aot scene-play; do echo "BarrageLab.Lines $f: $(shasum -a 256 $M/BarrageLab.Lines/$f/trace.tsv | cut -c1-16) (record 6969a524)"; done
for m in release aot; do echo "CollisionLab.Spatial $m:"; grep -h "kind=All" $M/spatial-$m.log | sed 's/linearP95us=[0-9.]* gridP95us=[0-9.]* //'; done
for m in release aot; do echo "BarrageLab.PrefabPools $m: $(grep PASS $M/pools-$m.log)"; done

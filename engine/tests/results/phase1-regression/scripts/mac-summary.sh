#!/bin/sh
# Compare the Mac harness outputs (h/) with the committed records. Usage: mac-summary.sh
H=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/h
W=/Users/paare/codex_project/brutal_engine/.worktrees/phase1; R=$W/engine/tests/results
D=$R/x64-determinism/scripts
s() { [ -f "$1" ] && shasum -a 256 "$1" | cut -c1-16 || echo missing; }
for f in release aot scene-play; do echo "Joints $f: $(python3 $D/tsvdiff.py $H/PlatformerLab.Joints/$f/trace.tsv $R/physics-box2d/osx-arm64/joints-release.tsv)"; done
for f in release aot scene-play; do echo "Paths $f: $(python3 $D/tsvdiff.py $H/PlatformerLab.Paths/$f/trace.tsv $R/lines-paths/osx-arm64/paths-trace.tsv)"; done
for f in release aot scene-play; do echo "TileCollision $f: $(s $H/PlatformerLab.TileCollision/$f/trace.tsv) (record f30ed686)"; done
for f in release aot scene-play; do echo "Lines $f: $(s $H/BarrageLab.Lines/$f/trace.tsv) (record 6969a524)"; done
for f in release native-aot scene-play; do echo "ScriptWaits $f: $(s $H/KeyRoom.ScriptWaits/$f/$f.trace) (record 98d74c78)"; done
for f in release native-aot scene-play; do echo "RichText $f: $(s $H/InventoryLab.RichText/runs/$f/reveal.tsv) (record 76118c09 = $(s $R/rich-text/reveal.tsv))"; done
for f in release native-aot scene-play; do echo "GridNavigation $f: $(s $H/SeedLab.GridNavigation/$f/result.json) (record d80dc873)"; done
for f in release native-aot scene-play; do echo "TileTerrain $f: trace $(s $H/SeedLab.TileTerrain/$f/trace.tsv) result $(s $H/SeedLab.TileTerrain/$f/result.json) (record c747efb9 / ae5d49b4)"; done
for f in replay-release replay-aot; do echo "CameraLab $f: record $(s $H/CameraLab.ScreenEffects/$f/record) (record d56a1838)"; done
for f in release aot; do echo "Spatial $f:"; grep -h 'kind=All' $H/CollisionLab/spatial-$f.log | sed 's/linearP95us=[0-9.]* gridP95us=[0-9.]* //'; done
for f in release native-aot; do echo "PrefabPools $f: $(grep PASS $H/BarrageLab.PrefabPools/$f.log 2>/dev/null || grep -h PASS $H/barrage-$f.log)"; done
echo "Aseprite: $(grep -h -o 'tick[s]\? [0-9, ]*' $H/ase-release.log $H/ase-aot.log $H/ase-sceneplay.log | sort -u | tr '\n' ';')"
for f in release native-aot; do echo "build_guest $f: $(s $H/InventoryLab/guest-results/$f.save) (record eed41def)"; done
for g in KeyRoom PlatformerLab; do echo "GameText $g:"; cat $H/Samples.GameText/runs/$g/comparison.tsv; done
echo "RichText comparison:"; cat $H/InventoryLab.RichText/runs/comparison.tsv

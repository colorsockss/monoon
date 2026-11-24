#!/bin/sh
# YAML defaults + determinism scan gates (macOS arm64). Usage: ydgates.sh <new dir>  (cwd: worktree root)
set -u
out=$1; mkdir -p "$out"
t=engine/tests; ed=editor/MoNoon.app/Contents/MacOS/MoNoon; bin=engine/.artifacts/bin
lock=${GUI_LOCK:-}  # optional wrapper that serialises GUI runs, e.g. "sh gui-lock.sh"
for p in PlatformerLab.Joints PlatformerLab.Paths ParticleLab.Trace; do
  dotnet build $t/$p -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false > "$out/probe-$p.log" 2>&1 || echo "probe $p FAIL"
done
for p in JointsProbe PathsProbe ParticlesProbe; do cp $bin/$p/release/$p.dll "$out/"; done
for spec in PlatformerLab.Joints:PlatformerLab:JointsProbe PlatformerLab.Paths:PlatformerLab:PathsProbe BarrageLab.Lines:BarrageLab:PathsProbe; do
  h=${spec%%:*}; rest=${spec#*:}; s=${rest%%:*}; probe=${rest#*:}; w="$out/$h"
  {
    python3 $t/$h/verify.py prepare samples/$s "$w/$s" &&
    ./monoon build "$w/$s" release --profile distribution --output "$w/builds" &&
    ./monoon build "$w/$s" native-aot --profile distribution --output "$w/builds" &&
    python3 $t/$h/verify.py play "$w/builds/$s.app/Contents/MacOS/MoNoonPlayer" "$w/release" &&
    python3 $t/$h/verify.py play "$w/builds/NativeAOT/$s.app/Contents/MacOS/MoNoonPlayer" "$w/aot" &&
    $lock python3 $t/$h/editor.py $ed "$w/$s" "$out/$probe.dll" "$w/scene-play" &&
    python3 $t/$h/verify.py compare "$w/scene-play" "$w/release" "$w/aot"
  } > "$out/$h.log" 2>&1
  echo "$h exit $?"
done
qa=$t/ParticleLab.Trace
for scene in Modules Sorting Stress; do
  w=$out/particles/$scene
  {
    python3 $qa/verify.py prepare samples/ParticleLab "$w/ParticleLab" $scene &&
    ./monoon build "$w/ParticleLab" release --profile distribution --output "$w/builds" &&
    ./monoon build "$w/ParticleLab" native-aot --profile distribution --output "$w/builds" &&
    python3 $qa/verify.py play "$w/builds/ParticleLab.app/Contents/MacOS/MoNoonPlayer" "$w/release" $scene &&
    python3 $qa/verify.py play "$w/builds/NativeAOT/ParticleLab.app/Contents/MacOS/MoNoonPlayer" "$w/aot" $scene &&
    python3 $qa/verify.py prepare samples/ParticleLab "$w/edit/ParticleLab" $scene &&
    $lock python3 $qa/verify.py editor $ed "$w/edit/ParticleLab" "$out/ParticlesProbe.dll" "$w/scene-play" $scene &&
    python3 $qa/verify.py compare "$w/scene-play" "$w/release" "$w/aot"
  } > "$out/particles-$scene.log" 2>&1
  echo "particles $scene exit $?"
done
./monoon build samples/BarrageLab release --profile distribution --output "$out/BarrageLab-builds" > "$out/BarrageLab-build.log" 2>&1 &&
./monoon build samples/BarrageLab native-aot --profile distribution --output "$out/BarrageLab-builds" >> "$out/BarrageLab-build.log" 2>&1 || echo "BarrageLab build FAIL"
for m in release aot; do
  d=$out/BarrageLab-builds; [ $m = aot ] && d=$d/NativeAOT
  python3 $t/BarrageLab.PrefabPools/replay.py "$d/BarrageLab.app/Contents/MacOS/MoNoonPlayer" "$out/pools-$m" > "$out/pools-$m.log" 2>&1; echo "pools $m exit $?"; grep PASS "$out/pools-$m.log"
done
./monoon build samples/InventoryLab release --profile distribution --output "$out/InventoryLab-builds" > "$out/InventoryLab-build.log" 2>&1 &&
./monoon build samples/InventoryLab native-aot --profile distribution --output "$out/InventoryLab-builds" >> "$out/InventoryLab-build.log" 2>&1 || echo "InventoryLab build FAIL"
./monoon test replay "$out/InventoryLab-builds" > "$out/replay.log" 2>&1; echo "replay exit $?"; grep "PASS InventoryLab" "$out/replay.log"
python3 $t/KeyRoom.WorldCheckpoints/ci.py osx-arm64 "$out/world" > "$out/world.log" 2>&1; echo "world exit $?"; tail -3 "$out/world.log"
echo YD-GATES-DONE

#!/bin/sh
# Release + Native AOT determinism gates for the AOT size work (macOS arm64, headless, no GUI lock).
# Usage: gates.sh <new dir>  (cwd: worktree root; templates current via engine/scripts/build-players.sh)
set -u
out=$1; mkdir -p "$out"
t=engine/tests
sha() { shasum -a 256 "$1" | cut -c1-16; }
# harness:sample:expected trace sha256 prefix
for spec in PlatformerLab.Joints:PlatformerLab:2d523cd0 PlatformerLab.TileCollision:PlatformerLab:f30ed686 \
            PlatformerLab.Paths:PlatformerLab:e37de6c0 PlatformerLab.Colliders:PlatformerLab:635f9029; do
  h=${spec%%:*}; rest=${spec#*:}; s=${rest%%:*}; want=${rest#*:}; w="$out/$h"
  {
    python3 $t/$h/verify.py prepare samples/$s "$w/$s" &&
    ./monoon build "$w/$s" release --profile distribution --output "$w/builds" &&
    ./monoon build "$w/$s" native-aot --profile distribution --output "$w/builds" &&
    python3 $t/$h/verify.py play "$w/builds/$s.app/Contents/MacOS/MoNoonPlayer" "$w/release" &&
    python3 $t/$h/verify.py play "$w/builds/NativeAOT/$s.app/Contents/MacOS/MoNoonPlayer" "$w/aot"
  } > "$out/$h.log" 2>&1
  code=$?
  r=$(sha "$w/release/trace.tsv" 2>/dev/null); a=$(sha "$w/aot/trace.tsv" 2>/dev/null)
  case "$r$a" in "$want"*"$want"*) verdict=PASS ;; *) verdict=FAIL ;; esac
  [ "$r" = "$a" ] || verdict=FAIL
  echo "$h exit $code release $r aot $a want $want $verdict"
done
python3 $t/ParticleLab.Trace/verify.py all samples/ParticleLab "$out/particles" > "$out/particles.log" 2>&1
echo "particles exit $?"; cat "$out/particles/summary.txt" 2>/dev/null
qa=$t/AudioLab.Buses
{
  python3 $qa/verify.py prepare "$out/audio/projects" &&
  for p in default ducking; do
    ./monoon build "$out/audio/projects/$p" release --profile distribution --output "$out/audio/builds-$p" &&
    ./monoon build "$out/audio/projects/$p" native-aot --profile distribution --output "$out/audio/builds-$p" &&
    python3 $qa/verify.py play "$out/audio/builds-$p/AudioLab.app/Contents/MacOS/MoNoonPlayer" "$out/audio/release-$p" &&
    python3 $qa/verify.py play "$out/audio/builds-$p/NativeAOT/AudioLab.app/Contents/MacOS/MoNoonPlayer" "$out/audio/aot-$p" || exit 1
  done &&
  for m in release aot; do python3 $qa/verify.py check "$out/audio/$m-default" "$out/audio/$m-ducking" || exit 1; done &&
  python3 $qa/verify.py compare "$out/audio/release-ducking" "$out/audio/aot-ducking"
} > "$out/audio.log" 2>&1
echo "audio exit $?"; grep PASS "$out/audio.log"
./monoon build samples/InventoryLab release --profile distribution --output "$out/InventoryLab-builds" > "$out/InventoryLab-build.log" 2>&1 &&
./monoon build samples/InventoryLab native-aot --profile distribution --output "$out/InventoryLab-builds" >> "$out/InventoryLab-build.log" 2>&1 || echo "InventoryLab build FAIL"
./monoon test replay "$out/InventoryLab-builds" > "$out/replay.log" 2>&1; echo "replay exit $?"; grep "PASS InventoryLab" "$out/replay.log"
python3 $t/KeyRoom.WorldCheckpoints/ci.py osx-arm64 "$out/world" > "$out/world.log" 2>&1; echo "world exit $?"; tail -3 "$out/world.log"
echo AOT-SIZE-GATES-DONE

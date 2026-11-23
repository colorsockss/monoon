#!/bin/sh
# Mac arm64 determinism gates: Joints/TileCollision/Paths/Lines, KeyRoom.ScriptWaits, InventoryLab.RichText (Release, AOT,
# Scene Play, compare), CollisionLab spatial and BarrageLab prefab pools (Release, AOT). Usage: macgates.sh <new dir>
# (cwd: worktree root). Builds the probe projects first; templates and editor must already be current.
set -u
out=$1; mkdir -p "$out"
t=engine/tests; ed=editor/MoNoon.app/Contents/MacOS/MoNoon; bin=engine/.artifacts/bin
lock=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/gui-lock.sh
for p in PlatformerLab.Joints PlatformerLab.TileCollision PlatformerLab.Paths KeyRoom.ScriptWaits InventoryLab.RichText; do
  dotnet build $t/$p -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false > "$out/probe-$p.log" 2>&1 || echo "probe $p FAIL"
done
for p in JointsProbe TileContactProbe PathsProbe EditorProbe RichTextProbe; do cp $bin/$p/release/$p.dll "$out/"; done
# harness:sample:probe:scene-play subdir:trace file (compare args)
for spec in PlatformerLab.Joints:PlatformerLab:JointsProbe PlatformerLab.TileCollision:PlatformerLab:TileContactProbe \
            PlatformerLab.Paths:PlatformerLab:PathsProbe BarrageLab.Lines:BarrageLab:PathsProbe \
            KeyRoom.ScriptWaits:KeyRoom:EditorProbe InventoryLab.RichText:InventoryLab:RichTextProbe; do
  h=${spec%%:*}; rest=${spec#*:}; s=${rest%%:*}; probe=${rest#*:}; w="$out/$h"
  case $h in
    KeyRoom.ScriptWaits) r=$w/release; a=$w/native-aot; p=$w/scene-play
      cmp="$p/scene-play.trace $r/release.trace $a/native-aot.trace" ;;
    InventoryLab.RichText) r=$w/runs/release; a=$w/runs/native-aot; p=$w/runs/scene-play; cmp="$w/runs" ;;
    PlatformerLab.TileCollision) r=$w/release; a=$w/aot; p=$w/scene-play; cmp=bytes ;;  # verify.py has no compare
    *) r=$w/release; a=$w/aot; p=$w/scene-play; cmp="$p $r $a" ;;
  esac
  {
    python3 $t/$h/verify.py prepare samples/$s "$w/$s" &&
    ./monoon build "$w/$s" release --profile distribution --output "$w/builds" &&
    ./monoon build "$w/$s" native-aot --profile distribution --output "$w/builds" &&
    python3 $t/$h/verify.py play "$w/builds/$s.app/Contents/MacOS/MoNoonPlayer" "$r" &&
    python3 $t/$h/verify.py play "$w/builds/NativeAOT/$s.app/Contents/MacOS/MoNoonPlayer" "$a" &&
    sh $lock python3 $t/$h/editor.py $ed "$w/$s" "$out/$probe.dll" "$p" &&
    if [ "$cmp" = bytes ]; then cmp $p/trace.tsv $r/trace.tsv && cmp $p/trace.tsv $a/trace.tsv; else python3 $t/$h/verify.py compare $cmp; fi
  } > "$out/$h.log" 2>&1
  echo "$h exit $?"
done
for s in CollisionLab BarrageLab; do
  ./monoon build samples/$s release --profile distribution --output "$out/$s-builds" > "$out/$s-build.log" 2>&1 &&
  ./monoon build samples/$s native-aot --profile distribution --output "$out/$s-builds" >> "$out/$s-build.log" 2>&1 || echo "$s build FAIL"
done
for m in release aot; do
  d=$out/CollisionLab-builds; [ $m = aot ] && d=$d/NativeAOT
  python3 $t/CollisionLab.Spatial/verify.py --player "$d/CollisionLab.app/Contents/MacOS/MoNoonPlayer" --output "$out/spatial-$m.log" > /dev/null 2>&1; echo "spatial $m exit $?"
  d=$out/BarrageLab-builds; [ $m = aot ] && d=$d/NativeAOT
  python3 $t/BarrageLab.PrefabPools/replay.py "$d/BarrageLab.app/Contents/MacOS/MoNoonPlayer" "$out/pools-$m" > "$out/pools-$m.log" 2>&1; echo "pools $m exit $?"; grep PASS "$out/pools-$m.log"
done
echo MAC-GATES-DONE

#!/bin/sh
# More Mac arm64 gates that run Affine2D call sites: SeedLab grid navigation and tile terrain (Release, AOT, Scene Play),
# CameraLab effect record (headless Release, AOT), KeyRoom world saves (ci.py), InventoryLab replay (build_guest.py).
# Usage: macgates-extra.sh <new dir>   (cwd: worktree root, templates and editor current)
set -u
out=$1; mkdir -p "$out"
t=engine/tests; ed=editor/MoNoon.app/Contents/MacOS/MoNoon; bin=engine/.artifacts/bin
lock=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/gui-lock.sh
for spec in SeedLab.GridNavigation:RouteProbe SeedLab.TileTerrain:TerrainProbe; do
  h=${spec%%:*}; probe=${spec#*:}; w="$out/$h"; extra=; [ $h = SeedLab.TileTerrain ] && extra="$w/SeedLab/Assets/Route.tileset"
  {
    dotnet build $t/$h -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false &&
    python3 $t/$h/verify.py prepare samples/SeedLab "$w/SeedLab" &&
    ./monoon build "$w/SeedLab" release --profile distribution --output "$w/builds" &&
    ./monoon build "$w/SeedLab" native-aot --profile distribution --output "$w/builds" &&
    python3 $t/$h/verify.py play "$w/builds/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$w/release" $extra &&
    python3 $t/$h/verify.py play "$w/builds/NativeAOT/SeedLab.app/Contents/MacOS/MoNoonPlayer" "$w/native-aot" $extra &&
    sh $lock python3 $t/$h/editor.py $ed "$w/SeedLab" $bin/$probe/release/$probe.dll "$w/scene-play"
  } > "$out/$h.log" 2>&1
  echo "$h exit $?"
done
w=$out/CameraLab; cp -R samples/CameraLab "$w-project"
{ ./monoon build "$w-project" release --profile distribution --output "$w/builds" &&
  ./monoon build "$w-project" native-aot --profile distribution --output "$w/builds" &&
  python3 $t/CameraLab.ScreenEffects/replay.py play "$w/builds/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$w/release" &&
  python3 $t/CameraLab.ScreenEffects/replay.py play "$w/builds/NativeAOT/CameraLab.app/Contents/MacOS/MoNoonPlayer" "$w/aot" &&
  python3 $t/CameraLab.ScreenEffects/replay.py compare "$w/release" "$w/aot"; } > "$out/CameraLab.log" 2>&1
echo "CameraLab replay exit $?"
python3 $t/KeyRoom.WorldCheckpoints/ci.py osx-arm64 "$out/world" > "$out/world.log" 2>&1; echo "KeyRoom.WorldCheckpoints exit $?"
python3 $t/InventoryLab.Replay/build_guest.py "$out/inv-builds" "$out/inv-results" > "$out/inventory.log" 2>&1; echo "InventoryLab replay exit $?"
echo MAC-EXTRA-DONE

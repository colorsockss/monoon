#!/bin/sh
# phase1 final regression, macOS arm64: every harness with verify.py / replay.py / editor.py / ci.py (Release, Native AOT,
# Scene Play where supported). Usage: 2-harnesses.sh [section]   (GUI steps under gui-lock.sh)
root=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/h
. /private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/lib.sh
only=${1:-all}
want() { [ "$only" = all ] || [ "$only" = "$1" ]; }

# Trace harnesses with prepare / play / editor.py / compare.
if want traces; then
for spec in PlatformerLab.Joints:PlatformerLab:JointsProbe PlatformerLab.TileCollision:PlatformerLab:TileContactProbe \
            PlatformerLab.Paths:PlatformerLab:PathsProbe BarrageLab.Lines:BarrageLab:PathsProbe; do
  h=${spec%%:*}; rest=${spec#*:}; s=${rest%%:*}; p=${rest#*:}; qa=engine/tests/$h; w=$root/$h; rm -rf $w
  pp=$qa; [ $h = BarrageLab.Lines ] && pp=engine/tests/PlatformerLab.Paths
  step $h-prepare python3 $qa/verify.py prepare samples/$s $w/$s
  step $h-build build2 $w/$s $w/builds
  step $h-release python3 $qa/verify.py play "$(mac $w/builds $s)" $w/release
  step $h-aot python3 $qa/verify.py play "$(aot $w/builds $s)" $w/aot
  step $h-probe probe $pp
  gstep $h-sceneplay python3 $qa/editor.py $ed $w/$s $bin/$p/release/$p.dll $w/scene-play
  if [ $h = PlatformerLab.TileCollision ]; then step $h-compare sh -c "cmp $w/scene-play/trace.tsv $w/release/trace.tsv && cmp $w/scene-play/trace.tsv $w/aot/trace.tsv"
  else step $h-compare python3 $qa/verify.py compare $w/scene-play $w/release $w/aot; fi
done
fi

if want waits; then
qa=engine/tests/KeyRoom.ScriptWaits; w=$root/KeyRoom.ScriptWaits; rm -rf $w
step waits-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step waits-build build2 $w/KeyRoom $w/builds
step waits-release python3 $qa/verify.py play "$(mac $w/builds KeyRoom)" $w/release
step waits-aot python3 $qa/verify.py play "$(aot $w/builds KeyRoom)" $w/native-aot
step waits-probe probe $qa
gstep waits-sceneplay python3 $qa/editor.py $ed $w/KeyRoom $bin/EditorProbe/release/EditorProbe.dll $w/scene-play
step waits-compare python3 $qa/verify.py compare $w/scene-play/scene-play.trace $w/release/release.trace $w/native-aot/native-aot.trace
step test-project ./monoon test project samples/KeyRoom
fi

if want richtext; then
qa=engine/tests/InventoryLab.RichText; w=$root/InventoryLab.RichText; rm -rf $w
step rt-prepare python3 $qa/verify.py prepare samples/InventoryLab $w/InventoryLab
step rt-build build2 $w/InventoryLab $w/builds
step rt-release python3 $qa/verify.py play "$(mac $w/builds InventoryLab)" $w/runs/release
step rt-aot python3 $qa/verify.py play "$(aot $w/builds InventoryLab)" $w/runs/native-aot
step rt-probe probe $qa
gstep rt-sceneplay python3 $qa/editor.py $ed $w/InventoryLab $bin/RichTextProbe/release/RichTextProbe.dll $w/runs/scene-play
step rt-compare python3 $qa/verify.py compare $w/runs
fi

if want gametext; then
qa=engine/tests/Samples.GameText; w=$root/Samples.GameText; rm -rf $w
step gt-probe probe $qa
for g in KeyRoom PlatformerLab; do
  step gt-$g-build build2 samples/$g $w/$g
  step gt-$g-release python3 $qa/verify.py play $g "$(mac $w/$g $g)" $w/runs/$g/release
  step gt-$g-aot python3 $qa/verify.py play $g "$(aot $w/$g $g)" $w/runs/$g/native-aot
  mkdir -p $w/projects; rsync -a --exclude .monoon samples/$g $w/projects/
  gstep gt-$g-sceneplay python3 $qa/editor.py $g $ed $w/projects/$g $bin/GameTextProbe/release/GameTextProbe.dll $w/runs/$g/scene-play
  step gt-$g-compare python3 $qa/verify.py compare $g $w/runs/$g
done
fi

if want audio; then
qa=engine/tests/AudioLab.Vorbis; w=$root/AudioLab.Vorbis; rm -rf $w; mkdir -p $w
gstep audio-ci python3 $qa/ci.py osx-arm64 $w/ci
step audio-probe probe $qa/AudioProbe.csproj
cp -R samples/AudioLab $w/AudioLab
gstep audio-sceneplay python3 $qa/editor.py $ed $w/AudioLab $bin/AudioProbe/release/AudioProbe.dll $w/scene-play
fi

if want screenfx; then
src=engine/tests/CameraLab.ScreenEffects; w=$root/CameraLab.ScreenEffects; rm -rf $w; mkdir -p $w
step fx-rumble sh -c "tar -xf $M/engine/.artifacts/native/downloads/SDL3-3.4.12.tar.gz -C $w SDL3-3.4.12/include && clang -dynamiclib -I$w/SDL3-3.4.12/include $src/rumble-pad.c -o $w/rumble.dylib"
step fx-probe probe $src
cp -R samples/CameraLab $w/CameraLab
step fx-build build2 $w/CameraLab $w/builds
gstep fx-verify env MONOON_QA_WINDOW=$M/engine/.artifacts/clip-qa/window MONOON_QA_RUMBLE_LIBRARY=$w/rumble.dylib python3 $src/verify.py \
  $w/CameraLab $w/builds/CameraLab.app $w/builds/NativeAOT/CameraLab.app $bin/CameraLabProbe/release/CameraLabProbe.dll $w/out
step fx-replay-release python3 $src/replay.py play "$(mac $w/builds CameraLab)" $w/replay-release
step fx-replay-aot python3 $src/replay.py play "$(aot $w/builds CameraLab)" $w/replay-aot
step fx-replay-compare python3 $src/replay.py compare $w/replay-release $w/replay-aot
fi

if want aseprite; then
qa=engine/tests/SpriteLab.Aseprite; w=$root/SpriteLab.Aseprite; rm -rf $w
step ase-prepare python3 $qa/verify.py prepare samples/SpriteLab $w/SpriteLab
step ase-build build2 $w/SpriteLab $w/builds
step ase-release python3 $qa/verify.py play "$(mac $w/builds SpriteLab)" $w/release $w/SpriteLab/Assets/ArrowCycle.aseprite
step ase-aot python3 $qa/verify.py play "$(aot $w/builds SpriteLab)" $w/native-aot $w/SpriteLab/Assets/ArrowCycle.aseprite
step ase-forbidden-release python3 $qa/verify.py forbidden $w/builds/SpriteLab.app
step ase-forbidden-aot python3 $qa/verify.py forbidden $w/builds/NativeAOT/SpriteLab.app
step ase-probe probe $qa
gstep ase-sceneplay python3 $qa/editor.py $ed $w/SpriteLab $bin/AsepriteProbe/release/AsepriteProbe.dll $w/scene-play
fi

if want copypaste; then
qa=engine/tests/KeyRoom.CopyPaste; w=$root/KeyRoom.CopyPaste; rm -rf $w
step cp-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step cp-probe probe $qa
gstep cp-editor python3 $qa/editor.py $ed $w/KeyRoom $bin/CopyPasteProbe/release/CopyPasteProbe.dll $w/editor
step cp-build build2 $w/KeyRoom $w/builds
step cp-release python3 $qa/verify.py play "$(mac $w/builds KeyRoom)" $w/release
step cp-aot python3 $qa/verify.py play "$(aot $w/builds KeyRoom)" $w/native-aot
step cp-forbidden-release python3 $qa/verify.py forbidden $w/builds/KeyRoom.app
step cp-forbidden-aot python3 $qa/verify.py forbidden $w/builds/NativeAOT/KeyRoom.app
fi

if want fieldrename; then
qa=engine/tests/KeyRoom.FieldRename; w=$root/KeyRoom.FieldRename; rm -rf $w
step fr-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step fr-probe probe $qa
gstep fr-editor python3 $qa/editor.py $ed $w/KeyRoom $bin/FieldRenameProbe/release/FieldRenameProbe.dll $w/editor
step fr-build build2 $w/KeyRoom $w/builds
step fr-release python3 $qa/verify.py play "$(mac $w/builds KeyRoom)" $w/release
step fr-aot python3 $qa/verify.py play "$(aot $w/builds KeyRoom)" $w/native-aot
fi

# Editor-only harnesses on samples/KeyRoom.
if want editoronly; then
for spec in KeyRoom.ScriptReload:ReloadProbe KeyRoom.EditorExtensions:ExtensionsProbe KeyRoom.EditorDocking:DockingProbe \
            KeyRoom.EditorTheme:EditorThemeProbe KeyRoom.EditorPanelsDetail:PanelsDetailProbe; do
  h=${spec%%:*}; p=${spec#*:}; w=$root/$h; rm -rf $w; mkdir -p $w
  step $h-probe probe engine/tests/$h
  gstep $h-editor python3 engine/tests/$h/editor.py $ed samples/KeyRoom $bin/$p/release/$p.dll $w/out
done
fi

if want rebind; then
qa=engine/tests/KeyRoom.Rebinding; w=$root/KeyRoom.Rebinding; rm -rf $w
step rebind-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step rebind-build build2 $w/KeyRoom $w/builds
step rebind-release python3 $qa/verify.py play "$(mac $w/builds KeyRoom)" $w/release
step rebind-aot python3 $qa/verify.py play "$(aot $w/builds KeyRoom)" $w/native-aot
step rebind-probe probe $qa
gstep rebind-sceneplay python3 $qa/editor.py $ed $w/KeyRoom $bin/EditorProbe/release/EditorProbe.dll $w/scene-play
fi

if want prefab; then
qa=engine/tests/KeyRoom.PrefabStructure; w=$root/KeyRoom.PrefabStructure; rm -rf $w
step prefab-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step prefab-probe probe $qa
gstep prefab-editor python3 $qa/editor.py $ed $w/KeyRoom $bin/PrefabStructureProbe/release/PrefabStructureProbe.dll $w/editor
step prefab-build build2 $w/KeyRoom $w/builds
step prefab-release python3 $qa/verify.py play "$(mac $w/builds KeyRoom)" $w/release
step prefab-aot python3 $qa/verify.py play "$(aot $w/builds KeyRoom)" $w/native-aot
fi

if want keys; then
qa=engine/tests/KeyRoom.ComponentKeys; w=$root/KeyRoom.ComponentKeys; rm -rf $w
step keys-prepare python3 $qa/verify.py prepare samples/KeyRoom $w/KeyRoom
step keys-probe probe $qa
gstep keys-editor python3 $qa/editor.py $ed $w/KeyRoom $bin/ComponentKeysProbe/release/ComponentKeysProbe.dll $w/editor
fi

if want groups; then
w=$root/KeyRoom.ObjectGroups; rm -rf $w
step groups-probe probe engine/tests/KeyRoom.ObjectGroups
step groups-package python3 engine/scripts/package-editor.py keyroom --output $w
gstep groups python3 engine/tests/KeyRoom.ObjectGroups/verify.py $ed $w/KeyRoom-sample.zip $bin/ObjectGroupsProbe/release/ObjectGroupsProbe.dll $w/run
fi

if want inv; then
qa=engine/tests/InventoryLab.DataList; w=$root/InventoryLab; rm -rf $w
step inv-build build2 samples/InventoryLab $w/builds
step list-release python3 $qa/verify.py play "$(mac $w/builds InventoryLab)" $w/release
step list-aot python3 $qa/verify.py play "$(aot $w/builds InventoryLab)" $w/native-aot
step inv-checkpoints ./monoon test inventorylab $w/builds --profile distribution
step inv-replay ./monoon test replay $w/builds
step inv-build-guest python3 engine/tests/InventoryLab.Replay/build_guest.py $w/guest-builds $w/guest-results --expected-rid osx-arm64
step inv-incr-cli dotnet build engine/src/Engine.Cli -c Release -m:1 -p:NuGetAudit=false
step inv-incr-prepare python3 engine/tests/InventoryLab.Incremental/prepare.py $w/IncrFixture
step inv-incr python3 engine/tests/InventoryLab.Incremental/verify.py engine/.artifacts/bin/Engine.Cli/release/MoNoon.Cli $w/IncrFixture \
  engine/tests/fixtures/InventoryLab $w/incr-results --dependencies
fi

if want seed; then
for spec in SeedLab.TileTerrain:TerrainProbe SeedLab.GridNavigation:RouteProbe; do
  h=${spec%%:*}; p=${spec#*:}; qa=engine/tests/$h; w=$root/$h; rm -rf $w
  extra=; [ $h = SeedLab.TileTerrain ] && extra=$w/SeedLab/Assets/Route.tileset
  step $h-prepare python3 $qa/verify.py prepare samples/SeedLab $w/SeedLab
  step $h-build build2 $w/SeedLab $w/builds
  step $h-release python3 $qa/verify.py play "$(mac $w/builds SeedLab)" $w/release $extra
  step $h-aot python3 $qa/verify.py play "$(aot $w/builds SeedLab)" $w/native-aot $extra
  step $h-probe probe $qa
  gstep $h-sceneplay python3 $qa/editor.py $ed $w/SeedLab $bin/$p/release/$p.dll $w/scene-play
done
fi

if want collision; then
w=$root/CollisionLab; rm -rf $w
step col-build sh -c "for p in development distribution; do ./monoon build samples/CollisionLab release --profile \$p --output $w/builds && ./monoon build samples/CollisionLab native-aot --profile \$p --output $w/builds || exit 1; done"
qa=engine/tests/CollisionLab.FrameProfiler
step prof-release python3 $qa/replay.py $w/builds/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/builds/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/prof-release
step prof-aot python3 $qa/replay.py $w/builds/Development/NativeAOT/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/builds/NativeAOT/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/prof-aot
step prof-probe probe $qa
gstep prof-sceneplay python3 $qa/editor.py $ed samples/CollisionLab $bin/ProfilerProbe/release/ProfilerProbe.dll $w/prof-scene-play
step spatial-release python3 engine/tests/CollisionLab.Spatial/verify.py --player "$(mac $w/builds CollisionLab)" --output $w/spatial-release.log
step spatial-aot python3 engine/tests/CollisionLab.Spatial/verify.py --player "$(aot $w/builds CollisionLab)" --output $w/spatial-aot.log
qa=engine/tests/CollisionLab.SplitLoading
step split-prepare python3 $qa/replay.py prepare $w/Split
step split-build sh -c "./monoon build $w/Split release --profile development --output $w/split-builds && ./monoon build $w/Split native-aot --profile development --output $w/split-builds"
step split-release python3 $qa/replay.py run $w/split-builds/Development/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/split-release --runs 3
step split-aot python3 $qa/replay.py run $w/split-builds/Development/NativeAOT/CollisionLab.app/Contents/MacOS/MoNoonPlayer $w/split-aot --runs 3
step split-probe probe $qa
gstep split-sceneplay python3 $qa/editor.py $ed $w/Split $bin/SplitLoadingProbe/release/SplitLoadingProbe.dll $w/split-scene-play 3
step perf-probe probe engine/tests/CollisionLab.EditorPerf
gstep editorperf python3 engine/tests/CollisionLab.EditorPerf/editor.py samples/CollisionLab $w/editor-perf 1 \
  cand=$ed,$bin/EditorPerfProbe/release/EditorPerfProbe.dll
fi

if want barrage; then
w=$root/BarrageLab.PrefabPools; rm -rf $w
step barrage-build build2 samples/BarrageLab $w/builds
step barrage-release python3 engine/tests/BarrageLab.PrefabPools/replay.py "$(mac $w/builds BarrageLab)" $w/release
step barrage-aot python3 engine/tests/BarrageLab.PrefabPools/replay.py "$(aot $w/builds BarrageLab)" $w/native-aot
fi

if want world; then
rm -rf $root/KeyRoom.WorldCheckpoints
step world-ci python3 engine/tests/KeyRoom.WorldCheckpoints/ci.py osx-arm64 $root/KeyRoom.WorldCheckpoints
fi

if want signing; then
o=$root/signing/out; x=$root/signing/extract; rm -rf $root/signing; mkdir -p $x
step pkg-mac-release ./monoon build samples/InventoryLab release --profile distribution --package on --output $o
step pkg-mac-aot ./monoon build samples/InventoryLab native-aot --profile distribution --package on --output $o
step pkg-win ./monoon build samples/InventoryLab release --target win-x64 --profile distribution --package on --output $o
step pkg-linux ./monoon build samples/InventoryLab release --target linux-x64 --profile distribution --package on --output $o
for kind in release aot; do
  d=$o; [ $kind = aot ] && d=$o/NativeAOT
  step sign-$kind-app codesign --verify --deep --strict --verbose=2 $d/InventoryLab.app
  sub=$x/zip; [ $kind = aot ] && sub=$x/zip/NativeAOT
  mkdir -p $sub
  step sign-$kind-zip sh -c "ditto -x -k $d/InventoryLab-osx-arm64.zip $sub && codesign --verify --deep --strict --verbose=2 $sub/InventoryLab.app"
done
step replay-signed ./monoon test replay $o
step replay-zip-extracted ./monoon test replay $x/zip
step pkg-listing sh -c "{ find $o $x -type f -o -type l; zipinfo -1 $o/InventoryLab-win-x64.zip; tar -tzf $o/InventoryLab-linux-x64.tar.gz; } > $root/signing/entries.txt; wc -l < $root/signing/entries.txt"
fi

echo "=== forbidden files in every build output"
find $root \( -path '*/builds/*' -o -path '*/guest-builds/*' -o -path '*/split-builds/*' -o -path '*/signing/*' -o -path '*/Samples.GameText/KeyRoom/*' -o -path '*/Samples.GameText/PlatformerLab/*' -o -path '*/KeyRoom.WorldCheckpoints/*' \) \
  \( -iname '*skia*' -o -iname 'Avalonia*' -o -iname 'Engine.Editor*' -o -iname 'Engine.Authoring*' -o -iname 'StbTrueTypeSharp*' -o -iname 'NVorbis*' -o -iname '*.glyphs' -o -iname 'HarfBuzz*' -o -iname 'MoNoon.dll' \) > $root/forbidden-${only}.txt
grep -Ei 'skia|avalonia|engine\.editor|engine\.authoring|stbtruetype|nvorbis|\.glyphs$|harfbuzz' $root/signing/entries.txt >> $root/forbidden-${only}.txt 2>/dev/null
if [ -s $root/forbidden-${only}.txt ]; then echo "FAIL forbidden files (see $root/forbidden-${only}.txt)"; status=1; else echo "OK   forbidden: none"; fi
echo overall=$status; exit $status

#!/bin/sh
# Known environment failures (Accessibility / key-event permission) and other GUI-input harnesses: run, record, no workarounds.
root=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/env
. /private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg/lib.sh
H=$S/p1reg/h
only=${1:-all}
want() { [ "$only" = all ] || [ "$only" = "$1" ]; }
if want bundles; then
gstep bundles python3 engine/tests/InventoryLab.Bundles/measure_mac.py $H/InventoryLab/builds $root/bundles.csv --repeats 1
fi
if want inspector; then
rm -rf $root/inspector $root/pkg; mkdir -p $root/pkg
step inspector-probe probe engine/tests/KeyRoom.PlayInspector
step inspector-package python3 engine/scripts/package-editor.py keyroom --output $root/pkg
step inspector-swift swiftc engine/tests/KeyRoom.PlayInspector/mac-input.swift -o $root/mac-input
gstep inspector python3 engine/tests/KeyRoom.PlayInspector/verify_play.py $ed $root/pkg/KeyRoom-sample.zip $bin/Probe/release/Probe.dll $root/inspector --mac-input $root/mac-input
fi
if want xcui; then
rm -rf $root/xcui
gstep xcui engine/tests/KeyRoom.Desktop/mac/verify.sh editor/MoNoon.app $root/pkg/KeyRoom-sample.zip $root/xcui engine/tests/results/editor-packaging/collected.save
fi
if want access; then
# verify.py writes captures into engine/tests/results/accessibility; restored with git afterwards (not part of this run's records).
q=$root/access; rm -rf $q; mkdir -p $q engine/.artifacts/clip-qa engine/.artifacts/tile-qa
cp $M/engine/.artifacts/clip-qa/window engine/.artifacts/clip-qa/window; cp $M/engine/.artifacts/tile-qa/access engine/.artifacts/tile-qa/access
src=engine/tests/Accessibility.Mac
step access-tools sh -c "tar -xf $M/engine/.artifacts/native/downloads/SDL3-3.4.12.tar.gz -C $q SDL3-3.4.12/include && clang -dynamiclib -I$q/SDL3-3.4.12/include $src/virtual-pad.c -o $q/pad.dylib && clang -fobjc-arc -framework Carbon -framework Foundation $src/input-source.m -o $q/input-source && clang -fobjc-arc -framework Cocoa $src/focus-ring.m -o $q/ring"
step access-build sh -c "./monoon build samples/AccessLab release --profile distribution --output game-builds/AccessLab && ./monoon build samples/AccessLab native-aot --profile distribution --output game-builds/AccessLab"
gstep access env MONOON_QA_PAD_LIBRARY=$q/pad.dylib MONOON_QA_INPUT_SOURCE=$q/input-source MONOON_QA_RING=$q/ring python3 $src/verify.py
git checkout -- engine/tests/results/accessibility; git clean -fdq engine/tests/results/accessibility
fi
if want widgets; then
q=$root/widgets; rm -rf $q; mkdir -p $q
step widgets-build ./monoon build samples/AudioLab release --profile distribution --output $q/builds
step widgets-pad dotnet build engine/tests/AudioLab.Widgets/VirtualPad.csproj -c Release -m:1 -p:NuGetAudit=false
step widgets-swift swiftc engine/tests/AudioLab.Vorbis/mac-input.swift -o $q/mac-input
gstep widgets python3 engine/tests/AudioLab.Widgets/verify_widgets.py "$(mac $q/builds AudioLab)" $bin/VirtualPad/release/VirtualPad.dll $q/results $q/mac-input
fi
if want display; then
q=$root/display; rm -rf $q; mkdir -p $q
step display-build ./monoon build samples/DisplayLab release --profile distribution --output $q/builds
step display-swift swiftc engine/tests/AudioLab.Vorbis/mac-input.swift -o $q/mac-input
gstep display python3 engine/tests/DisplayLab.Desktop/verify_display.py "$(mac $q/builds DisplayLab)" $q/results $q/mac-input
fi
echo overall=$status; exit $status

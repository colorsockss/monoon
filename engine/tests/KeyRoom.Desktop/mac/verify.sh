#!/bin/sh
set -eu

if [ "$#" -ne 4 ]; then
    echo "Usage: $0 MoNoon.app KeyRoom-sample.zip NEW_RESULTS_DIRECTORY MAC_REFERENCE.save" >&2
    exit 2
fi
test_dir=$(cd "$(dirname "$0")" && pwd -P)
app=$(cd "$1" && pwd -P)
reference=$(cd "$(dirname "$4")" && pwd -P)/$(basename "$4")
test "$(uname -m)" = arm64
lipo "$app/Contents/MacOS/MoNoon" -verify_arch arm64
test -f "$reference"
mkdir "$3"
results=$(cd "$3" && pwd -P)
mkdir -p "$results/baseline/profile" "$results/edit/profile" "$results/reopen/profile"
work=$(mktemp -d "${TMPDIR:-/tmp}/monoon-editor-ui.XXXXXX")
trap 'rm -rf "$work"' EXIT
ditto -x -k "$2" "$work/sample"
export TEST_RUNNER_MONOON_TEST_APP="$app"
export TEST_RUNNER_MONOON_TEST_PROJECT="$work/sample/KeyRoom"
export TEST_RUNNER_MONOON_TEST_RESULTS="$results"
export TEST_RUNNER_MONOON_TEST_REFERENCE="$reference"
status=0
xcodebuild test -project "$test_dir/EditorUITests.xcodeproj" -scheme EditorUITests \
    -destination 'platform=macOS,arch=arm64' -parallel-testing-enabled NO \
    -derivedDataPath "$work/DerivedData" -resultBundlePath "$results/ScenePlay.xcresult" || status=$?
cp "$work/sample/KeyRoom/Scenes/KeyRoom.scene" "$results/KeyRoom.scene"
if [ "$status" -ne 0 ]; then exit "$status"; fi
cp "$results/baseline"/profile/Play/*/slot-001.save "$results/baseline/original.save"
for phase in edit reopen; do
    cp "$results/$phase"/profile/Play/*/slot-001.save "$results/$phase/edited.save"
done

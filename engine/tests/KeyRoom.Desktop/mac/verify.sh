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
# The editor selects the scene's first object on open; the showcase lists the floor first, so the copy lists Player (and
# its children) first and the Inspector opens on the Player's Transform.
python3 - "$work/sample/KeyRoom/Scenes/Showcase.scene" <<'PY'
import re, sys
path = sys.argv[1]
head, body = open(path, encoding="utf-8").read().split("objects:\n", 1)
blocks = ["- id: " + b for b in body.split("- id: ")[1:]]
ids = {re.match(r"- id: (\S+)", b)[1] for b in blocks if "\n  name: Player\n" in b}
for b in blocks:
    parent = re.search(r"\n  parentId: (\S+)", b)
    if parent and parent[1] in ids: ids.add(re.match(r"- id: (\S+)", b)[1])
first = [b for b in blocks if re.match(r"- id: (\S+)", b)[1] in ids]
open(path, "w", encoding="utf-8").write(head + "objects:\n" + "".join(first + [b for b in blocks if b not in first]))
PY
export TEST_RUNNER_MONOON_TEST_APP="$app"
export TEST_RUNNER_MONOON_TEST_PROJECT="$work/sample/KeyRoom"
export TEST_RUNNER_MONOON_TEST_RESULTS="$results"
export TEST_RUNNER_MONOON_TEST_REFERENCE="$reference"
status=0
xcodebuild test -project "$test_dir/EditorUITests.xcodeproj" -scheme EditorUITests \
    -destination 'platform=macOS,arch=arm64' -parallel-testing-enabled NO \
    -derivedDataPath "$work/DerivedData" -resultBundlePath "$results/ScenePlay.xcresult" || status=$?
cp "$work/sample/KeyRoom/Scenes/Showcase.scene" "$results/Showcase.scene"
if [ "$status" -ne 0 ]; then exit "$status"; fi
cp "$results/baseline"/profile/Play/*/slot-001.save "$results/baseline/original.save"
for phase in edit reopen; do
    cp "$results/$phase"/profile/Play/*/slot-001.save "$results/$phase/edited.save"
done

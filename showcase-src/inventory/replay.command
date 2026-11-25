#!/bin/zsh
# Input replay showcase: record a play session of InventoryLab in a window, then replay the same input file in the Release and
# Native AOT players without a window and compare the save files with the recorded run's. REPLAY_DIR holds build/ (from
# ./monoon build samples/InventoryLab release|native-aot --output $REPLAY_DIR/build) and receives run/.
# Open it in Terminal through the GUI lock:  sh engine/scripts/gui-lock.sh open -a Terminal showcase-src/inventory/replay.command
printf '\e[8;34;66t'   # 34 rows x 66 columns, so the text stays large in the video
repo=$(cd "$(dirname "$0")/../.." && pwd)
S=${REPLAY_DIR:-/tmp/monoon-replay}
player="$S/build/InventoryLab.app/Contents/MacOS/MoNoonPlayer"
native="$S/build/NativeAOT/InventoryLab.app/Contents/MacOS/MoNoonPlayer"
run="$S/run"; rm -rf "$run"; mkdir -p "$run"; cd "$run"
step() { print -P "\n%F{yellow}\$%f %B$1%b" }
clear
print -P "%BMoNoon  input record and replay%b  (InventoryLab)"
step "MoNoonPlayer --record-input Yard.inputreplay record/"
print "  a window opens; play, then Esc"
sh "$repo/engine/scripts/gui-lock.sh" "$player" --record-input Yard.inputreplay record >/dev/null 2>&1
print "  recorded $(grep -c '  input:' Yard.inputreplay) input events"
step "MoNoonPlayer --replay-input Yard.inputreplay release/"
print "  Release player, no window"
"$player" --replay-input Yard.inputreplay release >/dev/null 2>&1; print "  done (exit $?)"
step "NativeAOT/MoNoonPlayer --replay-input Yard.inputreplay aot/"
print "  Native AOT player, no window"
"$native" --replay-input Yard.inputreplay aot >/dev/null 2>&1; print "  done (exit $?)"
for save in $(cd record && find . -name '*.save' | sort); do
  save=${save#./}
  step "cmp record/$save  release/  aot/"
  cmp -s "record/$save" "release/$save" && print -P "  %F{green}Release     identical%f" || print -P "  %F{red}Release     differs%f"
  cmp -s "record/$save" "aot/$save" && print -P "  %F{green}Native AOT  identical%f" || print -P "  %F{red}Native AOT  differs%f"
  for d in record release aot; do printf '  %-8s %s\n' $d "$(shasum -a 256 "$d/$save" | cut -c1-16)"; done
done
print -P "\n%BSame input file, same game state in every build.%b"
sleep 600

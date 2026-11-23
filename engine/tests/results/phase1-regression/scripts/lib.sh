# Shared helpers for the phase1 final regression. Source after setting $root.
S=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad
W=/Users/paare/codex_project/brutal_engine/.worktrees/phase1
M=/Users/paare/codex_project/brutal_engine
cd $W
mkdir -p "$root"
status=0
step() { name=$1; shift; echo "=== $name $(date +%T)"; if "$@" > "$root/$name.log" 2>&1; then echo "OK   $name"; else echo "FAIL $name (see $root/$name.log)"; tail -8 "$root/$name.log"; status=1; fi; }
gstep() { name=$1; shift; step "$name" sh $S/gui-lock.sh "$@"; }
mac() { echo "$1/$2.app/Contents/MacOS/MoNoonPlayer"; }
aot() { echo "$1/NativeAOT/$2.app/Contents/MacOS/MoNoonPlayer"; }
build2() { ./monoon build "$1" release --profile distribution --output "$2" && ./monoon build "$1" native-aot --profile distribution --output "$2"; }
probe() { dotnet build "$1" -c Release -m:1 -p:UsedAvaloniaProducts= -p:NuGetAudit=false; }
ed=editor/MoNoon.app/Contents/MacOS/MoNoon
bin=engine/.artifacts/bin

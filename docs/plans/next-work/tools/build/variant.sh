#!/bin/sh
# usage: variant.sh <sample> <variant-name> [msbuild -p args...]
# Publishes the worktree Native AOT kit with the sample's compiled scripts into R/var/<sample>/<variant>/app,
# then copies Game/, Licenses/, EngineAssets/ from the sample's development AOT build.
set -eu
R=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/research/t2-builds
KIT=/Users/paare/codex_project/brutal_engine/.claude/worktrees/agent-a1cc2177431e98601/player-templates/native-aot/src/Engine.Player/Engine.Player.csproj
s=$1; v=$2; shift 2
out=$R/var/$s/$v
rm -rf "$out"; mkdir -p "$out"
start=$(date +%s)
/usr/local/share/dotnet/dotnet publish "$KIT" -c Release -r osx-arm64 --self-contained true \
  -p:DebugType=portable -p:UseSharedCompilation=false -p:NuGetAudit=false -nr:false -m:1 \
  -p:MoNoonGameReferences=$R/work-$s/Scripts.props --artifacts-path "$out/artifacts" -o "$out/bin" "$@" > "$out/publish.log" 2>&1 || { tail -30 "$out/publish.log"; exit 1; }
echo "build_s=$(( $(date +%s) - start ))" > "$out/build-time"
src=$(ls -d $R/keep/$s/Development/NativeAOT/*.app/Contents/MacOS)
for d in "$src"/*/; do case "$d" in *.dSYM/) ;; *) cp -R "${d%/}" "$out/bin/";; esac; done
mkdir -p "$out/syms"
find "$out/bin" -maxdepth 1 \( -name '*.pdb' -o -name '*.dSYM' -o -name '*.dbg' -o -name '*.map*' -o -name '*.mstat' \) -exec mv {} "$out/syms/" \;
grep -c "warning IL" "$out/publish.log" | sed 's/^/IL_warnings=/' || true

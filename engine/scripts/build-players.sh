#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
if [ "$#" = 0 ]; then set -- osx-arm64 win-x64 linux-x64; fi
players=${MONOON_PLAYERS_DIR:-"$(dirname "$PWD")/player-templates"}
# bootstrap-worktree.sh may link player-templates to the shared cache; build local templates instead of writing into it.
if [ -z "${MONOON_PLAYERS_DIR:-}" ] && [ -L "$players" ]; then rm "$players"; fi
mkdir -p "$players"
stage=$(mktemp -d "$players/.publish.XXXXXX")
trap 'rm -rf "$stage"' EXIT
for target do
  case "$target" in osx-arm64|win-x64|linux-x64) ;; *) exit 2 ;; esac
  dotnet publish src/Engine.Player -c Release -r "$target" --self-contained true \
    -p:NuGetAudit=false -p:UseSharedCompilation=false -nr:false -m:1 \
    -o "$stage/$target"
  mkdir -p "$players/$target"
  rsync -a --delete "$stage/$target/" "$players/$target/"
done
scripts/build-native-aot-kit.sh

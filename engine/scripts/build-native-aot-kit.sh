#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
kit=${MONOON_PLAYERS_DIR:-"$(dirname "$PWD")/player-templates"}/native-aot
mkdir -p "$kit/src" "$kit/native/windows-x64"
cp Directory.Build.props Directory.Build.targets "$kit/"
for project in Engine.Core Engine.Desktop Engine.Player; do
  rsync -a --delete --exclude 'bin/' --exclude 'obj/' --exclude '.DS_Store' "src/$project/" "$kit/src/$project/"
done
cp native/windows-x64/SDL3.dll native/windows-x64/LICENSE.txt "$kit/native/windows-x64/"

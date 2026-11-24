#!/bin/sh
# usage: mapbuild.sh <worktree> <sample> <label>   builds distribution native-aot with ILC map+mstat into $AOT_SIZE_OUT/<label>/<sample>
set -eu
S=${AOT_SIZE_OUT:?set AOT_SIZE_OUT to a scratch folder}
wt=$1; s=$2; l=$3; out=$S/$l/$s; rm -rf "$out"; mkdir -p "$out/native"
cd "$wt"
IlcGenerateMapFile=true IlcGenerateMstatFile=true NativeIntermediateOutputPath="$out/native/" \
  ./monoon build samples/$s native-aot --profile distribution --output "$out/build" > "$out/build.log" 2>&1 || { tail -30 "$out/build.log"; exit 1; }
exe=$(find "$out/build" -path '*Contents/MacOS/MoNoonPlayer' -type f | head -1)
echo "$s $l exe=$(stat -f %z "$exe")"

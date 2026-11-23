#!/bin/sh
# Bytes of regular files (symlinks excluded) of the distribution outputs, base (b524375) vs cand.
R=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/p1reg
bytes() { find "$1" -type f -exec stat -f %z {} + | awk '{s+=$1} END {print s}'; }
printf 'output\tbase\tcand\tdiff\n'
for g in KeyRoom PlatformerLab InventoryLab; do
  for m in release aot aot-binary; do
    case $m in release) p=$g/$g.app;; aot) p=$g/NativeAOT/$g.app;; aot-binary) p=$g/NativeAOT/$g.app/Contents/MacOS/MoNoonPlayer;; esac
    b=$(bytes $R/perf/base/$p); c=$(bytes $R/perf/cand/$p)
    printf '%s %s\t%s\t%s\t%+d\n' $g $m $b $c $((c-b))
  done
done

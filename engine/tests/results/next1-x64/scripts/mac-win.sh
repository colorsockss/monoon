#!/bin/sh
# Build-host comparison: the same win-x64 Release builds the guest makes (KeyRoom, InventoryLab from plain sample copies),
# made on this Mac with the same CLI copy, listed file by file (manifest-mac-<game>.json).
set -eu
S=/private/tmp/claude-501/-Users-paare-codex-project-brutal-engine/d9b5815d-691b-46f7-b17a-6ed9a224a15b/scratchpad/n1x64
W=/Users/paare/codex_project/brutal_engine/.worktrees/x64reg
export MONOON_PLAYERS_DIR=$W/player-templates
rm -rf $S/macwin; mkdir -p $S/macwin/src
for g in KeyRoom InventoryLab; do
  rsync -a --exclude .monoon $W/samples/$g $S/macwin/src/
  dotnet $S/cli/MoNoon.Cli.dll build $S/macwin/src/$g release --target win-x64 --profile distribution --output $S/macwin/out-$g > $S/macwin/$g.log 2>&1
  python3 - "$S/macwin/out-$g/$g-win-x64" "$S/macwin/manifest-mac-$g.json" <<'PY'
import hashlib, json, os, sys
from pathlib import Path
d = Path(sys.argv[1])
files = {p.relative_to(d).as_posix(): [p.stat().st_size, hashlib.sha256(p.read_bytes()).hexdigest()] for p in sorted(d.rglob('*')) if p.is_file() and not p.is_symlink()}
Path(sys.argv[2]).write_text(json.dumps({'folder': d.name, 'files': files, 'links': {p.relative_to(d).as_posix(): os.readlink(p) for p in d.rglob('*') if p.is_symlink()}}, indent=1))
print(d.name, len(files), sum(v[0] for v in files.values()))
PY
done

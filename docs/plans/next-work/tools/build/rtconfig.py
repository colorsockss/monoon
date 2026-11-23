"""Copy a Release app and add runtimeconfig knobs. usage: rtconfig.py <src .app> <dst .app> key=value..."""
import json, shutil, sys
from pathlib import Path
src, dst = Path(sys.argv[1]), Path(sys.argv[2])
if not dst.exists(): shutil.copytree(src, dst, symlinks=True)
cfg = dst / 'Contents/MacOS/MoNoonPlayer.runtimeconfig.json'
data = json.loads(cfg.read_text())
for kv in sys.argv[3:]:
    k, v = kv.split('=', 1)
    data['runtimeOptions']['configProperties'][k] = json.loads(v)
cfg.write_text(json.dumps(data, indent=2))
print(cfg)

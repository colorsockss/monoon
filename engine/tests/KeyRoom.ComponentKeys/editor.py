from pathlib import Path
import json
import os
import shutil
import subprocess
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from verify import HERE, check

editor,project,hook,out = [Path(a).resolve() for a in sys.argv[1:]]
out.mkdir(parents=True, exist_ok=False)
shutil.copy(HERE/"Compare.inputreplay",out)
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_KEYS_EDITOR=str(out),
           MONOON_POSE_REPORT=str(out/"values.tsv"), MONOON_USER_DATA=str(out/"profile"))
with (out/"editor.log").open("wb") as log:
    subprocess.run([str(editor),str(project)],env=env,stdout=log,stderr=log,timeout=900,check=True)
if (out/"error.txt").exists(): raise RuntimeError((out/"error.txt").read_text(encoding="utf-8"))
world = check(out/"values.tsv")
preview = json.loads((out/"preview.json").read_text(encoding="utf-8"))
for row in preview: row["object"]=row.pop("label")
assert sorted(preview,key=lambda r:(r["object"],r["tick"])) == world, (preview,world)
(out/"result.json").write_text(json.dumps(world,indent=2)+"\n")
print("PASS clip edit, Undo/Redo, save/reopen, preview and Scene Play", flush=True)

"""Roadmap 13.2, 13.3, 13.4, 13.12 and 13.14 through the packaged Mac editor's Inspector (KeyRoom), with captures.
The Inspector lock and Debug view must survive a restart and leave the scene file and a Release build unchanged.

editor.py <MoNoon> <sample> <probe.dll> <out>
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:5]]
repo = Path(__file__).resolve().parents[3]
out.mkdir(parents=True)
project = out / "KeyRoom"
shutil.copytree(sample, project, ignore=shutil.ignore_patterns("bin", "obj", ".monoon"))
profile = out / "profile"
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message, flush=True)
    if not ok:
        failures.append(message)


def launch(phase, limit=600):
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_IC_OUTPUT=str(out), MONOON_IC_PHASE=phase, MONOON_USER_DATA=str(profile))
    with (out / f"{phase}-editor.log").open("wb") as log:
        code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log,
                              timeout=limit * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
    if (out / f"{phase}-error.txt").exists():
        raise SystemExit(f"FAIL {phase}: " + (out / f"{phase}-error.txt").read_text(encoding="utf-8"))
    if code != 0 or not (out / f"{phase}.json").exists():
        raise SystemExit(f"FAIL {phase}: editor exited with {code}; see {phase}-editor.log")
    print(f"PASS {phase}", flush=True)
    return json.loads((out / f"{phase}.json").read_text(encoding="utf-8"))


def tree(root):
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(root.rglob("*")) if p.is_file()}


def game(root):
    """The shipped game without the per-build identity: Symbols/<buildId>/ and the buildId line of MoNoonBuild."""
    files = {k: v for k, v in tree(root).items() if not k.startswith("Symbols/")}
    for key in [k for k in files if k.endswith("/MoNoonBuild")]:
        text = (root / key).read_text(encoding="utf-8")
        files[key] = hashlib.sha256("\n".join(l for l in text.splitlines() if not l.strip().startswith("buildId:")).encode()).hexdigest()
    return files


def build(name):
    target = out / name
    cli = os.environ.get("MONOON_CLI")
    command = (["dotnet", cli] if cli.endswith(".dll") else [cli]) if cli else [str(repo / "monoon")]
    with (out / f"{name}.log").open("wb") as log:
        subprocess.run([*command, "build", str(project), "release", "--output", str(target)], cwd=repo, stdout=log, stderr=log, timeout=1800, check=True)
    return game(target)


saved = launch("save")
first = launch("first", 900)
for key in ("active_enabled", "script_fields", "component_menu", "warnings", "lock_debug"):
    print(f"  {key}: {json.dumps(first[key], ensure_ascii=False)}")
for row in first["alignment"]:
    print(f"  alignment {row['label']}: title x {row['title_x']} icon x {row['icon_x']} check x {row['check_x']} name x {row['name_x']}")
scene_sha = first["scene"]["sha"]
check(scene_sha != saved["scene"]["sha"], "the first session saved MoveSpeed 300 (scene changed once)")
state = json.loads(first["scene"]["state"])
check(state["InspectorLock"].get(first["scene"]["path"]) and state["InspectorDebug"] is False, "lock stored per scene in editor-state.json")
before = build("build-before")

restart = launch("restart")
check(restart["restart"]["sha"] == scene_sha, "after restart (lock restored, Debug view on and off) the saved scene is byte-identical")
after = build("build-after")
check(not (project / "editor-state.json").exists() and (profile / "editor-state.json").exists(), "editor-state.json lives in the user folder")
changed = sorted(k for k in before.keys() | after.keys() if before.get(k) != after.get(k))
check(not changed, f"Release builds before and after the lock/Debug session are identical apart from the build id ({len(before)} files, changed {changed[:8]})")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS Inspector components in", out)

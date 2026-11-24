"""Roadmap 12.7 panel details in the packaged Mac editor: Hierarchy search and Scene eye/lock, Project path bar, tree and grid,
Inspector fold memory, Console filters, alignment. Scene eye/lock must leave the scene file and a Release build unchanged."""
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
    print(("PASS " if ok else "FAIL ") + message)
    if not ok:
        failures.append(message)


def launch(phase):
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_PANELS_OUTPUT=str(out),
               MONOON_PANELS_PHASE=phase, MONOON_USER_DATA=str(profile))
    with (out / f"{phase}-editor.log").open("wb") as log:
        code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=240 * float(os.environ.get('MONOON_QA_SLOW') or 1)).returncode
    if (out / f"{phase}-error.txt").exists():
        raise SystemExit(f"FAIL {phase}: " + (out / f"{phase}-error.txt").read_text(encoding="utf-8"))
    if code != 0 or not (out / f"{phase}.json").exists():
        raise SystemExit(f"FAIL {phase}: editor exited with {code}; see {phase}-editor.log")
    print(f"PASS {phase}")
    return json.loads((out / f"{phase}.json").read_text(encoding="utf-8"))


def tree(root):
    return {p.relative_to(root).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(root.rglob("*")) if p.is_file()}


def game(root):
    """The shipped game without the per-build identity: Symbols/<buildId>/ and the buildId line of MoNoonBuild."""
    files = {k: v for k, v in tree(root).items() if not k.startswith("Symbols/")}
    for key in [k for k in files if k.endswith("/MoNoonBuild")]:
        text = (root / key).read_text(encoding="utf-8")
        files[key] = hashlib.sha256("\n".join(l for l in text.splitlines() if not l.strip().startswith("buildId:")).encode()).hexdigest()
    return files


def build(name):
    target = out / name
    # MONOON_CLI: a packaged MoNoon.Cli(.dll) where there is no source checkout (x64 guests).
    cli = os.environ.get("MONOON_CLI")
    command = (["dotnet", cli] if cli.endswith(".dll") else [cli]) if cli else [str(repo / "monoon")]
    with (out / f"{name}.log").open("wb") as log:
        subprocess.run([*command, "build", str(project), "release", "--output", str(target)],
                       cwd=repo, stdout=log, stderr=log, timeout=1800, check=True)
    return game(target)


saved = launch("save")
scene_sha = saved["scene"]["sha"]
project_files = tree(project)
before = build("build-before")

first = launch("first")
for row in first["hierarchy_search"]:
    print(f"  search {row['query']!r}: {row['matches']} matches, rows {row['shown']}")
marks = first["scene_marks"]
print("  eye:", marks["hidden"], "lock:", marks["locked"])
check(marks["scene_sha_before"] == scene_sha == marks["scene_sha_after"], "eye/lock toggles + save leave the scene file bytes unchanged")
check(first["scene"]["sha"] == scene_sha, "scene saved with Door hidden and Wall Top locked is byte-identical to the untouched save")
state = json.loads(first["scene"]["state"])
scene_key = first["scene"]["path"]
check(len(state["Hidden"].get(scene_key, [])) == 1 and len(state["Locked"].get(scene_key, [])) == 1,
      "marks are stored in the per-user editor-state.json, keyed by scene path")
for row in first["alignment"]["rows"]:
    print(f"  Inspector {row['label']}: label x {row['label_x']} input x {row['input_x']} column {row['column']} drift {row['drift']}")
for row in first["alignment"]["toolbars"]:
    print(f"  toolbar {row['name']}: centre y {row['centre']}")
print("  long name:", first["alignment"]["long_name"])
print("  project:", json.dumps(first["project"]["steps"]))
print("  inspector:", json.dumps({k: first["inspector"][k] for k in ("folded", "shared_by_type", "multi_caption", "combos")}))
print("  console:", json.dumps({k: first["console"][k] for k in ("counts", "all", "without_info", "collapsed", "collapsed_all", "searched")}))

restart = launch("restart")
check(restart["restart"]["sha"] == scene_sha, "after restart (marks restored) the saved scene is still byte-identical")

after = build("build-after")
check(tree(project).keys() == project_files.keys(), "no editor state file was written into the project")
check(not (project / "editor-state.json").exists() and (profile / "editor-state.json").exists(), "editor-state.json lives in the user folder")
changed = sorted(k for k in before.keys() | after.keys() if before.get(k) != after.get(k))
check(not changed, f"Release builds before and after the eye/lock session are identical apart from the build id ({len(before)} files, changed {changed[:8]})")
leaks = [k for k in after if "Avalonia" in k or "Skia" in k or "editor-state" in k]
check(not leaks, f"Release build has no editor files {leaks[:5]}")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS editor panel details in", out)

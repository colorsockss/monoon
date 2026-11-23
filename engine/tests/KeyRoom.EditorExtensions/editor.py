"""Roadmap 11.10 project editor extensions in the packaged Mac editor, then Release and Native AOT builds of the same KeyRoom
copy: no Editor/ source, extension assembly, script reload code or editor library may reach a game build."""
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
failures = []
# File names and strings that only editor code carries. MoNoon.dll is the editor assembly; MoNoonPlayer is the player.
FORBIDDEN_FILES = ("Engine.Authoring", "Engine.Editor", "SkiaSharp", "libSkiaSharp", "Avalonia", "MoNoon.dll", "Microsoft.CodeAnalysis")
FORBIDDEN_TEXT = (b"KeyRoomTools", b"IEditorExtension", b"EditorExtensionRegistry", b"MoNoon.EditorExtensions",
                  b"ScriptReload", b"Scripts reloaded", b"Script reload failed")


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message, flush=True)
    if not ok:
        failures.append(message)


env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_EXT_OUTPUT=str(out), MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    code = subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=900).returncode
if (out / "error.txt").exists():
    raise SystemExit("FAIL editor: " + (out / "error.txt").read_text(encoding="utf-8"))
result = json.loads((out / "extensions.json").read_text(encoding="utf-8"))
check(code == 0, f"editor exited with {code}")
check(result["extension_menu"] == ["Place Coin"] and result["window_menu_has_coins"], "Project → Extensions → Place Coin and Window → Coins come from Editor/KeyRoomTools.cs")
place = result["place_coin"]
check(place["after"] == place["before"] + 1 and place["prefab"], f"Place Coin placed one Coin prefab instance ({place})")
check(result["undo_both"] == place["before"], "Undo removes both placements")
for width in (1280, 1600, 1920):
    rows = result[f"inspector_{width}"]
    check(rows["label_drift"] == 0 and rows["input_drift"] == 0 and
          all(r["column"] == rows["name_column"] for r in rows["rows"]),
          f"{width}px: extension Inspector rows share the LootTable row's label and input columns ({rows['rows']})")
    panel = result[f"panel_{width}"]
    check(panel["input_drift"] == 0 and panel["padding"] == panel["inspector_padding"] and
          all(r["column"] == panel["inspector_name_column"] for r in panel["rows"]),
          f"{width}px: Coins panel rows use the Inspector padding and label column ({panel['padding']} / {panel['rows'][0]['column']})")
check(result["inspector_coin_text"].endswith("in this scene"), f"Inspector row text: {result['inspector_coin_text']}")
check(result["panel_place"]["shown"] == str(result["panel_place"]["after"]), f"panel button placed a coin and the count updated ({result['panel_place']})")
error = result["extension_compile_error"]
check(error["File"] and error["File"].endswith("Editor/Broken.cs") and error["Line"] == 3 and error["menu"] == 0 and not error["panel"],
      f"an Editor/ compile error is a clickable Console row at Broken.cs:3 and unloads the extensions ({error['Message']})")
check(result["extension_restored"] == ["Place Coin"], "fixing the error restores the extension")


def build(mode):
    target = out / mode
    # MONOON_CLI: a packaged MoNoon.Cli(.dll) where there is no source checkout (x64 guests).
    cli = os.environ.get("MONOON_CLI")
    command = (["dotnet", cli] if cli.endswith(".dll") else [cli]) if cli else [str(repo / "monoon")]
    with (out / f"build-{mode}.log").open("wb") as log:
        subprocess.run([*command, "build", str(project), mode, "--output", str(target)],
                       cwd=repo, stdout=log, stderr=log, timeout=1800, check=True)
    return target


for mode in ("release", "native-aot"):
    target = build(mode)
    files = [p for p in target.rglob("*") if p.is_file()]
    names = [str(p.relative_to(target)) for p in files if any(n in p.name for n in FORBIDDEN_FILES)]
    sources = [str(p.relative_to(target)) for p in files if p.suffix == ".cs" or "/Editor/" in p.as_posix() or "/Tests/" in p.as_posix()]
    text = sorted({f"{p.relative_to(target)}:{t.decode()}" for p in files if p.stat().st_size < 400_000_000
                   for t in FORBIDDEN_TEXT if t in p.read_bytes()})
    check(not names, f"{mode}: no editor, authoring, Avalonia, SkiaSharp or Roslyn files ({len(files)} files) {names[:5]}")
    check(not sources, f"{mode}: no C# sources and no Editor/ or Tests/ folder {sources[:5]}")
    check(not text, f"{mode}: no extension or script reload code {text[:5]}")
    (out / f"{mode}-files.txt").write_text("\n".join(sorted(str(p.relative_to(target)) for p in files)) + "\n", encoding="utf-8")

if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS editor extensions in", out)

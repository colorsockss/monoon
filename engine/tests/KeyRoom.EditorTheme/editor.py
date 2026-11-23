"""Capture the packaged editor with KeyRoom and its separate windows, then check theme tokens and Inspector alignment."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

WINDOWS = ("console", "animator", "clip", "sheet-picker", "input-actions", "data", "table", "dialogue", "tileset")

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
out.mkdir(parents=True)
project = out / "KeyRoom"
shutil.copytree(sample, project, ignore=shutil.ignore_patterns("bin", "obj", ".monoon"))
env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project),
           MONOON_THEME_EDITOR=str(out), MONOON_USER_DATA=str(out / "profile"))
with (out / "editor.log").open("wb") as log:
    subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=900, check=True)
if (out / "error.txt").exists():
    raise SystemExit((out / "error.txt").read_text(encoding="utf-8"))
result = json.loads((out / "result.json").read_text(encoding="utf-8"))
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message)
    if not ok:
        failures.append(message)


for view in result["inspections"]:
    rows = view["rows"]
    label_x = sorted({round(r["label_x"] * 2) / 2 for r in rows})
    field_x = sorted({round(r["field_x"] * 2) / 2 for r in rows})
    widths = sorted({r["label_width"] for r in rows})
    offset = max(r["center_offset"] for r in rows)
    clipped = [r["text"] for r in rows if r["natural"] > r["label_width"] + .5]
    bare = [r["text"] for r in rows if r["natural"] > r["label_width"] + .5 and not (r["trimmed"] and r["tip"])]
    print(f"{view['capture']}: window {view['window_width']:.0f}px, Inspector {view['inspector_width']:.0f}px, "
          f"{len(rows)} rows, label x {label_x}, field x {field_x}, label width {widths}, clipped {clipped}")
    check(len(rows) >= 8, f"{view['capture']} measures {len(rows)} Inspector rows")
    check(len(label_x) == 1 and len(field_x) == 1, f"{view['capture']} labels and fields share one left edge")
    check(offset <= 1, f"{view['capture']} label and field centers differ by at most 1px ({offset}px)")
    check(not bare, f"{view['capture']} clipped labels trim with a tooltip {bare}")
    heights = sorted({r["field_height"] for r in rows if r["field_type"] in ("TextBox", "NumericUpDown", "ComboBox", "Button")})
    check(len(heights) == 1, f"{view['capture']} input fields share one height {heights}")

name = result["long_name"]
h = name["hierarchy"]
check(h["trimmed"] and h["tip"] and h["inside"] and h["shown"] < h["natural"],
      f"long Hierarchy name trims inside the tree with a tooltip ({h['shown']} of {h['natural']}px)")
check(name["inspector_width_before"] == name["inspector_width_after"],
      f"long name keeps the Inspector width ({name['inspector_width_after']}px)")
shown = [w["name"] for w in result["windows"]]
check(tuple(shown) == WINDOWS, f"captured editor panels and dialogs {shown}")
fonts = {w["name"]: w["font"] for w in result["windows"]}
check(result["window_font"] == 12 and set(fonts.values()) == {12}, f"main window, editor panels and dialogs use 12px text {fonts}")
check(result["tokens"].get("EditorFontSize") == "12", f"design tokens {result['tokens']}")
check(not result["missing_icons"], f"{result['icons']} icon geometries, missing {result['missing_icons']}")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS editor theme captures in", out)

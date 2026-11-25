"""Build KeyRoom from the editor's Build Game dialog and from `monoon build` with the saved settings, then compare the outputs."""
import hashlib
import json
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import tempfile

editor, sample, hook, out = [Path(p).resolve() for p in sys.argv[1:]]
repo = Path(__file__).resolve().parents[3]
out.mkdir(parents=True)
project = out / "KeyRoom"
shutil.copytree(sample, project, ignore=shutil.ignore_patterns("bin", "obj", ".monoon"))
builds = out / "game-builds" / "KeyRoom"  # GameBuild.DefaultOutputDirectory for out/KeyRoom
base_env = dict(os.environ, MONOON_USER_DATA=str(out / "profile"))  # no signing.yaml: ad-hoc signature, no notarisation
RECEIPT = "MoNoonBuild"
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message, flush=True)
    if not ok:
        failures.append(message)


def run_editor(step):
    env = dict(base_env, DOTNET_STARTUP_HOOKS=str(hook), MONOON_QA_PROJECT=str(project), MONOON_BUILD_EDITOR=str(out), MONOON_BUILD_STEP=step)
    with (out / f"editor-{step}.log").open("wb") as log:
        subprocess.run([str(editor), str(project)], env=env, stdout=log, stderr=log, timeout=900 * float(os.environ.get('MONOON_QA_SLOW') or 1))
    if (out / "error.txt").exists():
        raise SystemExit(f"FAIL editor {step}: " + (out / "error.txt").read_text(encoding="utf-8"))
    return json.loads((out / f"result-{step}.json").read_text(encoding="utf-8"))


def cli(label, *args):
    with (out / f"cli-{label}.log").open("wb") as log:
        return subprocess.run([str(repo / "monoon"), "build", str(project), *args], env=base_env, stdout=log, stderr=subprocess.STDOUT, timeout=3600).returncode


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def tree(root):
    """Every entry under root: file hash and executable bit, link target, or directory. Hidden build bookkeeping is skipped."""
    entries = {}
    for directory, dirs, files in os.walk(root):
        if Path(directory) == root:  # the content cache and staging folders beside the products are not outputs
            dirs[:] = [d for d in dirs if not d.startswith(".monoon")]
        for name in dirs + files:
            path = Path(directory) / name
            rel = path.relative_to(root).as_posix()
            if name.startswith(".") and name.endswith(".publish-lock"):
                continue
            if path.is_symlink():
                entries[rel] = (("link", os.readlink(path)), path)
            elif path.is_dir():
                entries[rel] = (("dir",), path)
            else:
                entries[rel] = (("file", sha(path), bool(path.stat().st_mode & 0o111)), path)
    return entries


def build_id(root, product):
    receipt = root / product / "Contents" / "MacOS" / RECEIPT
    return next(l.split(":", 1)[1].strip().strip('"') for l in receipt.read_text().splitlines() if l.strip().startswith("buildId:"))


def expanded(root):
    """The packages as their contents: ditto zip and dmg are expanded, so the comparison is per file inside them."""
    work = Path(tempfile.mkdtemp(dir=out, prefix=".expand-"))
    def packages(suffix):
        return sorted(p for p in root.rglob("*" + suffix) if not any(part.startswith(".") for part in p.relative_to(root).parts))
    for package in packages(".zip"):
        subprocess.run(["/usr/bin/ditto", "-x", "-k", str(package), str(work / package.relative_to(root))], check=True)
    for image in packages(".dmg"):
        mount = work / (image.name + ".mount")
        mount.mkdir()
        subprocess.run(["/usr/bin/hdiutil", "attach", "-quiet", "-nobrowse", "-readonly", "-mountpoint", str(mount), str(image)], check=True)
        try:
            shutil.copytree(mount, work / image.relative_to(root), symlinks=True, ignore=shutil.ignore_patterns(".fseventsd", ".Trashes", ".DS_Store"))
        finally:
            subprocess.run(["/usr/bin/hdiutil", "detach", "-quiet", str(mount)], check=True)
        mount.rmdir()
    return work


def normalised(root, ident):
    """Entries keyed with the random build ID replaced, so both builds name their symbol folder the same way."""
    return {rel.replace(ident, "<buildId>"): value for rel, value in tree(root).items()}


def without_build_ids(path):
    text = path.read_text()
    # Native AOT: nativeUuid is the linker's LC_UUID and playerSha256 hashes the player that contains it.
    ids = ("buildId:", "nativeUuid:", "playerSha256:") if "mode: \"native-aot\"" in text else ("buildId:",)
    return [l for l in text.splitlines() if not l.strip().startswith(ids)]


def without_uuid(data):
    """A 64-bit Mach-O file with its LC_UUID load command zeroed."""
    data = bytearray(data)
    offset = 32
    for _ in range(int.from_bytes(data[16:20], "little")):
        cmd, size = int.from_bytes(data[offset:offset + 4], "little"), int.from_bytes(data[offset + 4:offset + 8], "little")
        if cmd == 0x1B:
            data[offset + 8:offset + 24] = bytes(16)
        offset += size
    return hashlib.sha256(data).hexdigest()


def native(rel):
    return "NativeAOT/" in rel or "/native-aot/" in rel


def unsigned(path, rel):
    copy = Path(tempfile.mkdtemp(dir=out, prefix=".unsign-")) / path.name
    shutil.copy2(path, copy)
    subprocess.run(["/usr/bin/codesign", "--remove-signature", str(copy)], check=True)
    digest = without_uuid(copy.read_bytes()) if native(rel) else sha(copy)
    shutil.rmtree(copy.parent)
    return digest


def seal(path):
    files = plistlib.loads(path.read_bytes())
    for section in ("files", "files2"):
        files[section] = {k: v for k, v in files[section].items() if not k.endswith("MacOS/" + RECEIPT)}
    return files


# The only differences allowed between two builds of the same project and settings, each with the reason it must differ.
# Native AOT: ld derives LC_UUID from the debug map of the object files, which lie in a new temporary folder per build.
ALLOWED = [
    ("the receipt's buildId is a new random ID per build (it names the symbol folder); Native AOT also records the linker UUID; "
     "every other receipt line must match",
     lambda rel: rel.endswith("/" + RECEIPT), lambda a, b: without_build_ids(a) == without_build_ids(b)),
    ("the bundle seal hashes the receipt; every other sealed file hash must match",
     lambda rel: rel.endswith("Contents/_CodeSignature/CodeResources"), lambda a, b: seal(a) == seal(b)),
    ("the main executable's embedded ad-hoc signature covers the seal (Native AOT: and the linker UUID); "
     "the executable without its signature (and LC_UUID) must match",
     lambda rel: rel.endswith("Contents/MacOS/MoNoonPlayer"), lambda a, b: unsigned(a, str(a)) == unsigned(b, str(b))),
    ("the dSYM carries the executable's linker UUID; the debug information without LC_UUID must match",
     lambda rel: native(rel) and rel.endswith(".dSYM/Contents/Resources/DWARF/MoNoonPlayer"),
     lambda a, b: without_uuid(a.read_bytes()) == without_uuid(b.read_bytes())),
    ("the dSYM relocation map names the temporary link folder; every other line must match",
     lambda rel: native(rel) and rel.endswith(".dSYM/Contents/Resources/Relocations/aarch64/MoNoonPlayer.yml"),
     lambda a, b: [l for l in a.read_text().splitlines() if not l.startswith("binary-path:")]
                  == [l for l in b.read_text().splitlines() if not l.startswith("binary-path:")]),
    ("zip and dmg containers store file times; their expanded contents are compared file by file under package:",
     lambda rel: not rel.startswith("package:") and rel.endswith((".zip", ".dmg")), lambda a, b: True),
]


def compare(label, editor_root, cli_root, product):
    ids = build_id(editor_root, product), build_id(cli_root, product)
    a, b = normalised(editor_root, ids[0]), normalised(cli_root, ids[1])
    ea, eb = expanded(editor_root), expanded(cli_root)
    a.update({"package:" + k: v for k, v in normalised(ea, ids[0]).items()})
    b.update({"package:" + k: v for k, v in normalised(eb, ids[1]).items()})
    check(sorted(a) == sorted(b), f"{label}: same {len(a)} entries (missing in CLI {sorted(set(a) - set(b))[:5]}, extra {sorted(set(b) - set(a))[:5]})")
    leaked = [k for k in a if k.split("/")[-1] == "BuildSettings"
              or k.split("/")[-1].startswith(("Avalonia", "SkiaSharp", "libSkiaSharp", "Engine.Authoring", "Engine.Editor"))]
    check(not leaked, f"{label}: no BuildSettings or editor files in the outputs {leaked[:5]}")
    differing = sorted(k for k in set(a) & set(b) if a[k][0] != b[k][0])
    explained, unexplained = {}, []
    for rel in differing:
        rule = next(((reason, same) for reason, match, same in ALLOWED if match(rel)), None)
        if rule and rule[1](a[rel][1], b[rel][1]):
            explained[rel] = rule[0]
        else:
            unexplained.append(rel)
    report = {"label": label, "build_ids": ids, "entries": len(a), "identical": len(set(a) & set(b)) - len(differing),
              "explained": explained, "unexplained": unexplained}
    (out / f"compare-{label}.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"{label}: {report['identical']} of {len(a)} entries byte-identical; {len(explained)} differ only in build ID or signature:", flush=True)
    for rel, reason in explained.items():
        print(f"  {rel}: {reason}")
    check(not unexplained, f"{label}: no other differences {unexplained}")
    shutil.rmtree(ea)
    shutil.rmtree(eb)
    return report


def layout(view):
    rows = view["rows"]
    label_x = {r["label_x"] for r in rows}
    field_x = {r["field_x"] for r in rows}
    offset = max(r["center_offset"] for r in rows)
    print(f"{view['capture']}: main {view['main_width']:.0f}px, dialog {view['dialog_width']:.0f}x{view['dialog_height']:.0f}, "
          f"{len(rows)} rows, label x {sorted(label_x)}, field x {sorted(field_x)}, max centre offset {offset}")
    check([r["text"] for r in rows] == ["Start scene", "Platform", "Build type", "Bundles", "Content", "Profile", "Package", "Icon"],
          f"{view['capture']} rows in order")
    check(len(label_x) == 1 and len(field_x) == 1, f"{view['capture']} labels and fields share one left edge")
    check(offset <= 1, f"{view['capture']} label and field centres within 1px ({offset}px)")
    combo = {r["field_height"] for r in rows if r["field_type"] == "ComboBox"}
    check(len(combo) == 1, f"{view['capture']} combo boxes share one height {sorted(combo)}")
    check(len({b["y"] for b in view["buttons"]}) == 1, f"{view['capture']} buttons share one baseline {view['buttons']}")


# 1. Release: dialog layout, Save Settings round trip, Build with packages.
release = run_editor("release")
default = {"platform": "osx-arm64", "build_type": "Release", "profile": "Distribution", "package": False, "full_content": False}
check({k: release["initial"][k] for k in default} == default, f"first dialog shows the defaults {release['initial']}")
for view in release["layouts"]:
    layout(view)
a, b = release["layouts"]
check([(r["label_x"], r["field_x"], r["field_height"]) for r in a["rows"]] == [(r["label_x"], r["field_x"], r["field_height"]) for r in b["rows"]]
      and a["dialog_width"] == b["dialog_width"], "dialog rows identical with the main window at 1280px and 1920px")
check(release["reopened"] == release["chosen"], f"reopened dialog shows the saved choices {release['reopened']}")
check(release["chosen"]["package"] and release["chosen"]["icon"] == release["game_icon"], f"package on and icon {release['game_icon']} saved")
saved_release = (project / "BuildSettings").read_text()
check(saved_release == "target: osx-arm64\ntype: release\nprofile: distribution\ncontent: incremental\npackage: true\n", "BuildSettings after Release:\n" + saved_release)
check(release["status"].startswith("Built "), "editor status " + release["status"])


def asset_summary(step):
    console = step["console"]
    check(any(line.startswith("Info: Shipped ") for line in console) and any(line.startswith("Info: Unreferenced under Assets/: 3 files") for line in console)
          and console[-1].startswith("Info: Asset report: ") and console[-1].endswith("/assets.yaml"), "Console shows the asset report summary and path")


asset_summary(release)
product = "KeyRoom.app"
check(all((builds / f).exists() for f in (product, "KeyRoom-osx-arm64.zip", "KeyRoom-osx-arm64.dmg")), "editor Release made the app, zip and dmg")
builds.rename(out / "editor-release")
check(cli("release") == 0, "./monoon build <project> with the saved Release settings")
check((project / "BuildSettings").read_text() == saved_release, "CLI leaves BuildSettings unchanged")
release_report = compare("release", out / "editor-release", builds, product)
builds.rename(out / "cli-release")

# 2. Native AOT after an editor restart.
aot = run_editor("aot")
check({k: aot["reopened"][k] for k in default} == {k: release["chosen"][k] for k in default}, "settings survive an editor restart")
check(aot["foreign_aot"]["platform"] == "win-x64" and aot["foreign_aot"]["build_types"] == ["Release"] and aot["foreign_aot"]["build_type"] == "Release",
      f"saved win-x64 Native AOT falls back to Release in the dialog {aot['foreign_aot']}")
check(aot["linux_build_types"] == ["Release"], "Native AOT offered only for the host platform")
asset_summary(aot)
saved_aot = (project / "BuildSettings").read_text()
check(saved_aot == saved_release.replace("type: release", "type: native-aot"), "BuildSettings after Native AOT:\n" + saved_aot)
check(all((builds / "NativeAOT" / f).exists() for f in (product, "KeyRoom-osx-arm64.zip", "KeyRoom-osx-arm64.dmg")), "editor Native AOT made the app, zip and dmg")
builds.rename(out / "editor-aot")
check(cli("aot") == 0, "./monoon build <project> with the saved Native AOT settings")
aot_report = compare("native-aot", out / "editor-aot", builds, "NativeAOT/" + product)
builds.rename(out / "cli-aot")

# 3. A saved Native AOT target for another OS fails clearly in the CLI and leaves the file as it was.
foreign = "target: win-x64\ntype: native-aot\n"
(project / "BuildSettings").write_text(foreign)
code = cli("foreign-aot")
message = (out / "cli-foreign-aot.log").read_text()
check(code == 1 and "Build the win-x64 Native AOT player on a matching host." in message, f"CLI rejects saved win-x64 Native AOT (exit {code})")
check((project / "BuildSettings").read_text() == foreign, "failed CLI build leaves BuildSettings unchanged")
(project / "BuildSettings").write_text(saved_aot)

(out / "summary.json").write_text(json.dumps({"release": release_report, "native_aot": aot_report, "failures": failures}, indent=2), encoding="utf-8")
if failures:
    raise SystemExit(f"FAIL: {len(failures)} check(s)")
print("PASS editor Build and monoon build with saved settings in", out)

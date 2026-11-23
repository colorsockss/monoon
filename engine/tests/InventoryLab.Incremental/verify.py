"""Build and replay a prepared InventoryLab project on the current OS, using a temporary copy."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tempfile
import zipfile

BEFORE = "어서 오세요. 무슨 일로 오셨나요?"
AFTER = "무슨 일로 오셨나요? 어서 오세요."


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as file:
        for block in iter(lambda: file.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def replace(path, before, after):
    data = path.read_bytes()
    if data.count(before.encode()) != 1:
        raise ValueError(f"Expected exactly one match in {path}")
    path.write_bytes(data.replace(before.encode(), after.encode()))


def verify(cli, demo, fixtures, dest, dependencies=False):
    dest.mkdir(parents=True, exist_ok=False)
    saves = {}
    environment = platform.system()
    rid = {"Darwin": "osx-arm64", "Linux": "linux-x64", "Windows": "win-x64"}[environment]
    arch = platform.machine().lower()
    if arch not in (("arm64",) if rid == "osx-arm64" else ("amd64", "x86_64")):
        raise ValueError(f"Unexpected architecture {arch}")
    with tempfile.TemporaryDirectory(dir=dest.parent) as scratch:
        root = Path(scratch)
        project = root / "InventoryLab"
        shutil.copytree(demo, project)
        story = project / "Assets/Dialogue/Herbalist.data"
        def build(label, mode="incremental", fresh=False):
            output = root / ("fresh" if fresh else "builds")
            print("Building", rid, label, flush=True)
            cmd = [cli, "build", project, "release", "--target", rid, "--output", output, "--content", mode]
            proc = subprocess.run(cmd, capture_output=True, encoding="utf-8", errors="replace")
            log = proc.stderr + proc.stdout
            (dest / (label + ".log")).write_text(log, encoding="utf-8")
            if proc.returncode:
                raise RuntimeError(log)
            decisions = [line for line in log.splitlines() if line.startswith(("reuse ", "build "))]
            shutil.copyfile(next(output.glob(".monoon-build-cache/**/report.yaml")), dest / (label + ".yaml"))
            print(*decisions, sep="\n", flush=True)
            product = output / ("InventoryLab.app/Contents/MacOS" if rid == "osx-arm64" else "InventoryLab-" + rid)
            return product, decisions

        def files(product):
            return {p.relative_to(product).as_posix(): digest(p)
                    for name in ("Game", "ContentBundles", "Mods") for p in sorted((product/name).rglob("*"))
                    if p.is_file()}

        def replay(product, label):
            exe = product / ("MoNoonPlayer.exe" if rid == "win-x64" else "MoNoonPlayer")
            profile = root / label
            env = dict(os.environ, MONOON_USER_DATA=str(profile), SDL_VIDEO_DRIVER="monoon-no-video",
                       SDL_AUDIO_DRIVER="monoon-no-audio")
            subprocess.run([exe, "--replay-input", fixtures/"Herbalist.inputreplay", profile], env=env, check=True)
            slot = profile / "slot-001.save"
            if slot.read_bytes() != (fixtures/"Herbalist.save").read_bytes():
                raise AssertionError(label + " quest reward/save mismatch")
            shutil.copy2(slot, dest/(label + ".save"))
            saves[label] = digest(slot)

        product, cold = build("cold")
        if len(cold) != 7 or any(not s.startswith("build ") for s in cold):
            raise AssertionError(("cold", cold))
        shutil.copyfile(product/"Game/Assets/MarketArt/Torch.shader.frames.png", dest/"torch.png")
        baseline = files(product)
        replace(story, BEFORE, AFTER)
        product, decisions = build("dialogue")
        rebuilt = [s for s in decisions if s.startswith("build ")]
        reused = [s for s in decisions if s.startswith("reuse ")]
        if (len(rebuilt) != 1 or "bundle Assets/Dialogue" not in rebuilt[0] or
                "changed: Assets/Dialogue/Herbalist.data" not in rebuilt[0] or len(reused) != 6):
            raise AssertionError(decisions)
        bundle = product / "ContentBundles/Assets/Dialogue.bundle"
        with zipfile.ZipFile(bundle) as z:
            if AFTER.encode() not in z.read("Assets/Dialogue/Herbalist.data"):
                raise AssertionError("Updated line missing from shipped dialogue")
        incremental = files(product)
        if baseline == incremental:
            raise AssertionError("Content digest did not change")
        replay(product, "incremental")
        product, full = build("full", "full", fresh=True)
        if len(full) != 7 or any("full content build" not in s for s in full):
            raise AssertionError(("full",full))
        complete = files(product)
        if incremental != complete:
            raise AssertionError({k for k in incremental.keys() | complete.keys() if incremental.get(k) != complete.get(k)})
        replay(product, "full")
        for label, content in [("incremental", incremental), ("full", complete)]:
            (dest/(label+".sha256")).write_bytes("".join(f"{v}  {k}\n" for k,v in sorted(content.items())).encode())
        summary = dict(rid=rid, os=platform.platform(), architecture=arch, cold=len(cold), rebuilt=len(rebuilt),
                       reused=len(reused), files=len(complete), contentSha256=digest(dest/"full.sha256"),
                       inputSha256=digest(fixtures/"Herbalist.inputreplay"), saves=saves)
        if dependencies:
            replace(story, AFTER, AFTER + " 봄볕")
            replace(project/"Assets/MarketArt/Torch.shader", "fps: 24", "fps: 12")
            replace(project/"Scenes/Inventory.scene", "{index: 31, tile: 1}", "{index: 30, tile: 1}")
            product, changes = build("dependencies")
            rebuilt = [row for row in changes if row.startswith("build ")]
            for fragments in [("shader ", "changed: Assets/MarketArt/Torch.shader"),
                              ("(0,0)", "changed: cells"), ("(1,0)", "changed: neighbour borders"),
                              ("bundle Assets/Dialogue", "changed: Assets/Dialogue/Herbalist.data")]:
                if not any(all(part in line for part in fragments) for line in rebuilt):
                    raise AssertionError((fragments,changes))
            if len(rebuilt) != 4 or not any("(3,0)" in row and row.startswith("reuse ") for row in changes):
                raise AssertionError(changes)
            changed_content = files(product)
            # Damage only the test's cached sheet; the published game remains intact.
            sheet = product / "Game/Assets/MarketArt/Torch.shader.frames.png"
            blob = next((root/"builds/.monoon-build-cache").glob("**/blobs/" + digest(sheet).upper()))
            blob.write_bytes(b"damaged test cache")
            product, repair = build("repair")
            rebuilt = [row for row in repair if row.startswith("build ")]
            if len(rebuilt) != 1 or "cached file missing or damaged" not in rebuilt[0] or files(product) != changed_content:
                raise AssertionError(repair)
            reference, _ = build("dependency-full", "full", fresh=True)
            if changed_content != files(reference):
                raise AssertionError("Modified dialogue, shader or autotile seam differs from full build")
            summary["dependencyRebuilds"] = 4
            summary["repairedCacheFiles"] = 1
        (dest/"result.json").write_text(json.dumps(summary, indent=2),encoding="utf-8")
        print(json.dumps(summary),flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("cli", "demo", "fixtures", "results"):
        parser.add_argument(name, type=Path)
    parser.add_argument("--dependencies", action="store_true", help="Also change dialogue, shader and seam inputs and damage a cached sheet")
    args=parser.parse_args()
    verify(*(path.resolve() for path in (args.cli,args.demo,args.fixtures,args.results)), dependencies=args.dependencies)

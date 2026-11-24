"""Re-run KeyRoom on the two provisioned, powered-off x64 VMs on this Mac."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tarfile
import tempfile
import time

REPO = Path(__file__).resolve().parents[3]
LAB = REPO / ".test-env"
BUILDS = Path(os.environ.get("MONOON_QA_BUILDS", REPO / "game-builds/KeyRoom")).resolve()


def run(*args, **kwargs):
    return subprocess.run(args, check=True, **kwargs)


def wait_for(test, seconds, failure):
    until = time.monotonic() + seconds
    while not test():
        if time.monotonic() >= until:
            raise RuntimeError(failure)
        time.sleep(3)


def attest(guest, output):
    """Compare what the guest saw to the actual published game on this Mac."""
    root = BUILDS / ("KeyRoom-win-x64" if guest == "windows" else "KeyRoom-linux-x64")
    report = json.loads((output / "results.json").read_text())
    def sha(path):
        return hashlib.sha256(path.read_bytes()).hexdigest()
    data = root / "Game"
    patterns = ("*.scene", "*.prefab", "*.clip", "*.tileset", "*.data", "*.meta", "GameSettings", ".monoon-assets")
    docs = {p.relative_to(data).as_posix(): sha(p) for pattern in patterns for p in data.rglob(pattern)}
    if report["yaml_sha256"] != docs or list(data.rglob("*.json")):
        raise RuntimeError("Project data in the guest does not match the YAML export")
    if (data / "Assets/Room.tileset").exists() and not all(route.get("tile_wall") for route in report["routes"]):
        raise RuntimeError("Missing tile collision verification")
    expected = {"core_sha256": "Engine.Core.dll", "engine_sha256": "Engine.Desktop.dll",
                "executable_sha256": "MoNoonPlayer.exe" if guest == "windows" else "MoNoonPlayer"}
    if guest == "windows":
        expected["sdl_sha256"] = "SDL3.dll"
    scripts = data / ".monoon-compiled"
    bundle = scripts / "MoNoon.GameScripts.dll"
    if not bundle.is_file():
        bundle = data.parent / "MoNoon.GameScripts.dll"  # per-game Release publish (roadmap 15.7)
    if not bundle.is_file():
        bundle = scripts / "ToggleControls.dll"
    if bundle.is_file():
        expected["button_script_sha256"] = bundle.relative_to(root).as_posix()
        attest_controls(report.get("controls_ui", []), output / "controls")
    if any(report[key] != sha(root / name) for key, name in expected.items()):
        raise RuntimeError("Game binaries in the guest do not match the export")
    if [r["controls"] for r in report["routes"] if r["room"] == "ROOM 2 / KEY"] != ["arrows", "wasd"]:
        raise RuntimeError("Both input routes must reach room two")
    for mode in ("arrows", "wasd"):
        for frame in ("01-start", "02-key", "03-room-two"):
            if not (output / mode / (frame + ".png")).is_file():
                raise RuntimeError("Missing game window capture: " + frame)
    print(f"{guest}: guest binaries and {len(docs)} YAML documents match. Both routes passed.", flush=True)


def attest_controls(rows, shots):
    cases = [("base", [800,450], [0,0,800,450]), ("wide", [960,432], [96,0,864,432]),
             ("tall", [640,600], [0,120,640,480])]
    if len(rows) != len(cases):
        raise RuntimeError("Missing Controls window sizes")
    for row, (name, size, viewport) in zip(rows, cases):
        x,y,r,b = viewport
        expected = [round(x+u*(r-x)/800) if i%2 == 0 else round(y+u*(b-y)/450)
                    for i,u in enumerate((698,14,786,42))]
        actual = row["button_measured"]
        if (row["size"] != size or row["viewport"] != viewport or len(actual) != 4 or
                any(abs(v-e)>1 for v,e in zip(actual,expected)) or
                row["keyboard"] != ["focus","open","close"] or
                set(row["outside_clicks"]) != ({"left","right","top","bottom"} | ({"bar"} if name != "base" else set()))):
            raise RuntimeError(f"Unexpected Controls geometry/input record: {name}")
        l,t,r,b = actual
        def inside(point):
            return l <= point[0] < r and t <= point[1] < b
        if (len(row["inside_clicks"]) != 2 or not all(map(inside,row["inside_clicks"])) or
                any(map(inside,row["outside_clicks"].values()))):
            raise RuntimeError(f"Incorrect Controls click probes: {name}")
        states = ["01-idle", "02-tab-focus", "03-key-open", "04-key-close",
                  "05-click-open", "06-click-close", "07-misses-closed"]
        for side in row["outside_clicks"]:
            states.extend(["armed-" + side, "miss-" + side])
        for shot in states:
            if not (shots / name / (shot + ".png")).is_file():
                raise RuntimeError(f"Missing Controls capture: {name}/{shot}")
    print("Controls: keyboard, mouse and hit bounds verified at all three sizes.", flush=True)


def linux(output):
    vm = LAB / "linux"
    ssh = ["ssh", "-F", str(vm / "ssh.conf"), "monoon-qa"]
    scp = ["scp", "-q", "-F", str(vm / "ssh.conf")]
    root = BUILDS / "KeyRoom-linux-x64"
    with tempfile.TemporaryDirectory(dir=LAB) as tmp:
        package = Path(tmp) / "game.tgz"
        with tarfile.open(package, "w:gz", compresslevel=1) as tar:
            tar.add(root, arcname=root.name)
            tar.add(Path(__file__).with_name("verify.py"), arcname="verify.py")
        run(vm / "start.sh")
        wait_for(lambda: subprocess.run([*ssh, "true"], capture_output=True).returncode == 0, 240, "SSH not ready")
        try:
            remote = run(*ssh, "mktemp -d yaml-test.XXXXXXXX", capture_output=True, text=True).stdout.strip()
            if not remote.startswith("yaml-test.") or not remote.removeprefix("yaml-test.").isalnum():
                raise RuntimeError("Unexpected guest directory")
            run(*scp, package, "monoon-qa:" + remote + "/")
            with (output / "verify.log").open("w") as log:
                run(*ssh, f'cd {remote} && tar xzf game.tgz && SDL_AUDIODRIVER=dummy xvfb-run -a -s "-screen 0 1280x900x24" python3 -u verify.py KeyRoom-linux-x64 results', stdout=log, stderr=subprocess.STDOUT)
            run(*scp, "-r", f"monoon-qa:{remote}/results/.", str(output))
            run(*ssh, f"rm -r -- {remote}")
        finally:
            subprocess.run([*ssh, "sudo -n shutdown -h now"])
        wait_for(lambda: not (vm / "qemu.pid").exists(), 120, "Debian did not finish shutting down")


def windows(output):
    vm = LAB / "windows"
    run("python3", vm / "prepare-usb.py")
    run(vm / "start.sh")
    finish_windows(vm, output)


def finish_windows(vm, output):
    wait_for(lambda: not (vm / "qemu.pid").exists(), 900, "Windows has not shut down; inspect its console before touching the USB")
    collect_windows(vm, output)


def collect_windows(vm, output):
    run("python3", vm / "require-off.py")
    img = vm / "qa-usb.img"
    for name in ("verify.log", "windows.txt", "exit.txt"):
        run("mcopy", "-i", img, "::" + name, output / name)
    run("mcopy", "-s", "-i", img, "::results", output)
    for src in (output / "results").iterdir():
        shutil.move(src, output / src.name)
    (output / "results").rmdir()
    if (output / "exit.txt").read_text().strip() != "0":
        raise RuntimeError("Guest test failed; see verify.log")


if __name__ == "__main__":
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("guest", choices=["windows", "linux"])
    p.add_argument("out", type=Path, help="New or empty result directory")
    p.add_argument("--attest-only", action="store_true", help="Compare a completed guest run to the current export")
    args = p.parse_args()
    out = args.out.resolve()
    if not args.attest_only:
        vm = LAB / args.guest
        if not (vm / "start.sh").is_file() or (vm / "qemu.pid").exists():
            p.error("Use the provisioned, powered-off VM")
        out.mkdir(parents=True, exist_ok=True)
        if any(out.iterdir()):
            p.error("Choose an empty result directory")
        (windows if args.guest == "windows" else linux)(out)
    attest(args.guest, out)

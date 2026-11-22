"""Drive the exported AudioLab window. Pass the player, VirtualPad.dll and a fresh result directory."""
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import subprocess as sp
import sys
import time

exe, hook, output = (Path(arg).resolve() for arg in sys.argv[1:4])
output.mkdir(parents=True, exist_ok=False)
mac = sys.platform == "darwin"
if mac:
    helper = Path(sys.argv[4]).resolve()
else:
    sys.path[:0] = [str(Path(__file__).resolve().parent), str(Path(__file__).resolve().parents[1] / "KeyRoom.Desktop")]
    from verify import WindowsWindow, X11Window
    if sys.platform == "win32":
        ctypes.windll.user32.SetProcessDPIAware()
        console = ctypes.windll.kernel32.GetConsoleWindow()
        if console:
            ctypes.windll.user32.ShowWindow(console, 0)

def play(device, restored=False):
    profile = output / (device + "-profile")
    stage = output / (device + ("-reopened" if restored else "-first"))
    stage.mkdir()
    log = stage / "player.log"
    env = dict(os.environ, MONOON_USER_DATA=str(profile))
    pad = device == "gamepad" and not restored
    if pad:
        env.update(DOTNET_STARTUP_HOOKS=str(hook), MONOON_TEST_PAD=str(stage))
    with log.open("wb") as stream:
        proc = sp.Popen([str(exe)], cwd=exe.parent, env=env, stdout=stream, stderr=sp.STDOUT)
    def levels():
        return re.findall(r"Mixer: music=([\d.]+) effects=([\d.]+) master=([\d.]+)", log.read_text(errors="replace"))
    def wait(fn, item):
        until = time.monotonic() + 150
        while time.monotonic() < until:
            if fn(): return
            if (stage / "pad-error").exists(): raise RuntimeError((stage / "pad-error").read_text())
            if proc.poll() is not None: break
            time.sleep(.12)
        raise RuntimeError(f"{device}/{item}: {log.read_text(errors='replace')[-1300:]}")
    def level(music, fx="0.75", master="0.8"):
        expected = (music, fx, master)
        wait(lambda: levels()[-1:] == [expected], str(expected))
    try:
        if mac:
            def command(*args):
                return sp.run([helper, args[0], str(proc.pid), *map(str, args[1:])], capture_output=True, text=True)
            wait(lambda: command("focus").returncode == 0, "window focus")
            def key(k):
                command("focus").check_returncode()
                command(k).check_returncode()
            def click(x, y):
                command("focus").check_returncode()
                command("click", x, y).check_returncode()
            def shot():
                wid = command("focus").stdout.strip()
                sp.run(["screencapture", "-x", "-o", "-l", wid, stage / "window.png"], check=True)
        else:
            window = (WindowsWindow if sys.platform == "win32" else X11Window)(proc, "AudioLab")
            wait(window.locate, "window")
            window.focus()
            window.resize(800, 450)
            def key(k):
                window.key(k, True)
                time.sleep(.24)
                window.key(k, False)
                time.sleep(.20)
            click = window.click
            def shot():
                # On an emulated desktop the mixer can restore before the first SDL present.
                def capture_ready():
                    image = window.capture()
                    header = image.crop((32, 24, 185, 54)).convert("L")
                    if sum(header.histogram()[160:]) < 100:
                        return False
                    image.save(stage / "window.png")
                    return True
                wait(capture_ready, "first visible AudioLab frame")
        level("0.6" if restored else "0.8")
        if not mac:
            shot()
            window.focus()
        if not restored:
            if device == "mouse":
                click(312, 98)
            if device == "keyboard":
                key("Tab")
                for percent in (75, 70, 65, 60):
                    key("Left")
                    level(str(percent / 100))
            if pad:
                (stage / "attach").touch()
                wait((stage / "attached").exists, "SDL gamepad discovery")
                for i, button in enumerate(["Start"] + ["DpadLeft"] * 4):
                    (stage / f"press-{i}-{button}").touch()
                    wait((stage / f"released-{i}").exists, button)
                    if i: level(str((80 - 5 * i) / 100))
                (stage / "detach").touch()
                wait((stage / "detached").exists, "SDL gamepad release")
            if device == "controls":
                click(220, 134)  # Open effects list, choose its first row.
                if not mac:
                    def expanded():
                        frame = window.capture()
                        return frame.getpixel((398, 152)) != frame.getpixel((500, 152))
                    wait(expanded, "effects popup opened")
                click(220, 161)
                level("0.8", "0.375")
                print("PASS effects popup selection", flush=True)
                key("Return")
                if not mac: wait(expanded, "keyboard opened effects popup")
                key("Down")
                key("Return")
                level("0.8")
                click(220, 167)
                level("0.8", master="0")
                key("Return")
                level("0.8")
        if device != "controls":
            level("0.6")
            assert all(row[1:] == ("0.75", "0.8") for row in levels()), levels()
        time.sleep(.35)
        shot()
        key("Escape")
        if proc.wait(timeout=45) != 0: raise RuntimeError(log.read_text(errors="replace"))
        return levels()
    except Exception:
        if not mac and "window" in locals() and window.handle:
            window.capture().save(stage / "failure.png")
        raise
    finally:
        if proc.poll() is None: proc.terminate(); proc.wait(timeout=15)

report = dict(os=platform.platform(), machine=platform.machine(), player="Release", devices={})
report["dropdown_and_toggle"] = play("controls")
print("PASS dropdown and toggle", flush=True)
digests = set()
for device in ("mouse", "keyboard", "gamepad"):
    initial = play(device)
    slot, = (output / (device + "-profile")).rglob("player.settings")
    data = slot.read_bytes()
    reopened = play(device, True)
    assert slot.read_bytes() == data and reopened == [("0.6", "0.75", "0.8")], reopened
    digest = hashlib.sha256(data).hexdigest()
    digests.add(digest)
    report["devices"][device] = dict(mixer=initial, reopened=reopened, settings_sha256=digest)
    print("PASS", device, reopened, digest, flush=True)
assert len(digests) == 1
(output / "report.json").write_text(json.dumps(report, indent=2) + "\n")

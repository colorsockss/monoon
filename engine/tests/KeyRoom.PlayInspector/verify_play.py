"""Drive the packaged editor on the active Mac/Windows desktop or an isolated X11 display."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import zipfile


def run(editor, sample, probe, output, mac_input):
    output.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(sample) as archive:
        archive.extractall(output / "copy")
    project = output / "copy" / "KeyRoom"
    if sys.platform == "win32":
        ctypes.windll.user32.SetProcessDPIAware()
        console = ctypes.windll.kernel32.GetConsoleWindow()
        if console:
            ctypes.windll.user32.ShowWindow(console, 0)
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(probe), MONOON_PLAY_INSPECTOR_QA=str(output),
               MONOON_EDITOR_QA=str(output), MONOON_QA_PROJECT=str(project), MONOON_USER_DATA=str(output / "profile"))
    mac = sys.platform == "darwin"
    if not mac:
        here = Path(__file__).resolve().parent
        sys.path.insert(0, str(here if (here / "verify.py").exists() else here.parent / "KeyRoom.Desktop"))
        from verify import WindowsWindow, X11Window
        from verify_editor import locate_editor, named_windows
        existing = set() if sys.platform == "win32" else named_windows(False)
    with (output / "editor.log").open("wb") as log:
        process = subprocess.Popen([str(editor), str(project)], env=env, stdout=log, stderr=log)

    def apple(*args):
        return subprocess.check_output([str(mac_input), str(process.pid), *map(str, args)], text=True).strip()

    def key(k, shortcut=False):
        if mac:
            apple(k, *(["cmd"] if shortcut else []))
        else:
            if shortcut:
                window.key("Control_L", True)
            try:
                window.key(k, True)
                time.sleep(.09)
                window.key(k, False)
            finally:
                if shortcut:
                    window.key("Control_L", False)
        time.sleep(.12)

    def capture(name, fill=None, panel_edges=None):
        if mac:
            subprocess.run(["screencapture", "-x", "-o", "-l", apple("id"), output / name], check=True)
        else:
            # The compositor can present the previous frame after the game has processed the input.
            # Wait for the requested panel fill before taking the desktop evidence.
            limit = time.monotonic() + 120
            while True:
                frame = window.capture()
                if fill is None:
                    frame.save(output / name)
                    break
                if sys.platform == "win32":
                    origin = window.wt.POINT()
                    if not window.api.ClientToScreen(window.handle, ctypes.byref(origin)):
                        raise OSError("Cannot locate the editor client area")
                    ox, oy = origin.x, origin.y
                else:
                    ox, oy = window.client_origin()
                left, top, right, bottom = panel_edges
                left, right, top, bottom = left-ox, right-ox, top-oy, bottom-oy
                mx, my = (left+right)//2, (top+bottom)//2
                def matches(x, y):
                    return (0 <= x < frame.width and 0 <= y < frame.height and
                            all(abs(c-f) <= 2 for c,f in zip(frame.getpixel((x,y)), fill)))
                inside = [(left+4,my),(right-4,my),(mx,top+4),(mx,bottom-4)]
                outside = [(left-4,my),(right+4,my),(mx,top-4),(mx,bottom+4)]
                if all(matches(*p) for p in inside) and not any(matches(*p) for p in outside):
                    frame.save(output / name)
                    break
                if time.monotonic() > limit:
                    raise RuntimeError(f"{name}: expected panel edges {panel_edges} did not appear")
                time.sleep(.2)

    try:
        window = None
        if not mac:
            window = (WindowsWindow if sys.platform == "win32" else X11Window)(process, "MoNoon")
        seq = 1
        deadline = time.monotonic() + 300
        while not (output / "result.json").exists():
            if (output / "error.txt").exists():
                raise RuntimeError((output / "error.txt").read_text())
            if process.poll() is not None or time.monotonic() > deadline:
                raise RuntimeError(f"Editor exited or timed out at input {seq}: " + (output / "editor.log").read_text(errors="replace")[-1500:])
            path = output / f"input-{seq}.json"
            if not path.exists():
                time.sleep(.12)
                continue
            try:
                event = json.loads(path.read_text())
            except PermissionError:
                # Windows can expose a renamed request before releasing its sharing lock.
                if sys.platform != "win32":
                    raise
                time.sleep(.12)
                continue
            action = event["action"]
            if window is not None:
                if not (window.handle or locate_editor(window, process, existing)):
                    time.sleep(.2)
                    continue
                window.focus()
            print(seq, action, event.get("value"), flush=True)
            if action == "click":
                x, y = event["x"], event["y"]
                if mac:
                    apple("click", x, y)
                elif sys.platform == "win32":
                    pt = window.wt.POINT(x, y)
                    window.api.ScreenToClient(window.handle, ctypes.byref(pt))
                    window.click(pt.x, pt.y)
                else:
                    ox, oy = window.client_origin()
                    window.click(x - ox, y - oy)
            elif action == "number":
                key("a", True)
                for digit in event["value"]:
                    key(digit)
                key("Return")
            elif action == "capture":
                name = event.get("value") or "inspector.png"
                if Path(name).name != name or not name.endswith(".png"):
                    raise ValueError(name)
                fill = event.get("fill")
                capture(name, tuple(bytes.fromhex(fill)) if fill else None, event.get("panelEdges"))
            elif action == "key":
                key(event["value"])
            elif action in ("undo", "save"):
                key("z" if action == "undo" else "s", True)
            else:
                raise ValueError(action)
            path.with_suffix(".done").touch()
            seq += 1
            deadline = time.monotonic() + 240
        process.wait(90)
        if process.returncode:
            raise RuntimeError(f"Editor returned {process.returncode}")
        print((output / "result.json").read_text(), flush=True)
    finally:
        if process.poll() is None:
            try:
                if mac or (window is not None and window.handle):
                    capture("failure.png")
            finally:
                process.terminate()
                process.wait(30)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("editor", "sample", "probe", "output"):
        parser.add_argument(name, type=lambda p: Path(p).resolve())
    parser.add_argument("--mac-input", type=Path)
    opts = parser.parse_args()
    run(opts.editor, opts.sample, opts.probe, opts.output, opts.mac_input)

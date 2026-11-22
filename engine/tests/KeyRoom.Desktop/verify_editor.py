"""Native Windows/X11 editor regression. Run on a 100% scale interactive desktop."""
import argparse
import ctypes
import hashlib
import os
from pathlib import Path
import re
import subprocess
import tempfile
import time
import zipfile

from PIL import ImageChops, ImageGrab
from verify import WindowsWindow, X11Window


def wait(check, description, timeout=90):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        value = check()
        if value:
            return value
        time.sleep(.25)
    raise RuntimeError("Timed out waiting for " + description)


def play_button(image):
    # Find the accent-colored Play control in the Scene toolbar, including hover.
    strip = image.crop((image.width // 2, 0, image.width - 215, min(150, image.height)))
    for color in ((158, 218, 201), (176, 232, 217)):
        channels = [band.point([255 if i == target else 0 for i in range(256)])
                    for band, target in zip(strip.split(), color)]
        mask = ImageChops.multiply(ImageChops.multiply(channels[0], channels[1]), channels[2])
        box = mask.getbbox()
        if box and 48 <= box[2] - box[0] <= 61 and 14 <= box[3] - box[1] <= 23:
            return image.width // 2 + (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
    return None


def tap(window, key, modifier=None):
    if modifier:
        window.key(modifier, True)
    try:
        window.key(key, True)
        time.sleep(.20)
        window.key(key, False)
    finally:
        if modifier:
            window.key(modifier, False)
    time.sleep(.12)


def focus(window):
    if isinstance(window, WindowsWindow):
        # Autologon can leave the launcher in the foreground. Grant focus with
        # a real click on this HWND and immediately clear the topmost flag.
        api = window.api
        if api.GetForegroundWindow() != window.handle.value:
            api.ShowWindow(window.handle, 9)
            if not api.SetWindowPos(window.handle, window.wt.HWND(-1), 0, 0, 0, 0, 0x43):
                raise OSError("Cannot raise editor window")
            try:
                point = window.wt.POINT(380, 40)
                api.ClientToScreen(window.handle, ctypes.byref(point))
                api.WindowFromPoint.argtypes = [window.wt.POINT]
                api.WindowFromPoint.restype = window.wt.HWND
                wait(lambda: api.WindowFromPoint(point) == window.handle.value, "unobstructed editor")
                api.SetCursorPos(point.x, point.y)
                api.mouse_event(2, 0, 0, 0, 0)
                time.sleep(.15)
                api.mouse_event(4, 0, 0, 0, 0)
            finally:
                api.SetWindowPos(window.handle, window.wt.HWND(-2), 0, 0, 0, 0, 0x13)
    window.focus()


def named_windows(visible):
    args = ["--onlyvisible"] if visible else []
    result = subprocess.run(["xdotool", "search", *args, "--name", "^MoNoon$"],
                            capture_output=True, text=True)
    return set(result.stdout.splitlines())


def locate_editor(window, proc, existing):
    if window.locate():
        return True
    if isinstance(window, X11Window):
        # Some X11 clients omit _NET_WM_PID. Only accept a single new,
        # visible MoNoon window and never target a different process ID.
        candidates = named_windows(True) - existing
        if len(candidates) == 1:
            handle = candidates.pop()
            owner = subprocess.run(["xdotool", "getwindowpid", handle], capture_output=True, text=True)
            if owner.returncode or owner.stdout.strip() == str(proc.pid):
                window.handle = handle
                return True
    return False


def compare_save(path, reference):
    actual = path.read_bytes()
    digest = hashlib.sha256(actual).hexdigest()
    if actual != reference:
        raise ValueError(f"{path}: got {len(actual)} bytes SHA-256 {digest}, "
                         f"expected {len(reference)} bytes SHA-256 {hashlib.sha256(reference).hexdigest()}")
    print(f"PASS {path.parent.name}/{path.name}: {len(actual)} bytes SHA-256 {digest}", flush=True)


def exercise(executable, project, output, edit, reference):
    scene = project / "Scenes/KeyRoom.scene"
    position = lambda: re.search(r"^  name: Player\n  x: (\d+)$", scene.read_text(encoding="utf-8"), re.M)[1]
    assert position() == ("274" if edit else "190")
    output.mkdir()
    profile = output / "profile"
    log = (output / "editor.log").open("wb")
    existing = named_windows(False) if os.name != "nt" else set()
    proc = subprocess.Popen([str(executable), str(project)], cwd=executable.parent,
                            env=dict(os.environ, MONOON_USER_DATA=str(profile)), stdout=log, stderr=log)
    window = (WindowsWindow if os.name == "nt" else X11Window)(proc, "MoNoon")

    def locate():
        if proc.poll() is not None:
            raise RuntimeError(f"Editor exited ({proc.returncode}). See {log.name}")
        return locate_editor(window, proc, existing)

    def play(expected, name):
        window.click(*button)
        def save():
            slot = next(profile.rglob("slot-001.save"), None)
            if slot and re.search(r"^  key: " + expected + "$", slot.read_text(encoding="utf-8"), re.M):
                return slot
            tap(window, "F5")
            return None
        slot = wait(save, "Play checkpoint with key=" + expected, 120)
        evidence = output / (name + ".save")
        evidence.write_bytes(slot.read_bytes())
        if name == "edited" and reference is not None:
            compare_save(evidence, reference)
        tap(window, "i")
        time.sleep(1)
        window.capture().save(output / (name + "-inventory.png"))
        tap(window, "i")
        tap(window, "Escape")
        wait(lambda: window.capture().crop(labels).tobytes() == edit_labels, "editable Transform inspector")

    try:
        wait(locate, "MoNoon window", 150)
        if isinstance(window, WindowsWindow):
            # Keep both the toolbar and the right-hand Inspector on the runner's desktop.
            width = min(1220, window.api.GetSystemMetrics(0) - 32)
            height = min(740, window.api.GetSystemMetrics(1) - 96)
            window.resize(width, height)
        focus(window)
        button = wait(lambda: play_button(window.capture()), "Scene toolbar", 120)
        print("Scene toolbar", button, flush=True)
        frame = window.capture()
        labels = (frame.width - 212, button[1] + 48, frame.width - 155, button[1] + 91)
        edit_labels = frame.crop(labels).tobytes()
        if edit:
            play("false", "baseline")
            # KeyRoom opens with Player selected. Position X is the first
            # numeric field in its Transform section, relative to the toolbar.
            window.click(button[0] + 134, button[1] + 82)
            tap(window, "a", "Control_L")
            if isinstance(window, X11Window):
                window.command("type", "--delay", 140, "190")
            else:
                for digit in "190":
                    tap(window, digit)
            tap(window, "Return")
            window.capture().save(output / "position-entered.png")
            tap(window, "s", "Control_L")
            wait(lambda: position() == "190", "saved Player X=190", 25)
        window.capture().save(output / "inspector.png")
        play("true", "edited")
        print("PASS saved Player X=190 and collected key", flush=True)
    except Exception:
        if window.handle and proc.poll() is None:
            window.capture().save(output / "failure.png")
        elif isinstance(window, X11Window):
            ImageGrab.grab(xdisplay=os.environ["DISPLAY"]).save(output / "desktop.png")
            with (output / "x11-windows.txt").open("w") as report:
                subprocess.run(["ps", "-p", str(proc.pid), "-o", "pid,stat,etime,pcpu,wchan,command"], stdout=report)
                subprocess.run(["xwininfo", "-root", "-tree"], stdout=report, stderr=subprocess.STDOUT)
        raise
    finally:
        if proc.poll() is None:
            # The original project and all user saves are outside this fixture.
            proc.terminate()
            proc.wait(20)
        log.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("executable", type=Path)
    parser.add_argument("sample_zip", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--expected-save", type=Path, help="Compare both edited F5 saves with this Mac reference byte for byte.")
    args = parser.parse_args()
    reference = args.expected_save.read_bytes() if args.expected_save else None
    out = args.output.resolve()
    out.mkdir(parents=True, exist_ok=False)
    if os.name == "nt":
        ctypes.windll.user32.SetProcessDPIAware()
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000003)
    try:
        with tempfile.TemporaryDirectory(dir=out) as temp:
            with zipfile.ZipFile(args.sample_zip) as archive:
                archive.extractall(temp)
            project = Path(temp) / "KeyRoom"
            for phase in ("edit", "reopen"):
                exercise(args.executable.resolve(), project, out / phase, phase == "edit", reference)
            (out / "KeyRoom.scene").write_bytes((project / "Scenes/KeyRoom.scene").read_bytes())
    finally:
        if os.name == "nt":
            ctypes.windll.kernel32.SetThreadExecutionState(0x80000000)


if __name__ == "__main__":
    main()

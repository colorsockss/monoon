"""Run on an interactive Windows desktop or an isolated Linux X11 display.

Usage: python verify.py PATH_TO_GAME_PACKAGE OUTPUT_DIRECTORY
Requires Pillow; Linux also needs xdotool.
"""
import argparse
import ctypes
import hashlib
import json
import os
from pathlib import Path
import platform
import subprocess
import time

from PIL import Image, ImageChops, ImageGrab


class X11Window:
    def __init__(self, process):
        self.process = process
        self.handle = None

    @staticmethod
    def command(*args):
        return subprocess.check_output(["xdotool", *map(str, args)], text=True, stderr=subprocess.DEVNULL).strip()

    def locate(self):
        try:
            matches = self.command("search", "--onlyvisible", "--pid", self.process.pid).splitlines()
            self.handle = next((w for w in matches if self.command("getwindowname", w) == "KeyRoom"), None)
        except subprocess.CalledProcessError:
            return False
        return self.handle is not None

    def focus(self):
        self.command("windowfocus", "--sync", self.handle)

    def key(self, key, down):
        if down and self.command("getwindowfocus") != self.handle:
            raise RuntimeError("KeyRoom lost keyboard focus")
        self.command("keydown" if down else "keyup", key)

    def resize(self, width, height):
        self.command("windowmove", self.handle, 16, 40)
        self.command("windowsize", "--sync", self.handle, width, height)

    def click(self, x, y):
        if self.command("getwindowfocus") != self.handle:
            raise RuntimeError("KeyRoom lost mouse focus")
        self.command("mousemove", "--window", self.handle, x, y)
        self.command("click", "1")

    def capture(self):
        geometry = dict(line.split("=", 1) for line in self.command("getwindowgeometry", "--shell", self.handle).splitlines())
        x, y, w, h = (int(geometry[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))
        return ImageGrab.grab(bbox=(x, y, x + w, y + h), xdisplay=os.environ["DISPLAY"]).convert("RGB")


class WindowsWindow:
    def __init__(self, process):
        from ctypes import wintypes as wt
        self.process = process
        self.api = ctypes.windll.user32
        self.wt = wt
        self.api.GetForegroundWindow.restype = wt.HWND
        self.api.GetWindowLongW.restype = wt.LONG
        self.enumerate = ctypes.WINFUNCTYPE(wt.BOOL, wt.HWND, wt.LPARAM)
        self.handle = None

    def locate(self):
        @self.enumerate
        def visit(hwnd, unused):
            hwnd = self.wt.HWND(hwnd)
            pid = self.wt.DWORD()
            self.api.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
            if pid.value == self.process.pid and self.api.IsWindowVisible(hwnd):
                title = ctypes.create_unicode_buffer(256)
                self.api.GetWindowTextW(hwnd, title, len(title))
                if title.value == "KeyRoom":
                    self.handle = hwnd
                    return False
            return True
        self.api.EnumWindows(visit, 0)
        return self.handle is not None

    def focus(self):
        # Explorer or Terminal may take focus as an autologon desktop finishes
        # starting up. Only send game input after confirming this exact HWND.
        deadline = time.monotonic() + 25
        if self.api.GetForegroundWindow() == self.handle.value and not self.api.IsIconic(self.handle):
            return
        self.api.ShowWindow(self.handle, 9)
        while time.monotonic() < deadline:
            foreground = self.api.GetForegroundWindow()
            if foreground == self.handle.value:
                return
            owner = self.api.GetWindowThreadProcessId(self.wt.HWND(foreground), None) if foreground else 0
            caller = ctypes.windll.kernel32.GetCurrentThreadId()
            joined = owner and owner != caller and self.api.AttachThreadInput(caller, owner, True)
            try:
                self.api.BringWindowToTop(self.handle)
                self.api.SetForegroundWindow(self.handle)
            finally:
                if joined:
                    self.api.AttachThreadInput(caller, owner, False)
            time.sleep(.15)
        raise RuntimeError("KeyRoom could not acquire keyboard focus")

    def key(self, key, down):
        if down:
            self.focus()
        vk = {"Left": 0x25, "Right": 0x27, "Up": 0x26, "Down": 0x28, "Tab": 9, "Return": 13}.get(key, ord(key.upper()) if len(key) == 1 else 0)
        scan = self.api.MapVirtualKeyW(vk, 0)
        extended = 1 if key in ("Left", "Right", "Up", "Down") else 0
        self.api.keybd_event(vk, scan, extended | (0 if down else 2), 0)

    def resize(self, width, height):
        rect = self.wt.RECT(0, 0, width, height)
        self.api.AdjustWindowRectEx(ctypes.byref(rect), self.api.GetWindowLongW(self.handle, -16), False,
                                    self.api.GetWindowLongW(self.handle, -20))
        if not self.api.MoveWindow(self.handle, 8, 12, rect.right - rect.left, rect.bottom - rect.top, True):
            raise OSError("MoveWindow failed")

    def capture(self):
        rect = self.wt.RECT()
        point = self.wt.POINT()
        if not self.api.GetClientRect(self.handle, ctypes.byref(rect)) or not self.api.ClientToScreen(self.handle, ctypes.byref(point)):
            raise OSError("Cannot read KeyRoom's client rectangle")
        return ImageGrab.grab(bbox=(point.x, point.y, point.x + rect.right, point.y + rect.bottom),
                              all_screens=True).convert("RGB")

    def click(self, x, y):
        self.focus()
        point = self.wt.POINT(x, y)
        if not self.api.ClientToScreen(self.handle, ctypes.byref(point)) or not self.api.SetCursorPos(point.x, point.y):
            raise OSError("Cannot position pointer in KeyRoom")
        actual = self.wt.POINT()
        if not self.api.GetCursorPos(ctypes.byref(actual)) or (actual.x, actual.y) != (point.x, point.y):
            raise RuntimeError(f"Cursor did not reach requested screen pixel {(point.x, point.y)}: {(actual.x, actual.y)}")
        self.api.WindowFromPoint.argtypes = [self.wt.POINT]
        self.api.WindowFromPoint.restype = self.wt.HWND
        if self.api.WindowFromPoint(point) != self.handle.value:
            raise RuntimeError("Click is not over KeyRoom")
        self.api.mouse_event(2, 0, 0, 0, 0)
        try:
            time.sleep(.10)
        finally:
            self.api.mouse_event(4, 0, 0, 0, 0)


def content_box(img):
    # World background is lit across the entire logical viewport; the surrounding bars are black.
    return img.convert("L").point(lambda v: 255 if v > 12 else 0).getbbox()


def region_matches(img, box, predicate, minimum):
    return sum(n for n, rgb in (img.crop(box).getcolors(65536) or []) if predicate(*rgb)) >= minimum


def near(actual, expected, tolerance=8):
    return all(abs(a - b) <= tolerance for a, b in zip(actual, expected))


def normal_view(img):
    box = content_box(img)
    return img.crop(box).resize((800, 450), Image.Resampling.NEAREST) if box else None


def has_key(img):
    return region_matches(img, (14, 14, 50, 32), lambda r, g, b: r > 185 and g > 110 and b < 140 and r - b > 75, 30)


def room_two(img):
    return (has_key(img)
            and all(near(img.getpixel((x, y)), (87, 126, 150)) for x in (60, 390, 730) for y in (88, 348))
            and region_matches(img, (96, 12, 195, 39), lambda r, g, b: min(r, g, b) > 175, 25))


def player_y(img):
    # Scan only the open space between the two walls. The player is the only mint sprite.
    for y in range(103, 335, 2):
        for x in range(10, 795, 10):
            if near(img.getpixel((x, y)), (99, 230, 193)):
                return y
    return None


def wait_frame(window, name, predicate, output, key=None, timeout=75):
    last = None
    if key:
        window.key(key, True)
    try:
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if window.process.poll() is not None:
                raise RuntimeError(f"KeyRoom exited during {name}: {window.process.returncode}")
            last = window.capture()
            if predicate(last):
                last.save(output / (name + ".png"))
                print("PASS", name, flush=True)
                return last
            time.sleep(.05)
        if last is not None:
            last.save(output / (name + "-failed.png"))
        raise TimeoutError(name)
    finally:
        if key:
            window.key(key, False)


def run_controls(window, output):
    """Measure the painted button first, then send real client-area input at its edges."""
    results = []
    for name, width, height, viewport in [
        ("base", 800, 450, (0, 0, 800, 450)),
        ("wide", 960, 432, (96, 0, 864, 432)),
        ("tall", 640, 600, (0, 120, 640, 480)),
    ]:
        target = output / name
        target.mkdir(parents=True)
        window.resize(width, height)
        def ready(im):
            box = content_box(im)
            return im.size == (width, height) and box is not None and near(box, viewport, 1)
        frame = wait_frame(window, "01-idle", ready, target)
        vx, vy, vr, vb = content_box(frame)
        sx, sy = (vr-vx)/800, (vb-vy)/450
        def point(x, y):
            return round(vx+x*sx), round(vy+y*sy)
        expected = (*point(698, 14), *point(786, 42))
        # Search the top right region of the screenshot, not the predicted button bounds.
        rx, ry = point(600, 0)
        crop = frame.crop((rx, ry, vr, point(0, 90)[1]))
        channels = ImageChops.difference(crop, Image.new("RGB", crop.size, (59, 83, 107))).split()
        mask = ImageChops.lighter(ImageChops.lighter(*channels[:2]), channels[2]).point(lambda v: 255 if v <= 4 else 0)
        measured = mask.getbbox()
        if measured is None:
            raise AssertionError(f"{name}: idle button not visible")
        l,t,r,b = measured = tuple(v + (rx if i % 2 == 0 else ry) for i,v in enumerate(measured))
        if not near(measured, expected, 1):
            raise AssertionError(f"{name}: button painted at {measured}, expected {expected}")

        def state(im, opened):
            # Require the help panel's fill AND its light text, not the button focus highlight.
            filled = near(im.getpixel(point(305,423)), (52,75,96), 8)
            text = region_matches(im, (*point(20,412), *point(246,435)), lambda r,g,b: min(r,g,b)>160, 18)
            return filled == opened and text == opened
        def focused(im):
            return near(im.getpixel(point(703,19)), (70,130,180), 8)
        def tap(key):
            window.key(key, True)
            try:
                time.sleep(.12)
            finally:
                window.key(key, False)
        if not state(frame, False):
            raise AssertionError(f"{name}: help must start closed")
        tap("Tab")
        wait_frame(window, "02-tab-focus", lambda im: focused(im) and state(im,False), target)
        tap("Return")
        wait_frame(window, "03-key-open", lambda im: state(im,True), target)
        tap("Return")
        wait_frame(window, "04-key-close", lambda im: state(im,False), target)
        inside = [(l+2,t+2),(r-3,b-3)]
        window.click(*inside[0])
        wait_frame(window, "05-click-open", lambda im: state(im,True), target)
        window.click(*inside[1])
        wait_frame(window, "06-click-close", lambda im: state(im,False), target)
        mx,my = (l+r)//2,(t+b)//2
        misses = {"left":(l-2,my), "right":(r+1,my), "top":(mx,t-2), "bottom":(mx,b+1)}
        if vx: misses["bar"] = (vr+(width-vr)//2,my)
        if vy: misses["bar"] = (mx,vy//2)
        for side, xy in misses.items():
            # Make every miss observable. Re-arm keyboard focus, then wait for
            # the unfocused frame instead of assuming that a VM drew within .55s.
            tap("Tab")
            wait_frame(window, "armed-"+side, lambda im: focused(im) and state(im,False), target)
            window.click(*xy)
            def missed(im):
                if state(im,True):
                    im.save(target / ("failed-outside-"+side+".png"))
                    raise AssertionError(f"{name}: {side} outside click toggled help at {xy}")
                return not focused(im) and state(im,False)
            shot = wait_frame(window, "miss-"+side, missed, target)
        shot.save(target/"07-misses-closed.png")
        print(f"PASS Controls {name}: viewport {(vx,vy,vr,vb)}, button {measured}, keyboard/click/bounds", flush=True)
        results.append(dict(size=[width,height], viewport=[vx,vy,vr,vb], button_expected=expected,
                            button_measured=measured, keyboard=["focus","open","close"], inside_clicks=inside,
                            outside_clicks=misses))
    return results


def run_route(executable, output, mode):
    left, right, up, down = ("Left", "Right", "Up", "Down") if mode == "arrows" else ("a", "d", "w", "s")
    target = output / mode
    target.mkdir(parents=True, exist_ok=True)
    with (target / "player.log").open("wb") as log:
        process = subprocess.Popen([str(executable)], cwd=executable.parent, stdout=log, stderr=subprocess.STDOUT)
        try:
            window = WindowsWindow(process) if os.name == "nt" else X11Window(process)
            deadline = time.monotonic() + 120
            while not window.locate():
                if process.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError(f"No KeyRoom window; exit={process.poll()}, see {target / 'player.log'}")
                time.sleep(.25)
            window.focus()
            window.resize(800, 450)
            wait_frame(window, "01-start", lambda im: im.size == (800, 450) and not has_key(im)
                       and near(im.getpixel((292, 130)), (99, 230, 193)), target)
            ui = {}
            if mode == "arrows":
                ui["controls_ui"] = run_controls(window, output/"controls")
                window.resize(800,450)
                wait_frame(window, "01-restored", lambda im: im.size == (800,450)
                           and near(im.getpixel((292,130)), (99,230,193)), target)
            if (executable.parent / "Game/Assets/Room.tileset").exists():
                def stopped_at_tile(im):
                    return near(im.getpixel((260,154)), (85,103,121)) and near(im.getpixel((292,130)), (99,230,193))
                wait_frame(window, "tile-contact", stopped_at_tile, target, down)
                window.key(down, True)
                try:
                    time.sleep(.8)
                    frame = window.capture()
                    if not stopped_at_tile(frame):
                        raise RuntimeError("Player crossed the tile wall")
                    frame.save(target / "tile-blocked.png")
                finally:
                    window.key(down, False)
                ui["tile_wall"] = True
            wait_frame(window, "02-key", has_key, target, left)
            second = wait_frame(window, "03-room-two", room_two, target, right, timeout=150)
            initial_y = player_y(second)
            if initial_y is None:
                raise AssertionError("Missing player in second room")
            above = wait_frame(window, "04-up", lambda im: (player_y(im) or 999) <= initial_y - 12, target, up)
            above_y = player_y(above)
            wait_frame(window, "05-down", lambda im: (player_y(im) or 0) >= above_y + 12, target, down)
            results = []
            for name, width, height, expected in [
                ("06-wide", 960, 432, (96, 0, 864, 432)),
                ("07-tall", 640, 600, (0, 120, 640, 480))
            ]:
                window.resize(width, height)
                def correct(frame):
                    box = content_box(frame)
                    if frame.size != (width, height) or box is None or not near(box, expected, 1):
                        return False
                    return room_two(normal_view(frame))
                frame = wait_frame(window, name, correct, target)
                x0, y0, x1, y1 = content_box(frame)
                results.append(dict(window=[width, height], game=[x1 - x0, y1 - y0],
                                    bars=[x0, y0, width - x1, height - y1]))
            return dict(controls=mode, room="ROOM 2 / KEY", resize=results, **ui)
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("game", type=Path)
    p.add_argument("output", type=Path)
    args = p.parse_args()
    if platform.machine().lower() not in ("amd64", "x86_64") or platform.system() not in ("Windows", "Linux"):
        raise RuntimeError("Run on x64 Windows or Linux")
    if os.name == "nt":
        ctypes.windll.user32.SetProcessDPIAware()
    executable = args.game.resolve() / ("MoNoonPlayer.exe" if os.name == "nt" else "MoNoonPlayer")
    if not executable.is_file():
        raise FileNotFoundError(executable)
    data = executable.parent / "Game"
    patterns = ("*.scene", "*.prefab", "*.clip", "*.tileset", "*.data", "*.meta", "GameSettings", ".monoon-assets")
    docs = sorted(p for pattern in patterns for p in data.rglob(pattern))
    if any(data.rglob("*.json")) or any(data.rglob("*.yaml")) or not all((data / name).is_file() for name in
            ("GameSettings", ".monoon-assets", "Scenes/KeyRoom.scene", "Scenes/SecondRoom.scene", "Assets/Door.clip")):
        raise RuntimeError("Expected the YAML KeyRoom package with no legacy JSON content")
    content = {f.relative_to(data).as_posix(): hashlib.sha256(f.read_bytes()).hexdigest() for f in docs}
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    script = data / ".monoon-compiled/MoNoon.GameScripts.dll"
    if not script.is_file():  # Retest older exports with their original templates.
        script = data / ".monoon-compiled/ToggleControls.dll"
    result = dict(os=platform.platform(), machine=platform.machine(), renderer=os.getenv("SDL_RENDER_DRIVER", "default"),
                  executable_sha256=hashlib.sha256(executable.read_bytes()).hexdigest(),
                  engine_sha256=hashlib.sha256((executable.parent / "Engine.Desktop.dll").read_bytes()).hexdigest(),
                  yaml_sha256=content,
                  core_sha256=hashlib.sha256((executable.parent / "Engine.Core.dll").read_bytes()).hexdigest(),
                  button_script_sha256=hashlib.sha256(script.read_bytes()).hexdigest(),
                  routes=[])
    if os.name == "nt":
        result["sdl_sha256"] = hashlib.sha256((executable.parent / "SDL3.dll").read_bytes()).hexdigest()
    for mode in ("arrows", "wasd"):
        route = run_route(executable, output, mode)
        if "controls_ui" in route:
            result["controls_ui"] = route.pop("controls_ui")
        result["routes"].append(route)
        (output / "results.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()

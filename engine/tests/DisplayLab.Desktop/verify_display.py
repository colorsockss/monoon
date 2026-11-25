"""Drive a shipped DisplayLab player, measure its painted viewport and exercise real window input."""
import ctypes
import json
import os
from pathlib import Path
import platform
import re
import subprocess
import sys
import time

from PIL import Image, ImageChops

root = Path(__file__).resolve().parent
sys.path.extend([str(root), str(root.parent / "KeyRoom.Desktop")])
from verify import WindowsWindow, X11Window

player, output = (Path(v).resolve() for v in sys.argv[1:3])
output.mkdir(parents=True, exist_ok=False)
mac = sys.platform == "darwin"
bare_x11 = sys.platform.startswith("linux") and sys.argv[3:] == ["--no-window-manager"]
if mac:
    helper = Path(sys.argv[3]).resolve()
elif os.name == "nt":
    ctypes.windll.user32.SetProcessDPIAware()
    console = ctypes.windll.kernel32.GetConsoleWindow()
    if console:
        ctypes.windll.user32.ShowWindow(console, 0)


class MacWindow:
    def __init__(self, process):
        self.process = process
        self.chrome = 0
        self.density = 1  # device pixels per window point, read from each capture

    def command(self, action, *args):
        return subprocess.check_output([helper, action, str(self.process.pid), *map(str, args)],
                                       text=True, stderr=subprocess.DEVNULL).strip()

    def locate(self):
        try:
            self.command("geometry")
            return True
        except subprocess.CalledProcessError:
            return False

    def focus(self):
        self.command("focus")

    def geometry(self):
        return list(map(int, self.command("geometry").split()))

    def resize(self, w, h):
        self.command("resize", w, h + self.chrome)

    def click(self, x, y):
        self.command("click-window", x, y + self.chrome)

    def capture(self):
        wid, w, h, *_ = self.geometry()
        path = output / "mac-capture.png"
        subprocess.run(["screencapture", "-x", "-o", "-l", str(wid), path], check=True)
        with Image.open(path) as image:
            self.density = image.width / w
            return image.convert("RGB").resize((w, h)).crop((0, self.chrome, w, h))


def until(probe, what, proc, timeout=110):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline and proc.poll() is None:
        result = probe()
        if result:
            return result
        time.sleep(.18)
    raise RuntimeError(f"{what}; exit={proc.poll()}")


def tap_escape(window):
    if mac:
        window.command("Escape")
    else:
        window.key("Escape", True)
        time.sleep(.15)
        window.key("Escape", False)


# ShowcaseDisplay (800 x 450, integer scale): the click counter and the two settings toggles in logical game units.
COUNTER = (546, 270, 774, 304)
FULLSCREEN, VSYNC = (754, 91), (754, 127)


def inspect(frame):
    # Bars are pure black; the showcase room and its settings panel are not.
    colored = frame.convert("L").point(lambda v: 255 if v > 6 else 0)
    w, h = frame.size
    row = colored.crop((0, h//2, w, h//2+1)).getbbox()
    column = colored.crop((w//2, 0, w//2+1, h)).getbbox()
    if not row or not column:
        return None
    l, t, r, b = row[0], column[1], row[2], column[3]
    # Measure the painted button independently of its authored hit rectangle: the counter's blue-grey fill stands out
    # from the dark panel around it.
    sx, sy = (r-l)/800, (b-t)/450
    x, y = round(l+(COUNTER[0]-10)*sx), round(t+(COUNTER[1]-10)*sy)
    crop = frame.crop((x, y, round(l+(COUNTER[2]+10)*sx), round(t+(COUNTER[3]+10)*sy)))
    red, _, blue = crop.split()
    button = ImageChops.subtract(blue, red).point(lambda d: 255 if d > 34 else 0).getbbox()
    if not button:
        return None
    return [l, t, r, b], [x+button[0], y+button[1], x+button[2], y+button[3]]


report = {"os": platform.platform(), "machine": platform.machine(), "stages": {}}
windowed = (720, 540)
title_height = None
launches = ["new", "windowed-reopened"] if bare_x11 else ["new", "fullscreen-reopened", "windowed-reopened"]
for launch in launches:
    log = output / (launch + ".log")
    with log.open("wb") as stream:
        proc = subprocess.Popen([str(player)], cwd=player.parent, env=dict(os.environ, MONOON_USER_DATA=str(output/"profile")),
                                stdout=stream, stderr=subprocess.STDOUT)
    window = MacWindow(proc) if mac else (WindowsWindow if os.name == "nt" else X11Window)(proc, "DisplayLab")
    count = 0

    def logs():
        return log.read_text(errors="replace")

    def check_click_count():
        recorded = [int(value) for value in re.findall(r"^Clicks: (\d+)", logs(), re.M)]
        assert recorded == list(range(1, count + 1)), recorded

    def state(name, value):
        def check():
            values = re.findall(rf"^{name} = (True|False)\s*$", logs(), re.M)
            return values and values[-1] == str(value)
        until(check, (name, value), proc)

    def stage(name, full):
        if mac:
            window.chrome = 0 if full else title_height
        target = screen if full else windowed
        last = []
        def scan():
            frame = window.capture()
            last[:] = [frame]
            measured = inspect(frame)
            sizes = [tuple(target)]
            if mac and full:
                # Cocoa can round the top safe inset to adjacent whole window points.
                sizes.extend((target[0], target[1] - safe_top + delta) for delta in (-1, 0, 1))
            if frame.size not in sizes or not measured:
                return None
            view, button = measured
            w, h = frame.size
            # Integer scale: the largest whole number of device pixels per game pixel that fits, at least one.
            density = window.density if mac else 1
            scale = max(1, int(min(w*density/800, h*density/450))) / density
            dx, dy = (w-scale*800)/2, (h-scale*450)/2
            expected = [dx, dy, w-dx, h-dy]
            expected_button = [dx+COUNTER[0]*scale, dy+COUNTER[1]*scale, dx+COUNTER[2]*scale, dy+COUNTER[3]*scale]
            if any(abs(a-b)>2 for a,b in zip(view+button,expected+expected_button)):
                return None
            return frame, view, button
        try:
            frame, view, button = until(scan, (name, target), proc)
        except Exception:
            if last:
                last[0].save(output/(name+"-failed.png"))
                print(name, "last", last[0].size, inspect(last[0]), flush=True)
            raise
        frame.save(output/(name+".png"))
        report["stages"][name] = dict(client=frame.size, viewport=view, button=button)
        print("PASS", name, "client", frame.size, "viewport", view, "button", button, flush=True)
        return view, button

    def click_at(x, y):
        if not mac:
            # The automation warps the cursor. Let the VM deliver the movement
            # before injecting a button press, as with a pointer moved by hand.
            window.move_pointer(x, y)
            time.sleep(1.2 if os.name == "nt" else .65)
        if sys.platform.startswith("linux"):
            window.command("mousedown", 1)
            try:
                time.sleep(.18)
            finally:
                window.command("mouseup", 1)
        else:
            window.click(x, y)

    def click_widget(view, at):
        l,t,r,b = view
        window.focus()
        click_at(round(l+(r-l)*at[0]/800), round(t+(b-t)*at[1]/450))

    def probe(name, view, button):
        l,t,r,b = button
        x,y = (l+r)//2,(t+b)//2
        misses = [(l-3,y),(r+2,y),(x,t-3),(x,b+2)]
        if view[1]>6:
            misses.append((x,view[1]//2))
        elif view[0]>6:
            misses.append((view[0]//2,y))
        def hit(xy):
            global count
            click_at(*xy)
            count += 1
            until(lambda: f"Clicks: {count}\n" in logs(), ("probe",count), proc)

        hit((l+3,t+3))
        for xy in misses:
            click_at(*xy)
        # A later successful click serves as a frame barrier for the preceding misses.
        hit((r-4,b-4))
        check_click_count()
        report["stages"][name].update(outside=misses, inside=[[l+3,t+3],[r-4,b-4]], counter=count)

    try:
        until(window.locate, "locate game", proc)
        window.focus()
        full = launch == "fullscreen-reopened"
        state("Fullscreen",full)
        state("VSync", launch == "new" if bare_x11 else not full)
        if mac:
            _, _, outer_h, sw, sh, safe_top = window.geometry()
            screen = (sw,sh)
            if launch == "new": title_height = outer_h-450
            window.chrome = 0 if full else title_height
        elif os.name == "nt":
            screen = tuple(ctypes.windll.user32.GetSystemMetrics(axis) for axis in (0,1))
        else:
            screen = tuple(map(int,window.command("getdisplaygeometry").split()))
        if launch == "new":
            window.resize(*windowed)
            view, button = stage("resized",False)
            probe("resized",view,button)
            click_widget(view,VSYNC)
            state("VSync",False)
            click_widget(view,FULLSCREEN)
            if bare_x11:
                until(lambda: logs().count("Fullscreen = False") == 2, "fullscreen request declined", proc)
                view, button = stage("rejected-windowed", False)
                probe("rejected-windowed", view, button)
            else:
                state("Fullscreen",True)
                view, button = stage("fullscreen",True)
                probe("fullscreen",view,button)
        elif full:
            view, button = stage(launch,True)
            probe(launch,view,button)
            click_widget(view,FULLSCREEN)
            state("Fullscreen",False)
            view, button = stage("returned",False)
            probe("returned",view,button)
            click_widget(view,VSYNC)
            state("VSync",True)
        else:
            view, button = stage(launch,False)
            probe(launch,view,button)
        tap_escape(window)
        assert proc.wait(timeout=55) == 0
        check_click_count()
        settings, = (output/"profile").rglob("display.settings")
        content = settings.read_text()
        saved_fullscreen = not bare_x11 and launch == "new"
        saved_vsync = not bare_x11 and launch != "new"
        for key, value in [("width",720),("height",540),("fullscreen",str(saved_fullscreen).lower()),
                           ("vsync",str(saved_vsync).lower())]:
            assert re.search(rf"\b{key}: {value}\b",content), content
        report.setdefault("saved", {})[launch] = content
    finally:
        if proc.poll() is None:
            proc.terminate()
            proc.wait(timeout=25)
(output/"report.json").write_text(json.dumps(report,indent=2)+"\n")

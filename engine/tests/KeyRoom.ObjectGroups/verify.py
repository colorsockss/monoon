"""Exercise the prefab Inspector and KeyRoom Scene Play in a packaged desktop editor."""
import argparse
import ctypes
import os
from pathlib import Path
import re
import subprocess
import time
import sys
import zipfile


def focus_main(process):
    if sys.platform.startswith("linux"):
        # Xvfb without a window manager does not return focus to a modal window's owner.
        handles = subprocess.check_output(["xdotool", "search", "--onlyvisible", "--name", "^MoNoon$"], text=True).split()
        if len(handles) != 1:
            raise RuntimeError("Expected one MoNoon editor on the isolated X11 display")
        subprocess.run(["xdotool", "windowfocus", "--sync", handles[0]], check=True)
    elif sys.platform == "win32":
        from ctypes import wintypes as wt
        api = ctypes.windll.user32
        api.FindWindowW.restype = wt.HWND
        handle = wt.HWND(api.FindWindowW(None, "MoNoon"))
        pid = wt.DWORD()
        api.GetWindowThreadProcessId(handle, ctypes.byref(pid))
        if pid.value != process.pid:
            raise RuntimeError("Unexpected foreground target")
        caller = ctypes.windll.kernel32.GetCurrentThreadId()
        owner = api.GetWindowThreadProcessId(wt.HWND(api.GetForegroundWindow()), None)
        joined = owner != caller and api.AttachThreadInput(caller, owner, True)
        try:
            api.BringWindowToTop(handle)
            api.SetForegroundWindow(handle)
        finally:
            if joined:
                api.AttachThreadInput(caller, owner, False)


def run(editor, sample, probe, output):
    output.mkdir(parents=True, exist_ok=False)
    with zipfile.ZipFile(sample) as archive:
        archive.extractall(output)
    root = output / "KeyRoom"
    prefab = root / "Assets/Showcase/Coin.prefab"
    before = prefab.read_text(encoding="utf-8")
    # Prove the GUI writes both fields in a disposable copy of the shipped sample.
    assert re.search(r"^  tag: Coin$", before, re.M) and "  - 수집품" in before
    prefab.write_text(re.sub(r"^  (?:tag: Coin\n|groups:\n  - 수집품\n)", "", before, flags=re.M), encoding="utf-8")
    env = dict(os.environ, DOTNET_STARTUP_HOOKS=str(probe), MONOON_GROUP_QA=str(output),
               MONOON_USER_DATA=str(output / "profile"))
    with (output / "editor.log").open("wb") as log:
        proc = subprocess.Popen([str(editor), str(root)], env=env, stdout=log, stderr=log)
        try:
            deadline = time.monotonic() + 720
            while proc.poll() is None:
                if (output / "focus-main").exists():
                    focus_main(proc)
                    (output / "focus-main").unlink()
                if (output / "error.txt").exists():
                    raise RuntimeError((output / "error.txt").read_text(encoding="utf-8"))
                if time.monotonic() > deadline:
                    raise TimeoutError("Editor probe timed out. See editor.log.")
                time.sleep(.3)
            if proc.returncode or not (output / "result.json").exists():
                raise RuntimeError(f"Editor returned {proc.returncode}. See editor.log.")
            print((output / "result.json").read_text(encoding="utf-8"))
        finally:
            if proc.poll() is None:
                proc.terminate()
                proc.wait(30)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("editor", "sample", "probe", "output"):
        parser.add_argument(name, type=lambda p: Path(p).resolve())
    args = parser.parse_args()
    run(args.editor, args.sample, args.probe, args.output)

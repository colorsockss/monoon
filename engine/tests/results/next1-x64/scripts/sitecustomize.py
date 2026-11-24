"""Guest only (PYTHONPATH): TCG emulation is roughly 10-30x slower than the Mac, so scale every subprocess timeout the harness
scripts pass. What they check is unchanged (same approach as phase1-regression/scripts/guest.py, but for separate processes)."""
import os, subprocess
_scale = float(os.environ.get('MONOON_TIMEOUT_SCALE', '15'))
_communicate, _wait = subprocess.Popen.communicate, subprocess.Popen.wait
def communicate(self, input=None, timeout=None):
    return _communicate(self, input, None if timeout is None else timeout * _scale)
def wait(self, timeout=None):
    return _wait(self, None if timeout is None else timeout * _scale)
subprocess.Popen.communicate, subprocess.Popen.wait = communicate, wait
if os.environ.get('MONOON_SCALE_MONOTONIC') == '1':
    # AudioLab.Vorbis/ci.py waits on time.monotonic() deadlines (phase1 patched its module the same way). subprocess
    # imported the real clock above, so only the harness's own deadlines stretch.
    import time
    _monotonic = time.monotonic
    time.monotonic = lambda: _monotonic() / _scale

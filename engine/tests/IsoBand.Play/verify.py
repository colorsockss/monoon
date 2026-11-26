from pathlib import Path
import hashlib
import os
import sys

sys.dont_write_bytecode = True
TESTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TESTS / "Shared"))
from sample_route import Route, click, keys, main

RESULTS = TESTS / "results" / "isoband"
ROUTE = Route(
    "56a3a1e6-45d4-5055-90b4-d22196ec482f", "32ccf437-7ad0-53ae-962e-287d655678a6",
    "Expedition camera", "IsoBand zone ",
    r"IsoBand zone (\d+): (?:\d+ defeated, \d+ XP, \d+ crowns\.|Returned to Haven\.)",
    4570, (48, 246, 499, 1020, 1566, 2160, 3195, 4080, 4380), 4,
)


def events():
    steps = []
    for line in Path(__file__).with_name("route.tsv").read_text(encoding="utf-8").splitlines():
        if not line or line.startswith("#"):
            continue
        at, kind, a, b = line.split("\t")
        if kind == "click":
            steps.extend(click(int(at), a, b))
        elif kind == "key":
            steps.append(keys(int(at), a, b == "true"))
        else:
            raise ValueError(f"Unknown replay action: {kind}")
    return steps


def check_baseline(out):
    wanted = (RESULTS / "remake-route.txt").read_bytes()
    if (out / "route.txt").read_bytes() != wanted:
        raise RuntimeError(f"{out}: cleared the route with different rewards; see route.txt")

    # Capture bytes are OS-specific. The committed macOS images exercise the SDL software path.
    manifest = RESULTS / "macos-arm64-remake-sha256.tsv"
    if sys.platform != "darwin" or not (out / "captures").is_dir():
        return
    for line in manifest.read_text(encoding="utf-8").splitlines():
        name, wanted = line.split("\t")
        actual = hashlib.sha256((out / name).read_bytes()).hexdigest()
        if actual != wanted:
            raise RuntimeError(f"{out}: capture {name} changed ({actual}, expected {wanted})")


if __name__ == "__main__":
    if sys.argv[1] == "play":
        os.environ["MONOON_USER_DATA"] = str(Path(sys.argv[-1]).resolve() / "storage")
    main(ROUTE, events())
    if sys.argv[1] in ("play", "editor"):
        check_baseline(Path(sys.argv[-1]))

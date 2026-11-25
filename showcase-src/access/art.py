# Rebuilds samples/AccessLab/Assets/Showcase from the other showcases (nothing generated): grass, the hero and the herbalist,
# ParticleLab's rocket launcher and its two particle assets (the rocket keeps its burst sub-emitter id), the HUD panel, and a
# star field drawn here.  Run:  python3 showcase-src/access/art.py
import pathlib, random, shutil, sys
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]))
from pngio import save

SAMPLES = pathlib.Path(__file__).resolve().parents[2] / "samples"
OUT = SAMPLES / "AccessLab/Assets/Showcase"
OUT.mkdir(parents=True, exist_ok=True)
for src, name in (("InventoryLab/Assets/Showcase/Grass.png", "Grass.png"), ("InventoryLab/Assets/Showcase/Herbalist.png", "Herbalist.png"),
                  ("AnimatorLab/Assets/Showcase/HeroIdleE.png", "HeroIdleE.png"), ("ParticleLab/Assets/Showcase/Launcher.png", "Launcher.png"),
                  ("ParticleLab/Assets/Showcase/Rockets.particles", "Rockets.particles"), ("ParticleLab/Assets/Burst.particles", "Burst.particles"),
                  ("ParticleLab/Assets/Burst.particles.meta", "Burst.particles.meta"), ("PlatformerLab/Assets/Ui/UiShade.png", "Panel.png")):
    shutil.copy(SAMPLES / src, OUT / name)
# The festival rockets climb higher than ParticleLab's (the burst sits in open sky above the speech bubble).
rockets = (OUT / "Rockets.particles").read_text()
assert "speed: {min: 150, max: 170}" in rockets and "lifetime: {min: 0.85, max: 0.95}" in rockets
(OUT / "Rockets.particles").write_text(rockets.replace("speed: {min: 150, max: 170}", "speed: {min: 235, max: 250}")
                                       .replace("lifetime: {min: 0.85, max: 0.95}", "lifetime: {min: 0.95, max: 1.05}"))

# Stars: 64 × 32 art pixels tiled over the sky, a few soft dots.
rng = random.Random(3)
sky = [[(0, 0, 0, 0)] * 64 for _ in range(32)]
for _ in range(14):
    x, y = rng.randrange(64), rng.randrange(32)
    sky[y][x] = (236, 240, 255, rng.choice((120, 180, 255)))
save(OUT / "Stars.png", 128, 64, [bytearray(b"".join(bytes(c) * 2 for c in line)) for line in sky for _ in (0, 1)])
print("wrote", len(list(OUT.iterdir())), "files")

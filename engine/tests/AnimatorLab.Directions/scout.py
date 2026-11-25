"""Independent direction model for AnimatorLab.Directions (the Scout.animator rule written out)."""
PROJECT = '793807d6-c20d-4c32-982f-08aa2e216b01'
DIRS = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)]


# Independent direction model (the controller rule written out): keep the current direction while the vector stays within
# 30 degrees (8 directions) or 60 degrees (4 directions) of it, otherwise take the nearest; a zero vector keeps the facing
# (a 4-direction set shows a diagonal facing as the axis before it clockwise: NE as N).
def pick(facing, count, x, y):
    def score(i):
        dx, dy = DIRS[i * 8 // count]
        dot = x * dx + y * dy
        return dot * dot / (dx * dx + dy * dy) if dot > 0 else -1
    slot = facing * count // 8
    length = x * x + y * y
    if length > 0 and score(slot) < (0.75 if count == 8 else 0.25) * length:
        slot = max(range(count), key=lambda i: (score(i), -i))
    return slot * 8 // count

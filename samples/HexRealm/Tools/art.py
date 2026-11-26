#!/usr/bin/env python3
"""Paint the HexRealm board, pieces and interface using the local showcase toolchain."""
from math import cos, exp, pi, sin
from struct import pack
from wave import open as wav_open
from pathlib import Path
from random import Random
from uuid import UUID, uuid5

from PIL import Image, ImageChops, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'Assets'
NS = UUID('cbb32c55-fb51-581f-84c0-8c71d1f74721')
TEAL = '#56ccad'
RUST = '#ee956a'


def uid(name):
    return str(uuid5(NS, name))


def asset(name, content):
    path = ASSETS / name
    path.parent.mkdir(parents=True, exist_ok=True)
    if isinstance(content, str):
        path.write_text(content, encoding='utf-8')
    else:
        content.save(path, optimize=True)
    path.with_suffix(path.suffix + '.meta').write_text(f'id: {uid(name)}\n', encoding='utf-8')
    return uid(name)


def canvas(width, height, bg=(0, 0, 0, 0)):
    im = Image.new('RGBA', (width, height), bg)
    return im, ImageDraw.Draw(im)


W, H = 48, 56
HEX = [(24, 0), (47, 14), (47, 42), (24, 55), (0, 42), (0, 14)]
EDGES = list(zip(HEX[1:] + HEX[:1], HEX[2:] + HEX[:2]))


def fir(d, x, y, h):
    d.ellipse((x - h//2, y - 3, x + h//2 + 2, y + 1), fill='#395e55')
    d.line((x, y - 4, x, y + 1), fill='#374638')
    d.polygon([(x, y - h), (x - h//2, y - 2), (x + 1, y), (x + h//2, y - 2)], fill='#244a43')
    d.polygon([(x, y - h), (x - h//2, y - 2), (x, y - 3)], fill='#528467')
    d.line((x, y - h + 4, x - h//3, y - 4), fill='#87a976')


def meadow(d, rng, palette):
    for _ in range(47):
        x, y = rng.randint(0, 47), rng.randint(0, 55)
        c = rng.choice(palette)
        d.line((x, y, x + rng.randint(1, 5), y - 1), fill=c)
    for _ in range(8):
        x, y = rng.randrange(48), rng.randrange(56)
        d.point((x, y), fill='#c9c77e')


def terrain(kind, variant):
    pal = {
        1: ('#819c66', '#778f5e', '#8ba66b', '#9cb477'),
        2: ('#67866b', '#557767', '#749370', '#8aa57a'),
        3: ('#a7996f', '#93865f', '#b5aa79', '#c1ae81'),
        5: ('#738384', '#687977', '#84948d', '#99a69c'),
        6: ('#bb9b6d', '#ad895e', '#c8a778', '#d5b184'),
    }[kind]
    im, d = canvas(W, H, pal[0])
    rng = Random(1058 + kind * 31 + variant)
    d.polygon([(0, 0), (36, 0), (28, 10), (33, 18), (11, 26), (0, 19)], fill=pal[2])
    d.polygon([(7, 51), (15, 44), (11, 37), (33, 32), (48, 38), (48, 56)], fill=pal[1])
    meadow(d, rng, pal[1:])
    if kind == 2:
        trees = [(x+rng.randint(-3, 3), y+rng.randint(-2, 2), rng.randint(9, 15))
                 for y in (19, 28, 37, 45) for x in (8, 19, 29, 39)
                 if not (y in (19, 45) and x in (8, 39))]
        for x, y, h in sorted(trees, key=lambda p: p[1]):
            fir(d, x, y, h)
    if kind in (3, 5):
        for x, y, h in [(22, 35, 22), (31, 44, 15), (14, 43, 13)]:
            x += variant - 1
            if kind == 5:
                peak = (x-2, y-h)
                d.polygon([(x-13,y), peak, (x+11,y+1), (x+2,y+5)], fill='#566c72')
                d.polygon([(x-13,y), peak, (x+1,y+1), (x-2,y+3)], fill='#a0aaa2')
                d.polygon([(x-7,y-h+10), peak, (x+4,y-h+10), (x+2,y-h+8), (x+1,y-h+13), (x-3,y-h+8)], fill='#e3e7cf')
                d.line((x+2,y+5,x+11,y+1), fill='#455c61')
            else:
                d.arc((x-13,y-h+5,x+14,y+5), 192, 340, fill='#d6c38a', width=2)
                d.arc((x-12,y-h+8,x+14,y+9), 189, 353, fill='#766d51', width=2)
                d.arc((x-8,y-h+13,x+10,y+5), 200, 330, fill=pal[3])
    if kind == 6:
        for x, y in [(4, 21), (13, 28), (20, 39)]:
            d.arc((x, y-8, x+24, y+9), 205, 315, fill='#e0bc81')
            d.arc((x+2, y-4, x+22, y+10), 197, 329, fill='#9c7955')
    if kind == 1 and variant == 2:
        fir(d, 30, 43, 10)
        fir(d, 35, 38, 11)
    mask = Image.new('L', im.size)
    ImageDraw.Draw(mask).polygon(HEX, fill=255)
    im.putalpha(mask)
    d.line(HEX + HEX[:1], fill=pal[1])
    return im


def inset(p, amount):
    x, y = p
    return (round(24 + (x - 24) * amount), round(28 + (y - 28) * amount))


def overlay(mask, ink, shadow, width=2, fill=None):
    im, d = canvas(W, H)
    if fill:
        d.polygon([inset(p, .95) for p in HEX], fill=fill)
    for i, (a, b) in enumerate(EDGES):
        if mask & 1 << i:
            line = [inset(a, .89), inset(b, .89)]
            d.line(line, fill=shadow, width=width+2)
            d.line(line, fill=ink, width=width)
            for x, y in line:
                d.point((x, y), fill=ink)
    return im


def coast(mask):
    im, d = canvas(W, H)
    for i, (a, b) in enumerate(EDGES):
        if mask & 1 << i:
            d.line([inset(a, .96), inset(b, .96)], fill='#315e65', width=5)
            d.line([inset(a, .87), inset(b, .87)], fill='#d4c490', width=3)
            d.line([inset(a, .80), inset(b, .80)], fill='#b5b581', width=1)
    return im


def cloud(variant):
    # Wash and paper fibres on the chart's uncharted area.
    im, d = canvas(W, H, ['#91978a', '#93988c', '#909789'][variant])
    rng = Random(805 + variant)
    for index, y in enumerate((-4, 14, 32, 49)):
        edge = [(x, y + round(5 * sin(x / 13 + variant * 1.5 + index))) for x in range(-3, W+4, 3)]
        shade = ('#a4aa9b', '#9ea696', '#868f86', '#a0a698')[index]
        d.polygon(edge + [(x, h + 10) for x, h in reversed(edge)], fill=shade)
        d.line([(x, h+2) for x, h in edge], fill='#aab0a0')
    for _ in range(80):
        x, y = rng.randrange(W), rng.randrange(H)
        length = rng.choice((0, 0, 1, 2))
        d.line((x, y, x+length, y), fill=rng.choice(['#8e978e', '#acb1a2', '#9fa698']))
    mask = Image.new('L', im.size)
    ImageDraw.Draw(mask).polygon(HEX, fill=255)
    im.putalpha(mask)
    return im


def dry_fog():
    im, d = canvas(W, H)
    d.polygon(HEX, fill=(108, 111, 105, 90))
    return im


def rival_tint():
    im, d = canvas(W, H)
    d.polygon(HEX, fill=(220, 99, 72, 37))
    return im


def border_preview():
    im, d = canvas(W, H)
    d.polygon([inset(p, .91) for p in HEX], fill=(244, 217, 154, 64))
    for a, b in EDGES:
        a, b = inset(a, .90), inset(b, .90)
        for j in range(4):
            p = tuple(round(x + (y - x) * j / 4) for x, y in zip(a, b))
            q = tuple(round(x + (y - x) * (j + .63) / 4) for x, y in zip(a, b))
            d.line([p, q], fill='#433b2b', width=4)
            d.line([p, q], fill='#fce9ac', width=2)
    return im


def claim_ink(side):
    count = 8
    sheet, _ = canvas(W * count, H)
    rng = Random(1178)
    spokes = [rng.uniform(.79, 1.22) for _ in range(36)]
    fibers = [(rng.randrange(W), rng.randrange(H), rng.randint(1, 3)) for _ in range(90)]
    edge = [(24 + cos(i * pi / 18) * 28 * jitter, 28 + sin(i * pi / 18) * 30 * jitter)
            for i, jitter in enumerate(spokes)]
    clip = Image.new('L', (W, H))
    ImageDraw.Draw(clip).polygon([inset(p, .96) for p in HEX], fill=255)
    for i in range(count):
        t = i / (count - 1)
        radius = .16 + 1.17 * t ** .66
        alpha = round(218 * (1 - t ** 2.2))
        color = (24, 125, 112) if side == 1 else (177, 67, 44)
        im, d = canvas(W, H)
        stain = Image.new('L', (W, H))
        ImageDraw.Draw(stain).polygon([inset(p, radius) for p in edge], fill=255)
        stain = ImageChops.multiply(stain, clip)
        d.polygon([inset(p, radius) for p in edge], fill=(*color, alpha),
                  outline=(*(component // 2 for component in color), alpha), width=2)
        for x, y, length in fibers:
            if stain.getpixel((x, y)):
                d.line((x, y, x + length, y), fill=(*(min(255, c + 43) for c in color), alpha))
        im.putalpha(ImageChops.multiply(im.getchannel('A'), stain))
        sheet.paste(im, (i * W, 0))
    return asset(f'Claim {side}.png', sheet)


def tile_set(name, frames):
    columns = 16
    atlas, _ = canvas(W * columns, H * ((len(frames) + columns - 1) // columns))
    rows = []
    for i, (im, key, cost) in enumerate(frames):
        x, y = i % columns * W, i // columns * H
        atlas.paste(im, (x, y))
        rows += [f'- id: {i+1}\n  source: {{x: {x}, y: {y}, width: {W}, height: {H}}}'
                 f'\n  terrain: {key}\n  moveCost: {cost}\n']
    image = asset(name + '.png', atlas)
    return asset(name + '.tileset', f'image: {image}\ntileSize: {{width: {W}, height: {H}}}\n'
                 f'nextId: {len(frames)+1}\ntiles:\n' + ''.join(rows))


def board():
    land = [(terrain(i, v), n, c) for i, n, c in
            [(1, 'meadow', 10), (2, 'forest', 15), (3, 'heath', 15), (5, 'ridge', 0), (6, 'steppe', 15)]
            for v in range(3)]
    ground = tile_set('Terrain', land)
    overlays = [(cloud(v), 'mist', 0) for v in range(3)]
    overlays.append((dry_fog(), 'explored', 0))
    overlays += [(overlay(63, color, shadow, width, fill), key, 0) for color, shadow, width, fill, key in [
        ('#79d5bc', (32, 70, 71, 60), 1, (102, 213, 171, 22), 'range'),
        ('#fff1b1', '#3e655b', 2, (255, 220, 121, 25), 'selected'),
        ('#fff9e0', (24, 55, 65, 130), 1, None, 'hover'),
        ('#f18b66', '#593837', 2, None, 'attack'),
    ]]
    overlays.extend((coast(m), 'shore', 0) for m in range(1, 64))
    for color, shadow in [(TEAL, '#1e6261'), (RUST, '#7a4c44')]:
        overlays.extend((overlay(m, color, shadow, 2), 'border', 0) for m in range(1, 64))
    for kind in range(3):
        im, d = canvas(W,H)
        if kind == 0:
            d.polygon([(11,27),(27,23),(38,31),(23,36)], fill='#6d7549', outline='#c8b983')
            for n in range(4):
                d.line((14+n*4,27+n*2,27+n*2,24+n*2), fill='#e5c88b', width=1)
        elif kind == 1:
            d.polygon([(15,28),(19,24),(27,25),(33,32),(22,36)], fill='#5a645c', outline='#bab087')
            d.line((21,32,27,23,29,26), fill='#e4c88f', width=2)
        else:
            d.line([(13,43),(17,35),(25,31),(38,22)], fill='#5b624f', width=4)
            d.line([(13,43),(17,35),(25,31),(38,22)], fill='#c4b287', width=2)
        overlays.append((im, 'district', 0))
    overlays.append((border_preview(), 'survey', 0))
    overlays.append((rival_tint(), 'rival tint', 0))
    # Nine equal opacity steps, interleaved so a tile keeps its paper grain as it clears.
    mist = [im for im, _, _ in overlays[:3]]
    for level in range(1, 9):
        for im in mist:
            faded = im.copy()
            faded.putalpha(im.getchannel('A').point([round(alpha * level / 9) for alpha in range(256)]))
            overlays.append((faded, 'revealing', 0))
    return ground, tile_set('MapInk', overlays)


def sea():
    width, height = 640, 360
    im, d = canvas(width, height)
    for y in range(height):
        t = sin(pi*y/height)
        d.line((0,y,width,y), fill=(91+int(21*t),113+int(20*t),108+int(20*t)))
    rng = Random(208)
    for _ in range(310):
        x, y = rng.randrange(width), rng.randrange(height)
        w = rng.randint(5, 20)
        d.arc((x,y,x+w,y+4), 198, 332, fill=rng.choice(['#8c9d8c','#7f9488','#789084']))
    for off in (7, 10):
        d.rectangle((off,off,width-1-off,height-1-off), outline='#d6be91' if off == 7 else '#645848')
    for x in range(20, 630, 20):
        d.line((x,7,x,11 if x%100 else 15),fill='#b4a37c')
        d.line((x,349,x,352),fill='#b4a37c')
    cx, cy, r = 69, 281, 32
    for k in (r, r - 4):
        d.ellipse((cx-k,cy-k,cx+k,cy+k), outline='#c6b18b')
    for angle in range(8):
        a = angle*pi/4
        tip = r+13 if angle%2==0 else r-4
        v = (cos(a),sin(a))
        p = (-v[1]*5,v[0]*5)
        d.polygon([(cx+tip*v[0],cy+tip*v[1]),(cx+p[0],cy+p[1]),(cx,cy)], fill='#ead5a5')
        d.polygon([(cx+tip*v[0],cy+tip*v[1]),(cx-p[0],cy-p[1]),(cx,cy)], fill='#5d5145')
    d.ellipse((cx-3,cy-3,cx+3,cy+3),fill='#f1d998',outline='#605041')
    return asset('Sea.png', im)


def frame(name, fill, edge, shadow=(56, 41, 28, 128)):
    im, d = canvas(36, 32)
    paper = [(2,5),(5,5),(8,2),(12,3),(15,2),(22,2),(28,3),(32,1),
             (33,8),(32,12),(32,20),(34,24),(31,28),(27,29),(23,27),
             (13,27),(8,29),(4,28),(5,24),(2,21),(3,16),(3,10)]
    d.polygon([(x + 1, y + 3) for x, y in paper], fill=shadow)
    d.polygon(paper, fill=fill, outline=edge)
    d.line([(5,8),(9,5),(13,5),(23,5),(30,4)], fill='#fff2cc')
    d.line([(6,24),(10,25),(14,24),(22,24),(28,25),(31,24)], fill=edge)
    for x, y in [(9,9),(6,19),(11,22),(27,8),(27,21),(29,10)]:
        d.line((x,y,x+1,y), fill=edge)
    return asset(name + '.png', im)


def control_trim(name, ink, chip=False):
    """A transparent button face exposes the engine's state colors."""
    im, d = canvas(24, 24)
    d.rounded_rectangle((0, 0, 23, 23), radius=4, fill='#dfc69a' if chip else None, outline=ink)
    d.line((5, 2, 18, 2), fill='#f9e9bd')
    d.line((5, 21, 18, 21), fill='#9b7954')
    d.line((4, 22, 19, 22), fill=ink)
    for x, y in ((3, 5), (6, 6), (19, 17), (21, 19)):
        d.point((x, y), fill=(104, 75, 48, 58))
    return asset(name + '.png', im)


def stat_icon(name):
    im, d = canvas(16, 16)
    if name == 'HP':
        d.polygon([(8, 14),(2, 8),(2, 4),(5, 2),(8, 4),(11, 2),(14, 4),(14, 8)], fill='#9f493b', outline='#653326')
        d.line((4, 5, 5, 4), fill='#f8d2a0')
    elif name == 'MP':
        d.polygon([(3, 12),(5, 3),(9, 3),(9, 8),(14, 9),(14, 13),(3, 13)], fill='#587467', outline='#2b5043')
        d.line((4, 11, 13, 11), fill='#b4c5a4')
    else:
        for x,y in ((5,5),(11,6)):
            d.ellipse((x-2,y-2,x+2,y+2),fill='#386452')
            d.rounded_rectangle((x-3,y+3,x+3,y+7),radius=2,fill='#386452')
    return asset(name + ' icon.png', im)


def ribbon(side):
    name = f'Ribbon {side}'
    im, d = canvas(132, 44)
    dark, light = ('#38736a', '#82b8a4') if side == 1 else ('#a2543c', '#dc9d77')
    d.polygon([(2,3),(131,3),(125,22),(131,43),(2,43),(8,22)], fill=(56,41,28,160))
    d.polygon([(0,0),(131,0),(124,20),(131,40),(0,40),(7,20)], fill=dark)
    d.rectangle((11,0,120,40), fill='#f1dcad', outline=light)
    d.line((12,40,119,40), fill='#a0825d')
    return asset(name + '.png', im)


def land_note():
    im, d = canvas(86, 24)
    edge = [(1,4),(7,2),(79,1),(85,4),(82,11),(85,19),(80,21),(7,21),(0,18),(3,10)]
    d.polygon([(x, y+2) for x,y in edge], fill=(56,41,28,160))
    d.polygon(edge, fill='#f5e1ad', outline='#88623d')
    d.line((10,3,76,3), fill='#fff0cd')
    return asset('Land note.png', im)


def icon(key):
    im, d = canvas(24,24)
    ink, hl = '#634832', '#f5deb0'
    if key == 'Coin':
        d.ellipse((3,3,20,20), fill='#755649', outline=ink, width=2)
        d.ellipse((6,6,17,17), fill='#b0824e', outline=hl)
        d.line((12,8,12,15), fill=hl, width=2)
    elif key == 'Hour':
        d.polygon([(6,4),(18,4),(16,9),(13,12),(17,17),(18,20),(6,20),(7,16),(11,12),(7,8)], fill='#e4c696', outline=ink)
        d.polygon([(8,7),(16,7),(12,11)], fill='#ad7f51')
        d.polygon([(8,18),(16,18),(12,14)], fill='#ad7f51')
        d.line((5,4,19,4),fill=ink,width=2)
        d.line((5,20,19,20),fill=ink,width=2)
    elif key == 'Lore':
        d.polygon([(3,5),(11,7),(13,7),(21,5),(21,17),(13,19),(11,19),(3,17)], fill='#7d6e55', outline=hl)
        d.line((12,8,12,19), fill=hl)
        for y in (10,13,16):
            d.line((5,y,9,y+1),fill=ink)
            d.line((15,y+1,19,y),fill=ink)
    elif key == 'Land':
        d.polygon([(3,8),(10,3),(19,5),(21,14),(14,20),(5,17)], outline=ink)
        d.line((5,14,10,9,14,12,18,8),fill=ink,width=2)
    elif key == 'Crest':
        d.polygon([(9,1),(15,1),(21,4),(22,8),(23,14),(20,20),(14,23),
                   (8,22),(3,20),(1,13),(2,7),(5,3)],fill='#9d4834',outline='#6d3229')
        d.ellipse((4,4,19,19),fill='#ab513b',outline='#d1815d')
        d.line((6,15,10,10,12,13,16,7,19,12),fill=hl,width=2)
        d.line((7,17,17,17),fill='#dd9a71')
    elif key == 'Pin':
        d.ellipse((8,7,18,17),fill='#ae9066')
        d.ellipse((6,5,16,15),fill='#9f4f40',outline='#62392d')
        d.arc((7,6,15,14),190,310,fill='#e79b70',width=2)
    return asset(key + '.png', im)


def house(d, x, y, size, roof, tower=False):
    w = size
    h = 12 if tower else 7
    d.polygon([(x-w,y),(x+w,y),(x+w-2,y+2),(x-w+2,y+2)],fill='#3d5851')
    d.rectangle((x-w,y-h,x+w,y),fill='#dac39b',outline='#4e5955')
    d.polygon([(x-2,y-h),(x+w,y-h),(x+w,y),(x-2,y)],fill='#a8997a')
    d.polygon([(x-w-1,y-h),(x-2,y-h-7),(x+w+1,y-h),(x+2,y-h+2)],fill=roof,outline='#374b4d')
    d.line((x-2,y-h-6,x-w,y-h),fill='#e0d097')
    d.rectangle((x-1,y-4,x+1,y),fill='#42535a')


def town(side, level):
    im, d = canvas(50, 43)
    roof = '#71b9a4' if side == 1 else '#d58a64'
    d.ellipse((2,26,47,40), fill='#495e52', outline='#bac293')
    d.line((8,35,17,33,24,37,37,35),fill='#8a9373',width=2)
    if level >= 2:
        house(d, 37, 28, 6, roof)
    if level >= 3:
        house(d, 12, 31, 7, roof, True)
    house(d, 25, 34, 9, roof, level >= 2)
    d.line((28,7,28,23),fill='#e7d8ad')
    d.polygon([(28,7),(40,9),(35,12),(28,12)], fill=roof)
    return asset(f'Towns/{side}-{level}.png', im)


def piece(kind, side, state, phase):
    im,d = canvas(38,42)
    ink, light = ('#32716b','#80d1b7') if side == 1 else ('#a25343','#eea278')
    d.ellipse((2,29,35,39),fill='#142733',outline='#caaf83')
    d.ellipse((4,29,33,36),fill=ink,outline=light)
    stride = [0,2,0,-2][phase] if state == 'Walk' else 0
    rise = ([0,1,1,0][phase] if state in ('Idle','Walk') else 0) + (1 if stride else 0)
    shift = [0,-1,3,1][phase] if state == 'Strike' else 0
    x,y = 18+shift, 30-rise
    if kind == 'Settler':
        d.rectangle((8+shift,y-15,28+shift,y-5),fill='#c7ad80',outline='#393c42')
        d.polygon([(6+shift,y-13),(10+shift,y-21),(25+shift,y-21),(30+shift,y-13)],fill=light,outline=ink)
        d.line((13+shift,y-19,12+shift,y-14),fill='#ebdaba',width=2)
        if state == 'Idle':
            d.line((26+shift,y-19,28+shift,y-18+phase%2),fill='#dcce9c')
        for a in (10+shift,26+shift):
            d.ellipse((a-4,y-9,a+4,y-1),fill='#494547',outline='#e1b87d')
            d.line((a-2+stride,y-4,a+2,y-6),fill='#dfbd88')
    else:
        # A fixed silhouette and palette for the miniature. Animate feet, mantle and equipment independently.
        d.line((x-3,y-7,x-5+stride,y-1),fill='#29343b',width=4)
        d.line((x+4,y-7,x+6-stride,y-1),fill='#29343b',width=4)
        d.line((x-7+stride,y-1,x-2+stride,y-1),fill='#805c4a',width=2)
        d.line((x+4-stride,y-1,x+9-stride,y-1),fill='#a77f61',width=2)
        d.polygon([(x-7,y-18),(x+3,y-20),(x+7,y-14),(x+6,y-5),(x-8,y-7)],fill=ink,outline='#263f46')
        d.polygon([(x-6,y-18),(x-3,y-16),(x-4,y-9),(x-8,y-7)],fill=light)
        d.line((x-6,y-10,x+5,y-11),fill='#d3b076',width=2)
        d.ellipse((x-6,y-27,x+5,y-17),fill='#d6a57c',outline='#404b50')
        d.polygon([(x-7,y-22),(x-5,y-28),(x-1,y-30),(x+4,y-27),(x+7,y-23),(x+2,y-22)],
                  fill=light if kind in ('Scout','Archer') else '#bec9bf',outline='#455a5b')
        d.line((x-5,y-25,x-1,y-28,x+2,y-27), fill='#eff1cf')
        d.point((x+3,y-21),fill='#40383a')
        if state == 'Idle':
            d.line((x-7,y-15,x-9-(phase in (1,2)),y-8+(phase % 2)), fill=light)
            d.point((x+2+phase%2,y-25), fill='#f4dfac')
        if kind == 'Spear':
            d.ellipse((x-9,y-16,x-1,y-7),fill=ink,outline='#dfc495')
            d.point((x-5,y-12),fill='#f1dd9f')
            tipy = y-31 if state != 'Strike' or phase < 2 else y-21
            tipx = x+12 if tipy == y-31 else min(36,x+16)
            d.line((x+7,y-4,tipx,tipy),fill='#593e37',width=3)
            d.line((x+7,y-4,tipx,tipy),fill='#debd83')
            d.polygon([(tipx-2,tipy+4),(tipx,tipy-4),(tipx+2,tipy+1)],fill='#e3efdb')
        elif kind == 'Archer':
            if state == 'Shoot':
                hand = [x+8, x+3, x+6, x+10][phase]
                limb = [(x+9,y-27),(x+14,y-23),(x+16,y-17),(x+16,y-14),(x+14,y-9),(x+9,y-5)]
                d.line(limb, fill='#453339', width=3)
                d.line(limb, fill='#e8bd86', width=1)
                d.line([(x+9,y-27),(hand,y-16),(x+9,y-5)], fill='#f4e2b9')
                d.line([(x+4,y-17),(x+9,y-13),(x+15,y-15)], fill='#453339', width=4)
                d.line([(x+4,y-17),(x+9,y-13),(x+15,y-15)], fill=light, width=2)
                d.line((x-3,y-16,hand,y-16), fill='#986653', width=2)
                d.point((hand,y-16), fill='#f5cea0')
                if phase < 2:
                    d.line((hand,y-17,x+18,y-17), fill='#fff1bb')
                    d.point((x+19,y-17),fill='#ebf2d7')
            else:
                d.arc((x+3,y-26,x+17,y-7),268,85,fill='#e8bd86',width=2)
                d.line((x+10,y-25,x+10,y-8),fill='#f4e2b9')
                d.line((x+4,y-15,x+15,y-16),fill='#452f31')
        elif kind == 'Worker':
            d.line((x+8,y-8,x+12,y-23),fill='#d9b184',width=2)
            d.rectangle((x+8,y-25,x+16,y-21),fill='#a5b4ae',outline='#464d50')
        else:
            d.polygon([(x+7,y-15),(x+12,y-17),(x+15,y-14),(x+11,y-12)],fill='#e8cf98',outline='#66584e')
    return im


def units(kind, side):
    key = f'Pieces/{side}-{kind}'
    sheet, _ = canvas(38*4,42*3)
    action = 'Shoot' if kind == 'Archer' else 'Strike'
    states = ['Idle','Walk',action]
    for row, state in enumerate(states):
        for i in range(4):
            sheet.paste(piece(kind,side,state,i),(i*38,row*42))
    sprite = asset(key+'.png', sheet)
    clips = {}
    for row, state in enumerate(states):
        lengths = [.35,.22,.32,.27] if row == 0 else [.08]*4 if row == 1 else [.08,.08,.065,.10] if action == 'Shoot' else [.06]*4
        frames = ''.join(f'- sprite: {sprite}\n  seconds: {seconds}\n  useRenderer: false\n'
                         f'  source: {{x: {i*38}, y: {row*42}, width: 38, height: 42}}\n' +
                         ('  event: Loose\n' if state == 'Shoot' and i == 2 else '')
                         for i, seconds in enumerate(lengths))
        clips[state]=asset(f'{key}-{state}.clip', f'frames:\n{frames}loop: {str(row < 2).lower()}\n')
    ctrl = asset(key + '.animator', 'entry: Idle\nstates:\n' +
                 ''.join(f'- {{name: {state}, clip: {clip}}}\n' for state,clip in clips.items()) +
                 f'''parameters:
- {{name: Moving, kind: Bool, default: 0}}
- {{name: {action}, kind: Trigger, default: 0}}
transitions:
- {{from: "Any State", to: {action}, parameter: {action}, test: Triggered}}
- {{from: Idle, to: Walk, parameter: Moving, test: "True"}}
- {{from: Walk, to: Idle, parameter: Moving, test: "False"}}
- {{from: {action}, to: Idle, parameter: "", test: "", exitTime: 1}}
''')
    portrait = asset(f'{key}-Portrait.png', piece(kind,side,'Idle',0))
    return sprite,ctrl,portrait

def sound(name, notes, duration, noise=0):
    """Short round attacks, a fading harmonic and a quiet transient at 16 kHz."""
    rate = 16000
    rng = Random(411)
    pcm = bytearray()
    for frame in range(round(rate * duration)):
        t = frame / rate
        sample = 0.0
        for onset, hz, gain in notes:
            age = t - onset
            if age >= 0:
                phase = 2 * pi * hz * age
                envelope = min(1, age / .004) * exp(-19 * age)
                sample += gain * envelope * (sin(phase) + .24 * sin(phase * 2.41))
        sample += noise * min(1, t / .003) * exp(-42 * t) * rng.uniform(-1, 1)
        sample *= min(1, (duration - t) / .014)
        pcm += pack('<h', round(max(-1, min(1, sample)) * 32767))
    path = ASSETS / (name + '.wav')
    with wav_open(str(path), 'wb') as out:
        out.setparams((1, 2, rate, 0, 'NONE', 'not compressed'))
        out.writeframes(pcm)
    path.with_suffix('.wav.meta').write_text(f'id: {uid(path.name)}\n', encoding='utf-8')
    return uid(path.name)

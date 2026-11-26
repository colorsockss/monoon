"""Build LedgeRun's pixel assets and animation definitions using Python and Pillow."""
from pathlib import Path
from random import Random
from math import sin, tau
from uuid import UUID, uuid5
import wave
import struct
from PIL import Image, ImageDraw
from arcade import POST_CAP, courier, frames as courier_frames

ASSETS = Path(__file__).resolve().parents[1] / 'Assets'
NS = UUID('ce247eed-9c61-50a8-bdaf-486165fbaccc')


def asset_id(name):
    meta = ASSETS / (name + '.meta')
    if meta.exists():
        return meta.read_text().strip().split()[1]
    value = str(uuid5(NS, 'remake/' + name))
    meta.write_text(f'id: {value}\n')
    return value


def png(name, image, colors=None):
    path = name + '.png'
    if colors:
        image = image.quantize(colors, dither=Image.Dither.NONE)
    image.save(ASSETS / path, optimize=True)
    return asset_id(path)


def blank(width, height):
    image = Image.new('RGBA', (width, height))
    return image, ImageDraw.Draw(image)


def sheet(name, groups):
    """Write one indexed sheet per family and one clip per action."""
    first = next(iter(groups.values()))[0][0]
    width, height = first.size
    count = sum(len(frames) for frames, _ in groups.values())
    columns = min(count, 8)
    atlas = Image.new('RGBA', (columns * (width+2), ((count+columns-1)//columns) * (height+2)))
    sprite = asset_id(name+'.png')
    clips = {}
    index = 0
    for state, (frames, seconds) in groups.items():
        clip_name = name + state
        body = []
        for image in frames:
            x, y = (index % columns) * (width+2) + 1, (index // columns) * (height+2) + 1
            atlas.paste(image, (x, y))
            region = f'{{x: {x}, y: {y}, width: {width}, height: {height}}}'
            body.append(f'- sprite: {sprite}\n  seconds: {seconds}\n  source: {region}\n')
            index += 1
        path = clip_name + '.clip'
        loop = name != 'Courier' or state not in {'Ready', 'Win'}
        (ASSETS / path).write_text('frames:\n' + ''.join(body) + f'loop: {str(loop).lower()}\n')
        clips[state] = asset_id(path)
    png(name, atlas, 64)
    return clips


def animator(name, states):
    path = name + '.animator'
    entry = next(iter(states))
    body = f'entry: {entry}\nstates:\n'
    for state, sequence in states.items():
        body += f'- name: {state}\n  clip: {sequence}\n'
    body += 'parameters:\n- name: Pose\n  kind: Int\n  default: 0\ntransitions:\n'
    # Explicit sources also exclude self-transitions on the active state.
    for source in states:
        for pose, target in enumerate(states):
            if target != source:
                body += f'- from: {source}\n  to: {target}\n  parameter: Pose\n  test: Equal\n  value: {pose}\n'
    (ASSETS / path).write_text(body)
    asset_id(path)


INK = '#262234'
GOLD = '#f2c879'


def characters():
    groups = {state: ([courier(pose) for pose in poses], seconds)
              for state, (poses, seconds) in courier_frames().items()}
    png('CourierIdle1', groups['Idle'][0][0])
    dash = groups['Dash'][0][0]
    ghost = Image.new('RGBA', dash.size, '#ffc36a')
    ghost.putalpha(dash.getchannel('A').point(lambda value: value * 95 // 255))
    png('DashGhost', ghost, 8)
    animator('Courier', sheet('Courier', groups))

    def beetle(frame, charge=False, hurt=False, guardian=False):
        im, d = blank(32, 23)
        y = 1 if frame in (1,3) else 0
        shell = '#35435b' if guardian else '#573c69'
        edge = '#70bcb3' if guardian else '#b587b5'
        for leg in range(3):
            x = 9 + leg * 6
            tip = x + ((frame + leg) % 4) - 2
            d.line([(x,15),(x-1,18),(tip,21)], fill=INK, width=3)
            d.point((tip,21), fill=edge)
        d.polygon([(2,14),(4,8+y),(9,4+y),(20,3+y),(26,7+y),(27,16),(22,19),(8,18)],
                  fill=INK)
        d.polygon([(4,13),(6,8+y),(11,5+y),(20,4+y),(25,8+y),(24,15),(19,17),(8,16)], fill=shell)
        d.line([(7,10+y),(11,6+y),(18,5+y),(22,6+y)], fill=edge, width=2)
        d.line([(19,6+y),(18,10),(13,15)], fill=INK)
        d.line([(22,14),(27,11),(30,8 if charge else 10)], fill='#e88b68' if charge else edge)
        d.polygon([(21,13),(26,11),(30,13),(30,17),(26,19),(22,17)], fill=INK)
        d.line([(25,14),(28,14)], fill='#ffda83' if not hurt else '#ff957a', width=2)
        d.point((11,10+y), fill=GOLD if guardian else edge)
        if guardian:
            d.polygon([(9,5+y),(8,1+y),(13,4+y),(16,y),(19,4+y),(23,2+y),(22,6+y)], fill=GOLD)
        return im

    for name in ('Beetle', 'Warden'):
        groups = {state: ([beetle(i, state == 'Tell', state == 'Stagger', name == 'Warden')
                           for i in range(count)], .09 if state == 'Tell' else .13)
                  for state, count in [('Move',4),('Tell',4),('Stagger',2)]}
        png(name+'Move1', groups['Move'][0][0])
        if name == 'Warden':
            png('ArcadeWarden', groups['Move'][0][0].crop((2,0,31,22)), 24)
        animator(name, sheet(name+'Frames', groups))
        # The controller keeps its display name while the sheet and clips share a stem.

    tokens = []
    for i, (w, lit) in enumerate([(10, True), (8,True), (4,False), (2,False), (4,False), (8,False)]):
        im, d = blank(12,12)
        left = 6 - w // 2
        d.rounded_rectangle((left,1,left+w-1,10), radius=min(w//2,3), fill='#643a32')
        d.line((left+1,2,left+1,9), fill='#be7138')
        if w > 3:
            d.rounded_rectangle((left+1,2,left+w-2,8), radius=2, fill='#f6c55e')
        if lit:
            d.line((5,4,5,7), fill='#9d5e36')
            d.point((6,3), fill='#fff3ad')
        tokens.append(im)
    png('Token',tokens[0])
    sheet('TokenFrames', {'Turn':(tokens,.095)})
    turn = (ASSETS / 'TokenFramesTurn.clip').read_text().removesuffix('loop: true\n').split('- sprite: ')
    header, frames = turn[0], turn[1:]
    name = 'TokenCounterphase.clip'
    (ASSETS / name).write_text(header + ''.join('- sprite: ' + frame for frame in frames[2:] + frames[:2]) + 'loop: true\n')
    asset_id(name)

    flags = {}
    for active in [False,True]:
        frames = []
        for i, curl in enumerate([0,2,1,-1]):
            im, d = blank(24,32)
            d.polygon([(1,31),(3,27),(5,27),(6,24),(12,24),(13,27),(16,28),(17,31)], fill='#344052')
            d.line((4,29,14,29), fill='#859287')
            d.rectangle((7,3,9,26), fill='#4a3340')
            d.line((8,3,8,25), fill='#c5a16d')
            d.rectangle((7,1,9,3), fill=GOLD)
            d.polygon([(9,5),(15,6),(23,5+curl),(20,10+curl),(23,13+curl),(15,15),(9,14)],
                      fill='#30756f' if active else '#4c5262', outline=INK)
            d.line([(10,6),(15,7),(21,6+curl)], fill='#83d5ac' if active else '#8c8d8c')
            d.line((14,9,14,12), fill='#f3d784' if active else '#c0b7a1')
            d.line((12,10,16,10), fill='#f3d784' if active else '#c0b7a1')
            frames.append(im)
        state = 'Lit' if active else 'Dim'
        png('Flag' + state + '1', frames[0])
        flags[state] = (frames, .14 if active else .16)
    animator('Flag', sheet('FlagFrames', flags))


def ui():
    for name, fill, shine in [
        ('Panel','#fff6dc','#ffffff'), ('ArcadeSky','#9ce1f6','#d8f6ff'),
        ('ArcadeSun','#ffd66f','#fff0a9'), ('ArcadeHeart','#ffe2d0','#fff4ec'),
        ('ArcadeEmpty','#7ba3be','#a8c6d8'), ('ArcadeMeter','#fb705c','#ffac91'),
        ('ArcadeFocus','#ffd984','#fff4bd'),
        ('ArcadeAction','#9ce1f6','#e0f9ff'), ('ArcadePressed','#8bcbe0','#cceef7'),
        ('ArcadeNew','#fff0b6','#fffbdd'),
    ]:
        im,d = blank(26,26)
        d.rounded_rectangle((0,3,25,25), 11, fill='#634833' if name == 'ArcadeFocus' else '#32425f')
        d.rounded_rectangle((0,0,25,22), 11, fill=INK)
        d.rounded_rectangle((2,2,23,20), 9, fill=fill)
        d.line((10,3,15,3), fill=shine, width=2)
        if name in ('Panel', 'ArcadeFocus', 'ArcadeAction', 'ArcadePressed'):
            d.line((8,19,17,19), fill={
                'Panel':'#f0dfb3', 'ArcadeFocus':'#e3a34f', 'ArcadeAction':'#6db9d5',
                'ArcadePressed':'#5ea7c3',
            }[name])
        png(name,im,12)

    key, d = blank(18, 22)
    d.rounded_rectangle((0, 2, 17, 21), 4, fill='#32425f')
    d.rounded_rectangle((0, 0, 17, 19), 4, fill=INK)
    d.rounded_rectangle((1, 1, 16, 18), 3, fill='#eefaff')
    d.line((4, 2, 13, 2), fill='#ffffff')
    d.line((4, 17, 13, 17), fill='#b8d8e5')
    png('ArcadeKey', key, 8)

    def icon(name, contour, fill, highlight):
        im,d = blank(16,16)
        d.polygon(contour, fill=fill, outline=INK)
        d.line(highlight, fill='#fff7df', width=2)
        png(name, im)

    icon('HeartIcon',[(1,4),(4,1),(6,1),(8,3),(10,1),(12,1),(14,4),(14,8),(8,14),(1,8)],
         '#f45062',[(4,5),(4,4),(5,3)])
    icon('GemIcon',[(8,0),(14,5),(14,9),(8,15),(2,9),(2,5)],
         '#49c5dd',[(5,6),(8,2),(10,4)])
    icon('TokenIcon',[(5,1),(10,1),(14,5),(14,10),(10,14),(5,14),(1,10),(1,5)],
         '#f3a744',[(4,8),(4,5),(6,3),(9,3)])
    icon('ArcadePost',[(1,3),(14,3),(15,4),(15,12),(14,13),(1,13),(0,12),(0,4)],
         '#ffd36d',[(2,5),(8,9),(13,5)])

    im,d = blank(32,32)
    d.polygon([(16,0),(20,4),(26,3),(27,8),(32,12),(29,16),(31,23),(25,25),
               (23,30),(17,28),(11,31),(8,26),(2,25),(4,19),(0,15),(5,11),(5,5),(12,5)],
              fill=INK)
    d.ellipse((4,3,28,28),fill='#f45855')
    d.ellipse((7,6,25,25),fill='#ffd760',outline=INK)
    d.polygon([(8,17),(8,12),(11,10),(19,10),(22,13),(22,15),(24,16),
               (24,18),(16,20),(10,19)],fill=POST_CAP[1],outline=INK)
    d.line([(10,13),(13,11),(18,11)],fill=POST_CAP[2])
    d.line((17,17,22,17),fill=POST_CAP[2])
    d.rectangle((15,12,20,15),fill='#fff1c6',outline=POST_CAP[0])
    d.point((17,13),fill=POST_CAP[1])
    png('ArcadeSeal',im,12)

    im,d = blank(64,42)
    d.polygon([(0,36),(16,15),(24,21),(38,3),(61,30),(63,37),(56,40),(9,40)],
              fill='#327c8e',outline=INK)
    d.polygon([(16,17),(22,23),(16,22),(10,25)],fill='#d7f3f1')
    d.polygon([(38,5),(47,16),(41,14),(38,18),(34,14),(29,18)],fill='#d7f3f1')
    d.line([(3,35),(13,31),(23,33),(25,37),(40,37),(47,32),(58,32)],fill='#ffd88a',width=2)
    d.polygon([(24,24),(53,18),(56,20),(59,31),(57,33),(28,39),(26,37)],
              fill='#fff2cc',outline=INK)
    d.line([(25,25),(42,31),(54,20)],fill='#a87961')
    d.rectangle((40,29,44,32),fill='#f45855',outline=INK)
    d.line((13,28,21,26),fill='#fff2cc')
    png('ArcadeTrailLogo',im,16)

    for name,shape,color in [
        ('Meadow',[(1,10),(5,3),(7,6),(9,4),(12,10)],'#488667'),
        ('Cavern',[(1,10),(1,6),(4,3),(8,3),(11,6),(11,10),(8,10),(7,7),(5,7),(4,10)],'#835b99'),
        ('Tower',[(2,10),(2,3),(4,3),(4,5),(6,5),(6,3),(8,3),(8,5),(10,5),(10,10)],'#477da2'),
        ('Summit',[(3,11),(3,2),(10,2),(9,4),(11,6),(5,6),(5,11)],'#d75360'),
    ]:
        im,d = blank(13,13)
        d.rounded_rectangle((0,0,12,12),3,fill='#fff1bc',outline=INK)
        d.polygon(shape,fill=color,outline=INK)
        png('ArcadeStage'+name,im,8)
        locked,d = blank(13,13)
        d.polygon(shape,fill='#6b859b',outline='#344d62')
        png('ArcadeLocked'+name,locked,8)

    im,d = blank(24,57)
    d.rectangle((1,0,22,2), fill='#826a5d')
    d.rectangle((3,2,20,4), fill='#c6a76a')
    d.polygon([(4,4),(19,4),(19,51),(12,56),(4,51)], fill='#273b4e', outline='#253040')
    d.line((5,7,5,49), fill='#6a9b93')
    d.line((18,7,18,49), fill='#806f79')
    d.polygon([(7,29),(12,17),(17,29),(16,32),(8,32)], fill='#d3aa69')
    d.polygon([(8,29),(12,21),(14,25),(12,25)], fill='#f5dba3')
    d.line((7,38,16,38), fill='#9ba6a1')
    png('TowerBanner',im)

    # A carved trail marker, silhouetted with wide negative space around the arrow.
    im,d = blank(22,23)
    d.rectangle((9,7,12,22), fill='#674d47')
    d.line((10,8,10,22), fill='#ac7856')
    d.polygon([(1,4),(14,3),(21,9),(15,14),(1,14)], fill='#363343')
    d.polygon([(2,5),(14,4),(19,9),(14,12),(2,12)], fill='#8e6354')
    d.line((4,8,15,8), fill='#f2d58e', width=2)
    d.polygon([(13,5),(17,8),(13,11)], fill='#f2d58e')
    png('Waypost', im)

    torches = []
    for frame in range(4):
        im,d = blank(13,23)
        d.line((1,16,11,16), fill='#6d667e', width=3)
        d.polygon([(3,12),(10,12),(8,19),(5,20)], fill='#75617a', outline=INK)
        d.line((4,13,9,13), fill='#e8a76b')
        bend = (0,2,1,-1)[frame]
        d.polygon([(3,11),(2,7),(5,4),(6+bend,1),(8,6),(10,8),(9,12)], fill='#de6247')
        d.polygon([(4,11),(4,8),(6+bend,4),(6,8),(8,7),(8,11)], fill='#ffab58')
        d.polygon([(5,11),(6,8),(7,10),(7,12)], fill='#fff0a4')
        torches.append(im)
    png('Torch1', torches[0])
    sheet('TorchFrames', {'Flame':(torches,.11)})


def trail_details():
    im, d = blank(23, 11)
    d.polygon([(0,10),(3,7),(9,7),(13,4),(15,2),(17,5),(21,6),(22,9),(22,10)],
              fill='#ad7763')
    d.polygon([(5,9),(12,7),(15,3),(17,6),(21,8),(20,10),(6,10)], fill='#f1b665')
    d.line((3,10,21,10), fill='#f7d79a')
    d.point((1,6), fill='#e3a06c')
    d.point((9,4), fill='#f4c681')
    png('WardenWave', im)

    gem = Image.open(ASSETS / 'Gem.png').convert('RGBA').resize((10, 12), Image.Resampling.NEAREST)
    frames = []
    for radius in (0, 0, 1, 3, 2, 1, 0, 0, 0):
        im, d = blank(18, 18)
        im.paste(gem, (4, 3))
        if radius:
            d.line((13-radius, 5, 13+radius, 5), fill='#fff0b7')
            d.line((13, 5-radius, 13, 5+radius), fill='#fff0b7')
            d.point((3, 12), fill='#9cbac8')
        frames.append(im)
    png('GemGlimmer', frames[0])
    sheet('GemFrames', {'Glimmer': (frames, .16)})

    im, d = blank(32, 64)
    for y in range(64):
        alpha = min(255, y * 15)
        shade = max(20, 43 - y)
        d.line((0, y, 31, y), fill=(shade, shade, shade + 12, alpha))
    for i in range(37):
        y = 8 + i * 47 % 47
        x = i * 31 % 31
        if x < 7 or x > 23:
            d.line((x, y, x, y+2), fill=(75, 62, 66, 155))
    png('MeadowRavine', im, 64)

    im, d = blank(64, 13)
    d.polygon([(0,0),(63,0),(60,9),(48,12),(15,12),(3,9)], fill='#253345', outline=INK)
    d.rectangle((0,0,63,2), fill='#efc07d')
    d.rectangle((4,3,59,6), fill='#55727c')
    for x in range(5,59,9):
        d.rectangle((x,3,x+1,6), fill='#ab8c65')
    d.polygon([(23,7),(40,7),(35,11),(28,11)], fill='#d8aa72')
    png('LiftDeck', im)

    im, d = blank(80, 132)
    for x in (10,69):
        d.rectangle((x,0,x+2,131), fill='#4b5265')
        d.line((x,0,x,131), fill='#8d7778')
        for y in range(7,131,12):
            d.line((x-2,y,x+4,y), fill='#807078')
    for y in (4,118,128):
        d.rectangle((3,y,76,y+2), fill='#6f6575')
        d.line((14,y,64,y), fill='#977f7f')
    png('LiftRail', im)

    im, d = blank(48, 85)
    d.rounded_rectangle((2,2,45,84), radius=23, fill='#33465b', outline='#7c8393', width=2)
    d.rounded_rectangle((8,7,39,80), radius=17, fill='#a38682', outline='#283547', width=3)
    d.rounded_rectangle((11,10,36,76), radius=13, fill='#dfb28f')
    d.polygon([(11,62),(19,48),(25,54),(35,45),(37,74),(11,74)], fill='#977e88')
    d.rectangle((21,11,25,77), fill='#526271')
    d.line((11,40,37,40), fill='#4e5d6c', width=3)
    d.line((5,80,42,80), fill='#a8907f', width=2)
    png('TowerWindow', im)

    im, d = blank(23, 60)
    d.line([(12,0),(9,8),(13,16),(10,28),(12,36),(8,44),(9,56)], fill='#3d5c59', width=2)
    for i, y in enumerate(range(5,52,5)):
        x = 9 + (i * 7) % 5
        sign = 1 if i % 2 else -1
        d.polygon([(x,y+4),(x+5*sign,y+1),(x+7*sign,y),(x+5*sign,y+6)], fill='#6a896f')
        d.point((x+5*sign,y+3), fill='#a3ad79')
    png('TowerVine', im)



def landscapes():
    # Two RGBA layers per biome. Endpoints align across each repeating axis.
    for name, sky, colors in [
        ('Cave', ('#222938','#49314d'), ('#30273d','#523852','#6e4a65')),
        ('Tower', ('#243957','#92768b'), ('#272b46','#4d4863','#655f78')),
        ('Summit', ('#2c4264','#e09c78'), ('#534464','#776180','#a67486')),
    ]:
        rng = Random(name)
        im,d = blank(384,216)
        top,bottom = (tuple(int(c[i:i+2],16) for i in [1,3,5]) for c in sky)
        for y in range(216):
            t = (y//6)/35
            d.line((0,y,383,y), fill=tuple(round(a+(b-a)*t) for a,b in zip(top,bottom))+(255,))
        if name != 'Cave':
            d.ellipse((276,34,317,75), fill='#e9c3a1' if name == 'Tower' else '#f8d496')
        for band in range(3):
            base = 133+band*23
            ridge = [(x,base+int(13*sin(tau*x/384+band)+9*sin(tau*x/96+band*2))) for x in range(385)]
            d.polygon(ridge+[(384,216),(0,216)], fill=colors[band])
            for x in range(13,378,13):
                y = ridge[x][1]
                d.line((x,y+3,x+rng.randrange(1,12),y+rng.randrange(8,18)), fill=colors[max(0,band-1)])
        for _ in range(42):
            x,y = rng.randrange(6,378), rng.randrange(19,140)
            d.point((x,y), fill='#876579' if name == 'Cave' else '#d8b4a4')
        png(name+'Vista', im, 48)

    im,d=blank(384,216)
    rng=Random(20)
    for band, trunk, lit in [
        (0,'#68917e','#779c82'),(1,'#466e66','#547c67'),
    ]:
        for center, span in [(20,48),(118,65),(245,105),(353,54)]:
            for x in range(center-span//2, center+span//2, 4):
                y = 173 + band*17 - (span//2-abs(x-center))//3 + rng.randrange(-3,10)
                width = rng.randrange(5,12)
                for turn in (-384,0,384):
                    xx = x + turn
                    d.polygon([(xx-width,216),(xx,y),(xx+width,216)], fill=trunk)
                    d.line([(xx, y+7),(xx-2,y+14),(xx-width//2,y+28)], fill=lit, width=1)
    png('MeadowNear',im,16)

    im,d=blank(384,216)
    rng=Random(21)
    for x in range(-6,387,12):
        for y in (0,215):
            direction=1 if y==0 else -1
            length=rng.randrange(12,58) if direction>0 else rng.randrange(9,34)
            d.polygon([(x-9,y),(x+8,y),(x+2,y+direction*length)],fill='#322b43')
            d.line((x+4,y,x+2,y+direction*(length-3)),fill='#523d55',width=2)
    for x,y in [(44,191),(153,197),(277,180),(336,200)]:
        d.polygon([(x-9,216),(x,y),(x+10,216)],fill='#356b74')
        d.line((x,y+2,x+4,207),fill='#65a9a2',width=2)
    png('CaveNear',im,16)

    im,d=blank(192,216)
    for x in (9,175):
        d.rectangle((x,0,x+9,215),fill='#343952')
        d.line((x+2,0,x+2,215),fill='#665771',width=2)
        for y in range(5,216,24):
            d.line((x,y,x+9,y),fill='#574e68')
    d.arc((15,41,178,163),180,360,fill='#49485f',width=8)
    d.arc((14,37,178,162),184,356,fill='#73617a',width=2)
    for x in (19,170):
        d.rectangle((x,100,x+4,177),fill='#49485f')
    png('TowerNear',im,16)

    im,d=blank(384,216)
    for x in range(24,367,55):
        top=138-(x*19%27)
        d.rectangle((x,top,x+29,216),fill='#4a435d')
        for dx in (0,12,24):
            d.rectangle((x+dx,top-8,x+dx+5,top),fill='#4a435d')
        d.line((x,top,x+29,top),fill='#856a78')
        d.rectangle((x+12,top+8,x+16,top+23),fill='#b88d83')
    png('SummitNear',im,16)


def tile_variants():
    image = Image.open(ASSETS / 'TrailTiles.png').convert('RGBA')
    base = [image.crop((i*16,0,(i+1)*16,16)) for i in range(4)]
    # Use the original materials as palette and texture references on repeated runs.
    tiles = [(tile, True, i >= 4) for i,tile in enumerate([*base,base[0],base[2],base[3]])]
    for biome, indices, lip, side, gleam in [
        (0,(0,1),'#749155','#45303d','#c5bc68'),
        (1,(2,2),'#846d91','#372945','#a984a9'),
        (2,(3,3),'#7993a4','#374656','#d5b27b'),
    ]:
        top, fill = (base[i] for i in indices)
        # Top, left/right cliff, their corners, one-way center/caps, floor trim.
        for variant in range(10):
            one_way = 5 <= variant <= 7
            tile = (top if variant in (0,3,4,5,6,7) else fill).copy()
            d = ImageDraw.Draw(tile)
            if variant in (0,3,4,5,6,7):
                d.line((0,0,15,0),fill=gleam)
                for x in (1,5,6,10,13):
                    d.line((x,1,x,2+x%2),fill=lip)
            if variant in (1,2,3,4,6,7):
                left = variant in (1,3,6)
                x = 0 if left else 15
                d.line((x,1,x,15),fill=side,width=2)
                for y in (3,8,13):
                    d.point((x+(1 if left else -1),y),fill=lip)
            if one_way:
                d.rectangle((0,11,15,15),fill=(0,0,0,0))
                d.line((0,10,15,10),fill=side)
                d.line((3,9,12,9),fill=lip)
            if variant in (8,9):
                x = 3 if variant==8 else 10
                d.line([(x,5),(x+2,7),(x+1,11)], fill=side)
                d.point((x+2,8), fill=lip)
            tiles.append((tile, True, one_way))
    for i in range(6):
        tile,d=blank(16,16)
        if i in (0,1,2):
            d.line([(3,15),(3,10),(1,7)],fill='#648755',width=2)
            d.line([(7,15),(8,9),(10,6)],fill='#729b5f',width=2)
            d.line([(11,15),(11,11),(14,8)],fill='#425e54',width=2)
            if i != 1:
                d.line((7,6,11,6),fill='#f7c976' if i==0 else '#e79da0',width=2)
                d.point((9,6),fill='#fff0b5')
        elif i in (3,4):
            for x,y in [(5,8),(9,5),(12,10)]:
                d.polygon([(x-2,15),(x-2,y+3),(x,y),(x+2,y+3),(x+1,15)],fill='#4e8090')
                d.line((x,y+1,x,y+4),fill='#8fc7be')
        else:
            d.rectangle((3,0,10,10),fill='#465061')
            d.line((4,10,4,15),fill='#c4a574')
            d.line((9,10,9,13),fill='#c4a574')
            d.line((3,2,10,2),fill='#a88872')
        tiles.append((tile,False,False))
    # Meadow surfaces are alternate soil clusters, rooted turf, and taller silhouettes.
    for variant in range(6):
        tile,d = blank(16,16)
        rng = Random(872 + variant)
        soil = ('#744b4b','#694544','#825447')[variant%3]
        d.rectangle((0,0,15,15), fill=soil)
        for y,x in [(3,2),(2,9),(7,6),(11,1),(12,10),(15,5)]:
            x = (x + variant*3) % 16
            width = rng.choice((3,4,6,8))
            d.polygon([(x-1,y+2),(x,y),(x+width-2,y-1),(x+width,y+1),(x+width-1,y+3)], fill='#583d46')
            d.line((x,y,x+width-2,y-1), fill=rng.choice(('#c18c60','#aa735a','#98705b')), width=1)
            d.line((x,y+1,x+width-2,y), fill='#91604c')
        if variant < 3:
            caps = ('#aab958','#bdb867','#91af50')
            d.line((0,0,15,0), fill='#d6cf79')
            d.line((0,1,15,1), fill=caps[variant], width=2)
            d.line((0,3,15,3), fill='#597444')
            for x in range(variant,16,4):
                d.line((x,3,x,5 + x%2), fill='#597444')
                d.point((x,7), fill='#b68357')
        tiles.append((tile,True,False))
    for variant in range(4):
        tile,d = blank(16,16)
        if variant in (0,1):
            d.ellipse((0,6,12,18),fill='#3b5b4a')
            d.ellipse((5,3,15,15),fill='#52794c')
            d.line((2,9,5,7),fill='#759950')
            d.line((8,5,12,5),fill='#91a856')
            if variant == 1:
                for x,y in ((3,11),(14,10),(10,7)):
                    d.point((x,y),fill='#e9aa6e')
        elif variant == 2:
            d.polygon([(1,15),(4,10),(7,9),(9,6),(12,7),(15,15)], fill='#514750')
            d.polygon([(4,11),(7,10),(9,7),(12,8),(11,11)],fill='#a6a091')
            d.line((3,14,13,14),fill='#6b635c')
        else:
            for x,y in ((1,7),(5,4),(8,1),(10,6),(14,3)):
                d.line((8,15,x,y),fill='#57804a', width=2)
                d.point((x,y),fill='#a3b963')
        tiles.append((tile,False,False))
    sheet = Image.new('RGBA',(128,16*((len(tiles)+7)//8)))
    lines=['image: '+asset_id('TrailTiles.png'),'tileSize: {width: 16, height: 16}','margin: 0','spacing: 0',
           f'nextId: {len(tiles)+1}','tiles:']
    for i,(tile,solid,one_way) in enumerate(tiles):
        x,y=(i%8)*16,(i//8)*16
        sheet.paste(tile,(x,y))
        lines += [f'- id: {i+1}',f'  source: {{x: {x}, y: {y}, width: 16, height: 16}}',f'  solid: {str(solid).lower()}']
        if one_way:
            lines += ['  collision:','    oneWay: true','    vertices: [{x: 0, y: 0}, {x: 1, y: 0}]']
    png('TrailTiles',sheet)
    (ASSETS/'Trail.tileset').write_text('\n'.join(lines)+'\n')


def sounds():
    for name, length, low, high, volume, noise in [
        ('Step',.08,116,54,.13,.7), ('Rush',.16,340,125,.16,.55),
        ('Stomp',.16,180,63,.24,.26), ('Warning',.22,380,282,.14,.1),
    ]:
        rng = Random(name)
        sample_rate = 16000
        samples = int(length * sample_rate)
        with wave.open(str(ASSETS / (name+'.wav')), 'wb') as sound:
            sound.setparams((1, 2, sample_rate, samples, 'NONE', 'not compressed'))
            phase = 0
            data = []
            for i in range(samples):
                t = i / samples
                phase += tau * (low + (high-low)*t) / sample_rate
                envelope = min(i/100,1) * (1-t)**2
                value = (sin(phase)*(1-noise) + rng.uniform(-1,1)*noise) * envelope
                data.append(struct.pack('<h', round(value * volume * 32767)))
            sound.writeframes(b''.join(data))
        asset_id(name+'.wav')


if __name__ == '__main__':
    characters()
    ui()
    trail_details()
    landscapes()
    tile_variants()
    sounds()

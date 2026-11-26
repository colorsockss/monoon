"""Courier's pixel parts, shared across the concepts and every pose."""
from PIL import Image, ImageDraw

INK = '#211e32'
CREAM = '#fff1c6'
SKIN = '#ffc593'
SKIN_SHADE = '#e88b68'
RED = ('#9e314e', '#eb4e59', '#ff8a70')
JACKET = ('#285075', '#428aae', '#80d7dc')
LEATHER = ('#653954', '#ad624e', '#e7a366')
GOLD = ('#b2633d', '#f5af54', '#ffe799')
POST_CAP = ('#203650', '#347584', '#83c0c4')

# Knees and toes are relative to the hip. Positive X is toward the trail.
REST = ((-3, 5, -4, 9), (2, 5, 2, 9))
HAND = ((3, -4, 3, 0),)
STRIDES = [
    ((-4, 6, -8, 9), (3, 5, 6, 9)), ((-5, 6, -7, 8), (4, 5, 7, 9)),
    ((-4, 3, -5, 6), (4, 6, 4, 9)), ((0, 3, -2, 5), (0, 6, 0, 9)),
    ((3, 5, 6, 9), (-4, 6, -8, 9)), ((4, 5, 7, 9), (-5, 6, -7, 8)),
    ((4, 6, 4, 9), (-4, 3, -5, 6)), ((0, 6, 0, 9), (0, 3, -2, 5)),
]


def courier(pose, concept='post'):
    dx, bob, feet, arms, wind, tilt, eyes = pose
    image = Image.new('RGBA', (36, 40))
    draw = ImageDraw.Draw(image)
    x, y = 18 + dx, 29 + bob
    accent = RED if concept == 'post' else GOLD if concept == 'pilot' else JACKET
    hat = JACKET if concept == 'pilot' else RED

    def box(left, top, right, bottom, color, border=None):
        draw.rectangle((x+left, y+top, x+right, y+bottom), fill=color, outline=border)

    def poly(points, color, border=None):
        draw.polygon([(x+a, y+b) for a, b in points], fill=color, outline=border)

    def line(points, color, width=1):
        draw.line([(x+a, y+b) for a, b in points], fill=color, width=width)

    # Cloth follows the head while its free end follows the pose.
    poly([(-3,-14),(-9,-13),(-16,-13+wind),(-17,-10+wind),(-13,-8+wind),
          (-8,-10),(-2,-10),(3,-12)], accent[0], INK)
    line([(-16,-11+wind),(-12,-11+wind),(-8,-12),(-2,-12)], accent[2], 2)

    for i, (kx, ky, tx, ty) in enumerate(feet):
        line([(-tilt,0),(kx,ky),(tx,ty-2)], INK, 5)
        line([(-tilt,0),(kx,ky),(tx,ty-2)], JACKET[i], 3)
        poly([(tx-2,ty-3),(tx+1,ty-3),(tx+1,ty-2),(tx+3,ty-2),
              (tx+3,ty),(tx-2,ty)], LEATHER[i], INK)
        line([(tx,ty-2),(tx+1,ty-2)], LEATHER[2])

    poly([(-5-tilt,-1),(-4,-10),(-1,-13),(4,-12),(7,-7),(6,0),(2,3),(-5-tilt,2)], JACKET[1], INK)
    line([(-3-tilt,-1),(-3,-7),(-1,-10)], JACKET[2], 2)
    line([(-4-tilt,1),(3,1),(5,-1)], JACKET[0], 2)

    # The oversized satchel rides behind the elbow, with one bright sealed letter.
    pack = -7-tilt
    poly([(pack-5,-8),(pack+3,-8),(pack+5,-5),(pack+4,3),
          (pack+2,5),(pack-5,4),(pack-7,1),(pack-7,-5)], LEATHER[1], INK)
    poly([(pack-5,-7),(pack+3,-7),(pack+3,-2),(pack,-1),(pack-6,-3)], LEATHER[2])
    if concept != 'pilot':
        poly([(pack-4,-11),(pack+2,-10),(pack+1,-5),(pack-5,-6)], CREAM, INK)
        box(pack-3,-8,pack-2,-7,accent[1])
    line([(3,-10),(0,-7),(pack+4,-3)], INK, 3)
    line([(3,-10),(0,-7),(pack+4,-3)], LEATHER[2])

    poly([(-6,-19),(-4,-22),(4,-23),(8,-19),(8,-17),(10,-16),
          (9,-13),(6,-11),(1,-10),(-4,-12),(-6,-15)], SKIN, INK)
    line([(-5,-15),(-2,-12),(0,-12)], SKIN_SHADE, 2)
    line([(5,-13),(7,-13)], SKIN_SHADE)
    if eyes == 'blink':
        line([(5,-16),(6,-16)], INK)
    else:
        box(5,-17,6,-15,INK)
        box(5,-17,5,-17,'#fff9ec')
    poly([(-5,-14),(-7,-17),(-7,-20),(-4,-22),(-2,-20),(-2,-16),(-4,-15)],
         LEATHER[0], INK)
    if concept == 'pilot':
        poly([(-9,-21),(-7,-25),(0,-27),(6,-25),(8,-21),(8,-17),
              (5,-17),(4,-20),(-4,-18),(-4,-14),(-8,-15)], hat[1], INK)
        box(-2,-24,3,-21,CREAM,INK)
        box(4,-24,8,-21,JACKET[2],INK)
    elif concept == 'post':
        poly([(-9,-20),(-10,-24),(-7,-26),(-3,-27),(4,-27),(8,-25),
              (8,-23),(11,-22),(11,-20),(4,-19),(-5,-19)], POST_CAP[1], INK)
        line([(-8,-23),(-5,-26),(3,-26),(6,-25)], POST_CAP[2])
        line([(-7,-22),(-4,-21),(7,-22)], POST_CAP[0], 2)
        line([(5,-21),(10,-21)], POST_CAP[2])
        box(1,-25,6,-22,CREAM,POST_CAP[0])
        line([(2,-24),(3,-23),(5,-24)], POST_CAP[1])
    else:
        brim = 10
        poly([(-10,-22),(-8,-26),(-5,-28),(1,-28),(5,-27),(7,-23),
              (brim,-22),(brim,-20),(9,-19),(-8,-19),(-11,-20)], hat[1], INK)
        line([(-7,-25),(-5,-27),(0,-28),(4,-26)], hat[2], 2)
        line([(-8,-22),(-4,-22),(4,-23),(6,-23)], hat[0], 2)
        line([(0,-21),(brim-1,-21)], CREAM)
        poly([(-5,-26),(-7,-27),(-4,-28),(-2,-26)], CREAM, INK)
    line([(-4,-12),(1,-11),(3,-12)], INK, 4)
    line([(-4,-12),(1,-11),(3,-12)], accent[1], 2)
    for kx, ky, tx, ty in arms:
        line([(3,-9),(kx,ky),(tx,ty)], INK, 5)
        line([(3,-9),(kx,ky),(tx,ty)], JACKET[2], 3)
        box(tx-1,ty-1,tx+1,ty+1,SKIN,INK)
        box(tx,ty,tx+1,ty,SKIN)

        if eyes == 'celebrate':
            poly([(tx-2,ty-5),(tx+5,ty-6),(tx+6,ty-2),(tx-1,ty-1)], CREAM, INK)
            box(tx+1,ty-4,tx+2,ty-3,RED[1])
    return image


def frames():
    idle = [(0,bob,tuple((a,b,c,d-bob) for a,b,c,d in REST),HAND,wind,0,eyes)
            for bob,wind,eyes in [(0,0,''),(0,-1,''),(-1,-2,''),(0,-1,'blink'),(0,0,''),(0,1,'')]]
    run = [(1,[-1,0,1,0][i%4],feet,((5 if i<4 else 0,-5,8 if i<4 else -1,-7),),
            -2-i%2,2,'') for i,feet in enumerate(STRIDES)]

    def pose(feet=REST, arms=HAND, wind=0, dx=0, bob=0, tilt=0, eyes=''):
        return dx,bob,feet,arms,wind,tilt,eyes

    tuck = ((-4,3,-7,6),(4,3,3,6))
    brace = ((-4,4,-6,7),(4,4,5,7))
    reach = ((7,-7,10,-10),)
    up = ((7,-11,11,-17),)
    return {
        'Idle': (idle,.15), 'Run': (run,.065),
        'Rise': ([pose(tuck,up,2),pose(tuck,up,3,bob=-1)],.085),
        'Fall': ([pose(REST,reach,-3),pose(brace,reach,-2)],.11),
        'Wall': ([pose(brace,reach,-1,dx=1),pose(tuck,reach,0,dx=1)],.13),
        'Dash': ([pose(((-7,3,-10,5),(-4,2,-8,2)),reach,-3,dx=3,bob=1,tilt=4),
                  pose(((-7,4,-10,6),(-4,2,-9,3)),reach,-2,dx=3,bob=2,tilt=4)],.055),
        'Land': ([pose(brace,HAND,1,bob=2,tilt=1),pose(REST,HAND,0)],.06),
        'Hurt': ([pose(tuck,reach,-2,dx=-1,tilt=-2,eyes='blink'),pose(brace,reach,-1,dx=-1,tilt=-2)],.115),
        'Ready': ([pose(brace,HAND,2,bob=2,tilt=1)],.06),
        'Apex': ([pose(tuck,reach,-1,bob=-1),pose(tuck,reach,-2)],.085),
        'Stomp': ([pose(REST,up,-1),pose(tuck,up,2,bob=-1)],.09),
        'Win': ([pose(REST,((6,-10,10,-17),),-1,eyes='celebrate'),
                 pose(REST,up,2,eyes='celebrate'),pose(REST,up,0,eyes='celebrate')],.16),
    }


def concepts(path):
    reference = frames()['Idle'][0][0]
    backdrop = Image.new('RGBA', (306, 118), '#effaff')
    draw = ImageDraw.Draw(backdrop)
    for i,(name,label) in enumerate([('pilot','A  GOGGLE PILOT'),('post','B  RIDGE POST'),('feather','C  FEATHER RUN')]):
        x = 5 + 101*i
        draw.rounded_rectangle((x,5,x+96,112), radius=7, fill='#a9ddf2', outline=INK, width=2)
        draw.rectangle((x+2,82,x+94,94),fill='#78768c')
        art = courier(reference,name).resize((72,80),Image.Resampling.NEAREST)
        backdrop.alpha_composite(art,(x+12,10))
        draw.text((x+6,98),label,fill=INK)
    backdrop.resize((918,354),Image.Resampling.NEAREST).save(path,optimize=True)

"""Small sprite accents and deterministic, capped SDL particle effects."""
import math
import random

from PIL import Image, ImageDraw

# name: count, cap, lifetime, speed, gravity, size, blend, near/far tint
EFFECTS = {
    "Steel": (8, 26, (.16,.29), (38,95), 55, (2,5), "Additive", "#CEF5FF", "#71A7BC"),
    "Brass": (10, 24, (.20,.38), (30,85), 120, (2,5), "Additive", "#FFF1A8", "#CB702F"),
    "Feathers": (6, 16, (.20,.38), (24,66), 28, (3,6), "Alpha", "#FFF0BD", "#A78F6B"),
    "Critical": (1, 3, (.24,.25), (0,0), 0, (29,29), "Additive", "#FFF3A2", "#FFDA52"),
    "Jade": (3, 21, (.22,.39), (7,19), -36, (3,6), "Additive", "#CCFFBF", "#4FE49C"),
    "Bannerglow": (5, 9, (.35,.55), (7,24), -25, (19,26), "Additive", "#FFF0A0", "#E6B053"),
    "Emberfall": (1, 15, (.23,.40), (21,51), 15, (3,6), "Additive", "#FFED94", "#EF8448"),
    "Mud": (7, 24, (.22,.42), (26,61), 125, (3,8), "Multiply", "#58392D", "#967051"),
    "WispSmoke": (7, 24, (.26,.48), (18,48), -19, (5,10), "Alpha", "#B589ED", "#694785"),
    "Stone": (7, 28, (.22,.42), (27,73), 140, (3,8), "Alpha", "#DBCDB7", "#777789"),
    "MudPuff": (5, 14, (.26,.44), (12,31), -18, (7,12), "Alpha", "#926547", "#553C29"),
    "WispPuff": (5, 14, (.28,.50), (12,33), -27, (7,13), "Alpha", "#AB86CD", "#644885"),
    "StonePuff": (5, 14, (.25,.44), (13,32), -16, (7,12), "Alpha", "#A39793", "#685F70"),
    "Soul": (8, 22, (.32,.66), (15,38), -57, (3,6), "Additive", "#DBF7ED", "#77C8DA"),
}


def soft_puffs():
    im = Image.new("RGBA", (48, 12))
    for cell in range(4):
        lobes = [(5.5, 6, 4.6, 96), (3.4+cell*.4, 5, 3.7, 80),
                 (7.3, 4.9+cell*.28, 3.4, 69), (6, 8.1, 3.1, 60)]
        for y in range(12):
            for x in range(12):
                alpha = 1.
                for cx, cy, radius, opacity in lobes:
                    falloff = max(0., 1 - math.hypot(x-cx, y-cy)/radius)
                    alpha *= 1 - opacity/255 * falloff**1.4
                im.putpixel((cell*12+x, y), (255, 255, 255, round((1-alpha)*255)))
    return im


def sheet(name):
    if name in ("Critical", "Bannerglow"):
        im = Image.new("RGBA", (20, 20))
        d = ImageDraw.Draw(im)
        if name == "Critical":
            d.polygon([(10,0),(12,7),(19,3),(14,9),(19,10),(13,12),(16,17),(11,14),
                       (10,19),(8,13),(3,17),(6,11),(0,10),(7,8),(4,3),(9,7)], fill="#fff4b3")
            d.rectangle((8,8,11,11), fill="#ffffff")
        else:
            for box, alpha in [((3,0,16,19),25),((6,0,13,18),38),((8,1,11,17),78)]:
                d.polygon([(10,box[1]),(box[2],box[3]-2),(10,box[3]),(box[0],box[3]-2)], fill=(255,255,255,alpha))
            d.line((10,6,10,16), fill="#ffffff80")
        return im
    im = Image.new("RGBA", (32,8))
    d = ImageDraw.Draw(im)
    for i in range(4):
        x = i*8
        points = lambda pairs: [(x+a,b) for a,b in pairs]
        if name in ("Feathers", "Jade"):
            d.polygon(points([(1,6),(1+i,3),(6,1),(7,3),(5,6),(2,7)]), fill="#ffffffc0")
            d.line(points([(0,7),(6,2)]), fill="#ffffff")
        elif name == "Soul":
            d.polygon(points([(4,0),(5+i%2,3),(6,5),(4,7),(2,5),(3,3)]), fill="#ffffff93")
            d.line(points([(4,2),(4,5)]), fill="#ffffff")
        else:
            d.polygon(points([(1,1+i),(5,0),(7,4+i%2),(5,7),(2,6)]), fill="#ffffffbd")
            d.line(points([(1,2+i),(5,1),(6,3)]), fill="#ffffff")
    return im


def rune(name):
    width, height, tone, light = {
        "PulseRune": (204,104,"#C87932D6","#FFE28BFF"),
        "ShieldRune": (52,28,"#D18C42B0","#FFC979FF"),
        "HealRune": (44,26,"#368C53CA","#A8F6ACFF"),
        "GuardianRune": (96,96,"#E2514EB0","#FF9587FF"),
        "GuardianCrack": (104,104,"#621F32F0","#FFAA72FF"),
    }[name]
    im = Image.new("RGBA", (width,height))
    d = ImageDraw.Draw(im)
    cx, cy = width/2, height/2
    def p(angle, ratio):
        return (round(cx+math.cos(angle)*ratio*(cx-2)),round(cy+math.sin(angle)*ratio*(cy-2)))
    if name == "GuardianCrack":
        rng = random.Random(423)
        for i in range(9):
            angle = i*math.tau/9
            branch = [p(angle,.12),p(angle+.19,.42),p(angle-.04,.58),p(angle+.13,rng.uniform(.72,.99))]
            d.line(branch, fill=tone, width=3)
            d.line(branch[:-1], fill=light, width=1)
            d.line([branch[1],p(angle-.3,.62)], fill=tone, width=2)
        return im
    if name == "GuardianRune":
        d.ellipse((2,2,width-3,height-3),fill="#961C2336")
    for i in range(8):
        a = i*math.tau/8
        d.line([p(a+j*.021,1) for j in range(31)], fill=tone, width=2)
        d.line([p(a+j*.023,.8) for j in range(14)], fill=light, width=1)
        d.polygon([p(a,.99),p(a+.056,.89),p(a,.79),p(a-.056,.89)], fill=light)
        d.line([p(a-.085,.63),p(a,.69),p(a+.085,.63),p(a,.58),p(a-.085,.63)], fill=tone)
        d.line([p(a,.69),p(a,.52)], fill=light)
    if name == "HealRune":
        d.line([(cx-8,cy),(cx+8,cy)], fill=light)
        d.line([(cx,cy-4),(cx,cy+4)], fill=light)
    return im


def saber(frame):
    im = Image.new("RGBA", (52,52))
    d = ImageDraw.Draw(im)
    cy = cx = 26
    def arc(radius, start, stop):
        return [(round(cx+math.cos(math.radians(a))*radius),round(cy+math.sin(math.radians(a))*radius)) for a in range(start,stop+1,4)]
    start, stop = -111 + frame*18, 31 + frame*18
    tint = ["#829AAF75","#CBE6E8E0","#CAEBF9AD","#8BACCB55"][frame]
    d.polygon(arc(25,start,stop) + arc(18+frame,start+12,stop)[::-1],fill=tint)
    d.line(arc(25,start+8,stop),fill=["#CDDBDEAE","#F1FFFF","#D5F6FFCD","#A9D7ED8C"][frame],width=2)
    return im


def flag(frame):
    im = Image.new("RGBA", (44,50))
    d = ImageDraw.Draw(im)
    d.line((12,44,12,9),fill="#44271C",width=3)
    d.line((12,41,12,6),fill="#DEAB5D")
    d.polygon([(12,2),(15,6),(12,9),(9,6)],fill="#FFE9AD")
    tip = (41,11+(-2,2,4,1)[frame])
    bottom = tip[1]+12
    d.polygon([(13,9),(26,8),tip,(tip[0]-6,tip[1]+7),(43,bottom),(27,21),(13,25)],fill="#963D34",outline="#DB9E59")
    d.line((15,11,25,10,tip[0]-3,tip[1]+1),fill="#D66E51")
    d.polygon([(22,11),(27,15),(22,20),(17,16)],fill="#FFE5A0")
    d.point((22,16),fill="#A6593F")
    return im


def build(png, put, asset, atlas_factory, clip):
    for name in ("PulseRune","ShieldRune","HealRune","GuardianRune","GuardianCrack"):
        png("FX/"+name, rune(name))
    atlas = atlas_factory("FX/Combat", 224)
    for name, painter, seconds in (("Saber",saber,.05),("WarFlag",flag,.075)):
        rects = [atlas.add(painter(i)) for i in range(4)]
        clip("FX/"+name+".clip",atlas.path,rects,[seconds]*4,name=="WarFlag")
    atlas.save()
    png("FX/SoftPuffs", soft_puffs())
    for name, (count, cap, life, speed, gravity, size, blend, low, high) in EFFECTS.items():
        texture = "SoftPuffs" if name.endswith("Puff") or name == "WispSmoke" else name
        if texture == name:
            png("FX/"+name, sheet(name))
        wide = "sheet: {columns: 4, rows: 1, frames: 4, fps: 0}\n" if name not in ("Critical","Bannerglow") else ""
        sub = (f"subEmitters: [{{when: Death, particles: {asset('FX/Emberfall.particles')}, count: 2}}]\n"
               if name == "Bannerglow" else "")
        columns = (f"shape: Circle\nradius: 32\naspect: 2.6\n"
                   if name == "Bannerglow" else "")
        burst = f"bursts: [{{time: 0, count: {count}}}]\n" if name != "Emberfall" else ""
        put("FX/"+name+".particles", f"""duration: 0.2
looping: false
rate: 0
{burst}maxParticles: {cap}
spread: 360
{columns}space: World
speed: {{min: {speed[0]}, max: {speed[1]}}}
lifetime: {{min: {life[0]}, max: {life[1]}}}
size: {{min: {size[0]}, max: {size[1]}}}
gravity: {gravity}
color: {{min: "{low}", max: "{high}"}}
texture: {asset('FX/'+texture+'.png')}
{wide}blend: {blend}
sizeOverLifetime: {{keys: [{{time: 0, value: 0.65}}, {{time: 0.2, value: 1}}, {{time: 1, value: 0.42}}]}}
colorOverLifetime: {{colors: [{{time: 0, color: "#FFFFFF"}}, {{time: 1, color: "#BBA999"}}], alphas: [{{time: 0, alpha: 0.94}}, {{time: 0.56, alpha: 0.78}}, {{time: 1, alpha: 0}}]}}
{sub}""")

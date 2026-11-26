"""Projected Haven architecture and the Lantern Company field kit."""
import math
import random

from PIL import Image, ImageDraw

# Size and roof colors vary; projection, overhang and light stay fixed.
HOUSES = {
    "Inn": (23, 19, 23, 14, ("#a3543d", "#d48858", "#673849")),
    "Forge": (20, 18, 21, 12, ("#547a78", "#97ae8e", "#35565b")),
    "Store": (19, 17, 20, 12, ("#9e783c", "#d4a657", "#66503e")),
}


def cottage(name, guides=False):
    u, v, wall, rise, (roof, bright, shade) = HOUSES[name]
    overhang = 2
    width = 4 * (max(u, v) + 2*overhang) + 13
    front = u + v + wall + rise + 16
    im = Image.new("RGBA", (width, front + 5))
    d = ImageDraw.Draw(im)
    ox, oy = width//2 - 2*(u-v), front-u-v

    def p(a, b, z=0):
        return round(ox + 2*(a-b)), round(oy + a+b-z)

    def polygon(points, color, outline=None):
        d.polygon([p(*q) for q in points], fill=color, outline=outline)

    def line(points, color, width=1):
        d.line([p(*q) for q in points], fill=color, width=width)

    ink = "#382921"
    polygon([(-1, -1, 0), (u+3, -1, 0), (u+3, v+2, 0), (-1, v+2, 0)], "#25262448")
    for side, limit, color, plank, highlight in (
        (0, u, "#be9369", "#7b513d", "#d7ad7d"),
        (1, v, "#93684e", "#604036", "#b48660"),
    ):
        face = (lambda t,z: (t,v,z)) if side == 0 else (lambda t,z: (u,t,z))
        def panel(a, b, z0, z1, tone, border=None):
            polygon([face(a,z0), face(b,z0), face(b,z1), face(a,z1)], tone, border)
        panel(0, limit, 0, wall, color, ink)
        panel(0, limit, 0, 4, plank, ink)
        for z in range(5, wall, 4):
            line([face(1,z), face(limit-1,z)], plank)
            line([face(2,z+1), face(limit-2,z+1)], highlight)
        for t in (1, limit-1):
            line([face(t,1), face(t,wall)], ink, 2)
            line([face(t+.6,3), face(t+.6,wall-1)], highlight)
        if side == 0:
            panel(limit-9, limit-3, 2, 15, "#432f27", ink)
            panel(limit-8, limit-4, 3, 13, "#885337")
            line([face(limit-7,4), face(limit-7,12)], "#a56c4d")
            line([face(limit-5,7), face(limit-4,7)], "#ebb85d")
        for t in ([3] if side == 0 else [4, limit-8]):
            panel(t, t+4, 8, 15, "#51372a", ink)
            panel(t+.6, t+3.4, 9, 14, "#ffe19e" if side == 0 else "#d9a965")
            line([face(t+2,9), face(t+2,14)], plank)
            line([face(t-1,8), face(t+5,8)], highlight, 2)
    middle = v / 2
    u0, u1, v0, v1 = -overhang, u+overhang, -overhang, v+overhang

    def z(b):
        return wall + rise * (1 - abs(b-middle)/(middle+overhang))

    # The ridge and both eaves run along +u. The right wall ends in a gable.
    polygon([(u,0,wall),(u,middle,z(middle)),(u,v,wall)], "#ae7851", ink)
    for b in (4, 7, 10, 13, 16):
        top = z(b)
        if b < v and top > wall+3:
            line([(u,b,wall+1),(u,b,top-1)], "#6c4838")
    polygon([(u0,v0,z(v0)),(u1,v0,z(v0)),(u1,middle,z(middle)),(u0,middle,z(middle))], shade, ink)
    polygon([(u0,middle,z(middle)),(u1,middle,z(middle)),(u1,v1,z(v1)),(u0,v1,z(v1))], roof, ink)
    r = random.Random(name)
    for side, (left, right, tile) in enumerate(((v0, middle, shade), (middle, v1, roof))):
        tone = "#aa8168" if side == 0 else bright
        for row in range(1, 5):
            b = left + (right-left)*row/5
            line([(u0+.5,b,z(b)+.25),(u1-.5,b,z(b)+.25)], tone)
            prev = left+(right-left)*(row-1)/5
            for a in range(u0+2+(row%2)*2, u1, 4):
                line([(a,prev,z(prev)),(a,b,z(b))], ink if r.randrange(4)==0 else (shade if side else tile))
                if r.randrange(3) != 0:
                    line([(a+.5,b-.37,z(b-.37)),(min(u1-1,a+2),b-.37,z(b-.37))], tone)
    line([(u0,v1,wall),(u1,v1,wall)], bright, 2)
    line([(u0,middle,z(middle)),(u1,middle,z(middle))], bright, 2)
    # Chimney legs meet the sloped roof at their own elevations.
    a, b = round(u*.36), round(v*.67)
    top = max(z(b), z(b+3)) + 12
    polygon([(a,b+3,z(b+3)),(a+4,b+3,z(b+3)),(a+4,b+3,top),(a,b+3,top)], "#a28d79", ink)
    polygon([(a+4,b,z(b)),(a+4,b+3,z(b+3)),(a+4,b+3,top),(a+4,b,top)], "#69554c", ink)
    polygon([(a,b,top),(a+4,b,top),(a+4,b+3,top),(a,b+3,top)], "#d0af8e", ink)
    line([(a+1,b+1,top),(a+3,b+1,top)], "#49392f")
    if guides:
        line([(0,0,0),(u,0,0),(u,v,0),(0,v,0),(0,0,0)], "#62e9db")
        line([(u0,middle,z(middle)),(u1,middle,z(middle))], "#ffcf5f", 2)
        line([(u0,v1,wall),(u1,v1,wall)], "#ffcf5f")
    return im


def stall(guides=False):
    im = Image.new("RGBA", (73, 66))
    d = ImageDraw.Draw(im)
    def p(a,b,z=0):
        return 26 + 2*(a-b), 35+a+b-z
    def plane(z0, z1, color, a0=0, a1=16):
        d.polygon([p(a0,0,z0),p(a1,0,z0),p(a1,11,z1),p(a0,11,z1)], fill=color)
    plane(0, 0, "#33291f48")
    for a,b in ((0,0),(16,0),(0,11),(16,11)):
        top=33 if b==0 else 24
        d.line([p(a,b),p(a,b,top)],fill="#493124",width=3)
        d.line([p(a,b,2),p(a,b,top)],fill="#b3865c")
    plane(33,24,"#e1bb83")
    for a in (0,6,12):
        plane(33,24,"#9a5548",a,a+3)
    d.line([p(0,0,33),p(16,0,33)],fill="#f6d39a",width=2)
    d.line([p(0,11,24),p(16,11,24)],fill="#f6d39a",width=2)
    for a in range(0,16,2):
        d.line([p(a,11,23),p(a+1,11,21)],fill="#7a4638")
    if guides:
        d.line([p(0,0),p(16,0),p(16,11),p(0,11),p(0,0)], fill="#62e9db")
    return im


def kit(png):
    im = Image.new("RGBA",(36,22))
    d = ImageDraw.Draw(im)
    edge=[(0,4),(4,0),(31,0),(35,4),(35,18),(32,21),(3,21),(0,18)]
    d.polygon(edge,fill="#21130f")
    d.polygon([(1,5),(5,1),(30,1),(34,5),(34,17),(31,19),(4,19),(1,16)],fill="#4b2b1e",outline="#97643e")
    rng=random.Random(104)
    for _ in range(90):
        x,y=rng.randrange(3,33),rng.randrange(4,18)
        d.point((x,y),rng.choice(["#4e2d1f","#523121","#49291d","#45281b"]))
    for x in (5,30):
        for y in (4,16):
            d.rectangle((x-1,y-1,x+1,y+1),fill="#6c4326")
            d.point((x,y),"#efc581")
            d.point((x+1,y),"#b7814e")
    d.line((8,3,27,3),fill="#795235")
    d.line((8,17,27,17),fill="#795235")
    png("UI/Panel",im)

    im=Image.new("RGBA",(32,26))
    d=ImageDraw.Draw(im)
    d.polygon([(0,2),(5,0),(26,0),(31,2),(31,18),(27,24),(21,22),(16,25),(11,22),(4,24),(0,18)],fill="#382019")
    d.polygon([(1,3),(5,2),(26,2),(30,3),(30,17),(27,22),(21,20),(16,23),(11,20),(4,22),(1,17)],fill="#78372e")
    d.line((2,4,29,4),fill="#d5a664",width=2)
    d.line((2,18,5,21,11,20,16,23,21,20,26,21,29,18),fill="#a85b42")
    d.line((3,6,3,15),fill="#a45b47")
    d.line((28,6,28,15),fill="#a45b47")
    png("UI/Cloth",im)

    im=Image.new("RGBA",(32,26))
    d=ImageDraw.Draw(im)
    d.polygon([(1,3),(6,1),(27,2),(31,5),(30,23),(25,24),(2,23)],fill="#392519")
    d.polygon([(4,3),(26,3),(29,6),(28,21),(26,22),(6,22),(4,19)],fill="#e7c68c")
    d.line((7,4,24,4),fill="#fae2a9")
    d.line((7,21,27,21),fill="#af7849")
    for x in (1,28):
        d.rounded_rectangle((x,2,x+3,23),radius=1,fill="#8e5b36",outline="#493121")
        d.line((x+1,4,x+1,21),fill="#e7b67a")
    png("UI/Scroll",im)

    im=Image.new("RGBA",(86,50))
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((0,1,85,49),radius=4,fill="#24150f")
    d.rounded_rectangle((2,1,83,46),radius=4,fill="#75462f",outline="#aa7648")
    d.polygon([(6,4),(42,6),(42,43),(7,42),(4,38),(4,8)],fill="#e2bf85",outline="#956238")
    d.polygon([(43,6),(78,4),(81,8),(81,37),(78,42),(43,43)],fill="#edcf96",outline="#956238")
    d.line((42,6,42,43),fill="#aa7550")
    d.line((43,6,43,43),fill="#f9dea6")
    d.line((7,40,38,41),fill="#bd8754")
    d.line((47,41,77,40),fill="#bd8754")
    d.polygon([(64,43),(70,42),(70,49),(67,47),(64,49)],fill="#99463b")
    for x,y in ((4,4),(81,4),(4,43),(81,43)):
        d.point((x,y),"#f0c47c")
    png("UI/Ledger",im)

    im=Image.new("RGBA",(28,28))
    d=ImageDraw.Draw(im)
    d.ellipse((0,1,27,27),fill="#291c14")
    d.ellipse((1,0,26,25),fill="#85532c",outline="#bb844f")
    d.ellipse((5,4,22,21),fill="#482719",outline="#e3b373")
    d.ellipse((8,7,19,18),fill="#a7593e",outline="#6e3526")
    d.line((10,14,13,9,16,14,13,17,10,14),fill="#ffcf7b")
    png("UI/Pulse",im)
    for i in range(1, 13):
        ring=Image.new("RGBA",(28,28))
        face=ImageDraw.Draw(ring)
        face.arc((2,1,25,24),-90,-90+30*i,fill="#683B23",width=4)
        face.arc((2,1,25,24),-90,-90+30*i,fill="#FFD585",width=3)
        face.arc((3,2,24,23),-90,-90+30*i,fill="#FFF1B6",width=1)
        png(f"UI/Charge{i:02}",ring)
    spark=Image.new("RGBA",(28,28))
    d=ImageDraw.Draw(spark)
    d.polygon([(22,0),(23,4),(27,5),(23,6),(22,10),(21,6),(17,5),(21,4)],fill="#fff1b2")
    png("UI/MedalShine",spark)
    wax=Image.new("RGBA",(21,21))
    d=ImageDraw.Draw(wax)
    d.polygon([(3,2),(12,0),(16,2),(20,7),(18,16),(13,20),(6,19),(1,15),(0,7)],fill="#78352b",outline="#53291d")
    d.ellipse((4,4,16,16),outline="#b6624c")
    d.polygon([(10,6),(14,9),(10,14),(6,9)],outline="#eca981")
    png("UI/Wax",wax)
    field_kit(png)


def field_kit(png):
    for name, leather, rim, ridge in (
        ("Button", "#573122", "#A56C40", "#D09858"),
        ("ButtonFocus", "#633A26", "#FFE2A1", "#C6854C"),
        ("ButtonDown", "#43271D", "#DBA278", "#785437"),
        ("Keycap", "#3B271F", "#BD8E61", "#E2B680"),
        ("StateBadge", "#421F1D", "#B86A51", "#E09870"),
    ):
        tile=Image.new("RGBA",(22,16))
        d=ImageDraw.Draw(tile)
        d.polygon([(0,4),(4,1),(17,1),(21,4),(21,13),(18,15),(3,15),(0,13)],fill="#251713")
        d.polygon([(0,3),(3,0),(18,0),(21,3),(21,11),(18,14),(3,14),(0,11)],fill=leather,outline=rim)
        d.line((4,2,17,2),fill=ridge)
        d.line((3,12,18,12),fill="#332019")
        if name.startswith("Button"):
            d.point((3,5),"#F0BE7D")
            d.point((18,5),"#F0BE7D")
            for x,y in ((7,5),(9,8),(14,7),(12,10),(5,10)):
                d.point((x,y),"#74472E" if name!="ButtonDown" else "#50342A")
        png("UI/"+name,tile)
    badge=Image.new("RGBA",(24,24))
    d=ImageDraw.Draw(badge)
    d.ellipse((0,2,23,23),fill="#221613")
    d.ellipse((1,0,22,21),fill="#95623B",outline="#F1BF77")
    d.ellipse((3,2,20,19),fill="#512D20",outline="#C99357")
    d.arc((2,1,21,20),200,309,fill="#FFDF9D")
    png("UI/LevelBadge",badge)
    for name, fill, border in (("WaveOn","#FAD487","#BE8347"),("WaveOff","#563328","#986747")):
        dot=Image.new("RGBA",(9,9))
        d=ImageDraw.Draw(dot)
        d.polygon([(4,0),(8,4),(4,8),(0,4)],fill=border,outline="#3B2419")
        d.polygon([(4,2),(6,4),(4,6),(2,4)],fill=fill)
        png("UI/"+name,dot)
    foe=Image.new("RGBA",(12,12))
    d=ImageDraw.Draw(foe)
    d.polygon([(0,1),(3,4),(9,4),(11,1),(11,8),(8,10),(3,10),(0,8)],fill="#BC7657",outline="#371D1B")
    d.point((3,6),"#FFE0AD")
    d.point((8,6),"#FFE0AD")
    png("UI/FoeMark",foe)
    down=Image.new("RGBA",(24,24))
    d=ImageDraw.Draw(down)
    d.polygon([(3,1),(20,1),(22,3),(21,14),(18,19),(12,23),(6,19),(3,14),(1,3)],fill="#241713CF",outline="#B15D48")
    d.line((8,7,16,15),fill="#FFD6A4",width=2)
    d.line((16,7,8,15),fill="#FFD6A4",width=2)
    png("UI/Down",down)
    strip=Image.new("RGBA",(8,5))
    d=ImageDraw.Draw(strip)
    d.rectangle((0,0,7,4),fill="#E5A461")
    d.line((0,0,7,0),fill="#FFDF8E")
    d.line((0,4,7,4),fill="#AB603E")
    png("UI/GaugeGold",strip)



def portrait(base):
    im=Image.new("RGBA",(24,24))
    d=ImageDraw.Draw(im)
    bounds=[(3,1),(20,1),(22,3),(21,14),(18,19),(12,23),(6,19),(3,14),(1,3)]
    d.polygon(bounds,fill="#211710")
    inset=[(3,2),(20,2),(21,4),(20,14),(17,18),(12,21),(7,18),(4,14),(2,4)]
    d.polygon(inset,fill="#bd894f")
    mask=Image.new("L",(24,24))
    ImageDraw.Draw(mask).polygon([(4,4),(19,4),(18,14),(16,17),(12,20),(8,17),(6,14)],fill=255)
    face=Image.new("RGBA",(24,24),"#50301f")
    w=base.width
    face.alpha_composite(base.crop(((w-20)//2,0,(w-20)//2+20,20)),(2,2))
    im.paste(face,(0,0),mask)
    d.line((4,2,19,2),fill="#f3ca7d")
    d.point((3,5),fill="#faf0b6")
    d.point((20,5),fill="#faf0b6")
    return im


def rail():
    im = Image.new("RGBA", (40, 32))
    d = ImageDraw.Draw(im)

    def p(a, z):
        return 4 + 2*a, 14 + a - z

    for a in (1, 8, 15):
        d.line([p(a,0),p(a,13)], fill="#573828", width=3)
        d.line([p(a-1,1),p(a-1,12)], fill="#a47b4b")
        d.point(p(a,13), fill="#d3a375")
    for z in (4, 9):
        d.line([p(0,z),p(16,z)], fill="#694533", width=3)
        d.line([p(0,z+1),p(16,z+1)], fill="#ba8b5e")
    return im


def casks():
    im = Image.new("RGBA", (31, 23))
    d = ImageDraw.Draw(im)
    for x,y in ((7,10),(21,14)):
        d.ellipse((x-6,y+5,x+7,y+9), fill="#36261d72")
        d.rounded_rectangle((x-6,y-6,x+6,y+7), radius=4, fill="#785037", outline="#392b21")
        d.ellipse((x-6,y-7,x+6,y-2), fill="#c99460", outline="#4d372a")
        d.line((x-4,y-5,x+2,y-2), fill="#754d37")
        d.line((x-1,y-6,x+5,y-3), fill="#85573d")
        d.line((x-4,y-2,x-4,y+5), fill="#ab7751")
        d.line((x+4,y-2,x+4,y+4), fill="#543b31")
        for z in (0, 5):
            d.line((x-5,y+z,x-2,y+z+1,x+2,y+z+1,x+5,y+z), fill="#574943")
            d.point((x-3,y+z+1), fill="#bd9b76")
    return im

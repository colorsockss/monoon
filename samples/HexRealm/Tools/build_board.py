#!/usr/bin/env python3
"""Regenerate the compact atlases, prefabs and scene for HexRealm."""
import json
import re
from math import cos, sin, tau
from PIL import ImageFont

from art import ROOT, TEAL, RUST, asset, board, canvas, claim_ink, control_trim, frame, icon, land_note, ribbon, sea, sound, stat_icon, town, uid, units

SCENE_ID = '00247366-a56a-5b55-9708-240396719f71'
CONTROLLER = '666adbf5-6775-5a65-9cc4-254323f6985b'
INK = '#2b1c14'
SEPIA = '#3c291d'
MUTED = '#483023'
RULE = '#a98960'
HAVEN = '#245d50'
REDWATCH = '#803f2d'
PAPER = '#ebd5a5'
SLICE = dict(left=12, top=10, right=12, bottom=10)
TRIM = dict(left=6, top=6, right=6, bottom=6)
FONT = ImageFont.truetype(str(ROOT/'Assets/Fonts/GowunDodum-Regular.ttf'),32)
CENTERED = {}


def text_metrics(value, size):
    plain = re.sub(r'<[^>]+>', '', value) or 'HexRealm'
    _, top, _, bottom = FONT.getbbox(plain)
    ink_center = (top + bottom) / 2 + 32 - FONT.getmetrics()[0]
    return round(FONT.getlength(plain) * size / 32, 3), round(ink_center * size / 32, 3)


def fmt(value):
    return json.dumps(value, ensure_ascii=False)


def comp(kind, **fields):
    return '  - type: ' + kind + '\n' + ''.join(f'    {key}: {fmt(value)}\n' for key, value in fields.items())


def obj(name, x, y, w, h, components, parent=None, id=None, group=None):
    return (f'- id: {id or uid(name)}\n  name: {fmt(name)}\n  x: {x}\n  y: {y}\n  width: {w}\n  height: {h}\n' +
            (f'  parentId: {parent}\n' if parent else '') +
            (f'  groups:\n  - {group}\n' if group else '') +
            ('  components:\n' + ''.join(components) if components else '  components: []\n'))


def prefab(name, w, h, *components, group=None):
    return asset(name + '.prefab', 'schemaVersion: 40\nobjects:\n' + obj(name, 0, 0, w, h, components, group=group))


def sprite(image, order=0, **properties):
    return comp('spriteRenderer', assetId=image, order=order, **properties)


def solid(color, order=0):
    return comp('spriteRenderer', order=order, squareColor=color)


def layer(key, tileset, order):
    return obj(key, 24, 18, 48, 56, [
        comp('tilemap2D', layout='HexPointy', cellSize={'width':48,'height':56}, tileset=tileset, order=order, chunks=[])])


def anchored(name, x, y, w, h, *components, parent=None, anchor='TopLeft'):
    return obj(name, x, y, w, h, (comp('uiRect', anchor=anchor),) + components, parent=parent)


def label(name, parent, x, y, w, h, size, value, color=INK, rich=False, center=False):
    width, center_y = text_metrics(value, size)
    center_x = width / 2 if center else 0
    CENTERED[name] = (-center_x, -center_y)
    return anchored(name, x, y, w, h,
                    comp('uiText', text=value, size=size, color=color, richText=rich,
                         insetX=round(w/2-center_x, 3) if center else 0,
                         insetY=round(h/2-center_y, 3)), parent=uid(parent))


def pict(name, parent, x, y, w, h, image=None, color='#FFFFFF', borders=None):
    args = dict(sprite=image, color=color)
    if borders:
        args['borders'] = borders
    return anchored(name, x, y, w, h, comp('uiImage', **args), parent=uid(parent))


def panel(name, parent, x, y, w, h, paper='Frame'):
    return anchored(name, x, y, w, h, comp('uiImage', sprite=uid(paper + '.png'), borders=SLICE), parent=uid(parent))


def keycap(name, parent, x, y, w, h, text):
    return (pict(name,parent,x,y,w,h,uid('Keycap.png'),borders=TRIM) +
            label(name+' text',name,0,0,w,h,14,text,center=True))


def button(name, parent, x, y, w, h, options, action, primary=False):
    caption = name + ' caption'
    face = 'End trim' if primary else 'Button trim'
    values = dict(Action='HexRealm.Button.'+action,Caption=uid(caption),Width=w,Height=h,
                  Titles=[title for title,_ in options], Keys=[key for _,key in options],
                  TextMetrics=[v for title,_ in options for v in text_metrics(title,16)],
                  KeyMetrics=[v for _,key in options for v in (text_metrics(key,14) if key else (0,0))])
    body = ''
    if primary:
        values['Seal'] = uid(name+' seal')
        body += pict(name+' seal',name,0,0,24,24,uid('Crest.png'))
    if any(key for _,key in options):
        values.update(Key=uid(name+' key'),KeyCaption=uid(name+' key text'))
        body += pict(name+' key',name,0,0,24,24,uid('Keycap.png'),borders=TRIM)
        body += anchored(name+' key text',0,0,152,36,comp('uiText',text=options[0][1],size=14,color=INK),parent=uid(name+' key'))
    if action=='Research':
        values['SelectionMark'] = uid(name+' selected')
        body += pict(name+' selected',name,8,h-10,w-16,2,color=HAVEN)
    return (anchored(name, x, y, w, h, comp('uiButton',style='end' if primary else 'button'),
                     comp('uiImage',sprite=uid(face+'.png'),borders=TRIM),
                     comp('script', script='RealmButton', values=values), parent=uid(parent)) +
            anchored(caption, 0, 0, w, h, comp('uiText',text=options[0][0],size=16,color=INK),parent=uid(name)) +
            body)



def text2d(value, size, order=0, color=INK):
    return comp('text2D', text=value, size=size, order=order, color=color, richText=True, align='Center')


def burst(name, smoke):
    size, count = 52, 6
    sheet, _ = canvas(size * count, size)
    for frame_index in range(count):
        im, d = canvas(size, size)
        t = frame_index / (count - 1)
        for ray in range(8):
            angle = tau * ray / 8 + (ray % 3) * .11
            radius = 3 + 17 * t
            x = round(26 + cos(angle) * radius)
            y = round(26 + sin(angle) * radius - (5 * t if smoke else 0))
            r = round(2 + 3 * t) if smoke else max(1, round(3 - 2 * t))
            color = (167, 177, 153, int(195 * (1 - t))) if smoke else (255, 222, 133, int(255 * (1 - t)))
            d.ellipse((x-r, y-r, x+r, y+r), fill=color)
        if not smoke and frame_index < 2:
            d.line((19,26,33,26), fill='#fff3c1', width=2)
            d.line((26,19,26,33), fill='#fff3c1', width=2)
        sheet.paste(im, (frame_index * size, 0))
    image = asset(name + ' burst.png', sheet)
    frames = '\n'.join(
        f'- {{sprite: {image}, seconds: {0.13 if smoke else 0.085}, source: '
        f'{{x: {i*size}, y: 0, width: {size}, height: {size}}}, useRenderer: false}}'
        for i in range(count))
    clip = asset(name + '.clip', f'frames:\n{frames}\nloop: false\n')
    return prefab(name, size, size, sprite(image, 15), comp('spriteAnimator', clip=clip, playOnStart=True))


def archery_props():
    im, d = canvas(23, 6)
    d.line((2,3,20,3), fill='#49372a', width=3)
    d.line((2,2,20,2), fill='#ffe297')
    d.polygon([(15,0),(22,2),(15,5)],fill='#374348')
    d.line((16,1,21,2,16,4),fill='#eef0d2')
    d.polygon([(0,0),(7,1),(6,3),(0,5),(2,2)], fill='#e6e5cc')
    arrow = prefab('Bow arrow', 23,6,sprite(asset('Bow arrow.png', im), 17),
                   comp('trail2D', time=.10, width=1.35, startColor='#f0d28c', endColor='#9a9780',
                        maxPoints=8, minStep=1.2, order=16), group='bow-arrow')
    size = 52
    sheet, _ = canvas(size*4,size)
    for i in range(4):
        im,d = canvas(size,size)
        for n,(dx,dy) in enumerate(((-1,-1),(1,-.55),(-.65,.36),(.70,.48),(.15,-.80))):
            x, y = round(26+dx*(4+i*4)), round(26+dy*(3+i*3))
            fade = 225-i*54
            if n < 2:
                d.line((x-2,y+2,x+2,y-2),fill=(247,224,169,fade),width=2)
                d.point((x+1,y-2),fill=(255,242,203,fade))
            else:
                d.ellipse((x-2,y-1,x+2,y+1),fill=(194,149,102,fade))
        sheet.paste(im,(i*size,0))
    image = asset('Arrow dust.png',sheet)
    frames = '\n'.join(f'- {{sprite: {image}, seconds: 0.0625, useRenderer: false, '
                       f'source: {{x: {i*size}, y: 0, width: {size}, height: {size}}}}}' for i in range(4))
    clip = asset('Arrow dust.clip',f'frames:\n{frames}\nloop: false\n')
    dust = prefab('Arrow dust',size,size,sprite(image,16),comp('spriteAnimator',clip=clip,playOnStart=True))
    im,d = canvas(26,18)
    d.rounded_rectangle((1,1,24,16), radius=2, fill='#332821', outline='#a28459')
    damage = prefab('Arrow damage',26,18,sprite(asset('Arrow damage.png',im),19),text2d('',13,20))
    return arrow,dust,damage


def spreading_claim(side):
    name = f'Claim {side}'
    image = claim_ink(side)
    frames = '\n'.join(
        f'- {{sprite: {image}, seconds: 0.1, source: '
        f'{{x: {i*48}, y: 0, width: 48, height: 56}}, useRenderer: false}}'
        for i in range(8))
    clip = asset(name + '.clip', f'frames:\n{frames}\nloop: false\n')
    return prefab(name, 48, 56, sprite(image, -6), comp('spriteAnimator', clip=clip, playOnStart=True))


def build():
    ground, ink = board()
    background = sea()
    frame('Frame', PAPER, '#ad8b5d')
    frame('Ledger', '#f3e2b9', '#a38458')
    frame('Stamp', '#f6d9ad', '#974c34')
    control_trim('Button trim', '#78512f')
    control_trim('End trim', '#315849')
    control_trim('Keycap', '#78512f', chip=True)
    for key in ('HP','MP','Pop'):
        stat_icon(key)
    for key in ('Coin','Hour','Lore','Land','Crest','Pin'):
        icon(key)
    sprites = [town(side, level) for side in (1,2) for level in (1,2,3)]
    unit_prefabs = []
    portraits = []
    for side, kind in [(1,'Settler'),(1,'Worker'),(1,'Scout'),(1,'Spear'),(1,'Archer'),(2,'Settler'),(2,'Worker'),(2,'Scout'),(2,'Spear'),(2,'Archer')]:
        sheet, animator, portrait = units(kind, side)
        components = [sprite(portrait, 5, sortByY=True), comp('spriteAnimator', controller=animator, playOnStart=True)]
        if kind == 'Archer':
            components.append(comp('script',script='RealmBowRelease'))
        unit_prefabs.append(prefab(f'Pieces/{side}-{kind}',38,42,*components))
        if side == 1:
            portraits.append(portrait)
    unitbar = prefab('Health', 28, 4, solid('#182a32', 7))
    citybar = prefab('City progress', 78, 4, solid('#aa865e', 12))
    fills = [prefab(f'Fill {side}', 1, 2, solid(color,13)) for side,color in [(1,TEAL),(2,RUST)]]
    town_art = prefab('Town',57,49,sprite(sprites[0],3))
    signs = [prefab(f'Nameplate {side}',132,44,sprite(ribbon(side),10)) for side in (1,2)]
    town_line = prefab('Town line',132,16,text2d('',12,11))
    number = prefab('Damage',48,18,text2d('',15,20))
    gain = prefab('Land gain',86,24,sprite(land_note(),18),text2d('',16,19))
    claims = [spreading_claim(side) for side in (1,2)]
    spark_prefab = burst('Impact', False)
    smoke_prefab = burst('Smoke', True)
    arrow, arrow_dust, arrow_damage = archery_props()

    states = ('normal','hover','focused','pressed','disabled')
    palettes = {
        'button': ('#e6cea2','#f5dfb1','#f5dfb1','#c2a070','#cdb994'),
        'end': ('#b6c8a6','#c7d6b1','#c7d6b1','#94ad8d','#cdb994'),
    }
    theme = asset('Chart.table', 'version: 1\ncolumns:\n- {name: fill, type: Text}\n- {name: text, type: Text}\nrows:\n' +
                  ''.join(f'- {{key: {style}.{state}, cells: ["{color}", "{INK}"]}}\n'
                          for style,palette in palettes.items() for state,color in zip(states,palette)))
    s=['schemaVersion: 40\nobjects:\n']
    # The ocean is a pinned screen backdrop. Its borders don't follow either camera.
    s += [obj('Chart water',79,-8,1021,575,[sprite(background,-20),comp('parallax2D',scrollX=0,scrollY=0)]),
          layer('Ground',ground,-12), layer('Coast',ink,-11),layer('Districts',ink,-10),
          layer('Fog',ink,-9),layer('Borders',ink,-8),layer('Orders',ink,-7),layer('Reveal',ink,-6),
          layer('Pointer',ink,-5)]
    s.append(obj('Camera frame', 590-640/1.26, 280-360/1.26, 1280, 720, [],
                 group='chart-frame'))
    s.append(obj('Atlas camera',590,280,1,1,[comp('camera2D',zoom=1.62,frameGroup='chart-frame')]))
    s.append(obj('Route',0,0,1,1,[comp('line2D',points=[{'x':0,'y':0},{'x':1,'y':0}],width=2.2,
                                     startColor='#fff0b3',endColor='#ffc478',order=1)]))
    s.append(obj('Outer Reach', 210, 469, 218, 32, [text2d('T H E   O U T E R   R E A C H',12,-14,'#f5dfae')]))
    s.append(obj('North Passage', 871, 188, 210, 24, [text2d('N O R T H   P A S S A G E',12,-14,'#f5dfae')]))

    sounds = [
        ('Select', [(0, 840, .40)], .065, .05, .42),
        ('Move', [(0, 216, .30), (.040, 158, .22)], .12, .20, .48),
        ('Hit', [(0, 123, .36), (0, 510, .21)], .15, .38, .53),
        ('Bow', [(0, 292, .42), (.009, 507, .12)], .10, .29, .61),
        ('Build', [(0, 392, .36), (.061, 587, .36)], .22, .06, .57),
        ('Turn', [(0, 527, .35), (.072, 352, .29)], .22, .015, .49),
        ('Victory', [(0, 392, .30), (.08, 494, .29), (.16, 587, .25), (.24, 784, .31)], .44, .01, .64),
    ]
    for name, notes, length, noise, volume in sounds:
        source = sound(name, notes, length, noise)
        s.append(obj(name + ' sound',0,0,1,1,
                     [comp('audioSource', sound=source, bus='Effects', volume=volume, playOnStart=False)]))

    for name,order in [('Backdrop',9),('HUD',10),('Modal',20)]:
        s.append(obj(name,0,0,1,1,[comp('uiCanvas',order=order)]))
    shade, _ = canvas(1, 1, (31, 22, 15, 86))
    s += [pict('Chart shade','Backdrop',0,0,1280,720,asset('Chart shade.png',shade)),
          panel('Top rail','HUD',24,16,1232,88,'Ledger'),
          pict('Heraldry','Top rail',16,28,32,32,uid('Crest.png')),
          label('Wordmark','Top rail',56,8,192,40,20,'<b>HexRealm</b>',INK,True),
          label('Chapter','Top rail',56,48,196,28,14,'<b>The Salt Coast</b>',MUTED,True)]
    for i,(key, caption, image) in enumerate([
        ('Turn','Turn','Hour'),('Gold','Gold','Coin'),('Land','Land goal','Land'),('Lore','Research','Lore'),
    ]):
        x = 256 + 180 * i
        s += [pict(key+' divider','Top rail',x,16,1,56,color=RULE),
              pict(key+' icon','Top rail',x+12,32,24,24,uid(image+'.png')),
              label(key+' label','Top rail',x+44,12,132,28,14,caption,SEPIA),
              anchored(key+' number',x+44,40,136,36,parent=uid('Top rail'))]
        if key == 'Lore':
            s.append(pict('Research glint','Lore number',-6,24,16,3,color='#fff4c4'))
        s.append(label(key+' value',key+' number',0,0,136,36,16,'',INK,True))
    s += [pict('Actions divider','Top rail',976,16,1,56,color=RULE),
          pict('Research bar','Top rail',840,76,128,2,color=RULE),
          pict('Research fill','Top rail',840,76,1,2,color=HAVEN),
          button('Research toggle','Top rail',984,20,112,48,[('Fund',''),('Funded',''),('Done','')],'Research'),
          button('Help','Top rail',1104,20,112,48,[('Help','')],'Help'),
          panel('Command','HUD',904,408,352,224),
          pict('Command pin','Command',316,8,24,24,uid('Pin.png')),
          pict('Command crest','Command',12,8,32,32,uid('Crest.png')),
          label('Selection','Command',52,4,258,40,20,'',INK,True),
          pict('Command rule','Command',52,44,284,1,color=RULE),
          anchored('Command chips',52,52,284,36,parent=uid('Command')),
          pict('Health chip','Command chips',0,0,128,36,uid('Keycap.png'),borders=TRIM),
          pict('Health icon','Health chip',8,10,16,16,uid('HP icon.png')),
          label('Command stats','Health chip',32,0,96,36,16,'',INK,True),
          pict('Action chip','Command chips',136,0,148,36,uid('Keycap.png'),borders=TRIM),
          pict('Move icon','Action chip',8,10,16,16,uid('MP icon.png')),
          pict('People icon','Action chip',8,10,16,16,uid('Pop icon.png')),
          label('Action value','Action chip',32,0,112,36,16,'',INK,True),
          anchored('Command notes',52,92,284,64,parent=uid('Command')),
          label('Command advice','Command notes',0,0,284,32,16,'',MUTED,True),
          label('Command hint','Command notes',0,32,284,32,14,'',MUTED,True),
          button('Context','Command',52,160,284,48,[
              ('Found city','B'),('Build farm','I'),('Next unit','Tab'),('Train spear','4'),
              ('Settler in training',''),('Worker in training',''),('Scout in training',''),
              ('Spear in training',''),('Bow in training','')],'Context'),
          button('End','HUD',904,640,352,56,[('End turn','Enter')],'End',primary=True),
          panel('Objective','HUD',24,608,344,88),
          label('Objective label','Objective',16,8,312,36,20,'First orders',INK,True),
          label('Objective text','Objective',16,44,312,32,16,'Found Haven'),
          keycap('Objective key','Objective',272,44,56,32,'B'),
          panel('Tooltip','HUD',384,648,504,48),
          label('Tooltip text','Tooltip',16,8,472,32,16,'',INK,True),
          panel('Land guide','HUD',568,112,368,88,'Ledger'),
          label('Land guide heading','Land guide',16,8,336,32,16,'<b>Grow cities with food</b>',SEPIA,True),
          label('Land guide text','Land guide',16,44,336,36,16,'Found or capture cities'),
          panel('Notice panel','Modal',928,164,328,44,'Ledger'),
          label('Notice','Notice panel',16,4,296,36,16,'',INK,True),
          panel('Turn stamp','Modal',928,112,328,44,'Stamp'),
          label('Stamp text','Turn stamp',16,4,296,36,16,'Rival turn',REDWATCH,True,center=True)]
    for i, art in enumerate(portraits):
        s.append(pict(f'Portrait {i}', 'Command',12,8,32,32,art))
    s += [panel('Title card','Modal',80,156,552,392,'Ledger'),
          pict('Title crest','Title card',16,20,48,48,uid('Crest.png')),
          label('Title','Title card',76,16,456,48,24,'<b>HexRealm</b>',INK,True),
          label('Title subtitle','Title card',76,64,456,36,16,'The Salt Coast',MUTED),
          pict('Title rule','Title card',76,108,456,1,color=RULE),
          label('Title stakes','Title card',76,120,456,40,20,'Two banners. One coast.'),
          label('Title goal','Title card',76,172,456,36,16,'Found Haven. Take Redwatch.'),
          label('Title pace','Title card',76,216,456,36,14,'25 turns, one rival',MUTED),
          button('Start','Title card',76,272,224,48,[('New game','N')],'Start'),
          button('Continue','Title card',308,272,224,48,[('Load game','F9')],'Load'),
          button('Title help','Title card',76,328,160,48,[('Help','H')],'Help'),
          panel('Help card','Modal',320,96,640,528,'Ledger'),
          label('Help heading','Help card',16,12,608,52,24,'<b>Field orders</b>',INK,True)]
    for i, (left, lkey, right, rkey) in enumerate([
        ('Select','L click','Order','R click'),('Found city','B','Districts','I/M/O'),
        ('Train','1-5','Research','R'),('Next unit','Tab','End turn','Enter'),
        ('Save','F5','Load','F9'),('Zoom','+/-','Pan','WASD'),
    ]):
        y = 72 + i*48
        s += [label(f'Help left {i}','Help card',16,y,192,40,16,left),
              keycap(f'Help left key {i}','Help card',212,y+4,104,32,lkey),
              label(f'Help right {i}','Help card',332,y,168,40,16,right),
              keycap(f'Help right key {i}','Help card',512,y+4,112,32,rkey)]
    s += [pict('Help rule','Help card',16,368,608,1,color=RULE),
          label('Help land','Help card',16,380,608,36,16,'Claim land by growth, founding, or capture.'),
          label('Help win','Help card',16,420,608,36,16,'Win by land, capital, or score after turn 25.'),
          button('Back','Help card',16,464,204,48,[('Back','H')],'Help'),
          panel('Result card','Modal',320,156,640,472,'Ledger'),
          anchored('Charter contents',0,0,640,472,parent=uid('Result card')),
          pict('Result crest','Charter contents',560,16,48,48,uid('Crest.png')),
          label('Result heading','Charter contents',32,12,512,52,24,'',INK,True),
          label('Result cause','Charter contents',32,68,576,40,16,'',MUTED),
          pict('Result rule','Charter contents',32,116,576,1,color=RULE),
          pict('Player ribbon','Charter contents',16,128,296,40,uid('Ribbon 1.png'),borders=SLICE),
          pict('Rival ribbon','Charter contents',328,128,296,40,uid('Ribbon 2.png'),borders=SLICE),
          label('Player tally','Charter contents',32,128,264,40,20,'<b>Haven</b>',HAVEN,True),
          label('Rival tally','Charter contents',344,128,264,40,20,'<b>Redwatch</b>',REDWATCH,True)]
    for side,left in [('Player',32),('Rival',344)]:
        for i, field in enumerate(['Land','People','Treasury','Forces','Total']):
            y=176+i*44
            s += [label(side+' '+field+' label','Charter contents',left,y,164,36,16,field,SEPIA,i==4),
                  label(side+' '+field,'Charter contents',left+180,y,64,36,16,'',INK,True)]
    s += [button('Return','Charter contents',32,404,264,48,[('Title screen','N')],'Title'),
          button('Keep','Charter contents',344,404,264,48,[('Save result','F5')],'Save')]
    fields={name:uid(key) for name,key in [
        ('Ground','Ground'),('Coast','Coast'),('Districts','Districts'),('Fog','Fog'),('Borders','Borders'),
        ('RevealFog','Reveal'),('ResearchGlint','Research glint'),
        ('Orders','Orders'),('Pointer','Pointer'),('Route','Route'),('Camera','Atlas camera'),('CameraFrame','Camera frame'),
        ('Hud','HUD'),('ChartShade','Chart shade'),('Title','Title card'),('Help','Help card'),('Result','Result card'),('Objective','Objective'),
        ('ObjectiveLabel','Objective label'),('ObjectiveText','Objective text'),('ObjectiveKey','Objective key'),
        ('TurnLabel','Turn value'),('GoldLabel','Gold value'),('LandLabel','Land value'),('LoreLabel','Lore value'),
        ('LoreFill','Research fill'),('ResearchButton','Research toggle'),('Selection','Selection'),('Stats','Command stats'),('Advice','Command advice'),
        ('Hint','Command hint'),('CommandNotes','Command notes'),('CommandChips','Command chips'),('ActionValue','Action value'),('MoveIcon','Move icon'),('PeopleIcon','People icon'),('Chapter','Chapter'),('Crest','Command crest'),
        ('EndButton','End'),('CommandPanel','Command'),('Top','Top rail'),('Context','Context'),
        ('ResultHeading','Result heading'),
        ('ResultCause','Result cause'),('Tooltip','Tooltip'),('TooltipText','Tooltip text'),
        ('Notice','Notice'),('NoticePanel','Notice panel'),('TurnStamp','Turn stamp'),('StampText','Stamp text'),
        ('LandGuide','Land guide')
    ]}
    fields.update((name + 'Sound', uid(name + ' sound')) for name, *_ in sounds)
    fields['ResourceRows'] = [uid(f'{key} number') for key in ('Turn','Gold','Land','Lore')]
    fields['AlignedLabels'] = [uid(name) for name in CENTERED]
    fields['TextOffsets'] = [v for offset in CENTERED.values() for v in offset]
    fields['Portraits'] = [uid(f'Portrait {n}') for n in range(len(portraits))]
    fields['Tallies'] = [uid(f'{side} {field}') for side in ('Player','Rival') for field in ('Land','People','Treasury','Forces','Total')]
    fields |= dict(UnitArt=unit_prefabs, TownSprites=sprites, TownArt={'Id':town_art}, Signs=signs, TownLine={'Id':town_line},
                   HealthBar={'Id':unitbar}, CityBar={'Id':citybar}, Fill=fills, Number={'Id':number},
                   Sparks={'Id':spark_prefab}, Smoke={'Id':smoke_prefab}, ClaimInk=claims, LandGain={'Id':gain},
                   ArrowArt={'Id':arrow}, ArrowDust={'Id':arrow_dust}, ArrowDamage={'Id':arrow_damage})
    s += [obj('Board controller',0,0,1,1,[comp('script',script='RealmBoard',values=fields)],id=CONTROLLER),
          'defaultCameraId: ' + uid('Atlas camera') + '\n']
    (ROOT/'Scenes/Realm.scene').write_text(''.join(s),encoding='utf-8')
    settings = ROOT/'GameSettings'
    text = settings.read_text(encoding='utf-8')
    if 'uiTheme:' not in text:
        settings.write_text(text + f'uiTheme: {theme}\n',encoding='utf-8')


if __name__ == '__main__':
    build()

"""Draw project-test UI bounds on a matching replay PNG, then enlarge the region 2x."""
from argparse import ArgumentParser
from pathlib import Path
from PIL import Image, ImageDraw

LOG_PREFIX = 'LEDGERUN_UI|'
PANEL_COLOR = (255, 67, 142, 255)
LABEL_COLOR = (30, 216, 244, 255)


def overlay(log, scene, scale, source, output, region):
    screenshot = Image.open(source).convert('RGBA')
    sx, sy = screenshot.width / 384, screenshot.height / 216
    draw = ImageDraw.Draw(screenshot)
    count = 0
    for row in Path(log).read_text(encoding='utf-8').splitlines():
        if not row.startswith(LOG_PREFIX):
            continue
        _, name, text_scale, widget, x, y, width, height = row.split('|')
        if name != scene or int(text_scale) != scale:
            continue
        x, y, width, height = map(float, (x, y, width, height))
        left, top = round(x * sx), round(y * sy)
        right = max(left, round((x + width) * sx) - 1)
        bottom = max(top, round((y + height) * sy) - 1)
        is_keycap = not widget.endswith(' glyph') and (widget.startswith('Key ') or widget in ('Result key', 'Result pad key'))
        is_panel = is_keycap or widget.endswith(('panel', 'actions', 'chip', 'button', 'badge')) or 'Slot card' in widget
        draw.rectangle((left, top, right, bottom), outline=PANEL_COLOR if is_panel else LABEL_COLOR, width=1)
        count += 1
    if count == 0:
        raise ValueError(f'No runtime bounds for {scene} at {scale}x')
    if region:
        x0, y0, x1, y1 = region
        screenshot = screenshot.crop((round(x0 * sx), round(y0 * sy), round(x1 * sx), round(y1 * sy)))
    screenshot.resize((screenshot.width * 2, screenshot.height * 2), Image.Resampling.NEAREST).save(output, optimize=True)


if __name__ == '__main__':
    cli = ArgumentParser(description=__doc__)
    cli.add_argument('log')
    cli.add_argument('scene', choices=('Trailhead', 'Summit'))
    cli.add_argument('text_scale', type=int, choices=(1, 2))
    cli.add_argument('source')
    cli.add_argument('output')
    cli.add_argument('--crop', type=int, nargs=4, metavar=('LEFT', 'TOP', 'RIGHT', 'BOTTOM'))
    args = cli.parse_args()
    overlay(args.log, args.scene, args.text_scale, args.source, args.output, args.crop)

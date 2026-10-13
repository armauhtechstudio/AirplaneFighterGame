"""Adds a feature banner to each store screenshot.

Takes the newest raw capture for each angle from StoreScreenshots/raw (written by
Tools > AirStrike > Store Screenshots in Unity) and writes 1920x1080 PNGs to StoreScreenshots/final.

    python StoreScreenshots/make_banners.py              # newest capture per angle
    python StoreScreenshots/make_banners.py --test       # preview the banners on the menu background

Edit SHOTS to change the text. In the headline, text after "|" is drawn in the accent color.
"""
import argparse
import glob
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
RAW = os.path.join(HERE, "raw")
OUT = os.path.join(HERE, "final")

W, H = 1920, 1080
S = 2  # banner is drawn at 2x and downscaled, for smooth edges

HEADLINE_FONT = os.path.join(PROJECT, "Assets", "SS", "Bulletproof.otf")
SUB_FONT = r"C:\Windows\Fonts\bahnschrift.ttf"

WHITE = (255, 255, 255)
ACCENT = (255, 197, 51)        # gold, second part of the headline
RED = (232, 30, 36)            # neon border / sub-line strip
PLATE_TOP = (44, 47, 54)
PLATE_BOTTOM = (14, 15, 18)

# (angle in the raw file name, headline, sub-line), in store order
SHOTS = [
    ("1_chase",       "EPIC AIR |COMBAT",         "DOGFIGHT ENEMY SQUADRONS"),
    ("5_target_lock", "SHOOT DOWN |ENEMY PLANES", "MACHINE GUNS AND ROCKETS"),
    ("3_side",        "MODERN |FIGHTER JETS",     "FAST, POWERFUL AND DEADLY"),
    ("4_top_down",    "DESTROY |GROUND TARGETS",  "TANKS, BUILDINGS AND BALLOONS"),
    ("2_front_low",   "LEGENDARY |WARPLANES",     "FLY CLASSIC WW2 FIGHTERS"),
    ("6_wide",        "OPEN WORLD |MODE",         "CLIMB THE ONLINE LEADERBOARD"),
]


def font(path, size, variation=None):
    f = ImageFont.truetype(path, size)
    if variation:
        try:
            f.set_variation_by_name(variation)
        except (OSError, ValueError):
            pass
    return f


def cover(img, w, h):
    """Scale and center-crop to exactly w x h."""
    scale = max(w / img.width, h / img.height)
    img = img.resize((round(img.width * scale), round(img.height * scale)), Image.LANCZOS)
    left, top = (img.width - w) // 2, (img.height - h) // 2
    return img.crop((left, top, left + w, top + h))


def chamfer(x0, y0, x1, y1, c):
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c),
            (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def vertical_gradient(size, top, bottom):
    w, h = size
    grad = Image.new("RGB", (1, h))
    for y in range(h):
        t = y / max(h - 1, 1)
        grad.putpixel((0, y), tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)))
    return grad.resize((w, h))


def banner(base, headline, sub):
    cw, ch = W * S, H * S
    canvas = cover(base.convert("RGB"), cw, ch).convert("RGBA")

    # Darken the top so the banner reads on bright sky
    shade = Image.new("L", (1, ch))
    for y in range(ch):
        shade.putpixel((0, y), max(0, round(150 * (1 - y / (ch * 0.38)))))
    canvas = Image.alpha_composite(canvas, Image.merge("RGBA", (
        Image.new("L", (cw, ch), 0),) * 3 + (shade.resize((cw, ch)),)))

    head_font = font(HEADLINE_FONT, 92 * S)
    sub_font = font(SUB_FONT, 34 * S, "Bold Condensed")

    first, _, second = headline.partition("|")
    w1 = head_font.getlength(first)
    w2 = head_font.getlength(second)
    text_w = w1 + w2
    asc, desc = head_font.getmetrics()

    pad_x, plate_h, c = 70 * S, 150 * S, 34 * S
    plate_w = max(text_w + pad_x * 2, 900 * S)
    x0 = (cw - plate_w) / 2
    y0 = 46 * S
    x1, y1 = x0 + plate_w, y0 + plate_h
    shape = chamfer(x0, y0, x1, y1, c)

    # Red glow around the plate
    glow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    ImageDraw.Draw(glow).polygon(shape, outline=RED + (255,), width=14 * S)
    canvas = Image.alpha_composite(canvas, glow.filter(ImageFilter.GaussianBlur(16 * S)))

    # Drop shadow
    shadow = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).polygon([(x + 0, y + 14 * S) for x, y in shape], fill=(0, 0, 0, 150))
    canvas = Image.alpha_composite(canvas, shadow.filter(ImageFilter.GaussianBlur(12 * S)))

    # Steel plate with a gradient, gloss line and neon border
    mask = Image.new("L", (cw, ch), 0)
    ImageDraw.Draw(mask).polygon(shape, fill=240)
    plate = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    plate.paste(vertical_gradient((int(plate_w) + 2, plate_h + 2), PLATE_TOP, PLATE_BOTTOM), (int(x0), int(y0)))
    plate.putalpha(mask)
    canvas = Image.alpha_composite(canvas, plate)

    d = ImageDraw.Draw(canvas)
    d.line([(x0 + c + 10 * S, y0 + 12 * S), (x1 - c - 10 * S, y0 + 12 * S)], fill=(255, 255, 255, 45), width=2 * S)
    d.polygon(shape, outline=RED + (255,), width=5 * S)
    inner = chamfer(x0 + 12 * S, y0 + 12 * S, x1 - 12 * S, y1 - 12 * S, c - 8 * S)
    d.polygon(inner, outline=(255, 255, 255, 28), width=2 * S)

    # Headline: white + accent, with a hard shadow
    tx = (cw - text_w) / 2
    ty = y0 + (plate_h - (asc + desc)) / 2 + 4 * S
    for dx, dy, col in ((0, 6 * S, (0, 0, 0, 200)), (0, 0, None)):
        d.text((tx + dx, ty + dy), first, font=head_font, fill=col or WHITE + (255,))
        d.text((tx + w1 + dx, ty + dy), second, font=head_font, fill=col or ACCENT + (255,))

    # Sub-line on a red strip hanging off the bottom of the plate
    if sub:
        sw = sub_font.getlength(sub)
        strip_w, strip_h, sc = sw + 70 * S, 58 * S, 16 * S
        sx0 = (cw - strip_w) / 2
        sy0 = y1 - 14 * S
        strip = chamfer(sx0, sy0, sx0 + strip_w, sy0 + strip_h, sc)
        d.polygon([(x, y + 5 * S) for x, y in strip], fill=(0, 0, 0, 120))
        d.polygon(strip, fill=RED + (255,))
        d.line([(sx0 + sc, sy0 + 3 * S), (sx0 + strip_w - sc, sy0 + 3 * S)], fill=(255, 140, 140, 160), width=2 * S)
        sa, sd = sub_font.getmetrics()
        d.text(((cw - sw) / 2, sy0 + (strip_h - (sa + sd)) / 2 + 1 * S), sub, font=sub_font, fill=WHITE + (255,))

    return canvas.convert("RGB").resize((W, H), Image.LANCZOS)


def newest(angle):
    # manual captures: <scene>_<angle>_<time>.png, automatic: auto_<angle>.png
    files = glob.glob(os.path.join(RAW, f"*_{angle}_*.png")) + glob.glob(os.path.join(RAW, f"*_{angle}.png"))
    return max(files, key=os.path.getmtime) if files else None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--test", action="store_true", help="use the menu background instead of raw captures")
    args = parser.parse_args()

    os.makedirs(OUT, exist_ok=True)
    missing = []
    for i, (angle, headline, sub) in enumerate(SHOTS, 1):
        src = os.path.join(PROJECT, "Assets", "SS", "bg.png") if args.test else newest(angle)
        if not src:
            missing.append(angle)
            continue
        out = os.path.join(OUT, f"{i:02d}_{angle.split('_', 1)[1]}.png")
        banner(Image.open(src), headline, sub).save(out, optimize=True)
        print(f"{os.path.basename(src)} -> {os.path.relpath(out, PROJECT)}")

    if missing:
        print("No raw capture for: " + ", ".join(missing), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()

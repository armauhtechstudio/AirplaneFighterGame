"""Builds the Play Store icon (512x512) and feature graphic (1024x500) from the in-game renders in
StoreScreenshots/art (written by StoreShotDirector: "capture art").

    python StoreScreenshots/make_art.py
"""
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.dirname(HERE)
ART = os.path.join(HERE, "art")
OUT = os.path.join(HERE, "final")

HEADLINE_FONT = os.path.join(PROJECT, "Assets", "SS", "Bulletproof.otf")
SUB_FONT = r"C:\Windows\Fonts\bahnschrift.ttf"

WHITE = (255, 255, 255)
GOLD = (255, 197, 51)
RED = (232, 30, 36)

TITLE = ("ACE AIRCRAFT", "HEROES")
SUBTITLE = "WAR AIRPLANE"


def load(name):
    return Image.open(os.path.join(ART, name + ".png"))


def cutout(name):
    """Plane render cropped to its visible pixels."""
    im = load(name).convert("RGBA")
    return im.crop(im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox())


def fire_layer(size, feather=0.0):
    """The explosion render (fire on black) without its grey debris chunks; feather > 0 fades it to black
    toward its edges so no square border shows when it is screen-blended."""
    fire = load("explosion").convert("RGB").resize(size, Image.LANCZOS)
    h, s, v = fire.convert("HSV").split()
    # debris = grey (low saturation) and not bright; white-hot sparks are low saturation too but bright
    grey = s.point(lambda x: 255 if x < 110 else 0)
    dim = v.point(lambda x: 255 if x < 215 else 0)
    debris = ImageChops.multiply(grey, dim).filter(ImageFilter.MaxFilter(5))
    fire = Image.composite(Image.new("RGB", size, 0), fire, debris.filter(ImageFilter.GaussianBlur(2)))
    if feather:
        # radial() measures to the corner; the middle of each edge is at 0.7, so be black by then
        mask = radial(size, [(0, (255, 255, 255)), (0.68 * (1 - feather), (255, 255, 255)), (0.68, (0, 0, 0)), (1, (0, 0, 0))]).convert("L")
        fire = Image.composite(fire, Image.new("RGB", size, 0), mask)
    return fire


def fit(im, w=None, h=None):
    scale = min((w or 1e9) / im.width, (h or 1e9) / im.height)
    return im.resize((round(im.width * scale), round(im.height * scale)), Image.LANCZOS)


def screen(base, layer, pos):
    """Screen-blends an RGB layer (fire on black) onto base at pos."""
    region = base.crop((pos[0], pos[1], pos[0] + layer.width, pos[1] + layer.height)).convert("RGB")
    blended = ImageChops.screen(region, layer.convert("RGB"))
    base.paste(blended, pos)


def glow(im, color, radius, strength=1.0):
    """A soft halo in `color` following the alpha of im (same size)."""
    a = im.getchannel("A").filter(ImageFilter.GaussianBlur(radius))
    a = a.point(lambda v: min(255, int(v * strength)))
    halo = Image.new("RGBA", im.size, color + (0,))
    halo.putalpha(a)
    return halo


def place(base, im, center, halo=None):
    x, y = round(center[0] - im.width / 2), round(center[1] - im.height / 2)
    if halo:
        pad = halo[1] * 3
        padded = Image.new("RGBA", (im.width + pad * 2, im.height + pad * 2), (0, 0, 0, 0))
        padded.alpha_composite(im, (pad, pad))
        base.alpha_composite(glow(padded, halo[0], halo[1], halo[2]), (x - pad, y - pad))
    base.alpha_composite(im, (x, y))


def radial(size, stops):
    """Radial gradient from the centre: stops = [(t, (r, g, b)), ...], t in 0..1 of the half-diagonal."""
    w, h = size
    small = Image.new("RGB", (w // 4, h // 4))
    px = small.load()
    cx, cy = small.width / 2, small.height / 2
    rmax = math.hypot(cx, cy)
    for y in range(small.height):
        for x in range(small.width):
            t = math.hypot(x - cx, y - cy) / rmax
            for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
                if t <= t1:
                    k = (t - t0) / max(t1 - t0, 1e-6)
                    px[x, y] = tuple(round(a + (b - a) * k) for a, b in zip(c0, c1))
                    break
            else:
                px[x, y] = stops[-1][1]
    return small.resize(size, Image.BICUBIC)


def rays(size, count, color, alpha):
    """Speed-burst rays from the centre."""
    w, h = size
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    cx, cy, R = w / 2, h / 2, max(w, h)
    for i in range(count):
        a0 = (i / count) * 2 * math.pi
        a1 = a0 + math.pi / count * 0.55
        d.polygon([(cx, cy), (cx + R * math.cos(a0), cy + R * math.sin(a0)), (cx + R * math.cos(a1), cy + R * math.sin(a1))],
                  fill=color + (alpha,))
    return layer.filter(ImageFilter.GaussianBlur(w / 200))


def make_icon():
    S = 1024  # drawn at 2x
    icon = radial((S, S), [(0, (255, 170, 40)), (0.35, (226, 60, 18)), (0.75, (110, 10, 12)), (1, (30, 4, 6))]).convert("RGBA")
    icon.alpha_composite(rays((S, S), 18, (255, 230, 160), 38))

    screen(icon, ImageEnhance.Brightness(fire_layer((S, S))).enhance(0.9), (0, 0))

    # Dark vignette so the jet pops and the corners stay calm
    vign = radial((S, S), [(0, (0, 0, 0)), (0.55, (0, 0, 0)), (1, (255, 255, 255))]).convert("L")
    icon.alpha_composite(Image.merge("RGBA", [Image.new("L", (S, S), 0)] * 3 + [vign.point(lambda v: int(v * 0.75))]))

    jet = fit(cutout("jet_front"), w=S * 0.90, h=S * 0.72)
    jet = ImageEnhance.Contrast(jet).enhance(1.12)
    place(icon, jet, (S * 0.5, S * 0.53), halo=((255, 235, 170), 14, 1.6))

    icon = icon.convert("RGB").resize((512, 512), Image.LANCZOS)
    path = os.path.join(OUT, "icon_512.png")
    icon.save(path, optimize=True)
    return path


def plate(draw, box, chamfer, fill, outline=None, width=0):
    x0, y0, x1, y1 = box
    c = chamfer
    pts = [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]
    draw.polygon(pts, fill=fill)
    if outline:
        draw.polygon(pts, outline=outline, width=width)
    return pts


def make_feature():
    W, H = 2048, 1000  # drawn at 2x
    g = load("sky_bg").convert("RGB").resize((W, H), Image.LANCZOS)
    g = ImageEnhance.Color(g).enhance(1.15).convert("RGBA")

    # Fire burst behind the planes, on the right
    fire = ImageEnhance.Brightness(fire_layer((1300, 1300), feather=0.55)).enhance(1.0)
    fire = fire.crop((0, 150, 1300, 1150))
    screen(g, fire, (W - 1300 + 180, 0))

    # Darken the left for the title
    shade = Image.new("L", (W, 1))
    for x in range(W):
        shade.putpixel((x, 0), max(0, int(150 * (1 - x / (W * 0.62)) ** 1.4)) if x < W * 0.62 else 0)
    g.alpha_composite(Image.merge("RGBA", [Image.new("L", (W, H), 0)] * 3 + [shade.resize((W, H))]))

    ww2 = fit(cutout("ww2_front"), w=470, h=280)
    place(g, ww2, (W * 0.86, H * 0.19), halo=((255, 220, 150), 8, 1.2))
    jet = fit(cutout("jet_front"), w=1000, h=660)
    jet = ImageEnhance.Contrast(jet).enhance(1.1)
    place(g, jet, (W * 0.775, H * 0.62), halo=((255, 235, 170), 12, 1.4))

    # Title plate, same style as the screenshot banners
    d = ImageDraw.Draw(g)
    f1 = ImageFont.truetype(HEADLINE_FONT, 132)
    f2 = ImageFont.truetype(HEADLINE_FONT, 196)
    fs = ImageFont.truetype(SUB_FONT, 54)
    try:
        fs.set_variation_by_name("Bold Condensed")
    except (OSError, ValueError):
        pass

    left, top = 90, 250
    w1, w2 = f1.getlength(TITLE[0]), f2.getlength(TITLE[1])
    tw = max(w1, w2)
    box = (left, top, left + tw + 120, top + 410)
    shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    plate(ImageDraw.Draw(shadow), (box[0], box[1] + 18, box[2], box[3] + 18), 46, (0, 0, 0, 150))
    g.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(18)))
    halo = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    plate(ImageDraw.Draw(halo), box, 46, None, RED + (255,), 22)
    g.alpha_composite(halo.filter(ImageFilter.GaussianBlur(20)))

    d = ImageDraw.Draw(g)
    plate(d, box, 46, (20, 22, 26, 225), RED + (255,), 7)
    plate(d, (box[0] + 16, box[1] + 16, box[2] - 16, box[3] - 16), 36, None, (255, 255, 255, 30), 3)

    cx = (box[0] + box[2]) / 2
    for dy, col in ((8, (0, 0, 0, 200)), (0, None)):
        d.text((cx - w1 / 2, top + 40 + dy), TITLE[0], font=f1, fill=col or WHITE + (255,))
        d.text((cx - w2 / 2, top + 165 + dy), TITLE[1], font=f2, fill=col or GOLD + (255,))

    sw = fs.getlength(SUBTITLE)
    strip = (cx - sw / 2 - 50, box[3] - 22, cx + sw / 2 + 50, box[3] + 64)
    plate(d, (strip[0], strip[1] + 6, strip[2], strip[3] + 6), 20, (0, 0, 0, 120))
    plate(d, strip, 20, RED + (255,))
    a, dd = fs.getmetrics()
    d.text((cx - sw / 2, strip[1] + (strip[3] - strip[1] - a - dd) / 2), SUBTITLE, font=fs, fill=WHITE + (255,))

    out = g.convert("RGB").resize((1024, 500), Image.LANCZOS)
    path = os.path.join(OUT, "feature_1024x500.png")
    out.save(path, optimize=True)
    return path


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for p in (make_icon(), make_feature()):
        print(os.path.relpath(p, PROJECT), Image.open(p).size)

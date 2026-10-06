"""
Make MaliGo's Android app icon from the Mali art (DESIGN_SPEC.md §6.6).

Outputs (under Assets/MaliGo/Branding/, not in Resources; each gets a Unity .meta written once with a
fresh GUID: the §6 PNG template, textureType 0 (Default), max size 1024, mipmaps off):

* AppIcon_Background.png, 1024 x 1024, solid #FBF6EC (adaptive-icon background layer).
* AppIcon_Foreground.png, 1024 x 1024, transparent: `mali2.png` cropped to (88, 58)-(438, 478) (head,
  scarf and coin), scaled uniformly (Lanczos) to fit 640 x 640 and centred at (512, 530), so the art
  stays inside the adaptive-icon safe circle (diameter 676 = 66%, centred on the canvas). Measured: at
  640 the crop's flat bottom corners and the ears reach past the circle, so the script shrinks the fit
  box in 2 px steps (same crop, same centre) until no visible pixel is outside it, and prints both.
* AppIcon_Legacy.png, 1024 x 1024 opaque: the foreground composited on the background (Legacy and Round).

`mali2.png` is the React Native app's assets/images/mali2.png (534 x 615 RGBA); its MD5 is verified first
(09c1e7891c32a254d60a32d528337a47). The art is the founder's brand art, approved for the app icon (A5).

Usage:
    python tools/make_app_icon.py --mali2 <path to mali2.png>
Prints each output's size, the foreground's alpha bbox and how far the art reaches from the canvas centre
compared with the safe-circle radius (338). Exit code 0 = done and inside the safe circle, 1 = a check failed.
"""
import argparse
import hashlib
import math
import os
import sys
import uuid

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prepare_mali_art import png_meta  # noqa: E402  (the shared §6 PNG .meta template)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO_ROOT, "Assets", "MaliGo", "Branding")

MALI2_MD5 = "09c1e7891c32a254d60a32d528337a47"
MALI2_SIZE = (534, 615)
CANVAS = 1024
BACKGROUND = (0xFB, 0xF6, 0xEC, 0xFF)
ICON_CROP = (88, 58, 438, 478)        # head, scarf and coin
FIT_BOX = 640
ART_CENTRE = (512, 530)
SAFE_RADIUS = 676 / 2.0               # adaptive-icon safe circle, 66% of 1024
ALPHA_VISIBLE = 16                    # pixels at or above this alpha count as art for the checks


def md5(path):
    with open(path, "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def save(image, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    image.save(path, optimize=True)
    meta = path + ".meta"
    wrote_meta = False
    if not os.path.exists(meta):
        with open(meta, "w", encoding="utf-8", newline="\n") as handle:
            handle.write(png_meta(uuid.uuid4().hex, 1024, mipmaps=False, texture_type=0))
        wrote_meta = True
    print("  -> %s %dx%d %s%s" % (os.path.relpath(path, REPO_ROOT).replace("\\", "/"), image.width,
                                  image.height, image.mode, " (+ new .meta)" if wrote_meta else ""))
    return image.size == (CANVAS, CANVAS)


def place(crop, fit):
    """The crop scaled uniformly (Lanczos) to fit fit x fit, centred at ART_CENTRE on a transparent canvas."""
    scale = min(fit / float(crop.width), fit / float(crop.height))
    size = (int(round(crop.width * scale)), int(round(crop.height * scale)))
    scaled = crop.resize(size, Image.LANCZOS)
    left = int(round(ART_CENTRE[0] - size[0] / 2.0))
    top = int(round(ART_CENTRE[1] - size[1] / 2.0))
    foreground = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    foreground.paste(scaled, (left, top), scaled)
    return foreground, size, left, top


def safe_circle_reach(foreground):
    """Largest distance from the canvas centre of any visible pixel (pixel corners, conservative)."""
    alpha = foreground.getchannel("A")
    box = alpha.getbbox()
    if box is None:
        return 0.0, 0
    data = alpha.load()
    centre = CANVAS / 2.0
    reach = 0.0
    outside = 0
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            if data[x, y] < ALPHA_VISIBLE:
                continue
            dx = max(abs(x - centre), abs(x + 1 - centre))
            dy = max(abs(y - centre), abs(y + 1 - centre))
            d = math.hypot(dx, dy)
            reach = max(reach, d)
            if d > SAFE_RADIUS:
                outside += 1
    return reach, outside


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mali2", required=True, help="path to mali2.png (React Native app assets/images/)")
    args = parser.parse_args()

    if not os.path.isfile(args.mali2):
        print("FAIL %s not found" % args.mali2)
        return 1
    digest = md5(args.mali2)
    print("Source %s md5 %s" % (os.path.basename(args.mali2), digest))
    if digest != MALI2_MD5:
        print("FAIL md5 differs from the approved mali2.png (%s)" % MALI2_MD5)
        return 1
    source = Image.open(args.mali2).convert("RGBA")
    if source.size != MALI2_SIZE:
        print("FAIL source is %dx%d, expected %dx%d" % (source.size + MALI2_SIZE))
        return 1

    crop = source.crop(ICON_CROP)
    fit = FIT_BOX
    foreground, size, left, top = place(crop, fit)
    reach, outside = safe_circle_reach(foreground)
    print("  spec fit %dx%d: scaled %dx%d at (%d, %d)-(%d, %d), reach %.1f px, %d visible pixels outside the circle"
          % (fit, fit, size[0], size[1], left, top, left + size[0], top + size[1], reach, outside))
    # Deviation from §6.6 (recorded in the WP4 report): at 640 the crop's flat bottom corners and the
    # ears leave the safe circle, so the fit box shrinks (same crop and centre) until every visible
    # pixel is inside it.
    while outside and fit > 2:
        fit -= 2
        foreground, size, left, top = place(crop, fit)
        reach, outside = safe_circle_reach(foreground)
    if fit != FIT_BOX:
        print("  fit reduced to %dx%d: scaled %dx%d at (%d, %d)-(%d, %d)"
              % (fit, fit, size[0], size[1], left, top, left + size[0], top + size[1]))

    background = Image.new("RGBA", (CANVAS, CANVAS), BACKGROUND)
    legacy = Image.alpha_composite(background, foreground).convert("RGB")

    ok = save(background.convert("RGB"), "AppIcon_Background.png")
    ok = save(foreground, "AppIcon_Foreground.png") and ok
    ok = save(legacy, "AppIcon_Legacy.png") and ok

    bbox = foreground.getchannel("A").point(lambda a: 255 if a >= ALPHA_VISIBLE else 0).getbbox()
    print("Foreground alpha bbox (alpha >= %d): %s" % (ALPHA_VISIBLE, bbox))
    print("Art reach from centre: %.1f px; safe-circle radius %.1f px; visible pixels outside: %d"
          % (reach, SAFE_RADIUS, outside))
    if outside:
        print("FAIL the art leaves the adaptive-icon safe circle")
        ok = False
    else:
        print("OK the art is inside the adaptive-icon safe circle")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

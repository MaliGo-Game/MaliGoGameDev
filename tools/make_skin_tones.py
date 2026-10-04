"""
Recolour the two Kenney skater skins into three skin tones each (DESIGN_SPEC.md §6.3b).

The stock skins share one light peach skin (base colour (245, 140, 106) with a gradient of shades).
Skin pixels are those with R in [243, 245], G in [115, 155], B in [85, 122] (face/hands gradient and the
ear, nose and lip accents; shirt colours such as (242, 101, 76) and (228, 120, 62) fall outside). Each
skin pixel becomes round(target * pixel / base) per channel, clamped to 255, alpha kept, for the targets
light (198, 140, 100), medium (150, 96, 62) and deep (96, 60, 40).

Inputs:  Assets/kenney_animated-characters-protagonists/Skins/{skaterFemaleA,skaterMaleA}.png (CC0)
Outputs: Assets/Resources/MaliGo/Skins/<input>_{light,medium,deep}.png, each with a Unity .meta written
         once (fresh GUID; texture type Default, mipmaps on, sRGB, max size 1024), and
         tools/out/skin_preview.png (all six side by side, never committed) for the human check H3.

Usage:  python tools/make_skin_tones.py
Prints the number of changed pixels per file. Exit code 0 = every file changed some pixels, 1 otherwise.
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prepare_mali_art import write_meta_once  # noqa: E402  (same §6 PNG template)

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC_DIR = os.path.join(REPO_ROOT, "Assets", "kenney_animated-characters-protagonists", "Skins")
OUT_DIR = os.path.join(REPO_ROOT, "Assets", "Resources", "MaliGo", "Skins")
PREVIEW = os.path.join(REPO_ROOT, "tools", "out", "skin_preview.png")

INPUTS = ("skaterFemaleA", "skaterMaleA")
BASE = (245, 140, 106)
TARGETS = (("light", (198, 140, 100)), ("medium", (150, 96, 62)), ("deep", (96, 60, 40)))
R_RANGE, G_RANGE, B_RANGE = (243, 245), (115, 155), (85, 122)


def is_skin(r, g, b):
    return R_RANGE[0] <= r <= R_RANGE[1] and G_RANGE[0] <= g <= G_RANGE[1] and B_RANGE[0] <= b <= B_RANGE[1]


def recolour(image, target):
    pixels = list(image.getdata())
    out, changed = [], 0
    for r, g, b, a in pixels:
        if a > 0 and is_skin(r, g, b):
            new = tuple(min(255, int(round(t * c / float(base)))) for t, c, base in zip(target, (r, g, b), BASE))
            if new != (r, g, b):
                changed += 1
            out.append(new + (a,))
        else:
            out.append((r, g, b, a))
    result = Image.new("RGBA", image.size)
    result.putdata(out)
    return result, changed


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    results, ok = [], True
    for name in INPUTS:
        src = os.path.join(SRC_DIR, name + ".png")
        if not os.path.isfile(src):
            print("MISSING %s" % src)
            return 1
        image = Image.open(src).convert("RGBA")
        for tone, target in TARGETS:
            recoloured, changed = recolour(image, target)
            path = os.path.join(OUT_DIR, "%s_%s.png" % (name, tone))
            recoloured.save(path, optimize=True)
            wrote_meta = write_meta_once(path, 1024, mipmaps=True, texture_type=0)
            ok = ok and changed > 0
            print("%-26s %dx%d changed %d pixels%s" % (os.path.basename(path), image.width, image.height, changed,
                                                     " (+ new .meta)" if wrote_meta else ""))
            results.append(recoloured)

    gap = 8
    width = sum(r.width for r in results) + gap * (len(results) - 1)
    height = max(r.height for r in results)
    preview = Image.new("RGBA", (width, height), (251, 246, 236, 255))
    x = 0
    for r in results:
        preview.paste(r, (x, 0), r)
        x += r.width + gap
    os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
    preview.save(PREVIEW)
    print("preview %s (%dx%d, not committed)" % (os.path.relpath(PREVIEW, REPO_ROOT), width, height))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

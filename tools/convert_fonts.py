"""
Convert the staged Aileron OpenType-CFF fonts to TrueType outlines for Unity's legacy Text.

DESIGN_SPEC.md §5.2 rule 5 and §6.1: Unity's legacy `UnityEngine.UI.Text` has never been tested in this
project with CFF outlines, so the three weights the game uses are converted to quadratic (glyf/loca)
outlines with fontTools (cu2qu, tolerance 1.0 font unit). The CFF and VORG tables are dropped, `maxp` is
set to version 1.0, `post` to format 2 and the sfnt version to 0x00010000. Aileron is CC0, so the
conversion is allowed.

For each output the script prints the glyph count, the cmap size (expected 337) and whether U+2212
(minus sign) is mapped. A Unity `.meta` (template §6.5, fresh GUID) is written next to each font unless
one already exists, so re-running keeps the GUIDs.

Usage:
    python tools/convert_fonts.py                         # staging folder -> Assets/Resources/MaliGo/Fonts
    python tools/convert_fonts.py --src <dir> --out-dir <dir> [--no-meta]
Exit code: 0 = all converted and checked, 1 = a check failed, 2 = a source is missing.
"""
import argparse
import os
import sys
import uuid

from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_SRC = os.path.join(r"C:\Temp\MaliGoAssetsStaging", "dotcolon_aileron")
DEFAULT_OUT = os.path.join(REPO_ROOT, "Assets", "Resources", "MaliGo", "Fonts")
WEIGHTS = ("SemiBold", "Bold", "Black")
EXPECTED_CMAP = 337
TOLERANCE = 1.0  # font units

FONT_META = """fileFormatVersion: 2
guid: {guid}
TrueTypeFontImporter:
  externalObjects: {{}}
  serializedVersion: 4
  fontSize: 16
  forceTextureCase: -2
  characterSpacing: 0
  characterPadding: 1
  includeFontData: 1
  fontNames:
  - Aileron
  fallbackFontReferences: []
  customCharacters:
  fontRenderingMode: 0
  ascentCalculationMode: 1
  useLegacyBoundsCalculation: 0
  shouldRoundAdvanceValue: 1
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def quadratic_glyphs(glyph_set):
    result = {}
    for name in glyph_set.keys():
        tt_pen = TTGlyphPen(None)
        glyph_set[name].draw(Cu2QuPen(tt_pen, TOLERANCE, reverse_direction=True))
        result[name] = tt_pen.glyph()
    return result


def convert(font):
    if font.sfntVersion != "OTTO" or "CFF " not in font:
        raise ValueError("not an OpenType-CFF font")
    order = font.getGlyphOrder()

    font["loca"] = newTable("loca")
    glyf = font["glyf"] = newTable("glyf")
    glyf.glyphOrder = order
    glyf.glyphs = quadratic_glyphs(font.getGlyphSet())
    del font["CFF "]
    if "VORG" in font:
        del font["VORG"]
    glyf.compile(font)

    # Left side bearings follow the new outlines' bounds.
    hmtx = font["hmtx"]
    for name, glyph in glyf.glyphs.items():
        if hasattr(glyph, "xMin"):
            hmtx[name] = (hmtx[name][0], glyph.xMin)

    maxp = font["maxp"] = newTable("maxp")
    maxp.tableVersion = 0x00010000
    maxp.maxZones = 1
    maxp.maxTwilightPoints = 0
    maxp.maxStorage = 0
    maxp.maxFunctionDefs = 0
    maxp.maxInstructionDefs = 0
    maxp.maxStackElements = 0
    maxp.maxSizeOfInstructions = 0
    maxp.maxComponentElements = 0
    maxp.compile(font)

    post = font["post"]
    post.formatType = 2.0
    post.extraNames = []
    post.mapping = {}
    post.glyphOrder = order

    font["head"].glyphDataFormat = 0
    font.sfntVersion = "\x00\x01\x00\x00"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--src", default=DEFAULT_SRC, help="folder holding Aileron-<Weight>.otf")
    parser.add_argument("--out-dir", default=DEFAULT_OUT, help="where the .ttf files are written")
    parser.add_argument("--no-meta", action="store_true", help="do not write Unity .meta files")
    args = parser.parse_args()

    os.makedirs(args.out_dir, exist_ok=True)
    ok = True
    for weight in WEIGHTS:
        src = os.path.join(args.src, "Aileron-%s.otf" % weight)
        if not os.path.isfile(src):
            print("MISSING %s" % src)
            return 2
        font = TTFont(src)
        convert(font)
        dst = os.path.join(args.out_dir, "Aileron-%s.ttf" % weight)
        font.save(dst)

        # Re-open the written file so the printed numbers describe what Unity will import.
        check = TTFont(dst)
        cmap = check.getBestCmap()
        has_minus = 0x2212 in cmap
        tables_ok = "glyf" in check and "CFF " not in check and check.sfntVersion == "\x00\x01\x00\x00"
        line_ok = len(cmap) == EXPECTED_CMAP and has_minus and tables_ok
        ok = ok and line_ok
        print("%s %-22s glyphs=%d cmap=%d U+2212=%s maxp=%s post=%s %s" % (
            "OK  " if line_ok else "FAIL", os.path.basename(dst), len(check.getGlyphOrder()), len(cmap),
            "mapped" if has_minus else "MISSING", hex(check["maxp"].tableVersion), check["post"].formatType,
            "glyf" if tables_ok else "tables wrong"))

        meta = dst + ".meta"
        if not args.no_meta and not os.path.exists(meta):
            with open(meta, "w", encoding="utf-8", newline="\n") as handle:
                handle.write(FONT_META.format(guid=uuid.uuid4().hex))
            print("     wrote %s" % os.path.basename(meta))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

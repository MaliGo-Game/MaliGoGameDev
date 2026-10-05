"""
Prepare Mali's in-game art (DESIGN_SPEC.md §6.3).

Outputs (both under Assets/Resources/MaliGo/Mali/, each with a Unity .meta written once, fresh GUID,
following the §6 PNG template: sprite, single mode, mipmaps off, max size 1024):

* MaliPortrait.png, 512 x 512: from `mali2.png` (the React Native app's assets/images/, 534 x 615 RGBA).
  The source's MD5 is verified first (09c1e7891c32a254d60a32d528337a47). Crop (77, 47)-(449, 552)
  (the alpha bbox (93, 63)-(433, 536) padded 16 px), scale uniformly to 512 tall (Lanczos), paste centred
  horizontally and bottom-aligned on a transparent 512 x 512 canvas.
* MaliWave.png, 512 x 640: from `Assets/MaliGo Pitch Deck.png` (779 x 779 RGBA). Crop (152, 59)-(636, 699)
  (alpha bbox (168, 75)-(620, 683) padded 16 px) = 484 x 640, paste centred on a 512 x 640 canvas.

`Assets/Mali Dumbfound.png` is not used (mirrored R on the coin). The art is the founder's brand art,
approved for the app (A5), not CC0: it is not listed in any asset-pack licence file.

Usage:
    python tools/prepare_mali_art.py --mali2 <path to mali2.png>
    python tools/prepare_mali_art.py                 # MaliWave only; reports that the portrait was skipped
Prints each output's size and the source MD5s. Exit code 0 = done, 1 = a check failed (wrong MD5 or size).
"""
import argparse
import hashlib
import os
import sys
import uuid

from PIL import Image

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(REPO_ROOT, "Assets", "Resources", "MaliGo", "Mali")
WAVE_SOURCE = os.path.join(REPO_ROOT, "Assets", "MaliGo Pitch Deck.png")

MALI2_MD5 = "09c1e7891c32a254d60a32d528337a47"
MALI2_SIZE = (534, 615)
PORTRAIT_CROP = (77, 47, 449, 552)    # alpha bbox (93, 63)-(433, 536) padded 16
PORTRAIT_CANVAS = (512, 512)
WAVE_SIZE = (779, 779)
WAVE_CROP = (152, 59, 636, 699)       # alpha bbox (168, 75)-(620, 683) padded 16
WAVE_CANVAS = (512, 640)


def png_meta(guid, max_size, mipmaps, texture_type=8):
    """The §6 PNG .meta template (from MaliGoUI/panel_brown.png.meta, single sprite, no sprite entries)."""
    platforms = "".join("""  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
""".format(target=t, max_size=max_size) for t in ("DefaultTexturePlatform", "Standalone", "Android", "WebGL", "iOS"))
    return """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: {mip}
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: {texture_type}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
{platforms}  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""".format(guid=guid, mip=1 if mipmaps else 0, max_size=max_size, texture_type=texture_type, platforms=platforms)


def write_meta_once(png_path, max_size, mipmaps, texture_type=8):
    meta = png_path + ".meta"
    if os.path.exists(meta):
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(png_meta(uuid.uuid4().hex, max_size, mipmaps, texture_type))
    return True


def md5(path):
    with open(path, "rb") as handle:
        return hashlib.md5(handle.read()).hexdigest()


def alpha_bbox(image):
    return image.getchannel("A").getbbox()


def save(image, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    image.save(path, optimize=True)
    wrote_meta = write_meta_once(path, 1024, mipmaps=False)
    print("  -> %s %dx%d%s" % (os.path.relpath(path, REPO_ROOT), image.width, image.height,
                                " (+ new .meta)" if wrote_meta else ""))
    return path


def make_portrait(source):
    digest = md5(source)
    print("MaliPortrait source %s md5 %s" % (os.path.basename(source), digest))
    if digest != MALI2_MD5:
        print("FAIL md5 differs from the approved mali2.png (%s)" % MALI2_MD5)
        return False
    image = Image.open(source).convert("RGBA")
    if image.size != MALI2_SIZE:
        print("FAIL source is %dx%d, expected %dx%d" % (image.size + MALI2_SIZE))
        return False
    print("  alpha bbox %s (spec (93, 63)-(433, 536))" % (alpha_bbox(image),))
    crop = image.crop(PORTRAIT_CROP)
    height = PORTRAIT_CANVAS[1]
    width = int(round(crop.width * height / float(crop.height)))
    scaled = crop.resize((width, height), Image.LANCZOS)
    canvas = Image.new("RGBA", PORTRAIT_CANVAS, (0, 0, 0, 0))
    canvas.paste(scaled, ((PORTRAIT_CANVAS[0] - width) // 2, PORTRAIT_CANVAS[1] - height), scaled)
    print("  crop %dx%d -> scaled %dx%d" % (crop.width, crop.height, width, height))
    save(canvas, "MaliPortrait.png")
    return canvas.size == PORTRAIT_CANVAS


def make_wave():
    digest = md5(WAVE_SOURCE)
    print("MaliWave source %s md5 %s" % (os.path.basename(WAVE_SOURCE), digest))
    image = Image.open(WAVE_SOURCE).convert("RGBA")
    if image.size != WAVE_SIZE:
        print("FAIL source is %dx%d, expected %dx%d" % (image.size + WAVE_SIZE))
        return False
    print("  alpha bbox %s (spec (168, 75)-(620, 683))" % (alpha_bbox(image),))
    crop = image.crop(WAVE_CROP)
    canvas = Image.new("RGBA", WAVE_CANVAS, (0, 0, 0, 0))
    canvas.paste(crop, ((WAVE_CANVAS[0] - crop.width) // 2, (WAVE_CANVAS[1] - crop.height) // 2), crop)
    print("  crop %dx%d" % (crop.width, crop.height))
    save(canvas, "MaliWave.png")
    return canvas.size == WAVE_CANVAS


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mali2", help="path to mali2.png (React Native app assets/images/)")
    args = parser.parse_args()

    ok = make_wave()
    if args.mali2:
        if not os.path.isfile(args.mali2):
            print("FAIL %s not found" % args.mali2)
            return 1
        ok = make_portrait(args.mali2) and ok
    else:
        print("MaliPortrait SKIPPED: pass --mali2 <path to mali2.png> (md5 %s)" % MALI2_MD5)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

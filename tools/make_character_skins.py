"""
Build the selectable character outfits (CharacterLooks in Assets/MaliGo/Data) from Kenney CC0 skins.

Every Kenney "Animated Characters" pack ships the same model (characterMedium.fbx, byte-identical MD5
8fbdad2365309ebcfaca89674408badf) and the same idle/run/jump clips, so any of their skins maps onto the
player's model and animator unchanged. Six skins are used:

  id        source                                                     fix
  skater    kenney_animated-characters-protagonists/Skins/skaterFemaleA  -
  street    kenney_animated-characters-protagonists/Skins/skaterMaleA    -
  nightout  kenney_animated-characters-protagonists/Skins/cyborgFemaleA  mirror: the human half of the face and
                                                                          body replaces the robot half
  smart     kenney_animated-characters-protagonists/Skins/criminalMaleA  -
  outdoors  kenney_animated-characters-survivors/Skins/survivorFemaleA   destain: the small grey-brown dirt
  workwear  kenney_animated-characters-survivors/Skins/survivorMaleB       splashes are filled from their surroundings

The survivors pack (https://kenney.nl/assets/animated-characters-survivors, CC0) is not in the project; its two
skins and its License.txt are kept in Assets/MaliGo/Characters/SkinSources~ (Unity ignores "~" folders, so they
never import or ship).

Skin mask. Each skin has one flat skin colour B (with a few lighter/darker shades and accents). A texel C is
skin when its per-channel ratio to B is nearly uniform: k = mean(C / B) in [K_MIN, K_MAX] and every channel of
C / B within TOLERANCE of k (shirts and hair in similar hues fail the uniformity test). Anti-aliased edge texels
next to skin get a partial weight w by unmixing C = w B + (1 - w) O, O being the most different neighbour. The
mask is stored in the output's alpha: 255 = not skin, 255 - round(127 w) otherwise (128 = all skin; alpha never
goes below 128). The runtime (SkinToneMath) recolours from it once per selection, never per frame.

Outputs: Assets/Resources/MaliGo/Characters/Outfits/<id>.png, 512 x 512 RGBA (box-downscaled from 1024), each
with a Unity .meta written once (Default type, Read/Write on, uncompressed, no mipmaps, alpha is not transparency:
the runtime reads the texels and makes its own mipmapped copy). With --preview, tools/out/character_preview.png
renders every outfit in every tone on the model (bind pose; not committed) for the human check.

Usage:  python tools/make_character_skins.py [--preview]
Exit code 0 = every outfit written with a plausible skin mask, 1 otherwise.
"""
import argparse
import os
import sys
import uuid

import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.spatial import cKDTree

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(REPO_ROOT, "tools", "fbx_stride"))
import fbxparse  # noqa: E402

PROTAGONISTS = os.path.join(REPO_ROOT, "Assets", "kenney_animated-characters-protagonists")
SOURCES = os.path.join(REPO_ROOT, "Assets", "MaliGo", "Characters", "SkinSources~")
MODEL = os.path.join(PROTAGONISTS, "Model", "characterMedium.fbx")
OUT_DIR = os.path.join(REPO_ROOT, "Assets", "Resources", "MaliGo", "Characters", "Outfits")
PREVIEW = os.path.join(REPO_ROOT, "tools", "out", "character_preview.png")

OUT_SIZE = 512
K_MIN, K_MAX, TOLERANCE = 0.8, 1.2, 0.12
KENNEY_SKIN = (245, 146, 113)

# id, source png, skin base colour, fix
OUTFITS = (
    ("skater", os.path.join(PROTAGONISTS, "Skins", "skaterFemaleA.png"), KENNEY_SKIN, None),
    ("street", os.path.join(PROTAGONISTS, "Skins", "skaterMaleA.png"), KENNEY_SKIN, None),
    ("nightout", os.path.join(PROTAGONISTS, "Skins", "cyborgFemaleA.png"), KENNEY_SKIN, "mirror"),
    ("smart", os.path.join(PROTAGONISTS, "Skins", "criminalMaleA.png"), KENNEY_SKIN, None),
    ("outdoors", os.path.join(SOURCES, "survivorFemaleA.png"), (135, 34, 26), "destain"),
    ("workwear", os.path.join(SOURCES, "survivorMaleB.png"), KENNEY_SKIN, "destain"),
)

# Must match CharacterLooks.Tones (Assets/MaliGo/Data/CharacterLooks.cs); only used for the preview.
TONES = ((234, 186, 148), (198, 140, 100), (150, 96, 62), (124, 80, 52), (96, 60, 40), (78, 48, 32))


# ---------------------------------------------------------------- mesh

def load_mesh():
    """Triangles of characterMedium as (n, 3, 3) positions (x mirrored about the model's centre plane) and
    (n, 3, 2) UVs."""
    root, _ = fbxparse.parse(MODEL)
    positions, uvs = [], []
    for geometry in root.first("Objects").find("Geometry"):
        vertices_node = geometry.first("Vertices")
        if vertices_node is None:
            continue
        vertices = np.array(vertices_node.props[0]).reshape(-1, 3)
        indices = geometry.first("PolygonVertexIndex").props[0]
        layer = geometry.first("LayerElementUV")
        uv = np.array(layer.first("UV").props[0]).reshape(-1, 2)
        uv_index = layer.first("UVIndex").props[0]
        polygon = []
        for k, i in enumerate(indices):
            last = i < 0
            polygon.append((~i if last else i, uv_index[k]))
            if last:
                for t in range(1, len(polygon) - 1):
                    corners = (polygon[0], polygon[t], polygon[t + 1])
                    positions.append([vertices[v] for v, _ in corners])
                    uvs.append([uv[u] for _, u in corners])
                polygon = []
    positions = np.array(positions)
    # The FBX is Z-up; x is the left/right axis either way.
    positions = positions[..., [0, 2, 1]] * np.array([1.0, 1.0, -1.0])
    flat = positions.reshape(-1, 3)
    positions[..., 0] -= (flat[:, 0].min() + flat[:, 0].max()) * 0.5
    return positions, np.array(uvs)


def triangle_texels(uv_tri, width, height, slack=1.5):
    """Texel grid of a UV triangle: (y0, x0, barycentrics b0, b1, b2, inside mask) or None."""
    xs = uv_tri[:, 0] * width
    ys = (1.0 - uv_tri[:, 1]) * height
    x0, x1 = int(max(0, np.floor(xs.min()) - 1)), int(min(width - 1, np.ceil(xs.max()) + 1))
    y0, y1 = int(max(0, np.floor(ys.min()) - 1)), int(min(height - 1, np.ceil(ys.max()) + 1))
    if x1 < x0 or y1 < y0:
        return None
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
    if abs(d) < 1e-9:
        return None
    b0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
    b1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
    b2 = 1.0 - b0 - b1
    eps = -slack / max(1.0, abs(d) ** 0.5)
    inside = (b0 >= eps) & (b1 >= eps) & (b2 >= eps)
    return y0, x0, (b0, b1, b2), inside


# ---------------------------------------------------------------- fixes

def hsv(rgb):
    f = rgb.astype(np.float32) / 255.0
    mx, mn = f.max(-1), f.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = f[..., 0], f[..., 1], f[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) * 60.0
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def fill_from_nearest(image, mask):
    """Each masked texel takes the colour of the nearest unmasked texel."""
    if not mask.any():
        return image
    _, (iy, ix) = ndimage.distance_transform_edt(mask, return_indices=True)
    out = image.copy()
    out[mask] = image[iy[mask], ix[mask]]
    return out


def mirror_fix(image, mesh, band=0.15):
    """Copies the model's right half (x < 0, the human half of the cyborg skin) onto its left half through the
    mesh's mirror symmetry, then fills robot-coloured texels left along the centre seam."""
    positions, uvs = mesh
    source = image.copy()
    height, width = image.shape[:2]
    tree = cKDTree(positions.mean(1))
    near_centre = np.zeros((height, width), bool)
    for i, tri in enumerate(positions):
        mirrored = tri * np.array([-1.0, 1.0, 1.0])
        distance, j = tree.query(mirrored.mean(0))
        if distance > 0.005:
            continue
        perm = [int(np.argmin(np.linalg.norm(positions[j] - mirrored[k], axis=1))) for k in range(3)]
        grid = triangle_texels(uvs[i], width, height)
        if grid is None:
            continue
        y0, x0, bary, inside = grid
        px = bary[0] * tri[0, 0] + bary[1] * tri[1, 0] + bary[2] * tri[2, 0]
        hh, ww = inside.shape
        near_centre[y0:y0 + hh, x0:x0 + ww] |= inside & (np.abs(px) < band)
        take = inside & (px > 0)
        if not take.any():
            continue
        bj = [None, None, None]
        for k in range(3):
            bj[perm[k]] = bary[k]
        su = bj[0] * uvs[j][0, 0] + bj[1] * uvs[j][1, 0] + bj[2] * uvs[j][2, 0]
        sv = bj[0] * uvs[j][0, 1] + bj[1] * uvs[j][1, 1] + bj[2] * uvs[j][2, 1]
        sx = np.clip((su * width).astype(int), 0, width - 1)
        sy = np.clip(((1.0 - sv) * height).astype(int), 0, height - 1)
        region = image[y0:y0 + hh, x0:x0 + ww]
        region[take] = source[sy[take], sx[take]]
    h, s, v = hsv(image[..., :3])
    robot = (h >= 185) & (h <= 230) & (s <= 0.5) & (v >= 0.45)
    bright = (s < 0.3) & (v > 0.55)
    seam = near_centre & (robot | bright)
    seam = ndimage.binary_dilation(seam, iterations=2) & near_centre
    return fill_from_nearest(image, seam | (robot & ~near_centre & (s >= 0.08) & (s <= 0.35) & (v >= 0.6)))


def destain(image, max_area=20000):
    """Fills the survivors' small grey-brown dirt splashes from the colour around them (larger areas of the same
    colour, such as boots, are kept)."""
    h, s, v = hsv(image[..., :3])
    dirt = (h >= 15) & (h <= 35) & (s >= 0.12) & (s <= 0.34) & (v >= 0.30) & (v <= 0.52)
    labels, count = ndimage.label(dirt, structure=np.ones((3, 3)))
    if count == 0:
        return image
    areas = ndimage.sum(dirt, labels, range(1, count + 1))
    small = np.zeros(count + 1, bool)
    small[1:] = areas < max_area
    mask = ndimage.binary_dilation(small[labels] & dirt, iterations=2)
    return fill_from_nearest(image, mask)


# ---------------------------------------------------------------- skin mask

def skin_weight(rgb, base):
    """Per-texel skin weight in [0, 1]: 1 on skin (uniform ratio to base), a partial weight on anti-aliased edges
    next to skin, 0 elsewhere."""
    f = rgb.astype(np.float32)
    b = np.array(base, np.float32)
    ratio = f / b
    k = ratio.mean(-1)
    skin = (k >= K_MIN) & (k <= K_MAX) & (np.abs(ratio - k[..., None]).max(-1) <= TOLERANCE)
    weight = skin.astype(np.float32)

    edge = ndimage.binary_dilation(skin, structure=np.ones((3, 3))) & ~skin
    ys, xs = np.nonzero(edge)
    height, width = skin.shape
    for y, x in zip(ys, xs):
        y0, y1, x0, x1 = max(0, y - 1), min(height, y + 2), max(0, x - 1), min(width, x + 2)
        patch = f[y0:y1, x0:x1].reshape(-1, 3)
        patch_skin = skin[y0:y1, x0:x1].reshape(-1)
        others = patch[~patch_skin]
        if len(others) == 0:
            continue
        other = others[np.argmax(np.linalg.norm(others - b, axis=1))]
        axis = b - other
        length = float(axis @ axis)
        if length < 400.0:  # O too close to B to unmix
            continue
        weight[y, x] = float(np.clip((f[y, x] - other) @ axis / length, 0.0, 1.0))
    return weight


def encode(rgb, weight):
    alpha = np.where(weight > 0, 255 - np.round(weight * 127), 255).astype(np.uint8)
    return np.dstack([rgb.astype(np.uint8), alpha])


def downscale(rgb, weight, size):
    image = Image.fromarray(rgb.astype(np.uint8)).resize((size, size), Image.BOX)
    w = Image.fromarray(weight.astype(np.float32)).resize((size, size), Image.BOX)
    return np.array(image), np.clip(np.array(w, dtype=np.float32), 0.0, 1.0)


# ---------------------------------------------------------------- recolour (mirror of SkinToneMath.Recolour)

def recolour(rgba, base, tone):
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3].astype(np.int32)
    b = np.array(base, np.float32)
    t = np.array(tone, np.float32)
    w = np.clip((255 - a) / 127.0, 0, 1)[..., None]
    full = np.clip(np.round(rgb * t / b), 0, 255)
    partial = np.clip(np.round(rgb + w * (t - b)), 0, 255)
    out = np.where((a <= 128)[..., None], full, np.where((a < 255)[..., None], partial, rgb))
    return out.astype(np.uint8)


# ---------------------------------------------------------------- meta

def outfit_meta(guid, max_size):
    platforms = "".join("""  - serializedVersion: 4
    buildTarget: {target}
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
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
    enableMipMap: 0
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
  isReadable: 1
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 1
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
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 0
  spriteTessellationDetail: -1
  textureType: 0
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
""".format(guid=guid, max_size=max_size, platforms=platforms)


def write_meta_once(path):
    meta = path + ".meta"
    if os.path.exists(meta):
        return False
    with open(meta, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(outfit_meta(uuid.uuid4().hex, OUT_SIZE))
    return True


# ---------------------------------------------------------------- preview

def render(mesh, texture, size=240):
    """Front view of the bind pose with simple facing-ratio shading."""
    positions, uvs = mesh
    tex = texture.astype(np.float32)
    th, tw = tex.shape[:2]
    flat = positions.reshape(-1, 3)
    mn, mx = flat.min(0), flat.max(0)
    scale = (size - 12) / (mx[1] - mn[1])
    img = np.full((size, size, 3), 236.0)
    depth = np.full((size, size), -1e9)
    for tri, uv in zip(positions, uvs):
        xs = tri[:, 0] * scale + size / 2
        ys = size - 6 - (tri[:, 1] - mn[1]) * scale
        normal = np.cross(tri[1] - tri[0], tri[2] - tri[0])
        length = np.linalg.norm(normal)
        if length == 0:
            continue
        shade = 0.6 + 0.4 * abs(normal[2] / length)
        x0, x1 = int(max(0, np.floor(xs.min()))), int(min(size - 1, np.ceil(xs.max())))
        y0, y1 = int(max(0, np.floor(ys.min()))), int(min(size - 1, np.ceil(ys.max())))
        if x1 < x0 or y1 < y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        d = (ys[1] - ys[2]) * (xs[0] - xs[2]) + (xs[2] - xs[1]) * (ys[0] - ys[2])
        if abs(d) < 1e-9:
            continue
        w0 = ((ys[1] - ys[2]) * (gx - xs[2]) + (xs[2] - xs[1]) * (gy - ys[2])) / d
        w1 = ((ys[2] - ys[0]) * (gx - xs[2]) + (xs[0] - xs[2]) * (gy - ys[2])) / d
        w2 = 1 - w0 - w1
        m = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        z = w0 * tri[0, 2] + w1 * tri[1, 2] + w2 * tri[2, 2]
        sub = depth[y0:y1 + 1, x0:x1 + 1]
        m &= z > sub
        if not m.any():
            continue
        u = w0 * uv[0, 0] + w1 * uv[1, 0] + w2 * uv[2, 0]
        v = w0 * uv[0, 1] + w1 * uv[1, 1] + w2 * uv[2, 1]
        tx = np.clip((u % 1.0) * tw, 0, tw - 1).astype(int)
        ty = np.clip((1 - (v % 1.0)) * th, 0, th - 1).astype(int)
        sub[m] = z[m]
        img[y0:y1 + 1, x0:x1 + 1][m] = tex[ty, tx][m] * shade
    return img.clip(0, 255).astype(np.uint8)


# ---------------------------------------------------------------- main

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--preview", action="store_true", help="also write tools/out/character_preview.png")
    args = parser.parse_args()

    mesh = load_mesh()
    os.makedirs(OUT_DIR, exist_ok=True)
    ok = True
    outputs = []
    for outfit_id, source, base, fix in OUTFITS:
        if not os.path.isfile(source):
            print("MISSING %s" % source)
            return 1
        image = np.array(Image.open(source).convert("RGB"))
        if fix == "mirror":
            image = mirror_fix(image, mesh)
        elif fix == "destain":
            image = destain(image)
        weight = skin_weight(image, base)
        rgb, w = downscale(image, weight, OUT_SIZE)
        rgba = encode(rgb, w)
        path = os.path.join(OUT_DIR, outfit_id + ".png")
        Image.fromarray(rgba).save(path, optimize=True)
        wrote_meta = write_meta_once(path)
        share = float((w > 0.999).mean())
        plausible = 0.15 <= share <= 0.5
        ok = ok and plausible
        print("%-9s %dx%d skin %.1f%% edge %.1f%%%s%s" % (
            outfit_id, OUT_SIZE, OUT_SIZE, share * 100, float(((w > 0) & (w <= 0.999)).mean()) * 100,
            " (+ new .meta)" if wrote_meta else "", "" if plausible else "  <- implausible skin share"))
        outputs.append((rgba, base))

    if args.preview:
        cell = 240
        sheet = np.full((cell * len(outputs), cell * len(TONES), 3), 255, np.uint8)
        for row, (rgba, base) in enumerate(outputs):
            for col, tone in enumerate(TONES):
                sheet[row * cell:(row + 1) * cell, col * cell:(col + 1) * cell] = render(mesh, recolour(rgba, base, tone), cell)
        os.makedirs(os.path.dirname(PREVIEW), exist_ok=True)
        Image.fromarray(sheet).save(PREVIEW)
        print("preview %s (not committed)" % os.path.relpath(PREVIEW, REPO_ROOT))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())

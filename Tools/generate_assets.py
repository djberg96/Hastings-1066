#!/usr/bin/env python3
"""Derive the Unity map from the repository SVG without changing that source.

The SVG uses a duplicate id for 1420. Hexes are keyed by their printed four
digit coordinate, so duplicate drawing elements are collapsed here.
"""

import base64
import io
import json
import math
import random
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Images/Misc/hex_map.svg"
DEST = ROOT / "UnityProject/Assets/Resources/Data/Map.json"
TEXTURE = ROOT / "UnityProject/Assets/Resources/Art/Map/hex_map.png"
TERRAIN_SWATCHES = ROOT / "UnityProject/Assets/Resources/Art/Terrain"
TERRAIN_ART = ROOT / "Tools/terrain_art"
XLINK = "{http://www.w3.org/1999/xlink}href"
SVG = "{http://www.w3.org/2000/svg}"
TRANSLATE = re.compile(r"translate\(\s*([-\d.]+)(?:[,\s]+([-\d.]+))?\s*\)")
HEX_ID = re.compile(r"\d{4}$")


def offset(transform):
    x = y = 0.0
    for a, b in TRANSLATE.findall(transform or ""):
        x += float(a)
        y += float(b or 0)
    return x, y


def distance_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    if dx == dy == 0:
        return math.hypot(px - ax, py - ay)
    t = max(0, min(1, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


root = ET.parse(SOURCE).getroot()
hexes = {}
ridge_drawings = []
ridge_key_drawings = []
wood_points = []
marsh_points = []
streams = []
road = []


def visit(element, parent_x=0.0, parent_y=0.0, parent_id=""):
    ox, oy = offset(element.get("transform"))
    x, y = parent_x + ox, parent_y + oy
    ident = element.get("id", "")
    href = element.get(XLINK, "")
    if HEX_ID.fullmatch(ident) and href.startswith("#level"):
        hexes.setdefault(ident, {"id": ident, "x": x + 50, "y": y + 58,
                               "level": int(href[-1])})
    if re.match(r"^\d{4}_ridge(?:_|$)", ident) and href.startswith("#ridge_line_"):
        ridge_drawings.append((ident, x, y, href[len("#ridge_line_"):]))
    if ident in ("ridge_top_left", "ridge_top_right") and href.startswith("#ridge_line_"):
        ridge_key_drawings.append((ident, x, y, href[len("#ridge_line_"):]))
    if ident.startswith("trees_") and href == "#trees":
        wood_points.append((x + 40, y + 39))
    if ident.startswith("reeds_") and href == "#reeds":
        marsh_points.append((x + 40, y + 48))
    if element.tag.endswith("polyline") and parent_id == "all_streams":
        pass
    if element.tag.endswith("polyline") and any(a.get("id") == "all_streams" for a in ancestors):
        points = [tuple(map(float, p)) for p in re.findall(r"([-\d.]+)\s*,\s*([-\d.]+)", element.get("points", ""))]
        streams.extend([((a[0] + x, a[1] + y), (b[0] + x, b[1] + y))
                        for a, b in zip(points, points[1:])])
    if ident == "road":
        for child in element:
            if child.tag.endswith("polyline"):
                points = [tuple(map(float, p)) for p in re.findall(r"([-\d.]+)\s*,\s*([-\d.]+)", child.get("points", ""))]
                road.extend([((a[0] + x, a[1] + y), (b[0] + x, b[1] + y))
                             for a, b in zip(points, points[1:])])
    ancestors.append(element)
    for child in element:
        visit(child, x, y, ident)
    ancestors.pop()


ancestors = []
visit(root)

ids = list(hexes)
def nearest_hex(x, y):
    return min(hexes.values(), key=lambda h: math.hypot(h["x"] - x, h["y"] - y))["id"]

woods = {nearest_hex(x, y) for x, y in wood_points}
marshes = {nearest_hex(x, y) for x, y in marsh_points}
ridge_edges = set()
side_offsets = {"left": (-100, 0), "right": (100, 0),
                "upper_left": (-50, -86), "upper_right": (50, -86),
                "top_left": (-50, -86), "top_right": (50, -86),
                "lower_left": (-50, 86), "lower_right": (50, 86)}
for ident, _, _, side in ridge_drawings:
    a_id = ident[:4]
    if a_id not in hexes:
        continue
    for part in ("top_left", "top_right") if side == "top" else (side,):
        if part not in side_offsets:
            continue
        a = hexes[a_id]
        dx, dy = side_offsets[part]
        target = min(hexes.values(), key=lambda h: math.hypot(h["x"] - a["x"] - dx,
                                                             h["y"] - a["y"] - dy))
        if math.hypot(target["x"] - a["x"] - dx, target["y"] - a["y"] - dy) < 5:
            ridge_edges.add(tuple(sorted((a["id"], target["id"]))))
edges = []
for i, a_id in enumerate(ids):
    a = hexes[a_id]
    for b_id in ids[i + 1:]:
        b = hexes[b_id]
        if not 95 < math.hypot(a["x"] - b["x"], a["y"] - b["y"]) < 117:
            continue
        mx, my = (a["x"] + b["x"]) / 2, (a["y"] + b["y"]) / 2
        stream = any(distance_to_segment(mx, my, *p, *q) < 14 for p, q in streams)
        # Ridge markings are attached to a numbered hex and its graphic side.
        ridge = tuple(sorted((a_id, b_id))) in ridge_edges
        # Elevation transitions outside the marked ridge artwork are gentle slopes.
        edges.append({"a": a_id, "b": b_id, "stream": stream, "ridge": ridge})

for h in hexes.values():
    h["woods"] = h["id"] in woods
    h["marsh"] = h["id"] in marshes
    h["road"] = any(distance_to_segment(h["x"], h["y"], *p, *q) < 48 for p, q in road)

DEST.parent.mkdir(parents=True, exist_ok=True)
DEST.write_text(json.dumps({"width": 2950, "height": 2400,
                            "hexes": sorted(hexes.values(), key=lambda h: h["id"]),
                            "edges": edges}, separators=(",", ":")))


def png_uri(image):
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")


def terrain_mask(marked_hexes):
    mask = Image.new("L", (2950, 2400))
    draw = ImageDraw.Draw(mask)
    points = [(hexes[ident]["x"], hexes[ident]["y"]) for ident in sorted(marked_hexes)]
    # Join adjoining terrain hexes so each copse or wetland reads as one area,
    # instead of a repeated stamp centered in every hex.
    for index, (x, y) in enumerate(points):
        for other_x, other_y in points[index + 1:]:
            if 95 < math.hypot(x - other_x, y - other_y) < 117:
                draw.line((x, y, other_x, other_y), fill=255, width=47)
    for index, (x, y) in enumerate(points):
        rng = random.Random(1066 + index * 7919 + int(x) * 17)
        polygon = []
        for step in range(26):
            angle = 2 * math.pi * step / 26
            radius = rng.uniform(.88, 1.1)
            polygon.append((x + math.cos(angle) * 43 * radius,
                            y + math.sin(angle) * 34 * radius))
        draw.polygon(polygon, fill=255)
    return mask.filter(ImageFilter.GaussianBlur(2))


def tiled_art(filename, backing, palette):
    source = Image.open(TERRAIN_ART / filename).convert("RGBA")
    crop = source.crop((250, 250, 1004, 1004))
    flat = Image.new("RGB", crop.size, backing)
    flat.paste(crop, mask=crop.getchannel("A"))
    # A short, mapped ink palette keeps the detailed source art in the same
    # printed-map language as the flat elevation colors and ridge shading.
    gray = ImageOps.grayscale(flat)
    ink = Image.new("RGB", flat.size)
    ink.putdata([palette[min(value // 32, len(palette) - 1)]
                 for value in gray.get_flattened_data()])
    tile = ink.resize((360, 360), Image.Resampling.LANCZOS)
    texture = Image.new("RGB", (2950, 2400))
    for y in range(0, 2400, 360):
        for x in range(0, 2950, 360):
            reflected = ImageOps.mirror(tile) if x // 360 % 2 else tile
            reflected = ImageOps.flip(reflected) if y // 360 % 2 else reflected
            texture.paste(reflected, (x, y))
    return texture


overlay = Image.new("RGBA", (2950, 2400))
for marked, filename, backing, strength, palette in (
    (woods, "woodland_source.png", "#467c4b", .88,
     ((41, 102, 63), (59, 123, 71), (77, 143, 78),
      (103, 165, 91), (132, 183, 107))),
    (marshes, "marsh_source.png", "#628f82", .82,
     ((43, 105, 105), (58, 126, 123), (80, 150, 139),
      (109, 175, 155), (144, 194, 168))),
):
    mask = terrain_mask(marked).point(lambda value: round(value * strength))
    layer = tiled_art(filename, backing, palette).convert("RGBA")
    layer.putalpha(mask)
    overlay = Image.alpha_composite(overlay, layer)

# Place the terrain between the hex base and streams in the rasterization copy.
# Redraw the grid over it so boundaries remain clear. The source SVG and terrain
# flags retain the original marked hexes.
for parent in root.iter():
    for child in list(parent):
        if child.get(XLINK) in ("#trees", "#reeds"):
            parent.remove(child)
for key_id, marked in (("wooded_key", woods), ("marsh_key", marshes)):
    key = next(element for element in root.iter() if element.get("id") == key_id)
    center = hexes[sorted(marked)[0]]
    x, y = round(center["x"]), round(center["y"])
    swatch = overlay.crop((x - 43, y - 43, x + 43, y + 43))
    vignette = Image.new("L", swatch.size)
    ImageDraw.Draw(vignette).ellipse((8, 8, 78, 78), fill=255)
    vignette = vignette.filter(ImageFilter.GaussianBlur(4))
    swatch.putalpha(ImageChops.multiply(swatch.getchannel("A"), vignette))
    ET.SubElement(key, SVG + "image", {
        "x": "17", "y": "20", "width": "66", "height": "66",
        XLINK: png_uri(swatch),
    })
board_children = list(root)
stream_index = next(index for index, child in enumerate(board_children)
                    if child.get("id") == "all_streams")
root.insert(stream_index, ET.Element(SVG + "image", {
    "x": "0", "y": "0", "width": "2950", "height": "2400",
    XLINK: png_uri(overlay),
}))
grid = ET.Element(SVG + "g", {"fill": "none", "stroke": "#444444", "stroke-width": "1"})
affected_grid = {
    h["id"] for h in hexes.values()
    if any(math.hypot(h["x"] - hexes[ident]["x"],
                      h["y"] - hexes[ident]["y"]) < 120
           for ident in woods | marshes)
}
hex_points = (
    (50.24, .25), (100.23, 29.112), (100.23, 86.835),
    (50.24, 115.697), (.25, 86.835), (.25, 29.112),
)
draw_order = {ident: index for index, ident in enumerate(ids)}
used_sides = {ident: set() for ident in affected_grid}


def side_toward(source, target):
    """Return the source hex side whose midpoint faces the target hex."""
    x, y = source["x"] - 50, source["y"] - 58
    points = [(x + dx, y + dy) for dx, dy in hex_points]
    target_midpoint = ((source["x"] + target["x"]) / 2,
                       (source["y"] + target["y"]) / 2)
    index = min(range(6), key=lambda side: math.hypot(
        (points[side][0] + points[(side + 1) % 6][0]) / 2 - target_midpoint[0],
        (points[side][1] + points[(side + 1) % 6][1]) / 2 - target_midpoint[1]))
    return index, points[index], points[(index + 1) % 6]


# Adjacent SVG hexes overlap slightly. Filled hexes hide the earlier outline,
# but drawing whole outline polygons again reveals both parallel borders. Draw
# only the side belonging to the later painted hex so every shared border is
# restored exactly once.
for edge in edges:
    if edge["a"] not in affected_grid and edge["b"] not in affected_grid:
        continue
    a, b = hexes[edge["a"]], hexes[edge["b"]]
    a_side, _, _ = side_toward(a, b)
    b_side, _, _ = side_toward(b, a)
    if a["id"] in affected_grid:
        used_sides[a["id"]].add(a_side)
    if b["id"] in affected_grid:
        used_sides[b["id"]].add(b_side)
    later, other = (a, b) if draw_order[a["id"]] > draw_order[b["id"]] else (b, a)
    _, start, end = side_toward(later, other)
    ET.SubElement(grid, SVG + "line", {
        "x1": f"{start[0]:.3f}", "y1": f"{start[1]:.3f}",
        "x2": f"{end[0]:.3f}", "y2": f"{end[1]:.3f}",
    })

# Restore exposed map-edge sides, which have no neighboring hex entry.
for ident in affected_grid:
    h = hexes[ident]
    x, y = h["x"] - 50, h["y"] - 58
    points = [(x + dx, y + dy) for dx, dy in hex_points]
    for side in set(range(6)) - used_sides[ident]:
        start, end = points[side], points[(side + 1) % 6]
        ET.SubElement(grid, SVG + "line", {
            "x1": f"{start[0]:.3f}", "y1": f"{start[1]:.3f}",
            "x2": f"{end[0]:.3f}", "y2": f"{end[1]:.3f}",
        })
root.insert(stream_index + 1, grid)

# The paper and ridge turbulence filters make rasterization extremely slow on
# this Mac. Keep the much cheaper stream displacement filter so the water has
# the irregular banks specified by the SVG. Add the ridge texture with Pillow
# after rendering. The source SVG remains untouched.
for parent in root.iter():
    for child in list(parent):
        if (re.match(r"^\d{4}_ridge(?:_|$)", child.get("id", "")) or
                child.get("id", "") in ("ridge_top_left", "ridge_top_right")):
            parent.remove(child)
for element in root.iter():
    if element.get("filter") != "url(#stream)":
        element.attrib.pop("filter", None)
with tempfile.TemporaryDirectory() as temporary:
    clean = Path(temporary) / "map.svg"
    ET.ElementTree(root).write(clean, encoding="utf-8", xml_declaration=True)
    subprocess.run(["rsvg-convert", "-w", "5900", str(clean), "-o", str(TEXTURE)], check=True)


RIDGE_SIDES = {
    "upper_left": [((.25, 29.112), (50.24, .25))],
    "top_left": [((.25, 29.112), (50.24, .25))],
    "upper_right": [((50.24, .25), (100.23, 29.112))],
    "top_right": [((50.24, .25), (100.23, 29.112))],
    "top": [((.25, 29.112), (50.24, .25)), ((50.24, .25), (100.23, 29.112))],
    "left": [((.25, 29.112), (.25, 86.835))],
    "right": [((100.23, 29.112), (100.23, 86.835))],
    "lower_left": [((.25, 86.835), (50.24, 115.697))],
    "lower_right": [((50.24, 115.697), (100.23, 86.835))],
}


def ridge_polygon(start, end, inward, depth, rng):
    inner = []
    for step in range(11):
        t = step / 10
        x = start[0] + (end[0] - start[0]) * t
        y = start[1] + (end[1] - start[1]) * t
        jitter = rng.uniform(-2.5, 2.5) if 0 < step < 10 else 0
        inner.append(((x + inward[0] * (depth + jitter)) * 2,
                      (y + inward[1] * (depth + jitter)) * 2))
    return [(start[0] * 2, start[1] * 2),
            (end[0] * 2, end[1] * 2)] + list(reversed(inner))


def apply_paper_finish(image):
    """Give the rendered board the restrained grain of a printed paper map."""
    base = image.convert("RGB")
    width, height = base.size
    rng = random.Random(1066)

    # Take a little of the digital edge off the SVG without making labels or
    # hex boundaries fuzzy. At the final display size this reads as ink on
    # paper instead of perfectly sharp vector artwork.
    softened = base.filter(ImageFilter.GaussianBlur(.65))
    base = Image.blend(base, softened, .14)

    # Combine broad, cloudy variation with a fine tooth. Keeping both masks
    # close to white makes this a subtle multiplicative print texture rather
    # than visible dirt or a repeated pattern.
    coarse_size = (max(2, width // 24), max(2, height // 24))
    coarse = Image.frombytes("L", coarse_size,
                             rng.randbytes(coarse_size[0] * coarse_size[1]))
    coarse = ImageOps.autocontrast(coarse.filter(ImageFilter.GaussianBlur(3.5)))
    coarse = coarse.point(lambda value: 247 + value * 8 // 255)
    coarse = coarse.resize((width, height), Image.Resampling.BICUBIC)

    grain_size = (max(2, width // 4), max(2, height // 4))
    grain = Image.frombytes("L", grain_size,
                            rng.randbytes(grain_size[0] * grain_size[1]))
    grain = grain.filter(ImageFilter.GaussianBlur(.35))
    grain = grain.point(lambda value: 249 + value * 6 // 255)
    grain = grain.resize((width, height), Image.Resampling.BILINEAR)

    paper_value = ImageChops.multiply(coarse, grain)
    red = paper_value.point(lambda value: min(255, value + 6))
    green = paper_value.point(lambda value: min(255, value + 2))
    blue = paper_value.point(lambda value: max(0, value - 3))
    base = ImageChops.multiply(base, Image.merge("RGB", (red, green, blue)))

    # Sparse fibers break up large flat areas. They are intentionally faint;
    # the board should feel tactile without looking stained or distressed.
    fibers = Image.new("RGBA", (width, height))
    draw = ImageDraw.Draw(fibers)
    for _ in range(2600):
        x, y = rng.randrange(width), rng.randrange(height)
        length = rng.randint(16, 72)
        angle = rng.uniform(-math.pi, math.pi)
        end = (x + math.cos(angle) * length, y + math.sin(angle) * length)
        if rng.random() < .72:
            color = (82, 63, 40, rng.randint(5, 11))
        else:
            color = (255, 249, 228, rng.randint(7, 14))
        draw.line((x, y, *end), fill=color, width=rng.choice((1, 1, 2)))
    fibers = fibers.filter(ImageFilter.GaussianBlur(.35))
    return Image.alpha_composite(base.convert("RGBA"), fibers)


base = Image.open(TEXTURE).convert("RGBA")
shade = Image.new("RGBA", base.size)
flecks = Image.new("RGBA", base.size)
shade_draw = ImageDraw.Draw(shade)
fleck_draw = ImageDraw.Draw(flecks)
seen_sides = set()
for ident, ox, oy, side in sorted(ridge_drawings + ridge_key_drawings):
    for start_local, end_local in RIDGE_SIDES.get(side, []):
        numbered_owner = re.match(r"^\d{4}", ident)
        owner = numbered_owner.group(0) if numbered_owner else ident
        side_key = (owner, start_local, end_local)
        if side_key in seen_sides:
            continue
        seen_sides.add(side_key)
        start = (ox + start_local[0], oy + start_local[1])
        end = (ox + end_local[0], oy + end_local[1])
        midpoint = ((start[0] + end[0]) / 2, (start[1] + end[1]) / 2)
        toward_center = (ox + 50.24 - midpoint[0], oy + 57.97 - midpoint[1])
        length = math.hypot(*toward_center)
        inward = (toward_center[0] / length, toward_center[1] / length)
        owner_seed = int(owner) if owner.isdigit() else sum(
            (index + 1) * ord(character) for index, character in enumerate(owner))
        rng = random.Random(owner_seed * 17 + len(seen_sides) * 101)
        for depth, alpha in ((28, 12), (20, 27), (11, 49)):
            shade_draw.polygon(ridge_polygon(start, end, inward, depth, rng),
                               fill=(47, 58, 32, alpha))
        for _ in range(450):
            t = rng.uniform(.035, .965)
            depth = 28 * (rng.random() ** 2)
            x = start[0] + (end[0] - start[0]) * t + inward[0] * depth
            y = start[1] + (end[1] - start[1]) * t + inward[1] * depth
            radius = rng.choice((.55, .8, 1.1, 1.5)) * 2
            alpha = int((1 - depth / 34) * rng.randint(35, 95))
            fleck_draw.ellipse((x * 2 - radius, y * 2 - radius,
                               x * 2 + radius, y * 2 + radius),
                              fill=(42, 57, 34, alpha))

shade = shade.filter(ImageFilter.GaussianBlur(3))
base = Image.alpha_composite(base, shade)
base = Image.alpha_composite(base, flecks)
base = apply_paper_finish(base)
base.convert("RGB").save(TEXTURE, optimize=True)

# The in-game terrain chart uses the same artwork as the board. Crop a clean,
# label-free example of each terrain and mask it to the map's hex outline.
swatch_hexes = {
    "clear": "1312",
    "ridge": "0923",
    "marsh": "1101",
    "stream": "0303",
    "woods": "2311",
    "road": "1508",
}
TERRAIN_SWATCHES.mkdir(parents=True, exist_ok=True)
mask_scale = 4
mask = Image.new("L", (200 * mask_scale, 172 * mask_scale))
mask_draw = ImageDraw.Draw(mask)
mask_draw.polygon([(50 * mask_scale, 0), (150 * mask_scale, 0),
                   (200 * mask_scale - 1, 86 * mask_scale),
                   (150 * mask_scale, 172 * mask_scale - 1),
                   (50 * mask_scale, 172 * mask_scale - 1),
                   (0, 86 * mask_scale)], fill=255)
mask = mask.resize((200, 172), Image.Resampling.LANCZOS)
for name, ident in swatch_hexes.items():
    hex_data = hexes[ident]
    center_x = round(hex_data["x"] * 2)
    center_y = round(hex_data["y"] * 2)
    swatch = base.crop((center_x - 100, center_y - 86,
                        center_x + 100, center_y + 86))
    swatch.putalpha(mask)
    ImageDraw.Draw(swatch).line([(50, 1), (150, 1), (198, 86),
                                  (150, 170), (50, 170), (1, 86), (50, 1)],
                                 fill=(59, 58, 45, 235), width=2, joint="curve")
    swatch.save(TERRAIN_SWATCHES / f"{name}.png", optimize=True)

print(f"Generated {len(hexes)} hexes, {len(edges)}, {TEXTURE.name}, and terrain swatches")

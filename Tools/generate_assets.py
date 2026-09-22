#!/usr/bin/env python3
"""Derive the Unity map from the repository SVG without changing that source.

The SVG uses a duplicate id for 1420. Hexes are keyed by their printed four
digit coordinate, so duplicate drawing elements are collapsed here.
"""

import json
import math
import random
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Images/Misc/hex_map.svg"
DEST = ROOT / "UnityProject/Assets/Resources/Data/Map.json"
TEXTURE = ROOT / "UnityProject/Assets/Resources/Art/Map/hex_map.png"
XLINK = "{http://www.w3.org/1999/xlink}href"
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

# SVG turbulence filters make rasterization extremely slow on this Mac. Render
# a filter-free copy, then add the original board's soft, speckled ridge style
# from the SVG's marked hex sides. The source SVG remains untouched.
for parent in root.iter():
    for child in list(parent):
        if re.match(r"^\d{4}_ridge(?:_|$)", child.get("id", "")):
            parent.remove(child)
for element in root.iter():
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


base = Image.open(TEXTURE).convert("RGBA")
shade = Image.new("RGBA", base.size)
flecks = Image.new("RGBA", base.size)
shade_draw = ImageDraw.Draw(shade)
fleck_draw = ImageDraw.Draw(flecks)
seen_sides = set()
for ident, ox, oy, side in sorted(ridge_drawings):
    for start_local, end_local in RIDGE_SIDES.get(side, []):
        side_key = (ident[:4], start_local, end_local)
        if side_key in seen_sides:
            continue
        seen_sides.add(side_key)
        start = (ox + start_local[0], oy + start_local[1])
        end = (ox + end_local[0], oy + end_local[1])
        midpoint = ((start[0] + end[0]) / 2, (start[1] + end[1]) / 2)
        toward_center = (ox + 50.24 - midpoint[0], oy + 57.97 - midpoint[1])
        length = math.hypot(*toward_center)
        inward = (toward_center[0] / length, toward_center[1] / length)
        rng = random.Random(int(ident[:4]) * 17 + len(seen_sides) * 101)
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
base.convert("RGB").save(TEXTURE, optimize=True)

print(f"Generated {len(hexes)} hexes, {len(edges)} edges, and {TEXTURE.name}")

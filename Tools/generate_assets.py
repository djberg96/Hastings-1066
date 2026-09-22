#!/usr/bin/env python3
"""Derive the Unity map from the repository SVG without changing that source.

The SVG uses a duplicate id for 1420. Hexes are keyed by their printed four
digit coordinate, so duplicate drawing elements are collapsed here.
"""

import json
import math
import re
import subprocess
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

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
ridge_marks = set()
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
    if "_ridge_" in ident:
        ridge_marks.add(ident)
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
                "lower_left": (-50, 86), "lower_right": (50, 86)}
for mark in ridge_marks:
    match = re.match(r"^(\d{4})_ridge_(.*)$", mark)
    if not match or match.group(2) not in side_offsets or match.group(1) not in hexes:
        continue
    a = hexes[match.group(1)]
    dx, dy = side_offsets[match.group(2)]
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

# SVG turbulence filters make rasterization extremely slow on this Mac. A
# derived copy removes only those effects; the source SVG remains untouched.
for element in root.iter():
    element.attrib.pop("filter", None)
with tempfile.TemporaryDirectory() as temporary:
    clean = Path(temporary) / "map.svg"
    ET.ElementTree(root).write(clean, encoding="utf-8", xml_declaration=True)
    subprocess.run(["rsvg-convert", "-w", "5900", str(clean), "-o", str(TEXTURE)], check=True)

print(f"Generated {len(hexes)} hexes, {len(edges)} edges, and {TEXTURE.name}")

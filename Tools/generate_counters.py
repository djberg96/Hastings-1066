#!/usr/bin/env python3
"""Create restrained printed-cardboard unit art without changing the sources."""

import hashlib
import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Images/Counters"
DESTINATION = ROOT / "UnityProject/Assets/Resources/Art/Counters"


def style_counter(path):
    source = Image.open(path).convert("RGBA")
    alpha = source.getchannel("A")
    image = source.convert("RGB")

    # Replace flat screen primaries with the quieter ink colors of a printed
    # counter while retaining the strong faction distinctions and black type.
    image = ImageEnhance.Color(image).enhance(.82)
    image = ImageEnhance.Brightness(image).enhance(.96)
    image = ImageEnhance.Contrast(image).enhance(.97)

    width, height = image.size
    digest = hashlib.sha256(str(path.relative_to(SOURCE)).encode()).digest()
    rng = random.Random(int.from_bytes(digest[:8], "big"))

    # Broad stock variation and fine tooth are kept close to white so they
    # read as cardboard at game scale rather than dirt or damage.
    coarse_size = (max(2, width // 18), max(2, height // 18))
    coarse = Image.frombytes("L", coarse_size,
                             rng.randbytes(coarse_size[0] * coarse_size[1]))
    coarse = ImageOps.autocontrast(coarse.filter(ImageFilter.GaussianBlur(1.7)))
    coarse = coarse.point(lambda value: 250 + value * 5 // 255)
    coarse = coarse.resize((width, height), Image.Resampling.BICUBIC)

    tooth_size = (max(2, width // 3), max(2, height // 3))
    tooth = Image.frombytes("L", tooth_size,
                            rng.randbytes(tooth_size[0] * tooth_size[1]))
    tooth = tooth.filter(ImageFilter.GaussianBlur(.25))
    tooth = tooth.point(lambda value: 249 + value * 6 // 255)
    tooth = tooth.resize((width, height), Image.Resampling.BILINEAR)
    paper = ImageChops.multiply(coarse, tooth)
    image = ImageChops.multiply(image, Image.merge("RGB", (paper, paper, paper)))

    fibers = Image.new("RGBA", (width, height))
    draw = ImageDraw.Draw(fibers)
    for _ in range(120):
        x, y = rng.randrange(width), rng.randrange(height)
        length = rng.randint(12, 55)
        angle = rng.uniform(-math.pi, math.pi)
        end = (x + math.cos(angle) * length, y + math.sin(angle) * length)
        if rng.random() < .5:
            color = (245, 231, 196, rng.randint(3, 9))
        else:
            color = (39, 30, 22, rng.randint(2, 7))
        draw.line((x, y, *end), fill=color, width=1)
    image = Image.alpha_composite(image.convert("RGBA"),
                                  fibers.filter(ImageFilter.GaussianBlur(.2)))
    image = ImageEnhance.Sharpness(image.convert("RGB")).enhance(.88)
    image.putalpha(alpha)
    return image


count = 0
for nation in ("Normans", "Saxons"):
    for source_path in sorted((SOURCE / nation).glob("*.png")):
        destination = DESTINATION / nation / source_path.name
        destination.parent.mkdir(parents=True, exist_ok=True)
        style_counter(source_path).save(destination, optimize=True)
        count += 1

print(f"Generated {count} printed-cardboard unit counters")

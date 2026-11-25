"""Extract one frame per clip from the encoded MP4s and build the keyed 2x2 composite preview."""
import os
import subprocess

import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
CLIPS = os.path.join(ROOT, "Assets", "StreamingAssets", "Holograms")
PREV = os.path.join(HERE, "previews")
DOCS = os.path.join(ROOT, "Docs")
SHOTS = {"holo_globe": 2.0, "holo_logo": 5.0, "holo_reactor": 1.2, "holo_radar": 2.0}


def wall(w, h, seed=3):
    rng = np.random.default_rng(seed)
    n = rng.normal(0, 1, (h, w)).astype(np.float32)
    img = Image.fromarray(np.clip(n * 40 + 128, 0, 255).astype(np.uint8))
    soft = np.asarray(img.filter(ImageFilter.GaussianBlur(1.2)), np.float32) - 128
    coarse = np.asarray(Image.fromarray(np.clip(rng.normal(128, 40, (h // 16 + 2, w // 16 + 2)), 0, 255).astype(np.uint8))
                        .resize((w, h), Image.BICUBIC), np.float32) - 128
    base = 34 + soft * 0.10 + coarse * 0.10
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    vig = 1 - 0.45 * (((xx - w / 2) / w) ** 2 + ((yy - h / 2) / h) ** 2) * 2
    # faint panel seams
    seam = (((yy % 90) < 1.2) | ((xx % 160) < 1.2)).astype(np.float32) * -6
    g = np.clip((base + seam) * vig, 0, 255)
    return np.stack([g * 0.92, g * 0.97, g * 1.12], axis=-1)


def key(rgb, bg):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    spill = g - np.maximum(r, b)
    a = 1 - np.clip((spill - 30) / 160.0, 0, 1)
    gd = np.minimum(g, np.maximum(r, b) + 20)
    fg = np.stack([r, gd, b], axis=-1)
    return fg * a[..., None] + bg * (1 - a[..., None]), a


def main():
    os.makedirs(PREV, exist_ok=True)
    os.makedirs(DOCS, exist_ok=True)
    comp = Image.new("RGB", (1280, 720))
    for idx, (name, ts) in enumerate(SHOTS.items()):
        out = os.path.join(PREV, name + ".png")
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-ss", str(ts), "-i", os.path.join(CLIPS, name + ".mp4"),
                        "-frames:v", "1", out], check=True)
        fr = np.asarray(Image.open(out).convert("RGB"), np.float32)
        keyed, _ = key(fr, wall(1280, 720, seed=idx + 3))
        tile = Image.fromarray(np.clip(keyed, 0, 255).astype(np.uint8)).resize((640, 360), Image.LANCZOS)
        comp.paste(tile, ((idx % 2) * 640, (idx // 2) * 360))
    comp.save(os.path.join(DOCS, "holograms-preview.png"))
    print("ok")


if __name__ == "__main__":
    main()

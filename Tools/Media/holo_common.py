"""Shared helpers for generating HoloWall green-screen hologram clips.

All content is procedurally drawn (Pillow + numpy) and piped to ffmpeg.
Fonts: Audiowide / Rajdhani (SIL OFL, see fonts/).
"""
import math
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H, SS, FPS = 1280, 720, 2, 30
HERE = os.path.dirname(os.path.abspath(__file__))
FONT_DIR = os.path.join(HERE, "fonts")
OUT_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "StreamingAssets", "Holograms"))
PREV_DIR = os.path.join(HERE, "previews")

# Content palette (no green)
CYAN = (40, 200, 255)
BLUE = (70, 110, 255)
WHITE = (235, 245, 255)
MAGENTA = (255, 60, 220)
VIOLET = (150, 90, 255)

TAU = math.tau


def font(name, size):
    return ImageFont.truetype(os.path.join(FONT_DIR, name), int(size * SS))


def scale(c, k):
    return tuple(int(max(0, min(255, v * k))) for v in c)


def new_canvas():
    img = Image.new("RGB", (W * SS, H * SS), (0, 0, 0))
    return img, ImageDraw.Draw(img)


def P(x, y):
    """Logical (1280x720) coordinates -> supersampled canvas."""
    return (x * SS, y * SS)


def line(d, pts, color, width=2.0):
    d.line([P(*p) for p in pts], fill=color, width=max(1, int(round(width * SS))), joint="curve")


def circle(d, cx, cy, r, color, width=2.0):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], outline=color, width=max(1, int(round(width * SS))))


def disc(d, cx, cy, r, color):
    d.ellipse([P(cx - r, cy - r), P(cx + r, cy + r)], fill=color)


def arc(d, cx, cy, r, a0, a1, color, width=2.0):
    """Angles in radians, 0 = +x, increasing clockwise on screen."""
    d.arc([P(cx - r, cy - r), P(cx + r, cy + r)], math.degrees(a0), math.degrees(a1),
          fill=color, width=max(1, int(round(width * SS))))


def finish(img, glow=1.0, g1=3, g2=12, cutoff=0.06):
    """Supersampled black-background glow canvas -> keyed uint8 HxWx3 frame on pure green."""
    small = img.resize((W, H), Image.LANCZOS)
    base = np.asarray(small, dtype=np.float32)
    b1 = np.asarray(small.filter(ImageFilter.GaussianBlur(g1)), dtype=np.float32)
    b2 = np.asarray(small.filter(ImageFilter.GaussianBlur(g2)), dtype=np.float32)
    rgb = np.clip(base + glow * (0.9 * b1 + 0.8 * b2), 0, 255) / 255.0
    m = rgb.max(axis=2)
    alpha = np.clip((m - cutoff) / 0.35, 0, 1)
    alpha = alpha * alpha * (3 - 2 * alpha)  # smoothstep -> tight halo
    full = rgb / np.maximum(m, 1e-4)[..., None]  # full-brightness hue
    col = full * np.clip(0.35 + 0.65 * m, 0, 1)[..., None]
    green = np.array([0.0, 1.0, 0.0], dtype=np.float32)
    out = col * alpha[..., None] + green * (1 - alpha[..., None])
    out = np.clip(out * 255 + 0.5, 0, 255).astype(np.uint8)
    # force exact green where nothing is drawn
    out[alpha <= 0.0] = (0, 255, 0)
    return out


def run(name, nframes, render, argv=None):
    """render(i, n) -> (PIL supersampled canvas, glow kwargs dict). Pipes to ffmpeg.
    --frame K writes only a preview PNG of frame K."""
    argv = sys.argv[1:] if argv is None else argv
    if "--frame" in argv:
        k = int(argv[argv.index("--frame") + 1])
        img, kw = render(k, nframes)
        os.makedirs(PREV_DIR, exist_ok=True)
        p = os.path.join(PREV_DIR, f"{name}_frame.png")
        Image.fromarray(finish(img, **kw)).save(p)
        print("wrote", p)
        return
    os.makedirs(OUT_DIR, exist_ok=True)
    out = os.path.join(OUT_DIR, name + ".mp4")
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24",
           "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-", "-an",
           "-c:v", "libx264", "-preset", "slow", "-crf", "20", "-pix_fmt", "yuv420p",
           "-vf", "scale=out_color_matrix=bt709:out_range=tv", "-colorspace", "bt709",
           "-color_primaries", "bt709", "-color_trc", "bt709",
           "-movflags", "+faststart", out]
    pr = subprocess.Popen(cmd, stdin=subprocess.PIPE)
    for i in range(nframes):
        img, kw = render(i, nframes)
        pr.stdin.write(finish(img, **kw).tobytes())
    pr.stdin.close()
    pr.wait()
    print("wrote", out, os.path.getsize(out))


# ---- 3D helpers -------------------------------------------------------------
def rot_y(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_x(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_z(a):
    c, s = math.cos(a), math.sin(a)
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


def smooth(x):
    x = min(1.0, max(0.0, x))
    return x * x * (3 - 2 * x)

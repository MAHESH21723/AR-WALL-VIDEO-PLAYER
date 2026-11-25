import math
import numpy as np
from holo_common import *

N = 300  # 10 s
TITLE = "HoloWall"
SUB = "Made by Maheshwaran"
F_T = font("Audiowide-Regular.ttf", 168)
F_S = font("Rajdhani-Bold.ttf", 46)
F_M = font("Rajdhani-Bold.ttf", 22)


def text_center(d, cx, cy, s, f, fill, spacing=0):
    if spacing:
        widths = [d.textlength(ch, font=f) for ch in s]
        total = sum(widths) + spacing * SS * (len(s) - 1)
        x = cx * SS - total / 2
        for ch, w in zip(s, widths):
            d.text((x, cy * SS), ch, font=f, fill=fill, anchor="lm")
            x += w + spacing * SS
    else:
        d.text((cx * SS, cy * SS), s, font=f, fill=fill, anchor="mm")


def bracket(d, x0, y0, x1, y1, L, color, w=3):
    for (x, y, dx, dy) in [(x0, y0, 1, 1), (x1, y0, -1, 1), (x0, y1, 1, -1), (x1, y1, -1, -1)]:
        line(d, [(x + dx * L, y), (x, y), (x, y + dy * L)], color, w)


def play_icon(d, cx, cy, s, k):
    """small holo frame with play triangle"""
    x0, y0, x1, y1 = cx - 1.5 * s, cy - s, cx + 1.5 * s, cy + s
    bracket(d, x0, y0, x1, y1, s * 0.55, scale(CYAN, k), 3.5)
    tri = [(cx - 0.38 * s, cy - 0.5 * s), (cx - 0.38 * s, cy + 0.5 * s), (cx + 0.62 * s, cy)]
    d.polygon([P(*p) for p in tri], fill=scale(WHITE, k))
    line(d, tri + [tri[0]], scale(MAGENTA, k), 2.5)


def render(i, n):
    t = i / n
    sec = i / FPS
    rng = np.random.default_rng(i * 13 + 5)
    # global visibility envelope: 0 at start & end for seamless loop
    reveal = smooth((sec - 0.4) / 2.2)           # 0.4 -> 2.6 s
    fade = 1 - smooth((sec - 8.9) / 1.0)         # 8.9 -> 9.9 s dissolve
    out_wipe = 1.0
    vis = reveal * fade

    img, d = new_canvas()
    # content on a separate layer (so we can glitch it)
    cy_t = 330
    text_center(d, 640, cy_t, TITLE, F_T, CYAN)
    text_center(d, 640, cy_t, TITLE, F_T, CYAN)
    # inner white core: draw thinner highlight by re-drawing slightly smaller alpha-free
    text_center(d, 640, cy_t - 1, TITLE, F_T, (110, 225, 255))
    text_center(d, 640, 470, SUB.upper(), F_S, scale(WHITE, 0.95), spacing=9)
    # decorations
    pulse = 0.75 + 0.25 * math.sin(t * TAU * 3)
    line(d, [(250, 420), (1030, 420)], scale(BLUE, 1.0), 2)
    line(d, [(250, 420), (250 + 780 * smooth((sec - 1.4) / 1.8), 420)], scale(MAGENTA, 1.0), 3)
    bracket(d, 180, 190, 1100, 530, 60, scale(CYAN, 0.9 * pulse), 3)
    play_icon(d, 640, 150, 34, 1.0)
    text_center(d, 640, 565, "HOLOGRAPHIC AR VIDEO PLAYER", F_M, scale(BLUE, 1.0), spacing=6)
    for k in range(12):
        x = 180 + k * 76
        line(d, [(x, 195 if False else 520), (x, 530)], scale(CYAN, 0.35), 1.5)

    arr = np.asarray(img).astype(np.float32)
    h2, w2 = arr.shape[:2]

    # reveal wipe left -> right with bright leading edge
    wipe_x = int(w2 * smooth((sec - 0.4) / 2.0) * 1.0)
    mask = np.zeros((h2, w2), np.float32)
    if sec < 2.6:
        mask[:, :wipe_x] = 1.0
        edge = np.zeros_like(arr)
        e0 = max(0, wipe_x - 6)
        edge[:, e0:wipe_x] = np.array(WHITE, np.float32) * 0.9
        edge[::SS * 3] *= 0.3
    else:
        mask[:] = 1.0
        edge = 0
    arr = arr * mask[..., None] + (edge if sec < 2.6 else 0)

    # scanlines (rows) + slow roll band
    rows = np.arange(h2)
    scan = 0.80 + 0.20 * ((rows // (SS * 2)) % 2 == 0)
    band_y = (t * 2 % 1.0) * h2
    band = 1.0 + 0.55 * np.exp(-(((rows - band_y) / (h2 * 0.04)) ** 2))
    arr *= (scan * band)[:, None, None]

    # glitch: strongest during reveal & exit, sparse bursts while holding
    g = 0.0
    if sec < 3.0:
        g = 1.0 - smooth((sec - 0.6) / 2.4)
    elif sec > 8.4:
        g = smooth((sec - 8.4) / 1.4)
    for bs in (4.2, 6.1, 7.3):  # short hold bursts
        if 0 <= sec - bs < 0.25:
            g = max(g, 0.7)
    if g > 0.02:
        for _ in range(int(2 + 10 * g)):
            y0 = int(rng.uniform(150, 580)) * SS
            hh = int(rng.uniform(4, 40)) * SS
            dx = int(rng.normal(0, 60 * g)) * SS
            arr[y0:y0 + hh] = np.roll(arr[y0:y0 + hh], dx, axis=1)
        # chromatic split
        sh = int(8 * g) * SS
        if sh:
            r = np.roll(arr[..., 0], sh, axis=1)
            b = np.roll(arr[..., 2], -sh, axis=1)
            arr[..., 0] = np.maximum(arr[..., 0] * 0.5, r)
            arr[..., 2] = np.maximum(arr[..., 2], b)
        # flicker
        arr *= (0.75 + 0.25 * rng.random())

    arr *= fade
    # slight overall flicker while holding
    arr *= 0.96 + 0.04 * math.sin(t * TAU * 12)
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    return img, dict(glow=1.0, g1=3, g2=10)


if __name__ == "__main__":
    run("holo_logo", N, render)




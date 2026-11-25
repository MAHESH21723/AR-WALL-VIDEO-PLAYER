"""HoloWall app icon, adaptive layers, logo, store art. Pure Pillow + numpy."""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
ICON_DIR = os.path.join(ROOT, "Assets", "HoloWall", "Icon")
DOCS = os.path.join(ROOT, "Docs")
os.makedirs(ICON_DIR, exist_ok=True)
os.makedirs(DOCS, exist_ok=True)

CYAN = np.array([30, 215, 255], np.float32)
VIOLET = np.array([160, 80, 255], np.float32)
MAGENTA = np.array([255, 70, 215], np.float32)
NAVY0 = np.array([14, 20, 58], np.float32)
NAVY1 = np.array([4, 6, 22], np.float32)


def fnt(name, size):
    return ImageFont.truetype(os.path.join(HERE, "fonts", name), int(size))


def background(w, h, grid=True):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = w * 0.5, h * 0.45
    r = np.sqrt(((xx - cx) / w) ** 2 + ((yy - cy) / h) ** 2) / 0.75
    t = np.clip(r, 0, 1)[..., None]
    bg = NAVY0 * (1 - t) + NAVY1 * t
    # soft violet bloom lower right, cyan upper left
    bg += (np.exp(-(((xx - w * 0.2) / w) ** 2 + ((yy - h * 0.15) / h) ** 2) / 0.05)[..., None]) * np.array([0, 22, 40], np.float32)
    bg += (np.exp(-(((xx - w * 0.85) / w) ** 2 + ((yy - h * 0.95) / h) ** 2) / 0.05)[..., None]) * np.array([36, 8, 52], np.float32)
    if grid:
        step = max(24, w // 20)
        g = ((xx % step) < 1.3) | ((yy % step) < 1.3)
        bg += g[..., None] * np.array([6, 14, 30], np.float32) * (1 - t * 0.7)
    return np.clip(bg, 0, 255)


def grad_map(w, h, c0=CYAN, c1=VIOLET, angle=0.8, x0=0, y0=0, gw=None, gh=None):
    gw = gw or w
    gh = gh or h
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    u = ((xx - x0) / gw) * math.cos(angle) + ((yy - y0) / gh) * math.sin(angle)
    u = np.clip((u - u.min()) / (np.ptp(u) + 1e-6), 0, 1)[..., None] if False else np.clip(u / (math.cos(angle) + math.sin(angle)), 0, 1)[..., None]
    return c0 * (1 - u) + c1 * u


def blur(arr, r):
    ch = [Image.fromarray(np.clip(arr[..., k], 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(r)) for k in range(3)]
    return np.stack([np.asarray(c, np.float32) for c in ch], axis=-1)


def glyph_light(size, frame_w, cx=None, cy=None):
    """Additive 'light' RGB float array (size x size x 3) of the holo frame + play mark, with glow.
    frame_w = width of the frame in px of the final size."""
    SS = 2
    S = size * SS
    cx = (size / 2 if cx is None else cx) * SS
    cy = (size / 2 if cy is None else cy) * SS
    fw = frame_w * SS
    # slightly perspective-skewed frame: left edge taller than right (projected on a wall)
    hl, hr = fw * 0.34, fw * 0.28
    quad = [(cx - fw / 2, cy - hl), (cx + fw / 2, cy - hr), (cx + fw / 2, cy + hr), (cx - fw / 2, cy + hl)]

    def mask_of(draw_fn):
        m = Image.new("L", (S, S), 0)
        draw_fn(ImageDraw.Draw(m))
        return np.asarray(m.resize((size, size), Image.LANCZOS), np.float32) / 255.0

    lw = max(2, int(fw * 0.030))

    def outline(d):
        d.line(quad + [quad[0], quad[1]], fill=255, width=lw, joint="curve")
        for p in quad:
            d.ellipse([p[0] - lw / 2, p[1] - lw / 2, p[0] + lw / 2, p[1] + lw / 2], fill=255)

    def inner_outline(d):
        k = 0.82
        q2 = [(cx + (p[0] - cx) * k, cy + (p[1] - cy) * k) for p in quad]
        d.line(q2 + [q2[0], q2[1]], fill=255, width=max(1, lw // 3), joint="curve")

    def fill_quad(d):
        d.polygon(quad, fill=255)

    def corners(d):
        L = fw * 0.12
        for i, p in enumerate(quad):
            n = quad[(i + 1) % 4]
            pv = quad[i - 1]
            for q in (n, pv):
                v = np.array([q[0] - p[0], q[1] - p[1]])
                v = v / np.linalg.norm(v) * L
                d.line([p, (p[0] + v[0], p[1] + v[1])], fill=255, width=int(lw * 1.35))
            d.ellipse([p[0] - lw * 0.68, p[1] - lw * 0.68, p[0] + lw * 0.68, p[1] + lw * 0.68], fill=255)

    ts = fw * 0.175
    tcx = cx + fw * 0.012
    tri = [(tcx - ts * 0.55, cy - ts * 0.85), (tcx - ts * 0.55, cy + ts * 0.85), (tcx + ts * 0.95, cy)]

    def play(d):
        d.polygon(tri, fill=255)
        d.line(tri + [tri[0]], fill=255, width=int(lw * 0.9), joint="curve")

    def scan(d):
        y = cy - hl * 0.8
        step = fw * 0.045
        while y < cy + hl * 0.8:
            d.line([(cx - fw * 0.44, y), (cx + fw * 0.44, y)], fill=255, width=max(1, int(lw * 0.18)))
            y += step

    m_out = mask_of(outline)
    m_in = mask_of(inner_outline)
    m_fill = mask_of(fill_quad)
    m_cor = mask_of(corners)
    m_play = mask_of(play)
    m_scan = mask_of(scan) * m_fill

    g = grad_map(size, size, CYAN, VIOLET, angle=0.9)
    gp = grad_map(size, size, np.array([210, 245, 255], np.float32), MAGENTA, angle=1.2,
                  x0=0, y0=0)
    light = np.zeros((size, size, 3), np.float32)
    light += g * (m_fill * 0.10)[..., None]
    light += g * (m_scan * 0.12)[..., None]
    light += g * (m_in * 0.45)[..., None]
    light += g * (m_out * 0.85)[..., None]
    light += np.array([220, 250, 255], np.float32) * (m_cor * 0.5)[..., None] * 0.5 + g * (m_cor * 0.45)[..., None]
    light += gp * (m_play * 0.95)[..., None]
    # beam / floor reflection beneath the frame: faint projector fan
    base = light.copy()
    glow = blur(base, size * 0.012) * 1.0 + blur(base, size * 0.035) * 1.1 + blur(base, size * 0.08) * 0.9
    return np.clip(base + glow, 0, 255)


def projector_beam(size, cx, cy_top, cy_bottom, w_top, w_bottom):
    """faint light cone (trapezoid) as additive RGB"""
    SS = 2
    m = Image.new("L", (size * SS, size * SS), 0)
    d = ImageDraw.Draw(m)
    d.polygon([((cx - w_top / 2) * SS, cy_top * SS), ((cx + w_top / 2) * SS, cy_top * SS),
               ((cx + w_bottom / 2) * SS, cy_bottom * SS), ((cx - w_bottom / 2) * SS, cy_bottom * SS)], fill=255)
    a = np.asarray(m.resize((size, size), Image.LANCZOS).filter(ImageFilter.GaussianBlur(size * 0.012)), np.float32) / 255
    yy = np.linspace(0, 1, size)[:, None]
    fade = np.clip((yy * size - cy_top) / max(1, (cy_bottom - cy_top)), 0, 1)
    a = a * (1 - fade) ** 1.2
    return a[..., None] * np.array([30, 120, 200], np.float32) * 0.28


def compose_opaque(bg, light):
    # screen-ish additive
    return np.clip(bg + light, 0, 255).astype(np.uint8)


def to_rgba(light, floor=2.0):
    m = light.max(axis=2) / 255.0
    a = np.clip((m - floor / 255.0) * 1.0, 0, 1)
    col = light / np.maximum(m, 1e-4)[..., None]
    col = np.clip(col, 0, 255)
    rgba = np.concatenate([np.clip(col, 0, 255), (a * 255)[..., None]], axis=-1)
    return Image.fromarray(np.clip(rgba + 0.5, 0, 255).astype(np.uint8), "RGBA")


def gradient_text(text, f, w, h, pos, anchor="lm", xr=(430, 930), c1=CYAN, c2=VIOLET, spacing=0):
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    if spacing:
        widths = [d.textlength(ch, font=f) for ch in text]
        x = pos[0]
        for ch, wd in zip(text, widths):
            d.text((x, pos[1]), ch, font=f, fill=255, anchor=anchor)
            x += wd + spacing
    else:
        d.text(pos, text, font=f, fill=255, anchor=anchor)
    a = np.asarray(m, np.float32) / 255
    xx = np.linspace(0, 1, w)[None, :, None]
    u = np.clip((xx * w - xr[0]) / (xr[1] - xr[0]), 0, 1)
    col = np.broadcast_to(c1 * (1 - u) + c2 * u, (h, w, 3))
    return col * a[..., None], a


def main():
    N = 1024
    # --- AppIcon (opaque)
    bg = background(N, N)
    beam = projector_beam(N, N / 2, N * 0.72, N * 1.0, N * 0.30, N * 0.80)
    light = glyph_light(N, 640, cy=N * 0.47)
    icon = compose_opaque(bg + beam, light)
    Image.fromarray(icon, "RGB").save(os.path.join(ICON_DIR, "AppIcon.png"))
    # --- Adaptive background
    abg = background(N, N, grid=True)
    Image.fromarray(abg.astype(np.uint8), "RGB").save(os.path.join(ICON_DIR, "AdaptiveBackground.png"))
    # --- Adaptive foreground: content inside central ~60% (614 px); glow included
    lf = glyph_light(N, 440, cy=N * 0.5)
    to_rgba(lf).save(os.path.join(ICON_DIR, "AdaptiveForeground.png"))
    # --- Logo (transparent)
    ll = glyph_light(N, 660, cy=N * 0.5)
    to_rgba(ll).save(os.path.join(ICON_DIR, "Logo.png"))
    # --- Docs icon 512
    Image.fromarray(icon, "RGB").resize((512, 512), Image.LANCZOS).save(os.path.join(DOCS, "icon-512.png"))

    # --- Feature graphic 1024x500
    FW, FH = 1024, 500
    fbg = background(FW, FH)
    # wall: perspective-less brick-ish panel lines
    glyph_size = 400
    gl = glyph_light(glyph_size, 310, cy=glyph_size * 0.5)
    canvas = fbg.copy()
    ox, oy = 20, (FH - glyph_size) // 2
    canvas[oy:oy + glyph_size, ox:ox + glyph_size] = np.clip(canvas[oy:oy + glyph_size, ox:ox + glyph_size] + gl, 0, 255)
    # title
    ft = fnt("Audiowide-Regular.ttf", 92)
    fs = fnt("Rajdhani-Bold.ttf", 31)
    fb = fnt("Rajdhani-Bold.ttf", 27)
    tcol, ta = gradient_text("HoloWall", ft, FW, FH, (430, 190), "lm")
    tl = tcol.copy()
    glow = blur(tl, 5) * 0.9 + blur(tl, 16) * 0.9
    canvas = np.clip(canvas + glow + tl * 0.9, 0, 255)
    # white highlight overlay on text (inner)
    sub_col, _ = gradient_text("AR HOLOGRAM VIDEO PLAYER", fs, FW, FH, (434, 275), "lm", xr=(434, 900),
                               c1=np.array([210, 240, 255], np.float32), c2=np.array([230, 200, 255], np.float32), spacing=4)
    canvas = np.clip(canvas + sub_col * 0.95 + blur(sub_col, 6) * 0.5, 0, 255)
    by_col, _ = gradient_text("by Maheshwaran", fb, FW, FH, (436, 330), "lm", c1=VIOLET, c2=MAGENTA, spacing=3)
    canvas = np.clip(canvas + by_col * 1.0 + blur(by_col, 5) * 0.5, 0, 255)
    # accent line
    img = Image.fromarray(canvas.astype(np.uint8), "RGB")
    d = ImageDraw.Draw(img)
    d.line([(436, 238), (930, 238)], fill=(150, 90, 255), width=2)
    d.line([(436, 238), (570, 238)], fill=(255, 70, 215), width=3)
    img.save(os.path.join(DOCS, "feature-1024x500.png"))
    print("done")


if __name__ == "__main__":
    main()





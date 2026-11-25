import math
import numpy as np
from holo_common import *

N = 300  # 10 s
CX, CY, R = 640, 360, 270
F_S = font("Rajdhani-Bold.ttf", 22)
F_L = font("Rajdhani-Bold.ttf", 30)
rng = np.random.default_rng(99)
# blips: (angle, radius fraction, drift cycles) ; static positions with tiny orbit wobble
BLIPS = [(rng.uniform(0, TAU), rng.uniform(0.2, 0.95), rng.uniform(0, TAU)) for _ in range(9)]
SWEEPS = 2  # sweeps per loop


def txt(d, x, y, s, f, col, anchor="lm"):
    d.text((x * SS, y * SS), s, font=f, fill=col, anchor=anchor)


def render(i, n):
    t = i / n
    img, d = new_canvas()
    sweep_a = t * TAU * SWEEPS - math.pi / 2
    # range rings
    for k in range(1, 5):
        circle(d, CX, CY, R * k / 4, scale(BLUE, 0.9 if k < 4 else 1.0), 1.6 if k < 4 else 3)
    circle(d, CX, CY, R + 22, scale(CYAN, 0.8), 1.5)
    # ticks
    for k in range(180):
        a = k / 180 * TAU
        L = 16 if k % 15 == 0 else (10 if k % 5 == 0 else 5)
        line(d, [(CX + math.cos(a) * (R + 22), CY + math.sin(a) * (R + 22)),
                 (CX + math.cos(a) * (R + 22 + L), CY + math.sin(a) * (R + 22 + L))], scale(CYAN, 0.85), 1.4)
    # crosshair + diagonals
    for a in (0, math.pi / 2):
        line(d, [(CX - math.cos(a) * R, CY - math.sin(a) * R), (CX + math.cos(a) * R, CY + math.sin(a) * R)],
             scale(BLUE, 0.9), 1.4)
    for a in (math.pi / 4, 3 * math.pi / 4):
        line(d, [(CX - math.cos(a) * R, CY - math.sin(a) * R), (CX + math.cos(a) * R, CY + math.sin(a) * R)],
             scale(BLUE, 0.5), 1.0)
    # sweep trail (stack of wedges fading)
    trail = 0.9
    steps = 46
    for k in range(steps):
        u = k / steps
        a0 = sweep_a - trail * (u + 1 / steps)
        a1 = sweep_a - trail * u
        col = scale(CYAN, (1 - u) ** 1.7 * 0.55)
        d.pieslice([P(CX - R, CY - R), P(CX + R, CY + R)], math.degrees(a0), math.degrees(a1) + 0.4, fill=col)
    line(d, [(CX, CY), (CX + math.cos(sweep_a) * R, CY + math.sin(sweep_a) * R)], scale(WHITE, 1.0), 3)
    # blips: light up when the sweep passes, then fade
    for idx, (ang, rf, ph) in enumerate(BLIPS):
        a = ang + 0.12 * math.sin(t * TAU + ph)
        x, y = CX + math.cos(a) * R * rf, CY + math.sin(a) * R * rf
        diff = (sweep_a - a) % TAU
        inten = max(0.0, 1 - diff / (TAU / SWEEPS * 0.9)) if diff < TAU else 0
        inten = (1 - diff / TAU * SWEEPS) if (diff * SWEEPS) < TAU else 0.0
        inten = max(0.0, inten)
        col = MAGENTA if idx % 3 == 0 else WHITE
        if inten > 0.02:
            disc(d, x, y, 4 + 3 * inten, scale(col, 0.3 + 0.7 * inten))
            circle(d, x, y, 9 + 10 * (1 - inten), scale(col, inten), 1.6)
        else:
            disc(d, x, y, 2.2, scale(col, 0.25))
    disc(d, CX, CY, 6, WHITE)
    # HUD brackets & readouts
    for (x0, y0, dx, dy) in [(60, 60, 1, 1), (1220, 60, -1, 1), (60, 660, 1, -1), (1220, 660, -1, -1)]:
        line(d, [(x0 + dx * 50, y0), (x0, y0), (x0, y0 + dy * 50)], scale(CYAN, 0.9), 3)
    # left readouts
    bearing = (math.degrees(sweep_a + math.pi / 2)) % 360
    txt(d, 90, 120, "RADAR // SCAN", F_L, scale(WHITE, 0.95))
    txt(d, 90, 160, f"BEARING  {bearing:05.1f}", F_S, scale(CYAN, 1.0))
    txt(d, 90, 190, f"RANGE    {int(R / 1.0)} M", F_S, scale(CYAN, 1.0))
    txt(d, 90, 220, f"TARGETS  {len(BLIPS):02d}", F_S, scale(MAGENTA, 1.0))
    # bars left bottom (animated, periodic)
    for k in range(10):
        hgt = 14 + 52 * (0.5 + 0.5 * math.sin(t * TAU * 2 + k * 0.7))
        d.rectangle([P(90 + k * 18, 600 - hgt), P(90 + k * 18 + 10, 600)], fill=scale(BLUE, 1.0))
    txt(d, 90, 625, "SIGNAL", F_S, scale(CYAN, 0.9))
    # right readouts
    txt(d, 1190, 120, "SECTOR 07", F_L, scale(WHITE, 0.95), "rm")
    txt(d, 1190, 160, f"SWEEP {int(t * SWEEPS) + 1}/{SWEEPS}", F_S, scale(CYAN, 1.0), "rm")
    txt(d, 1190, 190, "LOCK  " + ("ACQ" if int(t * 12) % 2 == 0 else "TRK"), F_S, scale(MAGENTA, 1.0), "rm")
    # right sine trace (periodic)
    pts = []
    for k in range(60):
        x = 980 + k * 3.5
        y = 560 + 26 * math.sin(k / 59 * TAU * 2 + t * TAU * 2) * (0.6 + 0.4 * math.sin(k / 59 * math.pi))
        pts.append((x, y))
    line(d, pts, scale(CYAN, 1.0), 2.4)
    line(d, [(980, 600), (1190, 600)], scale(BLUE, 0.9), 1.5)
    line(d, [(980, 520), (1190, 520)], scale(BLUE, 0.5), 1.0)
    return img, dict(glow=1.0, g1=3, g2=11)


if __name__ == "__main__":
    run("holo_radar", N, render)

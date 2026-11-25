import math
import numpy as np
from holo_common import *

N = 300  # 10 s
CX, CY = 640, 360
rng = np.random.default_rng(21)
# particles: each has integer number of orbits / radial cycles per loop
PARTS = [(rng.uniform(0, 1), rng.uniform(100, 300), int(rng.integers(1, 4)) * (1 if rng.random() > .5 else -1),
          rng.uniform(1.5, 3.5), int(rng.integers(1, 3))) for _ in range(90)]


def ring(d, r, t, segs, gap, rot_cycles, color, width, phase=0.0):
    """segmented ring; rot_cycles = integer turns per loop (can be negative)."""
    off = phase + rot_cycles * t * TAU / 1.0
    step = TAU / segs
    for k in range(segs):
        a0 = off + k * step
        arc(d, CX, CY, r, a0, a0 + step * (1 - gap), color, width)


def render(i, n):
    t = i / n
    img, d = new_canvas()
    beat = 0.5 + 0.5 * math.sin(t * TAU * 3)   # 3 pulses per loop
    beat2 = beat ** 2
    # outer tick ring (rotates 1/6 of a turn -> tick pattern of 60 has period 1/60; use whole ticks)
    for k in range(120):
        a = k / 120 * TAU + t * TAU * 2 / 120 * 5
        r0, r1 = 300, 300 + (16 if k % 5 == 0 else 8)
        line(d, [(CX + math.cos(a) * r0, CY + math.sin(a) * r0), (CX + math.cos(a) * r1, CY + math.sin(a) * r1)],
             scale(BLUE, 0.9), 1.6)
    ring(d, 278, t, 6, 0.25, 1, scale(CYAN, 1.0), 6)
    ring(d, 245, t, 12, 0.35, -2, scale(MAGENTA, 0.95), 4)
    ring(d, 212, t, 3, 0.2, 3, scale(BLUE, 1.0), 9)
    ring(d, 180, t, 24, 0.5, -1, scale(WHITE, 0.85), 3)
    ring(d, 150, t, 8, 0.3, 2, scale(CYAN, 1.0), 5, phase=0.4)
    # hex-ish spokes
    for k in range(12):
        a = k / 12 * TAU + t * TAU / 12 * 1
        line(d, [(CX + math.cos(a) * 112, CY + math.sin(a) * 112), (CX + math.cos(a) * 140, CY + math.sin(a) * 140)],
             scale(VIOLET, 0.9), 2)
    # core: concentric rings pulsing
    core_r = 62 + 14 * beat
    circle(d, CX, CY, core_r + 22, scale(CYAN, 0.55 + 0.45 * beat), 3)
    circle(d, CX, CY, core_r + 8, scale(MAGENTA, 0.8), 2.5)
    # triangle inside rotating
    for tri, col in ((1, WHITE), (-1, CYAN)):
        pts = []
        for k in range(4):
            a = tri * t * TAU + k / 3 * TAU
            pts.append((CX + math.cos(a) * (core_r - 6), CY + math.sin(a) * (core_r - 6)))
        line(d, pts, scale(col, 0.9), 2.2)
    disc(d, CX, CY, core_r * 0.62, scale(CYAN, 0.65 + 0.35 * beat))
    disc(d, CX, CY, core_r * 0.38, scale(WHITE, 0.85 + 0.15 * beat))
    # expanding shock rings (3 per loop)
    for k in range(3):
        u = (t * 3 + k / 3.0) % 1.0
        # offset so each is spaced; one cycle per 1/3 loop
        r = 70 + u * 230
        circle(d, CX, CY, r, scale(CYAN, (1 - u) ** 1.5 * 0.9), 2.2)
    # particles drifting outward + orbiting
    for ph, r0, orb, sz, cyc in PARTS:
        u = (t * cyc + ph) % 1.0
        r = 80 + u * (r0 - 40) + 40
        a = ph * TAU + orb * t * TAU * 0.5 * 2 / 2
        # keep orbit integer cycles per loop
        a = ph * TAU + orb * t * TAU
        x, y = CX + math.cos(a) * r, CY + math.sin(a) * r
        life = math.sin(u * math.pi)
        col = MAGENTA if sz > 2.6 else (WHITE if sz > 2.0 else CYAN)
        disc(d, x, y, sz * (0.6 + life * 0.6), scale(col, life))
    # energy arcs lightning inside rings (deterministic per frame group)
    r2 = np.random.default_rng((i // 3) * 7 + 1)
    for _ in range(3):
        a0 = r2.uniform(0, TAU)
        pts = []
        for s in range(9):
            r = core_r + 10 + s * 12
            a = a0 + r2.normal(0, 0.06) + s * 0.01
            pts.append((CX + math.cos(a) * r, CY + math.sin(a) * r))
        line(d, pts, scale(WHITE, 0.8), 1.6)
    return img, dict(glow=1.0 + 0.5 * beat2, g1=4, g2=14)


if __name__ == "__main__":
    run("holo_reactor", N, render)

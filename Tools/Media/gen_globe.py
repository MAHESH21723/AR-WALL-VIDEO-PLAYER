import math
import numpy as np
from holo_common import *

N = 300  # 10 s
CX, CY, R = 640, 360, 235
TILT = rot_x(math.radians(-22)) @ rot_z(math.radians(12))
rng = np.random.default_rng(7)
# glowing points on globe (lat, lon)
POINTS = [(math.radians(rng.uniform(-60, 70)), math.radians(rng.uniform(0, 360))) for _ in range(16)]


def proj(v):
    return CX + v[0] * R, CY - v[1] * R


def sph(lat, lon):
    return np.array([math.cos(lat) * math.sin(lon), math.sin(lat), math.cos(lat) * math.cos(lon)])


def render(i, n):
    t = i / n
    img, d = new_canvas()
    spin = rot_y(t * TAU)
    M = TILT @ spin
    # back orbit ring half drawn first (dim), globe, then front half
    ring_r = R * 1.45
    ring_tilt = rot_x(math.radians(72)) @ rot_z(math.radians(-18))

    def ring_pt(a):
        v = ring_tilt @ np.array([math.cos(a) * ring_r / R, 0, math.sin(a) * ring_r / R])
        return v

    def draw_ring(front):
        pts = []
        for k in range(181):
            a = k / 180 * TAU
            v = ring_pt(a)
            pts.append((v, (v[2] > 0) == front))
        seg = []
        for v, ok in pts:
            if ok:
                seg.append(proj(v))
            else:
                if len(seg) > 1:
                    line(d, seg, scale(CYAN, 1.0 if front else 0.5), 2.2 if front else 1.4)
                seg = []
        if len(seg) > 1:
            line(d, seg, scale(CYAN, 1.0 if front else 0.5), 2.2 if front else 1.4)

    draw_ring(False)
    # outline + faint fill rim
    circle(d, CX, CY, R, scale(CYAN, 0.9), 2.5)
    circle(d, CX, CY, R + 10, scale(BLUE, 0.45), 1.2)
    # latitude lines
    for latd in range(-75, 90, 15):
        lat = math.radians(latd)
        seg = []
        for k in range(121):
            lon = k / 120 * TAU
            v = M @ sph(lat, lon)
            if v[2] > -0.02:
                seg.append(proj(v))
            else:
                if len(seg) > 1:
                    line(d, seg, scale(CYAN, 0.75 if latd else 1.0), 1.5 if latd else 2.4)
                seg = []
        if len(seg) > 1:
            line(d, seg, scale(CYAN, 0.75 if latd else 1.0), 1.5 if latd else 2.4)
    # longitude lines
    for lond in range(0, 360, 15):
        lon = math.radians(lond)
        seg = []
        for k in range(97):
            lat = -math.pi / 2 + k / 96 * math.pi
            v = M @ sph(lat, lon)
            if v[2] > -0.02:
                seg.append(proj(v))
            else:
                if len(seg) > 1:
                    line(d, seg, scale(BLUE, 1.0), 1.4)
                seg = []
        if len(seg) > 1:
            line(d, seg, scale(BLUE, 1.0), 1.4)
    # glowing points + arcs between a few pairs
    vis = []
    for idx, (lat, lon) in enumerate(POINTS):
        v = M @ sph(lat, lon)
        vis.append(v)
        if v[2] > 0:
            x, y = proj(v)
            pulse = 0.5 + 0.5 * math.sin(TAU * (t * 2 + idx / len(POINTS)))
            r = 3.5 + 2.5 * pulse
            disc(d, x, y, r, WHITE)
            circle(d, x, y, r + 6 + 4 * pulse, scale(MAGENTA, 0.5 + 0.5 * pulse), 1.5)
    for a, b in [(0, 3), (5, 9), (2, 12), (7, 14)]:
        la, lo = POINTS[a]
        lb, lob = POINTS[b]
        va, vb = sph(la, lo), sph(lb, lob)
        seg = []
        for k in range(41):
            u = k / 40
            p = va * (1 - u) + vb * u
            p = p / np.linalg.norm(p) * (1 + 0.28 * math.sin(math.pi * u))
            w = M @ p
            seg.append((w[2] > 0.0, proj(w)))
        run_ = []
        for ok, pt in seg:
            if ok:
                run_.append(pt)
            else:
                if len(run_) > 1:
                    line(d, run_, scale(MAGENTA, 0.9), 1.8)
                run_ = []
        if len(run_) > 1:
            line(d, run_, scale(MAGENTA, 0.9), 1.8)
    # equator scanning highlight band moving up/down (periodic)
    sl = math.sin(t * TAU) * 0.8
    half = math.sqrt(max(0, 1 - sl * sl))
    y = CY - sl * R
    line(d, [(CX - half * R, y), (CX + half * R, y)], scale(WHITE, 0.85), 1.6)
    draw_ring(True)
    # satellite on ring (2 laps per loop)
    a = t * TAU * 2
    v = ring_pt(a)
    x, y = proj(v)
    disc(d, x, y, 6, WHITE)
    circle(d, x, y, 12, scale(MAGENTA, 1.0), 2)
    # tick marks around outer ring rotating
    for k in range(60):
        ang = k / 60 * TAU + t * TAU / 60 * 5
        r0 = R + 28
        r1 = r0 + (14 if k % 5 == 0 else 7)
        line(d, [(CX + math.cos(ang) * r0, CY + math.sin(ang) * r0 * 0.0 + math.sin(ang) * r0),
                 (CX + math.cos(ang) * r1, CY + math.sin(ang) * r1)], scale(CYAN, 0.55), 1.4)
    return img, dict(glow=1.0)


if __name__ == "__main__":
    run("holo_globe", N, render)


"""Applica a uno screenshot la stessa formula di RetroCRT.shader (modalità Full), in Python + NumPy.

Uso: python tools/crt_preview.py <screenshot.png> <uscita.png> [altezza=1800]
Serve solo per l'immagine del README: nel gioco l'effetto lo fa lo shader sulla GPU.
"""
import sys
import numpy as np
from PIL import Image

src = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.float32) / 255.0
OH = int(sys.argv[3]) if len(sys.argv) > 3 else 1800
OW = OH * 4 // 3
lin = np.where(src <= 0.04045, src / 12.92, ((src + 0.055) / 1.055) ** 2.4)
SH, SW = lin.shape[:2]
curv, scanl, vign, aber, glow, lines = 0.045, 0.30, 0.85, 0.7, 0.35, 600.0

def sample(u, v):
    """tex2D bilineare (clamp) con uv in 0..1, v verso il basso come nell'immagine."""
    x = np.clip(u * SW - 0.5, 0, SW - 1); y = np.clip(v * SH - 0.5, 0, SH - 1)
    x0 = np.floor(x).astype(int); y0 = np.floor(y).astype(int)
    x1 = np.minimum(x0 + 1, SW - 1); y1 = np.minimum(y0 + 1, SH - 1)
    fx = (x - x0)[..., None]; fy = (y - y0)[..., None]
    a = lin[y0, x0] * (1 - fx) + lin[y0, x1] * fx
    b = lin[y1, x0] * (1 - fx) + lin[y1, x1] * fx
    return a * (1 - fy) + b * fy

j, i = np.mgrid[0:OH, 0:OW].astype(np.float32)
u = (i + 0.5) / OW; v = (j + 0.5) / OH          # v: 0 in alto (in Unity è 0 in basso, ma la formula è simmetrica)
d = np.stack([u * 2 - 1, v * 2 - 1], -1)
d = d * (1 + curv * d[..., ::-1] ** 2)
uv = d * 0.5 + 0.5
u, v = uv[..., 0], uv[..., 1]
outside = (u < 0) | (u > 1) | (v < 0) | (v > 1)
ab = aber / SW
col = np.empty((OH, OW, 3), np.float32)
col[..., 0] = sample(u + ab, v)[..., 0]
col[..., 1] = sample(u, v)[..., 1]
col[..., 2] = sample(u - ab, v)[..., 2]
ox, oy = 2.5 / SW, 2.5 / SH
blur = (sample(u + ox, v) + sample(u - ox, v) + sample(u, v + oy) + sample(u, v - oy)) * 0.25
col += np.maximum(blur - 0.12, 0) * glow
vy = 1 - v                                       # coordinata y "alla Unity" (0 in basso)
scan = 0.5 - 0.5 * np.cos(vy * lines * 2 * np.pi)
col *= (1 + (0.5 + 0.5 * scan - 1) * scanl)[..., None]
col *= 1 + scanl * 0.3
uvu = np.stack([u, vy], -1)
vv = uvu * (1 - uvu[..., ::-1])
col *= (np.clip(vv[..., 0] * vv[..., 1] * 16, 0, 1) ** (0.25 * vign))[..., None]
def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
edge = smooth(0, 0.004, uvu) * smooth(0, 0.004, 1 - uvu)
col *= (edge[..., 0] * edge[..., 1])[..., None]
col[outside] = 0
col = np.clip(col, 0, 1)
srgb = np.where(col <= 0.0031308, col * 12.92, 1.055 * col ** (1 / 2.4) - 0.055)
Image.fromarray((srgb * 255 + 0.5).astype(np.uint8)).save(sys.argv[2], optimize=True)

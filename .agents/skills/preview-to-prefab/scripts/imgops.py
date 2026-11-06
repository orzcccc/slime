"""Shared raster helpers (Pillow + NumPy only) for slicing UI preview images."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

LUMA = np.array([0.299, 0.587, 0.114], np.float32)


def load_rgb(path):
    return np.asarray(Image.open(path).convert('RGB')).astype(np.float32)


def luminance(rgb):
    return rgb @ LUMA


def flood(mask, seeds):
    """Pixels of boolean mask 4-connected to any seed (x, y)."""
    img = Image.fromarray(np.where(mask, 255, 0).astype(np.uint8)).copy()
    for x, y in seeds:
        x, y = int(x), int(y)
        if 0 <= x < img.width and 0 <= y < img.height and img.getpixel((x, y)) == 255:
            ImageDraw.floodfill(img, (x, y), 128, thresh=0)
    return np.asarray(img) == 128


def fill_holes(mask):
    padded = np.pad(~mask, 1, constant_values=True)
    return ~flood(padded, [(0, 0)])[1:-1, 1:-1]


def _filter(mask, r, flt):
    if r <= 0:
        return mask
    img = Image.fromarray(mask.astype(np.uint8) * 255)
    while r > 0:  # PIL rank filters are slow for big kernels; chain small ones
        step = min(r, 3)
        img = img.filter(flt(2 * step + 1))
        r -= step
    return np.asarray(img) > 0


def dilate(mask, r):
    return _filter(mask, r, ImageFilter.MaxFilter)


def erode(mask, r):
    return _filter(mask, r, ImageFilter.MinFilter)


def label(mask, min_area=1):
    """Connected components -> list of boolean masks (largest first)."""
    lab = Image.fromarray(np.where(mask, 1, 0).astype(np.int32), 'I').copy()
    out, next_id = [], 2
    while True:
        arr = np.asarray(lab)
        ys, xs = np.nonzero(arr == 1)
        if len(xs) == 0:
            break
        ImageDraw.floodfill(lab, (int(xs[0]), int(ys[0])), next_id, thresh=0)
        comp = np.asarray(lab) == next_id
        if comp.sum() >= min_area:
            out.append(comp)
        next_id += 1
    out.sort(key=lambda m: -m.sum())
    return out


def keep_main(mask, seed=None):
    """Keep the component containing seed (or the largest one)."""
    if seed is not None and mask[int(seed[1]), int(seed[0])]:
        return flood(mask, [seed])
    comps = label(mask)
    return comps[0] if comps else mask


def smooth(mask, r, seed=None):
    """Morphological opening that removes thin strands, keeping the main body."""
    if r <= 0:
        return mask
    opened = dilate(erode(mask, r), r)
    if not opened.any():
        return mask
    return mask & dilate(keep_main(opened, seed if seed is not None and opened[int(seed[1]), int(seed[0])] else None), 1)


def detect_background(rgb, tol=14, pocket_min=150):
    """Flat preview backdrop: border colour flood + enclosed pockets of the same colour."""
    h, w = rgb.shape[:2]
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    vals, counts = np.unique(border.astype(np.int32), axis=0, return_counts=True)
    color = vals[counts.argmax()].astype(np.float32)
    near = np.abs(rgb - color).max(-1) < tol
    seeds = [(x, 0) for x in range(0, w, 8)] + [(x, h - 1) for x in range(0, w, 8)]
    seeds += [(0, y) for y in range(0, h, 8)] + [(w - 1, y) for y in range(0, h, 8)]
    bg = flood(near, seeds)
    pockets = near & ~bg
    if pocket_min > 0 and pockets.any():
        for comp in label(erode(pockets, 1), pocket_min):
            bg |= dilate(comp, 1) & pockets
    return bg, color


def enclosed(lum, box, seed, dark=80, block=None):
    """Shape that contains seed and is closed by dark outline pixels inside box."""
    h, w = lum.shape
    x0, y0, x1, y1 = [int(v) for v in box]
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(w, x1), min(h, y1)
    passable = lum[y0:y1, x0:x1] >= dark
    if block is not None:
        passable &= ~block[y0:y1, x0:x1]
    outside = flood(np.pad(passable, 1, constant_values=True), [(0, 0)])[1:-1, 1:-1]
    comp = flood(~outside, [(seed[0] - x0, seed[1] - y0)])
    full = np.zeros((h, w), bool)
    full[y0:y1, x0:x1] = fill_holes(comp)
    return full


def blur_rgb(rgb, r):
    """Approximate gaussian blur (3 box passes, edge padded) for float HxWxC arrays."""
    k = max(1, int(round(r)))
    out = rgb.astype(np.float32)
    for axis in (0, 1):
        for _ in range(3):
            pad = [(0, 0)] * out.ndim
            pad[axis] = (k + 1, k)
            c = np.cumsum(np.pad(out, pad, mode='edge'), axis=axis)
            n = out.shape[axis]
            hi = np.take(c, np.arange(2 * k + 1, 2 * k + 1 + n), axis=axis)
            lo = np.take(c, np.arange(0, n), axis=axis)
            out = (hi - lo) / (2 * k + 1)
    return out


def pushpull(rgb, known):
    """Fill unknown pixels from known ones with a pyramid push-pull (smooth colours)."""
    def rec(img, wt):
        h, w = img.shape[:2]
        if min(h, w) <= 2:
            s = (img * wt[..., None]).sum((0, 1)) / max(wt.sum(), 1e-6)
            return np.broadcast_to(s, img.shape).copy()
        ph, pw = h + h % 2, w + w % 2
        I = np.pad(img * wt[..., None], ((0, ph - h), (0, pw - w), (0, 0)), mode='edge')
        Wt = np.pad(wt, ((0, ph - h), (0, pw - w)), mode='edge')
        Ic = I.reshape(ph // 2, 2, pw // 2, 2, -1).sum((1, 3))
        Wc = Wt.reshape(ph // 2, 2, pw // 2, 2).sum((1, 3))
        coarse = np.where(Wc[..., None] > 0, Ic / np.maximum(Wc[..., None], 1e-6), 0)
        coarse = rec(coarse, np.minimum(Wc, 1))
        up = np.stack([np.asarray(Image.fromarray(coarse[..., c].astype(np.float32), 'F').resize((pw, ph), Image.BILINEAR))
                       for c in range(img.shape[2])], -1)[:h, :w]
        return img * wt[..., None] + up * (1 - wt[..., None])
    return rec(np.where(known[..., None], rgb, 0).astype(np.float32), known.astype(np.float32))


def gradient_fill(rgb, known, hole):
    """Quadratic colour gradient fitted on known pixels (good for flat buttons/panels)."""
    ys, xs = np.nonzero(known)
    if len(xs) < 20:
        return pushpull(rgb, known)
    cx, cy, s = xs.mean(), ys.mean(), max(np.ptp(xs), np.ptp(ys), 1) / 2

    def basis(x, y):
        u, v = (x - cx) / s, (y - cy) / s
        return np.stack([np.ones_like(u), u, v, u * u, v * v, u * v], 1)
    coef = np.linalg.lstsq(basis(xs.astype(float), ys.astype(float)), rgb[ys, xs], rcond=None)[0]
    out = rgb.copy()
    hy, hx = np.nonzero(hole)
    out[hy, hx] = basis(hx.astype(float), hy.astype(float)) @ coef
    return out


def texture_fill(rgb, known, hole, patch=72, seed=7):
    """Smooth base colour + high-frequency detail copied from clean known patches."""
    base = blur_rgb(pushpull(rgb, known), 5)
    detail_src = rgb - blur_rgb(rgb, 6)
    ii = np.pad(known.astype(np.int32).cumsum(0).cumsum(1), ((1, 0), (1, 0)))
    full = ii[patch:, patch:] - ii[:-patch, patch:] - ii[patch:, :-patch] + ii[:-patch, :-patch]
    cand = np.argwhere(full == patch * patch)
    if len(cand) == 0 or not hole.any():
        return base
    cand = cand[::max(1, len(cand) // 600)]
    calm = [tuple(c) for c in cand if rgb[c[0]:c[0] + patch, c[1]:c[1] + patch].std(axis=(0, 1)).mean() < 18] or [tuple(cand[0])]
    rng = np.random.default_rng(seed)
    win1 = np.sin(np.linspace(0, np.pi, patch)) ** 2
    win = (win1[:, None] * win1[None, :])[..., None]
    h, w = hole.shape
    acc = np.zeros((h + patch, w + patch, 3), np.float32)
    wacc = np.zeros((h + patch, w + patch, 1), np.float32)
    ty, tx = np.nonzero(hole)
    step = patch // 2
    for gy in range(max(0, ty.min() - patch), ty.max() + 1, step):
        for gx in range(max(0, tx.min() - patch), tx.max() + 1, step):
            oy, ox = max(0, gy + int(rng.integers(-6, 7))), max(0, gx + int(rng.integers(-6, 7)))
            sy, sx = calm[int(rng.integers(len(calm)))]
            p = detail_src[sy:sy + patch, sx:sx + patch]
            if rng.random() < 0.5:
                p = p[:, ::-1]
            acc[oy:oy + patch, ox:ox + patch] += p * win
            wacc[oy:oy + patch, ox:ox + patch] += win
    detail = (acc / np.maximum(wacc, 1e-3))[:h, :w]
    fade = np.asarray(Image.fromarray(hole.astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(3))).astype(np.float32)[..., None] / 255
    return base + detail * fade


def soft_alpha(mask, rgb, outline):
    """1px anti-aliased edge painted with the outline colour."""
    ring = dilate(mask, 1) & ~mask
    a = np.asarray(Image.fromarray(mask.astype(np.uint8) * 255).filter(ImageFilter.GaussianBlur(0.6))).astype(np.float32) / 255
    alpha = np.where(mask, 1.0, np.where(ring, a, 0.0))
    out = rgb.copy()
    out[ring] = outline
    return out, alpha


def to_rgba(rgb, alpha):
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(alpha, 0, 1) * 255]).astype(np.uint8), 'RGBA')


def crop_box(alpha, pad=2):
    ys, xs = np.nonzero(alpha > 0)
    h, w = alpha.shape
    return max(0, xs.min() - pad), max(0, ys.min() - pad), min(w, xs.max() + 1 + pad), min(h, ys.max() + 1 + pad)


def checker(img, cell=16):
    bg = Image.new('RGBA', img.size, (255, 255, 255, 255))
    d = ImageDraw.Draw(bg)
    for y in range(0, img.height, cell):
        for x in range(0, img.width, cell):
            if (x // cell + y // cell) % 2:
                d.rectangle((x, y, x + cell - 1, y + cell - 1), fill=(255, 0, 255, 255))
    bg.alpha_composite(img)
    return bg


def contact_sheet(images, width=1500, pad=10, bg=(60, 60, 60)):
    x = y = rowh = 0
    pos = []
    for im in images:
        if x + im.width > width and x > 0:
            x, y, rowh = 0, y + rowh + pad, 0
        pos.append((x, y))
        x += im.width + pad
        rowh = max(rowh, im.height)
    sheet = Image.new('RGB', (width, max(1, y + rowh)), bg)
    for im, p in zip(images, pos):
        sheet.paste(im.convert('RGB'), p)
    return sheet

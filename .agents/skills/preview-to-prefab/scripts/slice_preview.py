"""Slice a UI preview image into transparent sprites.

  python slice_preview.py candidates --src preview.png --out .codex_tmp/slice/page
  python slice_preview.py run --spec slice_spec.json [--workers 4]

See references/slice-spec.md for the spec format.
"""
import argparse, json, os, sys, time
from concurrent.futures import ProcessPoolExecutor
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import imgops as io_

G = {}


def _init(src, bg_path):
    G['rgb'] = io_.load_rgb(src)
    G['lum'] = io_.luminance(G['rgb'])
    G['bg'] = np.load(bg_path) if bg_path else None


def pack(m):
    return (m.shape, np.packbits(m))


def unpack(p):
    shape, bits = p
    return np.unpackbits(bits)[:shape[0] * shape[1]].reshape(shape).astype(bool)


# ---------------------------------------------------------------- masks
def mask_job(part):
    lum, bg = G['lum'], G['bg']
    mode, dark = part.get('mode', 'enclosed'), part.get('dark', 80)
    h, w = lum.shape
    if mode == 'enclosed':
        m = io_.enclosed(lum, part['box'], part['seed'], dark)
    elif mode == 'region':
        x0, y0, x1, y1 = part['box']
        pas = np.zeros((h, w), bool)
        pas[y0:y1, x0:x1] = lum[y0:y1, x0:x1] >= dark
        if bg is not None:
            pas &= ~bg
        m = io_.fill_holes(io_.flood(pas, part['seeds']))
    elif mode == 'box':
        x0, y0, x1, y1 = part['box']
        m = np.zeros((h, w), bool)
        m[y0:y1, x0:x1] = True
        if bg is not None:
            m &= ~bg
    else:
        raise ValueError(mode)
    if part.get('grow'):
        m = io_.dilate(m, part['grow'])
        if bg is not None:
            m &= ~bg
        m = io_.fill_holes(m)
    if part.get('smooth'):
        seed = part.get('seed') or (part.get('seeds') or [None])[0]
        m = io_.smooth(m, part['smooth'], seed)
    return part['name'], pack(m)


def inner_mask(part, masks, lum):
    parent = masks[part['of']]
    ys, xs = np.nonzero(parent)
    box = part.get('box') or [xs.min() + 8, ys.min() + 8, xs.max() - 8, ys.max() - 8]
    x0, y0, x1, y1 = box
    sub = np.zeros_like(parent)
    pas = lum[y0:y1, x0:x1] >= part.get('dark', 80)
    outside = io_.flood(np.pad(pas, 1, constant_values=True), [(0, 0)])[1:-1, 1:-1]
    sub[y0:y1, x0:x1] = io_.fill_holes(~outside)
    edges = part.get('exclude_edges', 'ltr')
    edge = np.zeros_like(parent)
    if 'l' in edges: edge[y0:y1, x0] = True
    if 'r' in edges: edge[y0:y1, x1 - 1] = True
    if 't' in edges: edge[y0, x0:x1] = True
    if 'b' in edges: edge[y1 - 1, x0:x1] = True
    out = np.zeros_like(parent)
    for c in io_.label(sub & io_.erode(parent, part.get('within', 7)), part.get('min_area', 60)):
        if not (c & edge).any():
            out |= c
    return out


def rest_mask(part, masks, bg):
    under = [u if isinstance(u, dict) else {'part': u} for u in part.get('under', [])]
    occ = np.zeros_like(bg)
    for u in under:
        occ |= masks[u['part']]
    for name in part.get('exclude', []):
        occ |= masks[name]
    vis = io_.keep_main(~bg & ~io_.dilate(occ, 1))
    hole = io_.dilate(np.any([masks[u['part']] for u in under], 0), 2) if under else np.zeros_like(bg)
    known = vis & ~hole
    a = io_.pushpull(known[..., None].astype(np.float32), known | ~hole)[..., 0] if hole.any() else known
    new = known | (hole & (a > 0.5))
    new = io_.fill_holes(new) & ~(bg & ~hole)
    for u in under:
        if u.get('extend', True) is False:
            region = io_.dilate(masks[u['part']], 2)
            new &= ~(region & ~io_.fill_holes(known))
    new = io_.smooth(new, 2) | known
    return new, known


# ---------------------------------------------------------------- export
def text_rect(rect, inner, rgb, lum, pad):
    x0, y0, x1, y1 = rect
    sel = np.zeros_like(inner)
    sel[y0:y1, x0:x1] = True
    pix = sel & inner & ((lum < 120) | (rgb.min(-1) > 170))
    out = np.zeros_like(inner)
    if pix.any():
        ys, xs = np.nonzero(pix)
        out[max(0, ys.min() - pad):ys.max() + pad + 1, max(0, xs.min() - pad):xs.max() + pad + 1] = True
    return out


def export_job(args):
    part, m_p, refs_p, known_p, out_dir, outline = args
    rgb, lum = G['rgb'], G['lum']
    m = unpack(m_p)
    refs = {k: unpack(v) for k, v in refs_p.items()}
    edge = part.get('keep_edge', 6)
    hole = np.zeros_like(m)
    for e in part.get('erase', []):
        if 'part' in e:
            hole |= io_.dilate(refs[e['part']], e.get('grow', 4))
        elif 'text' in e:
            hole |= text_rect(e['text'], io_.erode(m, edge + 3), rgb, lum, e.get('pad', 4))
        elif 'box' in e:
            x0, y0, x1, y1 = e['box']
            hole[y0:y1, x0:x1] = True
    hole &= io_.erode(m, edge)
    known = unpack(known_p) if known_p is not None else m & ~hole
    if known_p is not None:
        hole = m & ~known
    fill = part.get('fill', 'smooth' if hole.any() else 'none')
    out = rgb.copy()
    if hole.any() and fill != 'none':
        src_known = known & io_.erode(m, edge) if part.get('mode') != 'rest' else known
        if fill == 'gradient':
            filled = io_.gradient_fill(rgb, src_known, hole)
        elif fill == 'texture':
            filled = io_.texture_fill(rgb, src_known, hole)
        else:
            filled = io_.blur_rgb(io_.pushpull(rgb, src_known), part.get('blur', 3))
        out = np.where(hole[..., None], filled, rgb)
        if part.get('mode') == 'rest':
            band = m & ~io_.erode(m, 4) & hole
            out[band] = outline
    out, alpha = io_.soft_alpha(m, out, outline)
    x0, y0, x1, y1 = io_.crop_box(alpha)
    io_.to_rgba(out[y0:y1, x0:x1], alpha[y0:y1, x0:x1]).save(os.path.join(out_dir, part['name'] + '.png'))
    fill_path = os.path.join(out_dir, 'fill', part['name'] + '.png')
    if hole.any():
        Image.fromarray((hole[y0:y1, x0:x1] * 255).astype(np.uint8)).save(fill_path)
    elif os.path.exists(fill_path):
        os.remove(fill_path)
    return part['name'], {'x': int(x0), 'y': int(y0), 'w': int(x1 - x0), 'h': int(y1 - y0), 'filled_px': int(hole.sum())}


def recompose(spec, out_dir, layout, size, bgc):
    order = spec.get('order') or [p['name'] for p in spec['parts']]
    canvas = Image.new('RGBA', size, tuple(int(v) for v in bgc) + (255,))
    for name in order:
        if name in layout:
            L = layout[name]
            canvas.alpha_composite(Image.open(os.path.join(out_dir, name + '.png')), (L['x'], L['y']))
    src = Image.open(spec['source']).convert('RGBA')
    half = (size[0] // 2, size[1] // 2)
    both = Image.new('RGB', (half[0] * 2, half[1]))
    both.paste(src.resize(half).convert('RGB'), (0, 0))
    both.paste(canvas.resize(half).convert('RGB'), (half[0], 0))
    both.save(os.path.join(out_dir, 'recompose.png'))
    sheet = io_.contact_sheet([io_.checker(Image.open(os.path.join(out_dir, n + '.png'))) for n in layout])
    sheet.save(os.path.join(out_dir, 'sheet.png'))


def cmd_run(a):
    t0 = time.time()
    spec = json.load(open(a.spec, encoding='utf-8'))
    out_dir = spec.get('out') or os.path.splitext(a.spec)[0]
    os.makedirs(os.path.join(out_dir, 'fill'), exist_ok=True)
    rgb = io_.load_rgb(spec['source'])
    lum = io_.luminance(rgb)
    bcfg = spec.get('background', {})
    bg, bgc = io_.detect_background(rgb, bcfg.get('tol', 14), bcfg.get('pocket_min', 150))
    bg_path = os.path.join(out_dir, 'bg.npy')
    np.save(bg_path, bg)
    outline = np.array(spec.get('outline') or np.median(rgb[lum < 40], 0) if (lum < 40).any() else [24, 24, 34], np.float32)
    parts = spec['parts']
    by = {p['name']: p for p in parts}
    workers = a.workers or min(8, os.cpu_count() or 2)
    masks = {}
    with ProcessPoolExecutor(workers, initializer=_init, initargs=(spec['source'], bg_path)) as ex:
        first = [p for p in parts if p.get('mode', 'enclosed') in ('enclosed', 'region', 'box')]
        for name, mp in ex.map(mask_job, first):
            masks[name] = unpack(mp)
        print('masks %.1fs' % (time.time() - t0))
        known = {}
        for p in parts:
            if p.get('mode') == 'inner':
                masks[p['name']] = inner_mask(p, masks, lum)
        for p in parts:
            if p.get('mode') == 'rest':
                masks[p['name']], known[p['name']] = rest_mask(p, masks, bg)
        jobs = []
        for p in parts:
            refs = {e['part']: pack(masks[e['part']]) for e in p.get('erase', []) if 'part' in e}
            jobs.append((p, pack(masks[p['name']]), refs, pack(known[p['name']]) if p['name'] in known else None, out_dir, outline))
        layout = dict(ex.map(export_job, jobs))
    meta = {'source': spec['source'], 'size': [rgb.shape[1], rgb.shape[0]], 'background': [int(v) for v in bgc],
            'parts': {k: dict(layout[k], **{x: by[k][x] for x in ('folder', 'file', 'border', 'readable', 'ai') if x in by[k]}) for k in layout}}
    json.dump(meta, open(os.path.join(out_dir, 'layout.json'), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    recompose(spec, out_dir, layout, (rgb.shape[1], rgb.shape[0]), bgc)
    print('done %.1fs ->' % (time.time() - t0), out_dir)
    for k, v in layout.items():
        print('  %-18s %4d,%4d %4dx%-4d filled=%d' % (k, v['x'], v['y'], v['w'], v['h'], v['filled_px']))


def cmd_candidates(a):
    os.makedirs(a.out, exist_ok=True)
    rgb = io_.load_rgb(a.src)
    lum = io_.luminance(rgb)
    bg, bgc = io_.detect_background(rgb, a.tol)
    regions = io_.label((lum >= a.dark) & ~bg, a.min_area)
    objects = io_.label(~bg, a.min_area)
    over = (rgb * 0.35).astype(np.uint8)
    rng = np.random.default_rng(3)
    info = []
    for i, m in enumerate(regions):
        col = rng.integers(80, 256, 3)
        over[m] = (rgb[m] * 0.4 + col * 0.6).astype(np.uint8)
        ys, xs = np.nonzero(m)
        k = np.argmin((ys - ys.mean()) ** 2 + (xs - xs.mean()) ** 2)
        info.append({'id': i, 'seed': [int(xs[k]), int(ys[k])], 'bbox': [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1],
                     'area': int(m.sum()), 'color': [int(v) for v in rgb[m].mean(0)]})
    img = Image.fromarray(over)
    d = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype('arial.ttf', 14)
    except OSError:
        font = ImageFont.load_default()
    for gx in range(0, img.width, 50):
        d.line((gx, 0, gx, img.height), fill=(90, 90, 90) if gx % 100 else (140, 140, 140))
        if gx % 100 == 0: d.text((gx + 2, 2), str(gx), fill=(255, 255, 0), font=font)
    for gy in range(0, img.height, 50):
        d.line((0, gy, img.width, gy), fill=(90, 90, 90) if gy % 100 else (140, 140, 140))
        if gy % 100 == 0: d.text((2, gy + 2), str(gy), fill=(255, 255, 0), font=font)
    for r in info:
        d.text(tuple(r['seed']), str(r['id']), fill=(255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0), anchor='mm')
    for m in objects:
        ys, xs = np.nonzero(m)
        d.rectangle((xs.min(), ys.min(), xs.max(), ys.max()), outline=(255, 255, 0))
    img.save(os.path.join(a.out, 'candidates.png'))
    json.dump({'size': [rgb.shape[1], rgb.shape[0]], 'background': [int(v) for v in bgc],
               'objects': [[int(v) for v in (np.nonzero(m)[1].min(), np.nonzero(m)[0].min(), np.nonzero(m)[1].max() + 1, np.nonzero(m)[0].max() + 1)] for m in objects],
               'regions': info}, open(os.path.join(a.out, 'candidates.json'), 'w'), indent=1)
    print(len(objects), 'objects,', len(regions), 'regions ->', os.path.join(a.out, 'candidates.png'))


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest='cmd', required=True)
    c = sub.add_parser('candidates')
    c.add_argument('--src', required=True); c.add_argument('--out', required=True)
    c.add_argument('--dark', type=int, default=80); c.add_argument('--tol', type=int, default=14); c.add_argument('--min-area', type=int, default=400)
    r = sub.add_parser('run')
    r.add_argument('--spec', required=True); r.add_argument('--workers', type=int)
    a = ap.parse_args()
    cmd_candidates(a) if a.cmd == 'candidates' else cmd_run(a)

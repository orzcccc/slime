"""Repaint the algorithmically filled (occluded) areas of sliced sprites with AI, in parallel.

  python ai_fill.py --slice-dir .codex_tmp/slice/page [--parts island,tab_bg_bag] [--workers 4] [--quality medium] [--dry-run]

Reads <slice-dir>/layout.json and <slice-dir>/fill/<part>.png (written by slice_preview.py run).
By default only parts with "ai" set in the slice spec are processed; --parts overrides that.
Visible source pixels are never changed: only pixels inside the fill mask are replaced, and
fill pixels the model paints with the key colour become transparent.
"""
import argparse, glob, json, os, shutil, subprocess, sys, time
from concurrent.futures import ThreadPoolExecutor, as_completed
import numpy as np
from PIL import Image, ImageFilter

EDIT = 1024
KEYS = [(255, 0, 255), (0, 255, 0), (0, 255, 255), (0, 0, 255), (255, 255, 0)]
BASE_PROMPT = """Use case: precise-object-edit
Asset type: 2D game UI sprite on a flat chroma-key background
Primary request: repaint ONLY the transparent (masked) area so it continues the surrounding artwork seamlessly. {hint}
Style/medium: same flat cartoon mobile-game art as the visible pixels: thick dark outlines, soft cel shading, identical palette and line weight.
Constraints: keep every unmasked pixel unchanged; continue existing shapes, outlines and textures across the masked area; anything that is not part of the object must stay the exact flat background colour {key}.
Avoid: new characters, new props, text, letters, numbers, logos, watermark, shadows on the background, gradients on the background."""


def find_imagegen(explicit):
    cands = [explicit] if explicit else []
    if os.environ.get('CODEX_HOME'):
        cands.append(os.path.join(os.environ['CODEX_HOME'], 'skills', 'aigw-imagegen'))
    cands += glob.glob(os.path.join(os.environ.get('APPDATA', ''), 'xiaojiu-ai', 'profiles', '*', 'codex-home', 'skills', 'aigw-imagegen'))
    cands.append(os.path.expanduser('~/.codex/skills/aigw-imagegen'))
    for c in cands:
        if c and os.path.exists(os.path.join(c, 'scripts', 'image_gen.py')):
            return c
    sys.exit('aigw-imagegen skill not found; install/enable it or pass --imagegen <dir>')


def pick_key(rgba):
    a = np.asarray(rgba)
    px = a[a[..., 3] > 128][:, :3].astype(np.float32)
    if len(px) > 20000:
        px = px[np.random.default_rng(0).choice(len(px), 20000, replace=False)]
    best = max(KEYS, key=lambda k: np.percentile(np.abs(px - np.array(k)).sum(1), 1) if len(px) else 0)
    return best


def prepare(name, slice_dir, work):
    sprite = Image.open(os.path.join(slice_dir, name + '.png')).convert('RGBA')
    fill = Image.open(os.path.join(slice_dir, 'fill', name + '.png')).convert('L')
    key = pick_key(sprite)
    w, h = sprite.size
    side = int(max(w, h) * 1.06) + 8
    ox, oy = (side - w) // 2, (side - h) // 2
    canvas = Image.new('RGBA', (side, side), key + (255,))
    canvas.alpha_composite(sprite, (ox, oy))
    hole = fill.filter(ImageFilter.MaxFilter(7))
    mask = Image.new('RGBA', (side, side), (255, 255, 255, 255))
    m_alpha = Image.new('L', (side, side), 255)
    m_alpha.paste(Image.eval(hole, lambda v: 0 if v > 0 else 255), (ox, oy))
    mask.putalpha(m_alpha)
    img_p, mask_p = os.path.join(work, name + '_in.png'), os.path.join(work, name + '_mask.png')
    canvas.convert('RGB').resize((EDIT, EDIT), Image.LANCZOS).save(img_p)
    mask.resize((EDIT, EDIT), Image.NEAREST).save(mask_p)
    return dict(name=name, key=key, side=side, off=(ox, oy), size=(w, h), image=img_p, mask=mask_p)


def run_edit(job, imagegen, hint, quality, work, dry):
    prompt_p = os.path.join(work, job['name'] + '_prompt.txt')
    open(prompt_p, 'w', encoding='utf-8').write(BASE_PROMPT.format(hint=hint or '', key='#%02x%02x%02x' % job['key']))
    out_p = os.path.join(work, job['name'] + '_ai.png')
    cmd = [sys.executable, os.path.join(imagegen, 'scripts', 'image_gen.py'), 'edit', '--image', job['image'], '--mask', job['mask'],
           '--prompt-file', prompt_p, '--size', '%dx%d' % (EDIT, EDIT), '--quality', quality, '--no-augment',
           '--out', out_p, '--force']
    if dry:
        cmd.append('--dry-run')
    t = time.time()
    r = subprocess.run(cmd, capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=600)
    job['seconds'] = round(time.time() - t, 1)
    if r.returncode != 0 or (not dry and not os.path.exists(out_p)):
        raise RuntimeError((r.stderr or r.stdout)[-800:])
    job['result'] = out_p
    return job


def merge(job, slice_dir):
    name = job['name']
    src_p = os.path.join(slice_dir, name + '.png')
    algo_dir = os.path.join(slice_dir, 'algo'); os.makedirs(algo_dir, exist_ok=True)
    if not os.path.exists(os.path.join(algo_dir, name + '.png')):
        shutil.copyfile(src_p, os.path.join(algo_dir, name + '.png'))
    base = Image.open(os.path.join(algo_dir, name + '.png')).convert('RGBA')
    w, h = job['size']; ox, oy = job['off']
    ai = Image.open(job['result']).convert('RGB').resize((job['side'], job['side']), Image.LANCZOS).crop((ox, oy, ox + w, oy + h))
    fill = Image.open(os.path.join(slice_dir, 'fill', name + '.png')).convert('L')
    zone = np.asarray(fill.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.5))).astype(np.float32)[..., None] / 255
    b = np.asarray(base).astype(np.float32)
    a = np.asarray(ai).astype(np.float32)
    keyd = np.abs(a - np.array(job['key'], np.float32)).sum(-1)
    is_key = np.clip((120 - keyd) / 60, 0, 1)[..., None]
    rgb = b[..., :3] * (1 - zone) + a * zone
    alpha = b[..., 3:] * (1 - zone * is_key)
    Image.fromarray(np.dstack([rgb, alpha]).clip(0, 255).astype(np.uint8), 'RGBA').save(src_p)
    cmp_ = Image.new('RGBA', (w * 2 + 10, h), (60, 60, 60, 255))
    cmp_.alpha_composite(base, (0, 0)); cmp_.alpha_composite(Image.open(src_p), (w + 10, 0))
    cmp_.save(os.path.join(slice_dir, 'ai', name + '_compare.png'))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--slice-dir', required=True)
    ap.add_argument('--parts')
    ap.add_argument('--workers', type=int, default=4)
    ap.add_argument('--quality', default='medium')
    ap.add_argument('--imagegen')
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args()
    layout = json.load(open(os.path.join(a.slice_dir, 'layout.json'), encoding='utf-8'))['parts']
    names = a.parts.split(',') if a.parts else [n for n, p in layout.items() if p.get('ai')]
    names = [n for n in names if os.path.exists(os.path.join(a.slice_dir, 'fill', n + '.png'))]
    if not names:
        sys.exit('no parts with fill masks to repaint')
    imagegen = find_imagegen(a.imagegen)
    if not a.dry_run:
        probe = subprocess.run([sys.executable, os.path.join(imagegen, 'scripts', 'get_current_env.py')], capture_output=True, text=True)
        if '"run_skill"' not in probe.stdout:
            sys.exit('image service not ready: ' + probe.stdout.strip()[:300])
    work = os.path.join(a.slice_dir, 'ai'); os.makedirs(work, exist_ok=True)
    jobs = [prepare(n, a.slice_dir, work) for n in names]
    t0, ok, failed = time.time(), [], []
    with ThreadPoolExecutor(a.workers) as ex:
        futs = {ex.submit(run_edit, j, imagegen, layout[j['name']].get('ai') if isinstance(layout[j['name']].get('ai'), str) else '', a.quality, work, a.dry_run): j for j in jobs}
        for f in as_completed(futs):
            j = futs[f]
            try:
                f.result()
                if not a.dry_run:
                    merge(j, a.slice_dir)
                ok.append(j['name']); print('ok   %-18s %5.1fs' % (j['name'], j['seconds']), flush=True)
            except Exception as e:
                failed.append(j['name']); print('FAIL %-18s %s' % (j['name'], str(e).strip()[-300:]), flush=True)
    print('total %.1fs, ok=%s failed=%s' % (time.time() - t0, ok, failed))
    print('compare images:', os.path.join(work, '<part>_compare.png'))
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()

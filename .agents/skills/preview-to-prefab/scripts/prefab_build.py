"""Build or incrementally update a UI prefab from a JSON node tree.

  python prefab_build.py --spec build.json [--dry-run]

See references/prefab-spec.md. Nodes that already exist (same name under the same parent) are
updated in place so fileIDs and external references survive; new nodes get fresh fileIDs.
"""
import argparse, hashlib, json, os, re, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prefab_lib import Prefab, GUIDS, HALIGN, VALIGN, CHILD_ALIGN, script_guid, v2, color, fmt

ANCHORS = {
    'center': ((.5, .5), (.5, .5), (.5, .5)), 'stretch': ((0, 0), (1, 1), (.5, .5)),
    'top': ((0, 1), (1, 1), (.5, 1)), 'bottom': ((0, 0), (1, 0), (.5, 0)), 'left': ((0, 0), (0, 1), (0, .5)), 'right': ((1, 0), (1, 1), (1, .5)),
    'top-left': ((0, 1), (0, 1), (0, 1)), 'top-right': ((1, 1), (1, 1), (1, 1)), 'bottom-left': ((0, 0), (0, 0), (0, 0)),
    'bottom-right': ((1, 0), (1, 0), (1, 0)), 'top-center': ((.5, 1), (.5, 1), (.5, 1)), 'bottom-center': ((.5, 0), (.5, 0), (.5, 0)),
    'middle-left': ((0, .5), (0, .5), (0, .5)), 'middle-right': ((1, .5), (1, .5), (1, .5)),
}
IMG_TYPES = {'simple': 0, 'sliced': 1, 'tiled': 2, 'filled': 3}


class Builder:
    def __init__(self, spec):
        self.spec = spec
        self.P = Prefab(spec['prefab'], seed=int(hashlib.md5(spec['prefab'].encode()).hexdigest()[:8], 16), create=True)
        # A newly created prefab starts empty: drop the placeholder header id from the object order.
        self.P.order = [i for i in self.P.order if i in self.P.docs]
        self.P.docs = {i: d for i, d in self.P.docs.items() if i in self.P.order}
        self.sprites = {}
        for f in ([spec['sprites']] if isinstance(spec.get('sprites'), str) else spec.get('sprites', [])):
            self.sprites.update(json.load(open(f, encoding='utf-8')))
        self.pending = []  # (component id, fields) resolved after the whole tree exists

    def sprite_guid(self, s):
        if not s:
            return None
        if s.startswith('guid:'):
            return s[5:]
        if s in self.sprites:
            return self.sprites[s]['guid']
        meta = s + '.meta' if s.endswith('.png') else None
        if meta and os.path.exists(meta):
            return re.search(r'guid: (\w+)', open(meta, encoding='utf-8').read()).group(1)
        raise KeyError('unknown sprite ' + s)

    def rect_args(self, node):
        r = dict(node.get('rect', {}))
        if 'sprite_rect' in node:
            s = self.sprites[node['sprite_rect']]
            r.setdefault('anchor', 'center')
            r.setdefault('size', s['ref_size'])
            pos = s['ref_center']
            if 'parent_center' in r:  # make position relative to a centred parent at this canvas position
                pos = [pos[0] - r['parent_center'][0], pos[1] - r['parent_center'][1]]
            r.setdefault('pos', pos)
        if not r:
            return None
        amin, amax, pivot = ANCHORS[r.get('anchor', 'center')]
        amin, amax, pivot = r.get('amin', amin), r.get('amax', amax), r.get('pivot', pivot)
        if 'offset' in r:  # [left, bottom, right, top] insets; fixed axes keep size/pos (offset shifts them)
            l, b, rr, t = r['offset']
            size, pos = list(r.get('size', (100, 100))), list(r.get('pos', (0, 0)))
            for i, (lo, hi) in enumerate(((l, rr), (b, t))):
                if amin[i] != amax[i]:
                    size[i] = -(lo + hi)
                    pos[i] = lo + size[i] * pivot[i]
                else:
                    pos[i] += lo - hi
            return amin, amax, pos, size, pivot
        return amin, amax, r.get('pos', (0, 0)), r.get('size', (0, 0) if amin != amax else (100, 100)), pivot

    def node(self, node, parent, index=None):
        P = self.P
        existing = None
        if parent is not None:
            existing = next((c for c in P.child_gos(parent) if P.name(c) == node['name']), None)
        elif P.roots():
            existing = next((g for g in P.roots() if P.name(g) == node['name']), None)
        ra = self.rect_args(node)
        visual = any(k in node for k in ('image', 'text'))
        if existing:
            go = existing
            if ra:
                P.layout(go, *ra)
            if index is not None:
                P.reparent(go, parent, index)
        else:
            amin, amax, pos, size, pivot = ra or ((.5, .5), (.5, .5), (0, 0), (100, 100), (.5, .5))
            go = P.new_go(node['name'], parent, index, amin, amax, pos, size, pivot, renderer=visual)
        if 'active' in node:
            P.set(go, 'm_IsActive', int(node['active']))
        img = None
        if 'image' in node:
            i = node['image']
            img = P.comp(go, 'Image', required=False)
            args = dict(sprite=self.sprite_guid(i.get('sprite')), type_=IMG_TYPES[i.get('type', 'simple')], preserve=i.get('preserve', False),
                        raycast=i.get('raycast', bool(node.get('button'))), ppu=i.get('ppu', 1))
            col = i.get('color', '#ffffff')
            if img:
                P.set_image(img, col=col, enabled=1, **{k: (v if v is not None else '') for k, v in args.items()})
            else:
                img = P.image(go, col=col, **args)
        if 'text' in node:
            t = node['text']
            args = dict(text=t.get('value', ''), size=t.get('size', 20), col=t.get('color', '#ffffff'), h_align=HALIGN[t.get('align', 'center')],
                        v_align=VALIGN[t.get('valign', 'middle')], wrap=t.get('wrap', False), bold=t.get('bold', False), raycast=0)
            c = P.comp(go, 'TMP', required=False)
            if c:
                P.set_tmp(c, **args)
            else:
                P.tmp(go, args.pop('text'), args.pop('size'), args.pop('col'), **args)
        if node.get('button'):
            c = P.comp(go, 'Button', required=False)
            if c:
                P.normalize_button(c, img)
            else:
                P.button(go, img)
        if 'layout' in node:
            L = node['layout']
            if L.get('type', 'horizontal') == 'grid':
                if not P.comp(go, 'GridLayoutGroup', required=False):
                    P.grid(go, L['cell'], L.get('spacing', (0, 0)), L.get('padding', (0, 0, 0, 0)), CHILD_ALIGN[L.get('align', 'upper-left')], L.get('count', 0))
            else:
                kind = 'VerticalLayoutGroup' if L['type'] == 'vertical' else 'HorizontalLayoutGroup'
                args = dict(pad=L.get('padding', (0, 0, 0, 0)), spacing=L.get('spacing', 0), align=CHILD_ALIGN[L.get('align', 'middle-center')],
                            ctrl=L.get('control', (0, 0)), expand=L.get('expand', (0, 0)))
                c = P.comp(go, kind, required=False)
                P.set_layout_group(c, **args) if c else P.layout_group(go, L['type'] == 'vertical', **args)
        if 'element' in node:
            E = node['element']
            mn, pf, fx = E.get('min', (-1, -1)), E.get('preferred', (-1, -1)), E.get('flexible', (-1, -1))
            args = dict(min_w=mn[0], min_h=mn[1], pref_w=pf[0], pref_h=pf[1], flex_w=fx[0], flex_h=fx[1])
            c = P.comp(go, 'LayoutElement', required=False)
            P.set_layout_element(c, **args) if c else P.layout_element(go, **args)
        for child_index, ch in enumerate(node.get('children', [])):
            self.node(ch, go, child_index if node.get('ordered_children') else None)
        if 'slider' in node:
            fill = P.find(P.path_of(go) + '/' + node['slider']['fill'])
            c = P.comp(go, 'Slider', required=False)
            if not c:
                P.slider(go, P.rt(fill), node['slider'].get('value', 0))
        for comp in node.get('components', []):
            guid = script_guid(comp['script'])
            c = P.comp(go, guid, required=False) or P.mono(go, guid, '')
            self.pending.append((c, go, comp.get('fields', {})))
        return go

    def resolve(self, value, ctx_go):
        P = self.P
        if isinstance(value, str) and value.startswith('@'):
            path, _, kind = value[1:].partition(':')
            go = P.find(path) if not path.startswith('./') else P.find(P.path_of(ctx_go) + path[1:])
            kind = kind or 'GameObject'
            if kind == 'GameObject':
                return '{fileID: %s}' % go
            if kind == 'RectTransform':
                return '{fileID: %s}' % P.rt(go)
            return '{fileID: %s}' % P.comp(go, GUIDS.get(kind) or script_guid(kind))
        if value is None:
            return '{fileID: 0}'
        if isinstance(value, (list, tuple)) and len(value) == 2:
            return v2(*value)
        if isinstance(value, str) and value.startswith('#'):
            return color(value)
        return fmt(value)

    def run(self):
        s = self.spec
        if 'root' in s:
            self.node(s['root'], None)
        if 'nodes' in s:
            parent = self.P.find(s['parent'])
            for n in s['nodes']:
                self.node(n, parent, n.get('index'))
        for target, fields in s.get('refs', {}).items():
            path, _, script = target.partition(':')
            go = self.P.find(path)
            self.pending.append((self.P.comp(go, script_guid(script)), go, fields))
        for c, go, fields in self.pending:
            for k, v in fields.items():
                self.P.set_or_add(c, k, self.resolve(v, go))
        for path in s.get('delete', []):
            self.P.delete_go(self.P.find(path))
        return self.P


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--spec', required=True)
    ap.add_argument('--dry-run', action='store_true', help='write to <prefab>.preview instead')
    a = ap.parse_args()
    spec = json.load(open(a.spec, encoding='utf-8'))
    new_file = not os.path.exists(spec['prefab'])
    P = Builder(spec).run()
    out = spec['prefab'] + ('.preview' if a.dry_run else '')
    P.save(out)
    if new_file and not a.dry_run and not os.path.exists(spec['prefab'] + '.meta'):
        g = hashlib.md5(('slime-ui:' + spec['prefab']).encode()).hexdigest()
        with open(spec['prefab'] + '.meta', 'w', encoding='utf-8', newline='\n') as f:
            f.write('fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % g)
    print('wrote', out, len(P.docs), 'objects')


if __name__ == '__main__':
    main()

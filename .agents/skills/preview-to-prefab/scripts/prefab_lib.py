"""Text-level editor for Unity UI prefab YAML. Keeps existing fileIDs; new objects get unique random ids."""
import glob, json, os, random, re

ID = re.compile(r'--- !u!(\d+) &(-?\d+)')
HEADER = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
GUIDS = {
    'Image': 'fe87c0e1cc204ed48ad3b37840f39efc', 'Button': '4e29b1a8efbd4b44bb3f3716e73f07ff',
    'TMP': 'f4688fdb7df04437aeb418b961361dc5', 'Slider': '67db9e8f0e2ae9c40bc1e2b64352a6b4',
    'HorizontalLayoutGroup': '30649d3a9faa99c48a7b1166b86bf2a0', 'VerticalLayoutGroup': '59f8146938fff824cb5fd77236b75775',
    'GridLayoutGroup': '8a8695521f0d02e499659fee002a26c2', 'LayoutElement': '306cc8c2b49d7114eaa3623786fc2126',
    'ContentSizeFitter': '3245ec927659c4140ac4f8d17403cc18', 'ScrollRect': '1aa08ab6e0800fa44ae55d278d1423e3',
    'RectMask2D': '3312d7739989d2b4e91e6319e9a96d76', 'CanvasScaler': '0cd44c1031e13a943bb63640046fad76',
    'GraphicRaycaster': 'dc42784cf147c0c48a680349fa168899',
}
HALIGN = {'left': 1, 'center': 2, 'right': 4, 'justified': 8}
VALIGN = {'top': 256, 'middle': 512, 'bottom': 1024}
CHILD_ALIGN = {'upper-left': 0, 'upper-center': 1, 'upper-right': 2, 'middle-left': 3, 'middle-center': 4, 'middle-right': 5,
               'lower-left': 6, 'lower-center': 7, 'lower-right': 8}


def fmt(v):
    if isinstance(v, bool):
        return str(int(v))
    if isinstance(v, float):
        s = ('%.7f' % v).rstrip('0').rstrip('.')
        return '0' if s in ('', '-0') else s
    return str(v)


def v2(x, y):
    return '{x: %s, y: %s}' % (fmt(x), fmt(y))


def color(c):
    if isinstance(c, str):
        h = c.lstrip('#')
        a = int(h[6:8], 16) / 255 if len(h) == 8 else 1.0
        c = (int(h[0:2], 16) / 255, int(h[2:4], 16) / 255, int(h[4:6], 16) / 255, a)
    return '{r: %s, g: %s, b: %s, a: %s}' % tuple(fmt(round(float(v), 7)) for v in c)


def script_guid(name):
    """GUID of a project MonoBehaviour script by class/file name."""
    for f in glob.glob('Assets/**/%s.cs.meta' % name, recursive=True):
        return re.search(r'guid: (\w+)', open(f, encoding='utf-8').read()).group(1)
    raise KeyError('script not found: ' + name)


def tmp_template():
    for f in glob.glob('Assets/**/*.prefab', recursive=True):
        t = open(f, encoding='utf-8').read()
        m = re.search(r'--- !u!114 &-?\d+\nMonoBehaviour:\n(?:.*\n)*?  m_Script: \{fileID: 11500000, guid: %s, type: 3\}\n(?:.*\n)*?(?=--- |\Z)' % GUIDS['TMP'], t)
        if m:
            body = m.group(0)
            return body[body.index('  m_Material:'):].rstrip('\n')
    raise RuntimeError('no TextMeshProUGUI found in project prefabs to use as template')


class Prefab:
    HEAD = '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'

    def __init__(self, path, seed=None, create=False):
        self.path = path
        if create and not os.path.exists(path):
            text = HEADER
        else:
            text = open(path, encoding='utf-8').read()
        parts = re.split(r'\n(?=--- !u!)', text)
        self.header = parts[0]
        self.order, self.docs = [], {}
        for d in parts[1:]:
            m = ID.match(d)
            self.order.append(m.group(2))
            self.docs[m.group(2)] = d.rstrip('\n')
        self.rng = random.Random(seed)
        self._tmp = None

    # ------------------------------------------------------------ basics
    def new_id(self):
        while True:
            i = str(self.rng.randrange(10 ** 17, 9 * 10 ** 18))
            if i not in self.docs:
                return i

    def add(self, i, text):
        assert i not in self.docs
        self.docs[i] = text.rstrip('\n')
        self.order.append(i)

    def kind(self, i):
        return ID.match(self.docs[i]).group(1)

    def get(self, i, key):
        m = re.search(r'\n  %s: (.*)' % re.escape(key), self.docs[i])
        return m.group(1) if m else None

    def has(self, i, key):
        return re.search(r'\n  %s:' % re.escape(key), self.docs[i]) is not None

    def set(self, i, key, value):
        d = self.docs[i] + '\n'
        pat = re.compile(r'(\n  %s:)( .*)?(?=\n)' % re.escape(key))
        if not pat.search(d):
            raise KeyError((i, key))
        self.docs[i] = pat.sub(lambda m: m.group(1) + ' ' + str(value), d, count=1).rstrip('\n')

    def set_or_add(self, i, key, value):
        if self.has(i, key):
            self.set(i, key, value)
        else:
            self.docs[i] += '\n  %s: %s' % (key, value)

    def set_block(self, i, key, lines):
        d = self.docs[i] + '\n  zz:'
        m = re.search(r'\n  %s:.*?(?=\n  [A-Za-z_])' % re.escape(key), d, re.S)
        body = '\n  %s:' % key + ('\n' + '\n'.join(lines) if lines else ' []')
        self.docs[i] = (d[:m.start()] + body + d[m.end():])[:-len('\n  zz:')]

    # ------------------------------------------------------------ hierarchy
    def go_of(self, comp):
        return re.search(r'm_GameObject: \{fileID: (-?\d+)\}', self.docs[comp]).group(1)

    def comps(self, go):
        return re.findall(r'component: \{fileID: (-?\d+)\}', self.docs[go])

    def rt(self, go):
        for c in self.comps(go):
            if self.kind(c) in ('224', '4'):
                return c

    def comp(self, go, guid_or_kind, required=True):
        guid = GUIDS.get(guid_or_kind, guid_or_kind)
        for c in self.comps(go):
            if guid in self.docs[c]:
                return c
        if required:
            raise KeyError((self.name(go), guid_or_kind))

    def children(self, rt):
        blk = self.docs[rt].split('m_Children:')[1].split('m_Father')[0]
        return re.findall(r'\{fileID: (-?\d+)\}', blk)

    def child_gos(self, go):
        return [self.go_of(c) for c in self.children(self.rt(go))]

    def father(self, rt):
        return re.search(r'm_Father: \{fileID: (-?\d+)\}', self.docs[rt]).group(1)

    def set_children(self, rt, ch):
        self.set_block(rt, 'm_Children', ['  - {fileID: %s}' % c for c in ch])

    def name(self, go):
        n = self.get(go, 'm_Name')
        return json.loads(n) if n and n.startswith('"') else n

    def rename(self, go, n):
        self.set(go, 'm_Name', n if re.fullmatch(r'[\w .+-]+', n) else json.dumps(n))

    def roots(self):
        return [self.go_of(i) for i in self.order if self.kind(i) == '224' and self.father(i) == '0']

    def find(self, path):
        """'Root/child/grandchild' by GameObject names (first match)."""
        names = [p for p in path.split('/') if p]
        cur = [g for g in self.roots() if self.name(g) == names[0]]
        if not cur:
            raise KeyError(path)
        go = cur[0]
        for n in names[1:]:
            nxt = [c for c in self.child_gos(go) if self.name(c) == n]
            if not nxt:
                raise KeyError(path)
            go = nxt[0]
        return go

    def path_of(self, go):
        names = []
        while go:
            names.append(self.name(go))
            f = self.father(self.rt(go))
            go = self.go_of(f) if f != '0' else None
        return '/'.join(reversed(names))

    def reparent(self, go, new_parent_go, index=None):
        r = self.rt(go)
        old = self.father(r)
        if old != '0':
            self.set_children(old, [c for c in self.children(old) if c != r])
        np_ = self.rt(new_parent_go)
        ch = [c for c in self.children(np_) if c != r]
        ch.insert(len(ch) if index is None else index, r)
        self.set_children(np_, ch)
        self.set(r, 'm_Father', '{fileID: %s}' % np_)

    def delete_go(self, go):
        r = self.rt(go)
        for ch in self.children(r):
            self.delete_go(self.go_of(ch))
        par = self.father(r)
        if par != '0' and par in self.docs:
            self.set_children(par, [c for c in self.children(par) if c != r])
        for c in self.comps(go):
            self._drop(c)
        self._drop(go)

    def _drop(self, i):
        del self.docs[i]
        self.order.remove(i)

    def remove_comp(self, go, c):
        self.docs[go] = re.sub(r'\n  - component: \{fileID: %s\}' % c, '', self.docs[go])
        self._drop(c)

    def add_comp_ref(self, go, c):
        d = self.docs[go]
        last = list(re.finditer(r'\n  - component: \{fileID: -?\d+\}', d))[-1]
        self.docs[go] = d[:last.end()] + '\n  - component: {fileID: %s}' % c + d[last.end():]

    def layout(self, go, amin, amax, pos, size, pivot=(0.5, 0.5)):
        r = self.rt(go)
        self.set(r, 'm_AnchorMin', v2(*amin)); self.set(r, 'm_AnchorMax', v2(*amax))
        self.set(r, 'm_AnchoredPosition', v2(*pos)); self.set(r, 'm_SizeDelta', v2(*size)); self.set(r, 'm_Pivot', v2(*pivot))

    # ------------------------------------------------------------ creation
    def new_go(self, name, parent_go, index=None, amin=(0.5, 0.5), amax=(0.5, 0.5), pos=(0, 0), size=(100, 100), pivot=(0.5, 0.5), renderer=True, active=1):
        go, r = self.new_id(), self.new_id()
        self.add(go, '--- !u!1 &%s\nGameObject:\n%s  serializedVersion: 6\n  m_Component:\n  - component: {fileID: %s}\n  m_Layer: 5\n  m_Name: %s\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: %d' % (go, self.HEAD, r, name, int(active)))
        pr = self.rt(parent_go) if parent_go else '0'
        self.add(r, '--- !u!224 &%s\nRectTransform:\n%s  m_GameObject: {fileID: %s}\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {fileID: %s}\n  m_RootOrder: 0\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n  m_AnchorMin: %s\n  m_AnchorMax: %s\n  m_AnchoredPosition: %s\n  m_SizeDelta: %s\n  m_Pivot: %s' % (r, self.HEAD, go, pr, v2(*amin), v2(*amax), v2(*pos), v2(*size), v2(*pivot)))
        if parent_go:
            ch = self.children(pr)
            ch.insert(len(ch) if index is None else index, r)
            self.set_children(pr, ch)
        if renderer:
            self.ensure_renderer(go)
        return go

    def ensure_renderer(self, go):
        for c in self.comps(go):
            if self.kind(c) == '222':
                return c
        c = self.new_id()
        self.add(c, '--- !u!222 &%s\nCanvasRenderer:\n%s  m_GameObject: {fileID: %s}\n  m_CullTransparentMesh: 1' % (c, self.HEAD, go))
        self.add_comp_ref(go, c)
        return c

    def mono(self, go, guid, body):
        c = self.new_id()
        self.add(c, '--- !u!114 &%s\nMonoBehaviour:\n%s  m_GameObject: {fileID: %s}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name:\n  m_EditorClassIdentifier:%s' % (c, self.HEAD, go, GUIDS.get(guid, guid), ('\n' + body.rstrip('\n')) if body else ''))
        self.add_comp_ref(go, c)
        return c

    def image(self, go, sprite=None, col=(1, 1, 1, 1), type_=0, preserve=0, raycast=0, ppu=1):
        self.ensure_renderer(go)
        sp = '{fileID: 21300000, guid: %s, type: 3}' % sprite if sprite else '{fileID: 0}'
        return self.mono(go, 'Image', '''  m_Material: {fileID: 0}
  m_Color: %s
  m_RaycastTarget: %d
  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}
  m_Maskable: 1
  m_OnCullStateChanged:
    m_PersistentCalls:
      m_Calls: []
  m_Sprite: %s
  m_Type: %d
  m_PreserveAspect: %d
  m_FillCenter: 1
  m_FillMethod: 4
  m_FillAmount: 1
  m_FillClockwise: 1
  m_FillOrigin: 0
  m_UseSpriteMesh: 0
  m_PixelsPerUnitMultiplier: %s''' % (color(col), int(raycast), sp, type_, int(preserve), fmt(ppu)))

    def set_image(self, c, sprite=None, col=None, type_=None, preserve=None, raycast=None, ppu=None, enabled=None):
        if sprite is not None:
            self.set(c, 'm_Sprite', '{fileID: 21300000, guid: %s, type: 3}' % sprite if sprite else '{fileID: 0}')
        if col is not None: self.set(c, 'm_Color', color(col))
        if type_ is not None: self.set(c, 'm_Type', type_)
        if preserve is not None: self.set(c, 'm_PreserveAspect', int(preserve))
        if raycast is not None: self.set(c, 'm_RaycastTarget', int(raycast))
        if ppu is not None: self.set(c, 'm_PixelsPerUnitMultiplier', fmt(ppu))
        if enabled is not None: self.set(c, 'm_Enabled', int(enabled))

    BUTTON_COLORS = '''    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}
    m_HighlightedColor: {r: 0.96, g: 0.96, b: 0.96, a: 1}
    m_PressedColor: {r: 0.78, g: 0.78, b: 0.78, a: 1}
    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}
    m_DisabledColor: {r: 0.78, g: 0.78, b: 0.78, a: 0.5}
    m_ColorMultiplier: 1
    m_FadeDuration: 0.1'''

    def _selectable(self, target, interactable=1):
        return '''  m_Navigation:
    m_Mode: 0
    m_WrapAround: 0
    m_SelectOnUp: {fileID: 0}
    m_SelectOnDown: {fileID: 0}
    m_SelectOnLeft: {fileID: 0}
    m_SelectOnRight: {fileID: 0}
  m_Transition: 1
  m_Colors:
%s
  m_SpriteState:
    m_HighlightedSprite: {fileID: 0}
    m_PressedSprite: {fileID: 0}
    m_SelectedSprite: {fileID: 0}
    m_DisabledSprite: {fileID: 0}
  m_AnimationTriggers:
    m_NormalTrigger: Normal
    m_HighlightedTrigger: Highlighted
    m_PressedTrigger: Pressed
    m_SelectedTrigger: Selected
    m_DisabledTrigger: Disabled
  m_Interactable: %d
  m_TargetGraphic: {fileID: %s}''' % (self.BUTTON_COLORS, interactable, target or 0)

    def button(self, go, target):
        return self.mono(go, 'Button', self._selectable(target) + '\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []')

    def normalize_button(self, c, target=None):
        d = self.docs[c]
        d = re.sub(r'    m_Mode: \d+', '    m_Mode: 0', d, count=1)
        d = re.sub(r'  m_Colors:\n(    .*\n)+?(?=  m_SpriteState)', '  m_Colors:\n' + self.BUTTON_COLORS + '\n', d)
        self.docs[c] = d
        if target:
            self.set(c, 'm_TargetGraphic', '{fileID: %s}' % target)

    def slider(self, go, fill_rt, value):
        return self.mono(go, 'Slider', self._selectable(0, interactable=0) + '''
  m_FillRect: {fileID: %s}
  m_HandleRect: {fileID: 0}
  m_Direction: 0
  m_MinValue: 0
  m_MaxValue: 1
  m_WholeNumbers: 0
  m_Value: %s
  m_OnValueChanged:
    m_PersistentCalls:
      m_Calls: []''' % (fill_rt, fmt(value)))

    @staticmethod
    def _lg(pad, spacing, align, ctrl, expand):
        return '''  m_Padding:
    m_Left: %d
    m_Right: %d
    m_Top: %d
    m_Bottom: %d
  m_ChildAlignment: %d
  m_Spacing: %s
  m_ChildForceExpandWidth: %d
  m_ChildForceExpandHeight: %d
  m_ChildControlWidth: %d
  m_ChildControlHeight: %d
  m_ChildScaleWidth: 0
  m_ChildScaleHeight: 0
  m_ReverseArrangement: 0''' % (tuple(pad) + (align, fmt(spacing)) + tuple(int(v) for v in expand) + tuple(int(v) for v in ctrl))

    def layout_group(self, go, vertical, pad=(0, 0, 0, 0), spacing=0, align=0, ctrl=(0, 0), expand=(0, 0)):
        return self.mono(go, 'VerticalLayoutGroup' if vertical else 'HorizontalLayoutGroup', self._lg(pad, spacing, align, ctrl, expand))

    def set_layout_group(self, c, pad=(0, 0, 0, 0), spacing=0, align=0, ctrl=(0, 0), expand=(0, 0)):
        d = self.docs[c]
        self.docs[c] = d[:d.index('  m_Padding:')] + self._lg(pad, spacing, align, ctrl, expand)

    def grid(self, go, cell, spacing=(0, 0), pad=(0, 0, 0, 0), align=0, count=0):
        return self.mono(go, 'GridLayoutGroup', '''  m_Padding:
    m_Left: %d
    m_Right: %d
    m_Top: %d
    m_Bottom: %d
  m_ChildAlignment: %d
  m_StartCorner: 0
  m_StartAxis: 0
  m_CellSize: %s
  m_Spacing: %s
  m_Constraint: %d
  m_ConstraintCount: %d''' % (tuple(pad) + (align, v2(*cell), v2(*spacing), 1 if count else 0, count or 2)))

    @staticmethod
    def _le(min_w=-1, min_h=-1, pref_w=-1, pref_h=-1, flex_w=-1, flex_h=-1):
        return '''  m_IgnoreLayout: 0
  m_MinWidth: %s
  m_MinHeight: %s
  m_PreferredWidth: %s
  m_PreferredHeight: %s
  m_FlexibleWidth: %s
  m_FlexibleHeight: %s
  m_LayoutPriority: 1''' % tuple(fmt(v) for v in (min_w, min_h, pref_w, pref_h, flex_w, flex_h))

    def layout_element(self, go, **kw):
        return self.mono(go, 'LayoutElement', self._le(**kw))

    def set_layout_element(self, c, **kw):
        d = self.docs[c]
        self.docs[c] = d[:d.index('  m_IgnoreLayout:')] + self._le(**kw)

    def tmp(self, go, text, size, col, h_align=2, v_align=512, wrap=0, bold=False, raycast=0):
        if self._tmp is None:
            self._tmp = tmp_template()
        self.ensure_renderer(go)
        c = self.mono(go, 'TMP', self._tmp)
        self.set_tmp(c, text=text, size=size, col=col, h_align=h_align, v_align=v_align, wrap=wrap, bold=bold, raycast=raycast)
        return c

    def set_tmp(self, c, text=None, size=None, col=None, h_align=None, v_align=None, wrap=None, bold=None, raycast=None):
        if text is not None: self.set(c, 'm_text', json.dumps(text, ensure_ascii=True))
        if size is not None:
            for k in ('m_fontSize', 'm_fontSizeBase', 'm_fontSizeMax'):
                self.set(c, k, fmt(size))
            self.set(c, 'm_enableAutoSizing', 0)
        if col is not None:
            self.set(c, 'm_Color', color(col)); self.set(c, 'm_fontColor', color(col))
            r, g, b, a = [int(round(float(v) * 255)) for v in re.findall(r'[\d.]+', color(col))]
            self.docs[c] = re.sub(r'(  m_fontColor32:\n    serializedVersion: 2\n    rgba: )\d+', lambda m: m.group(1) + str(r | g << 8 | b << 16 | a << 24), self.docs[c])
        if h_align is not None: self.set(c, 'm_HorizontalAlignment', h_align)
        if v_align is not None: self.set(c, 'm_VerticalAlignment', v_align)
        if wrap is not None:
            self.set(c, 'm_enableWordWrapping', int(wrap)); self.set(c, 'm_overflowMode', 0)
        if bold is not None: self.set(c, 'm_fontStyle', 1 if bold else 0)
        if raycast is not None: self.set(c, 'm_RaycastTarget', int(raycast))

    # ------------------------------------------------------------ save
    def fix_root_order(self):
        for i in list(self.docs):
            if self.kind(i) == '224':
                for idx, c in enumerate(self.children(i)):
                    self.set(c, 'm_RootOrder', idx)

    def save(self, path=None):
        self.fix_root_order()
        out = self.header.rstrip('\n') + '\n' + '\n'.join(self.docs[i] for i in self.order) + '\n'
        with open(path or self.path, 'w', encoding='utf-8', newline='\n') as f:
            f.write(out)

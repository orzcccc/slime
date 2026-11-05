import os, re, struct, json, sys
sys.stdout.reconfigure(encoding='utf-8')
d = r'Assets/Resources/icon'
for f in sorted(os.listdir(d)):
    if f.endswith('.png'):
        b = open(os.path.join(d, f), 'rb').read(32)
        w, h = struct.unpack('>II', b[16:24])
        m = open(os.path.join(d, f + '.meta'), encoding='utf-8').read()
        g = re.search(r'guid: (\w+)', m).group(1)
        tt = re.search(r'textureType: (\d+)', m)
        sm = re.search(r'spriteMode: (\d+)', m)
        bd = re.search(r'spriteBorder: (.*)', m)
        print(f, w, h, g, 'tt', tt and tt.group(1), 'sm', sm and sm.group(1), 'bd', bd and bd.group(1))

# prefab hierarchy
p = open('Assets/Resources/UI/MainUI.prefab', encoding='utf-8').read()
docs = re.split(r'^--- !u!(\d+) &(\d+).*$', p, flags=re.M)
objs = {}
for i in range(1, len(docs), 3):
    objs[docs[i+1]] = (docs[i], docs[i+2])
names = {}; rt_go = {}; children = {}; go_comps = {}
for fid, (t, body) in objs.items():
    if t == '1':
        n = re.search(r'm_Name: (.*)', body).group(1).strip()
        if n.startswith('"'): n = json.loads(n)
        names[fid] = n
        go_comps[fid] = re.findall(r'component: \{fileID: (\d+)\}', body)
    if t == '224':
        rt_go[fid] = re.search(r'm_GameObject: \{fileID: (\d+)\}', body).group(1)
        ch = re.search(r'm_Children:(.*?)m_Father', body, re.S).group(1)
        children[fid] = re.findall(r'fileID: (\d+)', ch)
        children[fid+'_f'] = re.search(r'm_Father: \{fileID: (\d+)\}', body).group(1)
def info(go):
    s = []
    for c in go_comps[go]:
        t, b = objs[c]
        if t == '224':
            a = re.search(r'm_AnchorMin: (.*)\n.*m_AnchorMax: (.*)\n.*m_AnchoredPosition: (.*)\n.*m_SizeDelta: (.*)', b)
            s.append('RT ' + ' '.join(a.groups()))
        if t == '114':
            sc = re.search(r'm_Script: \{fileID: \d+, guid: (\w+)', b).group(1)[:6]
            sp = re.search(r'm_Sprite: \{fileID: \d+(?:, guid: (\w+))?', b)
            tx = re.search(r'm_Text: (.*)', b)
            extra = ''
            if sp: extra += ' sprite=' + str(sp.group(1))[:8]
            if tx:
                v = tx.group(1).strip()
                try: v = json.loads(v) if v.startswith('"') else v
                except Exception: pass
                extra += ' text=' + repr(v)[:30]
            s.append(sc + extra)
    return ' | '.join(s)
def walk(rt, depth):
    go = rt_go[rt]
    print('  ' * depth + names[go] + '  [' + info(go) + ']')
    for c in children[rt]: walk(c, depth + 1)
for rt in rt_go:
    if children[rt+'_f'] == '0': walk(rt, 0)

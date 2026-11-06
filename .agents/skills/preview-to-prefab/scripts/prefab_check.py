"""Validate Unity prefab YAML written by hand or by prefab_build.py.

  python prefab_check.py Assets/Resources/UI/MainUI.prefab [more.prefab ...] [--tree]

Checks: duplicate fileIDs, dangling local references, GameObject<->component back links,
m_Children/m_Father consistency, m_RootOrder order, and that every external GUID has a .meta.
Exit code 1 when anything is wrong.
"""
import argparse, glob, os, re, sys

ID = re.compile(r'--- !u!(\d+) &(-?\d+)')
BUILTIN = {'0000000000000000f000000000000000', '0000000000000000e000000000000000'}


def known_guids():
    g = {}
    for pat in ('Assets/**/*.meta', 'Packages/**/*.meta', 'Library/PackageCache/**/*.meta'):
        for f in glob.glob(pat, recursive=True):
            try:
                m = re.search(r'guid: (\w{32})', open(f, encoding='utf-8', errors='ignore').read(2000))
                if m:
                    g[m.group(1)] = f
            except OSError:
                pass
    return g


def check(path, guids, tree=False):
    t = open(path, encoding='utf-8').read()
    objs, problems = {}, []
    for d in re.split(r'\n(?=--- !u!)', t)[1:]:
        m = ID.match(d)
        if m.group(2) in objs:
            problems.append('duplicate fileID ' + m.group(2))
        objs[m.group(2)] = (m.group(1), d)
    for k, (ty, d) in objs.items():
        for ref in re.findall(r'\{fileID: (-?\d+)\}', d):
            if ref != '0' and ref not in objs:
                problems.append('dangling ref %s in !u!%s &%s' % (ref, ty, k))
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', d)
        if go and go.group(1) in objs and 'component: {fileID: %s}' % k not in objs[go.group(1)][1]:
            problems.append('GameObject %s does not list component %s' % (go.group(1), k))
        if ty == '224':
            ch = re.findall(r'\{fileID: (-?\d+)\}', d.split('m_Children:')[1].split('m_Father')[0])
            for i, c in enumerate(ch):
                if c not in objs:
                    continue
                cd = objs[c][1]
                if 'm_Father: {fileID: %s}' % k not in cd:
                    problems.append('child %s of %s points to another father' % (c, k))
                ro = re.search(r'm_RootOrder: (-?\d+)', cd)
                if ro and int(ro.group(1)) != i:
                    problems.append('m_RootOrder of %s is %s, expected %d' % (c, ro.group(1), i))
            fa = re.search(r'm_Father: \{fileID: (-?\d+)\}', d).group(1)
            if fa != '0' and fa in objs and k not in objs[fa][1].split('m_Children:')[1].split('m_Father')[0]:
                problems.append('father %s does not list child %s' % (fa, k))
    missing = sorted({g for g in re.findall(r'guid: (\w{32})', t) if g not in guids and g not in BUILTIN})
    for g in missing:
        problems.append('missing asset/script GUID %s (x%d)' % (g, t.count(g)))
    print('%s: %d objects, %d problems' % (path, len(objs), len(problems)))
    for p in problems[:60]:
        print('  ' + p)
    if tree:
        print_tree(objs)
    return not problems


def print_tree(objs):
    def name(go):
        return re.search(r'm_Name: (.*)', objs[go][1]).group(1)
    def walk(rt, depth):
        go = re.search(r'm_GameObject: \{fileID: (-?\d+)\}', objs[rt][1]).group(1)
        d = objs[rt][1]
        f = lambda key: re.search(key + r': \{x: ([^,]+), y: ([^}]+)\}', d).groups()
        print('  ' * depth + '%s  amin(%s,%s) amax(%s,%s) pos(%s,%s) size(%s,%s)' % ((name(go),) + f('m_AnchorMin') + f('m_AnchorMax') + f('m_AnchoredPosition') + f('m_SizeDelta')))
        for c in re.findall(r'\{fileID: (-?\d+)\}', d.split('m_Children:')[1].split('m_Father')[0]):
            if c in objs:
                walk(c, depth + 1)
    for k, (ty, d) in objs.items():
        if ty == '224' and 'm_Father: {fileID: 0}' in d:
            walk(k, 0)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('prefabs', nargs='+')
    ap.add_argument('--tree', action='store_true')
    a = ap.parse_args()
    guids = known_guids()
    ok = all([check(p, guids, a.tree) for p in a.prefabs])
    sys.exit(0 if ok else 1)

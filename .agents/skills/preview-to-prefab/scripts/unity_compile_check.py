"""Compile the project's C# assemblies the way Unity splits them, without opening the Editor.

  python unity_compile_check.py [--unity "C:/Program Files/Unity/Hub/Editor/<ver>/Editor"] [--only Assembly-CSharp]

Uses the compiler options/references from Unity's last build (Library/Bee/artifacts/*/<Assembly>.rsp),
re-collects source files (new files are included), compiles project assemblies in dependency order and
swaps references to project assemblies for the freshly compiled ones. Unity must have compiled once.
"""
import argparse, glob, json, os, re, subprocess, sys

OUT = '.codex_tmp/compile_check'


def serialized_cs0649(msg):
    """Unity assigns [SerializeField] fields itself and does not report CS0649 for them."""
    m = re.match(r'(.+?)\((\d+),\d+\): warning CS0649', msg)
    if not m:
        return False
    try:
        lines = open(m.group(1), encoding='utf-8-sig').read().splitlines()
        n = int(m.group(2)) - 1
        return 'SerializeField' in lines[n] or (n > 0 and 'SerializeField' in lines[n - 1])
    except OSError:
        return False


def asmdefs():
    res = {}
    for f in glob.glob('Assets/**/*.asmdef', recursive=True):
        res[json.load(open(f, encoding='utf-8-sig'))['name']] = os.path.dirname(f).replace('\\', '/')
    return res


def sources(name, defs):
    dirs = sorted(defs.values(), key=len, reverse=True)
    def owner(path):
        p = path.replace('\\', '/')
        for d in dirs:
            if p.startswith(d + '/'):
                return d
    files = [f.replace('\\', '/') for f in glob.glob('Assets/**/*.cs', recursive=True)]
    if name in defs:
        return [f for f in files if owner(f) == defs[name]]
    editor = lambda f: '/Editor/' in '/' + f
    loose = [f for f in files if owner(f) is None]
    return [f for f in loose if editor(f) == (name == 'Assembly-CSharp-Editor')]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--unity')
    ap.add_argument('--only')
    a = ap.parse_args()
    rsps = sorted(glob.glob('Library/Bee/artifacts/**/Assembly-CSharp.rsp', recursive=True), key=os.path.getmtime)
    if not rsps:
        sys.exit('no Assembly-CSharp.rsp under Library/Bee; open the project in Unity once')
    dag = os.path.dirname(rsps[-1])
    defs = asmdefs()
    names = [n for n in list(defs) + ['Assembly-CSharp', 'Assembly-CSharp-Editor'] if os.path.exists(os.path.join(dag, n + '.rsp'))]
    if a.only:
        names = [n for n in names if n in a.only.split(',')] + [n for n in names if n not in a.only.split(',')]
    ver = re.search(r'm_EditorVersion: (\S+)', open('ProjectSettings/ProjectVersion.txt').read()).group(1)
    editor = a.unity or 'C:/Program Files/Unity/Hub/Editor/%s/Editor' % ver
    dotnet, csc = os.path.join(editor, 'Data/NetCoreRuntime/dotnet.exe'), os.path.join(editor, 'Data/DotNetSdkRoslyn/csc.dll')
    os.makedirs(OUT, exist_ok=True)
    opts = {n: [l for l in open(os.path.join(dag, n + '.rsp'), encoding='utf-8-sig').read().splitlines()
                if l.startswith(('-r:', '-define:', '-langversion', '-nostdlib', '-target', '-unsafe', '-deterministic', '-nowarn', '-warn', '-nullable'))]
            for n in names}
    deps = {n: [m for m in names if m != n and any(re.search(r'[\\/"]%s\.dll' % re.escape(m), l) for l in opts[n])] for n in names}
    done, failed, report = {}, False, []
    while len(done) < len(names):
        ready = [n for n in names if n not in done and all(d in done for d in deps[n])]
        if not ready:
            ready = [n for n in names if n not in done][:1]
        for n in ready:
            src = sources(n, defs)
            if not src:
                done[n] = None
                continue
            lines = []
            for l in opts[n]:
                m = next((d for d in deps[n] if done.get(d) and re.search(r'[\\/"]%s\.dll' % re.escape(d), l)), None)
                lines.append('-r:"%s"' % done[m] if m else l)
            out_dll = os.path.abspath(os.path.join(OUT, n + '.dll'))
            rsp = os.path.join(OUT, n + '.rsp')
            open(rsp, 'w', encoding='utf-8').write('\n'.join(lines + ['-out:"%s"' % out_dll] + ['"%s"' % s for s in src]))
            r = subprocess.run([dotnet, 'exec', csc, '/nologo', '@' + rsp], capture_output=True)
            text = (r.stdout + r.stderr).decode('utf-8', 'replace')
            msgs = [l for l in text.splitlines() if (': error ' in l or ': warning ' in l) and not serialized_cs0649(l)]
            report.append('%-32s %3d files  %s' % (n, len(src), 'ok' if r.returncode == 0 else 'FAILED'))
            report += ['    ' + m for m in msgs[:40]]
            failed |= r.returncode != 0
            done[n] = out_dll if r.returncode == 0 else None
    print('\n'.join(report))
    sys.exit(1 if failed else 0)


if __name__ == '__main__':
    main()

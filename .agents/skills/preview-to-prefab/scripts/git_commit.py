"""Commit an explicit file list with a UTF-8 message file and push to origin main (never force).

  python git_commit.py --message msg.txt path1 path2 ... [--no-push]
"""
import argparse, subprocess, sys


def git(*args):
    r = subprocess.run(['git', '-c', 'core.quotepath=false', *args], capture_output=True, text=True, encoding='utf-8', errors='replace')
    out = r.stdout + '\n'.join(l for l in r.stderr.splitlines() if 'LF will be replaced' not in l)
    print(out.strip())
    return r.returncode


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--message', required=True)
    ap.add_argument('--no-push', action='store_true')
    ap.add_argument('paths', nargs='+')
    a = ap.parse_args()
    if git('add', '-A', '--', *a.paths):
        sys.exit(1)
    git('diff', '--cached', '--stat')
    if git('commit', '-F', a.message):
        sys.exit(1)
    if not a.no_push and git('push', 'origin', 'main'):
        sys.exit('push failed; do not force push, report the reason to the user')

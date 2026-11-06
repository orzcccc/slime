"""Commit ONLY the given paths with a UTF-8 message file and push to origin main (never force).

  python git_commit.py --message msg.txt path1 path2 ... [--no-push]

Uses 'git commit --only', so changes other people already staged stay staged and are not
swept into this commit. Whole files are committed; to commit part of a file, stage it yourself
(e.g. a blob via 'git update-index --cacheinfo') and leave it out of the path list.
"""
import argparse, subprocess, sys


def git(*args, quiet=False):
    r = subprocess.run(['git', '-c', 'core.quotepath=false', *args], capture_output=True, text=True, encoding='utf-8', errors='replace')
    out = (r.stdout + '\n'.join(l for l in r.stderr.splitlines() if 'LF will be replaced' not in l)).strip()
    if out and not quiet:
        print(out)
    return r.returncode, r.stdout


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--message', required=True)
    ap.add_argument('--no-push', action='store_true')
    ap.add_argument('paths', nargs='+')
    a = ap.parse_args()
    _, staged = git('diff', '--cached', '--name-only', quiet=True)
    if staged.strip():
        print('note: other staged changes exist and will NOT be committed:\n  ' + '\n  '.join(staged.split()))
    if git('add', '-A', '--', *a.paths)[0]:
        sys.exit(1)
    if git('commit', '--only', '-F', a.message, '--', *a.paths)[0]:
        sys.exit(1)
    git('show', '--stat', '--format=%h %s', 'HEAD')
    if not a.no_push and git('push', 'origin', 'main')[0]:
        sys.exit('push failed; do not force push, report the reason to the user')

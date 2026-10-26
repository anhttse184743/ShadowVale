import sys
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
root = Path(__file__).resolve().parents[2] / 'Docs/Reviews/Map02_Audit_20261026/source/Assets/_Project/Map01'
for spec in sys.argv[1:]:
    name, _, limits = spec.partition(':')
    p = root / name
    lines = p.read_text(encoding='utf-8-sig').splitlines()
    a, b = (map(int, limits.split('-')) if limits else (1, len(lines)))
    print(f'FILE {name} ({len(lines)} lines)')
    for n in range(a, min(b,len(lines))+1):
        print(f'{n:4} {lines[n-1]}')

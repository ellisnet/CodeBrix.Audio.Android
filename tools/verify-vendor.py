"""Check vendored sources against the recorded, patched source manifest."""
from pathlib import Path
import hashlib

native = Path(__file__).resolve().parents[1] / 'native'
expected = set()
for line in (native / 'VENDOR-SHA256.txt').read_text().splitlines():
    digest, name = line.split('  ', 1)
    path = native / name
    expected.add(path)
    if hashlib.sha256(path.read_bytes()).hexdigest() != digest:
        raise SystemExit(f'Vendor hash mismatch: {name}; update provenance and patches for intentional changes.')
actual = {p for p in (native / 'vendor').rglob('*') if p.is_file()}
if actual != expected:
    raise SystemExit(f'Vendor file list mismatch: {actual ^ expected}')
print(f'Verified {len(expected)} vendored source files.')

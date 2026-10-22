"""Read-only handoff audit. Run after git lfs pull; only Python standard library required."""
import hashlib
import json
import re
import subprocess
from pathlib import Path
from urllib.parse import unquote

root = Path(__file__).resolve().parents[1]
repo = root.parents[1]
errors = []
def digest(path):
    h = hashlib.sha256()
    with path.open('rb') as source:
        for chunk in iter(lambda: source.read(1024*1024), b''):
            h.update(chunk)
    return h.hexdigest()
def check(path, expected, size=None):
    if not path.is_file():
        errors.append(f'Missing: {path.relative_to(repo)}'); return
    if size is not None and path.stat().st_size != size:
        errors.append(f'Wrong size: {path.relative_to(repo)}')
    if digest(path).lower() != expected.lower():
        errors.append(f'SHA256 mismatch: {path.relative_to(repo)}')

snapshot = json.loads((root/'snapshot.json').read_text(encoding='utf-8-sig'))
for name, record in snapshot['Package'].items():
    check(root/'package'/name, record['Sha256'], record['Bytes'])
check(root/'assets/owner-ui.psd',snapshot['Assets']['PsdSha256'])
layers=json.loads((root/'assets/psd-layers.json').read_text(encoding='utf-8-sig'))
if len(layers['Layers'])!=snapshot['Assets']['PsdLayers']:
    errors.append('PSD layer count differs')
images=json.loads((root/'assets/chat-images.json').read_text(encoding='utf-8-sig'))
if len(images)!=snapshot['Assets']['RequestedImages']:
    errors.append('Screenshot inventory count differs')
for record in images:
    if not record['Available']:
        errors.append(f"Unavailable image: {record['OriginalName']}");continue
    check(root/record['File'],record['Sha256'],record['Bytes'])
source_index=json.loads((root/'source-index.json').read_text(encoding='utf-8'))
for record in source_index['Files']:
    path=repo/record['Path']
    if not path.is_file():errors.append(f"Missing source: {record['Path']}");continue
    data=path.read_bytes()
    if record.get('HashMode')=='Utf8LF':data=data.replace(b'\r\n',b'\n')
    if len(data)!=record['Bytes'] or hashlib.sha256(data).hexdigest()!=record['Sha256']:
        errors.append(f"Source snapshot differs: {record['Path']}")

for document in root.rglob('*.md'):
    text=document.read_text(encoding='utf-8-sig')
    for match in re.finditer(r'\]\((<[^>]+>|[^\s)]+)\)', text):
        link=match.group(1).strip('<>')
        if re.match(r'^[a-zA-Z][a-zA-Z0-9+.-]*:',link) or link.startswith('#'):continue
        target=unquote(link.split('#',1)[0])
        if target and not (document.parent/target).exists():
            errors.append(f'Broken link in {document.relative_to(repo)}: {target}')

manifest_path=root/'HANDOFF_MANIFEST.json'
if not manifest_path.is_file():
    errors.append('Missing HANDOFF_MANIFEST.json')
else:
    manifest=json.loads(manifest_path.read_text(encoding='utf-8'))
    for record in manifest['Files']:
        check(root/record['Path'],record['Sha256'],record['Bytes'])
    actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file() and not any(x in {'bin','obj','__pycache__'} for x in p.relative_to(root).parts)}
    listed={r['Path'] for r in manifest['Files']}|{'HANDOFF_MANIFEST.json'}
    if actual!=listed:errors.append(f'Manifest coverage differs: {sorted(actual^listed)}')

for path in root.rglob('*'):
    if not path.is_file() or any(x in {'bin','obj','__pycache__'} for x in path.parts):continue
    if path.suffix.lower() in {'.pk8','.pfx','.key'} or path.name in {'.env','.git-credentials'}:
        errors.append(f'Forbidden credential file: {path.relative_to(repo)}')
    if path.suffix.lower() in {'.md','.json','.ps1','.py','.cs','.txt','.pem'}:
        value=path.read_text(encoding='utf-8-sig',errors='replace')
        if re.search(r'-----BEGIN (?:EC |RSA |ENCRYPTED )?PRIVATE KEY-----',value):
            errors.append(f'Private key content: {path.relative_to(repo)}')

if errors:
    raise SystemExit('\n'.join(errors))
print(f"PASS handoff: {len(manifest['Files'])} files hashed; {len(images)} images; local links; package; PSD; no private key files")

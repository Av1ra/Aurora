"""Recompute inventories after editing handoff. Review changes before committing."""
import hashlib
import json
import subprocess
from pathlib import Path

root=Path(__file__).resolve().parents[1]
repo=root.parents[1]
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for chunk in iter(lambda:f.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def write(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
names=subprocess.check_output(['git','ls-files','-z'],cwd=repo).decode().split('\0')
records=[]
for name in sorted(n for n in names if n and not n.startswith('handoff/') and n!='.gitattributes'):
    p=repo/name
    if not p.is_file():continue
    data=p.read_bytes()
    mode='Raw'
    try:
        data.decode('utf-8')
        if b'\0' not in data:
            data=data.replace(b'\r\n',b'\n');mode='Utf8LF'
    except UnicodeDecodeError:pass
    row={'Path':name,'HashMode':mode,'Bytes':len(data),'Sha256':hashlib.sha256(data).hexdigest()}
    if p.suffix.lower() in {'.cs','.cpp','.h','.js','.mjs','.html','.py','.ps1','.cmd','.md','.json','.yml','.xaml','.csproj','.txt'}:
        row['Lines']=len(p.read_text(encoding='utf-8-sig',errors='replace').splitlines())
    records.append(row)
write(root/'source-index.json',{'Schema':1,'Scope':'Aurora tracked tree outside handoff; UTF-8 text canonicalized to LF for cross-platform checkout, binaries raw','Files':records})
files=[]
for p in sorted(root.rglob('*')):
    rel=p.relative_to(root)
    if not p.is_file() or rel.as_posix()=='HANDOFF_MANIFEST.json' or any(x in {'bin','obj','__pycache__'} for x in rel.parts):continue
    files.append({'Path':rel.as_posix(),'Bytes':p.stat().st_size,'Sha256':sha(p)})
write(root/'HANDOFF_MANIFEST.json',{'Schema':1,'Hash':'SHA256','Files':files})
print(f'Inventory written: {len(records)} source records, {len(files)} handoff records')

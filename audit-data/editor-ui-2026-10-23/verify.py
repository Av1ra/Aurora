"""Verify recorded source anchors against the inspected Aurora revision."""
from pathlib import Path
import json
folder=Path(__file__).resolve().parent
root=folder.parents[1]
data=json.loads((folder/'source-evidence.json').read_text(encoding='utf-8'))
for check in data['checks']:
    lines=(root/check['file']).read_text(encoding='utf-8-sig').splitlines()
    assert check['token'] in lines[check['line']-1],check['id']
print('PASS',len(data['checks']),'source anchors; static evidence only, not interactive validation')

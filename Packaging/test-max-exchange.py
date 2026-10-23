"""Optional real 3ds Max batch regression. Never attaches to an existing Max session."""
import argparse
import json
import math
from pathlib import Path
import subprocess
import sys


def compare(folder):
    result = json.loads((folder / 'result.json').read_text(encoding='utf-8'))
    assert result.get('success'), result
    before = json.loads((folder / 'input.obj.aur-scene.json').read_text(encoding='utf-8'))
    after = json.loads((folder / 'output.obj.aur-scene.json').read_text(encoding='utf-8'))
    assert len(before['Nodes']) == len(after['Nodes'])
    maximum = 0.0
    for a, b in zip(before['Nodes'], after['Nodes']):
        for key in ('Name', 'Parent', 'Indices', 'Groups'):
            assert a[key] == b[key], (key, a[key], b[key])
        for key in ('Frame', 'Positions', 'Normals', 'Uv'):
            assert len(a[key]) == len(b[key]), key
            for x, y in zip(a[key], b[key]):
                assert math.isfinite(y) and abs(x - y) < 1e-4, (key, x, y)
                maximum = max(maximum, abs(x - y))
    assert before['Metadata'] == after['Metadata']
    assert before['Materials'] == after['Materials']
    print(f'PASS actual 3ds Max OBJ exchange: mesh, dummy, parent, frames, world vertices, normals, UVs, materials; max error {maximum:.3g}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--max-batch', type=Path)
    parser.add_argument('--verify', type=Path, help='Only verify an already completed fixture run')
    args = parser.parse_args()
    if args.verify:
        compare(args.verify)
        return
    if not args.max_batch or not args.max_batch.is_file():
        parser.error('--max-batch must explicitly name the installed 3dsmaxbatch.exe')
    root = Path(__file__).resolve().parents[1]
    folder = root / 'artifacts' / 'max-exchange-regression'
    folder.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, str(root / 'Exchange'))
    import scene_io
    scene = dict(Metadata='Aurora DCC regression', Materials=[dict(Name='paint', Texture='')], Nodes=[
        dict(Name='wheel', Parent=-1, Frame=[0,0,-1,0,0,1,0,0,1,0,0,0,3,2,4,1],
             Positions=[3,2,4,3,2,3,3,3,4], Normals=[1,0,0]*3, Uv=[0,0,1,0,0,1],
             Indices=[0,1,2], Groups=[dict(First=0, Count=3, Material=0)]),
        dict(Name='helper', Parent=0, Frame=[1,0,0,0,0,1,0,0,0,0,1,0,4,3,5,1],
             Positions=[], Normals=[], Uv=[], Indices=[], Groups=[])])
    scene_io.write(str(folder / 'input.obj'), scene)
    for name in ('result.json', 'output.obj', 'output.obj.aur-scene.json'):
        (folder / name).unlink(missing_ok=True)
    check = folder / 'check.py'
    check.write_text('''from pathlib import Path
from pymxs import runtime as rt
import json,traceback
folder=Path(%r)
try:
    path=Path(%r)
    namespace={"__file__":str(path)}
    exec(compile(path.read_text(encoding="utf-8").split("\\nif rt.queryBox")[0],str(path),"exec"),namespace)
    rt.resetMaxFile(rt.Name("noPrompt"))
    namespace["load"](str(folder/"input.obj"))
    namespace["save"](str(folder/"output.obj"))
    (folder/"result.json").write_text(json.dumps({"success":True}),encoding="utf-8")
except Exception:
    (folder/"result.json").write_text(json.dumps({"success":False,"error":traceback.format_exc()}),encoding="utf-8")
''' % (str(folder), str(root / 'Exchange' / 'max_aurora.py')), encoding='utf-8')
    subprocess.run([str(args.max_batch), str(check), '-dm', 'on', '-log', str(folder/'max.log'),
                    '-listenerlog', str(folder/'listener.log')], check=True, timeout=240,
                   creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
    compare(folder)


if __name__ == '__main__':
    main()

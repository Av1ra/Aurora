"""Aurora OBJ companion I/O, shared by Blender and 3ds Max scripts."""
import json
from pathlib import Path


def read(path):
    return json.loads(Path(str(path) + '.aur-scene.json').read_text(encoding='utf-8-sig'))


def write(path, scene):
    path = Path(path)
    lines = ['# Aurora OBJ with pivot companion', 'mtllib ' + path.with_suffix('.mtl').name]
    offset = 1
    for node in scene['Nodes']:
        if not node['Positions']:
            continue
        lines.append('o ' + node['Name'])
        for key, tag, width in [('Positions', 'v', 3), ('Normals', 'vn', 3), ('Uv', 'vt', 2)]:
            for i in range(0, len(node[key]), width):
                values = node[key][i:i+width]
                if key == 'Uv':
                    values = [values[0], 1-values[1]]
                lines.append(tag + ' ' + ' '.join(format(v, '.9g') for v in values))
        for group in node['Groups']:
            lines.append('usemtl ' + scene['Materials'][group['Material']]['Name'])
            for i in range(group['First'], group['First']+group['Count'], 3):
                lines.append('f ' + ' '.join('{0}/{0}/{0}'.format(v+offset) for v in node['Indices'][i:i+3]))
        offset += len(node['Positions'])//3
    path.write_text('\n'.join(lines)+'\n', encoding='utf-8')
    Path(str(path)+'.aur-scene.json').write_text(json.dumps(scene, ensure_ascii=False), encoding='utf-8')
    path.with_suffix('.mtl').write_text('\n'.join('newmtl '+m['Name']+'\nKd 1 1 1\n'+('map_Kd '+m['Texture'].replace('\\', '/')+'\n' if m.get('Texture') else '') for m in scene['Materials']), encoding='utf-8')

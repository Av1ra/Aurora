"""3ds Max Python: run this file; choose Aurora OBJ import or export.
The scene_io.py file must be beside it. Does not change system unit settings.
"""
import importlib.util
from pathlib import Path
from pymxs import runtime as rt

_spec = importlib.util.spec_from_file_location('aurora_scene_io', Path(__file__).with_name('scene_io.py'))
io = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(io)
unit = float(rt.units.decodeValue('1m'))


def point(v, scale=1):
    return rt.point3(v[0]*scale, -v[2]*scale, v[1]*scale)


def game(v, scale=1):
    return [v.x/scale, v.z/scale, -v.y/scale]


def load(path):
    scene = io.read(path)
    objects = []
    materials = []
    for value in scene['Materials']:
        material = rt.standardMaterial(name=value['Name'])
        if value.get('Texture'):
            material.diffuseMap = rt.bitmapTexture(filename=value['Texture'])
        materials.append(material)
    for node in scene['Nodes']:
        f = node['Frame']
        frame = rt.matrix3(point(f[0:3]), point([-v for v in f[8:11]]), point(f[4:7]), point(f[12:15], unit))
        if node['Positions']:
            inverse = rt.inverse(frame)
            vertices = [point(node['Positions'][i:i+3], unit)*inverse for i in range(0, len(node['Positions']), 3)]
            faces = [rt.point3(*[v+1 for v in node['Indices'][i:i+3]]) for i in range(0, len(node['Indices']), 3)]
            obj = rt.mesh(vertices=vertices, faces=faces, name=node['Name'])
            rt.setNumTVerts(obj, len(vertices))
            rt.buildTVFaces(obj)
            for vi in range(len(vertices)):
                rt.setTVert(obj, vi+1, rt.point3(node['Uv'][vi*2], 1-node['Uv'][vi*2+1], 0))
            for fi, face in enumerate(faces):
                rt.setTVFace(obj, fi+1, face)
            material = rt.multiMaterial(numsubs=len(materials))
            for mi, value in enumerate(materials):
                material.materialList[mi] = value
            obj.material = material
            for group in node['Groups']:
                for fi in range(group['First']//3, (group['First']+group['Count'])//3):
                    rt.setFaceMatID(obj, fi+1, group['Material']+1)
            rt.update(obj)
        else:
            obj = rt.dummy(name=node['Name'])
        obj.transform = frame
        rt.setUserProp(obj, 'AuroraScene', scene['Metadata'])
        rt.setUserProp(obj, 'AuroraObject', True)
        objects.append(obj)
    for i, node in enumerate(scene['Nodes']):
        if node['Parent'] >= 0:
            frame = objects[i].transform
            objects[i].parent = objects[node['Parent']]
            objects[i].transform = frame


def save(path):
    source = list(rt.selection) or [o for o in rt.objects if rt.getUserProp(o, 'AuroraObject')]
    nodes, materials = [], []
    metadata = next((str(rt.getUserProp(o, 'AuroraScene')) for o in source if rt.getUserProp(o, 'AuroraScene')), '')
    for obj in source:
        f = obj.transform
        # Max +Y corresponds to game -Z; Max +Z corresponds to game +Y.
        rows = [game(f.row1), game(f.row3), [-v for v in game(f.row2)], game(f.row4, unit)]
        node = dict(Name=str(obj.name), Parent=source.index(obj.parent) if obj.parent in source else -1,
                    Frame=[v for i, row in enumerate(rows) for v in row+[1 if i == 3 else 0]], Positions=[], Normals=[], Uv=[], Indices=[], Groups=[])
        if rt.canConvertTo(obj, rt.Editable_Mesh):
            mesh = rt.snapshotAsMesh(obj)
            for fi in range(1, int(mesh.numfaces)+1):
                face = rt.getFace(mesh, fi)
                tvface = rt.getTVFace(mesh, fi) if rt.getNumTVerts(mesh) else None
                mid = int(rt.getFaceMatID(mesh, fi))-1
                material = obj.material
                if material and rt.classOf(material) == rt.MultiMaterial:
                    material = material.materialList[mid] if mid < len(material.materialList) else None
                value = dict(Name=str(material.name) if material else 'Material', Texture='')
                if material and rt.isProperty(material, 'diffuseMap') and material.diffuseMap and rt.isProperty(material.diffuseMap, 'filename'):
                    value['Texture'] = str(material.diffuseMap.filename)
                if value not in materials:
                    materials.append(value)
                mi = materials.index(value)
                if not node['Groups'] or node['Groups'][-1]['Material'] != mi:
                    node['Groups'].append(dict(First=len(node['Indices']), Count=0, Material=mi))
                for corner in range(3):
                    vi = int(face[corner])
                    vertex = rt.getVert(mesh, vi)
                    # snapshotAsMesh returns world-space geometry, including normals.
                    n = rt.getNormal(mesh, vi)
                    normal = rt.normalize(n)
                    uv = rt.getTVert(mesh, int(tvface[corner])) if tvface else rt.point3(0, 0, 0)
                    node['Indices'].append(len(node['Positions'])//3)
                    node['Positions'].extend(game(vertex, unit))
                    node['Normals'].extend(game(normal))
                    node['Uv'].extend([uv.x, 1-uv.y])
                    node['Groups'][-1]['Count'] += 1
        nodes.append(node)
    io.write(path, dict(Metadata=metadata, Materials=materials or [dict(Name='Material', Texture='')], Nodes=nodes))


if rt.queryBox('Import Aurora OBJ? Choose No to export the selected objects.', title='Aurora OBJ'):
    path = rt.getOpenFileName(caption='Aurora OBJ import', types='OBJ (*.obj)|*.obj|')
    if path:
        load(str(path))
else:
    path = rt.getSaveFileName(caption='Aurora OBJ export', types='OBJ (*.obj)|*.obj|')
    if path:
        save(str(path))

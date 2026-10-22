"""Run in Blender Text Editor, then F3: Aurora OBJ import / export.
Uses the adjacent scene_io.py. Game metres/Y-up are converted to Blender Z-up.
"""
import bpy
import importlib.util
import json
from pathlib import Path
from mathutils import Matrix, Vector
from bpy_extras.io_utils import ImportHelper, ExportHelper

_folder = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location('aurora_scene_io', _folder/'scene_io.py')
io = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(io)
C = Matrix(((1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def frame(values):
    return Matrix([values[i:i+4] for i in range(0, 16, 4)]).transposed()


class AuroraImport(bpy.types.Operator, ImportHelper):
    bl_idname = 'aurora.import_obj'
    bl_label = 'Aurora OBJ import'
    filename_ext = '.obj'

    def execute(self, context):
        scene = io.read(self.filepath)
        objects = []
        materials = []
        for value in scene['Materials']:
            material = bpy.data.materials.new(value['Name'])
            material['AuroraTexture'] = value.get('Texture', '')
            materials.append(material)
        for node in scene['Nodes']:
            game_frame = frame(node['Frame'])
            world = C @ game_frame @ C.inverted()
            mesh = None
            if node['Positions']:
                mesh = bpy.data.meshes.new(node['Name'])
                positions = node['Positions']
                points = [world.inverted() @ (C @ Vector(positions[i:i+3])) for i in range(0, len(positions), 3)]
                faces = [node['Indices'][i:i+3] for i in range(0, len(node['Indices']), 3)]
                mesh.from_pydata(points, [], faces)
                for material in materials:
                    mesh.materials.append(material)
                uv = mesh.uv_layers.new()
                for polygon in mesh.polygons:
                    for loop_index in polygon.loop_indices:
                        vi = mesh.loops[loop_index].vertex_index
                        uv.data[loop_index].uv = (node['Uv'][vi*2], 1-node['Uv'][vi*2+1])
                normal_matrix = world.to_3x3().transposed() @ C.to_3x3()
                normals = [(normal_matrix @ Vector(node['Normals'][i:i+3])).normalized() for i in range(0, len(node['Normals']), 3)]
                mesh.normals_split_custom_set_from_vertices(normals)
                for group in node['Groups']:
                    for fi in range(group['First']//3, (group['First']+group['Count'])//3):
                        mesh.polygons[fi].material_index = group['Material']
            obj = bpy.data.objects.new(node['Name'], mesh)
            context.collection.objects.link(obj)
            obj.matrix_world = world
            obj['AuroraScene'] = scene['Metadata']
            obj['AuroraObject'] = True
            objects.append(obj)
        for index, node in enumerate(scene['Nodes']):
            if node['Parent'] >= 0:
                world = objects[index].matrix_world.copy()
                objects[index].parent = objects[node['Parent']]
                objects[index].matrix_world = world
        return {'FINISHED'}


class AuroraExport(bpy.types.Operator, ExportHelper):
    bl_idname = 'aurora.export_obj'
    bl_label = 'Aurora OBJ export'
    filename_ext = '.obj'

    def execute(self, context):
        source = [obj for obj in context.scene.objects if obj.get('AuroraObject') or obj.select_get()]
        materials, nodes = [], []
        metadata = next((o.get('AuroraScene') for o in source if o.get('AuroraScene')), '')
        for obj in source:
            world = C.inverted() @ obj.matrix_world @ C
            node = dict(Name=obj.name, Parent=source.index(obj.parent) if obj.parent in source else -1,
                        Frame=[v for row in world.transposed() for v in row], Positions=[], Normals=[], Uv=[], Indices=[], Groups=[])
            if obj.type == 'MESH':
                evaluated = obj.evaluated_get(context.evaluated_depsgraph_get())
                mesh = evaluated.to_mesh()
                try:
                    mesh.calc_loop_triangles()
                    normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
                    for tri in mesh.loop_triangles:
                        material = obj.material_slots[tri.material_index].material if len(obj.material_slots) > tri.material_index else None
                        value = dict(Name=material.name if material else 'Material', Texture=material.get('AuroraTexture', '') if material else '')
                        if value not in materials:
                            materials.append(value)
                        mi = materials.index(value)
                        if not node['Groups'] or node['Groups'][-1]['Material'] != mi:
                            node['Groups'].append(dict(First=len(node['Indices']), Count=0, Material=mi))
                        for li in tri.loops:
                            loop = mesh.loops[li]
                            point = C.inverted() @ (obj.matrix_world @ mesh.vertices[loop.vertex_index].co)
                            normal = C.to_3x3().inverted() @ (normal_matrix @ (mesh.corner_normals[li].vector if hasattr(mesh, 'corner_normals') else loop.normal)).normalized()
                            uv = mesh.uv_layers.active.data[li].uv if mesh.uv_layers.active else (0, 0)
                            node['Indices'].append(len(node['Positions'])//3)
                            node['Positions'].extend(point)
                            node['Normals'].extend(normal)
                            node['Uv'].extend((uv[0], 1-uv[1]))
                            node['Groups'][-1]['Count'] += 1
                finally:
                    evaluated.to_mesh_clear()
            nodes.append(node)
        io.write(self.filepath, dict(Metadata=metadata, Materials=materials or [dict(Name='Material', Texture='')], Nodes=nodes))
        return {'FINISHED'}


for cls in (AuroraImport, AuroraExport):
    if hasattr(bpy.types, cls.__name__):
        bpy.utils.unregister_class(getattr(bpy.types, cls.__name__))
    bpy.utils.register_class(cls)

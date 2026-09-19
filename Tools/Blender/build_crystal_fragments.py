"""Run with Blender --background --factory-startup --python <this file>."""
import bpy
import bmesh
import json
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Models/Effects/CrystalFragments'
ART = ROOT / 'Artifacts/Crystal'
OUT.mkdir(parents=True, exist_ok=True)
ART.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Textures/crystal20260824.fbx'))
source = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
bm = bmesh.new()
bm.from_mesh(source.data)
bmesh.ops.transform(bm, matrix=source.matrix_world, verts=list(bm.verts))
bmesh.ops.triangulate(bm, faces=list(bm.faces))
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
original_volume = abs(bm.calc_volume(signed=True))
assert all(e.is_manifold for e in bm.edges), 'Source must be closed'
lo = Vector(tuple(min(v.co[i] for v in bm.verts) for i in range(3)))
hi = Vector(tuple(max(v.co[i] for v in bm.verts) for i in range(3)))
center = (lo + hi) / 2

def clip(mesh, point, normal, clear_outer):
    result = mesh.copy()
    bmesh.ops.bisect_plane(result, geom=list(result.verts)+list(result.edges)+list(result.faces),
        dist=0.000001, plane_co=point, plane_no=normal,
        clear_outer=clear_outer, clear_inner=not clear_outer)
    boundary = [e for e in result.edges if e.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(result, edges=boundary, sides=0)
    bmesh.ops.recalc_face_normals(result, faces=list(result.faces))
    assert result.faces and all(e.is_manifold for e in result.edges), 'Invalid cut'
    return result

# Shared inclined planes keep adjacent pieces exactly complementary.
layers = []
remaining = bm.copy()
for i in range(1, 4):
    point = Vector((center.x, center.y, lo.z + (hi.z-lo.z)*i/4))
    normal = Vector((0.10 if i % 2 else -0.12, -0.08 if i % 2 else 0.07, 1))
    layers.append(clip(remaining, point, normal, True))
    upper = clip(remaining, point, normal, False)
    remaining.free()
    remaining = upper
layers.append(remaining)
bpy.data.objects.remove(source, do_unlink=True)
parent = bpy.data.objects.new('CrystalFragments_16', None)
bpy.context.collection.objects.link(parent)
pieces = []
report = []
for level, layer in enumerate(layers):
    midpoint = Vector(tuple((min(v.co[i] for v in layer.verts)+max(v.co[i] for v in layer.verts))/2 for i in range(3)))
    normal_a = Vector((1, 0.18*(-1)**level, 0.06))
    normal_b = Vector((-0.14*(-1)**level, 1, -0.05))
    quarter = 0
    for side_a in (True, False):
        half = clip(layer, midpoint, normal_a, side_a)
        for side_b in (True, False):
            part = clip(half, midpoint, normal_b, side_b)
            quarter += 1
            name = f'Crystal_L{level+1:02d}_P{quarter:02d}'
            volume = abs(part.calc_volume(signed=True))
            assert volume > original_volume * 0.0001, 'Degenerate fragment'
            pivot = sum((v.co for v in part.verts), Vector()) / len(part.verts)
            bmesh.ops.translate(part, vec=-pivot, verts=list(part.verts))
            mesh = bpy.data.meshes.new(name)
            part.to_mesh(mesh)
            part.free()
            obj = bpy.data.objects.new(name, mesh)
            bpy.context.collection.objects.link(obj)
            obj.parent = parent
            obj.location = pivot
            obj['assembly_layer'] = level+1
            obj['assembly_order'] = len(pieces)
            obj.color = (0.12+level*0.10, 0.48+quarter*0.06, 0.85, 1)
            pieces.append(obj)
            report.append(dict(name=name, position_blender=list(pivot), volume=volume))
        half.free()
    layer.free()
assert len(pieces) == 16
total = sum(p['volume'] for p in report)
assert abs(total-original_volume)/original_volume < 0.0001, (total, original_volume)
bpy.ops.object.select_all(action='DESELECT')
parent.select_set(True)
for obj in pieces:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parent
bpy.ops.export_scene.fbx(filepath=str(OUT/'crystal20260824_fragments16.fbx'),
    use_selection=True, object_types={'EMPTY','MESH'}, axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, bake_anim=False, use_custom_props=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'crystal20260824_fragments16.blend'))
(ART/'fragments16_report.json').write_text(json.dumps(dict(source_volume=original_volume,
    fragments_volume=total, fragments=report), indent=2), encoding='utf-8')

# Inspection image: assembled at left, separated by layer and quarter at right.
for obj in pieces:
    duplicate = obj.copy()
    duplicate.data = obj.data
    bpy.context.collection.objects.link(duplicate)
    duplicate.parent = None
    offset = obj.location - center
    duplicate.location = obj.location + Vector((1.2, 0, 0)) + offset*0.28
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'OBJECT'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.display.shading.background_type = 'WORLD'
scene.world.color = (0.035, 0.035, 0.045)
camera_data = bpy.data.cameras.new('PreviewCamera')
camera = bpy.data.objects.new('PreviewCamera', camera_data)
scene.collection.objects.link(camera)
target = center + Vector((0.6, 0, 0))
camera.location = target + Vector((3, -7, 2.7))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 3.5
scene.camera = camera
scene.render.resolution_x = 1100
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.filepath = str(ART/'fragments16_preview.png')
bpy.ops.render.render(write_still=True)
print('VERIFIED: 16 closed fragments; source volume:', original_volume, 'sum:', total)

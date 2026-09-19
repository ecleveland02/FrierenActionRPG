"""Correct limb placement and isolate limb skinning in a separate revision."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]/'ArtSource/DragonInspection/Rigged'
bpy.ops.wm.open_mainfile(filepath=str(root/'RedDragon_Rigged.blend'))
rig=bpy.data.objects['RedDragon_Rig']; mesh=bpy.data.objects['RedDragon_Skin']
rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis.identity()
bpy.context.view_layer.objects.active=rig
bpy.ops.object.mode_set(mode='EDIT')
chains={
 'Front.L':[(.06,-.25,.26),(.10,-.26,.115),(.10,-.405,.028),(.10,-.45,.012)],
 'Front.R':[(-.115,-.245,.26),(-.155,-.27,.12),(-.19,-.35,.028),(-.215,-.40,.012)],
 'Rear.L':[(.105,-.01,.235),(.095,-.19,.105),(.155,-.065,.045),(.14,-.12,.012)],
 'Rear.R':[(-.065,.005,.23),(-.115,-.135,.105),(-.105,.035,.04),(-.115,-.055,.012)]}
for limb,pts in chains.items():
    for i,label in enumerate(['Upper','Lower','Foot']):
        b=rig.data.edit_bones[limb+'_'+label];b.head=pts[i];b.tail=pts[i+1]
        b.use_connect=False
        b.parent=rig.data.edit_bones[(limb+'_'+['Upper','Lower'][i-1]) if i else ('Spine' if limb.startswith('Front') else 'Pelvis')]
bpy.ops.object.mode_set(mode='OBJECT')
names={g.index:g.name for g in mesh.vertex_groups}
def distance(p,b):
    a=b.head_local;d=b.tail_local-a
    return (p-a-d*max(0,min(1,(p-a).dot(d)/d.length_squared))).length
limb_indices={g.index for g in mesh.vertex_groups if g.name.startswith(('Front.','Rear.'))}
regions={name:[] for name in chains}
for v in mesh.data.vertices:
    x,y,z=v.co
    existing={names[g.group]:g.weight for g in v.groups}
    if not any(g.group in limb_indices for g in v.groups) and z>.215:continue
    # The front feet and elbows are anterior to the folded hind knees.
    # Never blend between separate legs, even where they touch in the posed mesh.
    side='L' if x>-.035 else 'R'
    choices=['Front.'+side,'Rear.'+side]
    limb=min(choices,key=lambda name:min(distance(v.co,rig.data.bones[name+'_'+part]) for part in ['Upper','Lower','Foot']))
    candidates=[rig.data.bones[limb+'_'+part] for part in ['Upper','Lower','Foot']]
    nearest=min(distance(v.co,b) for b in candidates)
    strength=max(0,min(1,(.295-z)/.09))
    if z>.215:strength*=max(0,min(1,(.13-nearest)/.08))
    if z>.32:strength=0
    if y>.10 or y<-.50:strength=0
    # Preserve existing torso/wing influences where they meet a limb.
    torso={n:w for n,w in existing.items() if not n.startswith(('Front.','Rear.'))}
    if not torso:torso={'Spine' if limb.startswith('Front') else 'Pelvis':1}
    total=sum(torso.values()); weights={n:(1-strength)*w/total for n,w in torso.items()}
    raw=[1/max(.012,distance(v.co,b))**4 for b in candidates];total=sum(raw)
    for b,w in zip(candidates,raw):weights[b.name]=strength*w/total
    for g in list(v.groups):mesh.vertex_groups[g.group].remove([v.index])
    weights=sorted(((n,w) for n,w in weights.items() if w>1e-6),key=lambda a:-a[1])[:4]
    total=sum(w for n,w in weights)
    for n,w in weights:mesh.vertex_groups[n].add([v.index],w/total,'REPLACE')
    if strength>.8:regions[limb].append(v.index)
bpy.context.view_layer.update()
def evaluated():
    obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); result=obj.to_mesh()
    coords=[v.co.copy() for v in result.vertices];obj.to_mesh_clear();return coords
baseline=evaluated();report={}
scene=bpy.context.scene;out=root/'LegFix';out.mkdir(exist_ok=True)
for limb in chains:
    p=rig.pose.bones[limb+'_Upper'];p.rotation_mode='XYZ';p.rotation_euler.x=math.radians(22)
    bpy.context.view_layer.update();points=evaluated()
    foreign=[i for other,indices in regions.items() if other!=limb for i in indices]
    movement=max((points[i]-baseline[i]).length for i in foreign)
    own=max((points[i]-baseline[i]).length for i in regions[limb])
    report[limb]={'other_leg_max_displacement':movement,'own_leg_max_displacement':own}
    if movement>1e-6 or own<.001:raise RuntimeError('Limb isolation failed '+str(report))
    scene.render.filepath=str(out/(limb+'_test.png'));bpy.ops.render.render(write_still=True)
    p.rotation_euler.x=0;bpy.context.view_layer.update()
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(out/'RedDragon_LegsFixed.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'RedDragon_LegsFixed.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,path_mode='COPY',embed_textures=True,bake_anim=False)
print('LIMB_ISOLATION',json.dumps(report))

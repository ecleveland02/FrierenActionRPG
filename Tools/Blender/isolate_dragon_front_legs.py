"""Isolate front limbs without changing the source or rear skeleton."""
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
 'Front.L':[(.065,-.30,.25),(.085,-.295,.15),(.10,-.425,.045),(.10,-.47,.015)],
 'Front.R':[(-.145,-.30,.25),(-.155,-.32,.15),(-.19,-.395,.04),(-.215,-.44,.015)]}
for limb,pts in chains.items():
    for i,label in enumerate(['Upper','Lower','Foot']):
        b=rig.data.edit_bones[limb+'_'+label];b.head=pts[i];b.tail=pts[i+1]
        b.use_connect=False
        b.parent=rig.data.edit_bones[(limb+'_'+['Upper','Lower'][i-1]) if i else 'Spine']
bpy.ops.object.mode_set(mode='OBJECT')
names={g.index:g.name for g in mesh.vertex_groups}
def distance(p,b):
    a=b.head_local;d=b.tail_local-a
    return (p-a-d*max(0,min(1,(p-a).dot(d)/d.length_squared))).length
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for v in mesh.data.vertices:
    x,y,z=v.co
    old={names[g.group]:g.weight for g in v.groups}
    limb='Front.L' if x>-.035 else 'Front.R'
    strength=(1-smooth(.20,.28,z))*(1-smooth(-.295,-.27,y))*smooth(-.54,-.50,y)
    if not strength and not any(n.startswith('Front.') for n in old):continue
    residual={n:w for n,w in old.items() if not n.startswith('Front.')}
    if strength:residual={'Spine':1}
    if not residual:
        choices=[b for b in rig.data.bones if b.use_deform and not b.name.startswith('Front.')]
        residual={min(choices,key=lambda b:distance(v.co,b)).name:1}
    total=sum(residual.values());weights={n:(1-strength)*w/total for n,w in residual.items()}
    bones=[rig.data.bones[limb+'_'+p] for p in ['Upper','Lower','Foot']]
    raw=[1/max(.015,distance(v.co,b))**4 for b in bones];total=sum(raw)
    for b,w in zip(bones,raw):weights[b.name]=strength*w/total
    for g in list(v.groups):mesh.vertex_groups[g.group].remove([v.index])
    weights=sorted(((n,w) for n,w in weights.items() if w>1e-7),key=lambda a:-a[1])[:4]
    total=sum(w for n,w in weights)
    for n,w in weights:mesh.vertex_groups[n].add([v.index],w/total,'REPLACE')
bpy.context.view_layer.update()
def evaluated():
    obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get()); result=obj.to_mesh()
    coords=[v.co.copy() for v in result.vertices];obj.to_mesh_clear();return coords
baseline=evaluated();report={}
scene=bpy.context.scene;out=root/'LegFix';out.mkdir(exist_ok=True)
rear=[v.index for v in mesh.data.vertices if v.co.y>=-.27 and v.co.z<.26]
for limb in chains:
    p=rig.pose.bones[limb+'_Upper'];p.rotation_mode='XYZ';p.rotation_euler.x=math.radians(30)
    bpy.context.view_layer.update();points=evaluated()
    movement=max((points[i]-baseline[i]).length for i in rear)
    report[limb]={'rear_and_tail_max_displacement':movement,'checked_vertices':len(rear)}
    assert movement<1e-6,report
    for view,pos in [('side',(2,0,.34)),('perspective',(1.25,-1.65,.9))]:
        scene.camera.location=pos
        scene.camera.rotation_euler=(Vector((0,0,.34))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(out/(limb+'_'+view+'.png'));bpy.ops.render.render(write_still=True)
    p.rotation_euler.x=0;bpy.context.view_layer.update()
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(out/'RedDragon_LegsFixed.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'RedDragon_LegsFixed.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,path_mode='COPY',embed_textures=True,bake_anim=False)
print('FRONT_LIMB_TEST',json.dumps(report))

import bpy, math, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]/'ArtSource/DragonInspection'
bpy.ops.wm.open_mainfile(filepath=str(root/'DragonInspected.blend'))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
mesh.name='RedDragon_Skin'
bpy.ops.object.select_all(action='DESELECT')
bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='RedDragon_Rig'; rig.show_in_front=True
bpy.ops.object.mode_set(mode='EDIT'); rig.data.edit_bones.remove(rig.data.edit_bones[0])
def bone(name,a,b,parent=None,deform=True):
    item=rig.data.edit_bones.new(name); item.head=a;item.tail=b;item.use_deform=deform
    if parent:item.parent=rig.data.edit_bones[parent]
    return name
bone('Root',(0,0,0),(0,0,.12),deform=False)
bone('Pelvis',(.035,.035,.24),(.0,-.08,.25),'Root')
bone('Spine',(.0,-.08,.25),(-.025,-.23,.28),'Pelvis')
bone('Neck',(-.025,-.23,.28),(-.075,-.37,.33),'Spine')
bone('Head',(-.075,-.37,.33),(-.085,-.56,.30),'Neck')
# Closed-mouth skin is kept on Head; a jaw split needs separate topology work.
tail=[(.035,.035,.24),(.09,.12,.14),(.13,.22,.055),(.16,.32,.045),(.20,.40,.10),(.26,.44,.22)]
parent='Pelvis'
for i in range(len(tail)-1):parent=bone('Tail_%02d'%i,tail[i],tail[i+1],parent)
legs={
'Front.L':[(.065,-.23,.25),(.11,-.24,.12),(.095,-.37,.035),(.10,-.43,.015)],
'Front.R':[(-.105,-.24,.25),(-.15,-.27,.13),(-.20,-.36,.025),(-.23,-.41,.015)],
'Rear.L':[(.11,-.015,.24),(.08,-.13,.12),(.16,-.02,.055),(.16,-.065,.012)],
'Rear.R':[(-.065,.015,.23),(-.12,-.09,.12),(-.105,.075,.045),(-.14,.025,.015)]}
for side,points in legs.items():
    parent='Spine' if side.startswith('Front') else 'Pelvis'
    for i,label in enumerate(['Upper','Lower','Foot']):parent=bone(side+'_'+label,points[i],points[i+1],parent)
wings={
'L':[(.06,-.25,.31),(.18,-.27,.44),(.20,-.18,.66),(.55,-.07,.45)],
'R':[(-.10,-.22,.32),(-.22,-.14,.44),(-.27,.03,.66),(-.44,.25,.45)]}
for side,p in wings.items():
    bone('Wing_'+side+'_Upper',p[0],p[1],'Spine')
    bone('Wing_'+side+'_Fore',p[1],p[2],'Wing_'+side+'_Upper')
    bone('Wing_'+side+'_Outer',p[2],p[3],'Wing_'+side+'_Fore')
    ends=[(.40,-.12,.37),(.29,-.16,.32)] if side=='L' else [(-.30,.13,.33),(-.19,.015,.30)]
    for i,end in enumerate(ends):bone('Wing_'+side+'_Finger'+str(i),p[2],end,'Wing_'+side+'_Fore')
bpy.ops.object.mode_set(mode='OBJECT');bpy.ops.object.select_all(action='DESELECT')
mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
unweighted=[v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups)<.0001]
if unweighted:
    print('Heat solver failed; creating provisional distance weights for manual refinement.')
    mesh.vertex_groups.clear()
    deform=[b for b in rig.data.bones if b.use_deform]
    groups={b.name:mesh.vertex_groups.new(name=b.name) for b in deform}
    def distance(p,b):
        a=b.head_local;d=b.tail_local-a
        t=max(0,min(1,(p-a).dot(d)/d.length_squared))
        return (p-a-d*t).length
    for v in mesh.data.vertices:
        near=sorted([(distance(v.co,b),b.name) for b in deform])[:3]
        values=[1/max(.008,d)**6 for d,n in near];total=sum(values)
        for (d,n),w in zip(near,values):groups[n].add([v.index],w/total,'REPLACE')
    unweighted=[v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups)<.0001]
    if unweighted:raise RuntimeError('Unweighted vertices remain')
# Keep no more than four normalized bone influences for game export.
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.vertex_group_limit_total(limit=4);bpy.ops.object.vertex_group_normalize_all(lock_active=False)
scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=60
for frame,amount in [(1,0),(16,1),(31,0),(46,-1),(60,0)]:
    for name,axis,angle in [('Wing_L_Upper',1,12),('Wing_R_Upper',1,-12),('Neck',0,5),('Tail_02',0,8),('Front.L_Lower',0,8)]:
        pb=rig.pose.bones[name];pb.rotation_mode='XYZ';pb.rotation_euler[axis]=math.radians(angle*amount)
        pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=name)
rig.animation_data.action.name='Rig_Deformation_Test'
scene.frame_set(1)
out=root/'Rigged';out.mkdir(exist_ok=True)
scene.camera.location=(1.25,-1.65,.9);scene.camera.rotation_euler=(Vector((0,-.05,.32))-scene.camera.location).to_track_quat('-Z','Y').to_euler()
for frame,label in [(1,'Rest'),(16,'PoseTest')]:
    scene.frame_set(frame);scene.render.filepath=str(out/(label+'.png'));bpy.ops.render.render(write_still=True)
scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D': area.spaces.active.region_3d.view_distance=1.8;area.spaces.active.region_3d.view_location=(0,0,.3)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'RedDragon_Rigged.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'RedDragon_Rigged.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,path_mode='COPY',embed_textures=True,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False)
print('RIG_REPORT',json.dumps({'bones':len(rig.data.bones),'vertices':len(mesh.data.vertices),'unweighted':len(unweighted),'output':str(out)}))

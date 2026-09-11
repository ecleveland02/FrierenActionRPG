"""Build the original Kael prototype. Run with Blender --background --python this_file.

All geometry and motion are authored here; no downloaded models or motion data.
Blender uses Z up and -Y forward. The FBX exporter converts to Unity's Y up.
"""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Kael'
EXPORT = ROOT / 'Assets/Art/Characters/Kael/Models'
PREVIEW = SOURCE / 'Previews'
for folder in (SOURCE, EXPORT, PREVIEW):
    folder.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for action in list(bpy.data.actions):
    bpy.data.actions.remove(action)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.fps = 30

def material(name, hex_color, metallic=0, roughness=.75):
    srgb = tuple(int(hex_color[i:i+2], 16) / 255 for i in (0, 2, 4))
    rgb = tuple(c/12.92 if c <= .04045 else ((c+.055)/1.055)**2.4 for c in srgb)
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*rgb, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1)
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    return m

M = {k: material(k, c, metal, rough) for k, c, metal, rough in [
    ('Kael_Navy', '28334F', 0, .85), ('Kael_CoatEdge', '35425C', 0, .8),
    ('Kael_Cream', 'D6C9A7', 0, .9), ('Kael_Gold', 'B19A61', .35, .45),
    ('Kael_Skin', 'C59A7B', 0, .75), ('Kael_Hair', '241E20', 0, .9),
    ('Kael_HairLight', '3B2F2C', 0, .8), ('Kael_Charcoal', '252A30', 0, .9),
    ('Kael_Leather', '533E32', 0, .85), ('Kael_LeatherEdge', '85644B', 0, .8),
    ('Kael_EyeWhite', 'D9DBD2', 0, .5), ('Kael_Eye', '648699', 0, .4),
    ('Kael_Wood', '72503A', 0, .9), ('Kael_Crystal', '4CAAD3', .15, .25),
]}

bones = {}
def bone(name, head, tail, parent=None):
    bones[name] = (Vector(head), Vector(tail), parent)

bone('Root', (0,0,0), (0,0,.16))
bone('Hips', (0,0,.92), (0,0,1.06), 'Root')
bone('Spine', (0,0,1.06), (0,0,1.25), 'Hips')
bone('Chest', (0,0,1.25), (0,0,1.43), 'Spine')
bone('Neck', (0,0,1.43), (0,0,1.51), 'Chest')
bone('Head', (0,0,1.51), (0,0,1.76), 'Neck')
for side, s in [('L',1), ('R',-1)]:
    bone('Shoulder.'+side, (s*.035,0,1.4), (s*.20,0,1.4), 'Chest')
    bone('UpperArm.'+side, (s*.20,0,1.4), (s*.43,0,1.20), 'Shoulder.'+side)
    bone('Forearm.'+side, (s*.43,0,1.20), (s*.59,-.025,1.00), 'UpperArm.'+side)
    bone('Hand.'+side, (s*.59,-.025,1.00), (s*.65,-.04,.92), 'Forearm.'+side)
    bone('Thigh.'+side, (s*.105,0,.94), (s*.105,-.025,.55), 'Hips')
    bone('Shin.'+side, (s*.105,-.025,.55), (s*.105,0,.13), 'Thigh.'+side)
    bone('Foot.'+side, (s*.105,0,.13), (s*.105,-.15,.065), 'Shin.'+side)
    bone('Toes.'+side, (s*.105,-.15,.065), (s*.105,-.23,.065), 'Foot.'+side)
    bone('Coat.'+side, (s*.11,.045,.99), (s*.16,.065,.43), 'Hips')
bone('StaffSocket', (-.63,-.055,.96), (-.63,-.055,1.07), 'Hand.R')

arm = bpy.data.armatures.new('KaelSkeleton')
rig = bpy.data.objects.new('KaelRig', arm)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name, (head, tail, parent) in bones.items():
    eb = arm.edit_bones.new(name)
    eb.head, eb.tail = head, tail
    if parent:
        eb.parent = arm.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front = True
meshes = []

def finish(obj, name, mat, joint):
    obj.name = name
    obj.data.materials.append(M[mat])
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    group = obj.vertex_groups.new(name=joint)
    group.add(list(range(len(obj.data.vertices))), 1, 'REPLACE')
    mod = obj.modifiers.new('KaelSkin', 'ARMATURE')
    mod.object = rig
    obj.parent = rig
    meshes.append(obj)
    return obj

def ellipsoid(name, pos, scale, mat, joint, segments=12, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=pos)
    obj = bpy.context.object
    obj.scale = scale
    return finish(obj, name, mat, joint)

def box(name, pos, scale, mat, joint, bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1, location=pos)
    obj = bpy.context.object
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = obj.modifiers.new('TailoredEdges', 'BEVEL')
        mod.width = bevel
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, name, mat, joint)

def segment(name, start, end, r1, r2, mat, joint, vertices=10):
    a, b = Vector(start), Vector(end)
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2,
                                   depth=(b-a).length, location=(a+b)/2)
    obj = bpy.context.object
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = (b-a).to_track_quat('Z','Y')
    return finish(obj, name, mat, joint)

def ring_mesh(name, rings, mat, joint, n=12):
    verts = [(rx*math.cos(2*math.pi*i/n), cy+ry*math.sin(2*math.pi*i/n), z)
             for z, rx, ry, cy in rings for i in range(n)]
    faces = []
    for k in range(len(rings)-1):
        for i in range(n):
            j=(i+1)%n
            faces.append((k*n+i,k*n+j,(k+1)*n+j,(k+1)*n+i))
    faces += [tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))]
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    return finish(obj,name,mat,joint)

# Tailored torso and waist: intermediate rings blend over the spine/chest seam.
body=ring_mesh('Coat_Bodice',[(.96,.16,.10,0),(1.08,.14,.09,0),(1.25,.18,.11,0),
                            (1.38,.205,.115,0),(1.43,.12,.085,0)],'Kael_Navy','Spine')
chest_group=body.vertex_groups.new(name='Chest')
for v in body.data.vertices:
    w=max(0,min(1,(v.co.z-1.12)/.23))
    body.vertex_groups['Spine'].add([v.index],1-w,'REPLACE')
    chest_group.add([v.index],w,'REPLACE')
box('Undershirt', (0,-.108,1.31), (.14,.027,.22), 'Kael_Cream','Chest')
for s in [-1,1]:
    segment('Lapel', (s*.14,-.092,1.41),(s*.045,-.123,1.17),.023,.015,'Kael_Gold','Chest',6)
    segment('CoatFrontTrim',(s*.022,-.10,1.16),(s*.027,-.10,1.03),.008,.008,'Kael_Cream','Spine',6)
segment('Neck', (0,0,1.41),(0,0,1.55),.056,.058,'Kael_Skin','Neck')
ring_mesh('HighCollar',[(1.405,.085,.077,0),(1.455,.079,.073,0)],'Kael_CoatEdge','Neck')
ellipsoid('FoldedHood',(0,.095,1.389),(.177,.101,.113),'Kael_CoatEdge','Chest')
for s in [-1,1]:
    segment('HoodRim',(s*.125,-.035,1.461),(s*.095,.156,1.37),.018,.021,'Kael_Gold','Chest',8)
ring_mesh('Belt',[(.995,.162,.107,0),(1.04,.155,.102,0)],'Kael_Leather','Hips')
box('Buckle',(0,-.111,1.016),(.055,.018,.04),'Kael_Gold','Hips',.005)

# Four open coat panels rather than a closed skirt: legs retain a walking slit.
for side,s in [('L',1),('R',-1)]:
    for back in [False,True]:
        ys=[-.08,-.13] if not back else [.08,.15]
        verts=[(s*.025,ys[0],1.01),(s*.16,ys[0]*.7,1.01),
               (s*.27,ys[1]*.85,.43),(s*.055,ys[1],.43)]
        mesh=bpy.data.meshes.new('CoatPanel')
        mesh.from_pydata(verts,[],[(0,1,2,3)])
        obj=bpy.data.objects.new('CoatTail_'+side+('_Back' if back else '_Front'),mesh)
        scene.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        finish(obj,obj.name,'Kael_Navy','Coat.'+side)
        mod=obj.modifiers.new('ClothThickness','SOLIDIFY')
        mod.thickness=.008
        segment('HemTrim',verts[2],verts[3],.009,.009,'Kael_Gold','Coat.'+side,6)
        # Small geometric embroidery reads from gameplay distance without textures.
        y=ys[1]+(-.008 if not back else .008)
        diamond=[(s*.125,y,.49),(s*.15,y,.55),(s*.185,y,.49),(s*.15,y,.46),(s*.125,y,.49)]
        for a,b in zip(diamond,diamond[1:]):
            segment('HemEmbroidery',a,b,.004,.004,'Kael_Gold','Coat.'+side,5)

box('CreamTabard',(0,-.112,.81),(.095,.014,.34),'Kael_Cream','Hips',.004)

for side,s in [('L',1),('R',-1)]:
    for name,mat,r1,r2 in [('UpperArm','Kael_Navy',.080,.060),('Forearm','Kael_Skin',.049,.034),
                           ('Thigh','Kael_Charcoal',.084,.063),('Shin','Kael_Charcoal',.064,.042)]:
        a,b,_=bones[name+'.'+side]
        segment(name+'.'+side,a,b,r1,r2,mat,name+'.'+side)
    ellipsoid('ShoulderCap.'+side,(s*.205,0,1.387),(.083,.083,.078),'Kael_CoatEdge','UpperArm.'+side)
    a,b,_=bones['Forearm.'+side]
    segment('SleeveHem.'+side,a,a.lerp(b,.12),.065,.065,'Kael_Cream','Forearm.'+side)
    segment('Bracer.'+side,a.lerp(b,.40),b,.047,.040,'Kael_Leather','Forearm.'+side)
    segment('Cuff.'+side,a.lerp(b,.86),b,.050,.048,'Kael_Gold','Forearm.'+side)
    ellipsoid('Glove.'+side,(s*.626,-.037,.95),(.049,.039,.077),'Kael_Leather','Hand.'+side)
    ellipsoid('Thumb.'+side,(s*.605,-.073,.967),(.022,.022,.04),'Kael_Leather','Hand.'+side,8,6)
    for j in range(4):
        ellipsoid('FingerTip.'+side,(s*(.612+j*.013),-.043,.889),(.009,.018,.023),'Kael_Skin','Hand.'+side,8,6)
    segment('BootShaft.'+side,(s*.105,0,.12),(s*.105,-.005,.38),.061,.070,'Kael_Leather','Shin.'+side)
    segment('BootCuff.'+side,(s*.105,-.005,.35),(s*.105,-.005,.39),.074,.074,'Kael_LeatherEdge','Shin.'+side)
    box('Boot.'+side,(s*.105,-.075,.075),(.132,.30,.13),'Kael_Leather','Foot.'+side,.035)
    box('Sole.'+side,(s*.105,-.075,.025),(.138,.30,.038),'Kael_Charcoal','Foot.'+side,.012)
    for z in [.18,.27]:
        box('BootStrap.'+side,(s*.105,-.057,z),(.117,.028,.019),'Kael_LeatherEdge','Shin.'+side,.005)

# Stylized face, eyes and layered hair silhouette.
ellipsoid('Head',(0,-.006,1.625),(.112,.095,.139),'Kael_Skin','Head',16,12)
ellipsoid('Jaw',(0,-.034,1.555),(.076,.073,.064),'Kael_Skin','Head')
ellipsoid('Nose',(0,-.098,1.613),(.023,.032,.038),'Kael_Skin','Head',8,6)
for s in [-1,1]:
    ellipsoid('Ear',(s*.108,0,1.622),(.021,.032,.047),'Kael_Skin','Head',8,6)
    ellipsoid('EyeWhite',(s*.047,-.091,1.650),(.024,.004,.010),'Kael_EyeWhite','Head')
    ellipsoid('Iris',(s*.045,-.095,1.650),(.009,.002,.010),'Kael_Eye','Head',10,6)
    ellipsoid('Pupil',(s*.045,-.097,1.650),(.004,.001,.007),'Kael_Hair','Head',8,6)
    segment('Brow',(s*.025,-.098,1.679),(s*.075,-.082,1.678),.006,.008,'Kael_Hair','Head',6)
segment('Mouth',(-.025,-.099,1.574),(.025,-.099,1.574),.003,.003,'Kael_Leather','Head',6)
ellipsoid('HairCap',(0,.01,1.709),(.117,.097,.086),'Kael_Hair','Head',14,8)
ellipsoid('HairBack',(0,.058,1.645),(.111,.058,.115),'Kael_Hair','Head')
for i,(x,z,dx) in enumerate([(-.093,1.737,.018),(-.060,1.768,.027),(-.022,1.778,-.024),
                              (.018,1.775,-.044),(.060,1.755,-.043),(.096,1.72,-.018)]):
    segment('Fringe_%02d'%i,(x,-.042,z),(x+dx,-.102,z-.10),.038,.004,
            'Kael_HairLight' if i%3==0 else 'Kael_Hair','Head',5)
for s in [-1,1]:
    segment('SideLock',(s*.107,.00,1.717),(s*.116,-.026,1.576),.033,.007,'Kael_Hair','Head',5)

# Strap follows the chest; bag and book follow the pelvis.
segment('SatchelStrap',(-.155,-.098,1.398),(.157,-.113,1.04),.024,.024,'Kael_Leather','Chest',4)
segment('CrossStrap',(.155,-.106,1.398),(-.157,-.121,1.04),.018,.018,'Kael_Leather','Chest',4)
box('Satchel',(.217,.028,.926),(.16,.125,.21),'Kael_Leather','Hips',.025)
box('SatchelFlap',(.22,-.043,.963),(.16,.018,.10),'Kael_LeatherEdge','Hips',.012)
box('SatchelClasp',(.22,-.059,.933),(.027,.01,.025),'Kael_Gold','Hips',.004)
box('SpellbookPages',(-.179,.052,.99),(.038,.12,.16),'Kael_Cream','Hips',.004)
for x in [-.203,-.156]:
    box('SpellbookCover',(x,.052,.99),(.009,.13,.18),'Kael_Navy','Hips',.004)
box('BookSpine',(-.18,.118,.99),(.056,.012,.18),'Kael_Gold','Hips',.003)

# Separate staff mesh, weighted to the equipment socket.
staff_parts=[]
path=[(-.64,-.057,.15),(-.65,-.057,.78),(-.63,-.057,1.25),(-.68,-.057,1.48),(-.65,-.057,1.61)]
for a,b in zip(path,path[1:]):
    staff_parts.append(segment('StaffWood',a,b,.018,.022,'Kael_Wood','StaffSocket',8))
for z in [.90,.94,.98,1.02]:
    staff_parts.append(segment('StaffGrip',(-.641,-.057,z),(-.642,-.057,z+.018),.024,.024,'Kael_Leather','StaffSocket',8))
staff_parts.append(ellipsoid('Crystal',(-.65,-.057,1.66),(.052,.035,.105),'Kael_Crystal','StaffSocket',6,4))
staff_parts.append(segment('CrystalSetting',(-.65,-.057,1.59),(-.65,-.057,1.615),.035,.038,'Kael_Gold','StaffSocket',8))
for s in [-1,1]:
    frame=[(-.65,-.057,1.53),(-.65+s*.080,-.057,1.64),(-.65+s*.055,-.057,1.74),(-.65,-.057,1.82)]
    for a,b in zip(frame,frame[1:]):
        staff_parts.append(segment('CrystalFrame',a,b,.012,.008,'Kael_Wood','StaffSocket',6))

def join_objects(items,name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in items:
        bpy.context.view_layer.objects.active=obj
        for modifier in list(obj.modifiers):
            if modifier.type != 'ARMATURE':
                bpy.ops.object.modifier_apply(modifier=modifier.name)
        obj.select_set(True)
    bpy.context.view_layer.objects.active=items[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=name
    return obj

body_mesh=join_objects([o for o in meshes if o not in staff_parts],'Kael_Body')
staff_mesh=join_objects(staff_parts,'Kael_Staff')
for obj in [body_mesh,staff_mesh]:
    obj.data.validate(verbose=True)
    obj.data.update()
    obj['authorship']='Original procedural prototype for Kael RPG'
    obj['forward']='-Y in Blender; +Z after FBX import'

def reset_pose():
    for p in rig.pose.bones:
        p.rotation_mode='XYZ'
        p.rotation_euler=(0,0,0)
        p.location=(0,0,0)
        p.scale=(1,1,1)

def rotate(name,x=0,y=0,z=0):
    rig.pose.bones[name].rotation_euler=tuple(math.radians(v) for v in (x,y,z))

def aim_bone(name,start,end):
    p=rig.pose.bones[name]
    rest=p.bone.tail_local-p.bone.head_local
    q=rest.rotation_difference(Vector(end)-Vector(start)) @ p.bone.matrix_local.to_quaternion()
    p.matrix=Matrix.Translation(Vector(start)) @ q.to_matrix().to_4x4()

def solve_limb(upper,lower,target,pole):
    a=rig.pose.bones[upper].head.copy()
    l1=rig.pose.bones[upper].bone.length
    l2=rig.pose.bones[lower].bone.length
    delta=Vector(target)-a
    d=min(delta.length,l1+l2-.0001)
    direction=delta.normalized()
    along=(l1*l1-l2*l2+d*d)/(2*d)
    bend=(Vector(pole)-direction*Vector(pole).dot(direction)).normalized()
    knee=a+direction*along+bend*math.sqrt(max(0,l1*l1-along*along))
    end=a+direction*d
    aim_bone(upper,a,knee)
    bpy.context.view_layer.update()
    aim_bone(lower,knee,end)

# Rotation X flexes legs (rest bones point down); arm Z lowers the A-pose toward the body.
def base_pose():
    rotate('UpperArm.L',0,0,28)
    rotate('UpperArm.R',0,0,-28)
    rotate('Forearm.L',-8,0,0)
    rotate('Forearm.R',-8,0,0)

def animate(name, frames, kind):
    action=bpy.data.actions.new(name)
    action.use_fake_user=True
    rig.animation_data_create()
    rig.animation_data.action=action
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1)
        phase=2*math.pi*t
        reset_pose()
        base_pose()
        if kind in ['idle','walk','run']:
            amp={'idle':0,'walk':24,'run':42}[kind]
            for side,s in [('L',1),('R',-1)]:
                wave=math.sin(phase)*s
                rotate('Thigh.'+side,amp*wave)
                rotate('Shin.'+side,-max(0,-wave)*amp*1.25)
                rotate('Foot.'+side,-amp*wave*.22)
                rotate('UpperArm.'+side,-amp*wave*.50,0,s*28)
                rotate('Forearm.'+side,-12-max(0,wave)*amp*.35)
                rotate('Coat.'+side,amp*wave*.55-4,0,-s*2)
            rig.pose.bones['Hips'].location.y=(.007 if kind=='idle' else .018)*math.sin(phase*2)
            rotate('Chest',(-7 if kind=='run' else 0)+1.5*math.sin(phase),0,2*math.sin(phase))
            rotate('Head',1.5*math.sin(phase+.5),0,-math.sin(phase))
        elif kind=='air':
            rotate('Thigh.L',-18); rotate('Thigh.R',12)
            rotate('Shin.L',-25); rotate('Shin.R',-20)
            rotate('UpperArm.L',-15,0,12); rotate('UpperArm.R',-15,0,-12)
            rotate('Coat.L',-16); rotate('Coat.R',-12)
        elif kind=='land':
            amount=math.sin(math.pi*t)
            rig.pose.bones['Hips'].location.y=-.095*amount
            for side in ['L','R']:
                rotate('Thigh.'+side,25*amount); rotate('Shin.'+side,-45*amount)
                rotate('Foot.'+side,20*amount)
            rotate('Chest',-12*amount)
        elif kind=='dodge':
            amount=math.sin(math.pi*t)
            rig.pose.bones['Hips'].location.y=-.12*amount
            rotate('Chest',-35*amount)
            rotate('Thigh.L',25*amount); rotate('Shin.L',-55*amount)
            rotate('Thigh.R',-25*amount); rotate('Shin.R',-25*amount)
            rotate('Coat.L',-35*amount); rotate('Coat.R',-35*amount)
        elif kind=='cast':
            amount=math.sin(math.pi*t)**.7
            rotate('Chest',0,-10*amount,0)
            rotate('UpperArm.L',-65*amount,0,28-18*amount)
            rotate('Forearm.L',-25*amount)
            rotate('Hand.L',-20*amount)
            rotate('UpperArm.R',-18*amount,0,-28)
            rotate('Head',-6*amount)
        elif kind=='hit':
            amount=math.sin(math.pi*t)
            rotate('Chest',18*amount,0,8*amount)
            rotate('Head',10*amount)
        if kind in ['idle','walk','run']:
            # Baked two-bone leg solving gives a planted stance half-cycle and a lifted
            # recovery half-cycle, rather than a stiff pendulum walk.
            rig.pose.bones['Hips'].location.y=-.025+(.005 if kind=='idle' else .012)*math.cos(phase*2)
            bpy.context.view_layer.update()
            stride={'idle':0,'walk':.18,'run':.30}[kind]
            lift={'idle':0,'walk':.075,'run':.15}[kind]
            for side,s in [('L',1),('R',-1)]:
                p=phase+(0 if side=='L' else math.pi)
                target=(s*.105,-stride*math.sin(p),.13+lift*max(0,math.cos(p)))
                solve_limb('Thigh.'+side,'Shin.'+side,target,(0,-1,0))
                bpy.context.view_layer.update()
                foot=rig.pose.bones['Foot.'+side]
                foot.matrix=Matrix.Translation(foot.head) @ foot.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
        if kind=='cast':
            bpy.context.view_layer.update()
            amount=math.sin(math.pi*t)**.7
            wrist=rig.pose.bones['Hand.L'].head.copy()
            solve_limb('UpperArm.L','Forearm.L',wrist.lerp(Vector((.20,-.46,1.30)),amount),(0,0,-1))
        # Keep the held staff upright as the wrist moves. Bake the compensation so Unity
        # needs no constraint or runtime IK for this prototype equipment socket.
        bpy.context.view_layer.update()
        socket=rig.pose.bones['StaffSocket']
        socket.matrix=Matrix.Translation(socket.head) @ socket.bone.matrix_local.to_quaternion().to_matrix().to_4x4()
        for p in rig.pose.bones:
            p.keyframe_insert('rotation_euler',frame=frame,group=p.name)
            p.keyframe_insert('location',frame=frame,group=p.name)
    return action

clips=[('Idle',61,'idle'),('Walk',31,'walk'),('Run',23,'run'),('Air',31,'air'),
       ('Land',13,'land'),('Dodge',13,'dodge'),('Cast',37,'cast'),('Hit',16,'hit')]
actions={name:animate(name,frames,kind) for name,frames,kind in clips}

def export_fbx(path, anim=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in [rig,body_mesh,staff_mesh]: obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        use_mesh_modifiers=True,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',
        bake_anim=anim,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,
        path_mode='AUTO')

rig.animation_data.action=None
reset_pose()
scene.frame_set(1)
export_fbx(EXPORT/'Kael.fbx')
for name,frames,kind in clips:
    rig.animation_data.action=actions[name]
    scene.frame_start=1
    scene.frame_end=frames
    scene.frame_set(1)
    export_fbx(EXPORT/('Kael_'+name+'.fbx'),True)

# A small studio in the source file gives future artists a useful starting view.
rig.animation_data.action=actions['Idle']
scene.frame_start=1
scene.frame_end=61
scene.frame_set(1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002))
ground=bpy.context.object
ground.name='StudioFloor_NOT_EXPORTED'
floor=material('Studio_Slate','1D2933',0,.9)
ground.data.materials.append(floor)
scene.world.color=(.18,.18,.18)
def area(name,pos,power,size,color):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=power; data.shape='DISK'; data.size=size; data.color=color
    obj=bpy.data.objects.new(name,data); scene.collection.objects.link(obj)
    obj.location=pos
    obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
area('Key',(-3,-4,5),450,4,(1,.88,.73))
area('Fill',(3,-2,2.5),260,3,(.70,.85,1))
area('Rim',(1,3,3.5),500,3,(.62,.78,1))
bpy.ops.object.camera_add(location=(2.5,-5,2.3))
cam=bpy.context.object
cam.name='Kael_PreviewCamera'
cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler()
cam.data.type='ORTHO'; cam.data.ortho_scale=2.25
scene.camera=cam
scene.render.engine='CYCLES'; scene.cycles.samples=32
scene.render.resolution_x=1000; scene.render.resolution_y=1000
scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
scene.render.filepath=str(PREVIEW/'Kael_ThreeQuarter.png')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Kael.blend'))
bpy.ops.render.render(write_still=True)
cam.location=(2.5,-5,2.3)
cam.rotation_euler=(Vector((0,0,.94))-cam.location).to_track_quat('-Z','Y').to_euler()
for clip,frame in [('Walk',9),('Run',7),('Cast',19),('Dodge',7)]:
    rig.animation_data.action=actions[clip]
    scene.frame_set(frame)
    scene.render.filepath=str(PREVIEW/('Kael_'+clip+'.png'))
    bpy.ops.render.render(write_still=True)
cam.location=(0,5,2.1)
rig.animation_data.action=actions['Idle']
scene.frame_set(1)
cam.rotation_euler=(Vector((0,0,.95))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(PREVIEW/'Kael_Back.png')
bpy.ops.render.render(write_still=True)

summary={'height_target_m':1.78,'bones':len(bones),'vertices':len(body_mesh.data.vertices)+len(staff_mesh.data.vertices),
         'triangles':sum(len(p.vertices)-2 for obj in [body_mesh,staff_mesh] for p in obj.data.polygons),
         'clips':{name:{'frames':frames,'seconds':(frames-1)/30,'loop':kind in ['idle','walk','run','air']}
                  for name,frames,kind in clips},'rig_type':'Generic; in-place motion; no facial rig or individual fingers'}
(SOURCE/'build-report.json').write_text(json.dumps(summary,indent=2)+'\n')
print('KAEL_BUILD_OK '+json.dumps(summary))

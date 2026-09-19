import bpy,json
from pathlib import Path
root=Path(__file__).resolve().parents[2]/'ArtSource/DragonInspectionV2'
bpy.ops.wm.open_mainfile(filepath=str(root/'DragonV2_Inspected.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
for b in rig.data.bones:
    g=mesh.vertex_groups.get(b.name)
    vertices=[v for v in mesh.data.vertices if g and any(w.group==g.index and w.weight>.1 for w in v.groups)]
    print(json.dumps({'bone':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local),'weighted':len(vertices)}))
print('UNWEIGHTED',sum(not any(g.weight>0 for g in v.groups) for v in mesh.data.vertices))

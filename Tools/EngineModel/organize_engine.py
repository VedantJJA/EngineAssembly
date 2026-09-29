"""Run in Blender against the ORIGINAL Assets/Model/Edit/3cyl.blend."""
import bpy, os, re, json, csv, collections
from mathutils import Vector

ROOT=os.path.dirname(os.path.dirname(os.path.dirname(__file__)))
OUT=os.path.join(ROOT,'Assets','Model','Edit')
REVIEW=os.path.join(ROOT,'ModelReview')
original=list(bpy.context.scene.objects)
original_nonmeshes=[o for o in original if o.type!='MESH']
meshes=[o for o in original if o.type=='MESH']
assert len(meshes)==430, 'Unexpected source; refusing to process a different model'
source={o.name:o for o in meshes}
world={o.name:o.matrix_world.copy() for o in meshes}
centers={o.name:sum((o.matrix_world @ Vector(c) for c in o.bound_box),Vector())/8 for o in meshes}
vertex_count=sum(len(o.data.vertices) for o in meshes)
face_count=sum(len(o.data.polygons) for o in meshes)

chapters={1:'Block_and_Crankshaft',2:'Pistons_and_Connecting_Rods',3:'Lower_Crankcase_and_Oil_Sump',4:'Cylinder_Head_and_Valves',5:'Camshafts_and_Timing_Drive',6:'Head_Cover_and_Fuel_System',7:'Turbocharger_and_Exhaust',8:'Intake_and_External_Plumbing',9:'Accessories_and_Motor'}
# source part : chapter, within-chapter step, readable label
parts={
1:(1,3,'Crankshaft'),2:(3,5,'Flywheel'),3:(2,2,'Connecting_Rod'),4:(3,6,'Flywheel_Retaining_Plate'),5:(2,6,'Connecting_Rod_Big_End_Cap'),6:(2,1,'Piston'),7:(2,4,'Piston_Ring'),8:(1,1,'Cylinder_Block'),9:(3,2,'Lower_Crankcase_Bedplate'),10:(1,2,'Main_Bearing_Shell'),11:(5,5,'Crankshaft_Pulley_Key'),12:(5,6,'Crankshaft_Pulley'),13:(5,7,'Crankshaft_Pulley_Washer'),14:(3,4,'Oil_Sump'),15:(3,3,'Oil_Sump_Baffle_Plate'),16:(4,1,'Cylinder_Head'),17:(4,4,'Exhaust_Valve'),18:(4,4,'Intake_Valve'),19:(4,3,'Valve_Stem_Seal'),20:(4,7,'Valve_Keeper_Half'),21:(4,6,'Valve_Spring_Retainer'),22:(4,2,'Valve_Spring_Seat'),23:(4,5,'Valve_Spring'),24:(5,2,'Intake_Camshaft'),25:(5,2,'Exhaust_Camshaft'),26:(5,3,'Camshaft_Pulley_Key'),27:(5,4,'Camshaft_Pulley_Hub'),28:(5,7,'Camshaft_Pulley_Washer'),29:(5,6,'Camshaft_Timing_Pulley'),30:(6,1,'Cylinder_Head_Cover'),31:(5,8,'Timing_Idler_Mounting_Plate'),32:(5,11,'Timing_Idler_Pulley'),33:(5,9,'Timing_Idler_Shaft'),34:(5,12,'Timing_Belt'),35:(6,2,'Fuel_Injector'),36:(6,5,'Crankcase_Breather_Housing'),37:(6,3,'Fuel_Rail'),38:(6,6,'Breather_Hose'),39:(6,4,'Fuel_Return_Line'),40:(7,1,'Turbocharger_Housing_and_Exhaust_Manifold'),41:(7,4,'Turbo_Compressor_Wheel'),42:(7,4,'Turbo_Turbine_Wheel'),43:(7,2,'Turbo_Rotor_Shaft'),44:(7,5,'Turbo_Outlet_Band_Clamp'),45:(7,6,'Exhaust_Outlet_and_Catalyst_Housing'),46:(9,3,'Alternator'),47:(9,4,'Starter_Motor'),48:(9,1,'Accessory_Upper_Mounting_Bracket'),49:(9,1,'Accessory_Lower_Mounting_Bracket'),50:(9,5,'Alternator_Pulley'),51:(5,1,'Front_Engine_Cover'),52:(9,2,'Accessory_Mounting_Spacer_A'),53:(9,2,'Accessory_Mounting_Spacer_B'),54:(9,6,'Accessory_Idler_Shaft'),55:(9,10,'Accessory_Drive_Belt'),56:(8,1,'Intake_Manifold'),57:(8,2,'Intake_Pipe_Fitting'),58:(8,3,'External_Service_Tube'),59:(8,2,'Intake_Pipe_Union')}
uncertain={15:'Plate inside sump; baffle function inferred.',36:'Breather function inferred from cover location and hose.',38:'Hose purpose inferred from connection to cover-mounted housing.',39:'Fuel return function inferred from injector connections.',45:'Exhaust canister identified geometrically; catalyst content unverified.',48:'Accessory mounting bracket; OEM name unavailable.',49:'Accessory mounting bracket; OEM name unavailable.',52:'Accessory mounting spacer; OEM function unverified.',53:'Accessory mounting spacer; OEM function unverified.',57:'Intake fitting; exact service function unverified.',58:'External tube; exact fluid/service function unverified.',59:'Intake union; exact service function unverified.'}

def empty(name,parent=None):
    o=bpy.data.objects.new(name,None); bpy.context.scene.collection.objects.link(o);o.parent=parent;o.empty_display_type='PLAIN_AXES';o.empty_display_size=.06
    return o
root=empty('000_Three_Cylinder_Engine')
root['source_format']='SolidWorks SLDASM converted to BLEND'
root['assembly_convention']='Ascending three-digit prefix; equal prefixes are unordered peers within a chapter.'
root['scale_note']='Original CAD-conversion world geometry and scale preserved; physical units not calibrated.'
groups={}
for ch,label in chapters.items():
    g=empty(f'{ch*100:03d}_Chapter_{ch:02d}_{label}',root);g['chapter_order']=ch;g['assembly_priority']=ch*100;groups[ch]=g
groups[2]['repair_reusable']=True
groups[2]['repair_suggestion']='Reuse a Cylinder subgroup for piston / connecting-rod service exercises.'
subgroups={}
def subgroup(ch,label):
    k=(ch,label)
    if k not in subgroups:
        subgroups[k]=empty(f'{ch*100:03d}_{label}',groups[ch]);subgroups[k]['chapter_order']=ch;subgroups[k]['assembly_priority']=ch*100
    return subgroups[k]
def cylinder(c):return min(range(3),key=lambda i:abs(c.y-[-.226,-.558,-.890][i]))+1
manifest=[];counts=collections.Counter()
def assign(o,ch,step,label,group='',fastener=False,note='',sources=None):
    old=o.name; matrix=o.matrix_world.copy(); key=(ch,step,label,group);counts[key]+=1
    suffix=f'_{counts[key]:02d}'
    if group:suffix='_'+group.replace('Cylinder_','Cyl')+suffix
    o.name=f'{ch*100+step:03d}_{label}{suffix}'
    o.parent=subgroup(ch,group) if group else groups[ch];o.matrix_world=matrix
    o['assembly_priority']=ch*100+step;o['chapter_order']=ch;o['display_name']=label.replace('_',' ')
    o['cad_source_name']=old if sources is None else json.dumps(sources)
    o['is_fastener']=fastener;o['identification_note']=note or 'Identified from CAD name, shape, and installed location.'
    o['identification_confidence']='provisional' if note else 'geometry_supported'
    if fastener:o['priority_rule']='All bolts, screws and nuts in this chapter share this priority.'
    # Keep separate CAD meshes and original materials. Only make data single-user to name it safely.
    if o.data.users>1:o.data=o.data.copy()
    o.data.name=o.name+'_Mesh'
    for col in list(o.users_collection):col.objects.unlink(o)
    chapter_col=bpy.data.collections.get(groups[ch].name)
    if not chapter_col:
        chapter_col=bpy.data.collections.new(groups[ch].name);bpy.context.scene.collection.children.link(chapter_col)
    chapter_col.objects.link(o)
    for cad in sources or [old]:manifest.append(dict(source_name=cad,object_name=o.name,chapter=ch,chapter_name=chapters[ch],assembly_priority=ch*100+step,parent=o.parent.name,is_fastener=fastener,identification_confidence=o['identification_confidence'],note=o['identification_note']))

motor=[o for o in meshes if o.name.startswith('asdasdasdasdasdasd-1_')]
assert len(motor)==113
bpy.ops.object.select_all(action='DESELECT')
for o in motor:o.hide_set(False);o.hide_viewport=False;o.select_set(True)
bpy.context.view_layer.objects.active=motor[0]
motor_names=sorted(o.name for o in motor)
bpy.ops.object.join();motor_object=bpy.context.object
assign(motor_object,9,8,'Motor_Unit_GSwirl',note='User-requested motor unit: merged original GSwirl internals and housing/pulley. Belt-driven accessory; exact OEM function unverified.',sources=motor_names)
motor_object['merged_source_count']=113

for old,o in source.items():
    if old in motor_names:continue
    c=centers[old];base=re.sub(r'\.\d+$','',old)
    if base.startswith('Part'):
        p=int(re.match(r'Part(\d+)',base)[1]);ch,step,label=parts[p];group='';note=uncertain.get(p,'')
        if p==10 and c.z<0:ch,step,label=3,1,'Lower_Main_Bearing_Shell'
        elif p==10:label='Upper_Main_Bearing_Shell'
        if p in (3,5,6,7):group=f'Cylinder_{cylinder(c):02d}'
        if p==7:
            piston_z=.676 if cylinder(c)!=2 else .434
            ring=min(range(3),key=lambda i:abs(c.z-(piston_z+.075-i*.02)))
            label=['Top_Compression_Ring','Second_Compression_Ring','Oil_Control_Ring'][ring]
        if 17<=p<=23:group=f'Cylinder_{cylinder(c):02d}_Valves'
        if p==32 and 'CPY' in base:ch,step,label=9,7,'Accessory_Idler_Pulley'
        assign(o,ch,step,label,group,note=note);continue
    if base=='GSwirl_Wires':assign(o,9,9,'Motor_Wiring',note='Original GSwirl_Wires object; disconnected source routing retained.');continue
    # Fastener chapter assignment uses actual installed location, not only the CAD nominal size.
    if base.startswith(('07160','07210')):ch=9
    elif base.startswith('ISO 4017'):ch=3
    elif base.startswith('ISO 4035'):ch=5
    elif base.startswith('ISO 4162 M10'):ch=3
    elif base.startswith('ISO 4162 M12'):ch=5
    elif base.startswith('ISO 4162 M16'):ch=3
    elif base.startswith('ISO 4162 M8 x 35'):ch=5
    elif base.startswith('ISO 4162 M8 x 80'):ch=3 if c.z<0 else (4 if c.z<1.2 else 6)
    elif base.startswith('ISO 4762 M4'):ch=7
    elif base.startswith('ISO 4762 M6'):ch=6 if c.z>1.2 else 7
    elif base.startswith('ISO 4762 M8 x 12'):ch=7
    elif base.startswith('ISO 4762 M8 x 20'):ch=6 if c.z>1.4 else 5
    elif base.startswith(('ISO 4762','ISO 7045')):ch=3
    elif base.startswith('JIS B 117'):ch=2
    else:ch=None
    if ch:
        kind='Nut' if base.startswith(('07210','ISO 4035','JIS B 1170')) else ('Stud' if base.startswith('JIS B 1173') else 'Bolt')
        if base.startswith('ISO 7045'):kind='Screw'
        label=kind+'_'+re.sub('[^A-Za-z0-9]+','_',base).strip('_')
        assign(o,ch,90,label,f'Cylinder_{cylinder(c):02d}' if ch==2 else 'Fasteners',fastener=True);continue
    if base.startswith('ISO 8734'):assign(o,2,3,'Piston_Wrist_Pin',f'Cylinder_{cylinder(c):02d}');continue
    if base.startswith('JIS B 2804 12x1'):assign(o,2,5,'Wrist_Pin_Circlip',f'Cylinder_{cylinder(c):02d}');continue
    if base.startswith('ISO 15 '):assign(o,7,3,'Turbo_Shaft_Bearing_10x15x3');continue
    if base.startswith('ISO 3030'):
        if '25 x 29' in base:assign(o,5,1,'Camshaft_Needle_Bearing_25x29x8')
        else:assign(o,1,2,'Crankshaft_Needle_Bearing_'+('45x50x10' if '45 x 50' in base else '60x66x12'))
        continue
    if base.startswith('JIS B 1521'):assign(o,9 if c.z<.5 else 5,7 if c.z<.5 else 10,'Idler_Ball_Bearing_6203');continue
    if base.startswith('JIS B 2804 10x1'):assign(o,7,3,'Turbo_Shaft_Circlip');continue
    if base.startswith(('JIS B 2804 17x1','JIS B 2804 40x')):
        assign(o,9 if c.z<.5 else 5,7 if c.z<.5 else 11,'Idler_'+('Shaft' if '17x1' in base else 'Bore')+'_Circlip');continue
    raise RuntimeError('Unclassified source: '+old)

for o in original_nonmeshes:
    bpy.data.objects.remove(o,do_unlink=True)
for col in list(bpy.data.collections):
    if len(col.objects)==0 and len(col.children)==0:bpy.data.collections.remove(col)
bpy.context.view_layer.update()
final_meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(final_meshes)==318
assert len(manifest)==430
assert sum(len(o.data.vertices) for o in final_meshes)==vertex_count
assert sum(len(o.data.polygons) for o in final_meshes)==face_count
max_delta=0
for row in manifest:
    if row['source_name'] in motor_names:continue
    delta=max(abs(bpy.data.objects[row['object_name']].matrix_world[r][c]-world[row['source_name']][r][c]) for r in range(4) for c in range(4))
    max_delta=max(max_delta,delta)
assert max_delta<1e-5,max_delta
for ch in chapters:
    assert len(set(r['assembly_priority'] for r in manifest if r['chapter']==ch and r['is_fastener']))<=1
assert all(re.match(r'^\d{3}_',o.name) for o in bpy.context.scene.objects)
with open(os.path.join(REVIEW,'parts_manifest.csv'),'w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=list(manifest[0]));w.writeheader();w.writerows(sorted(manifest,key=lambda r:(r['assembly_priority'],r['object_name'],r['source_name'])))
with open(os.path.join(REVIEW,'validation.json'),'w') as f:json.dump(dict(source_meshes=430,output_meshes=318,merged_motor_meshes=113,source_vertices=vertex_count,output_vertices=sum(len(o.data.vertices) for o in final_meshes),source_faces=face_count,output_faces=sum(len(o.data.polygons) for o in final_meshes),max_world_transform_error=max_delta,chapter_count=9,manifest_source_rows=len(manifest)),f,indent=2)
# Clean selection and useful initial viewport, without adding cameras to the asset.
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);bpy.context.view_layer.objects.active=root
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=Vector((0,-.55,.4));area.spaces.active.region_3d.view_distance=4
            area.spaces.active.region_3d.view_rotation=Vector((4,5,3)).to_track_quat('Z','Y')
            area.spaces.active.shading.color_type='MATERIAL'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'3cyl_Labeled.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'3cyl_Labeled.fbx'),use_selection=False,object_types={'EMPTY','MESH'},global_scale=1.0,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,use_custom_props=True,path_mode='AUTO')
print('DONE: 318 meshes, 9 chapters, 113 motor parts merged; original file unchanged.')

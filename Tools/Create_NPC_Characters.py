"""
Create 5 Chibi NPCs for The Last Empire
Each character is built with high-quality Toy-Chibi geometry,
fully rigged to Chibi_Base_Rig (18 bones), perfectly sharing the player's 10-clip animation suite.

NPCs:
1. NPC_Zack_Merchant   - Arms Dealer / Outpost Merchant (Trench vest, ammo bandolier, eyepatch)
2. NPC_Elena_Medic      - Field Combat Medic (White lab coat, red cross armband, medic cap)
3. NPC_Otto_Gunsmith    - Imperial Gunsmith (Leather blacksmith apron, welding goggles, tool belt)
4. NPC_Maya_Scout       - Wasteland Ranger / Informant (Desert cowl/hood, recon visor, daggers)
5. NPC_Roy_Veteran      - Old Scavenger Veteran (Weathered fur duster, scrap pauldron, backpack)
"""

import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Quaternion, Euler

PROJECT_ROOT = r"d:\TLEGIT\TheLastEmpire"
OUTPUT_DIR = os.path.join(PROJECT_ROOT, "Assets", "GeneratedModels", "NPCs")
os.makedirs(OUTPUT_DIR, exist_ok=True)

# ==============================================================================
# 1. MATERIAL BUILDER (URP-READY PRINCIPLED BSDF)
# ==============================================================================
def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def make_mat(name, rgba, roughness=0.85, metallic=0.0, specular=0.05):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    lin_rgba = (srgb_to_lin(rgba[0]), srgb_to_lin(rgba[1]), srgb_to_lin(rgba[2]), rgba[3])
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = lin_rgba
        bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = specular
        elif "Specular" in bsdf.inputs:
            bsdf.inputs["Specular"].default_value = specular
    mat.diffuse_color = lin_rgba
    mat.roughness = roughness
    mat.metallic = metallic
    return mat

# ==============================================================================
# 2. PROCEDURAL GEOMETRY HELPERS
# ==============================================================================
def assign_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.material_index = 0
        p.use_smooth = True
    return obj

def create_ellipsoid(name, center, rx, ry, rz, mat, seg_u=20, seg_v=14):
    bm = bmesh.new()
    verts = []
    # North pole
    v_top = bm.verts.new(center + Vector((0, 0, rz)))
    # Rings
    for j in range(1, seg_v):
        phi = math.pi * j / seg_v
        ring = []
        for i in range(seg_u):
            theta = 2.0 * math.pi * i / seg_u
            x = center.x + rx * math.sin(phi) * math.cos(theta)
            y = center.y + ry * math.sin(phi) * math.sin(theta)
            z = center.z + rz * math.cos(phi)
            ring.append(bm.verts.new((x, y, z)))
        verts.append(ring)
    # South pole
    v_bot = bm.verts.new(center + Vector((0, 0, -rz)))

    # Top cap
    for i in range(seg_u):
        bm.faces.new((v_top, verts[0][(i + 1) % seg_u], verts[0][i]))
    # Middle quads
    for j in range(len(verts) - 1):
        r0 = verts[j]
        r1 = verts[j + 1]
        for i in range(seg_u):
            ni = (i + 1) % seg_u
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))
    # Bottom cap
    for i in range(seg_u):
        bm.faces.new((verts[-1][i], verts[-1][(i + 1) % seg_u], v_bot))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_mat(o, mat)

def create_cylinder(name, pt_a, pt_b, radius_a, radius_b, mat, seg=16, cap=True):
    bm = bmesh.new()
    axis = pt_b - pt_a
    length = axis.length
    if length < 1e-6:
        length = 1e-6
        axis = Vector((0, 0, 1))
    dir_v = axis.normalized()
    up = Vector((0, 0, 1)) if abs(dir_v.z) < 0.9 else Vector((1, 0, 0))
    u = dir_v.cross(up).normalized()
    v = dir_v.cross(u).normalized()

    ring_a = []
    ring_b = []
    for i in range(seg):
        ang = 2.0 * math.pi * i / seg
        ca = math.cos(ang)
        sa = math.sin(ang)
        ring_a.append(bm.verts.new(pt_a + u * (radius_a * ca) + v * (radius_a * sa)))
        ring_b.append(bm.verts.new(pt_b + u * (radius_b * ca) + v * (radius_b * sa)))

    for i in range(seg):
        ni = (i + 1) % seg
        bm.faces.new((ring_a[i], ring_a[ni], ring_b[ni], ring_b[i]))

    if cap:
        c_a = bm.verts.new(pt_a)
        c_b = bm.verts.new(pt_b)
        for i in range(seg):
            ni = (i + 1) % seg
            bm.faces.new((c_a, ring_a[ni], ring_a[i]))
            bm.faces.new((c_b, ring_b[i], ring_b[ni]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_mat(o, mat)

def create_box(name, center, size, mat):
    bm = bmesh.new()
    hx, hy, hz = size.x * 0.5, size.y * 0.5, size.z * 0.5
    cx, cy, cz = center.x, center.y, center.z
    v = [
        bm.verts.new((cx - hx, cy - hy, cz - hz)),
        bm.verts.new((cx + hx, cy - hy, cz - hz)),
        bm.verts.new((cx + hx, cy + hy, cz - hz)),
        bm.verts.new((cx - hx, cy + hy, cz - hz)),
        bm.verts.new((cx - hx, cy - hy, cz + hz)),
        bm.verts.new((cx + hx, cy - hy, cz + hz)),
        bm.verts.new((cx + hx, cy + hy, cz + hz)),
        bm.verts.new((cx - hx, cy + hy, cz + hz)),
    ]
    bm.faces.new((v[0], v[3], v[2], v[1])) # Bottom
    bm.faces.new((v[4], v[5], v[6], v[7])) # Top
    bm.faces.new((v[0], v[1], v[5], v[4])) # Front
    bm.faces.new((v[2], v[3], v[7], v[6])) # Back
    bm.faces.new((v[1], v[2], v[6], v[5])) # Right
    bm.faces.new((v[3], v[0], v[4], v[7])) # Left

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_mat(o, mat)

def create_face_disc(name, cx, cz, rx, rz, mat, cy=-0.375, seg=16):
    bm = bmesh.new()
    verts = []
    c0 = bm.verts.new((cx, cy, cz))
    for i in range(seg):
        ang = 2.0 * math.pi * i / seg
        x = cx + rx * math.cos(ang)
        z = cz + rz * math.sin(ang)
        # Spherical wrap onto curved head surface
        dist = math.sqrt(x * x + (z - 1.76)**2)
        y = cy + (dist * dist) * 0.18
        verts.append(bm.verts.new((x, y, z)))
    for i in range(seg):
        bm.faces.new((c0, verts[i], verts[(i + 1) % seg]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_mat(o, mat)

def join_objects(obj_list, final_name):
    if not obj_list:
        return None
    valid = [o for o in obj_list if o and o.name in bpy.data.objects]
    if not valid:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in valid:
        o.select_set(True)
    bpy.context.view_layer.objects.active = valid[0]
    bpy.ops.object.join()
    res = bpy.context.view_layer.objects.active
    res.name = final_name
    return res

# ==============================================================================
# 3. 18-BONE CHIBI ARMATURE SPECIFICATION (Exact match to player rig)
# ==============================================================================
BONE_DEFS = [
    ("Root",          (0.0, 0.0, 0.02),    (0.0, 0.0, 0.66),    None,            (0, -1, 0)),
    ("Hips",          (0.0, 0.0, 0.66),    (0.0, 0.0, 0.86),    "Root",          (0, -1, 0)),
    ("Spine",         (0.0, 0.0, 0.86),    (0.0, 0.0, 1.06),    "Hips",          (0, -1, 0)),
    ("Chest",         (0.0, 0.0, 1.06),    (0.0, 0.0, 1.28),    "Spine",         (0, -1, 0)),
    ("Neck",          (0.0, 0.0, 1.28),    (0.0, 0.0, 1.48),    "Chest",         (0, -1, 0)),
    ("Head",          (0.0, 0.0, 1.48),    (0.0, 0.0, 2.08),    "Neck",          (0, -1, 0)),

    ("LeftShoulder",  (0.16, 0.0, 1.23),   (0.27, 0.0, 1.23),   "Chest",         (0, 0, 1)),
    ("LeftUpperArm",  (0.27, 0.0, 1.23),   (0.50, 0.0, 1.23),   "LeftShoulder",  (0, 0, 1)),
    ("LeftLowerArm",  (0.50, 0.0, 1.23),   (0.72, 0.0, 1.23),   "LeftUpperArm",  (0, 0, 1)),
    ("LeftHand",      (0.72, 0.0, 1.23),   (0.90, 0.0, 1.23),   "LeftLowerArm",  (0, 0, 1)),

    ("RightShoulder", (-0.16, 0.0, 1.23),  (-0.27, 0.0, 1.23),  "Chest",         (0, 0, 1)),
    ("RightUpperArm", (-0.27, 0.0, 1.23),  (-0.50, 0.0, 1.23),  "RightShoulder", (0, 0, 1)),
    ("RightLowerArm", (-0.50, 0.0, 1.23),  (-0.72, 0.0, 1.23),  "RightUpperArm", (0, 0, 1)),
    ("RightHand",     (-0.72, 0.0, 1.23),  (-0.90, 0.0, 1.23),  "RightLowerArm", (0, 0, 1)),

    ("LeftUpperLeg",  (0.150, 0.0, 0.68),  (0.150, 0.0, 0.37),  "Hips",          (0, 1, 0)),
    ("LeftLowerLeg",  (0.150, 0.0, 0.37),  (0.152, 0.0, 0.11),  "LeftUpperLeg",  (0, 1, 0)),
    ("LeftFoot",      (0.152, 0.0, 0.11),  (0.152, -0.20, 0.03),"LeftLowerLeg",  (0, 0, 1)),

    ("RightUpperLeg", (-0.150, 0.0, 0.68), (-0.150, 0.0, 0.37), "Hips",          (0, 1, 0)),
    ("RightLowerLeg", (-0.150, 0.0, 0.37), (-0.152, 0.0, 0.11), "RightUpperLeg", (0, 1, 0)),
    ("RightFoot",     (-0.152, 0.0, 0.11), (-0.152, -0.20, 0.03),"RightLowerLeg", (0, 0, 1)),
]

def build_chibi_armature():
    arm_data = bpy.data.armatures.new("Chibi_Base_Rig")
    rig_obj = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')

    edit_bones = {}
    for bname, h, t, pname, roll_vec in BONE_DEFS:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in edit_bones:
            eb.parent = edit_bones[pname]
            eb.use_connect = False
        edit_bones[bname] = eb

    bpy.ops.object.mode_set(mode='OBJECT')
    return rig_obj

def point_to_segment_dist(p, a, b):
    ab = b - a
    len_sq = ab.length_squared
    if len_sq < 1e-8:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / len_sq))
    proj = a + ab * t
    return (p - proj).length

def bind_mesh_to_rig(mesh_obj, rig_obj, is_head=False):
    mesh_obj.parent = rig_obj
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = rig_obj
    mod.use_deform_preserve_volume = True

    vgroups = {}
    for bname, _, _, _, _ in BONE_DEFS:
        vgroups[bname] = mesh_obj.vertex_groups.new(name=bname)

    bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in BONE_DEFS if bname != "Root"]

    for v in mesh_obj.data.vertices:
        co = v.co
        if is_head:
            neck_w = max(0.0, min(0.25, (1.46 - co.z) * 1.4)) if co.z < 1.46 else 0.0
            vgroups["Head"].add([v.index], 1.0 - neck_w, 'REPLACE')
            if neck_w > 0.001:
                vgroups["Neck"].add([v.index], neck_w, 'REPLACE')
            continue

        weights = {}
        for bname, h, t in bones_info:
            if bname.startswith("Left") and co.x < -0.01:
                continue
            if bname.startswith("Right") and co.x > 0.01:
                continue
            if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.19 or co.z < 1.04 or (co.z > 1.285 and abs(co.x) < 0.37)):
                continue
            if "Shoulder" in bname and (abs(co.x) < 0.14 or co.z < 1.02):
                continue
            if ("Leg" in bname or "Foot" in bname) and (co.z > 0.72 or abs(co.x) > 0.32):
                continue
            if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.30 or co.z < 0.54):
                continue
            if bname == "Head" and co.z < 1.42:
                continue

            d = point_to_segment_dist(co, h, t)
            sigma = 0.120 if ("Arm" in bname or "Shoulder" in bname or "Leg" in bname) else 0.115
            w = math.exp(-(d * d) / (2.0 * (sigma ** 2)))
            if w > 1e-5:
                weights[bname] = w

        if not weights:
            weights["Hips"] = 1.0

        top = sorted(weights.items(), key=lambda item: item[1], reverse=True)[:4]
        total = sum(w for _, w in top)
        for bname, w in top:
            vgroups[bname].add([v.index], w / total, 'REPLACE')

# ==============================================================================
# 4. CHIBI BASE BODY GEOMETRY GENERATOR (Calibrated to player proportions)
# ==============================================================================
def build_base_chibi_body(mat_skin, mat_shirt, mat_pants, mat_boots, mat_belt):
    parts = []
    # Pelvis / Hips
    parts.append(create_ellipsoid("Hips", Vector((0, 0, 0.76)), 0.23, 0.18, 0.14, mat_pants))
    # Waist belt
    parts.append(create_cylinder("Belt", Vector((0, 0, 0.83)), Vector((0, 0, 0.88)), 0.22, 0.22, mat_belt))
    # Torso / Chest
    parts.append(create_ellipsoid("Chest", Vector((0, 0, 1.10)), 0.25, 0.19, 0.20, mat_shirt))
    # Neck
    parts.append(create_cylinder("Neck", Vector((0, 0, 1.26)), Vector((0, 0, 1.42)), 0.08, 0.08, mat_skin))

    # Shoulders, Arms, Hands
    for side in [1.0, -1.0]:
        sx = "Left" if side > 0 else "Right"
        # Shoulder
        parts.append(create_ellipsoid(f"{sx}_Shoulder", Vector((side * 0.25, 0, 1.23)), 0.10, 0.10, 0.10, mat_shirt))
        # Upper Arm
        parts.append(create_cylinder(f"{sx}_UpperArm", Vector((side * 0.27, 0, 1.23)), Vector((side * 0.49, 0, 1.23)), 0.082, 0.076, mat_shirt))
        # Lower Arm
        parts.append(create_cylinder(f"{sx}_LowerArm", Vector((side * 0.49, 0, 1.23)), Vector((side * 0.70, 0, 1.23)), 0.076, 0.070, mat_skin))
        # Hand (Mitten/Chibi Fist)
        parts.append(create_ellipsoid(f"{sx}_Hand", Vector((side * 0.78, 0, 1.23)), 0.09, 0.075, 0.075, mat_skin))

    # Legs & Boots
    for side in [1.0, -1.0]:
        sx = "Left" if side > 0 else "Right"
        # Thigh
        parts.append(create_cylinder(f"{sx}_Thigh", Vector((side * 0.15, 0, 0.68)), Vector((side * 0.15, 0, 0.38)), 0.095, 0.085, mat_pants))
        # Shin
        parts.append(create_cylinder(f"{sx}_Shin", Vector((side * 0.15, 0, 0.38)), Vector((side * 0.152, 0, 0.14)), 0.090, 0.085, mat_boots))
        # Boot Foot
        parts.append(create_ellipsoid(f"{sx}_Foot", Vector((side * 0.152, -0.06, 0.07)), 0.095, 0.14, 0.07, mat_boots))

    return parts

def build_base_chibi_head(mat_skin, mat_hair, mat_eye_iris, mat_eye_white, mat_mouth, eye_style="normal"):
    parts = []
    # Main cute Chibi Head sphere
    head = create_ellipsoid("Head_Base", Vector((0, 0, 1.76)), 0.42, 0.40, 0.39, mat_skin, seg_u=24, seg_v=18)
    parts.append(head)

    # Ears
    for side in [1.0, -1.0]:
        ear = create_ellipsoid(f"Ear_{side}", Vector((side * 0.42, -0.02, 1.73)), 0.045, 0.065, 0.085, mat_skin)
        ear.rotation_euler = (0, math.radians(side * 15), 0)
        parts.append(ear)

    # Anime Eyes
    mat_black = make_mat("Mat_Black_Line", (0.1, 0.1, 0.12, 1.0), roughness=0.9)
    for side in [1.0, -1.0]:
        if eye_style == "eyepatch" and side > 0:
            # Zack's tactical eyepatch on right eye
            patch = create_face_disc(f"Eyepatch", side * 0.16, 1.74, 0.10, 0.09, mat_black)
            parts.append(patch)
            # Strap
            strap = create_cylinder("EyeStrap", Vector((side * 0.16, -0.36, 1.74)), Vector((side * 0.38, 0.1, 1.82)), 0.015, 0.015, mat_black)
            parts.append(strap)
            continue

        ex = side * 0.16
        ez = 1.73
        # Eye white
        parts.append(create_face_disc(f"EyeWhite_{side}", ex, ez, 0.085, 0.068, mat_eye_white))
        # Iris
        parts.append(create_face_disc(f"Iris_{side}", ex, ez - 0.005, 0.055, 0.050, mat_eye_iris))
        # Pupil / Dark Lash
        parts.append(create_face_disc(f"Pupil_{side}", ex, ez - 0.005, 0.025, 0.025, mat_black))
        # Eyebrow
        parts.append(create_face_disc(f"Brow_{side}", ex, ez + 0.09, 0.065, 0.015, mat_hair))

    # Cute Mouth
    mouth = create_face_disc("Mouth", 0.0, 1.58, 0.04, 0.02, mat_mouth)
    parts.append(mouth)

    return parts

# ==============================================================================
# 5. SPECIFIC NPC BUILDERS
# ==============================================================================

# ------------------------------------------------------------------------------
# NPC 1: ZACK (Outpost Merchant & Arms Dealer)
# ------------------------------------------------------------------------------
def build_npc_zack():
    mat_skin = make_mat("Zack_Skin", (0.94, 0.78, 0.68, 1.0))
    mat_hair = make_mat("Zack_Hair", (0.22, 0.16, 0.12, 1.0))
    mat_vest = make_mat("Zack_Vest", (0.35, 0.28, 0.20, 1.0)) # Weathered brown leather
    mat_shirt = make_mat("Zack_Shirt", (0.48, 0.50, 0.42, 1.0)) # Olive drab
    mat_pants = make_mat("Zack_Pants", (0.25, 0.28, 0.22, 1.0)) # Cargo green
    mat_boots = make_mat("Zack_Boots", (0.15, 0.14, 0.14, 1.0))
    mat_belt = make_mat("Zack_Belt", (0.12, 0.10, 0.08, 1.0))
    mat_gold = make_mat("Zack_Gold", (0.85, 0.65, 0.15, 1.0), roughness=0.3, metallic=0.7)
    mat_eye = make_mat("Zack_Eye", (0.30, 0.55, 0.70, 1.0)) # Steel blue
    mat_eye_white = make_mat("Zack_EyeWhite", (0.98, 0.98, 0.99, 1.0))
    mat_mouth = make_mat("Zack_Mouth", (0.5, 0.15, 0.18, 1.0))

    head_parts = build_base_chibi_head(mat_skin, mat_hair, mat_eye, mat_eye_white, mat_mouth, eye_style="eyepatch")
    body_parts = build_base_chibi_body(mat_skin, mat_shirt, mat_pants, mat_boots, mat_belt)

    # Zack Hair: Slicked back volume + side fringe
    hair_dome = create_ellipsoid("Zack_HairDome", Vector((0, 0.06, 1.88)), 0.44, 0.43, 0.32, mat_hair)
    head_parts.append(hair_dome)
    hair_front = create_ellipsoid("Zack_HairFront", Vector((-0.12, -0.28, 2.02)), 0.18, 0.15, 0.10, mat_hair)
    head_parts.append(hair_front)

    # Zack Outfit Additions:
    # Leather Vest over chest
    vest = create_cylinder("Zack_VestBody", Vector((0, 0, 0.90)), Vector((0, 0, 1.25)), 0.27, 0.28, mat_vest)
    body_parts.append(vest)
    # Vest Collar
    collar = create_cylinder("Zack_Collar", Vector((0, 0, 1.23)), Vector((0, 0, 1.35)), 0.24, 0.27, mat_vest)
    body_parts.append(collar)

    # Ammo Bandolier across chest
    band_strap = create_cylinder("Zack_Bandolier", Vector((-0.20, -0.15, 1.28)), Vector((0.20, -0.15, 0.92)), 0.045, 0.045, mat_belt)
    body_parts.append(band_strap)
    for k in range(5):
        t = (k + 0.5) / 5.0
        bx = -0.20 + 0.40 * t
        bz = 1.28 - 0.36 * t
        shell = create_cylinder(f"Shell_{k}", Vector((bx, -0.22, bz)), Vector((bx, -0.16, bz)), 0.016, 0.016, mat_gold)
        body_parts.append(shell)

    # Hip Pouches
    pouch_l = create_box("Pouch_L", Vector((0.24, 0, 0.85)), Vector((0.08, 0.12, 0.10)), mat_belt)
    pouch_r = create_box("Pouch_R", Vector((-0.24, 0, 0.85)), Vector((0.08, 0.12, 0.10)), mat_belt)
    body_parts.extend([pouch_l, pouch_r])

    head_obj = join_objects(head_parts, "NPC_Zack_Head")
    body_obj = join_objects(body_parts, "NPC_Zack_Body")
    return head_obj, body_obj

# ------------------------------------------------------------------------------
# NPC 2: DR. ELENA (Field Combat Medic)
# ------------------------------------------------------------------------------
def build_npc_elena():
    mat_skin = make_mat("Elena_Skin", (0.98, 0.84, 0.76, 1.0))
    mat_hair = make_mat("Elena_Hair", (0.24, 0.18, 0.32, 1.0)) # Deep violet-brown
    mat_coat = make_mat("Elena_Coat", (0.95, 0.96, 0.97, 1.0)) # Sterile lab coat white
    mat_trim = make_mat("Elena_Mint", (0.20, 0.75, 0.58, 1.0)) # Mint green
    mat_cross = make_mat("Elena_RedCross", (0.90, 0.10, 0.15, 1.0)) # Red cross
    mat_pants = make_mat("Elena_Pants", (0.22, 0.28, 0.36, 1.0)) # Clean Navy
    mat_boots = make_mat("Elena_Boots", (0.90, 0.92, 0.94, 1.0)) # White boots
    mat_gloves = make_mat("Elena_Gloves", (0.25, 0.70, 0.75, 1.0)) # Cyan surgical gloves
    mat_eye = make_mat("Elena_Eye", (0.15, 0.65, 0.55, 1.0)) # Mint eyes
    mat_eye_white = make_mat("Elena_EyeWhite", (0.99, 0.99, 1.00, 1.0))
    mat_mouth = make_mat("Elena_Mouth", (0.85, 0.35, 0.40, 1.0))

    head_parts = build_base_chibi_head(mat_skin, mat_hair, mat_eye, mat_eye_white, mat_mouth)
    body_parts = build_base_chibi_body(mat_skin, mat_coat, mat_pants, mat_boots, mat_trim)

    # Elena Hair: Neat bob + back bun
    hair_main = create_ellipsoid("Elena_HairMain", Vector((0, 0.05, 1.86)), 0.44, 0.42, 0.34, mat_hair)
    hair_bun = create_ellipsoid("Elena_HairBun", Vector((0, 0.38, 1.86)), 0.18, 0.16, 0.18, mat_hair)
    head_parts.extend([hair_main, hair_bun])

    # Medic Cap with Red Cross
    cap = create_box("Elena_Cap", Vector((0, -0.05, 2.15)), Vector((0.34, 0.28, 0.14)), mat_coat)
    cross_h = create_box("Cap_CrossH", Vector((0, -0.20, 2.15)), Vector((0.14, 0.02, 0.04)), mat_cross)
    cross_v = create_box("Cap_CrossV", Vector((0, -0.20, 2.15)), Vector((0.04, 0.02, 0.14)), mat_cross)
    head_parts.extend([cap, cross_h, cross_v])

    # Red Cross Armband on left arm
    armband = create_cylinder("Elena_Armband", Vector((0.36, 0, 1.23)), Vector((0.44, 0, 1.23)), 0.084, 0.084, mat_coat)
    cross_arm = create_box("ArmCross", Vector((0.40, -0.085, 1.23)), Vector((0.06, 0.02, 0.06)), mat_cross)
    body_parts.extend([armband, cross_arm])

    # Medical Bag / Satchel on right hip
    med_bag = create_box("MedBag", Vector((-0.26, 0, 0.80)), Vector((0.10, 0.20, 0.16)), mat_coat)
    med_cross = create_box("MedBagCross", Vector((-0.315, 0, 0.80)), Vector((0.02, 0.10, 0.10)), mat_cross)
    bag_strap = create_cylinder("MedStrap", Vector((0.20, -0.15, 1.25)), Vector((-0.26, 0, 0.88)), 0.025, 0.025, mat_trim)
    body_parts.extend([med_bag, med_cross, bag_strap])

    head_obj = join_objects(head_parts, "NPC_Elena_Head")
    body_obj = join_objects(body_parts, "NPC_Elena_Body")
    return head_obj, body_obj

# ------------------------------------------------------------------------------
# NPC 3: OTTO (Imperial Gunsmith & Mechanic)
# ------------------------------------------------------------------------------
def build_npc_otto():
    mat_skin = make_mat("Otto_Skin", (0.92, 0.74, 0.62, 1.0))
    mat_hair = make_mat("Otto_Hair", (0.85, 0.40, 0.10, 1.0)) # Burnt orange
    mat_apron = make_mat("Otto_Apron", (0.42, 0.22, 0.12, 1.0)) # Heavy leather
    mat_shirt = make_mat("Otto_Shirt", (0.18, 0.24, 0.36, 1.0)) # Work denim
    mat_pants = make_mat("Otto_Pants", (0.16, 0.18, 0.22, 1.0)) # Charcoal work pants
    mat_boots = make_mat("Otto_Boots", (0.10, 0.10, 0.12, 1.0)) # Steel toe
    mat_belt = make_mat("Otto_Belt", (0.15, 0.12, 0.10, 1.0))
    mat_steel = make_mat("Otto_Steel", (0.65, 0.68, 0.72, 1.0), roughness=0.35, metallic=0.75)
    mat_goggle_lens = make_mat("Otto_Lens", (0.10, 0.85, 0.95, 1.0), roughness=0.1, metallic=0.1) # Glowing cyan lens
    mat_eye = make_mat("Otto_Eye", (0.90, 0.55, 0.15, 1.0)) # Amber
    mat_eye_white = make_mat("Otto_EyeWhite", (0.98, 0.98, 0.99, 1.0))
    mat_mouth = make_mat("Otto_Mouth", (0.4, 0.15, 0.15, 1.0))

    head_parts = build_base_chibi_head(mat_skin, mat_hair, mat_eye, mat_eye_white, mat_mouth)
    body_parts = build_base_chibi_body(mat_skin, mat_shirt, mat_pants, mat_boots, mat_belt)

    # Otto Spiky Hair
    hair_base = create_ellipsoid("Otto_HairBase", Vector((0, 0.05, 1.88)), 0.45, 0.42, 0.35, mat_hair)
    head_parts.append(hair_base)
    for idx, (hx, hy, hz) in enumerate([(-0.25, -0.1, 2.15), (0.0, -0.15, 2.22), (0.25, -0.1, 2.15), (0.0, 0.25, 2.18)]):
        spike = create_cylinder(f"Spike_{idx}", Vector((hx*0.6, hy*0.6, 2.0)), Vector((hx, hy, hz)), 0.10, 0.02, mat_hair)
        head_parts.append(spike)

    # Flip-up Welding Goggles on forehead
    goggle_strap = create_cylinder("GoggleStrap", Vector((-0.43, 0.0, 1.95)), Vector((0.43, 0.0, 1.95)), 0.03, 0.03, mat_belt)
    lens_l = create_cylinder("Lens_L", Vector((0.14, -0.38, 1.96)), Vector((0.14, -0.44, 1.96)), 0.075, 0.070, mat_steel)
    glass_l = create_face_disc("Glass_L", 0.14, 1.96, 0.055, 0.055, mat_goggle_lens, cy=-0.445)
    lens_r = create_cylinder("Lens_R", Vector((-0.14, -0.38, 1.96)), Vector((-0.14, -0.44, 1.96)), 0.075, 0.070, mat_steel)
    glass_r = create_face_disc("Glass_R", -0.14, 1.96, 0.055, 0.055, mat_goggle_lens, cy=-0.445)
    bridge = create_box("GoggleBridge", Vector((0, -0.40, 1.96)), Vector((0.08, 0.04, 0.03)), mat_steel)
    head_parts.extend([goggle_strap, lens_l, glass_l, lens_r, glass_r, bridge])

    # Leather Blacksmith Apron
    apron_chest = create_box("ApronChest", Vector((0, -0.18, 1.08)), Vector((0.26, 0.04, 0.28)), mat_apron)
    apron_skirt = create_box("ApronSkirt", Vector((0, -0.16, 0.68)), Vector((0.30, 0.04, 0.32)), mat_apron)
    body_parts.extend([apron_chest, apron_skirt])

    # Large Wrench on right hip
    wrench_handle = create_cylinder("WrenchHandle", Vector((-0.26, -0.05, 0.95)), Vector((-0.26, -0.05, 0.65)), 0.02, 0.02, mat_steel)
    wrench_head = create_box("WrenchHead", Vector((-0.26, -0.05, 0.98)), Vector((0.08, 0.04, 0.08)), mat_steel)
    body_parts.extend([wrench_handle, wrench_head])

    # Steel Knee pads
    for side in [1.0, -1.0]:
        knee = create_ellipsoid(f"Knee_{side}", Vector((side * 0.15, -0.08, 0.38)), 0.06, 0.04, 0.07, mat_steel)
        body_parts.append(knee)

    head_obj = join_objects(head_parts, "NPC_Otto_Head")
    body_obj = join_objects(body_parts, "NPC_Otto_Body")
    return head_obj, body_obj

# ------------------------------------------------------------------------------
# NPC 4: MAYA (Wasteland Scout & Ranger)
# ------------------------------------------------------------------------------
def build_npc_maya():
    mat_skin = make_mat("Maya_Skin", (0.95, 0.80, 0.72, 1.0))
    mat_hair = make_mat("Maya_Hair", (0.15, 0.12, 0.18, 1.0)) # Jet black
    mat_hood = make_mat("Maya_Hood", (0.55, 0.48, 0.38, 1.0)) # Desert Khaki cowl
    mat_scarf = make_mat("Maya_Scarf", (0.45, 0.20, 0.55, 1.0)) # Tactical purple scarf
    mat_suit = make_mat("Maya_Suit", (0.18, 0.20, 0.22, 1.0)) # Slate stealth suit
    mat_boots = make_mat("Maya_Boots", (0.12, 0.12, 0.14, 1.0))
    mat_belt = make_mat("Maya_Belt", (0.10, 0.08, 0.08, 1.0))
    mat_visor = make_mat("Maya_Visor", (0.95, 0.40, 0.10, 1.0), roughness=0.1, metallic=0.6) # Amber recon visor
    mat_eye = make_mat("Maya_Eye", (0.85, 0.45, 0.15, 1.0)) # Amber cat-eyes
    mat_eye_white = make_mat("Maya_EyeWhite", (0.99, 0.99, 1.00, 1.0))
    mat_mouth = make_mat("Maya_Mouth", (0.6, 0.2, 0.25, 1.0))
    mat_steel = make_mat("Maya_Steel", (0.7, 0.72, 0.75, 1.0), roughness=0.3, metallic=0.8)

    head_parts = build_base_chibi_head(mat_skin, mat_hair, mat_eye, mat_eye_white, mat_mouth)
    body_parts = build_base_chibi_body(mat_skin, mat_suit, mat_suit, mat_boots, mat_belt)

    # Scout Desert Cowl / Hood draped over head
    hood = create_ellipsoid("Maya_HoodBase", Vector((0, 0.04, 1.88)), 0.47, 0.45, 0.38, mat_hood)
    head_parts.append(hood)

    # Monocle Recon Visor over right eye
    monocle = create_cylinder("Maya_Monocle", Vector((0.16, -0.38, 1.74)), Vector((0.16, -0.42, 1.74)), 0.08, 0.075, mat_steel)
    glass = create_face_disc("Maya_Glass", 0.16, 1.74, 0.065, 0.065, mat_visor, cy=-0.425)
    head_parts.extend([monocle, glass])

    # Scarf wrapped around neck
    scarf_ring = create_cylinder("Maya_Scarf", Vector((0, 0, 1.28)), Vector((0, 0, 1.44)), 0.22, 0.24, mat_scarf)
    scarf_tail = create_box("Maya_ScarfTail", Vector((0.12, -0.16, 1.15)), Vector((0.08, 0.04, 0.28)), mat_scarf)
    body_parts.extend([scarf_ring, scarf_tail])

    # Twin Crossed Daggers on back
    dagger1 = create_cylinder("Dagger_1", Vector((-0.18, 0.22, 1.25)), Vector((0.18, 0.22, 0.95)), 0.02, 0.02, mat_steel)
    dagger2 = create_cylinder("Dagger_2", Vector((0.18, 0.22, 1.25)), Vector((-0.18, 0.22, 0.95)), 0.02, 0.02, mat_steel)
    body_parts.extend([dagger1, dagger2])

    head_obj = join_objects(head_parts, "NPC_Maya_Head")
    body_obj = join_objects(body_parts, "NPC_Maya_Body")
    return head_obj, body_obj

# ------------------------------------------------------------------------------
# NPC 5: ROY (Grizzled Veteran Scavenger)
# ------------------------------------------------------------------------------
def build_npc_roy():
    mat_skin = make_mat("Roy_Skin", (0.88, 0.70, 0.58, 1.0))
    mat_hair = make_mat("Roy_Hair", (0.65, 0.65, 0.68, 1.0)) # Silver/grey
    mat_coat = make_mat("Roy_Coat", (0.32, 0.25, 0.18, 1.0)) # Weathered leather duster
    mat_fur = make_mat("Roy_Fur", (0.80, 0.75, 0.68, 1.0)) # Fur collar
    mat_pants = make_mat("Roy_Pants", (0.35, 0.38, 0.28, 1.0)) # Olive weathered
    mat_boots = make_mat("Roy_Boots", (0.12, 0.10, 0.08, 1.0))
    mat_belt = make_mat("Roy_Belt", (0.15, 0.12, 0.10, 1.0))
    mat_scrap = make_mat("Roy_Scrap", (0.45, 0.48, 0.50, 1.0), roughness=0.6, metallic=0.7) # Rusty scrap plate
    mat_eye = make_mat("Roy_Eye", (0.40, 0.50, 0.55, 1.0)) # Pale grey-blue
    mat_eye_white = make_mat("Roy_EyeWhite", (0.95, 0.95, 0.96, 1.0))
    mat_mouth = make_mat("Roy_Mouth", (0.35, 0.12, 0.12, 1.0))

    head_parts = build_base_chibi_head(mat_skin, mat_hair, mat_eye, mat_eye_white, mat_mouth)
    body_parts = build_base_chibi_body(mat_skin, mat_coat, mat_pants, mat_boots, mat_belt)

    # Grizzled Hair + Trapper Hat
    hat_crown = create_ellipsoid("Roy_HatCrown", Vector((0, 0.05, 1.95)), 0.46, 0.44, 0.26, mat_coat)
    fur_brim = create_cylinder("Roy_HatBrim", Vector((0, 0, 1.86)), Vector((0, 0, 1.96)), 0.46, 0.44, mat_fur)
    ear_flap_l = create_box("Roy_Flap_L", Vector((0.44, 0.0, 1.76)), Vector((0.05, 0.14, 0.24)), mat_fur)
    ear_flap_r = create_box("Roy_Flap_R", Vector((-0.44, 0.0, 1.76)), Vector((0.05, 0.14, 0.24)), mat_fur)
    head_parts.extend([hat_crown, fur_brim, ear_flap_l, ear_flap_r])

    # Full Grizzled Beard
    beard = create_ellipsoid("Roy_Beard", Vector((0, -0.22, 1.54)), 0.24, 0.22, 0.18, mat_hair)
    head_parts.append(beard)

    # Thick Fur Collar around neck/shoulders
    fur_collar = create_cylinder("Roy_FurCollar", Vector((0, 0, 1.18)), Vector((0, 0, 1.34)), 0.28, 0.32, mat_fur)
    body_parts.append(fur_collar)

    # Scavenged Scrap Shoulder Armor on Left Arm
    scrap_pauldron = create_ellipsoid("Roy_ScrapPauldron", Vector((0.30, 0, 1.28)), 0.14, 0.14, 0.12, mat_scrap)
    body_parts.append(scrap_pauldron)

    # Survival Backpack with rolled sleeping mat on back
    pack = create_box("Roy_Backpack", Vector((0, 0.24, 1.05)), Vector((0.28, 0.18, 0.34)), mat_coat)
    mat_roll = create_cylinder("Roy_Bedroll", Vector((-0.20, 0.26, 1.25)), Vector((0.20, 0.26, 1.25)), 0.07, 0.07, mat_fur)
    body_parts.extend([pack, mat_roll])

    head_obj = join_objects(head_parts, "NPC_Roy_Head")
    body_obj = join_objects(body_parts, "NPC_Roy_Body")
    return head_obj, body_obj

# ==============================================================================
# 6. MAIN EXPORT PIPELINE
# ==============================================================================
NPC_LIST = [
    ("NPC_Zack_Merchant",  build_npc_zack),
    ("NPC_Elena_Medic",     build_npc_elena),
    ("NPC_Otto_Gunsmith",   build_npc_otto),
    ("NPC_Maya_Scout",      build_npc_maya),
    ("NPC_Roy_Veteran",     build_npc_roy),
]

for npc_name, builder_func in NPC_LIST:
    print(f"\n=======================================================")
    print(f"BUILDING {npc_name}...")
    print(f"=======================================================")

    # Reset Blender Scene
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene

    # 1. Build Chibi Armature
    rig_obj = build_chibi_armature()

    # 2. Build Head and Body objects
    head_obj, body_obj = builder_func()

    # 3. Bind Meshes to Rig with Gaussian Radial Weights
    bind_mesh_to_rig(head_obj, rig_obj, is_head=True)
    bind_mesh_to_rig(body_obj, rig_obj, is_head=False)

    # 4. Select Rig and Meshes for Export
    bpy.ops.object.select_all(action='DESELECT')
    rig_obj.select_set(True)
    head_obj.select_set(True)
    body_obj.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj

    # 5. Export FBX
    fbx_filename = f"{npc_name}_Rigged.fbx"
    fbx_filepath = os.path.join(OUTPUT_DIR, fbx_filename)

    bpy.ops.export_scene.fbx(
        filepath=fbx_filepath,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        axis_forward='-Y',
        axis_up='Z',
        path_mode='AUTO'
    )

    print(f"--> Exported: {fbx_filepath}")

print("\nALL 5 CHIBI NPCS SUCCESSFULLY BUILT AND EXPORTED TO FBX!")


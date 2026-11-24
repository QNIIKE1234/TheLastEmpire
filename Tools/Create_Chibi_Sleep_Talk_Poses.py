import math
import os
import sys
import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion, Euler

# Reuse exact BONE_DEFS & helpers from ChibiAnimeKit.py
TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))
if TOOLS_DIR not in sys.path:
    sys.path.insert(0, TOOLS_DIR)

from ChibiAnimeKit import BONE_DEFS, _point_to_segment_dist

ROOT = os.path.abspath(os.path.join(TOOLS_DIR, ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedModels")
os.makedirs(OUT_DIR, exist_ok=True)
FBX_PATH = os.path.join(OUT_DIR, "Chibi_Sleep_Talk_Poses.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "Chibi_Sleep_Talk_Poses_Preview.png")


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 24
    return scene


def make_mat(name, color, roughness=0.68):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
    return m


def build_chibi_mannequin_meshes():
    """Builds a clean chibi mannequin matching BONE_DEFS proportions so FBX & preview renders read super clearly."""
    skin = make_mat("Mannequin_Skin", (0.94, 0.78, 0.68), 0.72)
    jacket = make_mat("Mannequin_Jacket", (0.24, 0.44, 0.68), 0.65)
    pants = make_mat("Mannequin_Pants", (0.18, 0.20, 0.25), 0.74)
    accent = make_mat("Mannequin_Accent", (0.94, 0.64, 0.16), 0.55)
    eye_m = make_mat("Mannequin_Eye", (0.08, 0.08, 0.10), 0.20)

    def add_sphere_to_bm(bm, center, scale, mat_idx, subdiv=2):
        ret = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=0.5)
        verts = ret["verts"]
        for v in verts:
            v.co.x = v.co.x * scale[0] + center[0]
            v.co.y = v.co.y * scale[1] + center[1]
            v.co.z = v.co.z * scale[2] + center[2]
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = mat_idx
            f.smooth = True

    def add_limb_to_bm(bm, p0, p1, r0, r1, mat_idx, segs=12):
        a, b = Vector(p0), Vector(p1)
        d = b - a
        L = d.length
        if L < 1e-4:
            return
        mid = (a + b) * 0.5
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=L)
        verts = ret["verts"]
        bmesh.ops.transform(bm, matrix=Matrix.Translation(mid) @ q.to_matrix().to_4x4(), verts=verts)
        for f in {f for v in verts for f in v.link_faces}:
            f.material_index = mat_idx
            f.smooth = True
        add_sphere_to_bm(bm, p0, (r0 * 2.0, r0 * 2.0, r0 * 2.0), mat_idx, subdiv=1)
        add_sphere_to_bm(bm, p1, (r1 * 2.0, r1 * 2.0, r1 * 2.0), mat_idx, subdiv=1)

    # Head mesh
    bm_h = bmesh.new()
    add_sphere_to_bm(bm_h, (0.0, 0.0, 1.78), (0.68, 0.64, 0.66), 0, subdiv=3)
    # Eyes facing -Y so head direction is 100% readable in preview!
    add_sphere_to_bm(bm_h, (-0.13, -0.29, 1.75), (0.10, 0.06, 0.13), 1, subdiv=2)
    add_sphere_to_bm(bm_h, (0.13, -0.29, 1.75), (0.10, 0.06, 0.13), 1, subdiv=2)
    # Hair cap / visor accent to show orientation
    add_sphere_to_bm(bm_h, (0.0, 0.04, 1.88), (0.72, 0.68, 0.52), 2, subdiv=2)
    mesh_h = bpy.data.meshes.new("Chibi_Head_Mesh")
    bm_h.to_mesh(mesh_h)
    bm_h.free()
    for m in (skin, eye_m, accent):
        mesh_h.materials.append(m)
    head_obj = bpy.data.objects.new("Chibi_Head", mesh_h)
    bpy.context.collection.objects.link(head_obj)

    # Body mesh
    bm_b = bmesh.new()
    # Torso (Hips -> Spine -> Chest -> Neck)
    add_limb_to_bm(bm_b, (0, 0, 0.66), (0, 0, 0.90), 0.21, 0.19, 1)  # pants/pelvis
    add_limb_to_bm(bm_b, (0, 0, 0.88), (0, 0, 1.28), 0.21, 0.23, 0)  # jacket torso
    add_limb_to_bm(bm_b, (0, 0, 1.26), (0, 0, 1.48), 0.11, 0.10, 3)  # neck skin
    # Arms
    for s in (1, -1):
        add_limb_to_bm(bm_b, (s * 0.22, 0, 1.23), (s * 0.50, 0, 1.23), 0.095, 0.085, 0)
        add_limb_to_bm(bm_b, (s * 0.50, 0, 1.23), (s * 0.72, 0, 1.23), 0.082, 0.070, 2)
        add_limb_to_bm(bm_b, (s * 0.72, 0, 1.23), (s * 0.88, 0, 1.23), 0.075, 0.068, 3)
    # Legs
    for s in (1, -1):
        add_limb_to_bm(bm_b, (s * 0.15, 0, 0.68), (s * 0.15, 0, 0.37), 0.105, 0.090, 1)
        add_limb_to_bm(bm_b, (s * 0.15, 0, 0.37), (s * 0.152, 0, 0.11), 0.088, 0.075, 1)
        add_limb_to_bm(bm_b, (s * 0.152, 0, 0.08), (s * 0.152, -0.18, 0.05), 0.085, 0.078, 2)
    mesh_b = bpy.data.meshes.new("Chibi_Body_Mesh")
    bm_b.to_mesh(mesh_b)
    bm_b.free()
    for m in (jacket, pants, accent, skin):
        mesh_b.materials.append(m)
    body_obj = bpy.data.objects.new("Chibi_Body", mesh_b)
    bpy.context.collection.objects.link(body_obj)
    return head_obj, body_obj


def build_rig(head_obj, body_obj):
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

    def bind_mesh(mesh_obj, is_head=False):
        mesh_obj.parent = rig_obj
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = rig_obj
        mod.use_deform_preserve_volume = True
        vgroups = {bname: mesh_obj.vertex_groups.new(name=bname) for bname, _, _, _, _ in BONE_DEFS}
        bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in BONE_DEFS if bname != "Root"]
        for v in mesh_obj.data.vertices:
            co = v.co
            if is_head:
                vgroups["Head"].add([v.index], 1.0, 'REPLACE')
                continue
            weights = {}
            for bname, h, t in bones_info:
                if bname.startswith("Left") and co.x < -0.01:
                    continue
                if bname.startswith("Right") and co.x > 0.01:
                    continue
                if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.19 or co.z < 1.04):
                    continue
                if ("Leg" in bname or "Foot" in bname) and (co.z > 0.72 or abs(co.x) > 0.32):
                    continue
                if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.30 or co.z < 0.54):
                    continue
                d = _point_to_segment_dist(co, h, t)
                sigma = 0.120
                w = math.exp(-(d * d) / (2.0 * (sigma ** 2)))
                if w > 1e-5:
                    weights[bname] = w
            if not weights:
                weights["Hips"] = 1.0
            top = sorted(weights.items(), key=lambda x: x[1], reverse=True)[:4]
            total = sum(w for _, w in top)
            for bname, w in top:
                vgroups[bname].add([v.index], w / total, 'REPLACE')

    bind_mesh(head_obj, is_head=True)
    bind_mesh(body_obj, is_head=False)
    for pb in rig_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return rig_obj, arm_data


def create_all_animations(scene, rig_obj, arm_data):
    rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}

    def begin_frame(frame):
        scene.frame_set(frame)
        for pb in rig_obj.pose.bones:
            pb.location = Vector((0, 0, 0))
            pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
            pb.scale = Vector((1, 1, 1))
        bpy.context.view_layer.update()

    def set_rot(bname, rx, ry, rz):
        pb = rig_obj.pose.bones[bname]
        pb.rotation_quaternion = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ').to_quaternion()
        bpy.context.view_layer.update()

    def set_hips(world_offset, pitch_deg, yaw_deg, roll_deg):
        pb = rig_obj.pose.bones["Hips"]
        wx, wy, wz = world_offset
        # Hips bone axis: local X = world X, local Y = world Z, local Z = -world Y
        pb.location = Vector((wx, wz, -wy))
        q_pitch = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
        q_yaw   = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
        q_roll  = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
        pb.rotation_quaternion = q_yaw @ q_roll @ q_pitch
        bpy.context.view_layer.update()

    def swing_bone(pb, target_pt):
        cur_dir = (pb.tail - pb.head).normalized()
        tgt_dir = (Vector(target_pt) - pb.head).normalized()
        q_swing = cur_dir.rotation_difference(tgt_dir)
        m = pb.matrix.copy()
        pb.matrix = Matrix.LocRotScale(m.to_translation(), q_swing @ m.to_quaternion(), Vector((1, 1, 1)))
        bpy.context.view_layer.update()

    def ik(upper_name, lower_name, end_name, target_world, pole_world, end_euler=(0, 0, 0)):
        pb_u = rig_obj.pose.bones[upper_name]
        pb_l = rig_obj.pose.bones[lower_name]
        pb_e = rig_obj.pose.bones[end_name]
        root_pos = pb_u.head.copy()
        L1, L2 = rest_lengths[upper_name], rest_lengths[lower_name]
        tgt = Vector(target_world)
        d_vec = tgt - root_pos
        D = d_vec.length
        max_r = (L1 + L2) * 0.992
        min_r = abs(L1 - L2) + 0.02
        if D > max_r:
            D = max_r
            tgt = root_pos + d_vec.normalized() * D
            d_vec = tgt - root_pos
        elif D < min_r:
            D = min_r
            tgt = root_pos + d_vec.normalized() * D
            d_vec = tgt - root_pos
        cos_a = max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2.0 * L1 * D)))
        sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))
        dir_n = d_vec.normalized()
        pole = Vector(pole_world).normalized()
        plane_pole = pole - dir_n * pole.dot(dir_n)
        plane_pole = Vector((0, 1, 0)) if plane_pole.length_squared < 1e-6 else plane_pole.normalized()
        mid_pos = root_pos + dir_n * (L1 * cos_a) + plane_pole * (L1 * sin_a)
        swing_bone(pb_u, mid_pos)
        swing_bone(pb_l, tgt)
        if any(abs(a) > 1e-4 for a in end_euler):
            set_rot(end_name, *end_euler)
        else:
            pb_e.rotation_quaternion = Quaternion((1, 0, 0, 0))
            bpy.context.view_layer.update()

    def key_all(frame, prev_q):
        for pb in rig_obj.pose.bones:
            q = pb.rotation_quaternion.copy()
            if pb.name in prev_q and prev_q[pb.name].dot(q) < 0.0:
                q = -q
                pb.rotation_quaternion = q
            prev_q[pb.name] = q.copy()
            pb.keyframe_insert(data_path="location", frame=frame)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            pb.keyframe_insert(data_path="scale", frame=frame)

    def new_action(name):
        act = bpy.data.actions.new(name=name)
        act.use_fake_user = True
        if rig_obj.animation_data is None:
            rig_obj.animation_data_create()
        rig_obj.animation_data.action = act
        if hasattr(rig_obj.animation_data, "action_slot") and hasattr(act, "slots"):
            if len(act.slots) == 0:
                slot = act.slots.new(id_type='OBJECT', name=rig_obj.name)
                rig_obj.animation_data.action_slot = slot
            else:
                rig_obj.animation_data.action_slot = act.slots[0]
        return act

    actions = []

    # ==========================================================================
    # 1. SLEEP (Lying flat on back peacefully, slow calm breathing loop, 48f)
    # ==========================================================================
    actions.append(new_action("Sleep"))
    pq = {}
    for f in range(1, 50):
        begin_frame(f)
        ang = ((f - 1) / 48.0) * 2.0 * math.pi
        br = math.sin(ang)
        # Pitch -88 deg lays character flat on back along +Y (head at +Y pillow, feet at -Y foot of bed)
        set_hips((0.0, -0.10, -0.44 + 0.008 * br), -88.0, 0.0, 0.0)
        set_rot("Spine", 3.0 + 2.5 * br, 0.0, 0.0)
        set_rot("Chest", 4.0 + 3.0 * br, 0.0, 0.0)
        set_rot("Neck", 6.0, 0.0, 0.0)
        set_rot("Head", 6.0 - 1.5 * br, 8.0 * math.sin(ang * 0.5), 4.0)
        # Arms resting comfortably by sides/on belly while lying on back
        ik("LeftUpperArm",  "LeftLowerArm",  "LeftHand",  (0.24, 0.08, 0.30 + 0.014 * br), (0.6, 0.1, -0.8))
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.26, 0.14, 0.28 + 0.012 * br), (-0.6, 0.1, -0.8))
        # Legs stretched relaxed along -Y with slight comfy bend
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.17, -0.66, 0.18), (0.15, 0.0, 1.0), (15, 0, 0))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.17, -0.66, 0.18), (-0.15, 0.0, 1.0), (15, 0, 0))
        key_all(f, pq)

    # ==========================================================================
    # 2. TALK (Lively standing conversation loop with expressive gestures, 72f)
    # ==========================================================================
    actions.append(new_action("Talk"))
    pq = {}
    for f in range(1, 74):
        begin_frame(f)
        t = (f - 1) / 72.0
        a1 = t * 2.0 * math.pi
        a2 = t * 4.0 * math.pi
        a3 = t * 6.0 * math.pi
        sway = math.sin(a1)
        nod = math.sin(a3)
        gest_r = 0.5 + 0.5 * math.sin(a2)
        gest_l = 0.5 + 0.5 * math.sin(a2 + math.pi * 0.85)
        set_hips((0.022 * sway, 0.0, 0.012 * abs(math.sin(a2))), 2.0 + 1.5 * nod, 4.5 * sway, -1.8 * sway)
        set_rot("Spine", 2.0 - 1.2 * nod, -2.5 * sway, 1.0 * sway)
        set_rot("Chest", 1.5 * nod, 3.0 * sway, 0.0)
        set_rot("Head", 4.5 * nod, 7.0 * math.sin(a1 + 0.5), 4.0 * math.cos(a1))
        # Alternating friendly conversational hand gestures
        ik("RightUpperArm", "RightLowerArm", "RightHand",
           (-0.24 - 0.08 * gest_r, -0.26 - 0.12 * gest_r, 0.94 + 0.14 * gest_r),
           (-0.55, 0.55, -0.35), (0, -25 * gest_r, -20))
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
           (0.24 + 0.07 * gest_l, -0.22 - 0.10 * gest_l, 0.90 + 0.12 * gest_l),
           (0.55, 0.55, -0.35), (0, 22 * gest_l, 18))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.16, -0.02, 0.11), (0.18, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.02, 0.11), (-0.18, -1.0, 0.1))
        key_all(f, pq)

    # Helper to build breathing signature cool poses cleanly (48 frames = 2.0s smooth idle loop)
    def build_pose_loop(action_name, pose_fn):
        actions.append(new_action(action_name))
        pq_local = {}
        for f in range(1, 50):
            begin_frame(f)
            ang = ((f - 1) / 48.0) * 2.0 * math.pi
            br = math.sin(ang)
            pose_fn(br, ang)
            key_all(f, pq_local)

    # ==========================================================================
    # 3. POSE 01 — Assassin Knife Inspect (Image 1: Black bob dress girl)
    # Right hand raised inspecting knife near face, left arm folded across waist, head turned coolly right
    # ==========================================================================
    def pose_01(br, ang):
        set_hips((0.02, 0.0, 0.010 * br), -1.0, -14.0, 2.5)
        set_rot("Spine", 1.0 - 1.2 * br, 6.0, -1.5)
        set_rot("Chest", -1.5 * br, 6.0, 0.0)
        set_rot("Head", 3.0 + 1.5 * br, -32.0 + 2.5 * math.sin(ang), -4.0)
        # Right hand (-X) raised gracefully in front holding knife upright
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.30, -0.26, 1.38 + 0.015 * br), (-0.65, 0.25, -0.75), (25, 0, -15))
        # Left arm (+X) tucked smoothly across waist supporting right elbow
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (-0.18, -0.20, 0.94 + 0.010 * br), (0.60, 0.45, -0.55), (0, 20, 0))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.14, 0.03, 0.11), (0.14, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.14, -0.05, 0.11), (-0.14, -1.0, 0.1))
    build_pose_loop("Pose_01_AssassinKnife", pose_01)

    # ==========================================================================
    # 4. POSE 02 — Bat on Shoulder Confident (Image 2: Blonde twin-buns P4NDORA)
    # Right hand up by right shoulder holding bat across back, left hand cocked on left hip
    # ==========================================================================
    def pose_02(br, ang):
        set_hips((0.03, 0.0, 0.012 * br), -2.0, 6.0, -4.5)
        set_rot("Spine", -1.5 * br, -4.0, 3.0)
        set_rot("Chest", -2.0 * br, -3.0, 2.0)
        set_rot("Head", 2.0 + 2.0 * br, -4.0, -7.0 + 2.0 * br)
        # Right hand (-X) raised beside right shoulder gripping bat handle
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.42, -0.14, 1.34 + 0.016 * br), (-0.75, 0.30, -0.60), (-20, 0, 25))
        # Left hand (+X) planted firmly on left hip with bold flared akimbo elbow!
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.21, -0.04, 0.89 + 0.008 * br), (1.0, 0.05, 0.22))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.19, -0.02, 0.11), (0.20, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.02, 0.11), (-0.18, -1.0, 0.1))
    build_pose_loop("Pose_02_BatShoulder", pose_02)

    # ==========================================================================
    # 5. POSE 03 — Tactical Forward Reach (Image 3: Purple ponytail yellow jacket)
    # Left arm extended boldly forward toward camera, right fist pulled back ready at hip
    # ==========================================================================
    def pose_03(br, ang):
        set_hips((0.0, -0.02, -0.02 + 0.010 * br), 8.0, -24.0, 2.0)
        set_rot("Spine", 4.0 - 1.5 * br, 10.0, 0.0)
        set_rot("Chest", 3.0, 12.0, 0.0)
        set_rot("Head", -8.0 + 1.5 * br, 4.0, -5.0)
        # Left arm (+X) thrust dynamically straight forward (-Y) reaching toward camera!
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.14, -0.58, 1.18 + 0.015 * br), (0.45, 0.20, -0.60), (0, 0, 12))
        # Right fist (-X) cocked tight at right ribs/hip ready to strike
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.30, -0.14, 0.96 + 0.010 * br), (-0.65, 0.70, -0.35))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.18, -0.10, 0.11), (0.20, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.20, 0.10, 0.11), (-0.20, -1.0, 0.1))
    build_pose_loop("Pose_03_TacticalReach", pose_03)

    # ==========================================================================
    # 6. POSE 04 — Headphone Pop Gun-Up (Image 4: Yellow headphones + pistol)
    # Left knee playfully lifted, left hand raised holding pistol up, right arm swept out/down
    # ==========================================================================
    def pose_04(br, ang):
        bounce = 0.018 * abs(math.sin(ang))
        set_hips((-0.02, 0.0, 0.02 + bounce), -2.0, -10.0, 4.0)
        set_rot("Spine", -1.5, 6.0, -3.0)
        set_rot("Chest", -2.0 * br, 6.0, -2.0)
        set_rot("Head", 3.0 * br, 6.0, 7.0 + 2.5 * br)
        # Left arm (+X) raised high out to left holding pistol up
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.46, -0.18, 1.44 + 0.02 * br), (0.65, 0.35, -0.65), (15, 0, -10))
        # Right arm (-X) extended out and downward playfully
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.52, -0.10, 0.88 + 0.015 * br), (-0.60, 0.45, 0.25))
        # Left leg lifted playfully with knee bent; right leg planted solidly
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.24, -0.08, 0.30 + bounce), (0.35, -1.0, 0.25), (24, 0, 0))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.14, 0.0, 0.11), (-0.12, -1.0, 0.1))
    build_pose_loop("Pose_04_HeadphonePop", pose_04)

    # ==========================================================================
    # 7. POSE 05 — Cyan Over-Shoulder Sassy (Image 5: Cyan ponytail crop top)
    # Turned 3/4 sideways looking back over right shoulder, left hand stylishly raised near chin, right arm tucked across waist
    # ==========================================================================
    def pose_05(br, ang):
        set_hips((-0.02, 0.0, 0.010 * br), -3.0, 36.0, -5.0)
        set_rot("Spine", -2.0, -12.0, 3.0)
        set_rot("Chest", -1.5 * br, -14.0, 2.0)
        set_rot("Head", 3.0 + 1.5 * br, -24.0 + 2.0 * br, -6.0)
        # Left hand (+X) lifted gracefully near cheek/shoulder
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.16, -0.34, 1.32 + 0.014 * br), (0.55, 0.20, -0.75))
        # Right arm (-X) folded cleanly across front waist
        ik("RightUpperArm", "RightLowerArm", "RightHand", (0.12, -0.22, 0.94 + 0.010 * br), (-0.60, 0.40, -0.50))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.15, 0.05, 0.11), (0.15, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.15, -0.05, 0.11), (-0.15, -1.0, 0.1))
    build_pose_loop("Pose_05_CyanSassy", pose_05)

    # ==========================================================================
    # 8. POSE 06 — Dreadlocks Power Grab + Low Pistol (Image 6: Dreadlocks vest guy)
    # Forward aggressive lean, right arm lunging straight at camera grabbing, left arm low with pistol
    # ==========================================================================
    def pose_06(br, ang):
        set_hips((0.0, -0.03, -0.05 + 0.010 * br), 16.0, 18.0, -2.0)
        set_rot("Spine", 8.0 - 1.5 * br, -8.0, 0.0)
        set_rot("Chest", 6.0, -8.0, 0.0)
        set_rot("Head", -18.0 + 1.5 * br, -6.0, 2.0)
        # Right arm (-X) lunging straight forward (-Y) wide grabbing hand!
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.14, -0.60, 1.08 + 0.015 * br), (-0.50, 0.15, -0.65))
        # Left arm (+X) lowered slightly back-side gripping pistol ready
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.38, -0.18, 0.80 + 0.010 * br), (0.65, 0.55, -0.20))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.22, 0.08, 0.11), (0.25, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.22, -0.10, 0.11), (-0.25, -1.0, 0.1))
    build_pose_loop("Pose_06_DreadGrab", pose_06)

    # ==========================================================================
    # 9. POSE 07 — Proud Smug Hands-on-Hips (Image 7: Pink curly ponytail + glasses)
    # Chin lifted proudly upward, right hand planted on right hip, left arm poised at hip/side
    # ==========================================================================
    def pose_07(br, ang):
        set_hips((-0.02, 0.0, 0.014 * br), -4.0, -8.0, 4.0)
        set_rot("Spine", -3.0 - 1.2 * br, 4.0, -2.5)
        set_rot("Chest", -3.0 - 1.5 * br, 4.0, -1.5)
        set_rot("Head", -12.0 + 1.5 * br, 6.0, 6.0 + 1.8 * br)
        # Right hand (-X) planted proudly on right waist with bold flared akimbo elbow!
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.21, -0.04, 0.89 + 0.008 * br), (-1.0, 0.05, 0.22))
        # Left hand (+X) planted proudly on left waist with flared elbow
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.22, -0.04, 0.88 + 0.008 * br), (1.0, 0.05, 0.22))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.18, -0.02, 0.11), (0.18, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.02, 0.11), (-0.18, -1.0, 0.1))
    build_pose_loop("Pose_07_CurlyHips", pose_07)

    # ==========================================================================
    # 10. POSE 08 — Tactical Arm-Fold & Perimeter Scan (Image 8: Purple scarred armor guy)
    # Head turned sharply right scanning, left forearm folded across chest/stomach, right hand on hip/belt
    # ==========================================================================
    def pose_08(br, ang):
        set_hips((0.0, 0.0, 0.010 * br), 2.0, 14.0, 0.0)
        set_rot("Spine", 1.5 - 1.0 * br, -6.0, 0.0)
        set_rot("Chest", -1.2 * br, -6.0, 0.0)
        set_rot("Head", 2.0, -38.0 + 3.0 * math.sin(ang), -2.0)
        # Left forearm (+X) folded solidly across stomach/chest
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (-0.08, -0.24, 0.96 + 0.010 * br), (0.70, 0.35, -0.55))
        # Right hand (-X) poised ready on right belt/hip with clear tactical elbow angle
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.21, -0.08, 0.88 + 0.008 * br), (-1.0, 0.15, 0.12))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.18, 0.02, 0.11), (0.18, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, -0.04, 0.11), (-0.18, -1.0, 0.1))
    build_pose_loop("Pose_08_StrikerScan", pose_08)

    # ==========================================================================
    # 11. POSE 09 — Stoic Crossed Arms Ronin (Image 9: White ponytail red shades ninja)
    # Both arms tightly crossed across chest, cool calm slight forward lean, grounded stance
    # ==========================================================================
    def pose_09(br, ang):
        set_hips((0.0, 0.0, -0.02 + 0.010 * br), 5.0, -10.0, 0.0)
        set_rot("Spine", 3.0 - 1.2 * br, 5.0, 0.0)
        set_rot("Chest", 2.0 - 1.5 * br, 5.0, 0.0)
        set_rot("Head", 5.0 + 1.5 * br, 4.0, -2.0)
        # Both forearms crossed cleanly across upper chest
        ik("LeftUpperArm",  "LeftLowerArm",  "LeftHand",  (-0.18, -0.26, 1.04 + 0.012 * br), (0.65, 0.20, -0.70))
        ik("RightUpperArm", "RightLowerArm", "RightHand", (0.18, -0.29, 1.00 + 0.012 * br), (-0.65, 0.20, -0.70))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.20, -0.03, 0.11), (0.20, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.20, 0.03, 0.11), (-0.20, -1.0, 0.1))
    build_pose_loop("Pose_09_RoninCrossed", pose_09)

    # ==========================================================================
    # 12. POSE 10 — Relaxed Smirking Operative (Image 10: Blue spiky hair green jacket)
    # Easygoing weight shift, left hand casually touching tactical shoulder radio, right arm relaxed at side
    # ==========================================================================
    def pose_10(br, ang):
        set_hips((0.02, 0.0, 0.012 * br), -1.5, 12.0, -3.0)
        set_rot("Spine", -1.0 * br, -5.0, 2.0)
        set_rot("Chest", -1.5 * br, -5.0, 1.5)
        set_rot("Head", 2.0 + 1.5 * br, -8.0 + 2.5 * math.sin(ang), 5.0)
        # Left hand (+X) casually raised near left shoulder radio strap
        ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.18, -0.22, 1.24 + 0.012 * br), (0.75, 0.25, -0.55))
        # Right arm (-X) relaxed loosely at side with slight swagger
        ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.34, -0.06, 0.78 + 0.010 * br), (-0.45, 0.60, -0.15))
        ik("LeftUpperLeg",  "LeftLowerLeg",  "LeftFoot",  (0.16, -0.04, 0.11), (0.16, -1.0, 0.1))
        ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.04, 0.11), (-0.18, -1.0, 0.1))
    build_pose_loop("Pose_10_BlueOperative", pose_10)

    return actions


def render_pose_preview_sheet(scene, actions):
    """Duplicates a lightweight mannequin for each of the 12 actions in a 4x3 showcase grid and renders a preview PNG."""
    # Freeze each action's hero frame (frame 15) into a static showcase copy on a 4x3 grid
    showcase_objs = []
    orig_rig = bpy.data.objects["Chibi_Base_Rig"]
    orig_head = bpy.data.objects["Chibi_Head"]
    orig_body = bpy.data.objects["Chibi_Body"]

    for idx, act in enumerate(actions):
        orig_rig.animation_data.action = act
        if hasattr(orig_rig.animation_data, "action_slot") and len(act.slots) > 0:
            orig_rig.animation_data.action_slot = act.slots[0]
        scene.frame_set(15)
        bpy.context.view_layer.update()

        depsgraph = bpy.context.evaluated_depsgraph_get()
        col = idx % 4
        row = idx // 4
        gx = (col - 1.5) * 2.25
        gz = (1.0 - row) * 2.55

        for src in (orig_head, orig_body):
            eval_obj = src.evaluated_get(depsgraph)
            mesh_copy = bpy.data.meshes.new_from_object(eval_obj)
            dup = bpy.data.objects.new(f"Show_{idx}_{src.name}", mesh_copy)
            bpy.context.collection.objects.link(dup)
            dup.location = (gx, 0.0, gz)
            showcase_objs.append(dup)

    # Hide original rig/meshes during showcase render
    orig_rig.hide_render = True
    orig_head.hide_render = True
    orig_body.hide_render = True

    cam_data = bpy.data.cameras.new("SheetCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 9.4
    cam_obj = bpy.data.objects.new("SheetCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = Vector((0.0, -14.0, 2.2))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.05)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam_obj

    sun_d = bpy.data.lights.new("SunKey", type='SUN')
    sun_d.energy = 3.2
    sun = bpy.data.objects.new("SunKey", sun_d)
    bpy.context.collection.objects.link(sun)
    sun.rotation_euler = Euler((math.radians(48), math.radians(-16), math.radians(-20)), 'XYZ')

    fill_d = bpy.data.lights.new("SunFill", type='SUN')
    fill_d.energy = 1.4
    fill = bpy.data.objects.new("SunFill", fill_d)
    bpy.context.collection.objects.link(fill)
    fill.rotation_euler = Euler((math.radians(60), math.radians(24), math.radians(150)), 'XYZ')

    world = bpy.data.worlds.new("SheetWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.16, 0.20, 1.0)
        bg.inputs[1].default_value = 1.1

    avail = bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys()
    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in avail else 'BLENDER_EEVEE'
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1200
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'Standard'
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"ANIM_PREVIEW_RENDERED: {PREVIEW_PATH}")


def main():
    scene = reset_scene()
    head_obj, body_obj = build_chibi_mannequin_meshes()
    rig_obj, arm_data = build_rig(head_obj, body_obj)
    actions = create_all_animations(scene, rig_obj, arm_data)

    # Export all 12 actions in ONE shared Chibi_Base_Rig FBX!
    bpy.ops.object.select_all(action='DESELECT')
    rig_obj.select_set(True)
    head_obj.select_set(True)
    body_obj.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH,
        use_selection=True,
        object_types={'ARMATURE', 'MESH'},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        axis_forward='-Y',
        axis_up='Z',
        path_mode='AUTO',
    )
    print(f"EXPORTED_CHIBI_ANIMS ({len(actions)}): {FBX_PATH}")

    render_pose_preview_sheet(scene, actions)


if __name__ == "__main__":
    main()

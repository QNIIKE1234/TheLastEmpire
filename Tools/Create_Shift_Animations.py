import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
from mathutils import Vector, Euler, Quaternion, Matrix
import ChibiAnimeKit as kit

OUT_DIR = kit.OUT_DIR
FBX_PATH = os.path.join(OUT_DIR, "Chibi_Shift_Animations.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "Chibi_Shift_Animations_Preview.png")


def build_mannequin_body_and_head():
    """Lightweight stylized mannequin mesh so Blender renders a clear 7-pose QA strip & Unity gets a valid FBX."""
    mat_body = kit.make_mat("Mat_Mannequin_Body", (0.28, 0.34, 0.44, 1.0), roughness=0.75)
    mat_accent = kit.make_mat("Mat_Mannequin_Accent", (0.95, 0.68, 0.12, 1.0), roughness=0.55)
    mat_head = kit.make_mat("Mat_Mannequin_Head", (0.92, 0.78, 0.66, 1.0), roughness=0.80)

    head_parts = [
        kit.create_ellipsoid("M_Head", (0.0, 0.0, 1.69), (0.34, 0.32, 0.34), mat_head),
        kit.create_rounded_box("M_Visor", (0.0, -0.31, 1.71), (0.36, 0.08, 0.10), mat_accent, bevel_width=0.02),
    ]
    head_obj = kit.join_objects_into(head_parts[0], head_parts, "Chibi_Base_Head")

    body_parts = [
        kit.create_ellipsoid("M_Torso", (0.0, 0.0, 1.04), (0.24, 0.18, 0.30), mat_body),
        kit.create_rounded_box("M_ChestMark", (0.0, -0.17, 1.12), (0.20, 0.05, 0.14), mat_accent, bevel_width=0.015),
        kit.create_ellipsoid("M_Pelvis", (0.0, 0.0, 0.70), (0.23, 0.17, 0.14), mat_body),
    ]
    for side in (1.0, -1.0):
        body_parts.extend([
            kit.create_ellipsoid(f"M_UArm_{side}", (side * 0.38, 0.0, 1.23), (0.14, 0.09, 0.09), mat_body),
            kit.create_ellipsoid(f"M_LArm_{side}", (side * 0.62, 0.0, 1.23), (0.13, 0.08, 0.08), mat_body),
            kit.create_ellipsoid(f"M_Hand_{side}", (side * 0.80, 0.0, 1.23), (0.09, 0.08, 0.08), mat_accent),
            kit.create_ellipsoid(f"M_ULeg_{side}", (side * 0.15, 0.0, 0.52), (0.10, 0.10, 0.18), mat_body),
            kit.create_ellipsoid(f"M_LLeg_{side}", (side * 0.15, 0.0, 0.24), (0.09, 0.09, 0.16), mat_body),
            kit.create_ellipsoid(f"M_Foot_{side}", (side * 0.15, -0.05, 0.06), (0.10, 0.15, 0.07), mat_accent),
        ])
    body_obj = kit.join_objects_into(body_parts[0], body_parts, "Chibi_Base_Mesh")
    return head_obj, body_obj


def main():
    scene = kit.reset_scene()
    head_obj, body_obj = build_mannequin_body_and_head()

    # Build the EXACT 18-bone Chibi_Base_Rig from ChibiAnimeKit.BONE_DEFS
    arm_data = bpy.data.armatures.new("Chibi_Base_Rig")
    rig_obj = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')

    edit_bones = {}
    for bname, h, t, pname, roll_vec in kit.BONE_DEFS:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in edit_bones:
            eb.parent = edit_bones[pname]
            eb.use_connect = False
        edit_bones[bname] = eb
    bpy.ops.object.mode_set(mode='OBJECT')

    # Bind mannequin to rig using exact ChibiAnimeKit weighting logic
    for mesh_obj, is_head in ((head_obj, True), (body_obj, False)):
        mesh_obj.parent = rig_obj
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = rig_obj
        vgroups = {bname: mesh_obj.vertex_groups.new(name=bname) for bname, _, _, _, _ in kit.BONE_DEFS}
        bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in kit.BONE_DEFS if bname != "Root"]
        for v in mesh_obj.data.vertices:
            if is_head:
                vgroups["Head"].add([v.index], 1.0, 'REPLACE')
                continue
            co = v.co
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
                d = kit._point_to_segment_dist(co, h, t)
                w = math.exp(-(d * d) / (2.0 * 0.115 ** 2))
                if w > 1e-5:
                    weights[bname] = w
            if not weights:
                weights["Hips"] = 1.0
            top = sorted(weights.items(), key=lambda item: item[1], reverse=True)[:4]
            total = sum(w for _, w in top)
            for bname, w in top:
                vgroups[bname].add([v.index], w / total, 'REPLACE')

    for pb in rig_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}

    def begin_frame_pose(frame):
        scene.frame_set(frame)
        for pb in rig_obj.pose.bones:
            pb.location = Vector((0, 0, 0))
            pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
            pb.scale = Vector((1, 1, 1))
        bpy.context.view_layer.update()

    def set_local_euler_deg(bname, rx_deg, ry_deg, rz_deg):
        pb = rig_obj.pose.bones[bname]
        pb.rotation_quaternion = Euler((math.radians(rx_deg), math.radians(ry_deg), math.radians(rz_deg)), 'XYZ').to_quaternion()
        bpy.context.view_layer.update()

    def set_hips_transform(loc_offset, pitch_deg, yaw_deg, roll_deg):
        pb = rig_obj.pose.bones["Hips"]
        pb.location = Vector(loc_offset)
        q_pitch = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
        q_yaw   = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
        q_roll  = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
        pb.rotation_quaternion = q_yaw @ q_roll @ q_pitch
        bpy.context.view_layer.update()

    def set_hips_world_offset(world_offset, pitch_deg, yaw_deg, roll_deg):
        pb = rig_obj.pose.bones["Hips"]
        wx, wy, wz = world_offset
        pb.location = Vector((wx, wz, -wy))
        q_pitch = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
        q_yaw   = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
        q_roll  = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
        pb.rotation_quaternion = q_yaw @ q_roll @ q_pitch
        bpy.context.view_layer.update()

    def swing_bone_zero_twist(pb, target_end_pt):
        cur_dir = (pb.tail - pb.head).normalized()
        tgt_dir = (Vector(target_end_pt) - pb.head).normalized()
        q_swing = cur_dir.rotation_difference(tgt_dir)
        world_mat = pb.matrix.copy()
        pb.matrix = Matrix.LocRotScale(world_mat.to_translation(), q_swing @ world_mat.to_quaternion(), Vector((1, 1, 1)))
        bpy.context.view_layer.update()

    def aim_two_bone_ik(upper_name, lower_name, end_name, target_world_pos, pole_world_vec, hand_rot_offset_deg=(0, 0, 0)):
        pb_u = rig_obj.pose.bones[upper_name]
        pb_l = rig_obj.pose.bones[lower_name]
        pb_e = rig_obj.pose.bones[end_name]
        root_pos = pb_u.head.copy()
        L1 = rest_lengths[upper_name]
        L2 = rest_lengths[lower_name]
        tgt = Vector(target_world_pos)
        d_vec = tgt - root_pos
        D = d_vec.length
        max_reach = (L1 + L2) * 0.992
        min_reach = abs(L1 - L2) + 0.02
        if D > max_reach:
            D = max_reach
            tgt = root_pos + d_vec.normalized() * D
            d_vec = tgt - root_pos
        elif D < min_reach:
            D = min_reach
            tgt = root_pos + d_vec.normalized() * D
            d_vec = tgt - root_pos
        cos_a = max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2.0 * L1 * D)))
        sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))
        dir_norm = d_vec.normalized()
        pole = Vector(pole_world_vec).normalized()
        plane_pole = pole - dir_norm * pole.dot(dir_norm)
        plane_pole = Vector((0, 1, 0)) if plane_pole.length_squared < 1e-6 else plane_pole.normalized()
        mid_pos = root_pos + dir_norm * (L1 * cos_a) + plane_pole * (L1 * sin_a)
        swing_bone_zero_twist(pb_u, mid_pos)
        swing_bone_zero_twist(pb_l, tgt)
        if any(abs(a) > 1e-4 for a in hand_rot_offset_deg):
            set_local_euler_deg(end_name, *hand_rot_offset_deg)
        else:
            pb_e.rotation_quaternion = Quaternion((1, 0, 0, 0))
            bpy.context.view_layer.update()

    def keyframe_all_bones(frame, prev_quats):
        for pb in rig_obj.pose.bones:
            q = pb.rotation_quaternion.copy()
            if pb.name in prev_quats and prev_quats[pb.name].dot(q) < 0.0:
                q = -q
                pb.rotation_quaternion = q
            prev_quats[pb.name] = q.copy()
            pb.keyframe_insert(data_path="location", frame=frame)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            pb.keyframe_insert(data_path="scale", frame=frame)

    def create_new_action(name):
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

    # =========================================================================
    # 0. VANGUARD: Shift_AdrenalineRush (Hyper-charged battle sprint! Frames 1..17)
    # =========================================================================
    actions.append((create_new_action("Shift_AdrenalineRush"), 5))
    prev_q = {}
    for f in range(1, 18):
        begin_frame_pose(f)
        ang = ((f - 1) / 16.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        bounce = (math.sin(ang * 2.0 - math.pi * 0.25) * 0.5 + 0.5) * 0.095
        set_hips_transform((0.030 * s, -0.04, bounce - 0.03), 22.0, -16.0 * s, 6.0 * c)
        set_local_euler_deg("Spine", 11.0, 8.0 * s, 0.0)
        set_local_euler_deg("Chest", 11.0, 15.0 * s, -5.0 * c)
        set_local_euler_deg("Head", -24.0, -10.0 * s, 0.0)
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.31, -0.20 + 0.32 * s, 1.00 - 0.12 * s), (0.48, 0.85, -0.25))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.31, -0.20 - 0.32 * s, 1.00 + 0.12 * s), (-0.48, 0.85, -0.25))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.34 * s, 0.12 + max(0.0, c) * 0.25), (0.16, -1.0, 0.35), (34.0 * s, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, 0.34 * s, 0.12 + max(0.0, -c) * 0.25), (-0.16, -1.0, 0.35), (-34.0 * s, 0, 0))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 1. JUGGERNAUT: Shift_UnstoppableCharge (Heavy shoulder battering-ram! Frames 1..17)
    # =========================================================================
    actions.append((create_new_action("Shift_UnstoppableCharge"), 5))
    prev_q = {}
    for f in range(1, 18):
        begin_frame_pose(f)
        ang = ((f - 1) / 16.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        stomp = abs(s) * 0.045
        # Low hunched charging bulldozer leading with right shoulder!
        set_hips_transform((0.0, -0.06, -0.09 + stomp), 28.0, 32.0 - 6.0 * s, -6.0)
        set_local_euler_deg("Spine", 12.0, 8.0, 0.0)
        set_local_euler_deg("Chest", 10.0, 12.0, -4.0)
        set_local_euler_deg("Head", -18.0, -34.0, 4.0)
        # Right arm braced forward across shoulder as heavy shield ram, left arm swept back for drive
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.06, -0.38, 0.96), (-0.70, 0.10, -0.30))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.40, 0.22 + 0.12 * s, 0.92), (0.50, 0.70, -0.20))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.17, -0.30 * s, 0.11 + max(0.0, c) * 0.20), (0.20, -1.0, 0.35), (26.0 * s, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.17, 0.30 * s, 0.11 + max(0.0, -c) * 0.20), (-0.20, -1.0, 0.35), (-26.0 * s, 0, 0))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 2. PHANTOM: Shift_ShadowStep (Low stealth anime ninja dash! Frames 1..25)
    # =========================================================================
    actions.append((create_new_action("Shift_ShadowStep"), 7))
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        ang = ((f - 1) / 24.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        glide = math.sin(ang * 2.0) * 0.025
        # Deep aerodynamic crouch with BOTH arms swept cleanly behind hips!
        set_hips_transform((0.02 * s, -0.06, -0.14 + glide), 34.0, -6.0 * s, 3.0 * c)
        set_local_euler_deg("Spine", 14.0, 3.0 * s, 0.0)
        set_local_euler_deg("Chest", 12.0, 4.0 * s, 0.0)
        set_local_euler_deg("Head", -40.0, 0.0, 0.0)
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.32, 0.42 + 0.05 * c, 0.94 + 0.04 * s), (0.45, 0.30, 0.65))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.32, 0.42 - 0.05 * c, 0.94 - 0.04 * s), (-0.45, 0.30, 0.65))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.26 * s, 0.11 + max(0.0, c) * 0.15), (0.18, -1.0, 0.30), (22.0 * s, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.26 * s, 0.11 + max(0.0, -c) * 0.15), (-0.18, -1.0, 0.30), (-22.0 * s, 0, 0))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 3. MARKSMAN: Shift_EagleEye (Locked crouched precision sniper stance! Frames 1..25)
    # =========================================================================
    actions.append((create_new_action("Shift_EagleEye"), 7))
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        ang = ((f - 1) / 24.0) * 2.0 * math.pi
        breath = math.sin(ang) * 0.008
        # Steady braced sharpshooter crouch peering down the sights
        set_hips_transform((0.0, 0.0, -0.10 + breath), 10.0, 24.0, 0.0)
        set_local_euler_deg("Spine", 8.0, -10.0, 0.0)
        set_local_euler_deg("Chest", 6.0, -12.0, 0.0)
        set_local_euler_deg("Head", -12.0, -4.0, -6.0)
        # Precision two-handed rifle aim extended cleanly forward!
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.04, -0.44, 1.06 + breath), (-0.42, 0.25, -0.75))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.06, -0.52, 1.05 + breath), (0.42, 0.20, -0.80))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.18, -0.14, 0.11), (0.20, -1.0, 0.25))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.16, 0.11), (-0.20, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 4. SCAVENGER: Shift_GrapplingHook (Dynamic wire-zip heroic flight pose! Frames 1..17)
    # =========================================================================
    actions.append((create_new_action("Shift_GrapplingHook"), 5))
    prev_q = {}
    for f in range(1, 18):
        begin_frame_pose(f)
        ang = ((f - 1) / 16.0) * 2.0 * math.pi
        flutter = math.sin(ang) * 0.020
        # Heroic mid-air forward wire-zip: lifted cleanly off the ground!
        set_hips_world_offset((0.0, -0.05, 0.16 + flutter), 30.0, -14.0, -8.0 + 3.0 * math.sin(ang))
        set_local_euler_deg("Spine", 10.0, 8.0, 0.0)
        set_local_euler_deg("Chest", 8.0, 12.0, 0.0)
        set_local_euler_deg("Head", -32.0, -6.0, 5.0)
        # Left grapple arm reaching straight forward toward anchor; Right arm cocked heroically back!
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.10, -0.62, 1.32 + flutter), (0.40, 0.10, -0.70))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.42, 0.34, 1.06 + flutter), (-0.50, 0.40, 0.50))
        # Dynamic airborne legs: left knee tucked forward, right leg trailing back!
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.22, 0.34 + flutter), (0.20, -1.0, 0.40), (-20.0, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.38, 0.26 - flutter), (-0.18, -1.0, 0.20), (35.0, 0, 0))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 5. REBEL: Shift_CycloneSwing (Full 360-degree tornado melee spin! Frames 1..13)
    # =========================================================================
    actions.append((create_new_action("Shift_CycloneSwing"), 4))
    prev_q = {}
    for f in range(1, 14):
        begin_frame_pose(f)
        u = (f - 1) / 12.0
        # Continuous seamless 360-degree spin around Y-up axis (local euler for arms/legs keeps weapon locked horizontally!)
        spin_deg = -360.0 * u
        set_hips_transform((0.0, 0.0, -0.05 + 0.02 * math.sin(u * 2.0 * math.pi)), 12.0, spin_deg, 0.0)
        set_local_euler_deg("Spine", 8.0, 0.0, 0.0)
        set_local_euler_deg("Chest", 6.0, -12.0, 0.0)
        set_local_euler_deg("Head", -12.0, 12.0, 0.0)
        # Right weapon arm extended straight out horizontally to cleave 360° ring!
        set_local_euler_deg("RightShoulder", 0.0, 0.0, -6.0)
        set_local_euler_deg("RightUpperArm", 0.0, -18.0, 0.0)
        set_local_euler_deg("RightLowerArm", 0.0, -12.0, 0.0)
        set_local_euler_deg("RightHand", 0.0, 0.0, -15.0)
        # Left arm raised outward for spinning momentum counter-balance
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, 10.0)
        set_local_euler_deg("LeftUpperArm", 0.0, 28.0, 15.0)
        set_local_euler_deg("LeftLowerArm", 0.0, 35.0, 0.0)
        # Athletic crouched spinning legs
        set_local_euler_deg("LeftUpperLeg", -28.0, 0.0, -12.0)
        set_local_euler_deg("LeftLowerLeg", 42.0, 0.0, 0.0)
        set_local_euler_deg("RightUpperLeg", -22.0, 0.0, 12.0)
        set_local_euler_deg("RightLowerLeg", 36.0, 0.0, 0.0)
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 6. DEMOLITIONIST: Shift_SatchelVault (C4 plant + 360° backward aerial vault! Frames 1..21)
    # =========================================================================
    actions.append((create_new_action("Shift_SatchelVault"), 11))
    prev_q = {}
    for f in range(1, 22):
        begin_frame_pose(f)
        if f <= 4:
            # Rapid C4 satchel slam crouch (Frames 1..4)
            u = (f - 1) / 3.0
            s = 3 * u * u - 2 * u * u * u
            set_hips_world_offset((0.0, -0.04 * s, -0.18 * s), 28.0 * s, 0.0, 0.0)
            set_local_euler_deg("Spine", 18.0 * s, 0.0, 0.0)
            set_local_euler_deg("Chest", 16.0 * s, 0.0, 0.0)
            set_local_euler_deg("Head", -18.0 * s, 0.0, 0.0)
            set_local_euler_deg("RightUpperArm", -55.0 * s, 30.0 * s, 25.0 * s)
            set_local_euler_deg("RightLowerArm", 0.0, 25.0 * s, 0.0)
            set_local_euler_deg("LeftUpperArm", 20.0 * s, -35.0 * s, -20.0 * s)
            set_local_euler_deg("LeftUpperLeg", -48.0 * s, 0.0, -10.0 * s)
            set_local_euler_deg("LeftLowerLeg", 72.0 * s, 0.0, 0.0)
            set_local_euler_deg("RightUpperLeg", -48.0 * s, 0.0, 10.0 * s)
            set_local_euler_deg("RightLowerLeg", 72.0 * s, 0.0, 0.0)
        elif f <= 17:
            # Explosive backward aerial 360° backflip vault! (Frames 5..17)
            u = (f - 4) / 13.0
            s = 3 * u * u - 2 * u * u * u
            arc = math.sin(u * math.pi)
            flip_pitch = 28.0 * (1.0 - s) - 360.0 * s
            # Soars cleanly UP +0.46m into the air while performing a crisp 360° backward aerial somersault!
            set_hips_world_offset((0.0, 0.14 * arc, -0.18 * (1.0 - arc) + 0.46 * arc), flip_pitch, 0.0, 0.0)
            set_local_euler_deg("Spine", -24.0 * arc, 0.0, 0.0)
            set_local_euler_deg("Chest", -22.0 * arc, 0.0, 0.0)
            set_local_euler_deg("Head", -20.0 * arc, 0.0, 0.0)
            # Arms swept wide in heroic aerial flip
            set_local_euler_deg("LeftUpperArm", 0.0, -25.0 * arc, 35.0 * arc)
            set_local_euler_deg("LeftLowerArm", 0.0, 35.0 * arc, 0.0)
            set_local_euler_deg("RightUpperArm", 0.0, 25.0 * arc, -35.0 * arc)
            set_local_euler_deg("RightLowerArm", 0.0, -35.0 * arc, 0.0)
            # Knees tucked tightly mid-flip
            set_local_euler_deg("LeftUpperLeg", -78.0 * arc, 0.0, -8.0 * arc)
            set_local_euler_deg("LeftLowerLeg", 108.0 * arc, 0.0, 0.0)
            set_local_euler_deg("RightUpperLeg", -78.0 * arc, 0.0, 8.0 * arc)
            set_local_euler_deg("RightLowerLeg", 108.0 * arc, 0.0, 0.0)
        else:
            # Crisp cushioned superhero combat landing back to neutral (Frames 18..21)
            u = (f - 17) / 4.0
            s = 3 * u * u - 2 * u * u * u
            crouch = 1.0 - s
            set_hips_world_offset((0.0, 0.0, -0.12 * crouch), -360.0 + 14.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Spine", 10.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Chest", 8.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Head", -12.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("LeftUpperLeg", -32.0 * crouch, 0.0, -8.0 * crouch)
            set_local_euler_deg("LeftLowerLeg", 52.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("RightUpperLeg", -32.0 * crouch, 0.0, 8.0 * crouch)
            set_local_euler_deg("RightLowerLeg", 52.0 * crouch, 0.0, 0.0)
        keyframe_all_bones(f, prev_q)

    # Export ALL 7 Shift actions into one clean lightweight FBX
    rig_obj.animation_data.action = actions[0][0]
    if hasattr(rig_obj.animation_data, "action_slot") and len(actions[0][0].slots) > 0:
        rig_obj.animation_data.action_slot = actions[0][0].slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()

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
        bake_anim_simplify_factor=0.0,
        path_mode='AUTO',
        embed_textures=False,
        apply_scale_options='FBX_SCALE_NONE'
    )

    # Render a crisp 7-pose side-by-side QA showcase strip!
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 480
    world = bpy.data.worlds.new("ShiftWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.16, 0.18, 0.22, 1.0)
        bg.inputs["Strength"].default_value = 0.9

    for name, loc, rot, energy in [
        ("KeyLight",  ( 2.0, -6.0, 4.0), (1.05, 0.0,  0.35), 4.0),
        ("FillLight", (-4.0, -4.0, 2.0), (1.20, 0.0, -0.60), 2.0),
    ]:
        ld = bpy.data.lights.new(name, type='SUN')
        ld.energy = energy
        lo = bpy.data.objects.new(name, ld)
        bpy.context.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = rot

    # Bake frozen snapshots of each of the 7 peak Shift poses spaced along X
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for idx, (act, sample_frame) in enumerate(actions):
        rig_obj.animation_data.action = act
        if hasattr(rig_obj.animation_data, "action_slot") and len(act.slots) > 0:
            rig_obj.animation_data.action_slot = act.slots[0]
        scene.frame_set(sample_frame)
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        px = (idx - 3.0) * 1.45
        for src in (head_obj, body_obj):
            eval_obj = src.evaluated_get(depsgraph)
            me = bpy.data.meshes.new_from_object(eval_obj)
            snap = bpy.data.objects.new(f"Snap_{idx}_{src.name}", me)
            bpy.context.collection.objects.link(snap)
            snap.location = Vector((px, 0.0, 0.0))
            snap.rotation_euler = Euler((0.0, 0.0, math.radians(-38.0)), 'XYZ')

    # Hide live rig from preview strip
    head_obj.hide_render = True
    body_obj.hide_render = True

    cam_d = bpy.data.cameras.new("ShiftCam")
    cam_d.type = 'ORTHO'
    cam_d.ortho_scale = 10.6
    cam_o = bpy.data.objects.new("ShiftCam", cam_d)
    bpy.context.collection.objects.link(cam_o)
    cam_o.location = (0.0, -6.0, 1.05)
    cam_o.rotation_euler = (math.pi / 2.0, 0.0, 0.0)
    scene.camera = cam_o
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    print(f"SUCCESSFULLY EXPORTED 7 SHIFT ANIMATIONS: {FBX_PATH}")
    print(f"PREVIEW STRIP: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()

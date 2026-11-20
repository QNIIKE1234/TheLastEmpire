import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math
from mathutils import Vector, Euler, Quaternion, Matrix
import ChibiAnimeKit as kit

OUT_DIR = kit.OUT_DIR
FBX_PATH = os.path.join(OUT_DIR, "Chibi_Melee_Animations.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "Chibi_Melee_Animations_Preview.png")


def build_mannequin():
    mat_body = kit.make_mat("Mat_MeleeMann_Body", (0.26, 0.32, 0.42, 1.0), roughness=0.75)
    mat_accent = kit.make_mat("Mat_MeleeMann_Accent", (0.95, 0.45, 0.12, 1.0), roughness=0.55)
    mat_head = kit.make_mat("Mat_MeleeMann_Head", (0.92, 0.78, 0.66, 1.0), roughness=0.80)

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


def smoothstep(u):
    u = max(0.0, min(1.0, u))
    return u * u * (3.0 - 2.0 * u)


def main():
    scene = kit.reset_scene()
    head_obj, body_obj = build_mannequin()

    # Exact 18-bone Chibi_Base_Rig from ChibiAnimeKit.BONE_DEFS
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

    def set_hips_world(world_offset, pitch_deg, yaw_deg, roll_deg):
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

    def aim_ik(upper_name, lower_name, end_name, target_world_pos, pole_world_vec, hand_rot_deg=(0, 0, 0)):
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
        if any(abs(a) > 1e-4 for a in hand_rot_deg):
            set_local_euler_deg(end_name, *hand_rot_deg)
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

    def create_action(name):
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
    # 1. KNIFE: Fast Reverse-Grip Forward Stab + Snappy Rip! (Frames 1..15)
    # =========================================================================
    actions.append((create_action("Melee_Knife"), 7))
    prev_q = {}
    for f in range(1, 16):
        begin_frame_pose(f)
        if f <= 3:
            t = smoothstep((f - 1) / 2.0)
            stab, rip = -0.35 * t, 0.0
        elif f <= 7:
            t = smoothstep((f - 3) / 4.0)
            stab, rip = -0.35 + 1.35 * t, 0.0
        elif f <= 11:
            t = smoothstep((f - 7) / 4.0)
            stab, rip = 1.0 - 0.35 * t, t
        else:
            t = smoothstep((f - 11) / 4.0)
            stab, rip = 0.65 * (1.0 - t), 1.0 - t
        yaw = 28.0 * stab - 36.0 * rip
        set_hips_world((0.0, -0.14 * max(0.0, stab), -0.06 * abs(stab)), 14.0 * max(0.0, stab), yaw * 0.45, 0.0)
        set_local_euler_deg("Spine", 8.0 * max(0.0, stab), yaw * 0.30, 0.0)
        set_local_euler_deg("Chest", 6.0 * max(0.0, stab), yaw * 0.35, 0.0)
        set_local_euler_deg("Head", -10.0 * max(0.0, stab), -yaw * 0.45, 0.0)
        # Straight forward dagger stab -> quick outward horizontal rip!
        rx = -0.16 + 0.14 * max(0.0, stab) + 0.36 * rip
        ry = -0.22 - 0.38 * stab + 0.14 * rip
        rz = 1.06 + 0.04 * stab
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (rx, ry, rz), (-0.45, 0.30, -0.45), (0, -25.0 * stab, -35.0 * rip))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.30, -0.10 + 0.15 * max(0.0, stab), 0.92), (0.45, 0.60, -0.3))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.14 * max(0.0, stab), 0.11), (0.16, -1.0, 0.2))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.14 * max(0.0, stab), 0.11), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 2. BASEBALL BAT: Two-Handed Slugger Home-Run Power Swing! (Frames 1..21)
    # =========================================================================
    actions.append((create_action("Melee_BaseballBat"), 10))
    prev_q = {}
    for f in range(1, 22):
        begin_frame_pose(f)
        if f <= 6:
            swing = -smoothstep((f - 1) / 5.0)
        elif f <= 12:
            swing = -1.0 + 2.0 * smoothstep((f - 6) / 6.0)
        else:
            swing = 1.0 - smoothstep((f - 12) / 9.0)
        yaw = -62.0 * swing
        set_hips_world((0.0, -0.10 * max(0.0, swing), -0.06 * abs(swing)), 10.0 * abs(swing), yaw * 0.45, -8.0 * swing)
        set_local_euler_deg("Spine", 6.0 * abs(swing), yaw * 0.30, 0.0)
        set_local_euler_deg("Chest", 6.0 * abs(swing), yaw * 0.35, 0.0)
        set_local_euler_deg("Head", -6.0 * abs(swing), -yaw * 0.55, 0.0)
        # Two-handed wide level batter's arc!
        arc = swing * 1.45
        hx = 0.52 * math.sin(arc)
        hy = -0.44 * math.cos(arc) - 0.04
        hz = 1.18 + 0.14 * (1.0 - abs(swing))
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (hx - 0.04, hy, hz), (-0.50, 0.25, -0.4), (0, 0, -45.0 * swing))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (hx + 0.05, hy + 0.02, hz - 0.03), (0.50, 0.25, -0.4))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.18, -0.16 * max(0.0, swing), 0.11), (0.18, -1.0, 0.2))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.12 * max(0.0, swing), 0.11), (-0.18, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 3. MACHETE: Heavy Diagonal Jungle Hack (High Right -> Low Left)! (Frames 1..19)
    # =========================================================================
    actions.append((create_action("Melee_Machete"), 10))
    prev_q = {}
    for f in range(1, 20):
        begin_frame_pose(f)
        if f <= 6:
            chop = -smoothstep((f - 1) / 5.0)
        elif f <= 11:
            chop = -1.0 + 2.0 * smoothstep((f - 6) / 5.0)
        else:
            chop = 1.0 - smoothstep((f - 11) / 8.0)
        yaw = -48.0 * chop
        set_hips_world((0.0, -0.09 * max(0.0, chop), -0.08 * max(0.0, chop)), 16.0 * max(0.0, chop), yaw * 0.45, -10.0 * chop)
        set_local_euler_deg("Spine", 10.0 * chop, yaw * 0.30, 0.0)
        set_local_euler_deg("Chest", 10.0 * chop, yaw * 0.35, 0.0)
        set_local_euler_deg("Head", -8.0 * chop, -yaw * 0.45, 0.0)
        # Diagonal high-right (-X, +Z) -> low-left (+X, -Z) jungle cleave!
        rx = -0.34 + 0.72 * ((chop + 1.0) * 0.5)
        ry = -0.22 - 0.30 * math.cos(chop * 1.1)
        rz = 1.56 - 0.76 * ((chop + 1.0) * 0.5)
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (rx, ry, rz), (-0.50, 0.25, -0.3), (25.0 * chop, 0, -40.0 * chop))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.34, 0.08 * chop, 0.86), (0.45, 0.60, -0.3))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.14 * max(0.0, chop), 0.11), (0.16, -1.0, 0.2))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.10 * max(0.0, chop), 0.11), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 4. FIRE AXE: Massive Heavy Two-Handed 180° Half-Circle Horizontal Cleave! (Frames 1..23)
    #    Coils deep to the right, then sweeps a full wide semicircle from far right -> front -> far left!
    # =========================================================================
    actions.append((create_action("Melee_FireAxe"), 11))
    prev_q = {}
    for f in range(1, 24):
        begin_frame_pose(f)
        if f <= 7:
            # Deep heavy windup coiled far to the right (-1.0)
            u = smoothstep((f - 1) / 6.0)
            sweep = -u
            weight = u
        elif f <= 14:
            # Explosive 180° half-circle horizontal cleave from far right (-1.0) -> front (0) -> far left (+1.0)!
            u = smoothstep((f - 7) / 7.0)
            sweep = -1.0 + 2.0 * u
            weight = 1.0
        elif f <= 17:
            # Heavy follow-through hold on far left
            sweep = 1.0
            weight = 1.0
        else:
            # Smooth heavy recovery back to neutral stance
            u = smoothstep((f - 17) / 6.0)
            sweep = 1.0 - u
            weight = 1.0 - u

        # Huge torso coil & horizontal rotation driving the 180° arc!
        yaw = -72.0 * sweep
        forward_lunge = math.cos(sweep * math.pi * 0.5) * weight
        set_hips_world((0.0, -0.14 * forward_lunge, -0.10 * weight), 14.0 * weight, yaw * 0.48, -10.0 * sweep)
        set_local_euler_deg("Spine", 8.0 * weight, yaw * 0.28, 0.0)
        set_local_euler_deg("Chest", 8.0 * weight, yaw * 0.34, 0.0)
        set_local_euler_deg("Head", -10.0 * weight, -yaw * 0.55, 0.0)

        # Wide 200° horizontal half-circle arc at chest height (radius ~0.58m!)
        arc_rad = sweep * math.radians(102.0)
        hx = 0.58 * math.sin(arc_rad)
        hy = -0.54 * math.cos(arc_rad) - 0.04
        hz = 1.08 + 0.06 * (1.0 - abs(sweep))
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (hx - 0.04 * math.cos(arc_rad), hy, hz), (-0.52, 0.25, -0.35), (0.0, 0.0, -68.0 * sweep))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (hx + 0.06 * math.cos(arc_rad), hy + 0.03, hz - 0.03), (0.52, 0.25, -0.35))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.19, -0.18 * forward_lunge, 0.11), (0.19, -1.0, 0.25))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.19, 0.14 * forward_lunge, 0.11), (-0.19, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 5. KATANA: Sleek Anime Iaijutsu Quick-Draw Horizontal Flash Cut! (Frames 1..19)
    # =========================================================================
    actions.append((create_action("Melee_Katana"), 9))
    prev_q = {}
    for f in range(1, 20):
        begin_frame_pose(f)
        if f <= 5:
            # Coiled low iaijutsu draw stance at left hip!
            t = smoothstep((f - 1) / 4.0)
            draw = -t
        elif f <= 9:
            # Lightning-fast razor horizontal flash slash left -> far right!
            t = smoothstep((f - 5) / 4.0)
            draw = -1.0 + 2.0 * t
        elif f <= 13:
            # Iconic anime samurai follow-through pause!
            draw = 1.0
        else:
            t = smoothstep((f - 13) / 6.0)
            draw = 1.0 - t
        yaw = 58.0 * draw
        crouch = -0.12 * abs(draw)
        set_hips_world((0.0, -0.14 * max(0.0, draw), crouch), 16.0 * abs(draw), yaw * 0.48, 8.0 * draw)
        set_local_euler_deg("Spine", 10.0 * abs(draw), yaw * 0.28, 0.0)
        set_local_euler_deg("Chest", 8.0 * abs(draw), yaw * 0.34, 0.0)
        set_local_euler_deg("Head", -14.0 * abs(draw), -yaw * 0.55, 0.0)
        # Sweeps from left hip (+0.36) explosively across front to wide right (-0.54)!
        arc = draw * 1.45
        rx = -0.52 * math.sin(arc) - 0.04
        ry = -0.46 * math.cos(arc)
        rz = 1.04 + 0.16 * max(0.0, draw)
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (rx, ry, rz), (-0.45, 0.30, -0.4), (0, 0, 48.0 * draw))
        # Left hand holds imaginary saya sheath at left hip!
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.28, -0.05 + 0.16 * max(0.0, draw), 0.84), (0.45, 0.65, -0.3))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.18, 0.14 * max(0.0, draw), 0.11), (0.18, -1.0, 0.25))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, -0.18 * max(0.0, draw), 0.11), (-0.18, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 6. SLEDGEHAMMER: Heroic Mini-Leap Overhead Earth-Shatter Slam! (Frames 1..25)
    # =========================================================================
    actions.append((create_action("Melee_Sledgehammer"), 14))
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        if f <= 9:
            u = smoothstep((f - 1) / 8.0)
            slam = -u
            hop = 0.28 * math.sin(u * math.pi * 0.85)
        elif f <= 14:
            u = (f - 9) / 5.0
            slam = -1.0 + 2.0 * (u * u)
            hop = 0.14 * (1.0 - u) - 0.20 * u
        elif f <= 18:
            slam, hop = 1.0, -0.20
        else:
            u = smoothstep((f - 18) / 7.0)
            slam = 1.0 - u
            hop = -0.20 * (1.0 - u)
        pitch = 32.0 * slam
        set_hips_world((0.0, -0.16 * max(0.0, slam), hop), pitch * 0.60, 0.0, 0.0)
        set_local_euler_deg("Spine", pitch * 0.50, 0.0, 0.0)
        set_local_euler_deg("Chest", pitch * 0.60, 0.0, 0.0)
        set_local_euler_deg("Head", -pitch * 0.40, 0.0, 0.0)
        hy = -0.16 + 0.30 * (-slam) if slam < 0 else -0.16 - 0.38 * slam
        hz = 1.20 + 0.66 * (-slam) if slam < 0 else 1.20 - 0.72 * slam
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.06, hy, hz), (-0.45, 0.20, -0.3))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.06, hy + 0.03, hz - 0.04), (0.45, 0.20, -0.3))
        foot_z = 0.11 + max(0.0, hop * 0.75)
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.18, -0.18 * max(0.0, slam), foot_z), (0.18, -1.0, 0.3))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.14 * max(0.0, slam), foot_z), (-0.18, -1.0, 0.3))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 7. SURVIVAL SPEAR: Long-Reach Two-Handed Forward Lunge Thrust! (Frames 1..19)
    # =========================================================================
    actions.append((create_action("Melee_SurvivalSpear"), 9))
    prev_q = {}
    for f in range(1, 20):
        begin_frame_pose(f)
        if f <= 5:
            thrust = -smoothstep((f - 1) / 4.0)
        elif f <= 9:
            thrust = -1.0 + 2.0 * smoothstep((f - 5) / 4.0)
        elif f <= 12:
            thrust = 1.0
        else:
            thrust = 1.0 - smoothstep((f - 12) / 7.0)
        # Sideways spear stance pulling right hip back -> explosive straight lunge forward!
        yaw = 34.0 - 18.0 * thrust
        set_hips_world((0.0, -0.22 * max(0.0, thrust) + 0.06 * max(0.0, -thrust), -0.09 * max(0.0, thrust)), 16.0 * max(0.0, thrust), yaw * 0.5, 0.0)
        set_local_euler_deg("Spine", 8.0 * max(0.0, thrust), yaw * 0.25, 0.0)
        set_local_euler_deg("Chest", 6.0 * max(0.0, thrust), yaw * 0.25, 0.0)
        set_local_euler_deg("Head", -12.0 * max(0.0, thrust), -yaw * 0.65, 0.0)
        # Right hand drives straight forward like a pool-cue spear jab; Left hand guides the front shaft!
        ry = -0.10 - 0.52 * thrust
        ly = -0.34 - 0.32 * thrust
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.14, ry, 1.02 + 0.06 * thrust), (-0.50, 0.30, -0.50))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (-0.02, ly, 1.06 + 0.06 * thrust), (0.45, 0.20, -0.60))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.26 * max(0.0, thrust), 0.11), (0.16, -1.0, 0.25))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.22 * max(0.0, thrust), 0.11), (-0.16, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 8. STUN BATON: Crisp Tactical Riot Electro-Jab & Whip Strike! (Frames 1..17)
    # =========================================================================
    actions.append((create_action("Melee_StunBaton"), 8))
    prev_q = {}
    for f in range(1, 18):
        begin_frame_pose(f)
        if f <= 4:
            strike = -smoothstep((f - 1) / 3.0)
        elif f <= 8:
            strike = -1.0 + 2.0 * smoothstep((f - 4) / 4.0)
        elif f <= 11:
            strike = 1.0
        else:
            strike = 1.0 - smoothstep((f - 11) / 6.0)
        yaw = -32.0 * strike
        set_hips_world((0.0, -0.10 * max(0.0, strike), -0.04 * abs(strike)), 10.0 * abs(strike), yaw * 0.45, 0.0)
        set_local_euler_deg("Spine", 6.0 * abs(strike), yaw * 0.28, 0.0)
        set_local_euler_deg("Chest", 6.0 * abs(strike), yaw * 0.32, 0.0)
        set_local_euler_deg("Head", -8.0 * abs(strike), -yaw * 0.45, 0.0)
        # Sharp snapping riot-baton forward electro-crack + left forearm raised in riot guard!
        rx = -0.26 + 0.38 * ((strike + 1.0) * 0.5)
        ry = -0.20 - 0.36 * max(0.0, strike)
        rz = 1.32 - 0.24 * ((strike + 1.0) * 0.5)
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (rx, ry, rz), (-0.45, 0.35, -0.40), (15.0 * strike, 0, -25.0 * strike))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.20, -0.28, 1.12), (0.55, 0.25, -0.50))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.12 * max(0.0, strike), 0.11), (0.16, -1.0, 0.2))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.10 * max(0.0, strike), 0.11), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 9. CHAINSAW: Aggressive Two-Handed Revving Saw Thrust & Rip! (Frames 1..13)
    # =========================================================================
    actions.append((create_action("Melee_Chainsaw"), 6))
    prev_q = {}
    for f in range(1, 14):
        begin_frame_pose(f)
        ang = ((f - 1) / 12.0) * 2.0 * math.pi
        saw = math.sin(ang)
        vib = math.sin(ang * 3.0) * 0.025
        # Hunched power stance thrusting the roaring chainsaw straight forward with fierce side-rip!
        set_hips_world((0.03 * saw, -0.10, -0.10), 18.0, -14.0 * saw, 4.0 * saw)
        set_local_euler_deg("Spine", 10.0, -8.0 * saw, 0.0)
        set_local_euler_deg("Chest", 10.0, -10.0 * saw, 0.0)
        set_local_euler_deg("Head", -20.0, 10.0 * saw, 0.0)
        # Right hand on rear chainsaw grip, Left hand on front wrap handle, sawing fiercely forward!
        aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.14 + 0.10 * saw, -0.34 - 0.08 * abs(saw) + vib, 0.96 + vib), (-0.48, 0.25, -0.60))
        aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.04 + 0.10 * saw, -0.48 - 0.08 * abs(saw) - vib, 1.04 + vib), (0.48, 0.20, -0.65))
        aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.18, -0.14, 0.11), (0.18, -1.0, 0.25))
        aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.18, 0.14, 0.11), (-0.18, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # Export all 9 melee animations into one clean FBX
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

    # Render a crisp 9-pose QA strip (left to right: Knife, Bat, Machete, FireAxe, Katana, Hammer, Spear, Baton, Chainsaw)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 2160
    scene.render.resolution_y = 480
    world = bpy.data.worlds.new("MeleeWorld")
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

    for idx, (act, sample_frame) in enumerate(actions):
        rig_obj.animation_data.action = act
        if hasattr(rig_obj.animation_data, "action_slot") and len(act.slots) > 0:
            rig_obj.animation_data.action_slot = act.slots[0]
        scene.frame_set(sample_frame)
        bpy.context.view_layer.update()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        px = (idx - 4.0) * 1.45
        for src in (head_obj, body_obj):
            eval_obj = src.evaluated_get(depsgraph)
            me = bpy.data.meshes.new_from_object(eval_obj)
            snap = bpy.data.objects.new(f"Snap_{idx}_{src.name}", me)
            bpy.context.collection.objects.link(snap)
            snap.location = Vector((px, 0.0, 0.0))
            snap.rotation_euler = Euler((0.0, 0.0, math.radians(-35.0)), 'XYZ')

    head_obj.hide_render = True
    body_obj.hide_render = True

    cam_d = bpy.data.cameras.new("MeleeCam")
    cam_d.type = 'ORTHO'
    cam_d.ortho_scale = 13.6
    cam_o = bpy.data.objects.new("MeleeCam", cam_d)
    bpy.context.collection.objects.link(cam_o)
    cam_o.location = (0.0, -6.0, 1.05)
    cam_o.rotation_euler = (math.pi / 2.0, 0.0, 0.0)
    scene.camera = cam_o
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    print(f"SUCCESSFULLY EXPORTED 9 MELEE WEAPON ANIMATIONS: {FBX_PATH}")
    print(f"PREVIEW STRIP: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()

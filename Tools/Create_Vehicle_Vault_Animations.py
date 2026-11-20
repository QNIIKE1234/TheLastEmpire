import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import math
from mathutils import Vector, Euler, Quaternion, Matrix
import ChibiAnimeKit as kit

OUT_DIR = kit.OUT_DIR
FBX_PATH = os.path.join(OUT_DIR, "Chibi_Vault_Animations.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "Chibi_Vault_Animations_Preview.png")


def build_mannequin():
    mat_body = kit.make_mat("Mat_VaultMann_Body", (0.24, 0.32, 0.44, 1.0), roughness=0.75)
    mat_accent = kit.make_mat("Mat_VaultMann_Accent", (0.22, 0.85, 0.96, 1.0), roughness=0.55)
    mat_head = kit.make_mat("Mat_VaultMann_Head", (0.92, 0.78, 0.66, 1.0), roughness=0.80)

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
    # 1. Vault_MotoJump: Athletic Parkour Hurdle Leap over Motorcycle! (Frames 1..21)
    #    (Vertical trajectory is driven by PlayerController; animation adds expressive
    #     tucked hurdle leap, bracing vault-hand, kicking legs, and cushioned landing!)
    # =========================================================================
    actions.append((create_action("Vault_MotoJump"), 10))
    prev_q = {}
    for f in range(1, 22):
        begin_frame_pose(f)
        if f <= 4:
            # Explosive takeoff crouch & spring!
            u = smoothstep((f - 1) / 3.0)
            set_hips_world((0.0, 0.0, -0.14 * u), 22.0 * u, 0.0, 0.0)
            set_local_euler_deg("Spine", 14.0 * u, 0.0, 0.0)
            set_local_euler_deg("Chest", 10.0 * u, 0.0, 0.0)
            set_local_euler_deg("Head", -18.0 * u, 0.0, 0.0)
            aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.28, -0.26 * u, 0.88), (0.45, 0.50, -0.3))
            aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.34, 0.22 * u, 0.90), (-0.45, 0.60, -0.2))
            aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.10 * u, 0.11), (0.16, -1.0, 0.25))
            aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.12 * u, 0.11), (-0.16, -1.0, 0.25))
        elif f <= 16:
            # Mid-air dynamic parkour hurdle! Left knee driven high & forward, right leg tucked back, left hand vault-pushing down!
            u = (f - 4) / 12.0
            air = math.sin(u * math.pi)
            phase = smoothstep(u)
            set_hips_world((0.0, 0.0, 0.08 * air), 26.0 * air, -14.0 * air, -10.0 * air)
            set_local_euler_deg("Spine", 14.0 * air, 6.0 * air, 0.0)
            set_local_euler_deg("Chest", 10.0 * air, 8.0 * air, 0.0)
            set_local_euler_deg("Head", -24.0 * air, -6.0 * air, 6.0 * air)
            # Left hand reaches down to vault off the motorcycle seat/bars; Right weapon arm sweeps wide for balance!
            aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.16, -0.18 + 0.30 * phase, 0.62 + 0.18 * (1.0 - air)), (0.50, 0.30, -0.3))
            aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.48, -0.04 + 0.18 * air, 1.24 + 0.14 * air), (-0.50, 0.50, 0.3))
            # High tucked hurdle legs clearing the bike!
            set_local_euler_deg("LeftUpperLeg", -82.0 * air, 0.0, -14.0 * air)
            set_local_euler_deg("LeftLowerLeg", 88.0 * air, 0.0, 0.0)
            set_local_euler_deg("RightUpperLeg", -45.0 * air + 28.0 * phase * air, 0.0, 14.0 * air)
            set_local_euler_deg("RightLowerLeg", 105.0 * air, 0.0, 0.0)
        else:
            # Cushioned heroic landing back to neutral!
            u = smoothstep((f - 16) / 5.0)
            crouch = 1.0 - u
            set_hips_world((0.0, 0.0, -0.14 * crouch), 16.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Spine", 10.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Chest", 8.0 * crouch, 0.0, 0.0)
            set_local_euler_deg("Head", -12.0 * crouch, 0.0, 0.0)
            aim_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.34, -0.10 * crouch, 0.82), (0.45, 0.60, -0.2))
            aim_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.34, -0.10 * crouch, 0.82), (-0.45, 0.60, -0.2))
            aim_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, -0.14 * crouch, 0.11), (0.16, -1.0, 0.25))
            aim_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, 0.14 * crouch, 0.11), (-0.16, -1.0, 0.25))
        keyframe_all_bones(f, prev_q)

    # =========================================================================
    # 2. Vault_CarSlide: Iconic Action-Movie Hood / Roof Side-Slide! (Frames 1..25)
    #    Phase 1 (1..5): Quick mount hop onto hood/roof
    #    Phase 2 (6..19): Sleek reclined sideways hip slide (left hand braced flat on roof,
    #                     body angled sideways ~52°, legs swept sleekly forward!)
    #    Phase 3 (20..25): Snappy spin-off athletic landing!
    # =========================================================================
    actions.append((create_action("Vault_CarSlide"), 12))
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        if f <= 5:
            slide = smoothstep((f - 1) / 4.0)
        elif f <= 19:
            slide = 1.0
        else:
            slide = 1.0 - smoothstep((f - 19) / 6.0)

        # Subtle glide rhythm while sliding across the car roof
        glide_wob = math.sin(((f - 5) / 14.0) * math.pi) * 0.04 if 5 < f < 20 else 0.0

        # Reclined sideways hip-slide! Hips drop low onto the roof surface, tilted sideways (-56° roll) & slightly reclined (-22° pitch) + yawed (+48°)!
        set_hips_world((0.0, 0.0, -0.38 * slide + glide_wob), -22.0 * slide, 48.0 * slide, -56.0 * slide)
        set_local_euler_deg("Spine", 10.0 * slide, -12.0 * slide, 18.0 * slide)
        set_local_euler_deg("Chest", 12.0 * slide, -16.0 * slide, 20.0 * slide)
        set_local_euler_deg("Head", 12.0 * slide, -28.0 * slide, 18.0 * slide)

        # Left arm reaches DOWN firmly onto the car roof to anchor the slide!
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, -20.0 * slide)
        set_local_euler_deg("LeftUpperArm", 12.0 * slide, 18.0 * slide, -52.0 * slide)
        set_local_euler_deg("LeftLowerArm", 0.0, 22.0 * slide, -10.0 * slide)
        set_local_euler_deg("LeftHand", 0.0, 0.0, 35.0 * slide)

        # Right weapon arm raised clear & poised stylishly above the roof!
        set_local_euler_deg("RightShoulder", 0.0, 0.0, 16.0 * slide)
        set_local_euler_deg("RightUpperArm", -22.0 * slide, -34.0 * slide, 36.0 * slide)
        set_local_euler_deg("RightLowerArm", 0.0, -45.0 * slide, 12.0 * slide)

        # Iconic hood-slide legs: Right leg extended sleekly straight forward, Left leg bent tucked underneath!
        set_local_euler_deg("RightUpperLeg", -62.0 * slide, 0.0, 18.0 * slide)
        set_local_euler_deg("RightLowerLeg", 18.0 * slide, 0.0, 0.0)
        set_local_euler_deg("LeftUpperLeg", -78.0 * slide, 0.0, -10.0 * slide)
        set_local_euler_deg("LeftLowerLeg", 96.0 * slide, 0.0, 0.0)
        keyframe_all_bones(f, prev_q)

    # Export both Vault animations into Chibi_Vault_Animations.fbx
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

    # Render crisp 2-pose preview (Left = MotoJump, Right = CarSlide)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 520
    world = bpy.data.worlds.new("VaultWorld")
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
        px = -1.05 if idx == 0 else 1.05
        for src in (head_obj, body_obj):
            eval_obj = src.evaluated_get(depsgraph)
            me = bpy.data.meshes.new_from_object(eval_obj)
            snap = bpy.data.objects.new(f"Snap_{idx}_{src.name}", me)
            bpy.context.collection.objects.link(snap)
            snap.location = Vector((px, 0.0, 0.0))
            snap.rotation_euler = Euler((0.0, 0.0, math.radians(-38.0)), 'XYZ')

    head_obj.hide_render = True
    body_obj.hide_render = True

    cam_d = bpy.data.cameras.new("VaultCam")
    cam_d.type = 'ORTHO'
    cam_d.ortho_scale = 4.6
    cam_o = bpy.data.objects.new("VaultCam", cam_d)
    bpy.context.collection.objects.link(cam_o)
    cam_o.location = (0.0, -6.0, 0.95)
    cam_o.rotation_euler = (math.pi / 2.0, 0.0, 0.0)
    scene.camera = cam_o
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)

    print(f"SUCCESSFULLY EXPORTED 2 VEHICLE VAULT ANIMATIONS: {FBX_PATH}")
    print(f"PREVIEW: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()

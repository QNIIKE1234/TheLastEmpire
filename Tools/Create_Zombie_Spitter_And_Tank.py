import sys
import os
import shutil
import time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler
import ChibiAnimeKit as kit

ZOMBIE_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels\Zombies"
ZOMBIE_TEX_DIR = os.path.join(ZOMBIE_DIR, "Textures")
TMP_EXPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_tmp_zombie_export")
os.makedirs(ZOMBIE_TEX_DIR, exist_ok=True)
os.makedirs(TMP_EXPORT_DIR, exist_ok=True)


def _copy_with_retry(src, dst, retries=12, delay=0.5):
    for attempt in range(retries):
        try:
            shutil.copy2(src, dst)
            return
        except OSError as e:
            if attempt == retries - 1:
                raise
            print(f"[Retry {attempt + 1}/{retries}] {os.path.basename(dst)}: {e}")
            time.sleep(delay)


def _setup_studio_render(scene):
    world = bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.17, 0.21, 1.0)
        bg.inputs[1].default_value = 1.35

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 52
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    key_data = bpy.data.lights.new(name="KeyLight", type='SUN')
    key_data.energy = 3.2
    key_obj = bpy.data.objects.new(name="KeyLight", object_data=key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = Euler((math.radians(52), math.radians(-18), math.radians(-22)), 'XYZ')

    fill_data = bpy.data.lights.new(name="FillLight", type='SUN')
    fill_data.energy = 1.9
    fill_obj = bpy.data.objects.new(name="FillLight", object_data=fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = Euler((math.radians(65), math.radians(30), math.radians(35)), 'XYZ')

    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    return cam_obj


def _make_pose_helpers(scene, rig_obj, rest_lengths):
    def begin_frame(frame):
        scene.frame_set(frame)
        for pb in rig_obj.pose.bones:
            pb.location = Vector((0, 0, 0))
            pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
            pb.scale = Vector((1, 1, 1))
        bpy.context.view_layer.update()

    def rot_deg(bname, rx, ry, rz):
        pb = rig_obj.pose.bones[bname]
        pb.rotation_quaternion = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ').to_quaternion()
        bpy.context.view_layer.update()

    def set_hips(loc_offset, pitch_deg, yaw_deg, roll_deg):
        pb = rig_obj.pose.bones["Hips"]
        pb.location = Vector(loc_offset)
        q_p = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
        q_y = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
        q_r = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
        pb.rotation_quaternion = q_y @ q_r @ q_p
        bpy.context.view_layer.update()

    def swing_bone(pb, target_pt):
        cur_dir = (pb.tail - pb.head).normalized()
        tgt_dir = (Vector(target_pt) - pb.head).normalized()
        q_s = cur_dir.rotation_difference(tgt_dir)
        m = pb.matrix.copy()
        pb.matrix = Matrix.LocRotScale(m.to_translation(), q_s @ m.to_quaternion(), Vector((1, 1, 1)))
        bpy.context.view_layer.update()

    def ik_two(upper_name, lower_name, end_name, target_pos, pole_vec, end_rot=(0, 0, 0)):
        pb_u = rig_obj.pose.bones[upper_name]
        pb_l = rig_obj.pose.bones[lower_name]
        pb_e = rig_obj.pose.bones[end_name]
        root = pb_u.head.copy()
        L1, L2 = rest_lengths[upper_name], rest_lengths[lower_name]
        tgt = Vector(target_pos)
        d_vec = tgt - root
        D = max(abs(L1 - L2) + 0.02, min((L1 + L2) * 0.99, d_vec.length))
        tgt = root + d_vec.normalized() * D
        cos_a = max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2.0 * L1 * D)))
        sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))
        dir_n = d_vec.normalized()
        pole = Vector(pole_vec).normalized()
        plane = pole - dir_n * pole.dot(dir_n)
        plane = Vector((0, 1, 0)) if plane.length_squared < 1e-6 else plane.normalized()
        mid = root + dir_n * (L1 * cos_a) + plane * (L1 * sin_a)
        swing_bone(pb_u, mid)
        swing_bone(pb_l, tgt)
        if any(abs(a) > 1e-4 for a in end_rot):
            rot_deg(end_name, *end_rot)
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
            slot = act.slots[0] if len(act.slots) > 0 else act.slots.new(id_type='OBJECT', name=rig_obj.name)
            rig_obj.animation_data.action_slot = slot
        return act

    return begin_frame, rot_deg, set_hips, ik_two, key_all, new_action


def _export_fbx_and_blend(rig_obj, head_obj, body_obj, base_name):
    tmp_fbx = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Rigged.fbx")
    tmp_blend = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Source.blend")

    bpy.ops.object.select_all(action='DESELECT')
    rig_obj.select_set(True)
    head_obj.select_set(True)
    body_obj.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj

    bpy.ops.export_scene.fbx(
        filepath=tmp_fbx,
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
        path_mode='COPY',
        embed_textures=True
    )
    bpy.ops.wm.save_as_mainfile(filepath=tmp_blend)
    _copy_with_retry(tmp_fbx, os.path.join(ZOMBIE_DIR, f"{base_name}_Rigged.fbx"))
    _copy_with_retry(tmp_blend, os.path.join(ZOMBIE_DIR, f"{base_name}_Source.blend"))
    print(f"EXPORTED {base_name} FBX + BLEND OK!")


# ==============================================================================
#  1. CHIBI ZOMBIE SPITTER — Crimson Mohawk Punk Cape Raider (Ref 1 + Ref 3 Sprint!)
# ==============================================================================

def generate_spitter_face_texture():
    W, H = 1024, 1024
    aa = 0.0022
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    # Dark-skinned undead zombie tone (deep dark-brown with cool ashen/necrotic pallor!)
    SKIN_BASE   = (0.25, 0.21, 0.20)
    SKIN_SHADOW = (0.14, 0.12, 0.15)
    SKIN_WARM   = (0.31, 0.27, 0.25)
    BRUISE_COL  = (0.16, 0.22, 0.17)
    ACID_COL    = (0.58, 0.98, 0.16)
    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.90, V) * 0.30, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.06, V) * 0.26, BRUISE_COL)
    for side in (-1.0, 1.0):
        d_ch = np.hypot((U - (0.5 + side * 0.18)) / 0.080, (V - 0.38) / 0.045)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.2, 1.0, d_ch)) * 0.28, BRUISE_COL)

    # Undead stitched scar on cheek + subtle dark brow shading behind sunglasses
    scar = [(0.62, 0.41), (0.71, 0.36)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, scar, [0.005, 0.005]))) * 0.85, (0.42, 0.12, 0.16))

    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.148
        cy = 0.478
        d_s = np.hypot((U - cx) / 0.092, (V - cy) / 0.048)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_s)) * 0.55, (0.09, 0.08, 0.11))
        brow = [(cx + side * (-0.082), cy + 0.068), (cx + side * (0.015), cy + 0.086), (cx + side * (0.086), cy + 0.074)]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, brow, [0.008, 0.011, 0.005])), (0.10, 0.06, 0.06))

    # Crisp nose dot
    d_n = np.hypot((U - 0.500) / 0.014, (V - 0.402) / 0.012)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.55, SKIN_SHADOW)

    # Signature cocky wide UNDEAD punk grin with yellowed teeth + glowing acid drool!
    mx = (U - 0.500) / 0.135
    my = V - 0.300
    y_top = 0.012 + 0.022 * (mx ** 2)
    y_bot = -0.046 * np.clip(1.0 - mx ** 2, 0.0, 1.0) + 0.020 * (mx ** 2)
    in_mx = kit.smoothstep(-1.04, -0.94, mx) * (1.0 - kit.smoothstep(0.94, 1.04, mx))
    mouth_mask = in_mx * kit.smoothstep(-aa, aa, my - y_bot) * kit.smoothstep(-aa, aa, y_top - my)

    kit.alpha_blend(canvas, mouth_mask, (0.90, 0.88, 0.68))
    red_inner = mouth_mask * (1.0 - kit.smoothstep(-0.012, 0.002, my - (y_bot + 0.016)))
    kit.alpha_blend(canvas, red_inner, (0.38, 0.12, 0.14))
    for tx in (0.42, 0.46, 0.50, 0.54, 0.58):
        d_tv = kit.polyline_sdf(U, V, [(tx, 0.285), (tx, 0.332)], [0.0030, 0.0030])
        kit.alpha_blend(canvas, mouth_mask * (1.0 - kit.smoothstep(-aa, aa, d_tv)) * 0.72, (0.12, 0.09, 0.08))

    top_b = [(0.365, 0.332), (0.435, 0.316), (0.500, 0.312), (0.565, 0.316), (0.635, 0.332)]
    bot_b = [(0.365, 0.328), (0.435, 0.276), (0.500, 0.256), (0.565, 0.276), (0.635, 0.328)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, top_b, [0.004, 0.005, 0.005, 0.005, 0.004])), (0.08, 0.06, 0.07))
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, bot_b, [0.004, 0.005, 0.005, 0.005, 0.004])), (0.08, 0.06, 0.07))

    # Glowing toxic acid drool trickling from corner of Spitter's undead grin!
    drool = [(0.612, 0.296), (0.620, 0.242), (0.616, 0.192)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, drool, [0.009, 0.007, 0.011]))) * 0.92, ACID_COL)

    path = os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieSpitter_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "Chibi_ZombieSpitter_Face.png")


def build_zombie_spitter():
    scene = kit.reset_scene()
    img_face = generate_spitter_face_texture()
    img_hair = kit.generate_hair_gradient_texture(
        os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieSpitter_Hair.png"), "Chibi_ZombieSpitter_Hair.png",
        root_rgb=(0.44, 0.07, 0.06),
        mid_rgb=(0.76, 0.14, 0.11),
        tip_rgb=(0.90, 0.24, 0.14),
        hi_rgb=(1.00, 0.56, 0.40),
        band_v=0.45, band_strength=0.42
    )

    m_face      = kit.make_mat("Mat_Spitter_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin      = kit.make_mat("Mat_Spitter_Skin",         (0.25, 0.21, 0.20, 1.0), roughness=0.88, specular=0.04)
    m_hair      = kit.make_mat("Mat_Spitter_Hair",         (1.0, 1.0, 1.0, 1.0), roughness=0.78, specular=0.08, image=img_hair)
    m_undercut  = kit.make_mat("Mat_Spitter_Undercut",     (0.32, 0.30, 0.34, 1.0), roughness=0.92, specular=0.03)
    m_headband  = kit.make_mat("Mat_Spitter_Headband",     (0.32, 0.20, 0.16, 1.0), roughness=0.88, specular=0.05)
    m_glasses   = kit.make_mat("Mat_Spitter_Glasses",      (0.05, 0.05, 0.07, 1.0), roughness=0.14, specular=0.85)
    m_tank      = kit.make_mat("Mat_Spitter_TankTop",      (0.58, 0.50, 0.36, 1.0), roughness=0.88, specular=0.04)
    m_cape      = kit.make_mat("Mat_Spitter_Cape",         (0.14, 0.14, 0.17, 1.0), roughness=0.86, specular=0.05)
    m_black     = kit.make_mat("Mat_Spitter_Black",        (0.09, 0.09, 0.11, 1.0), roughness=0.84, specular=0.06)
    m_jeans     = kit.make_mat("Mat_Spitter_Jeans",        (0.14, 0.21, 0.35, 1.0), roughness=0.90, specular=0.03)
    m_boot      = kit.make_mat("Mat_Spitter_Boot",         (0.13, 0.11, 0.12, 1.0), roughness=0.72, specular=0.12)
    m_silver    = kit.make_mat("Mat_Spitter_Silver",       (0.80, 0.83, 0.88, 1.0), roughness=0.35, metallic=0.72, specular=0.28)
    m_acid      = kit.make_mat("Mat_Spitter_AcidGlow",     (0.55, 0.96, 0.14, 1.0), roughness=0.20, specular=0.50)

    # --- HEAD ---
    head_parts = []
    HZ = 1.69
    head_parts.append(kit.create_anime_head_shell(HZ, m_face, m_skin, style="cute_round"))
    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.372, 0.015, HZ - 0.030), (0.048, 0.036, 0.074),
            m_skin, rot_euler=(0.06, side * -0.18, side * 0.16)
        ))

    # Shaved undercut grey buzzcut shell on sides/back
    head_parts.append(kit.create_hair_crown_shell(HZ, m_undercut, rx_base=0.408, ry_base=0.388, rz_base=0.388, cutout_z=0.52))

    # Brown punk headband across forehead
    hb_pts = []
    for k in range(24):
        ang = (2.0 * math.pi * k) / 24.0
        hb_pts.append((Vector((math.cos(ang) * 0.415, math.sin(ang) * 0.398, HZ + 0.17)),
                       Vector((math.cos(ang) * 0.018, math.sin(ang) * 0.018, 0.0)),
                       Vector((0.0, 0.0, 0.044))))
    hb_pts.append(hb_pts[0])
    head_parts.append(kit.create_loft_object("Headband", [
        (Vector((0.0, 0.0, HZ + 0.135)), Vector((0.422, 0.0, 0.0)), Vector((0.0, 0.402, 0.0))),
        (Vector((0.0, 0.0, HZ + 0.215)), Vector((0.412, 0.0, 0.0)), Vector((0.0, 0.392, 0.0))),
    ], m_headband, segments=28))

    # Cool sleek dark sunglasses (Ref 1 iconic punk shades — snug to face!)
    head_parts.append(kit.create_rounded_box("Shades_Bridge", (0.0, -0.396, HZ + 0.006), (0.10, 0.030, 0.030), m_glasses, bevel_width=0.008))
    for side in (-1.0, 1.0):
        head_parts.append(kit.create_rounded_box(f"Shades_Lens_{side}", (side * 0.135, -0.382, HZ + 0.004), (0.175, 0.052, 0.098), m_glasses, rot_euler=(0.0, 0.0, side * -0.14), bevel_width=0.016))
        head_parts.append(kit.create_rounded_box(f"Shades_Arm_{side}", (side * 0.300, -0.195, HZ + 0.014), (0.024, 0.30, 0.028), m_black, rot_euler=(0.0, 0.0, side * 0.18), bevel_width=0.005))

    # Signature Swept Crimson-Red Punk Mohawk / Side Fringe (exact Ref 1 silhouette!)
    mohawk_strands = [
        # Main front dramatic red sweep cascading down viewer-left (-X) side!
        [
            (Vector((-0.02, -0.02, HZ + 0.43)), 0.155, 0.072,  0.10),
            (Vector((-0.18, -0.22, HZ + 0.40)), 0.150, 0.064,  0.35),
            (Vector((-0.34, -0.26, HZ + 0.20)), 0.125, 0.050,  0.65),
            (Vector((-0.38, -0.20, HZ - 0.06)), 0.082, 0.030,  0.85),
            (Vector((-0.32, -0.16, HZ - 0.22)), 0.014, 0.008,  0.90),
        ],
        # Upper spiked red crest sweeping across top-left
        [
            (Vector(( 0.06, -0.06, HZ + 0.42)), 0.140, 0.066, -0.05),
            (Vector((-0.12, -0.26, HZ + 0.45)), 0.135, 0.058,  0.22),
            (Vector((-0.34, -0.32, HZ + 0.32)), 0.090, 0.036,  0.50),
            (Vector((-0.44, -0.30, HZ + 0.16)), 0.016, 0.008,  0.65),
        ],
        # High mohawk top ridge crest (flared spiky silhouette!)
        [
            (Vector(( 0.00, -0.14, HZ + 0.41)), 0.125, 0.068,  0.00),
            (Vector((-0.06,  0.04, HZ + 0.54)), 0.140, 0.074,  0.12),
            (Vector((-0.08,  0.26, HZ + 0.50)), 0.128, 0.064,  0.15),
            (Vector((-0.06,  0.44, HZ + 0.30)), 0.078, 0.036,  0.10),
            (Vector((-0.04,  0.50, HZ + 0.14)), 0.014, 0.008,  0.08),
        ],
        # Secondary side cheek wave
        [
            (Vector((-0.10,  0.08, HZ + 0.40)), 0.130, 0.056,  0.42),
            (Vector((-0.32,  0.04, HZ + 0.24)), 0.118, 0.046,  0.72),
            (Vector((-0.42,  0.02, HZ - 0.02)), 0.072, 0.028,  0.88),
            (Vector((-0.40,  0.02, HZ - 0.20)), 0.012, 0.006,  0.92),
        ],
        # Back-swept spiked tail locks
        [
            (Vector(( 0.04,  0.12, HZ + 0.40)), 0.120, 0.054, -0.18),
            (Vector(( 0.08,  0.34, HZ + 0.34)), 0.110, 0.048, -0.22),
            (Vector(( 0.08,  0.48, HZ + 0.12)), 0.065, 0.028, -0.20),
            (Vector(( 0.06,  0.52, HZ - 0.08)), 0.012, 0.007, -0.18),
        ],
        [
            (Vector((-0.12,  0.18, HZ + 0.36)), 0.120, 0.052,  0.30),
            (Vector((-0.24,  0.38, HZ + 0.24)), 0.105, 0.044,  0.40),
            (Vector((-0.28,  0.46, HZ - 0.02)), 0.060, 0.024,  0.45),
            (Vector((-0.26,  0.48, HZ - 0.20)), 0.012, 0.006,  0.45),
        ],
    ]
    for idx, ms in enumerate(mohawk_strands):
        head_parts.append(kit.create_ribbon_hair_strand(f"Mohawk_{idx}", ms, m_hair, v_start=0.06, v_end=0.96))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "Spitter_Head")

    # --- BODY ---
    body_parts = []
    neck_obj = kit.create_loft_object("Neck", [
        (Vector((0.0, 0.0, 1.24)), Vector((0.105, 0.0, 0.0)), Vector((0.0, 0.100, 0.0))),
        (Vector((0.0, 0.0, 1.46)), Vector((0.095, 0.0, 0.0)), Vector((0.0, 0.092, 0.0))),
    ], m_skin)

    # Athletic Olive-Tan Punk Tank Top
    body_parts.append(kit.create_loft_object("TankTop", [
        (Vector((0.0, 0.002, 0.78)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
        (Vector((0.0, -0.006, 1.00)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
        (Vector((0.0, -0.004, 1.22)), Vector((0.244, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, 0.000, 1.30)), Vector((0.185, 0.0, 0.0)), Vector((0.0, 0.150, 0.0))),
    ], m_tank))
    # Black wolf/skull emblem on chest!
    body_parts.append(kit.create_rounded_box("ChestEmblem_Head", (0.0, -0.192, 1.06), (0.120, 0.018, 0.105), m_black, bevel_width=0.014))
    body_parts.append(kit.create_ellipsoid("ChestEmblem_EarL", (-0.048, -0.192, 1.120), (0.026, 0.014, 0.032), m_black))
    body_parts.append(kit.create_ellipsoid("ChestEmblem_EarR", ( 0.048, -0.192, 1.120), (0.026, 0.014, 0.032), m_black))
    body_parts.append(kit.create_ellipsoid("ChestEmblem_EyeL", (-0.024, -0.200, 1.072), (0.014, 0.008, 0.012), m_acid))
    body_parts.append(kit.create_ellipsoid("ChestEmblem_EyeR", ( 0.024, -0.200, 1.072), (0.014, 0.008, 0.012), m_acid))

    # Spiked Punk Cape Collar + Metal Shoulder Studs (exact Ref 1!)
    body_parts.append(kit.create_loft_object("CapeCollar", [
        (Vector((0.0, 0.008, 1.24)), Vector((0.248, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, 0.004, 1.32)), Vector((0.195, 0.0, 0.0)), Vector((0.0, 0.155, 0.0))),
    ], m_cape))
    for side in (-1.0, 1.0):
        for k in range(3):
            sx = side * (0.17 + k * 0.052)
            sy = -0.05 + k * 0.035
            sz = 1.315 - k * 0.018
            body_parts.append(kit.create_loft_object(f"CollarSpike_{side}_{k}", [
                (Vector((sx, sy, sz)), Vector((0.020, 0.0, 0.0)), Vector((0.0, 0.020, 0.0))),
                (Vector((sx + side * 0.028, sy, sz + 0.068)), Vector((0.003, 0.0, 0.0)), Vector((0.0, 0.003, 0.0))),
            ], m_silver, segments=10))

    # Flowing Dark Raider Cape (will be weighted to Cape bone so it billows wildly when sprinting!)
    cape_rings = [
        (Vector((0.0, 0.165, 1.27)), Vector((0.245, 0.0, 0.0)), Vector((0.0, 0.026, 0.0))),
        (Vector((0.0, 0.220, 1.04)), Vector((0.305, 0.0, 0.0)), Vector((0.0, 0.026, 0.0))),
        (Vector((0.0, 0.275, 0.78)), Vector((0.355, 0.0, 0.0)), Vector((0.0, 0.024, 0.0))),
        (Vector((0.0, 0.335, 0.52)), Vector((0.395, 0.0, 0.0)), Vector((0.0, 0.020, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Raider_Cape", cape_rings, m_cape, segments=16))

    # Lean Athletic Bare Arms (dense multi-ring loft for clean dramatic Ref 3 sprint arm pump!) + Fingerless Studded Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        body_parts.append(kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.228, 0.0, AZ), (0.098, 0.094, 0.094), m_skin))
        body_parts.append(kit.create_loft_object(f"Arm_{side}", [
            (Vector((side * 0.20, 0.0, AZ)), Vector((0.0, 0.088, 0.0)), Vector((0.0, 0.0, 0.088 * side))),
            (Vector((side * 0.30, 0.0, AZ)), Vector((0.0, 0.082, 0.0)), Vector((0.0, 0.0, 0.082 * side))),
            (Vector((side * 0.42, 0.0, AZ)), Vector((0.0, 0.076, 0.0)), Vector((0.0, 0.0, 0.076 * side))),
            (Vector((side * 0.54, 0.0, AZ)), Vector((0.0, 0.074, 0.0)), Vector((0.0, 0.0, 0.074 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.070, 0.0)), Vector((0.0, 0.0, 0.070 * side))),
            (Vector((side * 0.74, 0.0, AZ)), Vector((0.0, 0.068, 0.0)), Vector((0.0, 0.0, 0.068 * side))),
        ], m_skin))
        # Studded punk wristband + fingerless glove + flat sprint hand!
        body_parts.append(kit.create_torus_ring(f"WristBand_{side}", (side * 0.68, 0.0, AZ), 0.076, 0.016, m_black, rot_euler=(0.0, math.pi * 0.5, 0.0)))
        body_parts.append(kit.create_rounded_box(f"Glove_{side}", (side * 0.78, -0.004, AZ), (0.125, 0.145, 0.090), m_black, bevel_width=0.020))
        body_parts.append(kit.create_rounded_box(f"FingersFlat_{side}", (side * 0.875, -0.004, AZ), (0.095, 0.120, 0.056), m_skin, bevel_width=0.018))

    # Belt + Silver Buckle + Dark Navy Jeans + Combat Boots
    body_parts.append(kit.create_loft_object("Pelvis_Jeans", [
        (Vector((0.0, 0.002, 0.58)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, 0.004, 0.72)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
        (Vector((0.0, 0.002, 0.81)), Vector((0.214, 0.0, 0.0)), Vector((0.0, 0.172, 0.0))),
    ], m_jeans))
    body_parts.append(kit.create_loft_object("Belt", [
        (Vector((0.0, 0.002, 0.75)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, 0.002, 0.81)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
    ], m_black))
    body_parts.append(kit.create_rounded_box("BeltBuckle", (0.0, -0.198, 0.78), (0.074, 0.024, 0.058), m_silver, bevel_width=0.008))

    for side in (1.0, -1.0):
        body_parts.append(kit.create_loft_object(f"Leg_{side}", [
            (Vector((side * 0.150, 0.002, 0.66)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.120, 0.0))),
            (Vector((side * 0.152, 0.000, 0.44)), Vector((0.106, 0.0, 0.0)), Vector((0.0, -0.108, 0.0))),
            (Vector((side * 0.154, 0.000, 0.26)), Vector((0.096, 0.0, 0.0)), Vector((0.0, -0.098, 0.0))),
        ], m_jeans))
        body_parts.append(kit.create_loft_object(f"Boot_{side}", [
            (Vector((side * 0.154,  0.000, 0.280)), Vector((0.104, 0.0, 0.0)), Vector((0.0, -0.106, 0.0))),
            (Vector((side * 0.154, -0.014, 0.140)), Vector((0.110, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.154, -0.058, 0.072)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.154, -0.064, 0.034)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.184, 0.0))),
        ], m_boot))
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", [
            (Vector((side * 0.154, -0.064, 0.038)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.190, 0.0))),
            (Vector((side * 0.154, -0.064, 0.003)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.186, 0.0))),
        ], m_black))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "Spitter_Body")

    # --- RIG (19 Bones: Standard 18 + Cape Bone for dynamic billowing!) ---
    arm_data = bpy.data.armatures.new("Chibi_Spitter_Rig")
    rig_obj = bpy.data.objects.new("Chibi_Spitter_Rig", arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')

    bone_defs = [
        ("Root",          (0.0, 0.0, 0.0),      (0.0, 0.0, 0.20),     None,            (0, 1, 0)),
        ("Hips",          (0.0, 0.0, 0.64),     (0.0, 0.0, 0.80),     "Root",          (0, 1, 0)),
        ("Spine",         (0.0, 0.0, 0.80),     (0.0, 0.0, 1.04),     "Hips",          (0, 1, 0)),
        ("Chest",         (0.0, 0.0, 1.04),     (0.0, 0.0, 1.26),     "Spine",         (0, 1, 0)),
        ("Neck",          (0.0, 0.0, 1.26),     (0.0, 0.0, 1.46),     "Chest",         (0, 1, 0)),
        ("Head",          (0.0, 0.0, 1.46),     (0.0, 0.0, 1.98),     "Neck",          (0, 1, 0)),
        ("Cape",          (0.0, 0.19, 1.24),    (0.0, 0.34, 0.56),    "Chest",         (0, 1, 0)),

        ("LeftShoulder",  (0.16, 0.0, 1.23),    (0.24, 0.0, 1.23),    "Chest",         (0, 0, 1)),
        ("LeftUpperArm",  (0.24, 0.0, 1.23),    (0.49, 0.0, 1.23),    "LeftShoulder",  (0, 0, 1)),
        ("LeftLowerArm",  (0.49, 0.0, 1.23),    (0.74, 0.0, 1.23),    "LeftUpperArm",  (0, 0, 1)),
        ("LeftHand",      (0.74, 0.0, 1.23),    (0.92, 0.0, 1.23),    "LeftLowerArm",  (0, 0, 1)),

        ("RightShoulder", (-0.16, 0.0, 1.23),   (-0.24, 0.0, 1.23),   "Chest",         (0, 0, 1)),
        ("RightUpperArm", (-0.24, 0.0, 1.23),   (-0.49, 0.0, 1.23),   "RightShoulder", (0, 0, 1)),
        ("RightLowerArm", (-0.49, 0.0, 1.23),   (-0.74, 0.0, 1.23),   "RightUpperArm", (0, 0, 1)),
        ("RightHand",     (-0.74, 0.0, 1.23),   (-0.92, 0.0, 1.23),   "RightLowerArm", (0, 0, 1)),

        ("LeftUpperLeg",  (0.15, 0.0, 0.64),    (0.15, 0.0, 0.35),    "Hips",          (0, 1, 0)),
        ("LeftLowerLeg",  (0.15, 0.0, 0.35),    (0.15, 0.0, 0.10),    "LeftUpperLeg",  (0, 1, 0)),
        ("LeftFoot",      (0.15, 0.0, 0.10),    (0.15, -0.22, 0.03),  "LeftLowerLeg",  (0, 0, 1)),

        ("RightUpperLeg", (-0.15, 0.0, 0.64),   (-0.15, 0.0, 0.35),   "Hips",          (0, 1, 0)),
        ("RightLowerLeg", (-0.15, 0.0, 0.35),   (-0.15, 0.0, 0.10),   "RightUpperLeg", (0, 1, 0)),
        ("RightFoot",     (-0.15, 0.0, 0.10),   (-0.15, -0.22, 0.03), "RightLowerLeg", (0, 0, 1)),
    ]

    edit_bones = {}
    for bname, h, t, pname, roll_vec in bone_defs:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in edit_bones:
            eb.parent = edit_bones[pname]
            eb.use_connect = False
        edit_bones[bname] = eb
    bpy.ops.object.mode_set(mode='OBJECT')

    def bind_spitter(mesh_obj, is_head=False):
        mesh_obj.parent = rig_obj
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = rig_obj
        mod.use_deform_preserve_volume = True
        vgroups = {bname: mesh_obj.vertex_groups.new(name=bname) for bname, _, _, _, _ in bone_defs}
        bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in bone_defs if bname not in ("Root", "Cape")]

        for v in mesh_obj.data.vertices:
            co = v.co
            if is_head:
                vgroups["Head"].add([v.index], 1.0, 'REPLACE')
                continue
            # Cape vertices (behind torso Y > 0.175 and Z < 1.24)
            if co.y > 0.172 and co.z < 1.25 and co.z > 0.46 and abs(co.x) < 0.44:
                t_cape = max(0.0, min(1.0, (1.25 - co.z) / 0.30))
                vgroups["Cape"].add([v.index], t_cape, 'REPLACE')
                if t_cape < 0.999:
                    vgroups["Chest"].add([v.index], 1.0 - t_cape, 'REPLACE')
                continue

            weights = {}
            for bname, h, t in bones_info:
                if bname.startswith("Left") and co.x < -0.01:
                    continue
                if bname.startswith("Right") and co.x > 0.01:
                    continue
                if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.21 or co.z < 1.05):
                    continue
                if "Shoulder" in bname and (abs(co.x) < 0.14 or co.z < 1.06):
                    continue
                if ("Leg" in bname or "Foot" in bname) and (co.z > 0.69 or abs(co.x) > 0.34):
                    continue
                if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.30 or co.z < 0.54):
                    continue
                if bname == "Head" and co.z < 1.44:
                    continue
                d = kit._point_to_segment_dist(co, h, t)
                sigma = 0.125 if ("Arm" in bname or "Leg" in bname) else 0.140
                w = math.exp(-(d * d) / (2.0 * sigma * sigma))
                if w > 1e-5:
                    weights[bname] = w
            if not weights:
                weights["Spine"] = 1.0
            top = sorted(weights.items(), key=lambda x: x[1], reverse=True)[:3]
            tot = sum(w for _, w in top)
            for bname, w in top:
                vgroups[bname].add([v.index], w / tot, 'REPLACE')

    bind_spitter(head_obj, is_head=True)
    bind_spitter(body_obj, is_head=False)

    for pb in rig_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}
    begin_frame, rot_deg, set_hips, ik_two, key_all, new_action = _make_pose_helpers(scene, rig_obj, rest_lengths)

    # 1. Idle (Frames 1..49 — Cocky punk crouch-ready forward stance matching Ref 1!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 50):
        begin_frame(f)
        ang = ((f - 1) / 48.0) * 2.0 * math.pi
        b = math.sin(ang)
        # Note: with bone roll (0,1,0), NEGATIVE pitch leans FORWARD (-Y), POSITIVE pitch leans BACK (+Y)
        set_hips((0.0, 0.0, -0.05 + 0.015 * b), pitch_deg=-8.0 - 2.0 * b, yaw_deg=4.0 * math.sin(ang * 0.5), roll_deg=0.0)
        rot_deg("Spine", -5.0, 0.0, 0.0)
        rot_deg("Chest", -4.0 + 2.0 * b, 0.0, 0.0)
        rot_deg("Head", 14.0 - 2.0 * b, -3.0 * b, 0.0)
        rot_deg("Cape", -14.0 - 4.0 * math.cos(ang), 0.0, 0.0)
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.36, -0.22, 0.86 + 0.02 * b), (0.65, 0.55, -0.1))
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.36, -0.28, 0.90 - 0.02 * b), (-0.65, 0.55, -0.1))
        ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.20, -0.06, 0.10), (0.22, -1.0, 0.25))
        ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.20, 0.06, 0.10), (-0.22, -1.0, 0.25))
        key_all(f, prev_q)

    # 2. Run & Walk (ICONIC REF 3 OFFICER EARL DRAMATIC SPRINTER RUN!! Forward lean + full outstretched karate-chop arm + high knees!)
    for clip_name, n_frames, intensity in (("Run", 17, 1.0), ("Walk", 21, 0.85)):
        act_move = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s = math.sin(ang)
            c = math.cos(ang)
            bounce = abs(c) * 0.10 * intensity

            # Aggressive FORWARD sprint lean (NEGATIVE X-pitch leans forward toward -Y! Exact Ref 3!)
            set_hips((0.0, -0.05, -0.07 + bounce), pitch_deg=-26.0 * intensity, yaw_deg=-20.0 * s * intensity, roll_deg=5.0 * s * intensity)
            rot_deg("Spine", -12.0 * intensity, 14.0 * s * intensity, -4.0 * s)
            rot_deg("Chest", -10.0 * intensity, 22.0 * s * intensity, -5.0 * s)
            rot_deg("Head", 38.0 * intensity, -16.0 * s * intensity, 4.0 * s)
            # Cape streaming horizontally backward in sprint wind!
            rot_deg("Cape", -42.0 * intensity - 8.0 * math.sin(ang * 2.0), 0.0, 8.0 * s)

            # DRAMATIC REF 3 ARM PUMP:
            # Forward arm stretches straight forward-upward (flat karate-chop hand reaching far ahead!)
            # Back arm pulls sharply back behind the hip with cocked elbow!
            l_fwd = max(0.0, s)
            l_bck = max(0.0, -s)
            r_fwd = max(0.0, -s)
            r_bck = max(0.0, s)

            # Left arm target
            lx = 0.26 + 0.14 * l_bck - 0.06 * l_fwd
            ly = -0.82 * l_fwd + 0.48 * l_bck - 0.16
            lz = 1.08 + 0.34 * l_fwd + 0.14 * l_bck
            l_pole = (0.30, 0.30, -1.0) if l_fwd >= l_bck else (0.45, 0.90, 0.70)
            ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (lx, ly, lz), l_pole, (0, 0, 0))

            # Right arm target
            rx = -0.26 - 0.14 * r_bck + 0.06 * r_fwd
            ry = -0.82 * r_fwd + 0.48 * r_bck - 0.16
            rz = 1.08 + 0.34 * r_fwd + 0.14 * r_bck
            r_pole = (-0.30, 0.30, -1.0) if r_fwd >= r_bck else (-0.45, 0.90, 0.70)
            ik_two("RightUpperArm", "RightLowerArm", "RightHand", (rx, ry, rz), r_pole, (0, 0, 0))

            # HIGH-KNEE EXPLOSIVE SPRINTER LEGS (opposite of arms!)
            l_leg_y = 0.38 * s * intensity
            l_leg_z = 0.10 + max(0.0, -c) * 0.28 * intensity + max(0.0, -s) * 0.10
            r_leg_y = -0.38 * s * intensity
            r_leg_z = 0.10 + max(0.0, c) * 0.28 * intensity + max(0.0, s) * 0.10

            ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.16, l_leg_y, l_leg_z), (0.10, -1.0, 0.35), (32.0 * s, 0, 0))
            ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.16, r_leg_y, r_leg_z), (-0.10, -1.0, 0.35), (-32.0 * s, 0, 0))
            key_all(f, prev_q)
        if clip_name == "Run":
            act_run = act_move

    # 3. Slash1 (Frames 1..19 — Dramatic Acid Spit / Point-Forward Blast matching Ref 1!)
    act_slash1 = new_action("Slash1")
    prev_q = {}
    for f in range(1, 20):
        begin_frame(f)
        u = (f - 1) / 18.0
        if u < 0.38:
            p = u / 0.38
            sw = -p
        else:
            p = (u - 0.38) / 0.62
            sw = math.sin(p * math.pi)
        set_hips((0.0, -0.10 * sw, -0.06), pitch_deg=-12.0 - 18.0 * sw, yaw_deg=18.0 * sw, roll_deg=0.0)
        rot_deg("Spine", -10.0 * sw, -10.0 * sw, 0.0)
        rot_deg("Chest", -14.0 * sw, -14.0 * sw, 0.0)
        rot_deg("Head", 20.0 + 14.0 * sw, 8.0 * sw, 0.0)
        rot_deg("Cape", -24.0 - 20.0 * max(0.0, sw), 0.0, 0.0)
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.18, -0.76 * max(0.0, sw) - 0.22, 1.08 + 0.08 * sw), (-0.5, 0.4, -0.6))
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.36, 0.14 * max(0.0, sw) - 0.14, 0.94), (0.7, 0.5, -0.2))
        ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.19, 0.10, 0.10), (0.18, -1.0, 0.3))
        ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.19, -0.14, 0.10), (-0.18, -1.0, 0.3))
        key_all(f, prev_q)

    # 4. Death (Frames 1..27 — Dramatic backward fall)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 28):
        begin_frame(f)
        u = (f - 1) / 26.0
        s = u * u * (3.0 - 2.0 * u)
        set_hips((0.0, 0.34 * s, -0.44 * s), pitch_deg=86.0 * s, yaw_deg=-12.0 * s, roll_deg=6.0 * s)
        rot_deg("Head", -18.0 * s, -14.0 * s, 0.0)
        rot_deg("Cape", -55.0 * s, 0.0, 0.0)
        rot_deg("LeftUpperArm", 0.0, -35.0 * s, 40.0 * s)
        rot_deg("RightUpperArm", 0.0, 35.0 * s, -40.0 * s)
        key_all(f, prev_q)

    # Export FBX & Blend at Idle frame 1
    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_fbx_and_blend(rig_obj, head_obj, body_obj, "Chibi_ZombieSpitter")

    # Render Front, Back, and Mid-Sprint (Ref 3 Pose!) Previews
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-0.75, -4.35, 1.35))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.15)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieSpitter_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ZOMBIE_DIR, "Chibi_ZombieSpitter_Preview.png"))

    cam_obj.location = Vector((1.55, 3.95, 1.45))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.15)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_b = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieSpitter_Preview_Back.png")
    scene.render.filepath = tmp_b
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_b, os.path.join(ZOMBIE_DIR, "Chibi_ZombieSpitter_Preview_Back.png"))

    # Mid-Sprint Pose (Frame 5 of Run — peak Officer Earl Ref 3 arm-drive + high knee!)
    rig_obj.animation_data.action = act_run
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_run.slots) > 0:
        rig_obj.animation_data.action_slot = act_run.slots[0]
    scene.frame_set(5)
    bpy.context.view_layer.update()
    cam_obj.location = Vector((-2.65, -3.55, 1.38))
    cam_obj.rotation_euler = (Vector((0.0, -0.08, 1.05)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_r = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieSpitter_Preview_Run.png")
    scene.render.filepath = tmp_r
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_r, os.path.join(ZOMBIE_DIR, "Chibi_ZombieSpitter_Preview_Run.png"))


# ==============================================================================
#  2. CHIBI ZOMBIE TANK — Spiked-Hood Tattooed Gorilla Bruiser (Ref 2 + Gorilla Run!)
# ==============================================================================

def generate_tank_face_texture():
    W, H = 1024, 1024
    aa = 0.0022
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    # Dark-skinned undead zombie bruiser tone (deep charcoal-brown with ashen necrotic undertone!)
    SKIN_BASE   = (0.22, 0.19, 0.18)
    SKIN_SHADOW = (0.12, 0.10, 0.13)
    BRUISE_COL  = (0.15, 0.21, 0.16)
    BEARD_COL   = (0.05, 0.06, 0.08)
    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.68, 0.92, V) * 0.30, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.06, V) * 0.28, BRUISE_COL)

    # Undead stitched scar across right cheek & necrotic bruise
    scar = [(0.30, 0.43), (0.39, 0.38)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, scar, [0.006, 0.006]))) * 0.85, (0.44, 0.12, 0.16))

    # Heavy fierce furrowed brows above sunglasses
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.152
        cy = 0.485
        d_s = np.hypot((U - cx) / 0.095, (V - cy) / 0.050)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_s)) * 0.55, (0.08, 0.07, 0.10))
        brow = [(cx + side * (-0.095), cy + 0.052), (cx + side * (0.005), cy + 0.082), (cx + side * (0.092), cy + 0.092)]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, brow, [0.012, 0.018, 0.008])), BEARD_COL)

    # Chunky nose bridge
    nose = [(0.482, 0.44), (0.474, 0.39), (0.526, 0.39), (0.518, 0.44)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, kit.polyline_sdf(U, V, nose, [0.008, 0.014, 0.014, 0.008]))) * 0.55, SKIN_SHADOW)

    # Iconic heavy thick dark Goatee & Mustache (exact Ref 2!)
    stache_pts = [(0.365, 0.285), (0.415, 0.335), (0.500, 0.348), (0.585, 0.335), (0.635, 0.285)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, stache_pts, [0.018, 0.026, 0.028, 0.026, 0.018])), BEARD_COL)
    chin_pts = [(0.382, 0.270), (0.430, 0.205), (0.500, 0.188), (0.570, 0.205), (0.618, 0.270)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, chin_pts, [0.020, 0.028, 0.032, 0.028, 0.020])), BEARD_COL)

    # Fierce snarling yellowed undead gritted teeth inside goatee!
    mouth_pts = [(0.415, 0.285), (0.500, 0.294), (0.585, 0.285)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, mouth_pts, [0.015, 0.020, 0.015])), (0.88, 0.85, 0.64))
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, mouth_pts, [0.004, 0.004, 0.004])), (0.12, 0.07, 0.07))

    path = os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieTank_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "Chibi_ZombieTank_Face.png")


def generate_tank_tattoo_texture():
    """512x512 Glowing Golden-Yellow Tribal Tattoo Sleeve over dark undead zombie skin (exact Ref 2!)"""
    W, H = 512, 512
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN = (0.22, 0.19, 0.18)
    GOLD = (1.00, 0.86, 0.22)
    DARK = (0.10, 0.09, 0.11)
    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN

    # Procedural bold tribal interlocking waves & runes wrapping cleanly around the arms!
    p1 = np.sin(U * 2.0 * math.pi * 5.0 + np.sin(V * 2.0 * math.pi * 3.0) * 2.4)
    p2 = np.cos(V * 2.0 * math.pi * 6.0 + np.cos(U * 2.0 * math.pi * 4.0) * 2.2)
    p3 = np.sin((U + V) * 2.0 * math.pi * 4.0)
    pattern = np.abs(p1 * p2) + 0.35 * np.abs(p3)
    band = (1.0 - kit.smoothstep(0.10, 0.24, np.abs(pattern - 0.42)))
    outline = (1.0 - kit.smoothstep(0.18, 0.32, np.abs(pattern - 0.42))) * (1.0 - band)

    kit.alpha_blend(canvas, outline * 0.55, DARK)
    kit.alpha_blend(canvas, band * 0.96, GOLD)

    path = os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieTank_Tattoo.png")
    return kit.save_rgba_array_to_png(canvas, path, "Chibi_ZombieTank_Tattoo.png")


def build_zombie_tank():
    scene = kit.reset_scene()
    img_face = generate_tank_face_texture()
    img_tattoo = generate_tank_tattoo_texture()

    m_face      = kit.make_mat("Mat_Tank_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin      = kit.make_mat("Mat_Tank_Skin",         (0.22, 0.19, 0.18, 1.0), roughness=0.86, specular=0.05)
    m_tattoo    = kit.make_mat("Mat_Tank_TattooArm",    (1.0, 1.0, 1.0, 1.0), roughness=0.80, specular=0.08, image=img_tattoo)
    m_hood      = kit.make_mat("Mat_Tank_Hood",         (0.08, 0.13, 0.21, 1.0), roughness=0.88, specular=0.05)
    m_redspike  = kit.make_mat("Mat_Tank_RedSpike",     (0.86, 0.16, 0.22, 1.0), roughness=0.45, specular=0.20)
    m_glasses   = kit.make_mat("Mat_Tank_Glasses",      (0.04, 0.05, 0.07, 1.0), roughness=0.12, specular=0.88)
    m_vest      = kit.make_mat("Mat_Tank_Vest",         (0.10, 0.16, 0.26, 1.0), roughness=0.80, specular=0.08)
    m_jeans     = kit.make_mat("Mat_Tank_Jeans",        (0.11, 0.17, 0.29, 1.0), roughness=0.90, specular=0.03)
    m_magenta   = kit.make_mat("Mat_Tank_Magenta",      (0.68, 0.18, 0.56, 1.0), roughness=0.72, specular=0.10)
    m_silver    = kit.make_mat("Mat_Tank_Silver",       (0.82, 0.85, 0.90, 1.0), roughness=0.35, metallic=0.75, specular=0.28)
    m_black     = kit.make_mat("Mat_Tank_Black",        (0.08, 0.08, 0.10, 1.0), roughness=0.85, specular=0.06)

    # --- HEAD (Nestled slightly forward onto hunched bruiser shoulders!) ---
    head_parts = []
    HZ = 1.66
    HY = -0.10  # Forward-jutting hunched gorilla bruiser head!
    head_shell = kit.create_anime_head_shell(HZ, m_face, m_skin, style="cute_round")
    head_shell.location.y += HY
    bpy.context.view_layer.objects.active = head_shell
    bpy.ops.object.transform_apply(location=True)
    head_parts.append(head_shell)

    # Dark Navy-Charcoal Spiked Hood / Heavy Metal Hair Curtain (exact Ref 2!)
    hood_crown = kit.create_hair_crown_shell(HZ, m_hood, rx_base=0.435, ry_base=0.415, rz_base=0.415, cutout_z=0.44)
    hood_crown.location.y += HY
    bpy.context.view_layer.objects.active = hood_crown
    bpy.ops.object.transform_apply(location=True)
    head_parts.append(hood_crown)

    # Smooth rounded-crown framing side hood/hair curtains (zero top bumps!)
    for side in (-1.0, 1.0):
        for j, y_off in enumerate((-0.14, 0.04, 0.22)):
            curtain = [
                (Vector((side * 0.16, HY + y_off * 0.45, HZ + 0.39)), 0.060, 0.028, side * (-0.95 + j * 0.28)),
                (Vector((side * 0.41, HY + y_off * 0.95, HZ + 0.18)), 0.108, 0.040, side * (-0.95 + j * 0.28)),
                (Vector((side * 0.43, HY + y_off,        HZ - 0.16)), 0.112, 0.040, side * (-0.95 + j * 0.28)),
                (Vector((side * 0.39, HY + y_off,        HZ - 0.38)), 0.092, 0.026, side * (-0.95 + j * 0.28)),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(f"TankHoodSide_{side}_{j}", curtain, m_hood))

    # 5 Bold Crimson-Red Spikes poking prominently out of the hood (exact Ref 2!)
    spike_specs = [
        (-0.24, HY - 0.02, HZ + 0.34, -0.45, 0.0,  0.65),
        ( 0.24, HY - 0.02, HZ + 0.34,  0.45, 0.0,  0.65),
        (-0.38, HY + 0.04, HZ + 0.18, -0.78, 0.0,  0.34),
        ( 0.38, HY + 0.04, HZ + 0.18,  0.78, 0.0,  0.34),
        ( 0.00, HY + 0.06, HZ + 0.40,  0.00, 0.06, 0.78),
    ]
    for idx, (sx, sy, sz, dx, dy, dz) in enumerate(spike_specs):
        tip = Vector((sx + dx * 0.30, sy + dy * 0.30, sz + dz * 0.30))
        head_parts.append(kit.create_loft_object(f"HoodSpike_{idx}", [
            (Vector((sx, sy, sz)), Vector((0.056, 0.0, 0.0)), Vector((0.0, 0.056, 0.0))),
            (tip, Vector((0.006, 0.0, 0.0)), Vector((0.0, 0.006, 0.0))),
        ], m_redspike, segments=12))

    # Cool dark rectangular sunglasses!
    head_parts.append(kit.create_rounded_box("Tank_ShadesBridge", (0.0, HY - 0.398, HZ + 0.010), (0.12, 0.032, 0.032), m_glasses, bevel_width=0.008))
    for side in (-1.0, 1.0):
        head_parts.append(kit.create_rounded_box(f"Tank_Lens_{side}", (side * 0.145, HY - 0.385, HZ + 0.008), (0.185, 0.056, 0.108), m_glasses, bevel_width=0.015))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "Tank_Head")

    # --- BODY (HUNCHED BACK + MASSIVE MUSCULAR CHEST + OVERSIZED GORILLA TATTOO ARMS!) ---
    body_parts = []
    # Thick muscular bull neck tilted forward into the hunch
    neck_obj = kit.create_loft_object("Tank_Neck", [
        (Vector((0.0, -0.02, 1.24)), Vector((0.165, 0.0, 0.0)), Vector((0.0, 0.155, 0.0))),
        (Vector((0.0, -0.09, 1.46)), Vector((0.135, 0.0, 0.0)), Vector((0.0, 0.128, 0.0))),
    ], m_skin)

    # Massive Hunched-Forward Muscular Torso (Back arches way out +Y at upper back -> true "หลังโก่งๆ"!)
    torso_rings = [
        (Vector((0.0,  0.02, 0.72)), Vector((0.270, 0.0, 0.0)), Vector((0.0, 0.210, 0.0))),
        (Vector((0.0,  0.01, 0.90)), Vector((0.305, 0.0, 0.0)), Vector((0.0, 0.235, 0.0))),
        (Vector((0.0,  0.04, 1.12)), Vector((0.385, 0.0, 0.0)), Vector((0.0, 0.285, 0.0))),  # Broad hunched upper back!
        (Vector((0.0,  0.08, 1.30)), Vector((0.365, 0.0, 0.0)), Vector((0.0, 0.270, 0.0))),  # Heavy gorilla trapezius hump!
        (Vector((0.0,  0.02, 1.40)), Vector((0.230, 0.0, 0.0)), Vector((0.0, 0.195, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Tank_HunchedTorso", torso_rings, m_skin, segments=24))
    # Sculpted muscular 6-pack abs & thick pecs down the open front!
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_ellipsoid(f"Pec_{side}", (side * 0.115, -0.228, 1.14), (0.112, 0.055, 0.082), m_skin))
        for row, az in enumerate((1.01, 0.91, 0.82)):
            body_parts.append(kit.create_ellipsoid(f"Ab_{side}_{row}", (side * 0.072, -0.226 + row * 0.008, az), (0.062, 0.036, 0.042), m_skin))

    # Extra Gorilla Upper-Back Hump (vest-covered hunched trapezius arch!)
    body_parts.append(kit.create_ellipsoid("Gorilla_BackHump", (0.0, 0.165, 1.24), (0.340, 0.205, 0.215), m_vest))

    # Open Dark-Blue Spiked Leather Vest (wraps sides & hunched back, leaves muscular chest/abs open in front!)
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(f"VestFront_{side}", (side * 0.235, -0.155, 1.06), (0.175, 0.140, 0.56), m_vest, rot_euler=(0.08, 0.0, side * -0.14), bevel_width=0.032))
        body_parts.append(kit.create_rounded_box(f"VestSide_{side}", (side * 0.340, 0.020, 1.08), (0.115, 0.320, 0.52), m_vest, bevel_width=0.030))
        # Silver shoulder spikes & lapel studs!
        for k in range(3):
            body_parts.append(kit.create_loft_object(f"VestSpike_{side}_{k}", [
                (Vector((side * (0.28 + k * 0.065), -0.04 + k * 0.04, 1.35 - k * 0.02)), Vector((0.026, 0.0, 0.0)), Vector((0.0, 0.026, 0.0))),
                (Vector((side * (0.31 + k * 0.085), -0.04 + k * 0.04, 1.44 - k * 0.02)), Vector((0.004, 0.0, 0.0)), Vector((0.0, 0.004, 0.0))),
            ], m_silver, segments=10))
            body_parts.append(kit.create_ellipsoid(f"VestStud_{side}_{k}", (side * 0.155, -0.238, 1.22 - k * 0.095), (0.018, 0.014, 0.018), m_silver))

    # Silver Skull Pendant Necklace on bare chest!
    body_parts.append(kit.create_torus_ring("NecklaceChain", (0.0, -0.135, 1.26), 0.145, 0.012, m_silver, rot_euler=(0.55, 0.0, 0.0)))
    body_parts.append(kit.create_ellipsoid("SkullPendant", (0.0, -0.258, 1.13), (0.056, 0.034, 0.064), m_silver))
    body_parts.append(kit.create_ellipsoid("SkullEyeL", (-0.020, -0.286, 1.140), (0.012, 0.010, 0.012), m_black))
    body_parts.append(kit.create_ellipsoid("SkullEyeR", ( 0.020, -0.286, 1.140), (0.012, 0.010, 0.012), m_black))

    # Giant muscular tattooed deltoid shoulder (with spherical UVs so golden tattoos cover the shoulders too!)
    AZ = 1.22
    for side in (1.0, -1.0):
        sh = kit.create_ellipsoid(f"GorillaDelt_{side}", (side * 0.365, -0.02, AZ + 0.02), (0.195, 0.185, 0.185), m_tattoo)
        bm_s = bmesh.new()
        bm_s.from_mesh(sh.data)
        uv_s = bm_s.loops.layers.uv.verify()
        for face in bm_s.faces:
            for loop in face.loops:
                co = loop.vert.co
                u = (math.atan2(co.z - (AZ + 0.02), co.y + 0.02) / (2.0 * math.pi)) + 0.5
                v = max(0.0, min(1.0, (abs(co.x) - 0.18) / 0.42))
                loop[uv_s].uv = (u, v)
        bm_s.to_mesh(sh.data)
        bm_s.free()
        body_parts.append(sh)
        # Huge bulging muscular bicep + giant gorilla forearm (with clean cylindrical UVs for golden tribal tattoos!)
        arm_rings = [
            (Vector((side * 0.34, -0.02, AZ)), Vector((0.0, 0.165, 0.0)), Vector((0.0, 0.0, 0.165 * side))),
            (Vector((side * 0.50, -0.02, AZ)), Vector((0.0, 0.178, 0.0)), Vector((0.0, 0.0, 0.178 * side))),
            (Vector((side * 0.66, -0.02, AZ)), Vector((0.0, 0.155, 0.0)), Vector((0.0, 0.0, 0.155 * side))),
            (Vector((side * 0.84, -0.02, AZ)), Vector((0.0, 0.172, 0.0)), Vector((0.0, 0.0, 0.172 * side))),
            (Vector((side * 1.02, -0.02, AZ)), Vector((0.0, 0.145, 0.0)), Vector((0.0, 0.0, 0.145 * side))),
        ]
        arm_obj = kit.create_loft_object(f"GorillaArm_{side}", arm_rings, m_tattoo, segments=20)
        # Apply cylindrical UV unwrap along X axis so golden tribal tattoos wrap cleanly!
        bm = bmesh.new()
        bm.from_mesh(arm_obj.data)
        uv_layer = bm.loops.layers.uv.verify()
        for face in bm.faces:
            for loop in face.loops:
                co = loop.vert.co
                u = (math.atan2(co.z - AZ, co.y + 0.02) / (2.0 * math.pi)) + 0.5
                v = max(0.0, min(1.0, (abs(co.x) - 0.32) / 0.72))
                loop[uv_layer].uv = (u, v)
        bm.to_mesh(arm_obj.data)
        bm.free()
        body_parts.append(arm_obj)

        # Massive Fingerless Black Knuckle-Brawler Glove + Giant Clenched Gorilla Fist!
        body_parts.append(kit.create_rounded_box(f"GorillaGlove_{side}", (side * 1.09, -0.02, AZ), (0.175, 0.215, 0.195), m_black, bevel_width=0.042))
        body_parts.append(kit.create_rounded_box(f"GorillaFist_{side}", (side * 1.21, -0.02, AZ - 0.01), (0.165, 0.210, 0.185), m_skin, bevel_width=0.048))
        # Knuckle ridge detail
        for k_i in (-1, 0, 1):
            body_parts.append(kit.create_ellipsoid(f"Knuckle_{side}_{k_i}", (side * 1.28, -0.02 + k_i * 0.055, AZ + 0.045), (0.032, 0.030, 0.034), m_skin))

    # Magenta-Purple Skull Belt + Ripped Dark Jeans + Stocky Gorilla Legs + Purple Boots!
    body_parts.append(kit.create_loft_object("Tank_Pelvis", [
        (Vector((0.0, 0.01, 0.52)), Vector((0.285, 0.0, 0.0)), Vector((0.0, 0.225, 0.0))),
        (Vector((0.0, 0.01, 0.68)), Vector((0.280, 0.0, 0.0)), Vector((0.0, 0.220, 0.0))),
        (Vector((0.0, 0.01, 0.77)), Vector((0.272, 0.0, 0.0)), Vector((0.0, 0.215, 0.0))),
    ], m_jeans))
    body_parts.append(kit.create_loft_object("MagentaBelt", [
        (Vector((0.0, 0.01, 0.69)), Vector((0.295, 0.0, 0.0)), Vector((0.0, 0.236, 0.0))),
        (Vector((0.0, 0.01, 0.77)), Vector((0.292, 0.0, 0.0)), Vector((0.0, 0.232, 0.0))),
    ], m_magenta))
    body_parts.append(kit.create_rounded_box("BeltSkull", (0.0, -0.242, 0.73), (0.095, 0.032, 0.082), m_magenta, bevel_width=0.016))
    body_parts.append(kit.create_ellipsoid("BeltSkullDot", (0.0, -0.258, 0.73), (0.045, 0.014, 0.042), m_silver))

    for side in (1.0, -1.0):
        # Stocky muscular gorilla legs in ripped jeans (exposed skin knee rip!)
        body_parts.append(kit.create_loft_object(f"TankLegTop_{side}", [
            (Vector((side * 0.205, 0.01, 0.62)), Vector((0.148, 0.0, 0.0)), Vector((0.0, -0.152, 0.0))),
            (Vector((side * 0.210, 0.00, 0.44)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.136, 0.0))),
        ], m_jeans))
        # Exposed ripped-jean skin band at knees (Ref 2 detail!)
        body_parts.append(kit.create_loft_object(f"TankKneeRip_{side}", [
            (Vector((side * 0.210, -0.01, 0.45)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.130, 0.0))),
            (Vector((side * 0.212, -0.01, 0.36)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
        ], m_skin))
        body_parts.append(kit.create_loft_object(f"TankLegBot_{side}", [
            (Vector((side * 0.212, 0.00, 0.37)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.214, 0.00, 0.22)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.116, 0.0))),
        ], m_jeans))
        # Purple/Magenta heavy stomper boots!
        body_parts.append(kit.create_loft_object(f"TankBoot_{side}", [
            (Vector((side * 0.214,  0.000, 0.240)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.214, -0.020, 0.130)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.150, 0.0))),
            (Vector((side * 0.214, -0.064, 0.070)), Vector((0.146, 0.0, 0.0)), Vector((0.0, -0.204, 0.0))),
            (Vector((side * 0.214, -0.070, 0.032)), Vector((0.150, 0.0, 0.0)), Vector((0.0, -0.210, 0.0))),
        ], m_magenta))
        body_parts.append(kit.create_loft_object(f"TankSole_{side}", [
            (Vector((side * 0.214, -0.070, 0.036)), Vector((0.156, 0.0, 0.0)), Vector((0.0, -0.216, 0.0))),
            (Vector((side * 0.214, -0.070, 0.003)), Vector((0.152, 0.0, 0.0)), Vector((0.0, -0.212, 0.0))),
        ], m_black))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "Tank_Body")

    # --- RIG (Proportioned for Long Gorilla Arms & Broad Hunched Shoulders!) ---
    arm_data = bpy.data.armatures.new("Chibi_Tank_Rig")
    rig_obj = bpy.data.objects.new("Chibi_Tank_Rig", arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')

    bone_defs = [
        ("Root",          (0.0, 0.0, 0.0),      (0.0, 0.0, 0.22),     None,            (0, 1, 0)),
        ("Hips",          (0.0, 0.01, 0.62),    (0.0, 0.01, 0.80),    "Root",          (0, 1, 0)),
        ("Spine",         (0.0, 0.01, 0.80),    (0.0, 0.03, 1.06),    "Hips",          (0, 1, 0)),
        ("Chest",         (0.0, 0.03, 1.06),    (0.0, 0.02, 1.32),    "Spine",         (0, 1, 0)),
        ("Neck",          (0.0, -0.02, 1.32),   (0.0, -0.09, 1.48),   "Chest",         (0, 1, 0)),
        ("Head",          (0.0, -0.09, 1.48),   (0.0, -0.10, 1.98),   "Neck",          (0, 1, 0)),

        ("LeftShoulder",  (0.22, -0.02, 1.22),  (0.36, -0.02, 1.22),  "Chest",         (0, 0, 1)),
        ("LeftUpperArm",  (0.36, -0.02, 1.22),  (0.68, -0.02, 1.22),  "LeftShoulder",  (0, 0, 1)),
        ("LeftLowerArm",  (0.68, -0.02, 1.22),  (1.04, -0.02, 1.22),  "LeftUpperArm",  (0, 0, 1)),
        ("LeftHand",      (1.04, -0.02, 1.22),  (1.28, -0.02, 1.22),  "LeftLowerArm",  (0, 0, 1)),

        ("RightShoulder", (-0.22, -0.02, 1.22), (-0.36, -0.02, 1.22), "Chest",         (0, 0, 1)),
        ("RightUpperArm", (-0.36, -0.02, 1.22), (-0.68, -0.02, 1.22), "RightShoulder", (0, 0, 1)),
        ("RightLowerArm", (-0.68, -0.02, 1.22), (-1.04, -0.02, 1.22), "RightUpperArm", (0, 0, 1)),
        ("RightHand",     (-1.04, -0.02, 1.22), (-1.28, -0.02, 1.22), "RightLowerArm", (0, 0, 1)),

        ("LeftUpperLeg",  (0.205, 0.0, 0.62),   (0.212, 0.0, 0.35),   "Hips",          (0, 1, 0)),
        ("LeftLowerLeg",  (0.212, 0.0, 0.35),   (0.214, 0.0, 0.10),   "LeftUpperLeg",  (0, 1, 0)),
        ("LeftFoot",      (0.214, 0.0, 0.10),   (0.214, -0.24, 0.03), "LeftLowerLeg",  (0, 0, 1)),

        ("RightUpperLeg", (-0.205, 0.0, 0.62),  (-0.212, 0.0, 0.35),  "Hips",          (0, 1, 0)),
        ("RightLowerLeg", (-0.212, 0.0, 0.35),  (-0.214, 0.0, 0.10),  "RightUpperLeg", (0, 1, 0)),
        ("RightFoot",     (-0.214, 0.0, 0.10),  (-0.214, -0.24, 0.03),"RightLowerLeg", (0, 0, 1)),
    ]

    edit_bones = {}
    for bname, h, t, pname, roll_vec in bone_defs:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in edit_bones:
            eb.parent = edit_bones[pname]
            eb.use_connect = False
        edit_bones[bname] = eb
    bpy.ops.object.mode_set(mode='OBJECT')

    def bind_tank(mesh_obj, is_head=False):
        mesh_obj.parent = rig_obj
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = rig_obj
        mod.use_deform_preserve_volume = True
        vgroups = {bname: mesh_obj.vertex_groups.new(name=bname) for bname, _, _, _, _ in bone_defs}
        bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in bone_defs if bname != "Root"]

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
                if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.33 or co.z < 0.96):
                    continue
                if "Shoulder" in bname and (abs(co.x) < 0.24 or co.z < 1.00):
                    continue
                if ("Leg" in bname or "Foot" in bname) and (co.z > 0.68 or abs(co.x) > 0.46):
                    continue
                if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.44 or co.z < 0.50):
                    continue
                if bname == "Head" and co.z < 1.46:
                    continue
                d = kit._point_to_segment_dist(co, h, t)
                sigma = 0.165 if ("Arm" in bname or "Shoulder" in bname or "Leg" in bname) else 0.170
                w = math.exp(-(d * d) / (2.0 * sigma * sigma))
                if w > 1e-5:
                    weights[bname] = w
            if not weights:
                weights["Spine"] = 1.0
            top = sorted(weights.items(), key=lambda x: x[1], reverse=True)[:3]
            tot = sum(w for _, w in top)
            for bname, w in top:
                vgroups[bname].add([v.index], w / tot, 'REPLACE')

    bind_tank(head_obj, is_head=True)
    bind_tank(body_obj, is_head=False)

    for pb in rig_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}
    begin_frame, rot_deg, set_hips, ik_two, key_all, new_action = _make_pose_helpers(scene, rig_obj, rest_lengths)

    # 1. Idle (Frames 1..49 — Heavy HUNCHED-FORWARD gorilla stance, giant fists hanging low near knees!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 50):
        begin_frame(f)
        ang = ((f - 1) / 48.0) * 2.0 * math.pi
        b = math.sin(ang)
        # NEGATIVE X-pitch hunches torso FORWARD ("หลังโก่งๆ"), POSITIVE Head pitch looks fiercely straight ahead!
        set_hips((0.0, 0.02, -0.06 + 0.018 * b), pitch_deg=-14.0 - 3.0 * b, yaw_deg=2.0 * math.sin(ang * 0.5), roll_deg=1.5 * math.cos(ang))
        rot_deg("Spine", -14.0 - 2.0 * b, 0.0, 0.0)
        rot_deg("Chest", -12.0 + 2.5 * b, 0.0, 0.0)
        rot_deg("Head", 34.0 - 3.0 * b, 4.0 * math.sin(ang), 0.0)
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.54, -0.34, 0.34 + 0.03 * b), (0.85, 0.45, 0.2))
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.54, -0.34, 0.34 + 0.03 * b), (-0.85, 0.45, 0.2))
        ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.24, 0.04, 0.10), (0.30, -1.0, 0.25))
        ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.24, 0.04, 0.10), (-0.30, -1.0, 0.25))
        key_all(f, prev_q)

    # 2. Run, Walk, & Charge — PRIMAL GORILLA KNUCKLE-RUN GALLOP! ("ท่าวิ่งแบบกอลิล่า" — deep forward hunch + ground knuckle slams!)
    for clip_name, n_frames, speed_mult in (("Run", 19, 1.0), ("Walk", 25, 0.78), ("Charge", 15, 1.15)):
        act_g = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s = math.sin(ang)
            c = math.cos(ang)

            # Deep hunched-forward gorilla bounding rhythm (NEGATIVE X-pitch = forward hunch!)
            bound_z = max(0.0, math.sin(ang * 2.0)) * 0.12 * speed_mult
            hunch_pitch = 26.0 + 7.0 * math.sin(ang) * speed_mult
            set_hips((0.06 * s, -0.06, -0.18 + bound_z), pitch_deg=-hunch_pitch, yaw_deg=-10.0 * s, roll_deg=8.0 * s)
            rot_deg("Spine", -18.0, 8.0 * s, -4.0 * s)
            rot_deg("Chest", -16.0, 12.0 * s, -5.0 * s)
            # Head tilts strongly UP (+X) so Tank glares straight forward at the player while knuckle-running!
            rot_deg("Head", hunch_pitch + 28.0, -10.0 * s, 4.0 * c)

            # ALTERNATING GORILLA KNUCKLE-SLAM ARMS (giant fists reach far forward & plant onto the ground z=0.14!)
            l_plant = max(0.0, -c)
            l_lift  = max(0.0, c)
            l_hand_y = -0.84 + 0.42 * s * speed_mult
            l_hand_z = 0.14 + 0.30 * l_lift * speed_mult
            ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.50, l_hand_y, l_hand_z), (0.85, 0.55, 0.35), (-25.0 * l_plant, 0, 0))

            r_plant = max(0.0, c)
            r_lift  = max(0.0, -c)
            r_hand_y = -0.84 - 0.42 * s * speed_mult
            r_hand_z = 0.14 + 0.30 * r_lift * speed_mult
            ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.50, r_hand_y, r_hand_z), (-0.85, 0.55, 0.35), (-25.0 * r_plant, 0, 0))

            # Stocky gorilla hind legs bounding behind the knuckle strikes!
            l_leg_y = 0.16 + 0.28 * s * speed_mult
            l_leg_z = 0.10 + max(0.0, -c) * 0.22 * speed_mult
            r_leg_y = 0.16 - 0.28 * s * speed_mult
            r_leg_z = 0.10 + max(0.0, c) * 0.22 * speed_mult
            ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.26, l_leg_y, l_leg_z), (0.38, -1.0, 0.35), (26.0 * s, 0, 0))
            ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.26, r_leg_y, r_leg_z), (-0.38, -1.0, 0.35), (-26.0 * s, 0, 0))
            key_all(f, prev_q)
        if clip_name == "Run":
            act_run = act_g

    # 3. Slash1 (Frames 1..21 — Massive Haymaker Gorilla Hook matching Ref 2 bottom-middle!)
    act_slash1 = new_action("Slash1")
    prev_q = {}
    for f in range(1, 22):
        begin_frame(f)
        u = (f - 1) / 20.0
        sw = math.sin(u * math.pi)
        wind = -math.sin(min(1.0, u * 2.4) * math.pi) if u < 0.4 else 0.0
        set_hips((0.0, -0.14 * sw, -0.08), pitch_deg=-16.0 - 16.0 * sw, yaw_deg=-26.0 * wind + 34.0 * sw, roll_deg=0.0)
        rot_deg("Spine", -14.0 - 8.0 * sw, 14.0 * sw, 0.0)
        rot_deg("Chest", -12.0 - 10.0 * sw, 20.0 * sw, 0.0)
        rot_deg("Head", 38.0, -18.0 * sw, 0.0)
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.18 + 0.45 * sw, -0.96 * sw - 0.18, 0.88 + 0.14 * sw), (-0.85, 0.4, 0.2))
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.58, 0.10 * sw - 0.22, 0.44), (0.85, 0.5, 0.2))
        ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.28, -0.14, 0.10), (0.32, -1.0, 0.3))
        ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.28, 0.16, 0.10), (-0.32, -1.0, 0.3))
        key_all(f, prev_q)

    # 4. GroundSlam / Slash2 (Frames 1..25 — Two-Hand Overhead Gorilla Ground Pound!)
    act_slam = new_action("GroundSlam")
    prev_q = {}
    for f in range(1, 26):
        begin_frame(f)
        u = (f - 1) / 24.0
        if u < 0.45:
            p = u / 0.45
            raise_s = p * p * (3.0 - 2.0 * p)
            set_hips((0.0, 0.08 * raise_s, 0.04 * raise_s), pitch_deg=-12.0 + 24.0 * raise_s, yaw_deg=0.0, roll_deg=0.0)
            rot_deg("Spine", -10.0 + 18.0 * raise_s, 0.0, 0.0)
            rot_deg("Chest", -8.0 + 16.0 * raise_s, 0.0, 0.0)
            rot_deg("Head", 22.0 + 10.0 * raise_s, 0.0, 0.0)
            ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.48 - 0.16 * raise_s, -0.25 - 0.10 * raise_s, 0.48 + 1.48 * raise_s), (0.8, 0.4, 0.4))
            ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.48 + 0.16 * raise_s, -0.25 - 0.10 * raise_s, 0.48 + 1.48 * raise_s), (-0.8, 0.4, 0.4))
        else:
            p = (u - 0.45) / 0.55
            slam_s = min(1.0, p * 2.2)
            set_hips((0.0, -0.14 * slam_s, -0.18 * slam_s), pitch_deg=12.0 - 50.0 * slam_s, yaw_deg=0.0, roll_deg=0.0)
            rot_deg("Spine", 8.0 - 30.0 * slam_s, 0.0, 0.0)
            rot_deg("Chest", 8.0 - 28.0 * slam_s, 0.0, 0.0)
            rot_deg("Head", 32.0 + 16.0 * slam_s, 0.0, 0.0)
            ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.34, -0.84 * slam_s - 0.15, 1.96 - 1.82 * slam_s), (0.8, 0.5, 0.3))
            ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.34, -0.84 * slam_s - 0.15, 1.96 - 1.82 * slam_s), (-0.8, 0.5, 0.3))
        ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.28, -0.02, 0.10), (0.35, -1.0, 0.3))
        ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.28, -0.02, 0.10), (-0.35, -1.0, 0.3))
        key_all(f, prev_q)

    # 5. Death (Frames 1..30 — Heavy forward gorilla collapse)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 31):
        begin_frame(f)
        u = (f - 1) / 29.0
        s = u * u * (3.0 - 2.0 * u)
        set_hips((0.0, -0.35 * s, -0.45 * s), pitch_deg=-16.0 - 74.0 * s, yaw_deg=10.0 * s, roll_deg=6.0 * s)
        rot_deg("Spine", -12.0, 0.0, 0.0)
        rot_deg("Chest", -10.0, 0.0, 0.0)
        rot_deg("Head", 20.0 * (1.0 - s), 12.0 * s, 0.0)
        rot_deg("LeftUpperArm", 0.0, -30.0 * s, -35.0 * s)
        rot_deg("RightUpperArm", 0.0, 30.0 * s, 35.0 * s)
        key_all(f, prev_q)

    # Export FBX & Blend at Idle frame 1
    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_fbx_and_blend(rig_obj, head_obj, body_obj, "Chibi_ZombieTank")

    # Render Front, Side-Profile (Hunched Back + Oversized Arms!), and Mid-Gorilla-Run Previews
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-0.85, -4.85, 1.38))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.05)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieTank_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ZOMBIE_DIR, "Chibi_ZombieTank_Preview.png"))

    cam_obj.location = Vector((-4.55, -0.65, 1.28))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.02)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_s = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieTank_Preview_Side.png")
    scene.render.filepath = tmp_s
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_s, os.path.join(ZOMBIE_DIR, "Chibi_ZombieTank_Preview_Side.png"))

    # Mid-Gorilla Knuckle-Run Pose (Frame 5 of Run!)
    rig_obj.animation_data.action = act_run
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_run.slots) > 0:
        rig_obj.animation_data.action_slot = act_run.slots[0]
    scene.frame_set(5)
    bpy.context.view_layer.update()
    cam_obj.location = Vector((-2.95, -3.75, 1.32))
    cam_obj.rotation_euler = (Vector((0.0, -0.18, 0.90)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_r = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieTank_Preview_Run.png")
    scene.render.filepath = tmp_r
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_r, os.path.join(ZOMBIE_DIR, "Chibi_ZombieTank_Preview_Run.png"))


if __name__ == "__main__":
    print("=== BUILDING CHIBI ZOMBIE SPITTER (REF 1 + REF 3 SPRINT) & ZOMBIE TANK (REF 2 + GORILLA RUN) ===")
    build_zombie_spitter()
    build_zombie_tank()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: CHIBI ZOMBIE SPITTER & TANK EXPORTED & RENDERED! ===")

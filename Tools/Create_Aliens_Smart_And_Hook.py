import sys
import os
import shutil
import time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
from mathutils import Vector, Matrix, Quaternion, Euler
import ChibiAnimeKit as kit

ALIEN_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels\Aliens"
TMP_EXPORT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_tmp_alien_export")
os.makedirs(ALIEN_DIR, exist_ok=True)
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


def make_emissive_mat(name, base_rgba, emit_rgb=None, emit_strength=1.8, roughness=0.45, metallic=0.0, alpha=1.0):
    mat = kit.make_mat(name, base_rgba, roughness=roughness, metallic=metallic, specular=0.35)
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        if emit_rgb is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emit_rgb[0], emit_rgb[1], emit_rgb[2], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emit_strength
        if alpha < 0.999 and "Alpha" in bsdf.inputs:
            bsdf.inputs["Alpha"].default_value = alpha
            if hasattr(mat, "blend_method"):
                mat.blend_method = 'BLEND'
    return mat


def _setup_studio_render(scene):
    world = bpy.data.worlds.new("AlienPreviewWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.10, 0.11, 0.16, 1.0)
        bg.inputs[1].default_value = 1.25

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 50
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    key_data = bpy.data.lights.new(name="KeyLight", type='SUN')
    key_data.energy = 3.0
    key_obj = bpy.data.objects.new(name="KeyLight", object_data=key_data)
    bpy.context.collection.objects.link(key_obj)
    key_obj.rotation_euler = Euler((math.radians(48), math.radians(-18), math.radians(-22)), 'XYZ')

    fill_data = bpy.data.lights.new(name="FillLight", type='SUN')
    fill_data.energy = 1.8
    fill_obj = bpy.data.objects.new(name="FillLight", object_data=fill_data)
    bpy.context.collection.objects.link(fill_obj)
    fill_obj.rotation_euler = Euler((math.radians(65), math.radians(32), math.radians(38)), 'XYZ')

    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    return cam_obj


def _make_rig_helpers(scene, rig_obj):
    def begin_frame(frame):
        scene.frame_set(frame)
        for pb in rig_obj.pose.bones:
            pb.location = Vector((0, 0, 0))
            pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
            pb.scale = Vector((1, 1, 1))
        bpy.context.view_layer.update()

    def set_bone(bname, loc=(0, 0, 0), rot_xyz=(0, 0, 0), scale=(1, 1, 1)):
        pb = rig_obj.pose.bones[bname]
        pb.location = Vector(loc)
        pb.rotation_quaternion = Euler((math.radians(rot_xyz[0]), math.radians(rot_xyz[1]), math.radians(rot_xyz[2])), 'XYZ').to_quaternion()
        pb.scale = Vector(scale)
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

    return begin_frame, set_bone, key_all, new_action


def _build_bone_tree(arm_name, bone_defs):
    arm_data = bpy.data.armatures.new(arm_name)
    rig_obj = bpy.data.objects.new(arm_name, arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')
    eb_map = {}
    for bname, h, t, pname, roll_vec in bone_defs:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in eb_map:
            eb.parent = eb_map[pname]
            eb.use_connect = False
        eb_map[bname] = eb
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in rig_obj.pose.bones:
        pb.rotation_mode = 'QUATERNION'
    return rig_obj, arm_data


def _bind_parts_to_bones(rig_obj, tagged_parts, mesh_name):
    """
    tagged_parts: list of (obj, [(bone_name, weight), ...])
    Assigns exact clean vertex groups per part before joining into a single rigged mesh!
    """
    all_bone_names = [b.name for b in rig_obj.data.bones]
    objs = []
    for obj, weights in tagged_parts:
        for bn in all_bone_names:
            if bn not in obj.vertex_groups:
                obj.vertex_groups.new(name=bn)
        v_indices = [v.index for v in obj.data.vertices]
        tot = sum(w for _, w in weights) or 1.0
        for bn, w in weights:
            obj.vertex_groups[bn].add(v_indices, w / tot, 'REPLACE')
        objs.append(obj)

    joined = kit.join_objects_into(objs[0], objs[1:], mesh_name)
    joined.parent = rig_obj
    mod = joined.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = rig_obj
    mod.use_deform_preserve_volume = True
    return joined


def _make_tube_chain(name, pts, radii, mat, segments=14):
    """Creates a smooth organic segmented alien tentacle/limb along 3D polyline pts."""
    rings = []
    for i, p in enumerate(pts):
        p_vec = Vector(p)
        if i == 0:
            tangent = (Vector(pts[1]) - p_vec).normalized()
        elif i == len(pts) - 1:
            tangent = (p_vec - Vector(pts[-2])).normalized()
        else:
            tangent = (Vector(pts[i + 1]) - Vector(pts[i - 1])).normalized()
        ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.92 else Vector((1, 0, 0))
        u_ax = tangent.cross(ref).normalized()
        v_ax = tangent.cross(u_ax).normalized()
        r = radii[i]
        rings.append((p_vec, u_ax * r, v_ax * r))
    return kit.create_loft_object(name, rings, mat, segments=segments)


def _export_alien(rig_obj, mesh_objs, base_name):
    tmp_fbx = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Rigged.fbx")
    tmp_blend = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Source.blend")

    bpy.ops.object.select_all(action='DESELECT')
    rig_obj.select_set(True)
    for m in mesh_objs:
        m.select_set(True)
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
    _copy_with_retry(tmp_fbx, os.path.join(ALIEN_DIR, f"{base_name}_Rigged.fbx"))
    _copy_with_retry(tmp_blend, os.path.join(ALIEN_DIR, f"{base_name}_Source.blend"))
    print(f"EXPORTED {base_name} FBX + BLEND OK!")


# ==============================================================================
#  1. ALIEN SMART — Lightbulb Brain-Jar 4-Legged 2-Armed Cyclops Alien (Ref 1!)
#     Color: Deep Cosmic Purple-Blue + Glowing Bio-Brain + Neon Yellow-Green Cyclops Eye!
# ==============================================================================

def build_alien_smart():
    scene = kit.reset_scene()

    # Pure Alien Palette: Cosmic Purple-Indigo + Electric Blue + Glowing Bio-Pink/Lime Brain + Neon Yellow Eye (NO grey/white!)
    m_purple    = make_emissive_mat("Mat_AlienSmart_PurpleShell", (0.24, 0.12, 0.48, 1.0), roughness=0.42, metallic=0.15)
    m_dark_blue = make_emissive_mat("Mat_AlienSmart_DeepBlue",    (0.10, 0.22, 0.56, 1.0), roughness=0.48, metallic=0.10)
    m_elec_blue = make_emissive_mat("Mat_AlienSmart_ElecBlue",    (0.14, 0.56, 0.96, 1.0), emit_rgb=(0.10, 0.48, 0.95), emit_strength=0.45, roughness=0.35)
    m_brain     = make_emissive_mat("Mat_AlienSmart_BrainGlow",   (0.95, 0.26, 0.72, 1.0), emit_rgb=(0.98, 0.22, 0.68), emit_strength=1.35, roughness=0.28)
    m_brain_v   = make_emissive_mat("Mat_AlienSmart_BrainVein",   (0.45, 0.98, 0.24, 1.0), emit_rgb=(0.45, 0.98, 0.22), emit_strength=1.90, roughness=0.25)
    m_glass     = make_emissive_mat("Mat_AlienSmart_GlassDome",   (0.32, 0.88, 0.98, 0.28), emit_rgb=(0.18, 0.65, 0.95), emit_strength=0.25, roughness=0.06, alpha=0.26)
    m_eye_glow  = make_emissive_mat("Mat_AlienSmart_CyclopsEye",  (0.96, 0.98, 0.16, 1.0), emit_rgb=(0.94, 0.98, 0.14), emit_strength=1.65, roughness=0.10)
    m_pupil     = make_emissive_mat("Mat_AlienSmart_Pupil",       (0.06, 0.02, 0.12, 1.0), roughness=0.05)
    m_lime_claw = make_emissive_mat("Mat_AlienSmart_LimeClaw",    (0.58, 0.96, 0.18, 1.0), emit_rgb=(0.42, 0.86, 0.12), emit_strength=0.55, roughness=0.36)
    m_eyelid    = make_emissive_mat("Mat_AlienSmart_BioFlesh",    (0.46, 0.14, 0.52, 1.0), roughness=0.55)

    bone_defs = [
        ("Root",         (0.0, 0.0, 0.0),      (0.0, 0.0, 0.20),      None,         (0, 1, 0)),
        ("Body",         (0.0, 0.0, 0.64),     (0.0, 0.0, 1.24),      "Root",       (0, 1, 0)),
        ("Brain",        (0.0, 0.0, 1.32),     (0.0, 0.0, 1.88),      "Body",       (0, 1, 0)),

        # 2 Long Alien Tentacle-Claw Arms (+X Left, -X Right)
        ("LeftArmUp",    (0.42, -0.02, 1.04),  (0.84, -0.08, 0.92),   "Body",       (0, 0, 1)),
        ("LeftArmFore",  (0.84, -0.08, 0.92),  (1.16, -0.22, 0.72),   "LeftArmUp",  (0, 0, 1)),
        ("RightArmUp",   (-0.42, -0.02, 1.04), (-0.84, -0.08, 0.92),  "Body",       (0, 0, 1)),
        ("RightArmFore", (-0.84, -0.08, 0.92), (-1.16, -0.22, 0.72),  "RightArmUp", (0, 0, 1)),

        # 4 Alien Spider-Claw Legs (Front-Left, Front-Right, Back-Left, Back-Right!)
        ("LegFL",        (0.24, -0.24, 0.62),  (0.42, -0.44, 0.06),   "Body",       (0, 1, 0)),
        ("LegFR",        (-0.24, -0.24, 0.62), (-0.42, -0.44, 0.06),  "Body",       (0, 1, 0)),
        ("LegBL",        (0.26,  0.24, 0.62),  (0.46,  0.44, 0.06),   "Body",       (0, 1, 0)),
        ("LegBR",        (-0.26,  0.24, 0.62), (-0.46,  0.44, 0.06),  "Body",       (0, 1, 0)),
    ]
    rig_obj, _ = _build_bone_tree("Alien_Smart_Rig", bone_defs)

    body_parts = []   # opaque mesh parts
    glass_parts = []  # separate translucent glass dome mesh for clean URP Transparent rendering!

    # --- 1. MAIN CYLINDRICAL ALIEN TORSO POD (Ref 1 silhouette in Deep Purple & Cobalt Blue!) ---
    body_parts.append((kit.create_loft_object("Pod_Torso", [
        (Vector((0.0, 0.0, 0.62)), Vector((0.34, 0.0, 0.0)), Vector((0.0, 0.34, 0.0))),
        (Vector((0.0, 0.0, 0.72)), Vector((0.44, 0.0, 0.0)), Vector((0.0, 0.44, 0.0))),
        (Vector((0.0, 0.0, 1.18)), Vector((0.46, 0.0, 0.0)), Vector((0.0, 0.46, 0.0))),
        (Vector((0.0, 0.0, 1.28)), Vector((0.42, 0.0, 0.0)), Vector((0.0, 0.42, 0.0))),
    ], m_purple, segments=28), [("Body", 1.0)]))

    # Glowing electric-blue collar rim supporting the lightbulb glass dome!
    body_parts.append((kit.create_torus_ring("DomeRim_Bot", (0.0, 0.0, 1.27), 0.44, 0.055, m_elec_blue), [("Body", 1.0)]))
    body_parts.append((kit.create_torus_ring("BaseRim_Bot", (0.0, 0.0, 0.68), 0.42, 0.045, m_dark_blue), [("Body", 1.0)]))

    # Bio-alien teeth/fang ridge around bottom lip of pod (Ref 1 detail!)
    for k in range(7):
        ang = math.radians(-68.0 + k * (136.0 / 6.0))
        tx = math.sin(ang) * 0.435
        ty = -math.cos(ang) * 0.435
        body_parts.append((kit.create_loft_object(f"BioFang_{k}", [
            (Vector((tx, ty, 0.74)), Vector((0.036, 0.0, 0.0)), Vector((0.0, 0.036, 0.0))),
            (Vector((tx * 1.05, ty * 1.05, 0.62)), Vector((0.005, 0.0, 0.0)), Vector((0.0, 0.005, 0.0))),
        ], m_lime_claw, segments=8), [("Body", 1.0)]))

    # --- 2. ONE HUGE GLOWING CYCLOPS EYE DEAD-CENTER ON FRONT! ---
    body_parts.append((kit.create_torus_ring("EyeSocket", (0.0, -0.455, 0.98), 0.175, 0.048, m_eyelid, rot_euler=(math.pi * 0.5, 0.0, 0.0)), [("Body", 1.0)]))
    body_parts.append((kit.create_ellipsoid("CyclopsEyeBall", (0.0, -0.445, 0.98), (0.165, 0.125, 0.165), m_eye_glow), [("Body", 1.0)]))
    # Fierce alien vertical-slit pupil + glossy catchlight
    body_parts.append((kit.create_ellipsoid("CyclopsPupil", (0.0, -0.562, 0.98), (0.044, 0.025, 0.112), m_pupil), [("Body", 1.0)]))
    body_parts.append((kit.create_ellipsoid("CyclopsGleam", (0.055, -0.558, 1.035), (0.028, 0.016, 0.028), m_elec_blue), [("Body", 1.0)]))
    # Glowing bio-nodes on sides of face
    for side in (-1.0, 1.0):
        body_parts.append((kit.create_ellipsoid(f"SideNode_{side}", (side * 0.34, -0.32, 0.92), (0.062, 0.055, 0.062), m_brain_v), [("Body", 1.0)]))

    # --- 3. HUGE PULSING ALIEN BRAIN INSIDE THE LIGHTBULB DOME! ---
    # Organic pedestal base inside dome
    body_parts.append((kit.create_loft_object("BrainBase", [
        (Vector((0.0, 0.0, 1.26)), Vector((0.32, 0.0, 0.0)), Vector((0.0, 0.32, 0.0))),
        (Vector((0.0, 0.0, 1.40)), Vector((0.26, 0.0, 0.0)), Vector((0.0, 0.26, 0.0))),
    ], m_dark_blue, segments=20), [("Body", 1.0)]))

    # Sculpted Giant Multi-Lobed Alien Brain (left & right hemispheres + bumpy gyri folds + glowing green synaptic veins!)
    for side in (-1.0, 1.0):
        # Main large hemisphere lobe
        body_parts.append((kit.create_ellipsoid(f"BrainHemi_{side}", (side * 0.135, 0.0, 1.66), (0.185, 0.285, 0.235), m_brain), [("Brain", 1.0)]))
        # Organic bumpy brain gyri folds wrapping the hemisphere!
        gyri_coords = [
            (side * 0.17, -0.17, 1.72, 0.10, 0.10, 0.09),
            (side * 0.20,  0.00, 1.77, 0.11, 0.12, 0.10),
            (side * 0.17,  0.17, 1.70, 0.10, 0.11, 0.09),
            (side * 0.22, -0.09, 1.56, 0.10, 0.10, 0.09),
            (side * 0.22,  0.10, 1.56, 0.10, 0.10, 0.09),
            (side * 0.09, -0.22, 1.60, 0.09, 0.09, 0.09),
            (side * 0.09,  0.22, 1.60, 0.09, 0.09, 0.09),
            (side * 0.11,  0.00, 1.84, 0.10, 0.14, 0.08),
        ]
        for gi, (gx, gy, gz, rx, ry, rz) in enumerate(gyri_coords):
            body_parts.append((kit.create_ellipsoid(f"BrainGyri_{side}_{gi}", (gx, gy, gz), (rx, ry, rz), m_brain), [("Brain", 1.0)]))

        # Glowing neon-green neural tendrils plugging from brain down into the alien pod!
        tendril = _make_tube_chain(
            f"NeuralCord_{side}",
            [(side * 0.16, -0.10, 1.48), (side * 0.28, -0.12, 1.38), (side * 0.24, -0.10, 1.26)],
            [0.024, 0.028, 0.024], m_brain_v, segments=10
        )
        body_parts.append((tendril, [("Brain", 0.6), ("Body", 0.4)]))
        tendril_b = _make_tube_chain(
            f"NeuralCordB_{side}",
            [(side * 0.16, 0.12, 1.48), (side * 0.28, 0.14, 1.38), (side * 0.24, 0.12, 1.26)],
            [0.024, 0.028, 0.024], m_elec_blue, segments=10
        )
        body_parts.append((tendril_b, [("Brain", 0.6), ("Body", 0.4)]))

    # Glowing brain core pulse strip down center fissure + neon-lime synaptic veins wrapping hemispheres!
    body_parts.append((kit.create_ellipsoid("BrainSynapse", (0.0, 0.0, 1.88), (0.045, 0.18, 0.045), m_brain_v), [("Brain", 1.0)]))
    for side in (-1.0, 1.0):
        vein_a = _make_tube_chain(
            f"BrainVeinA_{side}",
            [(side * 0.06, -0.22, 1.68), (side * 0.24, -0.10, 1.76), (side * 0.22, 0.12, 1.72), (side * 0.08, 0.22, 1.62)],
            [0.014, 0.018, 0.018, 0.014], m_brain_v, segments=8
        )
        body_parts.append((vein_a, [("Brain", 1.0)]))

    # --- 4. LIGHTBULB GLASS DOME SHELL (Separate High-Smooth Translucent Mesh!) ---
    dome_rings = [
        (Vector((0.0, 0.0, 1.26)), Vector((0.43, 0.0, 0.0)), Vector((0.0, 0.43, 0.0))),
        (Vector((0.0, 0.0, 1.48)), Vector((0.46, 0.0, 0.0)), Vector((0.0, 0.46, 0.0))),
        (Vector((0.0, 0.0, 1.72)), Vector((0.465, 0.0, 0.0)), Vector((0.0, 0.465, 0.0))),
        (Vector((0.0, 0.0, 1.90)), Vector((0.435, 0.0, 0.0)), Vector((0.0, 0.435, 0.0))),
        (Vector((0.0, 0.0, 2.02)), Vector((0.37, 0.0, 0.0)), Vector((0.0, 0.37, 0.0))),
        (Vector((0.0, 0.0, 2.11)), Vector((0.27, 0.0, 0.0)), Vector((0.0, 0.27, 0.0))),
        (Vector((0.0, 0.0, 2.17)), Vector((0.15, 0.0, 0.0)), Vector((0.0, 0.15, 0.0))),
        (Vector((0.0, 0.0, 2.20)), Vector((0.03, 0.0, 0.0)), Vector((0.0, 0.03, 0.0))),
    ]
    glass_parts.append((kit.create_loft_object("Lightbulb_GlassDome", dome_rings, m_glass, segments=36), [("Body", 1.0)]))

    # --- 5. 2 LONG FLEXIBLE ALIEN ARMS WITH BIG 4-FINGERED BIO-CLAWS! ---
    for side, up_b, fore_b in ((1.0, "LeftArmUp", "LeftArmFore"), (-1.0, "RightArmUp", "RightArmFore")):
        # Upper tentacle tube + organic rings
        up_arm = _make_tube_chain(
            f"SmartArmUp_{side}",
            [(side * 0.42, -0.02, 1.04), (side * 0.64, -0.05, 1.02), (side * 0.84, -0.08, 0.92)],
            [0.088, 0.072, 0.082], m_dark_blue, segments=14
        )
        body_parts.append((up_arm, [(up_b, 1.0)]))
        body_parts.append((kit.create_ellipsoid(f"ArmJoint_{side}", (side * 0.84, -0.08, 0.92), (0.098, 0.098, 0.098), m_elec_blue), [(up_b, 0.5), (fore_b, 0.5)]))

        # Thick muscular alien forearm cuff + big expressive palm + 4 big grasping alien claws (Ref 1!)
        fore_arm = _make_tube_chain(
            f"SmartArmFore_{side}",
            [(side * 0.84, -0.08, 0.92), (side * 0.98, -0.14, 0.83), (side * 1.12, -0.20, 0.74)],
            [0.098, 0.125, 0.145], m_purple, segments=16
        )
        body_parts.append((fore_arm, [(fore_b, 1.0)]))
        palm_pos = Vector((side * 1.20, -0.25, 0.67))
        body_parts.append((kit.create_ellipsoid(f"AlienPalm_{side}", palm_pos, (0.185, 0.195, 0.145), m_dark_blue), [(fore_b, 1.0)]))
        body_parts.append((kit.create_ellipsoid(f"PalmOrb_{side}", palm_pos + Vector((0.0, -0.04, 0.08)), (0.078, 0.078, 0.055), m_brain_v), [(fore_b, 1.0)]))
        # 4 big chunky alien fingers/claws spreading out!
        for fi, (fx, fy, fz) in enumerate((
            (side * 0.18, -0.18, -0.02),
            (side * 0.23, -0.06, -0.04),
            (side * 0.20,  0.08, -0.04),
            (side * 0.05, -0.21, -0.04),
        )):
            f_start = palm_pos + Vector((fx * 0.55, fy * 0.55, fz * 0.5))
            f_tip   = palm_pos + Vector((fx * 1.70, fy * 1.80, fz - 0.10))
            body_parts.append((_make_tube_chain(f"Finger_{side}_{fi}", [f_start, f_tip], [0.058, 0.018], m_lime_claw, segments=10), [(fore_b, 1.0)]))

    # --- 6. 4 ALIEN SPIDER-CLAW LEGS (FL, FR, BL, BR — exact Ref 1 four-legged walker!) ---
    leg_specs = [
        ("LegFL",  0.24, -0.24,  0.38, -0.38,  0.42, -0.44),
        ("LegFR", -0.24, -0.24, -0.38, -0.38, -0.42, -0.44),
        ("LegBL",  0.26,  0.24,  0.40,  0.38,  0.46,  0.44),
        ("LegBR", -0.26,  0.24, -0.40,  0.38, -0.46,  0.44),
    ]
    for lb, hx, hy, mx, my, fx, fy in leg_specs:
        upper_leg = _make_tube_chain(f"{lb}_Tube", [(hx, hy, 0.64), (mx, my, 0.42)], [0.072, 0.068], m_elec_blue, segments=12)
        body_parts.append((upper_leg, [(lb, 1.0)]))
        # Bulbous alien shin-pod + bright glowing alien claw hoof (exact Ref 1!)
        shin_pod = kit.create_loft_object(f"{lb}_Pod", [
            (Vector((mx, my, 0.44)), Vector((0.085, 0.0, 0.0)), Vector((0.0, 0.085, 0.0))),
            (Vector((fx, fy, 0.20)), Vector((0.128, 0.0, 0.0)), Vector((0.0, 0.128, 0.0))),
            (Vector((fx, fy, 0.12)), Vector((0.110, 0.0, 0.0)), Vector((0.0, 0.110, 0.0))),
        ], m_dark_blue, segments=16)
        body_parts.append((shin_pod, [(lb, 1.0)]))
        claw_dir = Vector((fx - hx, fy - hy, 0.0)).normalized()
        claw_tip = Vector((fx, fy, 0.03)) + claw_dir * 0.20
        claw_obj = _make_tube_chain(f"{lb}_Claw", [(fx, fy, 0.14), (fx + claw_dir.x * 0.08, fy + claw_dir.y * 0.08, 0.07), claw_tip], [0.082, 0.062, 0.012], m_lime_claw, segments=12)
        body_parts.append((claw_obj, [(lb, 1.0)]))

    opaque_mesh = _bind_parts_to_bones(rig_obj, body_parts, "AlienSmart_Body")
    glass_mesh = _bind_parts_to_bones(rig_obj, glass_parts, "AlienSmart_GlassDome")

    # --- ANIMATIONS (Idle, Walk, Run, Slash1, Death) ---
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    # 1. Idle (Pulsing alien brain + hovering bob + sinister flexing claws!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Body", loc=(0, 0, 0.03 * s), rot_xyz=(3.0 * s, 0, 2.5 * c))
        set_bone("Brain", scale=(1.0 + 0.08 * s, 1.0 + 0.08 * s, 1.0 + 0.06 * c))
        set_bone("LeftArmUp", rot_xyz=(6.0 * s, 8.0 * c, -6.0 * s))
        set_bone("LeftArmFore", rot_xyz=(-12.0 + 8.0 * s, 0, 10.0 * c))
        set_bone("RightArmUp", rot_xyz=(-6.0 * s, -8.0 * c, 6.0 * s))
        set_bone("RightArmFore", rot_xyz=(-12.0 - 8.0 * s, 0, -10.0 * c))
        key_all(f, prev_q)

    # 2. Run & Walk (Energetic 4-Leg Spider Scuttle + Pulsing Brain + Reaching Alien Claws!)
    for clip_name, n_frames, mult in (("Run", 17, 1.0), ("Walk", 23, 0.72)):
        act_m = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s, c = math.sin(ang), math.cos(ang)
            set_bone("Body", loc=(0.04 * s * mult, 0, abs(s) * 0.08 * mult), rot_xyz=(-12.0 * mult, 6.0 * s * mult, 10.0 * s * mult))
            set_bone("Brain", scale=(1.0 + 0.12 * abs(s), 1.0 + 0.12 * abs(s), 1.0 - 0.06 * abs(s)))
            # Diagonal spider-leg gait: (FL + BR) alternate with (FR + BL)!
            set_bone("LegFL", loc=(0, 0, max(0, c) * 0.12 * mult), rot_xyz=(24.0 * s * mult, 0, 12.0 * s))
            set_bone("LegBR", loc=(0, 0, max(0, c) * 0.12 * mult), rot_xyz=(24.0 * s * mult, 0, -12.0 * s))
            set_bone("LegFR", loc=(0, 0, max(0, -c) * 0.12 * mult), rot_xyz=(-24.0 * s * mult, 0, -12.0 * s))
            set_bone("LegBL", loc=(0, 0, max(0, -c) * 0.12 * mult), rot_xyz=(-24.0 * s * mult, 0, 12.0 * s))
            # Expressive swinging alien arms
            set_bone("LeftArmUp", rot_xyz=(18.0 * s * mult, 14.0 * c, -18.0 * s))
            set_bone("LeftArmFore", rot_xyz=(-16.0, 0, 20.0 * s))
            set_bone("RightArmUp", rot_xyz=(-18.0 * s * mult, -14.0 * c, 18.0 * s))
            set_bone("RightArmFore", rot_xyz=(-16.0, 0, -20.0 * s))
            key_all(f, prev_q)
        if clip_name == "Run":
            act_run = act_m

    # 3. Slash1 (Psychic Brain Surge + Double Alien Claw Lunge!)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        set_bone("Body", loc=(0, -0.14 * sw, 0.06 * sw), rot_xyz=(-16.0 * sw, 0, 0))
        set_bone("Brain", scale=(1.0 + 0.25 * sw, 1.0 + 0.25 * sw, 1.0 + 0.22 * sw))
        set_bone("LeftArmUp", rot_xyz=(-15.0 * sw, 0, -48.0 * sw))
        set_bone("LeftArmFore", rot_xyz=(-20.0 * sw, 0, -25.0 * sw))
        set_bone("RightArmUp", rot_xyz=(-15.0 * sw, 0, 48.0 * sw))
        set_bone("RightArmFore", rot_xyz=(-20.0 * sw, 0, 25.0 * sw))
        key_all(f, prev_q)

    # 4. Death (Collapse onto curled legs)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("Body", loc=(0, 0.12 * s, -0.46 * s), rot_xyz=(-68.0 * s, 18.0 * s, 12.0 * s))
        set_bone("Brain", scale=(1.0 - 0.25 * s, 1.0 - 0.25 * s, 1.0 - 0.25 * s))
        set_bone("LeftArmUp", rot_xyz=(20.0 * s, 0, 35.0 * s))
        set_bone("RightArmUp", rot_xyz=(20.0 * s, 0, -35.0 * s))
        key_all(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [opaque_mesh, glass_mesh], "Alien_Smart")

    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-1.15, -4.65, 1.65))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.10)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Alien_Smart_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ALIEN_DIR, "Alien_Smart_Preview.png"))

    cam_obj.location = Vector((1.75, 4.15, 1.75))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.10)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_b = os.path.join(TMP_EXPORT_DIR, "Alien_Smart_Preview_Back.png")
    scene.render.filepath = tmp_b
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_b, os.path.join(ALIEN_DIR, "Alien_Smart_Preview_Back.png"))


# ==============================================================================
#  2. ALIEN HOOK — Elongated Cyclops Tentacle Alien with GIANT EXTENDABLE LEFT HAND (Ref 2!)
#     Color: Toxic Emerald-Green & Yellow-Lime + Cosmic Purple-Blue (NO grey/white!)
# ==============================================================================

def build_alien_hook():
    scene = kit.reset_scene()

    # Panoptes Horror-Flesh Palette (Ref: Warm Peach-Tan Flesh, Shaded Ochre-Brown, Jet-Black Hair/Crest, Crimson-Rose Mouth/Claws, Ghostly White Cyclops Eye!)
    m_emerald   = make_emissive_mat("Mat_AlienHook_Emerald",    (0.88, 0.63, 0.42, 1.0), roughness=0.38, metallic=0.02)  # Warm fleshy peach-tan body & cranium
    m_lime      = make_emissive_mat("Mat_AlienHook_NeonLime",   (0.84, 0.44, 0.42, 1.0), roughness=0.32, metallic=0.02)  # Bloody rose-pink claws, coils & tubes
    m_purple    = make_emissive_mat("Mat_AlienHook_Purple",     (0.06, 0.05, 0.07, 1.0), roughness=0.28, metallic=0.08)  # Glossy jet-black tendrils & rims (Panoptes hair!)
    m_indigo    = make_emissive_mat("Mat_AlienHook_Indigo",     (0.66, 0.39, 0.23, 1.0), roughness=0.42, metallic=0.02)  # Shaded amber-tan flesh folds & tentacles
    m_dome_glow = make_emissive_mat("Mat_AlienHook_CraniumBio", (0.04, 0.03, 0.05, 1.0), roughness=0.14, metallic=0.12)  # Glossy jet-black dome crest
    m_eye_glow  = make_emissive_mat("Mat_AlienHook_CyclopsEye", (0.97, 0.95, 0.93, 1.0), emit_rgb=(0.96, 0.92, 0.90), emit_strength=0.45, roughness=0.08)  # Eerie bloodshot white Panoptes eye!
    m_pupil     = make_emissive_mat("Mat_AlienHook_Pupil",      (0.14, 0.02, 0.04, 1.0), roughness=0.05)                 # Dark crimson-black pupil
    m_yellow_g  = make_emissive_mat("Mat_AlienHook_BioGlow",    (0.62, 0.14, 0.16, 1.0), emit_rgb=(0.68, 0.16, 0.18), emit_strength=0.55, roughness=0.24)  # Deep fleshy crimson glow accents

    # Note: Character faces -Y. Character's LEFT side is +X!
    bone_defs = [
        ("Root",           (0.0, 0.0, 0.0),      (0.0, 0.0, 0.20),      None,           (0, 1, 0)),
        ("TentacleBase",   (0.0, 0.0, 0.42),     (0.0, 0.0, 0.66),      "Root",         (0, 1, 0)),
        ("Body",           (0.0, 0.0, 0.66),     (0.0, 0.0, 1.24),      "TentacleBase", (0, 1, 0)),
        ("Head",           (0.0, 0.0, 1.24),     (0.0, 0.14, 2.02),     "Body",         (0, 1, 0)),

        # GIANT EXTENDABLE LEFT HOOK ARM (+X — stretches way forward during Hook & Slash1!)
        ("LeftArm1",       (0.28, -0.04, 1.08),  (0.62, -0.14, 1.04),   "Body",         (0, 0, 1)),
        ("LeftArm2",       (0.62, -0.14, 1.04),  (0.96, -0.26, 0.98),   "LeftArm1",     (0, 0, 1)),
        ("LeftGiantHand",  (0.96, -0.26, 0.98),  (1.42, -0.44, 0.92),   "LeftArm2",     (0, 0, 1)),
        ("LeftClawTop",    (1.18, -0.34, 1.08),  (1.54, -0.48, 1.16),   "LeftGiantHand",(0, 0, 1)),
        ("LeftClawBot",    (1.18, -0.34, 0.86),  (1.54, -0.48, 0.78),   "LeftGiantHand",(0, 0, 1)),

        # RIGHT ARM (-X — sleek bio-tendril stinger!)
        ("RightArm",       (-0.28, -0.02, 1.06), (-0.76, -0.22, 0.96),  "Body",         (0, 0, 1)),

        # 4 Tentacle Cluster Bones at bottom for writhing slither!
        ("TentF",          (0.0, -0.16, 0.44),   (0.0, -0.48, 0.05),    "TentacleBase", (0, 1, 0)),
        ("TentB",          (0.0,  0.16, 0.44),   (0.0,  0.48, 0.05),    "TentacleBase", (0, 1, 0)),
        ("TentL",          (0.18, 0.0,  0.44),   (0.50, 0.0,  0.05),    "TentacleBase", (0, 1, 0)),
        ("TentR",          (-0.18, 0.0, 0.44),   (-0.50, 0.0, 0.05),    "TentacleBase", (0, 1, 0)),
    ]
    rig_obj, _ = _build_bone_tree("Alien_Hook_Rig", bone_defs)

    parts = []

    # --- 1. WRITHING ALIEN TENTACLE BASE (Ref 2's iconic cluster of organic tendrils!) ---
    # Central stalk
    parts.append((kit.create_loft_object("TentacleStalk", [
        (Vector((0.0, 0.0, 0.28)), Vector((0.18, 0.0, 0.0)), Vector((0.0, 0.18, 0.0))),
        (Vector((0.0, 0.0, 0.68)), Vector((0.20, 0.0, 0.0)), Vector((0.0, 0.20, 0.0))),
    ], m_purple, segments=18), [("TentacleBase", 1.0)]))

    # 8 curving alien tentacles radiating around the bottom with glowing lime tips!
    tent_angles = [
        (  0, "TentF"), ( 45, "TentF"), (-45, "TentF"),
        ( 90, "TentL"), (135, "TentB"), (180, "TentB"),
        (-135, "TentB"), (-90, "TentR"),
    ]
    for idx, (deg, bname) in enumerate(tent_angles):
        rad = math.radians(deg)
        dx, dy = math.sin(rad), -math.cos(rad)
        t_pts = [
            (dx * 0.10, dy * 0.10, 0.56),
            (dx * 0.24, dy * 0.24, 0.28),
            (dx * 0.44, dy * 0.44, 0.11),
            (dx * 0.58, dy * 0.58, 0.06),
        ]
        mat_t = m_indigo if idx % 2 == 0 else m_purple
        parts.append((_make_tube_chain(f"BaseTentacle_{idx}", t_pts, [0.064, 0.056, 0.066, 0.052], mat_t, segments=10), [(bname, 0.8), ("TentacleBase", 0.2)]))
        # Glowing neon-lime sucker cap on each tentacle tip!
        parts.append((kit.create_ellipsoid(f"TentTip_{idx}", (dx * 0.59, dy * 0.59, 0.06), (0.062, 0.062, 0.050), m_lime), [(bname, 1.0)]))

    # --- 2. BULBOUS ALIEN HIP PODS & SLENDER BIO-TORSO (Ref 2 silhouette!) ---
    parts.append((kit.create_loft_object("Hook_Torso", [
        (Vector((0.0, 0.0, 0.66)), Vector((0.24, 0.0, 0.0)), Vector((0.0, 0.22, 0.0))),
        (Vector((0.0, 0.0, 0.84)), Vector((0.20, 0.0, 0.0)), Vector((0.0, 0.18, 0.0))),
        (Vector((0.0, 0.0, 1.06)), Vector((0.28, 0.0, 0.0)), Vector((0.0, 0.24, 0.0))),
        (Vector((0.0, 0.0, 1.24)), Vector((0.18, 0.0, 0.0)), Vector((0.0, 0.16, 0.0))),
    ], m_emerald, segments=22), [("Body", 1.0)]))
    # Bulbous side hip pods (signature Ref 2 silhouette!)
    for side in (-1.0, 1.0):
        parts.append((kit.create_ellipsoid(f"HipPod_{side}", (side * 0.28, 0.04, 0.78), (0.18, 0.24, 0.19), m_emerald, rot_euler=(0.22, side * 0.18, 0.0)), [("Body", 1.0)]))
        parts.append((kit.create_ellipsoid(f"HipGlow_{side}", (side * 0.36, -0.06, 0.76), (0.065, 0.085, 0.065), m_yellow_g), [("Body", 1.0)]))
    # Glowing chest core panel
    parts.append((kit.create_rounded_box("ChestBioCore", (0.0, -0.24, 1.02), (0.16, 0.05, 0.14), m_purple, bevel_width=0.015), [("Body", 1.0)]))
    parts.append((kit.create_ellipsoid("ChestCoreOrb", (0.0, -0.268, 1.02), (0.055, 0.025, 0.048), m_yellow_g), [("Body", 1.0)]))

    # --- 3. ELONGATED SWEPT-BACK ALIEN CRANIUM + ONE HUGE CYCLOPS EYE! (Ref 2!) ---
    # Neck stalk
    parts.append((kit.create_loft_object("Hook_Neck", [
        (Vector((0.0, 0.0, 1.20)), Vector((0.12, 0.0, 0.0)), Vector((0.0, 0.12, 0.0))),
        (Vector((0.0, -0.02, 1.38)), Vector((0.15, 0.0, 0.0)), Vector((0.0, 0.14, 0.0))),
    ], m_lime, segments=16), [("Head", 1.0)]))

    # Swept-back elongated alien head carapace (exact Ref 2 iconic tall xenomorph/alien dome!)
    cranium_rings = [
        (Vector((0.0, -0.04, 1.36)), Vector((0.24, 0.0, 0.0)), Vector((0.0, 0.22, 0.0))),
        (Vector((0.0, -0.02, 1.56)), Vector((0.33, 0.0, 0.0)), Vector((0.0, 0.34, 0.0))),
        (Vector((0.0,  0.12, 1.84)), Vector((0.35, 0.0, 0.0)), Vector((0.0, 0.42, 0.0))),
        (Vector((0.0,  0.32, 2.12)), Vector((0.31, 0.0, 0.0)), Vector((0.0, 0.39, 0.0))),
        (Vector((0.0,  0.50, 2.30)), Vector((0.21, 0.0, 0.0)), Vector((0.0, 0.26, 0.0))),
        (Vector((0.0,  0.60, 2.38)), Vector((0.06, 0.0, 0.0)), Vector((0.0, 0.07, 0.0))),
    ]
    parts.append((kit.create_loft_object("AlienCranium", cranium_rings, m_emerald, segments=28), [("Head", 1.0)]))
    # Glowing deep cosmic-purple glossy bio-dome crest running along the elongated alien cranium!
    parts.append((kit.create_ellipsoid("CraniumDomeCrest", (0.0, 0.18, 1.98), (0.28, 0.56, 0.28), m_dome_glow, rot_euler=(-0.62, 0.0, 0.0)), [("Head", 1.0)]))

    # Side bio-tubes & pulsing alien ear-glands (Ref 2 detail!)
    for side in (-1.0, 1.0):
        parts.append((kit.create_ellipsoid(f"HeadGland_{side}", (side * 0.31, 0.04, 1.56), (0.075, 0.095, 0.085), m_purple), [("Head", 1.0)]))
        parts.append((kit.create_ellipsoid(f"HeadGlandGlow_{side}", (side * 0.37, 0.02, 1.56), (0.036, 0.048, 0.048), m_yellow_g), [("Head", 1.0)]))
        tube = _make_tube_chain(
            f"HeadBioTube_{side}",
            [(side * 0.24, 0.24, 1.82), (side * 0.34, 0.20, 1.56), (side * 0.22, 0.12, 1.24)],
            [0.032, 0.036, 0.028], m_lime, segments=10
        )
        parts.append((tube, [("Head", 0.7), ("Body", 0.3)]))

    # ONE HUGE MENACING CYCLOPS EYE DEAD-CENTER ON FACE!
    parts.append((kit.create_torus_ring("Hook_EyeRim", (0.0, -0.295, 1.49), 0.185, 0.052, m_purple, rot_euler=(math.pi * 0.5, 0.0, 0.0)), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("Hook_CyclopsEye", (0.0, -0.285, 1.49), (0.175, 0.135, 0.165), m_eye_glow), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("Hook_Pupil", (0.0, -0.412, 1.49), (0.048, 0.026, 0.115), m_pupil), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("Hook_EyeGleam", (0.060, -0.405, 1.545), (0.030, 0.016, 0.030), m_lime), [("Head", 1.0)]))
    # Alien breather/snout mandible below giant eye
    parts.append((kit.create_ellipsoid("Hook_Mandible", (0.0, -0.25, 1.32), (0.10, 0.08, 0.06), m_indigo), [("Head", 1.0)]))

    # --- 4. GIANT EXTENDABLE LEFT HOOK HAND (+X — "มือข้างซ้ายเป็นมือใหญ่ยืดได้"!) ---
    # Stretchy segmented elastic tentacle arm (LeftArm1 -> LeftArm2)
    l_seg1 = _make_tube_chain("LeftStretch1", [(0.26, -0.04, 1.08), (0.45, -0.09, 1.06), (0.62, -0.14, 1.04)], [0.095, 0.085, 0.095], m_purple, segments=14)
    l_seg2 = _make_tube_chain("LeftStretch2", [(0.62, -0.14, 1.04), (0.80, -0.20, 1.01), (0.96, -0.26, 0.98)], [0.095, 0.105, 0.125], m_indigo, segments=14)
    parts.append((l_seg1, [("LeftArm1", 1.0)]))
    parts.append((l_seg2, [("LeftArm2", 1.0)]))
    # Elastic glowing bio-coil rings along the stretchy left arm!
    for ci, (cx, cy, cz, cb) in enumerate((
        (0.40, -0.08, 1.06, "LeftArm1"),
        (0.55, -0.12, 1.05, "LeftArm1"),
        (0.74, -0.18, 1.02, "LeftArm2"),
        (0.88, -0.23, 0.99, "LeftArm2"),
    )):
        parts.append((kit.create_torus_ring(f"StretchCoil_{ci}", (cx, cy, cz), 0.112, 0.024, m_lime, rot_euler=(0.0, math.pi * 0.5, -0.28)), [(cb, 1.0)]))

    # MASSIVE OVERSIZED ALIEN GRAPPLING CLAW HAND (LeftGiantHand + Articulated Top/Bottom Pincers!)
    wrist_pos = Vector((1.06, -0.30, 0.96))
    parts.append((kit.create_ellipsoid("GiantHookPalm", wrist_pos + Vector((0.12, -0.04, 0.0)), (0.24, 0.22, 0.20), m_emerald), [("LeftGiantHand", 1.0)]))
    parts.append((kit.create_ellipsoid("GiantPalmCore", wrist_pos + Vector((0.16, -0.16, 0.0)), (0.11, 0.09, 0.11), m_yellow_g), [("LeftGiantHand", 1.0)]))
    parts.append((kit.create_torus_ring("GiantWristCuff", wrist_pos, 0.175, 0.045, m_purple, rot_euler=(0.0, math.pi * 0.5, -0.28)), [("LeftGiantHand", 1.0)]))

    # Giant articulated TOP 2 massive hook fingers (opening & snapping shut on grab!)
    for fi, y_off in enumerate((-0.09, 0.08)):
        c_pts = [
            (1.18, -0.34 + y_off, 1.07),
            (1.38, -0.44 + y_off * 1.1, 1.18),
            (1.56, -0.54 + y_off * 0.8, 1.05),
        ]
        parts.append((_make_tube_chain(f"GiantTopClaw_{fi}", c_pts, [0.088, 0.072, 0.018], m_lime, segments=12), [("LeftClawTop", 1.0)]))

    # Giant articulated BOTTOM opposing hook thumb/jaw!
    b_pts = [
        (1.18, -0.34, 0.85),
        (1.38, -0.44, 0.74),
        (1.56, -0.54, 0.88),
    ]
    parts.append((_make_tube_chain("GiantBotClaw", b_pts, [0.095, 0.078, 0.020], m_lime, segments=12), [("LeftClawBot", 1.0)]))

    # --- 5. RIGHT ARM (-X — Sleek Organic Bio-Tendril & Glowing Plasma Stinger!) ---
    r_arm = _make_tube_chain("RightBioArm", [(-0.26, -0.02, 1.06), (-0.52, -0.10, 1.02), (-0.74, -0.22, 0.96)], [0.072, 0.064, 0.082], m_purple, segments=12)
    parts.append((r_arm, [("RightArm", 1.0)]))
    parts.append((kit.create_ellipsoid("RightBioBulb", (-0.82, -0.28, 0.94), (0.115, 0.145, 0.105), m_emerald, rot_euler=(0.0, 0.0, 0.45)), [("RightArm", 1.0)]))
    parts.append((_make_tube_chain("RightStinger", [(-0.88, -0.34, 0.94), (-1.04, -0.48, 0.94)], [0.056, 0.012], m_yellow_g, segments=10), [("RightArm", 1.0)]))

    hook_mesh = _bind_parts_to_bones(rig_obj, parts, "AlienHook_Body")

    # --- ANIMATIONS (Idle, Walk, Run, Hook [GIANT LEFT HAND STRETCH!], Slash1, Death) ---
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    # 1. Idle (Hover-slither + menacing giant left claw opening/closing!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("TentacleBase", loc=(0, 0, 0.03 * s), rot_xyz=(3.0 * s, 0, 4.0 * c))
        set_bone("Body", rot_xyz=(4.0 * c, 0, -4.0 * s))
        set_bone("Head", rot_xyz=(-4.0 * s, 0, 6.0 * c))
        set_bone("LeftArm1", rot_xyz=(0, 6.0 * s, -12.0 + 6.0 * c))
        set_bone("LeftArm2", rot_xyz=(0, -6.0 * s, -16.0 - 6.0 * c))
        set_bone("LeftClawTop", rot_xyz=(0, -10.0 - 10.0 * s, 0))
        set_bone("LeftClawBot", rot_xyz=(0,  10.0 + 10.0 * s, 0))
        set_bone("RightArm", rot_xyz=(6.0 * s, 0, 8.0 * c))
        set_bone("TentF", rot_xyz=(12.0 * s, 0, 0))
        set_bone("TentB", rot_xyz=(-12.0 * s, 0, 0))
        set_bone("TentL", rot_xyz=(0, 12.0 * c, 0))
        set_bone("TentR", rot_xyz=(0, -12.0 * c, 0))
        key_all(f, prev_q)

    # 2. Run & Walk (Fast aggressive tentacle slither with giant left claw cocked forward!)
    for clip_name, n_frames, mult in (("Run", 17, 1.0), ("Walk", 23, 0.72)):
        act_m = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s, c = math.sin(ang), math.cos(ang)
            set_bone("TentacleBase", loc=(0.04 * s * mult, 0, 0.06 * abs(s) * mult), rot_xyz=(-12.0 * mult, 6.0 * s, 8.0 * s))
            set_bone("Body", rot_xyz=(-10.0 * mult, 0, -12.0 * s))
            set_bone("Head", rot_xyz=(18.0 * mult, 0, 8.0 * s))
            set_bone("LeftArm1", rot_xyz=(0, 8.0 * s, -28.0 * mult + 10.0 * c))
            set_bone("LeftArm2", rot_xyz=(0, 0, -24.0 * mult))
            set_bone("LeftClawTop", rot_xyz=(0, -14.0 - 12.0 * abs(s), 0))
            set_bone("LeftClawBot", rot_xyz=(0,  14.0 + 12.0 * abs(s), 0))
            set_bone("RightArm", rot_xyz=(10.0 * s, 0, 18.0 * mult))
            set_bone("TentF", rot_xyz=(28.0 * s * mult, 0, 0))
            set_bone("TentB", rot_xyz=(-28.0 * s * mult, 0, 0))
            set_bone("TentL", rot_xyz=(0, 26.0 * c * mult, 0))
            set_bone("TentR", rot_xyz=(0, -26.0 * c * mult, 0))
            key_all(f, prev_q)
        if clip_name == "Run":
            act_run = act_m

    # 3. Hook (Frames 1..25 — DRAMATIC ELASTIC STRETCH OF GIANT LEFT HAND -> GRAB -> YANK BACK!)
    act_hook = new_action("Hook")
    prev_q = {}
    for f in range(1, 26):
        begin_frame(f)
        u = (f - 1) / 24.0
        if u < 0.22:
            # Windup coil back
            p = u / 0.22
            ext = 0.0
            yaw_arm = 32.0 * p
            claw_open = 24.0 * p
            body_yaw = -18.0 * p
        elif u < 0.45:
            # EXPLOSIVE ELASTIC SHOOT FORWARD (LeftArm1 & LeftArm2 stretch 2.6x forward!)
            p = (u - 0.22) / 0.23
            s = p * p * (3.0 - 2.0 * p)
            ext = s
            yaw_arm = 32.0 * (1.0 - s) - 68.0 * s
            claw_open = 30.0
            body_yaw = -18.0 + 38.0 * s
        elif u < 0.65:
            # SNAP GIANT CLAW SHUT ON PLAYER!
            p = (u - 0.45) / 0.20
            ext = 1.0
            yaw_arm = -68.0
            claw_open = 30.0 * (1.0 - p) - 18.0 * p
            body_yaw = 20.0
        else:
            # HEAVY YANK BACK!
            p = (u - 0.65) / 0.35
            s = p * p * (3.0 - 2.0 * p)
            ext = 1.0 - s
            yaw_arm = -68.0 * (1.0 - s)
            claw_open = -18.0 * (1.0 - s)
            body_yaw = 20.0 * (1.0 - s) - 14.0 * math.sin(s * math.pi)

        # Clean single-parent elastic stretch: LeftArm1 scales along bone-Y (2.75x),
        # LeftArm2 inherits LeftArm1's stretch straight (no compound 3.1*3.1=9.6x!),
        # and LeftGiantHand applies exact inverse scale so the GIANT CLAW stays crisp & chunky!
        stretch_y = 1.0 + 1.75 * ext
        thin_xz = 1.0 - 0.16 * ext
        hand_boost = 1.0 + 0.16 * ext

        set_bone("TentacleBase", loc=(0, -0.10 * ext, 0.04 * ext), rot_xyz=(-10.0 * ext, 0, body_yaw))
        set_bone("Body", rot_xyz=(-8.0 * ext, 0, body_yaw * 0.5))
        set_bone("Head", rot_xyz=(14.0 * ext, 0, -body_yaw * 0.6))
        set_bone("LeftArm1", rot_xyz=(0, 0, yaw_arm), scale=(thin_xz, stretch_y, thin_xz))
        set_bone("LeftArm2", rot_xyz=(0, 0, 0), scale=(1.0, 1.0, 1.0))
        set_bone("LeftGiantHand", rot_xyz=(0, 0, 0), scale=(hand_boost / thin_xz, hand_boost / stretch_y, hand_boost / thin_xz))
        set_bone("LeftClawTop", rot_xyz=(0, -claw_open, 0))
        set_bone("LeftClawBot", rot_xyz=(0,  claw_open, 0))
        set_bone("RightArm", rot_xyz=(0, 0, 26.0 * ext))
        key_all(f, prev_q)

    # 4. Slash1 (Heavy Giant Left Claw Sweep Smash!)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        s_stretch = 1.0 + 0.55 * sw
        set_bone("TentacleBase", loc=(0, -0.12 * sw, 0), rot_xyz=(-10.0 * sw, 0, 28.0 * sw))
        set_bone("LeftArm1", rot_xyz=(0, 0, -58.0 * sw), scale=(1.0, s_stretch, 1.0))
        set_bone("LeftArm2", rot_xyz=(0, 0, 0), scale=(1.0, 1.0, 1.0))
        set_bone("LeftGiantHand", rot_xyz=(0, 0, 0), scale=(1.0, 1.0 / s_stretch, 1.0))
        set_bone("LeftClawTop", rot_xyz=(0, -20.0 * sw, 0))
        set_bone("LeftClawBot", rot_xyz=(0,  20.0 * sw, 0))
        key_all(f, prev_q)

    # 5. Death (Collapse)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("TentacleBase", loc=(0, 0.20 * s, -0.34 * s), rot_xyz=(-82.0 * s, 10.0 * s, 14.0 * s))
        set_bone("LeftArm1", rot_xyz=(0, 0, 35.0 * s))
        set_bone("RightArm", rot_xyz=(0, 0, -35.0 * s))
        key_all(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [hook_mesh], "Alien_Hook")

    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-1.15, -4.85, 1.55))
    cam_obj.rotation_euler = (Vector((0.15, 0.0, 1.08)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Alien_Hook_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ALIEN_DIR, "Alien_Hook_Preview.png"))

    # Mid-Hook Elastic Stretch Pose (Frame 12 of Hook action — GIANT LEFT HAND SHOOTING OUT!)
    rig_obj.animation_data.action = act_hook
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_hook.slots) > 0:
        rig_obj.animation_data.action_slot = act_hook.slots[0]
    scene.frame_set(12)
    bpy.context.view_layer.update()
    cam_obj.location = Vector((-4.60, -4.25, 2.25))
    cam_obj.rotation_euler = (Vector((0.40, -1.05, 1.05)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_h = os.path.join(TMP_EXPORT_DIR, "Alien_Hook_Preview_Stretch.png")
    scene.render.filepath = tmp_h
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_h, os.path.join(ALIEN_DIR, "Alien_Hook_Preview_Stretch.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ALIEN SMART (BRAIN LIGHTBULB 4-LEG) & ALIEN HOOK (GIANT STRETCH LEFT HAND) ===")
    build_alien_smart()
    build_alien_hook()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ALIEN SMART & ALIEN HOOK EXPORTED & RENDERED! ===")

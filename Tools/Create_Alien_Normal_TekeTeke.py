import math
import os
import shutil
import sys
import time
import bpy
from mathutils import Vector, Euler, Matrix

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import ChibiAnimeKit as kit

OUT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "Assets", "GeneratedModels"))
ALIEN_DIR = os.path.join(OUT_ROOT, "Aliens")
TMP_EXPORT_DIR = os.path.join(SCRIPT_DIR, "_tmp_alien_normal_export")
os.makedirs(ALIEN_DIR, exist_ok=True)
os.makedirs(TMP_EXPORT_DIR, exist_ok=True)


def _copy_with_retry(src, dst, retries=12, delay=0.25):
    for attempt in range(retries):
        try:
            shutil.copy2(src, dst)
            return
        except OSError:
            if attempt == retries - 1:
                raise
            time.sleep(delay)


def make_fleshy_mat(name, rgba, roughness=0.34, metallic=0.0, coat=0.35, emit_rgb=None, emit_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if "Coat Weight" in bsdf.inputs:
        bsdf.inputs["Coat Weight"].default_value = coat
    if "Coat Roughness" in bsdf.inputs:
        bsdf.inputs["Coat Roughness"].default_value = 0.16
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.58
    if emit_rgb is not None and emit_strength > 0.0:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (emit_rgb[0], emit_rgb[1], emit_rgb[2], 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = emit_strength
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def _make_tube_chain(name, pts, radii, mat, segments=14):
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        pos = Vector(p)
        if i == 0:
            tangent = (Vector(pts[1]) - pos).normalized()
        elif i == n - 1:
            tangent = (pos - Vector(pts[i - 1])).normalized()
        else:
            tangent = (Vector(pts[i + 1]) - Vector(pts[i - 1])).normalized()
        if tangent.length < 1e-6:
            tangent = Vector((0, 0, 1))
        up_ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((0, 1, 0))
        side_a = tangent.cross(up_ref).normalized()
        side_b = tangent.cross(side_a).normalized()
        r = radii[i]
        rings.append((pos, side_a * r, side_b * r))
    return kit.create_loft_object(name, rings, mat, segments=segments)


def _setup_studio_render(scene):
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.render.film_transparent = False
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("AlienNormalStudioWorld")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.16, 0.22, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, energy, loc, rot in (
        ("KeyLight", 2.8, (-2.8, -3.5, 4.0), (0.88, 0.12, -0.58)),
        ("FillLight", 1.5, (3.2, -2.2, 2.4), (1.05, -0.1, 0.85)),
        ("RimLight", 2.2, (0.4, 3.6, 3.0), (-0.95, 0.0, 0.05)),
    ):
        ld = bpy.data.lights.new(lname, type="SUN")
        ld.energy = energy
        lo = bpy.data.objects.new(lname, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = rot

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 48.0
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    return cam_obj


def _build_bone_tree(arm_name, bone_defs):
    arm_data = bpy.data.armatures.new(arm_name)
    rig_obj = bpy.data.objects.new(arm_name, arm_data)
    bpy.context.scene.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode="EDIT")
    ebones = {}
    for name, head, tail, parent, roll_vec in bone_defs:
        eb = arm_data.edit_bones.new(name)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        if parent and parent in ebones:
            eb.parent = ebones[parent]
        if roll_vec is not None:
            eb.align_roll(Vector(roll_vec))
        ebones[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    return rig_obj


def _bind_parts_to_bones(rig_obj, parts_with_weights, mesh_name):
    objs = []
    for obj, bone_weights in parts_with_weights:
        if obj is None:
            continue
        if obj.type == "MESH" and obj.data and len(obj.data.polygons) > 0:
            obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
        for bname, w in bone_weights:
            vg = obj.vertex_groups.get(bname) or obj.vertex_groups.new(name=bname)
            vg.add(list(range(len(obj.data.vertices))), w, "REPLACE")
        objs.append(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    merged = bpy.context.active_object
    merged.name = mesh_name
    merged.parent = rig_obj
    mod = merged.modifiers.new("Armature", "ARMATURE")
    mod.object = rig_obj
    return merged


def _make_rig_helpers(scene, rig_obj):
    rig_obj.animation_data_create()
    bone_names = [b.name for b in rig_obj.pose.bones]

    def new_action(name):
        act = bpy.data.actions.new(name=name)
        act.use_fake_user = True
        rig_obj.animation_data.action = act
        if hasattr(rig_obj.animation_data, "action_slot") and len(act.slots) > 0:
            rig_obj.animation_data.action_slot = act.slots[0]
        return act

    def begin_frame(f):
        scene.frame_set(f)
        for pb in rig_obj.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.location = (0.0, 0.0, 0.0)
            pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            pb.scale = (1.0, 1.0, 1.0)

    def set_bone(name, loc=None, rot_xyz=None, scale=None):
        pb = rig_obj.pose.bones.get(name)
        if pb is None:
            return
        if loc is not None:
            pb.location = loc
        if rot_xyz is not None:
            e = Euler((math.radians(rot_xyz[0]), math.radians(rot_xyz[1]), math.radians(rot_xyz[2])), "XYZ")
            pb.rotation_quaternion = e.to_quaternion()
        if scale is not None:
            pb.scale = scale

    def key_all(f, prev_q):
        for bn in bone_names:
            pb = rig_obj.pose.bones[bn]
            q = pb.rotation_quaternion.copy()
            if bn in prev_q and prev_q[bn].dot(q) < 0.0:
                q = -q
                pb.rotation_quaternion = q
            prev_q[bn] = q.copy()
            pb.keyframe_insert(data_path="location", frame=f)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=f)
            pb.keyframe_insert(data_path="scale", frame=f)

    return begin_frame, set_bone, key_all, new_action


def _export_alien(rig_obj, mesh_objs, base_name):
    bpy.ops.object.select_all(action="DESELECT")
    rig_obj.select_set(True)
    for m in mesh_objs:
        m.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj

    tmp_fbx = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Rigged.fbx")
    tmp_blend = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Source.blend")
    final_fbx = os.path.join(ALIEN_DIR, f"{base_name}_Rigged.fbx")
    final_blend = os.path.join(ALIEN_DIR, f"{base_name}_Source.blend")

    bpy.ops.export_scene.fbx(
        filepath=tmp_fbx,
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
        axis_forward="-Z",
        axis_up="Y",
    )
    bpy.ops.wm.save_as_mainfile(filepath=tmp_blend)
    _copy_with_retry(tmp_fbx, final_fbx)
    _copy_with_retry(tmp_blend, final_blend)
    print(f"EXPORTED {base_name} FBX + BLEND OK!")


# ==============================================================================
#  ALIEN NORMAL — Sightless Worm (Eyeless Huge Head, Giant Toothy Mouth,
#  2 Arms, NO Legs) with Terrifying TEKE TEKE Hand-Crawling Locomotion!
# ==============================================================================

def build_alien_normal():
    scene = kit.reset_scene()

    # Sightless Worm fleshy organic palette (vivid salmon-coral worm skin, crimson veins/gums, ivory fangs!)
    m_skin       = make_fleshy_mat("Mat_AlienNormal_FleshSkin",   (0.92, 0.38, 0.43, 1.0), roughness=0.34, coat=0.45)
    m_skin_dark  = make_fleshy_mat("Mat_AlienNormal_FleshFold",   (0.72, 0.22, 0.28, 1.0), roughness=0.38, coat=0.40)
    m_vein       = make_fleshy_mat("Mat_AlienNormal_BloodVein",   (0.48, 0.08, 0.13, 1.0), roughness=0.30, coat=0.55)
    m_lip        = make_fleshy_mat("Mat_AlienNormal_GumLip",      (0.36, 0.05, 0.09, 1.0), roughness=0.22, coat=0.70)
    m_mouth_cav  = make_fleshy_mat("Mat_AlienNormal_MouthDark",   (0.14, 0.02, 0.04, 1.0), roughness=0.25, coat=0.50)
    m_tongue     = make_fleshy_mat("Mat_AlienNormal_Tongue",      (0.76, 0.16, 0.24, 1.0), roughness=0.18, coat=0.80)
    m_fang       = make_fleshy_mat("Mat_AlienNormal_BoneFang",    (0.93, 0.89, 0.72, 1.0), roughness=0.26, coat=0.30)
    m_claw       = make_fleshy_mat("Mat_AlienNormal_ClawTip",     (0.54, 0.18, 0.22, 1.0), roughness=0.32, coat=0.35)
    m_saliva     = make_fleshy_mat("Mat_AlienNormal_Saliva",      (0.90, 0.42, 0.48, 1.0), roughness=0.10, coat=0.95)

    # Character faces -Y, +Z is UP, +X is LEFT!
    # Low crouched legless posture: fleshy worm stump rests/drags on ground, 2 arms reach to ground for Teke Teke crawl!
    bone_defs = [
        ("Root",          (0.0,  0.0,   0.0),   (0.0,  0.0,   0.18),  None,           (0, 1, 0)),
        ("BodyBase",      (0.0,  0.22,  0.16),  (0.0,  0.10,  0.54),  "Root",         (0, 1, 0)),
        ("TailStump",     (0.0,  0.26,  0.14),  (0.0,  0.62,  0.08),  "BodyBase",     (0, 1, 0)),
        ("Spine",         (0.0,  0.10,  0.54),  (0.0, -0.02,  1.00),  "BodyBase",     (0, 1, 0)),
        ("Head",          (0.0, -0.02,  1.00),  (0.0, -0.58,  1.12),  "Spine",        (0, 1, 0)),
        ("JawTop",        (0.0, -0.14,  1.10),  (0.0, -0.82,  1.16),  "Head",         (0, 1, 0)),
        ("JawBot",        (0.0, -0.12,  0.92),  (0.0, -0.78,  0.80),  "Head",         (0, 1, 0)),

        # 2 Strong Crawling Arms (Teke Teke hand-walk — reach down & forward to pull the legless body!)
        ("LeftUpperArm",  (0.24, -0.02, 0.68),  (0.46, -0.18, 0.36),  "Spine",        (0, 1, 0)),
        ("LeftForeArm",   (0.46, -0.18, 0.36),  (0.42, -0.44, 0.06),  "LeftUpperArm", (0, 1, 0)),
        ("RightUpperArm", (-0.24, -0.02, 0.68), (-0.46, -0.18, 0.36), "Spine",        (0, 1, 0)),
        ("RightForeArm",  (-0.46, -0.18, 0.36), (-0.42, -0.44, 0.06), "RightUpperArm",(0, 1, 0)),
    ]
    rig_obj = _build_bone_tree("Alien_Normal_Rig", bone_defs)

    parts = []

    # --- 1. LEGLESS FLESHY WORM TORSO & SEVERED STUMP BASE (NO LEGS!) ---
    # Main curving arched Sightless Worm body from ground stump up to thick neck!
    torso_rings = [
        (Vector((0.0,  0.34, 0.08)), Vector((0.36, 0.0, 0.0)), Vector((0.0, 0.34, 0.0))),
        (Vector((0.0,  0.26, 0.24)), Vector((0.32, 0.0, 0.0)), Vector((0.0, 0.30, 0.0))),
        (Vector((0.0,  0.18, 0.44)), Vector((0.30, 0.0, 0.0)), Vector((0.0, 0.28, 0.0))),
        (Vector((0.0,  0.10, 0.66)), Vector((0.31, 0.0, 0.0)), Vector((0.0, 0.29, 0.0))),
        (Vector((0.0,  0.02, 0.88)), Vector((0.33, 0.0, 0.0)), Vector((0.0, 0.32, 0.0))),
        (Vector((0.0, -0.06, 1.06)), Vector((0.34, 0.0, 0.0)), Vector((0.0, 0.34, 0.0))),
    ]
    torso_obj = kit.create_loft_object("WormTorso", torso_rings, m_skin, segments=26)
    parts.append((torso_obj, [("BodyBase", 0.45), ("Spine", 0.55)]))

    # Squelchy fleshy puddle folds & severed legless worm stump trailing behind (Ref bottom rim + Teke Teke!)
    for idx, (ang_deg, rad_dist, r_x, r_y, r_z) in enumerate((
        (  0, 0.32, 0.14, 0.14, 0.08),
        ( 45, 0.34, 0.15, 0.13, 0.08),
        (-45, 0.34, 0.15, 0.13, 0.08),
        ( 95, 0.36, 0.16, 0.14, 0.09),
        (-95, 0.36, 0.16, 0.14, 0.09),
        (145, 0.40, 0.18, 0.16, 0.09),
        (-145, 0.40, 0.18, 0.16, 0.09),
        (180, 0.46, 0.22, 0.20, 0.10),
    )):
        rad = math.radians(ang_deg)
        lx = math.sin(rad) * rad_dist
        ly = 0.28 + math.cos(rad) * rad_dist * 0.85
        mat_l = m_skin if idx % 2 == 0 else m_skin_dark
        w_spec = [("TailStump", 0.75), ("BodyBase", 0.25)] if abs(ang_deg) >= 120 else [("BodyBase", 1.0)]
        parts.append((kit.create_ellipsoid(f"FleshStumpLobe_{idx}", (lx, ly, 0.075), (r_x, r_y, r_z), mat_l), w_spec))

    # Trailing wet torn worm-tail drag nub behind the legless torso
    tail_drag = _make_tube_chain(
        "LeglessTailDrag",
        [(0.0, 0.34, 0.14), (0.0, 0.54, 0.10), (0.0, 0.72, 0.06)],
        [0.24, 0.16, 0.07], m_skin_dark, segments=14
    )
    parts.append((tail_drag, [("TailStump", 0.85), ("BodyBase", 0.15)]))

    # Fleshy neck wrinkles & crimson organic vein webbing on chest/belly (exact Sightless Worm detail!)
    for fi, (fz, fy, rx, ry) in enumerate((
        (0.32, -0.06, 0.29, 0.065),
        (0.46, -0.11, 0.28, 0.062),
        (0.60, -0.15, 0.27, 0.060),
        (0.76, -0.20, 0.28, 0.065),
    )):
        parts.append((kit.create_ellipsoid(f"BellyFold_{fi}", (0.0, fy, fz), (rx, ry, 0.048), m_skin_dark), [("Spine", 0.7), ("BodyBase", 0.3)]))

    # Raised dark-crimson organic veins climbing up the belly & sides
    for side in (-1.0, 0.0, 1.0):
        v_pts = [
            (side * 0.16, 0.06 - abs(side) * 0.04, 0.20),
            (side * 0.22, -0.08 + abs(side) * 0.06, 0.44),
            (side * 0.20, -0.14 + abs(side) * 0.08, 0.68),
            (side * 0.14, -0.18 + abs(side) * 0.06, 0.88),
        ]
        parts.append((_make_tube_chain(f"BodyVein_{side}", v_pts, [0.022, 0.026, 0.024, 0.018], m_vein, segments=8), [("Spine", 0.65), ("BodyBase", 0.35)]))

    # --- 2. HUGE EYELESS SMOOTH ALIEN HEAD & MASSIVE GAPING TOOTHY JAW! (NO EYES!) ---
    # Smooth arched eyeless cranium/back-of-head curving massively forward (Sightless Worm iconic silhouette!)
    cranium_rings = [
        (Vector((0.0,  0.14, 1.06)), Vector((0.35, 0.0, 0.0)), Vector((0.0, 0.0, 0.32))),
        (Vector((0.0, -0.06, 1.18)), Vector((0.38, 0.0, 0.0)), Vector((0.0, 0.0, 0.30))),
        (Vector((0.0, -0.32, 1.24)), Vector((0.36, 0.0, 0.0)), Vector((0.0, 0.0, 0.24))),
        (Vector((0.0, -0.58, 1.22)), Vector((0.30, 0.0, 0.0)), Vector((0.0, 0.0, 0.16))),
        (Vector((0.0, -0.82, 1.17)), Vector((0.20, 0.0, 0.0)), Vector((0.0, 0.0, 0.09))),
        (Vector((0.0, -0.92, 1.13)), Vector((0.06, 0.0, 0.0)), Vector((0.0, 0.0, 0.03))),
    ]
    parts.append((kit.create_loft_object("EyelessHeadDome", cranium_rings, m_skin, segments=28), [("JawTop", 0.85), ("Head", 0.15)]))

    # Smooth seamless eyeless head back-dome (explicitly NO eyes, pure slick blind worm flesh!)
    parts.append((kit.create_ellipsoid("HeadBackDome", (0.0, 0.08, 1.16), (0.35, 0.34, 0.29), m_skin), [("Head", 1.0)]))

    # Deep dark crimson inner throat / mouth cavity
    parts.append((kit.create_ellipsoid("MouthCavity", (0.0, -0.28, 1.00), (0.27, 0.36, 0.18), m_mouth_cav), [("Head", 1.0)]))

    # Thick glossy dark-crimson UPPER gum / lip arch
    up_lip_pts = []
    for k in range(11):
        ang = math.radians(-82.0 + k * (164.0 / 10.0))
        ux = math.sin(ang) * 0.31
        uy = -0.22 - math.cos(ang) * 0.64
        uz = 1.08 + math.cos(ang) * 0.04
        up_lip_pts.append((ux, uy, uz))
    parts.append((_make_tube_chain("UpperGumLip", up_lip_pts, [0.055] * len(up_lip_pts), m_lip, segments=10), [("JawTop", 1.0)]))

    # HUGE ROW OF JAGGED UPPER FANGS (extra-long curved front canines like Sightless Worm!)
    for k in range(11):
        ang = math.radians(-74.0 + k * (148.0 / 10.0))
        fx = math.sin(ang) * 0.285
        fy = -0.23 - math.cos(ang) * 0.60
        fz_top = 1.09 + math.cos(ang) * 0.03
        # Front fangs are HUGE curved daggers!
        front_factor = math.cos(ang)
        fang_len = 0.085 + 0.105 * (front_factor ** 2)
        if k in (4, 6):
            fang_len += 0.045  # twin hero saber-fangs!
        fang_rad = 0.022 + 0.018 * front_factor
        f_pts = [
            (fx, fy, fz_top),
            (fx * 0.98, fy - 0.025 * front_factor, fz_top - fang_len * 0.55),
            (fx * 0.94, fy + 0.015 * front_factor, fz_top - fang_len),
        ]
        parts.append((_make_tube_chain(f"UpperFang_{k}", f_pts, [fang_rad, fang_rad * 0.62, 0.005], m_fang, segments=8), [("JawTop", 1.0)]))

    # MASSIVE HINGED LOWER JAW (protruding menacing underbite!)
    low_jaw_rings = [
        (Vector((0.0, -0.06, 0.88)), Vector((0.30, 0.0, 0.0)), Vector((0.0, 0.0, 0.14))),
        (Vector((0.0, -0.34, 0.80)), Vector((0.33, 0.0, 0.0)), Vector((0.0, 0.0, 0.13))),
        (Vector((0.0, -0.64, 0.78)), Vector((0.28, 0.0, 0.0)), Vector((0.0, 0.0, 0.10))),
        (Vector((0.0, -0.86, 0.82)), Vector((0.18, 0.0, 0.0)), Vector((0.0, 0.0, 0.07))),
        (Vector((0.0, -0.94, 0.86)), Vector((0.05, 0.0, 0.0)), Vector((0.0, 0.0, 0.03))),
    ]
    parts.append((kit.create_loft_object("LowerJawFlesh", low_jaw_rings, m_skin_dark, segments=22), [("JawBot", 1.0)]))

    # Thick dark-crimson LOWER lip / gum rim
    low_lip_pts = []
    for k in range(11):
        ang = math.radians(-82.0 + k * (164.0 / 10.0))
        lx = math.sin(ang) * 0.30
        ly = -0.20 - math.cos(ang) * 0.67
        lz = 0.86 - math.cos(ang) * 0.02
        low_lip_pts.append((lx, ly, lz))
    parts.append((_make_tube_chain("LowerGumLip", low_lip_pts, [0.058] * len(low_lip_pts), m_lip, segments=10), [("JawBot", 1.0)]))

    # HUGE ROW OF JAGGED LOWER FANGS (massive front underbite tusks!)
    for k in range(11):
        ang = math.radians(-74.0 + k * (148.0 / 10.0))
        fx = math.sin(ang) * 0.275
        fy = -0.21 - math.cos(ang) * 0.63
        fz_bot = 0.85 - math.cos(ang) * 0.015
        front_factor = math.cos(ang)
        fang_len = 0.080 + 0.115 * (front_factor ** 2)
        if k in (4, 6):
            fang_len += 0.055  # giant protruding lower tusks!
        fang_rad = 0.022 + 0.018 * front_factor
        f_pts = [
            (fx, fy, fz_bot),
            (fx * 0.98, fy - 0.02 * front_factor, fz_bot + fang_len * 0.55),
            (fx * 0.93, fy + 0.02 * front_factor, fz_bot + fang_len),
        ]
        parts.append((_make_tube_chain(f"LowerFang_{k}", f_pts, [fang_rad, fang_rad * 0.62, 0.005], m_fang, segments=8), [("JawBot", 1.0)]))

    # Glistening crimson fleshy tongue inside the gaping mouth!
    tongue_pts = [
        (0.0, -0.26, 0.90),
        (0.0, -0.50, 0.91),
        (0.0, -0.70, 0.88),
        (0.0, -0.80, 0.85),
    ]
    parts.append((_make_tube_chain("AlienTongue", tongue_pts, [0.11, 0.10, 0.075, 0.032], m_tongue, segments=12), [("JawBot", 0.85), ("Head", 0.15)]))

    # Glossy drool / saliva strings stretching between upper & lower jaw corners (Ref Sightless Worm detail!)
    for side in (-1.0, 1.0):
        for si, (sy, sx_m) in enumerate(((-0.46, 0.24), (-0.64, 0.17))):
            s_pts = [
                (side * sx_m, sy, 1.08),
                (side * (sx_m - 0.015), sy, 0.97),
                (side * sx_m, sy, 0.86),
            ]
            parts.append((_make_tube_chain(f"Saliva_{side}_{si}", s_pts, [0.011, 0.005, 0.011], m_saliva, segments=6), [("JawTop", 0.5), ("JawBot", 0.5)]))

    # --- 3. TWO STRONG TEKE-TEKE CRAWLING ARMS & CLAWED HANDS! ---
    for side, up_b, fore_b in ((1.0, "LeftUpperArm", "LeftForeArm"), (-1.0, "RightUpperArm", "RightForeArm")):
        # Thick fleshy shoulder mound
        parts.append((kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.27, -0.02, 0.68), (0.12, 0.13, 0.12), m_skin), [("Spine", 0.55), (up_b, 0.45)]))
        # Muscular fleshy upper arm reaching down & outward
        up_arm = _make_tube_chain(
            f"UpperArm_{side}",
            [(side * 0.24, -0.02, 0.68), (side * 0.37, -0.10, 0.52), (side * 0.46, -0.18, 0.36)],
            [0.095, 0.084, 0.078], m_skin, segments=14
        )
        parts.append((up_arm, [(up_b, 1.0)]))
        parts.append((kit.create_ellipsoid(f"Elbow_{side}", (side * 0.46, -0.18, 0.36), (0.082, 0.082, 0.082), m_skin_dark), [(up_b, 0.5), (fore_b, 0.5)]))

        # Forearm reaching forward onto the ground to drag the legless torso!
        fore_arm = _make_tube_chain(
            f"ForeArm_{side}",
            [(side * 0.46, -0.18, 0.36), (side * 0.44, -0.32, 0.20), (side * 0.42, -0.44, 0.08)],
            [0.078, 0.070, 0.068], m_skin, segments=14
        )
        parts.append((fore_arm, [(fore_b, 1.0)]))

        # Splayed fleshy palm planted on floor + 4 crawling claws!
        palm_pos = Vector((side * 0.42, -0.48, 0.065))
        parts.append((kit.create_ellipsoid(f"HandPalm_{side}", palm_pos, (0.105, 0.115, 0.055), m_skin_dark), [(fore_b, 1.0)]))
        for fi, (fx, fy) in enumerate((
            (side * 0.08, -0.12),
            (side * 0.02, -0.16),
            (-side * 0.05, -0.15),
            (-side * 0.10, -0.09),
        )):
            f_start = palm_pos + Vector((fx * 0.55, fy * 0.55, 0.005))
            f_mid   = palm_pos + Vector((fx * 1.15, fy * 1.15, 0.015))
            f_tip   = palm_pos + Vector((fx * 1.65, fy * 1.65, -0.045))
            parts.append((_make_tube_chain(f"Finger_{side}_{fi}", [f_start, f_mid, f_tip], [0.028, 0.022, 0.008], m_claw, segments=8), [(fore_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, "AlienNormal_Body")

    # ==========================================================================
    #  ANIMATIONS — SIGNATURE TEKE TEKE HAND-CRAWL LOCOMOTION!
    # ==========================================================================
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    # 1. Idle (Creepy low crouch on 2 hands + legless stump, blind head swaying & gaping jaw snapping!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("BodyBase", loc=(0, 0, 0.02 * s), rot_xyz=(-4.0 + 3.0 * s, 4.0 * c, 3.0 * s))
        set_bone("Spine", rot_xyz=(-6.0 + 4.0 * c, 0, -5.0 * s))
        # Blind eyeless sniffing/sensing sway!
        set_bone("Head", rot_xyz=(4.0 * s, 6.0 * c, 12.0 * s))
        set_bone("JawTop", rot_xyz=(-4.0 - 4.0 * abs(s), 0, 0))
        set_bone("JawBot", rot_xyz=(10.0 + 16.0 * (0.5 + 0.5 * s), 0, 0))
        set_bone("TailStump", rot_xyz=(0, 0, 8.0 * c))
        # Hands planted steadily on ground gripping slightly
        set_bone("LeftUpperArm", rot_xyz=(-4.0 * s, 0, -5.0 * c))
        set_bone("LeftForeArm", rot_xyz=(4.0 * s, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(4.0 * s, 0, 5.0 * c))
        set_bone("RightForeArm", rot_xyz=(-4.0 * s, 0, 0))
        key_all(f, prev_q)

    # 2. Walk (Eerie alternating TEKE TEKE hand-over-hand legless drag!)
    act_walk = new_action("Walk")
    prev_q = {}
    for f in range(1, 25):
        begin_frame(f)
        ang = ((f - 1) / 23.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        # Body hunkers low, propped up slightly on its hauling arms while dragging legless stump!
        lurch = abs(s)
        set_bone("BodyBase", loc=(0.05 * s, -0.08 * lurch, 0.04 + 0.06 * lurch), rot_xyz=(-12.0 - 4.0 * lurch, 10.0 * s, 14.0 * s))
        set_bone("Spine", rot_xyz=(-4.0 + 4.0 * lurch, -5.0 * s, -10.0 * s))
        set_bone("Head", rot_xyz=(12.0, 4.0 * c, -8.0 * s))
        set_bone("JawTop", rot_xyz=(-5.0 - 4.0 * lurch, 0, 0))
        set_bone("JawBot", rot_xyz=(14.0 + 16.0 * lurch, 0, 0))
        set_bone("TailStump", rot_xyz=(-6.0, 0, -22.0 * s))

        # Alternating LOW floor-slapping Teke Teke arm strokes:
        # NEGATIVE X (-34 deg) reaches low & far FORWARD along the ground; POSITIVE X (+36 deg) hauls BACKWARD!
        l_swing = -32.0 * s + 4.0
        r_swing =  32.0 * s + 4.0
        l_out   = -12.0 - max(0.0, c) * 14.0
        r_out   =  12.0 + max(0.0, -c) * 14.0
        set_bone("LeftUpperArm", rot_xyz=(l_swing, l_out, -10.0 * s))
        set_bone("LeftForeArm", rot_xyz=(12.0 + 18.0 * max(0.0, -s) - 6.0 * max(0.0, s), 0, 0))
        set_bone("RightUpperArm", rot_xyz=(r_swing, r_out, -10.0 * s))
        set_bone("RightForeArm", rot_xyz=(12.0 + 18.0 * max(0.0, s) - 6.0 * max(0.0, -s), 0, 0))
        key_all(f, prev_q)

    # 3. Run (FRANTIC TERRIFYING FAST TEKE TEKE SKITTER-GALLOP ON 2 HANDS!!)
    act_run = new_action("Run")
    prev_q = {}
    for f in range(1, 15):
        begin_frame(f)
        ang = ((f - 1) / 13.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        # Fast frantic Teke Teke alternating slap-and-drag sprint: low to floor, hands slapping & pulling!
        hop = abs(s)
        set_bone("BodyBase", loc=(0.06 * s, -0.14 * hop, 0.04 + 0.08 * hop), rot_xyz=(-14.0 - 4.0 * hop, 14.0 * s, 18.0 * s))
        set_bone("Spine", rot_xyz=(-4.0 + 5.0 * hop, -6.0 * s, -14.0 * s))
        set_bone("Head", rot_xyz=(12.0, 6.0 * c, -10.0 * s))
        # Wide screaming toothy mouth snapping furiously as it sprints on its hands!
        set_bone("JawTop", rot_xyz=(-8.0 - 7.0 * hop, 0, 0))
        set_bone("JawBot", rot_xyz=(18.0 + 22.0 * hop, 0, 0))
        set_bone("TailStump", rot_xyz=(-10.0, 0, -28.0 * s))

        # Aggressive LOW floor-clawing Teke Teke arm reach & haul strokes (hands stay low slapping the ground!)
        l_swing = -38.0 * s + 4.0
        r_swing =  38.0 * s + 4.0
        l_out   = -16.0 - max(0.0, c) * 16.0
        r_out   =  16.0 + max(0.0, -c) * 16.0
        set_bone("LeftUpperArm", rot_xyz=(l_swing, l_out, -12.0 * s))
        set_bone("LeftForeArm", rot_xyz=(16.0 + 22.0 * max(0.0, -s) - 6.0 * max(0.0, s), 0, 0))
        set_bone("RightUpperArm", rot_xyz=(r_swing, r_out, -12.0 * s))
        set_bone("RightForeArm", rot_xyz=(16.0 + 22.0 * max(0.0, s) - 6.0 * max(0.0, -s), 0, 0))
        key_all(f, prev_q)

    # 4. Slash1 (Rear up -> GIANT EYELESS JAW CHOMP + Double Claw Lunge!)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        if u < 0.38:
            # Windup rear back & open HUGE toothy maw wide!
            p = u / 0.38
            s = p * p * (3.0 - 2.0 * p)
            set_bone("BodyBase", loc=(0, 0.12 * s, 0.08 * s), rot_xyz=(18.0 * s, 0, 0))
            set_bone("Spine", rot_xyz=(14.0 * s, 0, 0))
            set_bone("Head", rot_xyz=(10.0 * s, 0, 0))
            set_bone("JawTop", rot_xyz=(-18.0 * s, 0, 0))
            set_bone("JawBot", rot_xyz=(44.0 * s, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-38.0 * s, -34.0 * s, -22.0 * s))
            set_bone("RightUpperArm", rot_xyz=(-38.0 * s, 34.0 * s, 22.0 * s))
        elif u < 0.64:
            # VIOLENT FORWARD BITE SNAP + CLAW RAKE!
            p = (u - 0.38) / 0.26
            s = p * p * (3.0 - 2.0 * p)
            set_bone("BodyBase", loc=(0, 0.12 - 0.38 * s, 0.08 - 0.12 * s), rot_xyz=(18.0 - 46.0 * s, 0, 0))
            set_bone("Spine", rot_xyz=(14.0 - 34.0 * s, 0, 0))
            set_bone("Head", rot_xyz=(10.0 - 20.0 * s, 0, 0))
            set_bone("JawTop", rot_xyz=(-18.0 * (1.0 - s), 0, 0))
            set_bone("JawBot", rot_xyz=(44.0 * (1.0 - s) - 10.0 * s, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-38.0 + 70.0 * s, -34.0 * (1.0 - s), -22.0 + 48.0 * s))
            set_bone("RightUpperArm", rot_xyz=(-38.0 + 70.0 * s, 34.0 * (1.0 - s), 22.0 - 48.0 * s))
        else:
            # Recover back to crouch
            p = (u - 0.64) / 0.36
            s = 1.0 - (p * p * (3.0 - 2.0 * p))
            set_bone("BodyBase", loc=(0, -0.26 * s, -0.04 * s), rot_xyz=(-28.0 * s, 0, 0))
            set_bone("Spine", rot_xyz=(-20.0 * s, 0, 0))
            set_bone("Head", rot_xyz=(-10.0 * s, 0, 0))
            set_bone("JawBot", rot_xyz=(-10.0 * s, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(32.0 * s, 0, 26.0 * s))
            set_bone("RightUpperArm", rot_xyz=(32.0 * s, 0, -26.0 * s))
        key_all(f, prev_q)

    # 5. Death (Collapse flat on side with giant jaw hanging limp!)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("BodyBase", loc=(0.14 * s, 0.16 * s, -0.10 * s), rot_xyz=(-20.0 * s, 74.0 * s, 18.0 * s))
        set_bone("Spine", rot_xyz=(-12.0 * s, 14.0 * s, 0))
        set_bone("JawBot", rot_xyz=(34.0 * s, 0, 0))
        set_bone("LeftUpperArm", rot_xyz=(-40.0 * s, -28.0 * s, 0))
        set_bone("RightUpperArm", rot_xyz=(22.0 * s, 35.0 * s, 0))
        key_all(f, prev_q)

    # Export FBX & .blend on Idle frame 1
    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [body_mesh], "Alien_Normal")

    # Render Preview 1: 3/4 Front-Side (Shows HUGE eyeless head, gaping toothy mouth, 2 hands, NO legs!)
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-2.45, -3.15, 1.40))
    cam_obj.rotation_euler = (Vector((0.0, -0.15, 0.72)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Alien_Normal_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ALIEN_DIR, "Alien_Normal_Preview.png"))

    # Render Preview 2: Mid-Teke Teke Crawl Run Pose (Frame 4 — LEFT arm reaching WAY forward, RIGHT arm hauling legless body!)
    rig_obj.animation_data.action = act_run
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_run.slots) > 0:
        rig_obj.animation_data.action_slot = act_run.slots[0]
    scene.frame_set(4)
    bpy.context.view_layer.update()
    cam_obj.location = Vector((2.95, -2.85, 1.45))
    cam_obj.rotation_euler = (Vector((0.0, -0.25, 0.68)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_r = os.path.join(TMP_EXPORT_DIR, "Alien_Normal_Preview_TekeTeke.png")
    scene.render.filepath = tmp_r
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_r, os.path.join(ALIEN_DIR, "Alien_Normal_Preview_TekeTeke.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ALIEN NORMAL (SIGHTLESS WORM + TEKE TEKE HAND-CRAWL) ===")
    build_alien_normal()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ALIEN NORMAL EXPORTED & RENDERED! ===")

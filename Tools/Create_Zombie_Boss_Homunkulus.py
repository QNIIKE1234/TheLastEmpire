import math
import os
import shutil
import sys
import time
import bpy
from mathutils import Vector, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import ChibiAnimeKit as kit

OUT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "Assets", "GeneratedModels"))
ZOMBIE_DIR = os.path.join(OUT_ROOT, "Zombies")
TMP_EXPORT_DIR = os.path.join(SCRIPT_DIR, "_tmp_zombie_boss_export")
os.makedirs(ZOMBIE_DIR, exist_ok=True)
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


def make_mat(name, rgba, roughness=0.72, metallic=0.0, coat=0.0, emit_rgb=None, emit_strength=0.0):
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
        world = bpy.data.worlds.new("ZombieBossStudioWorld")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.16, 0.18, 0.24, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, energy, loc, rot in (
        ("KeyLight", 2.9, (-3.0, -3.8, 4.4), (0.88, 0.12, -0.58)),
        ("FillLight", 1.7, (3.4, -2.5, 2.8), (1.05, -0.1, 0.85)),
        ("RimLight", 2.4, (0.4, 3.8, 3.4), (-0.95, 0.0, 0.05)),
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


def _export_zombie(rig_obj, mesh_objs, base_name):
    bpy.ops.object.select_all(action="DESELECT")
    rig_obj.select_set(True)
    for m in mesh_objs:
        m.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj

    tmp_fbx = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Rigged.fbx")
    tmp_blend = os.path.join(TMP_EXPORT_DIR, f"{base_name}_Source.blend")
    final_fbx = os.path.join(ZOMBIE_DIR, f"{base_name}_Rigged.fbx")
    final_blend = os.path.join(ZOMBIE_DIR, f"{base_name}_Source.blend")

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
#  ZOMBIE BOSS — ANCIENT HOMUNKULUS (Ref 2!)
#  Massive hunched teal-corpse brute, swollen forehead, spiky ginger hair,
#  red scarf, purple ragged tunic, ochre pants, giant gorilla arms,
#  + GIANT PARASITIC BLOODSHOT EYEBALL & CRIMSON-TIPPED TENTACLES ON HIS BACK!
# ==============================================================================

def build_zombie_boss_homunkulus():
    scene = kit.reset_scene()

    # Exact Ancient Homunkulus palette!
    m_skin       = make_mat("Mat_Boss_TealSkin",       (0.36, 0.68, 0.62, 1.0), roughness=0.58)
    m_skin_dark  = make_mat("Mat_Boss_TealDark",       (0.20, 0.44, 0.42, 1.0), roughness=0.64)
    m_pustule    = make_mat("Mat_Boss_AcidGlow",       (0.45, 0.92, 0.36, 1.0), emit_rgb=(0.35, 0.88, 0.25), emit_strength=0.90, roughness=0.22)
    m_hair       = make_mat("Mat_Boss_GingerHair",     (0.66, 0.38, 0.14, 1.0), roughness=0.78)
    m_scarf      = make_mat("Mat_Boss_RedScarf",       (0.76, 0.12, 0.14, 1.0), roughness=0.68)
    m_vest       = make_mat("Mat_Boss_PurpleVest",     (0.34, 0.16, 0.48, 1.0), roughness=0.74)
    m_sash       = make_mat("Mat_Boss_NavySash",       (0.10, 0.14, 0.28, 1.0), roughness=0.70)
    m_pants      = make_mat("Mat_Boss_OchrePants",     (0.62, 0.36, 0.10, 1.0), roughness=0.76)
    m_shackle    = make_mat("Mat_Boss_GoldShackle",    (0.82, 0.64, 0.16, 1.0), roughness=0.32, metallic=0.65)
    m_eye_white  = make_mat("Mat_Boss_EyeGlossy",      (0.97, 0.88, 0.90, 1.0), roughness=0.08, coat=0.90)
    m_eye_red    = make_mat("Mat_Boss_EyeRed",         (0.84, 0.08, 0.10, 1.0), emit_rgb=(0.75, 0.06, 0.08), emit_strength=0.55, roughness=0.14)
    m_pupil      = make_mat("Mat_Boss_Pupil",          (0.06, 0.02, 0.03, 1.0), roughness=0.05)
    m_fang       = make_mat("Mat_Boss_YellowFang",     (0.88, 0.64, 0.18, 1.0), roughness=0.30)
    m_tent_tip   = make_mat("Mat_Boss_TentacleRed",    (0.88, 0.10, 0.12, 1.0), emit_rgb=(0.82, 0.08, 0.10), emit_strength=0.55, roughness=0.22)
    m_nail       = make_mat("Mat_Boss_Nail",           (0.84, 0.88, 0.82, 1.0), roughness=0.26)

    # Faces -Y, +Z UP, +X LEFT!
    # Massive hunched gorilla-brute (~2.25m tall including back parasite eyeball!)
    bone_defs = [
        ("Root",           (0.0,  0.0,   0.0),   (0.0,  0.0,   0.22),  None,          (0, 1, 0)),
        ("Hips",           (0.0,  0.14,  0.76),  (0.0,  0.10,  1.06),  "Root",        (0, 1, 0)),
        ("Spine",          (0.0,  0.10,  1.06),  (0.0, -0.14,  1.62),  "Hips",        (0.0, 1.0, 0.0)),
        ("Head",           (0.0, -0.24,  1.48),  (0.0, -0.58,  1.76),  "Spine",       (0, 1, 0)),

        # SIGNATURE BACK PARASITE EYEBALL + WRITHING TENTACLES!
        ("ParasiteBase",   (0.0,  0.22,  1.58),  (0.0,  0.34,  2.14),  "Spine",       (0, 1, 0)),
        ("ParasiteTentL",  (0.18, 0.30,  1.96),  (0.56, 0.42,  2.18),  "ParasiteBase",(0, 1, 0)),
        ("ParasiteTentR",  (-0.18,0.30,  1.96),  (-0.56,0.42,  2.18),  "ParasiteBase",(0, 1, 0)),
        ("ParasiteTentB",  (0.0,  0.40,  1.92),  (0.0,  0.78,  2.06),  "ParasiteBase",(0, 1, 0)),

        # HUGE HUNCHED GORILLA-MUSCULAR BRUTE ARMS (reaching down near ground!)
        ("LeftUpperArm",   (0.48, -0.06, 1.48),  (0.58, -0.16, 0.88),  "Spine",       (0, 1, 0)),
        ("LeftForeArm",    (0.58, -0.16, 0.88),  (0.54, -0.34, 0.34),  "LeftUpperArm",(0, 1, 0)),
        ("RightUpperArm",  (-0.48,-0.06, 1.48),  (-0.58,-0.16, 0.88),  "Spine",       (0, 1, 0)),
        ("RightForeArm",   (-0.58,-0.16, 0.88),  (-0.54,-0.34, 0.34),  "RightUpperArm",(0, 1, 0)),

        # STURDY CROUCHED LEGS + BARE CORPSE FEET
        ("LeftThigh",      (0.24,  0.14, 0.76),  (0.28, -0.02, 0.40),  "Hips",        (0, 1, 0)),
        ("LeftShin",       (0.28, -0.02, 0.40),  (0.28,  0.06, 0.08),  "LeftThigh",   (0, 1, 0)),
        ("RightThigh",     (-0.24, 0.14, 0.76),  (-0.28,-0.02, 0.40),  "Hips",        (0, 1, 0)),
        ("RightShin",      (-0.28,-0.02, 0.40),  (-0.28, 0.06, 0.08),  "RightThigh",  (0, 1, 0)),
    ]
    rig_obj = _build_bone_tree("Chibi_ZombieBoss_Rig", bone_defs)

    parts = []

    # --- 1. HUNCHED TORSO IN TATTERED PURPLE VEST + NAVY SASH + RED NECK SCARF ---
    torso_rings = [
        (Vector((0.0,  0.14, 0.76)), Vector((0.40, 0.0, 0.0)), Vector((0.0, 0.32, 0.0))),
        (Vector((0.0,  0.08, 1.06)), Vector((0.46, 0.0, 0.0)), Vector((0.0, 0.38, 0.0))),
        (Vector((0.0, -0.04, 1.44)), Vector((0.52, 0.0, 0.0)), Vector((0.0, 0.44, 0.0))),
        (Vector((0.0, -0.10, 1.66)), Vector((0.42, 0.0, 0.0)), Vector((0.0, 0.36, 0.0))),
    ]
    parts.append((kit.create_loft_object("Boss_PurpleVest", torso_rings, m_vest, segments=22), [("Spine", 0.75), ("Hips", 0.25)]))

    # Tattered jagged shoulder vest fringes (exact Ref 2 purple shoulder rags!)
    for side in (-1.0, 1.0):
        for ti, y_off in enumerate((-0.14, 0.0, 0.14)):
            parts.append((_make_tube_chain(f"VestTatter_{side}_{ti}", [(side * 0.42, y_off, 1.54), (side * 0.58, y_off + 0.02, 1.64)], [0.065, 0.012], m_vest, segments=8), [("Spine", 1.0)]))

    # Dark navy waist sash belt + hanging cloth tie
    parts.append((kit.create_torus_ring("NavyWaistSash", (0.0, 0.12, 0.86), 0.38, 0.068, m_sash), [("Hips", 1.0)]))
    parts.append((_make_tube_chain("SashTail", [(0.16, -0.20, 0.86), (0.20, -0.26, 0.62), (0.18, -0.24, 0.46)], [0.055, 0.048, 0.020], m_sash, segments=10), [("Hips", 1.0)]))

    # Torn Crimson Red Neck Scarf wrapping around the hunched neck!
    parts.append((kit.create_torus_ring("RedNeckScarf", (0.0, -0.26, 1.32), 0.28, 0.088, m_scarf, rot_euler=(0.35, 0.0, 0.0)), [("Spine", 0.65), ("Head", 0.35)]))
    parts.append((_make_tube_chain("ScarfEnd", [(-0.18, -0.44, 1.24), (-0.28, -0.48, 1.06), (-0.34, -0.46, 0.94)], [0.075, 0.062, 0.018], m_scarf, segments=10), [("Spine", 1.0)]))

    # --- 2. SWOLLEN BULBOUS TEAL CORPSE HEAD + SPIKY GINGER HAIR + HOOKED NOSE + YELLOW FANGS! ---
    # Heavy jutting hunched head
    parts.append((kit.create_ellipsoid("Boss_Skull", (0.0, -0.46, 1.60), (0.28, 0.30, 0.28), m_skin), [("Head", 1.0)]))
    # HUGE swollen bulbous forehead dome (exact Ancient Homunkulus iconic forehead!)
    parts.append((kit.create_ellipsoid("SwollenForehead", (0.0, -0.56, 1.78), (0.26, 0.24, 0.20), m_skin), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("BrowLump", (0.0, -0.68, 1.72), (0.22, 0.14, 0.10), m_skin), [("Head", 1.0)]))
    # Heavy square underbite jaw + grimacing lips + crooked yellow fangs
    parts.append((kit.create_rounded_box("Boss_Jaw", (0.0, -0.56, 1.40), (0.38, 0.26, 0.18), m_skin_dark, bevel_width=0.045), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("MouthGrimace", (0.0, -0.69, 1.44), (0.16, 0.035, 0.032), m_pupil), [("Head", 1.0)]))
    for side in (-1.0, 1.0):
        parts.append((_make_tube_chain(f"YellowFang_{side}", [(side * 0.10, -0.68, 1.42), (side * 0.11, -0.71, 1.50)], [0.026, 0.008], m_fang, segments=8), [("Head", 1.0)]))
    # Hooked bulbous corpse nose!
    parts.append((_make_tube_chain("HookedNose", [(0.0, -0.68, 1.62), (0.0, -0.82, 1.55), (0.0, -0.76, 1.48)], [0.068, 0.058, 0.036], m_skin, segments=10), [("Head", 1.0)]))
    # Small crazed bloodshot eyes under the massive swollen brow
    for side in (-1.0, 1.0):
        parts.append((kit.create_ellipsoid(f"BossEyeSocket_{side}", (side * 0.13, -0.68, 1.62), (0.068, 0.045, 0.060), m_skin_dark), [("Head", 1.0)]))
        parts.append((kit.create_ellipsoid(f"BossEyeWhite_{side}", (side * 0.13, -0.715, 1.62), (0.052, 0.032, 0.050), m_eye_white), [("Head", 1.0)]))
        parts.append((kit.create_ellipsoid(f"BossEyePupil_{side}", (side * 0.13, -0.742, 1.62), (0.022, 0.014, 0.022), m_eye_red), [("Head", 1.0)]))

    # Wild spiky golden-brown/ginger hair sweeping back from sides & crown (exact Ref 2!)
    for hi, (hx, hy, hz, tx, ty, tz, rad) in enumerate((
        (0.00, -0.44, 1.92,  0.00, -0.10, 1.98, 0.095),
        (0.16, -0.44, 1.88,  0.26, -0.10, 1.92, 0.085),
        (-0.16,-0.44, 1.88, -0.26, -0.10, 1.92, 0.085),
        (0.24, -0.42, 1.76,  0.34, -0.06, 1.76, 0.082),
        (-0.24,-0.42, 1.76, -0.34, -0.06, 1.76, 0.082),
        (0.25, -0.40, 1.62,  0.32, -0.08, 1.56, 0.075),
        (-0.25,-0.40, 1.62, -0.32, -0.08, 1.56, 0.075),
    )):
        parts.append((_make_tube_chain(f"GingerHair_{hi}", [(hx, hy, hz), (tx, ty, tz)], [rad, 0.015], m_hair, segments=10), [("Head", 1.0)]))

    # --- 3. SIGNATURE BACK PARASITE: GIANT BLOODSHOT EYEBALL + 10 CRIMSON-TIPPED TENTACLES! ---
    # Fleshy teal mutant tumor hump rising from his hunched upper back!
    para_rings = [
        (Vector((0.0, 0.24, 1.46)), Vector((0.36, 0.0, 0.0)), Vector((0.0, 0.34, 0.0))),
        (Vector((0.0, 0.30, 1.76)), Vector((0.32, 0.0, 0.0)), Vector((0.0, 0.30, 0.0))),
        (Vector((0.0, 0.32, 2.02)), Vector((0.26, 0.0, 0.0)), Vector((0.0, 0.25, 0.0))),
    ]
    parts.append((kit.create_loft_object("ParasiteHump", para_rings, m_skin_dark, segments=20), [("ParasiteBase", 0.8), ("Spine", 0.2)]))
    # GIANT BLOODSHOT WHITE-PINK PARASITE EYEBALL ON TOP OF BACK HUMP!
    parts.append((kit.create_ellipsoid("ParasiteEyeRim", (0.0, 0.30, 2.06), (0.25, 0.24, 0.16), m_skin), [("ParasiteBase", 1.0)]))
    parts.append((kit.create_ellipsoid("ParasiteGiantEye", (0.0, 0.29, 2.16), (0.21, 0.21, 0.18), m_eye_white), [("ParasiteBase", 1.0)]))
    parts.append((kit.create_ellipsoid("ParasiteEyeIris", (0.0, 0.20, 2.28), (0.088, 0.088, 0.062), m_eye_red), [("ParasiteBase", 1.0)]))
    parts.append((kit.create_ellipsoid("ParasiteEyePupil", (0.0, 0.18, 2.33), (0.042, 0.042, 0.032), m_pupil), [("ParasiteBase", 1.0)]))

    # 10 Writhing Teal-to-Crimson-Tipped Parasitic Tentacles bursting out around the giant eyeball!
    tent_specs = [
        ( 0.22, 0.22, 2.06,  0.54, 0.16, 2.24, "ParasiteTentL"),
        ( 0.26, 0.32, 2.02,  0.64, 0.36, 2.12, "ParasiteTentL"),
        ( 0.24, 0.42, 1.94,  0.58, 0.56, 1.88, "ParasiteTentL"),
        ( 0.18, 0.28, 2.14,  0.38, 0.24, 2.48, "ParasiteTentL"),
        (-0.22, 0.22, 2.06, -0.54, 0.16, 2.24, "ParasiteTentR"),
        (-0.26, 0.32, 2.02, -0.64, 0.36, 2.12, "ParasiteTentR"),
        (-0.24, 0.42, 1.94, -0.58, 0.56, 1.88, "ParasiteTentR"),
        (-0.18, 0.28, 2.14, -0.38, 0.24, 2.48, "ParasiteTentR"),
        ( 0.08, 0.46, 2.00,  0.14, 0.84, 2.16, "ParasiteTentB"),
        (-0.08, 0.46, 2.00, -0.14, 0.84, 2.16, "ParasiteTentB"),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, bname) in enumerate(tent_specs):
        xm, ym, zm = (x0 + x1) * 0.52, (y0 + y1) * 0.52, (z0 + z1) * 0.52 + 0.06
        # Teal fleshy base half
        parts.append((_make_tube_chain(f"ParaTentBase_{idx}", [(x0, y0, z0), (xm, ym, zm)], [0.052, 0.036], m_skin, segments=10), [(bname, 0.7), ("ParasiteBase", 0.3)]))
        # Crimson-red sharp tip half (exact Ref 2 red-tipped tentacles!)
        parts.append((_make_tube_chain(f"ParaTentTip_{idx}", [(xm, ym, zm), (x1, y1, z1)], [0.036, 0.008], m_tent_tip, segments=10), [(bname, 1.0)]))

    # --- 4. ENORMOUS HUNCHED GORILLA-MUSCULAR TEAL ARMS & GIANT CORPSE HANDS! ---
    for side, up_b, fore_b in ((1.0, "LeftUpperArm", "LeftForeArm"), (-1.0, "RightUpperArm", "RightForeArm")):
        # Massive bulging shoulder & bicep
        parts.append((kit.create_ellipsoid(f"BossShoulder_{side}", (side * 0.50, -0.06, 1.44), (0.22, 0.22, 0.24), m_skin), [(up_b, 0.7), ("Spine", 0.3)]))
        up_arm = _make_tube_chain(
            f"BossUpperArm_{side}",
            [(side * 0.48, -0.06, 1.44), (side * 0.55, -0.12, 1.16), (side * 0.58, -0.16, 0.88)],
            [0.19, 0.175, 0.165], m_skin, segments=16
        )
        fore_arm = _make_tube_chain(
            f"BossForeArm_{side}",
            [(side * 0.58, -0.16, 0.88), (side * 0.57, -0.26, 0.60), (side * 0.54, -0.34, 0.34)],
            [0.185, 0.21, 0.18], m_skin, segments=16
        )
        parts.append((up_arm, [(up_b, 1.0)]))
        parts.append((fore_arm, [(fore_b, 1.0)]))
        # Acid pustule sores on arms
        parts.append((kit.create_ellipsoid(f"ArmPustule_{side}", (side * 0.68, -0.26, 0.62), (0.045, 0.055, 0.055), m_pustule), [(fore_b, 1.0)]))
        # HUGE heavy gorilla-knuckle hand + 4 thick corpse fingers!
        hand_p = Vector((side * 0.54, -0.36, 0.24))
        parts.append((kit.create_rounded_box(f"BossHand_{side}", hand_p, (0.24, 0.26, 0.20), m_skin_dark, bevel_width=0.055), [(fore_b, 1.0)]))
        for fi, y_off in enumerate((-0.09, -0.03, 0.03, 0.09)):
            f_pts = [
                hand_p + Vector((0.0, y_off, -0.08)),
                hand_p + Vector((-side * 0.04, y_off - 0.03, -0.20)),
            ]
            parts.append((_make_tube_chain(f"BossFinger_{side}_{fi}", f_pts, [0.042, 0.032], m_skin, segments=8), [(fore_b, 1.0)]))
            parts.append((kit.create_ellipsoid(f"BossNail_{side}_{fi}", hand_p + Vector((-side * 0.05, y_off - 0.04, -0.21)), (0.024, 0.024, 0.018), m_nail), [(fore_b, 1.0)]))

    # --- 5. OCHRE-BROWN TATTERED PANTS + PURPLE-GOLD ANKLE SHACKLES + BARE CORPSE FEET ---
    for side, th_b, sh_b in ((1.0, "LeftThigh", "LeftShin"), (-1.0, "RightThigh", "RightShin")):
        thigh = _make_tube_chain(
            f"BossPantsThigh_{side}",
            [(side * 0.24, 0.14, 0.78), (side * 0.28, 0.04, 0.58), (side * 0.28, -0.02, 0.36)],
            [0.21, 0.19, 0.17], m_pants, segments=14
        )
        shin = _make_tube_chain(
            f"BossShin_{side}",
            [(side * 0.28, -0.02, 0.38), (side * 0.28, 0.04, 0.22), (side * 0.28, 0.06, 0.08)],
            [0.14, 0.125, 0.12], m_skin_dark, segments=12
        )
        parts.append((thigh, [(th_b, 1.0)]))
        parts.append((shin, [(sh_b, 1.0)]))
        # Purple & gold ankle cuff shackle (exact Ref 2 detail!)
        parts.append((kit.create_torus_ring(f"AnkleShackle_{side}", (side * 0.28, 0.05, 0.20), 0.135, 0.038, m_vest), [(sh_b, 1.0)]))
        parts.append((kit.create_torus_ring(f"AnkleGold_{side}", (side * 0.28, 0.05, 0.23), 0.138, 0.018, m_shackle), [(sh_b, 1.0)]))
        # Heavy bare teal corpse foot + toes
        parts.append((kit.create_rounded_box(f"BossFoot_{side}", (side * 0.28, -0.06, 0.055), (0.22, 0.34, 0.10), m_skin, bevel_width=0.035), [(sh_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, "Chibi_ZombieBoss_Body")

    # ==========================================================================
    #  ANIMATIONS — Idle, Walk, Run, Leap, Spit, GroundSlam, Throw, RageRoar, Slash1, Death
    # ==========================================================================
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    def _animate_parasite(s, c, mult=1.0):
        set_bone("ParasiteBase", scale=(1.0 + 0.05 * s * mult, 1.0 + 0.05 * s * mult, 1.0 + 0.06 * c * mult))
        set_bone("ParasiteTentL", rot_xyz=(12.0 * s * mult, 10.0 * c * mult, 14.0 * s * mult))
        set_bone("ParasiteTentR", rot_xyz=(-12.0 * s * mult, -10.0 * c * mult, -14.0 * s * mult))
        set_bone("ParasiteTentB", rot_xyz=(16.0 * c * mult, 0, 12.0 * s * mult))

    # 1. Idle (Heavy hunched breathing + back parasite eyeball & tentacles writhing!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Hips", loc=(0, 0, 0.03 * s), rot_xyz=(2.0 * s, 0, 2.0 * c))
        set_bone("Spine", rot_xyz=(-4.0 + 3.0 * s, 0, -2.0 * c))
        set_bone("Head", rot_xyz=(4.0 * c, 0, 5.0 * s))
        set_bone("LeftUpperArm", rot_xyz=(-5.0 + 5.0 * s, -4.0, 0))
        set_bone("RightUpperArm", rot_xyz=(-5.0 - 5.0 * s, 4.0, 0))
        _animate_parasite(s, c, 1.0)
        key_all(f, prev_q)

    # 2. Walk & Run (Heavy gorilla-hunched stomping gait!)
    for clip_name, n_frames, mult in (("Run", 17, 1.0), ("Walk", 25, 0.68)):
        act_m = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s, c = math.sin(ang), math.cos(ang)
            set_bone("Hips", loc=(0.04 * s * mult, 0, 0.07 * abs(s) * mult), rot_xyz=(-10.0 * mult, 5.0 * s, 8.0 * s))
            set_bone("Spine", rot_xyz=(-10.0 * mult, -4.0 * s, -8.0 * s))
            set_bone("Head", rot_xyz=(14.0 * mult, 0, 4.0 * c))
            set_bone("LeftThigh", rot_xyz=(-30.0 * s * mult, 0, 0))
            set_bone("LeftShin", rot_xyz=(max(0.0, c) * 34.0 * mult + 6.0, 0, 0))
            set_bone("RightThigh", rot_xyz=(30.0 * s * mult, 0, 0))
            set_bone("RightShin", rot_xyz=(max(0.0, -c) * 34.0 * mult + 6.0, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(32.0 * s * mult - 12.0, -8.0, 0))
            set_bone("LeftForeArm", rot_xyz=(-18.0, 0, 0))
            set_bone("RightUpperArm", rot_xyz=(-32.0 * s * mult - 12.0, 8.0, 0))
            set_bone("RightForeArm", rot_xyz=(-18.0, 0, 0))
            _animate_parasite(s, c, 1.45 * mult)
            key_all(f, prev_q)

    # 3. Leap (NEW BOSS JUMP ATTACK — crouch -> explosive aerial pounce -> double-fist slam!)
    act_leap = new_action("Leap")
    prev_q = {}
    for f in range(1, 25):
        begin_frame(f)
        u = (f - 1) / 23.0
        if u < 0.25:
            p = u / 0.25
            set_bone("Hips", loc=(0, 0.08 * p, -0.22 * p), rot_xyz=(-18.0 * p, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(28.0 * p, -16.0 * p, 0))
            set_bone("RightUpperArm", rot_xyz=(28.0 * p, 16.0 * p, 0))
            _animate_parasite(p, 1.0 - p, 1.6)
        elif u < 0.72:
            p = (u - 0.25) / 0.47
            arc = math.sin(p * math.pi)
            set_bone("Hips", loc=(0, -0.10 * arc, 0.28 * arc), rot_xyz=(-24.0 * p + 10.0 * arc, 0, 0))
            set_bone("Spine", rot_xyz=(-14.0 * p, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-125.0 * arc - 35.0 * p, -20.0 * arc, 0))
            set_bone("RightUpperArm", rot_xyz=(-125.0 * arc - 35.0 * p, 20.0 * arc, 0))
            set_bone("LeftThigh", rot_xyz=(-38.0 * arc, 0, 0))
            set_bone("RightThigh", rot_xyz=(-38.0 * arc, 0, 0))
            _animate_parasite(arc, p, 2.2)
        else:
            p = (u - 0.72) / 0.28
            slam = 1.0 - p
            set_bone("Hips", loc=(0, -0.06 * slam, -0.20 * slam), rot_xyz=(-28.0 * slam, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-50.0 * slam, -12.0 * slam, 0))
            set_bone("RightUpperArm", rot_xyz=(-50.0 * slam, 12.0 * slam, 0))
            _animate_parasite(slam, p, 1.8)
        key_all(f, prev_q)

    # 4. Spit (NEW BOSS ACID SPIT — rears back -> parasite flares -> hurls toxic acid forward!)
    act_spit = new_action("Spit")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        if u < 0.45:
            p = u / 0.45
            set_bone("Spine", rot_xyz=(20.0 * p, 0, 0))
            set_bone("Head", rot_xyz=(24.0 * p, 0, 0))
            set_bone("ParasiteBase", scale=(1.0 + 0.25 * p, 1.0 + 0.25 * p, 1.0 + 0.25 * p))
            set_bone("LeftUpperArm", rot_xyz=(18.0 * p, -24.0 * p, 0))
            set_bone("RightUpperArm", rot_xyz=(18.0 * p, 24.0 * p, 0))
        else:
            p = (u - 0.45) / 0.55
            snap = math.sin(p * math.pi)
            set_bone("Spine", rot_xyz=(-26.0 * snap, 0, 0))
            set_bone("Head", rot_xyz=(-24.0 * snap, 0, 0))
            set_bone("ParasiteBase", scale=(1.0 + 0.18 * snap, 1.0 + 0.18 * snap, 1.0 + 0.18 * snap))
            set_bone("LeftUpperArm", rot_xyz=(-48.0 * snap, -18.0 * snap, 0))
            set_bone("RightUpperArm", rot_xyz=(-48.0 * snap, 18.0 * snap, 0))
        key_all(f, prev_q)

    # 5. GroundSlam
    act_slam = new_action("GroundSlam")
    prev_q = {}
    for f in range(1, 25):
        begin_frame(f)
        u = (f - 1) / 23.0
        if u < 0.55:
            p = u / 0.55
            set_bone("Spine", rot_xyz=(22.0 * p, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-135.0 * p, -16.0 * p, 0))
            set_bone("RightUpperArm", rot_xyz=(-135.0 * p, 16.0 * p, 0))
            _animate_parasite(p, 1.0 - p, 2.0)
        else:
            p = (u - 0.55) / 0.45
            s = math.sin(p * math.pi)
            set_bone("Hips", loc=(0, 0, -0.22 * s), rot_xyz=(-22.0 * s, 0, 0))
            set_bone("Spine", rot_xyz=(-32.0 * s, 0, 0))
            set_bone("LeftUpperArm", rot_xyz=(-30.0 * s, -8.0 * s, 0))
            set_bone("RightUpperArm", rot_xyz=(-30.0 * s, 8.0 * s, 0))
            _animate_parasite(s, p, 2.2)
        key_all(f, prev_q)

    # 6. Throw (Hurl spawned minion zombie!)
    act_throw = new_action("Throw")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        set_bone("Spine", rot_xyz=(-18.0 * sw, 0, 26.0 * (1.0 - 2.0 * u)))
        set_bone("RightUpperArm", rot_xyz=(-115.0 * sw, 18.0 * sw, 0))
        set_bone("LeftUpperArm", rot_xyz=(-45.0 * sw, -18.0 * sw, 0))
        _animate_parasite(sw, u, 1.8)
        key_all(f, prev_q)

    # 7. RageRoar (Double-arm flex roar + parasite tentacles erupting!)
    act_roar = new_action("RageRoar")
    prev_q = {}
    for f in range(1, 25):
        begin_frame(f)
        u = (f - 1) / 23.0
        sw = math.sin(u * math.pi)
        set_bone("Spine", rot_xyz=(24.0 * sw, 0, 0))
        set_bone("Head", rot_xyz=(28.0 * sw, 0, 0))
        set_bone("LeftUpperArm", rot_xyz=(-45.0 * sw, -58.0 * sw, 0))
        set_bone("LeftForeArm", rot_xyz=(-68.0 * sw, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(-45.0 * sw, 58.0 * sw, 0))
        set_bone("RightForeArm", rot_xyz=(-68.0 * sw, 0, 0))
        set_bone("ParasiteBase", scale=(1.0 + 0.30 * sw, 1.0 + 0.30 * sw, 1.0 + 0.30 * sw))
        _animate_parasite(sw, math.cos(u * 4.0 * math.pi), 2.5)
        key_all(f, prev_q)

    # 8. Slash1 (Heavy sweeping fist smash)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        set_bone("Spine", rot_xyz=(-16.0 * sw, 0, -28.0 * sw))
        set_bone("RightUpperArm", rot_xyz=(-88.0 * sw, 22.0 * sw, 26.0 * sw))
        set_bone("LeftUpperArm", rot_xyz=(-40.0 * sw, -18.0 * sw, -16.0 * sw))
        key_all(f, prev_q)

    # 9. Death
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("Hips", loc=(0, 0.20 * s, -0.62 * s), rot_xyz=(-82.0 * s, 14.0 * s, 10.0 * s))
        set_bone("LeftUpperArm", rot_xyz=(-45.0 * s, -28.0 * s, 0))
        set_bone("RightUpperArm", rot_xyz=(-45.0 * s, 28.0 * s, 0))
        key_all(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_zombie(rig_obj, [body_mesh], "Chibi_ZombieBoss")

    # Preview 1: 3/4 Front-Side (Shows hunched Ancient Homunkulus brute, swollen forehead, red scarf, purple vest, giant gorilla arms + giant parasite eye on back!)
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-3.55, -3.55, 1.95))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.25)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieBoss_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoss_Preview.png"))

    # Preview 2: 3/4 Back (Clearly displays the GIANT parasitic bloodshot eyeball & 10 crimson-tipped tentacles!)
    cam_obj.location = Vector((2.95, 3.65, 2.25))
    cam_obj.rotation_euler = (Vector((0.0, 0.15, 1.38)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_b = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieBoss_Preview_Back.png")
    scene.render.filepath = tmp_b
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_b, os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoss_Preview_Back.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ZOMBIE BOSS (ANCIENT HOMUNKULUS + BACK PARASITE EYEBALL) ===")
    build_zombie_boss_homunkulus()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ZOMBIE BOSS (ANCIENT HOMUNKULUS) EXPORTED & RENDERED! ===")

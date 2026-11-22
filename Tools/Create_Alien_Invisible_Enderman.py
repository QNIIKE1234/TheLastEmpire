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
ALIEN_DIR = os.path.join(OUT_ROOT, "Aliens")
TMP_EXPORT_DIR = os.path.join(SCRIPT_DIR, "_tmp_alien_inv_export")
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


def make_shadow_mat(name, rgba, roughness=0.42, metallic=0.08, coat=0.25, emit_rgb=None, emit_strength=0.0):
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
        bsdf.inputs["Coat Roughness"].default_value = 0.22
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
        world = bpy.data.worlds.new("AlienInvStudioWorld")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        # Lighter slate-lavender backdrop so the pitch-black Enderman silhouette & long Majin Buu head-tail pop unmistakably!
        bg.inputs[0].default_value = (0.28, 0.30, 0.38, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, energy, loc, rot in (
        ("KeyLight", 3.2, (-2.8, -4.0, 4.8), (0.88, 0.12, -0.58)),
        ("FillLight", 2.0, (3.6, -2.5, 3.0), (1.02, -0.1, 0.85)),
        ("RimLight", 3.4, (0.6, 4.2, 3.8), (-0.95, 0.0, 0.08)),
    ):
        ld = bpy.data.lights.new(lname, type="SUN")
        ld.energy = energy
        lo = bpy.data.objects.new(lname, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = rot

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 46.0
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
#  ALIEN INVISIBLE — Tall Lanky Pitch-Black Enderman-Like Shadow Alien,
#  ONE Glowing Magenta Cyclops Eye, + SUPER-LONG Majin Buu Head-Tentacle
#  reaching ALL the way down to its butt!
# ==============================================================================

def build_alien_invisible():
    scene = kit.reset_scene()

    # Pitch-Black Obsidian Shadow Palette + Iconic Glowing Enderman Purple-Magenta Single Eye!
    m_black      = make_shadow_mat("Mat_AlienInv_Obsidian",    (0.025, 0.022, 0.038, 1.0), roughness=0.40, metallic=0.10, coat=0.30)
    m_black_rib  = make_shadow_mat("Mat_AlienInv_ShadowTrim",  (0.055, 0.032, 0.088, 1.0), roughness=0.35, metallic=0.16, coat=0.40)
    m_void_glow  = make_shadow_mat("Mat_AlienInv_VoidPulse",   (0.58,  0.10,  0.88,  1.0), emit_rgb=(0.55, 0.12, 0.90), emit_strength=1.20, roughness=0.22)
    m_eye_outer  = make_shadow_mat("Mat_AlienInv_CyclopsEye",  (0.92,  0.06,  1.00,  1.0), emit_rgb=(0.95, 0.08, 1.00), emit_strength=3.00, roughness=0.06)
    m_eye_core   = make_shadow_mat("Mat_AlienInv_EyeCore",     (1.00,  0.86,  1.00,  1.0), emit_rgb=(1.00, 0.90, 1.00), emit_strength=3.60, roughness=0.04)
    m_mouth_dark = make_shadow_mat("Mat_AlienInv_VoidMouth",   (0.18,  0.02,  0.26,  1.0), emit_rgb=(0.28, 0.04, 0.42), emit_strength=0.65, roughness=0.30)

    # Character faces -Y, +Z is UP, +X is LEFT!
    # Towering ~2.92m tall ultra-lanky Enderman silhouette!
    # Butt / Hip level is around z = 1.38 .. 1.46 -> Majin Buu head-tentacle sweeps from crown (z=2.84) ALL the way down to z=1.34!!
    bone_defs = [
        ("Root",           (0.0,  0.0,   0.0),   (0.0,  0.0,   0.20),  None,           (0, 1, 0)),
        ("Hips",           (0.0,  0.0,   1.42),  (0.0,  0.0,   1.66),  "Root",         (0, 1, 0)),
        ("Spine",          (0.0,  0.0,   1.66),  (0.0, -0.02,  2.26),  "Hips",         (0, 1, 0)),
        ("Head",           (0.0, -0.02,  2.26),  (0.0, -0.04,  2.84),  "Spine",        (0, 1, 0)),
        ("Jaw",            (0.0, -0.08,  2.42),  (0.0, -0.28,  2.34),  "Head",         (0, 1, 0)),

        # SUPER-LONG MAJIN BUU HEAD-TENTACLE (4 chained bones sweeping from crown z=2.82 ALL the way down to butt z=1.32!)
        ("BuuTail1",       (0.0,  0.08,  2.80),  (0.0,  0.42,  2.66),  "Head",         (0, 1, 0)),
        ("BuuTail2",       (0.0,  0.42,  2.66),  (0.0,  0.48,  2.20),  "BuuTail1",     (0, 1, 0)),
        ("BuuTail3",       (0.0,  0.48,  2.20),  (0.0,  0.44,  1.72),  "BuuTail2",     (0, 1, 0)),
        ("BuuTail4",       (0.0,  0.44,  1.72),  (0.0,  0.38,  1.30),  "BuuTail3",     (0, 1, 0)),

        # ULTRA-LONG SPINDLY ENDERMAN ARMS (hanging way past hips down to knee height z=0.60!)
        ("LeftUpperArm",   (0.24,  0.0,  2.20),  (0.30, -0.03, 1.46),  "Spine",        (0, 1, 0)),
        ("LeftForeArm",    (0.30, -0.03, 1.46),  (0.30, -0.08, 0.66),  "LeftUpperArm", (0, 1, 0)),
        ("RightUpperArm",  (-0.24, 0.0,  2.20),  (-0.30,-0.03, 1.46),  "Spine",        (0, 1, 0)),
        ("RightForeArm",   (-0.30,-0.03, 1.46),  (-0.30,-0.08, 0.66),  "RightUpperArm",(0, 1, 0)),

        # ULTRA-LONG SPINDLY ENDERMAN LEGS (1.42m long!)
        ("LeftThigh",      (0.11,  0.0,  1.42),  (0.12, -0.04, 0.72),  "Hips",         (0, 1, 0)),
        ("LeftShin",       (0.12, -0.04, 0.72),  (0.12,  0.0,  0.02),  "LeftThigh",    (0, 1, 0)),
        ("RightThigh",     (-0.11, 0.0,  1.42),  (-0.12,-0.04, 0.72),  "Hips",         (0, 1, 0)),
        ("RightShin",      (-0.12,-0.04, 0.72),  (-0.12, 0.0,  0.02),  "RightThigh",   (0, 1, 0)),
    ]
    rig_obj = _build_bone_tree("Alien_Invisible_Rig", bone_defs)

    parts = []

    # --- 1. TALL SLENDER ENDERMAN-STYLE OBSIDIAN TORSO & PELVIS ---
    # Very slim elongated torso with slight gaunt alien ribcage taper!
    torso_rings = [
        (Vector((0.0,  0.00, 1.38)), Vector((0.15, 0.0, 0.0)), Vector((0.0, 0.11, 0.0))),  # Butt/pelvis
        (Vector((0.0,  0.00, 1.56)), Vector((0.12, 0.0, 0.0)), Vector((0.0, 0.09, 0.0))),  # Ultra-slim waist
        (Vector((0.0, -0.01, 1.88)), Vector((0.14, 0.0, 0.0)), Vector((0.0, 0.10, 0.0))),
        (Vector((0.0, -0.02, 2.16)), Vector((0.22, 0.0, 0.0)), Vector((0.0, 0.13, 0.0))),  # Narrow gaunt shoulders
        (Vector((0.0, -0.02, 2.28)), Vector((0.14, 0.0, 0.0)), Vector((0.0, 0.10, 0.0))),
    ]
    parts.append((kit.create_loft_object("ObsidianTorso", torso_rings, m_black, segments=18), [("Spine", 0.7), ("Hips", 0.3)]))

    # Subtle obsidian rib-plates & gaunt spine ridges (keeps pitch-black Enderman silhouette rich in 3D!)
    for ri, rz in enumerate((1.78, 1.92, 2.06)):
        parts.append((kit.create_ellipsoid(f"ShadowRib_{ri}", (0.0, -0.02, rz), (0.17 + ri * 0.02, 0.115, 0.032), m_black_rib), [("Spine", 1.0)]))
    # Faint void-purple core slit on chest
    parts.append((kit.create_ellipsoid("ChestVoidSlit", (0.0, -0.128, 2.00), (0.022, 0.018, 0.12), m_void_glow), [("Spine", 1.0)]))

    # Slender neck stalk
    parts.append((_make_tube_chain("ShadowNeck", [(0.0, -0.02, 2.24), (0.0, -0.03, 2.40)], [0.072, 0.068], m_black, segments=12), [("Spine", 0.4), ("Head", 0.6)]))

    # --- 2. ENDERMAN-INSPIRED OBSIDIAN HEAD + ONE HUGE GLOWING MAGENTA CYCLOPS EYE! ---
    # Sleek beveled-blocky alien Enderman cranium (upper head + hinged unhinging lower jaw!)
    parts.append((kit.create_rounded_box("EndermanCranium", (0.0, -0.04, 2.64), (0.38, 0.38, 0.36), m_black, bevel_width=0.055), [("Head", 1.0)]))
    # Subtle obsidian brow/crown ridges
    parts.append((kit.create_rounded_box("HeadCrownPlate", (0.0, -0.02, 2.81), (0.30, 0.34, 0.07), m_black_rib, bevel_width=0.025), [("Head", 1.0)]))

    # Hinged lower jaw (snaps open wide like an angry Enderman when chasing/slashing!)
    parts.append((kit.create_rounded_box("EndermanJaw", (0.0, -0.05, 2.42), (0.34, 0.34, 0.12), m_black, bevel_width=0.035), [("Jaw", 1.0)]))
    # Eerie glowing dark-purple void mouth interior revealed when jaw drops!
    parts.append((kit.create_rounded_box("VoidMouthCore", (0.0, -0.10, 2.47), (0.24, 0.22, 0.08), m_mouth_dark, bevel_width=0.02), [("Head", 1.0)]))

    # ONE HUGE GLOWING ENDERMAN-PURPLE CYCLOPS EYE DEAD-CENTER ON FACE!
    # Wide horizontal-rectangular glowing Enderman cyclops eye (iconic Minecraft Enderman glow merged into ONE giant cyclops eye!)
    parts.append((kit.create_rounded_box("CyclopsEyeFrame", (0.0, -0.222, 2.63), (0.28, 0.04, 0.145), m_black_rib, bevel_width=0.02), [("Head", 1.0)]))
    parts.append((kit.create_rounded_box("CyclopsEyeGlow", (0.0, -0.236, 2.63), (0.24, 0.035, 0.115), m_eye_outer, bevel_width=0.022), [("Head", 1.0)]))
    # Hot-white glowing Enderman center pupil core!
    parts.append((kit.create_rounded_box("CyclopsEyePupil", (0.0, -0.248, 2.63), (0.095, 0.025, 0.088), m_eye_core, bevel_width=0.015), [("Head", 1.0)]))

    # --- 3. MASSIVE SUPER-LONG SMOOTH CONTINUOUS MAJIN BUU HEAD-TENTACLE REACHING ALL THE WAY DOWN TO THE BUTT! ---
    # Single seamless multi-segment organic tube from crown (z=2.82) sweeping down to butt (z=1.24) with smooth per-vertex bone blending!
    buu_pts = [
        (0.0, 0.00, 2.78),
        (0.0, 0.12, 2.86),
        (0.0, 0.26, 2.84),
        (0.0, 0.39, 2.74),
        (0.0, 0.47, 2.58),
        (0.0, 0.50, 2.40),
        (0.0, 0.50, 2.20),
        (0.0, 0.48, 2.00),
        (0.0, 0.46, 1.80),
        (0.0, 0.43, 1.62),
        (0.0, 0.39, 1.44),
        (0.0, 0.34, 1.28),
        (0.0, 0.32, 1.22),
    ]
    buu_radii = [0.118, 0.112, 0.104, 0.095, 0.088, 0.082, 0.076, 0.069, 0.062, 0.054, 0.045, 0.036, 0.016]
    buu_mesh = _make_tube_chain("BuuTentacle_Smooth", buu_pts, buu_radii, m_black, segments=20)
    # Assign smooth gradient weights across Head -> BuuTail1 -> BuuTail2 -> BuuTail3 -> BuuTail4 by vertex height Z!
    if buu_mesh.type == "MESH" and buu_mesh.data:
        buu_mesh.data.polygons.foreach_set("use_smooth", [True] * len(buu_mesh.data.polygons))
        vg_map = {bn: buu_mesh.vertex_groups.new(name=bn) for bn in ("Head", "BuuTail1", "BuuTail2", "BuuTail3", "BuuTail4")}
        for v in buu_mesh.data.vertices:
            vz = v.co.z
            vy = v.co.y
            if vy < 0.16 and vz > 2.72:
                vg_map["Head"].add([v.index], 0.5, "REPLACE")
                vg_map["BuuTail1"].add([v.index], 0.5, "REPLACE")
            elif vz >= 2.50:
                t = max(0.0, min(1.0, (2.78 - vz) / 0.28))
                vg_map["BuuTail1"].add([v.index], 1.0 - 0.35 * t, "REPLACE")
                vg_map["BuuTail2"].add([v.index], 0.35 * t, "REPLACE")
            elif vz >= 2.08:
                t = max(0.0, min(1.0, (2.50 - vz) / 0.42))
                vg_map["BuuTail1"].add([v.index], max(0.0, 0.65 * (1.0 - t)), "REPLACE")
                vg_map["BuuTail2"].add([v.index], 0.35 + 0.65 * (1.0 - abs(2.0 * t - 1.0) * 0.5), "REPLACE")
                vg_map["BuuTail3"].add([v.index], max(0.0, 0.45 * t), "REPLACE")
            elif vz >= 1.62:
                t = max(0.0, min(1.0, (2.08 - vz) / 0.46))
                vg_map["BuuTail2"].add([v.index], max(0.0, 0.5 * (1.0 - t)), "REPLACE")
                vg_map["BuuTail3"].add([v.index], 0.5 + 0.5 * (1.0 - abs(2.0 * t - 1.0) * 0.5), "REPLACE")
                vg_map["BuuTail4"].add([v.index], max(0.0, 0.5 * t), "REPLACE")
            else:
                t = max(0.0, min(1.0, (1.62 - vz) / 0.38))
                vg_map["BuuTail3"].add([v.index], max(0.0, 0.4 * (1.0 - t)), "REPLACE")
                vg_map["BuuTail4"].add([v.index], 0.6 + 0.4 * t, "REPLACE")
    parts.append((buu_mesh, []))
    # Faint glowing void-violet tip glint right at butt height (z=1.22)!
    parts.append((kit.create_ellipsoid("BuuTipGlow", (0.0, 0.32, 1.21), (0.024, 0.024, 0.028), m_void_glow), [("BuuTail4", 1.0)]))

    # --- 4. ULTRA-LONG SPINDLY ENDERMAN ARMS (hanging way past hips down to z=0.58!) ---
    for side, up_b, fore_b in ((1.0, "LeftUpperArm", "LeftForeArm"), (-1.0, "RightUpperArm", "RightForeArm")):
        parts.append((kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.24, 0.0, 2.19), (0.068, 0.072, 0.072), m_black_rib), [("Spine", 0.5), (up_b, 0.5)]))
        up_arm = _make_tube_chain(
            f"UpperArm_{side}",
            [(side * 0.24, 0.0, 2.20), (side * 0.28, -0.02, 1.82), (side * 0.30, -0.03, 1.46)],
            [0.052, 0.044, 0.042], m_black, segments=12
        )
        parts.append((up_arm, [(up_b, 1.0)]))
        fore_arm = _make_tube_chain(
            f"ForeArm_{side}",
            [(side * 0.30, -0.03, 1.46), (side * 0.30, -0.06, 1.06), (side * 0.30, -0.08, 0.66)],
            [0.042, 0.038, 0.044], m_black, segments=12
        )
        parts.append((fore_arm, [(fore_b, 1.0)]))
        # Long eerie spindly shadow claws at end of ultra-long arms!
        hand_p = Vector((side * 0.30, -0.08, 0.64))
        parts.append((kit.create_ellipsoid(f"Hand_{side}", hand_p, (0.048, 0.052, 0.065), m_black_rib), [(fore_b, 1.0)]))
        for fi, (fx, fy) in enumerate(((side * 0.025, -0.03), (0.0, -0.04), (-side * 0.025, -0.02))):
            c_pts = [
                hand_p + Vector((fx, fy, -0.02)),
                hand_p + Vector((fx * 1.4, fy - 0.02, -0.14)),
                hand_p + Vector((fx * 1.0, fy - 0.05, -0.22)),
            ]
            parts.append((_make_tube_chain(f"Claw_{side}_{fi}", c_pts, [0.016, 0.012, 0.004], m_black_rib, segments=8), [(fore_b, 1.0)]))

    # --- 5. ULTRA-LONG SPINDLY ENDERMAN LEGS (1.42m tall thin obsidian legs!) ---
    for side, th_b, sh_b in ((1.0, "LeftThigh", "LeftShin"), (-1.0, "RightThigh", "RightShin")):
        thigh = _make_tube_chain(
            f"Thigh_{side}",
            [(side * 0.11, 0.0, 1.42), (side * 0.12, -0.02, 1.06), (side * 0.12, -0.04, 0.72)],
            [0.062, 0.050, 0.046], m_black, segments=12
        )
        parts.append((thigh, [(th_b, 1.0)]))
        shin = _make_tube_chain(
            f"Shin_{side}",
            [(side * 0.12, -0.04, 0.72), (side * 0.12, -0.01, 0.36), (side * 0.12, 0.0, 0.04)],
            [0.046, 0.040, 0.044], m_black, segments=12
        )
        parts.append((shin, [(sh_b, 1.0)]))
        # Slender obsidian shadow foot
        parts.append((kit.create_rounded_box(f"Foot_{side}", (side * 0.12, -0.06, 0.035), (0.088, 0.19, 0.065), m_black_rib, bevel_width=0.018), [(sh_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, "AlienInvisible_Body")

    # ==========================================================================
    #  ANIMATIONS — EERIE TOWERING ENDERMAN + FLOWING MAJIN BUU HEAD-TENTACLE!
    # ==========================================================================
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    # 1. Idle (Unsettling Enderman hover-sway + hypnotic flowing Majin Buu head-tail!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Hips", loc=(0, 0, 0.02 * s), rot_xyz=(2.0 * s, 0, 2.0 * c))
        set_bone("Spine", rot_xyz=(-3.0 + 2.0 * c, 0, -2.0 * s))
        # Creepy Enderman head tilt
        set_bone("Head", rot_xyz=(3.0 * s, 5.0 * c, 4.0 * s))
        set_bone("Jaw", rot_xyz=(3.0 + 3.0 * (0.5 + 0.5 * s), 0, 0))
        # Flowing 4-segment Majin Buu head-tentacle wave!
        set_bone("BuuTail1", rot_xyz=(4.0 * s, 0, 6.0 * c))
        set_bone("BuuTail2", rot_xyz=(6.0 * c, 0, 10.0 * s))
        set_bone("BuuTail3", rot_xyz=(-8.0 * s, 0, -12.0 * c))
        set_bone("BuuTail4", rot_xyz=(-10.0 * c, 0, -14.0 * s))
        # Spindly dangling arms gently swaying
        set_bone("LeftUpperArm", rot_xyz=(-4.0 * s, -4.0, 0))
        set_bone("LeftForeArm", rot_xyz=(-6.0 - 4.0 * c, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(4.0 * s, 4.0, 0))
        set_bone("RightForeArm", rot_xyz=(-6.0 + 4.0 * c, 0, 0))
        key_all(f, prev_q)

    # 2. Walk (Towering eerie Enderman long strides + flowing head-tail!)
    act_walk = new_action("Walk")
    prev_q = {}
    for f in range(1, 25):
        begin_frame(f)
        ang = ((f - 1) / 23.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Hips", loc=(0, 0, 0.04 * abs(s)), rot_xyz=(-6.0, 4.0 * s, 6.0 * s))
        set_bone("Spine", rot_xyz=(-5.0, -3.0 * s, -5.0 * s))
        set_bone("Head", rot_xyz=(10.0, 3.0 * c, 0))
        set_bone("Jaw", rot_xyz=(6.0, 0, 0))
        # Long Majin Buu head-tentacle swaying gracefully behind
        set_bone("BuuTail1", rot_xyz=(8.0 + 5.0 * abs(s), 0, -10.0 * s))
        set_bone("BuuTail2", rot_xyz=(6.0 * c, 0, -14.0 * c))
        set_bone("BuuTail3", rot_xyz=(-6.0 * s, 0, 14.0 * s))
        set_bone("BuuTail4", rot_xyz=(-8.0 * c, 0, 18.0 * c))
        # Long spindly legs (NEGATIVE X = forward stride, POSITIVE X = back!)
        set_bone("LeftThigh", rot_xyz=(-24.0 * s, 0, 0))
        set_bone("LeftShin", rot_xyz=(max(0.0, c) * 28.0 + 6.0, 0, 0))
        set_bone("RightThigh", rot_xyz=(24.0 * s, 0, 0))
        set_bone("RightShin", rot_xyz=(max(0.0, -c) * 28.0 + 6.0, 0, 0))
        # Opposite arm swing
        set_bone("LeftUpperArm", rot_xyz=(22.0 * s, -5.0, 0))
        set_bone("LeftForeArm", rot_xyz=(-12.0, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(-22.0 * s, 5.0, 0))
        set_bone("RightForeArm", rot_xyz=(-12.0, 0, 0))
        key_all(f, prev_q)

    # 3. Run (ENRAGED ENDERMAN STALKING SPRINT — unhinged wide jaw, arms reaching forward, Majin Buu tail streaming!)
    act_run = new_action("Run")
    prev_q = {}
    for f in range(1, 17):
        begin_frame(f)
        ang = ((f - 1) / 15.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Hips", loc=(0, 0, 0.08 * abs(s)), rot_xyz=(-14.0, 5.0 * s, 9.0 * s))
        set_bone("Spine", rot_xyz=(-10.0, -4.0 * s, -8.0 * s))
        set_bone("Head", rot_xyz=(22.0, 4.0 * s, 0))
        # Iconic Angry Enderman UNHINGED JAW!!
        set_bone("Jaw", rot_xyz=(28.0 + 6.0 * abs(s), 0, 0))
        # Super-long Majin Buu head-tail streaming & whipping fast behind!
        set_bone("BuuTail1", rot_xyz=(18.0 + 8.0 * abs(s), 0, -14.0 * s))
        set_bone("BuuTail2", rot_xyz=(14.0 + 8.0 * c, 0, -18.0 * c))
        set_bone("BuuTail3", rot_xyz=(10.0 - 8.0 * s, 0, 18.0 * s))
        set_bone("BuuTail4", rot_xyz=(8.0 - 10.0 * c, 0, 24.0 * c))
        # Long predatory Enderman strides
        set_bone("LeftThigh", rot_xyz=(-38.0 * s, 0, 0))
        set_bone("LeftShin", rot_xyz=(max(0.0, c) * 44.0 + 10.0, 0, 0))
        set_bone("RightThigh", rot_xyz=(38.0 * s, 0, 0))
        set_bone("RightShin", rot_xyz=(max(0.0, -c) * 44.0 + 10.0, 0, 0))
        # Long arms raised & stalking forward menacingly!
        set_bone("LeftUpperArm", rot_xyz=(-36.0 + 20.0 * s, -10.0, 0))
        set_bone("LeftForeArm", rot_xyz=(-24.0, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(-36.0 - 20.0 * s, 10.0, 0))
        set_bone("RightForeArm", rot_xyz=(-24.0, 0, 0))
        key_all(f, prev_q)

    # 4. Slash1 (Enraged Enderman Twin-Claw Cross-Slash + Unhinged Jaw Scream!)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        set_bone("Spine", rot_xyz=(-18.0 * sw, 0, 0))
        set_bone("Head", rot_xyz=(14.0 * sw, 0, 0))
        set_bone("Jaw", rot_xyz=(34.0 * sw, 0, 0))
        set_bone("BuuTail1", rot_xyz=(24.0 * sw, 0, 0))
        set_bone("BuuTail2", rot_xyz=(20.0 * sw, 0, 0))
        set_bone("BuuTail3", rot_xyz=(16.0 * sw, 0, 0))
        set_bone("LeftUpperArm", rot_xyz=(-88.0 * sw, -18.0 * sw, -24.0 * sw))
        set_bone("LeftForeArm", rot_xyz=(-30.0 * sw, 0, 0))
        set_bone("RightUpperArm", rot_xyz=(-88.0 * sw, 18.0 * sw, 24.0 * sw))
        set_bone("RightForeArm", rot_xyz=(-30.0 * sw, 0, 0))
        key_all(f, prev_q)

    # 5. Death (Tall dramatic collapse)
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("Hips", loc=(0, 0.25 * s, -1.26 * s), rot_xyz=(-84.0 * s, 12.0 * s, 10.0 * s))
        set_bone("Jaw", rot_xyz=(26.0 * s, 0, 0))
        set_bone("BuuTail1", rot_xyz=(18.0 * s, 0, 14.0 * s))
        set_bone("BuuTail2", rot_xyz=(14.0 * s, 0, 18.0 * s))
        set_bone("LeftUpperArm", rot_xyz=(-48.0 * s, -22.0 * s, 0))
        set_bone("RightUpperArm", rot_xyz=(-40.0 * s, 26.0 * s, 0))
        key_all(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [body_mesh], "Alien_Invisible")

    # Preview 1: 3/4 Front-Side (Shows towering tall pitch-black Enderman silhouette, 1 glowing magenta eye, long arms & legs!)
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-2.45, -5.15, 1.95))
    cam_obj.rotation_euler = (Vector((0.0, 0.05, 1.48)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Alien_Invisible_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ALIEN_DIR, "Alien_Invisible_Preview.png"))

    # Preview 2: 3/4 Side-Back (Clearly displays the SUPER-LONG Majin Buu head-tentacle reaching ALL the way down to the butt!)
    cam_obj.location = Vector((-4.60, 2.35, 2.05))
    cam_obj.rotation_euler = (Vector((0.0, 0.18, 1.55)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_b = os.path.join(TMP_EXPORT_DIR, "Alien_Invisible_Preview_Back.png")
    scene.render.filepath = tmp_b
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_b, os.path.join(ALIEN_DIR, "Alien_Invisible_Preview_Back.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ALIEN INVISIBLE (TALL BLACK CYCLOPS ENDERMAN + BUTT-LENGTH MAJIN BUU TENTACLE) ===")
    build_alien_invisible()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ALIEN INVISIBLE EXPORTED & RENDERED! ===")

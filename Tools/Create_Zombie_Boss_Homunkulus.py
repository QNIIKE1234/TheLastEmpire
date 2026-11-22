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
    prev_side_a = None
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
        if prev_side_a is None:
            up_ref = Vector((1, 0, 0)) if abs(tangent.x) < 0.85 else Vector((0, 0, 1))
            side_a = tangent.cross(up_ref).normalized()
        else:
            proj = prev_side_a - tangent * prev_side_a.dot(tangent)
            if proj.length < 1e-5:
                up_ref = Vector((1, 0, 0)) if abs(tangent.x) < 0.85 else Vector((0, 0, 1))
                side_a = tangent.cross(up_ref).normalized()
            else:
                side_a = proj.normalized()
        side_b = tangent.cross(side_a).normalized()
        prev_side_a = side_a
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
        bg.inputs[0].default_value = (0.20, 0.22, 0.28, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, energy, loc, rot in (
        ("KeyLight", 3.0, (-3.0, -3.8, 4.4), (0.88, 0.12, -0.58)),
        ("FillLight", 1.8, (3.4, -2.5, 2.8), (1.05, -0.1, 0.85)),
        ("RimLight", 2.6, (0.4, 3.8, 3.4), (-0.95, 0.0, 0.05)),
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
#  ZOMBIE BOSS — SLASH ROCKSTAR UNDEAD BRUTE (Ref 1: Top Hat, Wild Curly Hair,
#  Dark Aviators, Black Rock Tank Top, HUGE Muscular Gorilla Arms, NO Guitar,
#  Hunched Walk) + FLESHY PINK-CRIMSON OCTOPUS PARASITE ON HIS BACK (Ref 2 Colors!)
# ==============================================================================

def build_zombie_boss_slash_parasite():
    scene = kit.reset_scene()

    # Slash Rockstar Undead Brute Palette (Ref 1)
    m_skin       = make_mat("Mat_Boss_ZombieSkin",     (0.56, 0.58, 0.48, 1.0), roughness=0.62)
    m_skin_dark  = make_mat("Mat_Boss_ZombieShade",    (0.36, 0.38, 0.31, 1.0), roughness=0.68)
    m_hat_black  = make_mat("Mat_Boss_TopHat",         (0.05, 0.05, 0.06, 1.0), roughness=0.72)
    m_hat_band   = make_mat("Mat_Boss_LeatherBand",    (0.14, 0.09, 0.07, 1.0), roughness=0.52)
    m_silver     = make_mat("Mat_Boss_Silver",         (0.82, 0.84, 0.88, 1.0), roughness=0.22, metallic=0.88)
    m_hair       = make_mat("Mat_Boss_CurlyHair",      (0.035, 0.03, 0.035, 1.0), roughness=0.84)
    m_glasses    = make_mat("Mat_Boss_Glasses",        (0.03, 0.03, 0.05, 1.0), roughness=0.06, metallic=0.25, coat=0.95)
    m_shirt      = make_mat("Mat_Boss_BlackTank",      (0.06, 0.06, 0.07, 1.0), roughness=0.76)
    m_shirt_pink = make_mat("Mat_Boss_RockPrint",      (0.84, 0.24, 0.56, 1.0), roughness=0.65)
    m_pants      = make_mat("Mat_Boss_SnakePants",     (0.11, 0.11, 0.13, 1.0), roughness=0.44, metallic=0.14)
    m_boots      = make_mat("Mat_Boss_LeatherBoots",   (0.04, 0.04, 0.05, 1.0), roughness=0.38)
    m_brace_gold = make_mat("Mat_Boss_GoldBead",       (0.84, 0.64, 0.18, 1.0), roughness=0.28, metallic=0.72)
    m_mouth      = make_mat("Mat_Boss_MouthDark",      (0.16, 0.03, 0.04, 1.0), roughness=0.30)
    m_fang       = make_mat("Mat_Boss_Fang",           (0.90, 0.86, 0.72, 1.0), roughness=0.26)

    # Back Octopus Parasite Palette — EXACT Ref 2 (AlienNormal glossy pink-coral flesh + deep crimson-maroon lips/suckers!)
    m_para_pink  = make_mat("Mat_Boss_ParasitePink",   (0.92, 0.42, 0.48, 1.0), roughness=0.28, coat=0.65)
    m_para_dark  = make_mat("Mat_Boss_ParasiteDark",   (0.72, 0.24, 0.30, 1.0), roughness=0.32, coat=0.55)
    m_para_crim  = make_mat("Mat_Boss_ParasiteCrimson",(0.48, 0.07, 0.12, 1.0), roughness=0.20, coat=0.75)

    # Faces -Y, +Z UP, +X LEFT!
    # Massive hunched gorilla-brute rig (~2.45m tall with Top Hat!)
    bone_defs = [
        ("Root",           (0.0,  0.0,   0.0),   (0.0,  0.0,   0.22),  None,          (0, 1, 0)),
        ("Hips",           (0.0,  0.14,  0.78),  (0.0,  0.10,  1.08),  "Root",        (0, 1, 0)),
        ("Spine",          (0.0,  0.10,  1.08),  (0.0, -0.14,  1.62),  "Hips",        (0, 1, 0)),
        ("Head",           (0.0, -0.22,  1.50),  (0.0, -0.44,  1.92),  "Spine",       (0, 1, 0)),

        # FLESHY PINK OCTOPUS PARASITE ON HIS HUNCHED BACK (Ref 2 colors!)
        ("ParasiteBase",   (0.0,  0.22,  1.42),  (0.0,  0.46,  1.88),  "Spine",       (0, 1, 0)),
        ("ParasiteTentL",  (0.22, 0.34,  1.66),  (0.78, 0.42,  2.02),  "ParasiteBase",(0, 1, 0)),
        ("ParasiteTentR",  (-0.22,0.34,  1.66),  (-0.78,0.42,  2.02),  "ParasiteBase",(0, 1, 0)),
        ("ParasiteTentB",  (0.0,  0.44,  1.56),  (0.0,  0.96,  1.78),  "ParasiteBase",(0, 1, 0)),

        # HUGE HUNCHED MUSCULAR GORILLA ARMS (NO GUITAR!)
        ("LeftUpperArm",   (0.48, -0.06, 1.48),  (0.58, -0.16, 0.88),  "Spine",       (0, 1, 0)),
        ("LeftForeArm",    (0.58, -0.16, 0.88),  (0.54, -0.34, 0.34),  "LeftUpperArm",(0, 1, 0)),
        ("RightUpperArm",  (-0.48,-0.06, 1.48),  (-0.58,-0.16, 0.88),  "Spine",       (0, 1, 0)),
        ("RightForeArm",   (-0.58,-0.16, 0.88),  (-0.54,-0.34, 0.34),  "RightUpperArm",(0, 1, 0)),

        # CROUCHED HEAVY ROCKSTAR LEGS
        ("LeftThigh",      (0.24,  0.14, 0.78),  (0.28, -0.02, 0.42),  "Hips",        (0, 1, 0)),
        ("LeftShin",       (0.28, -0.02, 0.42),  (0.28,  0.06, 0.08),  "LeftThigh",   (0, 1, 0)),
        ("RightThigh",     (-0.24, 0.14, 0.78),  (-0.28,-0.02, 0.42),  "Hips",        (0, 1, 0)),
        ("RightShin",      (-0.28,-0.02, 0.42),  (-0.28, 0.06, 0.08),  "RightThigh",  (0, 1, 0)),
    ]
    rig_obj = _build_bone_tree("Chibi_ZombieBoss_Rig", bone_defs)

    parts = []

    # --- 1. HUNCHED TORSO IN BLACK ROCKSTAR TANK TOP + PINK CHEST GRAPHIC STRIPES ---
    torso_rings = [
        (Vector((0.0,  0.14, 0.76)), Vector((0.39, 0.0, 0.0)), Vector((0.0, 0.31, 0.0))),
        (Vector((0.0,  0.08, 1.08)), Vector((0.46, 0.0, 0.0)), Vector((0.0, 0.36, 0.0))),
        (Vector((0.0, -0.04, 1.44)), Vector((0.50, 0.0, 0.0)), Vector((0.0, 0.42, 0.0))),
        (Vector((0.0, -0.10, 1.64)), Vector((0.38, 0.0, 0.0)), Vector((0.0, 0.32, 0.0))),
    ]
    parts.append((kit.create_loft_object("RockTankTop", torso_rings, m_shirt, segments=22), [("Spine", 0.75), ("Hips", 0.25)]))
    # Distressed magenta-pink rock logo bars across chest (Ref 1 "I NEED MORE" pink shirt vibe!)
    for gi, (gz, gw) in enumerate(((1.34, 0.28), (1.20, 0.32), (1.06, 0.24))):
        parts.append((kit.create_rounded_box(f"ChestPrint_{gi}", (0.0, -0.40 + gi * 0.02, gz), (gw, 0.04, 0.065), m_shirt_pink, bevel_width=0.01), [("Spine", 1.0)]))
    # Silver belt buckle & side wallet chain
    parts.append((kit.create_torus_ring("LeatherBelt", (0.0, 0.12, 0.84), 0.37, 0.055, m_hat_band), [("Hips", 1.0)]))
    parts.append((kit.create_rounded_box("BeltBuckle", (0.0, -0.25, 0.84), (0.14, 0.05, 0.10), m_silver, bevel_width=0.015), [("Hips", 1.0)]))
    parts.append((_make_tube_chain("WalletChain", [(-0.34, 0.06, 0.82), (-0.42, -0.06, 0.64), (-0.36, 0.18, 0.56)], [0.018, 0.018, 0.018], m_silver, segments=8), [("Hips", 1.0)]))

    # --- 2. SLASH HEAD: UNDEAD FACE, DARK AVIATOR SUNGLASSES, MASSIVE CURLY BLACK HAIR & TALL TOP HAT! ---
    # Hunched forward zombie rockstar head
    parts.append((kit.create_ellipsoid("Boss_Head", (0.0, -0.40, 1.64), (0.24, 0.26, 0.27), m_skin), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("Boss_Nose", (0.0, -0.65, 1.62), (0.045, 0.055, 0.052), m_skin_dark), [("Head", 1.0)]))
    # Snarling undead mouth + fangs
    parts.append((kit.create_ellipsoid("Boss_Mouth", (0.0, -0.61, 1.49), (0.12, 0.05, 0.048), m_mouth), [("Head", 1.0)]))
    for side in (-1.0, 1.0):
        parts.append((_make_tube_chain(f"BossFang_{side}", [(side * 0.065, -0.64, 1.52), (side * 0.065, -0.65, 1.46)], [0.018, 0.005], m_fang, segments=8), [("Head", 1.0)]))

    # Iconic Dark Aviator Sunglasses (Ref 1!)
    for side in (-1.0, 1.0):
        parts.append((kit.create_ellipsoid(f"AviatorLens_{side}", (side * 0.105, -0.635, 1.68), (0.095, 0.032, 0.078), m_glasses, rot_euler=(0.0, side * 0.10, 0.0)), [("Head", 1.0)]))
    parts.append((kit.create_rounded_box("AviatorBridge", (0.0, -0.65, 1.71), (0.28, 0.025, 0.022), m_silver, bevel_width=0.005), [("Head", 1.0)]))

    # Massive Voluminous Wild Curly Black Hair cascading down both sides & back (exact Slash silhouette!)
    curl_clusters = []
    for side in (-1.0, 1.0):
        for ci, (cy, cz, rx, ry, rz) in enumerate((
            (-0.46, 1.74, 0.13, 0.13, 0.13),
            (-0.44, 1.56, 0.15, 0.14, 0.15),
            (-0.40, 1.38, 0.16, 0.15, 0.15),
            (-0.34, 1.20, 0.14, 0.14, 0.14),
            (-0.24, 1.72, 0.14, 0.14, 0.14),
            (-0.22, 1.52, 0.16, 0.15, 0.16),
            (-0.20, 1.32, 0.15, 0.15, 0.15),
        )):
            cx = side * (0.25 + (ci % 2) * 0.05)
            parts.append((kit.create_ellipsoid(f"SlashCurl_{side}_{ci}", (cx, cy, cz), (rx, ry, rz), m_hair), [("Head", 0.85), ("Spine", 0.15)]))
    # Back mane curls under the top hat
    for bi, (bx, by, bz) in enumerate(((0.0, -0.14, 1.72), (0.14, -0.16, 1.56), (-0.14, -0.16, 1.56), (0.0, -0.12, 1.44))):
        parts.append((kit.create_ellipsoid(f"BackCurl_{bi}", (bx, by, bz), (0.16, 0.15, 0.16), m_hair), [("Head", 1.0)]))

    # ICONIC TALL BLACK TOP HAT WITH LEATHER BAND & SILVER CONCHO MEDALLIONS ("ใส่หมวกทรงสูง")!
    hat_c = Vector((0.0, -0.40, 1.88))
    # Curled wide brim
    brim_rings = [
        (hat_c + Vector((0.0, 0.0, -0.02)), Vector((0.40, 0.0, 0.0)), Vector((0.0, 0.43, 0.0))),
        (hat_c + Vector((0.0, 0.0,  0.03)), Vector((0.42, 0.0, 0.0)), Vector((0.0, 0.45, 0.0))),
        (hat_c + Vector((0.0, 0.0,  0.05)), Vector((0.36, 0.0, 0.0)), Vector((0.0, 0.39, 0.0))),
        (hat_c + Vector((0.0, 0.0,  0.02)), Vector((0.26, 0.0, 0.0)), Vector((0.0, 0.28, 0.0))),
    ]
    parts.append((kit.create_loft_object("TopHat_Brim", brim_rings, m_hat_black, segments=28), [("Head", 1.0)]))
    # Tall slightly flared stovepipe cylinder crown!
    crown_rings = [
        (hat_c + Vector((0.0, 0.0, 0.02)), Vector((0.26, 0.0, 0.0)), Vector((0.0, 0.28, 0.0))),
        (hat_c + Vector((0.0, 0.0, 0.24)), Vector((0.27, 0.0, 0.0)), Vector((0.0, 0.29, 0.0))),
        (hat_c + Vector((0.0, 0.0, 0.48)), Vector((0.31, 0.0, 0.0)), Vector((0.0, 0.33, 0.0))),
        (hat_c + Vector((0.0, 0.0, 0.56)), Vector((0.28, 0.0, 0.0)), Vector((0.0, 0.30, 0.0))),
        (hat_c + Vector((0.0, 0.0, 0.58)), Vector((0.04, 0.0, 0.0)), Vector((0.0, 0.04, 0.0))),
    ]
    parts.append((kit.create_loft_object("TopHat_Crown", crown_rings, m_hat_black, segments=28), [("Head", 1.0)]))
    # Leather hatband + 7 metallic silver Slash concho medallions around the hat!
    parts.append((kit.create_torus_ring("TopHat_Band", hat_c + Vector((0.0, 0.0, 0.085)), 0.27, 0.042, m_hat_band), [("Head", 1.0)]))
    for mi in range(7):
        ang = math.radians(-105.0 + mi * 35.0)
        mx = math.sin(ang) * 0.295
        my = -0.40 - math.cos(ang) * 0.315
        parts.append((kit.create_ellipsoid(f"HatConcho_{mi}", (mx, my, hat_c.z + 0.09), (0.046, 0.026, 0.046), m_silver, rot_euler=(0.0, 0.0, -ang)), [("Head", 1.0)]))

    # --- 3. FLESHY PINK-CRIMSON OCTOPUS PARASITE GROWING FROM HIS HUNCHED BACK (Ref 2 Colors!) ---
    # Glossy wet fleshy-pink octopus mantle/bulb rooted into his upper back!
    octo_rings = [
        (Vector((0.0, 0.24, 1.32)), Vector((0.34, 0.0, 0.0)), Vector((0.0, 0.26, 0.0))),
        (Vector((0.0, 0.40, 1.56)), Vector((0.36, 0.0, 0.0)), Vector((0.0, 0.32, 0.0))),
        (Vector((0.0, 0.50, 1.82)), Vector((0.30, 0.0, 0.0)), Vector((0.0, 0.28, 0.0))),
        (Vector((0.0, 0.54, 1.98)), Vector((0.18, 0.0, 0.0)), Vector((0.0, 0.18, 0.0))),
        (Vector((0.0, 0.55, 2.04)), Vector((0.04, 0.0, 0.0)), Vector((0.0, 0.04, 0.0))),
    ]
    parts.append((kit.create_loft_object("OctopusMantle", octo_rings, m_para_pink, segments=24), [("ParasiteBase", 0.8), ("Spine", 0.2)]))
    # Deep crimson-maroon fleshy vein ridge & pulsing parasite maw on the octopus dome (Ref 2 exact crimson lips/flesh!)
    parts.append((kit.create_ellipsoid("OctoMawLip", (0.0, 0.68, 1.66), (0.14, 0.08, 0.14), m_para_crim), [("ParasiteBase", 1.0)]))
    parts.append((kit.create_ellipsoid("OctoMawCore", (0.0, 0.72, 1.66), (0.08, 0.05, 0.08), m_mouth), [("ParasiteBase", 1.0)]))
    for ri, (ry, rz) in enumerate(((0.56, 1.86), (0.58, 1.46))):
        parts.append((kit.create_ellipsoid(f"OctoRidge_{ri}", (0.0, ry, rz), (0.24, 0.12, 0.065), m_para_dark), [("ParasiteBase", 1.0)]))

    def _catmull_rom_chain(ctrl_pts, subdivisions=4):
        pts = [Vector(p) for p in ctrl_pts]
        ext = [pts[0] + (pts[0] - pts[1])] + pts + [pts[-1] + (pts[-1] - pts[-2])]
        out = []
        for i in range(1, len(ext) - 2):
            p0, p1, p2, p3 = ext[i - 1], ext[i], ext[i + 1], ext[i + 2]
            for s in range(subdivisions):
                t = s / float(subdivisions)
                t2, t3 = t * t, t * t * t
                pos = 0.5 * (
                    (2.0 * p1)
                    + (-p0 + p2) * t
                    + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                    + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
                )
                out.append(pos)
        out.append(pts[-1])
        return out

    # 8 LONG SMOOTH CURLING PINK OCTOPUS TENTACLES WITH CRIMSON SUCKER CUPS bursting outward over shoulders & back!
    octo_tents = [
        # Left curling tentacles arching smoothly over left shoulder & side with curled tips!
        ([(0.20, 0.36, 1.68), (0.52, 0.34, 1.98), (0.86, 0.16, 2.08), (1.02, -0.10, 1.88), (0.94, -0.28, 1.68), (0.80, -0.20, 1.72)], "ParasiteTentL"),
        ([(0.24, 0.42, 1.54), (0.62, 0.52, 1.66), (0.94, 0.52, 1.50), (1.04, 0.34, 1.24), (0.92, 0.22, 1.14)], "ParasiteTentL"),
        ([(0.18, 0.46, 1.42), (0.46, 0.68, 1.32), (0.72, 0.78, 1.06), (0.68, 0.64, 0.84), (0.54, 0.58, 0.88)], "ParasiteTentL"),
        # Right curling tentacles arching smoothly over right shoulder & side with curled tips!
        ([(-0.20, 0.36, 1.68), (-0.52, 0.34, 1.98), (-0.86, 0.16, 2.08), (-1.02, -0.10, 1.88), (-0.94, -0.28, 1.68), (-0.80, -0.20, 1.72)], "ParasiteTentR"),
        ([(-0.24, 0.42, 1.54), (-0.62, 0.52, 1.66), (-0.94, 0.52, 1.50), (-1.04, 0.34, 1.24), (-0.92, 0.22, 1.14)], "ParasiteTentR"),
        ([(-0.18, 0.46, 1.42), (-0.46, 0.68, 1.32), (-0.72, 0.78, 1.06), (-0.68, 0.64, 0.84), (-0.54, 0.58, 0.88)], "ParasiteTentR"),
        # Back 2 high sweeping curling octopus tentacles!
        ([(0.12, 0.50, 1.72), (0.24, 0.82, 2.02), (0.36, 1.08, 2.12), (0.28, 1.20, 1.88), (0.18, 1.08, 1.82)], "ParasiteTentB"),
        ([(-0.12, 0.50, 1.72), (-0.24, 0.82, 2.02), (-0.36, 1.08, 2.12), (-0.28, 1.20, 1.88), (-0.18, 1.08, 1.82)], "ParasiteTentB"),
    ]
    for tidx, (ctrl_pts, bname) in enumerate(octo_tents):
        tpts = _catmull_rom_chain(ctrl_pts, subdivisions=4)
        n_p = len(tpts)
        rads = [0.095 * ((1.0 - 0.82 * (k / float(n_p - 1))) ** 0.85) for k in range(n_p)]
        rads[-1] = 0.014
        parts.append((_make_tube_chain(f"OctoTentacle_{tidx}", tpts, rads, m_para_pink, segments=14), [(bname, 0.85), ("ParasiteBase", 0.15)]))
        # Row of crimson-maroon octopus sucker cups lining the underside along the smooth curve!
        for k in range(2, n_p - 2, 2):
            sp = tpts[k] + Vector((0.0, 0.0, -rads[k] * 0.82))
            parts.append((kit.create_ellipsoid(f"OctoSucker_{tidx}_{k}", sp, (rads[k] * 0.52, rads[k] * 0.52, rads[k] * 0.42), m_para_crim), [(bname, 0.85), ("ParasiteBase", 0.15)]))

    # --- 4. TWO HUGE MUSCULAR GORILLA-HUNCHED ARMS + ROCKSTAR WRISTBANDS & SKULL RINGS (NO GUITAR!) ---
    for side, up_b, fore_b in ((1.0, "LeftUpperArm", "LeftForeArm"), (-1.0, "RightUpperArm", "RightForeArm")):
        # Massive bulging shoulder & bicep
        parts.append((kit.create_ellipsoid(f"BossShoulder_{side}", (side * 0.50, -0.06, 1.44), (0.23, 0.23, 0.25), m_skin), [(up_b, 0.7), ("Spine", 0.3)]))
        up_arm = _make_tube_chain(
            f"BossUpperArm_{side}",
            [(side * 0.48, -0.06, 1.44), (side * 0.56, -0.12, 1.16), (side * 0.58, -0.16, 0.88)],
            [0.20, 0.185, 0.175], m_skin, segments=16
        )
        fore_arm = _make_tube_chain(
            f"BossForeArm_{side}",
            [(side * 0.58, -0.16, 0.88), (side * 0.57, -0.26, 0.60), (side * 0.54, -0.34, 0.34)],
            [0.195, 0.22, 0.19], m_skin, segments=16
        )
        parts.append((up_arm, [(up_b, 1.0)]))
        parts.append((fore_arm, [(fore_b, 1.0)]))
        # Slash's stacked leather/beaded/silver rockstar wristbands!
        for wi, (wz, wy, wm) in enumerate(((0.44, -0.31, m_hat_black), (0.39, -0.33, m_silver), (0.34, -0.34, m_brace_gold))):
            parts.append((kit.create_torus_ring(f"RockWrist_{side}_{wi}", (side * 0.55, wy, wz), 0.195, 0.026, wm, rot_euler=(0.30, 0.0, 0.0)), [(fore_b, 1.0)]))
        # HUGE heavy gorilla-knuckle fists + silver rockstar rings!
        hand_p = Vector((side * 0.54, -0.36, 0.23))
        parts.append((kit.create_rounded_box(f"BossHand_{side}", hand_p, (0.25, 0.27, 0.21), m_skin_dark, bevel_width=0.055), [(fore_b, 1.0)]))
        for fi, y_off in enumerate((-0.09, -0.03, 0.03, 0.09)):
            f_pts = [hand_p + Vector((0.0, y_off, -0.08)), hand_p + Vector((-side * 0.04, y_off - 0.03, -0.20))]
            parts.append((_make_tube_chain(f"BossFinger_{side}_{fi}", f_pts, [0.044, 0.034], m_skin, segments=8), [(fore_b, 1.0)]))
            if fi in (1, 2):
                parts.append((kit.create_torus_ring(f"SkullRing_{side}_{fi}", hand_p + Vector((-side * 0.02, y_off - 0.015, -0.14)), 0.042, 0.014, m_silver), [(fore_b, 1.0)]))

    # --- 5. BLACK SNAKESKIN ROCKSTAR PANTS & HEAVY LEATHER BOOTS ---
    for side, th_b, sh_b in ((1.0, "LeftThigh", "LeftShin"), (-1.0, "RightThigh", "RightShin")):
        thigh = _make_tube_chain(
            f"RockPantsThigh_{side}",
            [(side * 0.24, 0.14, 0.78), (side * 0.28, 0.04, 0.58), (side * 0.28, -0.02, 0.38)],
            [0.21, 0.19, 0.17], m_pants, segments=14
        )
        shin = _make_tube_chain(
            f"RockBootShin_{side}",
            [(side * 0.28, -0.02, 0.38), (side * 0.28, 0.04, 0.22), (side * 0.28, 0.06, 0.08)],
            [0.16, 0.15, 0.145], m_boots, segments=12
        )
        parts.append((thigh, [(th_b, 1.0)]))
        parts.append((shin, [(sh_b, 1.0)]))
        parts.append((kit.create_torus_ring(f"BootStrap_{side}", (side * 0.28, 0.05, 0.20), 0.155, 0.022, m_silver), [(sh_b, 1.0)]))
        parts.append((kit.create_rounded_box(f"RockBootFoot_{side}", (side * 0.28, -0.06, 0.06), (0.22, 0.36, 0.12), m_boots, bevel_width=0.035), [(sh_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, "Chibi_ZombieBoss_Body")

    # ==========================================================================
    #  ANIMATIONS — Same Hunched Gorilla Brute Walk + Leap, Spit, GroundSlam, Throw, RageRoar, Slash1, Death
    # ==========================================================================
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    def _animate_parasite(s, c, mult=1.0):
        set_bone("ParasiteBase", scale=(1.0 + 0.06 * s * mult, 1.0 + 0.06 * s * mult, 1.0 + 0.06 * c * mult))
        set_bone("ParasiteTentL", rot_xyz=(14.0 * s * mult, 12.0 * c * mult, 16.0 * s * mult))
        set_bone("ParasiteTentR", rot_xyz=(-14.0 * s * mult, -12.0 * c * mult, -16.0 * s * mult))
        set_bone("ParasiteTentB", rot_xyz=(18.0 * c * mult, 0, 14.0 * s * mult))

    # 1. Idle
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

    # 2. Walk & Run (Hunched Gorilla-Brute Stomping Gait!)
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
            _animate_parasite(s, c, 1.5 * mult)
            key_all(f, prev_q)

    # 3. Leap (High-Arc Boss Jump Slam!)
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

    # 4. Spit (Acid Spit)
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

    # 6. Throw
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

    # 7. RageRoar
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

    # 8. Slash1
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

    # Preview 1: 3/4 Front-Side (Shows Top Hat, Curly Black Hair, Aviators, HUGE Gorilla Arms, NO Guitar, + Pink Octopus Tentacles rising behind shoulders!)
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-3.65, -3.65, 2.00))
    cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.32)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_f = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieBoss_Preview.png")
    scene.render.filepath = tmp_f
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_f, os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoss_Preview.png"))

    # Preview 2: 3/4 Back (Clearly displays the GIANT Fleshy Pink-Crimson Octopus Parasite & 8 curling tentacles on his back!)
    cam_obj.location = Vector((3.20, 3.85, 2.25))
    cam_obj.rotation_euler = (Vector((0.0, 0.25, 1.42)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_b = os.path.join(TMP_EXPORT_DIR, "Chibi_ZombieBoss_Preview_Back.png")
    scene.render.filepath = tmp_b
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_b, os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoss_Preview_Back.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ZOMBIE BOSS (SLASH TOP-HAT BRUTE + PINK OCTOPUS PARASITE) ===")
    build_zombie_boss_slash_parasite()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ZOMBIE BOSS (SLASH + PINK OCTOPUS PARASITE) EXPORTED & RENDERED! ===")

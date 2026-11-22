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
TMP_EXPORT_DIR = os.path.join(SCRIPT_DIR, "_tmp_alien_crawler_export")
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


def make_mat(name, rgba, roughness=0.45, metallic=0.0, coat=0.0, emit_rgb=None, emit_strength=0.0):
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
            # Parallel transport to guarantee zero twist/pinch across sharp bends!
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
        world = bpy.data.worlds.new("AlienCrawlerStudioWorld")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.18, 0.20, 0.26, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, energy, loc, rot in (
        ("KeyLight", 2.9, (-2.8, -3.5, 4.0), (0.88, 0.12, -0.58)),
        ("FillLight", 1.7, (3.2, -2.2, 2.5), (1.05, -0.1, 0.85)),
        ("RimLight", 2.3, (0.4, 3.6, 3.0), (-0.95, 0.0, 0.05)),
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
#  ALIEN CRAWLER — Hunched Furred Cyclops Beast-Crawler (Ref 3)
#  with EXTRA-LONG RAZOR CLAWS ("เล็บยาวๆ") & EXTRA-LONG WRITHING RED TONGUE ("ลิ้นยาว")!
# ==============================================================================

def build_alien_crawler():
    scene = kit.reset_scene()

    # Exact Ref 3 palette: Shaggy Chestnut-Brown Fur + Warm Orange-Ochre Face/Paws + Bloodshot White Cyclops Eye + Bright Red Long Tongue + Ivory-Black Long Claws!
    m_fur        = make_mat("Mat_AlienCrawler_Fur",        (0.36, 0.19, 0.08, 1.0), roughness=0.82)
    m_fur_dark   = make_mat("Mat_AlienCrawler_FurDark",    (0.22, 0.10, 0.04, 1.0), roughness=0.86)
    m_orange     = make_mat("Mat_AlienCrawler_OrangeSkin", (0.85, 0.45, 0.12, 1.0), roughness=0.44)
    m_ear_pink   = make_mat("Mat_AlienCrawler_EarPink",    (0.84, 0.24, 0.30, 1.0), roughness=0.38)
    m_eye_white  = make_mat("Mat_AlienCrawler_EyeWhite",   (0.97, 0.94, 0.94, 1.0), roughness=0.08, coat=0.85)
    m_eye_vein   = make_mat("Mat_AlienCrawler_EyeVein",    (0.85, 0.12, 0.14, 1.0), roughness=0.20)
    m_pupil      = make_mat("Mat_AlienCrawler_Pupil",      (0.05, 0.03, 0.04, 1.0), roughness=0.05)
    m_mouth_dark = make_mat("Mat_AlienCrawler_MouthDark",  (0.20, 0.03, 0.05, 1.0), roughness=0.28)
    m_tongue     = make_mat("Mat_AlienCrawler_Tongue",     (0.88, 0.08, 0.12, 1.0), roughness=0.16, coat=0.90)
    m_fang       = make_mat("Mat_AlienCrawler_Fang",       (0.95, 0.93, 0.86, 1.0), roughness=0.22, coat=0.30)
    m_claw       = make_mat("Mat_AlienCrawler_LongClaw",   (0.92, 0.86, 0.72, 1.0), roughness=0.26, coat=0.35)

    # Faces -Y, +Z UP, +X LEFT!
    bone_defs = [
        ("Root",       (0.0,  0.0,   0.0),   (0.0,  0.0,   0.18),  None,     (0, 1, 0)),
        ("Body",       (0.0,  0.14,  0.66),  (0.0, -0.26,  0.80),  "Root",   (0, 1, 0)),
        ("Head",       (0.0, -0.26,  0.80),  (0.0, -0.62,  0.82),  "Body",   (0, 1, 0)),
        ("JawBot",     (0.0, -0.34,  0.64),  (0.0, -0.66,  0.50),  "Head",   (0, 1, 0)),

        # EXTRA-LONG WRITHING RED TONGUE (3-bone sinuous whip stretching ~0.95m out in front!)
        ("Tongue1",    (0.0, -0.44,  0.62),  (0.0, -0.78,  0.50),  "JawBot", (0, 0, 1)),
        ("Tongue2",    (0.0, -0.78,  0.50),  (0.0, -1.12,  0.42),  "Tongue1",(0, 0, 1)),
        ("Tongue3",    (0.0, -1.12,  0.42),  (0.0, -1.42,  0.52),  "Tongue2",(0, 0, 1)),

        # Curled spiral beast tail behind rump
        ("Tail1",      (0.0,  0.48,  0.52),  (0.0,  0.82,  0.34),  "Body",   (0, 1, 0)),
        ("Tail2",      (0.0,  0.82,  0.34),  (0.0,  0.86,  0.14),  "Tail1",  (0, 1, 0)),

        # 2 Long Lanky Front Crawling Arms + HUGE LONG CLAWS!
        ("ArmL_Up",    (0.28, -0.14, 0.72),  (0.36, -0.16, 0.36),  "Body",   (0, 1, 0)),
        ("ArmL_Fore",  (0.36, -0.16, 0.36),  (0.32, -0.34, 0.06),  "ArmL_Up",(0, 1, 0)),
        ("ArmR_Up",    (-0.28,-0.14, 0.72),  (-0.36,-0.16, 0.36),  "Body",   (0, 1, 0)),
        ("ArmR_Fore",  (-0.36,-0.16, 0.36),  (-0.32,-0.34, 0.06),  "ArmR_Up",(0, 1, 0)),

        # 2 Hunched Hind Beast Legs + HUGE LONG CLAWS!
        ("LegL",       (0.24,  0.30, 0.56),  (0.28,  0.14, 0.06),  "Body",   (0, 1, 0)),
        ("LegR",       (-0.24, 0.30, 0.56),  (-0.28, 0.14, 0.06),  "Body",   (0, 1, 0)),
    ]
    rig_obj = _build_bone_tree("Alien_Crawler_Rig", bone_defs)

    parts = []

    # --- 1. HUNCHED SHAGGY FURRY BEAST BODY (exact Ref 3 rounded arched silhouette!) ---
    body_rings = [
        (Vector((0.0,  0.48, 0.54)), Vector((0.20, 0.0, 0.0)), Vector((0.0, 0.0, 0.22))),
        (Vector((0.0,  0.26, 0.72)), Vector((0.34, 0.0, 0.0)), Vector((0.0, 0.0, 0.34))),
        (Vector((0.0,  0.00, 0.80)), Vector((0.35, 0.0, 0.0)), Vector((0.0, 0.0, 0.34))),
        (Vector((0.0, -0.24, 0.78)), Vector((0.30, 0.0, 0.0)), Vector((0.0, 0.0, 0.28))),
    ]
    parts.append((kit.create_loft_object("Crawler_FurBody", body_rings, m_fur, segments=22), [("Body", 1.0)]))

    # Shaggy spiky fur tufts bristling along the hunched back & shoulders!
    for fi, (fy, fz, fx_spread, sc) in enumerate((
        (0.36, 0.90, 0.00, 0.11),
        (0.18, 1.06, 0.00, 0.13),
        (0.00, 1.12, 0.00, 0.13),
        (-0.16, 1.06, 0.00, 0.11),
        (0.22, 0.96, 0.18, 0.09),
        (0.22, 0.96, -0.18, 0.09),
        (0.02, 1.02, 0.22, 0.09),
        (0.02, 1.02, -0.22, 0.09),
    )):
        parts.append((_make_tube_chain(f"FurTuft_{fi}", [(fx_spread, fy, fz - 0.08), (fx_spread * 1.15, fy + 0.10, fz + sc)], [0.065, 0.008], m_fur_dark if fi % 2 else m_fur, segments=8), [("Body", 1.0)]))

    # --- 2. CURLED SPIRAL MONKEY/BEAST TAIL (exact Ref 3 curly tail!) ---
    tail_pts = [
        (0.0, 0.44, 0.54),
        (0.0, 0.68, 0.48),
        (0.0, 0.88, 0.34),
        (0.0, 0.94, 0.18),
        (0.0, 0.84, 0.08),
        (0.0, 0.70, 0.12),
        (0.0, 0.68, 0.22),
    ]
    tail_rad = [0.075, 0.065, 0.056, 0.048, 0.042, 0.035, 0.024]
    parts.append((_make_tube_chain("SpiralTail", tail_pts, tail_rad, m_fur, segments=12), [("Tail1", 0.55), ("Tail2", 0.45)]))

    # --- 3. HEAD + POINTED BEAST EARS + ONE HUGE BULGING BLOODSHOT CYCLOPS EYE! ---
    parts.append((kit.create_ellipsoid("HeadSkull", (0.0, -0.34, 0.84), (0.26, 0.26, 0.25), m_fur), [("Head", 1.0)]))
    # Pointed beast ears with pink inner flesh
    for side in (-1.0, 1.0):
        e_pts = [(side * 0.15, -0.26, 1.00), (side * 0.22, -0.30, 1.18), (side * 0.26, -0.36, 1.28)]
        parts.append((_make_tube_chain(f"Ear_{side}", e_pts, [0.085, 0.058, 0.012], m_fur_dark, segments=10), [("Head", 1.0)]))
        parts.append((kit.create_ellipsoid(f"EarInner_{side}", (side * 0.20, -0.34, 1.12), (0.036, 0.025, 0.075), m_ear_pink, rot_euler=(0.15, side * -0.25, 0.0)), [("Head", 1.0)]))

    # Fleshy orange-ochre eye socket + ONE MASSIVE BULGING BLOODSHOT WHITE CYCLOPS EYEBALL!
    parts.append((kit.create_ellipsoid("EyeSocket", (0.0, -0.50, 0.88), (0.23, 0.21, 0.22), m_orange), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("CyclopsEyeball", (0.0, -0.58, 0.90), (0.195, 0.195, 0.195), m_eye_white), [("Head", 1.0)]))
    # Tiny crazed pupil + bloodshot red eye veins
    parts.append((kit.create_ellipsoid("CyclopsPupil", (0.0, -0.768, 0.90), (0.038, 0.022, 0.038), m_pupil), [("Head", 1.0)]))
    for vi, ang_deg in enumerate((-38, 35, 145, -140)):
        r = math.radians(ang_deg)
        vx = math.cos(r) * 0.15
        vz = 0.90 + math.sin(r) * 0.15
        parts.append((_make_tube_chain(f"EyeVein_{vi}", [(vx * 1.15, -0.60, vz), (vx * 0.65, -0.72, 0.90 + math.sin(r) * 0.08)], [0.008, 0.004], m_eye_vein, segments=6), [("Head", 1.0)]))

    # --- 4. WIDE GAPING TOOTHY JAW + EXTRA-LONG WRITHING RED TONGUE ("ลิ้นยาว")! ---
    # Upper snout/lip + upper fangs
    parts.append((kit.create_ellipsoid("UpperLip", (0.0, -0.52, 0.70), (0.20, 0.16, 0.075), m_orange), [("Head", 1.0)]))
    parts.append((kit.create_ellipsoid("MouthCavity", (0.0, -0.44, 0.60), (0.18, 0.20, 0.14), m_mouth_dark), [("Head", 1.0)]))
    for k in range(7):
        ang = math.radians(-68.0 + k * (136.0 / 6.0))
        fx = math.sin(ang) * 0.175
        fy = -0.48 - math.cos(ang) * 0.17
        flen = 0.095 if k in (1, 5) else 0.068
        parts.append((_make_tube_chain(f"UpFang_{k}", [(fx, fy, 0.68), (fx * 0.95, fy - 0.01, 0.68 - flen)], [0.022, 0.004], m_fang, segments=8), [("Head", 1.0)]))

    # Hinged lower jaw + lower fangs
    parts.append((kit.create_ellipsoid("LowerJaw", (0.0, -0.50, 0.50), (0.19, 0.20, 0.075), m_orange), [("JawBot", 1.0)]))
    for k in range(7):
        ang = math.radians(-68.0 + k * (136.0 / 6.0))
        fx = math.sin(ang) * 0.165
        fy = -0.47 - math.cos(ang) * 0.18
        flen = 0.095 if k in (1, 5) else 0.065
        parts.append((_make_tube_chain(f"LowFang_{k}", [(fx, fy, 0.52), (fx * 0.95, fy - 0.01, 0.52 + flen)], [0.020, 0.004], m_fang, segments=8), [("JawBot", 1.0)]))

    # EXTRA-LONG SINUOUS WET RED TONGUE WHIPPING WAY OUT IN FRONT (single continuous smooth lofted mesh!)
    tongue_pts = [
        (0.0, -0.42, 0.60),
        (0.0, -0.60, 0.55),
        (0.0, -0.80, 0.47),
        (0.0, -1.00, 0.42),
        (0.0, -1.20, 0.43),
        (0.0, -1.36, 0.50),
        (0.0, -1.46, 0.58),
    ]
    tongue_radii = [0.072, 0.066, 0.060, 0.052, 0.044, 0.032, 0.014]
    tongue_mesh = _make_tube_chain("LongRedTongue", tongue_pts, tongue_radii, m_tongue, segments=14)
    if tongue_mesh.type == "MESH" and tongue_mesh.data:
        tongue_mesh.data.polygons.foreach_set("use_smooth", [True] * len(tongue_mesh.data.polygons))
        tvg = {bn: tongue_mesh.vertex_groups.new(name=bn) for bn in ("JawBot", "Tongue1", "Tongue2", "Tongue3")}
        for v in tongue_mesh.data.vertices:
            vy = v.co.y
            if vy > -0.65:
                t = max(0.0, min(1.0, (-0.42 - vy) / 0.23))
                tvg["JawBot"].add([v.index], 1.0 - 0.5 * t, "REPLACE")
                tvg["Tongue1"].add([v.index], 0.5 * t, "REPLACE")
            elif vy > -0.98:
                t = max(0.0, min(1.0, (-0.65 - vy) / 0.33))
                tvg["Tongue1"].add([v.index], 1.0 - 0.5 * t, "REPLACE")
                tvg["Tongue2"].add([v.index], 0.5 * t, "REPLACE")
            elif vy > -1.25:
                t = max(0.0, min(1.0, (-0.98 - vy) / 0.27))
                tvg["Tongue2"].add([v.index], 1.0 - 0.6 * t, "REPLACE")
                tvg["Tongue3"].add([v.index], 0.6 * t, "REPLACE")
            else:
                tvg["Tongue3"].add([v.index], 1.0, "REPLACE")
    parts.append((tongue_mesh, []))

    # --- 5. FOUR HUNCHED CRAWLING LIMBS WITH EXTRA-LONG RAZOR CLAWS ("เล็บยาวๆ")! ---
    # Front 2 lanky hunched arms + huge orange hands + 4 HUGE LONG CURVED RAZOR CLAWS each!
    for side, up_b, fore_b in ((1.0, "ArmL_Up", "ArmL_Fore"), (-1.0, "ArmR_Up", "ArmR_Fore")):
        up_arm = _make_tube_chain(
            f"FrontArmUp_{side}",
            [(side * 0.28, -0.14, 0.72), (side * 0.35, -0.10, 0.54), (side * 0.36, -0.16, 0.36)],
            [0.095, 0.082, 0.075], m_fur, segments=12
        )
        fore_arm = _make_tube_chain(
            f"FrontArmFore_{side}",
            [(side * 0.36, -0.16, 0.36), (side * 0.34, -0.26, 0.20), (side * 0.32, -0.34, 0.08)],
            [0.075, 0.068, 0.072], m_fur_dark, segments=12
        )
        parts.append((up_arm, [(up_b, 1.0)]))
        parts.append((fore_arm, [(fore_b, 1.0)]))
        paw_p = Vector((side * 0.32, -0.38, 0.075))
        parts.append((kit.create_ellipsoid(f"FrontPaw_{side}", paw_p, (0.105, 0.125, 0.068), m_orange), [(fore_b, 1.0)]))
        # 4 EXTRA-LONG CURVED RAZOR CLAWS (~0.28m long!)
        for ci, x_off in enumerate((-0.075, -0.025, 0.025, 0.075)):
            c_pts = [
                paw_p + Vector((x_off, -0.09, 0.01)),
                paw_p + Vector((x_off * 1.35, -0.24, 0.045)),
                paw_p + Vector((x_off * 1.55, -0.38, -0.055)),
            ]
            parts.append((_make_tube_chain(f"FrontLongClaw_{side}_{ci}", c_pts, [0.026, 0.018, 0.004], m_claw, segments=8), [(fore_b, 1.0)]))

    # Hind 2 crouched haunch legs + orange feet + 3 EXTRA-LONG RAZOR CLAWS each!
    for side, leg_b in ((1.0, "LegL"), (-1.0, "LegR")):
        haunch = _make_tube_chain(
            f"HindLeg_{side}",
            [(side * 0.24, 0.30, 0.56), (side * 0.30, 0.38, 0.32), (side * 0.28, 0.14, 0.08)],
            [0.125, 0.092, 0.072], m_fur, segments=12
        )
        parts.append((haunch, [(leg_b, 1.0)]))
        foot_p = Vector((side * 0.28, 0.08, 0.068))
        parts.append((kit.create_ellipsoid(f"HindPaw_{side}", foot_p, (0.095, 0.145, 0.062), m_orange), [(leg_b, 1.0)]))
        for ci, x_off in enumerate((-0.055, 0.0, 0.055)):
            c_pts = [
                foot_p + Vector((x_off, -0.11, 0.005)),
                foot_p + Vector((x_off * 1.25, -0.25, 0.03)),
                foot_p + Vector((x_off * 1.40, -0.36, -0.05)),
            ]
            parts.append((_make_tube_chain(f"HindLongClaw_{side}_{ci}", c_pts, [0.024, 0.016, 0.004], m_claw, segments=8), [(leg_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, "AlienCrawler_Body")

    # --- ANIMATIONS (Idle, Walk, Run, Slash1, Death) ---
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    # 1. Idle (Crouched panting, bulging eye twitching, EXTRA-LONG red tongue writhing & lashing!)
    act_idle = new_action("Idle")
    prev_q = {}
    for f in range(1, 49):
        begin_frame(f)
        ang = ((f - 1) / 47.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Body", loc=(0, 0, 0.03 * s), rot_xyz=(3.0 * s, 0, 2.5 * c))
        set_bone("Head", rot_xyz=(-4.0 * s, 0, 6.0 * c))
        set_bone("JawBot", rot_xyz=(8.0 + 10.0 * (0.5 + 0.5 * s), 0, 0))
        set_bone("Tongue1", rot_xyz=(8.0 * s, 0, 12.0 * c))
        set_bone("Tongue2", rot_xyz=(-12.0 * c, 0, 18.0 * s))
        set_bone("Tongue3", rot_xyz=(18.0 * s, 0, -24.0 * c))
        set_bone("Tail1", rot_xyz=(6.0 * s, 0, 10.0 * c))
        set_bone("Tail2", rot_xyz=(10.0 * c, 0, 14.0 * s))
        key_all(f, prev_q)

    # 2. Walk & Run (Savage fast quadrupedal crawler gallop with long claws & flapping long tongue!)
    for clip_name, n_frames, mult in (("Run", 15, 1.0), ("Walk", 23, 0.65)):
        act_m = new_action(clip_name)
        prev_q = {}
        for f in range(1, n_frames + 1):
            begin_frame(f)
            ang = ((f - 1) / float(n_frames - 1)) * 2.0 * math.pi
            s, c = math.sin(ang), math.cos(ang)
            set_bone("Body", loc=(0, -0.06 * abs(s) * mult, 0.12 * abs(s) * mult), rot_xyz=(-12.0 * s * mult, 0, 6.0 * c * mult))
            set_bone("Head", rot_xyz=(10.0 * s * mult, 0, -6.0 * c))
            set_bone("JawBot", rot_xyz=(16.0 + 12.0 * abs(s), 0, 0))
            set_bone("Tongue1", rot_xyz=(14.0 * s * mult, 0, 22.0 * c * mult))
            set_bone("Tongue2", rot_xyz=(-20.0 * c * mult, 0, -28.0 * s * mult))
            set_bone("Tongue3", rot_xyz=(26.0 * s * mult, 0, 34.0 * c * mult))
            set_bone("Tail1", rot_xyz=(18.0 * s * mult, 0, 14.0 * c))
            # Quadruped bounding gait (front pair vs hind pair!)
            set_bone("ArmL_Up", rot_xyz=(-38.0 * s * mult, -8.0, 0))
            set_bone("ArmL_Fore", rot_xyz=(22.0 * max(0.0, s) * mult, 0, 0))
            set_bone("ArmR_Up", rot_xyz=(-34.0 * math.sin(ang + 0.55) * mult, 8.0, 0))
            set_bone("ArmR_Fore", rot_xyz=(22.0 * max(0.0, math.sin(ang + 0.55)) * mult, 0, 0))
            set_bone("LegL", rot_xyz=(36.0 * s * mult, 0, 0))
            set_bone("LegR", rot_xyz=(32.0 * math.sin(ang + 0.55) * mult, 0, 0))
            key_all(f, prev_q)
        if clip_name == "Run":
            act_run = act_m

    # 3. Slash1 (Rear up -> Savage Double Long-Claw Rake + Long Tongue Lash!)
    act_slash = new_action("Slash1")
    prev_q = {}
    for f in range(1, 21):
        begin_frame(f)
        u = (f - 1) / 19.0
        sw = math.sin(u * math.pi)
        set_bone("Body", loc=(0, -0.14 * sw, 0.14 * sw), rot_xyz=(20.0 * sw, 0, 0))
        set_bone("JawBot", rot_xyz=(32.0 * sw, 0, 0))
        set_bone("Tongue1", rot_xyz=(-12.0 * sw, 0, 0), scale=(1.0, 1.0 + 0.45 * sw, 1.0))
        set_bone("Tongue2", rot_xyz=(10.0 * sw, 0, 0))
        set_bone("Tongue3", rot_xyz=(16.0 * sw, 0, 0))
        set_bone("ArmL_Up", rot_xyz=(-72.0 * sw, -22.0 * sw, -18.0 * sw))
        set_bone("ArmL_Fore", rot_xyz=(-18.0 * sw, 0, 0))
        set_bone("ArmR_Up", rot_xyz=(-72.0 * sw, 22.0 * sw, 18.0 * sw))
        set_bone("ArmR_Fore", rot_xyz=(-18.0 * sw, 0, 0))
        key_all(f, prev_q)

    # 4. Death
    act_death = new_action("Death")
    prev_q = {}
    for f in range(1, 27):
        begin_frame(f)
        u = (f - 1) / 25.0
        s = u * u * (3.0 - 2.0 * u)
        set_bone("Body", loc=(0, 0.12 * s, -0.34 * s), rot_xyz=(-24.0 * s, 82.0 * s, 14.0 * s))
        set_bone("JawBot", rot_xyz=(28.0 * s, 0, 0))
        set_bone("Tongue1", rot_xyz=(22.0 * s, 0, 20.0 * s))
        key_all(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [body_mesh], "Alien_Crawler")

    # Render Preview 1: 3/4 Side-Front (Shows huge cyclops eye, shaggy brown fur, curly tail, LONG claws & LONG red tongue!)
    cam_obj = _setup_studio_render(scene)
    cam_obj.location = Vector((-3.15, -2.35, 1.25))
    cam_obj.rotation_euler = (Vector((0.0, -0.22, 0.56)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    tmp_p = os.path.join(TMP_EXPORT_DIR, "Alien_Crawler_Preview.png")
    scene.render.filepath = tmp_p
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_p, os.path.join(ALIEN_DIR, "Alien_Crawler_Preview.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ALIEN CRAWLER (CYCLOPS BEAST + LONG CLAWS + LONG RED TONGUE) ===")
    build_alien_crawler()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ALIEN CRAWLER EXPORTED & RENDERED! ===")

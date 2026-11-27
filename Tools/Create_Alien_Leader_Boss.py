import math
import os
import shutil
import sys
import time
import bpy
import bmesh
from mathutils import Vector, Euler, Matrix

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import ChibiAnimeKit as kit

OUT_ROOT = os.path.abspath(os.path.join(SCRIPT_DIR, "..", "Assets", "GeneratedModels"))
ALIEN_DIR = os.path.join(OUT_ROOT, "Aliens")
TMP_EXPORT_DIR = os.path.join(SCRIPT_DIR, "_tmp_alien_leader_export")
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


def _make_tube_chain(name, pts, radii, mat, segments=12):
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


def _create_weighted_tube(name, pts, radii, mat, bone_chain, segments=12):
    """Creates a smooth organic tentacle tube with per-ring blended vertex weights across a 4-bone chain."""
    obj = _make_tube_chain(name, pts, radii, mat, segments=segments)
    mesh = obj.data
    n_rings = len(pts)
    n_bones = len(bone_chain)

    vgroups = {b: (obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)) for b in bone_chain}

    seg_vecs = [Vector(pts[i + 1]) - Vector(pts[i]) for i in range(n_rings - 1)]
    seg_lens = [v.length for v in seg_vecs]
    total_len = max(1e-5, sum(seg_lens))
    cum_u = [0.0]
    acc = 0.0
    for sl in seg_lens:
        acc += sl
        cum_u.append(acc / total_len)

    for v in mesh.vertices:
        p = v.co
        best_dist = 1e9
        best_u = 0.0
        for i in range(n_rings - 1):
            a = Vector(pts[i])
            ab = seg_vecs[i]
            ab_len2 = ab.length_squared
            t = 0.0 if ab_len2 < 1e-8 else max(0.0, min(1.0, (p - a).dot(ab) / ab_len2))
            proj = a + ab * t
            d = (p - proj).length_squared
            if d < best_dist:
                best_dist = d
                best_u = cum_u[i] + t * (cum_u[i + 1] - cum_u[i])

        f_idx = best_u * (n_bones - 1)
        idx0 = max(0, min(n_bones - 1, int(math.floor(f_idx))))
        idx1 = min(n_bones - 1, idx0 + 1)
        frac = f_idx - idx0
        if idx0 == idx1:
            vgroups[bone_chain[idx0]].add([v.index], 1.0, "REPLACE")
        else:
            vgroups[bone_chain[idx0]].add([v.index], 1.0 - frac, "REPLACE")
            vgroups[bone_chain[idx1]].add([v.index], frac, "REPLACE")

    return obj


def _create_split_orb(name, center, radius, mat, eye_center, split_z=1.34, is_upper=True):
    """
    Creates the upper skull hemisphere (weighted to Body) OR lower articulated jaw bowl (weighted to JawBot),
    leaving a genuine hollow cavernous mouth between them so JawBot swings WIDE OPEN to vomit!
    """
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)

    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=28, radius=radius)

    cx, cy, cz = center
    ec = Vector(eye_center)

    for v in bm.verts:
        d = v.co.normalized()
        bump = (
            0.022 * math.sin(d.x * 13.0 + d.z * 9.0)
            + 0.018 * math.cos(d.y * 11.0 - d.z * 14.0)
            + 0.014 * math.sin((d.x - d.y) * 19.0)
        )
        r_mod = radius + bump
        p = d * r_mod
        wp = Vector((cx + p.x, cy + p.y, cz + p.z))

        # Smile arch curve: mouth corners sweep upward along +-X
        smile_z = split_z + 0.30 * (wp.x * wp.x)

        if is_upper:
            # Upper skull stops cleanly at the smiling upper gum line on the front (-Y), while wrapping fully around back (+Y)!
            if wp.y < cy + 0.12 and wp.z < smile_z:
                # Fold lower front vertices inward & up to form the roof of the open mouth cavern!
                depth = smile_z - wp.z
                front_w = max(0.0, min(1.0, (cy + 0.12 - wp.y) / 0.50))
                wp.z = smile_z + 0.05 * front_w - depth * 0.12 * (1.0 - front_w)
                wp.y = wp.y + 0.38 * front_w * min(1.0, depth * 2.2)

            # Deep eye socket recess on front (-Y)
            if wp.y < cy - 0.18:
                dx = wp.x - ec.x
                dz = wp.z - ec.z
                ed = math.sqrt(dx * dx + dz * dz)
                if ed < 0.42:
                    indent = 0.25 * (math.cos((ed / 0.42) * math.pi * 0.5) ** 2)
                    wp.y += indent
        else:
            # Lower articulated jaw bowl: only occupies the lower front-to-mid hemisphere below smile_z!
            if wp.z > smile_z - 0.02:
                # Fold top vertices into the floor of the mouth
                over = wp.z - (smile_z - 0.02)
                wp.z = (smile_z - 0.02) - 0.04 * min(1.0, over * 2.5)
                wp.y = wp.y + 0.24 * min(1.0, over * 1.8)
            if wp.y > cy + 0.08:
                # Tuck back of jaw neatly inside the upper skull hinge
                back_t = min(1.0, (wp.y - (cy + 0.08)) / 0.65)
                wp.y = (cy + 0.08) - 0.12 * back_t
                wp.x *= (1.0 - 0.35 * back_t)
                wp.z = wp.z * (1.0 - 0.25 * back_t) + (split_z - 0.12) * (0.25 * back_t)
            # Sculpt pointed gothic chin taper at bottom
            if wp.z < cz - 0.45:
                t_c = min(1.0, ((cz - 0.45) - wp.z) / 0.40)
                wp.x *= (1.0 - 0.32 * t_c)
                wp.z -= 0.10 * t_c

        v.co = wp

    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    return obj


def _create_mushroom(name, base_pos, axis_dir, cap_radius, stalk_len, cap_h, m_stalk, m_cap, m_rim, m_gill, bone_name):
    """Builds a high-detail rounded purple-rose mushroom with curved stalk, visible gills, ribbed dome cap & creamy rim."""
    parts = []
    up = Vector(axis_dir).normalized()
    ref = Vector((1, 0, 0)) if abs(up.x) < 0.85 else Vector((0, 0, 1))
    side_a = up.cross(ref).normalized()
    side_b = up.cross(side_a).normalized()

    p0 = Vector(base_pos)
    p1 = p0 + up * (stalk_len * 0.48) + side_a * (stalk_len * 0.09)
    p2 = p0 + up * stalk_len

    # Thick curved fleshy stalk
    r_base = max(0.042, cap_radius * 0.26)
    r_mid = max(0.032, cap_radius * 0.19)
    r_top = max(0.038, cap_radius * 0.23)
    stalk = _make_tube_chain(f"{name}_Stalk", [p0, p1, p2], [r_base, r_mid, r_top], m_stalk, segments=12)
    parts.append((stalk, [(bone_name, 1.0)]))

    rot_mat = Matrix((
        (side_a.x, side_b.x, up.x, 0.0),
        (side_a.y, side_b.y, up.y, 0.0),
        (side_a.z, side_b.z, up.z, 0.0),
        (0.0,      0.0,      0.0,  1.0),
    ))

    segs = 28

    # Underside gills cone (deep burgundy-violet, clearly visible under the raised umbrella cap!)
    gill_mesh = bpy.data.meshes.new(f"{name}_Gill")
    gill_obj = bpy.data.objects.new(f"{name}_Gill", gill_mesh)
    bpy.context.scene.collection.objects.link(gill_obj)
    gill_obj.data.materials.append(m_gill)
    bm = bmesh.new()
    ring_in, ring_md, ring_ot = [], [], []
    for i in range(segs):
        a = (i / segs) * 2.0 * math.pi
        pleat = 1.0 + 0.03 * math.sin(a * 14.0)
        ca, sa = math.cos(a), math.sin(a)
        ring_in.append(bm.verts.new(p2 + (rot_mat @ Vector((ca * r_top * 0.92, sa * r_top * 0.92, -0.025)))))
        ring_md.append(bm.verts.new(p2 + (rot_mat @ Vector((ca * cap_radius * 0.56 * pleat, sa * cap_radius * 0.56 * pleat, cap_h * 0.20)))))
        ring_ot.append(bm.verts.new(p2 + (rot_mat @ Vector((ca * cap_radius * 0.95 * pleat, sa * cap_radius * 0.95 * pleat, cap_h * 0.02)))))
    for i in range(segs):
        ni = (i + 1) % segs
        bm.faces.new((ring_in[i], ring_in[ni], ring_md[ni], ring_md[i]))
        bm.faces.new((ring_md[i], ring_md[ni], ring_ot[ni], ring_ot[i]))
    bm.normal_update()
    bm.to_mesh(gill_mesh)
    bm.free()
    gill_mesh.polygons.foreach_set("use_smooth", [True] * len(gill_mesh.polygons))
    parts.append((gill_obj, [(bone_name, 1.0)]))

    # Smooth rounded dome cap with subtle radial striations (28 segments = silky round, ZERO hexagon look!)
    cap_mesh = bpy.data.meshes.new(f"{name}_Cap")
    cap_obj = bpy.data.objects.new(f"{name}_Cap", cap_mesh)
    bpy.context.scene.collection.objects.link(cap_obj)
    cap_obj.data.materials.append(m_cap)
    bm = bmesh.new()
    levels = [
        (0.99, 0.03),
        (0.92, 0.24),
        (0.76, 0.54),
        (0.52, 0.80),
        (0.24, 0.96),
    ]
    cap_rings = []
    for r_frac, h_frac in levels:
        ring = []
        for i in range(segs):
            a = (i / segs) * 2.0 * math.pi
            wavy = 1.0 + 0.028 * math.sin(a * 7.0) + 0.014 * math.cos(a * 14.0)
            rr = cap_radius * r_frac * wavy
            hh = cap_h * h_frac + 0.018 * cap_h * math.sin(a * 14.0) * r_frac
            ring.append(bm.verts.new(p2 + (rot_mat @ Vector((math.cos(a) * rr, math.sin(a) * rr, hh)))))
        cap_rings.append(ring)
    apex = bm.verts.new(p2 + (rot_mat @ Vector((0.0, 0.0, cap_h * 1.03))))
    for lv in range(len(cap_rings) - 1):
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((cap_rings[lv][i], cap_rings[lv][ni], cap_rings[lv + 1][ni], cap_rings[lv + 1][i]))
    for i in range(segs):
        ni = (i + 1) % segs
        bm.faces.new((cap_rings[-1][i], cap_rings[-1][ni], apex))
    bm.normal_update()
    bm.to_mesh(cap_mesh)
    bm.free()
    cap_mesh.polygons.foreach_set("use_smooth", [True] * len(cap_mesh.polygons))
    parts.append((cap_obj, [(bone_name, 1.0)]))

    # Delicate creamy-rose radial ridges + smooth frilled outer cap rim (matching concept art striations!)
    rim_pts = []
    for i in range(segs + 1):
        a = (i / segs) * 2.0 * math.pi
        wavy = 1.0 + 0.028 * math.sin(a * 7.0) + 0.014 * math.cos(a * 14.0)
        rr = cap_radius * 0.995 * wavy
        hh = cap_h * 0.035 + 0.020 * cap_h * math.sin(a * 7.0)
        rim_pts.append(p2 + (rot_mat @ Vector((math.cos(a) * rr, math.sin(a) * rr, hh))))
    rim = _make_tube_chain(f"{name}_Rim", rim_pts, [max(0.011, cap_radius * 0.048)] * len(rim_pts), m_rim, segments=6)
    parts.append((rim, [(bone_name, 1.0)]))

    # Subtle radial striations on hero caps (radius >= 0.24)
    if cap_radius >= 0.24:
        for ri in range(8):
            ra = (ri / 8.0) * 2.0 * math.pi
            ca, sa = math.cos(ra), math.sin(ra)
            s_pts = [
                p2 + (rot_mat @ Vector((ca * cap_radius * 0.28, sa * cap_radius * 0.28, cap_h * 0.94))),
                p2 + (rot_mat @ Vector((ca * cap_radius * 0.65, sa * cap_radius * 0.65, cap_h * 0.66))),
                p2 + (rot_mat @ Vector((ca * cap_radius * 0.95, sa * cap_radius * 0.95, cap_h * 0.14))),
            ]
            parts.append((_make_tube_chain(f"{name}_Stripe_{ri}", s_pts, [0.008, 0.011, 0.007], m_rim, segments=5), [(bone_name, 1.0)]))

    return parts


def _create_puffball_stalk(name, base_pos, axis_dir, stalk_len, ball_r, m_stalk, m_cap, bone_name):
    up = Vector(axis_dir).normalized()
    p0 = Vector(base_pos)
    p1 = p0 + up * (stalk_len * 0.55) + Vector((0.02, 0.01, 0.0))
    p2 = p0 + up * stalk_len
    stalk = _make_tube_chain(f"{name}_Stalk", [p0, p1, p2], [ball_r * 0.34, ball_r * 0.20, ball_r * 0.24], m_stalk, segments=8)
    bulb = kit.create_ellipsoid(f"{name}_Bulb", p2 + up * (ball_r * 0.65), (ball_r, ball_r, ball_r * 0.90), m_cap)
    return [(stalk, [(bone_name, 1.0)]), (bulb, [(bone_name, 1.0)])]


def _create_tooth(name, pos, rot_euler, sx, sy, sz, mat):
    """Builds a broad, chunky, slightly tapered cracked-bone grin tooth."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(mat)

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=2, use_grid_fill=True)
    rot_m = Euler(rot_euler, "XYZ").to_matrix()
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        # Broad tooth crown tapering gently toward the biting tip (z = -0.5)
        taper = 0.78 + 0.22 * (z + 0.5)
        round_front = 1.0 - 0.28 * (x * x * 4.0)
        nx = x * sx * taper
        ny = y * sy * (0.72 + 0.28 * round_front)
        nz = z * sz
        v.co = Vector(pos) + (rot_m @ Vector((nx, ny, nz)))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    return obj


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


def _bind_parts_to_bones(rig_obj, parts_with_weights, preweighted_objs, mesh_name):
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

    for obj in preweighted_objs:
        if obj is None:
            continue
        if obj.type == "MESH" and obj.data and len(obj.data.polygons) > 0:
            obj.data.polygons.foreach_set("use_smooth", [True] * len(obj.data.polygons))
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


def _setup_studio_render(scene):
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1024
    scene.render.resolution_y = 1024
    scene.render.film_transparent = False
    world = scene.world
    if world is None:
        world = bpy.data.worlds.new("AlienLeaderStudioWorld")
        scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.04, 0.035, 0.06, 1.0)
        bg.inputs[1].default_value = 0.65

    for lname, energy, loc, rot in (
        ("KeyLight", 2.8, (-3.2, -4.0, 4.5), (0.86, 0.14, -0.56)),
        ("FillLight", 1.3, (3.6, -2.5, 2.8), (1.02, -0.10, 0.85)),
        ("RimLight", 2.6, (0.6, 4.0, 3.2), (-0.95, 0.0, 0.08)),
    ):
        ld = bpy.data.lights.new(lname, type="SUN")
        ld.energy = energy
        lo = bpy.data.objects.new(lname, ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_euler = rot

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 45.0
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj
    return cam_obj


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
#  FLOATING FUNGAL CYCLOPS WORLD BOSS (Alien_Leader_Rigged — v2 Polished!)
#  - Front = -Y (Unity +Z), Back Tentacles = +Y (Unity -Z), Hover Z = 1.68m
#  - Split Skull + Articulated JawBot (hinges DOWNWARD WIDE OPEN to vomit!)
#  - High-contrast crimson flesh, aged-ivory porous bone ribs, wide grinning teeth,
#    deep crimson + glowing neon-magenta veined EyeBall, silky 28-seg striped mushrooms,
#    and 8 thick animated 4-bone wriggling back tentacles!
# ==============================================================================
def build_alien_leader_boss():
    scene = kit.reset_scene()
    scene.render.fps = 24

    # --- Rich High-Contrast Eldritch Fungal Palette (matching reference image!) ---
    m_flesh = make_mat("Mat_AlienLeader_CrimsonFlesh", (0.24, 0.022, 0.085, 1.0), roughness=0.40, coat=0.30)
    m_gum = make_mat("Mat_AlienLeader_GumDeep", (0.19, 0.015, 0.055, 1.0), roughness=0.28, coat=0.45)
    m_throat = make_mat("Mat_AlienLeader_ThroatGlow", (0.18, 0.015, 0.12, 1.0), roughness=0.38, emit_rgb=(0.68, 0.06, 0.42), emit_strength=0.65)
    m_tongue = make_mat("Mat_AlienLeader_TongueWet", (0.48, 0.045, 0.22, 1.0), roughness=0.22, coat=0.80, emit_rgb=(0.40, 0.04, 0.20), emit_strength=0.25)
    m_bone = make_mat("Mat_AlienLeader_BoneLattice", (0.76, 0.63, 0.45, 1.0), roughness=0.66)
    m_bone_dark = make_mat("Mat_AlienLeader_BoneCrevice", (0.46, 0.34, 0.24, 1.0), roughness=0.72)
    m_teeth = make_mat("Mat_AlienLeader_TeethIvory", (0.82, 0.71, 0.52, 1.0), roughness=0.38, coat=0.20)

    # Giant Cyclops Eye: dark glossy wine sclera + deep crimson iris + intense electric-magenta radial veins!
    m_sclera = make_mat("Mat_AlienLeader_EyeSclera", (0.08, 0.01, 0.04, 1.0), roughness=0.10, coat=0.95)
    m_iris_outer = make_mat("Mat_AlienLeader_EyeIrisDark", (0.34, 0.02, 0.16, 1.0), roughness=0.14, coat=0.85, emit_rgb=(0.55, 0.04, 0.28), emit_strength=0.9)
    m_iris_glow = make_mat("Mat_AlienLeader_EyeIrisGlow", (0.88, 0.08, 0.48, 1.0), roughness=0.14, coat=0.85, emit_rgb=(1.0, 0.12, 0.56), emit_strength=1.9)
    m_iris_vein = make_mat("Mat_AlienLeader_EyeVeinBright", (1.0, 0.42, 0.80, 1.0), roughness=0.12, emit_rgb=(1.0, 0.35, 0.78), emit_strength=3.4)
    m_pupil = make_mat("Mat_AlienLeader_EyePupil", (0.01, 0.002, 0.015, 1.0), roughness=0.06, coat=0.98)
    m_glint = make_mat("Mat_AlienLeader_EyeGlint", (1.0, 0.96, 1.0, 1.0), roughness=0.05, emit_rgb=(1.0, 0.95, 1.0), emit_strength=3.5)

    # Mushroom & Spore materials
    m_shroom_cap = make_mat("Mat_AlienLeader_ShroomCap", (0.52, 0.16, 0.32, 1.0), roughness=0.52)
    m_shroom_rim = make_mat("Mat_AlienLeader_ShroomRim", (0.86, 0.72, 0.68, 1.0), roughness=0.58)
    m_shroom_gill = make_mat("Mat_AlienLeader_ShroomGill", (0.24, 0.06, 0.14, 1.0), roughness=0.70)
    m_shroom_stalk = make_mat("Mat_AlienLeader_ShroomStalk", (0.46, 0.22, 0.26, 1.0), roughness=0.60)

    # Back Tentacle materials
    m_tentacle = make_mat("Mat_AlienLeader_TentacleFlesh", (0.30, 0.045, 0.16, 1.0), roughness=0.38, coat=0.30)
    m_tentacle_tip = make_mat("Mat_AlienLeader_TentacleGlow", (0.96, 0.16, 0.68, 1.0), roughness=0.20, emit_rgb=(0.98, 0.18, 0.72), emit_strength=2.4)

    ORB_C = Vector((0.0, 0.0, 1.68))
    ORB_R = 0.84
    EYE_C = Vector((0.0, -0.57, 1.84))
    JAW_HINGE = Vector((0.0, -0.05, 1.38))

    # --- 8 Sweeping Back Tentacles (trailing behind +Y so super visible top-down and 3/4!) ---
    tentacle_specs = [
        (0, -34,  0.46, -0.58, 2.00, 0.135),
        (1,  34,  0.46,  0.58, 2.00, 0.135),
        (2, -74,  0.14, -0.84, 2.15, 0.145),
        (3,  74,  0.14,  0.84, 2.15, 0.145),
        (4, -52, -0.24, -0.70, 1.95, 0.132),
        (5,  52, -0.24,  0.70, 1.95, 0.132),
        (6, -18, -0.46, -0.34, 1.90, 0.126),
        (7,  18, -0.46,  0.34, 1.90, 0.126),
    ]

    # Note bone orientation:
    # EyeBall & JawBot point toward -Y with roll (0, 0, 1) -> local +Y is forward (-Y), local +Z is UP, local +X is RIGHT (-X)!
    # Therefore for JawBot: NEGATIVE rot_x swings the jaw tip DOWNWARD (opening the mouth WIDE OPEN!)
    bone_defs = [
        ("Root",        (0.0, 0.0, 0.0),    (0.0, 0.0, 0.40),     None,   (0, -1, 0)),
        ("Body",        (0.0, 0.0, 1.68),   (0.0, 0.0, 2.38),     "Root", (0, -1, 0)),
        ("EyeBall",     tuple(EYE_C),       (EYE_C.x, EYE_C.y - 0.45, EYE_C.z), "Body", (0, 0, 1)),
        ("JawBot",      tuple(JAW_HINGE),   (0.0, -0.82, 1.16),   "Body", (0, 0, 1)),
        ("ShroomTopL",  (-0.32, -0.14, 2.40), (-0.44, -0.20, 2.86), "Body", (0, -1, 0)),
        ("ShroomTopR",  ( 0.24, -0.06, 2.40), ( 0.34, -0.08, 2.80), "Body", (0, -1, 0)),
        ("ShroomSideL", (-0.72, -0.18, 2.08), (-1.10, -0.26, 2.36), "Body", (0, -1, 0)),
        ("ShroomSideR", ( 0.68, -0.22, 1.72), ( 1.06, -0.30, 1.90), "Body", (0, -1, 0)),
    ]

    tentacle_curves = []
    for idx, ang_deg, elev_z, spread_x, length, thick in tentacle_specs:
        rad = math.radians(ang_deg)
        sx = math.sin(rad)
        sy = math.cos(rad)

        root_p = Vector((
            ORB_C.x + sx * (ORB_R * 0.72),
            ORB_C.y + sy * (ORB_R * 0.72) + 0.14,
            ORB_C.z + elev_z
        ))

        c_pts = []
        for s in range(5):
            u = s / 4.0
            curve_wave = math.sin(u * math.pi * 1.15) * 0.22 * (1.0 if idx % 2 == 0 else -1.0)
            droop = -0.30 * (u * u) + elev_z * 0.42 * u
            c_pts.append(Vector((
                root_p.x + spread_x * (u ** 0.85) + curve_wave * 0.35,
                root_p.y + length * u,
                root_p.z + droop + 0.18 * math.sin(u * math.pi)
            )))

        chain_names = []
        parent_b = "Body"
        for seg in range(4):
            bname = f"Tent_{idx}_{seg}"
            bone_defs.append((bname, tuple(c_pts[seg]), tuple(c_pts[seg + 1]), parent_b, (0, 0, 1)))
            chain_names.append(bname)
            parent_b = bname
        tentacle_curves.append((idx, c_pts, thick, chain_names))

    rig_obj = _build_bone_tree("AlienLeader_Rig", bone_defs)

    parts = []
    preweighted_objs = []

    def surface_pt(lon_deg, lat_deg, r_off=0.030):
        lon = math.radians(lon_deg)
        lat = math.radians(lat_deg)
        r = ORB_R + r_off
        return Vector((
            r * math.cos(lat) * math.sin(lon),
            -r * math.cos(lat) * math.cos(lon),
            ORB_C.z + r * math.sin(lat)
        ))

    # --------------------------------------------------------------------------
    # 1. SPLIT ORB: UPPER SKULL (Body) + LOWER JAW BOWL (JawBot) + GLOWING THROAT
    # --------------------------------------------------------------------------
    upper_orb = _create_split_orb("UpperSkullFlesh", ORB_C, ORB_R, m_flesh, EYE_C, split_z=1.34, is_upper=True)
    parts.append((upper_orb, [("Body", 1.0)]))

    lower_jaw_orb = _create_split_orb("LowerJawFlesh", ORB_C, ORB_R * 0.985, m_flesh, EYE_C, split_z=1.34, is_upper=False)
    parts.append((lower_jaw_orb, [("JawBot", 1.0)]))

    # Glowing cavernous bio-gullet inside the mouth (exposed dramatically when JawBot swings down!)
    throat = kit.create_ellipsoid("ThroatCavernGlow", Vector((0.0, -0.24, 1.30)), (0.48, 0.36, 0.28), m_throat)
    parts.append((throat, [("Body", 1.0)]))

    # Fleshy writhing vomit-tongue anchored to lower jaw
    tongue_pts = [
        Vector((0.0, -0.20, 1.24)),
        Vector((0.0, -0.44, 1.21)),
        Vector((0.0, -0.64, 1.17)),
    ]
    parts.append((_make_tube_chain("InnerTongue", tongue_pts, [0.16, 0.13, 0.06], m_tongue, segments=10), [("JawBot", 1.0)]))

    # --------------------------------------------------------------------------
    # 2. SCULPTED AGED-BONE POROUS LATTICE SHELL (High-contrast ivory over crimson!)
    # --------------------------------------------------------------------------
    # Heavy organic eye-socket bone rim
    eye_ring_pts = []
    eye_ring_rads = []
    n_er = 32
    for i in range(n_er + 1):
        a = (i / n_er) * 2.0 * math.pi
        rx = 0.44 + 0.025 * math.sin(a * 5.0)
        rz = 0.39 + 0.020 * math.cos(a * 4.0)
        ex = math.cos(a) * rx
        ez = EYE_C.z + math.sin(a) * rz
        dist_sq = ex * ex + (ez - ORB_C.z) ** 2
        ey = ORB_C.y - math.sqrt(max(0.08, (ORB_R + 0.032) ** 2 - dist_sq))
        eye_ring_pts.append((ex, ey, ez))
        eye_ring_rads.append(0.056 + 0.016 * math.sin(a * 6.0))
    parts.append((_make_tube_chain("BoneEyeOrbitRing", eye_ring_pts, eye_ring_rads, m_bone, segments=8), [("Body", 1.0)]))

    # Dark fleshy eyelid crease tucked inside bone rim
    lid_pts = []
    for i in range(29):
        a = (i / 28.0) * 2.0 * math.pi
        ex = math.cos(a) * 0.375
        ez = EYE_C.z + math.sin(a) * 0.325
        dist_sq = ex * ex + (ez - ORB_C.z) ** 2
        ey = ORB_C.y - math.sqrt(max(0.08, (ORB_R - 0.005) ** 2 - dist_sq))
        lid_pts.append((ex, ey, ez))
    parts.append((_make_tube_chain("FleshyEyeSocketRim", lid_pts, [0.048] * len(lid_pts), m_gum, segments=8), [("Body", 1.0)]))

    # Organic bone lattice arches & porous cross-ribs wrapping the upper skull (Body)
    lattice_arcs = [
        # Brow ridges sweeping over the giant cyclops eye
        ([surface_pt(lon, 40 + 12 * math.cos(math.radians(lon * 1.5)), 0.038) for lon in range(-92, 93, 14)], 0.050),
        ([surface_pt(lon, 58 + 8 * math.cos(math.radians(lon * 1.3)), 0.034) for lon in range(-108, 109, 18)], 0.045),
        ([surface_pt(lon, 74, 0.030) for lon in range(-115, 116, 23)], 0.038),
        # Upper-lip smiling bone crest right above upper gums
        ([surface_pt(lon, -17 + 21 * (abs(lon) / 68.0) ** 1.5, 0.046) for lon in range(-68, 69, 10)], 0.052),
        # Sweeping organic cheekbone & temple lattice struts
        ([surface_pt(-44 - i * 11, 46 - i * 14, 0.036) for i in range(6)], 0.046),
        ([surface_pt( 44 + i * 11, 46 - i * 14, 0.036) for i in range(6)], 0.046),
        ([surface_pt(-68 - i * 11, 58 - i * 15, 0.032) for i in range(6)], 0.042),
        ([surface_pt( 68 + i * 11, 58 - i * 15, 0.032) for i in range(6)], 0.042),
        ([surface_pt(-22 - i * 15, 32 + i * 11, 0.036) for i in range(5)], 0.036),
        ([surface_pt( 22 + i * 15, 32 + i * 11, 0.036) for i in range(5)], 0.036),
        ([surface_pt(-32 - i * 12, -2 + i * 12, 0.036) for i in range(5)], 0.038),
        ([surface_pt( 32 + i * 12, -2 + i * 12, 0.036) for i in range(5)], 0.038),
        # Rear bone ribcage framing the back tentacles
        ([surface_pt(180 + lon, 42 - abs(lon) * 0.35, 0.032) for lon in range(-65, 66, 18)], 0.044),
        ([surface_pt(180 + lon, -8 - abs(lon) * 0.25, 0.032) for lon in range(-65, 66, 18)], 0.042),
    ]
    for li, (arc_pts, rad_b) in enumerate(lattice_arcs):
        rads = [rad_b * (0.68 + 0.42 * math.sin((k / max(1, len(arc_pts) - 1)) * math.pi)) for k in range(len(arc_pts))]
        mat_use = m_bone if li % 3 != 2 else m_bone_dark
        parts.append((_make_tube_chain(f"BoneLattice_{li}", arc_pts, rads, mat_use, segments=7), [("Body", 1.0)]))

    # --------------------------------------------------------------------------
    # 3. GIANT HIGH-CONTRAST VEINED CYCLOPS EYEBALL (Weighted to `EyeBall`)
    # --------------------------------------------------------------------------
    eye_r = 0.355
    # Dark obsidian-crimson glossy eyeball sphere
    parts.append((kit.create_ellipsoid("EyeBall_Sclera", EYE_C, (eye_r, eye_r, eye_r), m_sclera), [("EyeBall", 1.0)]))
    # Deep wine-crimson outer iris ring
    parts.append((kit.create_ellipsoid("EyeBall_IrisDark", EYE_C + Vector((0.0, -eye_r * 0.66, 0.0)), (0.275, 0.145, 0.275), m_iris_outer), [("EyeBall", 1.0)]))
    # Glowing neon-magenta inner iris core ring
    parts.append((kit.create_ellipsoid("EyeBall_IrisGlow", EYE_C + Vector((0.0, -eye_r * 0.80, 0.0)), (0.195, 0.100, 0.195), m_iris_glow), [("EyeBall", 1.0)]))

    # 18 crisp electric-pink branching radial veins standing proud over the dark iris!
    for vi in range(18):
        va = (vi / 18.0) * 2.0 * math.pi
        ca, sa = math.cos(va), math.sin(va)
        j1 = 0.16 * math.sin(vi * 2.3)
        j2 = -0.14 * math.cos(vi * 1.9)
        vp0 = EYE_C + Vector((ca * 0.105, -eye_r * 1.01, sa * 0.105))
        vp1 = EYE_C + Vector((math.cos(va + j1) * 0.180, -eye_r * 0.94, math.sin(va + j1) * 0.180))
        vp2 = EYE_C + Vector((math.cos(va + j2) * 0.258, -eye_r * 0.82, math.sin(va + j2) * 0.258))
        vein = _make_tube_chain(f"EyeVein_{vi}", [vp0, vp1, vp2], [0.011, 0.013, 0.006], m_iris_vein, segments=5)
        parts.append((vein, [("EyeBall", 1.0)]))

    # Deep black glossy dilated pupil + double specular highlight beads
    parts.append((kit.create_ellipsoid("EyeBall_Pupil", EYE_C + Vector((0.0, -eye_r * 0.96, 0.0)), (0.112, 0.068, 0.112), m_pupil), [("EyeBall", 1.0)]))
    parts.append((kit.create_ellipsoid("EyeBall_Glint1", EYE_C + Vector((-0.072, -eye_r * 1.03, 0.088)), (0.040, 0.022, 0.028), m_glint), [("EyeBall", 1.0)]))
    parts.append((kit.create_ellipsoid("EyeBall_Glint2", EYE_C + Vector((0.068, -eye_r * 1.01, -0.056)), (0.020, 0.015, 0.020), m_glint), [("EyeBall", 1.0)]))

    # --------------------------------------------------------------------------
    # 4. WIDE CHESHIRE GRINNING MOUTH (Upper = Body, Lower Jaw + Chin = JawBot!)
    # --------------------------------------------------------------------------
    # Upper dark crimson gums (Body)
    upper_gum_pts = [surface_pt(t * 62.0, -19.5 + 20.0 * (t * t), 0.012) for t in [(i / 14.0) * 2.0 - 1.0 for i in range(15)]]
    parts.append((_make_tube_chain("UpperGumArch", upper_gum_pts, [0.056] * len(upper_gum_pts), m_gum, segments=8), [("Body", 1.0)]))

    # 10 Broad Tightly-Packed Upper Ivory Teeth (iconic grin from concept art!)
    n_teeth = 10
    for ti in range(n_teeth):
        t = ((ti + 0.5) / n_teeth) * 2.0 - 1.0
        lon = t * 56.0
        lat = -22.5 + 20.0 * (t * t)
        p_t = surface_pt(lon, lat, 0.032)
        cf = 1.0 - 0.34 * abs(t)
        tooth_h = 0.21 * cf
        tooth_w = 0.118 * (0.82 + 0.24 * cf)
        tooth_d = 0.076
        tooth = _create_tooth(
            f"UpperTooth_{ti}",
            p_t + Vector((0.0, 0.0, -tooth_h * 0.42)),
            (math.radians(-8.0), 0.0, math.radians(-lon)),
            tooth_w, tooth_d, tooth_h, m_teeth
        )
        parts.append((tooth, [("Body", 1.0)]))

    # Lower Gums, Lower Bone Chin Rim, Pointed Gothic V-Chin & 10 Lower Teeth (ALL on `JawBot`!)
    lower_gum_pts = [surface_pt(t * 60.0, -39.0 + 26.0 * (t * t), 0.014) for t in [(i / 14.0) * 2.0 - 1.0 for i in range(15)]]
    lower_rim_pts = [surface_pt(t * 62.0, -47.0 + 28.0 * (t * t), 0.044) for t in [(i / 14.0) * 2.0 - 1.0 for i in range(15)]]
    parts.append((_make_tube_chain("LowerGumArch", lower_gum_pts, [0.056] * len(lower_gum_pts), m_gum, segments=8), [("JawBot", 1.0)]))
    parts.append((_make_tube_chain("LowerJawBoneRim", lower_rim_pts, [0.058] * len(lower_rim_pts), m_bone, segments=8), [("JawBot", 1.0)]))

    # Sharp pointed gothic bone chin spike + converging jawbone ridges
    chin_pts = [
        Vector((0.0, -0.58, 1.05)),
        Vector((0.0, -0.50, 0.86)),
        Vector((0.0, -0.36, 0.64)),
    ]
    parts.append((_make_tube_chain("BoneChinSpike", chin_pts, [0.135, 0.080, 0.014], m_bone, segments=8), [("JawBot", 1.0)]))
    for side in (-1.0, 1.0):
        jstrut = [
            Vector((side * 0.62, -0.30, 1.28)),
            Vector((side * 0.36, -0.52, 0.98)),
            Vector((0.0, -0.46, 0.76)),
        ]
        parts.append((_make_tube_chain(f"JawBoneStrut_{side}", jstrut, [0.048, 0.056, 0.042], m_bone, segments=7), [("JawBot", 1.0)]))

    # 10 Broad Lower Ivory Teeth (move cleanly downward with JawBot when vomiting!)
    for ti in range(n_teeth):
        t = ((ti + 0.5) / n_teeth) * 2.0 - 1.0
        lon = t * 54.0
        lat = -35.5 + 25.0 * (t * t)
        p_t = surface_pt(lon, lat, 0.030)
        cf = 1.0 - 0.34 * abs(t)
        tooth_h = 0.195 * cf
        tooth_w = 0.112 * (0.82 + 0.24 * cf)
        tooth_d = 0.072
        tooth = _create_tooth(
            f"LowerTooth_{ti}",
            p_t + Vector((0.0, 0.0, tooth_h * 0.40)),
            (math.radians(172.0), 0.0, math.radians(-lon)),
            tooth_w, tooth_d, tooth_h, m_teeth
        )
        parts.append((tooth, [("JawBot", 1.0)]))

    # --------------------------------------------------------------------------
    # 5. SILKY ROUND STRIATED MUSHROOMS & CROWN PUFFBALLS (Longer stalks showing dark gills!)
    # --------------------------------------------------------------------------
    hero_mushrooms = [
        # (name, lon, lat, axis_tilt, cap_r, stalk_len, cap_h, bone)
        ("HeroCap_TopLeft",   -24,  68, (-0.26, -0.10, 0.96), 0.42, 0.40, 0.20, "ShroomTopL"),
        ("HeroCap_UpperLeft", -64,  44, (-0.64, -0.14, 0.75), 0.36, 0.34, 0.17, "ShroomSideL"),
        ("HeroCap_RightMid",   62,  14, ( 0.68, -0.18, 0.71), 0.38, 0.38, 0.18, "ShroomSideR"),
        ("HeroCap_CrownMid",   16,  78, ( 0.16, -0.04, 0.98), 0.24, 0.26, 0.12, "ShroomTopR"),
        ("MedCap_LeftMid",    -82,   8, (-0.82, -0.14, 0.55), 0.22, 0.24, 0.11, "ShroomSideL"),
        ("MedCap_LeftLow",    -74, -20, (-0.84, -0.18, 0.50), 0.20, 0.22, 0.10, "Body"),
        ("MedCap_RightLow",    72, -16, ( 0.80, -0.20, 0.56), 0.23, 0.24, 0.11, "ShroomSideR"),
        ("SmallCap_ChinL",    -38, -56, (-0.48, -0.35, 0.15), 0.15, 0.17, 0.08, "JawBot"),
        ("SmallCap_ChinR",     32, -52, ( 0.45, -0.36, 0.18), 0.16, 0.17, 0.08, "JawBot"),
        ("BackCap_Top",       155,  56, ( 0.26,  0.55, 0.79), 0.26, 0.28, 0.13, "Body"),
        ("BackCap_Left",     -142,  34, (-0.48,  0.60, 0.64), 0.24, 0.24, 0.12, "Body"),
    ]
    for mname, lon, lat, axis_t, cap_r, s_len, cap_h, bname in hero_mushrooms:
        base_p = surface_pt(lon, lat, -0.06)
        parts.extend(_create_mushroom(mname, base_p, axis_t, cap_r, s_len, cap_h, m_shroom_stalk, m_shroom_cap, m_shroom_rim, m_shroom_gill, bname))

    puffballs = [
        ("Puff_Crown1",   4,  84, ( 0.05, -0.05, 0.99), 0.34, 0.085, "ShroomTopR"),
        ("Puff_Crown2",  28,  76, ( 0.26, -0.06, 0.96), 0.28, 0.072, "ShroomTopR"),
        ("Puff_Crown3",  44,  64, ( 0.44, -0.08, 0.89), 0.24, 0.058, "ShroomTopR"),
        ("Puff_Crown4", -48,  60, (-0.46, -0.10, 0.88), 0.22, 0.055, "ShroomTopL"),
        ("Puff_Left1",  -86,  28, (-0.86, -0.12, 0.48), 0.22, 0.052, "ShroomSideL"),
        ("Puff_Right1",  82,  34, ( 0.82, -0.14, 0.54), 0.22, 0.054, "ShroomSideR"),
    ]
    for pname, lon, lat, axis_t, s_len, br, bname in puffballs:
        base_p = surface_pt(lon, lat, -0.04)
        parts.extend(_create_puffball_stalk(pname, base_p, axis_t, s_len, br, m_shroom_stalk, m_shroom_rim, bname))

    # --------------------------------------------------------------------------
    # 6. 8 ANIMATED BACK TENTACLES (4-bone smoothly skinned chains!)
    # --------------------------------------------------------------------------
    for idx, c_pts, thick, chain_names in tentacle_curves:
        dense_pts = []
        dense_rads = []
        n_samples = 15
        for si in range(n_samples):
            u = si / float(n_samples - 1)
            f = u * (len(c_pts) - 1)
            i0 = min(len(c_pts) - 2, int(math.floor(f)))
            t_seg = f - i0
            dense_pts.append(c_pts[i0].lerp(c_pts[i0 + 1], t_seg))
            dense_rads.append(thick * ((1.0 - u) ** 0.78) + 0.022 * u)

        tent_obj = _create_weighted_tube(f"BackTentacle_{idx}", dense_pts, dense_rads, m_tentacle, chain_names, segments=12)
        preweighted_objs.append(tent_obj)

        # Glowing magenta bio-bulb tip + 3 glowing sucker nodules along each tentacle!
        parts.append((kit.create_ellipsoid(f"TentTipGlow_{idx}", dense_pts[-1], (0.048, 0.072, 0.048), m_tentacle_tip), [(chain_names[-1], 1.0)]))
        for ni, (sample_i, seg_b) in enumerate(((4, chain_names[1]), (8, chain_names[2]), (12, chain_names[3]))):
            nod_pos = dense_pts[sample_i] + Vector((0.0, 0.0, -dense_rads[sample_i] * 0.85))
            parts.append((kit.create_ellipsoid(f"TentSucker_{idx}_{ni}", nod_pos, (0.036, 0.036, 0.026), m_tentacle_tip), [(seg_b, 1.0)]))

    body_mesh = _bind_parts_to_bones(rig_obj, parts, preweighted_objs, "AlienLeader_Body")

    # ==========================================================================
    #  ANIMATIONS (NOTE: JawBot NEGATIVE rot_x = OPEN JAW WIDE DOWNWARD!)
    # ==========================================================================
    begin_frame, set_bone, key_all, new_action = _make_rig_helpers(scene, rig_obj)

    def animate_tentacles(ang, amp_mult=1.0, stream_back_deg=0.0, phase_speed=1.0):
        for idx, _, _, chain_names in tentacle_curves:
            phase = idx * 0.82
            side_sign = -1.0 if idx % 2 == 0 else 1.0
            for seg, bname in enumerate(chain_names):
                w_yaw = math.sin(ang * phase_speed - seg * 0.95 + phase) * (11.0 + seg * 8.0) * amp_mult
                w_pitch = math.cos(ang * phase_speed - seg * 0.85 + phase * 1.3) * (9.0 + seg * 6.5) * amp_mult
                base_pull = stream_back_deg * (0.35 if seg == 0 else 0.15)
                set_bone(bname, rot_xyz=(w_pitch + base_pull, 0.0, w_yaw * side_sign))

    def animate_shrooms(ang, mult=1.0):
        s, c = math.sin(ang), math.cos(ang)
        set_bone("ShroomTopL",  rot_xyz=(4.5 * s * mult, 0.0,  5.0 * c * mult))
        set_bone("ShroomTopR",  rot_xyz=(-4.0 * c * mult, 0.0, -4.5 * s * mult))
        set_bone("ShroomSideL", rot_xyz=(5.0 * c * mult, 4.0 * s * mult, 0.0))
        set_bone("ShroomSideR", rot_xyz=(-5.0 * s * mult, -4.0 * c * mult, 0.0))

    # 1. Idle (Weightless Hover + Creepy Eye Rolling Left/Right/Up/Down + Undulating Back Tentacles!)
    act_idle = new_action("Idle")
    prev_q = {}
    n_idle = 48
    for f in range(1, n_idle + 1):
        begin_frame(f)
        u = (f - 1) / float(n_idle - 1)
        ang = u * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Body", loc=(0.06 * s, 0.03 * c, 0.15 * s), rot_xyz=(4.5 * s, 3.8 * c, 4.0 * math.sin(ang * 0.5)))
        # Creepy rapid eye darting!
        if u < 0.22:
            eye_yaw, eye_pitch = -26.0, 8.0
        elif u < 0.34:
            t = (u - 0.22) / 0.12
            eye_yaw, eye_pitch = -26.0 + 52.0 * t, 8.0 - 18.0 * t
        elif u < 0.56:
            eye_yaw, eye_pitch = 26.0, -10.0
        elif u < 0.68:
            t = (u - 0.56) / 0.12
            eye_yaw, eye_pitch = 26.0 - 26.0 * t, -10.0 + 26.0 * t
        elif u < 0.84:
            eye_yaw, eye_pitch = 0.0, 16.0
        else:
            t = (u - 0.84) / 0.16
            eye_yaw, eye_pitch = -26.0 * t, 16.0 - 8.0 * t
        set_bone("EyeBall", rot_xyz=(eye_pitch, 0.0, eye_yaw))
        # Subtle breathing grin (negative X = slightly open downward)
        set_bone("JawBot", rot_xyz=(-(2.0 + 4.5 * (0.5 + 0.5 * s)), 0.0, 0.0))
        animate_tentacles(ang, amp_mult=1.10, stream_back_deg=0.0, phase_speed=1.0)
        animate_shrooms(ang, mult=1.0)
        key_all(f, prev_q)

    # 2. Walk (Ethereal Floating Glide — leans forward smoothly, NO walking feet!)
    act_walk = new_action("Walk")
    prev_q = {}
    n_walk = 36
    for f in range(1, n_walk + 1):
        begin_frame(f)
        u = (f - 1) / float(n_walk - 1)
        ang = u * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Body", loc=(0.08 * c, -0.05 * s, 0.13 * s + 0.10), rot_xyz=(-14.0 + 4.0 * s, 6.5 * c, 5.0 * s))
        set_bone("EyeBall", rot_xyz=(6.0 * s, 0.0, 24.0 * s))
        set_bone("JawBot", rot_xyz=(-(5.0 + 5.5 * (0.5 + 0.5 * c)), 0.0, 0.0))
        animate_tentacles(ang, amp_mult=1.30, stream_back_deg=-10.0, phase_speed=1.0)
        animate_shrooms(ang, mult=1.2)
        key_all(f, prev_q)

    # 3. Run (Fast Floating Pursuit — aggressive forward pitch, tentacles whipping rapidly!)
    act_run = new_action("Run")
    prev_q = {}
    n_run = 24
    for f in range(1, n_run + 1):
        begin_frame(f)
        u = (f - 1) / float(n_run - 1)
        ang = u * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        set_bone("Body", loc=(0.06 * c, -0.10, 0.11 * s + 0.06), rot_xyz=(-20.0 + 5.0 * s, 8.0 * c, 4.0 * s))
        set_bone("EyeBall", rot_xyz=(14.0 + 4.0 * s, 0.0, 14.0 * c))
        set_bone("JawBot", rot_xyz=(-(10.0 + 7.5 * abs(s)), 0.0, 0.0))
        animate_tentacles(ang, amp_mult=1.60, stream_back_deg=-18.0, phase_speed=1.0)
        animate_shrooms(ang, mult=1.5)
        key_all(f, prev_q)

    # 4. Vomit (Summon Minions! Windup -> Jaw hinges DOWNWARD -56° MASSIVELY WIDE OPEN + violent heaves!)
    act_vomit = new_action("Vomit")
    prev_q = {}
    n_vomit = 36
    for f in range(1, n_vomit + 1):
        begin_frame(f)
        u = (f - 1) / float(n_vomit - 1)
        ang = u * 4.0 * math.pi
        if u < 0.26:
            t = u / 0.26
            e = t * t * (3.0 - 2.0 * t)
            set_bone("Body", loc=(0.0, 0.18 * e, 0.24 * e), rot_xyz=(20.0 * e, 0.0, 0.0), scale=(1.08 * e + (1.0 - e), 1.08 * e + (1.0 - e), 0.94 * e + (1.0 - e)))
            set_bone("JawBot", rot_xyz=(-18.0 * e, 0.0, 0.0))
            set_bone("EyeBall", rot_xyz=(18.0 * e, 0.0, 0.0))
            animate_tentacles(ang, amp_mult=0.9, stream_back_deg=14.0 * e, phase_speed=1.0)
            animate_shrooms(ang, mult=1.3)
        elif u < 0.78:
            t = (u - 0.26) / 0.52
            heave = math.sin(t * math.pi * 3.0)
            pulse = 0.5 + 0.5 * abs(heave)
            set_bone(
                "Body",
                loc=(0.0, -0.22 - 0.08 * pulse, 0.06 - 0.10 * pulse),
                rot_xyz=(-16.0 - 6.0 * heave, 4.0 * heave, 0.0),
                scale=(1.0 + 0.07 * pulse, 1.0 - 0.05 * pulse, 1.0 + 0.06 * pulse),
            )
            # NEGATIVE X = JAW SWINGS WIDE OPEN DOWNWARD (-54° to -64°)!
            set_bone("JawBot", rot_xyz=(-(52.0 + 12.0 * pulse), 0.0, 0.0))
            set_bone("EyeBall", rot_xyz=(-8.0 + 14.0 * heave, 0.0, 22.0 * math.cos(t * math.pi * 4.0)))
            animate_tentacles(ang, amp_mult=2.2, stream_back_deg=-24.0, phase_speed=1.5)
            animate_shrooms(ang, mult=2.2)
        else:
            t = (u - 0.78) / 0.22
            inv = 1.0 - (t * t * (3.0 - 2.0 * t))
            set_bone("Body", loc=(0.0, -0.22 * inv, 0.04 * inv), rot_xyz=(-16.0 * inv, 0.0, 0.0))
            set_bone("JawBot", rot_xyz=(-52.0 * inv, 0.0, 0.0))
            set_bone("EyeBall", rot_xyz=(-8.0 * inv, 0.0, 0.0))
            animate_tentacles(ang, amp_mult=1.0 + 0.8 * inv, stream_back_deg=-15.0 * inv, phase_speed=1.0)
            animate_shrooms(ang, mult=1.0 + 0.8 * inv)
        key_all(f, prev_q)

    # 5. Slash1 (Giant Snapping Chomp + Tentacle Whip!)
    act_slash = new_action("Slash1")
    prev_q = {}
    n_slash = 22
    for f in range(1, n_slash + 1):
        begin_frame(f)
        u = (f - 1) / float(n_slash - 1)
        sw = math.sin(u * math.pi)
        bite_open = math.sin(min(1.0, u / 0.55) * math.pi) if u < 0.55 else 0.0
        set_bone("Body", loc=(0.0, -0.28 * sw, 0.08 * sw), rot_xyz=(-18.0 * sw, 0.0, 0.0))
        set_bone("JawBot", rot_xyz=(-46.0 * bite_open, 0.0, 0.0))
        set_bone("EyeBall", rot_xyz=(-10.0 * sw, 0.0, 0.0))
        animate_tentacles(u * 2.0 * math.pi, amp_mult=1.8, stream_back_deg=18.0 * sw, phase_speed=1.0)
        animate_shrooms(u * 2.0 * math.pi, mult=1.6)
        key_all(f, prev_q)

    # 6. Death (Convulse, drop out of air onto ground, jaw hangs wide open slack!)
    act_death = new_action("Death")
    prev_q = {}
    n_death = 30
    for f in range(1, n_death + 1):
        begin_frame(f)
        u = (f - 1) / float(n_death - 1)
        s = u * u * (3.0 - 2.0 * u)
        shake = math.sin(u * math.pi * 6.0) * (1.0 - u) * 14.0
        set_bone("Body", loc=(0.0, 0.25 * s, -0.96 * s), rot_xyz=(-38.0 * s + shake, 65.0 * s, 22.0 * s))
        set_bone("JawBot", rot_xyz=(-48.0 * s, 0.0, 0.0))
        set_bone("EyeBall", rot_xyz=(28.0 * s, 0.0, 30.0 * s))
        animate_tentacles(u * math.pi, amp_mult=0.4 * (1.0 - s), stream_back_deg=25.0 * s, phase_speed=1.0)
        key_all(f, prev_q)

    # Export FBX + Blend with Idle frame 1
    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()
    _export_alien(rig_obj, [body_mesh], "Alien_Leader")

    # --- Render 2 Studio Previews: (1) 3/4 Front Grin + Eye + Tentacles, (2) Wide-Open Vomit Frame ---
    cam_obj = _setup_studio_render(scene)

    cam_obj.location = Vector((-2.45, -3.30, 2.18))
    cam_obj.rotation_euler = (Vector((0.0, 0.15, 1.65)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    tmp_p1 = os.path.join(TMP_EXPORT_DIR, "Alien_Leader_Preview_Front.png")
    scene.render.filepath = tmp_p1
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_p1, os.path.join(ALIEN_DIR, "Alien_Leader_Preview.png"))

    rig_obj.animation_data.action = act_vomit
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_vomit.slots) > 0:
        rig_obj.animation_data.action_slot = act_vomit.slots[0]
    scene.frame_set(18)
    bpy.context.view_layer.update()
    cam_obj.location = Vector((2.55, -3.05, 1.85))
    cam_obj.rotation_euler = (Vector((0.0, 0.05, 1.48)) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    tmp_p2 = os.path.join(TMP_EXPORT_DIR, "Alien_Leader_Preview_Vomit.png")
    scene.render.filepath = tmp_p2
    bpy.ops.render.render(write_still=True)
    _copy_with_retry(tmp_p2, os.path.join(ALIEN_DIR, "Alien_Leader_Vomit_Preview.png"))


if __name__ == "__main__":
    print("=== BUILDING RIGGED ALIEN LEADER BOSS v2 (HIGH-DETAIL FUNGAL CYCLOPS + WIDE VOMIT JAW) ===")
    build_alien_leader_boss()
    shutil.rmtree(TMP_EXPORT_DIR, ignore_errors=True)
    print("=== DONE: ALIEN LEADER BOSS v2 EXPORTED & RENDERED! ===")

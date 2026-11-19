"""
Generates All 8 Rigged & Animated Full-Robot Enemies for The Last Empire:
  Crimson Strike Division (Crimson Red + Graphite Gunmetal + Charcoal + Crimson Glow):
    1. Robot_Blade    - Agile digitigrade mantis assassin mech with curved forearm vibro-scythe
    2. Robot_Drone    - Hovering tactical eye-orb recon drone with side thrusters & antenna
    3. Robot_Laser    - Tall 3-legged hexagonal prism obelisk tripod with retractable laser spire
    4. Robot_Leader   - Massive 4-legged quadruped siege walker boss with shoulder missile pod & cleaver arm

  Hazard Siege Division (Industrial Hazard Yellow + Graphite Gunmetal + Charcoal + Amber/Red Glow):
    5. Robot_Normal   - Compact bipedal boxy-visor patrol sentry bot with wrist pulse blasters
    6. Robot_Gunner   - Heavy suppression autocannon mech with rear ammo drums & curved brass belt
    7. Robot_Kamikaze - 4-legged skittering spider-mine demolition bot with unstable plasma bomb core
    8. Robot_Tanker   - Hulking asymmetric juggernaut with massive oversized hydraulic riot-ram left arm

All models follow README_Model_Color_Standard.md (matte hard-surface finish, segments=1 bevels,
SLOT_ORDER == POLY_FIRST_APPEARANCE_ORDER, facing -Y in Blender -> +Z in Unity).
"""

import os
import math
import bpy
import bmesh
from mathutils import Vector, Matrix, Quaternion, Euler

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
ROOT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
OUT_DIR = os.path.join(ROOT_DIR, "Assets", "GeneratedModels", "Robots", "Rigged")
os.makedirs(OUT_DIR, exist_ok=True)


# ==============================================================================
# SCENE & MATERIAL UTILITIES
# ==============================================================================
def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 30
    return scene


def make_mat(name, rgb, roughness=0.84, metallic=0.04, emission_rgb=None, emission_strength=0.0):
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.18 if metallic < 0.3 else 0.50
        if emission_rgb is not None and emission_strength > 0.0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (emission_rgb[0], emission_rgb[1], emission_rgb[2], 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def create_robot_palette():
    return {
        "graphite": make_mat("Mat_Robot_Graphite",     (0.062, 0.070, 0.095), roughness=0.78, metallic=0.08),
        "armor_lt": make_mat("Mat_Robot_ArmorGrey",    (0.185, 0.200, 0.235), roughness=0.80, metallic=0.06),
        "charcoal": make_mat("Mat_Robot_Charcoal",     (0.022, 0.025, 0.033), roughness=0.90, metallic=0.00),
        "steel":    make_mat("Mat_Robot_Steel",        (0.460, 0.515, 0.610), roughness=0.42, metallic=0.68),
        "crimson":  make_mat("Mat_Robot_Crimson",      (0.490, 0.040, 0.045), roughness=0.84, metallic=0.04),
        "crimson_dk": make_mat("Mat_Robot_CrimsonDark",(0.220, 0.022, 0.028), roughness=0.88, metallic=0.02),
        "yellow":   make_mat("Mat_Robot_HazardYellow", (0.748, 0.410, 0.028), roughness=0.86, metallic=0.02),
        "brass":    make_mat("Mat_Robot_Brass",        (0.600, 0.410, 0.085), roughness=0.44, metallic=0.72),
        "glow_red": make_mat("Mat_Robot_GlowRed",      (1.000, 0.100, 0.080), roughness=0.22, metallic=0.00,
                             emission_rgb=(1.0, 0.10, 0.08), emission_strength=3.8),
        "glow_amb": make_mat("Mat_Robot_GlowAmber",    (1.000, 0.560, 0.060), roughness=0.22, metallic=0.00,
                             emission_rgb=(1.0, 0.56, 0.06), emission_strength=3.4),
    }


# ==============================================================================
# HARD-SURFACE RIGGED ROBOT BUILDER
# ==============================================================================
class RobotRigBuilder:
    def __init__(self, name, pal):
        self.name = name
        self.pal = pal
        self.mat_list = list(pal.values())
        self.mat_idx = {k: i for i, k in enumerate(pal.keys())}
        self.bone_bms = {}  # bone_name -> bmesh

    def _bm(self, bone_name):
        if bone_name not in self.bone_bms:
            self.bone_bms[bone_name] = bmesh.new()
        return self.bone_bms[bone_name]

    def _assign_mat(self, faces, mat_key):
        idx = self.mat_idx[mat_key]
        for f in faces:
            f.material_index = idx
            f.smooth = False

    def _mirror_vec(self, v):
        return Vector((-v[0], v[1], v[2]))

    def _mirror_rot(self, rot_deg):
        return (rot_deg[0], -rot_deg[1], -rot_deg[2])

    def add_box(self, bone, center, size, mat_key, rot_deg=(0, 0, 0), bevel_amt=0.0, mirror_bone=None):
        bm = self._bm(bone)
        sx, sy, sz = size
        m_scale = Matrix.Diagonal((sx, sy, sz, 1.0))
        m_rot = Euler([math.radians(a) for a in rot_deg], 'XYZ').to_matrix().to_4x4()
        m_loc = Matrix.Translation(Vector(center))
        res = bmesh.ops.create_cube(bm, size=1.0, matrix=m_loc @ m_rot @ m_scale)
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        if bevel_amt > 0.0:
            edges = list({e for f in faces for e in f.edges})
            b_res = bmesh.ops.bevel(bm, geom=edges, offset=bevel_amt, segments=1, affect='EDGES', clamp_overlap=True)
            faces = list(set(faces) | set(b_res.get("faces", [])))
        self._assign_mat(faces, mat_key)
        if mirror_bone:
            self.add_box(mirror_bone, self._mirror_vec(center), size, mat_key, self._mirror_rot(rot_deg), bevel_amt, None)

    def add_taper_box(self, bone, center, bot_size, top_size, height, mat_key, rot_deg=(0, 0, 0), mirror_bone=None):
        bm = self._bm(bone)
        bx, by = bot_size[0] * 0.5, bot_size[1] * 0.5
        tx, ty = top_size[0] * 0.5, top_size[1] * 0.5
        hz = height * 0.5
        pts = [
            (-bx, -by, -hz), (bx, -by, -hz), (bx, by, -hz), (-bx, by, -hz),
            (-tx, -ty,  hz), (tx, -ty,  hz), (tx, ty,  hz), (-tx, ty,  hz),
        ]
        m = Matrix.Translation(Vector(center)) @ Euler([math.radians(a) for a in rot_deg], 'XYZ').to_matrix().to_4x4()
        vs = [bm.verts.new(m @ Vector(p)) for p in pts]
        quads = [
            (0, 3, 2, 1), (4, 5, 6, 7),
            (0, 1, 5, 4), (1, 2, 6, 5),
            (2, 3, 7, 6), (3, 0, 4, 7),
        ]
        faces = [bm.faces.new([vs[i] for i in q]) for q in quads]
        self._assign_mat(faces, mat_key)
        if mirror_bone:
            self.add_taper_box(mirror_bone, self._mirror_vec(center), bot_size, top_size, height, mat_key, self._mirror_rot(rot_deg), None)

    def add_cyl(self, bone, p1, p2, r1, r2=None, mat_key="steel", segs=12, mirror_bone=None):
        if r2 is None:
            r2 = r1
        bm = self._bm(bone)
        v1, v2 = Vector(p1), Vector(p2)
        diff = v2 - v1
        length = diff.length
        if length < 1e-5:
            return
        mid = (v1 + v2) * 0.5
        q = Vector((0, 0, 1)).rotation_difference(diff.normalized())
        m = Matrix.Translation(mid) @ q.to_matrix().to_4x4()
        res = bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=segs,
            radius1=r1, radius2=r2, depth=length, matrix=m
        )
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._assign_mat(faces, mat_key)
        if mirror_bone:
            self.add_cyl(mirror_bone, self._mirror_vec(p1), self._mirror_vec(p2), r1, r2, mat_key, segs, None)

    def add_sphere(self, bone, center, radii, mat_key, segs=14, rings=10, mirror_bone=None):
        bm = self._bm(bone)
        rx, ry, rz = radii if isinstance(radii, (tuple, list)) else (radii, radii, radii)
        m = Matrix.Translation(Vector(center)) @ Matrix.Diagonal((rx, ry, rz, 1.0))
        res = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0, matrix=m)
        verts = res["verts"]
        faces = list({f for v in verts for f in v.link_faces})
        self._assign_mat(faces, mat_key)
        if mirror_bone:
            self.add_sphere(mirror_bone, self._mirror_vec(center), radii, mat_key, segs, rings, None)

    def add_curved_blade(self, bone, spine_pts, widths, thicks, mat_key, mirror_bone=None):
        bm = self._bm(bone)
        rings = []
        n = len(spine_pts)
        for i in range(n):
            p = Vector(spine_pts[i])
            if i < n - 1:
                tang = (Vector(spine_pts[i + 1]) - p).normalized()
            else:
                tang = (p - Vector(spine_pts[i - 1])).normalized()
            side = Vector((1, 0, 0))
            if abs(tang.dot(side)) > 0.9:
                side = Vector((0, 0, 1))
            edge_dir = tang.cross(side).normalized()
            thick_dir = edge_dir.cross(tang).normalized()
            w = widths[i] * 0.5
            t = thicks[i] * 0.5
            # Diamond blade cross-section: sharp front edge, flat/beveled back
            v_front = bm.verts.new(p + edge_dir * w)
            v_left  = bm.verts.new(p + thick_dir * t)
            v_back  = bm.verts.new(p - edge_dir * (w * 0.72))
            v_right = bm.verts.new(p - thick_dir * t)
            rings.append((v_front, v_left, v_back, v_right))
        faces = []
        for i in range(n - 1):
            r0, r1 = rings[i], rings[i + 1]
            for j in range(4):
                jn = (j + 1) % 4
                faces.append(bm.faces.new([r0[j], r0[jn], r1[jn], r1[j]]))
        faces.append(bm.faces.new([rings[0][3], rings[0][2], rings[0][1], rings[0][0]]))
        faces.append(bm.faces.new([rings[-1][0], rings[-1][1], rings[-1][2], rings[-1][3]]))
        self._assign_mat(faces, mat_key)
        if mirror_bone:
            m_pts = [self._mirror_vec(p) for p in spine_pts]
            self.add_curved_blade(mirror_bone, m_pts, widths, thicks, mat_key, None)

    def add_tube_chain(self, bone, pts, radius, mat_key, segs=8, mirror_bone=None):
        for i in range(len(pts) - 1):
            self.add_cyl(bone, pts[i], pts[i + 1], radius, radius, mat_key, segs=segs, mirror_bone=mirror_bone)
            self.add_sphere(bone, pts[i + 1], radius * 1.06, mat_key, segs=8, rings=6, mirror_bone=mirror_bone)

    def finalize_mesh_and_rig(self, bone_defs):
        """
        bone_defs: list of (name, head_xyz, tail_xyz, parent_name_or_None)
        Returns (rig_obj, mesh_obj)
        """
        # 1. Create Armature
        arm_data = bpy.data.armatures.new(f"{self.name}_Armature")
        rig_obj = bpy.data.objects.new(f"{self.name}_Rig", arm_data)
        bpy.context.collection.objects.link(rig_obj)
        bpy.context.view_layer.objects.active = rig_obj
        bpy.ops.object.mode_set(mode='EDIT')

        ebones = {}
        for bname, h, t, parent in bone_defs:
            eb = arm_data.edit_bones.new(bname)
            eb.head = Vector(h)
            eb.tail = Vector(t)
            if (eb.tail - eb.head).length < 0.01:
                eb.tail = eb.head + Vector((0, 0, 0.1))
            ebones[bname] = eb
        for bname, _, _, parent in bone_defs:
            if parent and parent in ebones:
                ebones[bname].parent = ebones[parent]
                ebones[bname].use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')

        for pb in rig_obj.pose.bones:
            pb.rotation_mode = 'QUATERNION'

        # 2. Convert each bone's BMesh into baked beveled mesh & combine with rigid weights
        part_objs = []
        valid_bones = {b[0] for b in bone_defs}
        for bname, bm in self.bone_bms.items():
            target_bone = bname if bname in valid_bones else bone_defs[0][0]
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            me = bpy.data.meshes.new(f"{self.name}_{bname}_tmp")
            for m in self.mat_list:
                me.materials.append(m)
            bm.to_mesh(me)
            bm.free()

            obj = bpy.data.objects.new(f"{self.name}_{bname}_tmp", me)
            bpy.context.collection.objects.link(obj)
            # Crisp hard-surface bevel
            bev = obj.modifiers.new("Bevel", 'BEVEL')
            bev.width = 0.0065
            bev.segments = 1
            bev.limit_method = 'ANGLE'
            bev.angle_limit = math.radians(34.0)
            bev.harden_normals = False
            bev.use_clamp_overlap = True

            deps = bpy.context.evaluated_depsgraph_get()
            eval_obj = obj.evaluated_get(deps)
            baked_me = bpy.data.meshes.new_from_object(eval_obj)
            obj.modifiers.clear()
            old_me = obj.data
            obj.data = baked_me
            bpy.data.meshes.remove(old_me)

            vg = obj.vertex_groups.new(name=target_bone)
            vg.add(list(range(len(baked_me.vertices))), 1.0, 'REPLACE')
            part_objs.append(obj)

        # Join all parts into one single clean SkinnedMesh
        bpy.ops.object.select_all(action='DESELECT')
        for o in part_objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = part_objs[0]
        if len(part_objs) > 1:
            bpy.ops.object.join()
        mesh_obj = bpy.context.view_layer.objects.active
        mesh_obj.name = f"{self.name}_Mesh"
        mesh_obj.data.name = f"{self.name}_MeshData"

        # Reorder material slots strictly by polygon first-appearance order (Unity rule!)
        me = mesh_obj.data
        old_mats = list(me.materials)
        seen = {}
        ordered_mats = []
        new_indices = []
        for poly in me.polygons:
            oi = poly.material_index
            if oi not in seen:
                seen[oi] = len(ordered_mats)
                ordered_mats.append(old_mats[oi])
            new_indices.append(seen[oi])
        me.materials.clear()
        for m in ordered_mats:
            me.materials.append(m)
        for poly, ni in zip(me.polygons, new_indices):
            poly.material_index = ni
        me.update()

        # Parent & Armature modifier
        mesh_obj.parent = rig_obj
        arm_mod = mesh_obj.modifiers.new("Armature", 'ARMATURE')
        arm_mod.object = rig_obj
        arm_mod.use_deform_preserve_volume = False

        return rig_obj, mesh_obj


# ==============================================================================
# ANIMATION HELPERS (Blender 5.2 Compatible)
# ==============================================================================
class RigAnimator:
    def __init__(self, rig_obj):
        self.rig = rig_obj
        self.scene = bpy.context.scene
        self.actions = {}

    def new_action(self, name):
        act = bpy.data.actions.new(name=name)
        act.use_fake_user = True
        if self.rig.animation_data is None:
            self.rig.animation_data_create()
        self.rig.animation_data.action = act
        if hasattr(self.rig.animation_data, "action_slot") and hasattr(act, "slots"):
            slot = act.slots[0] if len(act.slots) > 0 else act.slots.new(id_type='OBJECT', name=self.rig.name)
            self.rig.animation_data.action_slot = slot
        self.actions[name] = act
        return act

    def begin(self, frame):
        self.scene.frame_set(frame)
        for pb in self.rig.pose.bones:
            pb.location = Vector((0, 0, 0))
            pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
            pb.scale = Vector((1, 1, 1))

    def loc(self, bname, x, y, z):
        if bname in self.rig.pose.bones:
            self.rig.pose.bones[bname].location = Vector((x, y, z))

    def rot(self, bname, rx, ry, rz):
        if bname in self.rig.pose.bones:
            q = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ').to_quaternion()
            self.rig.pose.bones[bname].rotation_quaternion = q

    def scale(self, bname, sx, sy, sz):
        if bname in self.rig.pose.bones:
            self.rig.pose.bones[bname].scale = Vector((sx, sy, sz))

    def key(self, frame, prev_q):
        for pb in self.rig.pose.bones:
            q = pb.rotation_quaternion.copy()
            if pb.name in prev_q and prev_q[pb.name].dot(q) < 0.0:
                q = -q
                pb.rotation_quaternion = q
            prev_q[pb.name] = q.copy()
            pb.keyframe_insert(data_path="location", frame=frame)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            pb.keyframe_insert(data_path="scale", frame=frame)

    def set_idle_pose(self):
        act = self.actions.get("Idle")
        if act:
            self.rig.animation_data.action = act
            if hasattr(self.rig.animation_data, "action_slot") and len(act.slots) > 0:
                self.rig.animation_data.action_slot = act.slots[0]
            self.scene.frame_set(1)
            bpy.context.view_layer.update()


def export_robot_fbx(name, rig_obj, mesh_obj):
    fbx_path = os.path.join(OUT_DIR, f"{name}_Rigged.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    rig_obj.select_set(True)
    mesh_obj.select_set(True)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
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
    )
    print(f"[OK] Exported {fbx_path}")
    return fbx_path


# ==============================================================================
# SHARED BIPEDAL MECH ANIMATIONS (Normal, Blade, Gunner, Tanker)
# ==============================================================================
def animate_biped_mech(anim, style="normal"):
    is_heavy = style in ("tanker", "gunner")
    is_blade = style == "blade"
    stride_leg = 28.0 if is_blade else (18.0 if is_heavy else 24.0)
    run_leg = 42.0 if is_blade else (26.0 if is_heavy else 34.0)

    # 1. Idle (1..37)
    anim.new_action("Idle")
    pq = {}
    for f in range(1, 38):
        anim.begin(f)
        a = ((f - 1) / 36.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, 0, 0.018 * s)
        anim.rot("Body", (8.0 if is_blade else 3.0) + 1.8 * s, 0, 0)
        anim.rot("Head", -4.0 - 1.5 * s, 6.0 * math.sin(a * 0.5), 0)
        if style == "tanker":
            anim.rot("ArmL", 22.0 + 3.0 * s, -8.0, 10.0)
            anim.rot("ForearmL", 25.0, 0, 12.0)
            anim.rot("ArmR", 10.0 - 2.5 * s, 0, -8.0)
            anim.rot("ForearmR", 18.0, 0, 0)
        elif style == "gunner":
            anim.rot("ArmR", 32.0 + 2.0 * s, 0, -6.0)
            anim.rot("ArmL", 16.0 - 2.0 * s, 0, 12.0)
            anim.rot("ForearmL", 24.0, 0, 0)
        elif style == "blade":
            anim.rot("ArmR", 26.0 + 3.0 * s, 0, -18.0)
            anim.rot("ForearmR", 28.0, 0, -8.0)
            anim.rot("ArmL", -12.0 - 3.0 * s, 0, 16.0)
            anim.rot("ForearmL", 22.0, 0, 0)
        else:
            anim.rot("ArmL", 16.0 + 2.5 * s, 0, 8.0)
            anim.rot("ArmR", 16.0 - 2.5 * s, 0, -8.0)
            anim.rot("ForearmL", 16.0, 0, 0)
            anim.rot("ForearmR", 16.0, 0, 0)
        anim.key(f, pq)

    # 2. Walk (1..25)
    anim.new_action("Walk")
    pq = {}
    for f in range(1, 26):
        anim.begin(f)
        a = ((f - 1) / 24.0) * 2.0 * math.pi
        s, c = math.sin(a), math.cos(a)
        anim.loc("Body", 0.025 * s, 0, abs(s) * 0.035)
        anim.rot("Body", 6.0 if not is_blade else 14.0, 5.0 * s, 3.0 * s)
        anim.rot("Head", -5.0, -4.0 * s, 0)
        anim.rot("LegL", stride_leg * s, 0, 0)
        anim.rot("ShinL", -max(0.0, c) * 28.0, 0, 0)
        anim.rot("LegR", -stride_leg * s, 0, 0)
        anim.rot("ShinR", -max(0.0, -c) * 28.0, 0, 0)
        if style == "gunner":
            anim.rot("ArmR", 36.0 + 4.0 * s, 0, -6.0)
            anim.rot("ArmL", 14.0 - 12.0 * s, 0, 10.0)
        elif style == "tanker":
            anim.rot("ArmL", 26.0 - 8.0 * s, -10.0, 12.0)
            anim.rot("ForearmL", 28.0, 0, 10.0)
            anim.rot("ArmR", 12.0 + 18.0 * s, 0, -8.0)
            anim.rot("ForearmR", 20.0, 0, 0)
        else:
            anim.rot("ArmL", 14.0 - 18.0 * s, 0, 10.0)
            anim.rot("ArmR", 18.0 + 18.0 * s, 0, -10.0)
            anim.rot("ForearmL", 20.0, 0, 0)
            anim.rot("ForearmR", 22.0, 0, 0)
        anim.key(f, pq)

    # 3. Run (1..19)
    anim.new_action("Run")
    pq = {}
    for f in range(1, 20):
        anim.begin(f)
        a = ((f - 1) / 18.0) * 2.0 * math.pi
        s, c = math.sin(a), math.cos(a)
        anim.loc("Body", 0.02 * s, 0, abs(s) * 0.055)
        anim.rot("Body", 22.0 if is_blade else 13.0, 8.0 * s, 4.0 * s)
        anim.rot("Head", -14.0 if is_blade else -9.0, -6.0 * s, 0)
        anim.rot("LegL", run_leg * s, 0, 0)
        anim.rot("ShinL", -max(0.0, c) * 44.0, 0, 0)
        anim.rot("LegR", -run_leg * s, 0, 0)
        anim.rot("ShinR", -max(0.0, -c) * 44.0, 0, 0)
        if style == "blade":
            anim.rot("ArmR", -28.0 + 10.0 * s, 0, -24.0)
            anim.rot("ForearmR", 35.0, 0, -10.0)
            anim.rot("ArmL", -24.0 - 10.0 * s, 0, 22.0)
            anim.rot("ForearmL", 30.0, 0, 0)
        elif style == "tanker":
            # Bull-rush shield raised forward!
            anim.rot("ArmL", 52.0, -24.0, 18.0)
            anim.rot("ForearmL", 35.0, 0, 18.0)
            anim.rot("ArmR", -16.0 + 20.0 * s, 0, -14.0)
        elif style == "gunner":
            anim.rot("ArmR", 42.0 + 5.0 * s, 0, -5.0)
            anim.rot("ArmL", 10.0 - 18.0 * s, 0, 12.0)
        else:
            anim.rot("ArmL", 22.0 - 26.0 * s, 0, 10.0)
            anim.rot("ArmR", 22.0 + 26.0 * s, 0, -10.0)
            anim.rot("ForearmL", 30.0, 0, 0)
            anim.rot("ForearmR", 30.0, 0, 0)
        anim.key(f, pq)

    # 4. Slash1 (1..21 — Melee strike / bash / recoil)
    anim.new_action("Slash1")
    pq = {}
    for f in range(1, 22):
        anim.begin(f)
        t = (f - 1) / 20.0
        if t < 0.35:
            p = t / 0.35
            anim.rot("Body", -10.0 * p, -26.0 * p, 0)
            if style == "tanker":
                anim.rot("ArmL", 85.0 * p, -20.0 * p, 24.0 * p)
                anim.rot("ForearmL", 30.0 * p, 0, 0)
            else:
                anim.rot("ArmR", 95.0 * p, 0, -35.0 * p)
                anim.rot("ForearmR", 40.0 * p, 0, 0)
        elif t < 0.65:
            p = (t - 0.35) / 0.30
            anim.loc("Body", 0, -0.14 * math.sin(p * math.pi), -0.05 * math.sin(p * math.pi))
            anim.rot("Body", -10.0 + 36.0 * p, -26.0 + 62.0 * p, 0)
            if style == "tanker":
                anim.rot("ArmL", 85.0 - 65.0 * p, -20.0 + 48.0 * p, 24.0 - 18.0 * p)
                anim.rot("ForearmL", 30.0 - 15.0 * p, 0, 0)
            else:
                anim.rot("ArmR", 95.0 - 115.0 * p, 0, -35.0 + 55.0 * p)
                anim.rot("ForearmR", 40.0 - 30.0 * p, 0, 0)
        else:
            p = (t - 0.65) / 0.35
            anim.rot("Body", 26.0 * (1 - p), 36.0 * (1 - p), 0)
            if style == "tanker":
                anim.rot("ArmL", 20.0 * (1 - p), 28.0 * (1 - p), 6.0 * (1 - p))
            else:
                anim.rot("ArmR", -20.0 * (1 - p), 0, 20.0 * (1 - p))
        anim.key(f, pq)

    # 5. Attack (Special for Gunner firing & Blade dash)
    if style == "gunner":
        anim.new_action("Attack")
        pq = {}
        for f in range(1, 17):
            anim.begin(f)
            t = (f - 1) / 15.0
            kick = math.sin(t * math.pi * 6.0)
            anim.loc("Body", 0, 0.045 * max(0.0, kick), -0.03)
            anim.rot("Body", 6.0 - 4.0 * kick, -14.0, 0)
            anim.rot("ArmR", 68.0 - 6.0 * kick, 0, -4.0)
            anim.rot("CannonBarrel", 0, (f - 1) * 48.0, 0)
            anim.rot("ArmL", 36.0, 0, 18.0)
            anim.rot("LegL", 16.0, 0, 0)
            anim.rot("LegR", -18.0, 0, 0)
            anim.key(f, pq)
    elif style == "blade":
        anim.new_action("Attack")
        pq = {}
        for f in range(1, 19):
            anim.begin(f)
            t = (f - 1) / 17.0
            s = math.sin(t * math.pi)
            anim.loc("Body", 0, -0.18 * s, -0.08 * s)
            anim.rot("Body", 34.0 * s, -30.0 + 70.0 * t, 0)
            anim.rot("ArmR", 75.0 * s, 0, -35.0 + 60.0 * t)
            anim.rot("ForearmR", 20.0 * s, 0, 0)
            anim.rot("ArmL", -35.0 * s, 0, 35.0 * s)
            anim.rot("LegL", 32.0 * s, 0, 0)
            anim.rot("LegR", -38.0 * s, 0, 0)
            anim.key(f, pq)

    # 6. Death (1..25)
    anim.new_action("Death")
    pq = {}
    for f in range(1, 26):
        anim.begin(f)
        t = (f - 1) / 24.0
        s = t * t * (3 - 2 * t)
        anim.loc("Body", 0, 0.25 * s, -0.68 * s)
        anim.rot("Body", -86.0 * s, 12.0 * s, 10.0 * s)
        anim.rot("Head", 22.0 * s, -18.0 * s, 0)
        anim.rot("ArmL", -35.0 * s, 0, 42.0 * s)
        anim.rot("ArmR", -30.0 * s, 0, -45.0 * s)
        anim.rot("LegL", 20.0 * s, 0, -8.0 * s)
        anim.rot("LegR", 14.0 * s, 0, 12.0 * s)
        anim.key(f, pq)


# ==============================================================================
# 1. ROBOT NORMAL (Hazard Patrol Sentry Droid - Yellow/Graphite/Charcoal)
# ==============================================================================
def build_robot_normal():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Normal", pal)

    # Pelvis & Waist
    rb.add_taper_box("Body", (0, 0, 0.78), (0.30, 0.22), (0.38, 0.26), 0.16, "charcoal")
    rb.add_box("Body", (0, -0.02, 0.90), (0.26, 0.20, 0.12), "steel")
    # Armored Torso with Yellow Hazard Chest Plates
    rb.add_taper_box("Body", (0, 0.0, 1.14), (0.38, 0.28), (0.48, 0.34), 0.38, "graphite")
    rb.add_box("Body", (0, -0.15, 1.18), (0.38, 0.10, 0.26), "yellow", rot_deg=(8, 0, 0))
    rb.add_box("Body", (0, -0.20, 1.19), (0.18, 0.04, 0.07), "glow_amb")
    # Dual Rear Power Cells
    rb.add_cyl("Body", (0.11, 0.20, 1.04), (0.11, 0.20, 1.32), 0.065, 0.065, "armor_lt", 10)
    rb.add_cyl("Body", (-0.11, 0.20, 1.04), (-0.11, 0.20, 1.32), 0.065, 0.065, "armor_lt", 10)
    rb.add_box("Body", (0, 0.18, 1.18), (0.30, 0.08, 0.22), "charcoal")
    # Shoulder Pauldron Mounts
    rb.add_box("Body", (0.28, 0, 1.28), (0.16, 0.22, 0.14), "yellow", rot_deg=(0, 14, 0))
    rb.add_box("Body", (-0.28, 0, 1.28), (0.16, 0.22, 0.14), "yellow", rot_deg=(0, -14, 0))

    # Non-humanoid Boxy Sensor Turret Head
    rb.add_cyl("Head", (0, 0, 1.33), (0, 0, 1.43), 0.08, 0.09, "charcoal", 10)
    rb.add_taper_box("Head", (0, -0.02, 1.56), (0.32, 0.28), (0.28, 0.24), 0.24, "graphite")
    rb.add_box("Head", (0, -0.04, 1.67), (0.26, 0.22, 0.06), "yellow")
    rb.add_box("Head", (0, -0.15, 1.56), (0.24, 0.05, 0.08), "charcoal")
    rb.add_box("Head", (0, -0.175, 1.56), (0.20, 0.025, 0.045), "glow_red")
    rb.add_cyl("Head", (0.16, 0.04, 1.58), (0.18, 0.06, 1.84), 0.015, 0.008, "steel", 8)
    rb.add_sphere("Head", (0.18, 0.06, 1.85), 0.022, "glow_amb", 8, 6)

    # Arms + Twin Wrist Pulse Blasters (Mirror L -> R)
    rb.add_sphere("ArmL", (0.30, 0, 1.22), 0.085, "charcoal", 10, 8, mirror_bone="ArmR")
    rb.add_cyl("ArmL", (0.31, 0, 1.20), (0.34, -0.03, 0.94), 0.065, 0.055, "graphite", 10, mirror_bone="ArmR")
    rb.add_sphere("ForearmL", (0.34, -0.03, 0.94), 0.062, "steel", 10, 8, mirror_bone="ForearmR")
    rb.add_box("ForearmL", (0.35, -0.12, 0.80), (0.12, 0.26, 0.13), "armor_lt", rot_deg=(12, 0, 0), mirror_bone="ForearmR")
    rb.add_box("ForearmL", (0.38, -0.10, 0.82), (0.04, 0.18, 0.09), "yellow", rot_deg=(12, 0, 0), mirror_bone="ForearmR")
    rb.add_cyl("ForearmL", (0.35, -0.24, 0.78), (0.35, -0.40, 0.74), 0.028, 0.024, "steel", 8, mirror_bone="ForearmR")
    rb.add_cyl("ForearmL", (0.35, -0.39, 0.74), (0.35, -0.42, 0.73), 0.032, 0.032, "glow_amb", 8, mirror_bone="ForearmR")

    # Sturdy Armored Mech Legs (Feet flat at z=0.0)
    rb.add_cyl("LegL", (0.15, 0, 0.76), (0.17, -0.03, 0.44), 0.075, 0.065, "charcoal", 10, mirror_bone="LegR")
    rb.add_box("LegL", (0.17, -0.03, 0.60), (0.14, 0.16, 0.22), "graphite", mirror_bone="LegR")
    rb.add_box("ShinL", (0.17, -0.08, 0.44), (0.13, 0.13, 0.12), "yellow", mirror_bone="ShinR")
    rb.add_cyl("ShinL", (0.17, -0.02, 0.44), (0.16, 0.02, 0.12), 0.062, 0.055, "steel", 10, mirror_bone="ShinR")
    rb.add_box("ShinL", (0.16, -0.04, 0.26), (0.13, 0.14, 0.24), "graphite", mirror_bone="ShinR")
    rb.add_taper_box("ShinL", (0.16, -0.04, 0.055), (0.17, 0.30), (0.13, 0.22), 0.11, "charcoal", mirror_bone="ShinR")

    bones = [
        ("Root",     (0, 0, 0),          (0, 0, 0.35),      None),
        ("Body",     (0, 0, 0.80),       (0, 0, 1.34),      "Root"),
        ("Head",     (0, 0, 1.36),       (0, 0, 1.72),      "Body"),
        ("ArmL",     (0.30, 0, 1.22),    (0.34, -0.03, 0.94), "Body"),
        ("ForearmL", (0.34, -0.03, 0.94),(0.35, -0.35, 0.75), "ArmL"),
        ("ArmR",     (-0.30, 0, 1.22),   (-0.34, -0.03, 0.94), "Body"),
        ("ForearmR", (-0.34, -0.03, 0.94),(-0.35, -0.35, 0.75), "ArmR"),
        ("LegL",     (0.15, 0, 0.76),    (0.17, -0.03, 0.44), "Root"),
        ("ShinL",    (0.17, -0.03, 0.44),(0.16, 0.0, 0.04),  "LegL"),
        ("LegR",     (-0.15, 0, 0.76),   (-0.17, -0.03, 0.44), "Root"),
        ("ShinR",    (-0.17, -0.03, 0.44),(-0.16, 0.0, 0.04), "LegR"),
    ]
    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)
    animate_biped_mech(anim, style="normal")
    anim.set_idle_pose()
    return export_robot_fbx("Robot_Normal", rig, mesh)


# ==============================================================================
# 2. ROBOT BLADE (Crimson Mantis Assassin Mech - Ref Curved Blade + Red Eye)
# ==============================================================================
def build_robot_blade():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Blade", pal)

    # Sleek V-tapered Assassin Torso
    rb.add_taper_box("Body", (0, 0, 0.84), (0.22, 0.18), (0.30, 0.22), 0.16, "charcoal")
    rb.add_taper_box("Body", (0, -0.02, 1.14), (0.28, 0.22), (0.44, 0.30), 0.42, "graphite")
    # Crimson Chest Armor Chevron
    rb.add_taper_box("Body", (0, -0.15, 1.18), (0.22, 0.08), (0.36, 0.10), 0.28, "crimson", rot_deg=(12, 0, 0))
    rb.add_box("Body", (0, -0.20, 1.22), (0.10, 0.03, 0.10), "glow_red", rot_deg=(0, 45, 0))
    # Dual Swept Back Jump-Thrusters
    rb.add_cyl("Body", (0.12, 0.14, 1.24), (0.18, 0.30, 1.38), 0.06, 0.045, "charcoal", 10)
    rb.add_cyl("Body", (-0.12, 0.14, 1.24), (-0.18, 0.30, 1.38), 0.06, 0.045, "charcoal", 10)
    rb.add_cyl("Body", (0.18, 0.29, 1.37), (0.20, 0.33, 1.40), 0.042, 0.038, "glow_red", 10)
    rb.add_cyl("Body", (-0.18, 0.29, 1.37), (-0.20, 0.33, 1.40), 0.042, 0.038, "glow_red", 10)
    # Sharp Crimson Shoulder Fins
    rb.add_taper_box("Body", (0.26, 0.02, 1.32), (0.14, 0.20), (0.06, 0.28), 0.14, "crimson", rot_deg=(0, -28, 0))
    rb.add_taper_box("Body", (-0.26, 0.02, 1.32), (0.14, 0.20), (0.06, 0.28), 0.14, "crimson", rot_deg=(0, 28, 0))

    # Aerodynamic Predator Head with Cyclops Red Eye & Swept Crest
    rb.add_cyl("Head", (0, 0, 1.34), (0, -0.02, 1.44), 0.065, 0.065, "charcoal", 10)
    rb.add_taper_box("Head", (0, -0.03, 1.56), (0.22, 0.30), (0.18, 0.24), 0.22, "graphite", rot_deg=(8, 0, 0))
    # Crimson Swept Hair/Fin Crest (Nod to Ref silhouette turned mechanical!)
    rb.add_taper_box("Head", (0, 0.08, 1.66), (0.12, 0.24), (0.04, 0.34), 0.12, "crimson", rot_deg=(-18, 0, 0))
    rb.add_box("Head", (0, -0.17, 1.56), (0.18, 0.05, 0.07), "charcoal")
    rb.add_sphere("Head", (-0.04, -0.19, 1.56), (0.052, 0.032, 0.048), "glow_red", 12, 8)
    rb.add_box("Head", (0.05, -0.185, 1.56), (0.08, 0.02, 0.025), "glow_red")

    # Right Arm: MASSIVE Curved Swept Mantis Vibro-Scythe Blade (Direct Ref Inspiration!)
    rb.add_sphere("ArmR", (-0.26, 0, 1.24), 0.075, "charcoal", 10, 8)
    rb.add_cyl("ArmR", (-0.27, 0, 1.22), (-0.32, -0.08, 0.94), 0.058, 0.052, "graphite", 10)
    rb.add_box("ForearmR", (-0.33, -0.14, 0.84), (0.11, 0.24, 0.12), "crimson", rot_deg=(18, 0, 0))
    rb.add_box("ForearmR", (-0.33, -0.22, 0.80), (0.13, 0.14, 0.14), "charcoal")
    scythe_spine = [
        (-0.34, -0.16, 0.84),
        (-0.36, -0.44, 0.88),
        (-0.37, -0.76, 0.80),
        (-0.36, -1.04, 0.60),
    ]
    rb.add_curved_blade("ForearmR", scythe_spine, [0.11, 0.14, 0.11, 0.02], [0.032, 0.026, 0.018, 0.005], "steel")
    rb.add_tube_chain("ForearmR", [(-0.35, -0.26, 0.88), (-0.36, -0.56, 0.86), (-0.36, -0.88, 0.72)], 0.014, "glow_red", 6)

    # Left Arm: Agile Parrying Energy Claw
    rb.add_sphere("ArmL", (0.26, 0, 1.24), 0.075, "charcoal", 10, 8)
    rb.add_cyl("ArmL", (0.27, 0, 1.22), (0.31, -0.06, 0.94), 0.055, 0.048, "graphite", 10)
    rb.add_box("ForearmL", (0.32, -0.13, 0.84), (0.10, 0.22, 0.10), "crimson", rot_deg=(14, 0, 0))
    claw_spine = [(0.32, -0.20, 0.82), (0.33, -0.46, 0.78), (0.33, -0.66, 0.68)]
    rb.add_curved_blade("ForearmL", claw_spine, [0.07, 0.06, 0.015], [0.024, 0.018, 0.005], "steel")

    # Digitigrade Agile Blade-Runner Legs (Feet at z=0.0)
    rb.add_cyl("LegL", (0.13, 0, 0.82), (0.16, -0.08, 0.48), 0.062, 0.052, "graphite", 10, mirror_bone="LegR")
    rb.add_box("LegL", (0.17, -0.06, 0.64), (0.10, 0.14, 0.20), "crimson", rot_deg=(-14, 0, 0), mirror_bone="LegR")
    rb.add_sphere("ShinL", (0.16, -0.08, 0.48), 0.055, "steel", 10, 8, mirror_bone="ShinR")
    rb.add_cyl("ShinL", (0.16, -0.08, 0.48), (0.15, 0.08, 0.22), 0.052, 0.042, "charcoal", 10, mirror_bone="ShinR")
    rb.add_cyl("ShinL", (0.15, 0.08, 0.22), (0.15, -0.06, 0.05), 0.045, 0.036, "steel", 10, mirror_bone="ShinR")
    rb.add_taper_box("ShinL", (0.15, -0.08, 0.04), (0.12, 0.26), (0.09, 0.16), 0.08, "charcoal", mirror_bone="ShinR")

    bones = [
        ("Root",     (0, 0, 0),           (0, 0, 0.35),       None),
        ("Body",     (0, 0, 0.84),        (0, 0, 1.36),       "Root"),
        ("Head",     (0, 0, 1.38),        (0, 0, 1.72),       "Body"),
        ("ArmL",     (0.26, 0, 1.24),     (0.31, -0.06, 0.94),"Body"),
        ("ForearmL", (0.31, -0.06, 0.94), (0.33, -0.58, 0.72),"ArmL"),
        ("ArmR",     (-0.26, 0, 1.24),    (-0.32, -0.08, 0.94),"Body"),
        ("ForearmR", (-0.32, -0.08, 0.94),(-0.36, -0.88, 0.72),"ArmR"),
        ("LegL",     (0.13, 0, 0.82),     (0.16, -0.08, 0.48),"Root"),
        ("ShinL",    (0.16, -0.08, 0.48), (0.15, -0.06, 0.04),"LegL"),
        ("LegR",     (-0.13, 0, 0.82),    (-0.16, -0.08, 0.48),"Root"),
        ("ShinR",    (-0.16, -0.08, 0.48),(-0.15, -0.06, 0.04),"LegR"),
    ]
    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)
    animate_biped_mech(anim, style="blade")
    anim.set_idle_pose()
    return export_robot_fbx("Robot_Blade", rig, mesh)


# ==============================================================================
# 3. ROBOT DRONE (Hovering Eye-Orb Drone - Direct Ref #2 Evolved!)
# ==============================================================================
def build_robot_drone():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Drone", pal)

    HZ = 1.05  # Hover height
    # Armored Layered Spherical Hull (Graphite + Crimson Shell Plates)
    rb.add_sphere("Body", (0, 0, HZ), (0.32, 0.32, 0.28), "graphite", segs=16, rings=12)
    rb.add_taper_box("Body", (0, 0.02, HZ + 0.22), (0.36, 0.34), (0.24, 0.24), 0.12, "crimson")
    rb.add_taper_box("Body", (0, 0.02, HZ - 0.20), (0.24, 0.24), (0.34, 0.30), 0.10, "charcoal")
    # Armored Side Frame Collar Ring
    rb.add_box("Body", (0, 0.0, HZ), (0.68, 0.16, 0.12), "charcoal")
    rb.add_box("Body", (0, 0.28, HZ), (0.24, 0.10, 0.16), "armor_lt")
    rb.add_cyl("Body", (0, 0.32, HZ), (0, 0.39, HZ), 0.07, 0.055, "glow_amb", 12)

    # Central Recessed Glowing Red Cyclops Camera Eye (Iconic Ref #2!)
    rb.add_cyl("EyeCore", (0, -0.22, HZ), (0, -0.34, HZ), 0.16, 0.145, "charcoal", 16)
    rb.add_cyl("EyeCore", (0, -0.32, HZ), (0, -0.36, HZ), 0.13, 0.12, "steel", 16)
    rb.add_sphere("EyeCore", (0, -0.33, HZ), (0.105, 0.075, 0.105), "glow_red", 14, 10)

    # Twin Articulated Side Thruster Wing Pods (Iconic Ref #2 Side Wings!)
    rb.add_box("PodL", (0.44, 0.0, HZ), (0.26, 0.20, 0.11), "graphite", mirror_bone="PodR")
    rb.add_box("PodL", (0.46, 0.0, HZ + 0.055), (0.22, 0.14, 0.04), "crimson", mirror_bone="PodR")
    rb.add_cyl("PodL", (0.50, 0.02, HZ - 0.04), (0.50, 0.02, HZ - 0.16), 0.068, 0.082, "charcoal", 12, mirror_bone="PodR")
    rb.add_cyl("PodL", (0.50, 0.02, HZ - 0.15), (0.50, 0.02, HZ - 0.19), 0.065, 0.050, "glow_amb", 12, mirror_bone="PodR")
    rb.add_box("PodL", (0.56, -0.02, HZ), (0.06, 0.18, 0.05), "yellow", mirror_bone="PodR")

    # Angled Top Comms Antenna with Red Beacon Orb (Exact Ref #2 detail!)
    rb.add_cyl("Antenna", (0.08, 0.04, HZ + 0.24), (0.22, 0.10, HZ + 0.56), 0.022, 0.012, "steel", 8)
    rb.add_sphere("Antenna", (0.23, 0.11, HZ + 0.58), 0.048, "glow_red", 10, 8)

    # Underslung Dual Micro-Blaster Turret
    rb.add_box("Turret", (0, -0.06, HZ - 0.28), (0.16, 0.24, 0.10), "armor_lt")
    rb.add_cyl("Turret", (0.05, -0.16, HZ - 0.29), (0.05, -0.40, HZ - 0.29), 0.022, 0.020, "steel", 8)
    rb.add_cyl("Turret", (-0.05, -0.16, HZ - 0.29), (-0.05, -0.40, HZ - 0.29), 0.022, 0.020, "steel", 8)
    rb.add_cyl("Turret", (0.05, -0.39, HZ - 0.29), (0.05, -0.42, HZ - 0.29), 0.026, 0.026, "glow_red", 8)
    rb.add_cyl("Turret", (-0.05, -0.39, HZ - 0.29), (-0.05, -0.42, HZ - 0.29), 0.026, 0.026, "glow_red", 8)

    bones = [
        ("Root",    (0, 0, 0),            (0, 0, 0.40),          None),
        ("Body",    (0, 0, HZ),           (0, 0, HZ + 0.30),     "Root"),
        ("EyeCore", (0, -0.22, HZ),       (0, -0.38, HZ),        "Body"),
        ("PodL",    (0.32, 0, HZ),        (0.56, 0, HZ),         "Body"),
        ("PodR",    (-0.32, 0, HZ),       (-0.56, 0, HZ),        "Body"),
        ("Antenna", (0.08, 0.04, HZ+0.24),(0.23, 0.11, HZ+0.58), "Body"),
        ("Turret",  (0, -0.06, HZ-0.28),  (0, -0.40, HZ-0.28),   "Body"),
    ]
    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)

    # Idle
    anim.new_action("Idle")
    pq = {}
    for f in range(1, 37):
        anim.begin(f)
        a = ((f - 1) / 35.0) * 2.0 * math.pi
        s, c = math.sin(a), math.cos(a)
        anim.loc("Body", 0, 0, 0.08 * s)
        anim.rot("Body", 4.0 * c, 8.0 * s, 5.0 * s)
        anim.rot("PodL", -12.0 * c, 0, 6.0 * s)
        anim.rot("PodR", -12.0 * c, 0, -6.0 * s)
        anim.rot("Antenna", 5.0 * s, 0, 6.0 * c)
        anim.key(f, pq)

    # Walk / Patrol hover
    anim.new_action("Walk")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        a = ((f - 1) / 23.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, 0, 0.06 * s)
        anim.rot("Body", 14.0 + 3.0 * s, 0, 6.0 * s)
        anim.rot("PodL", -24.0, 0, 0)
        anim.rot("PodR", -24.0, 0, 0)
        anim.key(f, pq)

    # Run / Fast pursuit dive
    anim.new_action("Run")
    pq = {}
    for f in range(1, 19):
        anim.begin(f)
        a = ((f - 1) / 17.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, -0.05, 0.05 * s)
        anim.rot("Body", 26.0 + 4.0 * s, 0, 10.0 * s)
        anim.rot("PodL", -40.0, 0, 0)
        anim.rot("PodR", -40.0, 0, 0)
        anim.key(f, pq)

    # Slash1 / Burst Attack
    anim.new_action("Slash1")
    pq = {}
    for f in range(1, 17):
        anim.begin(f)
        t = (f - 1) / 15.0
        kick = math.sin(t * math.pi * 4.0)
        anim.loc("Body", 0, 0.08 * max(0, kick), 0.03 * kick)
        anim.rot("Body", -10.0 * kick, 0, 0)
        anim.loc("Turret", 0, 0.06 * max(0, kick), 0)
        anim.key(f, pq)

    # Death (Spin & crash to ground)
    anim.new_action("Death")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        t = (f - 1) / 23.0
        s = t * t
        anim.loc("Body", 0.12 * s, 0.10 * s, -0.82 * s)
        anim.rot("Body", 65.0 * s, 210.0 * s, 48.0 * s)
        anim.rot("PodL", 35.0 * s, 0, -40.0 * s)
        anim.rot("PodR", -30.0 * s, 0, 45.0 * s)
        anim.key(f, pq)

    anim.set_idle_pose()
    return export_robot_fbx("Robot_Drone", rig, mesh)


# ==============================================================================
# 4. ROBOT GUNNER (Heavy Belt-Fed Autocannon Mech - Ref #4 Full Robot!)
# ==============================================================================
def build_robot_gunner():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Gunner", pal)

    # Broad Armored Chassis
    rb.add_taper_box("Body", (0, 0, 0.80), (0.36, 0.26), (0.44, 0.30), 0.18, "charcoal")
    rb.add_taper_box("Body", (0, -0.02, 1.16), (0.44, 0.32), (0.56, 0.38), 0.44, "graphite")
    # Industrial Yellow Hazard Vest & Collar Armor (Ref #4 vest reimagined as heavy armor!)
    rb.add_box("Body", (0, -0.18, 1.18), (0.44, 0.10, 0.30), "yellow", rot_deg=(6, 0, 0))
    rb.add_box("Body", (0, -0.19, 1.18), (0.14, 0.12, 0.32), "charcoal", rot_deg=(6, 0, 0))
    rb.add_box("Body", (0, -0.24, 1.24), (0.08, 0.03, 0.12), "glow_amb")
    # Armored High Neck Collar
    rb.add_box("Body", (0, 0.04, 1.38), (0.42, 0.28, 0.10), "armor_lt")
    # Twin Large Rear Ammo Drum Canisters
    rb.add_cyl("Body", (0.15, 0.24, 1.02), (0.15, 0.24, 1.44), 0.12, 0.12, "charcoal", 12)
    rb.add_cyl("Body", (-0.15, 0.24, 1.02), (-0.15, 0.24, 1.44), 0.12, 0.12, "charcoal", 12)
    rb.add_cyl("Body", (0.15, 0.24, 1.24), (0.15, 0.24, 1.30), 0.128, 0.128, "yellow", 12)
    rb.add_cyl("Body", (-0.15, 0.24, 1.24), (-0.15, 0.24, 1.30), 0.128, 0.128, "yellow", 12)
    # Curved Brass Ammo Belt routing from rear drum to right autocannon!
    belt_pts = [
        (-0.18, 0.24, 1.06), (-0.32, 0.16, 0.90),
        (-0.42, 0.02, 0.80), (-0.42, -0.14, 0.86),
    ]
    rb.add_tube_chain("Body", belt_pts, 0.044, "brass", 8)

    # Cyclops Targeting Helmet Head (Iconic Ref #4 Red Cyclops Goggle turned Full Robot!)
    rb.add_cyl("Head", (0, 0, 1.38), (0, 0, 1.48), 0.09, 0.09, "charcoal", 10)
    rb.add_sphere("Head", (0, -0.01, 1.60), (0.17, 0.18, 0.15), "crimson", 14, 10)
    rb.add_box("Head", (0, -0.02, 1.56), (0.34, 0.24, 0.11), "charcoal")
    # Big Oversized Glowing Red Targeting Lens
    rb.add_cyl("Head", (0, -0.14, 1.57), (0, -0.22, 1.57), 0.088, 0.078, "steel", 14)
    rb.add_sphere("Head", (0, -0.21, 1.57), (0.068, 0.045, 0.068), "glow_red", 12, 8)
    # Side Rangefinder Ear Pod
    rb.add_box("Head", (0.19, 0.02, 1.58), (0.08, 0.14, 0.12), "yellow")

    # Right Arm: MASSIVE Integrated Rotary Heavy Autocannon
    rb.add_box("ArmR", (-0.34, 0, 1.26), (0.18, 0.22, 0.18), "yellow")
    rb.add_cyl("ArmR", (-0.36, 0, 1.20), (-0.40, -0.10, 0.94), 0.085, 0.075, "charcoal", 10)
    # Heavy Receiver Housing
    rb.add_box("ArmR", (-0.40, -0.22, 0.90), (0.18, 0.36, 0.18), "graphite")
    rb.add_box("ArmR", (-0.40, -0.20, 0.99), (0.14, 0.26, 0.05), "yellow")
    # Spinning 4-Barrel Cluster (Separate bone so it rotates during Attack!)
    rb.add_cyl("CannonBarrel", (-0.40, -0.40, 0.90), (-0.40, -0.46, 0.90), 0.082, 0.082, "charcoal", 12)
    rb.add_cyl("CannonBarrel", (-0.40, -0.78, 0.90), (-0.40, -0.84, 0.90), 0.082, 0.082, "charcoal", 12)
    for deg in (0, 90, 180, 270):
        rad = math.radians(deg)
        bx = -0.40 + math.cos(rad) * 0.046
        bz = 0.90 + math.sin(rad) * 0.046
        rb.add_cyl("CannonBarrel", (bx, -0.40, bz), (bx, -0.92, bz), 0.020, 0.020, "steel", 8)
    rb.add_cyl("CannonBarrel", (-0.40, -0.88, 0.90), (-0.40, -0.94, 0.90), 0.032, 0.032, "glow_amb", 8)

    # Left Arm: Stabilizer Claw + Forearm Armor
    rb.add_box("ArmL", (0.34, 0, 1.26), (0.16, 0.20, 0.16), "yellow")
    rb.add_cyl("ArmL", (0.36, 0, 1.20), (0.38, -0.06, 0.94), 0.070, 0.062, "graphite", 10)
    rb.add_box("ForearmL", (0.38, -0.16, 0.84), (0.12, 0.26, 0.12), "armor_lt")
    rb.add_box("ForearmL", (0.38, -0.32, 0.84), (0.10, 0.10, 0.12), "charcoal")

    # Heavy Planted Recoil-Stabilized Legs (Feet flat at z=0.0)
    rb.add_cyl("LegL", (0.18, 0, 0.78), (0.20, -0.04, 0.44), 0.095, 0.082, "charcoal", 10, mirror_bone="LegR")
    rb.add_box("LegL", (0.20, -0.04, 0.60), (0.18, 0.20, 0.24), "graphite", mirror_bone="LegR")
    rb.add_box("ShinL", (0.20, -0.10, 0.44), (0.16, 0.14, 0.14), "yellow", mirror_bone="ShinR")
    rb.add_cyl("ShinL", (0.20, -0.02, 0.44), (0.19, 0.04, 0.12), 0.082, 0.072, "steel", 10, mirror_bone="ShinR")
    rb.add_taper_box("ShinL", (0.19, -0.04, 0.065), (0.22, 0.36), (0.16, 0.26), 0.13, "charcoal", mirror_bone="ShinR")
    rb.add_cyl("ShinL", (0.19, 0.10, 0.24), (0.19, 0.24, 0.05), 0.032, 0.032, "steel", 8, mirror_bone="ShinR")

    bones = [
        ("Root",         (0, 0, 0),            (0, 0, 0.35),       None),
        ("Body",         (0, 0, 0.80),         (0, 0, 1.40),       "Root"),
        ("Head",         (0, 0, 1.42),         (0, 0, 1.74),       "Body"),
        ("ArmL",         (0.34, 0, 1.24),      (0.38, -0.06, 0.94),"Body"),
        ("ForearmL",     (0.38, -0.06, 0.94),  (0.38, -0.34, 0.84),"ArmL"),
        ("ArmR",         (-0.34, 0, 1.24),     (-0.40, -0.22, 0.90),"Body"),
        ("CannonBarrel", (-0.40, -0.40, 0.90), (-0.40, -0.92, 0.90),"ArmR"),
        ("LegL",         (0.18, 0, 0.78),      (0.20, -0.04, 0.44),"Root"),
        ("ShinL",        (0.20, -0.04, 0.44),  (0.19, 0.0, 0.05),  "LegL"),
        ("LegR",         (-0.18, 0, 0.78),     (-0.20, -0.04, 0.44),"Root"),
        ("ShinR",        (-0.20, -0.04, 0.44), (-0.19, 0.0, 0.05), "LegR"),
    ]
    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)
    animate_biped_mech(anim, style="gunner")
    anim.set_idle_pose()
    return export_robot_fbx("Robot_Gunner", rig, mesh)


# ==============================================================================
# 5. ROBOT KAMIKAZE (4-Legged Spider-Mine Bomb Bot - Non-Humanoid!)
# ==============================================================================
def build_robot_kamikaze():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Kamikaze", pal)

    BZ = 0.56  # Chassis height
    # Wide Octagonal Hazard-Yellow & Charcoal Mine Shell
    rb.add_cyl("Body", (0, 0, BZ - 0.14), (0, 0, BZ + 0.12), 0.44, 0.38, "graphite", 8)
    rb.add_cyl("Body", (0, 0, BZ - 0.04), (0, 0, BZ + 0.08), 0.48, 0.46, "yellow", 8)
    rb.add_cyl("Body", (0, 0, BZ - 0.16), (0, 0, BZ - 0.06), 0.36, 0.44, "charcoal", 8)
    # 4 Protective Roll-Cage Containment Ribs around the bomb core
    for deg in (45, 135, 225, 315):
        rad = math.radians(deg)
        cx, cy = math.cos(rad), math.sin(rad)
        rb.add_cyl("Body", (cx * 0.38, cy * 0.38, BZ + 0.06), (cx * 0.22, cy * 0.22, BZ + 0.42), 0.042, 0.032, "charcoal", 8)
        rb.add_box("Body", (cx * 0.46, cy * 0.46, BZ + 0.02), (0.14, 0.14, 0.10), "armor_lt", rot_deg=(0, 0, deg))
    # Front Optical Sensor Eye
    rb.add_box("Body", (0, -0.45, BZ + 0.02), (0.20, 0.08, 0.11), "charcoal")
    rb.add_sphere("Body", (0, -0.49, BZ + 0.02), (0.065, 0.04, 0.045), "glow_red", 10, 8)

    # Exposed Pulsing Unstable Plasma Bomb Core (Dedicated bone for Explode swelling!)
    rb.add_sphere("CoreBomb", (0, 0, BZ + 0.24), (0.26, 0.26, 0.24), "glow_red", 14, 10)
    rb.add_cyl("CoreBomb", (0, 0, BZ + 0.42), (0, 0, BZ + 0.66), 0.045, 0.018, "steel", 8)
    rb.add_sphere("CoreBomb", (0, 0, BZ + 0.68), 0.062, "glow_amb", 10, 8)

    # 4 Articulated Sharp Insectoid Spider Legs (Planted solidly at z=0.0!)
    leg_configs = [
        ("FL",  1, -1, 42),
        ("FR", -1, -1, 138),
        ("BL",  1,  1, -42),
        ("BR", -1,  1, -138),
    ]
    bones = [
        ("Root",     (0, 0, 0),       (0, 0, 0.30),      None),
        ("Body",     (0, 0, BZ),      (0, 0, BZ + 0.25), "Root"),
        ("CoreBomb", (0, 0, BZ+0.24), (0, 0, BZ + 0.68), "Body"),
    ]
    for tag, sx, sy, ang in leg_configs:
        hip  = Vector((sx * 0.38, sy * 0.34, BZ - 0.02))
        knee = Vector((sx * 0.66, sy * 0.56, BZ + 0.18))
        foot = Vector((sx * 0.78, sy * 0.66, 0.0))
        u_name, l_name = f"Leg{tag}_U", f"Leg{tag}_L"
        rb.add_sphere(u_name, hip, 0.075, "charcoal", 10, 8)
        rb.add_cyl(u_name, hip, knee, 0.055, 0.048, "graphite", 8)
        rb.add_sphere(l_name, knee, 0.068, "yellow", 10, 8)
        rb.add_cyl(l_name, knee, (knee + foot) * 0.5, 0.052, 0.038, "charcoal", 8)
        rb.add_cyl(l_name, (knee + foot) * 0.5, foot, 0.038, 0.016, "steel", 8)
        bones.append((u_name, tuple(hip), tuple(knee), "Body"))
        bones.append((l_name, tuple(knee), tuple(foot), u_name))

    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)

    # Idle
    anim.new_action("Idle")
    pq = {}
    for f in range(1, 31):
        anim.begin(f)
        a = ((f - 1) / 29.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, 0, 0.03 * s)
        anim.scale("CoreBomb", 1.0 + 0.08 * s, 1.0 + 0.08 * s, 1.0 + 0.08 * s)
        anim.key(f, pq)

    # Walk / Run spider skitter
    for act_name, frames, amp in (("Walk", 20, 18.0), ("Run", 14, 28.0)):
        anim.new_action(act_name)
        pq = {}
        for f in range(1, frames + 1):
            anim.begin(f)
            a = ((f - 1) / float(frames - 1)) * 2.0 * math.pi
            s, c = math.sin(a), math.cos(a)
            anim.loc("Body", 0, 0, 0.04 * abs(s))
            anim.rot("Body", 8.0 if act_name == "Run" else 3.0, 5.0 * s, 4.0 * c)
            anim.scale("CoreBomb", 1.0 + 0.12 * abs(s), 1.0 + 0.12 * abs(s), 1.0 + 0.12 * abs(s))
            # Diagonal gait: FL+BR vs FR+BL
            for tag, phase in (("FL", 1.0), ("BR", 1.0), ("FR", -1.0), ("BL", -1.0)):
                anim.rot(f"Leg{tag}_U", amp * 0.5 * s * phase, amp * 0.7 * c * phase, amp * 0.5 * s * phase)
                anim.rot(f"Leg{tag}_L", -max(0.0, s * phase) * amp, 0, 0)
            anim.key(f, pq)

    # Slash1 (Leap headbutt)
    anim.new_action("Slash1")
    pq = {}
    for f in range(1, 17):
        anim.begin(f)
        t = (f - 1) / 15.0
        s = math.sin(t * math.pi)
        anim.loc("Body", 0, -0.18 * s, 0.22 * s)
        anim.rot("Body", 22.0 * s, 0, 0)
        anim.scale("CoreBomb", 1.0 + 0.28 * s, 1.0 + 0.28 * s, 1.0 + 0.28 * s)
        anim.key(f, pq)

    # Explode (Hunker down + violent unstable core swelling!)
    anim.new_action("Explode")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        t = (f - 1) / 23.0
        vib = math.sin(t * math.pi * 12.0) * 6.0 * t
        sw = 1.0 + 0.85 * (t * t)
        anim.loc("Body", 0, 0, -0.18 * t)
        anim.rot("Body", vib, 0, vib * 0.8)
        anim.scale("CoreBomb", sw, sw, sw)
        for tag in ("FL", "FR", "BL", "BR"):
            anim.rot(f"Leg{tag}_U", 0, 0, 22.0 * t)
        anim.key(f, pq)

    # Death
    anim.new_action("Death")
    pq = {}
    for f in range(1, 21):
        anim.begin(f)
        t = (f - 1) / 19.0
        s = t * t
        anim.loc("Body", 0, 0, -0.40 * s)
        anim.rot("Body", 25.0 * s, 0, 18.0 * s)
        anim.scale("CoreBomb", 1.0 - 0.5 * s, 1.0 - 0.5 * s, 1.0 - 0.5 * s)
        for tag in ("FL", "FR", "BL", "BR"):
            anim.rot(f"Leg{tag}_U", 35.0 * s, 0, 0)
        anim.key(f, pq)

    anim.set_idle_pose()
    return export_robot_fbx("Robot_Kamikaze", rig, mesh)


# ==============================================================================
# 6. ROBOT LASER (3-Legged Tall Prism Obelisk Tripod - Direct Ref #1!)
# ==============================================================================
def build_robot_laser():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Laser", pal)

    # Central Waist Collar & Glowing Red Sensor Eye Ring (Direct Ref #1!)
    WZ = 0.62
    rb.add_cyl("Body", (0, 0, WZ - 0.08), (0, 0, WZ + 0.10), 0.30, 0.28, "armor_lt", 8)
    rb.add_cyl("Body", (0, 0, WZ - 0.02), (0, 0, WZ + 0.05), 0.32, 0.32, "crimson", 8)
    rb.add_cyl("Body", (0, -0.26, WZ + 0.02), (0, -0.34, WZ + 0.02), 0.11, 0.095, "charcoal", 12)
    rb.add_sphere("Body", (0, -0.33, WZ + 0.02), (0.075, 0.05, 0.075), "glow_red", 12, 8)

    # Tall Armored Hexagonal Prism Tower Column (z = 0.72..1.76)
    rb.add_cyl("Body", (0, 0, 0.72), (0, 0, 1.56), 0.25, 0.24, "graphite", 6)
    rb.add_cyl("Body", (0, 0, 1.56), (0, 0, 1.78), 0.24, 0.15, "graphite", 6)
    # Armored Side Crimson Accent Ribs along the prism
    for sx in (-1.0, 1.0):
        rb.add_box("Body", (sx * 0.23, 0.0, 1.18), (0.06, 0.14, 0.84), "crimson")
        rb.add_box("Body", (sx * 0.26, 0.0, 1.18), (0.03, 0.06, 0.76), "steel")
    # Iconic Vertical Recessed Glowing Crimson Energy Slit down the front face!
    rb.add_box("Body", (0, -0.21, 1.18), (0.12, 0.06, 0.88), "charcoal")
    rb.add_box("Body", (0, -0.24, 1.18), (0.065, 0.03, 0.84), "glow_red")
    rb.add_box("Body", (0, 0.20, 1.18), (0.14, 0.08, 0.72), "charcoal")

    # Retractable Laser Emitter Focus Spire (Extends up & glows during Charge/Attack like Ref #1 Row 2!)
    rb.add_cyl("PrismTop", (0, 0, 1.66), (0, 0, 1.84), 0.11, 0.07, "steel", 6)
    rb.add_cyl("PrismTop", (0, 0, 1.82), (0, 0, 2.08), 0.055, 0.008, "glow_red", 8)

    # 3 Articulated Razor-Curved Tripod Spider Legs (120 deg symmetry, planted cleanly at z=0.0!)
    # Angles: Front-Left (+55° from -Y), Front-Right (-55° from -Y), Back-Center (+Y)
    tripod_angles = [
        ("1", math.radians(-145)),  # Front-Left
        ("2", math.radians(-35)),   # Front-Right
        ("3", math.radians(90)),    # Back-Center
    ]
    bones = [
        ("Root",     (0, 0, 0),       (0, 0, 0.35), None),
        ("Body",     (0, 0, WZ),      (0, 0, 1.68), "Root"),
        ("PrismTop", (0, 0, 1.68),    (0, 0, 2.06), "Body"),
    ]
    for idx, ang in tripod_angles:
        dx, dy = math.cos(ang), math.sin(ang)
        hip  = Vector((dx * 0.26, dy * 0.26, WZ - 0.06))
        knee = Vector((dx * 0.56, dy * 0.56, WZ + 0.08))
        mid  = Vector((dx * 0.62, dy * 0.62, 0.28))
        foot = Vector((dx * 0.48, dy * 0.48, 0.0))
        u_name, l_name = f"Leg{idx}_U", f"Leg{idx}_L"
        rb.add_sphere(u_name, hip, 0.072, "charcoal", 10, 8)
        rb.add_cyl(u_name, hip, knee, 0.052, 0.044, "graphite", 8)
        rb.add_sphere(l_name, knee, 0.064, "crimson", 10, 8)
        rb.add_cyl(l_name, knee, mid, 0.048, 0.042, "charcoal", 8)
        rb.add_cyl(l_name, mid, foot, 0.042, 0.015, "steel", 8)
        bones.append((u_name, tuple(hip), tuple(knee), "Body"))
        bones.append((l_name, tuple(knee), tuple(foot), u_name))

    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)

    # Idle
    anim.new_action("Idle")
    pq = {}
    for f in range(1, 37):
        anim.begin(f)
        a = ((f - 1) / 35.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, 0, 0.025 * s)
        anim.loc("PrismTop", 0, 0, -0.10 + 0.02 * s)
        anim.key(f, pq)

    # Walk / Run (Eerie 3-legged tripod stalk)
    for act_name, frames, amp in (("Walk", 24, 18.0), ("Run", 16, 26.0)):
        anim.new_action(act_name)
        pq = {}
        for f in range(1, frames + 1):
            anim.begin(f)
            a = ((f - 1) / float(frames - 1)) * 2.0 * math.pi
            s, c = math.sin(a), math.cos(a)
            anim.loc("Body", 0, 0, 0.04 * abs(s))
            anim.rot("Body", 6.0 if act_name == "Run" else 3.0, 6.0 * s, 4.0 * c)
            anim.loc("PrismTop", 0, 0, -0.08)
            for idx, ph in (("1", 0.0), ("2", 2.1), ("3", 4.2)):
                lp = math.sin(a + ph)
                anim.rot(f"Leg{idx}_U", amp * 0.5 * lp, amp * 0.4 * math.cos(a + ph), 0)
                anim.rot(f"Leg{idx}_L", -max(0.0, lp) * amp, 0, 0)
            anim.key(f, pq)

    # Charge / Laser Fire (Plants low & extends glowing PrismTop spire high up like Ref #1 Row 2!)
    anim.new_action("Charge")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        a = ((f - 1) / 23.0) * 2.0 * math.pi
        pulse = math.sin(a * 3.0)
        anim.loc("Body", 0, 0, -0.06)
        anim.rot("Body", 5.0 + 1.5 * pulse, 0, 0)
        anim.loc("PrismTop", 0, 0, 0.18 + 0.03 * pulse)
        anim.scale("PrismTop", 1.18 + 0.10 * pulse, 1.18 + 0.10 * pulse, 1.25 + 0.10 * pulse)
        anim.key(f, pq)

    # Slash1 (Beam burst recoil)
    anim.new_action("Slash1")
    pq = {}
    for f in range(1, 17):
        anim.begin(f)
        t = (f - 1) / 15.0
        s = math.sin(t * math.pi)
        anim.loc("Body", 0, 0.08 * s, -0.05 * s)
        anim.rot("Body", -10.0 * s, 0, 0)
        anim.loc("PrismTop", 0, 0, 0.20 * s)
        anim.key(f, pq)

    # Death (Tilts & collapses sideways like Ref #1 bottom-right frame!)
    anim.new_action("Death")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        t = (f - 1) / 23.0
        s = t * t * (3 - 2 * t)
        anim.loc("Body", 0.25 * s, 0.10 * s, -0.45 * s)
        anim.rot("Body", 18.0 * s, 0, -78.0 * s)
        anim.loc("PrismTop", 0, 0, -0.18 * s)
        anim.key(f, pq)

    anim.set_idle_pose()
    return export_robot_fbx("Robot_Laser", rig, mesh)


# ==============================================================================
# 7. ROBOT TANKER (Hulking Asymmetric Juggernaut Riot-Ram Mech - Ref #5 Full Robot!)
# ==============================================================================
def build_robot_tanker():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Tanker", pal)

    # Massive Broad Bunker Torso (Yellow Hazard + Heavy Charcoal/Graphite Armor)
    rb.add_taper_box("Body", (0, 0, 0.88), (0.48, 0.36), (0.60, 0.44), 0.24, "charcoal")
    rb.add_taper_box("Body", (0, -0.03, 1.32), (0.58, 0.44), (0.76, 0.52), 0.58, "graphite")
    # Sloped Frontal Bunker Chest Plate + Crimson Warning Emblem
    rb.add_box("Body", (0, -0.26, 1.34), (0.60, 0.14, 0.42), "yellow", rot_deg=(10, 0, 0))
    rb.add_box("Body", (0, -0.32, 1.36), (0.28, 0.06, 0.24), "charcoal", rot_deg=(10, 0, 0))
    rb.add_box("Body", (0, -0.35, 1.38), (0.14, 0.03, 0.12), "glow_red", rot_deg=(10, 0, 0))
    # Heavy Rear Reactor Pack & Twin Exhaust Stacks
    rb.add_box("Body", (0, 0.28, 1.34), (0.52, 0.22, 0.42), "charcoal")
    rb.add_cyl("Body", (0.18, 0.34, 1.48), (0.22, 0.40, 1.82), 0.065, 0.055, "steel", 10)
    rb.add_cyl("Body", (-0.18, 0.34, 1.48), (-0.22, 0.40, 1.82), 0.065, 0.055, "steel", 10)
    # Shoulder Hydraulic Cable Arc (Nod to Ref #5 shoulder hose!)
    rb.add_tube_chain("Body", [(0.34, 0.16, 1.56), (0.52, 0.12, 1.72), (0.50, -0.02, 1.46)], 0.036, "charcoal", 8)

    # Recessed Heavy Iron Bunker Helmet Head with Red Cyclops Visor
    rb.add_taper_box("Head", (0, -0.06, 1.74), (0.34, 0.34), (0.28, 0.28), 0.26, "armor_lt")
    rb.add_box("Head", (0, -0.21, 1.73), (0.26, 0.06, 0.09), "charcoal")
    rb.add_sphere("Head", (0, -0.24, 1.73), (0.075, 0.045, 0.058), "glow_red", 12, 8)
    rb.add_box("Head", (0, -0.04, 1.88), (0.26, 0.24, 0.06), "yellow")

    # MASSIVE OVERSIZED LEFT ARM (Ref #5 Giant Mechanical Arm + Integrated Riot-Ram Shield!)
    rb.add_sphere("ArmL", (0.48, 0.0, 1.44), (0.20, 0.22, 0.20), "graphite", 12, 10)
    rb.add_box("ArmL", (0.52, 0.0, 1.54), (0.26, 0.32, 0.14), "yellow", rot_deg=(0, 16, 0))
    rb.add_cyl("ArmL", (0.50, 0, 1.40), (0.56, -0.10, 1.02), 0.14, 0.13, "armor_lt", 10)
    # Giant Hydraulic Battering Forearm + Welded Frontal Blast Shield Ram!
    rb.add_box("ForearmL", (0.58, -0.22, 0.88), (0.28, 0.44, 0.28), "graphite")
    rb.add_box("ForearmL", (0.58, -0.44, 0.86), (0.26, 0.16, 0.24), "steel")
    # Welded Frontal Ballistic Riot Shield mounted to the giant left gauntlet!
    rb.add_taper_box("ForearmL", (0.44, -0.44, 1.02), (0.64, 0.10), (0.58, 0.10), 0.86, "graphite", rot_deg=(0, 0, -14))
    rb.add_box("ForearmL", (0.42, -0.50, 1.18), (0.46, 0.04, 0.14), "yellow", rot_deg=(0, 0, -14))
    rb.add_box("ForearmL", (0.42, -0.50, 0.86), (0.46, 0.04, 0.14), "yellow", rot_deg=(0, 0, -14))
    rb.add_box("ForearmL", (0.42, -0.51, 1.02), (0.28, 0.04, 0.07), "glow_amb", rot_deg=(0, 0, -14))

    # Right Arm: Heavy Armored Piston Brawler Arm
    rb.add_sphere("ArmR", (-0.44, 0, 1.40), 0.13, "charcoal", 10, 8)
    rb.add_box("ArmR", (-0.46, 0, 1.48), (0.20, 0.24, 0.12), "yellow", rot_deg=(0, -14, 0))
    rb.add_cyl("ArmR", (-0.46, 0, 1.36), (-0.48, -0.06, 1.02), 0.10, 0.09, "graphite", 10)
    rb.add_box("ForearmR", (-0.48, -0.18, 0.88), (0.18, 0.34, 0.18), "armor_lt")
    rb.add_box("ForearmR", (-0.48, -0.36, 0.88), (0.18, 0.12, 0.18), "steel")

    # Thick Squat Armored Bunker Legs (Feet flat at z=0.0!)
    rb.add_cyl("LegL", (0.24, 0, 0.84), (0.26, -0.04, 0.46), 0.13, 0.12, "charcoal", 10, mirror_bone="LegR")
    rb.add_box("LegL", (0.26, -0.04, 0.64), (0.24, 0.26, 0.28), "crimson_dk", mirror_bone="LegR")
    rb.add_box("ShinL", (0.26, -0.12, 0.46), (0.22, 0.16, 0.18), "yellow", mirror_bone="ShinR")
    rb.add_box("ShinL", (0.26, -0.02, 0.26), (0.22, 0.24, 0.30), "graphite", mirror_bone="ShinR")
    rb.add_taper_box("ShinL", (0.26, -0.06, 0.075), (0.28, 0.44), (0.22, 0.34), 0.15, "charcoal", mirror_bone="ShinR")

    bones = [
        ("Root",     (0, 0, 0),            (0, 0, 0.40),       None),
        ("Body",     (0, 0, 0.88),         (0, 0, 1.62),       "Root"),
        ("Head",     (0, -0.04, 1.62),     (0, -0.04, 1.92),   "Body"),
        ("ArmL",     (0.48, 0, 1.44),      (0.56, -0.10, 1.02),"Body"),
        ("ForearmL", (0.56, -0.10, 1.02),  (0.58, -0.48, 0.86),"ArmL"),
        ("ArmR",     (-0.44, 0, 1.40),     (-0.48, -0.06, 1.02),"Body"),
        ("ForearmR", (-0.48, -0.06, 1.02), (-0.48, -0.38, 0.88),"ArmR"),
        ("LegL",     (0.24, 0, 0.84),      (0.26, -0.04, 0.46),"Root"),
        ("ShinL",    (0.26, -0.04, 0.46),  (0.26, -0.04, 0.06),"LegL"),
        ("LegR",     (-0.24, 0, 0.84),     (-0.26, -0.04, 0.46),"Root"),
        ("ShinR",    (-0.26, -0.04, 0.46), (-0.26, -0.04, 0.06),"LegR"),
    ]
    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)
    animate_biped_mech(anim, style="tanker")
    anim.set_idle_pose()
    return export_robot_fbx("Robot_Tanker", rig, mesh)


# ==============================================================================
# 8. ROBOT LEADER (Massive 4-Legged Quadruped Siege Walker Boss - Ref #3!)
# ==============================================================================
def build_robot_leader():
    reset_scene()
    pal = create_robot_palette()
    rb = RobotRigBuilder("Robot_Leader", pal)

    BZ = 1.06  # Massive Quadruped Chassis Deck Height
    # Heavy Central Siege Walker Core Deck
    rb.add_taper_box("Body", (0, 0, BZ), (0.86, 0.78), (0.96, 0.84), 0.34, "graphite")
    rb.add_box("Body", (0, -0.36, BZ + 0.02), (0.64, 0.16, 0.24), "crimson")
    rb.add_box("Body", (0, -0.44, BZ + 0.04), (0.32, 0.05, 0.08), "glow_amb")
    # Armored Rear Command Backrest & Roll-Cage Cradle (Iconic Ref #3 Cockpit Frame!)
    rb.add_box("Body", (0, 0.26, BZ + 0.48), (0.74, 0.34, 0.68), "charcoal")
    rb.add_box("Body", (0, 0.38, BZ + 0.56), (0.62, 0.14, 0.46), "crimson")
    rb.add_box("Body", (0.32, 0.28, BZ + 0.52), (0.14, 0.22, 0.42), "yellow")
    rb.add_box("Body", (-0.32, 0.28, BZ + 0.52), (0.14, 0.22, 0.42), "yellow")
    # Armored Roll-Cage Bars flanking the autonomous AI core
    for sx in (-1.0, 1.0):
        rb.add_tube_chain("Body", [
            (sx * 0.30, 0.22, BZ + 0.16),
            (sx * 0.32, -0.18, BZ + 0.24),
            (sx * 0.28, -0.14, BZ + 0.78),
            (sx * 0.28, 0.22, BZ + 0.82),
        ], 0.036, "steel", 8)
    # Twin Heavy Front Reactor Pistons inside the cockpit cradle (Replaces human pilot legs!)
    rb.add_cyl("Body", (0.14, -0.18, BZ + 0.12), (0.14, -0.26, BZ + 0.44), 0.09, 0.08, "crimson", 10)
    rb.add_cyl("Body", (-0.14, -0.18, BZ + 0.12), (-0.14, -0.26, BZ + 0.44), 0.09, 0.08, "crimson", 10)

    # Autonomous AI Commander Sensor Head nested inside the armored cockpit (Full Robot Boss!)
    rb.add_cyl("Head", (0, 0.02, BZ + 0.42), (0, 0.02, BZ + 0.56), 0.14, 0.14, "charcoal", 12)
    rb.add_taper_box("Head", (0, -0.02, BZ + 0.72), (0.38, 0.38), (0.30, 0.30), 0.32, "graphite")
    rb.add_box("Head", (0, -0.02, BZ + 0.89), (0.28, 0.28, 0.06), "crimson")
    # Oversized Menacing Crimson Boss Cyclops Eye
    rb.add_cyl("Head", (0, -0.18, BZ + 0.72), (0, -0.27, BZ + 0.72), 0.125, 0.11, "steel", 14)
    rb.add_sphere("Head", (0, -0.26, BZ + 0.72), (0.095, 0.065, 0.095), "glow_red", 14, 10)
    # Dual Commander Antennae with Spark Beacon Tips (Ref #3 detail!)
    rb.add_cyl("Head", (0.14, 0.06, BZ + 0.88), (0.20, 0.12, BZ + 1.18), 0.020, 0.010, "steel", 8)
    rb.add_cyl("Head", (-0.14, 0.06, BZ + 0.88), (-0.20, 0.12, BZ + 1.18), 0.020, 0.010, "steel", 8)
    rb.add_sphere("Head", (0.20, 0.12, BZ + 1.20), 0.038, "glow_amb", 8, 6)
    rb.add_sphere("Head", (-0.20, 0.12, BZ + 1.20), 0.038, "glow_amb", 8, 6)

    # RIGHT SHOULDER: MASSIVE Heavy Siege Missile/Cannon Pod (Direct Ref #3 Silhouette!)
    rb.add_box("CannonPod", (-0.54, 0.14, BZ + 0.78), (0.38, 0.74, 0.36), "graphite", rot_deg=(12, 0, 0))
    rb.add_box("CannonPod", (-0.54, 0.12, BZ + 0.96), (0.34, 0.64, 0.08), "crimson", rot_deg=(12, 0, 0))
    rb.add_box("CannonPod", (-0.72, 0.14, BZ + 0.78), (0.06, 0.56, 0.22), "yellow", rot_deg=(12, 0, 0))
    # 4 Recessed Rocket Launch Tubes
    for ox in (-0.08, 0.08):
        for oz in (-0.07, 0.07):
            rb.add_cyl("CannonPod", (-0.54 + ox, -0.20, BZ + 0.84 + oz), (-0.54 + ox, -0.32, BZ + 0.86 + oz), 0.058, 0.052, "charcoal", 10)
            rb.add_sphere("CannonPod", (-0.54 + ox, -0.28, BZ + 0.85 + oz), 0.042, "glow_red", 8, 6)

    # LEFT ARTICULATED HYDRAULIC SIEGE CLEAVER / BLADE ARM (Direct Ref #3 Row 2 Attack!)
    rb.add_sphere("CleaverArm", (0.56, -0.06, BZ + 0.36), 0.14, "charcoal", 10, 8)
    rb.add_cyl("CleaverArm", (0.56, -0.06, BZ + 0.36), (0.66, -0.44, BZ + 0.14), 0.095, 0.085, "graphite", 10)
    rb.add_sphere("CleaverBlade", (0.66, -0.44, BZ + 0.14), 0.11, "yellow", 10, 8)
    rb.add_box("CleaverBlade", (0.66, -0.64, BZ + 0.12), (0.14, 0.36, 0.18), "charcoal")
    cleaver_pts = [
        (0.66, -0.58, BZ + 0.10),
        (0.66, -0.94, BZ + 0.06),
        (0.66, -1.28, BZ - 0.08),
        (0.66, -1.52, BZ - 0.26),
    ]
    rb.add_curved_blade("CleaverBlade", cleaver_pts, [0.22, 0.28, 0.22, 0.04], [0.055, 0.045, 0.032, 0.008], "steel")
    rb.add_tube_chain("CleaverBlade", [(0.66, -0.72, BZ + 0.16), (0.66, -1.10, BZ + 0.06), (0.66, -1.38, BZ - 0.12)], 0.022, "glow_red", 8)

    # 4 MASSIVE HEAVY HYDRAULIC QUADRUPED WALKER LEGS (Planted flat on z=0.0!)
    quad_legs = [
        ("FL",  1, -1),
        ("FR", -1, -1),
        ("BL",  1,  1),
        ("BR", -1,  1),
    ]
    bones = [
        ("Root",         (0, 0, 0),                   (0, 0, 0.50),               None),
        ("Body",         (0, 0, BZ),                  (0, 0, BZ + 0.65),          "Root"),
        ("Head",         (0, 0.02, BZ + 0.54),        (0, 0.02, BZ + 1.05),       "Body"),
        ("CannonPod",    (-0.54, 0.18, BZ + 0.76),    (-0.54, -0.32, BZ + 0.86),  "Body"),
        ("CleaverArm",   (0.56, -0.06, BZ + 0.36),    (0.66, -0.44, BZ + 0.14),   "Body"),
        ("CleaverBlade", (0.66, -0.44, BZ + 0.14),    (0.66, -1.40, BZ - 0.18),   "CleaverArm"),
    ]
    for tag, sx, sy in quad_legs:
        hip  = Vector((sx * 0.44, sy * 0.38, BZ - 0.02))
        knee = Vector((sx * 0.84, sy * 0.68, BZ + 0.08))
        foot = Vector((sx * 0.98, sy * 0.78, 0.10))
        u_name, l_name = f"Leg{tag}_U", f"Leg{tag}_L"
        rb.add_sphere(u_name, hip, 0.14, "charcoal", 10, 8)
        rb.add_cyl(u_name, hip, knee, 0.12, 0.11, "armor_lt", 8)
        rb.add_box(u_name, (hip + knee) * 0.5 + Vector((0, 0, 0.06)), (0.22, 0.22, 0.14), "graphite")
        rb.add_sphere(l_name, knee, 0.14, "crimson", 10, 8)
        rb.add_cyl(l_name, knee, foot, 0.13, 0.11, "graphite", 8)
        rb.add_cyl(l_name, knee + Vector((0, 0, -0.10)), foot + Vector((0, 0, 0.14)), 0.055, 0.055, "steel", 8)
        # Wide Heavy Armored Foot Pad flat on z=0.0
        rb.add_taper_box(l_name, (foot.x, foot.y, 0.07), (0.34, 0.38), (0.24, 0.28), 0.14, "charcoal")
        rb.add_box(l_name, (foot.x, foot.y - 0.14 * (-sy), 0.07), (0.20, 0.08, 0.08), "yellow")
        bones.append((u_name, tuple(hip), tuple(knee), "Body"))
        bones.append((l_name, tuple(knee), (foot.x, foot.y, 0.0), u_name))

    rig, mesh = rb.finalize_mesh_and_rig(bones)
    anim = RigAnimator(rig)

    # Idle
    anim.new_action("Idle")
    pq = {}
    for f in range(1, 37):
        anim.begin(f)
        a = ((f - 1) / 35.0) * 2.0 * math.pi
        s = math.sin(a)
        anim.loc("Body", 0, 0, 0.03 * s)
        anim.rot("Head", 0, 10.0 * math.sin(a * 0.5), 0)
        anim.rot("CannonPod", 4.0 * s, 0, 0)
        anim.rot("CleaverArm", 5.0 * s, 0, 0)
        anim.key(f, pq)

    # Walk / Run (Heavy 4-legged mechanical siege march)
    for act_name, frames, amp in (("Walk", 28, 14.0), ("Run", 20, 22.0)):
        anim.new_action(act_name)
        pq = {}
        for f in range(1, frames + 1):
            anim.begin(f)
            a = ((f - 1) / float(frames - 1)) * 2.0 * math.pi
            s, c = math.sin(a), math.cos(a)
            anim.loc("Body", 0.03 * s, 0, 0.05 * abs(s))
            anim.rot("Body", 5.0 if act_name == "Run" else 2.5, 4.0 * s, 3.0 * c)
            anim.rot("CannonPod", 4.0 * c, 0, 0)
            for tag, phase in (("FL", 1.0), ("BR", 1.0), ("FR", -1.0), ("BL", -1.0)):
                anim.rot(f"Leg{tag}_U", amp * 0.5 * s * phase, amp * 0.6 * c * phase, 0)
                anim.rot(f"Leg{tag}_L", -max(0.0, s * phase) * amp, 0, 0)
            anim.key(f, pq)

    # Slash1 (Massive Cleaver Arm Horizontal Sweep!)
    anim.new_action("Slash1")
    pq = {}
    for f in range(1, 25):
        anim.begin(f)
        t = (f - 1) / 23.0
        if t < 0.35:
            p = t / 0.35
            anim.rot("Body", -6.0 * p, -18.0 * p, 0)
            anim.rot("CleaverArm", 35.0 * p, -35.0 * p, 22.0 * p)
            anim.rot("CleaverBlade", 20.0 * p, 0, 0)
        elif t < 0.68:
            p = (t - 0.35) / 0.33
            anim.rot("Body", -6.0 + 18.0 * p, -18.0 + 44.0 * p, 0)
            anim.rot("CleaverArm", 35.0 - 45.0 * p, -35.0 + 82.0 * p, 22.0 - 28.0 * p)
            anim.rot("CleaverBlade", 20.0 - 30.0 * p, 0, 0)
        else:
            p = (t - 0.68) / 0.32
            anim.rot("Body", 12.0 * (1 - p), 26.0 * (1 - p), 0)
            anim.rot("CleaverArm", -10.0 * (1 - p), 47.0 * (1 - p), -6.0 * (1 - p))
        anim.key(f, pq)

    # Attack (Heavy Siege Missile Barrage Recoil!)
    anim.new_action("Attack")
    pq = {}
    for f in range(1, 21):
        anim.begin(f)
        t = (f - 1) / 19.0
        kick = math.sin(t * math.pi * 4.0)
        anim.loc("Body", 0, 0.08 * max(0, kick), -0.05)
        anim.rot("Body", -6.0 * max(0, kick), 0, 0)
        anim.loc("CannonPod", 0, 0.14 * max(0, kick), 0)
        anim.rot("CannonPod", -12.0 * max(0, kick), 0, 0)
        anim.key(f, pq)

    # Death (Heavy Quad Collapse!)
    anim.new_action("Death")
    pq = {}
    for f in range(1, 29):
        anim.begin(f)
        t = (f - 1) / 27.0
        s = t * t * (3 - 2 * t)
        anim.loc("Body", 0, 0, -0.72 * s)
        anim.rot("Body", 18.0 * s, 0, 12.0 * s)
        anim.rot("Head", 34.0 * s, -20.0 * s, 0)
        anim.rot("CannonPod", 28.0 * s, 0, -18.0 * s)
        anim.rot("CleaverArm", -35.0 * s, 0, 28.0 * s)
        for tag in ("FL", "FR", "BL", "BR"):
            anim.rot(f"Leg{tag}_U", 0, 0, 32.0 * s)
        anim.key(f, pq)

    anim.set_idle_pose()
    return export_robot_fbx("Robot_Leader", rig, mesh)


# ==============================================================================
# STUDIO 4x2 LINEUP RENDER FOR VISUAL QA
# ==============================================================================
def render_all8_lineup(fbx_paths):
    scene = reset_scene()
    world = bpy.data.worlds.new("StudioWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.14, 0.15, 0.19, 1.0)
        bg.inputs[1].default_value = 1.35

    # Studio ground plane
    bpy.ops.mesh.primitive_plane_add(size=24.0, location=(0, 0, 0))
    plane = bpy.context.active_object
    plane.data.materials.append(make_mat("StudioFloor", (0.12, 0.13, 0.16), roughness=0.92, metallic=0.0))

    # Place all 8 robots in 2 staggered rows (Front row: 4 smaller/mid, Back row: 4 taller/heavy)
    # Order: [Normal, Blade, Drone, Kamikaze] front | [Gunner, Laser, Tanker, Leader] back
    layout = [
        ("Robot_Normal",   (-3.1, -1.6, 0.0)),
        ("Robot_Blade",    (-1.0, -1.6, 0.0)),
        ("Robot_Drone",    ( 1.0, -1.6, 0.0)),
        ("Robot_Kamikaze", ( 3.1, -1.6, 0.0)),
        ("Robot_Gunner",   (-3.4,  1.5, 0.0)),
        ("Robot_Laser",    (-1.2,  1.5, 0.0)),
        ("Robot_Tanker",   ( 1.0,  1.5, 0.0)),
        ("Robot_Leader",   ( 3.5,  1.6, 0.0)),
    ]
    path_map = {os.path.basename(p).replace("_Rigged.fbx", ""): p for p in fbx_paths}

    for rname, pos in layout:
        fpath = path_map.get(rname)
        if not fpath or not os.path.exists(fpath):
            continue
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=fpath)
        added = set(bpy.data.objects) - before
        roots = [o for o in added if o.parent is None]
        holder = bpy.data.objects.new(f"Holder_{rname}", None)
        bpy.context.collection.objects.link(holder)
        holder.location = Vector(pos)
        holder.rotation_euler = Euler((0, 0, math.radians(-18)), 'XYZ')
        for r in roots:
            r.parent = holder
            if r.animation_data:
                r.animation_data.action = None

    # Lights
    for name, energy, rot in (
        ("KeyLight",  3.4, (50, -18, -24)),
        ("FillLight", 1.8, (64,  28,  34)),
        ("RimLight",  2.2, (42,  10, 165)),
    ):
        ld = bpy.data.lights.new(name, type='SUN')
        ld.energy = energy
        lo = bpy.data.objects.new(name, ld)
        bpy.context.collection.objects.link(lo)
        lo.rotation_euler = Euler([math.radians(a) for a in rot], 'XYZ')

    # Camera
    cd = bpy.data.cameras.new("LineupCam")
    cd.lens = 42
    co = bpy.data.objects.new("LineupCam", cd)
    bpy.context.collection.objects.link(co)
    scene.camera = co
    co.location = Vector((0.0, -9.8, 4.35))
    co.rotation_euler = (Vector((0.0, 0.2, 1.05)) - co.location).to_track_quat('-Z', 'Y').to_euler()

    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    preview_path = os.path.join(OUT_DIR, "Robots_All8_Lineup_Preview.png")
    scene.render.filepath = preview_path
    bpy.ops.render.render(write_still=True)
    print(f"[OK] Rendered All-8 Robot Lineup Preview: {preview_path}")


if __name__ == "__main__":
    fbxs = [
        build_robot_normal(),
        build_robot_blade(),
        build_robot_drone(),
        build_robot_gunner(),
        build_robot_kamikaze(),
        build_robot_laser(),
        build_robot_tanker(),
        build_robot_leader(),
    ]
    render_all8_lineup(fbxs)
    print("=== ALL 8 RIGGED ROBOTS GENERATED SUCCESSFULLY ===")

import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix, Quaternion, Euler

# ==============================================================================
# 1. SCENE RESET & CALIBRATED URP-READY MATERIALS
# ==============================================================================
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24

def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

def make_mat(name, rgba, roughness=0.90, metallic=0.0, specular=0.04):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    lin_rgba = (srgb_to_lin(rgba[0]), srgb_to_lin(rgba[1]), srgb_to_lin(rgba[2]), rgba[3])
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = lin_rgba
        bsdf.inputs["Roughness"].default_value = roughness
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = specular
    mat.diffuse_color = lin_rgba
    mat.roughness = roughness
    mat.metallic = metallic
    mat.specular_intensity = specular
    return mat

MAT_SKIN            = make_mat("Mat_Skin",            (0.98, 0.84, 0.76, 1.0), roughness=0.90, specular=0.04)
MAT_HAIR_BLONDE     = make_mat("Mat_Hair_Blonde",     (0.96, 0.86, 0.58, 1.0), roughness=0.88, specular=0.05)
MAT_HAIR_BLONDE_DARK= make_mat("Mat_Hair_Blonde_Dark",(0.86, 0.74, 0.44, 1.0), roughness=0.90, specular=0.04)
MAT_HAIR_PINK       = make_mat("Mat_Hair_Pink",       (0.95, 0.24, 0.52, 1.0), roughness=0.86, specular=0.06)
MAT_HAIR_PINK_DARK  = make_mat("Mat_Hair_Pink_Dark",  (0.76, 0.12, 0.38, 1.0), roughness=0.88, specular=0.04)

MAT_JACKET_RED      = make_mat("Mat_Jacket_Red",      (0.88, 0.10, 0.16, 1.0), roughness=0.88, specular=0.05)
MAT_JACKET_RED_BRT  = make_mat("Mat_Jacket_Red_Bright", (0.96, 0.18, 0.22, 1.0), roughness=0.85, specular=0.06)
MAT_DARK_BLACK      = make_mat("Mat_Dark_Black",      (0.12, 0.12, 0.14, 1.0), roughness=0.92, specular=0.03)
MAT_DARK_STRAP      = make_mat("Mat_Dark_Strap",      (0.08, 0.08, 0.09, 1.0), roughness=0.90, specular=0.04)
MAT_METAL_SILVER    = make_mat("Mat_Metal_Silver",    (0.78, 0.80, 0.85, 1.0), roughness=0.40, metallic=0.75, specular=0.45)
MAT_ZIPPER_TEETH    = make_mat("Mat_Zipper_Teeth",    (0.85, 0.86, 0.90, 1.0), roughness=0.35, metallic=0.80, specular=0.50)

MAT_CHEST_TOP       = make_mat("Mat_Chest_Top",       (0.14, 0.14, 0.16, 1.0), roughness=0.90, specular=0.04)
MAT_PANTS_BLACK     = make_mat("Mat_Pants_Black",     (0.15, 0.15, 0.18, 1.0), roughness=0.92, specular=0.03)
MAT_GLOVE_BLACK     = make_mat("Mat_Glove_Black",     (0.12, 0.12, 0.14, 1.0), roughness=0.90, specular=0.04)
MAT_ACCENT_RED      = make_mat("Mat_Accent_Red",      (0.90, 0.12, 0.18, 1.0), roughness=0.85, specular=0.06)

MAT_EYE_WHITE       = make_mat("Mat_Eye_White",       (0.99, 0.99, 1.00, 1.0), roughness=0.12, specular=0.55)
MAT_EYE_RED         = make_mat("Mat_Eye_Red",         (0.86, 0.08, 0.14, 1.0), roughness=0.08, specular=0.75)
MAT_EYE_RED_DARK    = make_mat("Mat_Eye_Red_Dark",    (0.55, 0.04, 0.08, 1.0), roughness=0.12, specular=0.60)
MAT_EYE_GOLD_HI     = make_mat("Mat_Eye_Gold_Hi",     (1.00, 0.88, 0.40, 1.0), roughness=0.06, specular=0.85)
MAT_GLOSSY_BLACK    = make_mat("Mat_Glossy_Black",    (0.03, 0.03, 0.04, 1.0), roughness=0.05, specular=0.90)

MAT_WARPAINT_RED    = make_mat("Mat_Warpaint_Red",    (0.84, 0.08, 0.14, 1.0), roughness=0.80, specular=0.08)
MAT_EYELINER_BLACK  = make_mat("Mat_Eyeliner_Black",  (0.06, 0.06, 0.07, 1.0), roughness=0.50, specular=0.20)
MAT_MOUTH_DARK      = make_mat("Mat_Mouth_Dark",      (0.18, 0.05, 0.08, 1.0), roughness=0.60, specular=0.10)
MAT_FANG_WHITE      = make_mat("Mat_Fang_White",      (0.98, 0.98, 0.98, 1.0), roughness=0.25, specular=0.40)

MAT_CHARM_YELLOW    = make_mat("Mat_Charm_Yellow",    (0.98, 0.82, 0.15, 1.0), roughness=0.65, specular=0.15)
MAT_BAT_WOOD        = make_mat("Mat_Bat_Wood",        (0.68, 0.52, 0.38, 1.0), roughness=0.82, specular=0.05)
MAT_BAT_TAPE        = make_mat("Mat_Bat_Tape",        (0.86, 0.84, 0.78, 1.0), roughness=0.88, specular=0.04)
MAT_BAT_NAIL        = make_mat("Mat_Bat_Nail",        (0.48, 0.44, 0.42, 1.0), roughness=0.50, metallic=0.75, specular=0.35)

# ==============================================================================
# 2. GEOMETRY HELPERS
# ==============================================================================
def assign_single_material(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.material_index = 0
        poly.use_smooth = True
    return obj

def create_loft_object(name, rings, mat, cap_start=True, cap_end=True, segments=32):
    bm = bmesh.new()
    N = segments
    vert_rings = []
    for center, vec_u, vec_v in rings:
        ring = []
        for i in range(N):
            ang = (2.0 * math.pi * i) / N
            pt = center + vec_u * math.cos(ang) + vec_v * math.sin(ang)
            ring.append(bm.verts.new(pt))
        vert_rings.append(ring)

    for r in range(len(rings) - 1):
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((vert_rings[r][i], vert_rings[r][ni], vert_rings[r+1][ni], vert_rings[r+1][i]))

    if cap_start:
        c_start = bm.verts.new(rings[0][0])
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((c_start, vert_rings[0][ni], vert_rings[0][i]))

    if cap_end:
        last = len(rings) - 1
        c_end = bm.verts.new(rings[last][0])
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((c_end, vert_rings[last][i], vert_rings[last][ni]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def add_capsule(name, start, end, rx, ry=None, mat=MAT_DARK_BLACK, segments=24, rings_end=8):
    if ry is None: ry = rx
    a, b = Vector(start), Vector(end)
    axis = b - a
    length = axis.length
    if length < 1e-6:
        axis = Vector((0, 0, 1))
        length = 0.001
    else:
        axis.normalize()
    quat = Vector((0, 0, 1)).rotation_difference(axis)

    bm = bmesh.new()
    N = segments
    all_rings = []

    for r_i in range(rings_end + 1):
        frac = r_i / rings_end
        ang = frac * (math.pi * 0.5)
        cz = -math.cos(ang) * rx
        rad_scale = math.sin(ang)
        ring = []
        for i in range(N):
            a_rot = (2.0 * math.pi * i) / N
            lx = math.cos(a_rot) * rx * rad_scale
            ly = math.sin(a_rot) * ry * rad_scale
            local_pt = Vector((lx, ly, cz))
            world_pt = a + quat @ local_pt
            ring.append(bm.verts.new(world_pt))
        all_rings.append(ring)

    for r_i in range(rings_end + 1):
        frac = r_i / rings_end
        ang = frac * (math.pi * 0.5)
        cz = length + math.sin(ang) * rx
        rad_scale = math.cos(ang)
        ring = []
        for i in range(N):
            a_rot = (2.0 * math.pi * i) / N
            lx = math.cos(a_rot) * rx * rad_scale
            ly = math.sin(a_rot) * ry * rad_scale
            local_pt = Vector((lx, ly, cz))
            world_pt = a + quat @ local_pt
            ring.append(bm.verts.new(world_pt))
        all_rings.append(ring)

    for r in range(len(all_rings) - 1):
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((all_rings[r][i], all_rings[r][ni], all_rings[r+1][ni], all_rings[r+1][i]))

    c_bot = bm.verts.new(a - axis * rx)
    for i in range(N):
        ni = (i + 1) % N
        bm.faces.new((c_bot, all_rings[0][ni], all_rings[0][i]))

    last = len(all_rings) - 1
    c_top = bm.verts.new(b + axis * rx)
    for i in range(N):
        ni = (i + 1) % N
        bm.faces.new((c_top, all_rings[last][i], all_rings[last][ni]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def add_sphere(name, center, radius, mat=MAT_SKIN, segs=32, rings=24):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=radius)
    for v in bm.verts:
        v.co += Vector(center)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def add_box(name, center, size, mat=MAT_DARK_BLACK):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = size
    for v in bm.verts:
        v.co.x = v.co.x * sx + center[0]
        v.co.y = v.co.y * sy + center[1]
        v.co.z = v.co.z * sz + center[2]
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_ribbon_strand(name, curve_points, half_width_start, half_width_end, thickness, mat, segs_along=14):
    bm = bmesh.new()
    N = len(curve_points)
    top_f, bot_f, top_b, bot_b = [], [], [], []

    for i in range(N):
        t = i / max(1, N - 1)
        pt = Vector(curve_points[i])
        if i < N - 1:
            tangent = (Vector(curve_points[i+1]) - pt).normalized()
        else:
            tangent = (pt - Vector(curve_points[i-1])).normalized()

        side = tangent.cross(Vector((0, 0, 1)))
        if side.length < 1e-4:
            side = tangent.cross(Vector((0, 1, 0))).normalized()
        else:
            side.normalize()
        nrm = side.cross(tangent).normalized()

        hw = half_width_start * (1.0 - t) + half_width_end * t
        th = thickness * (1.0 - t * 0.4)

        top_f.append(bm.verts.new(pt + side * hw + nrm * th))
        bot_f.append(bm.verts.new(pt - side * hw + nrm * th))
        top_b.append(bm.verts.new(pt + side * hw - nrm * th))
        bot_b.append(bm.verts.new(pt - side * hw - nrm * th))

    for i in range(N - 1):
        bm.faces.new((bot_f[i], bot_f[i+1], top_f[i+1], top_f[i]))
        bm.faces.new((top_b[i], top_b[i+1], bot_b[i+1], bot_b[i]))
        bm.faces.new((top_f[i], top_f[i+1], top_b[i+1], top_b[i]))
        bm.faces.new((bot_b[i], bot_b[i+1], bot_f[i+1], bot_f[i]))

    bm.faces.new((bot_b[0], bot_f[0], top_f[0], top_b[0]))
    last = N - 1
    bm.faces.new((bot_f[last], bot_b[last], top_b[last], top_f[last]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def head_surface_point(x, z, hz=1.70, surface_offset=0.0):
    dz = z - hz
    t_h = (dz / 0.355 + 1.0) * 0.5
    t_h = max(0.0, min(1.0, t_h))
    cheek = (math.sin(t_h * math.pi) ** 1.25) * 0.024
    chin_taper = 1.0 - max(0.0, (-dz - 0.07)) * 0.18
    rx = (0.388 * chin_taper) + cheek
    rz = 0.355
    ry = (0.365 * (0.94 + 0.06 * t_h)) + cheek * 0.65

    val = 1.0 - (x / rx) ** 2 - (dz / rz) ** 2
    y_rel = -ry * math.sqrt(max(0.0, val)) if val > 0.0 else -0.01
    y = y_rel

    nx = 2.0 * x / (rx * rx)
    ny = 2.0 * y_rel / (ry * ry)
    nz = 2.0 * dz / (rz * rz)
    nrm = Vector((nx, ny, nz)).normalized()
    if nrm.y > 0.0: nrm = -nrm

    return Vector((x, y, z)) + nrm * surface_offset, nrm

def create_face_disc(name, cx, cz, rx, rz, mat, hz=1.70, offset=0.0015, thickness=0.0010, tilt_rad=0.0, rings=6, segs=36):
    bm = bmesh.new()
    ca, sa = math.cos(tilt_rad), math.sin(tilt_rad)
    front_rings = []
    back_rings = []
    for r_i in range(1, rings + 1):
        frac = r_i / rings
        f_ring, b_ring = [], []
        for s_i in range(segs):
            ang = (2.0 * math.pi * s_i) / segs
            lx = math.cos(ang) * rx * frac
            lz = math.sin(ang) * rz * frac
            wx = cx + lx * ca - lz * sa
            wz = cz + lx * sa + lz * ca
            pt, nrm = head_surface_point(wx, wz, hz=hz, surface_offset=offset)
            dome = math.sqrt(max(0.0, 1.0 - frac * frac * 0.85)) * thickness
            f_ring.append(bm.verts.new(pt + nrm * dome))
            b_ring.append(bm.verts.new(pt - nrm * 0.0015))
        front_rings.append(f_ring)
        back_rings.append(b_ring)

    c_pt, c_nrm = head_surface_point(cx, cz, hz=hz, surface_offset=offset)
    c_front = bm.verts.new(c_pt + c_nrm * thickness)
    c_back  = bm.verts.new(c_pt - c_nrm * 0.0015)

    for s_i in range(segs):
        ni = (s_i + 1) % segs
        bm.faces.new((c_front, front_rings[0][s_i], front_rings[0][ni]))
        bm.faces.new((c_back, back_rings[0][ni], back_rings[0][s_i]))

    for r_i in range(rings - 1):
        for s_i in range(segs):
            ni = (s_i + 1) % segs
            bm.faces.new((front_rings[r_i][s_i], front_rings[r_i+1][s_i], front_rings[r_i+1][ni], front_rings[r_i][ni]))
            bm.faces.new((back_rings[r_i][ni], back_rings[r_i+1][ni], back_rings[r_i+1][s_i], back_rings[r_i][s_i]))

    outer = rings - 1
    for s_i in range(segs):
        ni = (s_i + 1) % segs
        bm.faces.new((front_rings[outer][s_i], back_rings[outer][s_i], back_rings[outer][ni], front_rings[outer][ni]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_face_curved_strip(name, pts_xz_halfw, mat, hz=1.70, offset=0.005, thickness=0.005):
    bm = bmesh.new()
    top_f, bot_f, top_b, bot_b = [], [], [], []
    for i, (px, pz, hw) in enumerate(pts_xz_halfw):
        if i < len(pts_xz_halfw) - 1:
            dx = pts_xz_halfw[i+1][0] - px
            dz = pts_xz_halfw[i+1][1] - pz
        else:
            dx = px - pts_xz_halfw[i-1][0]
            dz = pz - pts_xz_halfw[i-1][1]
        length = max(1e-6, math.hypot(dx, dz))
        nx, nz = -dz / length, dx / length

        pt_t, nrm_t = head_surface_point(px + nx * hw, pz + nz * hw, hz=hz, surface_offset=offset)
        pt_b, nrm_b = head_surface_point(px - nx * hw, pz - nz * hw, hz=hz, surface_offset=offset)

        top_f.append(bm.verts.new(pt_t + nrm_t * thickness))
        bot_f.append(bm.verts.new(pt_b + nrm_b * thickness))
        top_b.append(bm.verts.new(pt_t - nrm_t * 0.003))
        bot_b.append(bm.verts.new(pt_b - nrm_b * 0.003))

    for i in range(len(pts_xz_halfw) - 1):
        bm.faces.new((bot_f[i], bot_f[i+1], top_f[i+1], top_f[i]))
        bm.faces.new((top_b[i], top_b[i+1], bot_b[i+1], bot_b[i]))
        bm.faces.new((top_f[i], top_f[i+1], top_b[i+1], top_b[i]))
        bm.faces.new((bot_b[i], bot_b[i+1], bot_f[i+1], bot_f[i]))

    bm.faces.new((bot_b[0], bot_f[0], top_f[0], top_b[0]))
    last = len(pts_xz_halfw) - 1
    bm.faces.new((bot_f[last], bot_b[last], top_b[last], top_f[last]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def join_objects_into(target_obj, obj_list, final_name):
    bpy.ops.object.select_all(action='DESELECT')
    target_obj.select_set(True)
    for o in obj_list:
        if o != target_obj:
            o.select_set(True)
    bpy.context.view_layer.objects.active = target_obj
    bpy.ops.object.join()
    target_obj.name = final_name
    return target_obj

# ==============================================================================
# 3. BUILD CHIBI PUNK GIRL HEAD (Chibi_Base_Head)
# ==============================================================================
def build_punk_girl_head():
    head_parts = []
    HZ = 1.70

    # 3.1 Base Head Mesh
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=56, v_segments=40, radius=1.0)
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        t_h = (z + 1.0) * 0.5
        cheek = (math.sin(t_h * math.pi) ** 1.25) * 0.024
        chin_taper = 1.0 - max(0.0, (-z - 0.20)) * 0.18
        rx = (0.388 * chin_taper) + cheek
        ry = (0.365 * (0.94 + 0.06 * t_h)) + cheek * 0.65
        rz = 0.355
        y_chin_shift = -0.014 * max(0.0, -z - 0.2) * max(0.0, -y)
        v.co.x = x * rx
        v.co.y = y * ry + y_chin_shift
        v.co.z = z * rz + HZ

    m = bpy.data.meshes.new("Punk_Head_Base")
    bm.to_mesh(m)
    bm.free()
    base_head = bpy.data.objects.new("Punk_Head_Base", m)
    bpy.context.collection.objects.link(base_head)
    assign_single_material(base_head, MAT_SKIN)
    head_parts.append(base_head)

    # 3.2 Ears
    for side in (-1.0, 1.0):
        ear_rings = [
            (Vector((side * 0.370, -0.015, 1.69)), Vector((0.0, 0.025, 0.0)), Vector((0.0, 0.0, 0.040))),
            (Vector((side * 0.405, -0.012, 1.69)), Vector((0.0, 0.028, 0.0)), Vector((0.0, 0.0, 0.046))),
            (Vector((side * 0.435, -0.008, 1.70)), Vector((0.0, 0.016, 0.0)), Vector((0.0, 0.0, 0.028))),
        ]
        ear_obj = create_loft_object(f"Ear_{'R' if side > 0 else 'L'}", ear_rings, MAT_SKIN, cap_end=True)
        head_parts.append(ear_obj)

    # 3.3 Eyes (Sclera + Crimson Red Iris + Pupil + High Catchlights)
    eye_offset = 0.0018
    for side in (-1.0, 1.0):
        cx = side * 0.168
        cz = 1.695
        tilt = -side * 0.08

        # Sclera
        sclera = create_face_disc(f"Eye_Sclera_{side}", cx, cz, 0.076, 0.065, MAT_EYE_WHITE, hz=HZ, offset=eye_offset, thickness=0.0022, tilt_rad=tilt)
        head_parts.append(sclera)

        # Crimson Red Iris (Striking Anime Eye)
        iris = create_face_disc(f"Eye_Iris_{side}", cx - side * 0.005, cz - 0.002, 0.052, 0.052, MAT_EYE_RED, hz=HZ, offset=eye_offset + 0.0018, thickness=0.0025, tilt_rad=tilt)
        head_parts.append(iris)

        # Deep Ruby Outer Ring
        dark_ring = create_face_disc(f"Eye_DarkRing_{side}", cx - side * 0.005, cz - 0.002, 0.038, 0.038, MAT_EYE_RED_DARK, hz=HZ, offset=eye_offset + 0.0024, thickness=0.0025, tilt_rad=tilt)
        head_parts.append(dark_ring)

        # Black Pupil
        pupil = create_face_disc(f"Eye_Pupil_{side}", cx - side * 0.005, cz - 0.002, 0.022, 0.024, MAT_GLOSSY_BLACK, hz=HZ, offset=eye_offset + 0.0030, thickness=0.0025)
        head_parts.append(pupil)

        # Dual White Sparkle Catchlights
        c1 = create_face_disc(f"Catchlight1_{side}", cx - side * 0.022, cz + 0.024, 0.016, 0.016, MAT_EYE_WHITE, hz=HZ, offset=eye_offset + 0.0038, thickness=0.0020)
        c2 = create_face_disc(f"Catchlight2_{side}", cx + side * 0.020, cz - 0.020, 0.009, 0.009, MAT_EYE_WHITE, hz=HZ, offset=eye_offset + 0.0038, thickness=0.0020)
        head_parts.extend([c1, c2])

        # Winged Black Eyeliner with Cat-Eye flick
        lash_pts = [
            (cx - side * 0.078, cz + 0.032, 0.006),
            (cx - side * 0.038, cz + 0.068, 0.012),
            (cx + side * 0.020, cz + 0.064, 0.014),
            (cx + side * 0.065, cz + 0.045, 0.012),
            (cx + side * 0.098, cz + 0.060, 0.005),  # Wing tip
        ]
        eyeliner = create_face_curved_strip(f"Eyeliner_{side}", lash_pts, MAT_EYELINER_BLACK, hz=HZ, offset=eye_offset + 0.0035, thickness=0.0035)
        head_parts.append(eyeliner)

        # Red Eyeshadow accent line above eyeliner
        shadow_pts = [
            (cx - side * 0.035, cz + 0.076, 0.006),
            (cx + side * 0.020, cz + 0.072, 0.008),
            (cx + side * 0.078, cz + 0.060, 0.006),
        ]
        red_shadow = create_face_curved_strip(f"RedEyeshadow_{side}", shadow_pts, MAT_WARPAINT_RED, hz=HZ, offset=eye_offset + 0.0032, thickness=0.0025)
        head_parts.append(red_shadow)

        # 3.4 Signature Red Facial War-paint Marks (Pointed teardrop marks below each eye)
        # Directly matching the reference image under eyes!
        mark_pts = [
            (cx + side * 0.002, cz - 0.052, 0.012),  # Base under lower lid
            (cx + side * 0.003, cz - 0.082, 0.009),  # Mid taper
            (cx + side * 0.004, cz - 0.125, 0.002),  # Sharp tip pointing down
        ]
        warpaint = create_face_curved_strip(f"Warpaint_Mark_{side}", mark_pts, MAT_WARPAINT_RED, hz=HZ, offset=eye_offset + 0.0032, thickness=0.0025)
        head_parts.append(warpaint)

        # Anime Eyebrows
        brow_pts = [
            (cx - side * 0.065, cz + 0.100, 0.005),
            (cx + side * 0.000, cz + 0.118, 0.010),
            (cx + side * 0.068, cz + 0.095, 0.006),
        ]
        brow = create_face_curved_strip(f"Eyebrow_{side}", brow_pts, MAT_HAIR_BLONDE_DARK, hz=HZ, offset=eye_offset + 0.0035, thickness=0.0030)
        head_parts.append(brow)

    # 3.5 Playful Mischievous Smirk & Cute White Fang
    mouth_pts = [
        (-0.065, 1.545, 0.004),
        (-0.020, 1.530, 0.009),
        ( 0.035, 1.536, 0.011),
        ( 0.075, 1.558, 0.005),  # Raised playful smirk corner
    ]
    mouth = create_face_curved_strip("Mouth_Smirk", mouth_pts, MAT_MOUTH_DARK, hz=HZ, offset=0.0030, thickness=0.0035)
    head_parts.append(mouth)

    # Cute Fang (Single white sharp fang peeking out)
    fang = create_face_disc("Cute_Fang", 0.040, 1.545, 0.009, 0.012, MAT_FANG_WHITE, hz=HZ, offset=0.0045, thickness=0.0025)
    head_parts.append(fang)

    # 3.6 Hair: Pale Blonde Base Cap + Spiky Front Bangs + Side Tendrils
    # Hair Cap (Covering back, sides and crown)
    haircap_rings = [
        (Vector((0.0, -0.05, 1.94)), Vector((0.395, 0.0, 0.0)), Vector((0.0, 0.380, 0.0))),
        (Vector((0.0, -0.02, 1.82)), Vector((0.415, 0.0, 0.0)), Vector((0.0, 0.395, 0.0))),
        (Vector((0.0,  0.03, 1.68)), Vector((0.410, 0.0, 0.0)), Vector((0.0, 0.390, 0.0))),
        (Vector((0.0,  0.06, 1.54)), Vector((0.380, 0.0, 0.0)), Vector((0.0, 0.360, 0.0))),
        (Vector((0.0,  0.08, 1.44)), Vector((0.320, 0.0, 0.0)), Vector((0.0, 0.300, 0.0))),
    ]
    haircap = create_loft_object("Hair_Cap_Blonde", haircap_rings, MAT_HAIR_BLONDE, cap_start=True, cap_end=True)
    head_parts.append(haircap)

    # Spiky Front Bangs (Curved anime strands falling over forehead)
    bang_specs = [
        # Center split bang
        [(-0.02, -0.32, 1.90), (-0.03, -0.36, 1.80), (-0.02, -0.38, 1.70), (-0.01, -0.36, 1.62)],
        [( 0.04, -0.32, 1.90), ( 0.06, -0.36, 1.80), ( 0.05, -0.38, 1.69), ( 0.04, -0.36, 1.61)],
        # Left frontal spikes
        [(-0.08, -0.30, 1.90), (-0.12, -0.35, 1.79), (-0.14, -0.36, 1.68), (-0.16, -0.35, 1.60)],
        [(-0.16, -0.28, 1.90), (-0.22, -0.32, 1.78), (-0.25, -0.33, 1.66), (-0.26, -0.31, 1.56)],
        # Right frontal spikes
        [( 0.12, -0.30, 1.90), ( 0.16, -0.35, 1.79), ( 0.18, -0.36, 1.68), ( 0.20, -0.35, 1.59)],
        [( 0.20, -0.28, 1.90), ( 0.26, -0.32, 1.78), ( 0.29, -0.33, 1.66), ( 0.30, -0.31, 1.55)],
    ]
    for idx, pts in enumerate(bang_specs):
        strand = create_ribbon_strand(f"Bang_Strand_{idx}", pts, half_width_start=0.035, half_width_end=0.006, thickness=0.016, mat=MAT_HAIR_BLONDE)
        head_parts.append(strand)

    # Long Side Hair Tendrils framing cheeks
    for side in (-1.0, 1.0):
        side_pts = [
            (side * 0.32, -0.16, 1.86),
            (side * 0.36, -0.24, 1.75),
            (side * 0.38, -0.28, 1.64),
            (side * 0.35, -0.26, 1.52),
            (side * 0.31, -0.22, 1.42),
        ]
        tendril = create_ribbon_strand(f"SideTendril_{side}", side_pts, half_width_start=0.042, half_width_end=0.008, thickness=0.018, mat=MAT_HAIR_BLONDE)
        head_parts.append(tendril)

    # 3.7 Dual Braided Odango Buns with Hot-Pink Inner Puffs & Ribbons!
    # Signature hairstyle from the reference image: Buns on top-left and top-right
    for side in (-1.0, 1.0):
        bun_x = side * 0.36
        bun_y = 0.02
        bun_z = 1.94

        # Inner hot-pink puff bun
        pink_puff = add_sphere(f"PinkPuff_{side}", (bun_x, bun_y, bun_z), radius=0.125, mat=MAT_HAIR_PINK)
        head_parts.append(pink_puff)

        # Pink bun texture lobes (fluffy bubbly appearance)
        for ang_deg in (0, 72, 144, 216, 288):
            rad = math.radians(ang_deg)
            lobe_x = bun_x + math.cos(rad) * 0.08
            lobe_z = bun_z + math.sin(rad) * 0.08
            lobe = add_sphere(f"PinkLobe_{side}_{ang_deg}", (lobe_x, bun_y + 0.03, lobe_z), radius=0.058, mat=MAT_HAIR_PINK_DARK)
            head_parts.append(lobe)

        # Thick Blonde Braided Coils wrapping around the front and side of the pink puff
        for coil_idx in range(4):
            frac = coil_idx / 3.0
            angle_start = math.pi * 0.2 + frac * math.pi * 1.4
            c_pts = []
            for step in range(8):
                a_step = angle_start + (step / 7.0) * (math.pi * 0.7)
                cx = bun_x + math.cos(a_step) * (0.135 + (coil_idx % 2) * 0.012)
                cz = bun_z + math.sin(a_step) * (0.135 + (coil_idx % 2) * 0.012)
                cy = bun_y - 0.04 + (step / 7.0) * 0.08
                c_pts.append((cx, cy, cz))
            braid_strand = create_ribbon_strand(f"BraidStrand_{side}_{coil_idx}", c_pts, half_width_start=0.028, half_width_end=0.024, thickness=0.018, mat=MAT_HAIR_BLONDE)
            head_parts.append(braid_strand)

        # Black and Red Hanging Ribbons tied at bun base
        ribbon_black_pts = [
            (bun_x + side * 0.05, bun_y - 0.06, bun_z - 0.08),
            (bun_x + side * 0.08, bun_y - 0.08, bun_z - 0.22),
            (bun_x + side * 0.10, bun_y - 0.07, bun_z - 0.38),
        ]
        rib_black = create_ribbon_strand(f"RibbonBlack_{side}", ribbon_black_pts, half_width_start=0.022, half_width_end=0.014, thickness=0.006, mat=MAT_DARK_BLACK)
        head_parts.append(rib_black)

        ribbon_red_pts = [
            (bun_x - side * 0.02, bun_y - 0.07, bun_z - 0.08),
            (bun_x - side * 0.04, bun_y - 0.09, bun_z - 0.24),
            (bun_x - side * 0.03, bun_y - 0.08, bun_z - 0.36),
        ]
        rib_red = create_ribbon_strand(f"RibbonRed_{side}", ribbon_red_pts, half_width_start=0.020, half_width_end=0.012, thickness=0.006, mat=MAT_JACKET_RED)
        head_parts.append(rib_red)

    head_final = join_objects_into(base_head, head_parts, "Chibi_Base_Head")
    return head_final

# ==============================================================================
# 4. BUILD CHIBI PUNK GIRL BODY & OUTFIT (Chibi_Base_Mesh)
# ==============================================================================
def build_punk_girl_body():
    body_parts = []

    # 4.1 Neck & Striped Black/Red Choker
    neck_rings = [
        (Vector((0.0, 0.00, 1.24)), Vector((0.125, 0.0, 0.0)), Vector((0.0, 0.115, 0.0))),
        (Vector((0.0, 0.00, 1.38)), Vector((0.102, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
        (Vector((0.0, 0.00, 1.52)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.095, 0.0))),
    ]
    neck_obj = create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Thick Choker with Red and Black stripes
    choker_black = create_loft_object("Choker_Black", [
        (Vector((0.0, 0.00, 1.35)), Vector((0.110, 0.0, 0.0)), Vector((0.0, 0.105, 0.0))),
        (Vector((0.0, 0.00, 1.43)), Vector((0.108, 0.0, 0.0)), Vector((0.0, 0.103, 0.0))),
    ], MAT_DARK_BLACK)
    body_parts.append(choker_black)

    # Red stripe band on choker
    choker_red = create_loft_object("Choker_Red", [
        (Vector((0.0, 0.00, 1.38)), Vector((0.114, 0.0, 0.0)), Vector((0.0, 0.109, 0.0))),
        (Vector((0.0, 0.00, 1.41)), Vector((0.114, 0.0, 0.0)), Vector((0.0, 0.109, 0.0))),
    ], MAT_JACKET_RED)
    body_parts.append(choker_red)

    # 4.2 Torso: Black Cropped Tube Top / Sports Bra + Exposed Midriff / Abs
    # Black Cropped Top (z = 0.96..1.26)
    bra_rings = [
        (Vector((0.0, 0.010, 0.98)), Vector((0.215, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
        (Vector((0.0, 0.012, 1.10)), Vector((0.235, 0.0, 0.0)), Vector((0.0, 0.182, 0.0))),
        (Vector((0.0, 0.010, 1.26)), Vector((0.245, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
    ]
    bra_top = create_loft_object("Torso_BraTop", bra_rings, MAT_CHEST_TOP)
    body_parts.append(bra_top)

    # Buckle strap across the tube top
    top_buckle_strap = create_loft_object("Bra_Strap", [
        (Vector((0.0, 0.012, 1.02)), Vector((0.222, 0.0, 0.0)), Vector((0.0, 0.174, 0.0))),
        (Vector((0.0, 0.012, 1.06)), Vector((0.222, 0.0, 0.0)), Vector((0.0, 0.174, 0.0))),
    ], MAT_DARK_STRAP)
    body_parts.append(top_buckle_strap)
    top_buckle = add_box("Bra_Buckle", (0.0, -0.170, 1.04), (0.045, 0.012, 0.035), MAT_METAL_SILVER)
    body_parts.append(top_buckle)

    # Exposed Midriff / Toned Abs (z = 0.78..0.98)
    midriff_rings = [
        (Vector((0.0, 0.005, 0.78)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.150, 0.0))),
        (Vector((0.0, 0.008, 0.88)), Vector((0.198, 0.0, 0.0)), Vector((0.0, 0.145, 0.0))),
        (Vector((0.0, 0.010, 0.98)), Vector((0.210, 0.0, 0.0)), Vector((0.0, 0.158, 0.0))),
    ]
    midriff_obj = create_loft_object("Torso_Midriff_Skin", midriff_rings, MAT_SKIN)
    body_parts.append(midriff_obj)

    # 4.3 Cropped Crimson Biker Jacket (Popped open collar, silver zipper, pushed up sleeves)
    # Jacket body (outer shell over upper torso z = 1.00..1.32)
    jacket_body_rings = [
        (Vector((0.0, -0.015, 1.00)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.195, 0.0))),
        (Vector((0.0, -0.010, 1.15)), Vector((0.268, 0.0, 0.0)), Vector((0.0, 0.208, 0.0))),
        (Vector((0.0,  0.000, 1.30)), Vector((0.278, 0.0, 0.0)), Vector((0.0, 0.215, 0.0))),
    ]
    jacket_body = create_loft_object("Jacket_Body", jacket_body_rings, MAT_JACKET_RED)
    body_parts.append(jacket_body)

    # Open Popped Collar (Red outside, dark black inside)
    collar_rings = [
        (Vector((0.0, -0.010, 1.30)), Vector((0.265, 0.0, 0.0)), Vector((0.0, 0.205, 0.0))),
        (Vector((0.0, -0.015, 1.42)), Vector((0.285, 0.0, 0.0)), Vector((0.0, 0.225, 0.0))),
    ]
    collar_obj = create_loft_object("Jacket_PoppedCollar", collar_rings, MAT_JACKET_RED_BRT)
    body_parts.append(collar_obj)

    # Silver Zipper Teeth along both front opening lapels
    for side in (-1.0, 1.0):
        zipper_pts = [
            (side * 0.055, -0.212, 1.00),
            (side * 0.065, -0.220, 1.15),
            (side * 0.095, -0.230, 1.32),
        ]
        zipper_strip = create_ribbon_strand(f"ZipperStrip_{side}", zipper_pts, half_width_start=0.010, half_width_end=0.010, thickness=0.008, mat=MAT_ZIPPER_TEETH)
        body_parts.append(zipper_strip)

    # White Chevron Insignia Patch on Right Shoulder (Matching reference!)
    patch_base = add_box("RightArm_PatchBase", (0.285, 0.0, 1.20), (0.012, 0.065, 0.075), MAT_DARK_BLACK)
    body_parts.append(patch_base)
    patch_red = add_box("RightArm_PatchRed", (0.292, 0.0, 1.20), (0.008, 0.052, 0.060), MAT_JACKET_RED)
    body_parts.append(patch_red)
    patch_chevron = add_box("RightArm_Chevron", (0.296, 0.0, 1.21), (0.006, 0.032, 0.022), MAT_EYE_WHITE)
    body_parts.append(patch_chevron)

    # 4.4 Arms & Pushed-Up Red Sleeves + Black Gloves
    for side in (-1.0, 1.0):
        sign = side
        arm_x = sign * 0.28

        # Red Upper Arm Sleeve (z = 1.00..1.24)
        sleeve_top = add_capsule(f"SleeveUpper_{sign}", (sign * 0.25, 0.0, 1.24), (arm_x, 0.0, 1.04), rx=0.082, mat=MAT_JACKET_RED)
        body_parts.append(sleeve_top)

        # Gathered Cuff (Scrunched at elbow)
        cuff_ring = add_sphere(f"SleeveCuff_{sign}", (arm_x, 0.0, 1.02), radius=0.088, mat=MAT_DARK_BLACK)
        body_parts.append(cuff_ring)

        # Forearm Bare Skin (z = 0.86..1.02)
        forearm_skin = add_capsule(f"ForearmSkin_{sign}", (arm_x, 0.0, 1.02), (arm_x, 0.0, 0.86), rx=0.064, mat=MAT_SKIN)
        body_parts.append(forearm_skin)

        # Red Wristband / Accent
        wrist_red = add_capsule(f"WristAccent_{sign}", (arm_x, 0.0, 0.86), (arm_x, 0.0, 0.82), rx=0.070, mat=MAT_JACKET_RED)
        body_parts.append(wrist_red)

        # Black Fingerless Glove (z = 0.70..0.82)
        glove_hand = add_capsule(f"GloveHand_{sign}", (arm_x, 0.0, 0.82), (arm_x, 0.0, 0.70), rx=0.066, mat=MAT_GLOVE_BLACK)
        body_parts.append(glove_hand)

        # Knuckle studs
        for k in (-0.025, 0.0, 0.025):
            knuckle = add_sphere(f"Knuckle_{sign}_{k}", (arm_x + k, -0.065, 0.72), radius=0.012, mat=MAT_METAL_SILVER)
            body_parts.append(knuckle)

    # 4.5 Chunky Tactical Utility Belt, Pouch, & Hanging Keychains
    # Main belt (z = 0.70..0.78)
    belt_rings = [
        (Vector((0.0, 0.005, 0.70)), Vector((0.225, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
        (Vector((0.0, 0.005, 0.78)), Vector((0.225, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
    ]
    main_belt = create_loft_object("Utility_Belt", belt_rings, MAT_DARK_STRAP)
    body_parts.append(main_belt)

    # Big Silver Buckle at front
    buckle_box = add_box("Belt_Buckle", (0.0, -0.175, 0.74), (0.065, 0.016, 0.055), MAT_METAL_SILVER)
    body_parts.append(buckle_box)

    # Right Hip Tactical Cargo Pouch (Matching reference!)
    pouch = add_box("HipPouch_Right", (0.235, -0.05, 0.68), (0.065, 0.095, 0.110), MAT_DARK_BLACK)
    body_parts.append(pouch)
    pouch_flap = add_box("HipPouch_Flap", (0.240, -0.05, 0.73), (0.070, 0.100, 0.035), MAT_JACKET_RED)
    body_parts.append(pouch_flap)

    # Keychains hanging from belt ring:
    # Yellow Smiley Face Charm (🙂)
    smiley_charm = add_sphere("Smiley_Charm", (0.12, -0.170, 0.65), radius=0.032, mat=MAT_CHARM_YELLOW)
    body_parts.append(smiley_charm)
    # Smiley eyes and mouth details
    smiley_eye_l = add_sphere("Smiley_Eye_L", (0.112, -0.200, 0.656), radius=0.005, mat=MAT_GLOSSY_BLACK)
    smiley_eye_r = add_sphere("Smiley_Eye_R", (0.128, -0.200, 0.656), radius=0.005, mat=MAT_GLOSSY_BLACK)
    body_parts.extend([smiley_eye_l, smiley_eye_r])

    # Red Cross Charm (➕)
    cross_h = add_box("Cross_H", (0.16, -0.168, 0.64), (0.040, 0.008, 0.012), MAT_ACCENT_RED)
    cross_v = add_box("Cross_V", (0.16, -0.168, 0.64), (0.012, 0.008, 0.040), MAT_ACCENT_RED)
    body_parts.extend([cross_h, cross_v])

    # 4.6 Black Tactical Pants & Thigh Straps (z = 0.22..0.70)
    for side in (-1.0, 1.0):
        leg_x = side * 0.125

        # Pelvis to Knee (Thighs)
        thigh_rings = [
            (Vector((leg_x, 0.00, 0.70)), Vector((0.115, 0.0, 0.0)), Vector((0.0, 0.115, 0.0))),
            (Vector((leg_x, 0.00, 0.54)), Vector((0.102, 0.0, 0.0)), Vector((0.0, 0.102, 0.0))),
            (Vector((leg_x, 0.00, 0.38)), Vector((0.092, 0.0, 0.0)), Vector((0.0, 0.092, 0.0))),
        ]
        thigh = create_loft_object(f"Pants_Thigh_{side}", thigh_rings, MAT_PANTS_BLACK)
        body_parts.append(thigh)

        # Thigh Holster Straps & Buckles (Wrapped around thighs as in reference!)
        thigh_strap1 = create_loft_object(f"ThighStrap1_{side}", [
            (Vector((leg_x, 0.00, 0.60)), Vector((0.108, 0.0, 0.0)), Vector((0.0, 0.108, 0.0))),
            (Vector((leg_x, 0.00, 0.57)), Vector((0.108, 0.0, 0.0)), Vector((0.0, 0.108, 0.0))),
        ], MAT_DARK_STRAP)
        body_parts.append(thigh_strap1)
        strap_buckle1 = add_box(f"ThighBuckle1_{side}", (leg_x, -0.115, 0.585), (0.032, 0.012, 0.024), MAT_METAL_SILVER)
        body_parts.append(strap_buckle1)

        thigh_strap2 = create_loft_object(f"ThighStrap2_{side}", [
            (Vector((leg_x, 0.00, 0.48)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
            (Vector((leg_x, 0.00, 0.45)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
        ], MAT_DARK_STRAP)
        body_parts.append(thigh_strap2)
        strap_buckle2 = add_box(f"ThighBuckle2_{side}", (leg_x, -0.105, 0.465), (0.032, 0.012, 0.024), MAT_METAL_SILVER)
        body_parts.append(strap_buckle2)

        # Calves
        calf_rings = [
            (Vector((leg_x, 0.00, 0.38)), Vector((0.092, 0.0, 0.0)), Vector((0.0, 0.092, 0.0))),
            (Vector((leg_x, 0.00, 0.22)), Vector((0.082, 0.0, 0.0)), Vector((0.0, 0.082, 0.0))),
        ]
        calf = create_loft_object(f"Pants_Calf_{side}", calf_rings, MAT_PANTS_BLACK)
        body_parts.append(calf)

        # 4.7 Chunky Combat Boots (Lug soles, toe cap, red lace accents)
        boot_base = add_capsule(f"Boot_Upper_{side}", (leg_x, 0.00, 0.24), (leg_x, -0.04, 0.07), rx=0.092, ry=0.115, mat=MAT_DARK_BLACK)
        body_parts.append(boot_base)

        sole = add_box(f"Boot_LugSole_{side}", (leg_x, -0.03, 0.035), (0.190, 0.260, 0.045), MAT_DARK_STRAP)
        body_parts.append(sole)

        # Red lace accents
        boot_lace = add_box(f"Boot_RedLace_{side}", (leg_x, -0.10, 0.12), (0.075, 0.018, 0.045), MAT_JACKET_RED)
        body_parts.append(boot_lace)

    # 4.8 Signature Spiked Apocalyptic Baseball Bat (Held in Left Hand)
    # Tapered wooden bat with athletic tape wrap and nails!
    bat_start = Vector((-0.28, -0.10, 0.40))
    bat_end   = Vector((-0.35, -0.18, 1.25))
    bat_axis  = (bat_end - bat_start).normalized()

    # Handle
    bat_handle = add_capsule("Bat_Handle", bat_start, bat_start + bat_axis * 0.35, rx=0.028, mat=MAT_BAT_WOOD)
    body_parts.append(bat_handle)

    # Tape Wrap
    bat_tape = add_capsule("Bat_TapeWrap", bat_start + bat_axis * 0.12, bat_start + bat_axis * 0.40, rx=0.032, mat=MAT_BAT_TAPE)
    body_parts.append(bat_tape)

    # Tapered Barrel
    bat_barrel = add_capsule("Bat_Barrel", bat_start + bat_axis * 0.35, bat_end, rx=0.052, ry=0.052, mat=MAT_BAT_WOOD)
    body_parts.append(bat_barrel)

    # Rusted Steel Nails / Spikes protruding through top barrel
    nail_offsets = [0.65, 0.72, 0.78, 0.82]
    for n_idx, dist in enumerate(nail_offsets):
        n_pos = bat_start + bat_axis * dist
        cross_dir = bat_axis.cross(Vector((1, 0, 0))).normalized()
        rot_ang = n_idx * 1.3
        dir_spike = (cross_dir * math.cos(rot_ang) + bat_axis.cross(cross_dir) * math.sin(rot_ang)).normalized()
        nail = add_capsule(f"Bat_Nail_{n_idx}", n_pos - dir_spike * 0.065, n_pos + dir_spike * 0.065, rx=0.007, mat=MAT_BAT_NAIL)
        body_parts.append(nail)

    body_final = join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")
    return body_final

# ==============================================================================
# 5. GENERATE HEAD & BODY MESHES
# ==============================================================================
head_obj = build_punk_girl_head()
body_obj = build_punk_girl_body()

# ==============================================================================
# 6. CREATE 20-BONE CHIBI ARMATURE (Chibi_Base_Rig)
# ==============================================================================
arm_data = bpy.data.armatures.new("Chibi_Base_Rig")
rig_obj = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
bpy.context.collection.objects.link(rig_obj)
bpy.context.view_layer.objects.active = rig_obj
bpy.ops.object.mode_set(mode='EDIT')

bone_defs = [
    ("Root",          (0.0, 0.0, 0.02),    (0.0, 0.0, 0.66),    None,            (0, -1, 0)),
    ("Hips",          (0.0, 0.0, 0.66),    (0.0, 0.0, 0.86),    "Root",          (0, -1, 0)),
    ("Spine",         (0.0, 0.0, 0.86),    (0.0, 0.0, 1.06),    "Hips",          (0, -1, 0)),
    ("Chest",         (0.0, 0.0, 1.06),    (0.0, 0.0, 1.28),    "Spine",         (0, -1, 0)),
    ("Neck",          (0.0, 0.0, 1.28),    (0.0, 0.0, 1.48),    "Chest",         (0, -1, 0)),
    ("Head",          (0.0, 0.0, 1.48),    (0.0, 0.0, 2.08),    "Neck",          (0, -1, 0)),

    ("LeftShoulder",  (0.16, 0.0, 1.23),   (0.27, 0.0, 1.23),   "Chest",         (0, 0, 1)),
    ("LeftUpperArm",  (0.27, 0.0, 1.23),   (0.28, 0.0, 1.02),   "LeftShoulder",  (0, 1, 0)),
    ("LeftLowerArm",  (0.28, 0.0, 1.02),   (0.28, 0.0, 0.84),   "LeftUpperArm",  (0, 1, 0)),
    ("LeftHand",      (0.28, 0.0, 0.84),   (0.28, 0.0, 0.70),   "LeftLowerArm",  (0, 1, 0)),

    ("RightShoulder", (-0.16, 0.0, 1.23),  (-0.27, 0.0, 1.23),  "Chest",         (0, 0, 1)),
    ("RightUpperArm", (-0.27, 0.0, 1.23),  (-0.28, 0.0, 1.02),  "RightShoulder", (0, 1, 0)),
    ("RightLowerArm", (-0.28, 0.0, 1.02),  (-0.28, 0.0, 0.84),  "RightUpperArm", (0, 1, 0)),
    ("RightHand",     (-0.28, 0.0, 0.84),  (-0.28, 0.0, 0.70),  "RightLowerArm", (0, 1, 0)),

    ("LeftUpperLeg",  (0.125, 0.0, 0.68),  (0.125, 0.0, 0.38),  "Hips",          (0, -1, 0)),
    ("LeftLowerLeg",  (0.125, 0.0, 0.38),  (0.125, 0.0, 0.12),  "LeftUpperLeg",  (0, -1, 0)),
    ("LeftFoot",      (0.125, 0.0, 0.12),  (0.125, -0.16, 0.02),"LeftLowerLeg",  (0, 0, 1)),

    ("RightUpperLeg", (-0.125, 0.0, 0.68), (-0.125, 0.0, 0.38), "Hips",          (0, -1, 0)),
    ("RightLowerLeg", (-0.125, 0.0, 0.38), (-0.125, 0.0, 0.12), "RightUpperLeg", (0, -1, 0)),
    ("RightFoot",     (-0.125, 0.0, 0.12), (-0.125, -0.16, 0.02),"RightLowerLeg",(0, 0, 1)),
]

for name, head, tail, parent, roll_vec in bone_defs:
    b = arm_data.edit_bones.new(name)
    b.head = Vector(head)
    b.tail = Vector(tail)
    if parent:
        b.parent = arm_data.edit_bones[parent]
    b.align_roll(Vector(roll_vec))

bpy.ops.object.mode_set(mode='OBJECT')

# ==============================================================================
# 7. RIGGING & SKIN WEIGHT PAINTING
# ==============================================================================
for obj in (head_obj, body_obj):
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = rig_obj
    mod.use_vertex_groups = True
    obj.parent = rig_obj

# Head weights: 100% to Head bone
grp = head_obj.vertex_groups.new(name="Head")
grp.add(list(range(len(head_obj.data.vertices))), 1.0, 'REPLACE')

# Body weights: Anatomical soft-bounding partition
bone_names = [b[0] for b in bone_defs if b[0] != "Head"]
body_groups = {name: body_obj.vertex_groups.new(name=name) for name in bone_names}

for i, v in enumerate(body_obj.data.vertices):
    x, y, z = v.co.x, v.co.y, v.co.z
    weights = {}

    if abs(x) > 0.18:
        arm_side = "Left" if x > 0 else "Right"
        if z > 1.15:
            weights[f"{arm_side}Shoulder"] = 0.85
            weights["Chest"] = 0.15
        elif z > 0.98:
            weights[f"{arm_side}UpperArm"] = 0.90
            weights[f"{arm_side}LowerArm"] = 0.10
        elif z > 0.80:
            weights[f"{arm_side}LowerArm"] = 0.90
            weights[f"{arm_side}UpperArm"] = 0.10
        else:
            weights[f"{arm_side}Hand"] = 1.0
    elif z < 0.68:
        leg_side = "Left" if x >= 0 else "Right"
        if z > 0.38:
            w = (z - 0.38) / 0.30
            weights[f"{leg_side}UpperLeg"] = 0.85 + 0.15 * w
            weights["Hips"] = 0.15 * w
        elif z > 0.14:
            weights[f"{leg_side}LowerLeg"] = 0.90
            weights[f"{leg_side}UpperLeg"] = 0.10
        else:
            weights[f"{leg_side}Foot"] = 1.0
    else:
        if z < 0.82:
            weights["Hips"] = 0.80
            weights["Spine"] = 0.20
        elif z < 1.05:
            weights["Spine"] = 0.70
            weights["Chest"] = 0.30
        elif z < 1.26:
            weights["Chest"] = 0.80
            weights["Neck"] = 0.20
        else:
            weights["Neck"] = 0.90
            weights["Chest"] = 0.10

    total_w = sum(weights.values())
    if total_w > 0:
        for b_name, w in weights.items():
            if b_name in body_groups:
                body_groups[b_name].add([i], w / total_w, 'REPLACE')
    else:
        body_groups["Chest"].add([i], 1.0, 'REPLACE')

# ==============================================================================
# 8. ANIMATION ENGINE (10 ACTIONS BAKED AT 24 FPS)
# ==============================================================================
bpy.context.view_layer.objects.active = rig_obj
bpy.ops.object.mode_set(mode='POSE')
for pbone in rig_obj.pose.bones:
    pbone.rotation_mode = 'XYZ'

def begin_frame_pose(f):
    scene.frame_set(f)
    for pbone in rig_obj.pose.bones:
        pbone.location = (0.0, 0.0, 0.0)
        pbone.rotation_euler = (0.0, 0.0, 0.0)

def set_bone_euler_deg(bone_name, pitch, roll, yaw):
    pbone = rig_obj.pose.bones[bone_name]
    pbone.rotation_euler = (math.radians(pitch), math.radians(roll), math.radians(yaw))

def keyframe_all_bones(f):
    for pbone in rig_obj.pose.bones:
        pbone.keyframe_insert(data_path="location", frame=f)
        pbone.keyframe_insert(data_path="rotation_euler", frame=f)

def create_action(name):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig_obj.animation_data_create()
    rig_obj.animation_data.action = act
    return act

# 8.1 Idle (49 Frames, Loop)
act_idle = create_action("Idle")
for f in range(1, 50):
    begin_frame_pose(f)
    u = (f - 1) / 48.0
    s = math.sin(u * math.pi * 2.0)
    rig_obj.pose.bones["Hips"].location = (0.0, 0.0, 0.012 * s)
    set_bone_euler_deg("Spine", 2.0 * s, 0.0, 0.0)
    set_bone_euler_deg("Chest", 3.0 * s, 0.0, 0.0)
    set_bone_euler_deg("Head", -2.0 * s, 0.0, 1.5 * math.cos(u * math.pi * 2.0))
    set_bone_euler_deg("LeftUpperArm", -4.0 + 2.0 * s, 0.0, 6.0)
    set_bone_euler_deg("RightUpperArm", -4.0 + 2.0 * s, 0.0, -6.0)
    keyframe_all_bones(f)

# 8.2 Walk (25 Frames, Loop)
act_walk = create_action("Walk")
for f in range(1, 26):
    begin_frame_pose(f)
    u = (f - 1) / 24.0
    sw = math.sin(u * math.pi * 2.0)
    cw = math.cos(u * math.pi * 2.0)
    rig_obj.pose.bones["Hips"].location = (0.015 * cw, 0.0, 0.025 * abs(sw))
    set_bone_euler_deg("LeftUpperLeg", 28.0 * sw, 0.0, 0.0)
    set_bone_euler_deg("RightUpperLeg", -28.0 * sw, 0.0, 0.0)
    set_bone_euler_deg("LeftLowerLeg", max(0.0, -35.0 * sw), 0.0, 0.0)
    set_bone_euler_deg("RightLowerLeg", max(0.0, 35.0 * sw), 0.0, 0.0)
    set_bone_euler_deg("LeftUpperArm", -30.0 * sw, 0.0, 8.0)
    set_bone_euler_deg("RightUpperArm", 30.0 * sw, 0.0, -8.0)
    set_bone_euler_deg("Spine", 4.0, 0.0, -5.0 * sw)
    keyframe_all_bones(f)

# 8.3 Run (17 Frames, Loop)
act_run = create_action("Run")
for f in range(1, 18):
    begin_frame_pose(f)
    u = (f - 1) / 16.0
    sw = math.sin(u * math.pi * 2.0)
    cw = math.cos(u * math.pi * 2.0)
    rig_obj.pose.bones["Hips"].location = (0.02 * cw, -0.04, 0.045 * abs(sw))
    set_bone_euler_deg("Spine", 14.0, 0.0, -8.0 * sw)
    set_bone_euler_deg("LeftUpperLeg", 52.0 * sw, 0.0, 0.0)
    set_bone_euler_deg("RightUpperLeg", -52.0 * sw, 0.0, 0.0)
    set_bone_euler_deg("LeftLowerLeg", max(0.0, -65.0 * sw), 0.0, 0.0)
    set_bone_euler_deg("RightLowerLeg", max(0.0, 65.0 * sw), 0.0, 0.0)
    set_bone_euler_deg("LeftUpperArm", -55.0 * sw, 0.0, 14.0)
    set_bone_euler_deg("RightUpperArm", 55.0 * sw, 0.0, -14.0)
    keyframe_all_bones(f)

# 8.4 GunHold / BatHold (25 Frames, Loop)
act_hold = create_action("GunHold")
for f in range(1, 26):
    begin_frame_pose(f)
    u = (f - 1) / 24.0
    s = math.sin(u * math.pi * 2.0)
    rig_obj.pose.bones["Hips"].location = (0.0, 0.0, 0.010 * s)
    set_bone_euler_deg("Spine", 6.0, 0.0, -12.0)
    set_bone_euler_deg("Chest", 8.0, 0.0, -15.0)
    set_bone_euler_deg("LeftUpperArm", -42.0, 0.0, 22.0)
    set_bone_euler_deg("LeftLowerArm", 35.0, 0.0, 15.0)
    set_bone_euler_deg("RightUpperArm", -38.0, 0.0, -18.0)
    set_bone_euler_deg("RightLowerArm", 40.0, 0.0, -12.0)
    keyframe_all_bones(f)

# 8.5 Shoot (15 Frames)
act_shoot = create_action("Shoot")
for f in range(1, 16):
    begin_frame_pose(f)
    recoil = math.exp(-((f - 3) / 2.5) ** 2) if f >= 2 else 0.0
    set_bone_euler_deg("Spine", 6.0 - 8.0 * recoil, 0.0, -12.0)
    set_bone_euler_deg("Chest", 8.0 - 12.0 * recoil, 0.0, -15.0)
    set_bone_euler_deg("LeftUpperArm", -42.0 - 18.0 * recoil, 0.0, 22.0)
    set_bone_euler_deg("RightUpperArm", -38.0 - 18.0 * recoil, 0.0, -18.0)
    keyframe_all_bones(f)

# 8.6 Slash1 (Bat Swing Right to Left, 19 Frames)
act_slash1 = create_action("Slash1")
for f in range(1, 20):
    begin_frame_pose(f)
    t = (f - 1) / 18.0
    if t < 0.35:
        w = t / 0.35
        rot_y = -35.0 * w
        swing = -25.0 * w
    else:
        w = (t - 0.35) / 0.65
        rot_y = -35.0 + 85.0 * (w ** 0.5)
        swing = -25.0 + 75.0 * (w ** 0.5)
    set_bone_euler_deg("Spine", 4.0, 0.0, rot_y)
    set_bone_euler_deg("LeftUpperArm", -30.0 + swing * 0.4, 0.0, 25.0 + swing)
    set_bone_euler_deg("RightUpperArm", -20.0, 0.0, -15.0)
    keyframe_all_bones(f)

# 8.7 Slash2 (Bat Swing Left to Right, 19 Frames)
act_slash2 = create_action("Slash2")
for f in range(1, 20):
    begin_frame_pose(f)
    t = (f - 1) / 18.0
    if t < 0.35:
        w = t / 0.35
        rot_y = 45.0 * w
        swing = 45.0 * w
    else:
        w = (t - 0.35) / 0.65
        rot_y = 45.0 - 95.0 * (w ** 0.5)
        swing = 45.0 - 90.0 * (w ** 0.5)
    set_bone_euler_deg("Spine", 4.0, 0.0, rot_y)
    set_bone_euler_deg("LeftUpperArm", -30.0 + swing * 0.4, 0.0, 25.0 + swing)
    keyframe_all_bones(f)

# 8.8 Slash3 (Heavy Overhead Slam, 23 Frames)
act_slash3 = create_action("Slash3")
for f in range(1, 24):
    begin_frame_pose(f)
    t = (f - 1) / 22.0
    if t < 0.45:
        w = t / 0.45
        pitch = -25.0 * w
        arm_up = -95.0 * w
    else:
        w = (t - 0.45) / 0.55
        pitch = -25.0 + 55.0 * (w ** 1.8)
        arm_up = -95.0 + 130.0 * (w ** 1.8)
    set_bone_euler_deg("Spine", pitch, 0.0, 0.0)
    set_bone_euler_deg("Chest", pitch * 0.8, 0.0, 0.0)
    set_bone_euler_deg("LeftUpperArm", arm_up, 0.0, 15.0)
    set_bone_euler_deg("RightUpperArm", arm_up, 0.0, -15.0)
    keyframe_all_bones(f)

# 8.9 DashRoll (Forward Roll, 21 Frames)
act_dash = create_action("DashRoll")
for f in range(1, 22):
    begin_frame_pose(f)
    t = (f - 1) / 20.0
    pitch = 360.0 * t
    elev = 0.28 * math.sin(t * math.pi)
    rig_obj.pose.bones["Hips"].location = (0.0, -0.15 * math.sin(t * math.pi * 2.0), elev)
    set_bone_euler_deg("Hips", pitch, 0.0, 0.0)
    set_bone_euler_deg("Spine", 35.0, 0.0, 0.0)
    set_bone_euler_deg("Head", 25.0, 0.0, 0.0)
    set_bone_euler_deg("LeftUpperLeg", -65.0, 0.0, 0.0)
    set_bone_euler_deg("RightUpperLeg", -65.0, 0.0, 0.0)
    keyframe_all_bones(f)

# 8.10 Death (1.5s Dramatic Collapse, 37 Frames)
act_death = create_action("Death")
for f in range(1, 38):
    begin_frame_pose(f)
    t = (f - 1) / 36.0
    if f <= 8:
        u = (f - 1) / 7.0
        rig_obj.pose.bones["Hips"].location = (0.0, 0.08 * u, 0.0)
        set_bone_euler_deg("Hips", -15.0 * u, 0.0, 0.0)
        set_bone_euler_deg("Spine", -22.0 * u, 0.0, 0.0)
        set_bone_euler_deg("Head", -30.0 * u, 0.0, 0.0)
    elif f <= 20:
        u = (f - 8) / 12.0
        rig_obj.pose.bones["Hips"].location = (0.0, 0.08 - 0.15 * u, -0.38 * u)
        set_bone_euler_deg("Hips", -15.0 + 35.0 * u, 0.0, 0.0)
        set_bone_euler_deg("LeftUpperLeg", 85.0 * u, 0.0, 0.0)
        set_bone_euler_deg("RightUpperLeg", 85.0 * u, 0.0, 0.0)
        set_bone_euler_deg("LeftLowerLeg", -110.0 * u, 0.0, 0.0)
        set_bone_euler_deg("RightLowerLeg", -110.0 * u, 0.0, 0.0)
    else:
        u = (f - 20) / 16.0
        rig_obj.pose.bones["Hips"].location = (0.0, -0.07 - 0.20 * u, -0.58)
        set_bone_euler_deg("Hips", 80.0 * u, 0.0, 25.0 * u)
        set_bone_euler_deg("Spine", 15.0, 0.0, 0.0)
        set_bone_euler_deg("LeftUpperArm", -25.0, 0.0, 45.0)
        set_bone_euler_deg("RightUpperArm", -25.0, 0.0, -45.0)
    keyframe_all_bones(f)

# Set active back to Idle
rig_obj.animation_data.action = act_idle
scene.frame_set(1)
bpy.ops.object.mode_set(mode='OBJECT')

# ==============================================================================
# 9. EXPORT FBX & BLEND + RENDER PREVIEW
# ==============================================================================
out_dir = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels"
os.makedirs(out_dir, exist_ok=True)
blend_path   = os.path.join(out_dir, "Chibi_PunkGirl_Source.blend")
fbx_path     = os.path.join(out_dir, "Chibi_PunkGirl_Rigged.fbx")
preview_path = os.path.join(out_dir, "Chibi_PunkGirl_Preview.png")

bpy.ops.object.select_all(action='DESELECT')
rig_obj.select_set(True)
head_obj.select_set(True)
body_obj.select_set(True)
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
    path_mode='AUTO'
)

bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print("SUCCESSFULLY EXPORTED CHIBI PUNK GIRL FBX AND BLEND:", fbx_path)

# Render Studio Preview
world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg_node = world.node_tree.nodes.get("Background")
if bg_node:
    bg_node.inputs[0].default_value = (0.16, 0.17, 0.20, 1.0)
    bg_node.inputs[1].default_value = 1.25

cam_data = bpy.data.cameras.new("PreviewCam")
cam_data.lens = 55
cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
bpy.context.collection.objects.link(cam_obj)
cam_obj.location = Vector((-0.65, -4.15, 1.25))
direction = Vector((0.0, 0.0, 1.15)) - cam_obj.location
cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam_obj

key_light_data = bpy.data.lights.new(name="KeyLight", type='SUN')
key_light_data.energy = 3.6
key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
bpy.context.collection.objects.link(key_light)
key_light.rotation_euler = Euler((math.radians(50), math.radians(-16), math.radians(-20)), 'XYZ')

fill_light_data = bpy.data.lights.new(name="FillLight", type='SUN')
fill_light_data.energy = 2.0
fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
bpy.context.collection.objects.link(fill_light)
fill_light.rotation_euler = Euler((math.radians(65), math.radians(26), math.radians(30)), 'XYZ')

scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)
print("SUCCESSFULLY RENDERED CHIBI PUNK GIRL PREVIEW:", preview_path)

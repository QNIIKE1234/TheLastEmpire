# Blonde Brawler (spiky golden-blonde hair, navy headband, black rectangular sunglasses,
# right-cheek white bandage, wide confident toothy grin, bright orange high-collar jacket
# with white trim & silver sleeve patches, navy inner vest, black tactical chest rig with
# silver H-buckle & black/orange chest pouches, black/orange fingerless brawling gloves).
# Same 18-bone Chibi_Base_Rig + bone names/rest pose as Create_Tactical_Girl.py & Create_Curly_Glasses_Girl.py
# so it plays every clip in Chibi_Player_Controller seamlessly.
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

# ==============================================================================
# 2. GEOMETRY HELPERS (HIGH-SUBDIVISION TOY-CHIBI PARTS)
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

    for r_idx in range(len(vert_rings) - 1):
        r0 = vert_rings[r_idx]
        r1 = vert_rings[r_idx + 1]
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))

    if cap_start:
        r0 = vert_rings[0]
        c0 = bm.verts.new(sum((v.co for v in r0), Vector((0, 0, 0))) / N)
        for i in range(N):
            bm.faces.new((r0[(i + 1) % N], r0[i], c0))

    if cap_end:
        rl = vert_rings[-1]
        cl = bm.verts.new(sum((v.co for v in rl), Vector((0, 0, 0))) / N)
        for i in range(N):
            bm.faces.new((rl[i], rl[(i + 1) % N], cl))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_rounded_box(name, center, size, mat, rot_euler=(0.0, 0.0, 0.0), bevel_width=0.015, segments=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    rot_mat = Euler(rot_euler, 'XYZ').to_matrix()
    for v in bm.verts:
        local = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        v.co = Vector(center) + (rot_mat @ local)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=bevel_width, segments=segments, profile=0.55)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_ellipsoid(name, center, radii, mat, rot_euler=(0.0, 0.0, 0.0), u_seg=28, v_seg=20):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u_seg, v_segments=v_seg, radius=1.0)
    rot_mat = Euler(rot_euler, 'XYZ').to_matrix()
    for v in bm.verts:
        local = Vector((v.co.x * radii[0], v.co.y * radii[1], v.co.z * radii[2]))
        v.co = Vector(center) + (rot_mat @ local)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_ribbon_hair_strand(name, points_widths, mat, segments=22, substeps=3):
    """
    Creates a smooth, flat-arched anime hair lock (crescent/lens cross-section)
    by subdividing control points with smooth Catmull-Rom interpolation.
    """
    dense_pts = []
    K = len(points_widths)
    for i in range(K - 1):
        p0, w0, t0, r0 = points_widths[max(0, i - 1)]
        p1, w1, t1, r1 = points_widths[i]
        p2, w2, t2, r2 = points_widths[i + 1]
        p3, w3, t3, r3 = points_widths[min(K - 1, i + 2)]
        steps = substeps if i < K - 2 else substeps + 1
        for s in range(steps):
            u = s / substeps
            u2 = u * u
            u3 = u2 * u
            pt = 0.5 * (
                (2.0 * p1)
                + (-p0 + p2) * u
                + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * u2
                + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * u3
            )
            wu = w1 + (w2 - w1) * u
            tv = t1 + (t2 - t1) * u
            rr = r1 + (r2 - r1) * u
            dense_pts.append((pt, wu, tv, rr))

    bm = bmesh.new()
    N = segments
    vert_rings = []
    for idx, (pt, wu, tv, roll_rad) in enumerate(dense_pts):
        if idx < len(dense_pts) - 1:
            tangent = (dense_pts[idx + 1][0] - pt).normalized()
        else:
            tangent = (pt - dense_pts[idx - 1][0]).normalized()
        radial = (pt - Vector((0.0, 0.02, 1.72)))
        radial = (radial - tangent * radial.dot(tangent)).normalized()
        side_vec = tangent.cross(radial).normalized()
        ca, sa = math.cos(roll_rad), math.sin(roll_rad)
        u_dir = side_vec * ca + radial * sa
        v_dir = -side_vec * sa + radial * ca

        ring = []
        for i in range(N):
            ang = (2.0 * math.pi * i) / N
            cu = math.cos(ang)
            sv = math.sin(ang)
            arch = (1.0 - cu * cu) * 0.35 * tv
            p = pt + u_dir * (cu * wu) + v_dir * (sv * tv + arch)
            ring.append(bm.verts.new(p))
        vert_rings.append(ring)

    for r_idx in range(len(vert_rings) - 1):
        r0 = vert_rings[r_idx]
        r1 = vert_rings[r_idx + 1]
        for i in range(N):
            ni = (i + 1) % N
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))

    r0 = vert_rings[0]
    c0 = bm.verts.new(sum((v.co for v in r0), Vector((0, 0, 0))) / N)
    for i in range(N):
        bm.faces.new((r0[(i + 1) % N], r0[i], c0))

    rl = vert_rings[-1]
    cl = bm.verts.new(sum((v.co for v in rl), Vector((0, 0, 0))) / N)
    for i in range(N):
        bm.faces.new((rl[i], rl[(i + 1) % N], cl))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def head_surface_point(x, z, hz=1.70, surface_offset=0.002):
    """
    Computes the exact front (-Y) surface point and outward normal on the Chibi head
    at horizontal (x) and vertical (z) so facial features conform 100% flush to the face!
    """
    zn = max(-0.98, min(0.98, (z - hz) / 0.355))
    t_h = (zn + 1.0) * 0.5
    cheek = (math.sin(t_h * math.pi) ** 1.25) * 0.024
    chin_taper = 1.0 - max(0.0, (-zn - 0.20)) * 0.18
    rx = (0.388 * chin_taper) + cheek
    ry = (0.365 * (0.94 + 0.06 * t_h)) + cheek * 0.65

    xn = max(-0.96, min(0.96, x / rx))
    yn_sq = max(0.02, 1.0 - xn * xn - zn * zn)
    yn = -math.sqrt(yn_sq)  # Front face is -Y
    y_chin_shift = -0.014 * max(0.0, -zn - 0.2) * (-yn)
    y = yn * ry + y_chin_shift
    nrm = Vector((xn / rx, yn / ry, zn / 0.355)).normalized()
    return Vector((x, y, z)) + nrm * surface_offset, nrm

def create_face_disc(name, cx, cz, rx, rz, mat, hz=1.70, offset=0.0015, thickness=0.0010, tilt_rad=0.0, rings=6, segs=40):
    """Creates a smooth, flush-contoured oval patch hugging the head curvature."""
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

def create_face_rect(name, cx, cz, hw, hh, mat, hz=1.70, offset=0.0020, thickness=0.0014, tilt_rad=0.0, nx_steps=8, nz_steps=6):
    """Creates a flush-contoured rectangular patch (e.g. cheek bandage / teeth / visor) hugging the head curvature."""
    bm = bmesh.new()
    ca, sa = math.cos(tilt_rad), math.sin(tilt_rad)
    f_grid = []
    b_grid = []
    for iz in range(nz_steps + 1):
        vz = -hh + (2.0 * hh * iz) / nz_steps
        f_row, b_row = [], []
        for ix in range(nx_steps + 1):
            vx = -hw + (2.0 * hw * ix) / nx_steps
            wx = cx + vx * ca - vz * sa
            wz = cz + vx * sa + vz * ca
            pt, nrm = head_surface_point(wx, wz, hz=hz, surface_offset=offset)
            f_row.append(bm.verts.new(pt + nrm * thickness))
            b_row.append(bm.verts.new(pt - nrm * 0.0015))
        f_grid.append(f_row)
        b_grid.append(b_row)

    for iz in range(nz_steps):
        for ix in range(nx_steps):
            bm.faces.new((f_grid[iz][ix], f_grid[iz][ix+1], f_grid[iz+1][ix+1], f_grid[iz+1][ix]))
            bm.faces.new((b_grid[iz][ix+1], b_grid[iz][ix], b_grid[iz+1][ix], b_grid[iz+1][ix+1]))

    # Perimeter walls
    for ix in range(nx_steps):
        bm.faces.new((b_grid[0][ix], f_grid[0][ix], f_grid[0][ix+1], b_grid[0][ix+1]))
        bm.faces.new((f_grid[nz_steps][ix], b_grid[nz_steps][ix], b_grid[nz_steps][ix+1], f_grid[nz_steps][ix+1]))
    for iz in range(nz_steps):
        bm.faces.new((f_grid[iz][0], b_grid[iz][0], b_grid[iz+1][0], f_grid[iz+1][0]))
        bm.faces.new((b_grid[iz][nx_steps], f_grid[iz][nx_steps], f_grid[iz+1][nx_steps], b_grid[iz+1][nx_steps]))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat)

def create_face_curved_strip(name, pts_xz_halfw, mat, hz=1.70, offset=0.005, thickness=0.005):
    """Creates a flush-contoured eyebrow or mouth strip following the head surface."""
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

def create_tube_path(name, pts, radius, mat, segments=10):
    rings = []
    for i, p in enumerate(pts):
        p = Vector(p)
        if i < len(pts) - 1:
            t = (Vector(pts[i + 1]) - p).normalized()
        else:
            t = (p - Vector(pts[i - 1])).normalized()
        ref = Vector((0.0, 0.0, 1.0)) if abs(t.z) < 0.9 else Vector((1.0, 0.0, 0.0))
        a = t.cross(ref).normalized()
        b = t.cross(a).normalized()
        rings.append((p, a * radius, b * radius))
    return create_loft_object(name, rings, mat, segments=segments)

def join_objects_into(target_obj, obj_list, final_name):
    bpy.ops.object.select_all(action='DESELECT')
    target_obj.select_set(True)
    for o in obj_list:
        if o != target_obj:
            o.select_set(True)
    bpy.context.view_layer.objects.active = target_obj
    bpy.ops.object.join()
    target_obj.name = final_name
    target_obj.data.name = final_name
    for poly in target_obj.data.polygons:
        poly.use_smooth = True
    return target_obj

# Calibrated Color Palette from Reference Artwork
MAT_SKIN          = make_mat("Mat_Skin",          (0.95, 0.72, 0.53, 1.0), roughness=0.88, specular=0.05)
MAT_NOSE_SHADE    = make_mat("Mat_Nose_Shade",    (0.84, 0.56, 0.39, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_BLONDE   = make_mat("Mat_Hair_Blonde",   (0.99, 0.65, 0.09, 1.0), roughness=0.82, specular=0.08)
MAT_HAIR_GOLD_HI  = make_mat("Mat_Hair_GoldHi",   (1.00, 0.78, 0.20, 1.0), roughness=0.78, specular=0.10)
MAT_HAIR_AMBER    = make_mat("Mat_Hair_Amber",    (0.88, 0.48, 0.05, 1.0), roughness=0.86, specular=0.06)
MAT_BROW_BROWN    = make_mat("Mat_Brow_Brown",    (0.28, 0.13, 0.05, 1.0), roughness=0.88, specular=0.04)
MAT_HEADBAND_NAVY = make_mat("Mat_Headband_Navy", (0.10, 0.19, 0.36, 1.0), roughness=0.88, specular=0.05)
MAT_GLASS_FRAME   = make_mat("Mat_Glass_Frame",   (0.08, 0.08, 0.10, 1.0), roughness=0.32, specular=0.45)
MAT_LENS_DARK     = make_mat("Mat_Lens_Dark",     (0.13, 0.15, 0.18, 1.0), roughness=0.08, specular=0.88)
MAT_LENS_STREAK   = make_mat("Mat_Lens_Streak",   (0.55, 0.59, 0.65, 1.0), roughness=0.08, specular=0.90)
MAT_BANDAGE_WHITE = make_mat("Mat_Bandage_White", (0.97, 0.97, 0.98, 1.0), roughness=0.92, specular=0.04)
MAT_TEETH_WHITE   = make_mat("Mat_Teeth_White",   (0.99, 0.99, 1.00, 1.0), roughness=0.18, specular=0.50)
MAT_TEETH_LINE    = make_mat("Mat_Teeth_Line",    (0.76, 0.80, 0.86, 1.0), roughness=0.35, specular=0.20)
MAT_JACKET_ORANGE = make_mat("Mat_Jacket_Orange", (0.98, 0.38, 0.02, 1.0), roughness=0.85, specular=0.06)
MAT_JACKET_SHADE  = make_mat("Mat_Jacket_Shade",  (0.80, 0.26, 0.01, 1.0), roughness=0.88, specular=0.05)
MAT_TRIM_WHITE    = make_mat("Mat_Trim_White",    (0.95, 0.96, 0.98, 1.0), roughness=0.82, specular=0.08)
MAT_INNER_NAVY    = make_mat("Mat_Inner_Navy",    (0.09, 0.18, 0.35, 1.0), roughness=0.90, specular=0.04)
MAT_PANTS_NAVY    = make_mat("Mat_Pants_Navy",    (0.11, 0.16, 0.28, 1.0), roughness=0.90, specular=0.04)
MAT_DARK_STRAP    = make_mat("Mat_Dark_Strap",    (0.09, 0.10, 0.12, 1.0), roughness=0.88, specular=0.05)
MAT_GLOVE_CHARCOAL= make_mat("Mat_Glove_Charcoal",(0.15, 0.16, 0.19, 1.0), roughness=0.84, specular=0.06)
MAT_METAL_SILVER  = make_mat("Mat_Metal_Silver",  (0.66, 0.70, 0.76, 1.0), roughness=0.42, metallic=0.55, specular=0.30)


# ==============================================================================
# 3. BUILD HEAD: SPIKY GOLDEN HAIR, NAVY HEADBAND, SUNGLASSES, CHEEK BANDAGE & GRIN
# ==============================================================================
def build_blonde_brawler_head():
    head_parts = []
    HZ = 1.70

    # 3.1 Smooth Nendoroid / Toy-Chibi Head Base
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
        v.co = Vector((x * rx, y * ry + y_chin_shift, HZ + z * rz))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m_head = bpy.data.meshes.new("Chibi_Base_Head")
    bm.to_mesh(m_head)
    bm.free()
    head_base = bpy.data.objects.new("Chibi_Base_Head", m_head)
    bpy.context.collection.objects.link(head_base)
    assign_single_material(head_base, MAT_SKIN)
    head_parts.append(head_base)

    # 3.2 Sculpted Anime Ears
    for side in [1.0, -1.0]:
        head_parts.append(create_ellipsoid(
            f"Ear_{side}", (side * 0.384, 0.015, HZ - 0.035), (0.054, 0.040, 0.080),
            MAT_SKIN, rot_euler=(0.08, side * -0.22, side * 0.14)))

    # 3.3 Fierce Angled Eyebrows (sloping down toward nose bridge above sunglasses)
    for side in [1.0, -1.0]:
        ex = side * 0.140
        ez = HZ - 0.025
        brow_pts = [
            (ex - side * 0.092, ez + 0.062, 0.013),
            (ex - side * 0.030, ez + 0.082, 0.017),
            (ex + side * 0.040, ez + 0.104, 0.016),
            (ex + side * 0.098, ez + 0.114, 0.009),
        ]
        head_parts.append(create_face_curved_strip(
            f"FierceBrow_{side}", brow_pts, MAT_BROW_BROWN, hz=HZ, offset=0.0024, thickness=0.0020))

    # 3.4 Subtle Sculpted Nose Tip
    head_parts.append(create_face_disc("NoseTip", 0.0, HZ - 0.072, 0.012, 0.016, MAT_NOSE_SHADE,
                                       hz=HZ, offset=0.0018, thickness=0.0032))

    # 3.5 White Adhesive Bandage on Right Cheek (-X side in character front view)
    head_parts.append(create_face_rect(
        "CheekBandage_Outer", -0.215, HZ - 0.095, 0.050, 0.028,
        MAT_BANDAGE_WHITE, hz=HZ, offset=0.0020, thickness=0.0018, tilt_rad=0.45))
    head_parts.append(create_face_rect(
        "CheekBandage_Pad", -0.215, HZ - 0.095, 0.024, 0.022,
        MAT_TRIM_WHITE, hz=HZ, offset=0.0032, thickness=0.0012, tilt_rad=0.45))

    # 3.6 Big Confident Wide Toothy Grin (Dark Outline + Bright White Teeth + Tooth Grid Lines)
    head_parts.append(create_face_disc(
        "Grin_Outline", 0.0, HZ - 0.148, 0.118, 0.048,
        MAT_DARK_STRAP, hz=HZ, offset=0.0012, thickness=0.0012))
    head_parts.append(create_face_disc(
        "Grin_Teeth", 0.0, HZ - 0.148, 0.108, 0.039,
        MAT_TEETH_WHITE, hz=HZ, offset=0.0020, thickness=0.0014))
    # Upper lip arch cutting cleanly across the top of the grin to give a cheerful D-grin shape
    upper_lip_pts = [
        (-0.116, HZ - 0.136, 0.006),
        (-0.055, HZ - 0.120, 0.007),
        ( 0.000, HZ - 0.116, 0.007),
        ( 0.055, HZ - 0.120, 0.007),
        ( 0.116, HZ - 0.136, 0.006),
    ]
    head_parts.append(create_face_curved_strip(
        "Grin_UpperLip", upper_lip_pts, MAT_DARK_STRAP, hz=HZ, offset=0.0028, thickness=0.0014))
    # Horizontal & vertical tooth divider lines
    horiz_tooth_line = [
        (-0.096, HZ - 0.150, 0.0028),
        (-0.045, HZ - 0.152, 0.0030),
        ( 0.000, HZ - 0.153, 0.0030),
        ( 0.045, HZ - 0.152, 0.0030),
        ( 0.096, HZ - 0.150, 0.0028),
    ]
    head_parts.append(create_face_curved_strip(
        "Grin_HorizLine", horiz_tooth_line, MAT_TEETH_LINE, hz=HZ, offset=0.0030, thickness=0.0008))
    for tx in (-0.058, 0.0, 0.058):
        v_line = [
            (tx, HZ - 0.122, 0.0025),
            (tx, HZ - 0.150, 0.0025),
            (tx, HZ - 0.176, 0.0025),
        ]
        head_parts.append(create_face_curved_strip(
            f"Grin_VertLine_{tx:.2f}", v_line, MAT_TEETH_LINE, hz=HZ, offset=0.0030, thickness=0.0008))

    # 3.7 Cool Black Rectangular Tactical Sunglasses + Glossy Lenses + Diagonal Reflection Streaks
    glass_centers = {}
    for side in [1.0, -1.0]:
        ex = side * 0.145
        ez = HZ - 0.020
        c, nrm = head_surface_point(ex, ez, hz=HZ, surface_offset=0.032)
        glass_centers[side] = c
        yaw_ang = side * 0.25
        roll_ang = side * -0.14

        # Outer Black Rectangular Frame
        head_parts.append(create_rounded_box(
            f"SunglassFrame_{side}", c,
            (0.214, 0.026, 0.118), MAT_GLASS_FRAME,
            rot_euler=(0.06, roll_ang, yaw_ang), bevel_width=0.012))

        # Inner Glossy Dark Lens
        lens_c = c + Vector((0.0, -0.005, 0.0))
        head_parts.append(create_rounded_box(
            f"SunglassLens_{side}", lens_c,
            (0.184, 0.022, 0.092), MAT_LENS_DARK,
            rot_euler=(0.06, roll_ang, yaw_ang), bevel_width=0.008))

        # Diagonal White/Silver Lens Glint Streak
        streak_c = lens_c + Vector((-side * 0.022, -0.008, 0.004))
        head_parts.append(create_rounded_box(
            f"SunglassStreak1_{side}", streak_c,
            (0.024, 0.008, 0.076), MAT_LENS_STREAK,
            rot_euler=(0.06, roll_ang + 0.42, yaw_ang), bevel_width=0.003))
        streak2_c = lens_c + Vector((side * 0.016, -0.008, -0.004))
        head_parts.append(create_rounded_box(
            f"SunglassStreak2_{side}", streak2_c,
            (0.011, 0.008, 0.062), MAT_LENS_STREAK,
            rot_euler=(0.06, roll_ang + 0.42, yaw_ang), bevel_width=0.002))

        # Temple arms wrapping back to the ears
        arm_pts = []
        for ax in (0.236, 0.295, 0.348, 0.378):
            p, _ = head_surface_point(side * ax, ez + 0.020, hz=HZ, surface_offset=0.022)
            arm_pts.append(p)
        arm_pts.append(Vector((side * 0.402, 0.005, ez + 0.012)))
        arm_pts.append(Vector((side * 0.398, 0.065, ez - 0.022)))
        head_parts.append(create_tube_path(f"SunglassArm_{side}", arm_pts, 0.012, MAT_GLASS_FRAME))

    # Center Nose Bridge + Top Brow Bar of Sunglasses
    cL = glass_centers[1.0]
    cR = glass_centers[-1.0]
    nose_p, _ = head_surface_point(0.0, HZ - 0.006, hz=HZ, surface_offset=0.034)
    bridge_pts = [
        Vector((cL.x - 0.072, cL.y - 0.002, cL.z + 0.024)),
        Vector((0.0, nose_p.y - 0.005, nose_p.z + 0.020)),
        Vector((cR.x + 0.072, cR.y - 0.002, cR.z + 0.024)),
    ]
    head_parts.append(create_tube_path("SunglassBridge", bridge_pts, 0.016, MAT_GLASS_FRAME))

    # 3.8 Dark Navy Tactical Headband wrapping around forehead (clearly visible across forehead!)
    hb_rings = [
        (Vector((0.0, -0.006, HZ + 0.082)), Vector((0.402, 0.0, 0.0)), Vector((0.0, 0.382, 0.0))),
        (Vector((0.0, -0.005, HZ + 0.122)), Vector((0.396, 0.0, 0.0)), Vector((0.0, 0.376, 0.0))),
        (Vector((0.0, -0.003, HZ + 0.162)), Vector((0.384, 0.0, 0.0)), Vector((0.0, 0.365, 0.0))),
    ]
    head_parts.append(create_loft_object("Navy_Headband", hb_rings, MAT_HEADBAND_NAVY))

    # 3.9 Golden-Blonde Spiky Hair Base Volumes (recessed behind forehead so headband stays crisp!)
    head_parts.append(create_ellipsoid(
        "Hair_BackVol", (0.0, 0.075, HZ + 0.055), (0.395, 0.365, 0.325),
        MAT_HAIR_BLONDE))
    head_parts.append(create_ellipsoid(
        "Hair_CrownVol", (0.0, 0.055, HZ + 0.215), (0.345, 0.315, 0.175),
        MAT_HAIR_BLONDE, rot_euler=(-0.05, 0.0, 0.0)))

    # 3.10 Signature Center-Parted Front Golden Bangs (arching out from above the navy headband!)
    front_bangs = [
        # Main Left Center-Parted Arching Bang (+X side - curves up then sweeps down toward inner brow)
        (
            [
                (Vector(( 0.020, -0.320, HZ + 0.185)), 0.068, 0.022, -0.15),
                (Vector(( 0.085, -0.392, HZ + 0.195)), 0.064, 0.020, -0.22),
                (Vector(( 0.115, -0.402, HZ + 0.125)), 0.046, 0.015, -0.18),
                (Vector(( 0.085, -0.386, HZ + 0.050)), 0.024, 0.009, -0.10),
                (Vector(( 0.055, -0.372, HZ + 0.005)), 0.006, 0.003, -0.05),
            ],
            MAT_HAIR_GOLD_HI
        ),
        # Main Right Center-Parted Arching Bang (-X side - curves up then sweeps down toward inner brow)
        (
            [
                (Vector((-0.020, -0.320, HZ + 0.185)), 0.068, 0.022,  0.15),
                (Vector((-0.085, -0.392, HZ + 0.195)), 0.064, 0.020,  0.22),
                (Vector((-0.115, -0.402, HZ + 0.125)), 0.046, 0.015,  0.18),
                (Vector((-0.085, -0.386, HZ + 0.050)), 0.024, 0.009,  0.10),
                (Vector((-0.055, -0.372, HZ + 0.005)), 0.006, 0.003,  0.05),
            ],
            MAT_HAIR_GOLD_HI
        ),
        # Secondary Left Forehead Bang sweeping down between headband and sunglasses (+X)
        (
            [
                (Vector(( 0.165, -0.335, HZ + 0.105)), 0.052, 0.016, -0.30),
                (Vector(( 0.150, -0.358, HZ + 0.055)), 0.036, 0.012, -0.25),
                (Vector(( 0.125, -0.362, HZ + 0.010)), 0.007, 0.004, -0.20),
            ],
            MAT_HAIR_BLONDE
        ),
        # Secondary Right Forehead Bang sweeping down between headband and sunglasses (-X)
        (
            [
                (Vector((-0.165, -0.335, HZ + 0.105)), 0.052, 0.016,  0.30),
                (Vector((-0.150, -0.358, HZ + 0.055)), 0.036, 0.012,  0.25),
                (Vector((-0.125, -0.362, HZ + 0.010)), 0.007, 0.004,  0.20),
            ],
            MAT_HAIR_BLONDE
        ),
        # Center-Under-Headband Sharp Forehead Spike Tip
        (
            [
                (Vector(( 0.005, -0.372, HZ + 0.092)), 0.044, 0.014,  0.00),
                (Vector((-0.005, -0.380, HZ + 0.042)), 0.026, 0.009,  0.05),
                (Vector((-0.012, -0.374, HZ - 0.002)), 0.006, 0.003,  0.08),
            ],
            MAT_HAIR_AMBER
        ),
        # Outer-Left Temple Bang peeking below headband (+X)
        (
            [
                (Vector(( 0.255, -0.275, HZ + 0.105)), 0.056, 0.018, -0.48),
                (Vector(( 0.275, -0.285, HZ + 0.045)), 0.038, 0.012, -0.52),
                (Vector(( 0.265, -0.275, HZ - 0.015)), 0.008, 0.004, -0.55),
            ],
            MAT_HAIR_BLONDE
        ),
        # Outer-Right Temple Bang peeking below headband (-X)
        (
            [
                (Vector((-0.255, -0.275, HZ + 0.105)), 0.056, 0.018,  0.48),
                (Vector((-0.275, -0.285, HZ + 0.045)), 0.038, 0.012,  0.52),
                (Vector((-0.265, -0.275, HZ - 0.015)), 0.008, 0.004,  0.55),
            ],
            MAT_HAIR_BLONDE
        ),
    ]
    for idx, (pts, mat) in enumerate(front_bangs):
        head_parts.append(create_ribbon_hair_strand(f"FrontBang_{idx}", pts, mat))

    # 3.11 Side Temple Locks (framing ears & sunglasses)
    for side in [1.0, -1.0]:
        temple_lock_1 = [
            (Vector((side * 0.31, -0.19, HZ + 0.11)), 0.068, 0.022, side * -0.68),
            (Vector((side * 0.36, -0.21, HZ + 0.04)), 0.054, 0.017, side * -0.74),
            (Vector((side * 0.37, -0.19, HZ - 0.03)), 0.032, 0.011, side * -0.78),
            (Vector((side * 0.35, -0.17, HZ - 0.09)), 0.007, 0.004, side * -0.80),
        ]
        temple_lock_2 = [
            (Vector((side * 0.35, -0.07, HZ + 0.15)), 0.078, 0.025, side * -0.95),
            (Vector((side * 0.43, -0.07, HZ + 0.08)), 0.066, 0.020, side * -1.05),
            (Vector((side * 0.47, -0.05, HZ + 0.01)), 0.038, 0.013, side * -1.12),
            (Vector((side * 0.46, -0.03, HZ - 0.05)), 0.008, 0.004, side * -1.15),
        ]
        head_parts.append(create_ribbon_hair_strand(f"TempleLock1_{side}", temple_lock_1, MAT_HAIR_BLONDE))
        head_parts.append(create_ribbon_hair_strand(f"TempleLock2_{side}", temple_lock_2, MAT_HAIR_AMBER))

    # 3.12 Wild Spiky Golden Front-Crown, Top, Side & Back Anime Spikes (100% layered spiky dome!)
    crown_spikes = [
        # Front-Crown Row (directly above navy headband, sweeping upward & outward so zero smooth dome shows!)
        (
            [
                (Vector(( 0.00, -0.32, HZ + 0.17)), 0.095, 0.032,  0.00),
                (Vector(( 0.02, -0.34, HZ + 0.27)), 0.082, 0.028, -0.06),
                (Vector(( 0.05, -0.31, HZ + 0.36)), 0.048, 0.016, -0.10),
                (Vector(( 0.08, -0.26, HZ + 0.42)), 0.008, 0.005, -0.12),
            ],
            MAT_HAIR_GOLD_HI
        ),
        (
            [
                (Vector(( 0.13, -0.29, HZ + 0.17)), 0.092, 0.030, -0.28),
                (Vector(( 0.20, -0.30, HZ + 0.26)), 0.078, 0.026, -0.36),
                (Vector(( 0.28, -0.27, HZ + 0.34)), 0.046, 0.015, -0.42),
                (Vector(( 0.34, -0.22, HZ + 0.38)), 0.008, 0.005, -0.48),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector((-0.13, -0.29, HZ + 0.17)), 0.092, 0.030,  0.28),
                (Vector((-0.20, -0.30, HZ + 0.26)), 0.078, 0.026,  0.36),
                (Vector((-0.28, -0.27, HZ + 0.34)), 0.046, 0.015,  0.42),
                (Vector((-0.34, -0.22, HZ + 0.38)), 0.008, 0.005,  0.48),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector(( 0.24, -0.21, HZ + 0.17)), 0.088, 0.028, -0.55),
                (Vector(( 0.35, -0.21, HZ + 0.24)), 0.074, 0.024, -0.64),
                (Vector(( 0.44, -0.18, HZ + 0.29)), 0.042, 0.014, -0.72),
                (Vector(( 0.50, -0.14, HZ + 0.31)), 0.008, 0.004, -0.78),
            ],
            MAT_HAIR_GOLD_HI
        ),
        (
            [
                (Vector((-0.24, -0.21, HZ + 0.17)), 0.088, 0.028,  0.55),
                (Vector((-0.35, -0.21, HZ + 0.24)), 0.074, 0.024,  0.64),
                (Vector((-0.44, -0.18, HZ + 0.29)), 0.042, 0.014,  0.72),
                (Vector((-0.50, -0.14, HZ + 0.31)), 0.008, 0.004,  0.78),
            ],
            MAT_HAIR_GOLD_HI
        ),
        # Mid-Crown Towering Spikes
        (
            [
                (Vector(( 0.00, -0.14, HZ + 0.25)), 0.115, 0.040,  0.00),
                (Vector(( 0.03, -0.10, HZ + 0.40)), 0.096, 0.034, -0.06),
                (Vector(( 0.07, -0.03, HZ + 0.50)), 0.056, 0.020, -0.10),
                (Vector(( 0.11,  0.03, HZ + 0.56)), 0.009, 0.005, -0.14),
            ],
            MAT_HAIR_GOLD_HI
        ),
        (
            [
                (Vector((-0.09, -0.14, HZ + 0.25)), 0.110, 0.038,  0.14),
                (Vector((-0.16, -0.09, HZ + 0.39)), 0.092, 0.032,  0.20),
                (Vector((-0.23, -0.02, HZ + 0.48)), 0.054, 0.019,  0.26),
                (Vector((-0.29,  0.04, HZ + 0.53)), 0.009, 0.005,  0.30),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector(( 0.11, -0.13, HZ + 0.25)), 0.110, 0.038, -0.16),
                (Vector(( 0.20, -0.08, HZ + 0.38)), 0.092, 0.032, -0.22),
                (Vector(( 0.29, -0.01, HZ + 0.46)), 0.054, 0.019, -0.28),
                (Vector(( 0.36,  0.05, HZ + 0.50)), 0.009, 0.005, -0.32),
            ],
            MAT_HAIR_BLONDE
        ),
        # Upper-Left Outward Flaring Spikes (+X)
        (
            [
                (Vector(( 0.20, -0.08, HZ + 0.22)), 0.104, 0.036, -0.42),
                (Vector(( 0.35, -0.04, HZ + 0.33)), 0.086, 0.030, -0.50),
                (Vector(( 0.47,  0.01, HZ + 0.39)), 0.050, 0.018, -0.58),
                (Vector(( 0.55,  0.06, HZ + 0.41)), 0.009, 0.005, -0.64),
            ],
            MAT_HAIR_GOLD_HI
        ),
        (
            [
                (Vector(( 0.26, -0.01, HZ + 0.16)), 0.098, 0.034, -0.66),
                (Vector(( 0.43,  0.03, HZ + 0.23)), 0.080, 0.028, -0.74),
                (Vector(( 0.54,  0.08, HZ + 0.25)), 0.046, 0.016, -0.80),
                (Vector(( 0.61,  0.12, HZ + 0.24)), 0.008, 0.005, -0.85),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector(( 0.28,  0.06, HZ + 0.09)), 0.090, 0.030, -0.88),
                (Vector(( 0.45,  0.11, HZ + 0.12)), 0.072, 0.024, -0.95),
                (Vector(( 0.55,  0.16, HZ + 0.10)), 0.040, 0.014, -1.00),
                (Vector(( 0.61,  0.20, HZ + 0.06)), 0.008, 0.004, -1.05),
            ],
            MAT_HAIR_AMBER
        ),
        # Upper-Right Outward Flaring Spikes (-X)
        (
            [
                (Vector((-0.20, -0.08, HZ + 0.22)), 0.104, 0.036,  0.42),
                (Vector((-0.35, -0.04, HZ + 0.33)), 0.086, 0.030,  0.50),
                (Vector((-0.47,  0.01, HZ + 0.39)), 0.050, 0.018,  0.58),
                (Vector((-0.55,  0.06, HZ + 0.41)), 0.009, 0.005,  0.64),
            ],
            MAT_HAIR_GOLD_HI
        ),
        (
            [
                (Vector((-0.26, -0.01, HZ + 0.16)), 0.098, 0.034,  0.66),
                (Vector((-0.43,  0.03, HZ + 0.23)), 0.080, 0.028,  0.74),
                (Vector((-0.54,  0.08, HZ + 0.25)), 0.046, 0.016,  0.80),
                (Vector((-0.61,  0.12, HZ + 0.24)), 0.008, 0.005,  0.85),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector((-0.28,  0.06, HZ + 0.09)), 0.090, 0.030,  0.88),
                (Vector((-0.45,  0.11, HZ + 0.12)), 0.072, 0.024,  0.95),
                (Vector((-0.55,  0.16, HZ + 0.10)), 0.040, 0.014,  1.00),
                (Vector((-0.61,  0.20, HZ + 0.06)), 0.008, 0.004,  1.05),
            ],
            MAT_HAIR_AMBER
        ),
        # Back-Crown & Nape Spikes (for full 360° silhouette in top-down gameplay!)
        (
            [
                (Vector(( 0.00,  0.12, HZ + 0.26)), 0.115, 0.044,  0.00),
                (Vector(( 0.00,  0.28, HZ + 0.40)), 0.102, 0.038,  0.00),
                (Vector(( 0.00,  0.42, HZ + 0.44)), 0.056, 0.020,  0.00),
                (Vector(( 0.00,  0.50, HZ + 0.42)), 0.009, 0.005,  0.00),
            ],
            MAT_HAIR_BLONDE
        ),
        (
            [
                (Vector(( 0.16,  0.16, HZ + 0.22)), 0.102, 0.038, -0.25),
                (Vector(( 0.26,  0.32, HZ + 0.32)), 0.088, 0.032, -0.32),
                (Vector(( 0.34,  0.44, HZ + 0.32)), 0.048, 0.016, -0.38),
                (Vector(( 0.40,  0.52, HZ + 0.28)), 0.009, 0.005, -0.42),
            ],
            MAT_HAIR_AMBER
        ),
        (
            [
                (Vector((-0.16,  0.16, HZ + 0.22)), 0.102, 0.038,  0.25),
                (Vector((-0.26,  0.32, HZ + 0.32)), 0.088, 0.032,  0.32),
                (Vector((-0.34,  0.44, HZ + 0.32)), 0.048, 0.016,  0.38),
                (Vector((-0.40,  0.52, HZ + 0.28)), 0.009, 0.005,  0.42),
            ],
            MAT_HAIR_AMBER
        ),
    ]
    for idx, (pts, mat) in enumerate(crown_spikes):
        head_parts.append(create_ribbon_hair_strand(f"CrownSpike_{idx}", pts, mat))

    return join_objects_into(head_base, head_parts, "Chibi_Base_Head")


# ==============================================================================
# 4. BUILD BODY: ORANGE TACTICAL JACKET, NAVY VEST, CHEST HARNESS & BRAWLER GLOVES
# ==============================================================================
def build_blonde_brawler_body():
    body_parts = []

    # 4.1 Neck (Skin) + High Navy Turtleneck Inner Collar
    neck_rings = [
        (Vector((0.0, 0.00, 1.24)), Vector((0.125, 0.0, 0.0)), Vector((0.0, 0.115, 0.0))),
        (Vector((0.0, 0.00, 1.38)), Vector((0.104, 0.0, 0.0)), Vector((0.0, 0.100, 0.0))),
        (Vector((0.0, 0.00, 1.52)), Vector((0.100, 0.0, 0.0)), Vector((0.0, 0.096, 0.0))),
    ]
    neck_obj = create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    navy_collar_rings = [
        (Vector((0.0, -0.004, 1.24)), Vector((0.165, 0.0, 0.0)), Vector((0.0, 0.148, 0.0))),
        (Vector((0.0, -0.006, 1.31)), Vector((0.152, 0.0, 0.0)), Vector((0.0, 0.138, 0.0))),
        (Vector((0.0, -0.006, 1.37)), Vector((0.144, 0.0, 0.0)), Vector((0.0, 0.130, 0.0))),
    ]
    body_parts.append(create_loft_object("Navy_MockCollar", navy_collar_rings, MAT_INNER_NAVY))

    # 4.2 Dark Navy Blue Tactical Inner Shirt / Vest
    tee_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.230, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
        (Vector((0.0, -0.004, 0.90)), Vector((0.244, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, -0.012, 1.04)), Vector((0.262, 0.0, 0.0)), Vector((0.0, 0.208, 0.0))),
        (Vector((0.0, -0.008, 1.18)), Vector((0.256, 0.0, 0.0)), Vector((0.0, 0.196, 0.0))),
        (Vector((0.0,  0.000, 1.29)), Vector((0.200, 0.0, 0.0)), Vector((0.0, 0.158, 0.0))),
    ]
    body_parts.append(create_loft_object("Torso_NavyVest", tee_rings, MAT_INNER_NAVY))

    # 4.3 Open Bright Orange Tactical Jacket with White Front Trim & High Popped Collar
    bm_jkt = bmesh.new()
    z_levels = [
        (0.79, 0.242, 0.188,  0.000),
        (0.90, 0.258, 0.202, -0.005),
        (1.04, 0.276, 0.218, -0.014),
        (1.18, 0.270, 0.204, -0.008),
        (1.29, 0.222, 0.172,  0.000),
    ]
    ang_steps = 36
    ang_start = -0.32 * math.pi  # Open in the front (-Y) so navy vest & chest buckle show!
    ang_end   =  1.32 * math.pi
    thick = 0.036
    outer_rings, inner_rings = [], []
    for z, rx, ry, y_off in z_levels:
        o_ring, i_ring = [], []
        for i in range(ang_steps):
            t = i / (ang_steps - 1)
            ang = ang_start + t * (ang_end - ang_start)
            ca, sa = math.cos(ang), math.sin(ang)
            o_ring.append(bm_jkt.verts.new(Vector((ca * (rx + thick), y_off + sa * (ry + thick), z))))
            i_ring.append(bm_jkt.verts.new(Vector((ca * (rx - 0.004), y_off + sa * (ry - 0.004), z))))
        outer_rings.append(o_ring)
        inner_rings.append(i_ring)
    for r in range(len(z_levels) - 1):
        for i in range(ang_steps - 1):
            bm_jkt.faces.new((outer_rings[r][i], outer_rings[r][i+1], outer_rings[r+1][i+1], outer_rings[r+1][i]))
            bm_jkt.faces.new((inner_rings[r][i+1], inner_rings[r][i], inner_rings[r+1][i], inner_rings[r+1][i+1]))
        bm_jkt.faces.new((inner_rings[r][0], outer_rings[r][0], outer_rings[r+1][0], inner_rings[r+1][0]))
        last = ang_steps - 1
        bm_jkt.faces.new((outer_rings[r][last], inner_rings[r][last], inner_rings[r+1][last], outer_rings[r+1][last]))
    for i in range(ang_steps - 1):
        bm_jkt.faces.new((inner_rings[0][i], inner_rings[0][i+1], outer_rings[0][i+1], outer_rings[0][i]))
        top_r = len(z_levels) - 1
        bm_jkt.faces.new((outer_rings[top_r][i], outer_rings[top_r][i+1], inner_rings[top_r][i+1], inner_rings[top_r][i]))
    bmesh.ops.recalc_face_normals(bm_jkt, faces=bm_jkt.faces)
    m_jkt = bpy.data.meshes.new("Jacket_OrangeBody")
    bm_jkt.to_mesh(m_jkt)
    bm_jkt.free()
    jkt_obj = bpy.data.objects.new("Jacket_OrangeBody", m_jkt)
    bpy.context.collection.objects.link(jkt_obj)
    assign_single_material(jkt_obj, MAT_JACKET_ORANGE)
    body_parts.append(jkt_obj)

    # Crisp White Vertical Trim along both open front jacket edges
    for side in [1.0, -1.0]:
        body_parts.append(create_rounded_box(
            f"Jacket_WhiteEdgeTrim_{side}", (side * 0.132, -0.215, 1.04),
            (0.022, 0.036, 0.48), MAT_TRIM_WHITE,
            rot_euler=(0.0, 0.0, side * 0.16), bevel_width=0.005))

    # High Popped Orange Jacket Collar + White Accent Strip on Right Collar Lapel
    bm_col = bmesh.new()
    col_levels = [(1.25, 0.182, 0.160, 0.00), (1.33, 0.205, 0.178, 0.01), (1.42, 0.228, 0.194, 0.02)]
    c_outer, c_inner = [], []
    for z, rx, ry, y_off in col_levels:
        co_r, ci_r = [], []
        for i in range(24):
            t = i / 23.0
            ang = (-0.30 * math.pi) + t * (1.60 * math.pi)
            ca, sa = math.cos(ang), math.sin(ang)
            co_r.append(bm_col.verts.new(Vector((ca * (rx + 0.028), y_off + sa * (ry + 0.028), z))))
            ci_r.append(bm_col.verts.new(Vector((ca * rx, y_off + sa * ry, z))))
        c_outer.append(co_r)
        c_inner.append(ci_r)
    for r in range(len(col_levels) - 1):
        for i in range(23):
            bm_col.faces.new((c_outer[r][i], c_outer[r][i+1], c_outer[r+1][i+1], c_outer[r+1][i]))
            bm_col.faces.new((c_inner[r][i+1], c_inner[r][i], c_inner[r+1][i], c_inner[r+1][i+1]))
        bm_col.faces.new((c_inner[r][0], c_outer[r][0], c_outer[r+1][0], inner_rings[0][0] if False else c_inner[r+1][0]))
        bm_col.faces.new((c_outer[r][23], c_inner[r][23], c_inner[r+1][23], c_outer[r+1][23]))
    for i in range(23):
        bm_col.faces.new((c_inner[0][i], c_inner[0][i+1], c_outer[0][i+1], c_outer[0][i]))
        bm_col.faces.new((c_outer[2][i], c_outer[2][i+1], c_inner[2][i+1], c_inner[2][i]))
    bmesh.ops.recalc_face_normals(bm_col, faces=bm_col.faces)
    m_col = bpy.data.meshes.new("Jacket_HighCollar")
    bm_col.to_mesh(m_col)
    bm_col.free()
    col_obj = bpy.data.objects.new("Jacket_HighCollar", m_col)
    bpy.context.collection.objects.link(col_obj)
    assign_single_material(col_obj, MAT_JACKET_ORANGE)
    body_parts.append(col_obj)

    # Signature White Reflective Strip on Right Collar Lapel (-X side)
    body_parts.append(create_rounded_box(
        "Collar_WhiteTab_R", (-0.158, -0.152, 1.325),
        (0.024, 0.028, 0.088), MAT_TRIM_WHITE,
        rot_euler=(0.12, -0.18, -0.35), bevel_width=0.004))

    # 4.4 Black Tactical Chest Harness, Center Silver H-Buckle & Black/Orange Pouches
    # Horizontal Chest Strap + Orange Webbing Accents + Silver Center H-Buckle
    body_parts.append(create_rounded_box(
        "Chest_HorizStrap", (0.0, -0.214, 1.035),
        (0.260, 0.026, 0.048), MAT_DARK_STRAP, bevel_width=0.006))
    for side in [1.0, -1.0]:
        body_parts.append(create_rounded_box(
            f"Chest_OrangeWeb_{side}", (side * 0.072, -0.220, 1.035),
            (0.036, 0.024, 0.044), MAT_JACKET_ORANGE, bevel_width=0.004))
    body_parts.append(create_rounded_box(
        "Chest_CenterBuckle", (0.0, -0.226, 1.035),
        (0.072, 0.024, 0.062), MAT_METAL_SILVER, bevel_width=0.006))
    body_parts.append(create_rounded_box(
        "Chest_CenterBuckleSlot", (0.0, -0.234, 1.035),
        (0.026, 0.012, 0.036), MAT_DARK_STRAP, bevel_width=0.003))
    # Lower Dark Tactical Pad under the buckle
    body_parts.append(create_rounded_box(
        "Chest_LowerPad", (0.0, -0.216, 0.945),
        (0.125, 0.028, 0.056), MAT_DARK_STRAP, bevel_width=0.008))

    # Shoulder Harness Straps, Silver Adjuster Buckles & Orange Accents
    for side in [1.0, -1.0]:
        strap_f = create_rounded_box(
            f"Harness_Front_{side}", (side * 0.155, -0.232, 1.11),
            (0.054, 0.032, 0.34), MAT_DARK_STRAP,
            rot_euler=(-0.10, 0.0, side * 0.10), bevel_width=0.008)
        strap_top = create_rounded_box(
            f"Harness_Top_{side}", (side * 0.165, -0.015, 1.315),
            (0.056, 0.40, 0.032), MAT_DARK_STRAP, bevel_width=0.008)
        strap_b = create_rounded_box(
            f"Harness_Back_{side}", (side * 0.155, 0.225, 1.10),
            (0.054, 0.032, 0.35), MAT_DARK_STRAP,
            rot_euler=(0.10, 0.0, -side * 0.10), bevel_width=0.008)
        buckle = create_rounded_box(
            f"Harness_Buckle_{side}", (side * 0.155, -0.252, 1.14),
            (0.068, 0.026, 0.056), MAT_METAL_SILVER, bevel_width=0.008)
        buckle_core = create_rounded_box(
            f"Harness_BuckleCore_{side}", (side * 0.155, -0.262, 1.14),
            (0.036, 0.012, 0.026), MAT_DARK_STRAP, bevel_width=0.004)
        orange_tab = create_rounded_box(
            f"Harness_OrangeTab_{side}", (side * 0.155, -0.246, 1.08),
            (0.044, 0.026, 0.038), MAT_JACKET_ORANGE, bevel_width=0.005)
        sjoint = create_ellipsoid(
            f"ShoulderJoint_{side}", (side * 0.22, 0.00, 1.23),
            (0.142, 0.135, 0.135), MAT_JACKET_ORANGE)
        body_parts.extend([strap_f, strap_top, strap_b, buckle, buckle_core, orange_tab, sjoint])

    # Signature Twin Black & Orange Tactical Pouches on Left Front Chest (+X side)
    for pi, px in enumerate((0.128, 0.195)):
        pouch_z = 0.935 - pi * 0.015
        pouch_y = -0.242 + pi * 0.012
        body_parts.append(create_rounded_box(
            f"ChestPouch_Body_{pi}", (px, pouch_y, pouch_z),
            (0.064, 0.052, 0.125), MAT_DARK_STRAP,
            rot_euler=(0.0, 0.0, 0.18), bevel_width=0.010))
        body_parts.append(create_rounded_box(
            f"ChestPouch_OrangeStrip_{pi}", (px + 0.004, pouch_y - 0.024, pouch_z - 0.008),
            (0.026, 0.014, 0.088), MAT_JACKET_ORANGE,
            rot_euler=(0.0, 0.0, 0.18), bevel_width=0.004))

    # Tactical Backpack on Back (Black with Silver & Orange Trim)
    body_parts.append(create_rounded_box(
        "Backpack_Main", (0.0, 0.285, 1.06),
        (0.280, 0.125, 0.340), MAT_DARK_STRAP, bevel_width=0.028))
    body_parts.append(create_rounded_box(
        "Backpack_TopPlate", (0.0, 0.310, 1.18),
        (0.220, 0.095, 0.090), MAT_METAL_SILVER, bevel_width=0.015))
    body_parts.append(create_rounded_box(
        "Backpack_OrangePocket", (0.0, 0.345, 0.99),
        (0.190, 0.040, 0.140), MAT_JACKET_ORANGE, bevel_width=0.012))

    # 4.5 Orange Jacket Arms, Silver Sleeve Patches & Black/Orange Fingerless Brawling Gloves
    AZ = 1.23
    for side in [1.0, -1.0]:
        sleeve_rings = [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.130, 0.0)), Vector((0.0, 0.0, 0.130 * side))),
            (Vector((side * 0.32, 0.0, AZ)), Vector((0.0, 0.126, 0.0)), Vector((0.0, 0.0, 0.126 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.120, 0.0)), Vector((0.0, 0.0, 0.120 * side))),
            (Vector((side * 0.62, 0.0, AZ)), Vector((0.0, 0.112, 0.0)), Vector((0.0, 0.0, 0.112 * side))),
        ]
        body_parts.append(create_loft_object(f"ArmSleeve_{side}", sleeve_rings, MAT_JACKET_ORANGE))

        # Dark Sleeve Cuff
        cuff_rings = [
            (Vector((side * 0.60, 0.0, AZ)), Vector((0.0, 0.110, 0.0)), Vector((0.0, 0.0, 0.110 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.100, 0.0)), Vector((0.0, 0.0, 0.100 * side))),
        ]
        body_parts.append(create_loft_object(f"SleeveCuff_{side}", cuff_rings, MAT_DARK_STRAP))

        # Metallic Silver Arm Patches on Upper & Mid Sleeve (matching reference art!)
        body_parts.append(create_rounded_box(
            f"SleevePatch_Upper_{side}", (side * 0.35, -0.055, AZ + 0.095),
            (0.115, 0.085, 0.032), MAT_DARK_STRAP,
            rot_euler=(0.45, 0.0, 0.0), bevel_width=0.006))
        body_parts.append(create_rounded_box(
            f"SleevePatch_Silver_{side}", (side * 0.35, -0.062, AZ + 0.102),
            (0.095, 0.066, 0.024), MAT_METAL_SILVER,
            rot_euler=(0.45, 0.0, 0.0), bevel_width=0.005))

        body_parts.append(create_rounded_box(
            f"SleevePatch_Lower_{side}", (side * 0.50, -0.065, AZ + 0.080),
            (0.082, 0.062, 0.028), MAT_METAL_SILVER,
            rot_euler=(0.50, 0.0, 0.0), bevel_width=0.005))

        # Black Fingerless Brawling Glove + Orange Wrist Band & Orange Knuckle Pads
        wrist_rings = [
            (Vector((side * 0.66, -0.002, AZ - 0.002)), Vector((0.0, 0.096, 0.0)), Vector((0.0, 0.0, 0.082 * side))),
            (Vector((side * 0.72, -0.002, AZ - 0.002)), Vector((0.0, 0.094, 0.0)), Vector((0.0, 0.0, 0.078 * side))),
        ]
        body_parts.append(create_loft_object(f"GloveWrist_{side}", wrist_rings, MAT_DARK_STRAP))
        body_parts.append(create_rounded_box(
            f"WristOrangeTab_{side}", (side * 0.69, -0.055, AZ + 0.052),
            (0.038, 0.068, 0.024), MAT_JACKET_ORANGE, bevel_width=0.004))

        glove_rings = [
            (Vector((side * 0.71, -0.004, AZ - 0.004)), Vector((0.0, 0.094, 0.0)), Vector((0.0, 0.0, 0.076 * side))),
            (Vector((side * 0.77, -0.004, AZ - 0.006)), Vector((0.0, 0.098, 0.0)), Vector((0.0, 0.0, 0.072 * side))),
            (Vector((side * 0.83, -0.004, AZ - 0.006)), Vector((0.0, 0.090, 0.0)), Vector((0.0, 0.0, 0.066 * side))),
        ]
        body_parts.append(create_loft_object(f"GlovePalm_{side}", glove_rings, MAT_GLOVE_CHARCOAL))

        # Segmented Bright Orange Back-of-Hand & Knuckle Armor Pads
        body_parts.append(create_rounded_box(
            f"GloveBackPad_{side}", (side * 0.75, -0.004, AZ + 0.052),
            (0.055, 0.115, 0.026), MAT_JACKET_ORANGE, bevel_width=0.006))
        body_parts.append(create_rounded_box(
            f"GloveKnucklePad_{side}", (side * 0.805, -0.004, AZ + 0.048),
            (0.042, 0.125, 0.026), MAT_JACKET_ORANGE, bevel_width=0.006))

        # Exposed Skin Fingers + Thumb
        finger_rings = [
            (Vector((side * 0.82, -0.004, AZ)), Vector((0.0, 0.082, 0.0)), Vector((0.0, 0.0, 0.064 * side))),
            (Vector((side * 0.87, -0.004, AZ)), Vector((0.0, 0.074, 0.0)), Vector((0.0, 0.0, 0.058 * side))),
            (Vector((side * 0.90, -0.004, AZ)), Vector((0.0, 0.048, 0.0)), Vector((0.0, 0.0, 0.038 * side))),
        ]
        body_parts.append(create_loft_object(f"FingersSkin_{side}", finger_rings, MAT_SKIN))
        thumb_base = [
            (Vector((side * 0.75, -0.045, AZ)), Vector((side * 0.036, 0.0, 0.0)), Vector((0.0, 0.0, 0.034 * side))),
            (Vector((side * 0.79, -0.088, AZ)), Vector((side * 0.032, 0.01, 0.0)), Vector((0.0, 0.0, 0.030 * side))),
        ]
        body_parts.append(create_loft_object(f"ThumbGlove_{side}", thumb_base, MAT_GLOVE_CHARCOAL))
        thumb_tip = [
            (Vector((side * 0.78, -0.082, AZ)), Vector((side * 0.030, 0.01, 0.0)), Vector((0.0, 0.0, 0.028 * side))),
            (Vector((side * 0.81, -0.118, AZ)), Vector((side * 0.022, 0.01, 0.0)), Vector((0.0, 0.0, 0.022 * side))),
        ]
        body_parts.append(create_loft_object(f"ThumbSkin_{side}", thumb_tip, MAT_SKIN))

    # 4.6 Tactical Belt, Dark Navy Cargo Pants, Knee Pads & Combat Boots
    belt_rings = [
        (Vector((0.0, 0.004, 0.72)), Vector((0.264, 0.0, 0.0)), Vector((0.0, 0.204, 0.0))),
        (Vector((0.0, 0.000, 0.80)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.198, 0.0))),
    ]
    body_parts.append(create_loft_object("Utility_Belt", belt_rings, MAT_DARK_STRAP))
    body_parts.append(create_rounded_box(
        "Belt_Buckle", (0.0, -0.212, 0.760),
        (0.092, 0.034, 0.064), MAT_METAL_SILVER, bevel_width=0.008))

    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.170, 0.0, 0.0)), Vector((0.0, 0.144, 0.0))),
        (Vector((0.0, 0.005, 0.64)), Vector((0.248, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, 0.006, 0.71)), Vector((0.262, 0.0, 0.0)), Vector((0.0, 0.200, 0.0))),
        (Vector((0.0, 0.004, 0.77)), Vector((0.244, 0.0, 0.0)), Vector((0.0, 0.186, 0.0))),
    ]
    body_parts.append(create_loft_object("Pelvis_Pants", pelvis_rings, MAT_PANTS_NAVY))

    for side in [1.0, -1.0]:
        leg_pants = [
            (Vector((side * 0.145,  0.004, 0.68)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.148,  0.004, 0.56)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.134, 0.0))),
            (Vector((side * 0.150,  0.000, 0.44)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.152, -0.002, 0.32)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.130, 0.0))),
        ]
        body_parts.append(create_loft_object(f"LegPants_{side}", leg_pants, MAT_PANTS_NAVY))

        # Side Thigh Cargo Pocket + Orange Accent Flap
        body_parts.append(create_rounded_box(
            f"CargoPocket_{side}", (side * 0.258, -0.005, 0.52),
            (0.048, 0.115, 0.135), MAT_INNER_NAVY, bevel_width=0.010))
        body_parts.append(create_rounded_box(
            f"CargoOrangeTab_{side}", (side * 0.278, -0.005, 0.575),
            (0.022, 0.095, 0.032), MAT_JACKET_ORANGE, bevel_width=0.005))

        # Tactical Knee Pad + Orange Trim
        body_parts.append(create_rounded_box(
            f"KneeArmor_{side}", (side * 0.152, -0.114, 0.365),
            (0.128, 0.052, 0.128), MAT_DARK_STRAP, bevel_width=0.016))
        body_parts.append(create_rounded_box(
            f"KneeOrangeStripe_{side}", (side * 0.152, -0.134, 0.365),
            (0.088, 0.022, 0.034), MAT_JACKET_ORANGE, bevel_width=0.005))

        # Heavy Combat Boots
        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
            (Vector((side * 0.152, -0.010, 0.13)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.152, -0.045, 0.08)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.168, 0.0))),
            (Vector((side * 0.152, -0.055, 0.035)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
        ]
        body_parts.append(create_loft_object(f"BootUpper_{side}", boot_upper, MAT_DARK_STRAP))
        boot_sole = [
            (Vector((side * 0.152, -0.055, 0.040)), Vector((0.134, 0.0, 0.0)), Vector((0.0, -0.184, 0.0))),
            (Vector((side * 0.152, -0.055, 0.004)), Vector((0.130, 0.0, 0.0)), Vector((0.0, -0.180, 0.0))),
        ]
        body_parts.append(create_loft_object(f"BootSole_{side}", boot_sole, MAT_JACKET_ORANGE))

    return join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_blonde_brawler_head()
body_obj = build_blonde_brawler_body()

# ==============================================================================
# 5. CREATE 18-BONE CHIBI ARMATURE (Chibi_Base_Rig) MATCHED TO TOY-CHIBI PROPORTIONS
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
    ("LeftUpperArm",  (0.27, 0.0, 1.23),   (0.50, 0.0, 1.23),   "LeftShoulder",  (0, 0, 1)),
    ("LeftLowerArm",  (0.50, 0.0, 1.23),   (0.72, 0.0, 1.23),   "LeftUpperArm",  (0, 0, 1)),
    ("LeftHand",      (0.72, 0.0, 1.23),   (0.90, 0.0, 1.23),   "LeftLowerArm",  (0, 0, 1)),

    ("RightShoulder", (-0.16, 0.0, 1.23),  (-0.27, 0.0, 1.23),  "Chest",         (0, 0, 1)),
    ("RightUpperArm", (-0.27, 0.0, 1.23),  (-0.50, 0.0, 1.23),  "RightShoulder", (0, 0, 1)),
    ("RightLowerArm", (-0.50, 0.0, 1.23),  (-0.72, 0.0, 1.23),  "RightUpperArm", (0, 0, 1)),
    ("RightHand",     (-0.72, 0.0, 1.23),  (-0.90, 0.0, 1.23),  "RightLowerArm", (0, 0, 1)),

    ("LeftUpperLeg",  (0.150, 0.0, 0.68),  (0.150, 0.0, 0.37),  "Hips",          (0, 1, 0)),
    ("LeftLowerLeg",  (0.150, 0.0, 0.37),  (0.152, 0.0, 0.11),  "LeftUpperLeg",  (0, 1, 0)),
    ("LeftFoot",      (0.152, 0.0, 0.11),  (0.152, -0.20, 0.03),"LeftLowerLeg",  (0, 0, 1)),

    ("RightUpperLeg", (-0.150, 0.0, 0.68), (-0.150, 0.0, 0.37), "Hips",          (0, 1, 0)),
    ("RightLowerLeg", (-0.150, 0.0, 0.37), (-0.152, 0.0, 0.11), "RightUpperLeg", (0, 1, 0)),
    ("RightFoot",     (-0.152, 0.0, 0.11), (-0.152, -0.20, 0.03),"RightLowerLeg", (0, 0, 1)),
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

# ==============================================================================
# 6. SKIN WEIGHT BINDING
# ==============================================================================
def point_to_segment_dist(p, a, b):
    ab = b - a
    len_sq = ab.length_squared
    if len_sq < 1e-8:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / len_sq))
    proj = a + ab * t
    return (p - proj).length

def bind_mesh_to_rig(mesh_obj, rig, is_head=False):
    mesh_obj.parent = rig
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = rig
    mod.use_deform_preserve_volume = True

    vgroups = {}
    for bname, _, _, _, _ in bone_defs:
        vgroups[bname] = mesh_obj.vertex_groups.new(name=bname)

    bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in bone_defs if bname != "Root"]

    for v in mesh_obj.data.vertices:
        co = v.co
        if is_head:
            neck_w = max(0.0, min(0.25, (1.46 - co.z) * 1.4)) if co.z < 1.46 else 0.0
            vgroups["Head"].add([v.index], 1.0 - neck_w, 'REPLACE')
            if neck_w > 0.001:
                vgroups["Neck"].add([v.index], neck_w, 'REPLACE')
            continue

        weights = {}
        for bname, h, t in bones_info:
            if bname.startswith("Left") and co.x < -0.01:
                continue
            if bname.startswith("Right") and co.x > 0.01:
                continue
            if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.19 or co.z < 1.04 or (co.z > 1.285 and abs(co.x) < 0.37)):
                continue
            if "Shoulder" in bname and (abs(co.x) < 0.14 or co.z < 1.02):
                continue
            if ("Leg" in bname or "Foot" in bname) and (co.z > 0.72 or abs(co.x) > 0.32):
                continue
            if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.30 or co.z < 0.54):
                continue
            if bname == "Head" and co.z < 1.42:
                continue

            d = point_to_segment_dist(co, h, t)
            sigma = 0.120 if ("Arm" in bname or "Shoulder" in bname or "Leg" in bname) else 0.115
            w = math.exp(-(d * d) / (2.0 * (sigma ** 2)))
            if w > 1e-5:
                weights[bname] = w

        if not weights:
            weights["Hips"] = 1.0

        top = sorted(weights.items(), key=lambda item: item[1], reverse=True)[:4]
        total = sum(w for _, w in top)
        for bname, w in top:
            vgroups[bname].add([v.index], w / total, 'REPLACE')

bind_mesh_to_rig(head_obj, rig_obj, is_head=True)
bind_mesh_to_rig(body_obj, rig_obj, is_head=False)

# ==============================================================================
# 7. ZERO-TWIST ANALYTICAL TWO-BONE IK & POSE HELPERS
# ==============================================================================
for pb in rig_obj.pose.bones:
    pb.rotation_mode = 'QUATERNION'

rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}

def begin_frame_pose(frame):
    scene.frame_set(frame)
    for pb in rig_obj.pose.bones:
        pb.location = Vector((0, 0, 0))
        pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
        pb.scale = Vector((1, 1, 1))
    bpy.context.view_layer.update()

def set_local_euler_deg(bname, rx_deg, ry_deg, rz_deg):
    pb = rig_obj.pose.bones[bname]
    eul = Euler((math.radians(rx_deg), math.radians(ry_deg), math.radians(rz_deg)), 'XYZ')
    pb.rotation_quaternion = eul.to_quaternion()
    bpy.context.view_layer.update()

def set_hips_transform(loc_offset, pitch_deg, yaw_deg, roll_deg):
    pb = rig_obj.pose.bones["Hips"]
    pb.location = Vector(loc_offset)
    q_pitch = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
    q_yaw   = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
    q_roll  = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
    pb.rotation_quaternion = q_yaw @ q_roll @ q_pitch
    bpy.context.view_layer.update()

def set_hips_world_offset(world_offset, pitch_deg, yaw_deg, roll_deg):
    pb = rig_obj.pose.bones["Hips"]
    wx, wy, wz = world_offset
    pb.location = Vector((wx, wz, -wy))
    q_pitch = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
    q_yaw   = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
    q_roll  = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
    pb.rotation_quaternion = q_yaw @ q_roll @ q_pitch
    bpy.context.view_layer.update()

def swing_bone_zero_twist(pb, target_end_pt):
    cur_dir = (pb.tail - pb.head).normalized()
    tgt_dir = (Vector(target_end_pt) - pb.head).normalized()
    q_swing = cur_dir.rotation_difference(tgt_dir)
    world_mat = pb.matrix.copy()
    world_loc = world_mat.to_translation()
    new_world_rot = q_swing @ world_mat.to_quaternion()
    new_world_mat = Matrix.LocRotScale(world_loc, new_world_rot, Vector((1, 1, 1)))
    pb.matrix = new_world_mat
    bpy.context.view_layer.update()

def aim_two_bone_ik(upper_name, lower_name, end_name, target_world_pos, pole_world_vec, hand_rot_offset_deg=(0, 0, 0)):
    pb_u = rig_obj.pose.bones[upper_name]
    pb_l = rig_obj.pose.bones[lower_name]
    pb_e = rig_obj.pose.bones[end_name]

    root_pos = pb_u.head.copy()
    L1 = rest_lengths[upper_name]
    L2 = rest_lengths[lower_name]
    tgt = Vector(target_world_pos)

    d_vec = tgt - root_pos
    D = d_vec.length
    max_reach = (L1 + L2) * 0.992
    min_reach = abs(L1 - L2) + 0.02
    if D > max_reach:
        D = max_reach
        tgt = root_pos + d_vec.normalized() * D
        d_vec = tgt - root_pos
    elif D < min_reach:
        D = min_reach
        tgt = root_pos + d_vec.normalized() * D
        d_vec = tgt - root_pos

    cos_a = max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2.0 * L1 * D)))
    sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))

    dir_norm = d_vec.normalized()
    pole = Vector(pole_world_vec).normalized()
    plane_pole = (pole - dir_norm * pole.dot(dir_norm))
    if plane_pole.length_squared < 1e-6:
        plane_pole = Vector((0, 1, 0))
    else:
        plane_pole.normalize()

    mid_pos = root_pos + dir_norm * (L1 * cos_a) + plane_pole * (L1 * sin_a)

    swing_bone_zero_twist(pb_u, mid_pos)
    swing_bone_zero_twist(pb_l, tgt)

    if any(abs(a) > 1e-4 for a in hand_rot_offset_deg):
        set_local_euler_deg(end_name, *hand_rot_offset_deg)
    else:
        pb_e.rotation_quaternion = Quaternion((1, 0, 0, 0))
        bpy.context.view_layer.update()

def keyframe_all_bones(frame, prev_quats):
    for pb in rig_obj.pose.bones:
        q = pb.rotation_quaternion.copy()
        if pb.name in prev_quats and prev_quats[pb.name].dot(q) < 0.0:
            q = -q
            pb.rotation_quaternion = q
        prev_quats[pb.name] = q.copy()
        pb.keyframe_insert(data_path="location", frame=frame)
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        pb.keyframe_insert(data_path="scale", frame=frame)

def create_new_action(name):
    act = bpy.data.actions.new(name=name)
    act.use_fake_user = True
    if rig_obj.animation_data is None:
        rig_obj.animation_data_create()
    rig_obj.animation_data.action = act
    if hasattr(rig_obj.animation_data, "action_slot") and hasattr(act, "slots"):
        if len(act.slots) == 0:
            slot = act.slots.new(id_type='OBJECT', name=rig_obj.name)
            rig_obj.animation_data.action_slot = slot
        else:
            rig_obj.animation_data.action_slot = act.slots[0]
    return act

# ==============================================================================
# 8. BUILD ALL 11 ANIMATIONS CALIBRATED FOR TOY-CHIBI PROPORTIONS
# ==============================================================================

# 1. Idle (Frames 1..49)
act_idle = create_new_action("Idle")
prev_q = {}
for f in range(1, 50):
    begin_frame_pose(f)
    t = (f - 1) / 48.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang)

    set_hips_transform((0.0, 0.0, 0.012 * breath), pitch_deg=1.2 * breath, yaw_deg=1.5 * math.sin(ang * 0.5), roll_deg=0.0)
    set_local_euler_deg("Spine", 1.5 - 1.2 * breath, 0.0, 0.0)
    set_local_euler_deg("Chest", -2.0 * breath, 0.0, 0.0)
    set_local_euler_deg("Head", 1.8 * breath, 2.5 * math.sin(ang), 1.2 * math.cos(ang))

    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.31, -0.04, 0.79 + 0.010 * breath),
                    pole_world_vec=(0.35, 0.65, -0.1))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.31, -0.04, 0.79 + 0.010 * breath),
                    pole_world_vec=(-0.35, 0.65, -0.1))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, -0.01, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, -0.01, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 2. Walk (Frames 1..25)
act_walk = create_new_action("Walk")
prev_q = {}
for f in range(1, 26):
    begin_frame_pose(f)
    t = (f - 1) / 24.0
    ang = t * 2.0 * math.pi
    s = math.sin(ang)
    c = math.cos(ang)
    bounce = abs(math.sin(ang)) * 0.038

    set_hips_transform((0.018 * s, 0.0, bounce), pitch_deg=4.0, yaw_deg=-7.0 * s, roll_deg=3.0 * c)
    set_local_euler_deg("Spine", 3.0, 4.0 * s, 0.0)
    set_local_euler_deg("Chest", 3.0, 8.0 * s, -2.0 * c)
    set_local_euler_deg("Head", -4.0, -5.0 * s, 0.0)

    l_arm_y = -0.04 + 0.18 * s
    r_arm_y = -0.04 - 0.18 * s
    l_arm_z = 0.81 + 0.04 * abs(s)
    r_arm_z = 0.81 + 0.04 * abs(s)
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32, l_arm_y, l_arm_z),
                    pole_world_vec=(0.40, 0.70, -0.20))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.32, r_arm_y, r_arm_z),
                    pole_world_vec=(-0.40, 0.70, -0.20))

    l_foot_y = -0.18 * s
    r_foot_y =  0.18 * s
    l_foot_z = 0.11 + max(0.0, c) * 0.10
    r_foot_z = 0.11 + max(0.0, -c) * 0.10
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, l_foot_y, l_foot_z),
                    pole_world_vec=(0.16, -1.0, 0.2),
                    hand_rot_offset_deg=(15.0 * s, 0, 0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, r_foot_y, r_foot_z),
                    pole_world_vec=(-0.16, -1.0, 0.2),
                    hand_rot_offset_deg=(-15.0 * s, 0, 0))
    keyframe_all_bones(f, prev_q)

# 3. Run (Frames 1..17)
act_run = create_new_action("Run")
prev_q = {}
for f in range(1, 18):
    begin_frame_pose(f)
    t = (f - 1) / 16.0
    ang = t * 2.0 * math.pi
    s = math.sin(ang)
    c = math.cos(ang)
    bounce = (math.sin(ang * 2.0 - math.pi * 0.25) * 0.5 + 0.5) * 0.080

    set_hips_transform((0.025 * s, 0.0, bounce - 0.015), pitch_deg=14.0, yaw_deg=-12.0 * s, roll_deg=4.5 * c)
    set_local_euler_deg("Spine", 8.0, 6.0 * s, 0.0)
    set_local_euler_deg("Chest", 8.0, 12.0 * s, -3.5 * c)
    set_local_euler_deg("Head", -16.0, -8.0 * s, 0.0)

    l_hand_y = -0.12 + 0.26 * s
    r_hand_y = -0.12 - 0.26 * s
    l_hand_z = 0.92 - 0.08 * s
    r_hand_z = 0.92 + 0.08 * s
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.33, l_hand_y, l_hand_z),
                    pole_world_vec=(0.45, 0.80, -0.25))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.33, r_hand_y, r_hand_z),
                    pole_world_vec=(-0.45, 0.80, -0.25))

    l_foot_y = -0.28 * s
    r_foot_y =  0.28 * s
    l_foot_z = 0.12 + max(0.0, c) * 0.20
    r_foot_z = 0.12 + max(0.0, -c) * 0.20
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, l_foot_y, l_foot_z),
                    pole_world_vec=(0.16, -1.0, 0.3),
                    hand_rot_offset_deg=(28.0 * s, 0, 0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, r_foot_y, r_foot_z),
                    pole_world_vec=(-0.16, -1.0, 0.3),
                    hand_rot_offset_deg=(-28.0 * s, 0, 0))
    keyframe_all_bones(f, prev_q)

# 4. GunHold (Frames 1..25)
act_gunhold = create_new_action("GunHold")
prev_q = {}
for f in range(1, 26):
    begin_frame_pose(f)
    t = (f - 1) / 24.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang) * 0.010

    set_hips_transform((0.0, 0.0, breath - 0.015), pitch_deg=4.0, yaw_deg=10.0, roll_deg=0.0)
    set_local_euler_deg("Spine", 4.0, -5.0, 0.0)
    set_local_euler_deg("Chest", 4.0, -5.0, 0.0)
    set_local_euler_deg("Head", -5.0, 0.0, 0.0)

    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.06, -0.38, 1.10 + breath),
                    pole_world_vec=(-0.40, 0.25, -0.8))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.06, -0.40, 1.09 + breath),
                    pole_world_vec=(0.40, 0.25, -0.8))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.05, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.05, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 5. Shoot (Frames 1..15)
act_shoot = create_new_action("Shoot")
prev_q = {}
for f in range(1, 16):
    begin_frame_pose(f)
    if f <= 3:
        recoil = (f - 1) / 2.0
    else:
        recoil = math.exp(-((f - 3) / 3.8)) * math.cos((f - 3) * 0.22)
        recoil = max(-0.08, recoil)

    set_hips_transform((0.0, 0.03 * recoil, -0.015 - 0.02 * recoil), pitch_deg=4.0 - 6.0 * recoil, yaw_deg=10.0 + 5.0 * recoil, roll_deg=0.0)
    set_local_euler_deg("Spine", 4.0 - 7.0 * recoil, -5.0, 0.0)
    set_local_euler_deg("Chest", 4.0 - 9.0 * recoil, -5.0 + 4.0 * recoil, 0.0)
    set_local_euler_deg("Head", -5.0 - 8.0 * recoil, 0.0, 0.0)

    kick_y = 0.11 * recoil
    kick_z = 0.15 * recoil
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.06, -0.38 + kick_y, 1.10 + kick_z),
                    pole_world_vec=(-0.42, 0.25, -0.75),
                    hand_rot_offset_deg=(-25.0 * recoil, 0, 0))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.06, -0.40 + kick_y, 1.09 + kick_z),
                    pole_world_vec=(0.42, 0.25, -0.75),
                    hand_rot_offset_deg=(-25.0 * recoil, 0, 0))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.05, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.05, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 6. Slash1 (Frames 1..19)
act_slash1 = create_new_action("Slash1")
prev_q = {}
for f in range(1, 20):
    begin_frame_pose(f)
    if f <= 5:
        u = (f - 1) / 4.0
        swing = -u * 0.55
    elif f <= 11:
        u = (f - 5) / 6.0
        swing = -0.55 + 1.55 * (3 * u * u - 2 * u * u * u)
    else:
        u = (f - 11) / 8.0
        swing = 1.0 * (1.0 - (3 * u * u - 2 * u * u * u))

    yaw_body = -42.0 * swing
    crouch = -0.05 * abs(swing)
    lunge_y = -0.08 * max(0.0, swing)

    set_hips_transform((0.0, lunge_y, crouch), pitch_deg=12.0 * abs(swing), yaw_deg=yaw_body * 0.45, roll_deg=-4.0 * swing)
    set_local_euler_deg("Spine", 8.0 * abs(swing), yaw_body * 0.30, 0.0)
    set_local_euler_deg("Chest", 6.0 * abs(swing), yaw_body * 0.35, 0.0)
    set_local_euler_deg("Head", -8.0 * abs(swing), -yaw_body * 0.45, 0.0)

    arc_ang = swing * 1.65
    rh_x = -0.16 + 0.56 * swing
    rh_y = -0.46 * math.cos(arc_ang * 0.65) + 0.06
    rh_z = 1.10 - 0.10 * swing
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(rh_x, rh_y, rh_z),
                    pole_world_vec=(-0.45, 0.30, -0.4),
                    hand_rot_offset_deg=(0, 0, -35.0 * swing))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32 + 0.08 * swing, -0.04 + 0.14 * swing, 0.84),
                    pole_world_vec=(0.45, 0.50, -0.4))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.12 * max(0.0, swing), 0.11),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.08 * max(0.0, swing), 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 7. Slash2 (Frames 1..19)
act_slash2 = create_new_action("Slash2")
prev_q = {}
for f in range(1, 20):
    begin_frame_pose(f)
    if f <= 5:
        u = (f - 1) / 4.0
        swing = -0.60 * u
    elif f <= 11:
        u = (f - 5) / 6.0
        swing = -0.60 + 1.60 * (3 * u * u - 2 * u * u * u)
    else:
        u = (f - 11) / 8.0
        swing = 1.0 * (1.0 - (3 * u * u - 2 * u * u * u))

    yaw_body = 45.0 * swing
    crouch = -0.05 * (1.0 - max(0.0, swing)) * abs(swing)

    set_hips_transform((0.0, -0.06 * abs(swing), crouch), pitch_deg=8.0 - 6.0 * swing, yaw_deg=yaw_body * 0.45, roll_deg=5.0 * swing)
    set_local_euler_deg("Spine", 6.0 - 5.0 * swing, yaw_body * 0.30, 0.0)
    set_local_euler_deg("Chest", 4.0 - 8.0 * swing, yaw_body * 0.35, 0.0)
    set_local_euler_deg("Head", -4.0 + 6.0 * swing, -yaw_body * 0.45, 0.0)

    rh_x = -0.12 - 0.48 * swing
    rh_y = -0.44 * math.cos(swing * 0.9) + 0.05
    rh_z = 1.08 + 0.30 * swing
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(rh_x, rh_y, rh_z),
                    pole_world_vec=(-0.42, 0.35, -0.4),
                    hand_rot_offset_deg=(20.0 * swing, 0, 40.0 * swing))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32 - 0.08 * swing, -0.12 + 0.10 * swing, 0.88),
                    pole_world_vec=(0.45, 0.45, -0.4))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, 0.06 * max(0.0, swing), 0.11),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, -0.12 * max(0.0, swing), 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 8. Slash3 (Frames 1..23)
act_slash3 = create_new_action("Slash3")
prev_q = {}
for f in range(1, 24):
    begin_frame_pose(f)
    if f <= 8:
        u = (f - 1) / 7.0
        s = 3 * u * u - 2 * u * u * u
        slam = -s
        hip_z = 0.20 * math.sin(u * math.pi * 0.85)
    elif f <= 13:
        u = (f - 8) / 5.0
        s = u * u
        slam = -1.0 + 2.0 * s
        hip_z = 0.10 * (1.0 - u) - 0.16 * u
    elif f <= 16:
        slam = 1.0
        hip_z = -0.16
    else:
        u = (f - 16) / 7.0
        s = 3 * u * u - 2 * u * u * u
        slam = 1.0 * (1.0 - s)
        hip_z = -0.16 * (1.0 - s)

    pitch_torso = 26.0 * slam
    set_hips_transform((0.0, -0.10 * max(0.0, slam), hip_z), pitch_deg=pitch_torso * 0.6, yaw_deg=0.0, roll_deg=0.0)
    set_local_euler_deg("Spine", pitch_torso * 0.5, 0.0, 0.0)
    set_local_euler_deg("Chest", pitch_torso * 0.6, 0.0, 0.0)
    set_local_euler_deg("Head", -pitch_torso * 0.4, 0.0, 0.0)

    if slam < 0:
        hand_y = -0.32 + 0.24 * (-slam)
        hand_z = 1.10 + 0.60 * (-slam)
    else:
        hand_y = -0.32 - 0.18 * slam
        hand_z = 1.10 - 0.52 * slam

    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.08, hand_y, hand_z),
                    pole_world_vec=(-0.45, 0.25, -0.3))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.08, hand_y, hand_z),
                    pole_world_vec=(0.45, 0.25, -0.3))

    foot_lift = max(0.0, hip_z * 0.65)
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.12 * max(0.0, slam), 0.11 + foot_lift),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.10 * max(0.0, slam), 0.11 + foot_lift),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 9. DashRoll (Frames 1..21)
act_dash = create_new_action("DashRoll")
prev_q = {}
for f in range(1, 22):
    begin_frame_pose(f)
    u = (f - 1) / 20.0
    s = 3 * u * u - 2 * u * u * u
    roll_deg = 360.0 * s
    tuck = math.sin(u * math.pi)
    hip_z = -0.22 * tuck

    set_hips_transform((0.0, -0.12 * tuck, hip_z), pitch_deg=roll_deg, yaw_deg=0.0, roll_deg=0.0)
    set_local_euler_deg("Spine", 38.0 * tuck, 0.0, 0.0)
    set_local_euler_deg("Chest", 35.0 * tuck, 0.0, 0.0)
    set_local_euler_deg("Head", 32.0 * tuck, 0.0, 0.0)

    set_local_euler_deg("LeftShoulder", 0.0, 0.0, -25.0 * tuck)
    set_local_euler_deg("LeftUpperArm", 0.0, 50.0 * tuck, 40.0 * tuck)
    set_local_euler_deg("LeftLowerArm", 0.0, 0.0, 80.0 * tuck)

    set_local_euler_deg("RightShoulder", 0.0, 0.0, 25.0 * tuck)
    set_local_euler_deg("RightUpperArm", 0.0, -50.0 * tuck, -40.0 * tuck)
    set_local_euler_deg("RightLowerArm", 0.0, 0.0, -80.0 * tuck)

    set_local_euler_deg("LeftUpperLeg", -85.0 * tuck, 0.0, 0.0)
    set_local_euler_deg("LeftLowerLeg", 105.0 * tuck, 0.0, 0.0)
    set_local_euler_deg("RightUpperLeg", -85.0 * tuck, 0.0, 0.0)
    set_local_euler_deg("RightLowerLeg", 105.0 * tuck, 0.0, 0.0)
    keyframe_all_bones(f, prev_q)

# 10. Death (Frames 1..37)
act_death = create_new_action("Death")
prev_q = {}
for f in range(1, 38):
    begin_frame_pose(f)
    if f <= 8:
        u = (f - 1) / 7.0
        s = 3 * u * u - 2 * u * u * u
        hip_x = 0.02 * s
        hip_y = 0.10 * s
        hip_z = -0.05 * s
        hip_pitch = -18.0 * s
        hip_yaw = -15.0 * s
        hip_roll = 6.0 * s
        spine_pitch = -16.0 * s
        chest_pitch = -20.0 * s
        head_pitch = -26.0 * s
        head_yaw = 18.0 * s
        set_local_euler_deg("RightShoulder", 0.0, 0.0, 15.0 * s)
        set_local_euler_deg("RightUpperArm", 0.0, -25.0 * s, -55.0 * s)
        set_local_euler_deg("RightLowerArm", 0.0, 0.0, -95.0 * s)
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, -20.0 * s)
        set_local_euler_deg("LeftUpperArm", 0.0, -35.0 * s, 25.0 * s)
        set_local_euler_deg("LeftLowerArm", 0.0, 0.0, 40.0 * s)
        set_local_euler_deg("LeftUpperLeg", -18.0 * s, 0.0, 0.0)
        set_local_euler_deg("LeftLowerLeg", 32.0 * s, 0.0, 0.0)
        set_local_euler_deg("RightUpperLeg", 10.0 * s, 0.0, 0.0)
        set_local_euler_deg("RightLowerLeg", 25.0 * s, 0.0, 0.0)
    elif f <= 20:
        u = (f - 8) / 12.0
        s = 3 * u * u - 2 * u * u * u
        hip_x = 0.02 + 0.02 * s
        hip_y = 0.10 - 0.06 * s
        hip_z = -0.05 - 0.22 * s
        hip_pitch = -18.0 + 28.0 * s
        hip_yaw = -15.0 + 25.0 * s
        hip_roll = 6.0 - 12.0 * s
        spine_pitch = -16.0 + 6.0 * s
        chest_pitch = -20.0 + 5.0 * s
        head_pitch = -26.0 - 10.0 * s
        head_yaw = 18.0 - 30.0 * s
        set_local_euler_deg("RightShoulder", 0.0, 0.0, 15.0)
        set_local_euler_deg("RightUpperArm", 0.0, -25.0 + 10.0 * s, -55.0)
        set_local_euler_deg("RightLowerArm", 0.0, 0.0, -95.0 + 15.0 * s)
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, -20.0 - 10.0 * s)
        set_local_euler_deg("LeftUpperArm", -55.0 * s, -35.0 * (1.0 - s), 25.0 + 35.0 * s)
        set_local_euler_deg("LeftLowerArm", 0.0, 0.0, 40.0 - 20.0 * s)
        set_local_euler_deg("LeftUpperLeg", -18.0 - 52.0 * s, 0.0, 0.0)
        set_local_euler_deg("LeftLowerLeg", 32.0 + 78.0 * s, 0.0, 0.0)
        set_local_euler_deg("RightUpperLeg", 10.0 - 75.0 * s, 0.0, 0.0)
        set_local_euler_deg("RightLowerLeg", 25.0 + 85.0 * s, 0.0, 0.0)
    elif f <= 31:
        u = (f - 20) / 11.0
        s = u * u * (3.0 - 2.0 * u)
        hip_x = 0.04 - 0.02 * s
        hip_y = 0.04 - 0.30 * s
        hip_z = -0.27 - 0.16 * s
        hip_pitch = 10.0 + 74.0 * s
        hip_yaw = 10.0 + 8.0 * s
        hip_roll = -6.0 - 12.0 * s
        spine_pitch = -10.0 + 18.0 * s
        chest_pitch = -15.0 + 22.0 * s
        head_pitch = -36.0 + 20.0 * s
        head_yaw = -12.0 + 42.0 * s
        set_local_euler_deg("RightShoulder", 0.0, 0.0, 15.0 * (1.0 - s))
        set_local_euler_deg("RightUpperArm", -70.0 * s, -15.0 + 40.0 * s, -55.0 * (1.0 - s))
        set_local_euler_deg("RightLowerArm", 0.0, 0.0, -80.0 + 45.0 * s)
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, -30.0 * (1.0 - s))
        set_local_euler_deg("LeftUpperArm", -55.0 - 20.0 * s, -25.0 * s, 60.0 * (1.0 - s))
        set_local_euler_deg("LeftLowerArm", 0.0, 0.0, 20.0 - 55.0 * s)
        set_local_euler_deg("LeftUpperLeg", -70.0 + 55.0 * s, 0.0, -8.0 * s)
        set_local_euler_deg("LeftLowerLeg", 110.0 - 85.0 * s, 0.0, 0.0)
        set_local_euler_deg("RightUpperLeg", -65.0 + 52.0 * s, 0.0, 8.0 * s)
        set_local_euler_deg("RightLowerLeg", 110.0 - 88.0 * s, 0.0, 0.0)
    else:
        u = (f - 31) / 6.0
        bounce = math.sin(u * math.pi) * (1.0 - u) * 0.025
        hip_x = 0.02
        hip_y = -0.26
        hip_z = -0.43 + bounce
        hip_pitch = 84.0 - 3.0 * bounce * 40.0
        hip_yaw = 18.0
        hip_roll = -18.0
        spine_pitch = 8.0
        chest_pitch = 7.0
        head_pitch = -15.0
        head_yaw = 30.0
        set_local_euler_deg("RightShoulder", 0.0, 0.0, 0.0)
        set_local_euler_deg("RightUpperArm", -70.0, 25.0, 0.0)
        set_local_euler_deg("RightLowerArm", 0.0, 0.0, 35.0)
        set_local_euler_deg("LeftShoulder", 0.0, 0.0, 0.0)
        set_local_euler_deg("LeftUpperArm", -75.0, -25.0, 0.0)
        set_local_euler_deg("LeftLowerArm", 0.0, 0.0, -35.0)
        set_local_euler_deg("LeftUpperLeg", -14.0, 0.0, -8.0)
        set_local_euler_deg("LeftLowerLeg", 25.0, 0.0, 0.0)
        set_local_euler_deg("RightUpperLeg", -12.0, 0.0, 8.0)
        set_local_euler_deg("RightLowerLeg", 22.0, 0.0, 0.0)

    set_hips_transform((hip_x, hip_y, hip_z), pitch_deg=hip_pitch, yaw_deg=hip_yaw, roll_deg=hip_roll)
    set_local_euler_deg("Spine", spine_pitch, 0.0, 0.0)
    set_local_euler_deg("Chest", chest_pitch, 0.0, 0.0)
    set_local_euler_deg("Head", head_pitch, head_yaw, 0.0)
    keyframe_all_bones(f, prev_q)

# 11. Sit (Frames 1..49)
act_sit = create_new_action("Sit")
prev_q = {}
for f in range(1, 50):
    begin_frame_pose(f)
    t = (f - 1) / 48.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang)

    set_hips_world_offset((0.0, 0.14, -0.49 + 0.003 * breath), pitch_deg=-10.0 + 0.8 * breath, yaw_deg=0.0, roll_deg=0.0)
    set_local_euler_deg("Spine", 14.0 - 1.2 * breath, 0.0, 0.0)
    set_local_euler_deg("Chest", 11.0 - 1.5 * breath, 0.0, 0.0)
    set_local_euler_deg("Neck", -4.0 + 0.8 * breath, 0.0, 0.0)
    set_local_euler_deg("Head", -3.0 + 1.8 * breath, 1.5 * math.sin(ang), 2.2 + 1.0 * math.cos(ang))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.15, 0.115),
                    pole_world_vec=(0.19, -0.22, 1.0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, -0.15, 0.115),
                    pole_world_vec=(-0.19, -0.22, 1.0))

    swing_bone_zero_twist(rig_obj.pose.bones["LeftFoot"], Vector((0.175, -0.35, 0.035)))
    swing_bone_zero_twist(rig_obj.pose.bones["RightFoot"], Vector((-0.175, -0.35, 0.035)))

    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.11, -0.24, 0.44 + 0.006 * breath),
                    pole_world_vec=(0.52, 0.12, -0.32))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.11, -0.25, 0.43 + 0.006 * breath),
                    pole_world_vec=(-0.52, 0.12, -0.32))

    swing_bone_zero_twist(rig_obj.pose.bones["LeftHand"], Vector((-0.03, -0.28, 0.40 + 0.006 * breath)))
    swing_bone_zero_twist(rig_obj.pose.bones["RightHand"], Vector((0.03, -0.29, 0.39 + 0.006 * breath)))

    keyframe_all_bones(f, prev_q)

# Set active action back to Idle at frame 1
rig_obj.animation_data.action = act_idle
if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
    rig_obj.animation_data.action_slot = act_idle.slots[0]
scene.frame_set(1)
bpy.context.view_layer.update()

# ==============================================================================
# 9. EXPORT .FBX AND .BLEND + RENDER PREVIEW
# ==============================================================================
out_dir = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels"
blend_path = os.path.join(out_dir, "Chibi_BlondeBrawler_Source.blend")
fbx_path = os.path.join(out_dir, "Chibi_BlondeBrawler_Rigged.fbx")
preview_path = os.path.join(out_dir, "Chibi_BlondeBrawler_Preview.png")

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
print("SUCCESSFULLY EXPORTED CHIBI BLONDE BRAWLER FBX AND BLEND:", fbx_path)

# Render high-contrast studio preview image
world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg_node = world.node_tree.nodes.get("Background")
if bg_node:
    bg_node.inputs[0].default_value = (0.18, 0.19, 0.22, 1.0)
    bg_node.inputs[1].default_value = 1.2

cam_data = bpy.data.cameras.new("PreviewCam")
cam_data.lens = 55
cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
bpy.context.collection.objects.link(cam_obj)
cam_obj.location = Vector((-0.65, -4.15, 1.22))
direction = Vector((0.0, 0.0, 1.15)) - cam_obj.location
cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam_obj

key_light_data = bpy.data.lights.new(name="KeyLight", type='SUN')
key_light_data.energy = 3.4
key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
bpy.context.collection.objects.link(key_light)
key_light.rotation_euler = Euler((math.radians(52), math.radians(-18), math.radians(-22)), 'XYZ')

fill_light_data = bpy.data.lights.new(name="FillLight", type='SUN')
fill_light_data.energy = 1.8
fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
bpy.context.collection.objects.link(fill_light)
fill_light.rotation_euler = Euler((math.radians(68), math.radians(28), math.radians(32)), 'XYZ')

scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)
print("SUCCESSFULLY RENDERED PREVIEW:", preview_path)

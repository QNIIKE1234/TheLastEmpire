import bpy
import bmesh
import math
import os
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler

OUT_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels"
TEX_DIR = os.path.join(OUT_DIR, "Textures")
os.makedirs(TEX_DIR, exist_ok=True)


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = 24
    return scene


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / max(1e-7, (edge1 - edge0)), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def alpha_blend(canvas, mask, rgb):
    m = np.clip(mask, 0.0, 1.0)[..., None]
    col = np.array(rgb, dtype=np.float32)[None, None, :]
    canvas[..., :3] = canvas[..., :3] * (1.0 - m) + col * m


def polyline_sdf(u, v, pts, widths):
    min_d = np.full_like(u, 1e6, dtype=np.float32)
    for i in range(len(pts) - 1):
        x0, y0 = pts[i]
        x1, y1 = pts[i + 1]
        w0 = widths[i]
        w1 = widths[i + 1]
        dx = x1 - x0
        dy = y1 - y0
        l2 = max(1e-9, dx * dx + dy * dy)
        t = np.clip(((u - x0) * dx + (v - y0) * dy) / l2, 0.0, 1.0)
        px = x0 + dx * t
        py = y0 + dy * t
        dist = np.hypot(u - px, v - py)
        rad = w0 + (w1 - w0) * t
        min_d = np.minimum(min_d, dist - rad)
    return min_d


def save_rgba_array_to_png(arr_srgb, filepath, img_name):
    H, W, _ = arr_srgb.shape
    img = bpy.data.images.new(name=img_name, width=W, height=H, alpha=True, float_buffer=False)
    img.colorspace_settings.name = 'sRGB'
    flat = np.clip(arr_srgb, 0.0, 1.0).astype(np.float32).ravel()
    img.pixels.foreach_set(flat)
    img.filepath_raw = filepath
    img.file_format = 'PNG'
    img.save()
    img.reload()
    try:
        img.pack()
    except Exception:
        pass
    return img


def generate_hair_gradient_texture(filepath, img_name, root_rgb, mid_rgb, tip_rgb, hi_rgb, band_v=0.54, band_strength=0.38):
    W, H = 512, 512
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    root_col = np.array(root_rgb, dtype=np.float32)
    mid_col  = np.array(mid_rgb, dtype=np.float32)
    tip_col  = np.array(tip_rgb, dtype=np.float32)
    hi_col   = np.array(hi_rgb, dtype=np.float32)

    t1 = smoothstep(0.0, 0.44, V)[..., None]
    t2 = smoothstep(0.44, 1.0, V)[..., None]
    col = root_col * (1.0 - t1) + mid_col * t1
    col = col * (1.0 - t2) + tip_col * t2

    strand_wave = (
        0.50 * np.sin(U * 2.0 * np.pi * 6.0)
        + 0.30 * np.sin(U * 2.0 * np.pi * 14.0 + 1.2)
        + 0.20 * np.sin(U * 2.0 * np.pi * 27.0 + 0.5)
    )
    col = col * (1.0 + 0.075 * strand_wave[..., None])

    band_center = band_v + 0.028 * np.sin(U * 2.0 * np.pi * 5.0)
    band = np.exp(-np.power((V - band_center) / 0.078, 2.0)) * (0.55 + 0.45 * np.clip(strand_wave + 0.3, 0.0, 1.0))
    col = col * (1.0 - band_strength * band[..., None]) + hi_col * (band_strength * band[..., None])

    edge_shade = 1.0 - 0.16 * np.power(np.abs(U - 0.5) * 2.0, 2.2)
    col = col * edge_shade[..., None]

    canvas = np.ones((H, W, 4), dtype=np.float32)
    canvas[..., :3] = np.clip(col, 0.0, 1.0)
    return save_rgba_array_to_png(canvas, filepath, img_name)


def make_mat(name, rgba, roughness=0.90, metallic=0.0, specular=0.04, image=None):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    lin_rgba = (srgb_to_lin(rgba[0]), srgb_to_lin(rgba[1]), srgb_to_lin(rgba[2]), rgba[3])
    if bsdf:
        if image is not None:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = image
            tex.extension = 'EXTEND'
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        else:
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


def ensure_uv_layer(mesh, default_uv=(0.5, 0.5)):
    if not mesh.uv_layers:
        uv_layer = mesh.uv_layers.new(name="UVMap")
        for loop in uv_layer.data:
            loop.uv = default_uv


def assign_single_material(obj, mat, default_uv=(0.5, 0.5)):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.material_index = 0
        poly.use_smooth = True
    ensure_uv_layer(obj.data, default_uv=default_uv)
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


def create_rounded_box(name, center, size, mat, rot_euler=(0.0, 0.0, 0.0), bevel_width=0.014, segments=3):
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


def create_ellipsoid(name, center, radii, mat, rot_euler=(0.0, 0.0, 0.0), u_seg=28, v_seg=20, uv_v=0.45):
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
    return assign_single_material(o, mat, default_uv=(0.5, uv_v))


def create_torus_ring(name, center, major_r, minor_r, mat, rot_euler=(0.0, 0.0, 0.0), maj_seg=32, min_seg=14, uv_v=0.5):
    bm = bmesh.new()
    rot_mat = Euler(rot_euler, 'XYZ').to_matrix()
    rings = []
    for i in range(maj_seg):
        a = (2.0 * math.pi * i) / maj_seg
        ca, sa = math.cos(a), math.sin(a)
        ring = []
        for j in range(min_seg):
            b = (2.0 * math.pi * j) / min_seg
            cb, sb = math.cos(b), math.sin(b)
            r = major_r + minor_r * cb
            local = Vector((r * ca, r * sa, minor_r * sb))
            ring.append(bm.verts.new(Vector(center) + rot_mat @ local))
        rings.append(ring)
    for i in range(maj_seg):
        ni = (i + 1) % maj_seg
        for j in range(min_seg):
            nj = (j + 1) % min_seg
            bm.faces.new((rings[i][j], rings[ni][j], rings[ni][nj], rings[i][nj]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return assign_single_material(o, mat, default_uv=(0.5, uv_v))


def create_ribbon_hair_strand(name, points_widths, mat, segments=18, substeps=4, v_start=0.06, v_end=0.98, radial_center=Vector((0.0, 0.02, 1.70))):
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
    uv_layer = bm.loops.layers.uv.new("UVMap")
    N = segments
    vert_rings = []
    ring_vs = []
    total_rings = len(dense_pts)

    for idx, (pt, wu, tv, roll_rad) in enumerate(dense_pts):
        if idx < total_rings - 1:
            tangent = (dense_pts[idx + 1][0] - pt).normalized()
        else:
            tangent = (pt - dense_pts[idx - 1][0]).normalized()
        radial = (pt - radial_center)
        radial = radial - tangent * radial.dot(tangent)
        if radial.length < 1e-5:
            radial = Vector((0.0, -1.0, 0.0))
        else:
            radial.normalize()
        side_vec = tangent.cross(radial).normalized()
        ca, sa = math.cos(roll_rad), math.sin(roll_rad)
        u_dir = side_vec * ca + radial * sa
        v_dir = -side_vec * sa + radial * ca

        ring = []
        for i in range(N):
            ang = (2.0 * math.pi * i) / N
            cu = math.cos(ang)
            sv = math.sin(ang)
            edge_pinch = 1.0 - 0.18 * (cu * cu)
            arch = (1.0 - cu * cu) * 0.48 * tv
            p = pt + u_dir * (cu * wu) + v_dir * (sv * tv * edge_pinch + arch)
            ring.append(bm.verts.new(p))
        vert_rings.append(ring)
        frac = idx / max(1, total_rings - 1)
        ring_vs.append(v_start + (v_end - v_start) * frac)

    for r_idx in range(total_rings - 1):
        r0 = vert_rings[r_idx]
        r1 = vert_rings[r_idx + 1]
        v0 = ring_vs[r_idx]
        v1 = ring_vs[r_idx + 1]
        for i in range(N):
            ni = (i + 1) % N
            u0 = 0.5 + 0.48 * math.cos((2.0 * math.pi * i) / N)
            u1 = 0.5 + 0.48 * math.cos((2.0 * math.pi * ni) / N)
            f = bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))
            f.loops[0][uv_layer].uv = (u0, v0)
            f.loops[1][uv_layer].uv = (u1, v0)
            f.loops[2][uv_layer].uv = (u1, v1)
            f.loops[3][uv_layer].uv = (u0, v1)

    r0 = vert_rings[0]
    c0 = bm.verts.new(sum((v.co for v in r0), Vector((0, 0, 0))) / N)
    for i in range(N):
        f = bm.faces.new((r0[(i + 1) % N], r0[i], c0))
        for lp in f.loops:
            lp[uv_layer].uv = (0.5, v_start)

    rl = vert_rings[-1]
    cl = bm.verts.new(sum((v.co for v in rl), Vector((0, 0, 0))) / N)
    for i in range(N):
        f = bm.faces.new((rl[i], rl[(i + 1) % N], cl))
        for lp in f.loops:
            lp[uv_layer].uv = (0.5, v_end)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m = bpy.data.meshes.new(name)
    bm.to_mesh(m)
    bm.free()
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    o.data.materials.clear()
    o.data.materials.append(mat)
    for poly in o.data.polygons:
        poly.material_index = 0
        poly.use_smooth = True
    return o


def create_anime_head_shell(hz, mat_face_tex, mat_skin, style="female_v"):
    """
    Creates a sculpted Avatar-Star anime Chibi head mesh with clean cylindrical-planar front UVs.
    style:
      - 'female_v'   : sleek anime V-chin (TacticalGirl, PunkGirl)
      - 'cute_round' : soft full cheeks + cute gentle chin (CurlyGirl)
      - 'male_sleek' : sharp youthful anime male jaw (BlueOperative)
      - 'male_strong': bold angular heroic jawline (HeavyStriker)
    """
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=48, radius=1.0)
    uv_layer = bm.loops.layers.uv.new("UVMap")

    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        if style == "male_strong":
            cheek_bulge = math.exp(-((z + 0.14) / 0.45) ** 2) * 0.016
            if z < -0.25:
                t_chin = min(1.0, (-z - 0.25) / 0.75)
                jaw_taper_x = 1.0 - 0.20 * (t_chin ** 1.6)
                jaw_taper_y = 1.0 - 0.14 * (t_chin ** 1.4)
            else:
                jaw_taper_x, jaw_taper_y = 1.0, 1.0
            rx = 0.376 * jaw_taper_x + cheek_bulge
            ry = 0.352 * jaw_taper_y + cheek_bulge * 0.4
            rz = 0.366
        elif style == "male_sleek":
            cheek_bulge = math.exp(-((z + 0.14) / 0.42) ** 2) * 0.014
            if z < -0.18:
                t_chin = min(1.0, (-z - 0.18) / 0.82)
                jaw_taper_x = 1.0 - 0.34 * (t_chin ** 1.35)
                jaw_taper_y = 1.0 - 0.22 * (t_chin ** 1.30)
            else:
                jaw_taper_x, jaw_taper_y = 1.0, 1.0
            rx = 0.372 * jaw_taper_x + cheek_bulge
            ry = 0.348 * jaw_taper_y + cheek_bulge * 0.45
            rz = 0.362
        elif style == "cute_round":
            cheek_bulge = math.exp(-((z + 0.18) / 0.40) ** 2) * 0.030
            if z < -0.24:
                t_chin = min(1.0, (-z - 0.24) / 0.76)
                jaw_taper_x = 1.0 - 0.28 * (t_chin ** 1.35)
                jaw_taper_y = 1.0 - 0.20 * (t_chin ** 1.30)
            else:
                jaw_taper_x, jaw_taper_y = 1.0, 1.0
            rx = 0.382 * jaw_taper_x + cheek_bulge
            ry = 0.352 * jaw_taper_y + cheek_bulge * 0.55
            rz = 0.356
        else:  # female_v
            cheek_bulge = math.exp(-((z + 0.16) / 0.42) ** 2) * 0.022
            if z < -0.18:
                t_chin = min(1.0, (-z - 0.18) / 0.82)
                jaw_taper_x = 1.0 - 0.36 * (t_chin ** 1.35)
                jaw_taper_y = 1.0 - 0.24 * (t_chin ** 1.30)
            else:
                jaw_taper_x, jaw_taper_y = 1.0, 1.0
            rx = 0.378 * jaw_taper_x + cheek_bulge
            ry = 0.348 * jaw_taper_y + cheek_bulge * 0.55
            rz = 0.358

        vx = x * rx
        vy = y * ry
        vz = hz + z * rz

        if y < -0.25:
            front_f = (-y - 0.25) / 0.75
            vy += 0.018 * front_f * (1.0 - abs(x) * 0.5)

        if y < 0.0 and z < -0.45:
            t_pt = min(1.0, (-z - 0.45) / 0.55)
            vy -= (0.022 if style == "male_strong" else 0.016) * t_pt * max(0.0, -y) * (1.0 - abs(x) * (0.4 if style == "male_strong" else 0.9))

        if y < -0.5:
            d_n = math.hypot(x / 0.11, (z + 0.18) / 0.12)
            if d_n < 1.0:
                vy -= (0.016 if "male" in style else 0.011) * ((1.0 - d_n * d_n) ** 1.8)

        v.co = Vector((vx, vy, vz))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    for f in bm.faces:
        center = f.calc_center_median()
        if center.y <= 0.03:
            f.material_index = 0
            for lp in f.loops:
                co = lp.vert.co
                theta = math.atan2(co.x, max(0.01, -co.y))
                u = 0.5 + (theta / (0.50 * math.pi)) * 0.46
                v_uv = 0.5 + ((co.z - hz) / 0.365) * 0.44
                lp[uv_layer].uv = (max(0.01, min(0.99, u)), max(0.01, min(0.99, v_uv)))
        else:
            f.material_index = 1
            for lp in f.loops:
                lp[uv_layer].uv = (0.04, 0.04)

    m_head = bpy.data.meshes.new("Chibi_Base_Head")
    bm.to_mesh(m_head)
    bm.free()
    head_base = bpy.data.objects.new("Chibi_Base_Head", m_head)
    bpy.context.collection.objects.link(head_base)
    head_base.data.materials.append(mat_face_tex)
    head_base.data.materials.append(mat_skin)
    for poly in head_base.data.polygons:
        poly.use_smooth = True
    return head_base


def create_hair_crown_shell(hz, mat_hair, rx_base=0.402, ry_base=0.382, rz_base=0.380, cutout_z=0.44, y_shift=0.028, z_shift=0.025):
    bm_hcap = bmesh.new()
    bmesh.ops.create_uvsphere(bm_hcap, u_segments=48, v_segments=32, radius=1.0)
    uv_h = bm_hcap.loops.layers.uv.new("UVMap")
    for v in bm_hcap.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        rx = rx_base + 0.014 * max(0.0, z)
        ry = ry_base + 0.015 * max(0.0, y)
        rz = rz_base
        if y < -0.14 and z < cutout_z:
            push = min(1.0, (-y - 0.14) / 0.86) * min(1.0, (cutout_z - z) / 0.65)
            ry *= (1.0 - 0.34 * push)
            rx *= (1.0 - 0.12 * push)
        v.co = Vector((x * rx, y * ry + y_shift, hz + z_shift + z * rz))
    bmesh.ops.recalc_face_normals(bm_hcap, faces=bm_hcap.faces)
    for f in bm_hcap.faces:
        for lp in f.loops:
            co = lp.vert.co
            u_val = 0.5 + 0.45 * math.sin(math.atan2(co.x, co.y) * 2.0)
            v_val = max(0.08, min(0.88, 0.18 + (co.z - (hz - 0.32)) / 0.72 * 0.65))
            lp[uv_h].uv = (u_val, v_val)
    m_hcap = bpy.data.meshes.new("Hair_CrownShell")
    bm_hcap.to_mesh(m_hcap)
    bm_hcap.free()
    hair_crown = bpy.data.objects.new("Hair_CrownShell", m_hcap)
    bpy.context.collection.objects.link(hair_crown)
    hair_crown.data.materials.append(mat_hair)
    for poly in hair_crown.data.polygons:
        poly.use_smooth = True
    return hair_crown


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


# ==============================================================================
# SHARED 18-BONE CHIBI RIG + SKINNING + ALL 11 ANIMATIONS + EXPORT + PREVIEW
# ==============================================================================
BONE_DEFS = [
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


def _point_to_segment_dist(p, a, b):
    ab = b - a
    len_sq = ab.length_squared
    if len_sq < 1e-8:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / len_sq))
    proj = a + ab * t
    return (p - proj).length


def build_rig_animate_and_export(scene, head_obj, body_obj, base_name):
    arm_data = bpy.data.armatures.new("Chibi_Base_Rig")
    rig_obj = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
    bpy.context.collection.objects.link(rig_obj)
    bpy.context.view_layer.objects.active = rig_obj
    bpy.ops.object.mode_set(mode='EDIT')

    edit_bones = {}
    for bname, h, t, pname, roll_vec in BONE_DEFS:
        eb = arm_data.edit_bones.new(bname)
        eb.head = Vector(h)
        eb.tail = Vector(t)
        eb.align_roll(Vector(roll_vec))
        if pname and pname in edit_bones:
            eb.parent = edit_bones[pname]
            eb.use_connect = False
        edit_bones[bname] = eb

    bpy.ops.object.mode_set(mode='OBJECT')

    def bind_mesh_to_rig(mesh_obj, is_head=False):
        mesh_obj.parent = rig_obj
        mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
        mod.object = rig_obj
        mod.use_deform_preserve_volume = True

        vgroups = {}
        for bname, _, _, _, _ in BONE_DEFS:
            vgroups[bname] = mesh_obj.vertex_groups.new(name=bname)

        bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in BONE_DEFS if bname != "Root"]

        for v in mesh_obj.data.vertices:
            co = v.co
            if is_head:
                is_near_neck = (co.z < 1.44) and (abs(co.x) < 0.22) and (co.y < 0.18) and (co.y > -0.22)
                neck_w = max(0.0, min(0.20, (1.44 - co.z) * 1.2)) if is_near_neck else 0.0
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

                d = _point_to_segment_dist(co, h, t)
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

    bind_mesh_to_rig(head_obj, is_head=True)
    bind_mesh_to_rig(body_obj, is_head=False)

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
        pb.matrix = Matrix.LocRotScale(world_loc, new_world_rot, Vector((1, 1, 1)))
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
        plane_pole = pole - dir_norm * pole.dot(dir_norm)
        plane_pole = Vector((0, 1, 0)) if plane_pole.length_squared < 1e-6 else plane_pole.normalized()
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

    # 1. Idle (Frames 1..49)
    act_idle = create_new_action("Idle")
    prev_q = {}
    for f in range(1, 50):
        begin_frame_pose(f)
        ang = ((f - 1) / 48.0) * 2.0 * math.pi
        breath = math.sin(ang)
        set_hips_transform((0.0, 0.0, 0.012 * breath), 1.2 * breath, 1.5 * math.sin(ang * 0.5), 0.0)
        set_local_euler_deg("Spine", 1.5 - 1.2 * breath, 0.0, 0.0)
        set_local_euler_deg("Chest", -2.0 * breath, 0.0, 0.0)
        set_local_euler_deg("Head", 1.8 * breath, 2.5 * math.sin(ang), 1.2 * math.cos(ang))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.32, -0.04, 0.79 + 0.010 * breath), (0.35, 0.65, -0.1))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.32, -0.04, 0.79 + 0.010 * breath), (-0.35, 0.65, -0.1))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.152, -0.01, 0.11), (0.16, -1.0, 0.1))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.152, -0.01, 0.11), (-0.16, -1.0, 0.1))
        keyframe_all_bones(f, prev_q)

    # 2. Walk (Frames 1..25)
    create_new_action("Walk")
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        ang = ((f - 1) / 24.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        bounce = abs(s) * 0.038
        set_hips_transform((0.018 * s, 0.0, bounce), 4.0, -7.0 * s, 3.0 * c)
        set_local_euler_deg("Spine", 3.0, 4.0 * s, 0.0)
        set_local_euler_deg("Chest", 3.0, 8.0 * s, -2.0 * c)
        set_local_euler_deg("Head", -4.0, -5.0 * s, 0.0)
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.32, -0.04 + 0.18 * s, 0.81 + 0.04 * abs(s)), (0.40, 0.70, -0.20))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.32, -0.04 - 0.18 * s, 0.81 + 0.04 * abs(s)), (-0.40, 0.70, -0.20))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.152, -0.18 * s, 0.11 + max(0.0, c) * 0.10), (0.16, -1.0, 0.2), (15.0 * s, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.152, 0.18 * s, 0.11 + max(0.0, -c) * 0.10), (-0.16, -1.0, 0.2), (-15.0 * s, 0, 0))
        keyframe_all_bones(f, prev_q)

    # 3. Run (Frames 1..17)
    create_new_action("Run")
    prev_q = {}
    for f in range(1, 18):
        begin_frame_pose(f)
        ang = ((f - 1) / 16.0) * 2.0 * math.pi
        s, c = math.sin(ang), math.cos(ang)
        bounce = (math.sin(ang * 2.0 - math.pi * 0.25) * 0.5 + 0.5) * 0.080
        set_hips_transform((0.025 * s, 0.0, bounce - 0.015), 14.0, -12.0 * s, 4.5 * c)
        set_local_euler_deg("Spine", 8.0, 6.0 * s, 0.0)
        set_local_euler_deg("Chest", 8.0, 12.0 * s, -3.5 * c)
        set_local_euler_deg("Head", -16.0, -8.0 * s, 0.0)
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.33, -0.12 + 0.26 * s, 0.92 - 0.08 * s), (0.45, 0.80, -0.25))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.33, -0.12 - 0.26 * s, 0.92 + 0.08 * s), (-0.45, 0.80, -0.25))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.152, -0.28 * s, 0.12 + max(0.0, c) * 0.20), (0.16, -1.0, 0.3), (28.0 * s, 0, 0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.152, 0.28 * s, 0.12 + max(0.0, -c) * 0.20), (-0.16, -1.0, 0.3), (-28.0 * s, 0, 0))
        keyframe_all_bones(f, prev_q)

    # 4. GunHold (Frames 1..25)
    create_new_action("GunHold")
    prev_q = {}
    for f in range(1, 26):
        begin_frame_pose(f)
        breath = math.sin(((f - 1) / 24.0) * 2.0 * math.pi) * 0.010
        set_hips_transform((0.0, 0.0, breath - 0.015), 4.0, 10.0, 0.0)
        set_local_euler_deg("Spine", 4.0, -5.0, 0.0)
        set_local_euler_deg("Chest", 4.0, -5.0, 0.0)
        set_local_euler_deg("Head", -5.0, 0.0, 0.0)
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.06, -0.38, 1.10 + breath), (-0.40, 0.25, -0.8))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.06, -0.40, 1.09 + breath), (0.40, 0.25, -0.8))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.05, 0.11), (0.16, -1.0, 0.1))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, 0.05, 0.11), (-0.16, -1.0, 0.1))
        keyframe_all_bones(f, prev_q)

    # 5. Shoot (Frames 1..15)
    create_new_action("Shoot")
    prev_q = {}
    for f in range(1, 16):
        begin_frame_pose(f)
        recoil = (f - 1) / 2.0 if f <= 3 else max(-0.08, math.exp(-((f - 3) / 3.8)) * math.cos((f - 3) * 0.22))
        set_hips_transform((0.0, 0.03 * recoil, -0.015 - 0.02 * recoil), 4.0 - 6.0 * recoil, 10.0 + 5.0 * recoil, 0.0)
        set_local_euler_deg("Spine", 4.0 - 7.0 * recoil, -5.0, 0.0)
        set_local_euler_deg("Chest", 4.0 - 9.0 * recoil, -5.0 + 4.0 * recoil, 0.0)
        set_local_euler_deg("Head", -5.0 - 8.0 * recoil, 0.0, 0.0)
        kick_y, kick_z = 0.11 * recoil, 0.15 * recoil
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.06, -0.38 + kick_y, 1.10 + kick_z), (-0.42, 0.25, -0.75), (-25.0 * recoil, 0, 0))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.06, -0.40 + kick_y, 1.09 + kick_z), (0.42, 0.25, -0.75), (-25.0 * recoil, 0, 0))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.05, 0.11), (0.16, -1.0, 0.1))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, 0.05, 0.11), (-0.16, -1.0, 0.1))
        keyframe_all_bones(f, prev_q)

    # 6. Slash1 (Frames 1..19)
    create_new_action("Slash1")
    prev_q = {}
    for f in range(1, 20):
        begin_frame_pose(f)
        if f <= 5:
            swing = -((f - 1) / 4.0) * 0.55
        elif f <= 11:
            u = (f - 5) / 6.0
            swing = -0.55 + 1.55 * (3 * u * u - 2 * u * u * u)
        else:
            u = (f - 11) / 8.0
            swing = 1.0 - (3 * u * u - 2 * u * u * u)
        yaw_body = -42.0 * swing
        set_hips_transform((0.0, -0.08 * max(0.0, swing), -0.05 * abs(swing)), 12.0 * abs(swing), yaw_body * 0.45, -4.0 * swing)
        set_local_euler_deg("Spine", 8.0 * abs(swing), yaw_body * 0.30, 0.0)
        set_local_euler_deg("Chest", 6.0 * abs(swing), yaw_body * 0.35, 0.0)
        set_local_euler_deg("Head", -8.0 * abs(swing), -yaw_body * 0.45, 0.0)
        arc_ang = swing * 1.65
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.16 + 0.56 * swing, -0.46 * math.cos(arc_ang * 0.65) + 0.06, 1.10 - 0.10 * swing), (-0.45, 0.30, -0.4), (0, 0, -35.0 * swing))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.32 + 0.08 * swing, -0.04 + 0.14 * swing, 0.84), (0.45, 0.50, -0.4))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.12 * max(0.0, swing), 0.11), (0.16, -1.0, 0.2))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, 0.08 * max(0.0, swing), 0.11), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # 7. Slash2 (Frames 1..19)
    create_new_action("Slash2")
    prev_q = {}
    for f in range(1, 20):
        begin_frame_pose(f)
        if f <= 5:
            swing = -0.60 * ((f - 1) / 4.0)
        elif f <= 11:
            u = (f - 5) / 6.0
            swing = -0.60 + 1.60 * (3 * u * u - 2 * u * u * u)
        else:
            u = (f - 11) / 8.0
            swing = 1.0 - (3 * u * u - 2 * u * u * u)
        yaw_body = 45.0 * swing
        crouch = -0.05 * (1.0 - max(0.0, swing)) * abs(swing)
        set_hips_transform((0.0, -0.06 * abs(swing), crouch), 8.0 - 6.0 * swing, yaw_body * 0.45, 5.0 * swing)
        set_local_euler_deg("Spine", 6.0 - 5.0 * swing, yaw_body * 0.30, 0.0)
        set_local_euler_deg("Chest", 4.0 - 8.0 * swing, yaw_body * 0.35, 0.0)
        set_local_euler_deg("Head", -4.0 + 6.0 * swing, -yaw_body * 0.45, 0.0)
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.12 - 0.48 * swing, -0.44 * math.cos(swing * 0.9) + 0.05, 1.08 + 0.30 * swing), (-0.42, 0.35, -0.4), (20.0 * swing, 0, 40.0 * swing))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.32 - 0.08 * swing, -0.12 + 0.10 * swing, 0.88), (0.45, 0.45, -0.4))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, 0.06 * max(0.0, swing), 0.11), (0.16, -1.0, 0.2))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, -0.12 * max(0.0, swing), 0.11), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # 8. Slash3 (Frames 1..23)
    create_new_action("Slash3")
    prev_q = {}
    for f in range(1, 24):
        begin_frame_pose(f)
        if f <= 8:
            u = (f - 1) / 7.0
            slam = -(3 * u * u - 2 * u * u * u)
            hip_z = 0.20 * math.sin(u * math.pi * 0.85)
        elif f <= 13:
            u = (f - 8) / 5.0
            slam = -1.0 + 2.0 * u * u
            hip_z = 0.10 * (1.0 - u) - 0.16 * u
        elif f <= 16:
            slam, hip_z = 1.0, -0.16
        else:
            u = (f - 16) / 7.0
            s = 3 * u * u - 2 * u * u * u
            slam = 1.0 - s
            hip_z = -0.16 * (1.0 - s)
        pitch_torso = 26.0 * slam
        set_hips_transform((0.0, -0.10 * max(0.0, slam), hip_z), pitch_torso * 0.6, 0.0, 0.0)
        set_local_euler_deg("Spine", pitch_torso * 0.5, 0.0, 0.0)
        set_local_euler_deg("Chest", pitch_torso * 0.6, 0.0, 0.0)
        set_local_euler_deg("Head", -pitch_torso * 0.4, 0.0, 0.0)
        hand_y = -0.32 + 0.24 * (-slam) if slam < 0 else -0.32 - 0.18 * slam
        hand_z = 1.10 + 0.60 * (-slam) if slam < 0 else 1.10 - 0.52 * slam
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.08, hand_y, hand_z), (-0.45, 0.25, -0.3))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.08, hand_y, hand_z), (0.45, 0.25, -0.3))
        foot_lift = max(0.0, hip_z * 0.65)
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.12 * max(0.0, slam), 0.11 + foot_lift), (0.16, -1.0, 0.2))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, 0.10 * max(0.0, slam), 0.11 + foot_lift), (-0.16, -1.0, 0.2))
        keyframe_all_bones(f, prev_q)

    # 9. DashRoll (Frames 1..21)
    create_new_action("DashRoll")
    prev_q = {}
    for f in range(1, 22):
        begin_frame_pose(f)
        u = (f - 1) / 20.0
        s = 3 * u * u - 2 * u * u * u
        tuck = math.sin(u * math.pi)
        set_hips_transform((0.0, -0.12 * tuck, -0.22 * tuck), 360.0 * s, 0.0, 0.0)
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
    create_new_action("Death")
    prev_q = {}
    for f in range(1, 38):
        begin_frame_pose(f)
        if f <= 8:
            u = (f - 1) / 7.0
            s = 3 * u * u - 2 * u * u * u
            hip_x, hip_y, hip_z = 0.02 * s, 0.10 * s, -0.05 * s
            hip_pitch, hip_yaw, hip_roll = -18.0 * s, -15.0 * s, 6.0 * s
            spine_pitch, chest_pitch, head_pitch, head_yaw = -16.0 * s, -20.0 * s, -26.0 * s, 18.0 * s
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
            hip_x, hip_y, hip_z = 0.02 + 0.02 * s, 0.10 - 0.06 * s, -0.05 - 0.22 * s
            hip_pitch, hip_yaw, hip_roll = -18.0 + 28.0 * s, -15.0 + 25.0 * s, 6.0 - 12.0 * s
            spine_pitch, chest_pitch, head_pitch, head_yaw = -16.0 + 6.0 * s, -20.0 + 5.0 * s, -26.0 - 10.0 * s, 18.0 - 30.0 * s
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
            hip_x, hip_y, hip_z = 0.04 - 0.02 * s, 0.04 - 0.30 * s, -0.27 - 0.16 * s
            hip_pitch, hip_yaw, hip_roll = 10.0 + 74.0 * s, 10.0 + 8.0 * s, -6.0 - 12.0 * s
            spine_pitch, chest_pitch, head_pitch, head_yaw = -10.0 + 18.0 * s, -15.0 + 22.0 * s, -36.0 + 20.0 * s, -12.0 + 42.0 * s
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
            hip_x, hip_y, hip_z = 0.02, -0.26, -0.43 + bounce
            hip_pitch, hip_yaw, hip_roll = 84.0 - 3.0 * bounce * 40.0, 18.0, -18.0
            spine_pitch, chest_pitch, head_pitch, head_yaw = 8.0, 7.0, -15.0, 30.0
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
        set_hips_transform((hip_x, hip_y, hip_z), hip_pitch, hip_yaw, hip_roll)
        set_local_euler_deg("Spine", spine_pitch, 0.0, 0.0)
        set_local_euler_deg("Chest", chest_pitch, 0.0, 0.0)
        set_local_euler_deg("Head", head_pitch, head_yaw, 0.0)
        keyframe_all_bones(f, prev_q)

    # 11. Sit (Frames 1..49)
    create_new_action("Sit")
    prev_q = {}
    for f in range(1, 50):
        begin_frame_pose(f)
        ang = ((f - 1) / 48.0) * 2.0 * math.pi
        breath = math.sin(ang)
        set_hips_world_offset((0.0, 0.14, -0.49 + 0.003 * breath), -10.0 + 0.8 * breath, 0.0, 0.0)
        set_local_euler_deg("Spine", 14.0 - 1.2 * breath, 0.0, 0.0)
        set_local_euler_deg("Chest", 11.0 - 1.5 * breath, 0.0, 0.0)
        set_local_euler_deg("Neck", -4.0 + 0.8 * breath, 0.0, 0.0)
        set_local_euler_deg("Head", -3.0 + 1.8 * breath, 1.5 * math.sin(ang), 2.2 + 1.0 * math.cos(ang))
        aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.155, -0.15, 0.115), (0.19, -0.22, 1.0))
        aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.155, -0.15, 0.115), (-0.19, -0.22, 1.0))
        swing_bone_zero_twist(rig_obj.pose.bones["LeftFoot"], Vector((0.175, -0.35, 0.035)))
        swing_bone_zero_twist(rig_obj.pose.bones["RightFoot"], Vector((-0.175, -0.35, 0.035)))
        aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.11, -0.24, 0.44 + 0.006 * breath), (0.52, 0.12, -0.32))
        aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand", (-0.11, -0.25, 0.43 + 0.006 * breath), (-0.52, 0.12, -0.32))
        swing_bone_zero_twist(rig_obj.pose.bones["LeftHand"], Vector((-0.03, -0.28, 0.40 + 0.006 * breath)))
        swing_bone_zero_twist(rig_obj.pose.bones["RightHand"], Vector((0.03, -0.29, 0.39 + 0.006 * breath)))
        keyframe_all_bones(f, prev_q)

    rig_obj.animation_data.action = act_idle
    if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
        rig_obj.animation_data.action_slot = act_idle.slots[0]
    scene.frame_set(1)
    bpy.context.view_layer.update()

    blend_path = os.path.join(OUT_DIR, f"{base_name}_Source.blend")
    fbx_path = os.path.join(OUT_DIR, f"{base_name}_Rigged.fbx")
    preview_path = os.path.join(OUT_DIR, f"{base_name}_Preview.png")
    preview_back_path = os.path.join(OUT_DIR, f"{base_name}_Preview_Back.png")

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
        path_mode='COPY',
        embed_textures=True
    )

    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    print(f"SUCCESSFULLY EXPORTED {base_name} FBX AND BLEND:", fbx_path)

    # Studio Preview Render (Front-3/4 & Back-3/4)
    world = bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    bg_node = world.node_tree.nodes.get("Background")
    if bg_node:
        bg_node.inputs[0].default_value = (0.17, 0.18, 0.22, 1.0)
        bg_node.inputs[1].default_value = 1.35

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 58
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    key_light_data = bpy.data.lights.new(name="KeyLight", type='SUN')
    key_light_data.energy = 3.2
    key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
    bpy.context.collection.objects.link(key_light)
    key_light.rotation_euler = Euler((math.radians(52), math.radians(-16), math.radians(-20)), 'XYZ')

    fill_light_data = bpy.data.lights.new(name="FillLight", type='SUN')
    fill_light_data.energy = 2.0
    fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
    bpy.context.collection.objects.link(fill_light)
    fill_light.rotation_euler = Euler((math.radians(65), math.radians(28), math.radians(34)), 'XYZ')

    scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900

    cam_obj.location = Vector((-0.55, -4.35, 1.28))
    cam_obj.rotation_euler = (Vector((0.02, 0.0, 1.18)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = preview_path
    bpy.ops.render.render(write_still=True)

    cam_obj.location = Vector((1.65, 3.85, 1.48))
    cam_obj.rotation_euler = (Vector((0.02, 0.0, 1.18)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = preview_back_path
    bpy.ops.render.render(write_still=True)

    preview_top_path = os.path.join(OUT_DIR, f"{base_name}_Preview_Top.png")
    cam_obj.location = Vector((0.0, -2.10, 4.15))
    cam_obj.rotation_euler = (Vector((0.0, 0.02, 1.55)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = preview_top_path
    bpy.ops.render.render(write_still=True)
    print(f"SUCCESSFULLY RENDERED {base_name} PREVIEWS:", preview_path, preview_back_path, preview_top_path)

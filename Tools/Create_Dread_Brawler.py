import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector, Euler
import ChibiAnimeKit as kit

scene = kit.reset_scene()

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_DreadBrawler_Face.png")
HAIR_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_DreadBrawler_Hair.png")
UNDERCUT_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_DreadBrawler_Undercut.png")


def generate_dread_brawler_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    # Rich warm brown skin matching portrait
    SKIN_BASE   = (0.640, 0.345, 0.175)
    SKIN_HIGH   = (0.760, 0.460, 0.260)
    SKIN_SHADOW = (0.435, 0.210, 0.095)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE

    # Forehead & jaw cel shading
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.90, V) * 0.28, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.07, V) * 0.28, SKIN_SHADOW)

    # Broad stylized nose with warm top highlight & crisp underside shadow (matches portrait!)
    d_nose_base = np.hypot((U - 0.500) / 0.068, (V - 0.390) / 0.032)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.55, 1.02, d_nose_base)) * 0.72, SKIN_HIGH)
    nose_under = [(0.440, 0.372), (0.500, 0.362), (0.560, 0.372)]
    d_nu = kit.polyline_sdf(U, V, nose_under, [0.0045, 0.0075, 0.0045])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_nu)) * 0.85, (0.18, 0.08, 0.04))

    # Fierce expressive eyes & thick furrowed black eyebrows (direct match to portrait!)
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.145
        cy = 0.478
        ex = (U - cx) * side
        ey = V - cy

        t_x = np.clip((ex + 0.076) / 0.160, 0.0, 1.0)
        y_top = -0.020 + 0.046 * np.sin(np.pi * np.power(t_x, 0.70)) + 0.040 * t_x
        y_bot = -0.026 - 0.024 * np.sin(np.pi * np.power(t_x, 0.85)) + 0.024 * t_x

        in_x = kit.smoothstep(-0.078, -0.070, ex) * (1.0 - kit.smoothstep(0.080, 0.086, ex))
        eye_mask = in_x * kit.smoothstep(-aa, aa, ey - y_bot) * kit.smoothstep(-aa, aa, y_top - ey)
        kit.alpha_blend(canvas, eye_mask, (0.995, 0.995, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.016, 0.0, y_top - ey) * 0.25, (0.68, 0.70, 0.76))

        # Focused intense dark-brown/black iris + crisp white catchlight
        ix_c, iy_c = -0.008, 0.002
        r_iris = np.hypot((ex - ix_c) / 0.038, (ey - iy_c) / 0.042)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(0.88, 1.04, r_iris))
        kit.alpha_blend(canvas, iris_mask, (0.09, 0.05, 0.04))

        r_hi = np.hypot((ex - (ix_c + 0.014)) / 0.012, (ey - (iy_c + 0.014)) / 0.011)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.04, r_hi)), (1.0, 1.0, 1.0))

        # Crisp dark eye outlines
        u_lash = [
            (cx + side * (-0.080), cy - 0.020),
            (cx + side * (-0.020), cy + 0.026),
            (cx + side * ( 0.048), cy + 0.048),
            (cx + side * ( 0.090), cy + 0.040),
        ]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, u_lash, [0.006, 0.011, 0.012, 0.005])), (0.05, 0.03, 0.03))
        l_lash = [
            (cx + side * (-0.060), cy - 0.026),
            (cx + side * ( 0.012), cy - 0.038),
            (cx + side * ( 0.080), cy - 0.004),
        ]
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, l_lash, [0.003, 0.004, 0.003]))) * 0.90, (0.07, 0.04, 0.03))

        # Thick bold V-angled black eyebrows
        brow = [
            (cx + side * (-0.100), cy + 0.024),
            (cx + side * (-0.026), cy + 0.062),
            (cx + side * ( 0.052), cy + 0.098),
            (cx + side * ( 0.114), cy + 0.116),
        ]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, brow, [0.012, 0.020, 0.019, 0.006])), (0.06, 0.04, 0.04))

    # Furrow wrinkle lines between brows
    f1 = [(0.492, 0.505), (0.484, 0.548)]
    f2 = [(0.508, 0.505), (0.516, 0.548)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, f1, [0.003, 0.004]))) * 0.65, SKIN_SHADOW)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, f2, [0.003, 0.004]))) * 0.65, SKIN_SHADOW)

    # Stern determined mouth
    mouth_pts = [(0.415, 0.286), (0.500, 0.302), (0.585, 0.286)]
    d_m = kit.polyline_sdf(U, V, mouth_pts, [0.0035, 0.0060, 0.0035])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_m), (0.16, 0.06, 0.04))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_DreadBrawler_Face.png")


def generate_brawler_undercut_texture():
    """Warm-brown faded undercut temples with 2 crisp diagonal shaved razor lines (matches portrait!)."""
    W, H = 512, 512
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)
    canvas = np.ones((H, W, 4), dtype=np.float32)

    FADE_DARK = (0.20, 0.13, 0.09)
    SKIN_LINE = (0.56, 0.31, 0.16)
    # Vertical fade from dark buzzcut top to warm skin near ears
    fade_t = kit.smoothstep(0.18, 0.72, V)
    for c in range(3):
        canvas[..., c] = SKIN_LINE[c] * (1.0 - fade_t) + FADE_DARK[c] * fade_t

    # 2 clean angled shaved razor-stripe lines matching the portrait temples!
    s1 = [(0.14, 0.56), (0.48, 0.68), (0.86, 0.78)]
    s2 = [(0.16, 0.38), (0.50, 0.49), (0.86, 0.58)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-0.008, 0.008, kit.polyline_sdf(U, V, s1, [0.028, 0.030, 0.026])), SKIN_LINE)
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-0.008, 0.008, kit.polyline_sdf(U, V, s2, [0.026, 0.028, 0.024])), SKIN_LINE)
    return kit.save_rgba_array_to_png(canvas, UNDERCUT_TEX_PATH, "Chibi_DreadBrawler_Undercut.png")


IMG_FACE = generate_dread_brawler_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_DreadBrawler_Hair.png",
    root_rgb=(0.05, 0.04, 0.05),
    mid_rgb=(0.11, 0.10, 0.12),
    tip_rgb=(0.17, 0.15, 0.18),
    hi_rgb=(0.28, 0.25, 0.30),
    band_v=0.52, band_strength=0.28
)
IMG_UNDERCUT = generate_brawler_undercut_texture()

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.640, 0.345, 0.175, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_DREAD  = kit.make_mat("Mat_Hair_Dread",   (1.0, 1.0, 1.0, 1.0), roughness=0.86, specular=0.04, image=IMG_HAIR)
MAT_UNDERCUT    = kit.make_mat("Mat_Undercut",     (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_UNDERCUT)
MAT_VEST_BLUE   = kit.make_mat("Mat_Vest_Blue",    (0.16, 0.28, 0.50, 1.0), roughness=0.80, metallic=0.06, specular=0.06)
MAT_VEST_DARK   = kit.make_mat("Mat_Vest_Dark",    (0.09, 0.15, 0.28, 1.0), roughness=0.84, metallic=0.04, specular=0.05)
MAT_ORANGE_PAD  = kit.make_mat("Mat_Orange_Pad",   (0.95, 0.36, 0.02, 1.0), roughness=0.78, metallic=0.04, specular=0.08)
MAT_SHIRT_BLACK = kit.make_mat("Mat_Shirt_Black",  (0.08, 0.08, 0.10, 1.0), roughness=0.90, specular=0.03)
MAT_OLIVE_STRAP = kit.make_mat("Mat_Olive_Strap",  (0.32, 0.31, 0.20, 1.0), roughness=0.90, specular=0.04)
MAT_GAUNTLET_RD = kit.make_mat("Mat_Gauntlet_Red", (0.82, 0.06, 0.07, 1.0), roughness=0.76, metallic=0.06, specular=0.08)
MAT_WHITE_TRIM  = kit.make_mat("Mat_White_Trim",   (0.94, 0.94, 0.96, 1.0), roughness=0.84, specular=0.04)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver", (0.58, 0.62, 0.68, 1.0), roughness=0.40, metallic=0.72, specular=0.25)


def _sample_catmull_rom(pts, samples_per_seg=3):
    vpts = [Vector(p) for p in pts]
    if len(vpts) < 3:
        return vpts
    out = []
    n = len(vpts)
    for i in range(n - 1):
        p0 = vpts[max(0, i - 1)]
        p1 = vpts[i]
        p2 = vpts[i + 1]
        p3 = vpts[min(n - 1, i + 2)]
        for k in range(samples_per_seg):
            t = k / float(samples_per_seg)
            t2 = t * t
            t3 = t2 * t
            pos = 0.5 * (
                (2.0 * p1) +
                (-p0 + p2) * t +
                (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 +
                (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
            )
            out.append(pos)
    out.append(vpts[-1])
    return out


def add_dread_tube(name, pts, radius, mat):
    """Builds a smooth organically curved dreadlock rope with rounded tip cap."""
    spts = _sample_catmull_rom(pts, samples_per_seg=3)
    bm = bmesh.new()
    uv_layer = bm.loops.layers.uv.new("UVMap")
    segs = 10
    rings = []
    n = len(spts)
    prev_u = None
    for i, p in enumerate(spts):
        t = i / max(1, n - 1)
        if i == 0:
            tang = (spts[1] - p).normalized()
        elif i == n - 1:
            tang = (p - spts[-2]).normalized()
        else:
            tang = (spts[i + 1] - spts[i - 1]).normalized()
        if prev_u is None:
            up_ref = Vector((0, 0, 1)) if abs(tang.z) < 0.9 else Vector((1, 0, 0))
            u_vec = tang.cross(up_ref).normalized()
        else:
            # Parallel transport to prevent twist pinch along curves!
            u_vec = (prev_u - tang * prev_u.dot(tang))
            if u_vec.length < 1e-4:
                up_ref = Vector((0, 0, 1)) if abs(tang.z) < 0.9 else Vector((1, 0, 0))
                u_vec = tang.cross(up_ref)
            u_vec.normalize()
        prev_u = u_vec
        v_vec = tang.cross(u_vec).normalized()
        # Subtle braided twist bulge + smooth rounded taper at tip
        taper = 1.0 if t < 0.84 else max(0.35, 1.0 - (t - 0.84) / 0.16 * 0.65)
        r_mod = radius * (0.94 + 0.08 * math.sin(i * 1.4)) * taper
        ring = []
        for s in range(segs):
            ang = (2.0 * math.pi * s) / segs
            co = p + (u_vec * math.cos(ang) + v_vec * math.sin(ang)) * r_mod
            ring.append(bm.verts.new(co))
        rings.append((ring, t))
    for i in range(n - 1):
        r0, t0 = rings[i]
        r1, t1 = rings[i + 1]
        for s in range(segs):
            sn = (s + 1) % segs
            f = bm.faces.new([r0[s], r0[sn], r1[sn], r1[s]])
            f.smooth = True
            for lp, tv in zip(f.loops, (t0, t0, t1, t1)):
                lp[uv_layer].uv = (0.5, tv)
    # Clean closed rounded tip cap with UVs
    tip_dir = (spts[-1] - spts[-2]).normalized()
    tip_c = bm.verts.new(spts[-1] + tip_dir * (radius * 0.35))
    r_last = rings[-1][0]
    for s in range(segs):
        sn = (s + 1) % segs
        f = bm.faces.new([r_last[s], r_last[sn], tip_c])
        f.smooth = True
        for lp in f.loops:
            lp[uv_layer].uv = (0.5, 0.95)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def build_dread_brawler_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="male_strong")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        ear = kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.376, 0.018, HZ - 0.025), (0.052, 0.038, 0.080),
            MAT_SKIN, rot_euler=(0.06, side * -0.18, side * 0.14)
        )
        head_parts.append(ear)

    # Shaved undercut fade shell wrapping temples & back with 2 diagonal razor lines!
    bm_uc = bmesh.new()
    bmesh.ops.create_uvsphere(bm_uc, u_segments=44, v_segments=28, radius=1.0)
    uv_uc = bm_uc.loops.layers.uv.new("UVMap")
    for v in bm_uc.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        rx, ry, rz = 0.388, 0.365, 0.362
        if y < 0.02:
            push = min(1.0, (0.02 - y) / 0.55)
            center_mask = max(0.0, 1.0 - max(0.0, (abs(x) - 0.56) / 0.32))
            ry *= (1.0 - 0.42 * push * center_mask)
            rx *= (1.0 - 0.16 * push * center_mask)
            if z < 0.35:
                ry *= (1.0 - 0.25 * push)
        v.co = Vector((x * rx, y * ry + 0.020, HZ + 0.015 + z * rz))
    bmesh.ops.recalc_face_normals(bm_uc, faces=bm_uc.faces)
    for f in bm_uc.faces:
        for lp in f.loops:
            co = lp.vert.co
            u_val = max(0.02, min(0.98, 0.5 + (co.y / 0.38) * 0.45))
            v_val = max(0.02, min(0.98, 0.5 + ((co.z - (HZ + 0.04)) / 0.32) * 0.45))
            lp[uv_uc].uv = (u_val, v_val)
    m_uc = bpy.data.meshes.new("Brawler_Undercut")
    bm_uc.to_mesh(m_uc)
    bm_uc.free()
    uc_obj = bpy.data.objects.new("Brawler_Undercut", m_uc)
    bpy.context.collection.objects.link(uc_obj)
    uc_obj.data.materials.append(MAT_UNDERCUT)
    for poly in uc_obj.data.polygons:
        poly.use_smooth = True
    head_parts.append(uc_obj)

    # Top Hair Base Cushion supporting the dreadlocks
    top_cap = kit.create_ellipsoid("Dread_TopBase", (0.0, 0.04, HZ + 0.25), (0.285, 0.315, 0.145), MAT_HAIR_DREAD, uv_v=0.30)
    head_parts.append(top_cap)

    # Sculpted Dreadlock Ropes sweeping up & back into a thick high tied dreadlock bun!
    # 1. Crown dread rows sweeping from hairline back to the tie point (0.02, 0.18, HZ + 0.42)
    crown_starts = [
        (-0.20, -0.22, 0.22), (-0.11, -0.28, 0.24), ( 0.00, -0.30, 0.24),
        ( 0.11, -0.28, 0.24), ( 0.20, -0.22, 0.22),
        (-0.26, -0.10, 0.22), ( 0.26, -0.10, 0.22),
        (-0.24,  0.05, 0.24), ( 0.24,  0.05, 0.24),
    ]
    for idx, (sx, sy, sz) in enumerate(crown_starts):
        pts = [
            (sx, sy, HZ + sz),
            (sx * 0.85, sy * 0.45 + 0.02, HZ + sz + 0.14),
            (sx * 0.50, 0.06, HZ + 0.40),
            (sx * 0.22 + 0.02, 0.18, HZ + 0.42),
        ]
        head_parts.append(add_dread_tube(f"DreadCrown_{idx}", pts, 0.052, MAT_HAIR_DREAD))

    # 2. Iconic single loose dreadlock hanging down over the right forehead (exact portrait detail!)
    front_dread = [
        (0.08, -0.24, HZ + 0.30),
        (0.14, -0.34, HZ + 0.24),
        (0.17, -0.38, HZ + 0.14),
        (0.16, -0.39, HZ + 0.05),
    ]
    head_parts.append(add_dread_tube("DreadFrontDrop", front_dread, 0.044, MAT_HAIR_DREAD))

    # 3. Hair Tie Band + 10 Lush Thick Dreadlocks cascading out from the high bun toward back & right!
    tie_ring = [
        (Vector((0.02, 0.16, HZ + 0.38)), Vector((0.125, 0.0, 0.0)), Vector((0.0, 0.115, 0.0))),
        (Vector((0.03, 0.19, HZ + 0.44)), Vector((0.118, 0.0, 0.0)), Vector((0.0, 0.108, 0.0))),
    ]
    head_parts.append(kit.create_loft_object("Dread_Tie", tie_ring, MAT_SHIRT_BLACK))

    bun_dreads = [
        ((0.02, 0.18, 0.42), ( 0.14, 0.28, 0.54), ( 0.30, 0.36, 0.46), ( 0.42, 0.38, 0.26)),
        ((0.02, 0.18, 0.42), ( 0.04, 0.30, 0.58), ( 0.16, 0.44, 0.52), ( 0.26, 0.52, 0.32)),
        ((0.02, 0.18, 0.42), (-0.08, 0.30, 0.56), (-0.18, 0.44, 0.48), (-0.24, 0.52, 0.26)),
        ((0.02, 0.18, 0.42), ( 0.18, 0.24, 0.48), ( 0.36, 0.28, 0.34), ( 0.46, 0.26, 0.10)),
        ((0.02, 0.18, 0.42), ( 0.12, 0.34, 0.46), ( 0.24, 0.48, 0.32), ( 0.32, 0.54, 0.08)),
        ((0.02, 0.18, 0.42), ( 0.00, 0.36, 0.46), ( 0.02, 0.52, 0.30), ( 0.04, 0.56, 0.04)),
        ((0.02, 0.18, 0.42), (-0.12, 0.34, 0.44), (-0.24, 0.48, 0.28), (-0.30, 0.52, 0.04)),
        ((0.02, 0.18, 0.42), ( 0.22, 0.18, 0.42), ( 0.40, 0.18, 0.24), ( 0.46, 0.14, -0.02)),
        ((0.02, 0.18, 0.42), (-0.18, 0.26, 0.42), (-0.32, 0.36, 0.24), (-0.36, 0.38, 0.00)),
        ((0.02, 0.18, 0.42), ( 0.08, 0.22, 0.58), ( 0.22, 0.32, 0.56), ( 0.34, 0.38, 0.40)),
    ]
    for idx, (p0, p1, p2, p3) in enumerate(bun_dreads):
        pts = [(p[0], p[1], HZ + p[2]) for p in (p0, p1, p2, p3)]
        head_parts.append(add_dread_tube(f"DreadBun_{idx}", pts, 0.050, MAT_HAIR_DREAD))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_dread_brawler_body():
    body_parts = []

    # Thick Muscular Neck
    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.128, 0.0, 0.0)), Vector((0.0, 0.120, 0.0))),
        (Vector((0.0, 0.0, 1.48)), Vector((0.110, 0.0, 0.0)), Vector((0.0, 0.104, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Dark Charcoal Short-Sleeve Tactical Tee under the Blue Armored Vest
    torso_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.182, 0.0))),
        (Vector((0.0,  0.000, 0.90)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
        (Vector((0.0, -0.012, 1.06)), Vector((0.268, 0.0, 0.0)), Vector((0.0, 0.212, 0.0))),
        (Vector((0.0, -0.010, 1.22)), Vector((0.278, 0.0, 0.0)), Vector((0.0, 0.216, 0.0))),
        (Vector((0.0,  0.000, 1.32)), Vector((0.222, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_Tee", torso_rings, MAT_SHIRT_BLACK))

    # Heavy Slate-Blue Armored Tactical Chest Vest (contoured plates + collar straps + back plate!)
    vest_chest = kit.create_rounded_box("Vest_FrontMain", (0.0, -0.205, 1.08), (0.40, 0.072, 0.30), MAT_VEST_BLUE, bevel_width=0.022)
    vest_abs   = kit.create_rounded_box("Vest_FrontLower", (0.0, -0.200, 0.90), (0.34, 0.064, 0.14), MAT_VEST_BLUE, bevel_width=0.018)
    vest_back  = kit.create_rounded_box("Vest_BackMain", (0.0,  0.212, 1.06), (0.38, 0.072, 0.36), MAT_VEST_BLUE, bevel_width=0.022)
    vest_trim  = kit.create_rounded_box("Vest_ChestTrim", (0.0, -0.236, 1.12), (0.24, 0.026, 0.12), MAT_VEST_DARK, bevel_width=0.008)
    body_parts.extend([vest_chest, vest_abs, vest_back, vest_trim])

    for side in (1.0, -1.0):
        v_strap = kit.create_rounded_box(f"VestShoulderStrap_{side}", (side * 0.162, -0.04, 1.29), (0.095, 0.28, 0.078), MAT_VEST_BLUE, bevel_width=0.016)
        v_clip  = kit.create_rounded_box(f"VestClip_{side}", (side * 0.165, -0.195, 1.24), (0.068, 0.042, 0.048), MAT_METAL_SILV, bevel_width=0.008)
        body_parts.extend([v_strap, v_clip])

    # Iconic Diagonal Olive-Drab Chest Bandolier Strap + Twin Silver Buckles + Front Utility Pouch!
    # Tilted cleanly in the front X-Z plane via Y-axis rotation (+0.45 rad: top-right shoulder -> bottom-left hip)
    band_f = kit.create_rounded_box("Bandolier_Front", ( 0.020, -0.236, 1.05), (0.078, 0.036, 0.52), MAT_OLIVE_STRAP, rot_euler=(0.0, 0.45, 0.0), bevel_width=0.008)
    band_b = kit.create_rounded_box("Bandolier_Back",  ( 0.020,  0.244, 1.05), (0.078, 0.036, 0.52), MAT_OLIVE_STRAP, rot_euler=(0.0, 0.45, 0.0), bevel_width=0.008)
    b_buck1 = kit.create_rounded_box("BandBuckle1", ( 0.105, -0.250, 1.21), (0.092, 0.032, 0.056), MAT_METAL_SILV, rot_euler=(0.0, 0.45, 0.0), bevel_width=0.006)
    b_buck2 = kit.create_rounded_box("BandBuckle2", ( 0.068, -0.250, 1.13), (0.092, 0.032, 0.056), MAT_METAL_SILV, rot_euler=(0.0, 0.45, 0.0), bevel_width=0.006)
    b_pouch = kit.create_rounded_box("BandPouch",   (-0.045, -0.255, 0.91), (0.125, 0.068, 0.160), MAT_OLIVE_STRAP, rot_euler=(0.0, 0.42, 0.0), bevel_width=0.014)
    body_parts.extend([band_f, band_b, b_buck1, b_buck2, b_pouch])

    # Bright Vivid Orange Rounded Shoulder Pauldrons with Rivet Studs + Muscular Bare Arms + Red/White Gauntlets (NO PISTOLS!)
    AZ = 1.23
    for side in (1.0, -1.0):
        # Short black tee sleeve under orange pauldron
        sjoint = kit.create_ellipsoid(f"TeeSleeve_{side}", (side * 0.250, 0.0, AZ), (0.148, 0.140, 0.140), MAT_SHIRT_BLACK)
        # Signature Rounded Orange Shoulder Armor Pad
        opad = kit.create_ellipsoid(f"OrangePauldron_{side}", (side * 0.285, 0.0, AZ + 0.082), (0.155, 0.142, 0.098), MAT_ORANGE_PAD, rot_euler=(0.0, side * 0.36, 0.0))
        rivet_f = kit.create_ellipsoid(f"PauldronRivetF_{side}", (side * 0.295, -0.118, AZ + 0.095), (0.022, 0.022, 0.022), MAT_SHIRT_BLACK)
        rivet_b = kit.create_ellipsoid(f"PauldronRivetB_{side}", (side * 0.295,  0.118, AZ + 0.095), (0.022, 0.022, 0.022), MAT_SHIRT_BLACK)
        body_parts.extend([sjoint, opad, rivet_f, rivet_b])

        # Muscular Bare Forearms & Biceps (rich warm brown skin!)
        arm_rings = [
            (Vector((side * 0.26, 0.0, AZ)), Vector((0.0, 0.132, 0.0)), Vector((0.0, 0.0, 0.132 * side))),
            (Vector((side * 0.42, 0.0, AZ)), Vector((0.0, 0.126, 0.0)), Vector((0.0, 0.0, 0.126 * side))),
            (Vector((side * 0.56, 0.0, AZ)), Vector((0.0, 0.114, 0.0)), Vector((0.0, 0.0, 0.114 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.108, 0.0)), Vector((0.0, 0.0, 0.108 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"BareArm_{side}", arm_rings, MAT_SKIN))

        # Iconic Red / White / Black Armored Tactical Wrist Gauntlets (matches portrait!)
        gaunt_red = kit.create_rounded_box(f"GauntletRed_{side}", (side * 0.62, 0.0, AZ), (0.155, 0.238, 0.234), MAT_GAUNTLET_RD, bevel_width=0.022)
        gaunt_wht = kit.create_rounded_box(f"GauntletWhite_{side}", (side * 0.61, 0.0, AZ + 0.095), (0.135, 0.056, 0.058), MAT_WHITE_TRIM, bevel_width=0.010)
        gaunt_blk = kit.create_rounded_box(f"GauntletStrap_{side}", (side * 0.68, 0.0, AZ), (0.048, 0.244, 0.240), MAT_SHIRT_BLACK, bevel_width=0.010)
        body_parts.extend([gaunt_red, gaunt_wht, gaunt_blk])

        # Black Fingerless Combat Gloves — COMPLETELY EMPTY HANDS (ZERO pistols!)
        glove_palm = kit.create_rounded_box(f"GlovePalm_{side}", (side * 0.77, -0.004, AZ), (0.145, 0.188, 0.148), MAT_SHIRT_BLACK, bevel_width=0.024)
        glove_knk  = kit.create_rounded_box(f"GloveKnuckle_{side}", (side * 0.78, -0.004, AZ + 0.054), (0.100, 0.168, 0.048), MAT_GAUNTLET_RD, bevel_width=0.012)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.868, -0.004, AZ - 0.006), (0.070, 0.080, 0.062), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.78, -0.096, AZ), (0.044, 0.046, 0.038), MAT_SKIN)
        body_parts.extend([glove_palm, glove_knk, fingers, thumb])

    # Heavy Tactical Utility Belt + Pouches
    belt_rings = [
        (Vector((0.0, 0.002, 0.73)), Vector((0.272, 0.0, 0.0)), Vector((0.0, 0.212, 0.0))),
        (Vector((0.0, 0.000, 0.84)), Vector((0.266, 0.0, 0.0)), Vector((0.0, 0.206, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Utility_Belt", belt_rings, MAT_SHIRT_BLACK))
    for side in (1.0, -1.0):
        bp = kit.create_rounded_box(f"BeltPouch_{side}", (side * 0.155, -0.215, 0.79), (0.105, 0.062, 0.125), MAT_VEST_DARK, bevel_width=0.012)
        sp = kit.create_rounded_box(f"SidePouch_{side}", (side * 0.266, -0.01, 0.78), (0.088, 0.145, 0.135), MAT_OLIVE_STRAP, bevel_width=0.014)
        body_parts.extend([bp, sp])

    # Heavy Tactical Cargo Trousers + Slate-Blue Knee Pads + Rugged Combat Boots
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.180, 0.0, 0.0)), Vector((0.0, 0.152, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.265, 0.0, 0.0)), Vector((0.0, 0.206, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.250, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_SHIRT_BLACK))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.150,  0.004, 0.68)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.136, 0.0))),
            (Vector((side * 0.152,  0.004, 0.54)), Vector((0.138, 0.0, 0.0)), Vector((0.0, -0.142, 0.0))),
            (Vector((side * 0.154,  0.000, 0.42)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.154, -0.002, 0.30)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
            (Vector((side * 0.154,  0.000, 0.21)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.130, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_SHIRT_BLACK))

        kpad = kit.create_rounded_box(f"KneePad_{side}", (side * 0.154, -0.118, 0.372), (0.132, 0.058, 0.140), MAT_VEST_BLUE, bevel_width=0.018)
        kstrap = kit.create_rounded_box(f"KneeOrangeAccent_{side}", (side * 0.154, -0.140, 0.372), (0.072, 0.024, 0.036), MAT_ORANGE_PAD, bevel_width=0.006)
        body_parts.extend([kpad, kstrap])

        boot_upper = [
            (Vector((side * 0.154,  0.000, 0.210)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.154, -0.015, 0.130)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.140, 0.0))),
            (Vector((side * 0.154, -0.054, 0.080)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.186, 0.0))),
            (Vector((side * 0.154, -0.064, 0.038)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_VEST_DARK))
        boot_sole = [
            (Vector((side * 0.154, -0.064, 0.044)), Vector((0.148, 0.0, 0.0)), Vector((0.0, -0.204, 0.0))),
            (Vector((side * 0.154, -0.064, 0.003)), Vector((0.144, 0.0, 0.0)), Vector((0.0, -0.200, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_SHIRT_BLACK))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_dread_brawler_head()
body_obj = build_dread_brawler_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_DreadBrawler")

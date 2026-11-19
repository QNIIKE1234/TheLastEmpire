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

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_HeavyStriker_Face.png")
HAIR_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_HeavyStriker_Hair.png")
UNDERCUT_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_HeavyStriker_Undercut.png")


def generate_heavy_striker_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.945, 0.645, 0.405)
    SKIN_WARM   = (0.865, 0.525, 0.310)
    SKIN_SHADOW = (0.765, 0.430, 0.255)
    SCAR_COL    = (0.720, 0.340, 0.220)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE

    # Heroic cel-shaded forehead & strong jaw shading
    kit.alpha_blend(canvas, kit.smoothstep(0.64, 0.88, V) * 0.24, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.26, 0.08, V) * 0.22, SKIN_WARM)

    # Angular masculine anime nose bridge shadow (matches portrait!)
    nose_pts = [(0.492, 0.452), (0.486, 0.382), (0.506, 0.376)]
    d_nose = kit.polyline_sdf(U, V, nose_pts, [0.006, 0.014, 0.009])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, d_nose)) * 0.52, SKIN_SHADOW)

    # Fierce narrowed golden-amber eyes
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.145
        cy = 0.478
        ex = (U - cx) * side
        ey = V - cy

        t_x = np.clip((ex + 0.074) / 0.156, 0.0, 1.0)
        # Sharp stern narrowed eye slit (angled down inward toward nose!)
        y_top = -0.018 + 0.044 * np.sin(np.pi * np.power(t_x, 0.72)) + 0.036 * t_x
        y_bot = -0.024 - 0.020 * np.sin(np.pi * np.power(t_x, 0.85)) + 0.024 * t_x

        in_x = kit.smoothstep(-0.076, -0.068, ex) * (1.0 - kit.smoothstep(0.078, 0.084, ex))
        eye_mask = in_x * kit.smoothstep(-aa, aa, ey - y_bot) * kit.smoothstep(-aa, aa, y_top - ey)
        kit.alpha_blend(canvas, eye_mask, (0.995, 0.995, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.014, 0.0, y_top - ey) * 0.26, (0.64, 0.65, 0.76))

        # Focused golden-amber iris
        ix_c, iy_c = -0.004, 0.002
        rx_i, ry_i = 0.044, 0.046
        r_iris = np.hypot((ex - ix_c) / rx_i, (ey - iy_c) / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey - (iy_c - ry_i)) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.98 - 0.34 * np.power(v_grad, 1.4), 0.0, 1.0)
        iris_g = np.clip(0.76 - 0.44 * np.power(v_grad, 1.1), 0.0, 1.0)
        iris_b = np.clip(0.18 - 0.16 * np.power(v_grad, 0.8), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.80, 0.96, r_iris) * 0.85, (0.14, 0.05, 0.02))
        r_pupil = np.hypot((ex - ix_c) / 0.018, (ey - iy_c) / 0.020)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.85, 1.04, r_pupil)), (0.06, 0.03, 0.03))

        r_hi1 = np.hypot((ex - (ix_c + 0.018)) / 0.011, (ey - (iy_c + 0.014)) / 0.010)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.04, r_hi1)), (1.0, 1.0, 1.0))

        # Thick sharp male upper lash line
        u_lash_pts = [
            (cx + side * (-0.078), cy - 0.018),
            (cx + side * (-0.024), cy + 0.022),
            (cx + side * ( 0.044), cy + 0.044),
            (cx + side * ( 0.088), cy + 0.038),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.005, 0.0095, 0.011, 0.004])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.05, 0.03, 0.06))

        l_lash_pts = [
            (cx + side * (-0.055), cy - 0.024),
            (cx + side * ( 0.010), cy - 0.036),
            (cx + side * ( 0.076), cy - 0.002),
        ]
        d_ll = kit.polyline_sdf(U, V, l_lash_pts, [0.0022, 0.0030, 0.0025])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_ll)) * 0.85, (0.08, 0.04, 0.06))

        # Heavy fierce furrowed dark-indigo eyebrows (slanted strongly down toward nose bridge!)
        brow_pts = [
            (cx + side * (-0.092), cy + 0.026),
            (cx + side * (-0.026), cy + 0.056),
            (cx + side * ( 0.045), cy + 0.086),
            (cx + side * ( 0.104), cy + 0.104),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.009, 0.0155, 0.0145, 0.0045])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.14, 0.09, 0.28))

    # Iconic diagonal battle scar across left eye & cheek (viewer's right = u > 0.5)
    scar_main = [(0.562, 0.620), (0.618, 0.530), (0.665, 0.430), (0.715, 0.310)]
    d_scar = kit.polyline_sdf(U, V, scar_main, [0.003, 0.011, 0.012, 0.0025])
    # Mask scar over eye white/iris slightly so it passes neatly over brow & cheek like portrait!
    scar_mask = (1.0 - kit.smoothstep(-aa * 1.4, aa * 1.4, d_scar)) * 0.84
    # Keep eye sclera clean across the middle of the eye
    eye_keep = kit.smoothstep(0.038, 0.052, np.hypot((U - 0.645) / 1.4, (V - 0.478)))
    kit.alpha_blend(canvas, scar_mask * eye_keep, SCAR_COL)

    # Small furrow crease between brows
    frown_pts = [(0.502, 0.510), (0.515, 0.548)]
    d_fr = kit.polyline_sdf(U, V, frown_pts, [0.0025, 0.0035])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_fr)) * 0.55, SKIN_SHADOW)

    # Stern serious mouth (slightly curved down, confident badass!)
    mouth_pts = [(0.422, 0.288), (0.492, 0.304), (0.562, 0.294)]
    d_m = kit.polyline_sdf(U, V, mouth_pts, [0.0032, 0.0052, 0.0032])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_m), (0.28, 0.12, 0.09))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_HeavyStriker_Face.png")


def generate_undercut_zigzag_texture():
    """Shaved silver-grey undercut side texture with crisp razor zigzag stripe (matches portrait!)."""
    W, H = 512, 512
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)
    canvas = np.ones((H, W, 4), dtype=np.float32)
    SHAVED_DARK = (0.45, 0.43, 0.49)
    SHAVED_LIGHT = (0.76, 0.74, 0.80)
    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SHAVED_DARK

    # Crisp diagonal lightning/zigzag razor stripe matching HeavyStriker's temple shave!
    pts = [(0.12, 0.42), (0.45, 0.62), (0.62, 0.36), (0.90, 0.56)]
    d_zz = kit.polyline_sdf(U, V, pts, [0.052, 0.055, 0.055, 0.052])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-0.008, 0.008, d_zz), SHAVED_LIGHT)
    return kit.save_rgba_array_to_png(canvas, UNDERCUT_TEX_PATH, "Chibi_HeavyStriker_Undercut.png")


IMG_FACE = generate_heavy_striker_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_HeavyStriker_Hair.png",
    root_rgb=(0.14, 0.09, 0.28),
    mid_rgb=(0.24, 0.16, 0.46),
    tip_rgb=(0.35, 0.24, 0.62),
    hi_rgb=(0.54, 0.42, 0.84),
    band_v=0.52, band_strength=0.34
)
IMG_UNDERCUT = generate_undercut_zigzag_texture()

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.945, 0.645, 0.405, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_INDIGO = kit.make_mat("Mat_Hair_Indigo",  (1.0, 1.0, 1.0, 1.0), roughness=0.84, specular=0.05, image=IMG_HAIR)
MAT_UNDERCUT    = kit.make_mat("Mat_Undercut",     (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_UNDERCUT)
MAT_SUIT_DARK   = kit.make_mat("Mat_Suit_Dark",    (0.16, 0.15, 0.34, 1.0), roughness=0.90, specular=0.04)
MAT_SUIT_PLATE  = kit.make_mat("Mat_Suit_Plate",   (0.25, 0.24, 0.48, 1.0), roughness=0.80, metallic=0.06, specular=0.06)
MAT_SCARF       = kit.make_mat("Mat_Scarf_Dark",   (0.13, 0.12, 0.24, 1.0), roughness=0.92, specular=0.03)
MAT_TAN_STRAP   = kit.make_mat("Mat_Tan_Strap",    (0.64, 0.59, 0.38, 1.0), roughness=0.90, specular=0.04)
MAT_TAN_POUCH   = kit.make_mat("Mat_Tan_Pouch",    (0.56, 0.51, 0.32, 1.0), roughness=0.90, specular=0.04)
MAT_GLOVE_BLUE  = kit.make_mat("Mat_Glove_Blue",   (0.22, 0.28, 0.78, 1.0), roughness=0.84, specular=0.05)
MAT_DARK_STRAP  = kit.make_mat("Mat_Dark_Strap",   (0.10, 0.10, 0.14, 1.0), roughness=0.90, specular=0.04)
MAT_ARMOR_GREY  = kit.make_mat("Mat_Armor_Grey",   (0.28, 0.29, 0.35, 1.0), roughness=0.76, metallic=0.08, specular=0.08)
MAT_WHITE_TRIM  = kit.make_mat("Mat_White_Trim",   (0.92, 0.93, 0.96, 1.0), roughness=0.86, specular=0.04)


def build_heavy_striker_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="male_strong")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        ear = kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.372, 0.018, HZ - 0.025), (0.048, 0.036, 0.076),
            MAT_SKIN, rot_euler=(0.06, side * -0.18, side * 0.14)
        )
        head_parts.append(ear)

    # Tactical black comms earpiece + mic wire on left ear (+X, matching portrait!)
    earpiece = kit.create_rounded_box("Comms_Earpiece", (0.398, -0.010, HZ - 0.042), (0.044, 0.052, 0.095), MAT_DARK_STRAP, rot_euler=(0.12, 0.0, 0.15), bevel_width=0.010)
    ear_mic = kit.create_rounded_box("Comms_Wire", (0.372, -0.068, HZ - 0.118), (0.020, 0.095, 0.022), MAT_ARMOR_GREY, rot_euler=(0.45, 0.0, -0.40), bevel_width=0.005)
    head_parts.extend([earpiece, ear_mic])

    # Shaved silver-grey undercut side & back shell ONLY on temples & back (zero grey forehead band!)
    bm_uc = bmesh.new()
    bmesh.ops.create_uvsphere(bm_uc, u_segments=44, v_segments=28, radius=1.0)
    uv_uc = bm_uc.loops.layers.uv.new("UVMap")
    for v in bm_uc.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        rx, ry, rz = 0.388, 0.365, 0.362
        # Tuck front face & entire forehead completely inside skull so undercut only wraps temples & back!
        if y < 0.02:
            push = min(1.0, (0.02 - y) / 0.55)
            # Keep side temples exposed (|x| > 0.72), tuck center forehead deeply
            center_mask = max(0.0, 1.0 - max(0.0, (abs(x) - 0.55) / 0.35))
            ry *= (1.0 - 0.42 * push * center_mask)
            rx *= (1.0 - 0.16 * push * center_mask)
            if z < 0.35:
                ry *= (1.0 - 0.25 * push)
        v.co = Vector((x * rx, y * ry + 0.022, HZ + 0.014 + z * rz))
    bmesh.ops.recalc_face_normals(bm_uc, faces=bm_uc.faces)
    for f in bm_uc.faces:
        for lp in f.loops:
            co = lp.vert.co
            u_val = max(0.02, min(0.98, 0.5 + (co.y / 0.38) * 0.45))
            v_val = max(0.02, min(0.98, 0.5 + ((co.z - (HZ + 0.04)) / 0.32) * 0.45))
            lp[uv_uc].uv = (u_val, v_val)
    m_uc = bpy.data.meshes.new("Hair_UndercutShell")
    bm_uc.to_mesh(m_uc)
    bm_uc.free()
    uc_obj = bpy.data.objects.new("Hair_UndercutShell", m_uc)
    bpy.context.collection.objects.link(uc_obj)
    uc_obj.data.materials.append(MAT_UNDERCUT)
    for poly in uc_obj.data.polygons:
        poly.use_smooth = True
    head_parts.append(uc_obj)

    # Sleek Low-Profile Top Indigo Hair Base (military undercut fade!)
    top_cap = kit.create_ellipsoid("Hair_TopBase", (0.0, 0.03, HZ + 0.21), (0.312, 0.352, 0.155), MAT_HAIR_INDIGO, uv_v=0.30)
    head_parts.append(top_cap)

    # 13 Sleek Razor-Cut Swept-Back Indigo Military Undercut Spikes (lower, sharper silhouette!)
    spike_specs = [
        ( 0.00, -0.33, 0.20,  0.00,  0.12, 0.40,  0.00,  0.34, 0.34, 0.112, 0.038,  0.00),
        (-0.11, -0.31, 0.20, -0.11,  0.10, 0.39, -0.10,  0.33, 0.33, 0.104, 0.035,  0.18),
        ( 0.11, -0.31, 0.20,  0.11,  0.10, 0.39,  0.10,  0.33, 0.33, 0.104, 0.035, -0.18),
        (-0.21, -0.26, 0.19, -0.22,  0.06, 0.35, -0.20,  0.30, 0.29, 0.094, 0.032,  0.38),
        ( 0.21, -0.26, 0.19,  0.22,  0.06, 0.35,  0.20,  0.30, 0.29, 0.094, 0.032, -0.38),
        (-0.05, -0.24, 0.26, -0.04,  0.08, 0.45, -0.02,  0.29, 0.42, 0.088, 0.032,  0.08),
        ( 0.06, -0.22, 0.26,  0.07,  0.10, 0.45,  0.08,  0.31, 0.41, 0.088, 0.032, -0.08),
        (-0.17, -0.18, 0.25, -0.18,  0.10, 0.42, -0.16,  0.32, 0.37, 0.084, 0.030,  0.28),
        ( 0.17, -0.18, 0.25,  0.18,  0.10, 0.42,  0.16,  0.32, 0.37, 0.084, 0.030, -0.28),
        (-0.28, -0.12, 0.18, -0.30,  0.12, 0.31, -0.27,  0.33, 0.24, 0.078, 0.028,  0.55),
        ( 0.28, -0.12, 0.18,  0.30,  0.12, 0.31,  0.27,  0.33, 0.24, 0.078, 0.028, -0.55),
        (-0.08,  0.04, 0.32, -0.08,  0.28, 0.40, -0.07,  0.46, 0.31, 0.092, 0.032,  0.12),
        ( 0.08,  0.04, 0.32,  0.08,  0.28, 0.40,  0.07,  0.46, 0.31, 0.092, 0.032, -0.12),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, x2, y2, z2, w, t, roll) in enumerate(spike_specs):
        strand = [
            (Vector((x0, y0, HZ + z0)), w * 0.85, t * 0.85, roll),
            (Vector((x1, y1, HZ + z1)), w * 0.95, t * 0.95, roll),
            (Vector((x2, y2, HZ + z2)), w * 0.55, t * 0.55, roll),
            (Vector((x2 * 1.04, y2 + 0.10, HZ + z2 - 0.02)), 0.008, 0.005, roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"Spike_{idx}", strand, MAT_HAIR_INDIGO, v_start=0.08, v_end=0.98))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_heavy_striker_body():
    body_parts = []

    # Neck + Thick Wrapped Tactical Dark Scarf/Gaiter (signature portrait detail!)
    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.122, 0.0, 0.0)), Vector((0.0, 0.114, 0.0))),
        (Vector((0.0, 0.0, 1.48)), Vector((0.104, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    scarf_rings = [
        (Vector((0.0, -0.005, 1.24)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.175, 0.0))),
        (Vector((0.0, -0.015, 1.32)), Vector((0.218, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, -0.005, 1.40)), Vector((0.190, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Neck_Scarf", scarf_rings, MAT_SCARF))

    # Broad Heroic Armored Tactical Torso
    torso_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.176, 0.0))),
        (Vector((0.0,  0.000, 0.90)), Vector((0.220, 0.0, 0.0)), Vector((0.0, 0.172, 0.0))),
        (Vector((0.0, -0.012, 1.06)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.204, 0.0))),
        (Vector((0.0, -0.010, 1.22)), Vector((0.268, 0.0, 0.0)), Vector((0.0, 0.208, 0.0))),
        (Vector((0.0,  0.000, 1.32)), Vector((0.215, 0.0, 0.0)), Vector((0.0, 0.172, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_Suit", torso_rings, MAT_SUIT_DARK))

    # Segmented Tactical Armor Vest Plates (front + back)
    for row_z in (0.94, 1.05, 1.16):
        for side in (1.0, -1.0):
            plate = kit.create_rounded_box(
                f"ChestPlate_{row_z}_{side}", (side * 0.088, -0.200, row_z),
                (0.145, 0.048, 0.088), MAT_SUIT_PLATE,
                rot_euler=(0.0, 0.0, side * 0.12), bevel_width=0.012
            )
            body_parts.append(plate)
    back_armor = kit.create_rounded_box("Back_ArmorPack", (0.0, 0.214, 1.08), (0.30, 0.068, 0.28), MAT_SUIT_PLATE, bevel_width=0.018)
    body_parts.append(back_armor)

    # Signature Olive-Tan Shoulder Webbing Straps + Buckles + Asymmetric Ammo Pouches (exact portrait!)
    for side in (1.0, -1.0):
        strap_f = kit.create_rounded_box(f"TanStrapF_{side}", (side * 0.148, -0.212, 1.06), (0.056, 0.036, 0.44), MAT_TAN_STRAP, rot_euler=(-0.06, 0.0, side * 0.14), bevel_width=0.008)
        strap_b = kit.create_rounded_box(f"TanStrapB_{side}", (side * 0.140,  0.228, 1.08), (0.056, 0.036, 0.40), MAT_TAN_STRAP, bevel_width=0.008)
        buckle = kit.create_rounded_box(f"TanBuckle_{side}", (side * 0.148, -0.228, 1.18), (0.072, 0.026, 0.052), MAT_ARMOR_GREY, bevel_width=0.007)
        # Chest ammo pouches
        pouch_z = 1.02 if side < 0 else 1.04
        chest_pouch = kit.create_rounded_box(f"ChestPouch_{side}", (side * 0.125, -0.232, pouch_z), (0.105, 0.062, 0.128), MAT_TAN_POUCH, rot_euler=(0.0, 0.0, side * 0.14), bevel_width=0.012)
        chest_flap = kit.create_rounded_box(f"ChestFlap_{side}", (side * 0.125, -0.238, pouch_z + 0.048), (0.110, 0.066, 0.038), MAT_TAN_STRAP, rot_euler=(0.0, 0.0, side * 0.14), bevel_width=0.008)
        body_parts.extend([strap_f, strap_b, buckle, chest_pouch, chest_flap])

        # Heavy Layered Armored Shoulder Pauldrons
        sjoint = kit.create_ellipsoid(f"ShoulderJoint_{side}", (side * 0.235, 0.0, 1.23), (0.145, 0.138, 0.138), MAT_SUIT_DARK)
        spad = kit.create_rounded_box(f"ShoulderPad_{side}", (side * 0.272, 0.0, 1.315), (0.215, 0.235, 0.088), MAT_SUIT_PLATE, rot_euler=(0.0, side * 0.34, 0.0), bevel_width=0.022)
        spad_trim = kit.create_rounded_box(f"ShoulderTrim_{side}", (side * 0.220, 0.0, 1.352), (0.060, 0.210, 0.044), MAT_TAN_STRAP, rot_euler=(0.0, side * 0.34, 0.0), bevel_width=0.010)
        body_parts.extend([sjoint, spad, spad_trim])

        # Signature crisp white horizontal stripe on left shoulder pad (+X, matching portrait!)
        if side > 0:
            stripe = kit.create_rounded_box("Shoulder_WhiteStripe", (0.310, 0.0, 1.345), (0.105, 0.055, 0.026), MAT_WHITE_TRIM, rot_euler=(0.0, 0.34, 0.0), bevel_width=0.005)
            body_parts.append(stripe)

    # Sturdy Armored Arms + Royal-Blue Knuckle Combat Gloves (matches portrait!)
    AZ = 1.23
    for side in (1.0, -1.0):
        sleeve_rings = [
            (Vector((side * 0.20, 0.0, AZ)), Vector((0.0, 0.132, 0.0)), Vector((0.0, 0.0, 0.132 * side))),
            (Vector((side * 0.36, 0.0, AZ)), Vector((0.0, 0.122, 0.0)), Vector((0.0, 0.0, 0.122 * side))),
            (Vector((side * 0.54, 0.0, AZ)), Vector((0.0, 0.114, 0.0)), Vector((0.0, 0.0, 0.114 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.108, 0.0)), Vector((0.0, 0.0, 0.108 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_SUIT_DARK))
        forearm_guard = kit.create_rounded_box(f"ForearmArmor_{side}", (side * 0.56, 0.0, AZ + 0.055), (0.16, 0.16, 0.075), MAT_SUIT_PLATE, bevel_width=0.016)
        wrist_strap = kit.create_rounded_box(f"WristTan_{side}", (side * 0.67, 0.0, AZ), (0.072, 0.228, 0.222), MAT_TAN_STRAP, bevel_width=0.016)
        body_parts.extend([forearm_guard, wrist_strap])

        # Chunky Fighter Gloves with Royal-Blue Knuckle Plates (exact portrait detail!)
        glove_palm = kit.create_rounded_box(f"GlovePalm_{side}", (side * 0.77, -0.004, AZ), (0.145, 0.185, 0.145), MAT_DARK_STRAP, bevel_width=0.024)
        knuckle_blue = kit.create_rounded_box(f"GloveBlueKnuckle_{side}", (side * 0.79, -0.004, AZ + 0.052), (0.115, 0.170, 0.058), MAT_GLOVE_BLUE, bevel_width=0.014)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.865, -0.004, AZ - 0.006), (0.068, 0.078, 0.060), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.78, -0.095, AZ), (0.042, 0.044, 0.036), MAT_SKIN)
        body_parts.extend([glove_palm, knuckle_blue, fingers, thumb])

    # Heavy Olive-Tan Utility Belt & Twin Front Pouches
    belt_rings = [
        (Vector((0.0, 0.002, 0.73)), Vector((0.268, 0.0, 0.0)), Vector((0.0, 0.208, 0.0))),
        (Vector((0.0, 0.000, 0.84)), Vector((0.262, 0.0, 0.0)), Vector((0.0, 0.202, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Utility_Belt", belt_rings, MAT_TAN_STRAP))
    for side in (1.0, -1.0):
        bpouch = kit.create_rounded_box(f"BeltPouch_{side}", (side * 0.128, -0.215, 0.805), (0.108, 0.065, 0.132), MAT_TAN_POUCH, bevel_width=0.012)
        side_pouch = kit.create_rounded_box(f"SidePouch_{side}", (side * 0.262, -0.02, 0.775), (0.088, 0.145, 0.142), MAT_TAN_POUCH, bevel_width=0.014)
        body_parts.extend([bpouch, side_pouch])

    # Heavy Armored Cargo Pants & Chunky Combat Boots
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.175, 0.0, 0.0)), Vector((0.0, 0.148, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.260, 0.0, 0.0)), Vector((0.0, 0.202, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.245, 0.0, 0.0)), Vector((0.0, 0.190, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_SUIT_DARK))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.148,  0.004, 0.68)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.150,  0.004, 0.54)), Vector((0.134, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
            (Vector((side * 0.152,  0.000, 0.42)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.152, -0.002, 0.30)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_SUIT_DARK))

        thigh_strap = [
            (Vector((side * 0.150, 0.002, 0.50)), Vector((0.138, 0.0, 0.0)), Vector((0.0, -0.142, 0.0))),
            (Vector((side * 0.150, 0.002, 0.55)), Vector((0.139, 0.0, 0.0)), Vector((0.0, -0.143, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"ThighStrap_{side}", thigh_strap, MAT_TAN_STRAP))

        kpad = kit.create_rounded_box(f"KneePad_{side}", (side * 0.152, -0.112, 0.370), (0.128, 0.056, 0.138), MAT_SUIT_PLATE, bevel_width=0.018)
        body_parts.append(kpad)

        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.210)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
            (Vector((side * 0.152, -0.054, 0.080)), Vector((0.134, 0.0, 0.0)), Vector((0.0, -0.184, 0.0))),
            (Vector((side * 0.152, -0.064, 0.038)), Vector((0.138, 0.0, 0.0)), Vector((0.0, -0.194, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_DARK_STRAP))
        boot_sole = [
            (Vector((side * 0.152, -0.064, 0.044)), Vector((0.146, 0.0, 0.0)), Vector((0.0, -0.202, 0.0))),
            (Vector((side * 0.152, -0.064, 0.003)), Vector((0.142, 0.0, 0.0)), Vector((0.0, -0.198, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_ARMOR_GREY))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_heavy_striker_head()
body_obj = build_heavy_striker_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_HeavyStriker")

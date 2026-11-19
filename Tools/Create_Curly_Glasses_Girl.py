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

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_CurlyGirl_Face.png")
HAIR_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_CurlyGirl_Hair.png")


def generate_curly_girl_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.935, 0.585, 0.355)
    SKIN_WARM   = (0.865, 0.485, 0.275)
    SKIN_SHADOW = (0.765, 0.400, 0.230)
    BLUSH_COL   = (0.960, 0.440, 0.360)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.64, 0.86, V) * 0.22, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.16, SKIN_WARM)

    # Warm energetic blush under round glasses
    for side in (-1.0, 1.0):
        d_b = np.hypot((U - (0.5 + side * 0.170)) / 0.070, (V - 0.376) / 0.030)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.15, 1.0, d_b)) * 0.26, BLUSH_COL)

    # Cute button anime nose with warm cel shadow & tiny highlight
    d_n = np.hypot((U - 0.500) / 0.014, (V - 0.390) / 0.011)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.42, SKIN_SHADOW)
    d_nh = np.hypot((U - 0.502) / 0.006, (V - 0.402) / 0.008)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.2, 1.0, d_nh)) * 0.40, (1.0, 0.88, 0.78))

    # Huge Sparkling Violet-Purple Anime Eyes (framed behind her iconic big round glasses!)
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.148
        cy = 0.468
        ex = (U - cx) * side
        ey = V - cy

        # Big wide round-cute anime eye silhouette
        r_eye = np.hypot(ex / 0.084, ey / 0.076)
        eye_mask = 1.0 - kit.smoothstep(1.0 - aa / 0.084, 1.0 + aa / 0.084, r_eye)
        kit.alpha_blend(canvas, eye_mask, (0.998, 0.998, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.050, 0.076, ey) * 0.24, (0.70, 0.68, 0.84))

        # Luminous Violet-Indigo Iris
        ix_c, iy_c = -0.002, 0.000
        rx_i, ry_i = 0.056, 0.064
        r_iris = np.hypot((ex - ix_c) / rx_i, (ey - iy_c) / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey - (iy_c - ry_i)) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.68 - 0.48 * np.power(v_grad, 1.25), 0.0, 1.0)
        iris_g = np.clip(0.54 - 0.46 * np.power(v_grad, 1.05), 0.0, 1.0)
        iris_b = np.clip(1.00 - 0.42 * np.power(v_grad, 1.40), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Lavender glowing lower crescent
        r_glow = np.hypot((ex - ix_c) / (rx_i * 0.72), (ey - (iy_c - 0.026)) / (ry_i * 0.36))
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.28, 1.0, r_glow)) * 0.80, (0.82, 0.70, 1.00))

        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.82, 0.96, r_iris) * 0.88, (0.10, 0.05, 0.22))
        r_pupil = np.hypot((ex - ix_c) / 0.022, (ey - (iy_c + 0.002)) / 0.026)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.85, 1.04, r_pupil)), (0.07, 0.04, 0.14))

        # Big sparkling double anime catchlights
        r_hi1 = np.hypot((ex - (ix_c + 0.024)) / 0.017, (ey - (iy_c + 0.025)) / 0.016)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi1)), (1.0, 1.0, 1.0))
        r_hi2 = np.hypot((ex - (ix_c - 0.022)) / 0.009, (ey - (iy_c - 0.022)) / 0.009)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.72, 1.04, r_hi2)), (1.0, 0.98, 1.0))

        # Cute thick curved upper eyelash with playful flick
        u_lash_pts = [
            (cx + side * (-0.080), cy + 0.012),
            (cx + side * (-0.040), cy + 0.064),
            (cx + side * ( 0.015), cy + 0.078),
            (cx + side * ( 0.068), cy + 0.058),
            (cx + side * ( 0.102), cy + 0.068),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.005, 0.0115, 0.014, 0.0125, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.05, 0.03, 0.06))

        # Soft cute lower lash rim
        l_lash_pts = [
            (cx + side * (-0.056), cy - 0.052),
            (cx + side * ( 0.004), cy - 0.074),
            (cx + side * ( 0.064), cy - 0.048),
        ]
        d_ll = kit.polyline_sdf(U, V, l_lash_pts, [0.0022, 0.0036, 0.0024])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_ll)) * 0.85, (0.08, 0.04, 0.08))

        # Expressive berry-dark eyebrows
        brow_pts = [
            (cx + side * (-0.068), cy + 0.105),
            (cx + side * ( 0.008), cy + 0.124),
            (cx + side * ( 0.078), cy + 0.114),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.0045, 0.0075, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.42, 0.06, 0.20))

    # Cute energetic open-O mouth (exact portrait expression!)
    r_m = np.hypot((U - 0.500) / 0.030, (V - 0.295) / 0.038)
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(0.92, 1.08, r_m), (0.36, 0.10, 0.12))
    r_tongue = np.hypot((U - 0.500) / 0.022, (V - 0.278) / 0.020)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.92, 1.08, r_m)) * (1.0 - kit.smoothstep(0.82, 1.04, r_tongue)), (0.86, 0.34, 0.34))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_CurlyGirl_Face.png")


IMG_FACE = generate_curly_girl_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_CurlyGirl_Hair.png",
    root_rgb=(0.56, 0.06, 0.24),
    mid_rgb=(0.90, 0.12, 0.38),
    tip_rgb=(1.00, 0.32, 0.56),
    hi_rgb=(1.00, 0.64, 0.78),
    band_v=0.55, band_strength=0.38
)

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.935, 0.585, 0.355, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_BERRY  = kit.make_mat("Mat_Hair_Berry",   (1.0, 1.0, 1.0, 1.0), roughness=0.84, specular=0.06, image=IMG_HAIR)
MAT_TEAL_BAND   = kit.make_mat("Mat_Teal_Band",    (0.14, 0.58, 0.62, 1.0), roughness=0.88, specular=0.04)
MAT_JACKET_AQUA = kit.make_mat("Mat_Jacket_Aqua",  (0.48, 0.84, 0.88, 1.0), roughness=0.88, specular=0.04)
MAT_JACKET_TRIM = kit.make_mat("Mat_Jacket_Trim",  (0.32, 0.68, 0.74, 1.0), roughness=0.90, specular=0.04)
MAT_PANTS_GOLD  = kit.make_mat("Mat_Pants_Gold",   (0.96, 0.72, 0.10, 1.0), roughness=0.88, specular=0.04)
MAT_POUCH_GOLD  = kit.make_mat("Mat_Pouch_Gold",   (0.86, 0.60, 0.08, 1.0), roughness=0.90, specular=0.04)
MAT_BELT_BROWN  = kit.make_mat("Mat_Belt_Brown",   (0.36, 0.24, 0.18, 1.0), roughness=0.90, specular=0.04)
MAT_DARK_STRAP  = kit.make_mat("Mat_Dark_Strap",   (0.12, 0.12, 0.15, 1.0), roughness=0.90, specular=0.04)
MAT_ARMOR_GREY  = kit.make_mat("Mat_Armor_Grey",   (0.28, 0.29, 0.34, 1.0), roughness=0.76, metallic=0.08, specular=0.08)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver", (0.78, 0.82, 0.88, 1.0), roughness=0.42, metallic=0.65, specular=0.25)
MAT_WHITE_WRAP  = kit.make_mat("Mat_White_Wrap",   (0.95, 0.94, 0.90, 1.0), roughness=0.90, specular=0.03)


def build_curly_girl_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="cute_round")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.368, 0.018, HZ - 0.032), (0.048, 0.036, 0.074),
            MAT_SKIN, rot_euler=(0.06, side * -0.18, side * 0.16)
        ))

    # Iconic Big Round Matte-Black Glasses framing her huge violet eyes!
    glass_y = -0.354
    glass_z = HZ - 0.018
    for side in (1.0, -1.0):
        lx = side * 0.150
        rim = kit.create_torus_ring(
            f"GlassRim_{side}", (lx, glass_y, glass_z), major_r=0.118, minor_r=0.015,
            mat=MAT_DARK_STRAP, rot_euler=(math.pi * 0.5, 0.0, side * -0.12)
        )
        temple = kit.create_rounded_box(
            f"GlassTemple_{side}", (side * 0.328, -0.165, glass_z + 0.022),
            (0.018, 0.36, 0.018), MAT_DARK_STRAP,
            rot_euler=(0.05, 0.0, side * -0.28), bevel_width=0.004
        )
        head_parts.extend([rim, temple])
    head_parts.append(kit.create_rounded_box("GlassBridge", (0.0, glass_y - 0.006, glass_z + 0.016), (0.092, 0.018, 0.018), MAT_DARK_STRAP, bevel_width=0.005))

    # Voluminous Berry-Pink Curly Crown Base
    head_parts.append(kit.create_hair_crown_shell(HZ, MAT_HAIR_BERRY, rx_base=0.415, ry_base=0.392, rz_base=0.392, cutout_z=0.46))

    # Sweeping Fluffy S-Wave Curly Bangs & Playful Cheek Ringlets (zero lumpy meatballs!)
    curly_bangs = [
        # Center sweeping wavy puff bang
        [
            (Vector(( 0.04, -0.20, HZ + 0.38)), 0.115, 0.044, -0.05),
            (Vector((-0.02, -0.36, HZ + 0.25)), 0.110, 0.040,  0.10),
            (Vector(( 0.02, -0.40, HZ + 0.13)), 0.072, 0.026, -0.08),
            (Vector((-0.02, -0.38, HZ + 0.04)), 0.010, 0.006,  0.00),
        ],
        # Left wavy curl crest sweeping outward
        [
            (Vector((-0.08, -0.18, HZ + 0.39)), 0.120, 0.048,  0.22),
            (Vector((-0.22, -0.31, HZ + 0.28)), 0.112, 0.044,  0.34),
            (Vector((-0.30, -0.30, HZ + 0.14)), 0.072, 0.028,  0.42),
            (Vector((-0.24, -0.29, HZ + 0.05)), 0.010, 0.006,  0.45),
        ],
        # Right playful flared wavy bang crest (kicking out joyfully on viewer's right like portrait!)
        [
            (Vector(( 0.10, -0.18, HZ + 0.40)), 0.125, 0.050, -0.25),
            (Vector(( 0.28, -0.26, HZ + 0.32)), 0.118, 0.046, -0.42),
            (Vector(( 0.43, -0.20, HZ + 0.21)), 0.072, 0.028, -0.52),
            (Vector(( 0.50, -0.14, HZ + 0.26)), 0.012, 0.007, -0.55),
        ],
        # Right secondary lower curl sweeping above right lens
        [
            (Vector(( 0.16, -0.22, HZ + 0.32)), 0.105, 0.040, -0.35),
            (Vector(( 0.30, -0.27, HZ + 0.18)), 0.095, 0.036, -0.48),
            (Vector(( 0.40, -0.21, HZ + 0.07)), 0.058, 0.022, -0.55),
            (Vector(( 0.45, -0.16, HZ + 0.12)), 0.010, 0.006, -0.58),
        ],
    ]
    # Playful corkscrew cheek ringlets curling beside her glasses on both sides!
    for side in (-1.0, 1.0):
        curly_bangs.append([
            (Vector((side * 0.26, -0.15, HZ + 0.22)), 0.090, 0.036, side * -0.72),
            (Vector((side * 0.38, -0.22, HZ + 0.06)), 0.084, 0.034, side * -0.82),
            (Vector((side * 0.34, -0.23, HZ - 0.10)), 0.062, 0.024, side * -0.88),
            (Vector((side * 0.41, -0.20, HZ - 0.21)), 0.036, 0.014, side * -0.92),
            (Vector((side * 0.33, -0.18, HZ - 0.27)), 0.008, 0.005, side * -0.95),
        ])
    for idx, cb in enumerate(curly_bangs):
        head_parts.append(kit.create_ribbon_hair_strand(f"CurlyBang_{idx}", cb, MAT_HAIR_BERRY, v_start=0.08, v_end=0.98))

    # Back-Nape Fluffy Curly Ends
    for idx, x_off in enumerate((-0.24, -0.12, 0.0, 0.12, 0.24)):
        nape = [
            (Vector((x_off * 0.85, 0.34, HZ + 0.04)), 0.092, 0.034, x_off * 1.3),
            (Vector((x_off * 0.96, 0.38, HZ - 0.12)), 0.084, 0.030, x_off * 1.4),
            (Vector((x_off * 0.88, 0.33, HZ - 0.25)), 0.048, 0.018, x_off * 1.4),
            (Vector((x_off * 0.82, 0.27, HZ - 0.32)), 0.010, 0.006, x_off * 1.4),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"NapeCurl_{idx}", nape, MAT_HAIR_BERRY, v_start=0.28, v_end=0.80))

    # HUGE DRAMATIC CASCADING CURLY SIDE PONYTAIL WITH TEAL SCRUNCHIE (Viewer's LEFT = -X, exact portrait!)
    pt_anchor = Vector((-0.22, 0.20, HZ + 0.32))
    # Thick Teal-Turquoise Double Hair Scrunchie
    head_parts.append(kit.create_torus_ring("TealScrunchie_1", pt_anchor, 0.132, 0.042, MAT_TEAL_BAND, rot_euler=(0.45, 0.55, 0.20)))
    head_parts.append(kit.create_torus_ring("TealScrunchie_2", pt_anchor + Vector((-0.03, 0.02, 0.03)), 0.124, 0.032, MAT_TEAL_BAND, rot_euler=(0.45, 0.55, 0.20)))

    # 11 Lush Layered Cascading Wavy Curly Ribbon Plumes (replaces stacked round balls!)
    pt_center = pt_anchor + Vector((0.04, -0.02, -0.04))
    curly_tail_ribbons = [
        # High billowing top crest wave
        [
            (pt_anchor + Vector(( 0.00, 0.00,  0.03)), 0.145, 0.068,  0.18),
            (pt_anchor + Vector((-0.18, 0.04,  0.24)), 0.168, 0.076,  0.26),
            (pt_anchor + Vector((-0.38, 0.10,  0.14)), 0.142, 0.062,  0.34),
            (pt_anchor + Vector((-0.52, 0.14, -0.04)), 0.078, 0.034,  0.40),
            (pt_anchor + Vector((-0.44, 0.16, -0.18)), 0.012, 0.008,  0.42),
        ],
        # Upper-inner curly puff flick
        [
            (pt_anchor + Vector(( 0.02, 0.00,  0.05)), 0.115, 0.054, -0.05),
            (pt_anchor + Vector((-0.06, 0.04,  0.28)), 0.125, 0.058,  0.05),
            (pt_anchor + Vector((-0.24, 0.08,  0.34)), 0.086, 0.038,  0.14),
            (pt_anchor + Vector((-0.34, 0.10,  0.26)), 0.012, 0.008,  0.20),
        ],
        # Main sweeping outer cascading S-wave (cascades down past left cheek/shoulder!)
        [
            (pt_anchor + Vector((-0.02, 0.02,  0.00)), 0.165, 0.078,  0.40),
            (pt_anchor + Vector((-0.28, 0.06,  0.08)), 0.182, 0.082,  0.48),
            (pt_anchor + Vector((-0.42, 0.12, -0.14)), 0.172, 0.076,  0.54),
            (pt_anchor + Vector((-0.52, 0.16, -0.38)), 0.148, 0.064,  0.58),
            (pt_anchor + Vector((-0.40, 0.18, -0.60)), 0.088, 0.038,  0.60),
            (pt_anchor + Vector((-0.48, 0.20, -0.74)), 0.014, 0.009,  0.62),
        ],
        # Mid-back billowing S-wave cascade
        [
            (pt_anchor + Vector((-0.03, 0.05, -0.02)), 0.155, 0.072,  0.24),
            (pt_anchor + Vector((-0.22, 0.16, -0.08)), 0.170, 0.078,  0.30),
            (pt_anchor + Vector((-0.34, 0.24, -0.32)), 0.160, 0.072,  0.34),
            (pt_anchor + Vector((-0.28, 0.26, -0.56)), 0.125, 0.054,  0.36),
            (pt_anchor + Vector((-0.36, 0.26, -0.76)), 0.014, 0.009,  0.38),
        ],
        # Lower outer curly flare tip
        [
            (pt_anchor + Vector((-0.04, 0.02, -0.04)), 0.138, 0.064,  0.60),
            (pt_anchor + Vector((-0.30, 0.08, -0.22)), 0.155, 0.068,  0.66),
            (pt_anchor + Vector((-0.48, 0.12, -0.44)), 0.132, 0.056,  0.70),
            (pt_anchor + Vector((-0.56, 0.14, -0.56)), 0.070, 0.030,  0.74),
            (pt_anchor + Vector((-0.50, 0.16, -0.64)), 0.012, 0.008,  0.76),
        ],
        # Inner cascading curl tucking behind shoulder
        [
            (pt_anchor + Vector(( 0.00, 0.06, -0.05)), 0.130, 0.060,  0.10),
            (pt_anchor + Vector((-0.14, 0.20, -0.24)), 0.145, 0.066,  0.14),
            (pt_anchor + Vector((-0.22, 0.28, -0.48)), 0.128, 0.056,  0.18),
            (pt_anchor + Vector((-0.16, 0.26, -0.70)), 0.078, 0.034,  0.20),
            (pt_anchor + Vector((-0.22, 0.26, -0.84)), 0.012, 0.008,  0.22),
        ],
        # Rear volume curl wave (makes back view lush & balanced!)
        [
            (pt_anchor + Vector(( 0.02, 0.08,  0.02)), 0.135, 0.062, -0.10),
            (pt_anchor + Vector((-0.10, 0.26,  0.04)), 0.148, 0.068, -0.05),
            (pt_anchor + Vector((-0.18, 0.36, -0.18)), 0.130, 0.058,  0.00),
            (pt_anchor + Vector((-0.14, 0.38, -0.42)), 0.080, 0.036,  0.04),
            (pt_anchor + Vector((-0.20, 0.38, -0.56)), 0.012, 0.008,  0.06),
        ],
    ]
    for idx, cr in enumerate(curly_tail_ribbons):
        head_parts.append(kit.create_ribbon_hair_strand(
            f"CurlyTail_{idx}", cr, MAT_HAIR_BERRY,
            v_start=0.06, v_end=0.98, radial_center=pt_center
        ))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_curly_girl_body():
    body_parts = []

    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.108, 0.0, 0.0)), Vector((0.0, 0.100, 0.0))),
        (Vector((0.0, 0.0, 1.50)), Vector((0.092, 0.0, 0.0)), Vector((0.0, 0.088, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Cropped Bright Aqua-Mint Zip-Up Tactical Jacket (exact portrait colors!)
    jkt_rings = [
        (Vector((0.0,  0.000, 0.84)), Vector((0.214, 0.0, 0.0)), Vector((0.0, 0.166, 0.0))),
        (Vector((0.0, -0.008, 0.96)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
        (Vector((0.0, -0.022, 1.12)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.204, 0.0))),
        (Vector((0.0, -0.010, 1.24)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.180, 0.0))),
        (Vector((0.0,  0.000, 1.31)), Vector((0.185, 0.0, 0.0)), Vector((0.0, 0.154, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Jacket_Aqua", jkt_rings, MAT_JACKET_AQUA))

    # Tall Popped Aqua Collar (open V-neck showing dark inner tee)
    bm_col = bmesh.new()
    col_levels = [(1.26, 0.154, 0.136, 0.0), (1.34, 0.184, 0.160, 0.012), (1.42, 0.214, 0.184, 0.024)]
    co_r, ci_r = [], []
    for z, rx, ry, y_off in col_levels:
        o_, i_ = [], []
        for k in range(24):
            ang = (-0.28 * math.pi) + (k / 23.0) * (1.56 * math.pi)
            ca, sa = math.cos(ang), math.sin(ang)
            o_.append(bm_col.verts.new(Vector((ca * (rx + 0.024), y_off + sa * (ry + 0.024), z))))
            i_.append(bm_col.verts.new(Vector((ca * rx, y_off + sa * ry, z))))
        co_r.append(o_)
        ci_r.append(i_)
    for r in range(2):
        for k in range(23):
            bm_col.faces.new((co_r[r][k], co_r[r][k+1], co_r[r+1][k+1], co_r[r+1][k]))
            bm_col.faces.new((ci_r[r][k+1], ci_r[r][k], ci_r[r+1][k], ci_r[r+1][k+1]))
        bm_col.faces.new((ci_r[r][0], co_r[r][0], co_r[r+1][0], ci_r[r+1][0]))
        bm_col.faces.new((co_r[r][23], ci_r[r][23], ci_r[r+1][23], co_r[r+1][23]))
    for k in range(23):
        bm_col.faces.new((ci_r[0][k], ci_r[0][k+1], co_r[0][k+1], co_r[0][k]))
        bm_col.faces.new((co_r[2][k], co_r[2][k+1], ci_r[2][k+1], ci_r[2][k]))
    bmesh.ops.recalc_face_normals(bm_col, faces=bm_col.faces)
    mc = bpy.data.meshes.new("Jacket_Collar")
    bm_col.to_mesh(mc)
    bm_col.free()
    oc = bpy.data.objects.new("Jacket_Collar", mc)
    bpy.context.collection.objects.link(oc)
    body_parts.append(kit.assign_single_material(oc, MAT_JACKET_AQUA))

    # Center Dark Zipper Seam + Chunky Silver Zipper Pull + Dark Backpack Harness Straps!
    body_parts.append(kit.create_rounded_box("CenterZipSeam", (0.0, -0.212, 1.05), (0.018, 0.018, 0.36), MAT_DARK_STRAP, bevel_width=0.004))
    body_parts.append(kit.create_rounded_box("CenterZipPull", (0.0, -0.224, 1.18), (0.036, 0.022, 0.064), MAT_METAL_SILV, bevel_width=0.006))

    for side in (1.0, -1.0):
        body_parts.append(kit.create_rounded_box(f"PackStrapF_{side}", (side * 0.145, -0.204, 1.12), (0.052, 0.034, 0.36), MAT_DARK_STRAP, rot_euler=(-0.08, 0.0, side * 0.18), bevel_width=0.008))
        body_parts.append(kit.create_rounded_box(f"PackBuckle_{side}", (side * 0.148, -0.220, 1.17), (0.066, 0.024, 0.056), MAT_METAL_SILV, rot_euler=(-0.08, 0.0, side * 0.18), bevel_width=0.007))
        body_parts.append(kit.create_rounded_box(f"PackStrapB_{side}", (side * 0.128,  0.206, 1.10), (0.052, 0.032, 0.34), MAT_DARK_STRAP, bevel_width=0.008))
        body_parts.append(kit.create_ellipsoid(f"ShoulderJoint_{side}", (side * 0.218, 0.0, 1.23), (0.132, 0.126, 0.126), MAT_JACKET_AQUA))

    # Compact Tactical Backpack on Back (completes the shoulder harness straps!)
    body_parts.append(kit.create_rounded_box("Back_PackMain", (0.0, 0.232, 1.08), (0.28, 0.105, 0.26), MAT_ARMOR_GREY, bevel_width=0.022))
    body_parts.append(kit.create_rounded_box("Back_PackAccent", (0.0, 0.278, 1.14), (0.20, 0.036, 0.068), MAT_PANTS_GOLD, bevel_width=0.010))

    # Puffy Aqua Sleeves + White Square Patch + Crisp White Athletic Wrist Wraps + Black Brawler Gloves!
    AZ = 1.23
    for side in (1.0, -1.0):
        sleeve_rings = [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.122, 0.0)), Vector((0.0, 0.0, 0.122 * side))),
            (Vector((side * 0.34, 0.0, AZ)), Vector((0.0, 0.118, 0.0)), Vector((0.0, 0.0, 0.118 * side))),
            (Vector((side * 0.50, 0.0, AZ)), Vector((0.0, 0.114, 0.0)), Vector((0.0, 0.0, 0.114 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_JACKET_AQUA))
        if side < 0:
            body_parts.append(kit.create_rounded_box("Sleeve_WhitePatch", (-0.34, -0.02, AZ + 0.098), (0.105, 0.110, 0.036), MAT_WHITE_WRAP, bevel_width=0.008))

        forearm = kit.create_loft_object(f"Forearm_{side}", [
            (Vector((side * 0.50, 0.0, AZ)), Vector((0.0, 0.088, 0.0)), Vector((0.0, 0.0, 0.086 * side))),
            (Vector((side * 0.60, 0.0, AZ)), Vector((0.0, 0.082, 0.0)), Vector((0.0, 0.0, 0.080 * side))),
        ], MAT_SKIN)
        # Signature crisp white athletic wrist wraps!
        wrist_wrap = kit.create_loft_object(f"WristWrap_{side}", [
            (Vector((side * 0.59, 0.0, AZ)), Vector((0.0, 0.096, 0.0)), Vector((0.0, 0.0, 0.094 * side))),
            (Vector((side * 0.68, 0.0, AZ)), Vector((0.0, 0.098, 0.0)), Vector((0.0, 0.0, 0.096 * side))),
            (Vector((side * 0.72, 0.0, AZ)), Vector((0.0, 0.092, 0.0)), Vector((0.0, 0.0, 0.090 * side))),
        ], MAT_WHITE_WRAP)
        glove_palm = kit.create_rounded_box(f"GlovePalm_{side}", (side * 0.78, -0.004, AZ), (0.138, 0.175, 0.136), MAT_DARK_STRAP, bevel_width=0.022)
        glove_plate = kit.create_rounded_box(f"GlovePlate_{side}", (side * 0.79, -0.004, AZ + 0.050), (0.085, 0.130, 0.042), MAT_METAL_SILV, bevel_width=0.009)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.87, -0.004, AZ - 0.006), (0.064, 0.074, 0.056), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.785, -0.090, AZ), (0.038, 0.040, 0.034), MAT_SKIN)
        body_parts.extend([forearm, wrist_wrap, glove_palm, glove_plate, fingers, thumb])

    # Dark-Brown Utility Belt + Silver Buckle + Twin Bright Mustard-Yellow Hip Pouches!
    belt_rings = [
        (Vector((0.0, 0.002, 0.75)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, 0.000, 0.85)), Vector((0.246, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Utility_Belt", belt_rings, MAT_BELT_BROWN))
    body_parts.append(kit.create_rounded_box("Belt_Buckle", (0.0, -0.200, 0.80), (0.106, 0.044, 0.090), MAT_METAL_SILV, bevel_width=0.010))
    body_parts.append(kit.create_rounded_box("Belt_BuckleCore", (0.0, -0.214, 0.80), (0.046, 0.022, 0.060), MAT_ARMOR_GREY, bevel_width=0.005))

    for side in (1.0, -1.0):
        body_parts.append(kit.create_rounded_box(f"FrontPouch_{side}", (side * 0.145, -0.205, 0.805), (0.095, 0.060, 0.125), MAT_PANTS_GOLD, rot_euler=(0.0, 0.0, side * 0.25), bevel_width=0.012))
        body_parts.append(kit.create_rounded_box(f"FrontPouchStr_{side}", (side * 0.148, -0.228, 0.805), (0.034, 0.026, 0.118), MAT_DARK_STRAP, rot_euler=(0.0, 0.0, side * 0.25), bevel_width=0.006))

    # Vibrant Golden-Yellow Baggy Cargo Pants + Side Cargo Pockets + Chunky Dark Boots!
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.170, 0.0, 0.0)), Vector((0.0, 0.144, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.198, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.240, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_PANTS_GOLD))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.146,  0.004, 0.68)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.150,  0.004, 0.54)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.140, 0.0))),
            (Vector((side * 0.152,  0.000, 0.42)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.152, -0.002, 0.30)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_PANTS_GOLD))
        # Baggy side cargo pocket
        body_parts.append(kit.create_rounded_box(f"CargoPocket_{side}", (side * 0.262, -0.01, 0.56), (0.068, 0.145, 0.155), MAT_POUCH_GOLD, bevel_width=0.016))
        body_parts.append(kit.create_rounded_box(f"KneePad_{side}", (side * 0.152, -0.112, 0.370), (0.118, 0.048, 0.124), MAT_ARMOR_GREY, bevel_width=0.016))

        boot_cuff = [
            (Vector((side * 0.152, 0.000, 0.24)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.152, 0.000, 0.185)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.120, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootCuff_{side}", boot_cuff, MAT_TEAL_BAND))
        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.195)), Vector((0.108, 0.0, 0.0)), Vector((0.0, -0.114, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.152, -0.052, 0.080)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.152, -0.062, 0.038)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_DARK_STRAP))
        boot_sole = [
            (Vector((side * 0.152, -0.062, 0.044)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_ARMOR_GREY))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_curly_girl_head()
body_obj = build_curly_girl_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_CurlyGirl")

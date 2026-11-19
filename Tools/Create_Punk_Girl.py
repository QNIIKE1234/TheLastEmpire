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

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_PunkGirl_Face.png")
HAIR_BLONDE_PATH = os.path.join(kit.TEX_DIR, "Chibi_PunkGirl_HairBlonde.png")
HAIR_PINK_PATH = os.path.join(kit.TEX_DIR, "Chibi_PunkGirl_HairPink.png")


def generate_punk_girl_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.992, 0.912, 0.902)
    SKIN_WARM   = (0.955, 0.830, 0.825)
    SKIN_SHADOW = (0.870, 0.725, 0.730)
    BLUSH_COL   = (0.985, 0.665, 0.695)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.64, 0.86, V) * 0.22, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.16, SKIN_WARM)

    # Soft rosy cheek blush
    for side in (-1.0, 1.0):
        d_b = np.hypot((U - (0.5 + side * 0.172)) / 0.068, (V - 0.378) / 0.028)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.15, 1.0, d_b)) * 0.26, BLUSH_COL)

    # Cute tiny anime nose dot/shadow
    d_n = np.hypot((U - 0.500) / 0.009, (V - 0.376) / 0.007)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.35, (0.86, 0.64, 0.65))

    # Huge mischievous crimson-red gothic anime eyes + signature red tear-mark triangles!
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.146
        cy = 0.464
        ex = (U - cx) * side
        ey = V - cy

        t_x = np.clip((ex + 0.084) / 0.176, 0.0, 1.0)
        y_top = -0.024 + 0.098 * np.sin(np.pi * np.power(t_x, 0.78)) + 0.020 * t_x
        y_bot = -0.036 - 0.036 * np.sin(np.pi * np.power(t_x, 0.88)) + 0.022 * t_x

        in_x = kit.smoothstep(-0.086, -0.078, ex) * (1.0 - kit.smoothstep(0.086, 0.094, ex))
        eye_mask = in_x * kit.smoothstep(-aa, aa, ey - y_bot) * kit.smoothstep(-aa, aa, y_top - ey)
        kit.alpha_blend(canvas, eye_mask, (0.998, 0.996, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.020, 0.0, y_top - ey) * 0.22, (0.72, 0.68, 0.78))

        # Vivid glowing Crimson-Ruby Iris
        ix_c, iy_c = -0.002, 0.004
        rx_i, ry_i = 0.058, 0.068
        r_iris = np.hypot((ex - ix_c) / rx_i, (ey - iy_c) / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey - (iy_c - ry_i)) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(1.00 - 0.34 * np.power(v_grad, 1.4), 0.0, 1.0)
        iris_g = np.clip(0.32 - 0.28 * np.power(v_grad, 0.85), 0.0, 1.0)
        iris_b = np.clip(0.36 - 0.32 * np.power(v_grad, 0.85), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Bright ruby lower crescent highlight
        r_glow = np.hypot((ex - ix_c) / (rx_i * 0.74), (ey - (iy_c - 0.026)) / (ry_i * 0.38))
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.28, 1.0, r_glow)) * 0.80, (1.00, 0.44, 0.48))

        # Thick dark gothic limbal ring + sleek dark pupil
        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.80, 0.96, r_iris) * 0.92, (0.14, 0.02, 0.04))
        r_pupil = np.hypot((ex - ix_c) / 0.022, (ey - (iy_c + 0.004)) / 0.027)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.85, 1.04, r_pupil)), (0.07, 0.02, 0.03))
        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.018, 0.002, y_top - ey) * 0.50, (0.20, 0.02, 0.04))

        # Crisp anime catchlights
        r_hi1 = np.hypot((ex - (ix_c + 0.024)) / 0.016, (ey - (iy_c + 0.025)) / 0.014)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi1)), (1.0, 1.0, 1.0))
        r_hi2 = np.hypot((ex - (ix_c - 0.022)) / 0.008, (ey - (iy_c - 0.022)) / 0.008)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.72, 1.04, r_hi2)), (1.0, 0.96, 0.96))

        # Heavy gothic winged black upper eyeliner with crimson-red outer shadow accent (matches portrait!)
        u_lash_pts = [
            (cx + side * (-0.086), cy - 0.024),
            (cx + side * (-0.046), cy + 0.036),
            (cx + side * ( 0.004), cy + 0.076),
            (cx + side * ( 0.056), cy + 0.076),
            (cx + side * ( 0.092), cy + 0.056),
            (cx + side * ( 0.120), cy + 0.076),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.006, 0.0125, 0.0165, 0.0165, 0.0135, 0.0030])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.05, 0.02, 0.04))

        # Lower gothic black eyeliner
        l_lash_pts = [
            (cx + side * (-0.070), cy - 0.038),
            (cx + side * (-0.006), cy - 0.064),
            (cx + side * ( 0.056), cy - 0.046),
            (cx + side * ( 0.094), cy + 0.004),
        ]
        d_ll = kit.polyline_sdf(U, V, l_lash_pts, [0.0038, 0.0056, 0.0062, 0.0045])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ll), (0.06, 0.02, 0.04))

        # SIGNATURE RED INVERTED-TRIANGLE TEAR MARK UNDER EACH EYE (exact portrait detail!)
        tear_pts = [(cx + side * 0.006, cy - 0.066), (cx + side * 0.002, cy - 0.118)]
        d_tear = kit.polyline_sdf(U, V, tear_pts, [0.014, 0.0015])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_tear), (0.86, 0.12, 0.18))

        # Playful arched dark eyebrows
        brow_pts = [
            (cx + side * (-0.072), cy + 0.094),
            (cx + side * ( 0.005), cy + 0.118),
            (cx + side * ( 0.078), cy + 0.112),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.004, 0.0065, 0.0028])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.16, 0.08, 0.10))

    # Signature cheeky wide dark-plum mischievous smile (exact portrait grin!)
    m_x = (U - 0.500) / 0.064
    m_y = V - 0.302
    # Curved smile arc: upper lip gently curved, lower open grin dipping down
    y_m_top = 0.012 * (m_x ** 2)
    y_m_bot = -0.028 * np.clip(1.0 - m_x ** 2, 0.0, 1.0) + 0.010 * (m_x ** 2)
    in_mx = kit.smoothstep(-1.05, -0.92, m_x) * (1.0 - kit.smoothstep(0.92, 1.05, m_x))
    mouth_fill = in_mx * kit.smoothstep(-aa, aa, m_y - y_m_bot) * kit.smoothstep(-aa, aa, y_m_top - m_y)
    kit.alpha_blend(canvas, mouth_fill, (0.16, 0.05, 0.09))

    smile_line = [(0.422, 0.316), (0.465, 0.302), (0.500, 0.300), (0.535, 0.302), (0.578, 0.316)]
    d_sl = kit.polyline_sdf(U, V, smile_line, [0.0025, 0.0042, 0.0046, 0.0042, 0.0025])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_sl), (0.09, 0.03, 0.05))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_PunkGirl_Face.png")


IMG_FACE = generate_punk_girl_face_texture()
IMG_HAIR_BLONDE = kit.generate_hair_gradient_texture(
    HAIR_BLONDE_PATH, "Chibi_PunkGirl_HairBlonde.png",
    root_rgb=(0.76, 0.58, 0.28),
    mid_rgb=(0.98, 0.87, 0.58),
    tip_rgb=(1.00, 0.94, 0.72),
    hi_rgb=(1.00, 0.99, 0.90),
    band_v=0.56, band_strength=0.35
)
IMG_HAIR_PINK = kit.generate_hair_gradient_texture(
    HAIR_PINK_PATH, "Chibi_PunkGirl_HairPink.png",
    root_rgb=(0.68, 0.10, 0.36),
    mid_rgb=(0.95, 0.24, 0.54),
    tip_rgb=(1.00, 0.48, 0.72),
    hi_rgb=(1.00, 0.78, 0.88),
    band_v=0.55, band_strength=0.38
)

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.992, 0.912, 0.902, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_BLONDE = kit.make_mat("Mat_Hair_Blonde",  (1.0, 1.0, 1.0, 1.0), roughness=0.84, specular=0.06, image=IMG_HAIR_BLONDE)
MAT_HAIR_PINK   = kit.make_mat("Mat_Hair_Pink",    (1.0, 1.0, 1.0, 1.0), roughness=0.84, specular=0.06, image=IMG_HAIR_PINK)
MAT_JACKET_RED  = kit.make_mat("Mat_Jacket_Red",   (0.88, 0.10, 0.15, 1.0), roughness=0.86, specular=0.05)
MAT_PUNK_BLACK  = kit.make_mat("Mat_Punk_Black",   (0.11, 0.11, 0.13, 1.0), roughness=0.90, specular=0.04)
MAT_ARMOR_GREY  = kit.make_mat("Mat_Armor_Grey",   (0.24, 0.24, 0.28, 1.0), roughness=0.78, metallic=0.08, specular=0.08)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver", (0.80, 0.83, 0.88, 1.0), roughness=0.40, metallic=0.68, specular=0.25)
MAT_CHARM_YEL   = kit.make_mat("Mat_Charm_Yellow", (0.98, 0.80, 0.16, 1.0), roughness=0.86, specular=0.05)
MAT_WHITE_PATCH = kit.make_mat("Mat_White_Patch",  (0.96, 0.96, 0.98, 1.0), roughness=0.88, specular=0.04)


def build_punk_girl_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="female_v")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.365, 0.018, HZ - 0.035), (0.046, 0.034, 0.072),
            MAT_SKIN, rot_euler=(0.06, side * -0.20, side * 0.16)
        ))

    # Plush Voluminous Blonde Crown & Back Hair Dome (full & balanced front-to-back from top-down camera!)
    head_parts.append(kit.create_hair_crown_shell(
        HZ, MAT_HAIR_BLONDE,
        rx_base=0.424, ry_base=0.435, rz_base=0.408,
        cutout_z=0.48, y_shift=0.052, z_shift=0.036
    ))

    # Smooth Crown-Rooted Wispy Cream-Blonde Anime Bangs (tapered roots buried seamlessly at top crown part — ZERO cut-off shelf!)
    bang_ribbons = [
        # Center wispy fringe lock (flows smoothly from top crown part -> arch -> sharp tip!)
        [
            (Vector(( 0.01, -0.02, HZ + 0.425)), 0.028, 0.012, -0.04),
            (Vector(( 0.02, -0.23, HZ + 0.365)), 0.088, 0.028, -0.06),
            (Vector(( 0.00, -0.36, HZ + 0.205)), 0.078, 0.022, -0.04),
            (Vector((-0.02, -0.38, HZ + 0.070)), 0.046, 0.014,  0.00),
            (Vector((-0.03, -0.37, HZ - 0.030)), 0.006, 0.004,  0.00),
        ],
        # Center-left secondary wispy lock
        [
            (Vector((-0.03, -0.02, HZ + 0.422)), 0.028, 0.012,  0.10),
            (Vector((-0.09, -0.23, HZ + 0.355)), 0.080, 0.026,  0.14),
            (Vector((-0.11, -0.35, HZ + 0.195)), 0.068, 0.020,  0.16),
            (Vector((-0.12, -0.36, HZ + 0.070)), 0.038, 0.012,  0.18),
            (Vector((-0.11, -0.35, HZ - 0.020)), 0.006, 0.004,  0.18),
        ],
        # Center-right secondary wispy lock
        [
            (Vector(( 0.04, -0.02, HZ + 0.422)), 0.028, 0.012, -0.10),
            (Vector(( 0.10, -0.23, HZ + 0.355)), 0.080, 0.026, -0.16),
            (Vector(( 0.14, -0.34, HZ + 0.195)), 0.068, 0.020, -0.20),
            (Vector(( 0.15, -0.35, HZ + 0.070)), 0.038, 0.012, -0.22),
            (Vector(( 0.14, -0.34, HZ - 0.020)), 0.006, 0.004, -0.22),
        ],
        # Outer forehead swept bangs (left & right — smoothly rooted in upper crown!)
        [
            (Vector((-0.06, -0.01, HZ + 0.415)), 0.032, 0.014,  0.22),
            (Vector((-0.19, -0.20, HZ + 0.325)), 0.084, 0.026,  0.34),
            (Vector((-0.27, -0.30, HZ + 0.165)), 0.074, 0.021,  0.42),
            (Vector((-0.29, -0.29, HZ + 0.020)), 0.044, 0.014,  0.48),
            (Vector((-0.26, -0.27, HZ - 0.090)), 0.006, 0.004,  0.50),
        ],
        [
            (Vector(( 0.06, -0.01, HZ + 0.415)), 0.032, 0.014, -0.22),
            (Vector(( 0.19, -0.20, HZ + 0.325)), 0.084, 0.026, -0.34),
            (Vector(( 0.27, -0.30, HZ + 0.165)), 0.074, 0.021, -0.42),
            (Vector(( 0.29, -0.29, HZ + 0.020)), 0.044, 0.014, -0.48),
            (Vector(( 0.26, -0.27, HZ - 0.090)), 0.006, 0.004, -0.50),
        ],
    ]
    # Long dramatic cheek-framing curved blonde locks (smoothly rooted from upper crown!)
    for side in (-1.0, 1.0):
        bang_ribbons.append([
            (Vector((side * 0.10,  0.00, HZ + 0.400)), 0.036, 0.016, side * -0.45),
            (Vector((side * 0.27, -0.15, HZ + 0.255)), 0.096, 0.030, side * -0.68),
            (Vector((side * 0.37, -0.22, HZ + 0.040)), 0.086, 0.025, side * -0.76),
            (Vector((side * 0.35, -0.21, HZ - 0.150)), 0.054, 0.016, side * -0.82),
            (Vector((side * 0.26, -0.18, HZ - 0.320)), 0.007, 0.004, side * -0.85),
        ])
        bang_ribbons.append([
            (Vector((side * 0.15,  0.04, HZ + 0.375)), 0.036, 0.016, side * -0.82),
            (Vector((side * 0.33, -0.04, HZ + 0.195)), 0.090, 0.028, side * -1.08),
            (Vector((side * 0.40, -0.05, HZ - 0.020)), 0.076, 0.023, side * -1.14),
            (Vector((side * 0.37, -0.05, HZ - 0.190)), 0.044, 0.014, side * -1.18),
            (Vector((side * 0.28, -0.04, HZ - 0.310)), 0.007, 0.004, side * -1.20),
        ])
    for idx, br in enumerate(bang_ribbons):
        head_parts.append(kit.create_ribbon_hair_strand(f"BlondeBang_{idx}", br, MAT_HAIR_BLONDE, v_start=0.06, v_end=0.98))

    # VOLUMINOUS SCULPTED BACK-CROWN BLONDE HAIR LAYERS (balances front bangs 1:1 from top-down!)
    # Sweeps smoothly from the top crown part backward & around the full back hemisphere down to the nape!
    back_crown_specs = [
        ( 0.00,  0.00, 0.440,  0.00, 0.28, 0.445,  0.00, 0.49, 0.18,  0.00, 0.45, -0.14,  0.00, 0.34, -0.34, 0.145, 0.046,  0.00),
        (-0.04,  0.00, 0.435, -0.14, 0.26, 0.430, -0.22, 0.46, 0.16, -0.20, 0.42, -0.14, -0.15, 0.32, -0.33, 0.135, 0.044,  0.34),
        ( 0.04,  0.00, 0.435,  0.14, 0.26, 0.430,  0.22, 0.46, 0.16,  0.20, 0.42, -0.14,  0.15, 0.32, -0.33, 0.135, 0.044, -0.34),
        (-0.08,  0.01, 0.422, -0.26, 0.20, 0.390, -0.37, 0.36, 0.12, -0.34, 0.33, -0.16, -0.26, 0.24, -0.32, 0.125, 0.040,  0.68),
        ( 0.08,  0.01, 0.422,  0.26, 0.20, 0.390,  0.37, 0.36, 0.12,  0.34, 0.33, -0.16,  0.26, 0.24, -0.32, 0.125, 0.040, -0.68),
        (-0.12,  0.02, 0.400, -0.34, 0.12, 0.320, -0.43, 0.20, 0.06, -0.40, 0.18, -0.18, -0.31, 0.12, -0.31, 0.112, 0.036,  1.02),
        ( 0.12,  0.02, 0.400,  0.34, 0.12, 0.320,  0.43, 0.20, 0.06,  0.40, 0.18, -0.18,  0.31, 0.12, -0.31, 0.112, 0.036, -1.02),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, x2, y2, z2, x3, y3, z3, x4, y4, z4, w, t, roll) in enumerate(back_crown_specs):
        b_strand = [
            (Vector((x0, y0, HZ + z0)), w * 0.26, t * 0.35, roll * 0.4),
            (Vector((x1, y1, HZ + z1)), w * 0.96, t * 1.00, roll * 0.8),
            (Vector((x2, y2, HZ + z2)), w * 1.00, t * 0.95, roll),
            (Vector((x3, y3, HZ + z3)), w * 0.68, t * 0.65, roll),
            (Vector((x4, y4, HZ + z4)), 0.012,    0.008,    roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"BackCrown_{idx}", b_strand, MAT_HAIR_BLONDE, v_start=0.06, v_end=0.92))

    # ICONIC TWIN TWO-TONE BRAIDED BUNS (positioned cleanly outside the plush fuller crown!)
    for side in (-1.0, 1.0):
        bun_c = Vector((side * 0.415, 0.065, HZ + 0.260))
        # Black & Red striped hair ribbon band wrapping the bun base
        head_parts.append(kit.create_torus_ring(
            f"BunBandBlack_{side}", bun_c - Vector((side * 0.065, 0.0, 0.035)),
            major_r=0.145, minor_r=0.032, mat=MAT_PUNK_BLACK,
            rot_euler=(0.15, side * 1.15, 0.0)
        ))
        head_parts.append(kit.create_torus_ring(
            f"BunBandRed_{side}", bun_c - Vector((side * 0.055, 0.0, 0.030)),
            major_r=0.152, minor_r=0.016, mat=MAT_JACKET_RED,
            rot_euler=(0.15, side * 1.15, 0.0)
        ))

        # Sculpted Blonde Braided Inner Bun (4 interlocking braided blonde loops!)
        for k in range(5):
            ang = (2.0 * math.pi * k) / 5.0
            dy = math.cos(ang) * 0.085
            dz = math.sin(ang) * 0.085
            braid_bead = kit.create_ellipsoid(
                f"BlondeBraid_{side}_{k}",
                bun_c + Vector((side * 0.01, dy, dz)),
                (0.088, 0.074, 0.074),
                MAT_HAIR_BLONDE,
                rot_euler=(ang, side * 0.25, 0.0),
                uv_v=0.45 + 0.25 * math.sin(ang)
            )
            head_parts.append(braid_bead)

        # Sleek Layered Hot-Pink Crescent Anime Bun Fan (sweeping cleanly outward & slightly back!)
        pink_origin = bun_c + Vector((side * 0.045, 0.025, 0.0))
        for k in range(6):
            frac = (k / 5.0) - 0.5  # -0.5..+0.5
            ang = frac * 1.15 * math.pi
            dz = math.sin(ang) * 0.18
            dx = (0.14 + 0.06 * math.cos(ang)) * side
            tuft = [
                (pink_origin + Vector((side * -0.02, -0.01, dz * 0.25)), 0.068, 0.036, 0.0),
                (pink_origin + Vector((dx * 0.65,     0.02, dz * 0.80)), 0.072, 0.036, 0.0),
                (pink_origin + Vector((dx * 1.10,     0.06, dz * 1.05)), 0.042, 0.020, 0.0),
                (pink_origin + Vector((dx * 1.32,     0.10, dz * 0.92)), 0.008, 0.005, 0.0),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(
                f"PinkTuft_{side}_{k}", tuft, MAT_HAIR_PINK,
                v_start=0.06, v_end=0.98, radial_center=bun_c
            ))

        # Dangling Black & Red Pointed Ribbon Tails hanging cleanly behind each bun!
        for r_idx, (r_mat, y_o, x_o) in enumerate(((MAT_PUNK_BLACK, 0.03, 0.0), (MAT_JACKET_RED, 0.05, 0.02))):
            rtail = [
                (bun_c + Vector((side * (-0.02 + x_o), y_o, -0.08)), 0.034, 0.010, side * -0.8),
                (bun_c + Vector((side * ( 0.04 + x_o), y_o + 0.02, -0.22)), 0.038, 0.010, side * -0.8),
                (bun_c + Vector((side * ( 0.08 + x_o), y_o + 0.03, -0.35)), 0.008, 0.004, side * -0.8),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(f"RibbonTail_{side}_{r_idx}", rtail, r_mat, v_start=0.2, v_end=0.8))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_punk_girl_body():
    body_parts = []

    # Neck + Black/Red Punk Choker (signature portrait detail!)
    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.106, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
        (Vector((0.0, 0.0, 1.50)), Vector((0.090, 0.0, 0.0)), Vector((0.0, 0.086, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)
    body_parts.append(kit.create_torus_ring("Punk_ChokerBlack", (0.0, 0.0, 1.325), 0.104, 0.024, MAT_PUNK_BLACK))
    body_parts.append(kit.create_torus_ring("Punk_ChokerRed",   (0.0, 0.0, 1.325), 0.114, 0.010, MAT_JACKET_RED))

    # Exposed Pale Midriff + Fitted Black Cropped Punk Bustier Top (exact portrait!)
    midriff_rings = [
        (Vector((0.0,  0.000, 0.78)), Vector((0.194, 0.0, 0.0)), Vector((0.0, 0.148, 0.0))),
        (Vector((0.0,  0.002, 0.91)), Vector((0.178, 0.0, 0.0)), Vector((0.0, 0.138, 0.0))),
        (Vector((0.0, -0.006, 1.00)), Vector((0.190, 0.0, 0.0)), Vector((0.0, 0.150, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_Midriff", midriff_rings, MAT_SKIN))

    top_rings = [
        (Vector((0.0, -0.008, 0.96)), Vector((0.200, 0.0, 0.0)), Vector((0.0, 0.158, 0.0))),
        (Vector((0.0, -0.024, 1.12)), Vector((0.230, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, -0.010, 1.24)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.158, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_CropTop", top_rings, MAT_PUNK_BLACK))
    body_parts.append(kit.create_rounded_box("Top_CenterBuckle", (0.0, -0.196, 1.16), (0.052, 0.024, 0.044), MAT_METAL_SILV, bevel_width=0.006))

    # Vibrant Scarlet-Red Open Cropped Biker Jacket (z = 0.91..1.30)
    bm_jkt = bmesh.new()
    z_levels = [
        (0.91, 0.214, 0.165,  0.000),
        (1.02, 0.234, 0.184, -0.008),
        (1.14, 0.254, 0.204, -0.016),
        (1.24, 0.244, 0.186, -0.008),
        (1.30, 0.195, 0.154,  0.000),
    ]
    ang_steps = 36
    ang_start, ang_end = -0.32 * math.pi, 1.32 * math.pi
    thick = 0.030
    outer_r, inner_r = [], []
    for z, rx, ry, y_off in z_levels:
        or_, ir_ = [], []
        for i in range(ang_steps):
            ang = ang_start + (i / (ang_steps - 1)) * (ang_end - ang_start)
            ca, sa = math.cos(ang), math.sin(ang)
            or_.append(bm_jkt.verts.new(Vector((ca * (rx + thick), y_off + sa * (ry + thick), z))))
            ir_.append(bm_jkt.verts.new(Vector((ca * (rx - 0.005), y_off + sa * (ry - 0.005), z))))
        outer_r.append(or_)
        inner_r.append(ir_)
    for r in range(len(z_levels) - 1):
        for i in range(ang_steps - 1):
            bm_jkt.faces.new((outer_r[r][i], outer_r[r][i+1], outer_r[r+1][i+1], outer_r[r+1][i]))
            bm_jkt.faces.new((inner_r[r][i+1], inner_r[r][i], inner_r[r+1][i], inner_r[r+1][i+1]))
        bm_jkt.faces.new((inner_r[r][0], outer_r[r][0], outer_r[r+1][0], inner_r[r+1][0]))
        bm_jkt.faces.new((outer_r[r][-1], inner_r[r][-1], inner_r[r+1][-1], outer_r[r+1][-1]))
    for i in range(ang_steps - 1):
        bm_jkt.faces.new((inner_r[0][i], inner_r[0][i+1], outer_r[0][i+1], outer_r[0][i]))
        bm_jkt.faces.new((outer_r[-1][i], outer_r[-1][i+1], inner_r[-1][i+1], inner_r[-1][i]))
    bmesh.ops.recalc_face_normals(bm_jkt, faces=bm_jkt.faces)
    mj = bpy.data.meshes.new("Jacket_Red")
    bm_jkt.to_mesh(mj)
    bm_jkt.free()
    oj = bpy.data.objects.new("Jacket_Red", mj)
    bpy.context.collection.objects.link(oj)
    body_parts.append(kit.assign_single_material(oj, MAT_JACKET_RED))

    # Popped Red Collar + Bright Metallic Silver Zipper Tracks + Back Punk Emblem
    bm_col = bmesh.new()
    col_levels = [(1.26, 0.155, 0.136, 0.0), (1.33, 0.180, 0.156, 0.012), (1.40, 0.206, 0.176, 0.022)]
    co_r, ci_r = [], []
    for z, rx, ry, y_off in col_levels:
        o_, i_ = [], []
        for k in range(24):
            ang = (-0.28 * math.pi) + (k / 23.0) * (1.56 * math.pi)
            ca, sa = math.cos(ang), math.sin(ang)
            o_.append(bm_col.verts.new(Vector((ca * (rx + 0.022), y_off + sa * (ry + 0.022), z))))
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
    body_parts.append(kit.assign_single_material(oc, MAT_JACKET_RED))

    for side in (1.0, -1.0):
        body_parts.append(kit.create_rounded_box(f"Zipper_{side}", (side * 0.114, -0.195, 1.10), (0.018, 0.024, 0.36), MAT_METAL_SILV, bevel_width=0.004))
        body_parts.append(kit.create_ellipsoid(f"ShoulderJoint_{side}", (side * 0.215, 0.0, 1.23), (0.130, 0.124, 0.124), MAT_JACKET_RED))

    # Stylish black punk X-strap on back of red jacket
    for side in (1.0, -1.0):
        body_parts.append(kit.create_rounded_box(f"BackX_{side}", (0.0, 0.212, 1.10), (0.036, 0.022, 0.26), MAT_PUNK_BLACK, rot_euler=(0.0, side * 0.72, 0.0), bevel_width=0.005))

    # Puffy Red Bomber Sleeves + Ribbed Black Cuffs + Red-Trimmed Fingerless Punk Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        sleeve_rings = [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.122, 0.0)), Vector((0.0, 0.0, 0.122 * side))),
            (Vector((side * 0.32, 0.0, AZ)), Vector((0.0, 0.120, 0.0)), Vector((0.0, 0.0, 0.120 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.116, 0.0)), Vector((0.0, 0.0, 0.116 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_JACKET_RED))
        # White & black punk sleeve patch
        body_parts.append(kit.create_rounded_box(f"SleevePatch_{side}", (side * 0.33, -0.02, AZ + 0.096), (0.088, 0.092, 0.040), MAT_PUNK_BLACK, bevel_width=0.008))
        body_parts.append(kit.create_rounded_box(f"SleeveChev_{side}", (side * 0.33, -0.02, AZ + 0.112), (0.056, 0.060, 0.018), MAT_WHITE_PATCH, bevel_width=0.004))

        cuff = kit.create_rounded_box(f"SleeveCuff_{side}", (side * 0.515, 0.0, AZ), (0.078, 0.236, 0.232), MAT_PUNK_BLACK, bevel_width=0.018)
        forearm = kit.create_loft_object(f"Forearm_{side}", [
            (Vector((side * 0.54, 0.0, AZ)), Vector((0.0, 0.084, 0.0)), Vector((0.0, 0.0, 0.082 * side))),
            (Vector((side * 0.65, 0.0, AZ)), Vector((0.0, 0.076, 0.0)), Vector((0.0, 0.0, 0.074 * side))),
        ], MAT_SKIN)
        wrist_red = kit.create_rounded_box(f"WristRed_{side}", (side * 0.66, 0.0, AZ), (0.068, 0.204, 0.198), MAT_JACKET_RED, bevel_width=0.014)
        wrist_blk = kit.create_rounded_box(f"WristBlk_{side}", (side * 0.695, 0.0, AZ), (0.044, 0.210, 0.204), MAT_PUNK_BLACK, bevel_width=0.010)
        glove_palm = kit.create_rounded_box(f"GlovePalm_{side}", (side * 0.775, -0.004, AZ), (0.135, 0.170, 0.132), MAT_PUNK_BLACK, bevel_width=0.022)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.865, -0.004, AZ - 0.006), (0.062, 0.072, 0.054), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.78, -0.088, AZ), (0.036, 0.038, 0.032), MAT_SKIN)
        body_parts.extend([cuff, forearm, wrist_red, wrist_blk, glove_palm, fingers, thumb])

    # Punk Belt + Silver Buckle + Iconic Yellow Smiley Charm & Red Cross Charm (exact portrait!)
    belt_rings = [
        (Vector((0.0, 0.002, 0.74)), Vector((0.246, 0.0, 0.0)), Vector((0.0, 0.190, 0.0))),
        (Vector((0.0, 0.000, 0.84)), Vector((0.240, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Punk_Belt", belt_rings, MAT_PUNK_BLACK))
    body_parts.append(kit.create_rounded_box("Belt_Buckle", (0.0, -0.196, 0.792), (0.105, 0.042, 0.086), MAT_METAL_SILV, bevel_width=0.010))
    body_parts.append(kit.create_rounded_box("Belt_BuckleCore", (0.0, -0.210, 0.792), (0.046, 0.022, 0.058), MAT_PUNK_BLACK, bevel_width=0.005))

    # Right hip red-trimmed pouch + yellow smiley badge + red cross charm (-X = viewer left!)
    body_parts.append(kit.create_rounded_box("HipPouch_R", (-0.238, -0.05, 0.75), (0.088, 0.140, 0.135), MAT_PUNK_BLACK, bevel_width=0.014))
    body_parts.append(kit.create_rounded_box("HipPouchTrim_R", (-0.255, -0.05, 0.79), (0.068, 0.146, 0.032), MAT_JACKET_RED, bevel_width=0.008))
    body_parts.append(kit.create_ellipsoid("Smiley_Charm", (-0.125, -0.205, 0.725), (0.046, 0.022, 0.046), MAT_CHARM_YEL))
    body_parts.append(kit.create_rounded_box("Cross_CharmH", (-0.065, -0.205, 0.715), (0.048, 0.018, 0.016), MAT_JACKET_RED, bevel_width=0.003))
    body_parts.append(kit.create_rounded_box("Cross_CharmV", (-0.065, -0.205, 0.715), (0.016, 0.018, 0.052), MAT_JACKET_RED, bevel_width=0.003))

    # Sleek Matte-Black Punk Pants, Silver-Buckled Thigh Straps, Red Boot Cuffs & Combat Boots
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.162, 0.0, 0.0)), Vector((0.0, 0.138, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.244, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.230, 0.0, 0.0)), Vector((0.0, 0.176, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_PUNK_BLACK))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.144,  0.004, 0.68)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.148,  0.004, 0.54)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.130, 0.0))),
            (Vector((side * 0.150,  0.000, 0.42)), Vector((0.112, 0.0, 0.0)), Vector((0.0, -0.116, 0.0))),
            (Vector((side * 0.152, -0.002, 0.30)), Vector((0.106, 0.0, 0.0)), Vector((0.0, -0.110, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.120, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_PUNK_BLACK))
        # Buckled thigh strap
        thigh_strap = [
            (Vector((side * 0.148, 0.002, 0.52)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.136, 0.0))),
            (Vector((side * 0.148, 0.002, 0.56)), Vector((0.133, 0.0, 0.0)), Vector((0.0, -0.137, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"ThighStrap_{side}", thigh_strap, MAT_ARMOR_GREY))
        body_parts.append(kit.create_rounded_box(f"ThighBuckle_{side}", (side * 0.148, -0.138, 0.54), (0.048, 0.022, 0.048), MAT_METAL_SILV, bevel_width=0.006))
        body_parts.append(kit.create_rounded_box(f"KneePad_{side}", (side * 0.152, -0.106, 0.370), (0.118, 0.048, 0.124), MAT_ARMOR_GREY, bevel_width=0.016))

        # Red Punk Boot Cuff + Heavy Combat Boot
        boot_cuff = [
            (Vector((side * 0.152, 0.000, 0.24)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
            (Vector((side * 0.152, 0.000, 0.185)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.120, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootCuff_{side}", boot_cuff, MAT_JACKET_RED))
        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.195)), Vector((0.108, 0.0, 0.0)), Vector((0.0, -0.114, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.152, -0.052, 0.080)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.152, -0.062, 0.038)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_PUNK_BLACK))
        boot_sole = [
            (Vector((side * 0.152, -0.062, 0.044)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_ARMOR_GREY))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_punk_girl_head()
body_obj = build_punk_girl_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_PunkGirl")

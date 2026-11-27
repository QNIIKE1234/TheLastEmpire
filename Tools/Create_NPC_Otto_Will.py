import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
import shutil
import time
import numpy as np
from mathutils import Vector, Euler
import ChibiAnimeKit as kit

NPC_OUT_DIR = os.path.join(kit.OUT_DIR, "NPCs")
TMP_OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_tmp_npc_otto_export")
os.makedirs(NPC_OUT_DIR, exist_ok=True)
os.makedirs(TMP_OUT_DIR, exist_ok=True)
os.makedirs(kit.TEX_DIR, exist_ok=True)


def _copy_with_retry(src, dst, retries=12, delay=0.5):
    for attempt in range(retries):
        try:
            shutil.copy2(src, dst)
            return
        except OSError as e:
            if attempt == retries - 1:
                raise
            print(f"[Retry {attempt + 1}/{retries}] Waiting to overwrite {os.path.basename(dst)}: {e}")
            time.sleep(delay)


def export_npc_with_kit(scene, head_obj, body_obj, base_name):
    orig_out = kit.OUT_DIR
    kit.OUT_DIR = TMP_OUT_DIR
    try:
        kit.build_rig_animate_and_export(scene, head_obj, body_obj, base_name)
    finally:
        kit.OUT_DIR = orig_out

    for suffix in ("_Rigged.fbx", "_Source.blend", "_Preview.png", "_Preview_Back.png", "_Preview_Top.png"):
        src = os.path.join(TMP_OUT_DIR, f"{base_name}{suffix}")
        dst = os.path.join(NPC_OUT_DIR, f"{base_name}{suffix}")
        if os.path.exists(src):
            _copy_with_retry(src, dst)


# ==============================================================================
#  NPC_Otto_Gunsmith — Swashbuckler / Pirate Artisan Male (Will Turner Reference)
#  Long dark wavy hair tied in a low ponytail, light goatee/mustache,
#  billowy cream linen shirt, weathered taupe waistcoat, diagonal leather baldric
#  with silver buckle, wide waist belt, dark trousers & rugged leather boots.
# ==============================================================================

def generate_otto_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.955, 0.775, 0.655)
    SKIN_WARM   = (0.890, 0.660, 0.525)
    SKIN_SHADOW = (0.775, 0.530, 0.405)
    BEARD_COL   = (0.145, 0.098, 0.072)
    STUBBLE_COL = (0.420, 0.295, 0.230)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    # Soft forehead shadow & warm jaw contour
    kit.alpha_blend(canvas, kit.smoothstep(0.68, 0.90, V) * 0.18, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.06, V) * 0.16, SKIN_WARM)

    # Subtle masculine cheekbone warmth (not girly pink blush)
    for side in (-1.0, 1.0):
        d_ch = np.hypot((U - (0.5 + side * 0.185)) / 0.072, (V - 0.370) / 0.032)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.20, 1.0, d_ch)) * 0.14, SKIN_WARM)

    # Subtle jawline stubble shadow along lower cheeks & chin
    jaw_mask = (1.0 - kit.smoothstep(0.18, 0.34, V)) * kit.smoothstep(0.06, 0.16, V)
    jaw_x = 1.0 - kit.smoothstep(0.14, 0.30, np.abs(U - 0.5))
    kit.alpha_blend(canvas, jaw_mask * jaw_x * 0.14, STUBBLE_COL)

    # Crisp anime masculine nose bridge shadow
    nose_pts = [(0.496, 0.442), (0.490, 0.386), (0.508, 0.382)]
    d_n = kit.polyline_sdf(U, V, nose_pts, [0.004, 0.009, 0.006])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, d_n)) * 0.44, SKIN_SHADOW)

    # Sharp Handsome Warm-Espresso Male Anime Eyes
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.148
        cy = 0.468
        ex = (U - cx) * side
        ey = V - cy

        # Slightly sleeker/narrower masculine eye aperture
        r_eye = np.hypot(ex / 0.078, ey / 0.052)
        eye_mask = 1.0 - kit.smoothstep(1.0 - aa / 0.078, 1.0 + aa / 0.078, r_eye)
        kit.alpha_blend(canvas, eye_mask, (0.992, 0.990, 0.988))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.026, 0.052, ey) * 0.28, (0.72, 0.68, 0.66))

        rx_i, ry_i = 0.044, 0.047
        r_iris = np.hypot(ex / rx_i, (ey + 0.002) / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        # Deep warm espresso-amber iris gradient
        v_grad = np.clip((ey + ry_i) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.46 - 0.28 * np.power(v_grad, 1.1), 0.0, 1.0)
        iris_g = np.clip(0.28 - 0.19 * np.power(v_grad, 1.1), 0.0, 1.0)
        iris_b = np.clip(0.16 - 0.11 * np.power(v_grad, 1.1), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Warm amber lower catch-glow + crisp dark rim + center pupil
        r_glow = np.hypot(ex / (rx_i * 0.68), (ey + 0.018) / (ry_i * 0.34))
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.25, 1.0, r_glow)) * 0.55, (0.78, 0.52, 0.26))
        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.80, 0.96, r_iris) * 0.90, (0.09, 0.05, 0.03))
        r_pupil = np.hypot(ex / 0.018, (ey + 0.002) / 0.019)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.82, 1.05, r_pupil)), (0.05, 0.03, 0.02))

        # Crisp masculine specular catchlight
        r_hi1 = np.hypot((ex - 0.015) / 0.011, (ey - 0.014) / 0.010)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.72, 1.02, r_hi1)), (1.0, 0.98, 0.96))
        r_hi2 = np.hypot((ex + 0.014) / 0.005, (ey + 0.012) / 0.005)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.72, 1.02, r_hi2)), (0.98, 0.94, 0.90))

        # Clean bold masculine upper eyelid line (no girly winged flick)
        u_lid_pts = [
            (cx + side * (-0.078), cy + 0.004),
            (cx + side * (-0.032), cy + 0.044),
            (cx + side * ( 0.024), cy + 0.049),
            (cx + side * ( 0.078), cy + 0.024),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lid_pts, [0.0045, 0.0115, 0.0125, 0.0055])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.06, 0.04, 0.03))

        # Subtle lower eyelid crease
        l_lid_pts = [
            (cx + side * (-0.052), cy - 0.042),
            (cx + side * ( 0.006), cy - 0.052),
            (cx + side * ( 0.058), cy - 0.038),
        ]
        d_ll = kit.polyline_sdf(U, V, l_lid_pts, [0.0020, 0.0032, 0.0020])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_ll)) * 0.65, (0.14, 0.09, 0.07))

        # Bold expressive masculine eyebrows (warm dark espresso)
        brow_pts = [
            (cx + side * (-0.084), cy + 0.078),
            (cx + side * (-0.014), cy + 0.092),
            (cx + side * ( 0.052), cy + 0.096),
            (cx + side * ( 0.092), cy + 0.082),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.0085, 0.0135, 0.0120, 0.0045])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), BEARD_COL)

    # Signature Will Turner Light Mustache + Confident Slight Smirk + Neat Chin Goatee!
    # 1. Thin groomed mustache curving smoothly above the mouth
    for side in (-1.0, 1.0):
        stache_pts = [
            (0.500 + side * 0.006, 0.338),
            (0.500 + side * 0.034, 0.336),
            (0.500 + side * 0.064, 0.324),
            (0.500 + side * 0.082, 0.308),
        ]
        d_st = kit.polyline_sdf(U, V, stache_pts, [0.0055, 0.0085, 0.0072, 0.0035])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 1.5, aa * 1.5, d_st)) * 0.85, BEARD_COL)

    # 2. Confident slight half-smile mouth
    mouth_pts = [
        (0.442, 0.300),
        (0.478, 0.294),
        (0.518, 0.296),
        (0.562, 0.312),
    ]
    d_m = kit.polyline_sdf(U, V, mouth_pts, [0.0028, 0.0050, 0.0050, 0.0030])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_m), (0.36, 0.16, 0.13))

    # 3. Neat soul-patch below lower lip + sculpted chin goatee tuft (signature Reference 1!)
    soul_d = np.hypot((U - 0.500) / 0.014, (V - 0.270) / 0.010)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.65, 1.08, soul_d)) * 0.72, BEARD_COL)

    goatee_pts = [
        (0.456, 0.242),
        (0.482, 0.222),
        (0.500, 0.214),
        (0.518, 0.222),
        (0.544, 0.242),
    ]
    d_gt = kit.polyline_sdf(U, V, goatee_pts, [0.0055, 0.0125, 0.0165, 0.0125, 0.0055])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2.0, aa * 2.0, d_gt)) * 0.82, BEARD_COL)

    # Subtle sideburns near temple edges
    for side in (-1.0, 1.0):
        sb_pts = [
            (0.5 + side * 0.425, 0.500),
            (0.5 + side * 0.415, 0.415),
            (0.5 + side * 0.398, 0.345),
        ]
        d_sb = kit.polyline_sdf(U, V, sb_pts, [0.014, 0.016, 0.008])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, d_sb)) * 0.65, BEARD_COL)

    path = os.path.join(kit.TEX_DIR, "NPC_Otto_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "NPC_Otto_Face.png")


def build_otto_pirate_artisan():
    scene = kit.reset_scene()
    img_face = generate_otto_face_texture()
    img_hair = kit.generate_hair_gradient_texture(
        os.path.join(kit.TEX_DIR, "NPC_Otto_Hair.png"), "NPC_Otto_Hair.png",
        root_rgb=(0.055, 0.036, 0.028),
        mid_rgb=(0.135, 0.092, 0.068),
        tip_rgb=(0.085, 0.055, 0.040),
        hi_rgb=(0.460, 0.355, 0.275),
        band_v=0.46, band_strength=0.42
    )

    m_face      = kit.make_mat("Otto_Face_Tex",    (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin      = kit.make_mat("Otto_Skin",        (0.955, 0.775, 0.655, 1.0), roughness=0.90, specular=0.03)
    m_hair      = kit.make_mat("Otto_Hair",        (1.0, 1.0, 1.0, 1.0), roughness=0.72, specular=0.10, image=img_hair)
    m_cream     = kit.make_mat("Otto_LinenShirt",  (0.925, 0.895, 0.835, 1.0), roughness=0.92, specular=0.03)
    m_cream_shd = kit.make_mat("Otto_LinenCuff",   (0.840, 0.800, 0.730, 1.0), roughness=0.92, specular=0.03)
    m_vest      = kit.make_mat("Otto_TaupeVest",   (0.510, 0.470, 0.400, 1.0), roughness=0.90, specular=0.04)
    m_vest_trim = kit.make_mat("Otto_VestTrim",    (0.360, 0.325, 0.268, 1.0), roughness=0.90, specular=0.04)
    m_leather   = kit.make_mat("Otto_DarkLeather", (0.105, 0.075, 0.058, 1.0), roughness=0.68, specular=0.10)
    m_boot      = kit.make_mat("Otto_BrownBoot",   (0.175, 0.115, 0.078, 1.0), roughness=0.78, specular=0.08)
    m_pants     = kit.make_mat("Otto_NavyPants",   (0.115, 0.145, 0.195, 1.0), roughness=0.90, specular=0.04)
    m_silver    = kit.make_mat("Otto_PewterBuckle",(0.680, 0.705, 0.735, 1.0), roughness=0.38, metallic=0.72, specular=0.25)
    m_cord      = kit.make_mat("Otto_NeckCord",    (0.080, 0.065, 0.055, 1.0), roughness=0.88, specular=0.04)

    # ==========================================================================
    #  HEAD + LONG WAVY DARK HAIR TIED IN LOW PONYTAIL (ผมยาวหยักศกมัดผม!)
    # ==========================================================================
    head_parts = []
    HZ = 1.69
    head_parts.append(kit.create_anime_head_shell(HZ, m_face, m_skin, style="male_sleek"))

    # Masculine ears peeking slightly out beneath swept-back wavy locks
    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.368, 0.015, HZ - 0.025), (0.046, 0.036, 0.076),
            m_skin, rot_euler=(0.06, side * -0.18, side * 0.15)
        ))

    # Sleek slicked-back crown hair shell (cleanly behind jaw & chin — ZERO under-chin bleed!)
    head_parts.append(kit.create_hair_crown_shell(
        HZ, m_hair, rx_base=0.398, ry_base=0.376, rz_base=0.358, cutout_z=0.72, y_shift=0.056, z_shift=0.058
    ))

    # 1. Smooth Sleek Swept-Back Wavy Crown Locks (flush with scalp, tapered hairline, natural wave!)
    top_waves = [
        # Center-left brushed-back wave
        [
            (Vector((-0.07, -0.26, HZ + 0.29)), 0.042, 0.016,  0.10),
            (Vector((-0.06, -0.06, HZ + 0.38)), 0.078, 0.024,  0.06),
            (Vector((-0.05,  0.20, HZ + 0.34)), 0.084, 0.026,  0.04),
            (Vector((-0.03,  0.39, HZ + 0.12)), 0.078, 0.026,  0.02),
            (Vector(( 0.00,  0.39, HZ - 0.12)), 0.062, 0.022,  0.00),
        ],
        # Center-right brushed-back wave
        [
            (Vector(( 0.07, -0.26, HZ + 0.29)), 0.044, 0.016, -0.10),
            (Vector(( 0.06, -0.05, HZ + 0.38)), 0.080, 0.024, -0.08),
            (Vector(( 0.05,  0.20, HZ + 0.34)), 0.084, 0.026, -0.04),
            (Vector(( 0.03,  0.39, HZ + 0.12)), 0.078, 0.026, -0.02),
            (Vector(( 0.01,  0.39, HZ - 0.12)), 0.062, 0.022,  0.00),
        ],
        # Mid-left wave sweeping smoothly over temple toward low tie
        [
            (Vector((-0.18, -0.24, HZ + 0.26)), 0.042, 0.016,  0.32),
            (Vector((-0.28, -0.02, HZ + 0.28)), 0.078, 0.024,  0.48),
            (Vector((-0.32,  0.21, HZ + 0.17)), 0.082, 0.026,  0.44),
            (Vector((-0.23,  0.36, HZ - 0.02)), 0.072, 0.024,  0.26),
            (Vector((-0.05,  0.39, HZ - 0.13)), 0.056, 0.020,  0.10),
        ],
        # Mid-right wave sweeping smoothly over temple toward low tie
        [
            (Vector(( 0.18, -0.24, HZ + 0.26)), 0.042, 0.016, -0.32),
            (Vector(( 0.28, -0.02, HZ + 0.28)), 0.078, 0.024, -0.48),
            (Vector(( 0.32,  0.21, HZ + 0.17)), 0.082, 0.026, -0.44),
            (Vector(( 0.23,  0.36, HZ - 0.02)), 0.072, 0.024, -0.26),
            (Vector(( 0.05,  0.39, HZ - 0.13)), 0.056, 0.020, -0.10),
        ],
        # Outer-left side wave tucked smoothly behind ear to low tie
        [
            (Vector((-0.26, -0.17, HZ + 0.18)), 0.044, 0.016,  0.58),
            (Vector((-0.37,  0.03, HZ + 0.11)), 0.076, 0.024,  0.74),
            (Vector((-0.36,  0.22, HZ - 0.02)), 0.074, 0.024,  0.62),
            (Vector((-0.22,  0.36, HZ - 0.11)), 0.064, 0.022,  0.30),
            (Vector((-0.05,  0.39, HZ - 0.14)), 0.052, 0.018,  0.10),
        ],
        # Outer-right side wave tucked smoothly behind ear to low tie
        [
            (Vector(( 0.26, -0.17, HZ + 0.18)), 0.044, 0.016, -0.58),
            (Vector(( 0.37,  0.03, HZ + 0.11)), 0.076, 0.024, -0.74),
            (Vector(( 0.36,  0.22, HZ - 0.02)), 0.074, 0.024, -0.62),
            (Vector(( 0.22,  0.36, HZ - 0.11)), 0.064, 0.022, -0.30),
            (Vector(( 0.05,  0.39, HZ - 0.14)), 0.052, 0.018, -0.10),
        ],
    ]
    for idx, st in enumerate(top_waves):
        head_parts.append(kit.create_ribbon_hair_strand(f"SweptWave_{idx}", st, m_hair, v_start=0.06, v_end=0.92))

    # 2. Loose Wavy Face-Framing Strands (natural wavy locks falling softly at temples & jawline like Ref 1!)
    loose_strands = [
        # Soft wavy front-right brow wisp (iconic Will Turner stray forehead lock)
        [
            (Vector((-0.12, -0.24, HZ + 0.31)), 0.030, 0.014,  0.20),
            (Vector((-0.23, -0.33, HZ + 0.20)), 0.036, 0.014,  0.38),
            (Vector((-0.29, -0.30, HZ + 0.08)), 0.026, 0.011,  0.52),
            (Vector((-0.26, -0.29, HZ - 0.01)), 0.008, 0.004,  0.58),
        ],
        # Soft wavy front-left temple lock curving past ear
        [
            (Vector(( 0.15, -0.23, HZ + 0.30)), 0.032, 0.014, -0.24),
            (Vector(( 0.28, -0.28, HZ + 0.16)), 0.038, 0.015, -0.50),
            (Vector(( 0.35, -0.18, HZ + 0.01)), 0.030, 0.012, -0.70),
            (Vector(( 0.34, -0.11, HZ - 0.13)), 0.020, 0.009, -0.78),
            (Vector(( 0.31, -0.09, HZ - 0.21)), 0.007, 0.004, -0.80),
        ],
        # Loose curly neck-framing strand behind right ear (-X, clearly visible from front 3/4!)
        [
            (Vector((-0.34,  0.05, HZ + 0.06)), 0.044, 0.018,  0.76),
            (Vector((-0.40,  0.09, HZ - 0.09)), 0.046, 0.018,  0.80),
            (Vector((-0.35,  0.11, HZ - 0.23)), 0.038, 0.015,  0.72),
            (Vector((-0.39,  0.08, HZ - 0.35)), 0.024, 0.011,  0.65),
            (Vector((-0.35,  0.06, HZ - 0.43)), 0.008, 0.005,  0.60),
        ],
        # Loose curly neck-framing strand behind left ear (+X, visible from front & side!)
        [
            (Vector(( 0.34,  0.05, HZ + 0.06)), 0.044, 0.018, -0.76),
            (Vector(( 0.40,  0.09, HZ - 0.09)), 0.046, 0.018, -0.80),
            (Vector(( 0.35,  0.11, HZ - 0.23)), 0.038, 0.015, -0.72),
            (Vector(( 0.39,  0.08, HZ - 0.35)), 0.024, 0.011, -0.65),
            (Vector(( 0.35,  0.06, HZ - 0.43)), 0.008, 0.005, -0.60),
        ],
    ]
    for idx, st in enumerate(loose_strands):
        head_parts.append(kit.create_ribbon_hair_strand(f"LooseWave_{idx}", st, m_hair, v_start=0.08, v_end=0.98))

    # 3. Low Tied Wavy Ponytail at the Nape (มัดผมหางม้าต่ำหยักศก!)
    pt_anchor = Vector((0.0, 0.395, HZ - 0.135))
    # Warm weathered tan leather hair-tie band so the tie pops crisply against dark hair!
    head_parts.append(kit.create_torus_ring(
        "LowPonytail_Tie", pt_anchor, 0.112, 0.028, m_vest, rot_euler=(0.82, 0.0, 0.0)
    ))
    head_parts.append(kit.create_torus_ring(
        "LowPonytail_Tie2", pt_anchor + Vector((0.0, 0.015, -0.030)), 0.106, 0.020, m_vest_trim, rot_euler=(0.82, 0.0, 0.0)
    ))

    # 7 Rich Layered S-Curve Wavy/Curly Low Ponytail Strands cascading down his back & peeking past neck!
    wavy_ponytail_strands = [
        # Center thick wavy main lock (bold S-wave!)
        [
            (pt_anchor + Vector(( 0.00,  0.00,  0.01)), 0.115, 0.056,  0.00),
            (pt_anchor + Vector(( 0.04,  0.12, -0.12)), 0.125, 0.058, -0.08),
            (pt_anchor + Vector((-0.05,  0.19, -0.28)), 0.112, 0.050,  0.12),
            (pt_anchor + Vector(( 0.05,  0.22, -0.44)), 0.074, 0.034, -0.12),
            (pt_anchor + Vector(( 0.01,  0.25, -0.56)), 0.012, 0.007,  0.00),
        ],
        # Left wavy curl flaring gently outward (visible peeking from front/3-4 angle!)
        [
            (pt_anchor + Vector(( 0.05,  0.00,  0.00)), 0.100, 0.048, -0.28),
            (pt_anchor + Vector(( 0.17,  0.08, -0.12)), 0.106, 0.048, -0.38),
            (pt_anchor + Vector(( 0.13,  0.14, -0.28)), 0.090, 0.040, -0.30),
            (pt_anchor + Vector(( 0.22,  0.16, -0.42)), 0.058, 0.026, -0.45),
            (pt_anchor + Vector(( 0.18,  0.18, -0.52)), 0.010, 0.006, -0.40),
        ],
        # Right wavy curl flaring gently outward (visible peeking from front/3-4 angle!)
        [
            (pt_anchor + Vector((-0.05,  0.00,  0.00)), 0.100, 0.048,  0.28),
            (pt_anchor + Vector((-0.17,  0.08, -0.12)), 0.106, 0.048,  0.38),
            (pt_anchor + Vector((-0.13,  0.14, -0.28)), 0.090, 0.040,  0.30),
            (pt_anchor + Vector((-0.22,  0.16, -0.42)), 0.058, 0.026,  0.45),
            (pt_anchor + Vector((-0.18,  0.18, -0.52)), 0.010, 0.006,  0.40),
        ],
        # Upper-back curly wave crest arching out from tie
        [
            (pt_anchor + Vector(( 0.00,  0.03,  0.03)), 0.095, 0.044,  0.00),
            (pt_anchor + Vector((-0.03,  0.17, -0.05)), 0.102, 0.046,  0.10),
            (pt_anchor + Vector(( 0.04,  0.25, -0.20)), 0.084, 0.036, -0.12),
            (pt_anchor + Vector((-0.02,  0.28, -0.36)), 0.048, 0.022,  0.10),
            (pt_anchor + Vector(( 0.02,  0.30, -0.46)), 0.010, 0.006,  0.00),
        ],
        # Outer-left curly tail flipping slightly forward toward shoulder (iconic pirate curl!)
        [
            (pt_anchor + Vector(( 0.07, -0.01, -0.02)), 0.076, 0.034, -0.52),
            (pt_anchor + Vector(( 0.22,  0.02, -0.14)), 0.078, 0.034, -0.60),
            (pt_anchor + Vector(( 0.26, -0.02, -0.26)), 0.058, 0.026, -0.66),
            (pt_anchor + Vector(( 0.20, -0.04, -0.35)), 0.012, 0.007, -0.62),
        ],
        # Outer-right curly tail flipping slightly forward toward shoulder!
        [
            (pt_anchor + Vector((-0.07, -0.01, -0.02)), 0.076, 0.034,  0.52),
            (pt_anchor + Vector((-0.22,  0.02, -0.14)), 0.078, 0.034,  0.60),
            (pt_anchor + Vector((-0.26, -0.02, -0.26)), 0.058, 0.026,  0.66),
            (pt_anchor + Vector((-0.20, -0.04, -0.35)), 0.012, 0.007,  0.62),
        ],
        # Deep under-tail wavy lock resting along spine
        [
            (pt_anchor + Vector(( 0.00, -0.01, -0.04)), 0.095, 0.044,  0.00),
            (pt_anchor + Vector(( 0.02,  0.08, -0.22)), 0.090, 0.040, -0.06),
            (pt_anchor + Vector((-0.03,  0.12, -0.40)), 0.064, 0.028,  0.08),
            (pt_anchor + Vector(( 0.00,  0.14, -0.52)), 0.010, 0.006,  0.00),
        ],
    ]
    for idx, ps in enumerate(wavy_ponytail_strands):
        head_parts.append(kit.create_ribbon_hair_strand(f"WavyPonytail_{idx}", ps, m_hair, v_start=0.06, v_end=0.98))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "NPC_Otto_Head")

    # ==========================================================================
    #  BODY — PIRATE LINEN SHIRT + TAUPE WAISTCOAT + DIAGONAL BALDRIC + BELT
    # ==========================================================================
    body_parts = []

    # Warm tanned neck + open V-neck exposed upper chest!
    neck_obj = kit.create_loft_object("Neck", [
        (Vector((0.0, 0.000, 1.20)), Vector((0.120, 0.0, 0.0)), Vector((0.0, 0.112, 0.0))),
        (Vector((0.0, 0.000, 1.47)), Vector((0.096, 0.0, 0.0)), Vector((0.0, 0.092, 0.0))),
    ], m_skin)

    # Exposed tanned V-chest skin wedge (visible through unbuttoned shirt/vest!)
    body_parts.append(kit.create_ellipsoid("Chest_SkinV", (0.0, -0.138, 1.215), (0.072, 0.062, 0.115), m_skin))

    # Black Cord Necklace + Silver Pendant hanging inside the open collar (Ref 1!)
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(
            f"NeckCord_{side}", (side * 0.042, -0.172, 1.245), (0.012, 0.014, 0.150),
            m_cord, rot_euler=(0.14, side * 0.34, 0.0), bevel_width=0.003
        ))
    body_parts.append(kit.create_ellipsoid("NeckPendant", (0.0, -0.204, 1.158), (0.022, 0.016, 0.028), m_silver))

    # Open Off-White Linen Shirt Collar Flaps framing the open neck!
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(
            f"ShirtCollar_{side}", (side * 0.118, -0.075, 1.318), (0.075, 0.150, 0.048),
            m_cream, rot_euler=(0.18, side * -0.35, side * -0.22), bevel_width=0.010
        ))
    body_parts.append(kit.create_loft_object("ShirtCollar_Back", [
        (Vector((0.0, 0.030, 1.28)), Vector((0.155, 0.0, 0.0)), Vector((0.0, 0.135, 0.0))),
        (Vector((0.0, 0.036, 1.35)), Vector((0.145, 0.0, 0.0)), Vector((0.0, 0.125, 0.0))),
    ], m_cream))

    # Weathered Taupe Brocade Waistcoat / Long Sleeveless Vest over Torso!
    vest_rings = [
        (Vector((0.0,  0.002, 0.76)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.180, 0.0))),
        (Vector((0.0,  0.000, 0.90)), Vector((0.224, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
        (Vector((0.0, -0.008, 1.06)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.202, 0.0))),
        (Vector((0.0, -0.006, 1.22)), Vector((0.256, 0.0, 0.0)), Vector((0.0, 0.200, 0.0))),
        (Vector((0.0,  0.002, 1.31)), Vector((0.208, 0.0, 0.0)), Vector((0.0, 0.168, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Taupe_Waistcoat", vest_rings, m_vest))

    # Subtle waistcoat V-lapel & front button placket + small weathered pewter buttons!
    body_parts.append(kit.create_rounded_box("Vest_Placket", (0.0, -0.204, 0.96), (0.034, 0.016, 0.34), m_vest_trim, bevel_width=0.005))
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(
            f"Vest_VLapel_{side}", (side * 0.072, -0.194, 1.205), (0.044, 0.022, 0.185),
            m_vest_trim, rot_euler=(0.06, side * 0.38, 0.0), bevel_width=0.007
        ))
        # Subtle brocade panel accent trims on waistcoat front
        body_parts.append(kit.create_rounded_box(
            f"Vest_BrocadeAccent_{side}", (side * 0.120, -0.196, 0.98), (0.062, 0.014, 0.21),
            m_vest_trim, rot_euler=(0.0, side * -0.06, 0.0), bevel_width=0.005
        ))
    for bz in (1.07, 0.99, 0.91, 0.83):
        body_parts.append(kit.create_ellipsoid(f"VestBtn_{bz:.2f}", (0.0, -0.214, bz), (0.015, 0.010, 0.015), m_silver))

    # Flared Split Waistcoat Coat-Tails draping below the waist belt (exact Reference 1 silhouette!)
    # Left & right front flared panels (leaving front center slit open to reveal dark navy trousers!)
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(
            f"CoatTail_Front_{side}", (side * 0.175, -0.115, 0.59), (0.165, 0.185, 0.30),
            m_vest, rot_euler=(-0.10, side * -0.14, side * 0.22), bevel_width=0.018
        ))
        # Side pocket flap on coat tail
        body_parts.append(kit.create_rounded_box(
            f"CoatPocket_{side}", (side * 0.235, -0.110, 0.68), (0.056, 0.130, 0.048),
            m_vest_trim, rot_euler=(0.0, side * -0.14, side * 0.18), bevel_width=0.008
        ))
    # Back flared waistcoat tail
    body_parts.append(kit.create_loft_object("CoatTail_Back", [
        (Vector((0.0, 0.065, 0.44)), Vector((0.270, 0.0, 0.0)), Vector((0.0, 0.185, 0.0))),
        (Vector((0.0, 0.045, 0.60)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
        (Vector((0.0, 0.025, 0.76)), Vector((0.238, 0.0, 0.0)), Vector((0.0, 0.175, 0.0))),
    ], m_vest))

    # Iconic Diagonal Black/Dark-Brown Leather Baldric / Bandolier Strap (Right Shoulder -X -> Left Hip +X!)
    baldric_front_pts = [
        (Vector((-0.145, -0.175, 1.30)), 0.052, 0.018,  0.0),
        (Vector((-0.055, -0.214, 1.14)), 0.056, 0.020,  0.0),
        (Vector(( 0.065, -0.216, 0.96)), 0.056, 0.020,  0.0),
        (Vector(( 0.215, -0.165, 0.79)), 0.052, 0.018,  0.0),
    ]
    baldric_back_pts = [
        (Vector((-0.145,  0.175, 1.30)), 0.052, 0.018,  0.0),
        (Vector((-0.040,  0.212, 1.13)), 0.055, 0.020,  0.0),
        (Vector(( 0.085,  0.210, 0.95)), 0.055, 0.020,  0.0),
        (Vector(( 0.215,  0.155, 0.79)), 0.052, 0.018,  0.0),
    ]
    # Shoulder bridge connecting front & back baldric strap over right shoulder (-X)
    body_parts.append(kit.create_rounded_box("Baldric_ShoulderCap", (-0.152, 0.0, 1.315), (0.092, 0.365, 0.036), m_leather, rot_euler=(0.0, 0.14, 0.0), bevel_width=0.010))
    body_parts.append(kit.create_ribbon_hair_strand("Baldric_Front", baldric_front_pts, m_leather, segments=14, substeps=4, radial_center=Vector((0.0, 0.0, 1.05))))
    body_parts.append(kit.create_ribbon_hair_strand("Baldric_Back", baldric_back_pts, m_leather, segments=14, substeps=4, radial_center=Vector((0.0, 0.0, 1.05))))

    # Large Ornate Pewter/Silver Baldric Chest Buckle (sits cleanly ON TOP of baldric strap with zero clipping!)
    body_parts.append(kit.create_rounded_box(
        "Baldric_BuckleFrame", (-0.052, -0.244, 1.115), (0.118, 0.026, 0.098),
        m_silver, rot_euler=(-0.08, 0.10, -0.58), bevel_width=0.010
    ))
    body_parts.append(kit.create_rounded_box(
        "Baldric_BuckleCenter", (-0.052, -0.250, 1.115), (0.062, 0.020, 0.050),
        m_leather, rot_euler=(-0.08, 0.10, -0.58), bevel_width=0.006
    ))

    # Wide Dark Leather Waist Belt + Big Rectangular Silver Buckle + Clean Hanging Belt Tongue!
    belt_rings = [
        (Vector((0.0, 0.002, 0.715)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.208, 0.0))),
        (Vector((0.0, 0.002, 0.815)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.204, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Waist_LeatherBelt", belt_rings, m_leather))
    # Big hammered rectangular silver belt buckle at center front
    body_parts.append(kit.create_rounded_box("Waist_BuckleOuter", (0.0, -0.218, 0.765), (0.132, 0.028, 0.118), m_silver, bevel_width=0.010))
    body_parts.append(kit.create_rounded_box("Waist_BuckleInner", (0.0, -0.224, 0.765), (0.072, 0.022, 0.064), m_leather, bevel_width=0.006))
    body_parts.append(kit.create_rounded_box("Waist_BucklePin",   (0.0, -0.230, 0.765), (0.014, 0.014, 0.078), m_silver, bevel_width=0.003))
    # Hanging dark leather belt strap tail neatly OUTSIDE coat-tail (zero mesh intersection!)
    body_parts.append(kit.create_rounded_box("Waist_BeltTail", (0.115, -0.228, 0.635), (0.056, 0.022, 0.175), m_leather, rot_euler=(-0.14, 0.0, 0.12), bevel_width=0.007))

    # Billowy Cream Linen Pirate Sleeves + Ruffled Flared Cuffs + Bare Tanned Chibi Hands!
    AZ = 1.23
    for side in (1.0, -1.0):
        # Puffy gathered shoulder cap of white linen shirt emerging from sleeveless waistcoat
        body_parts.append(kit.create_ellipsoid(f"LinenShoulder_{side}", (side * 0.238, 0.0, AZ), (0.138, 0.134, 0.136), m_cream))
        # Billowy linen sleeve puffing nicely at elbow/forearm and cinching at wrist!
        sleeve_rings = [
            (Vector((side * 0.22, 0.0, AZ)), Vector((0.0, 0.128, 0.0)), Vector((0.0, 0.0, 0.128 * side))),
            (Vector((side * 0.36, 0.0, AZ)), Vector((0.0, 0.136, 0.0)), Vector((0.0, 0.0, 0.136 * side))),
            (Vector((side * 0.50, 0.0, AZ)), Vector((0.0, 0.144, 0.0)), Vector((0.0, 0.0, 0.144 * side))),
            (Vector((side * 0.61, 0.0, AZ)), Vector((0.0, 0.114, 0.0)), Vector((0.0, 0.0, 0.114 * side))),
            (Vector((side * 0.65, 0.0, AZ)), Vector((0.0, 0.092, 0.0)), Vector((0.0, 0.0, 0.092 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"LinenSleeve_{side}", sleeve_rings, m_cream))

        # Gathered wristband tie + ruffled pirate sleeve cuff!
        body_parts.append(kit.create_torus_ring(f"CuffBand_{side}", (side * 0.645, 0.0, AZ), 0.094, 0.016, m_cream_shd, rot_euler=(0.0, math.pi * 0.5, 0.0)))
        ruffle_rings = [
            (Vector((side * 0.650, 0.0, AZ)), Vector((0.0, 0.092, 0.0)), Vector((0.0, 0.0, 0.092 * side))),
            (Vector((side * 0.715, 0.0, AZ)), Vector((0.0, 0.118, 0.0)), Vector((0.0, 0.0, 0.118 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"LinenRuffle_{side}", ruffle_rings, m_cream_shd))

        # Warm tanned wrist & bare Chibi hand (no gloves, matches Ref 1!)
        body_parts.append(kit.create_loft_object(f"Wrist_{side}", [
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.072, 0.0)), Vector((0.0, 0.0, 0.072 * side))),
            (Vector((side * 0.74, 0.0, AZ)), Vector((0.0, 0.070, 0.0)), Vector((0.0, 0.0, 0.070 * side))),
        ], m_skin))
        body_parts.append(kit.create_rounded_box(f"HandPalm_{side}", (side * 0.80, -0.004, AZ), (0.128, 0.148, 0.120), m_skin, bevel_width=0.028))
        body_parts.append(kit.create_ellipsoid(f"HandFingers_{side}", (side * 0.88, -0.004, AZ - 0.004), (0.052, 0.062, 0.048), m_skin))

    # Dark Navy-Charcoal Trousers + Rugged Cuffed Dark-Brown Leather Pirate Boots!
    body_parts.append(kit.create_loft_object("Pants_Pelvis", [
        (Vector((0.0, 0.002, 0.56)), Vector((0.242, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, 0.002, 0.75)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
    ], m_pants))

    for side in (1.0, -1.0):
        # Dark navy trousers from hip down into boot tops
        body_parts.append(kit.create_loft_object(f"TrouserLeg_{side}", [
            (Vector((side * 0.148, 0.002, 0.68)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.150, 0.000, 0.44)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.116, 0.0))),
            (Vector((side * 0.152, 0.000, 0.30)), Vector((0.102, 0.0, 0.0)), Vector((0.0, -0.104, 0.0))),
        ], m_pants))

        # Folded pirate leather boot cuff!
        body_parts.append(kit.create_loft_object(f"BootCuff_{side}", [
            (Vector((side * 0.152, 0.000, 0.345)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.152, 0.000, 0.255)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
        ], m_boot))

        # Main boot shaft & vamp
        body_parts.append(kit.create_loft_object(f"PirateBoot_{side}", [
            (Vector((side * 0.152,  0.000, 0.295)), Vector((0.106, 0.0, 0.0)), Vector((0.0, -0.108, 0.0))),
            (Vector((side * 0.152,  0.000, 0.185)), Vector((0.102, 0.0, 0.0)), Vector((0.0, -0.106, 0.0))),
            (Vector((side * 0.152, -0.018, 0.118)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.136, 0.0))),
            (Vector((side * 0.152, -0.056, 0.072)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.180, 0.0))),
            (Vector((side * 0.152, -0.062, 0.036)), Vector((0.130, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ], m_boot))
        # Dark leather ankle strap & thick rugged sole
        body_parts.append(kit.create_torus_ring(f"BootStrap_{side}", (side * 0.152, -0.005, 0.155), 0.106, 0.014, m_leather))
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", [
            (Vector((side * 0.152, -0.062, 0.042)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.194, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.190, 0.0))),
        ], m_leather))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "NPC_Otto_Body")
    export_npc_with_kit(scene, head_obj, body_obj, "NPC_Otto_Gunsmith")


if __name__ == "__main__":
    print("=== BUILDING CUSTOM CHIBI NPC: NPC_Otto_Gunsmith (Will Turner Style) ===")
    build_otto_pirate_artisan()
    print("=== NPC_Otto_Gunsmith EXPORTED & RENDERED SUCCESSFULLY! ===")

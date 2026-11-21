import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector, Euler
import ChibiAnimeKit as kit

import shutil
import time

NPC_OUT_DIR = os.path.join(kit.OUT_DIR, "NPCs")
TMP_OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "_tmp_npc_export")
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
    """Exports to a staging folder outside Assets first, then copies cleanly into Assets/GeneratedModels/NPCs/ (avoids Unity file-lock collisions)."""
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
#  1. NPC_Elena_Medic — Yellow "69" Street-Tech Headphones Girl (Reference 1)
# ==============================================================================

def generate_elena_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.985, 0.945, 0.930)
    SKIN_WARM   = (0.945, 0.875, 0.855)
    SKIN_SHADOW = (0.875, 0.785, 0.775)
    BLUSH_COL   = (0.980, 0.560, 0.620)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.88, V) * 0.16, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.12, SKIN_WARM)

    # Soft cheek blush
    for side in (-1.0, 1.0):
        d_b = np.hypot((U - (0.5 + side * 0.175)) / 0.064, (V - 0.378) / 0.026)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.18, 1.0, d_b)) * 0.22, BLUSH_COL)

    # Iconic tiny red heart tattoo under viewer's right / character's left eye!
    hx, hy = 0.655, 0.365
    rx1 = np.hypot((U - (hx - 0.011)) / 0.013, (V - (hy + 0.008)) / 0.013)
    rx2 = np.hypot((U - (hx + 0.011)) / 0.013, (V - (hy + 0.008)) / 0.013)
    tri_d = np.maximum(np.abs(U - hx) * 1.05 + (hy + 0.008 - V) * 0.78 - 0.022, (V - (hy + 0.008)))
    heart_mask = np.maximum(
        1.0 - kit.smoothstep(0.85, 1.05, np.minimum(rx1, rx2)),
        (1.0 - kit.smoothstep(-0.002, 0.002, tri_d)) * (V <= hy + 0.010)
    )
    kit.alpha_blend(canvas, heart_mask, (0.94, 0.12, 0.18))

    # Cute subtle nose dot
    d_n = np.hypot((U - 0.500) / 0.010, (V - 0.392) / 0.008)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.35, SKIN_SHADOW)

    # Vivid Magenta-Purple Anime Eyes with signature X-cross sparkle pattern!
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.150
        cy = 0.468
        ex = (U - cx) * side
        ey = V - cy

        r_eye = np.hypot(ex / 0.082, ey / 0.072)
        eye_mask = 1.0 - kit.smoothstep(1.0 - aa / 0.082, 1.0 + aa / 0.082, r_eye)
        kit.alpha_blend(canvas, eye_mask, (0.998, 0.998, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.045, 0.072, ey) * 0.22, (0.76, 0.64, 0.86))

        rx_i, ry_i = 0.054, 0.060
        r_iris = np.hypot(ex / rx_i, ey / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey + ry_i) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.90 - 0.38 * np.power(v_grad, 1.1), 0.0, 1.0)
        iris_g = np.clip(0.24 - 0.16 * np.power(v_grad, 1.2), 0.0, 1.0)
        iris_b = np.clip(0.95 - 0.35 * np.power(v_grad, 1.1), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Signature white X-cross sparkle inside purple iris (from artwork!)
        d_x1 = np.abs(ex - ey) / 1.414
        d_x2 = np.abs(ex + ey) / 1.414
        x_mask = iris_mask * (1.0 - kit.smoothstep(0.007, 0.011, np.minimum(d_x1, d_x2)))
        kit.alpha_blend(canvas, x_mask * 0.92, (1.0, 0.96, 1.0))

        # Outer dark rim & tiny center pupil
        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.80, 0.96, r_iris) * 0.92, (0.14, 0.02, 0.20))
        r_pupil = np.hypot(ex / 0.014, ey / 0.014)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.8, 1.05, r_pupil)), (0.10, 0.02, 0.14))

        # Crisp upper & lower lashes
        u_lash_pts = [
            (cx + side * (-0.080), cy + 0.012),
            (cx + side * (-0.036), cy + 0.062),
            (cx + side * ( 0.016), cy + 0.072),
            (cx + side * ( 0.070), cy + 0.056),
            (cx + side * ( 0.098), cy + 0.066),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.005, 0.012, 0.015, 0.012, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.04, 0.03, 0.05))

        l_lash_pts = [
            (cx + side * (-0.058), cy - 0.050),
            (cx + side * ( 0.002), cy - 0.068),
            (cx + side * ( 0.062), cy - 0.046),
        ]
        d_ll = kit.polyline_sdf(U, V, l_lash_pts, [0.0025, 0.0040, 0.0025])
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, d_ll)) * 0.90, (0.05, 0.04, 0.06))

        # Clean dark eyebrows
        brow_pts = [
            (cx + side * (-0.068), cy + 0.104),
            (cx + side * ( 0.006), cy + 0.114),
            (cx + side * ( 0.074), cy + 0.106),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.0045, 0.0070, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.06, 0.06, 0.08))

    # Cute tiny surprised/pout O-mouth from artwork
    r_m = np.hypot((U - 0.500) / 0.016, (V - 0.300) / 0.016)
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(0.85, 1.08, r_m), (0.85, 0.36, 0.44))
    r_mi = np.hypot((U - 0.500) / 0.009, (V - 0.300) / 0.009)
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(0.80, 1.08, r_mi), (0.45, 0.12, 0.18))

    path = os.path.join(kit.TEX_DIR, "NPC_Elena_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "NPC_Elena_Face.png")


def build_elena_yellow69():
    scene = kit.reset_scene()
    img_face = generate_elena_face_texture()
    img_hair = kit.generate_hair_gradient_texture(
        os.path.join(kit.TEX_DIR, "NPC_Elena_Hair.png"), "NPC_Elena_Hair.png",
        root_rgb=(0.04, 0.04, 0.06),
        mid_rgb=(0.09, 0.09, 0.12),
        tip_rgb=(0.05, 0.05, 0.07),
        hi_rgb=(0.82, 0.84, 0.90),
        band_v=0.42, band_strength=0.55
    )

    m_face    = kit.make_mat("Elena_Face_Tex", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin    = kit.make_mat("Elena_Skin",     (0.985, 0.945, 0.930, 1.0), roughness=0.90, specular=0.03)
    m_hair    = kit.make_mat("Elena_Hair",     (1.0, 1.0, 1.0, 1.0), roughness=0.78, specular=0.08, image=img_hair)
    m_yellow  = kit.make_mat("Elena_Yellow",   (0.99, 0.79, 0.06, 1.0), roughness=0.84, specular=0.05)
    m_gold    = kit.make_mat("Elena_Gold",     (0.96, 0.68, 0.04, 1.0), roughness=0.52, metallic=0.25, specular=0.18)
    m_black   = kit.make_mat("Elena_Black",    (0.08, 0.08, 0.10, 1.0), roughness=0.88, specular=0.05)
    m_white   = kit.make_mat("Elena_White",    (0.96, 0.96, 0.97, 1.0), roughness=0.90, specular=0.03)
    m_red     = kit.make_mat("Elena_Red",      (0.84, 0.14, 0.12, 1.0), roughness=0.84, specular=0.06)
    m_cyan    = kit.make_mat("Elena_VialCyan", (0.18, 0.76, 0.98, 1.0), roughness=0.25, specular=0.25)
    m_green   = kit.make_mat("Elena_VialGrn",  (0.18, 0.88, 0.42, 1.0), roughness=0.25, specular=0.25)
    m_pink    = kit.make_mat("Elena_VialPnk",  (0.92, 0.24, 0.78, 1.0), roughness=0.25, specular=0.25)
    m_silver  = kit.make_mat("Elena_Silver",   (0.82, 0.85, 0.90, 1.0), roughness=0.35, metallic=0.65, specular=0.25)
    m_bear    = kit.make_mat("Elena_Bear",     (0.60, 0.36, 0.18, 1.0), roughness=0.88, specular=0.04)

    # --- HEAD ---
    head_parts = []
    HZ = 1.69
    head_parts.append(kit.create_anime_head_shell(HZ, m_face, m_skin, style="cute_round"))
    head_parts.append(kit.create_hair_crown_shell(HZ, m_hair, rx_base=0.415, ry_base=0.395, rz_base=0.395, cutout_z=0.44))

    # Straight Blunt Bob Bangs across forehead (curved cleanly from crown)
    for idx, bx in enumerate((-0.23, -0.14, -0.05, 0.04, 0.13, 0.22)):
        strand = [
            (Vector((bx * 0.62, -0.12, HZ + 0.39)), 0.052, 0.022, bx * -0.35),
            (Vector((bx * 0.92, -0.34, HZ + 0.26)), 0.066, 0.024, bx * -0.35),
            (Vector((bx * 1.00, -0.39, HZ + 0.11)), 0.060, 0.020, bx * -0.35),
            (Vector((bx * 1.00, -0.38, HZ + 0.05)), 0.046, 0.012, bx * -0.35),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"BobBang_{idx}", strand, m_hair, v_start=0.10, v_end=0.92))

    # Smooth Rounded-Dome Chin-Length Bob Side Curtains & Back Wall (no square top corners!)
    for side in (-1.0, 1.0):
        for j, y_off in enumerate((-0.18, -0.04, 0.11, 0.25)):
            x_rad = 0.402 - abs(y_off) * 0.18
            twist = side * (-0.98 + j * 0.24)
            curtain = [
                (Vector((side * (x_rad * 0.45), y_off * 0.55, HZ + 0.37)), 0.054, 0.022, twist),
                (Vector((side * (x_rad + 0.00), y_off * 0.95, HZ + 0.20)), 0.092, 0.032, twist),
                (Vector((side * (x_rad + 0.02), y_off,        HZ - 0.08)), 0.098, 0.032, twist),
                (Vector((side * (x_rad + 0.03), y_off,        HZ - 0.35)), 0.086, 0.020, twist),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(f"BobSide_{side}_{j}", curtain, m_hair, v_start=0.10, v_end=0.92))

    for idx, x_off in enumerate((-0.26, -0.13, 0.0, 0.13, 0.26)):
        back_s = [
            (Vector((x_off * 0.48, 0.16, HZ + 0.36)), 0.058, 0.024, x_off * 1.1),
            (Vector((x_off * 0.88, 0.37, HZ + 0.16)), 0.096, 0.034, x_off * 1.1),
            (Vector((x_off * 0.98, 0.40, HZ - 0.12)), 0.100, 0.034, x_off * 1.1),
            (Vector((x_off * 1.00, 0.39, HZ - 0.36)), 0.086, 0.020, x_off * 1.1),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"BobBack_{idx}", back_s, m_hair, v_start=0.10, v_end=0.92))

    # Iconic Bright Yellow Over-Ear Headphones (prominently visible on sides of bob!)
    hb_rings = []
    for k in range(17):
        ang = math.radians(-88.0 + k * (176.0 / 16.0))
        cx = math.sin(ang) * 0.478
        cz = HZ + 0.02 + math.cos(ang) * 0.452
        n_dir = Vector((math.sin(ang), 0.0, math.cos(ang))).normalized()
        hb_rings.append((Vector((cx, -0.02, cz)), Vector((0.0, 0.056, 0.0)), n_dir * 0.028))
    head_parts.append(kit.create_loft_object("Headphone_Band", hb_rings, m_gold, segments=16))

    for side in (-1.0, 1.0):
        # Cushion pad + bright yellow outer cup + black inner logo badge
        head_parts.append(kit.create_ellipsoid(f"HP_Pad_{side}", (side * 0.448, -0.02, HZ - 0.02), (0.052, 0.122, 0.142), m_black))
        head_parts.append(kit.create_ellipsoid(f"HP_Cup_{side}", (side * 0.498, -0.02, HZ - 0.02), (0.064, 0.114, 0.132), m_yellow))
        head_parts.append(kit.create_ellipsoid(f"HP_Logo_{side}", (side * 0.550, -0.02, HZ - 0.02), (0.018, 0.050, 0.056), m_black))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "NPC_Elena_Head")

    # --- BODY ---
    body_parts = []
    neck_obj = kit.create_loft_object("Neck", [
        (Vector((0.0, 0.0, 1.24)), Vector((0.102, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
        (Vector((0.0, 0.0, 1.46)), Vector((0.092, 0.0, 0.0)), Vector((0.0, 0.090, 0.0))),
    ], m_skin)

    # Pale exposed midriff waist
    body_parts.append(kit.create_loft_object("Midriff_Waist", [
        (Vector((0.0, 0.002, 0.80)), Vector((0.195, 0.0, 0.0)), Vector((0.0, 0.152, 0.0))),
        (Vector((0.0, 0.000, 0.94)), Vector((0.186, 0.0, 0.0)), Vector((0.0, 0.146, 0.0))),
    ], m_skin))

    # Cropped Bright Yellow Puffer Street Jacket
    jacket_rings = [
        (Vector((0.0, 0.000, 0.91)), Vector((0.238, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
        (Vector((0.0, -0.006, 1.06)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.205, 0.0))),
        (Vector((0.0, -0.004, 1.21)), Vector((0.262, 0.0, 0.0)), Vector((0.0, 0.202, 0.0))),
        (Vector((0.0, 0.000, 1.30)), Vector((0.210, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Yellow_Jacket", jacket_rings, m_yellow))
    # Center black zipper seam + "69" chest accent block
    body_parts.append(kit.create_rounded_box("Jacket_Zip", (0.0, -0.206, 1.11), (0.018, 0.016, 0.34), m_black, bevel_width=0.004))
    body_parts.append(kit.create_rounded_box("Chest_69_Badge", (-0.105, -0.204, 1.17), (0.088, 0.016, 0.056), m_black, bevel_width=0.005))
    body_parts.append(kit.create_rounded_box("Chest_69_Dot", (-0.105, -0.212, 1.17), (0.052, 0.010, 0.022), m_yellow, bevel_width=0.003))

    # Fluffy White Fur Collar
    for k in range(10):
        ang = (2.0 * math.pi * k) / 10.0
        fx = math.cos(ang) * 0.165
        fy = math.sin(ang) * 0.145
        body_parts.append(kit.create_ellipsoid(f"FurCollar_{k}", (fx, fy, 1.31), (0.072, 0.068, 0.058), m_white))

    # Oversized Yellow Street Sleeves with Black Flame Cuffs + Bear Patch + Black Fingerless Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        body_parts.append(kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.225, 0.0, AZ), (0.134, 0.128, 0.128), m_yellow))
        # Puffy oversized sleeve flaring toward wrist!
        sleeve_rings = [
            (Vector((side * 0.20, 0.0, AZ)), Vector((0.0, 0.124, 0.0)), Vector((0.0, 0.0, 0.124 * side))),
            (Vector((side * 0.38, 0.0, AZ)), Vector((0.0, 0.132, 0.0)), Vector((0.0, 0.0, 0.132 * side))),
            (Vector((side * 0.56, 0.0, AZ)), Vector((0.0, 0.148, 0.0)), Vector((0.0, 0.0, 0.148 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"BigSleeve_{side}", sleeve_rings, m_yellow))
        # Black flame cuff band + yellow edge trim
        cuff_blk = [
            (Vector((side * 0.49, 0.0, AZ)), Vector((0.0, 0.152, 0.0)), Vector((0.0, 0.0, 0.152 * side))),
            (Vector((side * 0.61, 0.0, AZ)), Vector((0.0, 0.156, 0.0)), Vector((0.0, 0.0, 0.156 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"SleeveCuffBlk_{side}", cuff_blk, m_black))
        cuff_yel = [
            (Vector((side * 0.60, 0.0, AZ)), Vector((0.0, 0.152, 0.0)), Vector((0.0, 0.0, 0.152 * side))),
            (Vector((side * 0.65, 0.0, AZ)), Vector((0.0, 0.140, 0.0)), Vector((0.0, 0.0, 0.140 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"SleeveCuffYel_{side}", cuff_yel, m_yellow))
        # Cute Bear patch on left sleeve (+X)
        if side > 0:
            body_parts.append(kit.create_ellipsoid("BearPatch_Head", (0.40, -0.132, AZ), (0.042, 0.018, 0.038), m_bear))
            body_parts.append(kit.create_ellipsoid("BearPatch_EarL", (0.37, -0.132, AZ + 0.032), (0.016, 0.014, 0.016), m_bear))
            body_parts.append(kit.create_ellipsoid("BearPatch_EarR", (0.43, -0.132, AZ + 0.032), (0.016, 0.014, 0.016), m_bear))

        # Wrist & Black Fingerless Gloves
        body_parts.append(kit.create_loft_object(f"Wrist_{side}", [
            (Vector((side * 0.62, 0.0, AZ)), Vector((0.0, 0.072, 0.0)), Vector((0.0, 0.0, 0.072 * side))),
            (Vector((side * 0.72, 0.0, AZ)), Vector((0.0, 0.070, 0.0)), Vector((0.0, 0.0, 0.070 * side))),
        ], m_skin))
        body_parts.append(kit.create_rounded_box(f"Glove_{side}", (side * 0.78, -0.004, AZ), (0.132, 0.158, 0.126), m_black, bevel_width=0.022))
        body_parts.append(kit.create_ellipsoid(f"Fingers_{side}", (side * 0.87, -0.004, AZ - 0.005), (0.058, 0.068, 0.052), m_skin))

    # Yellow Shorts + Red Tilted Utility Belt with 3 Colorful Vials (Cyan, Green, Pink) & Red Side Pouch!
    shorts_pelvis = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.205, 0.0))),
        (Vector((0.0, 0.004, 0.72)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.198, 0.0))),
        (Vector((0.0, 0.002, 0.82)), Vector((0.212, 0.0, 0.0)), Vector((0.0, 0.168, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Yellow_ShortsPelvis", shorts_pelvis, m_yellow))

    # Red Utility Belt
    belt_rings = [
        (Vector((0.0, 0.002, 0.72)), Vector((0.268, 0.0, 0.0)), Vector((0.0, 0.214, 0.0))),
        (Vector((0.0, 0.002, 0.79)), Vector((0.264, 0.0, 0.0)), Vector((0.0, 0.210, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Red_Belt", belt_rings, m_red))
    # Red hip pouch on right side (-X)
    body_parts.append(kit.create_rounded_box("Red_HipPouch", (-0.285, 0.02, 0.72), (0.088, 0.165, 0.155), m_red, bevel_width=0.018))
    # 3 Colorful Potion/Vial Canisters across front of belt (Cyan, Green, Pink!)
    vial_specs = [
        (-0.11, -0.225, 0.73, m_cyan),
        ( 0.06, -0.228, 0.75, m_green),
        ( 0.19, -0.195, 0.77, m_pink),
    ]
    for idx, (vx, vy, vz, vmat) in enumerate(vial_specs):
        body_parts.append(kit.create_loft_object(f"VialBody_{idx}", [
            (Vector((vx, vy, vz - 0.055)), Vector((0.036, 0.0, 0.0)), Vector((0.0, 0.036, 0.0))),
            (Vector((vx, vy, vz + 0.025)), Vector((0.036, 0.0, 0.0)), Vector((0.0, 0.036, 0.0))),
        ], m_silver, segments=16))
        body_parts.append(kit.create_ellipsoid(f"VialCap_{idx}", (vx, vy, vz + 0.042), (0.036, 0.036, 0.032), vmat))

    # Legs: Yellow short cuffs -> Black undershorts / thigh straps -> Pale legs -> Black socks -> White N.69 Sneakers!
    for side in (1.0, -1.0):
        body_parts.append(kit.create_loft_object(f"ShortLeg_{side}", [
            (Vector((side * 0.148, 0.002, 0.68)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.150, 0.002, 0.52)), Vector((0.138, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
        ], m_yellow))
        # Black spats on right leg (-X), twin black thigh straps + red buckle on left leg (+X)
        if side < 0:
            body_parts.append(kit.create_loft_object("Right_Spats", [
                (Vector((side * 0.150, 0.000, 0.54)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.114, 0.0))),
                (Vector((side * 0.150, 0.000, 0.43)), Vector((0.106, 0.0, 0.0)), Vector((0.0, -0.106, 0.0))),
            ], m_black))
        else:
            for s_idx, sz in enumerate((0.49, 0.44)):
                body_parts.append(kit.create_torus_ring(f"ThighStrap_{s_idx}", (side * 0.150, 0.0, sz), 0.104, 0.014, m_black))
            body_parts.append(kit.create_rounded_box("ThighBuckle", (side * 0.245, -0.02, 0.465), (0.030, 0.048, 0.048), m_red, bevel_width=0.006))

        # Bare pale legs
        body_parts.append(kit.create_loft_object(f"LegSkin_{side}", [
            (Vector((side * 0.150, 0.000, 0.52)), Vector((0.102, 0.0, 0.0)), Vector((0.0, -0.104, 0.0))),
            (Vector((side * 0.152, 0.000, 0.28)), Vector((0.092, 0.0, 0.0)), Vector((0.0, -0.094, 0.0))),
        ], m_skin))
        # Black socks
        body_parts.append(kit.create_loft_object(f"Sock_{side}", [
            (Vector((side * 0.152, 0.000, 0.29)), Vector((0.096, 0.0, 0.0)), Vector((0.0, -0.098, 0.0))),
            (Vector((side * 0.152, 0.000, 0.16)), Vector((0.094, 0.0, 0.0)), Vector((0.0, -0.096, 0.0))),
        ], m_black))
        # White N.69 Sneakers + red heel accent + dark sole
        body_parts.append(kit.create_loft_object(f"Sneaker_{side}", [
            (Vector((side * 0.152,  0.000, 0.180)), Vector((0.104, 0.0, 0.0)), Vector((0.0, -0.112, 0.0))),
            (Vector((side * 0.152, -0.018, 0.120)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.134, 0.0))),
            (Vector((side * 0.152, -0.056, 0.072)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.180, 0.0))),
            (Vector((side * 0.152, -0.064, 0.036)), Vector((0.130, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ], m_white))
        body_parts.append(kit.create_rounded_box(f"SneakerHeel_{side}", (side * 0.152, 0.112, 0.085), (0.088, 0.036, 0.062), m_red, bevel_width=0.008))
        body_parts.append(kit.create_loft_object(f"Sole_{side}", [
            (Vector((side * 0.152, -0.064, 0.042)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.194, 0.0))),
            (Vector((side * 0.152, -0.064, 0.003)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.190, 0.0))),
        ], m_black))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "NPC_Elena_Body")
    export_npc_with_kit(scene, head_obj, body_obj, "NPC_Elena_Medic")


# ==============================================================================
#  2. NPC_Maya_Scout — P4NDORA Gothic Black Bob Dress Girl (Reference 2)
# ==============================================================================

def generate_maya_pandora_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.992, 0.942, 0.935)
    SKIN_WARM   = (0.955, 0.875, 0.870)
    SKIN_SHADOW = (0.885, 0.790, 0.795)
    BLUSH_COL   = (0.960, 0.620, 0.660)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.88, V) * 0.14, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.12, SKIN_WARM)

    # Subtle cool rose blush
    for side in (-1.0, 1.0):
        d_b = np.hypot((U - (0.5 + side * 0.172)) / 0.062, (V - 0.382) / 0.025)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.18, 1.0, d_b)) * 0.16, BLUSH_COL)

    # Delicate beauty mark below right eye (from P4NDORA artwork!)
    d_mole = np.hypot((U - 0.618) / 0.006, (V - 0.408) / 0.006)
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(0.75, 1.1, d_mole), (0.12, 0.08, 0.09))

    # Sleek nose highlight/shadow
    d_n = np.hypot((U - 0.500) / 0.011, (V - 0.394) / 0.010)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.32, SKIN_SHADOW)

    # Cool Sleek Jet-Obsidian Anime Eyes
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.150
        cy = 0.468
        ex = (U - cx) * side
        ey = V - cy

        r_eye = np.hypot(ex / 0.080, ey / 0.060)
        eye_mask = 1.0 - kit.smoothstep(1.0 - aa / 0.080, 1.0 + aa / 0.080, r_eye)
        kit.alpha_blend(canvas, eye_mask, (0.996, 0.996, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.036, 0.060, ey) * 0.25, (0.72, 0.72, 0.78))

        rx_i, ry_i = 0.048, 0.052
        r_iris = np.hypot(ex / rx_i, ey / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey + ry_i) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.26 - 0.20 * v_grad, 0.0, 1.0)
        iris_g = np.clip(0.18 - 0.14 * v_grad, 0.0, 1.0)
        iris_b = np.clip(0.24 - 0.16 * v_grad, 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Crisp white catchlights
        r_hi1 = np.hypot((ex - 0.018) / 0.013, (ey - 0.018) / 0.012)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi1)), (1.0, 1.0, 1.0))
        r_hi2 = np.hypot((ex + 0.016) / 0.007, (ey + 0.016) / 0.007)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi2)), (1.0, 0.96, 0.98))

        # Winged chic black eyeliner!
        u_lash_pts = [
            (cx + side * (-0.078), cy + 0.006),
            (cx + side * (-0.032), cy + 0.052),
            (cx + side * ( 0.022), cy + 0.058),
            (cx + side * ( 0.072), cy + 0.044),
            (cx + side * ( 0.105), cy + 0.062),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.005, 0.011, 0.014, 0.012, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.03, 0.03, 0.04))

        brow_pts = [
            (cx + side * (-0.064), cy + 0.095),
            (cx + side * ( 0.008), cy + 0.106),
            (cx + side * ( 0.074), cy + 0.098),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.004, 0.006, 0.0025])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.04, 0.04, 0.05))

    # Signature Bold Crimson Red Lips (P4NDORA!)
    lip_upper = np.hypot((U - 0.500) / 0.044, (V - 0.304) / 0.015)
    lip_lower = np.hypot((U - 0.500) / 0.038, (V - 0.290) / 0.016)
    lip_mask = np.maximum(1.0 - kit.smoothstep(0.82, 1.05, lip_upper), 1.0 - kit.smoothstep(0.82, 1.05, lip_lower))
    kit.alpha_blend(canvas, lip_mask, (0.76, 0.06, 0.10))
    # Glossy red lip highlight
    lip_hi = np.hypot((U - 0.504) / 0.016, (V - 0.289) / 0.005)
    kit.alpha_blend(canvas, lip_mask * (1.0 - kit.smoothstep(0.5, 1.0, lip_hi)) * 0.55, (1.0, 0.58, 0.60))

    path = os.path.join(kit.TEX_DIR, "NPC_Maya_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "NPC_Maya_Face.png")


def build_maya_pandora():
    scene = kit.reset_scene()
    img_face = generate_maya_pandora_face_texture()
    img_hair = kit.generate_hair_gradient_texture(
        os.path.join(kit.TEX_DIR, "NPC_Maya_Hair.png"), "NPC_Maya_Hair.png",
        root_rgb=(0.03, 0.03, 0.04),
        mid_rgb=(0.07, 0.07, 0.09),
        tip_rgb=(0.03, 0.03, 0.04),
        hi_rgb=(0.45, 0.48, 0.55),
        band_v=0.45, band_strength=0.42
    )

    m_face   = kit.make_mat("Maya_Face_Tex", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin   = kit.make_mat("Maya_Skin",     (0.992, 0.942, 0.935, 1.0), roughness=0.90, specular=0.03)
    m_hair   = kit.make_mat("Maya_Hair",     (1.0, 1.0, 1.0, 1.0), roughness=0.72, specular=0.10, image=img_hair)
    m_dress  = kit.make_mat("Maya_Dress",    (0.055, 0.055, 0.068, 1.0), roughness=0.84, specular=0.06)
    m_lace   = kit.make_mat("Maya_Lace",     (0.095, 0.090, 0.110, 1.0), roughness=0.90, specular=0.04)
    m_silver = kit.make_mat("Maya_Silver",   (0.84, 0.86, 0.90, 1.0), roughness=0.28, metallic=0.78, specular=0.30)
    m_red    = kit.make_mat("Maya_RedCharm", (0.82, 0.08, 0.12, 1.0), roughness=0.35, metallic=0.2, specular=0.25)
    m_boot   = kit.make_mat("Maya_Boot",     (0.06, 0.06, 0.07, 1.0), roughness=0.48, specular=0.18)

    # --- HEAD ---
    head_parts = []
    HZ = 1.69
    head_parts.append(kit.create_anime_head_shell(HZ, m_face, m_skin, style="cute_round"))
    head_parts.append(kit.create_hair_crown_shell(HZ, m_hair, rx_base=0.418, ry_base=0.398, rz_base=0.398, cutout_z=0.44))

    # Sleek Straight Blunt Bangs (signature P4NDORA bob, curved smoothly from dome)
    for idx, bx in enumerate((-0.23, -0.14, -0.05, 0.04, 0.13, 0.22)):
        strand = [
            (Vector((bx * 0.62, -0.12, HZ + 0.39)), 0.054, 0.022, bx * -0.35),
            (Vector((bx * 0.92, -0.34, HZ + 0.26)), 0.068, 0.024, bx * -0.35),
            (Vector((bx * 1.00, -0.39, HZ + 0.12)), 0.062, 0.020, bx * -0.35),
            (Vector((bx * 1.00, -0.38, HZ + 0.06)), 0.050, 0.012, bx * -0.35),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"PandoraBang_{idx}", strand, m_hair, v_start=0.08, v_end=0.92))

    # Sleek Smooth Rounded-Crown Shoulder-Length Straight Bob Curtains (zero square ears!)
    for side in (-1.0, 1.0):
        for j, y_off in enumerate((-0.19, -0.05, 0.10, 0.24)):
            x_rad = 0.402 - abs(y_off) * 0.18
            twist = side * (-0.98 + j * 0.24)
            curtain = [
                (Vector((side * (x_rad * 0.45), y_off * 0.55, HZ + 0.37)), 0.056, 0.022, twist),
                (Vector((side * (x_rad + 0.01), y_off * 0.95, HZ + 0.20)), 0.096, 0.032, twist),
                (Vector((side * (x_rad + 0.03), y_off,        HZ - 0.10)), 0.102, 0.032, twist),
                (Vector((side * (x_rad - 0.02), y_off,        HZ - 0.40)), 0.088, 0.020, twist),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(f"PandoraSide_{side}_{j}", curtain, m_hair, v_start=0.08, v_end=0.94))

    for idx, x_off in enumerate((-0.26, -0.13, 0.0, 0.13, 0.26)):
        back_s = [
            (Vector((x_off * 0.48, 0.16, HZ + 0.36)), 0.058, 0.024, x_off * 1.1),
            (Vector((x_off * 0.88, 0.38, HZ + 0.16)), 0.098, 0.034, x_off * 1.1),
            (Vector((x_off * 0.98, 0.40, HZ - 0.14)), 0.102, 0.034, x_off * 1.1),
            (Vector((x_off * 0.94, 0.37, HZ - 0.40)), 0.086, 0.020, x_off * 1.1),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"PandoraBack_{idx}", back_s, m_hair, v_start=0.08, v_end=0.94))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "NPC_Maya_Head")

    # --- BODY ---
    body_parts = []
    neck_obj = kit.create_loft_object("Neck", [
        (Vector((0.0, 0.0, 1.24)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.094, 0.0))),
        (Vector((0.0, 0.0, 1.46)), Vector((0.088, 0.0, 0.0)), Vector((0.0, 0.086, 0.0))),
    ], m_skin)

    # Signature Black Choker + Gleaming Silver O-Ring!
    body_parts.append(kit.create_loft_object("Choker_Band", [
        (Vector((0.0, 0.0, 1.335)), Vector((0.104, 0.0, 0.0)), Vector((0.0, 0.100, 0.0))),
        (Vector((0.0, 0.0, 1.385)), Vector((0.102, 0.0, 0.0)), Vector((0.0, 0.098, 0.0))),
    ], m_dress))
    body_parts.append(kit.create_torus_ring("Choker_Ring", (0.0, -0.112, 1.360), 0.028, 0.008, m_silver, rot_euler=(math.pi * 0.5, 0.0, 0.0)))

    # Pale upper chest & bare shoulders
    body_parts.append(kit.create_loft_object("UpperChest_Skin", [
        (Vector((0.0, 0.000, 1.14)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.160, 0.0))),
        (Vector((0.0, 0.000, 1.28)), Vector((0.195, 0.0, 0.0)), Vector((0.0, 0.150, 0.0))),
    ], m_skin))

    # Chic Gothic Black Sleeveless Mini Dress (fitted bodice + flared pleated mini skirt!)
    dress_rings = [
        (Vector((0.0, 0.002, 0.48)), Vector((0.315, 0.0, 0.0)), Vector((0.0, 0.265, 0.0))),
        (Vector((0.0, 0.002, 0.62)), Vector((0.270, 0.0, 0.0)), Vector((0.0, 0.222, 0.0))),
        (Vector((0.0, 0.000, 0.78)), Vector((0.208, 0.0, 0.0)), Vector((0.0, 0.166, 0.0))),
        (Vector((0.0, -0.006, 0.96)), Vector((0.215, 0.0, 0.0)), Vector((0.0, 0.174, 0.0))),
        (Vector((0.0, -0.010, 1.12)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
        (Vector((0.0, -0.006, 1.18)), Vector((0.215, 0.0, 0.0)), Vector((0.0, 0.172, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Gothic_Dress", dress_rings, m_dress))

    # Ruffled Lace Neckline Trim + Delicate Spaghetti Shoulder Straps
    for k in range(12):
        ang = math.radians(-150.0 + k * (300.0 / 11.0))
        rx = math.sin(ang) * 0.212
        ry = -math.cos(ang) * 0.172
        body_parts.append(kit.create_ellipsoid(f"DressRuffle_{k}", (rx, ry, 1.18), (0.036, 0.030, 0.028), m_lace))
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(f"DressStrapF_{side}", (side * 0.150, -0.140, 1.24), (0.022, 0.022, 0.16), m_dress, rot_euler=(-0.18, 0.0, side * 0.12), bevel_width=0.005))
        body_parts.append(kit.create_rounded_box(f"DressStrapB_{side}", (side * 0.145,  0.135, 1.24), (0.022, 0.022, 0.16), m_dress, rot_euler=( 0.18, 0.0, side * 0.12), bevel_width=0.005))

    # Bare Pale Arms (dense multi-ring loft for buttery-smooth elbow bend!) + Black Charm Bracelet on Right Wrist (-X)
    AZ = 1.23
    for side in (1.0, -1.0):
        body_parts.append(kit.create_ellipsoid(f"ShoulderSkin_{side}", (side * 0.195, 0.0, AZ), (0.092, 0.092, 0.090), m_skin))
        body_parts.append(kit.create_loft_object(f"ArmSkin_{side}", [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.084, 0.0)), Vector((0.0, 0.0, 0.084 * side))),
            (Vector((side * 0.28, 0.0, AZ)), Vector((0.0, 0.080, 0.0)), Vector((0.0, 0.0, 0.080 * side))),
            (Vector((side * 0.38, 0.0, AZ)), Vector((0.0, 0.076, 0.0)), Vector((0.0, 0.0, 0.076 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.074, 0.0)), Vector((0.0, 0.0, 0.074 * side))),
            (Vector((side * 0.58, 0.0, AZ)), Vector((0.0, 0.070, 0.0)), Vector((0.0, 0.0, 0.070 * side))),
            (Vector((side * 0.72, 0.0, AZ)), Vector((0.0, 0.066, 0.0)), Vector((0.0, 0.0, 0.066 * side))),
        ], m_skin))
        body_parts.append(kit.create_ellipsoid(f"HandSkin_{side}", (side * 0.79, -0.004, AZ), (0.088, 0.082, 0.072), m_skin))
        # Chic black bracelet + red diamond charm on right wrist!
        if side < 0:
            body_parts.append(kit.create_torus_ring("CharmBracelet", (side * 0.67, 0.0, AZ), 0.072, 0.012, m_dress, rot_euler=(0.0, math.pi * 0.5, 0.0)))
            body_parts.append(kit.create_ellipsoid("RedCharm", (side * 0.67, -0.082, AZ - 0.022), (0.018, 0.014, 0.024), m_red))
            body_parts.append(kit.create_ellipsoid("BlackCharm", (side * 0.67, -0.068, AZ - 0.068), (0.020, 0.014, 0.020), m_dress))

    # Slender Pale Legs + Chic Glossy Black Gothic Boots with Silver Buckles
    for side in (1.0, -1.0):
        body_parts.append(kit.create_loft_object(f"LegSkin_{side}", [
            (Vector((side * 0.146, 0.002, 0.60)), Vector((0.104, 0.0, 0.0)), Vector((0.0, -0.106, 0.0))),
            (Vector((side * 0.150, 0.000, 0.36)), Vector((0.092, 0.0, 0.0)), Vector((0.0, -0.094, 0.0))),
            (Vector((side * 0.152, 0.000, 0.24)), Vector((0.086, 0.0, 0.0)), Vector((0.0, -0.088, 0.0))),
        ], m_skin))
        # Black gothic boots
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", [
            (Vector((side * 0.152,  0.000, 0.270)), Vector((0.098, 0.0, 0.0)), Vector((0.0, -0.100, 0.0))),
            (Vector((side * 0.152, -0.012, 0.140)), Vector((0.104, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
            (Vector((side * 0.152, -0.052, 0.074)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.172, 0.0))),
            (Vector((side * 0.152, -0.060, 0.036)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.180, 0.0))),
        ], m_boot))
        body_parts.append(kit.create_rounded_box(f"BootBuckle_{side}", (side * 0.152, -0.108, 0.22), (0.052, 0.022, 0.034), m_silver, bevel_width=0.005))
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", [
            (Vector((side * 0.152, -0.060, 0.040)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.186, 0.0))),
            (Vector((side * 0.152, -0.060, 0.003)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.182, 0.0))),
        ], m_dress))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "NPC_Maya_Body")
    export_npc_with_kit(scene, head_obj, body_obj, "NPC_Maya_Scout")


# ==============================================================================
#  3. NPC_Roy_Veteran — Cyan Ponytail "69" Cyber-Tech Girl (Reference 3)
# ==============================================================================

def generate_roy_cyan69_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.985, 0.920, 0.890)
    SKIN_WARM   = (0.935, 0.840, 0.810)
    SKIN_SHADOW = (0.855, 0.745, 0.725)
    BLUSH_COL   = (0.960, 0.560, 0.600)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.88, V) * 0.16, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.12, SKIN_WARM)

    for side in (-1.0, 1.0):
        d_b = np.hypot((U - (0.5 + side * 0.172)) / 0.064, (V - 0.376) / 0.025)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.18, 1.0, d_b)) * 0.18, BLUSH_COL)

    d_n = np.hypot((U - 0.500) / 0.011, (V - 0.392) / 0.010)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.3, 1.0, d_n)) * 0.34, SKIN_SHADOW)

    # Fierce Vivid Purple-Magenta Anime Eyes (Reference 3!)
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.148
        cy = 0.466
        ex = (U - cx) * side
        ey = V - cy

        r_eye = np.hypot(ex / 0.082, ey / 0.064)
        eye_mask = 1.0 - kit.smoothstep(1.0 - aa / 0.082, 1.0 + aa / 0.082, r_eye)
        kit.alpha_blend(canvas, eye_mask, (0.996, 0.996, 1.000))
        kit.alpha_blend(canvas, eye_mask * kit.smoothstep(0.038, 0.064, ey) * 0.24, (0.72, 0.62, 0.84))

        rx_i, ry_i = 0.052, 0.056
        r_iris = np.hypot(ex / rx_i, ey / ry_i)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        v_grad = np.clip((ey + ry_i) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(0.88 - 0.40 * np.power(v_grad, 1.15), 0.0, 1.0)
        iris_g = np.clip(0.26 - 0.18 * np.power(v_grad, 1.10), 0.0, 1.0)
        iris_b = np.clip(0.94 - 0.36 * np.power(v_grad, 1.20), 0.0, 1.0)
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Glowing magenta lower crescent + dark pupil + double catchlights
        r_glow = np.hypot(ex / (rx_i * 0.70), (ey + 0.022) / (ry_i * 0.35))
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.28, 1.0, r_glow)) * 0.80, (1.0, 0.62, 0.96))
        kit.alpha_blend(canvas, iris_mask * kit.smoothstep(0.82, 0.96, r_iris) * 0.90, (0.14, 0.03, 0.22))
        r_pupil = np.hypot(ex / 0.020, ey / 0.022)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.82, 1.05, r_pupil)), (0.08, 0.02, 0.14))

        r_hi1 = np.hypot((ex - 0.020) / 0.014, (ey - 0.020) / 0.013)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi1)), (1.0, 1.0, 1.0))
        r_hi2 = np.hypot((ex + 0.018) / 0.007, (ey + 0.018) / 0.007)
        kit.alpha_blend(canvas, eye_mask * (1.0 - kit.smoothstep(0.75, 1.02, r_hi2)), (1.0, 0.96, 1.0))

        # Confident sharp anime lashes
        u_lash_pts = [
            (cx + side * (-0.080), cy + 0.012),
            (cx + side * (-0.032), cy + 0.056),
            (cx + side * ( 0.020), cy + 0.062),
            (cx + side * ( 0.072), cy + 0.048),
            (cx + side * ( 0.102), cy + 0.064),
        ]
        d_ul = kit.polyline_sdf(U, V, u_lash_pts, [0.005, 0.012, 0.014, 0.012, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_ul), (0.05, 0.04, 0.06))

        # Cyan-tinted sharp eyebrows!
        brow_pts = [
            (cx + side * (-0.068), cy + 0.098),
            (cx + side * ( 0.006), cy + 0.114),
            (cx + side * ( 0.076), cy + 0.102),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.005, 0.008, 0.003])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.18, 0.56, 0.64))

    # Rich dark-crimson lips
    lip_upper = np.hypot((U - 0.500) / 0.040, (V - 0.302) / 0.014)
    lip_lower = np.hypot((U - 0.500) / 0.035, (V - 0.289) / 0.015)
    lip_mask = np.maximum(1.0 - kit.smoothstep(0.82, 1.05, lip_upper), 1.0 - kit.smoothstep(0.82, 1.05, lip_lower))
    kit.alpha_blend(canvas, lip_mask, (0.62, 0.10, 0.16))

    path = os.path.join(kit.TEX_DIR, "NPC_Roy_Face.png")
    return kit.save_rgba_array_to_png(canvas, path, "NPC_Roy_Face.png")


def build_roy_cyan69():
    scene = kit.reset_scene()
    img_face = generate_roy_cyan69_face_texture()
    img_hair = kit.generate_hair_gradient_texture(
        os.path.join(kit.TEX_DIR, "NPC_Roy_Hair.png"), "NPC_Roy_Hair.png",
        root_rgb=(0.16, 0.56, 0.64),
        mid_rgb=(0.36, 0.80, 0.88),
        tip_rgb=(0.56, 0.92, 0.96),
        hi_rgb=(0.86, 0.99, 1.00),
        band_v=0.52, band_strength=0.40
    )

    m_face   = kit.make_mat("Roy_Face_Tex", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=img_face)
    m_skin   = kit.make_mat("Roy_Skin",     (0.985, 0.920, 0.890, 1.0), roughness=0.90, specular=0.03)
    m_hair   = kit.make_mat("Roy_Hair",     (1.0, 1.0, 1.0, 1.0), roughness=0.80, specular=0.08, image=img_hair)
    m_white  = kit.make_mat("Roy_WhiteTop", (0.97, 0.97, 0.98, 1.0), roughness=0.88, specular=0.04)
    m_black  = kit.make_mat("Roy_Black",    (0.08, 0.08, 0.10, 1.0), roughness=0.85, specular=0.06)
    m_yellow = kit.make_mat("Roy_Yellow",   (0.98, 0.78, 0.08, 1.0), roughness=0.65, metallic=0.15, specular=0.12)
    m_gold   = kit.make_mat("Roy_Gold",     (0.96, 0.76, 0.14, 1.0), roughness=0.30, metallic=0.75, specular=0.28)
    m_grey   = kit.make_mat("Roy_DarkGrey", (0.18, 0.20, 0.24, 1.0), roughness=0.82, specular=0.06)

    # --- HEAD ---
    head_parts = []
    HZ = 1.69
    head_parts.append(kit.create_anime_head_shell(HZ, m_face, m_skin, style="cute_round"))
    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.368, 0.018, HZ - 0.032), (0.048, 0.036, 0.074),
            m_skin, rot_euler=(0.06, side * -0.18, side * 0.16)
        ))
        # Gold hoop earrings!
        head_parts.append(kit.create_torus_ring(f"Earring_{side}", (side * 0.392, 0.012, HZ - 0.102), 0.030, 0.008, m_gold, rot_euler=(0.0, math.pi * 0.5, 0.0)))

    head_parts.append(kit.create_hair_crown_shell(HZ, m_hair, rx_base=0.415, ry_base=0.395, rz_base=0.395, cutout_z=0.66))

    # Sweeping Center-Parted Face-Framing Cyan Bangs (exact Reference 3, clean parted hairline!)
    for side in (-1.0, 1.0):
        # Soft inner brow-sweeping part lock
        lock0 = [
            (Vector((side * 0.02, -0.22, HZ + 0.39)), 0.080, 0.028, side * -0.10),
            (Vector((side * 0.14, -0.36, HZ + 0.25)), 0.078, 0.026, side * -0.26),
            (Vector((side * 0.22, -0.37, HZ + 0.12)), 0.048, 0.016, side * -0.40),
            (Vector((side * 0.24, -0.36, HZ + 0.04)), 0.012, 0.006, side * -0.45),
        ]
        # Outer long sweeping cheek lock
        lock1 = [
            (Vector((side * 0.04, -0.22, HZ + 0.39)), 0.094, 0.034, side * -0.18),
            (Vector((side * 0.24, -0.34, HZ + 0.24)), 0.095, 0.034, side * -0.45),
            (Vector((side * 0.34, -0.33, HZ + 0.02)), 0.076, 0.026, side * -0.68),
            (Vector((side * 0.30, -0.30, HZ - 0.22)), 0.040, 0.014, side * -0.78),
            (Vector((side * 0.26, -0.28, HZ - 0.32)), 0.008, 0.005, side * -0.82),
        ]
        # Secondary side temple strand
        lock2 = [
            (Vector((side * 0.12, -0.18, HZ + 0.36)), 0.088, 0.032, side * -0.35),
            (Vector((side * 0.32, -0.22, HZ + 0.16)), 0.084, 0.030, side * -0.62),
            (Vector((side * 0.39, -0.18, HZ - 0.08)), 0.056, 0.020, side * -0.78),
            (Vector((side * 0.36, -0.16, HZ - 0.24)), 0.010, 0.006, side * -0.85),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"CyanBang0_{side}", lock0, m_hair, v_start=0.06, v_end=0.94))
        head_parts.append(kit.create_ribbon_hair_strand(f"CyanBang1_{side}", lock1, m_hair, v_start=0.08, v_end=0.96))
        head_parts.append(kit.create_ribbon_hair_strand(f"CyanBang2_{side}", lock2, m_hair, v_start=0.10, v_end=0.96))

    # High Fluffy Cyan Back Ponytail with Black Hair Tie!
    pt_anchor = Vector((0.0, 0.38, HZ + 0.16))
    head_parts.append(kit.create_torus_ring("Ponytail_Tie", pt_anchor, 0.118, 0.034, m_black, rot_euler=(0.45, 0.0, 0.0)))

    ponytail_strands = [
        # Center main plume
        [
            (pt_anchor + Vector(( 0.00, 0.00,  0.02)), 0.145, 0.068,  0.00),
            (pt_anchor + Vector(( 0.00, 0.22,  0.14)), 0.165, 0.074,  0.00),
            (pt_anchor + Vector(( 0.00, 0.38, -0.06)), 0.148, 0.064,  0.00),
            (pt_anchor + Vector(( 0.00, 0.44, -0.32)), 0.082, 0.036,  0.00),
            (pt_anchor + Vector(( 0.00, 0.46, -0.46)), 0.012, 0.008,  0.00),
        ],
        # Left flared ponytail wave
        [
            (pt_anchor + Vector(( 0.04, 0.00,  0.00)), 0.132, 0.058, -0.32),
            (pt_anchor + Vector(( 0.18, 0.18,  0.06)), 0.142, 0.062, -0.42),
            (pt_anchor + Vector(( 0.26, 0.30, -0.14)), 0.118, 0.050, -0.48),
            (pt_anchor + Vector(( 0.24, 0.34, -0.36)), 0.056, 0.024, -0.52),
            (pt_anchor + Vector(( 0.28, 0.36, -0.46)), 0.010, 0.006, -0.55),
        ],
        # Right flared ponytail wave
        [
            (pt_anchor + Vector((-0.04, 0.00,  0.00)), 0.132, 0.058,  0.32),
            (pt_anchor + Vector((-0.18, 0.18,  0.06)), 0.142, 0.062,  0.42),
            (pt_anchor + Vector((-0.26, 0.30, -0.14)), 0.118, 0.050,  0.48),
            (pt_anchor + Vector((-0.24, 0.34, -0.36)), 0.056, 0.024,  0.52),
            (pt_anchor + Vector((-0.28, 0.36, -0.46)), 0.010, 0.006,  0.55),
        ],
        # Upper spiked crest flip
        [
            (pt_anchor + Vector(( 0.00, 0.02,  0.05)), 0.115, 0.052,  0.00),
            (pt_anchor + Vector(( 0.04, 0.24,  0.24)), 0.118, 0.052, -0.12),
            (pt_anchor + Vector(( 0.06, 0.42,  0.20)), 0.064, 0.028, -0.18),
            (pt_anchor + Vector(( 0.08, 0.50,  0.14)), 0.010, 0.006, -0.20),
        ],
        # Lower cascading under-plume
        [
            (pt_anchor + Vector(( 0.00, 0.02, -0.04)), 0.128, 0.056,  0.00),
            (pt_anchor + Vector(( 0.00, 0.20, -0.18)), 0.132, 0.056,  0.00),
            (pt_anchor + Vector(( 0.00, 0.28, -0.42)), 0.086, 0.036,  0.00),
            (pt_anchor + Vector(( 0.00, 0.32, -0.56)), 0.012, 0.007,  0.00),
        ],
    ]
    for idx, ps in enumerate(ponytail_strands):
        head_parts.append(kit.create_ribbon_hair_strand(f"CyanPonytail_{idx}", ps, m_hair, v_start=0.06, v_end=0.98))

    head_obj = kit.join_objects_into(head_parts[0], head_parts[1:], "NPC_Roy_Head")

    # --- BODY ---
    body_parts = []
    neck_obj = kit.create_loft_object("Neck", [
        (Vector((0.0, 0.0, 1.24)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.094, 0.0))),
        (Vector((0.0, 0.0, 1.46)), Vector((0.090, 0.0, 0.0)), Vector((0.0, 0.088, 0.0))),
    ], m_skin)

    # Signature Black Cyber High-Collar with Yellow "69" Trim!
    body_parts.append(kit.create_loft_object("Cyber_Collar", [
        (Vector((0.0, 0.0, 1.24)), Vector((0.155, 0.0, 0.0)), Vector((0.0, 0.142, 0.0))),
        (Vector((0.0, 0.0, 1.38)), Vector((0.118, 0.0, 0.0)), Vector((0.0, 0.112, 0.0))),
    ], m_black))
    body_parts.append(kit.create_rounded_box("Collar_69_Plate", (0.0, -0.134, 1.315), (0.088, 0.022, 0.050), m_yellow, bevel_width=0.005))
    body_parts.append(kit.create_rounded_box("Collar_69_Cut", (0.0, -0.142, 1.315), (0.046, 0.012, 0.022), m_black, bevel_width=0.003))

    # Exposed athletic midriff waist
    body_parts.append(kit.create_loft_object("Waist_Skin", [
        (Vector((0.0, 0.002, 0.78)), Vector((0.194, 0.0, 0.0)), Vector((0.0, 0.154, 0.0))),
        (Vector((0.0, 0.000, 0.96)), Vector((0.188, 0.0, 0.0)), Vector((0.0, 0.148, 0.0))),
    ], m_skin))

    # Crisp White Fitted Crop Tank Top!
    tank_rings = [
        (Vector((0.0, 0.000, 0.94)), Vector((0.208, 0.0, 0.0)), Vector((0.0, 0.166, 0.0))),
        (Vector((0.0, -0.012, 1.08)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, -0.004, 1.23)), Vector((0.214, 0.0, 0.0)), Vector((0.0, 0.172, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("White_TankTop", tank_rings, m_white))
    # Black shoulder harness straps + hip cross-straps
    for side in (-1.0, 1.0):
        body_parts.append(kit.create_rounded_box(f"CyberStrap_{side}", (side * 0.156, -0.152, 1.21), (0.040, 0.026, 0.19), m_black, rot_euler=(-0.18, 0.0, side * 0.22), bevel_width=0.006))
        body_parts.append(kit.create_rounded_box(f"CyberStrapBack_{side}", (side * 0.152, 0.148, 1.21), (0.040, 0.026, 0.19), m_black, rot_euler=( 0.18, 0.0, side * 0.22), bevel_width=0.006))
        body_parts.append(kit.create_rounded_box(f"HipHarness_{side}", (side * 0.160, -0.156, 0.84), (0.024, 0.020, 0.14), m_black, rot_euler=(0.0, 0.0, side * -0.45), bevel_width=0.004))

    # Bare Athletic Arms (smooth multi-ring loft) + Snug Black/Yellow "HUGE" Armband + Black Fingerless Tactical Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        body_parts.append(kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.198, 0.0, AZ), (0.094, 0.092, 0.092), m_skin))
        body_parts.append(kit.create_loft_object(f"Arm_{side}", [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.086, 0.0)), Vector((0.0, 0.0, 0.086 * side))),
            (Vector((side * 0.28, 0.0, AZ)), Vector((0.0, 0.082, 0.0)), Vector((0.0, 0.0, 0.082 * side))),
            (Vector((side * 0.38, 0.0, AZ)), Vector((0.0, 0.078, 0.0)), Vector((0.0, 0.0, 0.078 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.076, 0.0)), Vector((0.0, 0.0, 0.076 * side))),
            (Vector((side * 0.58, 0.0, AZ)), Vector((0.0, 0.072, 0.0)), Vector((0.0, 0.0, 0.072 * side))),
            (Vector((side * 0.72, 0.0, AZ)), Vector((0.0, 0.068, 0.0)), Vector((0.0, 0.0, 0.068 * side))),
        ], m_skin))
        # Snug Black & Yellow "HUGE" armband positioned cleanly on upper arm (zero shoulder stretch!)
        if side < 0:
            body_parts.append(kit.create_torus_ring("Huge_Armband_Top", (side * 0.445, 0.0, AZ), 0.082, 0.018, m_black, rot_euler=(0.0, math.pi * 0.5, 0.0)))
            body_parts.append(kit.create_torus_ring("Huge_Armband_Mid", (side * 0.470, 0.0, AZ), 0.084, 0.016, m_yellow, rot_euler=(0.0, math.pi * 0.5, 0.0)))
            body_parts.append(kit.create_torus_ring("Huge_Armband_Bot", (side * 0.495, 0.0, AZ), 0.082, 0.018, m_black, rot_euler=(0.0, math.pi * 0.5, 0.0)))
        else:
            body_parts.append(kit.create_torus_ring("LeftArmBand", (side * 0.56, 0.0, AZ), 0.078, 0.016, m_black, rot_euler=(0.0, math.pi * 0.5, 0.0)))

        # Black Fingerless Tactical Gloves
        body_parts.append(kit.create_rounded_box(f"TacGlove_{side}", (side * 0.78, -0.004, AZ), (0.130, 0.156, 0.124), m_black, bevel_width=0.022))
        body_parts.append(kit.create_ellipsoid(f"GloveCutout_{side}", (side * 0.78, -0.004, AZ + 0.056), (0.036, 0.042, 0.014), m_skin))
        body_parts.append(kit.create_ellipsoid(f"Fingers_{side}", (side * 0.87, -0.004, AZ - 0.005), (0.058, 0.068, 0.052), m_skin))

    # Black Tactical Shorts with Yellow Vertical Side Stripes & Cargo Pockets!
    body_parts.append(kit.create_loft_object("Black_ShortsPelvis", [
        (Vector((0.0, 0.002, 0.56)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.200, 0.0))),
        (Vector((0.0, 0.004, 0.72)), Vector((0.246, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, 0.002, 0.80)), Vector((0.214, 0.0, 0.0)), Vector((0.0, 0.170, 0.0))),
    ], m_black))

    for side in (1.0, -1.0):
        body_parts.append(kit.create_loft_object(f"ShortLeg_{side}", [
            (Vector((side * 0.148, 0.002, 0.68)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.150, 0.002, 0.51)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
        ], m_black))
        # Iconic bright yellow vertical side stripe on shorts!
        body_parts.append(kit.create_rounded_box(f"YellowStripe_{side}", (side * 0.268, 0.0, 0.64), (0.024, 0.046, 0.24), m_yellow, bevel_width=0.006))
        body_parts.append(kit.create_rounded_box(f"ShortPocket_{side}", (side * 0.228, -0.088, 0.59), (0.068, 0.052, 0.112), m_grey, bevel_width=0.010))

        # Bare thighs -> High black tactical stockings/boots with yellow soles
        body_parts.append(kit.create_loft_object(f"ThighSkin_{side}", [
            (Vector((side * 0.150, 0.000, 0.52)), Vector((0.104, 0.0, 0.0)), Vector((0.0, -0.106, 0.0))),
            (Vector((side * 0.152, 0.000, 0.36)), Vector((0.094, 0.0, 0.0)), Vector((0.0, -0.096, 0.0))),
        ], m_skin))
        body_parts.append(kit.create_loft_object(f"TacBoot_{side}", [
            (Vector((side * 0.152,  0.000, 0.370)), Vector((0.100, 0.0, 0.0)), Vector((0.0, -0.102, 0.0))),
            (Vector((side * 0.152,  0.000, 0.200)), Vector((0.098, 0.0, 0.0)), Vector((0.0, -0.100, 0.0))),
            (Vector((side * 0.152, -0.016, 0.120)), Vector((0.112, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.152, -0.056, 0.072)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.152, -0.062, 0.036)), Vector((0.130, 0.0, 0.0)), Vector((0.0, -0.186, 0.0))),
        ], m_black))
        body_parts.append(kit.create_torus_ring(f"BootTrim_{side}", (side * 0.152, 0.0, 0.365), 0.102, 0.014, m_yellow))
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", [
            (Vector((side * 0.152, -0.062, 0.040)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ], m_yellow))

    body_obj = kit.join_objects_into(neck_obj, body_parts, "NPC_Roy_Body")
    export_npc_with_kit(scene, head_obj, body_obj, "NPC_Roy_Veteran")


if __name__ == "__main__":
    print("=== BUILDING 3 CUSTOM CHIBI ANIME NPCS (ELENA, MAYA, ROY) ===")
    build_elena_yellow69()
    build_maya_pandora()
    build_roy_cyan69()
    print("=== ALL 3 CUSTOM CHIBI ANIME NPCS EXPORTED & RENDERED! ===")

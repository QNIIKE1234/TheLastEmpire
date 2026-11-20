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

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_WhiteRonin_Face.png")
HAIR_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_WhiteRonin_Hair.png")


def generate_white_ronin_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.955, 0.725, 0.560)
    SKIN_WARM   = (0.880, 0.610, 0.440)
    SKIN_SHADOW = (0.760, 0.490, 0.340)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE

    # Clean cel-shaded forehead shadow (under headband) & chin contour
    kit.alpha_blend(canvas, kit.smoothstep(0.62, 0.85, V) * 0.28, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.18, SKIN_WARM)

    # Crisp anime nose bridge shadow
    nose_pts = [(0.494, 0.448), (0.488, 0.382), (0.506, 0.376)]
    d_nose = kit.polyline_sdf(U, V, nose_pts, [0.005, 0.011, 0.007])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, d_nose)) * 0.48, SKIN_SHADOW)

    # Sharp furious dark brows peaking cleanly above the red sunglasses frame
    # (No white sclera behind the red lenses so nothing peeks out from under the visor!)
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.145
        cy = 0.478
        brow_pts = [
            (cx + side * (-0.095), cy + 0.048),
            (cx + side * (-0.025), cy + 0.078),
            (cx + side * ( 0.050), cy + 0.106),
            (cx + side * ( 0.108), cy + 0.120),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.009, 0.015, 0.014, 0.005])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.08, 0.06, 0.08))

    # Cool stoic focused mouth
    mouth_pts = [(0.440, 0.296), (0.500, 0.304), (0.560, 0.296)]
    d_m = kit.polyline_sdf(U, V, mouth_pts, [0.0030, 0.0048, 0.0030])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_m), (0.30, 0.14, 0.10))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_WhiteRonin_Face.png")


IMG_FACE = generate_white_ronin_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_WhiteRonin_Hair.png",
    root_rgb=(0.66, 0.69, 0.75),
    mid_rgb=(0.86, 0.88, 0.92),
    tip_rgb=(0.97, 0.98, 1.00),
    hi_rgb=(1.00, 1.00, 1.00),
    band_v=0.56, band_strength=0.22
)

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.955, 0.725, 0.560, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_WHITE  = kit.make_mat("Mat_Hair_White",   (1.0, 1.0, 1.0, 1.0), roughness=0.84, specular=0.05, image=IMG_HAIR)
MAT_CLOTH_KHAKI = kit.make_mat("Mat_Cloth_Khaki",  (0.68, 0.54, 0.26, 1.0), roughness=0.90, specular=0.03)
MAT_POUCH_TAN   = kit.make_mat("Mat_Pouch_Tan",    (0.46, 0.36, 0.20, 1.0), roughness=0.90, specular=0.04)
MAT_JACKET_DARK = kit.make_mat("Mat_Jacket_Dark",  (0.11, 0.11, 0.13, 1.0), roughness=0.90, specular=0.04)
MAT_CLOTH_MID   = kit.make_mat("Mat_Cloth_Mid",    (0.19, 0.19, 0.22, 1.0), roughness=0.88, specular=0.04)
MAT_LEATHER_BRN = kit.make_mat("Mat_Leather_Brn",  (0.18, 0.11, 0.07, 1.0), roughness=0.88, specular=0.04)
MAT_ARMOR_RED   = kit.make_mat("Mat_Armor_Red",    (0.76, 0.06, 0.07, 1.0), roughness=0.76, metallic=0.06, specular=0.08)
MAT_VISOR_RED   = kit.make_mat("Mat_Visor_Red",    (0.92, 0.04, 0.05, 1.0), roughness=0.14, metallic=0.10, specular=0.65)
MAT_FRAME_BLACK = kit.make_mat("Mat_Frame_Black",  (0.04, 0.04, 0.05, 1.0), roughness=0.50, metallic=0.10, specular=0.15)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver", (0.58, 0.62, 0.68, 1.0), roughness=0.40, metallic=0.72, specular=0.25)


def build_white_ronin_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="male_strong")
    head_parts.append(head_base)

    # Ears
    for side in (1.0, -1.0):
        ear = kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.372, 0.016, HZ - 0.022), (0.046, 0.036, 0.074),
            MAT_SKIN, rot_euler=(0.06, side * -0.18, side * 0.14)
        )
        head_parts.append(ear)

    # Iconic Sleek Red Visor Sunglasses (wrap-around black rim + glossy bright red lenses!)
    visor_bridge = kit.create_rounded_box("Visor_Bridge", (0.0, -0.362, HZ + 0.002), (0.085, 0.038, 0.036), MAT_FRAME_BLACK, bevel_width=0.008)
    head_parts.append(visor_bridge)
    for side in (1.0, -1.0):
        rim = kit.create_rounded_box(
            f"Visor_Rim_{side}", (side * 0.135, -0.344, HZ - 0.004),
            (0.185, 0.046, 0.118), MAT_FRAME_BLACK,
            rot_euler=(0.04, side * -0.24, side * 0.05), bevel_width=0.014
        )
        lens = kit.create_rounded_box(
            f"Visor_Lens_{side}", (side * 0.136, -0.360, HZ - 0.004),
            (0.156, 0.032, 0.088), MAT_VISOR_RED,
            rot_euler=(0.04, side * -0.24, side * 0.05), bevel_width=0.010
        )
        temple = kit.create_rounded_box(
            f"Visor_Temple_{side}", (side * 0.315, -0.165, HZ + 0.012),
            (0.032, 0.340, 0.034), MAT_FRAME_BLACK,
            rot_euler=(-0.05, 0.0, side * 0.32), bevel_width=0.006
        )
        head_parts.extend([rim, lens, temple])

    # Woven Khaki Ninja Headband wrapped tightly around forehead + knot + fluttering twin tails!
    hb_rings = [
        (Vector((0.0, 0.008, HZ + 0.095)), Vector((0.398, 0.0, 0.0)), Vector((0.0, 0.378, 0.0))),
        (Vector((0.0, 0.006, HZ + 0.145)), Vector((0.405, 0.0, 0.0)), Vector((0.0, 0.384, 0.0))),
        (Vector((0.0, 0.008, HZ + 0.195)), Vector((0.394, 0.0, 0.0)), Vector((0.0, 0.372, 0.0))),
    ]
    head_parts.append(kit.create_loft_object("Headband_Main", hb_rings, MAT_CLOTH_KHAKI))
    hb_knot = kit.create_ellipsoid("Headband_Knot", (-0.12, 0.375, HZ + 0.135), (0.068, 0.055, 0.062), MAT_CLOTH_KHAKI)
    head_parts.append(hb_knot)

    # Twin fluttering khaki headband tails at back
    tail1 = [
        (Vector((-0.12, 0.38, HZ + 0.13)), 0.062, 0.018,  0.15),
        (Vector((-0.26, 0.48, HZ + 0.08)), 0.072, 0.016,  0.22),
        (Vector((-0.44, 0.56, HZ - 0.02)), 0.065, 0.014,  0.10),
        (Vector((-0.56, 0.62, HZ - 0.10)), 0.012, 0.006,  0.00),
    ]
    tail2 = [
        (Vector((-0.09, 0.38, HZ + 0.11)), 0.056, 0.016, -0.10),
        (Vector((-0.18, 0.50, HZ - 0.02)), 0.064, 0.014, -0.16),
        (Vector((-0.28, 0.58, HZ - 0.18)), 0.055, 0.012, -0.10),
        (Vector((-0.34, 0.62, HZ - 0.28)), 0.010, 0.005,  0.00),
    ]
    head_parts.append(kit.create_ribbon_hair_strand("Headband_Tail1", tail1, MAT_CLOTH_KHAKI, v_start=0.0, v_end=1.0))
    head_parts.append(kit.create_ribbon_hair_strand("Headband_Tail2", tail2, MAT_CLOTH_KHAKI, v_start=0.0, v_end=1.0))

    # Full White Anime Hair Crown Cap (smoothly covering top & back above headband, plus temple sides)
    crown_cap = kit.create_ellipsoid("Hair_CrownCap", (0.0, 0.032, HZ + 0.175), (0.376, 0.378, 0.245), MAT_HAIR_WHITE, uv_v=0.32)
    back_nape = kit.create_ellipsoid("Hair_BackNape", (0.0, 0.165, HZ + 0.015), (0.340, 0.250, 0.240), MAT_HAIR_WHITE, uv_v=0.25)
    head_parts.extend([crown_cap, back_nape])

    # Swept Spiky White Front Bangs & Side Spikes (cascading dramatically forward/left like portrait!)
    bang_specs = [
        # (start_x, start_y, start_z, mid_x, mid_y, mid_z, tip_x, tip_y, tip_z, w, t, roll)
        ( 0.02,  0.02, 0.39,  0.16, -0.26, 0.30,  0.26, -0.44, 0.08, 0.125, 0.042, -0.18),
        ( 0.10,  0.02, 0.38,  0.28, -0.20, 0.26,  0.38, -0.34, 0.00, 0.115, 0.038, -0.36),
        ( 0.18,  0.04, 0.34,  0.36, -0.10, 0.20,  0.44, -0.22,-0.06, 0.100, 0.034, -0.52),
        (-0.06,  0.02, 0.40,  0.04, -0.28, 0.33,  0.12, -0.46, 0.15, 0.120, 0.040,  0.05),
        (-0.14,  0.02, 0.38, -0.08, -0.28, 0.30, -0.02, -0.44, 0.14, 0.112, 0.038,  0.18),
        (-0.22,  0.02, 0.35, -0.22, -0.24, 0.26, -0.20, -0.39, 0.12, 0.102, 0.034,  0.32),
        (-0.28,  0.04, 0.30, -0.34, -0.14, 0.21, -0.38, -0.26, 0.08, 0.092, 0.030,  0.46),
        # Side & back layered spiky tufts for lush anime silhouette from 3rd-person camera!
        ( 0.14,  0.12, 0.38,  0.34,  0.08, 0.30,  0.44,  0.02, 0.14, 0.102, 0.035, -0.45),
        (-0.14,  0.12, 0.38, -0.34,  0.08, 0.30, -0.42,  0.02, 0.14, 0.102, 0.035,  0.45),
        ( 0.00,  0.14, 0.40,  0.00,  0.34, 0.30,  0.00,  0.43, 0.04, 0.125, 0.040,  0.00),
        ( 0.14,  0.16, 0.36,  0.24,  0.33, 0.24,  0.26,  0.40, 0.00, 0.112, 0.036, -0.25),
        (-0.14,  0.16, 0.36, -0.24,  0.33, 0.24, -0.26,  0.40, 0.00, 0.112, 0.036,  0.25),
        ( 0.24,  0.14, 0.30,  0.34,  0.24, 0.16,  0.36,  0.30,-0.04, 0.102, 0.034, -0.42),
        (-0.24,  0.14, 0.30, -0.34,  0.24, 0.16, -0.36,  0.30,-0.04, 0.102, 0.034,  0.42),
        ( 0.06,  0.22, 0.26,  0.10,  0.38, 0.10,  0.12,  0.41,-0.10, 0.108, 0.034, -0.10),
        (-0.08,  0.22, 0.26, -0.12,  0.38, 0.10, -0.14,  0.41,-0.10, 0.108, 0.034,  0.10),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, x2, y2, z2, w, t, roll) in enumerate(bang_specs):
        strand = [
            (Vector((x0, y0, HZ + z0)), w * 0.72, t * 0.75, roll),
            (Vector((x1, y1, HZ + z1)), w * 1.00, t * 1.00, roll),
            (Vector((x2, y2, HZ + z2)), w * 0.50, t * 0.50, roll),
            (Vector((x2 * 1.08, y2 * 1.08, HZ + z2 - 0.06)), 0.008, 0.005, roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"RoninHair_{idx}", strand, MAT_HAIR_WHITE, v_start=0.05, v_end=0.98))

    # Iconic Messy White Samurai Topknot Ponytail on upper-back crown + dark brown hair tie!
    tie_ring = [
        (Vector((-0.05, 0.14, HZ + 0.38)), Vector((0.068, 0.0, 0.0)), Vector((0.0, 0.062, 0.0))),
        (Vector((-0.06, 0.16, HZ + 0.43)), Vector((0.062, 0.0, 0.0)), Vector((0.0, 0.056, 0.0))),
    ]
    head_parts.append(kit.create_loft_object("Topknot_Tie", tie_ring, MAT_LEATHER_BRN))
    topknot_tufts = [
        ((-0.06, 0.16, 0.43), (-0.12, 0.24, 0.56), (-0.18, 0.34, 0.58), 0.088, 0.034,  0.22),
        ((-0.06, 0.16, 0.43), (-0.02, 0.26, 0.58), ( 0.04, 0.38, 0.56), 0.084, 0.032, -0.15),
        ((-0.06, 0.16, 0.43), (-0.16, 0.26, 0.48), (-0.28, 0.38, 0.42), 0.082, 0.030,  0.40),
        ((-0.06, 0.16, 0.43), (-0.06, 0.28, 0.48), (-0.08, 0.42, 0.40), 0.080, 0.030,  0.00),
        ((-0.06, 0.16, 0.43), (-0.08, 0.18, 0.58), (-0.10, 0.22, 0.68), 0.072, 0.028,  0.10),
    ]
    for idx, (p0, p1, p2, w, t, roll) in enumerate(topknot_tufts):
        strand = [
            (Vector((p0[0], p0[1], HZ + p0[2])), w * 0.60, t * 0.65, roll),
            (Vector((p1[0], p1[1], HZ + p1[2])), w * 1.00, t * 1.00, roll),
            (Vector((p2[0], p2[1], HZ + p2[2])), w * 0.48, t * 0.48, roll),
            (Vector((p2[0] * 1.12, p2[1] + 0.06, HZ + p2[2] - 0.02)), 0.008, 0.005, roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"Topknot_{idx}", strand, MAT_HAIR_WHITE, v_start=0.10, v_end=0.98))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_white_ronin_body():
    body_parts = []

    # Neck
    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.114, 0.0, 0.0)), Vector((0.0, 0.106, 0.0))),
        (Vector((0.0, 0.0, 1.48)), Vector((0.098, 0.0, 0.0)), Vector((0.0, 0.092, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Thick Wrapped Khaki Ninja Scarf around neck + sleek layered V-fold front drape
    scarf_rings = [
        (Vector((0.0, -0.010, 1.22)), Vector((0.218, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
        (Vector((0.0, -0.022, 1.30)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.205, 0.0))),
        (Vector((0.0, -0.014, 1.39)), Vector((0.204, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Neck_Scarf", scarf_rings, MAT_CLOTH_KHAKI))
    scarf_fold_l = kit.create_rounded_box("Scarf_FoldL", (-0.06, -0.188, 1.25), (0.165, 0.044, 0.072), MAT_CLOTH_KHAKI, rot_euler=(0.18,  0.36, 0.0), bevel_width=0.016)
    scarf_fold_r = kit.create_rounded_box("Scarf_FoldR", ( 0.06, -0.194, 1.24), (0.165, 0.044, 0.072), MAT_CLOTH_KHAKI, rot_euler=(0.18, -0.36, 0.0), bevel_width=0.016)
    body_parts.extend([scarf_fold_l, scarf_fold_r])

    # Sleek Athletic Shinobi Tactical Jacket Torso (NO sword on back!)
    torso_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.215, 0.0, 0.0)), Vector((0.0, 0.168, 0.0))),
        (Vector((0.0,  0.000, 0.90)), Vector((0.208, 0.0, 0.0)), Vector((0.0, 0.164, 0.0))),
        (Vector((0.0, -0.010, 1.06)), Vector((0.242, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, -0.008, 1.22)), Vector((0.250, 0.0, 0.0)), Vector((0.0, 0.196, 0.0))),
        (Vector((0.0,  0.000, 1.32)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.164, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_Jacket", torso_rings, MAT_JACKET_DARK))

    # Diagonal Front Lapel Trim + Leather Tactical Chest Harness + Silver Buckles
    lapel = kit.create_rounded_box("Jacket_LapelTrim", (0.015, -0.196, 1.04), (0.036, 0.026, 0.42), MAT_CLOTH_KHAKI, rot_euler=(0.0, -0.16, 0.0), bevel_width=0.006)
    body_parts.append(lapel)
    for side in (1.0, -1.0):
        h_front = kit.create_rounded_box(f"HarnessF_{side}", (side * 0.135, -0.202, 1.06), (0.050, 0.032, 0.44), MAT_LEATHER_BRN, rot_euler=(-0.05, side * 0.12, 0.0), bevel_width=0.008)
        h_back  = kit.create_rounded_box(f"HarnessB_{side}", (side * 0.130,  0.204, 1.06), (0.050, 0.032, 0.42), MAT_LEATHER_BRN, bevel_width=0.008)
        buckle  = kit.create_rounded_box(f"HarnessBuckle_{side}", (side * 0.138, -0.218, 1.06), (0.066, 0.024, 0.058), MAT_METAL_SILV, rot_euler=(0.0, side * 0.12, 0.0), bevel_width=0.006)
        body_parts.extend([h_front, h_back, buckle])
    # Clean tactical back panel (no katana!)
    back_pad = kit.create_rounded_box("Back_TacticalPad", (0.0, 0.202, 1.08), (0.26, 0.045, 0.26), MAT_CLOTH_MID, bevel_width=0.014)
    body_parts.append(back_pad)

    # Arms: Dark Shinobi Sleeves + Khaki Arm-Wraps + Layered Crimson Samurai Forearm Vambraces + Fingerless Gloves!
    AZ = 1.23
    for side in (1.0, -1.0):
        sjoint = kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.228, 0.0, AZ), (0.135, 0.128, 0.128), MAT_JACKET_DARK)
        sleeve_rings = [
            (Vector((side * 0.20, 0.0, AZ)), Vector((0.0, 0.122, 0.0)), Vector((0.0, 0.0, 0.122 * side))),
            (Vector((side * 0.38, 0.0, AZ)), Vector((0.0, 0.112, 0.0)), Vector((0.0, 0.0, 0.112 * side))),
            (Vector((side * 0.56, 0.0, AZ)), Vector((0.0, 0.104, 0.0)), Vector((0.0, 0.0, 0.104 * side))),
            (Vector((side * 0.68, 0.0, AZ)), Vector((0.0, 0.098, 0.0)), Vector((0.0, 0.0, 0.098 * side))),
        ]
        body_parts.extend([sjoint, kit.create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_CLOTH_MID)])

        # Twin khaki upper-arm cloth wraps (exact portrait detail!)
        for bx in (0.35, 0.44):
            band = kit.create_rounded_box(f"ArmBand_{side}_{bx}", (side * bx, 0.0, AZ), (0.042, 0.232, 0.230), MAT_CLOTH_KHAKI, bevel_width=0.010)
            body_parts.append(band)

        # Layered Crimson Samurai Forearm Vambrace Plates (3 stacked plates on outer/top forearm!)
        for idx_p, px in enumerate((0.52, 0.585, 0.65)):
            plate = kit.create_rounded_box(
                f"RedVambrace_{side}_{idx_p}", (side * px, -0.012, AZ + 0.056),
                (0.062, 0.186, 0.072), MAT_ARMOR_RED,
                rot_euler=(0.0, side * 0.08, 0.0), bevel_width=0.012
            )
            trim = kit.create_rounded_box(
                f"RedVambraceStrap_{side}_{idx_p}", (side * px, 0.0, AZ - 0.052),
                (0.036, 0.205, 0.042), MAT_JACKET_DARK, bevel_width=0.008
            )
            body_parts.extend([plate, trim])

        # Black fingerless combat gloves with exposed skin fingers
        glove = kit.create_rounded_box(f"Glove_{side}", (side * 0.76, -0.004, AZ), (0.135, 0.174, 0.136), MAT_JACKET_DARK, bevel_width=0.022)
        glove_red = kit.create_rounded_box(f"GloveRedTrim_{side}", (side * 0.72, -0.004, AZ + 0.052), (0.048, 0.165, 0.045), MAT_ARMOR_RED, bevel_width=0.010)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.855, -0.004, AZ - 0.006), (0.066, 0.076, 0.058), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.77, -0.092, AZ), (0.040, 0.042, 0.034), MAT_SKIN)
        body_parts.extend([glove, glove_red, fingers, thumb])

    # Tied Khaki Waist Sash + Leather Belt + Buckles + Side Pouches + Hanging Front Sash Ribbons!
    sash_rings = [
        (Vector((0.0, 0.002, 0.74)), Vector((0.258, 0.0, 0.0)), Vector((0.0, 0.202, 0.0))),
        (Vector((0.0, 0.000, 0.85)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.196, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Waist_Sash", sash_rings, MAT_CLOTH_KHAKI))
    sash_knot = kit.create_ellipsoid("Sash_Knot", (0.04, -0.215, 0.79), (0.072, 0.052, 0.065), MAT_CLOTH_KHAKI)
    belt_buckle = kit.create_rounded_box("Waist_Buckle", (-0.08, -0.212, 0.77), (0.068, 0.032, 0.056), MAT_METAL_SILV, bevel_width=0.007)
    side_pouch = kit.create_rounded_box("Hip_Pouch", (-0.235, -0.08, 0.81), (0.092, 0.145, 0.135), MAT_POUCH_TAN, bevel_width=0.014)
    body_parts.extend([sash_knot, belt_buckle, side_pouch])

    # Hanging front khaki sash tails (flared diagonally in front plane!)
    sash_tail_l = kit.create_rounded_box("SashTailL", (0.09, -0.215, 0.65), (0.072, 0.026, 0.24), MAT_CLOTH_KHAKI, rot_euler=(0.14, -0.24, 0.0), bevel_width=0.008)
    sash_tail_r = kit.create_rounded_box("SashTailR", (-0.01, -0.218, 0.62), (0.068, 0.026, 0.26), MAT_CLOTH_KHAKI, rot_euler=(0.12,  0.16, 0.0), bevel_width=0.008)
    body_parts.extend([sash_tail_l, sash_tail_r])

    # Pelvis + Shinobi Cargo Pants + Layered Crimson Thigh/Knee Armor Plates + Dark Combat Boots
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.172, 0.0, 0.0)), Vector((0.0, 0.146, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.196, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.238, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_JACKET_DARK))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.148,  0.004, 0.68)), Vector((0.126, 0.0, 0.0)), Vector((0.0, -0.130, 0.0))),
            (Vector((side * 0.150,  0.004, 0.54)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.136, 0.0))),
            (Vector((side * 0.152,  0.000, 0.42)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
            (Vector((side * 0.152, -0.002, 0.30)), Vector((0.112, 0.0, 0.0)), Vector((0.0, -0.116, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.120, 0.0, 0.0)), Vector((0.0, -0.124, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_JACKET_DARK))

        # Layered Crimson Samurai Thigh/Knee Armor Plates (signature red plates from portrait!)
        for idx_l, lz in enumerate((0.52, 0.44, 0.36)):
            tplate = kit.create_rounded_box(
                f"RedThighArmor_{side}_{idx_l}", (side * 0.152, -0.116, lz),
                (0.148, 0.052, 0.068), MAT_ARMOR_RED, bevel_width=0.012
            )
            body_parts.append(tplate)

        # Khaki shin wrap + dark ninja combat boots
        shin_wrap = [
            (Vector((side * 0.152, 0.000, 0.21)), Vector((0.124, 0.0, 0.0)), Vector((0.0, -0.128, 0.0))),
            (Vector((side * 0.152, 0.000, 0.28)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"ShinWrap_{side}", shin_wrap, MAT_CLOTH_KHAKI))

        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.210)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
            (Vector((side * 0.152, -0.054, 0.080)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.182, 0.0))),
            (Vector((side * 0.152, -0.064, 0.038)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_JACKET_DARK))
        boot_sole = [
            (Vector((side * 0.152, -0.064, 0.044)), Vector((0.144, 0.0, 0.0)), Vector((0.0, -0.200, 0.0))),
            (Vector((side * 0.152, -0.064, 0.003)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_CLOTH_MID))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_white_ronin_head()
body_obj = build_white_ronin_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_WhiteRonin")

import sys
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import bpy
import bmesh
import math
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler
import ChibiAnimeKit as kit

scene = kit.reset_scene()

ZOMBIE_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels\Zombies"
ZOMBIE_TEX_DIR = os.path.join(ZOMBIE_DIR, "Textures")
os.makedirs(ZOMBIE_TEX_DIR, exist_ok=True)

FACE_TEX_PATH = os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieBoomer_Face.png")
HAIR_TEX_PATH = os.path.join(ZOMBIE_TEX_DIR, "Chibi_ZombieBoomer_Hair.png")


# ==============================================================================
# 1. 1024x1024 UNDEAD FAT BOOMER FACE TEXTURE (Inspired by Reference #2, Zombified!)
# ==============================================================================
def generate_boomer_face_texture():
    W, H = 1024, 1024
    aa = 0.0022
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    # Cohesive undead sage-green zombie skin matching Chibi_ZombieNormal & Boss!
    SKIN_BASE   = (0.54, 0.66, 0.51)
    SKIN_SHADOW = (0.34, 0.44, 0.33)
    BRUISE_COL  = (0.32, 0.26, 0.38)
    STUBBLE_COL = (0.14, 0.14, 0.16)
    BILE_COL    = (0.86, 0.94, 0.18)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE

    # Undead shading around forehead & heavy sagging jowls
    kit.alpha_blend(canvas, kit.smoothstep(0.66, 0.90, V) * 0.32, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.06, V) * 0.30, BRUISE_COL)

    # Patchy scruffy chin beard / stubble along the heavy jaw (exact detail from Ref #2!)
    for bx, by, rx, ry in (
        (0.500, 0.185, 0.115, 0.036),
        (0.415, 0.215, 0.048, 0.028),
        (0.585, 0.215, 0.048, 0.028),
        (0.455, 0.165, 0.032, 0.022),
        (0.545, 0.165, 0.032, 0.022),
    ):
        d_st = np.hypot((U - bx) / rx, (V - by) / ry)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.4, 1.0, d_st)) * 0.72, STUBBLE_COL)

    # Bruised dark sunken eye sockets
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.155
        cy = 0.485
        d_sock = np.hypot((U - cx) / 0.105, (V - (cy - 0.012)) / 0.070)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.25, 1.0, d_sock)) * 0.52, BRUISE_COL)

        ex = (U - cx) * side
        ey = V - cy
        t_x = np.clip((ex + 0.074) / 0.156, 0.0, 1.0)
        # Furious crazed narrowed undead eyes
        y_top = -0.020 + 0.048 * np.sin(np.pi * np.power(t_x, 0.72)) + 0.034 * t_x
        y_bot = -0.028 - 0.026 * np.sin(np.pi * np.power(t_x, 0.85)) + 0.022 * t_x
        in_x = kit.smoothstep(-0.076, -0.068, ex) * (1.0 - kit.smoothstep(0.078, 0.084, ex))
        eye_mask = in_x * kit.smoothstep(-aa, aa, ey - y_bot) * kit.smoothstep(-aa, aa, y_top - ey)

        # Bloodshot yellowed undead sclera
        kit.alpha_blend(canvas, eye_mask, (0.94, 0.90, 0.68))

        # Glowing feral crimson-red & toxic-amber iris!
        r_iris = np.hypot(ex / 0.044, ey / 0.044)
        iris_mask = eye_mask * (1.0 - kit.smoothstep(0.92, 1.06, r_iris))
        kit.alpha_blend(canvas, iris_mask, (0.94, 0.16, 0.10))
        r_core = np.hypot(ex / 0.026, ey / 0.026)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.75, 1.04, r_core)), (1.00, 0.78, 0.14))
        r_pup = np.hypot(ex / 0.012, ey / 0.012)
        kit.alpha_blend(canvas, iris_mask * (1.0 - kit.smoothstep(0.80, 1.05, r_pup)), (0.08, 0.02, 0.02))

        # Thick black eye rim + furious furrowed dark eyebrows (exact Ref #2 brow angle!)
        u_lash = [(cx + side * (-0.078), cy - 0.020), (cx + side * (-0.020), cy + 0.024), (cx + side * 0.046, cy + 0.046), (cx + side * 0.088, cy + 0.038)]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, u_lash, [0.006, 0.010, 0.011, 0.005])), (0.06, 0.05, 0.08))

        brow = [(cx + side * (-0.095), cy + 0.026), (cx + side * (-0.022), cy + 0.064), (cx + side * 0.052, cy + 0.094), (cx + side * 0.106, cy + 0.102)]
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, brow, [0.011, 0.017, 0.016, 0.006])), (0.12, 0.11, 0.13))

    # Broad chunky undead nose
    nose = [(0.486, 0.435), (0.476, 0.384), (0.524, 0.384), (0.514, 0.435)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, kit.polyline_sdf(U, V, nose, [0.008, 0.014, 0.014, 0.008]))) * 0.48, SKIN_SHADOW)

    # ICONIC HUGE CRAZED TOOTHY ZOMBIE GRIN (Direct Ref #2 wide grin turned undead!)
    mx = (U - 0.500) / 0.165
    my = V - 0.305
    y_m_top = 0.016 + 0.028 * (mx ** 2)
    y_m_bot = -0.056 * np.clip(1.0 - mx ** 2, 0.0, 1.0) + 0.026 * (mx ** 2)
    in_mx = kit.smoothstep(-1.04, -0.94, mx) * (1.0 - kit.smoothstep(0.94, 1.04, mx))
    mouth_mask = in_mx * kit.smoothstep(-aa, aa, my - y_m_bot) * kit.smoothstep(-aa, aa, y_m_top - my)

    # Dark necrotic mouth outline + bright jagged yellowed teeth!
    kit.alpha_blend(canvas, mouth_mask, (0.92, 0.90, 0.74))
    # Teeth vertical dividers & slight jagged bite line
    bite_line = kit.polyline_sdf(U, V, [(0.34, 0.312), (0.42, 0.296), (0.50, 0.292), (0.58, 0.296), (0.66, 0.312)], [0.004, 0.005, 0.005, 0.005, 0.004])
    kit.alpha_blend(canvas, mouth_mask * (1.0 - kit.smoothstep(-aa, aa, bite_line)), (0.14, 0.06, 0.08))
    for tx in (0.39, 0.445, 0.50, 0.555, 0.61):
        d_tv = kit.polyline_sdf(U, V, [(tx, 0.250), (tx, 0.345)], [0.0032, 0.0032])
        kit.alpha_blend(canvas, mouth_mask * (1.0 - kit.smoothstep(-aa, aa, d_tv)), (0.16, 0.08, 0.10))

    # Crisp dark outer mouth border
    top_border = [(0.335, 0.345), (0.415, 0.326), (0.500, 0.320), (0.585, 0.326), (0.665, 0.345)]
    bot_border = [(0.335, 0.340), (0.415, 0.278), (0.500, 0.252), (0.585, 0.278), (0.665, 0.340)]
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, top_border, [0.005, 0.006, 0.006, 0.006, 0.005])), (0.10, 0.06, 0.08))
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, bot_border, [0.005, 0.006, 0.007, 0.006, 0.005])), (0.10, 0.06, 0.08))

    # Glowing toxic bile drool trickling from right corner of grin!
    drool = [(0.625, 0.305), (0.634, 0.250), (0.630, 0.195)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, drool, [0.010, 0.008, 0.011]))) * 0.90, BILE_COL)

    # Undead stitched scar on forehead & cheek
    scar = [(0.355, 0.610), (0.425, 0.560)]
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa, aa, kit.polyline_sdf(U, V, scar, [0.006, 0.006]))) * 0.80, (0.38, 0.14, 0.18))

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_ZombieBoomer_Face.png")


IMG_FACE = generate_boomer_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_ZombieBoomer_Hair.png",
    root_rgb=(0.10, 0.10, 0.12),
    mid_rgb=(0.18, 0.17, 0.20),
    tip_rgb=(0.24, 0.22, 0.25),
    hi_rgb=(0.38, 0.36, 0.42),
    band_v=0.48, band_strength=0.25
)

# ==============================================================================
# 2. MATERIALS (MATTE UNDEAD SKIN + GLOWING TOXIC EXPLOSIVE BOILS!)
# ==============================================================================
MAT_FACE_TEX    = kit.make_mat("Mat_Boomer_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Zombie_Skin",         (0.54, 0.66, 0.51, 1.0), roughness=0.90, specular=0.03)
MAT_SKIN_BRUISE = kit.make_mat("Mat_Zombie_Bruise",       (0.35, 0.30, 0.42, 1.0), roughness=0.90, specular=0.04)
MAT_SKIN_WOUND  = kit.make_mat("Mat_Zombie_Wound",        (0.46, 0.14, 0.18, 1.0), roughness=0.82, specular=0.08)
MAT_HAIR_DARK   = kit.make_mat("Mat_Boomer_Hair",         (1.0, 1.0, 1.0, 1.0), roughness=0.88, specular=0.04, image=IMG_HAIR)
MAT_HAIR_REDTIP = kit.make_mat("Mat_Hair_RedTip",         (0.84, 0.16, 0.14, 1.0), roughness=0.86, specular=0.05)
MAT_HEADBAND    = kit.make_mat("Mat_Dark_Strap",          (0.14, 0.14, 0.17, 1.0), roughness=0.90, specular=0.04)
MAT_BELT_TAN    = kit.make_mat("Mat_Belt_Tan",            (0.62, 0.51, 0.38, 1.0), roughness=0.90, specular=0.04)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver",        (0.76, 0.78, 0.84, 1.0), roughness=0.45, metallic=0.65, specular=0.25)
MAT_PANTS_NAVY  = kit.make_mat("Mat_Pants_Navy",          (0.16, 0.18, 0.38, 1.0), roughness=0.92, specular=0.03)
MAT_KNEE_GREY   = kit.make_mat("Mat_Armor_Grey",          (0.56, 0.58, 0.66, 1.0), roughness=0.72, metallic=0.12, specular=0.10)
MAT_WRIST_BRN   = kit.make_mat("Mat_Wrist_Leather",       (0.32, 0.22, 0.22, 1.0), roughness=0.88, specular=0.05)
MAT_BOOT_BLACK  = kit.make_mat("Mat_Boot_Black",          (0.12, 0.12, 0.15, 1.0), roughness=0.90, specular=0.04)

# Glowing Volatile Toxic Pustules (signals "EXPLOSIVE BOOMER" from top-down!)
MAT_BOIL_GLOW = kit.make_mat("Mat_Boomer_Boil_Glow", (0.98, 0.86, 0.14, 1.0), roughness=0.22, specular=0.55)
bsdf_boil = MAT_BOIL_GLOW.node_tree.nodes.get("Principled BSDF")
if bsdf_boil and "Emission Color" in bsdf_boil.inputs:
    bsdf_boil.inputs["Emission Color"].default_value = (1.0, 0.72, 0.05, 1.0)
    bsdf_boil.inputs["Emission Strength"].default_value = 2.4


# ==============================================================================
# 3. BUILD FAT BOOMER ZOMBIE HEAD (Image #2 Spiky Dreadlocks w/ Red Tips!)
# ==============================================================================
def build_boomer_head():
    head_parts = []
    HZ = 1.72

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="male_strong")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        head_parts.append(kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.372, 0.015, HZ - 0.025), (0.048, 0.036, 0.076),
            MAT_SKIN, rot_euler=(0.06, side * -0.18, side * 0.14)
        ))

    # Dark Hair Crown Base
    head_parts.append(kit.create_hair_crown_shell(HZ, MAT_HAIR_DARK, rx_base=0.400, ry_base=0.382, rz_base=0.375, cutout_z=0.48))

    # Dark Headband / Hairline Strap wrapping upper forehead crown (keeps furrowed brows 100% visible!)
    head_parts.append(kit.create_torus_ring(
        "Boomer_Headband", (0.0, 0.015, HZ + 0.215), major_r=0.352, minor_r=0.028,
        mat=MAT_HEADBAND, rot_euler=(0.30, 0.0, 0.0)
    ))

    # Swept-back wild spiky dreadlocks + signature CRIMSON-RED tips/beads (exact Ref #2!)
    dread_specs = [
        # Top swept-back spikes
        ( 0.00, -0.22, 0.32,  0.00,  0.18, 0.44,  0.00,  0.54, 0.32, 0.105, 0.042,  0.00, True),
        (-0.14, -0.20, 0.30, -0.18,  0.18, 0.40, -0.22,  0.52, 0.28, 0.098, 0.038,  0.22, True),
        ( 0.14, -0.20, 0.30,  0.18,  0.18, 0.40,  0.22,  0.52, 0.28, 0.098, 0.038, -0.22, True),
        (-0.24, -0.12, 0.24, -0.32,  0.20, 0.30, -0.38,  0.50, 0.16, 0.092, 0.036,  0.42, False),
        ( 0.24, -0.12, 0.24,  0.32,  0.20, 0.30,  0.38,  0.50, 0.16, 0.092, 0.036, -0.42, False),
        # Lower swept-back side dread spikes with red tips!
        (-0.28, -0.02, 0.12, -0.38,  0.22, 0.12, -0.44,  0.48, 0.00, 0.088, 0.034,  0.65, True),
        ( 0.28, -0.02, 0.12,  0.38,  0.22, 0.12,  0.44,  0.48, 0.00, 0.088, 0.034, -0.65, True),
        (-0.16,  0.10, 0.16, -0.22,  0.36, 0.10, -0.26,  0.56, -0.08, 0.090, 0.034, 0.30, False),
        ( 0.16,  0.10, 0.16,  0.22,  0.36, 0.10,  0.26,  0.56, -0.08, 0.090, 0.034, -0.30, False),
        ( 0.00,  0.12, 0.18,  0.00,  0.38, 0.12,  0.00,  0.58, -0.06, 0.095, 0.036, 0.00, True),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, x2, y2, z2, w, t, roll, has_red_tip) in enumerate(dread_specs):
        p0 = Vector((x0, y0, HZ + z0))
        p1 = Vector((x1, y1, HZ + z1))
        p2 = Vector((x2, y2, HZ + z2))
        strand = [
            (p0, w * 0.90, t * 0.90, roll),
            (p1, w * 0.95, t * 0.95, roll),
            (p2, w * 0.50, t * 0.50, roll),
            (p2 + (p2 - p1).normalized() * 0.12, 0.010, 0.006, roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"Dread_{idx}", strand, MAT_HAIR_DARK, v_start=0.06, v_end=0.95))
        if has_red_tip:
            tip_dir = (p2 - p1).normalized()
            red_tip = [
                (p2 - tip_dir * 0.03, w * 0.52, t * 0.52, roll),
                (p2 + tip_dir * 0.07, w * 0.36, t * 0.36, roll),
                (p2 + tip_dir * 0.16, 0.008, 0.005, roll),
            ]
            head_parts.append(kit.create_ribbon_hair_strand(f"DreadRedTip_{idx}", red_tip, MAT_HAIR_REDTIP, v_start=0.2, v_end=0.9))

    # Short Framing Temple Dreadlocks hanging beside cheeks with crimson-red tip beads (exact Ref #2!)
    for side in (-1.0, 1.0):
        side_dread = [
            (Vector((side * 0.28, -0.14, HZ + 0.18)), 0.068, 0.028, side * -0.82),
            (Vector((side * 0.36, -0.18, HZ + 0.00)), 0.064, 0.026, side * -0.90),
            (Vector((side * 0.36, -0.17, HZ - 0.16)), 0.048, 0.020, side * -0.95),
            (Vector((side * 0.32, -0.15, HZ - 0.25)), 0.012, 0.008, side * -0.95),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"SideDread_{side}", side_dread, MAT_HAIR_DARK, v_start=0.06, v_end=0.90))
        head_parts.append(kit.create_ellipsoid(
            f"SideDreadRedBead_{side}", (side * 0.34, -0.16, HZ - 0.21), (0.042, 0.042, 0.058),
            MAT_HAIR_REDTIP, rot_euler=(0.15, side * -0.25, 0.0)
        ))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


# ==============================================================================
# 4. BUILD SHIRTLESS FAT BLOATED BOOMER ZOMBIE BODY (Ref #2 Silhouette, Shirtless!)
# ==============================================================================
def build_boomer_body():
    body_parts = []

    # Thick Bull-Neck
    neck_rings = [
        (Vector((0.0, 0.00, 1.28)), Vector((0.185, 0.0, 0.0)), Vector((0.0, 0.165, 0.0))),
        (Vector((0.0, 0.00, 1.54)), Vector((0.140, 0.0, 0.0)), Vector((0.0, 0.130, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Massive Shirtless Hulking Upper Torso + Huge Bloated Explosive Belly!
    torso_rings = [
        (Vector((0.0, -0.02, 0.74)), Vector((0.365, 0.0, 0.0)), Vector((0.0, 0.300, 0.0))),
        (Vector((0.0, -0.09, 0.88)), Vector((0.430, 0.0, 0.0)), Vector((0.0, 0.380, 0.0))),
        (Vector((0.0, -0.10, 1.04)), Vector((0.435, 0.0, 0.0)), Vector((0.0, 0.375, 0.0))),
        (Vector((0.0, -0.04, 1.20)), Vector((0.390, 0.0, 0.0)), Vector((0.0, 0.295, 0.0))),
        (Vector((0.0,  0.00, 1.35)), Vector((0.285, 0.0, 0.0)), Vector((0.0, 0.220, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_FatBare", torso_rings, MAT_SKIN))

    # Dedicated Overhanging Explosive Beer-Belly Mound (Driven directly by the 'Belly' bone so it swells & bursts!)
    belly_mound = kit.create_ellipsoid(
        "Boomer_BellyMound", (0.0, -0.255, 0.965), (0.375, 0.285, 0.285),
        MAT_SKIN, u_seg=36, v_seg=26
    )
    body_parts.append(belly_mound)

    # Shirtless Chest Pecs + Necrotic Belly Bruise/Scar Details
    for side in (-1.0, 1.0):
        pec = kit.create_ellipsoid(f"BarePec_{side}", (side * 0.165, -0.255, 1.195), (0.155, 0.105, 0.095), MAT_SKIN)
        body_parts.append(pec)
    belly_wound = kit.create_rounded_box("Belly_Wound", (-0.11, -0.505, 1.02), (0.14, 0.035, 0.036), MAT_SKIN_WOUND, rot_euler=(0.0, 0.32, 0.22), bevel_width=0.010)
    belly_navel = kit.create_ellipsoid("Belly_Navel", (0.0, -0.525, 0.91), (0.032, 0.020, 0.028), MAT_SKIN_BRUISE)
    body_parts.extend([belly_wound, belly_navel])

    # GLOWING TOXIC EXPLOSIVE PUSTULES / BIO-BOILS across the bloated shirtless gut, chest & back!
    boil_specs = [
        # Front bloated explosive belly cluster (inflates dramatically with Belly bone!)
        ( 0.00, -0.515, 0.995, 0.095, 0.072, 0.090),
        ( 0.15, -0.475, 0.945, 0.076, 0.060, 0.072),
        (-0.16, -0.460, 0.910, 0.072, 0.056, 0.068),
        ( 0.08, -0.490, 0.845, 0.058, 0.048, 0.055),
        (-0.08, -0.465, 1.095, 0.054, 0.046, 0.052),
        ( 0.24, -0.365, 1.025, 0.064, 0.052, 0.060),
        (-0.25, -0.350, 0.985, 0.058, 0.048, 0.055),
        # Upper chest & shoulder explosive boils
        ( 0.22, -0.250, 1.245, 0.060, 0.050, 0.056),
        (-0.24, -0.180, 1.325, 0.056, 0.048, 0.052),
        # Back hunched explosive boils (super readable from top-down camera!)
        ( 0.00,  0.305, 1.150, 0.095, 0.070, 0.090),
        ( 0.18,  0.265, 1.060, 0.078, 0.060, 0.074),
        (-0.17,  0.270, 1.090, 0.072, 0.056, 0.068),
        ( 0.06,  0.285, 0.960, 0.062, 0.050, 0.060),
    ]
    for idx, (bx, by, bz, rx, ry, rz) in enumerate(boil_specs):
        # Dark bruised crater base + bright glowing toxic boil sphere
        crater = kit.create_ellipsoid(f"BoilCrater_{idx}", (bx * 0.96, by * 0.95, bz), (rx * 1.22, ry * 0.92, rz * 1.22), MAT_SKIN_BRUISE, u_seg=18, v_seg=14)
        boil   = kit.create_ellipsoid(f"BoilGlow_{idx}",   (bx, by, bz), (rx, ry, rz), MAT_BOIL_GLOW, u_seg=18, v_seg=14)
        body_parts.extend([crater, boil])

    # Massive Bare Muscular-Fat Zombie Arms + Dark Leather Wrist Bracers (Exact Ref #2!)
    AZ = 1.24
    for side in (1.0, -1.0):
        body_parts.append(kit.create_ellipsoid(f"Shoulder_{side}", (side * 0.34, -0.01, AZ), (0.175, 0.165, 0.165), MAT_SKIN))
        arm_rings = [
            (Vector((side * 0.30, 0.0, AZ)), Vector((0.0, 0.155, 0.0)), Vector((0.0, 0.0, 0.155 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.145, 0.0)), Vector((0.0, 0.0, 0.145 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.132, 0.0)), Vector((0.0, 0.0, 0.132 * side))),
            (Vector((side * 0.78, 0.0, AZ)), Vector((0.0, 0.118, 0.0)), Vector((0.0, 0.0, 0.118 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"BareArm_{side}", arm_rings, MAT_SKIN))

        # Thick Dark Leather Wrist Bracers with Grey Trim (exact Ref #2 detail!)
        bracer_rings = [
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.142, 0.0)), Vector((0.0, 0.0, 0.142 * side))),
            (Vector((side * 0.78, 0.0, AZ)), Vector((0.0, 0.134, 0.0)), Vector((0.0, 0.0, 0.134 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"WristBracer_{side}", bracer_rings, MAT_WRIST_BRN))
        bracer_trim = kit.create_loft_object(f"WristTrim_{side}", [
            (Vector((side * 0.64, 0.0, AZ)), Vector((0.0, 0.148, 0.0)), Vector((0.0, 0.0, 0.148 * side))),
            (Vector((side * 0.68, 0.0, AZ)), Vector((0.0, 0.148, 0.0)), Vector((0.0, 0.0, 0.148 * side))),
        ], MAT_KNEE_GREY)
        body_parts.append(bracer_trim)

        # Big Hulking Bare Zombie Fists / Clawed Hands
        hand = kit.create_rounded_box(f"ZombieHand_{side}", (side * 0.87, -0.01, AZ), (0.175, 0.195, 0.155), MAT_SKIN, bevel_width=0.036)
        thumb = kit.create_ellipsoid(f"ZombieThumb_{side}", (side * 0.85, -0.115, AZ), (0.052, 0.056, 0.044), MAT_SKIN)
        body_parts.extend([hand, thumb])

    # Thick Tan Leather Belt + Chunky Silver Buckle right beneath the huge overhanging belly (Exact Ref #2!)
    belt_rings = [
        (Vector((0.0, -0.02, 0.67)), Vector((0.378, 0.0, 0.0)), Vector((0.0, 0.305, 0.0))),
        (Vector((0.0, -0.02, 0.77)), Vector((0.386, 0.0, 0.0)), Vector((0.0, 0.312, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Boomer_Belt", belt_rings, MAT_BELT_TAN))
    body_parts.append(kit.create_rounded_box("Belt_Buckle", (0.0, -0.345, 0.72), (0.165, 0.065, 0.125), MAT_METAL_SILV, bevel_width=0.016))
    body_parts.append(kit.create_rounded_box("Belt_BuckleIn", (0.0, -0.368, 0.72), (0.075, 0.035, 0.075), MAT_HEADBAND, bevel_width=0.008))

    # Dark Navy-Indigo Baggy Biker Pants + Big Round Grey Armored Knee Pads + Heavy Biker Boots (Exact Ref #2!)
    pelvis_rings = [
        (Vector((0.0, -0.01, 0.50)), Vector((0.265, 0.0, 0.0)), Vector((0.0, 0.220, 0.0))),
        (Vector((0.0, -0.01, 0.66)), Vector((0.368, 0.0, 0.0)), Vector((0.0, 0.295, 0.0))),
        (Vector((0.0, -0.01, 0.74)), Vector((0.360, 0.0, 0.0)), Vector((0.0, 0.285, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_PANTS_NAVY))

    for side in (1.0, -1.0):
        # Wide sturdy stance legs (x = +-0.205 matching Ref #2!)
        leg_rings = [
            (Vector((side * 0.200,  0.00, 0.66)), Vector((0.165, 0.0, 0.0)), Vector((0.0, -0.175, 0.0))),
            (Vector((side * 0.210,  0.00, 0.50)), Vector((0.162, 0.0, 0.0)), Vector((0.0, -0.170, 0.0))),
            (Vector((side * 0.215,  0.00, 0.36)), Vector((0.150, 0.0, 0.0)), Vector((0.0, -0.158, 0.0))),
            (Vector((side * 0.220,  0.00, 0.21)), Vector((0.152, 0.0, 0.0)), Vector((0.0, -0.160, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_rings, MAT_PANTS_NAVY))

        # Big Round Metallic-Grey Knee Pads + Grey Straps (exact Ref #2 detail!)
        kstrap = kit.create_loft_object(f"KneeStrap_{side}", [
            (Vector((side * 0.215, 0.0, 0.35)), Vector((0.158, 0.0, 0.0)), Vector((0.0, -0.165, 0.0))),
            (Vector((side * 0.215, 0.0, 0.42)), Vector((0.158, 0.0, 0.0)), Vector((0.0, -0.165, 0.0))),
        ], MAT_KNEE_GREY)
        kpad = kit.create_ellipsoid(f"KneePad_{side}", (side * 0.215, -0.155, 0.385), (0.112, 0.052, 0.105), MAT_KNEE_GREY)
        body_parts.extend([kstrap, kpad])

        # Heavy Wide Biker Boots
        boot_upper = [
            (Vector((side * 0.220,  0.00, 0.21)), Vector((0.142, 0.0, 0.0)), Vector((0.0, -0.152, 0.0))),
            (Vector((side * 0.220, -0.02, 0.13)), Vector((0.148, 0.0, 0.0)), Vector((0.0, -0.172, 0.0))),
            (Vector((side * 0.220, -0.06, 0.07)), Vector((0.160, 0.0, 0.0)), Vector((0.0, -0.225, 0.0))),
            (Vector((side * 0.220, -0.07, 0.03)), Vector((0.162, 0.0, 0.0)), Vector((0.0, -0.235, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_BOOT_BLACK))
        boot_sole = [
            (Vector((side * 0.220, -0.07, 0.04)), Vector((0.170, 0.0, 0.0)), Vector((0.0, -0.242, 0.0))),
            (Vector((side * 0.220, -0.07, 0.003)), Vector((0.166, 0.0, 0.0)), Vector((0.0, -0.238, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_KNEE_GREY))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_boomer_head()
body_obj = build_boomer_body()

# ==============================================================================
# 5. CUSTOM 19-BONE BOOMER ARMATURE (INCLUDES DEDICATED 'Belly' INFLATION BONE!)
# ==============================================================================
arm_data = bpy.data.armatures.new("Chibi_Boomer_Rig")
rig_obj = bpy.data.objects.new("Chibi_Boomer_Rig", arm_data)
bpy.context.collection.objects.link(rig_obj)
bpy.context.view_layer.objects.active = rig_obj
bpy.ops.object.mode_set(mode='EDIT')

bone_defs = [
    ("Root",          (0.0, 0.0, 0.02),     (0.0, 0.0, 0.66),     None,            (0, -1, 0)),
    ("Hips",          (0.0, 0.0, 0.66),     (0.0, 0.0, 0.86),     "Root",          (0, -1, 0)),
    ("Spine",         (0.0, 0.0, 0.86),     (0.0, 0.0, 1.08),     "Hips",          (0, -1, 0)),
    ("Belly",         (0.0, -0.10, 0.96),   (0.0, -0.48, 0.94),   "Spine",         (0, 0, 1)),
    ("Chest",         (0.0, 0.0, 1.08),     (0.0, 0.0, 1.32),     "Spine",         (0, -1, 0)),
    ("Neck",          (0.0, 0.0, 1.32),     (0.0, 0.0, 1.52),     "Chest",         (0, -1, 0)),
    ("Head",          (0.0, 0.0, 1.52),     (0.0, 0.0, 2.10),     "Neck",          (0, -1, 0)),

    ("LeftShoulder",  (0.22, 0.0, 1.24),    (0.36, 0.0, 1.24),    "Chest",         (0, 0, 1)),
    ("LeftUpperArm",  (0.36, 0.0, 1.24),    (0.58, 0.0, 1.24),    "LeftShoulder",  (0, 0, 1)),
    ("LeftLowerArm",  (0.58, 0.0, 1.24),    (0.78, 0.0, 1.24),    "LeftUpperArm",  (0, 0, 1)),
    ("LeftHand",      (0.78, 0.0, 1.24),    (0.96, 0.0, 1.24),    "LeftLowerArm",  (0, 0, 1)),

    ("RightShoulder", (-0.22, 0.0, 1.24),   (-0.36, 0.0, 1.24),   "Chest",         (0, 0, 1)),
    ("RightUpperArm", (-0.36, 0.0, 1.24),   (-0.58, 0.0, 1.24),   "RightShoulder", (0, 0, 1)),
    ("RightLowerArm", (-0.58, 0.0, 1.24),   (-0.78, 0.0, 1.24),   "RightUpperArm", (0, 0, 1)),
    ("RightHand",     (-0.78, 0.0, 1.24),   (-0.96, 0.0, 1.24),   "RightLowerArm", (0, 0, 1)),

    ("LeftUpperLeg",  (0.205, 0.0, 0.66),   (0.215, 0.0, 0.37),   "Hips",          (0, 1, 0)),
    ("LeftLowerLeg",  (0.215, 0.0, 0.37),   (0.220, 0.0, 0.11),   "LeftUpperLeg",  (0, 1, 0)),
    ("LeftFoot",      (0.220, 0.0, 0.11),   (0.220, -0.24, 0.03), "LeftLowerLeg",  (0, 0, 1)),

    ("RightUpperLeg", (-0.205, 0.0, 0.66),  (-0.215, 0.0, 0.37),  "Hips",          (0, 1, 0)),
    ("RightLowerLeg", (-0.215, 0.0, 0.37),  (-0.220, 0.0, 0.11),  "RightUpperLeg", (0, 1, 0)),
    ("RightFoot",     (-0.220, 0.0, 0.11),  (-0.220, -0.24, 0.03),"RightLowerLeg", (0, 0, 1)),
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
# 6. SKIN WEIGHT BINDING (WITH SMOOTH BELLY-SWELL WEIGHTING!)
# ==============================================================================
def bind_boomer_mesh(mesh_obj, is_head=False):
    mesh_obj.parent = rig_obj
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = rig_obj
    mod.use_deform_preserve_volume = True

    vgroups = {bname: mesh_obj.vertex_groups.new(name=bname) for bname, _, _, _, _ in bone_defs}
    bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in bone_defs if bname not in ("Root", "Belly")]

    for v in mesh_obj.data.vertices:
        co = v.co
        if is_head:
            is_near_neck = (co.z < 1.48) and (abs(co.x) < 0.24) and (co.y < 0.18) and (co.y > -0.24)
            neck_w = max(0.0, min(0.18, (1.48 - co.z) * 1.1)) if is_near_neck else 0.0
            vgroups["Head"].add([v.index], 1.0 - neck_w, 'REPLACE')
            if neck_w > 0.001:
                vgroups["Neck"].add([v.index], neck_w, 'REPLACE')
            continue

        # Smooth Gaussian Belly inflation weight (zero hard-edge pinch when bloating during Explode!)
        d_belly = math.hypot((co.x / 0.38), ((co.z - 0.96) / 0.25))
        t_fwd = max(0.0, min(1.0, (-co.y - 0.06) / 0.28))
        fwd_smooth = t_fwd * t_fwd * (3.0 - 2.0 * t_fwd)
        belly_w = math.exp(-(d_belly * d_belly) / 0.65) * fwd_smooth * 0.88 if (abs(co.x) < 0.44 and 0.68 < co.z < 1.28) else 0.0

        weights = {}
        for bname, h, t in bones_info:
            if bname.startswith("Left") and co.x < -0.01:
                continue
            if bname.startswith("Right") and co.x > 0.01:
                continue
            if ("Arm" in bname or "Hand" in bname) and (abs(co.x) < 0.30 or co.z < 1.02):
                continue
            if "Shoulder" in bname and (abs(co.x) < 0.22 or co.z < 1.04):
                continue
            if ("Leg" in bname or "Foot" in bname) and (co.z > 0.70 or abs(co.x) > 0.44):
                continue
            if bname in ("Hips", "Spine", "Chest", "Neck", "Head") and (abs(co.x) > 0.46 or co.z < 0.52):
                continue
            if bname == "Head" and co.z < 1.46:
                continue

            d = kit._point_to_segment_dist(co, h, t)
            sigma = 0.145 if ("Arm" in bname or "Shoulder" in bname or "Leg" in bname) else 0.155
            w = math.exp(-(d * d) / (2.0 * (sigma ** 2)))
            if w > 1e-5:
                weights[bname] = w

        if not weights:
            weights["Spine"] = 1.0

        top = sorted(weights.items(), key=lambda item: item[1], reverse=True)[:3]
        total = sum(w for _, w in top)
        rem = 1.0 - belly_w
        if belly_w > 0.01:
            vgroups["Belly"].add([v.index], belly_w, 'REPLACE')
        for bname, w in top:
            vgroups[bname].add([v.index], (w / total) * rem, 'REPLACE')


bind_boomer_mesh(head_obj, is_head=True)
bind_boomer_mesh(body_obj, is_head=False)

# ==============================================================================
# 7. POSE & IK HELPERS + 6 BOOMER ANIMATIONS (IDLE, WALK, RUN, EXPLODE, SLASH1, DEATH)
# ==============================================================================
for pb in rig_obj.pose.bones:
    pb.rotation_mode = 'QUATERNION'
rest_lengths = {b.name: (b.tail_local - b.head_local).length for b in arm_data.bones}


def begin_frame(frame):
    scene.frame_set(frame)
    for pb in rig_obj.pose.bones:
        pb.location = Vector((0, 0, 0))
        pb.rotation_quaternion = Quaternion((1, 0, 0, 0))
        pb.scale = Vector((1, 1, 1))
    bpy.context.view_layer.update()


def rot_deg(bname, rx, ry, rz):
    pb = rig_obj.pose.bones[bname]
    pb.rotation_quaternion = Euler((math.radians(rx), math.radians(ry), math.radians(rz)), 'XYZ').to_quaternion()
    bpy.context.view_layer.update()


def set_scale(bname, sx, sy, sz):
    rig_obj.pose.bones[bname].scale = Vector((sx, sy, sz))
    bpy.context.view_layer.update()


def set_hips(loc_offset, pitch_deg, yaw_deg, roll_deg):
    pb = rig_obj.pose.bones["Hips"]
    pb.location = Vector(loc_offset)
    q_p = Quaternion(Vector((1, 0, 0)), math.radians(pitch_deg))
    q_y = Quaternion(Vector((0, 1, 0)), math.radians(yaw_deg))
    q_r = Quaternion(Vector((0, 0, 1)), math.radians(roll_deg))
    pb.rotation_quaternion = q_y @ q_r @ q_p
    bpy.context.view_layer.update()


def swing_bone(pb, target_pt):
    cur_dir = (pb.tail - pb.head).normalized()
    tgt_dir = (Vector(target_pt) - pb.head).normalized()
    q_s = cur_dir.rotation_difference(tgt_dir)
    m = pb.matrix.copy()
    pb.matrix = Matrix.LocRotScale(m.to_translation(), q_s @ m.to_quaternion(), Vector((1, 1, 1)))
    bpy.context.view_layer.update()


def ik_two(upper_name, lower_name, end_name, target_pos, pole_vec, end_rot=(0, 0, 0)):
    pb_u = rig_obj.pose.bones[upper_name]
    pb_l = rig_obj.pose.bones[lower_name]
    pb_e = rig_obj.pose.bones[end_name]
    root = pb_u.head.copy()
    L1, L2 = rest_lengths[upper_name], rest_lengths[lower_name]
    tgt = Vector(target_pos)
    d_vec = tgt - root
    D = max(abs(L1 - L2) + 0.02, min((L1 + L2) * 0.99, d_vec.length))
    tgt = root + d_vec.normalized() * D
    cos_a = max(-1.0, min(1.0, (L1 * L1 + D * D - L2 * L2) / (2.0 * L1 * D)))
    sin_a = math.sqrt(max(0.0, 1.0 - cos_a * cos_a))
    dir_n = d_vec.normalized()
    pole = Vector(pole_vec).normalized()
    plane = pole - dir_n * pole.dot(dir_n)
    plane = Vector((0, 1, 0)) if plane.length_squared < 1e-6 else plane.normalized()
    mid = root + dir_n * (L1 * cos_a) + plane * (L1 * sin_a)
    swing_bone(pb_u, mid)
    swing_bone(pb_l, tgt)
    if any(abs(a) > 1e-4 for a in end_rot):
        rot_deg(end_name, *end_rot)
    else:
        pb_e.rotation_quaternion = Quaternion((1, 0, 0, 0))
        bpy.context.view_layer.update()


def key_all(frame, prev_q):
    for pb in rig_obj.pose.bones:
        q = pb.rotation_quaternion.copy()
        if pb.name in prev_q and prev_q[pb.name].dot(q) < 0.0:
            q = -q
            pb.rotation_quaternion = q
        prev_q[pb.name] = q.copy()
        pb.keyframe_insert(data_path="location", frame=frame)
        pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
        pb.keyframe_insert(data_path="scale", frame=frame)


def new_action(name):
    act = bpy.data.actions.new(name=name)
    act.use_fake_user = True
    if rig_obj.animation_data is None:
        rig_obj.animation_data_create()
    rig_obj.animation_data.action = act
    if hasattr(rig_obj.animation_data, "action_slot") and hasattr(act, "slots"):
        slot = act.slots[0] if len(act.slots) > 0 else act.slots.new(id_type='OBJECT', name=rig_obj.name)
        rig_obj.animation_data.action_slot = slot
    return act


# 1. Idle (Frames 1..49 — Heavy wheezing bloated breathing!)
act_idle = new_action("Idle")
prev_q = {}
for f in range(1, 50):
    begin_frame(f)
    ang = ((f - 1) / 48.0) * 2.0 * math.pi
    b = math.sin(ang)
    set_hips((0.0, 0.0, 0.015 * b), pitch_deg=6.0 + 2.5 * b, yaw_deg=2.0 * math.sin(ang * 0.5), roll_deg=1.5 * math.cos(ang))
    rot_deg("Spine", 4.0 + 2.0 * b, 0.0, 0.0)
    rot_deg("Chest", 3.0 - 2.5 * b, 0.0, 0.0)
    rot_deg("Head", -5.0 + 3.0 * b, 4.0 * math.sin(ang), 2.5 * math.cos(ang))
    set_scale("Belly", 1.0 + 0.06 * b, 1.0 + 0.08 * b, 1.0 + 0.05 * b)

    ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.50, -0.14, 0.84 + 0.02 * b), (0.65, 0.55, -0.1))
    ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.50, -0.14, 0.84 + 0.02 * b), (-0.65, 0.55, -0.1))
    ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.22, -0.01, 0.11), (0.22, -1.0, 0.1))
    ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.22, -0.01, 0.11), (-0.22, -1.0, 0.1))
    key_all(f, prev_q)

# 2. Walk (Frames 1..25 — Heavy lumbering fat-zombie waddle with outstretched arms & bouncing belly!)
act_walk = new_action("Walk")
prev_q = {}
for f in range(1, 26):
    begin_frame(f)
    ang = ((f - 1) / 24.0) * 2.0 * math.pi
    s, c = math.sin(ang), math.cos(ang)
    bounce = abs(s) * 0.052
    # Heavy side-to-side fat waddle!
    set_hips((0.048 * s, 0.0, bounce - 0.02), pitch_deg=11.0, yaw_deg=-10.0 * s, roll_deg=8.5 * s)
    rot_deg("Spine", 6.0, 6.0 * s, -3.5 * s)
    rot_deg("Chest", 5.0, 10.0 * s, -4.0 * s)
    rot_deg("Head", -9.0, -7.0 * s, 4.0 * c)
    # Bouncing fat belly jiggle on every stomp!
    jiggle = math.sin(ang * 2.0 - 0.5)
    set_scale("Belly", 1.0 + 0.07 * jiggle, 1.0 + 0.09 * jiggle, 1.0 - 0.05 * jiggle)

    # Classic undead arms outstretched forward, swaying clumsily
    ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.44, -0.52 + 0.14 * s, 1.12 + 0.06 * c), (0.65, 0.35, -0.5))
    ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.44, -0.52 - 0.14 * s, 1.12 - 0.06 * c), (-0.65, 0.35, -0.5))

    ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.23, -0.22 * s, 0.11 + max(0.0, c) * 0.14), (0.24, -1.0, 0.25), (18.0 * s, 0, 0))
    ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.23, 0.22 * s, 0.11 + max(0.0, -c) * 0.14), (-0.24, -1.0, 0.25), (-18.0 * s, 0, 0))
    key_all(f, prev_q)

# 3. Run (Frames 1..19 — Frantic heavy charging waddle toward the player!)
act_run = new_action("Run")
prev_q = {}
for f in range(1, 20):
    begin_frame(f)
    ang = ((f - 1) / 18.0) * 2.0 * math.pi
    s, c = math.sin(ang), math.cos(ang)
    bounce = abs(s) * 0.075
    set_hips((0.060 * s, 0.0, bounce - 0.03), pitch_deg=18.0, yaw_deg=-13.0 * s, roll_deg=10.0 * s)
    rot_deg("Spine", 10.0, 8.0 * s, -4.5 * s)
    rot_deg("Chest", 8.0, 12.0 * s, -5.0 * s)
    rot_deg("Head", -16.0, -9.0 * s, 5.0 * c)
    jiggle = math.sin(ang * 2.0 - 0.6)
    set_scale("Belly", 1.0 + 0.11 * jiggle, 1.0 + 0.13 * jiggle, 1.0 - 0.07 * jiggle)

    ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.46, -0.58 + 0.22 * s, 1.18 + 0.10 * c), (0.70, 0.30, -0.45))
    ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.46, -0.58 - 0.22 * s, 1.18 - 0.10 * c), (-0.70, 0.30, -0.45))
    ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.24, -0.30 * s, 0.11 + max(0.0, c) * 0.20), (0.24, -1.0, 0.30), (26.0 * s, 0, 0))
    ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.24, 0.30 * s, 0.11 + max(0.0, -c) * 0.20), (-0.24, -1.0, 0.30), (-26.0 * s, 0, 0))
    key_all(f, prev_q)

# 4. Explode (Frames 1..25 = 1.0s — DRAMATIC SELF-DESTRUCT INFLATE, TREMBLE & BURST!)
act_explode = new_action("Explode")
prev_q = {}
for f in range(1, 26):
    begin_frame(f)
    u = (f - 1) / 24.0
    if f <= 10:
        # Phase 1: Deep gasp / windup — plants wide, flings arms back, head screams upward, belly starts swelling!
        p = (f - 1) / 9.0
        s = p * p * (3.0 - 2.0 * p)
        belly_s = 1.0 + 0.48 * s
        chest_s = 1.0 + 0.16 * s
        shake_x, shake_y, shake_r = 0.0, 0.0, 0.0
        set_hips((0.0, 0.04 * s, -0.07 * s), pitch_deg=-16.0 * s, yaw_deg=0.0, roll_deg=0.0)
        rot_deg("Spine", -14.0 * s, 0.0, 0.0)
        rot_deg("Chest", -18.0 * s, 0.0, 0.0)
        rot_deg("Head", -32.0 * s, 0.0, 0.0)
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.68 + 0.12 * s, 0.10 + 0.18 * s, 1.32 + 0.18 * s), (0.5, 0.8, -0.3))
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.68 - 0.12 * s, 0.10 + 0.18 * s, 1.32 + 0.18 * s), (-0.5, 0.8, -0.3))
    elif f <= 19:
        # Phase 2: CRITICAL PRESSURE — Violent trembling as bloated belly balloons to bursting point!
        p = (f - 10) / 9.0
        belly_s = 1.48 + 0.44 * p + 0.06 * math.sin(f * 3.1)
        chest_s = 1.16 + 0.18 * p
        shake = math.sin(f * 3.8)
        set_hips((0.035 * shake, 0.04, -0.09 + 0.02 * math.cos(f * 3.8)), pitch_deg=-18.0 + 6.0 * p, yaw_deg=7.5 * shake, roll_deg=6.0 * shake)
        rot_deg("Spine", -12.0 + 18.0 * p, 5.0 * shake, 4.0 * shake)
        rot_deg("Chest", -16.0 + 22.0 * p, -6.0 * shake, -4.0 * shake)
        rot_deg("Head", -36.0 + 10.0 * shake, 12.0 * shake, 8.0 * shake)
        # Arms clutch inward/up trembling under internal pressure
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.74 + 0.06 * shake, -0.18, 1.38 + 0.08 * math.cos(f * 3.5)), (0.8, 0.4, -0.2))
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.74 + 0.06 * shake, -0.18, 1.38 - 0.08 * math.cos(f * 3.5)), (-0.8, 0.4, -0.2))
    else:
        # Phase 3: DETONATION BURST! Explosive outward snap as belly hits 2.15x peak!
        p = (f - 19) / 6.0
        belly_s = 1.92 + 0.28 * p
        chest_s = 1.34 + 0.18 * p
        set_hips((0.0, 0.08 * p, 0.04 * p), pitch_deg=-28.0, yaw_deg=0.0, roll_deg=0.0)
        rot_deg("Spine", -22.0, 0.0, 0.0)
        rot_deg("Chest", -26.0, 0.0, 0.0)
        rot_deg("Head", -48.0, 0.0, 0.0)
        ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.94, 0.25, 1.56), (0.4, 0.8, 0.4))
        ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.94, 0.25, 1.56), (-0.4, 0.8, 0.4))

    set_scale("Belly", belly_s, belly_s * 1.08, belly_s)
    set_scale("Chest", chest_s, chest_s, 1.0 + (chest_s - 1.0) * 0.5)

    ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.28, -0.02, 0.11), (0.28, -1.0, 0.2))
    ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.28, -0.02, 0.11), (-0.28, -1.0, 0.2))
    key_all(f, prev_q)

# 5. Slash1 (Frames 1..19 — Heavy double-arm lunge belly-smash)
act_slash1 = new_action("Slash1")
prev_q = {}
for f in range(1, 20):
    begin_frame(f)
    u = (f - 1) / 18.0
    sw = math.sin(u * math.pi)
    set_hips((0.0, -0.14 * sw, -0.05 * sw), pitch_deg=18.0 * sw, yaw_deg=0.0, roll_deg=0.0)
    rot_deg("Spine", 12.0 * sw, 0.0, 0.0)
    rot_deg("Chest", 14.0 * sw, 0.0, 0.0)
    rot_deg("Head", -12.0 * sw, 0.0, 0.0)
    set_scale("Belly", 1.0 + 0.18 * sw, 1.0 + 0.22 * sw, 1.0 + 0.12 * sw)
    ik_two("LeftUpperArm", "LeftLowerArm", "LeftHand", (0.38 - 0.12 * sw, -0.64 * sw - 0.15, 1.05 + 0.25 * sw), (0.7, 0.4, -0.3))
    ik_two("RightUpperArm", "RightLowerArm", "RightHand", (-0.38 + 0.12 * sw, -0.64 * sw - 0.15, 1.05 + 0.25 * sw), (-0.7, 0.4, -0.3))
    ik_two("LeftUpperLeg", "LeftLowerLeg", "LeftFoot", (0.24, -0.06 * sw, 0.11), (0.24, -1.0, 0.2))
    ik_two("RightUpperLeg", "RightLowerLeg", "RightFoot", (-0.24, 0.06 * sw, 0.11), (-0.24, -1.0, 0.2))
    key_all(f, prev_q)

# 6. Death (Frames 1..29 — Heavy backward collapse)
act_death = new_action("Death")
prev_q = {}
for f in range(1, 30):
    begin_frame(f)
    u = (f - 1) / 28.0
    s = u * u * (3.0 - 2.0 * u)
    set_hips((0.0, 0.35 * s, -0.45 * s), pitch_deg=-86.0 * s, yaw_deg=12.0 * s, roll_deg=8.0 * s)
    rot_deg("Spine", -10.0 * s, 0.0, 0.0)
    rot_deg("Chest", -8.0 * s, 0.0, 0.0)
    rot_deg("Head", 18.0 * s, 15.0 * s, 0.0)
    set_scale("Belly", 1.0 - 0.22 * s, 1.0 - 0.30 * s, 1.0 + 0.10 * s)
    rot_deg("LeftUpperArm", 0.0, -35.0 * s, 45.0 * s)
    rot_deg("RightUpperArm", 0.0, 35.0 * s, -45.0 * s)
    rot_deg("LeftUpperLeg", 20.0 * s, 0.0, -10.0 * s)
    rot_deg("RightUpperLeg", 18.0 * s, 0.0, 10.0 * s)
    key_all(f, prev_q)

# Reset active action to Idle frame 1 for FBX export
rig_obj.animation_data.action = act_idle
if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
    rig_obj.animation_data.action_slot = act_idle.slots[0]
scene.frame_set(1)
bpy.context.view_layer.update()

# ==============================================================================
# 8. EXPORT .FBX + .BLEND + 3 STUDIO PREVIEWS (FRONT, BACK, MID-EXPLODE!)
# ==============================================================================
blend_path = os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoomer_Source.blend")
fbx_path = os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoomer_Rigged.fbx")
preview_path = os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoomer_Preview.png")
preview_back_path = os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoomer_Preview_Back.png")
preview_explode_path = os.path.join(ZOMBIE_DIR, "Chibi_ZombieBoomer_Preview_Explode.png")

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
print("SUCCESSFULLY EXPORTED CHIBI ZOMBIE BOOMER FBX:", fbx_path)

# Studio Preview Setup
world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg_node = world.node_tree.nodes.get("Background")
if bg_node:
    bg_node.inputs[0].default_value = (0.16, 0.17, 0.21, 1.0)
    bg_node.inputs[1].default_value = 1.30

cam_data = bpy.data.cameras.new("PreviewCam")
cam_data.lens = 54
cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
bpy.context.collection.objects.link(cam_obj)
scene.camera = cam_obj

key_light_data = bpy.data.lights.new(name="KeyLight", type='SUN')
key_light_data.energy = 3.2
key_light = bpy.data.objects.new(name="KeyLight", object_data=key_light_data)
bpy.context.collection.objects.link(key_light)
key_light.rotation_euler = Euler((math.radians(52), math.radians(-16), math.radians(-20)), 'XYZ')

fill_light_data = bpy.data.lights.new(name="FillLight", type='SUN')
fill_light_data.energy = 1.9
fill_light = bpy.data.objects.new(name="FillLight", object_data=fill_light_data)
bpy.context.collection.objects.link(fill_light)
fill_light.rotation_euler = Euler((math.radians(65), math.radians(28), math.radians(34)), 'XYZ')

scene.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items.keys() else 'BLENDER_EEVEE'
scene.render.resolution_x = 900
scene.render.resolution_y = 900

# 1) Front-3/4 Idle Pose
cam_obj.location = Vector((-0.85, -4.55, 1.32))
cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.15)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)

# 2) Back-3/4 Idle Pose
cam_obj.location = Vector((1.75, 4.05, 1.48))
cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.15)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = preview_back_path
bpy.ops.render.render(write_still=True)

# 3) Mid-Explode Swollen Pose (Frame 18 of Explode action!)
rig_obj.animation_data.action = act_explode
if hasattr(rig_obj.animation_data, "action_slot") and len(act_explode.slots) > 0:
    rig_obj.animation_data.action_slot = act_explode.slots[0]
scene.frame_set(18)
bpy.context.view_layer.update()
cam_obj.location = Vector((-1.15, -4.75, 1.38))
cam_obj.rotation_euler = (Vector((0.0, 0.0, 1.12)) - cam_obj.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = preview_explode_path
bpy.ops.render.render(write_still=True)

print("SUCCESSFULLY RENDERED BOOMER PREVIEWS:", preview_path, preview_back_path, preview_explode_path)

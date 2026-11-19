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

FACE_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_BlueOperative_Face.png")
HAIR_TEX_PATH = os.path.join(kit.TEX_DIR, "Chibi_BlueOperative_Hair.png")


def generate_blue_operative_face_texture():
    W, H = 1024, 1024
    aa = 0.0020
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    SKIN_BASE   = (0.990, 0.805, 0.675)
    SKIN_WARM   = (0.945, 0.680, 0.545)
    SKIN_SHADOW = (0.860, 0.560, 0.430)

    canvas[..., 0], canvas[..., 1], canvas[..., 2] = SKIN_BASE
    kit.alpha_blend(canvas, kit.smoothstep(0.64, 0.86, V) * 0.20, SKIN_SHADOW)
    kit.alpha_blend(canvas, kit.smoothstep(0.24, 0.08, V) * 0.16, SKIN_WARM)

    # Subtle anime nose triangle shadow (clean & warm like portrait!)
    nose_pts = [(0.500, 0.415), (0.490, 0.372), (0.510, 0.372)]
    d_n = kit.polyline_sdf(U, V, nose_pts, [0.005, 0.011, 0.008])
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(-aa * 2, aa * 2, d_n)) * 0.44, SKIN_SHADOW)

    # Expressive cocky dark eyebrows peeking above the round blue sunglasses!
    for side in (-1.0, 1.0):
        cx = 0.5 + side * 0.150
        cy = 0.480
        # Subtle eye hint underneath glasses (just in case viewed from side angle)
        r_eye = np.hypot((U - cx) / 0.042, (V - (cy - 0.004)) / 0.028)
        kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.8, 1.0, r_eye)) * 0.35, (0.20, 0.18, 0.25))

        # Playful cocky angled eyebrows (slight smug raise!)
        brow_pts = [
            (cx + side * (-0.085), cy + 0.086),
            (cx + side * (-0.015), cy + 0.098),
            (cx + side * ( 0.055), cy + 0.118),
            (cx + side * ( 0.105), cy + 0.122),
        ]
        d_br = kit.polyline_sdf(U, V, brow_pts, [0.007, 0.0125, 0.0115, 0.0035])
        kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_br), (0.18, 0.09, 0.08))

    # Signature wide confident smirk (exact portrait smile!)
    mouth_pts = [
        (0.398, 0.320),
        (0.450, 0.294),
        (0.505, 0.290),
        (0.560, 0.298),
        (0.610, 0.324),
    ]
    d_m = kit.polyline_sdf(U, V, mouth_pts, [0.0025, 0.0060, 0.0072, 0.0060, 0.0028])
    kit.alpha_blend(canvas, 1.0 - kit.smoothstep(-aa, aa, d_m), (0.72, 0.24, 0.20))

    # Soft lower lip shadow
    d_lip = np.hypot((U - 0.502) / 0.040, (V - 0.268) / 0.009)
    kit.alpha_blend(canvas, (1.0 - kit.smoothstep(0.2, 1.0, d_lip)) * 0.28, SKIN_SHADOW)

    return kit.save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_BlueOperative_Face.png")


IMG_FACE = generate_blue_operative_face_texture()
IMG_HAIR = kit.generate_hair_gradient_texture(
    HAIR_TEX_PATH, "Chibi_BlueOperative_Hair.png",
    root_rgb=(0.06, 0.10, 0.42),
    mid_rgb=(0.11, 0.24, 0.88),
    tip_rgb=(0.24, 0.48, 1.00),
    hi_rgb=(0.56, 0.78, 1.00),
    band_v=0.55, band_strength=0.40
)

MAT_FACE_TEX    = kit.make_mat("Mat_Face_Texture", (1.0, 1.0, 1.0, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN        = kit.make_mat("Mat_Skin",         (0.990, 0.805, 0.675, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_BLUE   = kit.make_mat("Mat_Hair_Blue",    (1.0, 1.0, 1.0, 1.0), roughness=0.82, specular=0.06, image=IMG_HAIR)
MAT_SHAVED_GREY = kit.make_mat("Mat_Shaved_Grey",  (0.60, 0.61, 0.66, 1.0), roughness=0.90, specular=0.03)
MAT_GLASS_FRAME = kit.make_mat("Mat_Dark_Strap",   (0.11, 0.10, 0.12, 1.0), roughness=0.86, specular=0.05)
MAT_LENS_COBALT = kit.make_mat("Mat_Lens_Cobalt",  (0.10, 0.42, 0.98, 1.0), roughness=0.14, specular=0.75)
MAT_JACKET_GRN  = kit.make_mat("Mat_Jacket_Green", (0.19, 0.36, 0.26, 1.0), roughness=0.90, specular=0.04)
MAT_JACKET_DARK = kit.make_mat("Mat_Jacket_Dark",  (0.12, 0.24, 0.18, 1.0), roughness=0.92, specular=0.03)
MAT_KHAKI_LINER = kit.make_mat("Mat_Khaki_Liner",  (0.74, 0.67, 0.52, 1.0), roughness=0.90, specular=0.04)
MAT_TAN_POUCH   = kit.make_mat("Mat_Tan_Pouch",    (0.52, 0.47, 0.34, 1.0), roughness=0.90, specular=0.04)
MAT_ARMOR_GREY  = kit.make_mat("Mat_Armor_Grey",   (0.24, 0.25, 0.30, 1.0), roughness=0.76, metallic=0.08, specular=0.08)
MAT_METAL_SILV  = kit.make_mat("Mat_Metal_Silver", (0.76, 0.80, 0.86, 1.0), roughness=0.42, metallic=0.65, specular=0.25)


def build_blue_operative_head():
    head_parts = []
    HZ = 1.69

    head_base = kit.create_anime_head_shell(HZ, MAT_FACE_TEX, MAT_SKIN, style="male_sleek")
    head_parts.append(head_base)

    for side in (1.0, -1.0):
        ear = kit.create_ellipsoid(
            f"Ear_{side}", (side * 0.370, 0.014, HZ - 0.025), (0.048, 0.036, 0.076),
            MAT_SKIN, rot_euler=(0.06, side * -0.20, side * 0.16)
        )
        head_parts.append(ear)

    # Tactical Earpiece + Thin Mic Wire on Left Ear (+X, matches portrait!)
    earpiece = kit.create_rounded_box("Comms_Earpiece", (0.394, -0.008, HZ - 0.036), (0.036, 0.040, 0.070), MAT_GLASS_FRAME, bevel_width=0.008)
    mic_wire = kit.create_rounded_box("Comms_MicWire", (0.358, -0.068, HZ - 0.105), (0.016, 0.092, 0.018), MAT_GLASS_FRAME, rot_euler=(0.45, 0.0, -0.45), bevel_width=0.004)
    head_parts.extend([earpiece, mic_wire])

    # Iconic Round Black-Framed Sunglasses with Vibrant Cobalt-Blue Lenses!
    glass_y = -0.352
    glass_z = HZ - 0.020
    for side in (1.0, -1.0):
        lx = side * 0.145
        rim = kit.create_torus_ring(
            f"GlassRim_{side}", (lx, glass_y, glass_z), major_r=0.102, minor_r=0.017,
            mat=MAT_GLASS_FRAME, rot_euler=(math.pi * 0.5, 0.0, side * -0.14)
        )
        lens = kit.create_ellipsoid(
            f"GlassLens_{side}", (lx, glass_y + 0.005, glass_z), (0.094, 0.016, 0.094),
            MAT_LENS_COBALT, rot_euler=(0.0, 0.0, side * -0.14), u_seg=28, v_seg=18
        )
        # Temple arm extending back to top of ear
        temple = kit.create_rounded_box(
            f"GlassTemple_{side}", (side * 0.315, -0.165, glass_z + 0.022),
            (0.022, 0.36, 0.022), MAT_GLASS_FRAME,
            rot_euler=(0.05, 0.0, side * -0.30), bevel_width=0.005
        )
        head_parts.extend([rim, lens, temple])

    bridge = kit.create_rounded_box("GlassBridge", (0.0, glass_y - 0.008, glass_z + 0.018), (0.115, 0.024, 0.024), MAT_GLASS_FRAME, bevel_width=0.006)
    head_parts.append(bridge)

    # Clean Silver-Grey Shaved Undercut Shell ONLY on sides & back (zero grey forehead band!)
    bm_uc = bmesh.new()
    bmesh.ops.create_uvsphere(bm_uc, u_segments=44, v_segments=28, radius=1.0)
    for v in bm_uc.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        rx, ry, rz = 0.384, 0.362, 0.360
        if y < 0.02:
            push = min(1.0, (0.02 - y) / 0.55)
            center_mask = max(0.0, 1.0 - max(0.0, (abs(x) - 0.55) / 0.35))
            ry *= (1.0 - 0.44 * push * center_mask)
            rx *= (1.0 - 0.16 * push * center_mask)
            if z < 0.35:
                ry *= (1.0 - 0.26 * push)
        v.co = Vector((x * rx, y * ry + 0.022, HZ + 0.014 + z * rz))
    bmesh.ops.recalc_face_normals(bm_uc, faces=bm_uc.faces)
    m_uc = bpy.data.meshes.new("Hair_Undercut")
    bm_uc.to_mesh(m_uc)
    bm_uc.free()
    uc_obj = bpy.data.objects.new("Hair_Undercut", m_uc)
    bpy.context.collection.objects.link(uc_obj)
    kit.assign_single_material(uc_obj, MAT_SHAVED_GREY)
    head_parts.append(uc_obj)

    # Cobalt-Blue Crown Base + Signature Sweeping Forehead Swoop & Dynamic Spiky Crest!
    head_parts.append(kit.create_ellipsoid("Hair_TopBase", (0.0, 0.03, HZ + 0.22), (0.312, 0.348, 0.162), MAT_HAIR_BLUE, uv_v=0.32))

    # Signature curved cobalt-blue front forehead lock dipping down above left lens!
    swoop_lock = [
        (Vector(( 0.08, -0.22, HZ + 0.38)), 0.095, 0.032, -0.10),
        (Vector((-0.04, -0.35, HZ + 0.31)), 0.088, 0.028,  0.18),
        (Vector((-0.11, -0.39, HZ + 0.19)), 0.056, 0.018,  0.34),
        (Vector((-0.07, -0.39, HZ + 0.09)), 0.008, 0.005,  0.40),
    ]
    head_parts.append(kit.create_ribbon_hair_strand("BlueForeheadSwoop", swoop_lock, MAT_HAIR_BLUE, v_start=0.06, v_end=0.98))

    # 12 Dynamic Swept-Up & Flared Cobalt-Blue Spikes (exact anime crest silhouette!)
    blue_spikes = [
        # Towering swept-left hero crest spikes
        ( 0.02, -0.28, 0.31, -0.10, -0.02, 0.56, -0.24,  0.22, 0.52, 0.118, 0.042,  0.22),
        ( 0.12, -0.26, 0.30,  0.04,  0.00, 0.58, -0.08,  0.26, 0.56, 0.112, 0.040,  0.06),
        ( 0.21, -0.22, 0.28,  0.18,  0.04, 0.52,  0.12,  0.28, 0.48, 0.104, 0.038, -0.18),
        (-0.12, -0.26, 0.29, -0.24, -0.04, 0.49, -0.38,  0.14, 0.42, 0.102, 0.036,  0.42),
        # Flared outer left dynamic tips (matching portrait's sharp blue wings on viewer's left!)
        (-0.16, -0.18, 0.27, -0.32,  0.02, 0.42, -0.46,  0.14, 0.32, 0.094, 0.034,  0.56),
        (-0.22, -0.08, 0.24, -0.36,  0.10, 0.36, -0.48,  0.22, 0.24, 0.088, 0.032,  0.65),
        # Right sweeping spikes
        ( 0.26, -0.14, 0.25,  0.30,  0.10, 0.42,  0.32,  0.30, 0.36, 0.092, 0.034, -0.40),
        ( 0.28, -0.02, 0.22,  0.34,  0.18, 0.34,  0.36,  0.36, 0.24, 0.084, 0.030, -0.52),
        # High back-crown spikes for rich 3/4 & Back silhouette
        (-0.08,  0.04, 0.38, -0.14,  0.28, 0.48, -0.18,  0.48, 0.38, 0.105, 0.036,  0.18),
        ( 0.06,  0.06, 0.38,  0.08,  0.30, 0.48,  0.10,  0.48, 0.36, 0.105, 0.036, -0.12),
        ( 0.18,  0.08, 0.34,  0.24,  0.30, 0.38,  0.28,  0.46, 0.26, 0.090, 0.032, -0.28),
        (-0.20,  0.08, 0.32, -0.26,  0.28, 0.36, -0.30,  0.44, 0.24, 0.090, 0.032,  0.30),
    ]
    for idx, (x0, y0, z0, x1, y1, z1, x2, y2, z2, w, t, roll) in enumerate(blue_spikes):
        strand = [
            (Vector((x0, y0, HZ + z0)), w * 0.85, t * 0.85, roll),
            (Vector((x1, y1, HZ + z1)), w * 0.96, t * 0.96, roll),
            (Vector((x2, y2, HZ + z2)), w * 0.56, t * 0.56, roll),
            (Vector((x2 * 1.12, y2 + 0.08, HZ + z2 - 0.03)), 0.008, 0.005, roll),
        ]
        head_parts.append(kit.create_ribbon_hair_strand(f"BlueSpike_{idx}", strand, MAT_HAIR_BLUE, v_start=0.08, v_end=0.98))

    return kit.join_objects_into(head_base, head_parts, "Chibi_Base_Head")


def build_blue_operative_body():
    body_parts = []

    neck_rings = [
        (Vector((0.0, 0.0, 1.24)), Vector((0.112, 0.0, 0.0)), Vector((0.0, 0.104, 0.0))),
        (Vector((0.0, 0.0, 1.50)), Vector((0.095, 0.0, 0.0)), Vector((0.0, 0.090, 0.0))),
    ]
    neck_obj = kit.create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # Inner dark shirt
    shirt_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.160, 0.0))),
        (Vector((0.0, -0.006, 1.05)), Vector((0.228, 0.0, 0.0)), Vector((0.0, 0.182, 0.0))),
        (Vector((0.0,  0.000, 1.30)), Vector((0.160, 0.0, 0.0)), Vector((0.0, 0.140, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Torso_Shirt", shirt_rings, MAT_GLASS_FRAME))

    # Forest-Green Tactical Field Jacket
    jkt_rings = [
        (Vector((0.0,  0.000, 0.78)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.182, 0.0))),
        (Vector((0.0, -0.008, 1.02)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.198, 0.0))),
        (Vector((0.0, -0.006, 1.22)), Vector((0.254, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0,  0.000, 1.30)), Vector((0.198, 0.0, 0.0)), Vector((0.0, 0.162, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Jacket_Green", jkt_rings, MAT_JACKET_GRN))

    # Tall Flared Popped Collar (Forest-Green Outer + Warm Khaki-Tan Inner Lining — exact portrait!)
    for mat_c, r_off in ((MAT_JACKET_GRN, 0.022), (MAT_KHAKI_LINER, 0.0)):
        bm_c = bmesh.new()
        col_levels = [
            (1.26, 0.160 + r_off, 0.142 + r_off, 0.000),
            (1.34, 0.192 + r_off, 0.168 + r_off, 0.012),
            (1.43, 0.228 + r_off, 0.196 + r_off, 0.024),
        ]
        rings_o, rings_i = [], []
        for z, rx, ry, y_off in col_levels:
            ro, ri = [], []
            for i in range(24):
                ang = (-0.26 * math.pi) + (i / 23.0) * (1.52 * math.pi)
                ca, sa = math.cos(ang), math.sin(ang)
                ro.append(bm_c.verts.new(Vector((ca * (rx + 0.016), y_off + sa * (ry + 0.016), z))))
                ri.append(bm_c.verts.new(Vector((ca * rx, y_off + sa * ry, z))))
            rings_o.append(ro)
            rings_i.append(ri)
        for r in range(2):
            for i in range(23):
                bm_c.faces.new((rings_o[r][i], rings_o[r][i+1], rings_o[r+1][i+1], rings_o[r+1][i]))
                bm_c.faces.new((rings_i[r][i+1], rings_i[r][i], rings_i[r+1][i], rings_i[r+1][i+1]))
            bm_c.faces.new((rings_i[r][0], rings_o[r][0], rings_o[r+1][0], rings_i[r+1][0]))
            bm_c.faces.new((rings_o[r][23], rings_i[r][23], rings_i[r+1][23], rings_o[r+1][23]))
        for i in range(23):
            bm_c.faces.new((rings_i[0][i], rings_i[0][i+1], rings_o[0][i+1], rings_o[0][i]))
            bm_c.faces.new((rings_o[2][i], rings_o[2][i+1], rings_i[2][i+1], rings_i[2][i]))
        bmesh.ops.recalc_face_normals(bm_c, faces=bm_c.faces)
        mc = bpy.data.meshes.new(f"Collar_{mat_c.name}")
        bm_c.to_mesh(mc)
        bm_c.free()
        oc = bpy.data.objects.new(f"Collar_{mat_c.name}", mc)
        bpy.context.collection.objects.link(oc)
        body_parts.append(kit.assign_single_material(oc, mat_c))

    # Black Chest Harness + Center Silver Buckle + Shoulder Walkie-Talkie Radio w/ Antenna!
    chest_bar = kit.create_rounded_box("Chest_StrapH", (0.0, -0.205, 1.06), (0.36, 0.036, 0.062), MAT_GLASS_FRAME, bevel_width=0.008)
    chest_buckle = kit.create_rounded_box("Chest_Buckle", (0.0, -0.222, 1.06), (0.095, 0.030, 0.088), MAT_METAL_SILV, bevel_width=0.008)
    chest_buckle_in = kit.create_rounded_box("Chest_BuckleIn", (0.0, -0.232, 1.06), (0.042, 0.020, 0.056), MAT_GLASS_FRAME, bevel_width=0.004)
    body_parts.extend([chest_bar, chest_buckle, chest_buckle_in])

    for side in (1.0, -1.0):
        v_strap = kit.create_rounded_box(f"ChestStrapV_{side}", (side * 0.145, -0.202, 1.12), (0.046, 0.034, 0.34), MAT_GLASS_FRAME, rot_euler=(-0.06, 0.0, side * 0.15), bevel_width=0.007)
        b_strap = kit.create_rounded_box(f"BackStrapV_{side}", (side * 0.125,  0.204, 1.10), (0.046, 0.032, 0.34), MAT_GLASS_FRAME, bevel_width=0.007)
        pouch = kit.create_rounded_box(f"ChestPouch_{side}", (side * 0.155, -0.214, 0.94), (0.098, 0.058, 0.118), MAT_TAN_POUCH, rot_euler=(0.0, 0.0, side * 0.20), bevel_width=0.012)
        sjoint = kit.create_ellipsoid(f"ShoulderJoint_{side}", (side * 0.220, 0.0, 1.23), (0.132, 0.126, 0.126), MAT_JACKET_GRN)
        body_parts.extend([v_strap, b_strap, pouch, sjoint])

    # Iconic Black Walkie-Talkie Radio + Vertical Antenna on Left Shoulder (+X, flush on strap!)
    radio = kit.create_rounded_box("Shoulder_Radio", (0.148, -0.215, 1.185), (0.068, 0.048, 0.108), MAT_ARMOR_GREY, rot_euler=(-0.08, -0.10, 0.18), bevel_width=0.010)
    antenna = kit.create_rounded_box("Shoulder_Antenna", (0.176, -0.208, 1.310), (0.014, 0.014, 0.155), MAT_GLASS_FRAME, rot_euler=(-0.08, -0.18, 0.0), bevel_width=0.004)
    body_parts.extend([radio, antenna])

    # Back Tactical Pack
    back_pack = kit.create_rounded_box("Back_Pack", (0.0, 0.212, 1.06), (0.24, 0.058, 0.22), MAT_JACKET_DARK, bevel_width=0.016)
    body_parts.append(back_pack)

    # Arms + Flush Khaki Patch + Black Fingerless Tactical Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        sleeve_rings = [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.122, 0.0)), Vector((0.0, 0.0, 0.122 * side))),
            (Vector((side * 0.34, 0.0, AZ)), Vector((0.0, 0.114, 0.0)), Vector((0.0, 0.0, 0.114 * side))),
            (Vector((side * 0.54, 0.0, AZ)), Vector((0.0, 0.106, 0.0)), Vector((0.0, 0.0, 0.106 * side))),
            (Vector((side * 0.65, 0.0, AZ)), Vector((0.0, 0.100, 0.0)), Vector((0.0, 0.0, 0.100 * side))),
        ]
        body_parts.append(kit.create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_JACKET_GRN))
        if side > 0:
            patch = kit.create_rounded_box("Sleeve_KhakiPatch", (0.37, 0.0, AZ + 0.098), (0.095, 0.085, 0.022), MAT_KHAKI_LINER, bevel_width=0.006)
            body_parts.append(patch)

        wrist_cuff = kit.create_rounded_box(f"WristCuff_{side}", (side * 0.665, 0.0, AZ), (0.072, 0.214, 0.208), MAT_TAN_POUCH, bevel_width=0.014)
        glove_palm = kit.create_rounded_box(f"GlovePalm_{side}", (side * 0.77, -0.004, AZ), (0.140, 0.175, 0.138), MAT_GLASS_FRAME, bevel_width=0.022)
        glove_guard = kit.create_rounded_box(f"GloveGuard_{side}", (side * 0.79, -0.004, AZ + 0.050), (0.095, 0.155, 0.044), MAT_ARMOR_GREY, bevel_width=0.010)
        fingers = kit.create_ellipsoid(f"Fingers_{side}", (side * 0.865, -0.004, AZ - 0.006), (0.064, 0.074, 0.056), MAT_SKIN)
        thumb = kit.create_ellipsoid(f"Thumb_{side}", (side * 0.78, -0.090, AZ), (0.038, 0.040, 0.034), MAT_SKIN)
        body_parts.extend([wrist_cuff, glove_palm, glove_guard, fingers, thumb])

    # Utility Belt + Pouches + Forest-Green Cargo Pants + Combat Boots
    belt_rings = [
        (Vector((0.0, 0.002, 0.74)), Vector((0.252, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, 0.000, 0.84)), Vector((0.246, 0.0, 0.0)), Vector((0.0, 0.188, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Utility_Belt", belt_rings, MAT_GLASS_FRAME))
    body_parts.append(kit.create_rounded_box("Belt_Buckle", (0.0, -0.198, 0.79), (0.098, 0.042, 0.084), MAT_METAL_SILV, bevel_width=0.009))
    for side in (1.0, -1.0):
        body_parts.append(kit.create_rounded_box(f"HipPouch_{side}", (side * 0.246, -0.03, 0.77), (0.086, 0.140, 0.136), MAT_TAN_POUCH, bevel_width=0.014))

    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.165, 0.0, 0.0)), Vector((0.0, 0.140, 0.0))),
        (Vector((0.0, 0.004, 0.70)), Vector((0.248, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, 0.002, 0.77)), Vector((0.234, 0.0, 0.0)), Vector((0.0, 0.180, 0.0))),
    ]
    body_parts.append(kit.create_loft_object("Pelvis_Pants", pelvis_rings, MAT_JACKET_DARK))

    for side in (1.0, -1.0):
        leg_pants = [
            (Vector((side * 0.146,  0.004, 0.68)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.148,  0.004, 0.55)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.150,  0.000, 0.43)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
            (Vector((side * 0.152, -0.002, 0.31)), Vector((0.108, 0.0, 0.0)), Vector((0.0, -0.112, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"LegPants_{side}", leg_pants, MAT_JACKET_DARK))
        thigh_band = [
            (Vector((side * 0.148, 0.002, 0.50)), Vector((0.133, 0.0, 0.0)), Vector((0.0, -0.137, 0.0))),
            (Vector((side * 0.149, 0.002, 0.55)), Vector((0.134, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"ThighBand_{side}", thigh_band, MAT_TAN_POUCH))
        body_parts.append(kit.create_rounded_box(f"KneePad_{side}", (side * 0.152, -0.108, 0.370), (0.118, 0.050, 0.126), MAT_ARMOR_GREY, bevel_width=0.016))

        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.205)), Vector((0.110, 0.0, 0.0)), Vector((0.0, -0.116, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.152, -0.052, 0.080)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.152, -0.062, 0.038)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootUpper_{side}", boot_upper, MAT_GLASS_FRAME))
        boot_sole = [
            (Vector((side * 0.152, -0.062, 0.044)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
        ]
        body_parts.append(kit.create_loft_object(f"BootSole_{side}", boot_sole, MAT_ARMOR_GREY))

    return kit.join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_blue_operative_head()
body_obj = build_blue_operative_body()
kit.build_rig_animate_and_export(scene, head_obj, body_obj, "Chibi_BlueOperative")

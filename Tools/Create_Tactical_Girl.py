import bpy
import bmesh
import math
import os
import numpy as np
from mathutils import Vector, Matrix, Quaternion, Euler

# ==============================================================================
# 1. SCENE RESET & PATHS
# ==============================================================================
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24

OUT_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire\Assets\GeneratedModels"
TEX_DIR = os.path.join(OUT_DIR, "Textures")
os.makedirs(TEX_DIR, exist_ok=True)

FACE_TEX_PATH = os.path.join(TEX_DIR, "Chibi_TacticalGirl_Face.png")
HAIR_TEX_PATH = os.path.join(TEX_DIR, "Chibi_TacticalGirl_Hair.png")


def srgb_to_lin(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


# ==============================================================================
# 2. PROCEDURAL HIGH-RES ANIME TEXTURE GENERATORS (1024x1024 FACE + 512x512 HAIR)
# ==============================================================================
def save_rgba_array_to_png(arr_srgb, filepath, img_name):
    """Saves an (H, W, 4) float32 sRGB array (row 0 = bottom, matching Blender UV) to PNG and returns Blender Image."""
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


def smoothstep(edge0, edge1, x):
    t = np.clip((x - edge0) / max(1e-7, (edge1 - edge0)), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def alpha_blend(canvas, mask, rgb):
    m = np.clip(mask, 0.0, 1.0)[..., None]
    col = np.array(rgb, dtype=np.float32)[None, None, :]
    canvas[..., :3] = canvas[..., :3] * (1.0 - m) + col * m


def polyline_sdf(u, v, pts, widths):
    """Exact distance-to-tapered-polyline field for crisp anti-aliased anime eyeliner, brows & mouth."""
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


def generate_tactical_girl_face_texture():
    """
    Generates a crisp 1024x1024 Avatar-Star / Anime Chibi face texture matching the
    Tactical Girl 2D reference artwork (fierce amber cat-eyes, dark purple brows,
    soft peach-ivory skin with subtle blush & cool neutral expression).
    UV convention:
      u = 0.5 is nose centerline (u < 0.5 viewer-left / char-right, u > 0.5 viewer-right / char-left)
      v = 0.5 is head center HZ (v = 0 bottom chin, v = 1 top forehead)
    """
    W, H = 1024, 1024
    aa = 0.0020  # Crisp anti-aliasing edge width in UV space

    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)  # row 0 = bottom (v=0), row H-1 = top (v=1)

    canvas = np.ones((H, W, 4), dtype=np.float32)
    # Fair warm ivory-peach skin (matches 2D portrait closely!)
    SKIN_BASE   = (0.985, 0.875, 0.815)
    SKIN_WARM   = (0.960, 0.810, 0.740)
    SKIN_SHADOW = (0.875, 0.705, 0.640)
    BLUSH_COL   = (0.975, 0.690, 0.645)

    canvas[..., 0] = SKIN_BASE[0]
    canvas[..., 1] = SKIN_BASE[1]
    canvas[..., 2] = SKIN_BASE[2]

    # Subtle warm cel-shading around forehead hairline & lower jaw (gives Avatar Star hand-painted depth!)
    forehead_ao = smoothstep(0.62, 0.84, V) * 0.24
    alpha_blend(canvas, forehead_ao, SKIN_SHADOW)

    jaw_ao = smoothstep(0.28, 0.10, V) * 0.18
    alpha_blend(canvas, jaw_ao, SKIN_WARM)

    # Soft rosy anime cheek blush under the outer eyes
    for side in (-1.0, 1.0):
        bx = 0.5 + side * 0.165
        by = 0.382
        d_blush = np.hypot((U - bx) / 0.072, (V - by) / 0.032)
        blush_mask = (1.0 - smoothstep(0.15, 1.0, d_blush)) * 0.28
        alpha_blend(canvas, blush_mask, BLUSH_COL)

    # Tiny delicate anime nose shadow (warm cel-shaded triangle slightly left of center, no ugly dark dot!)
    nose_x = U - 0.496
    nose_y = V - 0.378
    d_nose = np.hypot(nose_x / 0.011, nose_y / 0.0085)
    alpha_blend(canvas, (1.0 - smoothstep(0.3, 1.0, d_nose)) * 0.42, (0.82, 0.60, 0.52))
    # Tiny bright nose bridge highlight
    d_nhi = np.hypot((U - 0.501) / 0.006, (V - 0.392) / 0.014)
    alpha_blend(canvas, (1.0 - smoothstep(0.2, 1.0, d_nhi)) * 0.25, (1.0, 0.96, 0.93))

    # Fierce sharp anime cat-eyes (symmetric for left/right!)
    for side in (-1.0, 1.0):
        # Local eye coordinates: ex > 0 points OUTWARD toward ear, ex < 0 points INWARD toward nose
        cx = 0.5 + side * 0.140
        cy = 0.468
        ex = (U - cx) * side
        ey = V - cy

        # Upper & lower eyelid boundary curves in local eye coords (sharp expressive anime cat-eye!)
        # Spans ex in [-0.082, +0.092]
        t_x = np.clip((ex + 0.082) / 0.174, 0.0, 1.0)
        # Upper lid rises sharply from inner corner, arches high & clean over iris, sweeps to outer wing
        y_top = (
            -0.022
            + 0.088 * np.sin(np.pi * np.power(t_x, 0.76))
            + 0.028 * t_x
        )
        # Lower lid sweeps smoothly downward and rises cleanly toward outer wing
        y_bot = (
            -0.032
            - 0.028 * np.sin(np.pi * np.power(t_x, 0.88))
            + 0.026 * t_x
        )

        # Smooth eye aperture mask (strictly inside ex range and between y_bot and y_top)
        in_x = smoothstep(-0.084, -0.076, ex) * (1.0 - smoothstep(0.086, 0.094, ex))
        above_bot = smoothstep(-aa * 1.1, aa * 1.1, ey - y_bot)
        below_top = smoothstep(-aa * 1.1, aa * 1.1, y_top - ey)
        eye_mask = in_x * above_bot * below_top

        # Crisp white sclera
        alpha_blend(canvas, eye_mask, (0.998, 0.998, 1.000))

        # Subtle cool blue-grey upper-eyelid shadow across very top of sclera
        lid_shadow = eye_mask * smoothstep(0.018, 0.000, y_top - ey) * 0.22
        alpha_blend(canvas, lid_shadow, (0.68, 0.70, 0.82))

        # Large luminous vertical-oval Fiery Golden-Amber Iris (pops brightly like 2D portrait!)
        ix_c = -0.003
        iy_c = 0.004
        rx_i = 0.056
        ry_i = 0.064
        r_iris = np.hypot((ex - ix_c) / rx_i, (ey - iy_c) / ry_i)
        iris_mask = eye_mask * (1.0 - smoothstep(1.0 - aa / rx_i, 1.0 + aa / rx_i, r_iris))

        # Vivid vertical gradient inside iris: fiery copper-orange top -> bright amber -> glowing gold bottom!
        v_grad = np.clip((ey - (iy_c - ry_i)) / (2.0 * ry_i), 0.0, 1.0)
        iris_r = np.clip(1.00 - 0.24 * np.power(v_grad, 1.5), 0.0, 1.0)
        iris_g = np.clip(0.90 - 0.56 * np.power(v_grad, 1.1), 0.0, 1.0)
        iris_b = np.clip(0.28 - 0.26 * np.power(v_grad, 0.75), 0.0, 1.0)

        # Apply iris gradient
        m_i = iris_mask[..., None]
        canvas[..., 0:1] = canvas[..., 0:1] * (1.0 - m_i) + iris_r[..., None] * m_i
        canvas[..., 1:2] = canvas[..., 1:2] * (1.0 - m_i) + iris_g[..., None] * m_i
        canvas[..., 2:3] = canvas[..., 2:3] * (1.0 - m_i) + iris_b[..., None] * m_i

        # Luminous lower-iris golden crescent highlight
        r_glow = np.hypot((ex - ix_c) / (rx_i * 0.76), (ey - (iy_c - 0.025)) / (ry_i * 0.38))
        glow_mask = iris_mask * (1.0 - smoothstep(0.28, 1.0, r_glow)) * 0.88
        alpha_blend(canvas, glow_mask, (1.00, 0.94, 0.34))

        # Crisp dark outer iris limbal ring (thin & clean so golden amber stays super bright!)
        limbal = iris_mask * smoothstep(0.84, 0.97, r_iris) * 0.85
        alpha_blend(canvas, limbal, (0.16, 0.05, 0.02))

        # Sleek dark anime pupil (proportionate so lots of bright orange-gold iris shows around it!)
        r_pupil = np.hypot((ex - ix_c) / 0.021, (ey - (iy_c + 0.004)) / 0.026)
        pupil_mask = iris_mask * (1.0 - smoothstep(0.86, 1.04, r_pupil))
        alpha_blend(canvas, pupil_mask, (0.08, 0.03, 0.04))

        # Crisp shallow upper iris shadow right under top eyelash
        upper_iris_sh = iris_mask * smoothstep(0.018, 0.002, y_top - ey) * 0.50
        alpha_blend(canvas, upper_iris_sh, (0.28, 0.08, 0.02))

        # Crisp anime catchlights (large upper-outer oval + tiny lower-inner spark!)
        r_hi1 = np.hypot((ex - (ix_c + 0.024)) / 0.016, (ey - (iy_c + 0.024)) / 0.014)
        alpha_blend(canvas, eye_mask * (1.0 - smoothstep(0.76, 1.02, r_hi1)), (1.0, 1.0, 1.0))

        r_hi2 = np.hypot((ex - (ix_c - 0.022)) / 0.008, (ey - (iy_c - 0.022)) / 0.008)
        alpha_blend(canvas, eye_mask * (1.0 - smoothstep(0.72, 1.04, r_hi2)), (1.0, 0.99, 0.94))

        # Thick sharp winged jet-black upper eyeliner (drawn in UV space directly!)
        u_lash_pts = [
            (cx + side * (-0.084), cy - 0.022),
            (cx + side * (-0.048), cy + 0.028),
            (cx + side * ( 0.002), cy + 0.068),
            (cx + side * ( 0.054), cy + 0.072),
            (cx + side * ( 0.090), cy + 0.060),
            (cx + side * ( 0.114), cy + 0.078),
        ]
        u_lash_w = [0.0045, 0.0100, 0.0135, 0.0140, 0.0115, 0.0025]
        d_ulash = polyline_sdf(U, V, u_lash_pts, u_lash_w)
        alpha_blend(canvas, 1.0 - smoothstep(-aa, aa, d_ulash), (0.04, 0.03, 0.05))

        # Crisp thin lower outer lash line
        l_lash_pts = [
            (cx + side * (-0.068), cy - 0.034),
            (cx + side * (-0.008), cy - 0.052),
            (cx + side * ( 0.052), cy - 0.036),
            (cx + side * ( 0.092), cy + 0.006),
        ]
        l_lash_w = [0.0020, 0.0030, 0.0040, 0.0034]
        d_llash = polyline_sdf(U, V, l_lash_pts, l_lash_w)
        alpha_blend(canvas, (1.0 - smoothstep(-aa, aa, d_llash)) * 0.90, (0.06, 0.04, 0.06))

        # Subtle double-eyelid crease line above the eye
        crease_pts = [
            (cx + side * (-0.040), cy + 0.062),
            (cx + side * ( 0.014), cy + 0.088),
            (cx + side * ( 0.070), cy + 0.090),
        ]
        crease_w = [0.0014, 0.0020, 0.0012]
        d_crease = polyline_sdf(U, V, crease_pts, crease_w)
        alpha_blend(canvas, (1.0 - smoothstep(-aa, aa, d_crease)) * 0.50, (0.56, 0.36, 0.34))

        # Sharp determined dark-purple anime eyebrow
        brow_pts = [
            (cx + side * (-0.080), cy + 0.082),
            (cx + side * (-0.020), cy + 0.106),
            (cx + side * ( 0.045), cy + 0.124),
            (cx + side * ( 0.096), cy + 0.122),
        ]
        brow_w = [0.0045, 0.0076, 0.0070, 0.0025]
        d_brow = polyline_sdf(U, V, brow_pts, brow_w)
        alpha_blend(canvas, 1.0 - smoothstep(-aa, aa, d_brow), (0.20, 0.07, 0.34))

    # Cool confident mouth (small clean anime line matching portrait!)
    mouth_pts = [
        (0.464, 0.290),
        (0.494, 0.296),
        (0.528, 0.292),
    ]
    mouth_w = [0.0026, 0.0038, 0.0026]
    d_mouth = polyline_sdf(U, V, mouth_pts, mouth_w)
    alpha_blend(canvas, 1.0 - smoothstep(-aa, aa, d_mouth), (0.14, 0.06, 0.08))

    # Tiny soft lower-lip shadow under the mouth
    d_lip = np.hypot((U - 0.496) / 0.020, (V - 0.275) / 0.006)
    alpha_blend(canvas, (1.0 - smoothstep(0.2, 1.0, d_lip)) * 0.26, (0.82, 0.60, 0.54))

    return save_rgba_array_to_png(canvas, FACE_TEX_PATH, "Chibi_TacticalGirl_Face.png")


def generate_tactical_girl_hair_texture():
    """
    Generates a 512x512 stylized Avatar-Star hand-painted violet-purple hair gradient texture.
    v = 0 is hair root (deep indigo-plum), v = 1 is hair tip (vibrant violet-magenta),
    with a crisp stylized anime sheen band around v = 0.56 and subtle longitudinal strand shading!
    """
    W, H = 512, 512
    us = np.linspace(0.0, 1.0, W, dtype=np.float32)
    vs = np.linspace(0.0, 1.0, H, dtype=np.float32)
    U, V = np.meshgrid(us, vs)

    # Vertical root-to-tip rich purple ramp (matches 2D portrait vivid violet!)
    root_col = np.array([0.24, 0.07, 0.44], dtype=np.float32)
    mid_col  = np.array([0.52, 0.16, 0.86], dtype=np.float32)
    tip_col  = np.array([0.66, 0.26, 0.96], dtype=np.float32)
    hi_col   = np.array([0.84, 0.54, 1.00], dtype=np.float32)

    t1 = smoothstep(0.0, 0.45, V)[..., None]
    t2 = smoothstep(0.45, 1.0, V)[..., None]
    col = root_col * (1.0 - t1) + mid_col * t1
    col = col * (1.0 - t2) + tip_col * t2

    # Fine hand-painted longitudinal strand variation along U
    strand_wave = (
        0.50 * np.sin(U * 2.0 * np.pi * 6.0)
        + 0.30 * np.sin(U * 2.0 * np.pi * 14.0 + 1.2)
        + 0.20 * np.sin(U * 2.0 * np.pi * 27.0 + 0.5)
    )
    col = col * (1.0 + 0.08 * strand_wave[..., None])

    # Anime cel-shaded highlight sheen band across mid-crown (broken gently by strands!)
    band_center = 0.54 + 0.03 * np.sin(U * 2.0 * np.pi * 5.0)
    band = np.exp(-np.power((V - band_center) / 0.075, 2.0)) * (0.55 + 0.45 * np.clip(strand_wave + 0.3, 0.0, 1.0))
    col = col * (1.0 - 0.38 * band[..., None]) + hi_col * (0.38 * band[..., None])

    # Slightly darker edges on u = 0 and u = 1 so every ribbon strand has crisp self-shadowing!
    edge_shade = 1.0 - 0.16 * np.power(np.abs(U - 0.5) * 2.0, 2.2)
    col = col * edge_shade[..., None]

    canvas = np.ones((H, W, 4), dtype=np.float32)
    canvas[..., :3] = np.clip(col, 0.0, 1.0)
    return save_rgba_array_to_png(canvas, HAIR_TEX_PATH, "Chibi_TacticalGirl_Hair.png")


IMG_FACE = generate_tactical_girl_face_texture()
IMG_HAIR = generate_tactical_girl_hair_texture()


# ==============================================================================
# 3. CALIBRATED URP-READY MATERIALS (MATTE AVATAR-STAR PALETTE)
# ==============================================================================
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


MAT_FACE_TEX     = make_mat("Mat_Face_Texture", (1.00, 1.00, 1.00, 1.0), roughness=0.90, specular=0.03, image=IMG_FACE)
MAT_SKIN         = make_mat("Mat_Skin",         (0.985, 0.875, 0.815, 1.0), roughness=0.90, specular=0.03)
MAT_HAIR_PURPLE  = make_mat("Mat_Hair_Purple",  (1.00, 1.00, 1.00, 1.0), roughness=0.84, specular=0.06, image=IMG_HAIR)
MAT_HAIR_DARK    = make_mat("Mat_Hair_Dark",    (0.24, 0.08, 0.44, 1.0), roughness=0.90, specular=0.04)
MAT_CAP_BLACK    = make_mat("Mat_Cap_Black",    (0.14, 0.15, 0.18, 1.0), roughness=0.92, specular=0.03)
MAT_DARK_STRAP   = make_mat("Mat_Dark_Strap",   (0.10, 0.11, 0.14, 1.0), roughness=0.90, specular=0.04)
MAT_JACKET_GOLD  = make_mat("Mat_Jacket_Gold",  (0.96, 0.75, 0.10, 1.0), roughness=0.88, specular=0.04)
MAT_JACKET_SHADE = make_mat("Mat_Jacket_Shade", (0.76, 0.54, 0.06, 1.0), roughness=0.92, specular=0.03)
MAT_ARMOR_GREY   = make_mat("Mat_Armor_Grey",   (0.27, 0.28, 0.34, 1.0), roughness=0.76, metallic=0.08, specular=0.08)
MAT_METAL_SILVER = make_mat("Mat_Metal_Silver", (0.76, 0.80, 0.86, 1.0), roughness=0.42, metallic=0.65, specular=0.25)
MAT_GLOVE_BLUE   = make_mat("Mat_Glove_Blue",   (0.16, 0.36, 0.82, 1.0), roughness=0.86, specular=0.05)
MAT_BELT_PURPLE  = make_mat("Mat_Belt_Purple",  (0.46, 0.18, 0.74, 1.0), roughness=0.88, specular=0.04)
MAT_PANTS_NAVY   = make_mat("Mat_Pants_Navy",   (0.18, 0.26, 0.46, 1.0), roughness=0.92, specular=0.03)


# ==============================================================================
# 4. GEOMETRY HELPERS (WITH CLEAN AUTOMATIC UV SUPPORT)
# ==============================================================================
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


def create_ellipsoid(name, center, radii, mat, rot_euler=(0.0, 0.0, 0.0), u_seg=28, v_seg=20, uv_v=0.40):
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


def create_ribbon_hair_strand(name, points_widths, mat, segments=18, substeps=4, v_start=0.05, v_end=0.98, radial_center=Vector((0.0, 0.02, 1.72))):
    """
    Creates a sleek, sharp-edged anime hair lock with Catmull-Rom spline smoothing AND
    clean root-to-tip UV mapping (u = across width, v = root->tip) for hand-painted anime hair shading!
    """
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
            # Crisp anime diamond-arch cross-section (sharp outer edges, softly ridged center!)
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
# 5. BUILD AVATAR-STAR INSPIRED ANIME CHIBI HEAD (Chibi_Base_Head)
#    - Sculpted V-chin anime jaw + high-res 1024x1024 UV Face Texture
#    - Sleek black athletic headband (NO baseball cap!)
#    - Dynamic jagged violet bangs + dramatic high side-swept spiky ponytail!
# ==============================================================================
def build_tactical_girl_head():
    head_parts = []
    HZ = 1.69  # Compact Avatar-Star Chibi head center

    # 5.1 Sculpted Anime Chibi Head Mesh (V-tapered jaw + flatter anime front plane!)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=64, v_segments=48, radius=1.0)
    uv_layer = bm.loops.layers.uv.new("UVMap")

    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        # Soft full cheeks around z = -0.25, tapering smoothly into a crisp Avatar-Star V-chin!
        cheek_bulge = math.exp(-((z + 0.16) / 0.42) ** 2) * 0.022
        if z < -0.18:
            t_chin = min(1.0, (-z - 0.18) / 0.82)
            jaw_taper_x = 1.0 - 0.36 * (t_chin ** 1.35)
            jaw_taper_y = 1.0 - 0.24 * (t_chin ** 1.30)
        else:
            jaw_taper_x = 1.0
            jaw_taper_y = 1.0

        rx = 0.378 * jaw_taper_x + cheek_bulge
        ry = 0.348 * jaw_taper_y + cheek_bulge * 0.55
        rz = 0.358

        vx = x * rx
        vy = y * ry
        vz = HZ + z * rz

        # Gentle flattening on the front eye/cheek plane so anime eyes read clean & sharp!
        if y < -0.25:
            front_f = (-y - 0.25) / 0.75
            vy += 0.018 * front_f * (1.0 - abs(x) * 0.5)

        # Subtle forward chin point at the bottom front
        if y < 0.0 and z < -0.45:
            t_pt = min(1.0, (-z - 0.45) / 0.55)
            vy -= 0.018 * t_pt * max(0.0, -y) * (1.0 - abs(x))

        # Tiny sculpted anime nose bridge bump
        if y < -0.5:
            d_n = math.hypot(x / 0.11, (z + 0.18) / 0.12)
            if d_n < 1.0:
                vy -= 0.012 * ((1.0 - d_n * d_n) ** 1.8)

        v.co = Vector((vx, vy, vz))

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    # Assign clean cylindrical-planar front UV coordinates to every face loop
    for f in bm.faces:
        center = f.calc_center_median()
        if center.y <= 0.03:
            f.material_index = 0  # MAT_FACE_TEX
            for lp in f.loops:
                co = lp.vert.co
                theta = math.atan2(co.x, max(0.01, -co.y))
                u = 0.5 + (theta / (0.50 * math.pi)) * 0.46
                v_uv = 0.5 + ((co.z - HZ) / 0.365) * 0.44
                lp[uv_layer].uv = (max(0.01, min(0.99, u)), max(0.01, min(0.99, v_uv)))
        else:
            f.material_index = 1  # MAT_SKIN
            for lp in f.loops:
                lp[uv_layer].uv = (0.04, 0.04)

    m_head = bpy.data.meshes.new("Chibi_Base_Head")
    bm.to_mesh(m_head)
    bm.free()
    head_base = bpy.data.objects.new("Chibi_Base_Head", m_head)
    bpy.context.collection.objects.link(head_base)
    head_base.data.materials.append(MAT_FACE_TEX)
    head_base.data.materials.append(MAT_SKIN)
    for poly in head_base.data.polygons:
        poly.use_smooth = True
    head_parts.append(head_base)

    # 5.2 Cute Sculpted Anime Ears
    for side in (1.0, -1.0):
        ear = create_ellipsoid(
            f"Ear_{side}",
            (side * 0.365, 0.018, HZ - 0.035),
            (0.046, 0.034, 0.072),
            MAT_SKIN,
            rot_euler=(0.06, side * -0.20, side * 0.16)
        )
        head_parts.append(ear)

    # 5.3 Sculpted Layered Purple Hair Crown & Back Shell (Replaces cap completely!)
    bm_hcap = bmesh.new()
    bmesh.ops.create_uvsphere(bm_hcap, u_segments=48, v_segments=32, radius=1.0)
    uv_h = bm_hcap.loops.layers.uv.new("UVMap")
    for v in bm_hcap.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        # Slightly puffed anime hair crown, hugging the back/top skull cleanly and tucked behind forehead
        rx = 0.402 + 0.014 * max(0.0, z)
        ry = 0.382 + 0.015 * max(0.0, y)
        rz = 0.380
        # Open the front face cutout cleanly higher above brows so zero purple rim peeks behind bangs!
        if y < -0.14 and z < 0.44:
            push = min(1.0, (-y - 0.14) / 0.86) * min(1.0, (0.44 - z) / 0.65)
            ry *= (1.0 - 0.34 * push)
            rx *= (1.0 - 0.12 * push)
        v.co = Vector((x * rx, y * ry + 0.028, HZ + 0.025 + z * rz))
    bmesh.ops.recalc_face_normals(bm_hcap, faces=bm_hcap.faces)
    for f in bm_hcap.faces:
        for lp in f.loops:
            co = lp.vert.co
            u_val = 0.5 + 0.45 * math.sin(math.atan2(co.x, co.y) * 2.0)
            v_val = max(0.08, min(0.88, 0.18 + (co.z - (HZ - 0.32)) / 0.72 * 0.65))
            lp[uv_h].uv = (u_val, v_val)
    m_hcap = bpy.data.meshes.new("Hair_CrownShell")
    bm_hcap.to_mesh(m_hcap)
    bm_hcap.free()
    hair_crown = bpy.data.objects.new("Hair_CrownShell", m_hcap)
    bpy.context.collection.objects.link(hair_crown)
    hair_crown.data.materials.append(MAT_HAIR_PURPLE)
    for poly in hair_crown.data.polygons:
        poly.use_smooth = True
    head_parts.append(hair_crown)

    # 5.4 Sleek Wide Matte-Black Athletic Headband (Prouder & clearly visible like 2D portrait!)
    # Curves cleanly over upper-front crown right behind the bangs and tucks behind both ears
    bm_hb = bmesh.new()
    hb_steps = 36
    hb_half_w = 0.050
    hb_thick = 0.032
    rings_hb = []
    for i in range(hb_steps):
        t = i / (hb_steps - 1)
        # Arc from right-ear (-0.56*pi) over top crown (0) to left-ear (+0.56*pi)
        ang = (-0.58 * math.pi) + t * (1.16 * math.pi)
        sx = math.sin(ang)
        cz = math.cos(ang)
        # Sits prominently across upper-front crown right behind the bangs!
        cx = sx * 0.415
        cz_pos = HZ + 0.036 + cz * 0.390
        cy = -0.145 * (max(0.0, cz) ** 0.95) + 0.055 * (1.0 - max(0.0, cz))
        # Taper slightly near ear tips
        w_scale = 0.68 + 0.32 * max(0.0, cz)
        hw = hb_half_w * w_scale
        out_n = Vector((sx, -0.28 * cz, cz)).normalized()
        fwd_t = Vector((0.0, 1.0, 0.25 * cz)).normalized()
        fwd_t = (fwd_t - out_n * fwd_t.dot(out_n)).normalized()

        base = Vector((cx, cy, cz_pos))
        ring = [
            bm_hb.verts.new(base - fwd_t * hw - out_n * 0.008),
            bm_hb.verts.new(base - fwd_t * (hw * 0.85) + out_n * hb_thick),
            bm_hb.verts.new(base + fwd_t * (hw * 0.85) + out_n * hb_thick),
            bm_hb.verts.new(base + fwd_t * hw - out_n * 0.008),
        ]
        rings_hb.append(ring)

    for r_i in range(hb_steps - 1):
        r0, r1 = rings_hb[r_i], rings_hb[r_i + 1]
        for k in range(4):
            nk = (k + 1) % 4
            bm_hb.faces.new((r0[k], r0[nk], r1[nk], r1[k]))
    bm_hb.faces.new((rings_hb[0][3], rings_hb[0][2], rings_hb[0][1], rings_hb[0][0]))
    bm_hb.faces.new((rings_hb[-1][0], rings_hb[-1][1], rings_hb[-1][2], rings_hb[-1][3]))
    bmesh.ops.recalc_face_normals(bm_hb, faces=bm_hb.faces)
    m_hb = bpy.data.meshes.new("Headband_Black")
    bm_hb.to_mesh(m_hb)
    bm_hb.free()
    hb_obj = bpy.data.objects.new("Headband_Black", m_hb)
    bpy.context.collection.objects.link(hb_obj)
    assign_single_material(hb_obj, MAT_CAP_BLACK)
    head_parts.append(hb_obj)

    # 5.5 Dynamic Sweeping Layered Violet Anime Bangs (Matches 2D portrait's jagged parted fringe!)
    # Note: In front view, -X is viewer's LEFT (portrait left), +X is viewer's RIGHT (portrait right).
    bang_ribbons = [
        # Hero Center-Left Spike (sweeping down right between the eyes — signature portrait lock!)
        [
            (Vector((-0.02, -0.22, HZ + 0.34)), 0.092, 0.028,  0.10),
            (Vector((-0.05, -0.35, HZ + 0.19)), 0.088, 0.025,  0.12),
            (Vector((-0.04, -0.39, HZ + 0.05)), 0.058, 0.018,  0.08),
            (Vector((-0.01, -0.37, HZ - 0.06)), 0.006, 0.004,  0.04),
        ],
        # Secondary Center-Right Spike (sweeping over inner-right brow!)
        [
            (Vector(( 0.06, -0.23, HZ + 0.34)), 0.084, 0.026, -0.14),
            (Vector(( 0.11, -0.34, HZ + 0.19)), 0.076, 0.022, -0.18),
            (Vector(( 0.14, -0.36, HZ + 0.06)), 0.046, 0.014, -0.22),
            (Vector(( 0.13, -0.35, HZ - 0.03)), 0.006, 0.004, -0.22),
        ],
        # Wide Sweeping Left-Forehead Lock (sweeping boldly over viewer's left eye!)
        [
            (Vector((-0.08, -0.21, HZ + 0.35)), 0.105, 0.032,  0.24),
            (Vector((-0.18, -0.32, HZ + 0.21)), 0.096, 0.028,  0.30),
            (Vector((-0.23, -0.33, HZ + 0.06)), 0.062, 0.018,  0.34),
            (Vector((-0.20, -0.31, HZ - 0.06)), 0.007, 0.004,  0.34),
        ],
        # Outer Left Swept Bang + Flared Wing Spike (matches portrait's energetic left crest!)
        [
            (Vector((-0.12, -0.18, HZ + 0.37)), 0.100, 0.032,  0.42),
            (Vector((-0.28, -0.26, HZ + 0.26)), 0.095, 0.028,  0.52),
            (Vector((-0.41, -0.23, HZ + 0.12)), 0.056, 0.018,  0.58),
            (Vector((-0.48, -0.18, HZ + 0.01)), 0.008, 0.005,  0.60),
        ],
        # Top-Left Crown Spiky Tuft (gives asymmetric anime volume above forehead!)
        [
            (Vector((-0.06, -0.12, HZ + 0.39)), 0.098, 0.032,  0.35),
            (Vector((-0.24, -0.17, HZ + 0.36)), 0.088, 0.028,  0.45),
            (Vector((-0.40, -0.15, HZ + 0.27)), 0.048, 0.016,  0.52),
            (Vector((-0.49, -0.11, HZ + 0.18)), 0.008, 0.005,  0.55),
        ],
        # Right-Forehead Layered Swept Bang (hugging right side cleanly under headband)
        [
            (Vector(( 0.14, -0.22, HZ + 0.31)), 0.085, 0.025, -0.32),
            (Vector(( 0.23, -0.30, HZ + 0.16)), 0.078, 0.022, -0.40),
            (Vector(( 0.27, -0.29, HZ + 0.02)), 0.048, 0.014, -0.46),
            (Vector(( 0.24, -0.27, HZ - 0.09)), 0.006, 0.004, -0.48),
        ],
        # Long Dramatic Left Cheek-Framing Side Lock (curves down past jaw to sharp tip!)
        [
            (Vector((-0.24, -0.16, HZ + 0.24)), 0.095, 0.030,  0.68),
            (Vector((-0.36, -0.21, HZ + 0.06)), 0.086, 0.026,  0.76),
            (Vector((-0.37, -0.20, HZ - 0.13)), 0.058, 0.018,  0.80),
            (Vector((-0.29, -0.17, HZ - 0.31)), 0.008, 0.005,  0.82),
        ],
        # Secondary Outer-Left Flared Cheek Lock
        [
            (Vector((-0.30, -0.08, HZ + 0.18)), 0.092, 0.030,  0.98),
            (Vector((-0.43, -0.11, HZ - 0.01)), 0.082, 0.026,  1.05),
            (Vector((-0.45, -0.09, HZ - 0.18)), 0.048, 0.016,  1.10),
            (Vector((-0.40, -0.05, HZ - 0.32)), 0.008, 0.005,  1.12),
        ],
        # Long Sleek Right Cheek-Framing Lock (tucks slightly in front of ear & tapers to chin!)
        [
            (Vector(( 0.24, -0.17, HZ + 0.22)), 0.082, 0.026, -0.68),
            (Vector(( 0.34, -0.22, HZ + 0.04)), 0.074, 0.022, -0.76),
            (Vector(( 0.33, -0.21, HZ - 0.13)), 0.048, 0.015, -0.82),
            (Vector(( 0.25, -0.18, HZ - 0.28)), 0.007, 0.004, -0.84),
        ],
        # Right Behind-Ear Frame Lock
        [
            (Vector(( 0.30,  0.04, HZ + 0.16)), 0.084, 0.026, -1.18),
            (Vector(( 0.38,  0.04, HZ - 0.04)), 0.072, 0.022, -1.24),
            (Vector(( 0.35,  0.05, HZ - 0.19)), 0.044, 0.014, -1.26),
            (Vector(( 0.28,  0.06, HZ - 0.29)), 0.007, 0.004, -1.26),
        ],
        # Crown Top Swept Hair Plates (give sculpted spiky silhouette on top of the head!)
        [
            (Vector((-0.02, -0.14, HZ + 0.38)), 0.135, 0.036,  0.08),
            (Vector(( 0.06,  0.02, HZ + 0.42)), 0.145, 0.038, -0.06),
            (Vector(( 0.14,  0.20, HZ + 0.36)), 0.120, 0.032, -0.14),
            (Vector(( 0.18,  0.30, HZ + 0.27)), 0.060, 0.020, -0.18),
        ],
        [
            (Vector((-0.14, -0.10, HZ + 0.36)), 0.125, 0.034,  0.38),
            (Vector((-0.12,  0.08, HZ + 0.40)), 0.132, 0.036,  0.24),
            (Vector(( 0.02,  0.24, HZ + 0.35)), 0.110, 0.030,  0.08),
            (Vector(( 0.14,  0.31, HZ + 0.27)), 0.055, 0.018,  0.00),
        ],
    ]
    for idx, br in enumerate(bang_ribbons):
        head_parts.append(create_ribbon_hair_strand(f"BangRibbon_{idx}", br, MAT_HAIR_PURPLE, v_start=0.06, v_end=0.98))

    # Back-Nape Short Tapered Locks (clean finished silhouette from behind!)
    for idx, x_off in enumerate((-0.24, -0.12, 0.0, 0.12, 0.24)):
        nape_lock = [
            (Vector((x_off * 0.85, 0.32, HZ + 0.02)), 0.082, 0.028, x_off * 1.4),
            (Vector((x_off * 0.95, 0.35, HZ - 0.14)), 0.070, 0.022, x_off * 1.5),
            (Vector((x_off * 0.90, 0.31, HZ - 0.27)), 0.040, 0.014, x_off * 1.5),
            (Vector((x_off * 0.80, 0.25, HZ - 0.34)), 0.008, 0.005, x_off * 1.5),
        ]
        head_parts.append(create_ribbon_hair_strand(f"NapeLock_{idx}", nape_lock, MAT_HAIR_PURPLE, v_start=0.28, v_end=0.78))

    # 5.6 DRAMATIC HIGH SIDE-SWEPT SPIKY PONYTAIL (Exact portrait placement: upper-right in front view = +X!)
    pt_anchor = Vector((0.18, 0.26, HZ + 0.30))
    tie = create_ellipsoid(
        "Ponytail_Tie",
        pt_anchor,
        (0.110, 0.088, 0.095),
        MAT_CAP_BLACK,
        rot_euler=(0.45, -0.42, -0.25)
    )
    head_parts.append(tie)

    pt_center = Vector((0.14, 0.24, HZ + 0.28))
    ponytail_ribbons = [
        # 1. Towering Hero Top Crest Spike (sweeps UP and dramatically right, exactly like portrait!)
        [
            (pt_anchor + Vector((0.00,  0.00,  0.02)), 0.125, 0.052, -0.15),
            (pt_anchor + Vector((0.14,  0.04,  0.24)), 0.135, 0.054, -0.22),
            (pt_anchor + Vector((0.36,  0.10,  0.30)), 0.095, 0.036, -0.28),
            (pt_anchor + Vector((0.55,  0.16,  0.22)), 0.010, 0.006, -0.32),
        ],
        # 2. Sharp Upper-Inner Spike (pointing up-left slightly above hair tie, exact portrait tuft!)
        [
            (pt_anchor + Vector((-0.02, 0.00,  0.04)), 0.095, 0.040,  0.12),
            (pt_anchor + Vector(( 0.02, 0.03,  0.24)), 0.088, 0.036,  0.04),
            (pt_anchor + Vector(( 0.08, 0.07,  0.37)), 0.050, 0.020, -0.04),
            (pt_anchor + Vector(( 0.14, 0.10,  0.44)), 0.008, 0.005, -0.08),
        ],
        # 3. Main Wide Sweeping Upper-Side Plume (biggest sweeping sickle silhouette!)
        [
            (pt_anchor + Vector((0.02,  0.01,  0.01)), 0.145, 0.062, -0.36),
            (pt_anchor + Vector((0.24,  0.08,  0.18)), 0.160, 0.064, -0.44),
            (pt_anchor + Vector((0.48,  0.16,  0.14)), 0.115, 0.044, -0.52),
            (pt_anchor + Vector((0.66,  0.22, -0.02)), 0.012, 0.007, -0.58),
        ],
        # 4. Mid-Outer Cascading Sweeping Plume
        [
            (pt_anchor + Vector((0.03,  0.03, -0.01)), 0.135, 0.056, -0.52),
            (pt_anchor + Vector((0.28,  0.12,  0.06)), 0.148, 0.058, -0.60),
            (pt_anchor + Vector((0.48,  0.22, -0.08)), 0.102, 0.040, -0.66),
            (pt_anchor + Vector((0.60,  0.28, -0.26)), 0.011, 0.006, -0.70),
        ],
        # 5. Lower Curved Sickle Tail (curves gracefully downward behind shoulder!)
        [
            (pt_anchor + Vector((0.04,  0.04, -0.04)), 0.120, 0.050, -0.68),
            (pt_anchor + Vector((0.24,  0.14, -0.10)), 0.128, 0.052, -0.74),
            (pt_anchor + Vector((0.39,  0.24, -0.26)), 0.088, 0.034, -0.78),
            (pt_anchor + Vector((0.44,  0.28, -0.46)), 0.010, 0.006, -0.82),
        ],
        # 6. Lower-Inner Flowing Plume (fills back-right curve nicely!)
        [
            (pt_anchor + Vector((0.02,  0.06, -0.05)), 0.110, 0.046, -0.40),
            (pt_anchor + Vector((0.15,  0.20, -0.16)), 0.118, 0.048, -0.46),
            (pt_anchor + Vector((0.24,  0.30, -0.34)), 0.078, 0.032, -0.50),
            (pt_anchor + Vector((0.25,  0.32, -0.52)), 0.010, 0.006, -0.52),
        ],
        # 7. Back-Volume Sweeping Plume (so 3/4 & Back views look lush & full!)
        [
            (pt_anchor + Vector((-0.02, 0.06,  0.00)), 0.130, 0.056,  0.05),
            (pt_anchor + Vector(( 0.12, 0.24,  0.08)), 0.140, 0.058,  0.00),
            (pt_anchor + Vector(( 0.26, 0.38, -0.06)), 0.098, 0.040, -0.05),
            (pt_anchor + Vector(( 0.32, 0.44, -0.26)), 0.011, 0.006, -0.08),
        ],
        # 8. Secondary High Back Accent Spike
        [
            (pt_anchor + Vector(( 0.00, 0.05,  0.03)), 0.105, 0.044, -0.10),
            (pt_anchor + Vector(( 0.18, 0.20,  0.22)), 0.112, 0.046, -0.15),
            (pt_anchor + Vector(( 0.38, 0.32,  0.20)), 0.072, 0.030, -0.20),
            (pt_anchor + Vector(( 0.50, 0.38,  0.08)), 0.010, 0.006, -0.24),
        ],
        # 9. Dark-Purple Inner Under-Plume (adds rich anime depth & contrast under the tail!)
        [
            (pt_anchor + Vector(( 0.04, 0.03, -0.05)), 0.095, 0.038, -0.60),
            (pt_anchor + Vector(( 0.22, 0.12, -0.14)), 0.100, 0.038, -0.66),
            (pt_anchor + Vector(( 0.32, 0.19, -0.32)), 0.064, 0.024, -0.72),
            (pt_anchor + Vector(( 0.33, 0.21, -0.48)), 0.009, 0.005, -0.75),
        ],
    ]
    for idx, pr in enumerate(ponytail_ribbons):
        m_use = MAT_HAIR_DARK if idx == len(ponytail_ribbons) - 1 else MAT_HAIR_PURPLE
        head_parts.append(create_ribbon_hair_strand(
            f"PonytailRibbon_{idx}", pr, m_use,
            v_start=0.05, v_end=0.98, radial_center=pt_center
        ))

    return join_objects_into(head_base, head_parts, "Chibi_Base_Head")


# ==============================================================================
# 6. BUILD AVATAR-STAR PROPORTIONED TACTICAL BODY (Chibi_Base_Mesh)
#    Preserves exact 18-bone Rig coordinates so all 11 animations match 100%!
# ==============================================================================
def build_tactical_girl_body():
    body_parts = []

    # 6.1 Sleek Neck
    neck_rings = [
        (Vector((0.0, 0.00, 1.24)), Vector((0.112, 0.0, 0.0)), Vector((0.0, 0.104, 0.0))),
        (Vector((0.0, 0.00, 1.38)), Vector((0.094, 0.0, 0.0)), Vector((0.0, 0.090, 0.0))),
        (Vector((0.0, 0.00, 1.52)), Vector((0.092, 0.0, 0.0)), Vector((0.0, 0.088, 0.0))),
    ]
    neck_obj = create_loft_object("Torso_Neck", neck_rings, MAT_SKIN)
    body_parts.append(neck_obj)

    # 6.2 Fitted Charcoal-Black Tactical Inner Tee (Sleeker Avatar-Star hourglass taper!)
    tee_rings = [
        (Vector((0.0,  0.000, 0.76)), Vector((0.205, 0.0, 0.0)), Vector((0.0, 0.156, 0.0))),
        (Vector((0.0,  0.002, 0.89)), Vector((0.184, 0.0, 0.0)), Vector((0.0, 0.142, 0.0))),
        (Vector((0.0, -0.010, 1.02)), Vector((0.208, 0.0, 0.0)), Vector((0.0, 0.168, 0.0))),
        (Vector((0.0, -0.024, 1.13)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.194, 0.0))),
        (Vector((0.0, -0.010, 1.23)), Vector((0.214, 0.0, 0.0)), Vector((0.0, 0.164, 0.0))),
        (Vector((0.0,  0.000, 1.30)), Vector((0.145, 0.0, 0.0)), Vector((0.0, 0.128, 0.0))),
    ]
    body_parts.append(create_loft_object("Torso_BlackTee", tee_rings, MAT_CAP_BLACK))

    # 6.3 Structured Cropped Golden-Yellow Tactical Jacket (Flared open-front silhouette!)
    bm_jkt = bmesh.new()
    z_levels = [
        (0.86, 0.218, 0.168,  0.000),
        (0.96, 0.232, 0.180, -0.006),
        (1.10, 0.256, 0.204, -0.016),
        (1.22, 0.252, 0.188, -0.008),
        (1.29, 0.206, 0.158,  0.000),
    ]
    ang_steps = 36
    ang_start = -0.34 * math.pi
    ang_end   =  1.34 * math.pi
    thick = 0.030

    outer_rings, inner_rings = [], []
    for z, rx, ry, y_off in z_levels:
        o_ring, i_ring = [], []
        for i in range(ang_steps):
            t = i / (ang_steps - 1)
            ang = ang_start + t * (ang_end - ang_start)
            ca, sa = math.cos(ang), math.sin(ang)
            o_ring.append(bm_jkt.verts.new(Vector((ca * (rx + thick), y_off + sa * (ry + thick), z))))
            i_ring.append(bm_jkt.verts.new(Vector((ca * (rx - 0.005), y_off + sa * (ry - 0.005), z))))
        outer_rings.append(o_ring)
        inner_rings.append(i_ring)

    for r in range(len(z_levels) - 1):
        for i in range(ang_steps - 1):
            bm_jkt.faces.new((outer_rings[r][i], outer_rings[r][i+1], outer_rings[r+1][i+1], outer_rings[r+1][i]))
            bm_jkt.faces.new((inner_rings[r][i+1], inner_rings[r][i], inner_rings[r+1][i], inner_rings[r+1][i+1]))
        bm_jkt.faces.new((inner_rings[r][0], outer_rings[r][0], outer_rings[r+1][0], inner_rings[r+1][0]))
        last = ang_steps - 1
        bm_jkt.faces.new((outer_rings[r][last], inner_rings[r][last], inner_rings[r+1][last], outer_rings[r+1][last]))
    for i in range(ang_steps - 1):
        bm_jkt.faces.new((inner_rings[0][i], inner_rings[0][i+1], outer_rings[0][i+1], outer_rings[0][i]))
        top_r = len(z_levels) - 1
        bm_jkt.faces.new((outer_rings[top_r][i], outer_rings[top_r][i+1], inner_rings[top_r][i+1], inner_rings[top_r][i]))

    bmesh.ops.recalc_face_normals(bm_jkt, faces=bm_jkt.faces)
    m_jkt = bpy.data.meshes.new("Jacket_Body")
    bm_jkt.to_mesh(m_jkt)
    bm_jkt.free()
    jkt_obj = bpy.data.objects.new("Jacket_Body", m_jkt)
    bpy.context.collection.objects.link(jkt_obj)
    assign_single_material(jkt_obj, MAT_JACKET_GOLD)
    body_parts.append(jkt_obj)

    # Tall Sharp Flared Popped Anime Collar (Signature silhouette from 2D portrait!)
    bm_col = bmesh.new()
    col_levels = [
        (1.26, 0.155, 0.136, 0.000),
        (1.33, 0.180, 0.156, 0.012),
        (1.41, 0.212, 0.180, 0.024),
    ]
    c_outer, c_inner = [], []
    for z, rx, ry, y_off in col_levels:
        co_r, ci_r = [], []
        for i in range(26):
            t = i / 25.0
            ang = (-0.28 * math.pi) + t * (1.56 * math.pi)
            ca, sa = math.cos(ang), math.sin(ang)
            co_r.append(bm_col.verts.new(Vector((ca * (rx + 0.024), y_off + sa * (ry + 0.024), z))))
            ci_r.append(bm_col.verts.new(Vector((ca * rx, y_off + sa * ry, z))))
        c_outer.append(co_r)
        c_inner.append(ci_r)
    for r in range(len(col_levels) - 1):
        for i in range(25):
            bm_col.faces.new((c_outer[r][i], c_outer[r][i+1], c_outer[r+1][i+1], c_outer[r+1][i]))
            bm_col.faces.new((c_inner[r][i+1], c_inner[r][i], c_inner[r+1][i], c_inner[r+1][i+1]))
        bm_col.faces.new((c_inner[r][0], c_outer[r][0], c_outer[r+1][0], c_inner[r+1][0]))
        bm_col.faces.new((c_outer[r][25], c_inner[r][25], c_inner[r+1][25], c_outer[r+1][25]))
    for i in range(25):
        bm_col.faces.new((c_inner[0][i], c_inner[0][i+1], c_outer[0][i+1], c_outer[0][i]))
        bm_col.faces.new((c_outer[2][i], c_outer[2][i+1], c_inner[2][i+1], c_inner[2][i]))
    bmesh.ops.recalc_face_normals(bm_col, faces=bm_col.faces)
    m_col = bpy.data.meshes.new("Jacket_Collar")
    bm_col.to_mesh(m_col)
    bm_col.free()
    col_obj = bpy.data.objects.new("Jacket_Collar", m_col)
    bpy.context.collection.objects.link(col_obj)
    assign_single_material(col_obj, MAT_JACKET_GOLD)
    body_parts.append(col_obj)

    # Front Jacket Pockets, Harness Straps, Metallic Zippers & Layered Gunmetal Shoulder Pauldrons
    for side in (1.0, -1.0):
        # Lower front flap pocket with dark gold recessed seam
        pkt = create_rounded_box(
            f"Jacket_Pocket_{side}",
            (side * 0.165, -0.192, 0.96),
            (0.095, 0.044, 0.105),
            MAT_JACKET_GOLD,
            rot_euler=(0.04, 0.0, side * 0.30),
            bevel_width=0.010
        )
        pkt_flap = create_rounded_box(
            f"Jacket_PocketFlap_{side}",
            (side * 0.166, -0.198, 1.005),
            (0.100, 0.048, 0.032),
            MAT_JACKET_SHADE,
            rot_euler=(0.04, 0.0, side * 0.30),
            bevel_width=0.008
        )
        # Tactical chest harness straps + angled buckles (exact portrait detail!)
        strap = create_rounded_box(
            f"Harness_Strap_{side}",
            (side * 0.156, -0.196, 1.155),
            (0.036, 0.036, 0.21),
            MAT_DARK_STRAP,
            rot_euler=(-0.14, 0.0, side * 0.24),
            bevel_width=0.006
        )
        buckle = create_rounded_box(
            f"Harness_Buckle_{side}",
            (side * 0.158, -0.210, 1.135),
            (0.054, 0.024, 0.040),
            MAT_ARMOR_GREY,
            rot_euler=(-0.12, 0.0, side * 0.24),
            bevel_width=0.006
        )
        buckle_pin = create_rounded_box(
            f"Harness_BucklePin_{side}",
            (side * 0.158, -0.218, 1.135),
            (0.026, 0.014, 0.020),
            MAT_METAL_SILVER,
            rot_euler=(-0.12, 0.0, side * 0.24),
            bevel_width=0.003
        )
        # Crisp Metallic Open-Jacket Zipper Track
        zipper = create_rounded_box(
            f"Zipper_{side}",
            (side * 0.102, -0.196, 1.08),
            (0.014, 0.020, 0.39),
            MAT_METAL_SILVER,
            bevel_width=0.003
        )
        # Seamless Jacket Shoulder Sphere
        sjoint = create_ellipsoid(
            f"ShoulderJoint_{side}",
            (side * 0.215, 0.00, 1.23),
            (0.130, 0.124, 0.124),
            MAT_JACKET_GOLD
        )
        # 2-Tier Sculpted Gunmetal Armored Shoulder Pauldron (matches portrait armor!)
        spad = create_rounded_box(
            f"ShoulderPad_{side}",
            (side * 0.248, 0.00, 1.318),
            (0.185, 0.210, 0.068),
            MAT_ARMOR_GREY,
            rot_euler=(0.0, side * 0.36, 0.0),
            bevel_width=0.020
        )
        spad_ridge = create_rounded_box(
            f"ShoulderPadRidge_{side}",
            (side * 0.258, 0.00, 1.345),
            (0.125, 0.155, 0.028),
            MAT_DARK_STRAP,
            rot_euler=(0.0, side * 0.36, 0.0),
            bevel_width=0.008
        )
        body_parts.extend([pkt, pkt_flap, strap, buckle, buckle_pin, zipper, sjoint, spad, spad_ridge])

    # Back Tactical Y-Harness & Small Metallic Clip (clean top-down/behind silhouette!)
    for side in (1.0, -1.0):
        b_strap = create_rounded_box(
            f"Back_HarnessStrap_{side}",
            (side * 0.095, 0.208, 1.14),
            (0.040, 0.026, 0.24),
            MAT_DARK_STRAP,
            rot_euler=(0.06, side * 0.34, 0.0),
            bevel_width=0.006
        )
        body_parts.append(b_strap)
    back_ring = create_rounded_box("Back_HarnessRing", (0.0, 0.214, 1.02), (0.072, 0.028, 0.064), MAT_ARMOR_GREY, bevel_width=0.010)
    back_clip = create_rounded_box("Back_HarnessClip", (0.0, 0.222, 1.02), (0.036, 0.018, 0.036), MAT_METAL_SILVER, bevel_width=0.005)
    body_parts.extend([back_ring, back_clip])

    # 6.4 Compact Avatar-Star Arms & Chunky Royal-Blue Fingerless Tactical Gloves
    AZ = 1.23
    for side in (1.0, -1.0):
        sleeve_rings = [
            (Vector((side * 0.18, 0.0, AZ)), Vector((0.0, 0.118, 0.0)), Vector((0.0, 0.0, 0.118 * side))),
            (Vector((side * 0.29, 0.0, AZ)), Vector((0.0, 0.112, 0.0)), Vector((0.0, 0.0, 0.112 * side))),
            (Vector((side * 0.41, 0.0, AZ)), Vector((0.0, 0.106, 0.0)), Vector((0.0, 0.0, 0.106 * side))),
            (Vector((side * 0.48, 0.0, AZ)), Vector((0.0, 0.104, 0.0)), Vector((0.0, 0.0, 0.104 * side))),
        ]
        body_parts.append(create_loft_object(f"Sleeve_{side}", sleeve_rings, MAT_JACKET_GOLD))

        # Thick Rolled Yellow Cuff
        cuff_rings = [
            (Vector((side * 0.46, 0.0, AZ)), Vector((0.0, 0.116, 0.0)), Vector((0.0, 0.0, 0.116 * side))),
            (Vector((side * 0.50, 0.0, AZ)), Vector((0.0, 0.124, 0.0)), Vector((0.0, 0.0, 0.124 * side))),
            (Vector((side * 0.54, 0.0, AZ)), Vector((0.0, 0.112, 0.0)), Vector((0.0, 0.0, 0.112 * side))),
        ]
        body_parts.append(create_loft_object(f"SleeveCuff_{side}", cuff_rings, MAT_JACKET_GOLD))

        # Fair Skin Forearm
        forearm_rings = [
            (Vector((side * 0.52, 0.0, AZ)), Vector((0.0, 0.086, 0.0)), Vector((0.0, 0.0, 0.084 * side))),
            (Vector((side * 0.60, 0.0, AZ)), Vector((0.0, 0.080, 0.0)), Vector((0.0, 0.0, 0.078 * side))),
            (Vector((side * 0.66, 0.0, AZ)), Vector((0.0, 0.076, 0.0)), Vector((0.0, 0.0, 0.074 * side))),
        ]
        body_parts.append(create_loft_object(f"ForearmSkin_{side}", forearm_rings, MAT_SKIN))

        # Chunky Royal-Blue Tactical Wrist Guard + Dark Strap Accent
        wrist_rings = [
            (Vector((side * 0.64, 0.0, AZ)), Vector((0.0, 0.106, 0.0)), Vector((0.0, 0.0, 0.102 * side))),
            (Vector((side * 0.69, 0.0, AZ)), Vector((0.0, 0.112, 0.0)), Vector((0.0, 0.0, 0.108 * side))),
            (Vector((side * 0.74, 0.0, AZ)), Vector((0.0, 0.100, 0.0)), Vector((0.0, 0.0, 0.096 * side))),
        ]
        body_parts.append(create_loft_object(f"WristGuard_{side}", wrist_rings, MAT_GLOVE_BLUE))
        wrist_trim = create_rounded_box(
            f"WristTrim_{side}",
            (side * 0.69, 0.0, AZ + 0.082),
            (0.072, 0.120, 0.044),
            MAT_DARK_STRAP,
            bevel_width=0.008
        )
        body_parts.append(wrist_trim)

        # Fingerless Glove Palm + Armored Royal-Blue Dorsal Plate + Knuckle Guard
        glove_rings = [
            (Vector((side * 0.73, -0.004, AZ - 0.008)), Vector((0.0, 0.086, 0.0)), Vector((0.0, 0.0, 0.062 * side))),
            (Vector((side * 0.78, -0.004, AZ - 0.008)), Vector((0.0, 0.090, 0.0)), Vector((0.0, 0.0, 0.064 * side))),
            (Vector((side * 0.83, -0.004, AZ - 0.008)), Vector((0.0, 0.084, 0.0)), Vector((0.0, 0.0, 0.060 * side))),
        ]
        body_parts.append(create_loft_object(f"GlovePalm_{side}", glove_rings, MAT_DARK_STRAP))

        dorsal_plate = create_rounded_box(
            f"GlovePlate_{side}",
            (side * 0.785, -0.004, AZ + 0.036),
            (0.118, 0.166, 0.066),
            MAT_GLOVE_BLUE,
            bevel_width=0.014
        )
        knuckle_bar = create_rounded_box(
            f"GloveKnuckle_{side}",
            (side * 0.822, -0.004, AZ + 0.056),
            (0.038, 0.148, 0.032),
            MAT_ARMOR_GREY,
            bevel_width=0.008
        )
        body_parts.extend([dorsal_plate, knuckle_bar])

        # Cute Chunky Chibi Fingers & Thumb
        finger_rings = [
            (Vector((side * 0.82, -0.004, AZ)), Vector((0.0, 0.082, 0.0)), Vector((0.0, 0.0, 0.066 * side))),
            (Vector((side * 0.87, -0.004, AZ)), Vector((0.0, 0.074, 0.0)), Vector((0.0, 0.0, 0.058 * side))),
            (Vector((side * 0.905, -0.004, AZ)), Vector((0.0, 0.048, 0.0)), Vector((0.0, 0.0, 0.038 * side))),
        ]
        body_parts.append(create_loft_object(f"FingersSkin_{side}", finger_rings, MAT_SKIN))

        thumb_base = [
            (Vector((side * 0.75, -0.045, AZ)), Vector((side * 0.038, 0.0, 0.0)), Vector((0.0, 0.0, 0.036 * side))),
            (Vector((side * 0.79, -0.088, AZ)), Vector((side * 0.034, 0.01, 0.0)), Vector((0.0, 0.0, 0.032 * side))),
        ]
        body_parts.append(create_loft_object(f"ThumbGlove_{side}", thumb_base, MAT_GLOVE_BLUE))

        thumb_tip = [
            (Vector((side * 0.78, -0.082, AZ)), Vector((side * 0.032, 0.01, 0.0)), Vector((0.0, 0.0, 0.030 * side))),
            (Vector((side * 0.81, -0.120, AZ)), Vector((side * 0.024, 0.01, 0.0)), Vector((0.0, 0.0, 0.024 * side))),
        ]
        body_parts.append(create_loft_object(f"ThumbSkin_{side}", thumb_tip, MAT_SKIN))

    # 6.5 Signature Vivid Purple Utility Belt, Silver Cobra Buckle, Carabiner & Chunky Hip Pouches
    belt_rings = [
        (Vector((0.0, 0.004, 0.74)), Vector((0.248, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, 0.002, 0.795)), Vector((0.256, 0.0, 0.0)), Vector((0.0, 0.198, 0.0))),
        (Vector((0.0, 0.000, 0.855)), Vector((0.240, 0.0, 0.0)), Vector((0.0, 0.184, 0.0))),
    ]
    body_parts.append(create_loft_object("Utility_Belt", belt_rings, MAT_BELT_PURPLE))

    belt_buckle = create_rounded_box("Belt_Buckle", (0.0, -0.198, 0.798), (0.108, 0.044, 0.090), MAT_METAL_SILVER, bevel_width=0.010)
    buckle_core = create_rounded_box("Belt_BuckleCore", (0.0, -0.212, 0.798), (0.046, 0.024, 0.062), MAT_DARK_STRAP, bevel_width=0.005)
    # Portrait detail: Silver carabiner hook hanging next to the front buckle!
    carabiner = create_rounded_box("Belt_Carabiner", (0.105, -0.194, 0.742), (0.036, 0.020, 0.068), MAT_METAL_SILVER, rot_euler=(0.08, 0.0, 0.22), bevel_width=0.006)
    body_parts.extend([belt_buckle, buckle_core, carabiner])

    for side in (1.0, -1.0):
        pouch = create_rounded_box(
            f"HipPouch_{side}",
            (side * 0.244, -0.040, 0.770),
            (0.092, 0.142, 0.146),
            MAT_BELT_PURPLE,
            rot_euler=(0.0, side * -0.10, side * -0.18),
            bevel_width=0.016
        )
        pouch_flap = create_rounded_box(
            f"HipPouchFlap_{side}",
            (side * 0.248, -0.040, 0.828),
            (0.098, 0.148, 0.044),
            MAT_BELT_PURPLE,
            rot_euler=(0.0, side * -0.10, side * -0.18),
            bevel_width=0.010
        )
        pouch_strap = create_rounded_box(
            f"HipPouchStrap_{side}",
            (side * 0.280, -0.044, 0.782),
            (0.036, 0.046, 0.136),
            MAT_DARK_STRAP,
            rot_euler=(0.0, side * -0.10, side * -0.18),
            bevel_width=0.006
        )
        body_parts.extend([pouch, pouch_flap, pouch_strap])

    # 6.6 Avatar-Star Tapered Tactical Pants, Asymmetric Yellow Thigh Accent, Knee Armor & Chunky Combat Boots
    pelvis_rings = [
        (Vector((0.0, 0.002, 0.56)), Vector((0.162, 0.0, 0.0)), Vector((0.0, 0.138, 0.0))),
        (Vector((0.0, 0.004, 0.64)), Vector((0.236, 0.0, 0.0)), Vector((0.0, 0.182, 0.0))),
        (Vector((0.0, 0.005, 0.71)), Vector((0.248, 0.0, 0.0)), Vector((0.0, 0.192, 0.0))),
        (Vector((0.0, 0.003, 0.77)), Vector((0.232, 0.0, 0.0)), Vector((0.0, 0.178, 0.0))),
    ]
    body_parts.append(create_loft_object("Pelvis_Pants", pelvis_rings, MAT_PANTS_NAVY))

    for side in (1.0, -1.0):
        # Tapered cargo silhouette (wider thigh, slightly cinched knee, flared tactical boot cuff!)
        leg_pants = [
            (Vector((side * 0.144,  0.004, 0.68)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.148,  0.004, 0.55)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.150,  0.000, 0.43)), Vector((0.114, 0.0, 0.0)), Vector((0.0, -0.118, 0.0))),
            (Vector((side * 0.152, -0.002, 0.31)), Vector((0.108, 0.0, 0.0)), Vector((0.0, -0.112, 0.0))),
            (Vector((side * 0.152,  0.000, 0.21)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
        ]
        body_parts.append(create_loft_object(f"LegPants_{side}", leg_pants, MAT_PANTS_NAVY))

        # Portrait-inspired Yellow Tactical Thigh Panel on Right Leg (-1) + Sleek Yellow Thigh Band on Both
        thigh_band = [
            (Vector((side * 0.148, 0.002, 0.50)), Vector((0.133, 0.0, 0.0)), Vector((0.0, -0.137, 0.0))),
            (Vector((side * 0.149, 0.002, 0.55)), Vector((0.134, 0.0, 0.0)), Vector((0.0, -0.138, 0.0))),
        ]
        body_parts.append(create_loft_object(f"ThighBand_{side}", thigh_band, MAT_JACKET_GOLD))

        if side < 0:
            # Flush contoured yellow tactical outer-thigh cargo pocket (zero protruding corners!)
            thigh_panel = create_rounded_box(
                "RightThigh_YellowPanel",
                (-0.242, -0.008, 0.595),
                (0.058, 0.125, 0.128),
                MAT_JACKET_GOLD,
                rot_euler=(0.0, 0.04, 0.0),
                bevel_width=0.014
            )
            body_parts.append(thigh_panel)

        # 2-Layer Sculpted Tactical Knee Pad
        kpad_outer = create_rounded_box(
            f"KneePadOuter_{side}",
            (side * 0.152, -0.106, 0.370),
            (0.120, 0.050, 0.126),
            MAT_ARMOR_GREY,
            bevel_width=0.016
        )
        kpad_inner = create_rounded_box(
            f"KneePadInner_{side}",
            (side * 0.152, -0.120, 0.370),
            (0.084, 0.036, 0.088),
            MAT_DARK_STRAP,
            bevel_width=0.012
        )
        body_parts.extend([kpad_outer, kpad_inner])

        # Royal-Blue Tactical Boot Cuff Rim
        boot_cuff = [
            (Vector((side * 0.152, 0.000, 0.24)), Vector((0.122, 0.0, 0.0)), Vector((0.0, -0.126, 0.0))),
            (Vector((side * 0.152, 0.000, 0.185)), Vector((0.118, 0.0, 0.0)), Vector((0.0, -0.122, 0.0))),
        ]
        body_parts.append(create_loft_object(f"BootCuff_{side}", boot_cuff, MAT_GLOVE_BLUE))

        # Chunky Avatar-Star Combat Boots (Wide grounded stance, armored toe cap & thick lugged sole!)
        boot_upper = [
            (Vector((side * 0.152,  0.000, 0.195)), Vector((0.108, 0.0, 0.0)), Vector((0.0, -0.114, 0.0))),
            (Vector((side * 0.152, -0.015, 0.130)), Vector((0.116, 0.0, 0.0)), Vector((0.0, -0.132, 0.0))),
            (Vector((side * 0.152, -0.052, 0.080)), Vector((0.128, 0.0, 0.0)), Vector((0.0, -0.178, 0.0))),
            (Vector((side * 0.152, -0.062, 0.038)), Vector((0.132, 0.0, 0.0)), Vector((0.0, -0.188, 0.0))),
        ]
        body_parts.append(create_loft_object(f"BootUpper_{side}", boot_upper, MAT_DARK_STRAP))

        # Armored Boot Ankle Strap + Toe Cap Accent
        ankle_strap = create_rounded_box(
            f"BootStrap_{side}",
            (side * 0.152, -0.068, 0.148),
            (0.196, 0.112, 0.036),
            MAT_ARMOR_GREY,
            bevel_width=0.010
        )
        body_parts.append(ankle_strap)

        boot_sole = [
            (Vector((side * 0.152, -0.062, 0.044)), Vector((0.140, 0.0, 0.0)), Vector((0.0, -0.196, 0.0))),
            (Vector((side * 0.152, -0.062, 0.003)), Vector((0.136, 0.0, 0.0)), Vector((0.0, -0.192, 0.0))),
        ]
        body_parts.append(create_loft_object(f"BootSole_{side}", boot_sole, MAT_ARMOR_GREY))

    return join_objects_into(neck_obj, body_parts, "Chibi_Base_Mesh")


head_obj = build_tactical_girl_head()
body_obj = build_tactical_girl_body()

# ==============================================================================
# 7. CREATE EXACT 18-BONE CHIBI ARMATURE (Chibi_Base_Rig) — 100% COMPATIBLE
# ==============================================================================
arm_data = bpy.data.armatures.new("Chibi_Base_Rig")
rig_obj = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
bpy.context.collection.objects.link(rig_obj)
bpy.context.view_layer.objects.active = rig_obj
bpy.ops.object.mode_set(mode='EDIT')

bone_defs = [
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
# 8. SKIN WEIGHT BINDING
# ==============================================================================
def point_to_segment_dist(p, a, b):
    ab = b - a
    len_sq = ab.length_squared
    if len_sq < 1e-8:
        return (p - a).length
    t = max(0.0, min(1.0, (p - a).dot(ab) / len_sq))
    proj = a + ab * t
    return (p - proj).length


def bind_mesh_to_rig(mesh_obj, rig, is_head=False):
    mesh_obj.parent = rig
    mod = mesh_obj.modifiers.new(name="Armature", type='ARMATURE')
    mod.object = rig
    mod.use_deform_preserve_volume = True

    vgroups = {}
    for bname, _, _, _, _ in bone_defs:
        vgroups[bname] = mesh_obj.vertex_groups.new(name=bname)

    bones_info = [(bname, Vector(h), Vector(t)) for bname, h, t, _, _ in bone_defs if bname != "Root"]

    for v in mesh_obj.data.vertices:
        co = v.co
        if is_head:
            # Keep ponytail & hair locks 100% bound to Head so long spikes never stretch into the neck!
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

            d = point_to_segment_dist(co, h, t)
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


bind_mesh_to_rig(head_obj, rig_obj, is_head=True)
bind_mesh_to_rig(body_obj, rig_obj, is_head=False)

# ==============================================================================
# 9. ZERO-TWIST ANALYTICAL TWO-BONE IK & ALL 11 ANIMATIONS (IDENTICAL TIMINGS)
# ==============================================================================
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
    new_world_mat = Matrix.LocRotScale(world_loc, new_world_rot, Vector((1, 1, 1)))
    pb.matrix = new_world_mat
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
    plane_pole = (pole - dir_norm * pole.dot(dir_norm))
    if plane_pole.length_squared < 1e-6:
        plane_pole = Vector((0, 1, 0))
    else:
        plane_pole.normalize()

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
    t = (f - 1) / 48.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang)

    set_hips_transform((0.0, 0.0, 0.012 * breath), pitch_deg=1.2 * breath, yaw_deg=1.5 * math.sin(ang * 0.5), roll_deg=0.0)
    set_local_euler_deg("Spine", 1.5 - 1.2 * breath, 0.0, 0.0)
    set_local_euler_deg("Chest", -2.0 * breath, 0.0, 0.0)
    set_local_euler_deg("Head", 1.8 * breath, 2.5 * math.sin(ang), 1.2 * math.cos(ang))

    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.31, -0.04, 0.79 + 0.010 * breath),
                    pole_world_vec=(0.35, 0.65, -0.1))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.31, -0.04, 0.79 + 0.010 * breath),
                    pole_world_vec=(-0.35, 0.65, -0.1))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, -0.01, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, -0.01, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 2. Walk (Frames 1..25)
act_walk = create_new_action("Walk")
prev_q = {}
for f in range(1, 26):
    begin_frame_pose(f)
    t = (f - 1) / 24.0
    ang = t * 2.0 * math.pi
    s = math.sin(ang)
    c = math.cos(ang)
    bounce = abs(math.sin(ang)) * 0.038

    set_hips_transform((0.018 * s, 0.0, bounce), pitch_deg=4.0, yaw_deg=-7.0 * s, roll_deg=3.0 * c)
    set_local_euler_deg("Spine", 3.0, 4.0 * s, 0.0)
    set_local_euler_deg("Chest", 3.0, 8.0 * s, -2.0 * c)
    set_local_euler_deg("Head", -4.0, -5.0 * s, 0.0)

    l_arm_y = -0.04 + 0.18 * s
    r_arm_y = -0.04 - 0.18 * s
    l_arm_z = 0.81 + 0.04 * abs(s)
    r_arm_z = 0.81 + 0.04 * abs(s)
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32, l_arm_y, l_arm_z),
                    pole_world_vec=(0.40, 0.70, -0.20))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.32, r_arm_y, r_arm_z),
                    pole_world_vec=(-0.40, 0.70, -0.20))

    l_foot_y = -0.18 * s
    r_foot_y =  0.18 * s
    l_foot_z = 0.11 + max(0.0, c) * 0.10
    r_foot_z = 0.11 + max(0.0, -c) * 0.10
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, l_foot_y, l_foot_z),
                    pole_world_vec=(0.16, -1.0, 0.2),
                    hand_rot_offset_deg=(15.0 * s, 0, 0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, r_foot_y, r_foot_z),
                    pole_world_vec=(-0.16, -1.0, 0.2),
                    hand_rot_offset_deg=(-15.0 * s, 0, 0))
    keyframe_all_bones(f, prev_q)

# 3. Run (Frames 1..17)
act_run = create_new_action("Run")
prev_q = {}
for f in range(1, 18):
    begin_frame_pose(f)
    t = (f - 1) / 16.0
    ang = t * 2.0 * math.pi
    s = math.sin(ang)
    c = math.cos(ang)
    bounce = (math.sin(ang * 2.0 - math.pi * 0.25) * 0.5 + 0.5) * 0.080

    set_hips_transform((0.025 * s, 0.0, bounce - 0.015), pitch_deg=14.0, yaw_deg=-12.0 * s, roll_deg=4.5 * c)
    set_local_euler_deg("Spine", 8.0, 6.0 * s, 0.0)
    set_local_euler_deg("Chest", 8.0, 12.0 * s, -3.5 * c)
    set_local_euler_deg("Head", -16.0, -8.0 * s, 0.0)

    l_hand_y = -0.12 + 0.26 * s
    r_hand_y = -0.12 - 0.26 * s
    l_hand_z = 0.92 - 0.08 * s
    r_hand_z = 0.92 + 0.08 * s
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.33, l_hand_y, l_hand_z),
                    pole_world_vec=(0.45, 0.80, -0.25))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.33, r_hand_y, r_hand_z),
                    pole_world_vec=(-0.45, 0.80, -0.25))

    l_foot_y = -0.28 * s
    r_foot_y =  0.28 * s
    l_foot_z = 0.12 + max(0.0, c) * 0.20
    r_foot_z = 0.12 + max(0.0, -c) * 0.20
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.152, l_foot_y, l_foot_z),
                    pole_world_vec=(0.16, -1.0, 0.3),
                    hand_rot_offset_deg=(28.0 * s, 0, 0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.152, r_foot_y, r_foot_z),
                    pole_world_vec=(-0.16, -1.0, 0.3),
                    hand_rot_offset_deg=(-28.0 * s, 0, 0))
    keyframe_all_bones(f, prev_q)

# 4. GunHold (Frames 1..25)
act_gunhold = create_new_action("GunHold")
prev_q = {}
for f in range(1, 26):
    begin_frame_pose(f)
    t = (f - 1) / 24.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang) * 0.010

    set_hips_transform((0.0, 0.0, breath - 0.015), pitch_deg=4.0, yaw_deg=10.0, roll_deg=0.0)
    set_local_euler_deg("Spine", 4.0, -5.0, 0.0)
    set_local_euler_deg("Chest", 4.0, -5.0, 0.0)
    set_local_euler_deg("Head", -5.0, 0.0, 0.0)

    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.06, -0.38, 1.10 + breath),
                    pole_world_vec=(-0.40, 0.25, -0.8))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.06, -0.40, 1.09 + breath),
                    pole_world_vec=(0.40, 0.25, -0.8))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.05, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.05, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 5. Shoot (Frames 1..15)
act_shoot = create_new_action("Shoot")
prev_q = {}
for f in range(1, 16):
    begin_frame_pose(f)
    if f <= 3:
        recoil = (f - 1) / 2.0
    else:
        recoil = math.exp(-((f - 3) / 3.8)) * math.cos((f - 3) * 0.22)
        recoil = max(-0.08, recoil)

    set_hips_transform((0.0, 0.03 * recoil, -0.015 - 0.02 * recoil), pitch_deg=4.0 - 6.0 * recoil, yaw_deg=10.0 + 5.0 * recoil, roll_deg=0.0)
    set_local_euler_deg("Spine", 4.0 - 7.0 * recoil, -5.0, 0.0)
    set_local_euler_deg("Chest", 4.0 - 9.0 * recoil, -5.0 + 4.0 * recoil, 0.0)
    set_local_euler_deg("Head", -5.0 - 8.0 * recoil, 0.0, 0.0)

    kick_y = 0.11 * recoil
    kick_z = 0.15 * recoil
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.06, -0.38 + kick_y, 1.10 + kick_z),
                    pole_world_vec=(-0.42, 0.25, -0.75),
                    hand_rot_offset_deg=(-25.0 * recoil, 0, 0))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.06, -0.40 + kick_y, 1.09 + kick_z),
                    pole_world_vec=(0.42, 0.25, -0.75),
                    hand_rot_offset_deg=(-25.0 * recoil, 0, 0))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.05, 0.11),
                    pole_world_vec=(0.16, -1.0, 0.1))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.05, 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.1))
    keyframe_all_bones(f, prev_q)

# 6. Slash1 (Frames 1..19)
act_slash1 = create_new_action("Slash1")
prev_q = {}
for f in range(1, 20):
    begin_frame_pose(f)
    if f <= 5:
        u = (f - 1) / 4.0
        swing = -u * 0.55
    elif f <= 11:
        u = (f - 5) / 6.0
        swing = -0.55 + 1.55 * (3 * u * u - 2 * u * u * u)
    else:
        u = (f - 11) / 8.0
        swing = 1.0 * (1.0 - (3 * u * u - 2 * u * u * u))

    yaw_body = -42.0 * swing
    crouch = -0.05 * abs(swing)
    lunge_y = -0.08 * max(0.0, swing)

    set_hips_transform((0.0, lunge_y, crouch), pitch_deg=12.0 * abs(swing), yaw_deg=yaw_body * 0.45, roll_deg=-4.0 * swing)
    set_local_euler_deg("Spine", 8.0 * abs(swing), yaw_body * 0.30, 0.0)
    set_local_euler_deg("Chest", 6.0 * abs(swing), yaw_body * 0.35, 0.0)
    set_local_euler_deg("Head", -8.0 * abs(swing), -yaw_body * 0.45, 0.0)

    arc_ang = swing * 1.65
    rh_x = -0.16 + 0.56 * swing
    rh_y = -0.46 * math.cos(arc_ang * 0.65) + 0.06
    rh_z = 1.10 - 0.10 * swing
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(rh_x, rh_y, rh_z),
                    pole_world_vec=(-0.45, 0.30, -0.4),
                    hand_rot_offset_deg=(0, 0, -35.0 * swing))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32 + 0.08 * swing, -0.04 + 0.14 * swing, 0.84),
                    pole_world_vec=(0.45, 0.50, -0.4))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.12 * max(0.0, swing), 0.11),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.08 * max(0.0, swing), 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 7. Slash2 (Frames 1..19)
act_slash2 = create_new_action("Slash2")
prev_q = {}
for f in range(1, 20):
    begin_frame_pose(f)
    if f <= 5:
        u = (f - 1) / 4.0
        swing = -0.60 * u
    elif f <= 11:
        u = (f - 5) / 6.0
        swing = -0.60 + 1.60 * (3 * u * u - 2 * u * u * u)
    else:
        u = (f - 11) / 8.0
        swing = 1.0 * (1.0 - (3 * u * u - 2 * u * u * u))

    yaw_body = 45.0 * swing
    crouch = -0.05 * (1.0 - max(0.0, swing)) * abs(swing)

    set_hips_transform((0.0, -0.06 * abs(swing), crouch), pitch_deg=8.0 - 6.0 * swing, yaw_deg=yaw_body * 0.45, roll_deg=5.0 * swing)
    set_local_euler_deg("Spine", 6.0 - 5.0 * swing, yaw_body * 0.30, 0.0)
    set_local_euler_deg("Chest", 4.0 - 8.0 * swing, yaw_body * 0.35, 0.0)
    set_local_euler_deg("Head", -4.0 + 6.0 * swing, -yaw_body * 0.45, 0.0)

    rh_x = -0.12 - 0.48 * swing
    rh_y = -0.44 * math.cos(swing * 0.9) + 0.05
    rh_z = 1.08 + 0.30 * swing
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(rh_x, rh_y, rh_z),
                    pole_world_vec=(-0.42, 0.35, -0.4),
                    hand_rot_offset_deg=(20.0 * swing, 0, 40.0 * swing))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.32 - 0.08 * swing, -0.12 + 0.10 * swing, 0.88),
                    pole_world_vec=(0.45, 0.45, -0.4))
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, 0.06 * max(0.0, swing), 0.11),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, -0.12 * max(0.0, swing), 0.11),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 8. Slash3 (Frames 1..23)
act_slash3 = create_new_action("Slash3")
prev_q = {}
for f in range(1, 24):
    begin_frame_pose(f)
    if f <= 8:
        u = (f - 1) / 7.0
        s = 3 * u * u - 2 * u * u * u
        slam = -s
        hip_z = 0.20 * math.sin(u * math.pi * 0.85)
    elif f <= 13:
        u = (f - 8) / 5.0
        s = u * u
        slam = -1.0 + 2.0 * s
        hip_z = 0.10 * (1.0 - u) - 0.16 * u
    elif f <= 16:
        slam = 1.0
        hip_z = -0.16
    else:
        u = (f - 16) / 7.0
        s = 3 * u * u - 2 * u * u * u
        slam = 1.0 * (1.0 - s)
        hip_z = -0.16 * (1.0 - s)

    pitch_torso = 26.0 * slam
    set_hips_transform((0.0, -0.10 * max(0.0, slam), hip_z), pitch_deg=pitch_torso * 0.6, yaw_deg=0.0, roll_deg=0.0)
    set_local_euler_deg("Spine", pitch_torso * 0.5, 0.0, 0.0)
    set_local_euler_deg("Chest", pitch_torso * 0.6, 0.0, 0.0)
    set_local_euler_deg("Head", -pitch_torso * 0.4, 0.0, 0.0)

    if slam < 0:
        hand_y = -0.32 + 0.24 * (-slam)
        hand_z = 1.10 + 0.60 * (-slam)
    else:
        hand_y = -0.32 - 0.18 * slam
        hand_z = 1.10 - 0.52 * slam

    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.08, hand_y, hand_z),
                    pole_world_vec=(-0.45, 0.25, -0.3))
    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.08, hand_y, hand_z),
                    pole_world_vec=(0.45, 0.25, -0.3))

    foot_lift = max(0.0, hip_z * 0.65)
    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.12 * max(0.0, slam), 0.11 + foot_lift),
                    pole_world_vec=(0.16, -1.0, 0.2))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, 0.10 * max(0.0, slam), 0.11 + foot_lift),
                    pole_world_vec=(-0.16, -1.0, 0.2))
    keyframe_all_bones(f, prev_q)

# 9. DashRoll (Frames 1..21)
act_dash = create_new_action("DashRoll")
prev_q = {}
for f in range(1, 22):
    begin_frame_pose(f)
    u = (f - 1) / 20.0
    s = 3 * u * u - 2 * u * u * u
    roll_deg = 360.0 * s
    tuck = math.sin(u * math.pi)
    hip_z = -0.22 * tuck

    set_hips_transform((0.0, -0.12 * tuck, hip_z), pitch_deg=roll_deg, yaw_deg=0.0, roll_deg=0.0)
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
act_death = create_new_action("Death")
prev_q = {}
for f in range(1, 38):
    begin_frame_pose(f)
    if f <= 8:
        u = (f - 1) / 7.0
        s = 3 * u * u - 2 * u * u * u
        hip_x = 0.02 * s
        hip_y = 0.10 * s
        hip_z = -0.05 * s
        hip_pitch = -18.0 * s
        hip_yaw = -15.0 * s
        hip_roll = 6.0 * s
        spine_pitch = -16.0 * s
        chest_pitch = -20.0 * s
        head_pitch = -26.0 * s
        head_yaw = 18.0 * s
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
        hip_x = 0.02 + 0.02 * s
        hip_y = 0.10 - 0.06 * s
        hip_z = -0.05 - 0.22 * s
        hip_pitch = -18.0 + 28.0 * s
        hip_yaw = -15.0 + 25.0 * s
        hip_roll = 6.0 - 12.0 * s
        spine_pitch = -16.0 + 6.0 * s
        chest_pitch = -20.0 + 5.0 * s
        head_pitch = -26.0 - 10.0 * s
        head_yaw = 18.0 - 30.0 * s
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
        hip_x = 0.04 - 0.02 * s
        hip_y = 0.04 - 0.30 * s
        hip_z = -0.27 - 0.16 * s
        hip_pitch = 10.0 + 74.0 * s
        hip_yaw = 10.0 + 8.0 * s
        hip_roll = -6.0 - 12.0 * s
        spine_pitch = -10.0 + 18.0 * s
        chest_pitch = -15.0 + 22.0 * s
        head_pitch = -36.0 + 20.0 * s
        head_yaw = -12.0 + 42.0 * s
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
        hip_x = 0.02
        hip_y = -0.26
        hip_z = -0.43 + bounce
        hip_pitch = 84.0 - 3.0 * bounce * 40.0
        hip_yaw = 18.0
        hip_roll = -18.0
        spine_pitch = 8.0
        chest_pitch = 7.0
        head_pitch = -15.0
        head_yaw = 30.0
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

    set_hips_transform((hip_x, hip_y, hip_z), pitch_deg=hip_pitch, yaw_deg=hip_yaw, roll_deg=hip_roll)
    set_local_euler_deg("Spine", spine_pitch, 0.0, 0.0)
    set_local_euler_deg("Chest", chest_pitch, 0.0, 0.0)
    set_local_euler_deg("Head", head_pitch, head_yaw, 0.0)
    keyframe_all_bones(f, prev_q)

# 11. Sit (Frames 1..49)
act_sit = create_new_action("Sit")
prev_q = {}
for f in range(1, 50):
    begin_frame_pose(f)
    t = (f - 1) / 48.0
    ang = t * 2.0 * math.pi
    breath = math.sin(ang)

    set_hips_world_offset((0.0, 0.14, -0.49 + 0.003 * breath), pitch_deg=-10.0 + 0.8 * breath, yaw_deg=0.0, roll_deg=0.0)
    set_local_euler_deg("Spine", 14.0 - 1.2 * breath, 0.0, 0.0)
    set_local_euler_deg("Chest", 11.0 - 1.5 * breath, 0.0, 0.0)
    set_local_euler_deg("Neck", -4.0 + 0.8 * breath, 0.0, 0.0)
    set_local_euler_deg("Head", -3.0 + 1.8 * breath, 1.5 * math.sin(ang), 2.2 + 1.0 * math.cos(ang))

    aim_two_bone_ik("LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
                    target_world_pos=(0.155, -0.15, 0.115),
                    pole_world_vec=(0.19, -0.22, 1.0))
    aim_two_bone_ik("RightUpperLeg", "RightLowerLeg", "RightFoot",
                    target_world_pos=(-0.155, -0.15, 0.115),
                    pole_world_vec=(-0.19, -0.22, 1.0))

    swing_bone_zero_twist(rig_obj.pose.bones["LeftFoot"], Vector((0.175, -0.35, 0.035)))
    swing_bone_zero_twist(rig_obj.pose.bones["RightFoot"], Vector((-0.175, -0.35, 0.035)))

    aim_two_bone_ik("LeftUpperArm", "LeftLowerArm", "LeftHand",
                    target_world_pos=(0.11, -0.24, 0.44 + 0.006 * breath),
                    pole_world_vec=(0.52, 0.12, -0.32))
    aim_two_bone_ik("RightUpperArm", "RightLowerArm", "RightHand",
                    target_world_pos=(-0.11, -0.25, 0.43 + 0.006 * breath),
                    pole_world_vec=(-0.52, 0.12, -0.32))

    swing_bone_zero_twist(rig_obj.pose.bones["LeftHand"], Vector((-0.03, -0.28, 0.40 + 0.006 * breath)))
    swing_bone_zero_twist(rig_obj.pose.bones["RightHand"], Vector((0.03, -0.29, 0.39 + 0.006 * breath)))

    keyframe_all_bones(f, prev_q)

# Set active action back to Idle at frame 1
rig_obj.animation_data.action = act_idle
if hasattr(rig_obj.animation_data, "action_slot") and len(act_idle.slots) > 0:
    rig_obj.animation_data.action_slot = act_idle.slots[0]
scene.frame_set(1)
bpy.context.view_layer.update()

# ==============================================================================
# 10. EXPORT .FBX (WITH EMBEDDED & LINKED TEXTURES) + .BLEND + STUDIO PREVIEW
# ==============================================================================
blend_path = os.path.join(OUT_DIR, "Chibi_TacticalGirl_Source.blend")
fbx_path = os.path.join(OUT_DIR, "Chibi_TacticalGirl_Rigged.fbx")
preview_path = os.path.join(OUT_DIR, "Chibi_TacticalGirl_Preview.png")
preview_back_path = os.path.join(OUT_DIR, "Chibi_TacticalGirl_Preview_Back.png")

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
print("SUCCESSFULLY EXPORTED AVATAR-STAR CHIBI TACTICAL GIRL FBX AND BLEND:", fbx_path)

# Render crisp studio front-3/4 and back-3/4 previews
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

# Front-3/4 shot
cam_obj.location = Vector((-0.55, -4.35, 1.28))
direction = Vector((0.06, 0.0, 1.18)) - cam_obj.location
cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = preview_path
bpy.ops.render.render(write_still=True)

# Back-3/4 shot
cam_obj.location = Vector((1.65, 3.85, 1.48))
direction = Vector((0.08, 0.0, 1.18)) - cam_obj.location
cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
scene.render.filepath = preview_back_path
bpy.ops.render.render(write_still=True)

print("SUCCESSFULLY RENDERED PREVIEWS:", preview_path, preview_back_path)

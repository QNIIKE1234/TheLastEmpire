import math
import os
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Stylized_US_Houses_Source.blend")
PREVIEW_ROW1_PATH = os.path.join(OUT_DIR, "Stylized_US_Houses_Preview_A.png")
PREVIEW_ROW2_PATH = os.path.join(OUT_DIR, "Stylized_US_Houses_Preview_B.png")

# Clean scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for item in list(collection):
        if item.users == 0:
            collection.remove(item)


def make_material(name, color, roughness=0.90, metallic=0.0, specular=0.02):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if "Specular IOR Level" in shader.inputs:
        shader.inputs["Specular IOR Level"].default_value = specular
    return material


# 100% README_Model_Color_Standard.md Chibi Tactical Matte Palette (Linear RGB for Unity useSRGBMaterialColor=1)
MATS = [
    make_material("Hazard | faded amber", (0.7484, 0.3931, 0.0272), 0.90, 0.0),          # Signature Tactical Girl yellow
    make_material("Titanium | graphite armor", (0.0550, 0.0637, 0.0890), 0.78, 0.08),    # Signature dark slate roof / armor
    make_material("Iron | dark forged steel", (0.0380, 0.0440, 0.0620), 0.78, 0.08),     # Fire escapes, rails, brackets
    make_material("Charcoal | blackened", (0.0220, 0.0245, 0.0331), 0.92, 0.0),          # Deep recesses, vents
    make_material("Titanium | bright bevel", (0.4770, 0.5382, 0.6383), 0.45, 0.55),      # Metal trim, AC louvers, flashing
    make_material("Paint | faded survival blue", (0.0331, 0.0946, 0.3000), 0.88, 0.0),   # Signature blue siding / bay / awning
    make_material("Wood | weathered cedar", (0.3600, 0.2300, 0.1200), 0.88, 0.0),        # Timber framing, rafters, porch
    make_material("Wood | exposed grain", (0.2600, 0.1600, 0.0800), 0.88, 0.0),          # Doors, shutters, deck planks
    make_material("Wood | charred and stained", (0.1200, 0.0700, 0.0350), 0.90, 0.0),    # Dark beams, corbels, roof fascia
    make_material("Wood | cut end", (0.4500, 0.3000, 0.1600), 0.88, 0.0),                # Warm wood highlights
    make_material("Brick | fired clay", (0.3800, 0.1400, 0.0700), 0.88, 0.0),            # American brick & terracotta roof
    make_material("Rust | orange oxide", (0.3200, 0.1200, 0.0400), 0.88, 0.0),           # Warm terracotta tile / rust accents
    make_material("Stone | charcoal gray", (0.0900, 0.1000, 0.1300), 0.88, 0.0),         # Dark stone plinth / coping
    make_material("Mortar | pale sand", (0.1600, 0.1600, 0.1800), 0.88, 0.0),            # Medium stone steps & lintels
    make_material("Concrete | cracked ash", (0.2400, 0.2500, 0.2800), 0.90, 0.0),        # Ash concrete walls
    make_material("Concrete | broken aggregate", (0.3400, 0.3500, 0.3800), 0.90, 0.0),   # Light concrete trim & cornice
    make_material("Stucco | warm plaster", (0.4200, 0.3800, 0.3100), 0.90, 0.0),         # Stylized warm stucco/clapboard
    make_material("Stucco | dusty cream", (0.4700, 0.4550, 0.4150), 0.90, 0.0),          # Bright window frames & siding trim
    make_material("Paint | muted teal", (0.0550, 0.2050, 0.2000), 0.88, 0.0),            # Retro American diner teal
    make_material("Glass | dusty blue", (0.0650, 0.1450, 0.2100), 0.45, 0.0, 0.25),      # Cool glass panes
    make_material("Glass | warm amber", (0.5800, 0.3300, 0.0650), 0.45, 0.0, 0.20),      # Cozy lit interior glass accent
    make_material("Preview | stage floor", (0.1200, 0.1300, 0.1500), 0.95, 0.0),
]

(
    AMBER,
    SLATE_DARK,
    IRON_DARK,
    CHARCOAL,
    SILVER,
    BLUE,
    WOOD_CEDAR,
    WOOD_OAK,
    WOOD_DARK,
    WOOD_LIGHT,
    BRICK,
    TERRA,
    STONE_DARK,
    STONE_MID,
    CONC_ASH,
    CONC_LIGHT,
    STUCCO_WARM,
    TRIM_CREAM,
    TEAL,
    GLASS_BLUE,
    GLASS_WARM,
    PREVIEW_FLOOR,
) = range(len(MATS))


class BuildingBuilder:
    """Stylized Chibi / Avatar-Star-inspired architectural mesh builder."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()

    def _add(self, src, mat_idx, matrix=None):
        if matrix is not None:
            bmesh.ops.transform(src, matrix=matrix, verts=src.verts)
        vmap = {v: self.bm.verts.new(v.co) for v in src.verts}
        for f in src.faces:
            try:
                nf = self.bm.faces.new([vmap[v] for v in f.verts])
                nf.material_index = mat_idx
                nf.smooth = False
            except ValueError:
                pass
        src.free()

    def box(self, center, size, mat_idx, rot=(0.0, 0.0, 0.0), bevel=0.0):
        src = bmesh.new()
        bmesh.ops.create_cube(src, size=1.0)
        sx, sy, sz = size
        for v in src.verts:
            v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        if bevel > 0.0:
            bmesh.ops.bevel(
                src,
                geom=list(src.edges),
                offset=min(bevel, min(sx, sy, sz) * 0.22),
                offset_type="OFFSET",
                segments=1,
                profile=0.5,
                affect="EDGES",
            )
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def flared_box(self, center, size, mat_idx, flare=1.05, bevel=0.025):
        """Avatar-Star-signature gently flared chunky cartoon wall block (wider near eaves)."""
        src = bmesh.new()
        bmesh.ops.create_cube(src, size=1.0)
        sx, sy, sz = size
        for v in src.verts:
            mult = flare if v.co.z > 0.0 else 1.0
            v.co = Vector((v.co.x * sx * mult, v.co.y * sy * mult, v.co.z * sz))
        if bevel > 0.0:
            bmesh.ops.bevel(
                src,
                geom=list(src.edges),
                offset=min(bevel, min(sx, sy, sz) * 0.15),
                offset_type="OFFSET",
                segments=1,
                profile=0.5,
                affect="EDGES",
            )
        self._add(src, mat_idx, Matrix.Translation(Vector(center)))

    def cyl(self, center, radius, height, mat_idx, segs=10, rot=(0.0, 0.0, 0.0)):
        src = bmesh.new()
        bmesh.ops.create_cone(
            src,
            cap_ends=True,
            cap_tris=False,
            segments=segs,
            radius1=radius,
            radius2=radius,
            depth=height,
        )
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def gable_prism(self, base_center, width, depth, height, mat_idx, ridge_along="Y"):
        """Solid triangular gable end fill (X=width, Y=depth, Z=height)."""
        src = bmesh.new()
        hw, hd = width * 0.5, depth * 0.5
        if ridge_along == "Y":
            v0 = src.verts.new(Vector((-hw, -hd, 0.0)))
            v1 = src.verts.new(Vector((hw, -hd, 0.0)))
            v2 = src.verts.new(Vector((0.0, -hd, height)))
            v3 = src.verts.new(Vector((-hw, hd, 0.0)))
            v4 = src.verts.new(Vector((hw, hd, 0.0)))
            v5 = src.verts.new(Vector((0.0, hd, height)))
        else:
            v0 = src.verts.new(Vector((-hw, -hd, 0.0)))
            v1 = src.verts.new(Vector((-hw, hd, 0.0)))
            v2 = src.verts.new(Vector((-hw, 0.0, height)))
            v3 = src.verts.new(Vector((hw, -hd, 0.0)))
            v4 = src.verts.new(Vector((hw, hd, 0.0)))
            v5 = src.verts.new(Vector((hw, 0.0, height)))

        src.faces.new((v0, v1, v2))
        src.faces.new((v4, v3, v5))
        src.faces.new((v0, v3, v4, v1))
        src.faces.new((v1, v4, v5, v2))
        src.faces.new((v3, v0, v2, v5))
        bmesh.ops.recalc_face_normals(src, faces=src.faces)
        self._add(src, mat_idx, Matrix.Translation(Vector(base_center)))

    def pitched_roof(
        self,
        eave_center,
        width,
        depth,
        peak_h,
        roof_mat,
        trim_mat,
        gable_mat,
        overhang_x=0.38,
        overhang_y=0.40,
        thickness=0.16,
        ridge_along="X",
        add_ribs=True,
    ):
        """Chunky stylized pitched roof with gable fill, overhanging slabs, thick bargeboards, ridge cap & tile ribs."""
        ex, ey, ez = eave_center
        self.gable_prism((ex, ey, ez), width, depth, peak_h, gable_mat, ridge_along=ridge_along)

        if ridge_along == "X":
            half_span = depth * 0.5 + overhang_y
            slope_len = math.hypot(half_span, peak_h) + 0.08
            ang = math.atan2(peak_h, half_span)
            mid_y = half_span * 0.5
            mid_z = ez + peak_h * 0.5 + 0.05
            roof_w = width + overhang_x * 2.0

            # Front (-Y) and Back (+Y) roof slopes
            self.box((ex, ey - mid_y, mid_z), (roof_w, slope_len, thickness), roof_mat, rot=(ang, 0.0, 0.0), bevel=0.02)
            self.box((ex, ey + mid_y, mid_z), (roof_w, slope_len, thickness), roof_mat, rot=(-ang, 0.0, 0.0), bevel=0.02)
            # Chunky ridge cap beam
            self.box((ex, ey, ez + peak_h + thickness * 0.52), (roof_w + 0.10, 0.22, 0.18), trim_mat, bevel=0.02)
            # Thick gable fascia / bargeboards on left & right ends
            for sx in (-1.0, 1.0):
                fx = ex + sx * (roof_w * 0.5 - 0.05)
                self.box((fx, ey - mid_y, mid_z - 0.04), (0.14, slope_len + 0.06, thickness + 0.06), trim_mat, rot=(ang, 0.0, 0.0))
                self.box((fx, ey + mid_y, mid_z - 0.04), (0.14, slope_len + 0.06, thickness + 0.06), trim_mat, rot=(-ang, 0.0, 0.0))
            # Subtle shingle ridge courses so roof reads richly from top-down
            if add_ribs:
                for step in (0.28, 0.58, 0.84):
                    ry = half_span * step
                    rz = ez + peak_h * (1.0 - step) + thickness * 0.56
                    self.box((ex, ey - ry, rz), (roof_w - 0.16, 0.09, 0.06), trim_mat)
                    self.box((ex, ey + ry, rz), (roof_w - 0.16, 0.09, 0.06), trim_mat)
        else:
            half_span = width * 0.5 + overhang_x
            slope_len = math.hypot(half_span, peak_h) + 0.08
            ang = math.atan2(peak_h, half_span)
            mid_x = half_span * 0.5
            mid_z = ez + peak_h * 0.5 + 0.05
            roof_d = depth + overhang_y * 2.0

            self.box((ex - mid_x, ey, mid_z), (slope_len, roof_d, thickness), roof_mat, rot=(0.0, -ang, 0.0), bevel=0.02)
            self.box((ex + mid_x, ey, mid_z), (slope_len, roof_d, thickness), roof_mat, rot=(0.0, ang, 0.0), bevel=0.02)
            self.box((ex, ey, ez + peak_h + thickness * 0.52), (0.22, roof_d + 0.10, 0.18), trim_mat, bevel=0.02)
            for sy in (-1.0, 1.0):
                fy = ey + sy * (roof_d * 0.5 - 0.05)
                self.box((ex - mid_x, fy, mid_z - 0.04), (slope_len + 0.06, 0.14, thickness + 0.06), trim_mat, rot=(0.0, -ang, 0.0))
                self.box((ex + mid_x, fy, mid_z - 0.04), (slope_len + 0.06, 0.14, thickness + 0.06), trim_mat, rot=(0.0, ang, 0.0))
            if add_ribs:
                for step in (0.28, 0.58, 0.84):
                    rx = half_span * step
                    rz = ez + peak_h * (1.0 - step) + thickness * 0.56
                    self.box((ex - rx, ey, rz), (0.09, roof_d - 0.16, 0.06), trim_mat)
                    self.box((ex + rx, ey, rz), (0.09, roof_d - 0.16, 0.06), trim_mat)

    def dormer(self, center, width, depth, height, wall_mat, roof_mat, trim_mat, glass_mat, face_dir="-Y"):
        """Cute Avatar-Star-style roof dormer poking out of a pitched roof."""
        cx, cy, cz = center
        self.box((cx, cy, cz + height * 0.5), (width, depth, height), wall_mat, bevel=0.015)
        self.pitched_roof(
            (cx, cy, cz + height),
            width,
            depth,
            height * 0.58,
            roof_mat,
            trim_mat,
            wall_mat,
            overhang_x=0.14,
            overhang_y=0.16,
            thickness=0.09,
            ridge_along="Y" if face_dir in ("-Y", "+Y") else "X",
            add_ribs=False,
        )
        fy = cy - depth * 0.5 if face_dir == "-Y" else cy + depth * 0.5
        self.window((cx, fy, cz + height * 0.52), width * 0.62, height * 0.58, trim_mat, glass_mat, face_dir=face_dir, shutters=None)

    def window(self, center, w, h, frame_mat, glass_mat, face_dir="-Y", shutters=None, sill_mat=None):
        """Deeply framed chunky stylized window with cross-mullions, protruding sill & optional shutters."""
        cx, cy, cz = center
        s_mat = sill_mat if sill_mat is not None else frame_mat
        if face_dir in ("-Y", "+Y"):
            sgn = -1.0 if face_dir == "-Y" else 1.0
            # Outer frame
            self.box((cx, cy + sgn * 0.04, cz), (w, 0.14, h), frame_mat, bevel=0.015)
            # Recessed glass pane
            self.box((cx, cy + sgn * 0.08, cz), (w - 0.18, 0.08, h - 0.18), glass_mat)
            # Cross mullions
            self.box((cx, cy + sgn * 0.11, cz), (0.07, 0.06, h - 0.14), frame_mat)
            self.box((cx, cy + sgn * 0.11, cz), (w - 0.14, 0.06, 0.07), frame_mat)
            # Chunky projecting sill + top lintel
            self.box((cx, cy + sgn * 0.10, cz - h * 0.5 - 0.03), (w + 0.16, 0.22, 0.10), s_mat, bevel=0.015)
            self.box((cx, cy + sgn * 0.08, cz + h * 0.5 + 0.03), (w + 0.14, 0.18, 0.10), s_mat, bevel=0.015)
            if shutters is not None:
                sw = w * 0.34
                for sx in (-1.0, 1.0):
                    self.box((cx + sx * (w * 0.5 + sw * 0.5 + 0.02), cy + sgn * 0.05, cz), (sw, 0.08, h - 0.06), shutters, bevel=0.01)
        else:
            sgn = -1.0 if face_dir == "-X" else 1.0
            self.box((cx + sgn * 0.04, cy, cz), (0.14, w, h), frame_mat, bevel=0.015)
            self.box((cx + sgn * 0.08, cy, cz), (0.08, w - 0.18, h - 0.18), glass_mat)
            self.box((cx + sgn * 0.11, cy, cz), (0.06, 0.07, h - 0.14), frame_mat)
            self.box((cx + sgn * 0.11, cy, cz), (0.06, w - 0.14, 0.07), frame_mat)
            self.box((cx + sgn * 0.10, cy, cz - h * 0.5 - 0.03), (0.22, w + 0.16, 0.10), s_mat, bevel=0.015)
            self.box((cx + sgn * 0.08, cy, cz + h * 0.5 + 0.03), (0.18, w + 0.14, 0.10), s_mat, bevel=0.015)
            if shutters is not None:
                sw = w * 0.34
                for sy in (-1.0, 1.0):
                    self.box((cx + sgn * 0.05, cy + sy * (w * 0.5 + sw * 0.5 + 0.02), cz), (0.08, sw, h - 0.06), shutters, bevel=0.01)

    def door(self, base_center, w, h, frame_mat, panel_mat, step_mat=STONE_MID, face_dir="-Y"):
        """Chunky framed American doorway with recessed door panel, knob & stone threshold step."""
        cx, cy, cz = base_center
        sgn = -1.0 if face_dir == "-Y" else 1.0
        self.box((cx, cy + sgn * 0.05, cz + h * 0.5), (w + 0.22, 0.18, h + 0.14), frame_mat, bevel=0.02)
        self.box((cx, cy + sgn * 0.09, cz + h * 0.48), (w - 0.06, 0.12, h - 0.04), panel_mat, bevel=0.015)
        # Upper door glass inset
        self.box((cx, cy + sgn * 0.13, cz + h * 0.68), (w * 0.54, 0.06, h * 0.26), GLASS_WARM)
        # Silver handle
        self.box((cx + w * 0.30, cy + sgn * 0.17, cz + h * 0.45), (0.07, 0.07, 0.16), SILVER)
        # Stone doorstep
        self.box((cx, cy + sgn * 0.24, cz + 0.09), (w + 0.44, 0.46, 0.18), step_mat, bevel=0.02)

    def bay_window(self, center, w, d, h, body_mat, trim_mat, roof_mat, glass_mat, bracket_mat, face_dir="-Y"):
        """Avatar-Star-signature cantilevered 2nd-floor bay window with diagonal corbel brackets."""
        cx, cy, cz = center
        sgn = -1.0 if face_dir == "-Y" else 1.0
        by = cy + sgn * (d * 0.5)
        # Main protruding bay box
        self.box((cx, by, cz), (w, d, h), body_mat, bevel=0.02)
        # Sloped mini-roof cap on top
        self.box((cx, by + sgn * 0.04, cz + h * 0.5 + 0.12), (w + 0.22, d + 0.20, 0.18), roof_mat, rot=(sgn * -0.22, 0.0, 0.0), bevel=0.02)
        # Bottom wooden ledge
        self.box((cx, by, cz - h * 0.5 - 0.06), (w + 0.16, d + 0.12, 0.12), trim_mat, bevel=0.015)
        # Diagonal corbel brackets underneath
        for sx in (-1.0, 1.0):
            self.box((cx + sx * (w * 0.34), by - sgn * 0.08, cz - h * 0.5 - 0.26), (0.14, d * 0.72, 0.16), bracket_mat, rot=(sgn * 0.68, 0.0, 0.0))
        # Twin windows on bay face
        if w >= 1.5:
            self.window((cx - w * 0.24, cy + sgn * d, cz + 0.02), w * 0.38, h * 0.62, trim_mat, glass_mat, face_dir=face_dir)
            self.window((cx + w * 0.24, cy + sgn * d, cz + 0.02), w * 0.38, h * 0.62, trim_mat, glass_mat, face_dir=face_dir)
        else:
            self.window((cx, cy + sgn * d, cz + 0.02), w * 0.68, h * 0.62, trim_mat, glass_mat, face_dir=face_dir)

    def awning(self, center, w, d, main_mat, stripe_mat, bracket_mat=IRON_DARK, face_dir="-Y"):
        """Chunky American shop awning with alternating color segments and iron diagonal struts."""
        cx, cy, cz = center
        sgn = -1.0 if face_dir == "-Y" else 1.0
        ay = cy + sgn * (d * 0.5)
        segs = 5
        sw = w / segs
        for i in range(segs):
            sx = cx - w * 0.5 + (i + 0.5) * sw
            m = main_mat if i % 2 == 0 else stripe_mat
            self.box((sx, ay, cz), (sw - 0.01, d, 0.12), m, rot=(sgn * -0.34, 0.0, 0.0), bevel=0.015)
            # Scalloped front valance drop
            self.box((sx, cy + sgn * (d - 0.04), cz - d * 0.24), (sw - 0.01, 0.07, 0.18), m)
        for sx in (-1.0, 1.0):
            self.box((cx + sx * (w * 0.44), ay - sgn * 0.06, cz - 0.24), (0.07, d * 0.80, 0.07), bracket_mat, rot=(sgn * 0.52, 0.0, 0.0))

    def chimney(self, base_center, w, h, brick_mat=BRICK, cap_mat=STONE_DARK):
        cx, cy, cz = base_center
        self.box((cx, cy, cz + h * 0.5), (w, w, h), brick_mat, bevel=0.02)
        self.box((cx, cy, cz + h + 0.08), (w + 0.16, w + 0.16, 0.14), cap_mat, bevel=0.02)
        self.box((cx, cy, cz + h + 0.20), (w * 0.52, w * 0.52, 0.16), CHARCOAL)

    def ac_unit(self, base_center, scale=1.0):
        cx, cy, cz = base_center
        self.box((cx, cy, cz + 0.26 * scale), (0.82 * scale, 0.68 * scale, 0.52 * scale), CONC_LIGHT, bevel=0.02)
        self.box((cx, cy, cz + 0.54 * scale), (0.52 * scale, 0.52 * scale, 0.06 * scale), CHARCOAL)
        self.box((cx, cy - 0.35 * scale, cz + 0.26 * scale), (0.58 * scale, 0.04 * scale, 0.32 * scale), IRON_DARK)

    def water_tank(self, base_center, radius=0.58, height=1.25):
        """Classic NYC / American rooftop wooden water tower on steel legs."""
        cx, cy, cz = base_center
        leg_h = 0.65
        for lx in (-0.34, 0.34):
            for ly in (-0.34, 0.34):
                self.box((cx + lx, cy + ly, cz + leg_h * 0.5), (0.10, 0.10, leg_h), IRON_DARK)
        self.box((cx, cy, cz + leg_h), (radius * 2.05, radius * 2.05, 0.10), WOOD_DARK)
        self.cyl((cx, cy, cz + leg_h + height * 0.5), radius, height, WOOD_CEDAR, segs=10)
        # Iron hoops
        for hz in (0.25, 0.55, 0.85):
            self.cyl((cx, cy, cz + leg_h + height * hz), radius + 0.025, 0.05, IRON_DARK, segs=10)
        # Conical dark roof cap
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=10, radius1=radius + 0.12, radius2=0.03, depth=0.45)
        self._add(src, SLATE_DARK, Matrix.Translation(Vector((cx, cy, cz + leg_h + height + 0.22))))

    def timber_frame_band(self, center_z, w, d, mat_idx=WOOD_DARK, thickness=0.14):
        """Horizontal belt beam wrapping all 4 walls."""
        self.box((0.0, 0.0, center_z), (w + 0.10, d + 0.10, thickness), mat_idx, bevel=0.015)

    def corner_posts(self, z0, z1, w, d, mat_idx=WOOD_DARK, post_w=0.18):
        cz = (z0 + z1) * 0.5
        h = z1 - z0
        for sx in (-1.0, 1.0):
            for sy in (-1.0, 1.0):
                self.box((sx * (w * 0.5 + 0.01), sy * (d * 0.5 + 0.01), cz), (post_w, post_w, h), mat_idx, bevel=0.015)

    def parapet_roof(self, z_top, w, d, wall_mat, cap_mat, deck_mat=SLATE_DARK, wall_h=0.44):
        """Stepped American flat roof with recessed roof membrane & thick beveled stone parapet walls."""
        self.box((0.0, 0.0, z_top + 0.05), (w - 0.10, d - 0.10, 0.12), deck_mat)
        # Cornice ledge right beneath parapet
        self.box((0.0, 0.0, z_top - 0.02), (w + 0.26, d + 0.26, 0.18), cap_mat, bevel=0.02)
        t = 0.20
        for sy in (-1.0, 1.0):
            self.box((0.0, sy * (d * 0.5 - t * 0.5), z_top + wall_h * 0.5), (w + 0.04, t, wall_h), wall_mat, bevel=0.015)
            self.box((0.0, sy * (d * 0.5 - t * 0.5), z_top + wall_h + 0.05), (w + 0.14, t + 0.08, 0.10), cap_mat, bevel=0.015)
        for sx in (-1.0, 1.0):
            self.box((sx * (w * 0.5 - t * 0.5), 0.0, z_top + wall_h * 0.5), (t, d - t * 1.8, wall_h), wall_mat, bevel=0.015)
            self.box((sx * (w * 0.5 - t * 0.5), 0.0, z_top + wall_h + 0.05), (t + 0.08, d + 0.10, 0.10), cap_mat, bevel=0.015)

    def hanging_sign(self, attach_pt, out_dir_x=1.0, sign_mat=AMBER, frame_mat=IRON_DARK):
        ax, ay, az = attach_pt
        self.box((ax + out_dir_x * 0.36, ay, az + 0.26), (0.76, 0.07, 0.07), frame_mat)
        self.box((ax + out_dir_x * 0.40, ay, az), (0.58, 0.09, 0.44), sign_mat, bevel=0.015)
        self.box((ax + out_dir_x * 0.40, ay, az), (0.64, 0.06, 0.50), frame_mat)

    def finish(self, location=(0.0, 0.0, 0.0)):
        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        used_mats = sorted({f.material_index for f in self.bm.faces})
        remap = {old_idx: new_idx for new_idx, old_idx in enumerate(used_mats)}
        for f in self.bm.faces:
            f.material_index = remap[f.material_index]
            f.smooth = False
        self.bm.to_mesh(mesh)
        self.bm.free()
        for old_idx in used_mats:
            mesh.materials.append(MATS[old_idx])
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        return obj


# =====================================================================
# 14 HANDCRAFTED STYLIZED AMERICAN HOUSES & SHOPS
# =====================================================================

def make_01_cottage_bungalow(loc):
    """Building_CottageHouse -> American Craftsman Bungalow (3.6 x 3.4m)."""
    b = BuildingBuilder("Stylized_Building_CottageHouse")
    w, d = 3.4, 3.0
    # Chunky stone plinth
    b.box((0.0, 0.0, 0.24), (w + 0.24, d + 0.24, 0.48), STONE_MID, bevel=0.03)
    # Flared warm plaster bungalow body
    b.flared_box((0.0, 0.0, 1.38), (w, d, 1.85), STUCCO_WARM, flare=1.06, bevel=0.03)
    b.corner_posts(0.42, 2.32, w + 0.06, d + 0.06, WOOD_DARK, 0.18)
    b.timber_frame_band(2.26, w + 0.16, d + 0.16, WOOD_CEDAR, 0.14)
    # Steep overhanging terracotta roof + dormer + brick chimney
    b.pitched_roof((0.0, 0.0, 2.30), w + 0.16, d + 0.16, 1.32, TERRA, WOOD_DARK, STUCCO_WARM, overhang_x=0.38, overhang_y=0.42, ridge_along="X")
    b.dormer((0.0, -0.78, 2.72), 1.05, 0.95, 0.72, STUCCO_WARM, TERRA, WOOD_DARK, GLASS_WARM, face_dir="-Y")
    b.chimney((-1.15, 0.55, 2.85), 0.56, 1.25, BRICK, STONE_DARK)
    # Cozy front porch deck + tapered Craftsman columns + amber front door
    b.box((0.0, -1.92, 0.20), (2.45, 0.95, 0.40), WOOD_OAK, bevel=0.02)
    b.box((0.0, -2.42, 0.10), (1.15, 0.35, 0.20), STONE_MID, bevel=0.02)
    for px in (-1.05, 1.05):
        b.box((px, -2.26, 0.62), (0.28, 0.28, 0.52), STONE_MID, bevel=0.02)
        b.box((px, -2.26, 1.46), (0.18, 0.18, 1.22), WOOD_CEDAR, bevel=0.015)
    b.box((0.0, -2.02, 2.12), (2.68, 1.15, 0.16), SLATE_DARK, rot=(-0.18, 0.0, 0.0), bevel=0.02)
    b.door((0.0, -1.52, 0.40), 0.78, 1.48, WOOD_DARK, AMBER, STONE_MID, "-Y")
    b.window((-1.05, -1.52, 1.38), 0.72, 0.76, TRIM_CREAM, GLASS_WARM, "-Y", shutters=BLUE)
    b.window((1.05, -1.52, 1.38), 0.72, 0.76, TRIM_CREAM, GLASS_WARM, "-Y", shutters=BLUE)
    b.window((1.72, 0.0, 1.38), 0.82, 0.78, TRIM_CREAM, GLASS_BLUE, "+X", shutters=WOOD_CEDAR)
    b.window((-1.72, 0.0, 1.38), 0.82, 0.78, TRIM_CREAM, GLASS_BLUE, "-X", shutters=WOOD_CEDAR)
    return b.finish(loc)


def make_02_brooklyn_townhouse(loc):
    """Building_Townhouse -> Classic Brooklyn Brownstone Townhouse (3.4 x 3.4m)."""
    b = BuildingBuilder("Stylized_Building_Townhouse")
    w, d = 3.3, 3.2
    # Heavy brownstone base story + stepped stone stoop stairs
    b.box((0.0, 0.0, 0.55), (w + 0.18, d + 0.18, 1.10), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.55), (w, d, 2.95), BRICK, flare=1.04, bevel=0.03)
    b.timber_frame_band(1.12, w + 0.16, d + 0.16, CONC_LIGHT, 0.16)
    b.timber_frame_band(2.60, w + 0.12, d + 0.12, STONE_MID, 0.14)
    # Chunky stepped Brooklyn cornice + parapet + chimney + AC
    b.parapet_roof(4.02, w + 0.12, d + 0.12, BRICK, CONC_LIGHT, SLATE_DARK, wall_h=0.46)
    b.chimney((-1.0, 0.8, 4.10), 0.56, 0.95, BRICK, STONE_DARK)
    b.ac_unit((0.75, 0.55, 4.12), 0.85)
    # Iconic front stoop stairs + iron railings
    for i in range(4):
        b.box((-0.78, -1.78 - i * 0.22, 0.68 - i * 0.18), (0.98, 0.26, 0.18), CONC_LIGHT, bevel=0.015)
    for rx in (-1.24, -0.32):
        b.box((rx, -2.12, 0.82), (0.07, 0.95, 0.07), IRON_DARK, rot=(-0.55, 0.0, 0.0))
        b.box((rx, -2.48, 0.40), (0.07, 0.07, 0.62), IRON_DARK)
    b.door((-0.78, -1.62, 0.82), 0.78, 1.48, STONE_DARK, WOOD_OAK, CONC_LIGHT, "-Y")
    # Protruding 2nd-floor bay window on right side
    b.bay_window((0.68, -1.62, 3.15), 1.42, 0.52, 1.12, SLATE_DARK, TRIM_CREAM, SLATE_DARK, GLASS_WARM, IRON_DARK, "-Y")
    b.window((0.68, -1.64, 1.75), 0.86, 0.96, TRIM_CREAM, GLASS_BLUE, "-Y", sill_mat=CONC_LIGHT)
    b.window((-0.78, -1.64, 3.15), 0.82, 0.96, TRIM_CREAM, GLASS_BLUE, "-Y", sill_mat=CONC_LIGHT)
    for side in ("-X", "+X"):
        sx = -1.68 if side == "-X" else 1.68
        b.window((sx, 0.0, 3.15), 0.84, 0.94, TRIM_CREAM, GLASS_BLUE, side, sill_mat=CONC_LIGHT)
    return b.finish(loc)


def make_03_mainstreet_shop(loc):
    """Building_CornerShop -> American Main Street Corner Deli / Shop (3.8 x 3.2m)."""
    b = BuildingBuilder("Stylized_Building_CornerShop")
    w, d = 3.7, 3.1
    b.box((0.0, 0.0, 0.26), (w + 0.18, d + 0.18, 0.52), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.88), (w, d, 2.75), STUCCO_WARM, flare=1.04, bevel=0.03)
    # Accent brick pilasters on corners
    b.corner_posts(0.50, 3.28, w + 0.08, d + 0.08, BRICK, 0.26)
    b.parapet_roof(3.26, w + 0.14, d + 0.14, BRICK, CONC_LIGHT, SLATE_DARK, wall_h=0.52)
    # Raised central shopfront signboard crest
    b.box((0.0, -1.52, 3.84), (2.45, 0.26, 0.56), AMBER, bevel=0.02)
    b.box((0.0, -1.52, 4.14), (2.60, 0.34, 0.12), CONC_LIGHT, bevel=0.02)
    b.ac_unit((0.85, 0.45, 3.38), 0.95)
    b.cyl((-1.05, 0.65, 3.65), 0.18, 0.72, SILVER, segs=8)
    # Striped survival-blue & amber shop awning over wide storefront window
    b.awning((0.0, -1.58, 2.15), 3.35, 0.92, BLUE, AMBER, IRON_DARK, "-Y")
    b.door((-1.05, -1.56, 0.24), 0.84, 1.60, IRON_DARK, WOOD_CEDAR, STONE_MID, "-Y")
    # Big corner display storefront window
    b.window((0.68, -1.58, 1.18), 1.65, 1.12, IRON_DARK, GLASS_WARM, "-Y", sill_mat=CONC_LIGHT)
    # Cute hanging amber shop sign on right wall
    b.hanging_sign((1.88, -0.80, 2.38), out_dir_x=1.0, sign_mat=AMBER, frame_mat=IRON_DARK)
    b.window((1.88, 0.25, 1.55), 1.05, 0.95, TRIM_CREAM, GLASS_BLUE, "+X")
    # Side wooden supply crate
    b.box((-2.14, -0.75, 0.32), (0.56, 0.56, 0.64), WOOD_CEDAR, bevel=0.02)
    return b.finish(loc)


def make_04_narrow_rowhouse(loc):
    """Building_CanalHouse -> Tall 3-Story East Coast Narrow Brick Rowhouse (3.6 x 3.8m)."""
    b = BuildingBuilder("Stylized_Building_CanalHouse")
    w, d = 3.5, 3.6
    b.box((0.0, 0.0, 0.42), (w + 0.20, d + 0.20, 0.84), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 3.05), (w, d, 4.45), BRICK, flare=1.04, bevel=0.03)
    b.timber_frame_band(2.25, w + 0.16, d + 0.16, CONC_LIGHT, 0.15)
    b.timber_frame_band(3.75, w + 0.18, d + 0.18, CONC_LIGHT, 0.15)
    # Iconic Avatar-Star stepped gable roof facade + slate pitch behind
    b.pitched_roof((0.0, 0.0, 5.26), w + 0.14, d + 0.14, 1.15, SLATE_DARK, CONC_LIGHT, BRICK, overhang_x=0.22, overhang_y=0.28, ridge_along="Y")
    # Stepped front masonry gable crown
    b.box((0.0, -1.82, 5.55), (2.75, 0.26, 0.62), BRICK, bevel=0.02)
    b.box((0.0, -1.82, 6.08), (1.72, 0.26, 0.52), BRICK, bevel=0.02)
    b.box((0.0, -1.82, 6.38), (1.88, 0.34, 0.12), CONC_LIGHT, bevel=0.02)
    b.chimney((1.12, 0.85, 5.45), 0.54, 1.15, BRICK, STONE_DARK)
    # Signature cantilevered survival-blue bay window on floor 2 (like Ref Image 1!)
    b.bay_window((0.0, -1.82, 3.00), 1.95, 0.62, 1.18, BLUE, TRIM_CREAM, SLATE_DARK, GLASS_WARM, WOOD_DARK, "-Y")
    b.door((-0.82, -1.82, 0.18), 0.82, 1.62, CONC_LIGHT, AMBER, STONE_MID, "-Y")
    b.window((0.82, -1.84, 1.32), 0.88, 0.96, TRIM_CREAM, GLASS_BLUE, "-Y", sill_mat=CONC_LIGHT)
    b.window((-0.78, -1.84, 4.52), 0.80, 0.92, TRIM_CREAM, GLASS_BLUE, "-Y", shutters=BLUE)
    b.window((0.78, -1.84, 4.52), 0.80, 0.92, TRIM_CREAM, GLASS_BLUE, "-Y", shutters=BLUE)
    return b.finish(loc)


def make_05_timber_lodge(loc):
    """Building_AlpineChalet -> American Mountain Timber Lodge (4.3 x 3.9m)."""
    b = BuildingBuilder("Stylized_Building_AlpineChalet")
    w, d = 4.1, 3.6
    # Heavy stone ground story
    b.box((0.0, 0.0, 0.72), (w + 0.16, d + 0.16, 1.44), STONE_MID, bevel=0.03)
    # Overhanging timber 2nd floor
    b.flared_box((0.0, 0.0, 2.24), (w + 0.18, d + 0.18, 1.62), WOOD_CEDAR, flare=1.06, bevel=0.03)
    b.corner_posts(1.42, 3.08, w + 0.24, d + 0.24, WOOD_DARK, 0.22)
    b.timber_frame_band(1.44, w + 0.30, d + 0.30, WOOD_DARK, 0.18)
    # Steep overhanging slate lodge roof + rafters + stone chimney
    b.pitched_roof((0.0, 0.0, 3.06), w + 0.30, d + 0.30, 1.48, SLATE_DARK, WOOD_DARK, WOOD_OAK, overhang_x=0.45, overhang_y=0.48, ridge_along="Y")
    b.chimney((-1.55, 0.45, 3.15), 0.68, 1.65, STONE_DARK, CONC_LIGHT)
    # Front wooden balcony overhanging doorway
    b.box((0.0, -2.18, 1.56), (2.75, 0.72, 0.16), WOOD_DARK, bevel=0.02)
    b.box((0.0, -2.50, 2.02), (2.75, 0.10, 0.10), WOOD_LIGHT)
    for bx in (-1.28, -0.64, 0.0, 0.64, 1.28):
        b.box((bx, -2.50, 1.80), (0.10, 0.10, 0.44), WOOD_DARK)
    b.door((0.0, -1.86, 0.14), 0.88, 1.32, WOOD_DARK, AMBER, STONE_DARK, "-Y")
    b.window((-1.28, -1.88, 0.88), 0.78, 0.72, WOOD_DARK, GLASS_WARM, "-Y")
    b.window((1.28, -1.88, 0.88), 0.78, 0.72, WOOD_DARK, GLASS_WARM, "-Y")
    b.window((-0.92, -1.94, 2.34), 0.82, 0.82, TRIM_CREAM, GLASS_WARM, "-Y")
    b.window((0.92, -1.94, 2.34), 0.82, 0.82, TRIM_CREAM, GLASS_WARM, "-Y")
    return b.finish(loc)


def make_06_suburban_a(loc):
    """House_Suburban_A -> American Suburban Family Clapboard Home (5.8 x 5.0m)."""
    b = BuildingBuilder("Stylized_House_Suburban_A")
    w, d = 5.6, 4.6
    b.box((0.0, 0.0, 0.28), (w + 0.24, d + 0.24, 0.56), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.72), (w, d, 2.35), STUCCO_WARM, flare=1.05, bevel=0.03)
    # Clapboard trim corner boards & eave band
    b.corner_posts(0.52, 2.92, w + 0.08, d + 0.08, TRIM_CREAM, 0.20)
    b.timber_frame_band(2.86, w + 0.22, d + 0.22, TRIM_CREAM, 0.16)
    b.pitched_roof((0.0, 0.0, 2.90), w + 0.22, d + 0.22, 1.55, TERRA, WOOD_DARK, STUCCO_WARM, overhang_x=0.42, overhang_y=0.46, ridge_along="X")
    b.dormer((-1.25, -1.15, 3.35), 1.15, 1.05, 0.80, STUCCO_WARM, TERRA, TRIM_CREAM, GLASS_WARM, "-Y")
    b.dormer((1.25, -1.15, 3.35), 1.15, 1.05, 0.80, STUCCO_WARM, TERRA, TRIM_CREAM, GLASS_WARM, "-Y")
    b.chimney((-2.05, 0.95, 3.45), 0.68, 1.45, BRICK, STONE_DARK)
    # Full American front porch with 3 tapered pillars & railing
    b.box((1.05, -2.92, 0.24), (3.15, 1.22, 0.48), WOOD_OAK, bevel=0.02)
    b.box((1.05, -3.62, 0.12), (1.30, 0.38, 0.24), STONE_MID, bevel=0.02)
    for px in (-0.38, 1.05, 2.48):
        b.box((px, -3.40, 0.72), (0.30, 0.30, 0.54), STONE_MID, bevel=0.02)
        b.box((px, -3.40, 1.66), (0.18, 0.18, 1.38), TRIM_CREAM, bevel=0.015)
    b.box((1.05, -3.00, 2.42), (3.42, 1.45, 0.16), SLATE_DARK, rot=(-0.18, 0.0, 0.0), bevel=0.02)
    b.door((1.05, -2.32, 0.46), 0.92, 1.72, TRIM_CREAM, AMBER, STONE_MID, "-Y")
    b.window((-1.35, -2.34, 1.62), 1.25, 1.05, TRIM_CREAM, GLASS_WARM, "-Y", shutters=BLUE)
    b.window((2.05, -2.34, 1.62), 0.82, 0.95, TRIM_CREAM, GLASS_BLUE, "-Y")
    for side in ("-X", "+X"):
        sx = -2.84 if side == "-X" else 2.84
        b.window((sx, -0.65, 1.62), 0.95, 0.95, TRIM_CREAM, GLASS_BLUE, side, shutters=BLUE)
        b.window((sx, 0.85, 1.62), 0.95, 0.95, TRIM_CREAM, GLASS_BLUE, side, shutters=BLUE)
    return b.finish(loc)


def make_07_suburban_b(loc):
    """House_Suburban_B -> American 2-Story Colonial Suburban House (6.6 x 5.2m)."""
    b = BuildingBuilder("Stylized_House_Suburban_B")
    w, d = 6.4, 5.0
    b.box((0.0, 0.0, 0.30), (w + 0.24, d + 0.24, 0.60), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.58), (w, d, 4.02), BLUE, flare=1.04, bevel=0.03)
    b.corner_posts(0.56, 4.62, w + 0.10, d + 0.10, TRIM_CREAM, 0.22)
    b.timber_frame_band(2.58, w + 0.20, d + 0.20, TRIM_CREAM, 0.16)
    b.timber_frame_band(4.56, w + 0.26, d + 0.26, TRIM_CREAM, 0.16)
    b.pitched_roof((0.0, 0.0, 4.60), w + 0.26, d + 0.26, 1.62, SLATE_DARK, TRIM_CREAM, BLUE, overhang_x=0.42, overhang_y=0.44, ridge_along="X")
    b.dormer((0.0, -1.25, 5.05), 1.30, 1.15, 0.85, STUCCO_WARM, SLATE_DARK, TRIM_CREAM, GLASS_WARM, "-Y")
    b.chimney((-2.25, 1.10, 5.10), 0.72, 1.45, BRICK, STONE_DARK)
    # Cantilevered 2nd-floor bay/balcony + front door + side paneled garage door
    b.bay_window((-1.55, -2.54, 3.55), 1.85, 0.58, 1.18, STUCCO_WARM, TRIM_CREAM, SLATE_DARK, GLASS_WARM, WOOD_DARK, "-Y")
    b.door((1.65, -2.52, 0.32), 0.94, 1.82, TRIM_CREAM, AMBER, STONE_MID, "-Y")
    b.awning((1.65, -2.56, 2.38), 1.65, 0.78, AMBER, WOOD_DARK, IRON_DARK, "-Y")
    b.window((-1.55, -2.54, 1.55), 1.25, 1.05, TRIM_CREAM, GLASS_BLUE, "-Y", shutters=SLATE_DARK)
    b.window((0.10, -2.54, 1.55), 0.95, 1.05, TRIM_CREAM, GLASS_BLUE, "-Y")
    b.window((1.65, -2.54, 3.55), 1.05, 1.05, TRIM_CREAM, GLASS_WARM, "-Y", shutters=SLATE_DARK)
    # Garage door on back/side (+Y)
    b.box((1.55, 2.54, 1.22), (2.25, 0.18, 2.15), TRIM_CREAM, bevel=0.02)
    for gz in (0.65, 1.18, 1.72):
        b.box((1.55, 2.64, gz), (1.95, 0.05, 0.08), STONE_DARK)
    return b.finish(loc)


def make_08_suburban_c(loc):
    """House_Suburban_C -> American Mid-Century Ranch House (7.6 x 5.2m)."""
    b = BuildingBuilder("Stylized_House_Suburban_C")
    w, d = 7.4, 4.9
    # Stone wainscoting base
    b.box((0.0, 0.0, 0.45), (w + 0.18, d + 0.18, 0.90), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.98), (w, d, 2.22), CONC_LIGHT, flare=1.04, bevel=0.03)
    # Wide overhanging Mid-Century flat fascia roof with timber soffit
    b.box((0.0, 0.0, 3.22), (w + 1.05, d + 0.95, 0.34), SLATE_DARK, bevel=0.03)
    b.box((0.0, 0.0, 3.04), (w + 0.95, d + 0.85, 0.10), WOOD_CEDAR)
    b.ac_unit((-2.25, 0.85, 3.40), 1.05)
    b.chimney((0.25, 1.25, 3.36), 0.72, 0.95, BRICK, STONE_DARK)
    # 2-car paneled garage door on right
    b.box((2.15, -2.48, 1.18), (2.65, 0.18, 2.12), TRIM_CREAM, bevel=0.02)
    for gz in (0.62, 1.14, 1.66):
        b.box((2.15, -2.58, gz), (2.35, 0.05, 0.08), SLATE_DARK)
    # Amber front door + teal porch awning + big picture windows
    b.door((-0.25, -2.48, 0.18), 0.94, 1.92, WOOD_DARK, AMBER, STONE_DARK, "-Y")
    b.awning((-2.15, -2.50, 2.52), 2.85, 0.88, TEAL, TRIM_CREAM, IRON_DARK, "-Y")
    b.window((-1.65, -2.50, 1.62), 1.15, 1.12, TRIM_CREAM, GLASS_WARM, "-Y")
    b.window((-2.95, -2.50, 1.62), 1.15, 1.12, TRIM_CREAM, GLASS_BLUE, "-Y")
    return b.finish(loc)


def make_09_apoc_brick_rowhouse(loc):
    """Apoc_House_01_Brick_Rowhouse -> Stylized American Weathered Brick Duplex (5.2 x 4.8m)."""
    b = BuildingBuilder("Stylized_Apoc_House_01_Brick_Rowhouse")
    w, d = 5.0, 4.6
    b.box((0.0, 0.0, 0.42), (w + 0.22, d + 0.22, 0.84), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.68), (w, d, 3.72), BRICK, flare=1.04, bevel=0.03)
    b.corner_posts(0.80, 4.58, w + 0.12, d + 0.12, CONC_ASH, 0.28)
    b.timber_frame_band(2.65, w + 0.18, d + 0.18, CONC_LIGHT, 0.16)
    b.parapet_roof(4.54, w + 0.18, d + 0.18, BRICK, CONC_LIGHT, SLATE_DARK, wall_h=0.52)
    b.water_tank((1.25, 0.85, 4.62), radius=0.56, height=1.15)
    b.ac_unit((-1.25, 0.65, 4.62), 1.0)
    # Steel fire-escape balcony on front 2nd floor
    b.box((0.95, -2.62, 2.75), (2.25, 0.72, 0.10), IRON_DARK)
    b.box((0.95, -2.96, 3.22), (2.25, 0.07, 0.07), IRON_DARK)
    for rx in (-0.12, 0.42, 0.95, 1.48, 2.02):
        b.box((rx, -2.96, 2.98), (0.06, 0.06, 0.48), IRON_DARK)
    b.door((-1.15, -2.34, 0.18), 0.95, 1.85, STONE_DARK, WOOD_OAK, CONC_ASH, "-Y")
    b.box((-1.15, -2.42, 2.22), (1.35, 0.24, 0.18), AMBER, bevel=0.02)
    b.window((0.95, -2.36, 1.55), 1.22, 1.05, STONE_MID, GLASS_BLUE, "-Y")
    b.window((-1.15, -2.36, 3.55), 1.05, 1.05, STONE_MID, GLASS_WARM, "-Y")
    b.window((0.95, -2.36, 3.55), 1.22, 1.05, STONE_MID, GLASS_BLUE, "-Y")
    return b.finish(loc)


def make_10_apoc_concrete_duplex(loc):
    """Apoc_House_02_Concrete_Duplex -> Stylized American Concrete Duplex (4.8 x 4.6m)."""
    b = BuildingBuilder("Stylized_Apoc_House_02_Concrete_Duplex")
    w, d = 4.6, 4.4
    b.box((0.0, 0.0, 0.38), (w + 0.22, d + 0.22, 0.76), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.58), (w, d, 3.68), CONC_ASH, flare=1.04, bevel=0.03)
    b.corner_posts(0.72, 4.46, w + 0.10, d + 0.10, SLATE_DARK, 0.24)
    b.timber_frame_band(2.54, w + 0.22, d + 0.22, SLATE_DARK, 0.18)
    b.parapet_roof(4.42, w + 0.16, d + 0.16, CONC_ASH, SLATE_DARK, CHARCOAL, wall_h=0.48)
    b.ac_unit((-1.0, 0.7, 4.52), 1.05)
    # Rooftop skylight housing
    b.box((0.95, 0.25, 4.78), (1.35, 1.25, 0.56), SLATE_DARK, bevel=0.02)
    b.box((0.95, -0.38, 4.82), (0.92, 0.08, 0.36), GLASS_BLUE)
    # Protruding cantilevered concrete balcony + steel rail
    b.box((0.0, -2.58, 2.66), (2.65, 0.76, 0.18), CONC_LIGHT, bevel=0.02)
    b.box((0.0, -2.92, 3.16), (2.65, 0.08, 0.08), IRON_DARK)
    for rx in (-1.25, -0.62, 0.0, 0.62, 1.25):
        b.box((rx, -2.92, 2.92), (0.06, 0.06, 0.46), IRON_DARK)
    b.door((0.0, -2.24, 0.18), 0.96, 1.85, SLATE_DARK, AMBER, STONE_MID, "-Y")
    b.window((-1.35, -2.26, 1.55), 0.96, 0.96, SLATE_DARK, GLASS_BLUE, "-Y")
    b.window((1.35, -2.26, 1.55), 0.96, 0.96, SLATE_DARK, GLASS_BLUE, "-Y")
    b.window((-1.05, -2.26, 3.48), 1.05, 1.02, SLATE_DARK, GLASS_WARM, "-Y")
    b.window((1.05, -2.26, 3.48), 1.05, 1.02, SLATE_DARK, GLASS_BLUE, "-Y")
    return b.finish(loc)


def make_11_apoc_tin_shack(loc):
    """Apoc_House_03_Salvaged_Tin_Shack -> Stylized American Roadside Garage / Timber Outpost (4.2 x 3.6m)."""
    b = BuildingBuilder("Stylized_Apoc_House_03_Salvaged_Tin_Shack")
    w, d = 4.0, 3.4
    b.box((0.0, 0.0, 0.24), (w + 0.24, d + 0.24, 0.48), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.52), (w, d, 2.12), WOOD_OAK, flare=1.05, bevel=0.03)
    b.corner_posts(0.45, 2.60, w + 0.08, d + 0.08, WOOD_DARK, 0.20)
    # Slanted overhanging corrugated steel shed roof
    b.box((0.0, 0.0, 2.84), (w + 0.72, d + 0.85, 0.18), SLATE_DARK, rot=(-0.18, 0.0, 0.0), bevel=0.02)
    for rx in (-1.6, -0.8, 0.0, 0.8, 1.6):
        b.box((rx, 0.0, 2.95), (0.10, d + 0.75, 0.07), IRON_DARK, rot=(-0.18, 0.0, 0.0))
    # Bold roadside amber sign above doorway
    b.box((0.0, -1.82, 2.48), (2.45, 0.18, 0.46), AMBER, bevel=0.02)
    b.door((-0.95, -1.72, 0.18), 0.86, 1.65, WOOD_DARK, SLATE_DARK, STONE_MID, "-Y")
    # Roll-up metal garage shutter on right
    b.box((0.88, -1.74, 1.12), (1.42, 0.14, 1.68), CONC_ASH, bevel=0.02)
    for gz in (0.62, 0.96, 1.30, 1.64):
        b.box((0.88, -1.82, gz), (1.28, 0.05, 0.06), IRON_DARK)
    # Roadside rusty barrels beside wall
    b.cyl((2.36, -0.85, 0.44), 0.28, 0.88, TERRA, segs=10)
    b.cyl((2.36, -0.18, 0.44), 0.28, 0.88, AMBER, segs=10)
    return b.finish(loc)


def make_12_apoc_corner_shop(loc):
    """Apoc_House_04_Ruined_Corner_Shop -> Stylized American Retro Corner Diner / Market (5.6 x 4.4m)."""
    b = BuildingBuilder("Stylized_Apoc_House_04_Ruined_Corner_Shop")
    w, d = 5.4, 4.2
    b.box((0.0, 0.0, 0.34), (w + 0.22, d + 0.22, 0.68), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.02), (w, d, 2.75), TEAL, flare=1.04, bevel=0.03)
    b.corner_posts(0.65, 3.42, w + 0.10, d + 0.10, CONC_LIGHT, 0.26)
    b.parapet_roof(3.38, w + 0.18, d + 0.18, TEAL, CONC_LIGHT, SLATE_DARK, wall_h=0.56)
    # Retro stepped diner crest sign in signature Tactical Amber
    b.box((0.0, -2.08, 4.02), (3.45, 0.28, 0.62), AMBER, bevel=0.02)
    b.box((0.0, -2.08, 4.38), (3.62, 0.36, 0.14), CONC_LIGHT, bevel=0.02)
    b.ac_unit((-1.45, 0.75, 3.50), 1.05)
    b.ac_unit((1.35, 0.85, 3.50), 0.90)
    # Wide amber-and-slate diner canopy
    b.awning((0.0, -2.14, 2.38), 4.95, 1.05, AMBER, SLATE_DARK, IRON_DARK, "-Y")
    b.door((0.0, -2.12, 0.18), 1.02, 1.88, IRON_DARK, WOOD_CEDAR, STONE_MID, "-Y")
    b.window((-1.62, -2.14, 1.32), 1.48, 1.18, IRON_DARK, GLASS_WARM, "-Y", sill_mat=CONC_LIGHT)
    b.window((1.62, -2.14, 1.32), 1.48, 1.18, IRON_DARK, GLASS_WARM, "-Y", sill_mat=CONC_LIGHT)
    b.hanging_sign((2.76, -1.10, 2.65), out_dir_x=1.0, sign_mat=AMBER, frame_mat=IRON_DARK)
    return b.finish(loc)


def make_13_apoc_suburban_home(loc):
    """Apoc_House_05_Collapsed_Suburban_Home -> Stylized American Two-Story Gable House (5.4 x 4.8m)."""
    b = BuildingBuilder("Stylized_Apoc_House_05_Collapsed_Suburban_Home")
    w, d = 5.2, 4.5
    b.box((0.0, 0.0, 0.34), (w + 0.24, d + 0.24, 0.68), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 2.15), (w, d, 3.05), CONC_ASH, flare=1.05, bevel=0.03)
    b.corner_posts(0.65, 3.70, w + 0.10, d + 0.10, WOOD_DARK, 0.22)
    b.timber_frame_band(2.22, w + 0.20, d + 0.20, WOOD_CEDAR, 0.16)
    b.pitched_roof((0.0, 0.0, 3.68), w + 0.22, d + 0.22, 1.52, TERRA, WOOD_DARK, CONC_ASH, overhang_x=0.40, overhang_y=0.44, ridge_along="X")
    b.chimney((1.85, 0.85, 4.15), 0.68, 1.35, BRICK, STONE_DARK)
    b.bay_window((-1.25, -2.30, 2.95), 1.65, 0.56, 1.05, WOOD_CEDAR, TRIM_CREAM, SLATE_DARK, GLASS_WARM, WOOD_DARK, "-Y")
    b.door((1.15, -2.28, 0.18), 0.92, 1.78, WOOD_DARK, WOOD_OAK, STONE_MID, "-Y")
    b.awning((1.15, -2.32, 2.15), 1.52, 0.72, SLATE_DARK, AMBER, IRON_DARK, "-Y")
    b.window((-1.25, -2.30, 1.35), 1.15, 0.95, TRIM_CREAM, GLASS_BLUE, "-Y", shutters=WOOD_DARK)
    b.window((1.15, -2.30, 2.95), 0.96, 0.96, TRIM_CREAM, GLASS_BLUE, "-Y")
    return b.finish(loc)


def make_14_american_corner_loft(loc):
    """building.prefab -> Stylized American 3-Story Brick & Stone Corner Loft (5.0 x 5.0m)."""
    b = BuildingBuilder("Stylized_Building_Block")
    w, d = 4.8, 4.8
    # Heavy stone ground arcade
    b.box((0.0, 0.0, 0.95), (w + 0.20, d + 0.20, 1.90), STONE_MID, bevel=0.03)
    # Brick upper stories
    b.flared_box((0.0, 0.0, 3.55), (w, d, 3.40), BRICK, flare=1.03, bevel=0.03)
    b.corner_posts(1.88, 5.28, w + 0.12, d + 0.12, CONC_LIGHT, 0.28)
    b.timber_frame_band(1.92, w + 0.26, d + 0.26, CONC_LIGHT, 0.20)
    b.timber_frame_band(3.62, w + 0.16, d + 0.16, CONC_ASH, 0.14)
    b.parapet_roof(5.24, w + 0.18, d + 0.18, BRICK, CONC_LIGHT, SLATE_DARK, wall_h=0.56)
    b.water_tank((-1.15, 0.95, 5.34), radius=0.64, height=1.35)
    b.ac_unit((1.15, 0.85, 5.34), 1.05)
    # Shop awning + recessed doorway + fire escape
    b.awning((0.0, -2.52, 1.82), 4.15, 0.85, BLUE, AMBER, IRON_DARK, "-Y")
    b.door((0.0, -2.50, 0.14), 1.05, 1.62, IRON_DARK, AMBER, STONE_DARK, "-Y")
    for wx in (-1.35, 0.0, 1.35):
        b.window((wx, -2.44, 2.75), 0.92, 0.96, TRIM_CREAM, GLASS_WARM if wx == 0 else GLASS_BLUE, "-Y", sill_mat=CONC_LIGHT)
        b.window((wx, -2.44, 4.38), 0.92, 0.96, TRIM_CREAM, GLASS_BLUE, "-Y", sill_mat=CONC_LIGHT)
    for side in ("-X", "+X"):
        sx = -2.44 if side == "-X" else 2.44
        for wy in (-1.15, 1.15):
            b.window((sx, wy, 2.75), 0.92, 0.96, TRIM_CREAM, GLASS_BLUE, side)
            b.window((sx, wy, 4.38), 0.92, 0.96, TRIM_CREAM, GLASS_BLUE, side)
    return b.finish(loc)


# Place all 14 buildings in 2 clean lineup rows (7 per row) for preview rendering
BUILDINGS_ROW_A = [
    make_01_cottage_bungalow((-18.0, 0.0, 0.0)),
    make_02_brooklyn_townhouse((-12.0, 0.0, 0.0)),
    make_03_mainstreet_shop((-6.0, 0.0, 0.0)),
    make_04_narrow_rowhouse((0.0, 0.0, 0.0)),
    make_05_timber_lodge((6.2, 0.0, 0.0)),
    make_06_suburban_a((13.0, 0.0, 0.0)),
    make_07_suburban_b((20.8, 0.0, 0.0)),
]

BUILDINGS_ROW_B = [
    make_08_suburban_c((-20.0, 12.0, 0.0)),
    make_09_apoc_brick_rowhouse((-12.0, 12.0, 0.0)),
    make_10_apoc_concrete_duplex((-5.5, 12.0, 0.0)),
    make_11_apoc_tin_shack((0.5, 12.0, 0.0)),
    make_12_apoc_corner_shop((7.0, 12.0, 0.0)),
    make_13_apoc_suburban_home((14.0, 12.0, 0.0)),
    make_14_american_corner_loft((21.0, 12.0, 0.0)),
]

ALL_BUILDINGS = BUILDINGS_ROW_A + BUILDINGS_ROW_B

# Export every single building as an upright individual FBX with clean pivot at (0, 0, 0)
for obj in ALL_BUILDINGS:
    orig_loc = obj.location.copy()
    obj.location = (0.0, 0.0, 0.0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    fbx_path = os.path.join(OUT_DIR, obj.name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        bake_anim=False,
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="AUTO",
    )
    obj.location = orig_loc
    print("Exported:", fbx_path)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


# Preview floor slab
floor_bm = bmesh.new()
bmesh.ops.create_cube(floor_bm, size=1.0)
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((1.0, 6.0, -0.15)) @ Matrix.Diagonal((54.0, 26.0, 0.30, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_FLOOR])
floor_obj = bpy.data.objects.new("Preview_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

bpy.ops.object.camera_add(location=(2.0, -26.0, 12.5))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 48.0
point_at(camera, (1.5, 0.0, 2.2))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(12.0, -18.0, 20.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.94, 0.82)
sun.data.energy = 3.0
sun.data.angle = math.radians(10.0)
point_at(sun, (0.0, 5.0, 2.0))

bpy.ops.object.light_add(type="AREA", location=(-16.0, -14.0, 14.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 1200.0
fill.data.size = 18.0
point_at(fill, (0.0, 5.0, 2.0))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 2000
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.14, 0.17, 0.22)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# Render Row A (7 houses: Cottage..Suburban_B)
for o in BUILDINGS_ROW_B:
    o.hide_render = True
scene.render.filepath = PREVIEW_ROW1_PATH
bpy.ops.render.render(write_still=True)

# Render Row B (7 houses: Suburban_C..CornerLoft)
for o in BUILDINGS_ROW_A:
    o.hide_render = True
for o in BUILDINGS_ROW_B:
    o.hide_render = False
camera.location = (1.0, -14.0, 12.5)
point_at(camera, (0.5, 12.0, 2.2))
scene.render.filepath = PREVIEW_ROW2_PATH
bpy.ops.render.render(write_still=True)

for o in ALL_BUILDINGS:
    o.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("All 14 Stylized American Houses & Shops generated successfully!")

import math
import os
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Stylized_US_Midrises_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Stylized_US_Midrises_Preview.png")

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


# 100% README_Model_Color_Standard.md Matte Chibi Tactical Palette
MATS = [
    make_material("Hazard | faded amber", (0.7484, 0.3931, 0.0272), 0.90, 0.0),
    make_material("Titanium | graphite armor", (0.0550, 0.0637, 0.0890), 0.78, 0.08),
    make_material("Iron | dark forged steel", (0.0380, 0.0440, 0.0620), 0.78, 0.08),
    make_material("Charcoal | blackened", (0.0220, 0.0245, 0.0331), 0.92, 0.0),
    make_material("Titanium | bright bevel", (0.4770, 0.5382, 0.6383), 0.45, 0.55),
    make_material("Paint | faded survival blue", (0.0331, 0.0946, 0.3000), 0.88, 0.0),
    make_material("Wood | weathered cedar", (0.3600, 0.2300, 0.1200), 0.88, 0.0),
    make_material("Wood | charred and stained", (0.1200, 0.0700, 0.0350), 0.90, 0.0),
    make_material("Brick | fired clay", (0.3800, 0.1400, 0.0700), 0.88, 0.0),
    make_material("Rust | orange oxide", (0.3200, 0.1200, 0.0400), 0.88, 0.0),
    make_material("Stone | charcoal gray", (0.0900, 0.1000, 0.1300), 0.88, 0.0),
    make_material("Mortar | pale sand", (0.1600, 0.1600, 0.1800), 0.88, 0.0),
    make_material("Concrete | cracked ash", (0.2400, 0.2500, 0.2800), 0.90, 0.0),
    make_material("Concrete | broken aggregate", (0.3400, 0.3500, 0.3800), 0.90, 0.0),
    make_material("Stucco | warm plaster", (0.4200, 0.3800, 0.3100), 0.90, 0.0),
    make_material("Stucco | dusty cream", (0.4700, 0.4550, 0.4150), 0.90, 0.0),
    make_material("Paint | muted crimson", (0.3800, 0.0750, 0.0650), 0.88, 0.0),
    make_material("Paint | muted teal", (0.0550, 0.2050, 0.2000), 0.88, 0.0),
    make_material("Glass | dusty blue", (0.0650, 0.1450, 0.2100), 0.45, 0.0, 0.25),
    make_material("Glass | warm amber", (0.5800, 0.3300, 0.0650), 0.45, 0.0, 0.20),
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
    WOOD_DARK,
    BRICK,
    TERRA,
    STONE_DARK,
    STONE_MID,
    CONC_ASH,
    CONC_LIGHT,
    STUCCO_WARM,
    TRIM_CREAM,
    CRIMSON,
    TEAL,
    GLASS_BLUE,
    GLASS_WARM,
    PREVIEW_FLOOR,
) = range(len(MATS))


class MidriseBuilder:
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

    def flared_box(self, center, size, mat_idx, flare=1.035, bevel=0.03):
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
                offset=min(bevel, min(sx, sy, sz) * 0.14),
                offset_type="OFFSET",
                segments=1,
                profile=0.5,
                affect="EDGES",
            )
        self._add(src, mat_idx, Matrix.Translation(Vector(center)))

    def cyl(self, center, radius, height, mat_idx, segs=10, rot=(0.0, 0.0, 0.0)):
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=segs, radius1=radius, radius2=radius, depth=height)
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def window(self, center, w, h, frame_mat, glass_mat, face_dir="-Y", sill_mat=CONC_LIGHT):
        cx, cy, cz = center
        if face_dir in ("-Y", "+Y"):
            sgn = -1.0 if face_dir == "-Y" else 1.0
            self.box((cx, cy + sgn * 0.04, cz), (w, 0.14, h), frame_mat, bevel=0.015)
            self.box((cx, cy + sgn * 0.08, cz), (w - 0.16, 0.08, h - 0.16), glass_mat)
            self.box((cx, cy + sgn * 0.11, cz), (0.06, 0.05, h - 0.14), frame_mat)
            self.box((cx, cy + sgn * 0.11, cz), (w - 0.14, 0.05, 0.06), frame_mat)
            self.box((cx, cy + sgn * 0.10, cz - h * 0.5 - 0.03), (w + 0.16, 0.22, 0.10), sill_mat, bevel=0.015)
            self.box((cx, cy + sgn * 0.08, cz + h * 0.5 + 0.03), (w + 0.14, 0.18, 0.10), sill_mat, bevel=0.015)
        else:
            sgn = -1.0 if face_dir == "-X" else 1.0
            self.box((cx + sgn * 0.04, cy, cz), (0.14, w, h), frame_mat, bevel=0.015)
            self.box((cx + sgn * 0.08, cy, cz), (0.08, w - 0.16, h - 0.16), glass_mat)
            self.box((cx + sgn * 0.11, cy, cz), (0.05, 0.06, h - 0.14), frame_mat)
            self.box((cx + sgn * 0.11, cy, cz), (0.05, w - 0.14, 0.06), frame_mat)
            self.box((cx + sgn * 0.10, cy, cz - h * 0.5 - 0.03), (0.22, w + 0.16, 0.10), sill_mat, bevel=0.015)
            self.box((cx + sgn * 0.08, cy, cz + h * 0.5 + 0.03), (0.18, w + 0.14, 0.10), sill_mat, bevel=0.015)

    def parapet(self, z_top, w, d, wall_mat, cap_mat, wall_h=0.58):
        self.box((0.0, 0.0, z_top + 0.06), (w - 0.12, d - 0.12, 0.14), SLATE_DARK)
        # Bold overhanging stylized cornice
        self.box((0.0, 0.0, z_top - 0.04), (w + 0.42, d + 0.42, 0.24), cap_mat, bevel=0.03)
        t = 0.24
        for sy in (-1.0, 1.0):
            self.box((0.0, sy * (d * 0.5 - t * 0.5), z_top + wall_h * 0.5), (w + 0.06, t, wall_h), wall_mat, bevel=0.02)
            self.box((0.0, sy * (d * 0.5 - t * 0.5), z_top + wall_h + 0.06), (w + 0.20, t + 0.10, 0.12), cap_mat, bevel=0.02)
        for sx in (-1.0, 1.0):
            self.box((sx * (w * 0.5 - t * 0.5), 0.0, z_top + wall_h * 0.5), (t, d - t * 1.8, wall_h), wall_mat, bevel=0.02)
            self.box((sx * (w * 0.5 - t * 0.5), 0.0, z_top + wall_h + 0.06), (t + 0.10, d + 0.14, 0.12), cap_mat, bevel=0.02)

    def fire_escape(self, x_center, front_y, z_levels, width=2.2):
        """Iconic NYC dark forged-steel fire escape balconies + diagonal stairs."""
        for idx, z in enumerate(z_levels):
            self.box((x_center, front_y - 0.36, z), (width, 0.72, 0.10), IRON_DARK)
            self.box((x_center, front_y - 0.70, z + 0.46), (width, 0.07, 0.07), IRON_DARK)
            for k in range(5):
                rx = x_center - width * 0.46 + k * (width * 0.92 / 4.0)
                self.box((rx, front_y - 0.70, z + 0.23), (0.06, 0.06, 0.46), IRON_DARK)
            if idx < len(z_levels) - 1:
                z_next = z_levels[idx + 1]
                dz = z_next - z
                self.box((x_center, front_y - 0.42, z + dz * 0.5), (0.14, 0.10, math.hypot(dz, width * 0.55)), IRON_DARK, rot=(0.0, 0.55 if idx % 2 == 0 else -0.55, 0.0))

    def water_tank(self, base_center, radius=0.68, height=1.35):
        cx, cy, cz = base_center
        leg_h = 0.75
        for lx in (-0.40, 0.40):
            for ly in (-0.40, 0.40):
                self.box((cx + lx, cy + ly, cz + leg_h * 0.5), (0.11, 0.11, leg_h), IRON_DARK)
        self.box((cx, cy, cz + leg_h), (radius * 2.05, radius * 2.05, 0.10), WOOD_DARK)
        self.cyl((cx, cy, cz + leg_h + height * 0.5), radius, height, WOOD_CEDAR, segs=10)
        for hz in (0.25, 0.55, 0.85):
            self.cyl((cx, cy, cz + leg_h + height * hz), radius + 0.03, 0.06, IRON_DARK, segs=10)
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=10, radius1=radius + 0.14, radius2=0.04, depth=0.52)
        self._add(src, SLATE_DARK, Matrix.Translation(Vector((cx, cy, cz + leg_h + height + 0.26))))

    def ac_unit(self, base_center, scale=1.0):
        cx, cy, cz = base_center
        self.box((cx, cy, cz + 0.28 * scale), (0.90 * scale, 0.74 * scale, 0.56 * scale), CONC_LIGHT, bevel=0.02)
        self.box((cx, cy, cz + 0.58 * scale), (0.58 * scale, 0.58 * scale, 0.06 * scale), CHARCOAL)
        self.box((cx, cy - 0.38 * scale, cz + 0.28 * scale), (0.64 * scale, 0.05 * scale, 0.34 * scale), IRON_DARK)

    def canopy_marquee(self, center, w, d, body_mat, trim_mat=AMBER):
        cx, cy, cz = center
        self.box((cx, cy - d * 0.5, cz), (w, d, 0.42), body_mat, bevel=0.025)
        self.box((cx, cy - d * 0.5, cz + 0.04), (w + 0.08, d + 0.06, 0.14), trim_mat, bevel=0.015)
        for sx in (-1.0, 1.0):
            self.box((cx + sx * (w * 0.42), cy - d * 0.45, cz + 0.48), (0.07, d * 0.78, 0.07), IRON_DARK, rot=(-0.62, 0.0, 0.0))

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
# 7 STYLIZED AMERICAN DOWNTOWN MIDRISES (Apoc_Building_01..07)
# =====================================================================

def make_b01_nyc_apartment(loc):
    """Apoc_Building_01 -> NYC Classic Brick Apartment Midrise with Fire Escape & Water Tank."""
    b = MidriseBuilder("Stylized_Apoc_Building_01_Residential_Ruin")
    w, d = 5.2, 5.0
    b.box((0.0, 0.0, 1.05), (w + 0.24, d + 0.24, 2.10), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 4.75), (w, d, 5.35), BRICK, flare=1.03, bevel=0.03)
    b.box((0.0, 0.0, 2.12), (w + 0.34, d + 0.34, 0.22), CONC_LIGHT, bevel=0.02)
    b.parapet(7.42, w + 0.16, d + 0.16, BRICK, CONC_LIGHT, wall_h=0.58)
    b.water_tank((-1.25, 0.85, 7.52), radius=0.66, height=1.35)
    b.box((1.25, 0.95, 8.12), (1.65, 1.45, 1.22), BRICK, bevel=0.02)
    b.box((1.25, 0.95, 8.78), (1.82, 1.62, 0.14), CONC_LIGHT, bevel=0.02)
    # Entrance canopy & doorway
    b.canopy_marquee((0.0, -2.62, 2.02), 2.35, 0.95, SLATE_DARK, AMBER)
    b.box((0.0, -2.64, 0.98), (1.28, 0.18, 1.88), IRON_DARK, bevel=0.02)
    b.box((0.0, -2.68, 0.96), (0.98, 0.10, 1.68), GLASS_WARM)
    # Window grids (front + sides)
    for fz in (3.15, 4.75, 6.35):
        for wx in (-1.55, 0.0, 1.55):
            b.window((wx, -2.55, fz), 0.95, 1.05, TRIM_CREAM, GLASS_WARM if (wx == 0 and fz == 4.75) else GLASS_BLUE, "-Y")
        for side in ("-X", "+X"):
            sx = -2.56 if side == "-X" else 2.56
            for wy in (-1.35, 1.35):
                b.window((sx, wy, fz), 0.95, 1.05, TRIM_CREAM, GLASS_BLUE, side)
    b.fire_escape(0.0, -2.58, [2.65, 4.25, 5.85], width=2.45)
    return b.finish(loc)


def make_b02_artdeco_office(loc):
    """Apoc_Building_02 -> American Art-Deco Concrete & Glass Office Building."""
    b = MidriseBuilder("Stylized_Apoc_Building_02_Office_Glass_Ruin")
    w, d = 5.8, 5.2
    b.box((0.0, 0.0, 1.15), (w + 0.28, d + 0.28, 2.30), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 4.95), (w, d, 5.45), CONC_ASH, flare=1.03, bevel=0.03)
    # Art-deco vertical concrete pilasters
    for px in (-2.65, -0.95, 0.95, 2.65):
        b.box((px, -2.64, 4.95), (0.42, 0.24, 5.55), CONC_LIGHT, bevel=0.02)
        b.box((px, 2.64, 4.95), (0.42, 0.24, 5.55), CONC_LIGHT, bevel=0.02)
    b.parapet(7.68, w + 0.16, d + 0.16, CONC_ASH, CONC_LIGHT, wall_h=0.62)
    # Stepped Art-Deco rooftop crown + spire
    b.box((0.0, 0.0, 8.55), (3.45, 3.05, 1.15), CONC_LIGHT, bevel=0.03)
    b.box((0.0, 0.0, 9.22), (2.25, 1.95, 0.32), SLATE_DARK, bevel=0.02)
    b.cyl((0.0, 0.0, 10.15), 0.12, 1.65, SILVER, segs=8)
    b.canopy_marquee((0.0, -2.74, 2.15), 3.15, 1.05, BLUE, AMBER)
    b.box((0.0, -2.76, 1.05), (1.75, 0.18, 1.92), IRON_DARK, bevel=0.02)
    b.box((0.0, -2.80, 1.05), (1.42, 0.10, 1.72), GLASS_BLUE)
    for fz in (3.25, 4.85, 6.45):
        for wx in (-1.80, 0.0, 1.80):
            b.window((wx, -2.64, fz), 1.15, 1.12, SLATE_DARK, GLASS_BLUE if fz != 4.85 else GLASS_WARM, "-Y", sill_mat=SLATE_DARK)
        for side in ("-X", "+X"):
            sx = -2.94 if side == "-X" else 2.94
            for wy in (-1.35, 0.0, 1.35):
                b.window((sx, wy, fz), 0.96, 1.08, SLATE_DARK, GLASS_BLUE, side, sill_mat=SLATE_DARK)
    return b.finish(loc)


def make_b03_signal_tower(loc):
    """Apoc_Building_03 -> Narrow American Broadcast / Radio Communications Tower."""
    b = MidriseBuilder("Stylized_Apoc_Building_03_Narrow_Signal_Tower")
    w, d = 4.6, 4.6
    b.box((0.0, 0.0, 1.10), (w + 0.24, d + 0.24, 2.20), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 5.45), (w, d, 6.65), CONC_LIGHT, flare=1.025, bevel=0.03)
    # Survival-blue tactical accent bands
    b.box((0.0, 0.0, 2.22), (w + 0.28, d + 0.28, 0.28), BLUE, bevel=0.02)
    b.box((0.0, 0.0, 5.55), (w + 0.22, d + 0.22, 0.22), BLUE, bevel=0.02)
    b.parapet(8.78, w + 0.14, d + 0.14, CONC_ASH, SLATE_DARK, wall_h=0.58)
    # Rooftop radio control booth + tall antenna mast + amber beacon
    b.box((0.0, 0.0, 9.48), (2.25, 2.25, 1.25), SLATE_DARK, bevel=0.03)
    b.box((0.0, -1.16, 9.55), (1.35, 0.10, 0.42), AMBER)
    b.cyl((0.0, 0.0, 11.45), 0.14, 2.85, SILVER, segs=8)
    b.box((0.0, 0.0, 11.35), (1.75, 0.09, 0.09), IRON_DARK)
    b.box((0.0, 0.0, 12.15), (1.15, 0.09, 0.09), IRON_DARK)
    b.box((0.0, 0.0, 12.92), (0.28, 0.28, 0.28), AMBER, bevel=0.03)
    b.canopy_marquee((0.0, -2.42, 2.05), 2.15, 0.85, SLATE_DARK, AMBER)
    for fz in (3.35, 4.85, 6.65, 7.95):
        for wx in (-1.15, 1.15):
            b.window((wx, -2.35, fz), 1.05, 0.96, SLATE_DARK, GLASS_BLUE if fz != 6.65 else GLASS_WARM, "-Y", sill_mat=STONE_MID)
        for side in ("-X", "+X"):
            sx = -2.35 if side == "-X" else 2.35
            for wy in (-1.15, 1.15):
                b.window((sx, wy, fz), 0.96, 0.96, SLATE_DARK, GLASS_BLUE, side, sill_mat=STONE_MID)
    return b.finish(loc)


def make_b04_county_hospital(loc):
    """Apoc_Building_04 -> American County Hospital / Civic Medical Building."""
    b = MidriseBuilder("Stylized_Apoc_Building_04_Industrial_Hospital_Ruin")
    w, d = 6.0, 5.4
    b.box((0.0, 0.0, 1.05), (w + 0.24, d + 0.24, 2.10), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 4.45), (w, d, 4.75), STUCCO_WARM, flare=1.03, bevel=0.03)
    b.box((0.0, 0.0, 2.12), (w + 0.30, d + 0.30, 0.22), CONC_LIGHT, bevel=0.02)
    b.parapet(6.82, w + 0.16, d + 0.16, STUCCO_WARM, CONC_LIGHT, wall_h=0.58)
    # Raised civic parapet crest with crimson medical cross emblem
    b.box((0.0, -2.72, 7.52), (2.45, 0.30, 0.96), TRIM_CREAM, bevel=0.02)
    b.box((0.0, -2.88, 7.52), (0.72, 0.10, 0.24), CRIMSON)
    b.box((0.0, -2.88, 7.52), (0.24, 0.10, 0.72), CRIMSON)
    b.ac_unit((-1.75, 0.85, 6.94), 1.15)
    b.ac_unit((1.75, 0.85, 6.94), 1.15)
    # Protruding ambulance entrance portico
    b.box((0.0, -3.32, 2.02), (3.15, 1.25, 0.34), CONC_LIGHT, bevel=0.02)
    for px in (-1.38, 1.38):
        b.box((px, -3.78, 1.00), (0.34, 0.34, 2.00), STONE_MID, bevel=0.02)
    b.box((0.0, -2.82, 1.00), (1.75, 0.16, 1.88), IRON_DARK)
    b.box((0.0, -2.86, 1.00), (1.42, 0.10, 1.68), GLASS_WARM)
    for fz in (3.25, 5.25):
        for wx in (-1.95, 0.0, 1.95):
            b.window((wx, -2.74, fz), 1.28, 1.18, SLATE_DARK, GLASS_BLUE, "-Y")
        for side in ("-X", "+X"):
            sx = -3.04 if side == "-X" else 3.04
            for wy in (-1.45, 1.45):
                b.window((sx, wy, fz), 1.22, 1.15, SLATE_DARK, GLASS_BLUE, side)
    return b.finish(loc)


def make_b05_vintage_hotel(loc):
    """Apoc_Building_05 -> Vintage American Downtown Hotel with Amber Marquee & Vertical Sign."""
    b = MidriseBuilder("Stylized_Apoc_Building_05_Shattered_Hotel_Midrise")
    w, d = 5.6, 5.2
    b.box((0.0, 0.0, 1.10), (w + 0.24, d + 0.24, 2.20), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 4.65), (w, d, 4.95), BRICK, flare=1.035, bevel=0.03)
    b.box((0.0, 0.0, 2.22), (w + 0.32, d + 0.32, 0.24), TRIM_CREAM, bevel=0.02)
    b.parapet(7.12, w + 0.18, d + 0.18, BRICK, TRIM_CREAM, wall_h=0.60)
    b.water_tank((1.35, 0.95, 7.24), radius=0.64, height=1.30)
    b.ac_unit((-1.35, 0.85, 7.24), 1.05)
    # Bold vintage hotel amber marquee over front entrance
    b.canopy_marquee((0.0, -2.72, 2.15), 2.85, 1.15, SLATE_DARK, AMBER)
    b.box((0.0, -2.74, 1.02), (1.42, 0.18, 1.92), WOOD_DARK, bevel=0.02)
    b.box((0.0, -2.78, 1.02), (1.08, 0.10, 1.68), GLASS_WARM)
    # Iconic vertical blade hotel sign on right corner
    b.box((2.98, -2.65, 4.85), (0.68, 0.28, 3.15), AMBER, bevel=0.025)
    b.box((2.98, -2.65, 4.85), (0.76, 0.18, 3.25), IRON_DARK)
    for fz in (3.25, 4.75, 6.20):
        for wx in (-1.70, 0.0, 1.70):
            b.window((wx, -2.64, fz), 1.02, 1.02, TRIM_CREAM, GLASS_WARM if (wx == 0 or fz == 4.75) else GLASS_BLUE, "-Y")
        for side in ("-X", "+X"):
            sx = -2.84 if side == "-X" else 2.84
            for wy in (-1.35, 1.35):
                b.window((sx, wy, fz), 1.02, 1.02, TRIM_CREAM, GLASS_BLUE, side)
    return b.finish(loc)


def make_b06_downtown_clinic(loc):
    """Apoc_Building_06 -> American Downtown Corner Clinic / Pharmacy Lowrise."""
    b = MidriseBuilder("Stylized_Apoc_Building_06_Ruined_Clinic_Lowrise")
    w, d = 5.0, 4.8
    b.box((0.0, 0.0, 0.95), (w + 0.22, d + 0.22, 1.90), CONC_ASH, bevel=0.03)
    b.flared_box((0.0, 0.0, 3.85), (w, d, 3.95), TEAL, flare=1.04, bevel=0.03)
    b.box((0.0, 0.0, 1.92), (w + 0.28, d + 0.28, 0.20), TRIM_CREAM, bevel=0.02)
    b.parapet(5.82, w + 0.16, d + 0.16, TEAL, TRIM_CREAM, wall_h=0.52)
    b.box((0.0, -2.42, 6.45), (2.75, 0.28, 0.56), BLUE, bevel=0.02)
    b.box((0.0, -2.42, 6.76), (2.92, 0.34, 0.12), TRIM_CREAM, bevel=0.02)
    b.ac_unit((-1.15, 0.75, 5.94), 1.0)
    b.ac_unit((1.15, 0.75, 5.94), 0.92)
    b.canopy_marquee((0.0, -2.52, 1.92), 4.25, 0.95, BLUE, AMBER)
    b.box((0.0, -2.54, 0.92), (1.18, 0.16, 1.75), IRON_DARK)
    b.box((0.0, -2.58, 0.92), (0.92, 0.10, 1.55), GLASS_WARM)
    for fz in (3.05, 4.75):
        for wx in (-1.42, 0.0, 1.42):
            b.window((wx, -2.44, fz), 0.96, 1.02, TRIM_CREAM, GLASS_BLUE if wx != 0 else GLASS_WARM, "-Y")
        for side in ("-X", "+X"):
            sx = -2.54 if side == "-X" else 2.54
            for wy in (-1.20, 1.20):
                b.window((sx, wy, fz), 0.96, 1.02, TRIM_CREAM, GLASS_BLUE, side)
    return b.finish(loc)


def make_b07_freight_depot(loc):
    """Apoc_Building_07 -> American Brick Industrial Freight Depot / Loft Warehouse."""
    b = MidriseBuilder("Stylized_Apoc_Building_07_Collapsed_Depot_Midrise")
    w, d = 6.2, 5.4
    b.box((0.0, 0.0, 0.90), (w + 0.24, d + 0.24, 1.80), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 4.15), (w, d, 4.75), BRICK, flare=1.03, bevel=0.03)
    # Industrial steel corner columns
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            b.box((sx * (w * 0.5 + 0.05), sy * (d * 0.5 + 0.05), 3.25), (0.34, 0.34, 6.50), SLATE_DARK, bevel=0.02)
    b.parapet(6.52, w + 0.16, d + 0.16, BRICK, SLATE_DARK, wall_h=0.56)
    # Rooftop industrial sawtooth skylight + steel smokestacks
    b.box((-0.65, 0.35, 7.18), (3.45, 2.25, 1.15), SLATE_DARK, bevel=0.03)
    b.box((-0.65, -0.78, 7.22), (2.95, 0.10, 0.65), GLASS_BLUE)
    b.cyl((1.95, 1.15, 7.65), 0.26, 2.15, IRON_DARK, segs=10)
    b.cyl((1.95, 0.25, 7.35), 0.22, 1.55, TERRA, segs=10)
    # Concrete loading dock + roll-up bay door + amber hazard canopy
    b.box((-0.85, -3.12, 0.38), (3.45, 0.92, 0.76), CONC_ASH, bevel=0.02)
    b.box((-0.85, -2.74, 1.75), (2.65, 0.16, 2.25), CONC_LIGHT, bevel=0.02)
    for gz in (1.05, 1.45, 1.85, 2.25, 2.65):
        b.box((-0.85, -2.83, gz), (2.35, 0.05, 0.07), IRON_DARK)
    b.canopy_marquee((-0.85, -2.78, 3.12), 3.35, 1.05, SLATE_DARK, AMBER)
    # Side steel door + industrial upper factory windows
    b.box((1.95, -2.74, 1.15), (1.05, 0.16, 1.95), IRON_DARK, bevel=0.02)
    for wx in (-1.85, 0.0, 1.85):
        b.window((wx, -2.74, 4.95), 1.32, 1.25, IRON_DARK, GLASS_BLUE if wx != 1.85 else GLASS_WARM, "-Y", sill_mat=CONC_ASH)
    for side in ("-X", "+X"):
        sx = -3.14 if side == "-X" else 3.14
        for fz in (3.15, 4.95):
            for wy in (-1.35, 1.35):
                b.window((sx, wy, fz), 1.25, 1.15, IRON_DARK, GLASS_BLUE, side, sill_mat=CONC_ASH)
    return b.finish(loc)


MIDRISES = [
    make_b01_nyc_apartment((-24.0, 0.0, 0.0)),
    make_b02_artdeco_office((-16.0, 0.0, 0.0)),
    make_b03_signal_tower((-8.2, 0.0, 0.0)),
    make_b04_county_hospital((0.0, 0.0, 0.0)),
    make_b05_vintage_hotel((8.2, 0.0, 0.0)),
    make_b06_downtown_clinic((16.0, 0.0, 0.0)),
    make_b07_freight_depot((24.0, 0.0, 0.0)),
]

for obj in MIDRISES:
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


floor_bm = bmesh.new()
bmesh.ops.create_cube(floor_bm, size=1.0)
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((0.0, 0.0, -0.15)) @ Matrix.Diagonal((58.0, 16.0, 0.30, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_FLOOR])
floor_obj = bpy.data.objects.new("Preview_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

bpy.ops.object.camera_add(location=(0.0, -32.0, 16.0))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 56.0
point_at(camera, (0.0, 0.0, 4.2))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(14.0, -20.0, 24.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.94, 0.82)
sun.data.energy = 3.0
sun.data.angle = math.radians(10.0)
point_at(sun, (0.0, 0.0, 4.0))

bpy.ops.object.light_add(type="AREA", location=(-18.0, -16.0, 16.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 1500.0
fill.data.size = 20.0
point_at(fill, (0.0, 0.0, 4.0))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 2000
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.14, 0.17, 0.22)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)
print("All 7 Stylized American City Midrises generated successfully!")

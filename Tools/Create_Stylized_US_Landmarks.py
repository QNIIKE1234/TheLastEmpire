import math
import os
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Stylized_US_Landmarks_Source.blend")
PREVIEW_A_PATH = os.path.join(OUT_DIR, "Stylized_US_Landmarks_Preview_A.png")
PREVIEW_B_PATH = os.path.join(OUT_DIR, "Stylized_US_Landmarks_Preview_B.png")

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


MATS = [
    make_material("Hazard | faded amber", (0.7484, 0.3931, 0.0272), 0.90, 0.0),
    make_material("Titanium | graphite armor", (0.0550, 0.0637, 0.0890), 0.78, 0.08),
    make_material("Iron | dark forged steel", (0.0380, 0.0440, 0.0620), 0.78, 0.08),
    make_material("Charcoal | blackened", (0.0220, 0.0245, 0.0331), 0.92, 0.0),
    make_material("Titanium | bright bevel", (0.4770, 0.5382, 0.6383), 0.45, 0.55),
    make_material("Paint | faded survival blue", (0.0331, 0.0946, 0.3000), 0.88, 0.0),
    make_material("Wood | weathered cedar", (0.3600, 0.2300, 0.1200), 0.88, 0.0),
    make_material("Wood | exposed grain", (0.2600, 0.1600, 0.0800), 0.88, 0.0),
    make_material("Wood | charred and stained", (0.1200, 0.0700, 0.0350), 0.90, 0.0),
    make_material("Brick | fired clay", (0.3800, 0.1400, 0.0700), 0.88, 0.0),
    make_material("Paint | muted crimson", (0.3800, 0.0750, 0.0650), 0.88, 0.0),
    make_material("Paint | muted teal", (0.0550, 0.2050, 0.2000), 0.88, 0.0),
    make_material("Cloth | military olive", (0.0850, 0.1750, 0.1050), 0.92, 0.0),
    make_material("Cloth | field canvas", (0.4200, 0.3650, 0.2650), 0.92, 0.0),
    make_material("Stone | charcoal gray", (0.0900, 0.1000, 0.1300), 0.88, 0.0),
    make_material("Mortar | pale sand", (0.1600, 0.1600, 0.1800), 0.88, 0.0),
    make_material("Concrete | cracked ash", (0.2400, 0.2500, 0.2800), 0.90, 0.0),
    make_material("Concrete | broken aggregate", (0.3400, 0.3500, 0.3800), 0.90, 0.0),
    make_material("Stucco | dusty cream", (0.4700, 0.4550, 0.4150), 0.90, 0.0),
    make_material("Glass | dusty blue", (0.0650, 0.1450, 0.2100), 0.45, 0.0, 0.25),
    make_material("Glass | warm amber", (0.5800, 0.3300, 0.0650), 0.45, 0.0, 0.20),
    make_material("Water | purified cyan", (0.0750, 0.2800, 0.3600), 0.45, 0.0, 0.25),
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
    BRICK,
    CRIMSON,
    TEAL,
    OLIVE,
    CANVAS_TAN,
    STONE_DARK,
    STONE_MID,
    CONC_ASH,
    CONC_LIGHT,
    CREAM,
    GLASS_BLUE,
    GLASS_WARM,
    WATER_CYAN,
    PREVIEW_FLOOR,
) = range(len(MATS))


class LandmarkBuilder:
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

    def flared_box(self, center, size, mat_idx, flare=1.04, bevel=0.025):
        src = bmesh.new()
        bmesh.ops.create_cube(src, size=1.0)
        sx, sy, sz = size
        for v in src.verts:
            m = flare if v.co.z > 0.0 else 1.0
            v.co = Vector((v.co.x * sx * m, v.co.y * sy * m, v.co.z * sz))
        if bevel > 0.0:
            bmesh.ops.bevel(src, geom=list(src.edges), offset=min(bevel, min(sx, sy, sz) * 0.16), offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES")
        self._add(src, mat_idx, Matrix.Translation(Vector(center)))

    def cyl(self, center, r1, h, mat_idx, r2=None, segs=10, rot=(0.0, 0.0, 0.0)):
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=segs, radius1=r1, radius2=r1 if r2 is None else r2, depth=h)
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def prism_tent(self, center, w, d, h, mat_idx):
        """A-frame ridge tent triangular prism."""
        src = bmesh.new()
        hw, hd = w * 0.5, d * 0.5
        v0 = src.verts.new(Vector((-hw, -hd, 0.0)))
        v1 = src.verts.new(Vector((hw, -hd, 0.0)))
        v2 = src.verts.new(Vector((0.0, -hd, h)))
        v3 = src.verts.new(Vector((-hw, hd, 0.0)))
        v4 = src.verts.new(Vector((hw, hd, 0.0)))
        v5 = src.verts.new(Vector((0.0, hd, h)))
        src.faces.new((v0, v1, v2))
        src.faces.new((v4, v3, v5))
        src.faces.new((v0, v3, v4, v1))
        src.faces.new((v1, v4, v5, v2))
        src.faces.new((v3, v0, v2, v5))
        bmesh.ops.recalc_face_normals(src, faces=src.faces)
        self._add(src, mat_idx, Matrix.Translation(Vector(center)))

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
# 17 CORE AMERICAN LANDMARKS
# =====================================================================

def make_warehouse_hangar(loc):
    """01: American Industrial Quonset/Brick Warehouse Hangar (5.8 x 4.6 x 3.8m)."""
    b = LandmarkBuilder("Stylized_Landmark_WarehouseHangar")
    w, d = 5.4, 4.4
    b.box((0.0, 0.0, 0.30), (w + 0.24, d + 0.24, 0.60), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.55), (w, d, 2.05), CONC_ASH, flare=1.03, bevel=0.03)
    # Chunky arched/barrel Quonset steel roof with exterior structural ribs
    b.cyl((0.0, 0.0, 2.55), d * 0.52, w + 0.36, SLATE_DARK, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
    for rx in (-2.2, -1.1, 0.0, 1.1, 2.2):
        b.cyl((rx, 0.0, 2.58), d * 0.545, 0.16, IRON_DARK, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
    # Big steel hangar roll-up bay door + amber hazard lintel
    b.box((0.0, -2.26, 1.25), (3.15, 0.16, 2.05), CONC_LIGHT, bevel=0.02)
    for gz in (0.62, 1.02, 1.42, 1.82):
        b.box((0.0, -2.35, gz), (2.85, 0.05, 0.07), IRON_DARK)
    b.box((0.0, -2.34, 2.38), (3.45, 0.24, 0.24), AMBER, bevel=0.02)
    # Side cargo crates + steel exhaust vents
    b.box((2.98, -1.05, 0.42), (0.72, 0.72, 0.84), WOOD_CEDAR, bevel=0.02)
    b.cyl((-1.6, 0.0, 4.82), 0.22, 0.55, SILVER, segs=8)
    b.cyl((1.6, 0.0, 4.82), 0.22, 0.55, SILVER, segs=8)
    return b.finish(loc)


def make_cafe_patio(loc):
    """02: American Sidewalk Cafe Patio with Brick Counter, Striped Awning & Umbrella Tables."""
    b = LandmarkBuilder("Stylized_Landmark_CafePatio")
    # Paved stone patio deck
    b.box((0.0, 0.0, 0.10), (4.4, 3.6, 0.20), CONC_ASH, bevel=0.02)
    # Back brick kiosk wall + wooden serving counter
    b.box((0.0, 1.25, 1.28), (4.0, 0.85, 2.25), BRICK, bevel=0.03)
    b.box((0.0, 0.78, 0.62), (3.6, 0.48, 0.88), WOOD_OAK, bevel=0.02)
    b.box((0.0, 0.74, 1.08), (3.75, 0.58, 0.10), CREAM, bevel=0.015)
    # Striped amber-and-blue awning over counter
    for i in range(5):
        sx = -1.6 + i * 0.8
        m = AMBER if i % 2 == 0 else BLUE
        b.box((sx, 0.32, 2.28), (0.78, 1.25, 0.12), m, rot=(0.28, 0.0, 0.0), bevel=0.015)
    # Two patio café tables with umbrella + stools
    for tx, umb_m in ((-1.15, BLUE), (1.15, CRIMSON)):
        b.cyl((tx, -0.75, 0.45), 0.08, 0.72, IRON_DARK, segs=8)
        b.cyl((tx, -0.75, 0.82), 0.56, 0.09, WOOD_CEDAR, segs=10)
        b.cyl((tx, -0.75, 1.52), 0.05, 1.45, WOOD_DARK, segs=8)
        b.cyl((tx, -0.75, 2.28), 0.92, 0.34, umb_m, r2=0.06, segs=8)
        for sx in (-0.68, 0.68):
            b.cyl((tx + sx, -0.75, 0.32), 0.22, 0.46, WOOD_OAK, segs=8)
    return b.finish(loc)


def make_market_stall(loc, name, stripe_a, stripe_b):
    """03 & 04: Chunky American Street Market Stall (Red or Blue striped canvas)."""
    b = LandmarkBuilder(name)
    w, d = 3.2, 2.2
    # Wooden deck base + 4 chunky cedar posts
    b.box((0.0, 0.0, 0.10), (w, d, 0.20), WOOD_DARK, bevel=0.02)
    for sx in (-1.42, 1.42):
        for sy in (-0.92, 0.92):
            h = 2.45 if sy > 0 else 2.15
            b.box((sx, sy, h * 0.5 + 0.16), (0.16, 0.16, h), WOOD_CEDAR, bevel=0.015)
    # Front wooden counter + back shelf
    b.box((0.0, -0.68, 0.62), (2.92, 0.62, 0.88), WOOD_OAK, bevel=0.02)
    b.box((0.0, -0.68, 1.08), (3.08, 0.74, 0.10), CREAM, bevel=0.015)
    # Striped slanted canopy roof
    for i in range(5):
        sx = -1.30 + i * 0.65
        m = stripe_a if i % 2 == 0 else stripe_b
        b.box((sx, 0.0, 2.42), (0.64, d + 0.55, 0.12), m, rot=(0.18, 0.0, 0.0), bevel=0.015)
        b.box((sx, -d * 0.5 - 0.22, 2.12), (0.64, 0.08, 0.22), m)
    # Supply crates on counter & ground
    b.box((-0.85, -0.68, 1.32), (0.52, 0.44, 0.38), WOOD_CEDAR, bevel=0.015)
    b.box((0.10, -0.68, 1.30), (0.48, 0.42, 0.34), AMBER, bevel=0.015)
    b.box((0.92, -0.68, 1.32), (0.52, 0.44, 0.38), WOOD_CEDAR, bevel=0.015)
    return b.finish(loc)


def make_brownstone_row(loc):
    """05: Wide NYC Twin Brownstone Rowhouse (5.6 x 4.4 x 5.6m)."""
    b = LandmarkBuilder("Stylized_Landmark_BrownstoneRow")
    w, d = 5.4, 4.2
    b.box((0.0, 0.0, 0.52), (w + 0.22, d + 0.22, 1.04), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 3.05), (w, d, 4.05), BRICK, flare=1.03, bevel=0.03)
    b.box((0.0, 0.0, 1.06), (w + 0.28, d + 0.28, 0.18), CONC_LIGHT, bevel=0.02)
    b.box((0.0, 0.0, 3.08), (w + 0.22, d + 0.22, 0.16), CONC_LIGHT, bevel=0.02)
    # Chunky overhanging cornice & parapet
    b.box((0.0, 0.0, 5.14), (w + 0.46, d + 0.46, 0.28), SLATE_DARK, bevel=0.03)
    b.box((0.0, 0.0, 5.48), (w + 0.14, d + 0.14, 0.42), BRICK, bevel=0.02)
    b.box((0.0, 0.0, 5.56), (w - 0.25, d - 0.25, 0.36), SLATE_DARK)
    # Twin front stone stoops + doorways
    for sx in (-1.35, 1.35):
        for i in range(4):
            b.box((sx, -2.25 - i * 0.20, 0.72 - i * 0.18), (0.98, 0.24, 0.18), CONC_LIGHT, bevel=0.015)
        b.box((sx, -2.14, 1.48), (0.86, 0.16, 1.56), WOOD_DARK, bevel=0.015)
        b.box((sx, -2.20, 1.48), (0.64, 0.10, 1.36), AMBER if sx < 0 else WOOD_OAK)
    # Windows across 2 upper floors
    for fz in (2.25, 4.05):
        for wx in (-1.95, -0.65, 0.65, 1.95):
            b.box((wx, -2.16, fz), (0.82, 0.16, 0.96), CREAM, bevel=0.015)
            b.box((wx, -2.21, fz), (0.62, 0.08, 0.76), GLASS_WARM if (fz == 4.05 and abs(wx) < 1.0) else GLASS_BLUE)
            b.box((wx, -2.22, fz - 0.52), (0.94, 0.22, 0.10), CONC_LIGHT, bevel=0.015)
    return b.finish(loc)


def make_camp_tent(loc, name, canvas_mat, accent_mat):
    """06 & 07: Chunky Stylized Survivor A-Frame Ridge Tent with Timber Frame & Supply Crate."""
    b = LandmarkBuilder(name)
    w, d, h = 2.8, 3.4, 2.05
    b.box((0.0, 0.0, 0.08), (w + 0.35, d + 0.35, 0.16), WOOD_DARK, bevel=0.02)
    b.prism_tent((0.0, 0.0, 0.14), w, d, h, canvas_mat)
    # Chunky ridge pole & front/back upright wooden tent posts
    b.box((0.0, 0.0, h + 0.16), (0.16, d + 0.45, 0.16), WOOD_CEDAR, bevel=0.015)
    for sy in (-1.0, 1.0):
        b.box((0.0, sy * (d * 0.5 + 0.12), h * 0.5 + 0.14), (0.14, 0.14, h + 0.12), WOOD_CEDAR)
    # Dark interior doorway recess + rolled flaps
    b.prism_tent((0.0, -d * 0.5 + 0.06, 0.14), 1.25, 0.22, 1.38, CHARCOAL)
    for sx in (-0.68, 0.68):
        b.box((sx, -d * 0.5 - 0.05, 0.78), (0.24, 0.16, 1.15), accent_mat, rot=(0.0, sx * 0.25, 0.0), bevel=0.02)
    # Side supply crate & lantern box
    b.box((1.72, -0.85, 0.34), (0.58, 0.64, 0.52), WOOD_OAK, bevel=0.02)
    b.box((1.72, -0.85, 0.64), (0.28, 0.28, 0.16), AMBER, bevel=0.015)
    return b.finish(loc)


def make_guard_watchtower(loc):
    """08: Chunky American Survivor Timber & Steel Watchtower (3.0 x 3.0 x 6.4m)."""
    b = LandmarkBuilder("Stylized_Landmark_GuardWatchtower")
    # 4 splayed heavy cedar support legs + cross braces
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            b.box((sx * 1.15, sy * 1.15, 0.20), (0.48, 0.48, 0.40), STONE_DARK, bevel=0.02)
            b.box((sx * 1.02, sy * 1.02, 1.95), (0.24, 0.24, 3.65), WOOD_DARK, rot=(sy * -0.08, sx * 0.08, 0.0), bevel=0.015)
    # Horizontal & diagonal braces
    b.box((0.0, -1.05, 1.75), (2.15, 0.14, 0.18), WOOD_CEDAR)
    b.box((0.0, 1.05, 1.75), (2.15, 0.14, 0.18), WOOD_CEDAR)
    b.box((-1.05, 0.0, 1.75), (0.14, 2.15, 0.18), WOOD_CEDAR)
    b.box((1.05, 0.0, 1.75), (0.14, 2.15, 0.18), WOOD_CEDAR)
    # Elevated wooden guard deck + parapet walls + sandbag armor
    b.box((0.0, 0.0, 3.72), (2.85, 2.85, 0.22), WOOD_OAK, bevel=0.02)
    for sy in (-1.34, 1.34):
        b.box((0.0, sy, 4.22), (2.85, 0.18, 0.92), WOOD_CEDAR, bevel=0.02)
        b.box((0.0, sy, 4.72), (2.92, 0.24, 0.12), AMBER, bevel=0.015)
    for sx in (-1.34, 1.34):
        b.box((sx, 0.0, 4.22), (0.18, 2.65, 0.92), WOOD_CEDAR, bevel=0.02)
    # Roof posts & overhanging corrugated steel roof + floodlight
    for sx in (-1.22, 1.22):
        for sy in (-1.22, 1.22):
            b.box((sx, sy, 5.15), (0.16, 0.16, 1.65), IRON_DARK)
    b.box((0.0, 0.0, 6.08), (3.45, 3.45, 0.22), SLATE_DARK, rot=(-0.10, 0.0, 0.0), bevel=0.025)
    b.box((0.0, -1.55, 5.75), (0.52, 0.34, 0.34), AMBER, bevel=0.02)
    # Access ladder on side
    for lx in (-0.28, 0.28):
        b.box((1.48, lx, 1.90), (0.09, 0.09, 3.75), IRON_DARK)
    for lz in range(1, 10):
        b.box((1.48, 0.0, lz * 0.38), (0.08, 0.56, 0.07), IRON_DARK)
    return b.finish(loc)


def make_park_gazebo(loc):
    """09: Charming American Town Square Octagonal Park Gazebo (3.8 x 3.8 x 4.4m)."""
    b = LandmarkBuilder("Stylized_Landmark_ParkGazebo")
    b.cyl((0.0, 0.0, 0.22), 1.95, 0.44, STONE_MID, segs=8)
    b.cyl((0.0, 0.0, 0.48), 1.82, 0.10, WOOD_OAK, segs=8)
    # 8 perimeter columns + wooden railings (leaving front opening clear!)
    for i in range(8):
        ang = i * (math.tau / 8.0) + math.pi / 8.0
        px, py = math.cos(ang) * 1.62, math.sin(ang) * 1.62
        b.box((px, py, 1.62), (0.18, 0.18, 2.28), CREAM, bevel=0.015)
        if i not in (5, 6):
            ang2 = (i + 1) * (math.tau / 8.0) + math.pi / 8.0
            mx, my = (px + math.cos(ang2) * 1.62) * 0.5, (py + math.sin(ang2) * 1.62) * 0.5
            b.box((mx, my, 0.92), (1.22, 0.12, 0.72), WOOD_CEDAR, rot=(0.0, 0.0, ang + math.pi * 0.5 + math.pi / 8.0), bevel=0.015)
    # Front stone entry steps
    b.box((0.0, -2.02, 0.14), (1.35, 0.46, 0.28), CONC_LIGHT, bevel=0.02)
    # Tiered octagonal dark-slate roof + amber cupola finial
    b.cyl((0.0, 0.0, 2.80), 2.18, 0.22, CREAM, segs=8)
    b.cyl((0.0, 0.0, 3.45), 2.28, 1.15, SLATE_DARK, r2=0.45, segs=8)
    b.cyl((0.0, 0.0, 4.18), 0.48, 0.36, AMBER, r2=0.05, segs=8)
    return b.finish(loc)


def make_subway_entrance(loc):
    """10: Iconic NYC Subway Stairwell Entrance with Iron Railings & Globe Lamps."""
    b = LandmarkBuilder("Stylized_Landmark_SubwayEntrance")
    w, d = 2.8, 4.6
    # Raised stone curb U-wall around stairwell
    b.box((0.0, 0.0, 0.12), (w + 0.3, d + 0.3, 0.24), CONC_ASH, bevel=0.02)
    # Dark interior stairwell pit + descending stone stairs
    b.box((0.0, 0.15, 0.22), (w - 0.55, d - 0.65, 0.08), CHARCOAL)
    for i in range(5):
        b.box((0.0, -1.65 + i * 0.45, 0.22 - i * 0.03), (w - 0.65, 0.35, 0.08), STONE_MID)
    # Iconic green/teal NYC iron perimeter railings + back wall
    for sx in (-1.28, 1.28):
        b.box((sx, 0.0, 0.75), (0.14, d, 0.96), TEAL, bevel=0.015)
        b.box((sx, 0.0, 1.26), (0.20, d + 0.12, 0.10), IRON_DARK, bevel=0.015)
    b.box((0.0, d * 0.5 - 0.08, 0.75), (w, 0.16, 0.96), TEAL, bevel=0.015)
    # Front arched sign portal + iconic subway globe lamps
    for sx in (-1.28, 1.28):
        b.cyl((sx, -d * 0.5 + 0.08, 1.45), 0.11, 2.75, IRON_DARK, segs=8)
        b.cyl((sx, -d * 0.5 + 0.08, 2.95), 0.24, 0.34, AMBER, r2=0.18, segs=10)
    b.box((0.0, -d * 0.5 + 0.08, 2.65), (w + 0.18, 0.22, 0.46), SLATE_DARK, bevel=0.02)
    b.box((0.0, -d * 0.5 - 0.04, 2.65), (w - 0.28, 0.06, 0.26), AMBER)
    # Overhanging steel canopy over rear half of stairwell
    b.box((0.0, 0.85, 2.55), (w + 0.38, 2.65, 0.16), SLATE_DARK, rot=(-0.10, 0.0, 0.0), bevel=0.02)
    for sx in (-1.28, 1.28):
        b.box((sx, 2.05, 1.85), (0.14, 0.14, 1.35), IRON_DARK)
    return b.finish(loc)


def make_billboard_sign(loc):
    """11: Classic American Highway Steel Billboard Sign (5.6 x 1.6 x 5.4m)."""
    b = LandmarkBuilder("Stylized_Landmark_BillboardSign")
    # Heavy concrete footings + twin steel I-beam legs
    for sx in (-1.75, 1.75):
        b.box((sx, 0.0, 0.24), (0.78, 0.86, 0.48), CONC_ASH, bevel=0.02)
        b.box((sx, 0.0, 1.95), (0.28, 0.32, 3.25), IRON_DARK, bevel=0.015)
        b.box((sx, 0.52, 1.35), (0.14, 1.35, 0.14), IRON_DARK, rot=(0.68, 0.0, 0.0))
    # Horizontal catwalk platform + safety rail
    b.box((0.0, -0.52, 2.75), (5.35, 0.65, 0.10), IRON_DARK)
    b.box((0.0, -0.82, 3.25), (5.35, 0.06, 0.06), IRON_DARK)
    # Huge framed stylized American billboard panel (blue/cream/amber graphic blocks!)
    b.box((0.0, 0.0, 4.15), (5.55, 0.30, 2.55), SLATE_DARK, bevel=0.03)
    b.box((0.0, -0.16, 4.15), (5.15, 0.08, 2.15), CREAM)
    b.box((-1.35, -0.20, 4.35), (2.15, 0.06, 1.15), BLUE)
    b.box((1.25, -0.20, 4.20), (2.10, 0.06, 1.45), AMBER)
    b.box((-1.35, -0.20, 3.45), (2.15, 0.06, 0.38), CRIMSON)
    # 3 overhead billboard floodlights
    for lx in (-1.75, 0.0, 1.75):
        b.box((lx, -0.58, 5.48), (0.10, 0.85, 0.10), IRON_DARK)
        b.box((lx, -0.96, 5.38), (0.36, 0.24, 0.18), AMBER, bevel=0.015)
    return b.finish(loc)


def make_gas_station_canopy(loc):
    """12: Retro American Route-66 Gas Station Canopy with 2 Fuel Pumps (6.2 x 3.8 x 4.2m)."""
    b = LandmarkBuilder("Stylized_Landmark_GasStationCanopy")
    # Raised concrete pump island
    b.box((0.0, 0.0, 0.14), (4.8, 1.45, 0.28), CONC_LIGHT, bevel=0.03)
    # Twin sturdy canopy pillars with amber warning bases
    for sx in (-1.85, 1.85):
        b.box((sx, 0.0, 0.58), (0.58, 0.58, 0.65), AMBER, bevel=0.02)
        b.box((sx, 0.0, 2.25), (0.42, 0.42, 2.85), CREAM, bevel=0.02)
    # Wide overhanging stylized canopy roof with blue & amber racing stripes
    b.box((0.0, 0.0, 3.82), (6.2, 3.8, 0.62), CREAM, bevel=0.03)
    b.box((0.0, 0.0, 3.92), (6.32, 3.92, 0.20), BLUE, bevel=0.02)
    b.box((0.0, 0.0, 3.64), (6.30, 3.90, 0.14), AMBER, bevel=0.015)
    b.box((0.0, 0.0, 4.18), (6.0, 3.6, 0.10), SLATE_DARK)
    # Two vintage chunky red/amber fuel pumps on the island
    for px in (-0.72, 0.72):
        b.box((px, 0.0, 0.92), (0.64, 0.52, 1.28), CRIMSON, bevel=0.025)
        b.box((px, -0.26, 1.12), (0.44, 0.08, 0.42), CREAM)
        b.box((px, 0.0, 1.62), (0.48, 0.36, 0.22), AMBER, bevel=0.02)
        b.box((px + 0.36, 0.0, 0.95), (0.10, 0.12, 0.62), CHARCOAL)
    return b.finish(loc)


def make_clock_tower(loc):
    """13: Chunky American Town-Hall Brick Clock Tower (3.4 x 3.4 x 8.4m)."""
    b = LandmarkBuilder("Stylized_Landmark_ClockTower")
    w = 3.2
    b.box((0.0, 0.0, 0.95), (w + 0.28, w + 0.28, 1.90), STONE_MID, bevel=0.03)
    b.flared_box((0.0, 0.0, 3.65), (w - 0.18, w - 0.18, 3.65), BRICK, flare=1.02, bevel=0.03)
    b.box((0.0, 0.0, 1.92), (w + 0.36, w + 0.36, 0.22), CONC_LIGHT, bevel=0.02)
    # Upper clock chamber
    b.box((0.0, 0.0, 6.25), (w + 0.12, w + 0.12, 1.65), CREAM, bevel=0.03)
    b.box((0.0, 0.0, 5.46), (w + 0.32, w + 0.32, 0.20), CONC_LIGHT, bevel=0.02)
    # 4-sided clock faces
    for dx, dy, rot in ((0, -1.68, (math.pi * 0.5, 0, 0)), (0, 1.68, (math.pi * 0.5, 0, 0)), (-1.68, 0, (0, math.pi * 0.5, 0)), (1.68, 0, (0, math.pi * 0.5, 0))):
        b.cyl((dx, dy, 6.28), 0.62, 0.12, SLATE_DARK, segs=12, rot=rot)
        b.cyl((dx * 1.02, dy * 1.02, 6.28), 0.50, 0.10, AMBER, segs=12, rot=rot)
    # Steep pyramidal slate spire + amber finial
    b.cyl((0.0, 0.0, 7.18), 2.38, 0.24, CONC_LIGHT, segs=4, rot=(0, 0, math.pi * 0.25))
    b.cyl((0.0, 0.0, 8.25), 2.25, 1.95, SLATE_DARK, r2=0.14, segs=4, rot=(0, 0, math.pi * 0.25))
    b.cyl((0.0, 0.0, 9.48), 0.12, 0.65, AMBER, r2=0.03, segs=8)
    # Arched wooden front entry door
    b.box((0.0, -1.72, 0.92), (1.05, 0.18, 1.65), WOOD_DARK, bevel=0.02)
    b.box((0.0, -1.78, 0.92), (0.76, 0.10, 1.42), AMBER)
    return b.finish(loc)


def make_fountain_plaza(loc):
    """14: Stylized American Town Plaza Octagonal Tiered Stone Fountain (4.4 x 4.4 x 2.6m)."""
    b = LandmarkBuilder("Stylized_Landmark_FountainPlaza")
    b.cyl((0.0, 0.0, 0.12), 2.25, 0.24, STONE_MID, segs=8)
    b.cyl((0.0, 0.0, 0.44), 2.02, 0.52, CONC_LIGHT, segs=8)
    # Cyan water basin
    b.cyl((0.0, 0.0, 0.62), 1.72, 0.20, WATER_CYAN, segs=8)
    # Central tiered stone pedestal & bowls
    b.cyl((0.0, 0.0, 1.05), 0.52, 0.85, STONE_MID, segs=8)
    b.cyl((0.0, 0.0, 1.48), 1.02, 0.22, CONC_LIGHT, r2=0.88, segs=8)
    b.cyl((0.0, 0.0, 1.56), 0.82, 0.08, WATER_CYAN, segs=8)
    b.cyl((0.0, 0.0, 1.95), 0.28, 0.68, STONE_MID, segs=8)
    b.cyl((0.0, 0.0, 2.34), 0.54, 0.18, CONC_LIGHT, segs=8)
    b.cyl((0.0, 0.0, 2.65), 0.14, 0.45, WATER_CYAN, r2=0.04, segs=8)
    return b.finish(loc)


def make_water_tower(loc):
    """15: Iconic Tall American Wooden Barrel Water Tower (3.4 x 3.4 x 6.8m)."""
    b = LandmarkBuilder("Stylized_Landmark_WaterTower")
    # 4 sturdy steel truss legs on concrete pads
    for sx in (-1.0, 1.0):
        for sy in (-1.0, 1.0):
            b.box((sx * 1.25, sy * 1.25, 0.20), (0.52, 0.52, 0.40), CONC_ASH, bevel=0.02)
            b.box((sx * 1.05, sy * 1.05, 2.05), (0.22, 0.22, 3.85), IRON_DARK, rot=(sy * -0.10, sx * 0.10, 0.0))
    # Cross steel tie-beams
    for hz in (1.45, 2.85):
        b.box((0.0, -1.05, hz), (2.15, 0.12, 0.14), IRON_DARK)
        b.box((0.0, 1.05, hz), (2.15, 0.12, 0.14), IRON_DARK)
        b.box((-1.05, 0.0, hz), (0.12, 2.15, 0.14), IRON_DARK)
        b.box((1.05, 0.0, hz), (0.12, 2.15, 0.14), IRON_DARK)
    # Central water standpipe
    b.cyl((0.0, 0.0, 1.95), 0.24, 3.90, SLATE_DARK, segs=8)
    # Wooden catwalk deck + huge wooden barrel tank with iron bands + conical slate roof
    b.cyl((0.0, 0.0, 3.92), 1.58, 0.16, WOOD_DARK, segs=12)
    b.cyl((0.0, 0.0, 5.02), 1.22, 2.08, WOOD_CEDAR, segs=12)
    for hz in (4.35, 4.95, 5.55):
        b.cyl((0.0, 0.0, hz), 1.26, 0.08, IRON_DARK, segs=12)
    b.cyl((0.0, 0.0, 6.48), 1.48, 0.88, SLATE_DARK, r2=0.08, segs=12)
    b.cyl((0.0, 0.0, 7.05), 0.06, 0.42, AMBER, segs=6)
    return b.finish(loc)


def make_diner_retro(loc):
    """16: Iconic 1950s American Chrome & Teal Retro Roadside Diner (5.8 x 3.8 x 3.8m)."""
    b = LandmarkBuilder("Stylized_Landmark_DinerRetro")
    w, d = 5.4, 3.6
    b.box((0.0, 0.0, 0.22), (w + 0.24, d + 0.24, 0.44), STONE_DARK, bevel=0.03)
    b.flared_box((0.0, 0.0, 1.58), (w, d, 2.35), TEAL, flare=1.03, bevel=0.03)
    # Iconic 1950s chrome/silver horizontal fluted trim bands
    b.box((0.0, 0.0, 0.56), (w + 0.18, d + 0.18, 0.22), SILVER, bevel=0.02)
    b.box((0.0, 0.0, 2.78), (w + 0.46, d + 0.46, 0.34), SILVER, bevel=0.03)
    b.box((0.0, 0.0, 2.78), (w + 0.52, d + 0.52, 0.12), CRIMSON)
    b.box((0.0, 0.0, 3.04), (w - 0.15, d - 0.15, 0.16), SLATE_DARK)
    # Huge cantilevered retro rooftop "DINER" signboard in amber & crimson
    b.box((0.0, -1.55, 3.58), (3.45, 0.30, 0.82), AMBER, bevel=0.025)
    b.box((0.0, -1.55, 4.02), (3.62, 0.36, 0.12), CRIMSON, bevel=0.015)
    # Center chrome entrance vestibule + big wrap-around warm lit diner windows
    b.box((0.0, -1.96, 1.28), (1.32, 0.42, 2.15), SILVER, bevel=0.025)
    b.box((0.0, -2.18, 1.18), (0.86, 0.08, 1.72), GLASS_WARM)
    for wx in (-1.68, 1.68):
        b.box((wx, -1.84, 1.52), (1.65, 0.14, 1.05), SILVER, bevel=0.015)
        b.box((wx, -1.89, 1.52), (1.42, 0.08, 0.84), GLASS_WARM)
    return b.finish(loc)


def make_wooden_pier_dock(loc):
    """17: Weathered Cedar American Waterfront Pier Dock with Pilings & Supply Crates."""
    b = LandmarkBuilder("Stylized_Landmark_WoodenPierDock")
    w, d = 4.6, 3.4
    # Thick timber pilings with mooring caps
    for sx in (-2.05, 0.0, 2.05):
        for sy in (-1.45, 1.45):
            b.cyl((sx, sy, 0.48), 0.19, 0.96, WOOD_DARK, segs=8)
            if sy < 0:
                b.cyl((sx, sy, 1.04), 0.21, 0.18, WOOD_CEDAR, segs=8)
    # Individual beveled wooden deck planks
    for i in range(9):
        py = -1.48 + i * 0.37
        b.box((0.0, py, 0.56), (w, 0.33, 0.12), WOOD_CEDAR if i % 2 == 0 else WOOD_OAK, bevel=0.015)
    # Stacked harbor wooden crates + iron mooring cleat
    b.box((-1.35, 0.72, 0.96), (0.76, 0.76, 0.68), WOOD_OAK, bevel=0.02)
    b.box((-0.62, 0.95, 0.88), (0.58, 0.58, 0.52), AMBER, bevel=0.02)
    b.box((1.55, -1.20, 0.68), (0.42, 0.18, 0.14), IRON_DARK)
    return b.finish(loc)


ROW_A = [
    make_warehouse_hangar((-22.0, 0.0, 0.0)),
    make_cafe_patio((-15.5, 0.0, 0.0)),
    make_market_stall((-10.2, 0.0, 0.0), "Stylized_Landmark_MarketStall_Red", CRIMSON, CREAM),
    make_market_stall((-5.8, 0.0, 0.0), "Stylized_Landmark_MarketStall_Blue", BLUE, AMBER),
    make_brownstone_row((0.5, 0.0, 0.0)),
    make_camp_tent((6.5, 0.0, 0.0), "Stylized_Landmark_CampTentGreen", OLIVE, WOOD_CEDAR),
    make_camp_tent((11.2, 0.0, 0.0), "Stylized_Landmark_CampTentTan", CANVAS_TAN, AMBER),
    make_guard_watchtower((16.5, 0.0, 0.0)),
    make_park_gazebo((22.2, 0.0, 0.0)),
]

ROW_B = [
    make_subway_entrance((-21.0, 11.0, 0.0)),
    make_billboard_sign((-14.5, 11.0, 0.0)),
    make_gas_station_canopy((-7.2, 11.0, 0.0)),
    make_clock_tower((-0.8, 11.0, 0.0)),
    make_fountain_plaza((5.2, 11.0, 0.0)),
    make_water_tower((11.2, 11.0, 0.0)),
    make_diner_retro((17.5, 11.0, 0.0)),
    make_wooden_pier_dock((24.0, 11.0, 0.0)),
]

ALL_LANDMARKS = ROW_A + ROW_B

for obj in ALL_LANDMARKS:
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
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((1.0, 5.5, -0.15)) @ Matrix.Diagonal((58.0, 24.0, 0.30, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_FLOOR])
floor_obj = bpy.data.objects.new("Preview_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

bpy.ops.object.camera_add(location=(0.5, -28.0, 14.0))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 54.0
point_at(camera, (0.5, 0.0, 2.4))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(12.0, -18.0, 22.0))
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
scene.render.resolution_x = 2100
scene.render.resolution_y = 860
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.14, 0.17, 0.22)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

for o in ROW_B:
    o.hide_render = True
scene.render.filepath = PREVIEW_A_PATH
bpy.ops.render.render(write_still=True)

for o in ROW_A:
    o.hide_render = True
for o in ROW_B:
    o.hide_render = False
camera.location = (1.5, -17.0, 15.0)
point_at(camera, (1.5, 11.0, 3.0))
scene.render.filepath = PREVIEW_B_PATH
bpy.ops.render.render(write_still=True)

for o in ALL_LANDMARKS:
    o.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("All 17 Stylized American Landmarks generated successfully!")

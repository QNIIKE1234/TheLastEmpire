import math
import os
import random
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Stylized_US_Props_Monuments_Source.blend")
PREVIEW_PROPS_PATH = os.path.join(OUT_DIR, "Stylized_US_StreetProps_Preview.png")
PREVIEW_MONUMENTS_PATH = os.path.join(OUT_DIR, "Stylized_US_Monuments_Preview.png")

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
    make_material("Rust | orange oxide", (0.3800, 0.1400, 0.0600), 0.88, 0.0),
    make_material("Hazard | cone orange", (0.5800, 0.2100, 0.0300), 0.88, 0.0),
    make_material("Paint | dumpster olive", (0.0750, 0.1650, 0.1050), 0.86, 0.05),
    make_material("Copper | liberty patina", (0.1400, 0.2950, 0.2450), 0.88, 0.0),
    make_material("Stone | charcoal gray", (0.0900, 0.1000, 0.1300), 0.90, 0.0),
    make_material("Mortar | pale sand", (0.1600, 0.1600, 0.1800), 0.90, 0.0),
    make_material("Concrete | cracked ash", (0.2400, 0.2500, 0.2800), 0.90, 0.0),
    make_material("Concrete | broken aggregate", (0.3400, 0.3500, 0.3800), 0.90, 0.0),
    make_material("Stucco | dusty cream", (0.4700, 0.4550, 0.4150), 0.90, 0.0),
    make_material("Foliage | cozy meadow green", (0.1550, 0.3850, 0.1350), 0.90, 0.0),
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
    WOOD_OAK,
    WOOD_DARK,
    BRIDGE_RED,
    CONE_ORANGE,
    DUMPSTER_GREEN,
    PATINA,
    STONE_DARK,
    STONE_MID,
    CONC_ASH,
    CONC_LIGHT,
    CREAM,
    MOSS_GREEN,
    GLASS_BLUE,
    GLASS_WARM,
    PREVIEW_FLOOR,
) = range(len(MATS))


class PropBuilder:
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
            bmesh.ops.bevel(src, geom=list(src.edges), offset=min(bevel, min(sx, sy, sz) * 0.22), offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES")
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def cyl(self, center, r1, h, mat_idx, r2=None, segs=10, rot=(0.0, 0.0, 0.0)):
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=segs, radius1=r1, radius2=r1 if r2 is None else r2, depth=h)
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def rock_chunk(self, center, scale, mat_idx, seed=1, rot=(0.0, 0.0, 0.0), subdiv=1, jitter=0.24):
        rng = random.Random(seed)
        src = bmesh.new()
        bmesh.ops.create_icosphere(src, subdivisions=subdiv, radius=1.0)
        sx, sy, sz = scale
        for v in src.verts:
            d = 1.0 + rng.uniform(-jitter, jitter)
            co = v.co * d
            if co.z < 0.0:
                co.z *= 0.45
            v.co = Vector((co.x * sx, co.y * sy, co.z * sz))
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

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
# 8 STYLIZED AMERICAN STREET PROPS
# =====================================================================

def make_prop_boulder_cluster(loc):
    """Crisp Flat-Shaded Low-Poly Granite Boulder Cluster (replaces bowling-ball spheres!)."""
    b = PropBuilder("Stylized_Prop_BoulderCluster")
    b.rock_chunk((-0.22, 0.06, 0.48), (0.96, 0.84, 0.78), CONC_ASH, seed=11, rot=(0.15, -0.1, 0.4), subdiv=2, jitter=0.16)
    b.rock_chunk((0.72, -0.24, 0.34), (0.68, 0.62, 0.56), STONE_MID, seed=12, rot=(-0.12, 0.18, 1.3), subdiv=1, jitter=0.20)
    b.rock_chunk((-0.78, -0.44, 0.24), (0.50, 0.46, 0.40), STONE_DARK, seed=13, rot=(0.20, 0.08, 2.2), subdiv=1, jitter=0.20)
    b.rock_chunk((0.18, 0.66, 0.22), (0.46, 0.40, 0.34), CONC_LIGHT, seed=14, rot=(-0.08, 0.22, 0.9), subdiv=1, jitter=0.18)
    # Small flat-shaded moss tuft & pebbles at base
    b.rock_chunk((0.22, -0.62, 0.12), (0.28, 0.24, 0.18), STONE_DARK, seed=15, subdiv=1, jitter=0.18)
    b.rock_chunk((-0.18, -0.48, 0.16), (0.34, 0.30, 0.24), MOSS_GREEN, seed=16, subdiv=1, jitter=0.15)
    b.rock_chunk((0.55, 0.32, 0.14), (0.28, 0.26, 0.20), MOSS_GREEN, seed=17, subdiv=1, jitter=0.15)
    return b.finish(loc)


def make_prop_streetlight(loc):
    """Chunky Stylized American Cast-Iron Streetlamp with Warm Amber Lantern."""
    b = PropBuilder("Stylized_Prop_Streetlight")
    b.cyl((0.0, 0.0, 0.12), 0.34, 0.24, STONE_DARK, r2=0.28, segs=8)
    b.cyl((0.0, 0.0, 0.48), 0.22, 0.56, SLATE_DARK, r2=0.14, segs=8)
    b.cyl((0.0, 0.0, 2.35), 0.10, 3.65, IRON_DARK, r2=0.075, segs=8)
    b.cyl((0.0, 0.0, 4.22), 0.12, 0.18, SLATE_DARK, r2=0.04, segs=8)
    # Cantilevered bracket arm + diagonal scroll brace
    b.box((-0.52, 0.0, 4.02), (1.05, 0.10, 0.10), IRON_DARK, bevel=0.015)
    b.box((-0.34, 0.0, 3.74), (0.72, 0.07, 0.07), SLATE_DARK, rot=(0.0, -0.62, 0.0))
    # Vintage hooded lantern head + warm amber glowing glass
    b.cyl((-0.96, 0.0, 3.92), 0.32, 0.14, SLATE_DARK, r2=0.16, segs=8)
    b.cyl((-0.96, 0.0, 3.74), 0.20, 0.26, GLASS_WARM, r2=0.15, segs=8)
    b.cyl((-0.96, 0.0, 3.58), 0.12, 0.08, IRON_DARK, r2=0.04, segs=8)
    return b.finish(loc)


def make_prop_bench(loc):
    """Chunky Stylized American Park Bench (Cast-Iron Frame + Cedar Slats)."""
    b = PropBuilder("Stylized_Prop_Bench")
    for sx in (-0.76, 0.76):
        b.box((sx, 0.0, 0.28), (0.12, 0.62, 0.56), IRON_DARK, bevel=0.015)
        b.box((sx, 0.24, 0.78), (0.10, 0.12, 0.64), IRON_DARK, rot=(-0.18, 0.0, 0.0), bevel=0.015)
        b.box((sx, -0.02, 0.72), (0.12, 0.54, 0.08), IRON_DARK, bevel=0.015)
    # Seat & backrest beveled cedar slats
    for sy in (-0.19, 0.0, 0.19):
        b.box((0.0, sy, 0.54), (1.82, 0.15, 0.07), WOOD_CEDAR, bevel=0.012)
    for sz, sy in ((0.76, 0.24), (0.94, 0.27), (1.12, 0.30)):
        b.box((0.0, sy, sz), (1.82, 0.07, 0.14), WOOD_OAK if sz == 0.94 else WOOD_CEDAR, rot=(-0.18, 0.0, 0.0), bevel=0.012)
    return b.finish(loc)


def make_prop_mailbox(loc):
    """Iconic US Postal Service Blue Street Collection Box."""
    b = PropBuilder("Stylized_Prop_Mailbox")
    for sx in (-0.28, 0.28):
        for sy in (-0.24, 0.24):
            b.box((sx, sy, 0.14), (0.10, 0.10, 0.28), IRON_DARK, bevel=0.01)
    b.box((0.0, 0.0, 0.82), (0.68, 0.58, 1.12), BLUE, bevel=0.025)
    # Arched USPS top dome
    b.cyl((0.0, 0.0, 1.38), 0.29, 0.68, BLUE, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
    # Mail chute hood + cream/amber USPS badge stripe
    b.box((0.0, -0.32, 1.16), (0.50, 0.14, 0.24), SLATE_DARK, rot=(0.24, 0.0, 0.0), bevel=0.015)
    b.box((0.0, -0.30, 0.76), (0.48, 0.05, 0.14), CREAM)
    b.box((0.0, -0.30, 0.62), (0.48, 0.05, 0.08), AMBER)
    return b.finish(loc)


def make_prop_traffic_cone(loc):
    """Chunky Low-Poly American Safety Traffic Cone with Double Reflective Bands."""
    b = PropBuilder("Stylized_Prop_TrafficCone")
    b.box((0.0, 0.0, 0.06), (0.68, 0.68, 0.12), CHARCOAL, bevel=0.02)
    b.cyl((0.0, 0.0, 0.44), 0.24, 0.72, CONE_ORANGE, r2=0.075, segs=10)
    b.cyl((0.0, 0.0, 0.36), 0.205, 0.11, CREAM, r2=0.180, segs=10)
    b.cyl((0.0, 0.0, 0.56), 0.155, 0.09, CREAM, r2=0.135, segs=10)
    return b.finish(loc)


def make_prop_dumpster(loc):
    """Chunky American Alley Steel Dumpster with Split Rubber Lids & Forklift Pockets."""
    b = PropBuilder("Stylized_Prop_Dumpster")
    b.box((0.0, 0.0, 0.62), (2.15, 1.32, 1.12), DUMPSTER_GREEN, bevel=0.03)
    # Structural side ribs + forklift channels
    for rx in (-0.65, 0.0, 0.65):
        b.box((rx, -0.68, 0.60), (0.10, 0.06, 0.96), SLATE_DARK)
    for sx in (-1.12, 1.12):
        b.box((sx, 0.0, 0.52), (0.16, 0.42, 0.22), IRON_DARK, bevel=0.015)
    # Amber corner hazard stripes
    for sx in (-1.04, 1.04):
        b.box((sx, -0.66, 0.98), (0.16, 0.06, 0.22), AMBER)
    # Split slanted black rubber lids + caster wheels
    for lx in (-0.52, 0.52):
        b.box((lx, 0.02, 1.24), (1.02, 1.38, 0.14), CHARCOAL, rot=(0.08, 0.0, 0.0), bevel=0.02)
    for sx in (-0.82, 0.82):
        for sy in (-0.46, 0.46):
            b.cyl((sx, sy, 0.08), 0.09, 0.10, CHARCOAL, segs=8, rot=(0.0, math.pi * 0.5, 0.0))
    return b.finish(loc)


def make_prop_wooden_fence(loc):
    """Chunky Stylized American Weathered Cedar Plank & Rail Fence Section."""
    b = PropBuilder("Stylized_Prop_WoodenFence")
    for px in (-1.28, 0.0, 1.28):
        b.box((px, 0.0, 0.68), (0.18, 0.18, 1.36), WOOD_DARK, bevel=0.015)
        b.box((px, 0.0, 1.40), (0.24, 0.24, 0.10), WOOD_CEDAR, bevel=0.015)
    for rz in (0.36, 0.74, 1.12):
        b.box((0.0, -0.06, rz), (2.75, 0.08, 0.16), WOOD_CEDAR if rz != 0.74 else WOOD_OAK, bevel=0.012)
    b.box((-0.64, 0.04, 0.74), (1.30, 0.07, 0.11), WOOD_OAK, rot=(0.0, 0.48, 0.0))
    b.box((0.64, 0.04, 0.74), (1.30, 0.07, 0.11), WOOD_OAK, rot=(0.0, -0.48, 0.0))
    return b.finish(loc)


def make_prop_trash_can(loc):
    """Chunky Stylized American Slatted Street Trash Bin with Hooded Top."""
    b = PropBuilder("Stylized_Prop_TrashCan")
    b.cyl((0.0, 0.0, 0.06), 0.36, 0.12, STONE_DARK, segs=10)
    b.cyl((0.0, 0.0, 0.52), 0.32, 0.84, SLATE_DARK, r2=0.35, segs=10)
    b.cyl((0.0, 0.0, 0.94), 0.38, 0.09, IRON_DARK, segs=10)
    # Hooded rain dome with trash opening
    b.cyl((0.0, 0.0, 1.14), 0.35, 0.28, SLATE_DARK, r2=0.22, segs=10)
    b.box((0.0, -0.28, 1.06), (0.36, 0.14, 0.18), CHARCOAL)
    b.cyl((0.0, 0.0, 0.78), 0.355, 0.07, AMBER, segs=10)
    return b.finish(loc)


# =====================================================================
# 7 ICONIC STYLIZED AMERICAN MONUMENTS
# =====================================================================

def make_statue_of_liberty(loc):
    b = PropBuilder("Stylized_Landmark_StatueOfLiberty")
    b.box((0.0, 0.0, 0.45), (3.4, 3.4, 0.90), STONE_MID, bevel=0.04)
    b.box((0.0, 0.0, 1.65), (2.4, 2.4, 1.65), CONC_LIGHT, bevel=0.04)
    b.box((0.0, 0.0, 2.55), (2.6, 2.6, 0.22), STONE_MID, bevel=0.03)
    # Chunky Chibi copper-patina robed figure
    b.cyl((0.0, 0.0, 4.15), 0.92, 3.05, PATINA, r2=0.58, segs=8)
    b.cyl((0.0, 0.0, 5.95), 0.46, 0.78, PATINA, r2=0.42, segs=8)
    # Spiked crown
    for i in range(7):
        ang = -1.2 + i * 0.4
        b.box((math.sin(ang) * 0.52, -0.18, 6.45 + math.cos(ang) * 0.22), (0.09, 0.09, 0.48), PATINA, rot=(0.0, ang, 0.0))
    # Tablet + raised torch arm with amber flame
    b.box((-0.78, -0.28, 4.45), (0.34, 0.22, 0.78), PATINA, rot=(0.0, 0.2, 0.0), bevel=0.02)
    b.box((0.76, -0.12, 6.25), (0.28, 0.28, 1.85), PATINA, rot=(0.0, -0.14, 0.0), bevel=0.02)
    b.cyl((0.92, -0.12, 7.28), 0.24, 0.28, AMBER, r2=0.14, segs=8)
    b.cyl((0.92, -0.12, 7.62), 0.16, 0.45, AMBER, r2=0.03, segs=8)
    return b.finish(loc)


def make_capitol_dome(loc):
    b = PropBuilder("Stylized_Landmark_CapitolDome")
    b.box((0.0, 0.0, 0.28), (6.4, 4.4, 0.56), STONE_MID, bevel=0.03)
    b.box((0.0, 0.0, 1.55), (5.8, 3.8, 2.05), CREAM, bevel=0.03)
    b.box((0.0, 0.0, 2.66), (6.1, 4.1, 0.24), CONC_LIGHT, bevel=0.02)
    for px in (-2.2, -1.32, -0.44, 0.44, 1.32, 2.2):
        b.cyl((px, -2.02, 1.55), 0.16, 2.02, CONC_LIGHT, segs=8)
    b.cyl((0.0, 0.0, 3.55), 1.68, 1.65, CREAM, segs=12)
    b.cyl((0.0, 0.0, 4.95), 1.58, 1.25, CONC_LIGHT, r2=0.58, segs=12)
    b.cyl((0.0, 0.0, 5.95), 0.46, 0.85, CREAM, segs=10)
    b.cyl((0.0, 0.0, 6.58), 0.42, 0.42, AMBER, r2=0.05, segs=10)
    return b.finish(loc)


def make_lincoln_monument(loc):
    b = PropBuilder("Stylized_Landmark_LincolnMonument")
    b.box((0.0, 0.0, 0.32), (6.2, 4.6, 0.64), CONC_ASH, bevel=0.03)
    b.box((0.0, 0.0, 0.78), (5.6, 4.0, 0.32), CONC_LIGHT, bevel=0.02)
    b.box((0.0, 0.15, 2.15), (4.2, 2.6, 2.45), STONE_MID, bevel=0.02)
    for px in (-2.35, -1.41, -0.47, 0.47, 1.41, 2.35):
        b.cyl((px, -1.72, 2.18), 0.19, 2.52, CREAM, segs=8)
        b.cyl((px, 1.72, 2.18), 0.19, 2.52, CREAM, segs=8)
    b.box((0.0, 0.0, 3.62), (5.5, 3.9, 0.48), CREAM, bevel=0.03)
    b.box((0.0, 0.0, 4.18), (4.8, 3.2, 0.68), CONC_LIGHT, bevel=0.03)
    # Stylized seated bronze statue silhouette inside
    b.box((0.0, -0.55, 1.55), (0.95, 0.85, 1.15), WOOD_DARK, bevel=0.02)
    b.box((0.0, -0.45, 2.32), (0.58, 0.52, 0.65), AMBER, bevel=0.02)
    return b.finish(loc)


def make_golden_gate_tower(loc):
    b = PropBuilder("Stylized_Landmark_GoldenGateTower")
    for sx in (-1.65, 1.65):
        b.box((sx, 0.0, 0.42), (1.25, 1.25, 0.84), CONC_ASH, bevel=0.03)
        b.box((sx, 0.0, 4.65), (0.76, 0.86, 7.85), BRIDGE_RED, bevel=0.03)
        b.box((sx, 0.0, 8.72), (0.56, 0.64, 0.45), BRIDGE_RED, bevel=0.02)
    for hz in (2.45, 4.55, 6.45, 8.15):
        b.box((0.0, 0.0, hz), (3.45, 0.48, 0.52), BRIDGE_RED, bevel=0.02)
    # Road deck crossbeam
    b.box((0.0, 0.0, 2.15), (4.8, 1.45, 0.34), SLATE_DARK, bevel=0.02)
    return b.finish(loc)


def make_space_needle(loc):
    b = PropBuilder("Stylized_Landmark_SpaceNeedle")
    b.cyl((0.0, 0.0, 0.25), 2.15, 0.50, STONE_MID, segs=9)
    # Tripod curved-look steel legs
    for i in range(3):
        ang = i * (math.tau / 3.0)
        b.box((math.cos(ang) * 1.12, math.sin(ang) * 1.12, 2.35), (0.34, 0.34, 4.25), CREAM, rot=(math.sin(ang) * 0.22, -math.cos(ang) * 0.22, 0.0), bevel=0.02)
        b.box((math.cos(ang) * 0.65, math.sin(ang) * 0.65, 5.45), (0.28, 0.28, 2.65), CREAM, rot=(-math.sin(ang) * 0.16, math.cos(ang) * 0.16, 0.0), bevel=0.02)
    b.cyl((0.0, 0.0, 3.65), 0.52, 6.45, SLATE_DARK, segs=9)
    # Iconic flared observation saucer deck + glass ring + amber spire
    b.cyl((0.0, 0.0, 6.82), 1.85, 0.32, CREAM, r2=2.25, segs=12)
    b.cyl((0.0, 0.0, 7.22), 2.12, 0.52, GLASS_BLUE, segs=12)
    b.cyl((0.0, 0.0, 7.62), 2.28, 0.28, AMBER, r2=1.15, segs=12)
    b.cyl((0.0, 0.0, 8.65), 0.12, 1.85, SILVER, r2=0.03, segs=8)
    return b.finish(loc)


def make_hollywood_sign(loc):
    b = PropBuilder("Stylized_Landmark_HollywoodSign")
    b.box((0.0, 0.0, 0.22), (7.6, 1.25, 0.44), STONE_DARK, bevel=0.03)
    for i in range(7):
        lx = -3.0 + i * 1.0
        # Steel rear support struts
        b.box((lx, 0.32, 1.28), (0.10, 0.10, 2.15), IRON_DARK, rot=(0.24, 0.0, 0.0))
        # Chunky iconic white letter slabs
        h = 1.85 + (0.16 if i % 2 == 0 else 0.0)
        b.box((lx, -0.10, 0.44 + h * 0.5), (0.72, 0.20, h), CREAM, bevel=0.02)
        if i in (0, 2, 5):
            b.box((lx, -0.14, 0.44 + h * 0.52), (0.32, 0.12, 0.38), CHARCOAL)
    b.box((0.0, 0.16, 1.45), (7.0, 0.10, 0.12), IRON_DARK)
    return b.finish(loc)


def make_sears_tower(loc):
    """Chunky Stylized Chicago Willis/Sears Bundled-Tube Skyscraper."""
    b = PropBuilder("Stylized_Landmark_SearsTower")
    b.box((0.0, 0.0, 0.45), (4.8, 4.8, 0.90), STONE_DARK, bevel=0.03)
    tubes = [
        (-1.4, -1.4, 5.2), (0.0, -1.4, 6.8), (1.4, -1.4, 5.2),
        (-1.4, 0.0, 6.8),  (0.0, 0.0, 9.2),  (1.4, 0.0, 7.6),
        (-1.4, 1.4, 5.2),  (0.0, 1.4, 9.2),  (1.4, 1.4, 6.8),
    ]
    for tx, ty, th in tubes:
        b.box((tx, ty, 0.90 + th * 0.5), (1.36, 1.36, th), SLATE_DARK, bevel=0.025)
        b.box((tx, ty, 0.90 + th + 0.06), (1.30, 1.30, 0.10), SILVER)
        for wz in range(2, int(th), 2):
            b.box((tx, ty, 0.90 + wz), (1.40, 1.40, 0.24), GLASS_BLUE if wz % 4 != 0 else GLASS_WARM)
    for ax in (-0.35, 0.35):
        b.cyl((ax, 0.45, 11.45), 0.10, 2.75, CREAM, r2=0.03, segs=8)
    return b.finish(loc)


PROPS = [
    make_prop_boulder_cluster((-10.5, 0.0, 0.0)),
    make_prop_streetlight((-7.2, 0.0, 0.0)),
    make_prop_bench((-4.2, 0.0, 0.0)),
    make_prop_mailbox((-1.4, 0.0, 0.0)),
    make_prop_traffic_cone((1.0, 0.0, 0.0)),
    make_prop_dumpster((3.8, 0.0, 0.0)),
    make_prop_wooden_fence((7.4, 0.0, 0.0)),
    make_prop_trash_can((10.5, 0.0, 0.0)),
]

MONUMENTS = [
    make_statue_of_liberty((-22.0, 12.0, 0.0)),
    make_capitol_dome((-14.5, 12.0, 0.0)),
    make_lincoln_monument((-6.5, 12.0, 0.0)),
    make_golden_gate_tower((1.2, 12.0, 0.0)),
    make_space_needle((8.2, 12.0, 0.0)),
    make_hollywood_sign((16.0, 12.0, 0.0)),
    make_sears_tower((24.0, 12.0, 0.0)),
]

ALL_ITEMS = PROPS + MONUMENTS

for obj in ALL_ITEMS:
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
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((1.0, 6.0, -0.15)) @ Matrix.Diagonal((58.0, 26.0, 0.30, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_FLOOR])
floor_obj = bpy.data.objects.new("Preview_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

bpy.ops.object.camera_add(location=(0.0, -16.0, 7.5))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 26.0
point_at(camera, (0.0, 0.0, 1.4))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(12.0, -18.0, 22.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.94, 0.82)
sun.data.energy = 3.0
sun.data.angle = math.radians(10.0)
point_at(sun, (0.0, 5.0, 2.0))

bpy.ops.object.light_add(type="AREA", location=(-14.0, -12.0, 12.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 1000.0
fill.data.size = 16.0
point_at(fill, (0.0, 5.0, 2.0))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 1900
scene.render.resolution_y = 850
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.14, 0.17, 0.22)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

for o in MONUMENTS:
    o.hide_render = True
scene.render.filepath = PREVIEW_PROPS_PATH
bpy.ops.render.render(write_still=True)

for o in PROPS:
    o.hide_render = True
for o in MONUMENTS:
    o.hide_render = False
camera.data.ortho_scale = 58.0
camera.location = (1.0, -22.0, 18.0)
point_at(camera, (1.0, 12.0, 4.5))
scene.render.filepath = PREVIEW_MONUMENTS_PATH
bpy.ops.render.render(write_still=True)

for o in ALL_ITEMS:
    o.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("All 8 Street Props & 7 US Monuments generated successfully!")

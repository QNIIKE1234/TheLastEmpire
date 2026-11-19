import math
import os
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Stylized_US_Vehicles_Source.blend")
PREVIEW_A_PATH = os.path.join(OUT_DIR, "Stylized_US_Vehicles_Preview_A.png")
PREVIEW_B_PATH = os.path.join(OUT_DIR, "Stylized_US_Vehicles_Preview_B.png")

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
    make_material("Hazard | faded amber", (0.7484, 0.3931, 0.0272), 0.90, 0.0),          # Signature NYC Taxi / Amber
    make_material("Titanium | graphite armor", (0.0550, 0.0637, 0.0890), 0.78, 0.08),    # Police Black / Armor
    make_material("Iron | dark forged steel", (0.0380, 0.0440, 0.0620), 0.78, 0.08),     # Chassis / Bullbar / Frame
    make_material("Charcoal | blackened", (0.0220, 0.0245, 0.0331), 0.94, 0.0),          # Rubber Tires / Grilles
    make_material("Titanium | bright bevel", (0.4770, 0.5382, 0.6383), 0.45, 0.55),      # Chrome Bumpers / Rims / Ladder
    make_material("Paint | faded survival blue", (0.0331, 0.0946, 0.3000), 0.88, 0.0),   # Sedan / Bus / Police Blue
    make_material("Paint | fire crimson", (0.4400, 0.0750, 0.0550), 0.88, 0.0),          # Fire Truck / Cruiser Red
    make_material("Rust | orange oxide", (0.3600, 0.1300, 0.0450), 0.88, 0.0),           # Pickup / Scrambler Rust
    make_material("Paint | muted teal", (0.0550, 0.2100, 0.2050), 0.88, 0.0),            # Retro Scooter Teal
    make_material("Stucco | dusty cream", (0.4700, 0.4600, 0.4350), 0.90, 0.0),          # Police White / Cream Trim
    make_material("Wood | weathered cedar", (0.3600, 0.2300, 0.1200), 0.88, 0.0),        # Truck Crates / Leather
    make_material("Wood | charred and stained", (0.1200, 0.0700, 0.0350), 0.90, 0.0),    # Seats / Dark Trim
    make_material("Glass | dusty blue", (0.0650, 0.1450, 0.2100), 0.45, 0.0, 0.25),      # Windshields / Windows
    make_material("Signal | siren red", (0.6200, 0.0700, 0.0600), 0.45, 0.0, 0.22),      # Taillights / Red Beacon
    make_material("Signal | siren blue", (0.0500, 0.2200, 0.6400), 0.45, 0.0, 0.22),     # Police Blue Beacon
    make_material("Signal | headlight warm", (0.8200, 0.6800, 0.3200), 0.45, 0.0, 0.22), # Headlights / Taxi Sign
    make_material("Preview | stage floor", (0.1200, 0.1300, 0.1500), 0.95, 0.0),
]

(
    AMBER,
    SLATE_DARK,
    IRON_DARK,
    RUBBER,
    SILVER,
    BLUE,
    CRIMSON,
    RUST,
    TEAL,
    CREAM,
    WOOD_CEDAR,
    SEAT_DARK,
    GLASS_BLUE,
    SIGNAL_RED,
    SIGNAL_BLUE,
    HEADLIGHT,
    PREVIEW_FLOOR,
) = range(len(MATS))


class VehicleBuilder:
    """Chunky Avatar-Star-inspired Chibi American Vehicle Builder (+Y = forward, +Z = up in Blender)."""

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
            bmesh.ops.bevel(src, geom=list(src.edges), offset=min(bevel, min(sx, sy, sz) * 0.24), offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES")
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def tapered_cabin(self, center, size, mat_idx, top_scale_x=0.86, top_scale_y=0.82, bevel=0.04):
        """Chunky stylized vehicle cabin with slanted windshield & pillars."""
        src = bmesh.new()
        bmesh.ops.create_cube(src, size=1.0)
        sx, sy, sz = size
        for v in src.verts:
            mx = top_scale_x if v.co.z > 0.0 else 1.0
            my = top_scale_y if v.co.z > 0.0 else 1.0
            v.co = Vector((v.co.x * sx * mx, v.co.y * sy * my, v.co.z * sz))
        if bevel > 0.0:
            bmesh.ops.bevel(src, geom=list(src.edges), offset=min(bevel, min(sx, sy, sz) * 0.16), offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES")
        self._add(src, mat_idx, Matrix.Translation(Vector(center)))

    def cyl(self, center, r1, h, mat_idx, r2=None, segs=12, rot=(0.0, 0.0, 0.0)):
        src = bmesh.new()
        bmesh.ops.create_cone(src, cap_ends=True, segments=segs, radius1=r1, radius2=r1 if r2 is None else r2, depth=h)
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add(src, mat_idx, mat)

    def wheel(self, x, y, radius=0.38, width=0.26):
        """Chunky stylized tire + silver alloy rim + hub bolt."""
        self.cyl((x, y, radius), radius, width, RUBBER, segs=12, rot=(0.0, math.pi * 0.5, 0.0))
        sgn = 1.0 if x >= 0 else -1.0
        self.cyl((x + sgn * (width * 0.25), y, radius), radius * 0.56, width * 0.62, SILVER, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
        self.cyl((x + sgn * (width * 0.45), y, radius), radius * 0.20, width * 0.30, IRON_DARK, segs=8, rot=(0.0, math.pi * 0.5, 0.0))

    def moto_wheel(self, y, radius=0.36, width=0.18):
        self.cyl((0.0, y, radius), radius, width, RUBBER, segs=12, rot=(0.0, math.pi * 0.5, 0.0))
        self.cyl((0.0, y, radius), radius * 0.58, width * 1.08, SILVER, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
        self.cyl((0.0, y, radius), radius * 0.20, width * 1.24, IRON_DARK, segs=8, rot=(0.0, math.pi * 0.5, 0.0))

    def lightbar(self, center_z, y, width=0.92):
        self.box((0.0, y, center_z - 0.04), (width + 0.08, 0.26, 0.08), IRON_DARK, bevel=0.015)
        self.box((-width * 0.28, y, center_z + 0.06), (width * 0.38, 0.22, 0.14), SIGNAL_RED, bevel=0.02)
        self.box((0.0, y, center_z + 0.06), (width * 0.16, 0.22, 0.14), CREAM, bevel=0.015)
        self.box((width * 0.28, y, center_z + 0.06), (width * 0.38, 0.22, 0.14), SIGNAL_BLUE, bevel=0.02)

    def lights_front_back(self, half_w, front_y, back_y, z):
        for sx in (-1.0, 1.0):
            self.box((sx * (half_w - 0.28), front_y, z), (0.34, 0.10, 0.18), HEADLIGHT, bevel=0.015)
            self.box((sx * (half_w - 0.28), back_y, z), (0.36, 0.10, 0.18), SIGNAL_RED, bevel=0.015)

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
# 1. STANDARD & NEW AMERICAN 4-WHEEL VEHICLES
# =====================================================================

def make_sedan_base(name, body_mat, door_mat, roof_mat, loc, is_police=False, is_taxi=False):
    b = VehicleBuilder(name)
    # Chassis undercarriage & chunky lower body
    b.box((0.0, 0.0, 0.34), (1.76, 3.85, 0.26), IRON_DARK, bevel=0.02)
    b.box((0.0, 0.0, 0.68), (1.92, 4.05, 0.56), body_mat, bevel=0.05)
    # Contrasting door panels (Police black/white) or Taxi checkered stripe
    if door_mat != body_mat:
        b.box((0.0, -0.05, 0.70), (1.96, 1.85, 0.48), door_mat, bevel=0.03)
    if is_taxi:
        for i in range(8):
            cy = -1.35 + i * 0.38
            m = RUBBER if i % 2 == 0 else CREAM
            for sx in (-0.98, 0.98):
                b.box((sx, cy, 0.66), (0.04, 0.34, 0.14), m)
    # Chunky flared wheel arches + 4 wheels
    for wx in (-0.94, 0.94):
        for wy in (-1.22, 1.24):
            b.wheel(wx, wy, 0.38, 0.26)
            b.box((wx * 0.96, wy, 0.58), (0.20, 0.92, 0.34), IRON_DARK, bevel=0.02)
    # Stylized tapered cabin + wraparound dusty glass
    b.tapered_cabin((0.0, -0.14, 1.24), (1.66, 2.08, 0.68), roof_mat, 0.86, 0.78, bevel=0.04)
    b.tapered_cabin((0.0, -0.14, 1.22), (1.70, 1.90, 0.44), GLASS_BLUE, 0.88, 0.80, bevel=0.02)
    # Center B-pillar divider
    b.box((0.0, -0.16, 1.24), (1.72, 0.16, 0.54), roof_mat)
    # Chrome bumpers + front radiator grille + headlights/taillights
    b.box((0.0, 2.08, 0.46), (1.98, 0.20, 0.22), SILVER, bevel=0.025)
    b.box((0.0, -2.08, 0.46), (1.98, 0.20, 0.22), SILVER, bevel=0.025)
    b.box((0.0, 2.04, 0.74), (0.92, 0.08, 0.24), RUBBER, bevel=0.015)
    b.lights_front_back(0.96, 2.04, -2.04, 0.74)
    # Side mirrors
    for sx in (-0.94, 0.94):
        b.box((sx, 0.52, 1.06), (0.18, 0.14, 0.12), body_mat, bevel=0.015)
    if is_police:
        # Heavy front police push-bumper + rooftop lightbar + gold badge
        b.box((0.0, 2.24, 0.62), (1.15, 0.12, 0.42), IRON_DARK, bevel=0.015)
        for sx in (-0.42, 0.42):
            b.box((sx, 2.24, 0.62), (0.12, 0.22, 0.54), IRON_DARK, bevel=0.015)
        b.lightbar(1.64, -0.05, 1.05)
        for sx in (-0.99, 0.99):
            b.box((sx, 0.08, 0.72), (0.04, 0.34, 0.24), AMBER)
    if is_taxi:
        # Iconic NYC glowing rooftop TAXI sign box
        b.box((0.0, -0.05, 1.70), (0.76, 0.28, 0.24), HEADLIGHT, bevel=0.02)
        b.box((0.0, -0.05, 1.70), (0.80, 0.20, 0.10), RUBBER)
    return b.finish(loc)


def make_police_suv(loc):
    """NEW: American Police Tactical SUV / Interceptor."""
    b = VehicleBuilder("Stylized_Vehicle_Police_SUV")
    b.box((0.0, 0.0, 0.38), (1.92, 4.18, 0.30), IRON_DARK, bevel=0.02)
    b.box((0.0, 0.0, 0.80), (2.10, 4.35, 0.64), SLATE_DARK, bevel=0.05)
    # White/Cream side doors + blue tactical accent stripe
    b.box((0.0, -0.15, 0.82), (2.14, 2.15, 0.54), CREAM, bevel=0.03)
    for sx in (-1.08, 1.08):
        b.box((sx, -0.15, 0.76), (0.04, 2.08, 0.14), BLUE)
        b.box((sx, 0.25, 0.84), (0.05, 0.34, 0.26), AMBER)
    # Tall squared-off SUV cabin
    b.tapered_cabin((0.0, -0.45, 1.48), (1.92, 2.85, 0.80), SLATE_DARK, 0.90, 0.88, bevel=0.04)
    b.tapered_cabin((0.0, -0.45, 1.48), (1.96, 2.62, 0.50), GLASS_BLUE, 0.91, 0.89, bevel=0.02)
    for py in (-1.15, -0.15):
        b.box((0.0, py, 1.48), (1.98, 0.18, 0.62), SLATE_DARK)
    # Oversized chunky SUV tires + wheel arches
    for wx in (-1.02, 1.02):
        for wy in (-1.32, 1.35):
            b.wheel(wx, wy, 0.44, 0.30)
            b.box((wx * 0.96, wy, 0.66), (0.22, 1.04, 0.38), IRON_DARK, bevel=0.02)
    # Heavy tactical bullbar + roof rack + big police lightbar + rear spare tire
    b.box((0.0, 2.22, 0.54), (2.16, 0.22, 0.26), IRON_DARK, bevel=0.02)
    b.box((0.0, 2.38, 0.80), (1.42, 0.14, 0.52), IRON_DARK, bevel=0.015)
    for sx in (-0.52, 0.52):
        b.box((sx, 2.36, 0.80), (0.14, 0.26, 0.64), IRON_DARK, bevel=0.015)
    b.box((0.0, -2.22, 0.54), (2.14, 0.20, 0.26), IRON_DARK, bevel=0.02)
    b.cyl((0.0, -2.24, 0.98), 0.38, 0.24, RUBBER, segs=12, rot=(math.pi * 0.5, 0.0, 0.0))
    b.cyl((0.0, -2.30, 0.98), 0.20, 0.16, SILVER, segs=10, rot=(math.pi * 0.5, 0.0, 0.0))
    b.lights_front_back(1.04, 2.20, -2.20, 0.86)
    b.lightbar(1.94, 0.28, 1.32)
    # Rooftop cargo rack behind lightbar
    b.box((0.0, -0.95, 1.94), (1.45, 1.35, 0.14), IRON_DARK, bevel=0.015)
    return b.finish(loc)


def make_fire_truck(loc):
    """NEW: Iconic American Hook-and-Ladder Fire Truck."""
    b = VehicleBuilder("Stylized_Vehicle_FireTruck")
    # Heavy chassis + bright crimson fire engine body
    b.box((0.0, 0.0, 0.44), (2.20, 6.25, 0.36), IRON_DARK, bevel=0.02)
    # Forward cab
    b.tapered_cabin((0.0, 2.05, 1.52), (2.28, 1.95, 1.88), CRIMSON, 0.94, 0.92, bevel=0.05)
    b.box((0.0, 2.98, 1.72), (1.96, 0.12, 0.72), GLASS_BLUE)
    for sx in (-1.15, 1.15):
        b.box((sx, 2.15, 1.72), (0.08, 1.15, 0.72), GLASS_BLUE)
    # Chrome front radiator grille + bumper + amber headlights + emergency lightbar
    b.box((0.0, 3.12, 0.62), (2.38, 0.28, 0.34), SILVER, bevel=0.03)
    b.box((0.0, 3.04, 1.08), (1.35, 0.12, 0.56), SILVER, bevel=0.02)
    b.box((0.0, 3.06, 1.08), (1.15, 0.10, 0.42), IRON_DARK)
    b.lights_front_back(1.12, 3.04, -3.14, 0.96)
    b.lightbar(2.54, 2.32, 1.62)
    # Rear crimson equipment body with silver roll-up tool lockers + amber hazard stripe
    b.box((0.0, -1.05, 1.44), (2.32, 4.15, 1.68), CRIMSON, bevel=0.05)
    b.box((0.0, 0.0, 1.02), (2.38, 6.18, 0.20), CREAM)
    b.box((0.0, 0.0, 0.84), (2.38, 6.18, 0.12), AMBER)
    for sx in (-1.18, 1.18):
        for ly in (-2.25, -1.05, 0.15):
            b.box((sx, ly, 1.48), (0.08, 0.96, 0.92), SILVER, bevel=0.015)
    # 6 heavy fire-engine wheels (front axle + dual rear axles)
    for wx in (-1.12, 1.12):
        b.wheel(wx, 2.02, 0.48, 0.32)
        b.wheel(wx, -1.15, 0.48, 0.34)
        b.wheel(wx, -2.25, 0.48, 0.34)
    # Iconic extendable silver turntable ladder along the roof!
    b.cyl((0.0, -2.15, 2.42), 0.52, 0.28, SLATE_DARK, segs=10)
    for lx in (-0.36, 0.36):
        b.box((lx, -0.35, 2.68), (0.11, 4.65, 0.22), SILVER, rot=(-0.06, 0.0, 0.0), bevel=0.015)
    for i in range(11):
        ry = -2.45 + i * 0.42
        rz = 2.55 + i * 0.025
        b.box((0.0, ry, rz), (0.68, 0.07, 0.07), SILVER)
    # Rear bumper + rolled hose pack
    b.box((0.0, -3.20, 0.58), (2.34, 0.24, 0.30), SILVER, bevel=0.02)
    b.cyl((0.0, -2.95, 2.38), 0.24, 1.65, AMBER, segs=10, rot=(0.0, math.pi * 0.5, 0.0))
    return b.finish(loc)


def make_delivery_van(loc):
    """Apoc_Vehicle_01 -> Chunky Tactical Amber-Gold American Utility Van."""
    b = VehicleBuilder("Stylized_Apoc_Vehicle_01_Damaged_Van")
    b.box((0.0, 0.0, 0.36), (1.88, 4.25, 0.28), IRON_DARK, bevel=0.02)
    b.box((0.0, -0.15, 0.84), (2.06, 4.42, 0.72), AMBER, bevel=0.05)
    b.tapered_cabin((0.0, -0.35, 1.56), (1.96, 3.45, 0.86), AMBER, 0.92, 0.92, bevel=0.05)
    # Wrap-around front windshield & side windows
    b.box((0.0, 1.32, 1.56), (1.68, 0.14, 0.58), GLASS_BLUE, rot=(-0.20, 0.0, 0.0))
    for sx in (-1.00, 1.00):
        b.box((sx, 0.65, 1.54), (0.08, 0.96, 0.54), GLASS_BLUE)
        b.box((sx, -0.75, 1.54), (0.08, 1.35, 0.54), GLASS_BLUE)
        b.box((sx, -0.15, 0.72), (0.06, 4.20, 0.18), SLATE_DARK)
    for wx in (-1.00, 1.00):
        for wy in (-1.35, 1.32):
            b.wheel(wx, wy, 0.42, 0.28)
    b.box((0.0, 2.12, 0.50), (2.12, 0.22, 0.26), SLATE_DARK, bevel=0.02)
    b.box((0.0, -2.40, 0.50), (2.12, 0.22, 0.26), SLATE_DARK, bevel=0.02)
    b.lights_front_back(1.02, 2.08, -2.38, 0.86)
    # Rooftop steel utility rack + spare crate
    b.box((0.0, -0.55, 2.08), (1.58, 2.25, 0.14), IRON_DARK, bevel=0.015)
    b.box((-0.28, -0.85, 2.28), (0.76, 0.88, 0.34), WOOD_CEDAR, bevel=0.02)
    return b.finish(loc)


def make_pickup_truck(loc):
    """Apoc_Vehicle_02 -> Chunky American Hauler Pickup Truck with Cargo Bed."""
    b = VehicleBuilder("Stylized_Apoc_Vehicle_02_Ruined_Truck")
    b.box((0.0, 0.0, 0.40), (1.96, 4.85, 0.30), IRON_DARK, bevel=0.02)
    # Front hood + rugged cab
    b.box((0.0, 1.25, 0.86), (2.12, 2.35, 0.68), RUST, bevel=0.05)
    b.tapered_cabin((0.0, 0.85, 1.56), (1.94, 1.48, 0.84), RUST, 0.90, 0.86, bevel=0.04)
    b.tapered_cabin((0.0, 0.85, 1.54), (1.98, 1.30, 0.52), GLASS_BLUE, 0.91, 0.88, bevel=0.02)
    # Rear pickup flatbed with raised wooden/steel side walls
    b.box((0.0, -1.20, 0.68), (2.12, 2.45, 0.32), SLATE_DARK, bevel=0.03)
    for sx in (-1.02, 1.02):
        b.box((sx, -1.20, 1.08), (0.12, 2.42, 0.56), RUST, bevel=0.02)
    b.box((0.0, -2.38, 1.08), (2.12, 0.14, 0.56), RUST, bevel=0.02)
    # Cargo crates & amber fuel barrel in bed
    b.box((-0.38, -0.95, 1.18), (0.86, 0.92, 0.72), WOOD_CEDAR, bevel=0.02)
    b.cyl((0.46, -1.35, 1.24), 0.32, 0.86, AMBER, segs=10)
    # Rollbar with twin amber offroad spotlights
    for sx in (-0.82, 0.82):
        b.box((sx, 0.08, 1.65), (0.12, 0.12, 0.92), IRON_DARK)
    b.box((0.0, 0.08, 2.08), (1.76, 0.12, 0.12), IRON_DARK)
    for lx in (-0.42, 0.42):
        b.cyl((lx, 0.14, 2.22), 0.14, 0.14, HEADLIGHT, segs=8, rot=(math.pi * 0.5, 0.0, 0.0))
    for wx in (-1.04, 1.04):
        for wy in (-1.45, 1.48):
            b.wheel(wx, wy, 0.46, 0.32)
    b.box((0.0, 2.48, 0.56), (2.18, 0.24, 0.28), SILVER, bevel=0.02)
    b.box((0.0, -2.48, 0.56), (2.16, 0.20, 0.26), IRON_DARK, bevel=0.02)
    b.lights_front_back(1.04, 2.44, -2.45, 0.92)
    return b.finish(loc)


def make_city_bus(loc):
    """Apoc_Vehicle_03 -> Chunky American Metro City Bus in Survival Blue & Cream."""
    b = VehicleBuilder("Stylized_Apoc_Vehicle_03_Abandoned_Bus")
    b.box((0.0, 0.0, 0.44), (2.28, 6.85, 0.34), IRON_DARK, bevel=0.02)
    b.box((0.0, 0.0, 1.02), (2.42, 7.05, 0.92), BLUE, bevel=0.05)
    b.tapered_cabin((0.0, 0.0, 1.98), (2.38, 6.88, 1.12), CREAM, 0.96, 0.97, bevel=0.05)
    # Amber transit stripe along sides + dark slate roof
    b.box((0.0, 0.0, 1.38), (2.46, 7.08, 0.18), AMBER)
    b.box((0.0, 0.0, 2.58), (2.26, 6.65, 0.14), SLATE_DARK, bevel=0.02)
    # Panoramic front windshield + destination sign + side passenger windows
    b.box((0.0, 3.44, 1.96), (2.06, 0.14, 0.78), GLASS_BLUE)
    b.box((0.0, 3.46, 2.44), (1.35, 0.10, 0.22), AMBER)
    for sx in (-1.20, 1.20):
        for wy in (-2.4, -1.2, 0.0, 1.2, 2.4):
            b.box((sx, wy, 1.96), (0.08, 0.96, 0.72), GLASS_BLUE)
    # Rooftop AC pods
    for ay in (-1.55, 1.25):
        b.box((0.0, ay, 2.78), (1.42, 1.35, 0.30), CONC_LIGHT if False else CREAM, bevel=0.03)
        b.box((0.0, ay, 2.94), (0.92, 0.85, 0.06), IRON_DARK)
    for wx in (-1.18, 1.18):
        for wy in (-2.15, 2.15):
            b.wheel(wx, wy, 0.48, 0.34)
    b.box((0.0, 3.58, 0.60), (2.46, 0.24, 0.30), SILVER, bevel=0.02)
    b.box((0.0, -3.58, 0.60), (2.46, 0.24, 0.30), SILVER, bevel=0.02)
    b.lights_front_back(1.18, 3.54, -3.54, 0.92)
    return b.finish(loc)


# =====================================================================
# 2. STYLIZED AMERICAN MOTORCYCLES (incl. NEW Police Motorcycle!)
# =====================================================================

def make_motorcycle(name, body_mat, accent_mat, loc, style="cruiser"):
    b = VehicleBuilder(name)
    # Front & rear chunky wheels
    r_front = 0.36 if style != "scooter" else 0.28
    r_rear = 0.36 if style != "scooter" else 0.28
    b.moto_wheel(0.76, r_front, 0.18)
    b.moto_wheel(-0.74, r_rear, 0.20)
    # Engine block & chassis frame
    b.box((0.0, 0.0, 0.48), (0.38, 0.92, 0.42), IRON_DARK, bevel=0.03)
    b.box((0.0, 0.02, 0.48), (0.44, 0.46, 0.32), SILVER, bevel=0.02)
    # Exhaust pipes
    for sx in (-0.24, 0.24):
        b.cyl((sx, -0.45, 0.36), 0.065, 0.95, SILVER, segs=8, rot=(math.pi * 0.5 + 0.08, 0.0, 0.0))
    # Front fork + wide handlebars + headlamp
    for sx in (-0.14, 0.14):
        b.cyl((sx, 0.64, 0.72), 0.045, 0.88, SILVER, segs=8, rot=(0.30, 0.0, 0.0))
    b.box((0.0, 0.50, 1.12), (0.86, 0.08, 0.08), IRON_DARK, bevel=0.015)
    b.cyl((0.0, 0.76, 0.92), 0.14, 0.18, HEADLIGHT, segs=10, rot=(math.pi * 0.5, 0.0, 0.0))
    # Fuel tank & seat
    if style == "scooter":
        b.box((0.0, 0.58, 0.72), (0.56, 0.30, 0.76), body_mat, rot=(0.14, 0.0, 0.0), bevel=0.04)
        b.box((0.0, 0.02, 0.34), (0.52, 0.85, 0.14), IRON_DARK, bevel=0.02)
        b.box((0.0, -0.42, 0.66), (0.58, 0.96, 0.46), body_mat, bevel=0.04)
        b.box((0.0, -0.35, 0.94), (0.44, 0.72, 0.16), CREAM, bevel=0.03)
        b.box((0.0, -0.92, 1.02), (0.46, 0.42, 0.38), AMBER, bevel=0.025)
    else:
        b.box((0.0, 0.26, 0.86), (0.46, 0.66, 0.34), body_mat, rot=(-0.10, 0.0, 0.0), bevel=0.05)
        b.box((0.0, -0.32, 0.80), (0.40, 0.72, 0.18), SEAT_DARK, bevel=0.03)
        b.box((0.0, -0.78, 0.78), (0.34, 0.52, 0.14), body_mat, rot=(0.18, 0.0, 0.0), bevel=0.02)
        b.box((0.0, 0.78, 0.74), (0.28, 0.46, 0.10), accent_mat, rot=(-0.22, 0.0, 0.0), bevel=0.02)
        b.box((0.0, -1.02, 0.76), (0.18, 0.08, 0.10), SIGNAL_RED)
    if style == "police":
        # Touring front fairing + tall windshield + twin saddlebags + rear beacon pole!
        b.box((0.0, 0.62, 0.94), (0.64, 0.36, 0.48), CREAM, rot=(0.18, 0.0, 0.0), bevel=0.04)
        b.box((0.0, 0.58, 1.32), (0.52, 0.08, 0.42), GLASS_BLUE, rot=(0.24, 0.0, 0.0))
        for sx in (-0.34, 0.34):
            b.box((sx, -0.68, 0.64), (0.24, 0.58, 0.38), CREAM, bevel=0.03)
            b.box((sx * 1.38, -0.68, 0.68), (0.04, 0.48, 0.10), BLUE)
        # Rear flashing police beacon pole
        b.cyl((-0.22, -0.96, 1.18), 0.035, 0.86, SILVER, segs=8)
        b.cyl((-0.22, -0.96, 1.65), 0.10, 0.16, SIGNAL_BLUE, segs=8)
        b.cyl((0.22, -0.96, 1.18), 0.035, 0.86, SILVER, segs=8)
        b.cyl((0.22, -0.96, 1.65), 0.10, 0.16, SIGNAL_RED, segs=8)
    return b.finish(loc)


# Arrange 13 Vehicles into 2 clean rows for preview
ROW_A = [
    make_sedan_base("Stylized_Vehicle_Sedan", BLUE, BLUE, BLUE, (-16.0, 0.0, 0.0)),
    make_sedan_base("Stylized_Vehicle_Police_Sedan", SLATE_DARK, CREAM, CREAM, (-11.0, 0.0, 0.0), is_police=True),
    make_sedan_base("Stylized_Vehicle_Taxi_NYC", AMBER, AMBER, AMBER, (-6.0, 0.0, 0.0), is_taxi=True),
    make_police_suv((-0.5, 0.0, 0.0)),
    make_fire_truck((6.0, 0.0, 0.0)),
    make_delivery_van((12.5, 0.0, 0.0)),
    make_pickup_truck((18.0, 0.0, 0.0)),
]

ROW_B = [
    make_city_bus((-14.0, 10.0, 0.0)),
    make_motorcycle("Stylized_Vehicle_Motorcycle", CRIMSON, SILVER, (-6.5, 10.0, 0.0), style="cruiser"),
    make_motorcycle("Stylized_Vehicle_Police_Motorcycle", SLATE_DARK, CREAM, (-2.5, 10.0, 0.0), style="police"),
    make_motorcycle("Stylized_Apoc_Motorcycle_01_Rusted_Scrambler", RUST, SLATE_DARK, (1.5, 10.0, 0.0), style="scrambler"),
    make_motorcycle("Stylized_Apoc_Motorcycle_02_Smashed_Scooter", TEAL, CREAM, (5.5, 10.0, 0.0), style="scooter"),
    make_motorcycle("Stylized_Apoc_Motorcycle_03_Abandoned_Dirt_Bike", AMBER, SLATE_DARK, (9.5, 10.0, 0.0), style="dirt"),
]

ALL_VEHICLES = ROW_A + ROW_B

for obj in ALL_VEHICLES:
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
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((1.0, 5.0, -0.15)) @ Matrix.Diagonal((48.0, 24.0, 0.30, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_FLOOR])
floor_obj = bpy.data.objects.new("Preview_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

bpy.ops.object.camera_add(location=(4.0, -22.0, 12.0))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 42.0
point_at(camera, (1.5, 0.0, 1.2))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(12.0, -18.0, 22.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.94, 0.82)
sun.data.energy = 3.0
sun.data.angle = math.radians(10.0)
point_at(sun, (0.0, 5.0, 1.5))

bpy.ops.object.light_add(type="AREA", location=(-14.0, -14.0, 14.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 1100.0
fill.data.size = 18.0
point_at(fill, (0.0, 5.0, 1.5))

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

for o in ROW_B:
    o.hide_render = True
scene.render.filepath = PREVIEW_A_PATH
bpy.ops.render.render(write_still=True)

for o in ROW_A:
    o.hide_render = True
for o in ROW_B:
    o.hide_render = False
camera.data.ortho_scale = 34.0
camera.location = (0.0, -10.0, 10.5)
point_at(camera, (-2.0, 10.0, 1.2))
scene.render.filepath = PREVIEW_B_PATH
bpy.ops.render.render(write_still=True)

for o in ALL_VEHICLES:
    o.hide_render = False
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("All 13 Stylized American Vehicles generated successfully!")

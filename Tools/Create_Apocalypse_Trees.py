import math
import os
import bpy
from mathutils import Vector, Euler


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)

BLEND_PATH = os.path.join(OUT_DIR, "Apocalypse_Trees_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Trees_Preview.png")
TOP_PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Trees_TopDown_Preview.png")


# Clean scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)


def make_material(name, color, roughness=0.85, metallic=0.0, emission=None, emission_strength=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission is not None and emission_strength > 0.0:
        if "Emission Color" in shader.inputs:
            shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        elif "Emission" in shader.inputs:
            shader.inputs["Emission"].default_value = (*emission, 1.0)
        if "Emission Strength" in shader.inputs:
            shader.inputs["Emission Strength"].default_value = emission_strength
    return material


MATS = [
    make_material("Bark | weathered oak", (0.26, 0.18, 0.115), 0.90, 0.0),
    make_material("Bark | charred deadwood", (0.15, 0.12, 0.10), 0.94, 0.0),
    make_material("Bark | pine reddish", (0.29, 0.16, 0.095), 0.88, 0.0),
    make_material("Wood | heartwood split", (0.58, 0.41, 0.22), 0.80, 0.0),
    make_material("Foliage | faded olive", (0.26, 0.35, 0.17), 0.84, 0.0),
    make_material("Foliage | autumn amber", (0.55, 0.36, 0.13), 0.82, 0.0),
    make_material("Foliage | dry rust leaf", (0.46, 0.23, 0.105), 0.85, 0.0),
    make_material("Foliage | dark pine", (0.13, 0.25, 0.16), 0.86, 0.0),
    make_material("Foliage | teal pine", (0.17, 0.32, 0.23), 0.84, 0.0),
    make_material("Concrete | cracked curb", (0.41, 0.42, 0.40), 0.88, 0.0),
    make_material("Stone | wasteland rock", (0.30, 0.31, 0.30), 0.90, 0.0),
    make_material("Soil | dry earth", (0.19, 0.15, 0.11), 0.95, 0.0),
    make_material("Metal | rusted sign", (0.39, 0.18, 0.095), 0.72, 0.45),
    make_material("Fungus | bioluminescent spore", (0.20, 0.80, 0.62), 0.35, 0.0, (0.18, 0.82, 0.64), 1.25),
]

(
    BARK_OAK,
    BARK_DEAD,
    BARK_PINE,
    WOOD_HEART,
    LEAF_OLIVE,
    LEAF_AMBER,
    LEAF_RUST,
    PINE_DARK,
    PINE_TEAL,
    CONCRETE_CURB,
    STONE_ROCK,
    SOIL_EARTH,
    RUST_METAL,
    GLOW_SPORE,
) = range(len(MATS))


class TreeBuilder:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth_faces = []

    def _face(self, indices, material, smooth=False):
        idx = tuple(indices)
        if len(set(idx)) < 3:
            return
        self.faces.append(idx)
        self.material_indices.append(material)
        self.smooth_faces.append(smooth)

    def _transform(self, pt, center, rot_euler):
        v = Vector(pt)
        if rot_euler is not None:
            v.rotate(Euler(rot_euler, "XYZ"))
        return (v.x + center[0], v.y + center[1], v.z + center[2])

    def box(self, center, size, material, rot=(0.0, 0.0, 0.0)):
        sx, sy, sz = (v * 0.5 for v in size)
        start = len(self.vertices)
        corners = [
            (-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
            (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz),
        ]
        for c in corners:
            self.vertices.append(self._transform(c, center, rot))
        for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            self._face((start + i for i in f), material, False)

    def branch_chain(self, points_radii, material, segments=7, twist=0.12, cap_end_mat=None):
        """Builds an organic faceted trunk/branch using parallel transport so rings never flip."""
        rings = []
        count = len(points_radii)
        u_prev = None
        for idx, (px, py, pz, rad) in enumerate(points_radii):
            p = Vector((px, py, pz))
            if idx == 0:
                p_next = Vector(points_radii[1][:3])
                tangent = (p_next - p).normalized()
            elif idx == count - 1:
                p_prev = Vector(points_radii[idx - 1][:3])
                tangent = (p - p_prev).normalized()
            else:
                p_prev = Vector(points_radii[idx - 1][:3])
                p_next = Vector(points_radii[idx + 1][:3])
                tangent = (p_next - p_prev).normalized()

            if u_prev is None:
                ref = Vector((0, 1, 0)) if abs(tangent.z) > 0.7 else Vector((0, 0, 1))
                u = tangent.cross(ref).normalized()
            else:
                # Parallel transport u_prev onto the plane perpendicular to tangent
                u = u_prev - tangent * u_prev.dot(tangent)
                if u.length < 1e-4:
                    u = tangent.cross(Vector((1, 0, 0))).normalized()
                else:
                    u.normalize()
            v = tangent.cross(u).normalized()
            u_prev = u

            ring_start = len(self.vertices)
            for i in range(segments):
                ang = (2.0 * math.pi * i / segments) + idx * twist
                ridge = 0.94 + 0.10 * math.sin(i * 2.0 + idx * 1.1)
                r = rad * ridge
                pos = p + r * (math.cos(ang) * u + math.sin(ang) * v)
                self.vertices.append((pos.x, pos.y, pos.z))
            rings.append(ring_start)

        for idx in range(count - 1):
            r0 = rings[idx]
            r1 = rings[idx + 1]
            for i in range(segments):
                j = (i + 1) % segments
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), material, False)

        self._face((rings[0] + i for i in reversed(range(segments))), material, False)
        end_mat = cap_end_mat if cap_end_mat is not None else material
        self._face((rings[-1] + i for i in range(segments)), end_mat, False)

    def foliage_cloud(self, center, radii, material, rot=(0.0, 0.0, 0.0), seed=0, segs=8):
        """Creates a stylized faceted low-poly foliage canopy cluster."""
        rx, ry, rz = radii
        start = len(self.vertices)
        rings = [
            (-0.78 * rz, 0.62),
            (-0.22 * rz, 0.98),
            (0.32 * rz, 0.92),
            (0.76 * rz, 0.54),
        ]
        for r_idx, (z_val, scale) in enumerate(rings):
            for i in range(segs):
                ang = (2.0 * math.pi * i / segs) + (r_idx * 0.25) + (seed * 0.45)
                puff = 0.93 + 0.09 * math.sin(seed * 2.3 + i * 2.0 + r_idx * 1.5)
                px = rx * scale * puff * math.cos(ang)
                py = ry * scale * puff * math.sin(ang)
                pz = z_val * (0.95 + 0.06 * math.cos(seed + i * 1.7))
                self.vertices.append(self._transform((px, py, pz), center, rot))

        bot_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, -0.92 * rz), center, rot))
        top_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, 0.96 * rz), center, rot))

        for i in range(segs):
            j = (i + 1) % segs
            self._face((bot_idx, start + j, start + i), material, False)
            top_ring = start + (len(rings) - 1) * segs
            self._face((top_idx, top_ring + i, top_ring + j), material, False)

        for r_idx in range(len(rings) - 1):
            r0 = start + r_idx * segs
            r1 = start + (r_idx + 1) * segs
            for i in range(segs):
                j = (i + 1) % segs
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), material, False)

    def pine_tier(self, center, base_radius, height, material, tilt=(0.0, 0.0, 0.0), segs=10, seed=0):
        """Creates a stylized drooping jagged pine foliage tier."""
        start = len(self.vertices)
        rings = [
            (0.00 * height, 1.00, True),
            (0.38 * height, 0.72, False),
            (0.75 * height, 0.38, False),
        ]
        for r_idx, (z_val, scale, jagged) in enumerate(rings):
            for i in range(segs):
                ang = (2.0 * math.pi * i / segs) + (r_idx * 0.22) + (seed * 0.3)
                jag = (1.08 if i % 2 == 0 else 0.84) if jagged else 0.95
                r = base_radius * scale * jag
                droop = -0.06 * height if (jagged and i % 2 == 0) else 0.0
                pt = (r * math.cos(ang), r * math.sin(ang), z_val + droop)
                self.vertices.append(self._transform(pt, center, tilt))

        bot_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, 0.08 * height), center, tilt))
        top_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, height), center, tilt))

        for i in range(segs):
            j = (i + 1) % segs
            self._face((bot_idx, start + j, start + i), material, False)
            top_ring = start + (len(rings) - 1) * segs
            self._face((top_idx, top_ring + i, top_ring + j), material, False)

        for r_idx in range(len(rings) - 1):
            r0 = start + r_idx * segs
            r1 = start + (r_idx + 1) * segs
            for i in range(segs):
                j = (i + 1) % segs
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), material, False)

    def finish(self, location=(0.0, 0.0, 0.0)):
        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.materials.clear()
        for mat in MATS:
            mesh.materials.append(mat)
        mesh.update()
        for poly, mat_idx, smooth in zip(mesh.polygons, self.material_indices, self.smooth_faces):
            poly.material_index = mat_idx
            poly.use_smooth = smooth

        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        bevel = obj.modifiers.new("SoftEdge", "BEVEL")
        bevel.width = 0.010
        bevel.segments = 1
        bevel.limit_method = "ANGLE"
        bevel.angle_limit = math.radians(42)
        return obj


def make_tree_01_withered_street_oak(location=(0, 0, 0)):
    """Tree 01: Urban Sidewalk Withered Oak with Cracked Concrete Planter Curb & Autumn/Olive Canopy."""
    b = TreeBuilder("Apoc_Tree_01_Withered_Street_Oak")

    # 1. Cracked Urban Sidewalk Planter Curb & Soil Bed
    b.foliage_cloud((0, 0, 0.05), (0.82, 0.82, 0.10), SOIL_EARTH, seed=1, segs=10)
    curb_pieces = [
        ((-0.48, -0.82, 0.09), (0.68, 0.16, 0.18), (0.04, -0.06, 0.05)),
        ((0.46, -0.80, 0.08), (0.64, 0.16, 0.16), (-0.05, 0.08, -0.12)),
        ((0.82, -0.15, 0.09), (0.16, 0.85, 0.18), (0.06, 0.03, 0.04)),
        ((0.78, 0.55, 0.08), (0.16, 0.48, 0.16), (-0.08, -0.04, 0.18)),
        ((0.05, 0.82, 0.09), (1.15, 0.16, 0.18), (0.02, 0.04, -0.03)),
        ((-0.82, 0.18, 0.09), (0.16, 0.96, 0.18), (-0.03, 0.05, 0.02)),
        ((-0.79, -0.52, 0.07), (0.16, 0.42, 0.15), (0.12, -0.08, -0.15)),
    ]
    for c_pos, c_size, c_rot in curb_pieces:
        b.box(c_pos, c_size, CONCRETE_CURB, rot=c_rot)

    # 2. Root Flares breaking out over the soil/curb
    roots = [
        [(0.0, 0.0, 0.35, 0.24), (-0.35, -0.32, 0.14, 0.14), (-0.68, -0.55, 0.03, 0.06)],
        [(0.0, 0.0, 0.32, 0.22), (0.38, -0.25, 0.13, 0.13), (0.65, -0.45, 0.03, 0.05)],
        [(0.0, 0.0, 0.34, 0.23), (0.28, 0.38, 0.14, 0.14), (0.55, 0.68, 0.03, 0.06)],
        [(0.0, 0.0, 0.30, 0.21), (-0.42, 0.24, 0.12, 0.13), (-0.70, 0.42, 0.03, 0.05)],
    ]
    for r_pts in roots:
        b.branch_chain(r_pts, BARK_OAK, segments=6)

    # 3. Gnarled Main Trunk
    trunk = [
        (0.00, 0.00, 0.02, 0.35),
        (0.03, -0.02, 0.68, 0.28),
        (-0.04, 0.03, 1.38, 0.24),
        (-0.07, 0.04, 2.05, 0.20),
        (-0.02, 0.02, 2.68, 0.14),
    ]
    b.branch_chain(trunk, BARK_OAK, segments=8, twist=0.14)

    # 4. Primary & Secondary Branches
    branches = [
        ([(-0.05, 0.03, 1.85, 0.17), (-0.48, -0.18, 2.35, 0.12), (-0.88, -0.32, 2.75, 0.07)], BARK_OAK, None),
        ([(-0.06, 0.04, 2.00, 0.16), (0.45, 0.32, 2.50, 0.11), (0.85, 0.52, 2.90, 0.07)], BARK_OAK, None),
        ([(-0.04, 0.02, 2.20, 0.14), (0.42, -0.38, 2.68, 0.10), (0.72, -0.62, 2.95, 0.06)], BARK_OAK, None),
        ([(-0.02, 0.02, 2.50, 0.13), (-0.32, 0.45, 3.05, 0.09), (-0.52, 0.68, 3.35, 0.05)], BARK_OAK, None),
        # Broken dead snag branch jutting out on the right
        ([(0.02, -0.01, 1.48, 0.13), (0.45, -0.12, 1.72, 0.085), (0.76, -0.18, 1.88, 0.058)], BARK_OAK, WOOD_HEART),
        # Bare dead twig fork sticking out top-left
        ([(-0.62, -0.22, 2.52, 0.075), (-0.98, -0.05, 2.88, 0.040), (-1.18, 0.05, 3.12, 0.018)], BARK_OAK, None),
    ]
    for b_pts, mat, cap_mat in branches:
        b.branch_chain(b_pts, mat, segments=6, cap_end_mat=cap_mat)

    # 5. Stylized Multi-Clump Wasteland Canopy
    canopy_clusters = [
        ((-0.85, -0.30, 2.88), (0.68, 0.62, 0.44), LEAF_OLIVE, (0.08, -0.06, 0.2), 1),
        ((-0.45, -0.52, 3.10), (0.55, 0.50, 0.38), LEAF_AMBER, (-0.05, 0.08, 0.9), 2),
        ((0.05, 0.02, 3.35), (0.78, 0.72, 0.48), LEAF_OLIVE, (0.04, 0.04, 0.5), 3),
        ((0.78, 0.48, 2.98), (0.66, 0.60, 0.42), LEAF_AMBER, (-0.06, -0.05, 1.4), 4),
        ((0.68, -0.55, 3.02), (0.58, 0.54, 0.38), LEAF_RUST, (0.07, 0.06, 2.1), 5),
        ((-0.45, 0.60, 3.40), (0.64, 0.58, 0.42), LEAF_OLIVE, (-0.04, 0.07, 2.8), 6),
        ((0.28, 0.55, 3.48), (0.52, 0.48, 0.36), LEAF_RUST, (0.05, -0.04, 3.3), 7),
    ]
    for c_pos, c_rad, c_mat, c_rot, c_seed in canopy_clusters:
        b.foliage_cloud(c_pos, c_rad, c_mat, rot=c_rot, seed=c_seed, segs=8)

    b.foliage_cloud((-0.52, 0.42, 0.14), (0.24, 0.20, 0.12), LEAF_RUST, seed=12, segs=7)
    b.foliage_cloud((0.48, -0.45, 0.13), (0.20, 0.18, 0.10), LEAF_AMBER, seed=15, segs=7)

    return b.finish(location)


def make_tree_02_scorched_dead_tree(location=(0, 0, 0)):
    """Tree 02: Scorched Wasteland Dead Tree + Snapped Hollow Log & Rocks at Base."""
    b = TreeBuilder("Apoc_Tree_02_Scorched_Dead_Tree")

    # 1. Base Earth Mound + Wasteland Boulders
    b.foliage_cloud((0, 0, 0.04), (0.88, 0.80, 0.11), SOIL_EARTH, seed=20, segs=10)
    b.foliage_cloud((-0.55, -0.35, 0.14), (0.26, 0.22, 0.16), STONE_ROCK, rot=(0.1, -0.1, 0.4), seed=21, segs=7)
    b.foliage_cloud((0.62, 0.28, 0.12), (0.22, 0.19, 0.14), STONE_ROCK, rot=(-0.1, 0.1, 1.2), seed=22, segs=7)
    b.foliage_cloud((-0.25, 0.58, 0.10), (0.18, 0.16, 0.11), STONE_ROCK, rot=(0.05, 0.12, 2.0), seed=23, segs=7)

    # 2. Twisted Dead Trunk with smooth continuous taper
    trunk = [
        (-0.08, 0.04, 0.02, 0.33),
        (-0.14, 0.07, 0.68, 0.26),
        (-0.05, 0.02, 1.38, 0.21),
        (0.08, -0.04, 2.08, 0.16),
        (0.03, -0.02, 2.75, 0.11),
        (-0.06, 0.04, 3.32, 0.055),
        (-0.12, 0.07, 3.68, 0.018),
    ]
    b.branch_chain(trunk, BARK_DEAD, segments=7, twist=0.18)

    dead_roots = [
        [(-0.08, 0.04, 0.30, 0.21), (-0.45, 0.28, 0.12, 0.12), (-0.78, 0.45, 0.02, 0.045)],
        [(-0.08, 0.04, 0.28, 0.20), (0.32, 0.35, 0.10, 0.11), (0.58, 0.62, 0.02, 0.045)],
        [(-0.08, 0.04, 0.28, 0.20), (-0.22, -0.42, 0.10, 0.11), (-0.38, -0.72, 0.02, 0.045)],
    ]
    for r_pts in dead_roots:
        b.branch_chain(r_pts, BARK_DEAD, segments=6)

    # 3. Jagged Claw-Like Dead Branches & Forks
    snags = [
        [(-0.05, 0.02, 1.55, 0.16), (-0.52, 0.12, 1.95, 0.105), (-0.92, 0.22, 2.38, 0.060), (-1.15, 0.28, 2.72, 0.020)],
        [(-0.52, 0.12, 1.95, 0.085), (-0.68, -0.25, 2.42, 0.046), (-0.78, -0.45, 2.75, 0.016)],
        [(0.06, -0.03, 1.95, 0.14), (0.55, -0.22, 2.32, 0.090), (0.95, -0.35, 2.65, 0.048), (1.18, -0.42, 2.92, 0.016)],
        [(0.55, -0.22, 2.32, 0.070), (0.72, 0.18, 2.68, 0.038), (0.85, 0.38, 2.95, 0.015)],
        [(0.04, -0.02, 2.55, 0.095), (0.28, 0.42, 3.02, 0.052), (0.45, 0.68, 3.38, 0.018)],
        [(0.01, -0.01, 2.85, 0.080), (-0.35, -0.38, 3.25, 0.042), (-0.55, -0.58, 3.52, 0.016)],
    ]
    for s_pts in snags:
        b.branch_chain(s_pts, BARK_DEAD, segments=6, twist=0.15)

    # 4. Snapped Twin Stump + Fallen Log lying beside the base
    stump = [
        (0.32, -0.24, 0.02, 0.18),
        (0.35, -0.27, 0.30, 0.145),
        (0.37, -0.29, 0.54, 0.125),
    ]
    b.branch_chain(stump, BARK_DEAD, segments=7, cap_end_mat=WOOD_HEART)

    fallen_log = [
        (0.42, -0.38, 0.15, 0.115),
        (0.65, -0.58, 0.12, 0.105),
        (0.92, -0.78, 0.09, 0.095),
    ]
    b.branch_chain(fallen_log, BARK_DEAD, segments=7, cap_end_mat=WOOD_HEART)

    b.foliage_cloud((-0.42, -0.52, 0.16), (0.25, 0.22, 0.15), LEAF_RUST, seed=29, segs=7)

    return b.finish(location)


def make_tree_03_mutated_overgrown_pine(location=(0, 0, 0)):
    """Tree 03: Overgrown Wasteland Pine with Buried Road Sign, Rocks & Bioluminescent Spores."""
    b = TreeBuilder("Apoc_Tree_03_Mutated_Overgrown_Pine")

    # 1. Rocky Base + Half-Buried Rusted Warning Sign & Concrete Slab
    b.foliage_cloud((0, 0, 0.05), (0.80, 0.80, 0.12), SOIL_EARTH, seed=40, segs=10)
    b.foliage_cloud((0.48, -0.35, 0.15), (0.28, 0.24, 0.18), STONE_ROCK, rot=(0.1, 0.08, 0.6), seed=41, segs=7)
    b.foliage_cloud((-0.52, 0.30, 0.13), (0.24, 0.20, 0.15), STONE_ROCK, rot=(-0.08, 0.1, 1.7), seed=42, segs=7)
    b.box((-0.42, -0.45, 0.14), (0.46, 0.34, 0.10), CONCRETE_CURB, rot=(0.28, -0.22, 0.55))
    b.box((0.52, 0.42, 0.28), (0.04, 0.36, 0.36), RUST_METAL, rot=(-0.25, 0.32, 0.85))

    # 2. Tall Straight-Leaning Conifer Trunk
    trunk = [
        (0.00, 0.00, 0.02, 0.29),
        (0.02, -0.02, 0.85, 0.22),
        (0.04, -0.03, 1.75, 0.17),
        (0.02, -0.01, 2.75, 0.12),
        (0.00, 0.00, 3.65, 0.065),
        (0.00, 0.00, 4.25, 0.022),
    ]
    b.branch_chain(trunk, BARK_PINE, segments=8, twist=0.12)

    stubs = [
        [(0.02, -0.02, 0.95, 0.085), (-0.42, -0.18, 0.88, 0.048)],
        [(0.03, -0.02, 1.15, 0.080), (0.38, 0.25, 1.08, 0.042)],
    ]
    for st in stubs:
        b.branch_chain(st, BARK_PINE, segments=6, cap_end_mat=WOOD_HEART)

    # 3. Bioluminescent / Mutated Shelf Spores clinging to the lower trunk & rocks
    spores = [
        ((0.18, -0.14, 0.48), (0.12, 0.09, 0.04), (0.1, 0.15, -0.6)),
        ((0.20, -0.05, 0.58), (0.09, 0.07, 0.035), (0.05, 0.18, -0.2)),
        ((-0.19, 0.12, 0.72), (0.11, 0.08, 0.038), (-0.12, -0.1, 2.4)),
        ((0.36, -0.28, 0.26), (0.09, 0.08, 0.035), (0.15, -0.1, 0.5)),
    ]
    for sp_pos, sp_rad, sp_rot in spores:
        b.foliage_cloud(sp_pos, sp_rad, GLOW_SPORE, rot=sp_rot, seed=50, segs=7)

    # 4. 5 Layered Stylized Conifer Tiers
    tiers = [
        ((0.03, -0.02, 1.25), 1.12, 0.95, PINE_DARK, (0.03, -0.02, 0.0), 1),
        ((0.03, -0.02, 1.85), 0.96, 0.90, PINE_TEAL, (-0.02, 0.03, 0.4), 2),
        ((0.02, -0.01, 2.42), 0.80, 0.85, PINE_DARK, (0.02, 0.02, 0.9), 3),
        ((0.01, 0.00, 2.98), 0.62, 0.78, PINE_TEAL, (-0.03, -0.02, 1.5), 4),
        ((0.00, 0.00, 3.50), 0.42, 0.82, PINE_DARK, (0.01, -0.01, 2.1), 5),
    ]
    for t_pos, t_rad, t_h, t_mat, t_tilt, t_seed in tiers:
        b.pine_tier(t_pos, t_rad, t_h, t_mat, tilt=t_tilt, segs=10, seed=t_seed)

    return b.finish(location)


# Build the 3 trees side by side for the preview scene
tree_01 = make_tree_01_withered_street_oak(location=(-3.0, 0.0, 0.0))
tree_02 = make_tree_02_scorched_dead_tree(location=(0.0, 0.0, 0.0))
tree_03 = make_tree_03_mutated_overgrown_pine(location=(3.0, 0.0, 0.0))

TREES = [tree_01, tree_02, tree_03]

# Export each tree as an individual FBX with clean origin pivot at (0, 0, 0)
for obj in TREES:
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
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="AUTO",
    )
    obj.location = orig_loc
    print("Exported:", fbx_path)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


# Ground slab for preview rendering only
ground = TreeBuilder("Preview_Floor")
ground.box((0.0, 0.0, -0.04), (10.5, 4.8, 0.08), STONE_ROCK)
ground.finish((0.0, 0.0, 0.0))

bpy.ops.object.camera_add(location=(0.0, -10.2, 4.4))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 35
point_at(camera, (0.0, 0.0, 1.95))
bpy.context.scene.camera = camera

# Lighting setup
bpy.ops.object.light_add(type="AREA", location=(4.5, -6.5, 7.5))
key_light = bpy.context.object
key_light.data.color = (1.0, 0.94, 0.84)
key_light.data.energy = 850.0
key_light.data.size = 6.0
point_at(key_light, (0.0, 0.0, 1.8))

bpy.ops.object.light_add(type="AREA", location=(-5.5, -4.5, 5.0))
fill_light = bpy.context.object
fill_light.data.color = (0.68, 0.78, 0.92)
key_light_fill = fill_light.data
key_light_fill.energy = 420.0
key_light_fill.size = 5.0
point_at(fill_light, (0.0, 0.0, 1.6))

bpy.ops.object.light_add(type="AREA", location=(0.0, 5.5, 6.0))
rim_light = bpy.context.object
rim_light.data.color = (0.85, 0.72, 0.58)
rim_light.data.energy = 450.0
rim_light.data.size = 5.0
point_at(rim_light, (0.0, 0.0, 2.0))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 1600
scene.render.resolution_y = 950
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.04, 0.048, 0.06)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# 1. Front 3/4 Full Lineup Render
scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# 2. Top-Down In-Game Camera Angle Render
camera.location = (0.0, -6.5, 8.8)
point_at(camera, (0.0, 0.0, 1.6))
scene.render.filepath = TOP_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

print("All 3 Apocalypse Trees generated successfully!")

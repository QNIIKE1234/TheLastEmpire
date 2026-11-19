import math
import os
import random
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Trees_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Trees_Preview.png")
TOP_PREVIEW_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Trees_TopDown_Preview.png")

# Clean scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in (bpy.data.meshes, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for item in list(collection):
        if item.users == 0:
            collection.remove(item)


def make_material(name, color, roughness=0.92, metallic=0.0, specular=0.02):
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


# Matte Flat-Shaded Cozy Low-Poly Palette (Linear RGB for Blender -> Unity useSRGBMaterialColor=1)
MATS = [
    make_material("Bark | cozy warm wood", (0.2500, 0.1450, 0.0750), 0.92, 0.0),
    make_material("Bark | cozy dark wood", (0.1550, 0.0900, 0.0500), 0.94, 0.0),
    make_material("Bark | cozy birch wood", (0.4600, 0.4150, 0.3400), 0.90, 0.0),
    make_material("Foliage | cozy deep green", (0.0850, 0.2750, 0.1150), 0.92, 0.0),
    make_material("Foliage | cozy meadow green", (0.1550, 0.3850, 0.1350), 0.90, 0.0),
    make_material("Foliage | cozy sunlit leaf", (0.2650, 0.4550, 0.1250), 0.90, 0.0),
    make_material("Foliage | cozy dark pine", (0.0550, 0.1950, 0.1250), 0.92, 0.0),
    make_material("Foliage | cozy mid pine", (0.0950, 0.2850, 0.1650), 0.90, 0.0),
    make_material("Foliage | cozy sage pine", (0.1550, 0.3450, 0.1850), 0.90, 0.0),
    make_material("Foliage | cozy autumn gold", (0.7484, 0.3931, 0.0272), 0.90, 0.0),
    make_material("Foliage | cozy autumn orange", (0.5800, 0.2150, 0.0450), 0.90, 0.0),
    make_material("Foliage | cozy autumn red", (0.4800, 0.1150, 0.0650), 0.90, 0.0),
    make_material("Stone | cozy pebble", (0.2400, 0.2500, 0.2750), 0.94, 0.0),
    make_material("Preview | meadow grass", (0.1850, 0.3350, 0.1250), 0.95, 0.0),
]

(
    BARK_WARM,
    BARK_DARK,
    BARK_BIRCH,
    LEAF_DEEP,
    LEAF_MEADOW,
    LEAF_SUNLIT,
    PINE_DARK,
    PINE_MID,
    PINE_SAGE,
    AUTUMN_GOLD,
    AUTUMN_ORANGE,
    AUTUMN_RED,
    STONE_PEBBLE,
    PREVIEW_GRASS,
) = range(len(MATS))


class CozyTreeBuilder:
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()

    def _add_part(self, src_bm, mat_idx, matrix=None):
        if matrix is not None:
            bmesh.ops.transform(src_bm, matrix=matrix, verts=src_bm.verts)
        vert_map = {}
        for v in src_bm.verts:
            vert_map[v] = self.bm.verts.new(v.co)
        for f in src_bm.faces:
            try:
                nf = self.bm.faces.new([vert_map[v] for v in f.verts])
                nf.material_index = mat_idx
                nf.smooth = False
            except ValueError:
                pass
        src_bm.free()

    def tube_path(self, points, radii, mat_idx, segs=6, cap_top=True, cap_bottom=True):
        """Build a crisp faceted polygonal trunk/branch following 3D control points."""
        src = bmesh.new()
        rings = []
        n_pts = len(points)
        prev_right = Vector((1.0, 0.0, 0.0))

        for i, (pt, rad) in enumerate(zip(points, radii)):
            p = Vector(pt)
            if i == 0:
                tangent = (Vector(points[1]) - p).normalized()
            elif i == n_pts - 1:
                tangent = (p - Vector(points[i - 1])).normalized()
            else:
                tangent = (Vector(points[i + 1]) - Vector(points[i - 1])).normalized()

            # Stable frame orthogonal to tangent
            right = prev_right - tangent * prev_right.dot(tangent)
            if right.length < 1e-4:
                alt = Vector((0.0, 1.0, 0.0)) if abs(tangent.y) < 0.9 else Vector((1.0, 0.0, 0.0))
                right = alt.cross(tangent)
            right.normalize()
            fwd = tangent.cross(right).normalized()
            prev_right = right

            ring = []
            for s in range(segs):
                ang = (2.0 * math.pi * s) / segs + (0.18 if i % 2 == 1 else 0.0)
                offset = (right * math.cos(ang) + fwd * math.sin(ang)) * rad
                ring.append(src.verts.new(p + offset))
            rings.append(ring)

        for i in range(n_pts - 1):
            r0, r1 = rings[i], rings[i + 1]
            for s in range(segs):
                s_next = (s + 1) % segs
                src.faces.new((r0[s], r0[s_next], r1[s_next], r1[s]))

        if cap_bottom:
            src.faces.new(list(reversed(rings[0])))
        if cap_top:
            src.faces.new(rings[-1])

        bmesh.ops.recalc_face_normals(src, faces=src.faces)
        self._add_part(src, mat_idx)

    def add_roots(self, count, base_r, length, height, mat_idx, seed=1):
        rng = random.Random(seed)
        for i in range(count):
            ang = (2.0 * math.pi * i) / count + rng.uniform(-0.22, 0.22)
            dx, dy = math.cos(ang), math.sin(ang)
            pts = [
                (dx * base_r * 0.35, dy * base_r * 0.35, height),
                (dx * (base_r + length * 0.45), dy * (base_r + length * 0.45), height * 0.38),
                (dx * (base_r + length), dy * (base_r + length), 0.0),
            ]
            rads = [base_r * 0.56, base_r * 0.40, base_r * 0.24]
            self.tube_path(pts, rads, mat_idx, segs=5, cap_top=True, cap_bottom=True)

    def foliage_puff(
        self,
        center,
        scale,
        mat_idx,
        subdivisions=1,
        jitter=0.14,
        rot=(0.0, 0.0, 0.0),
        seed=1,
        flatten_bottom=0.22,
    ):
        """Chunky flat-shaded faceted polyhedron puff (signature Cozy Low-Poly look)."""
        rng = random.Random(seed)
        src = bmesh.new()
        bmesh.ops.create_icosphere(src, subdivisions=subdivisions, radius=1.0)

        sx, sy, sz = scale
        for v in src.verts:
            d = 1.0 + rng.uniform(-jitter, jitter)
            co = v.co * d
            # Gently flatten the underside so crowns sit naturally like stylized poly canopies
            if co.z < 0.0 and flatten_bottom > 0.0:
                co.z *= (1.0 - flatten_bottom)
            v.co = Vector((co.x * sx, co.y * sy, co.z * sz))

        rot_m = Euler(rot, "XYZ").to_matrix().to_4x4()
        mat = Matrix.Translation(Vector(center)) @ rot_m
        self._add_part(src, mat_idx, mat)

    def pine_tier(
        self,
        base_center,
        bottom_radius,
        height,
        mat_idx,
        segs=6,
        top_radius=0.0,
        tilt=(0.0, 0.0, 0.0),
        jitter=0.08,
        seed=1,
    ):
        """Crisp flat-shaded faceted pine cone tier with flat bottom cap and subtle organic wobble."""
        rng = random.Random(seed)
        src = bmesh.new()

        bottom_ring = []
        for s in range(segs):
            ang = (2.0 * math.pi * s) / segs + rng.uniform(-0.06, 0.06)
            rad = bottom_radius * (1.0 + rng.uniform(-jitter, jitter))
            z_off = rng.uniform(-height * 0.04, height * 0.04)
            bottom_ring.append(src.verts.new(Vector((math.cos(ang) * rad, math.sin(ang) * rad, z_off))))

        if top_radius <= 0.02:
            apex = src.verts.new(Vector((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), height)))
            for s in range(segs):
                s_next = (s + 1) % segs
                src.faces.new((bottom_ring[s], bottom_ring[s_next], apex))
        else:
            top_ring = []
            for s in range(segs):
                ang = (2.0 * math.pi * s) / segs
                rad = top_radius * (1.0 + rng.uniform(-jitter * 0.5, jitter * 0.5))
                top_ring.append(src.verts.new(Vector((math.cos(ang) * rad, math.sin(ang) * rad, height))))
            for s in range(segs):
                s_next = (s + 1) % segs
                src.faces.new((bottom_ring[s], bottom_ring[s_next], top_ring[s_next], top_ring[s]))
            src.faces.new(top_ring)

        # Flat cap underneath
        src.faces.new(list(reversed(bottom_ring)))
        bmesh.ops.recalc_face_normals(src, faces=src.faces)

        rot_m = Euler(tilt, "XYZ").to_matrix().to_4x4()
        mat = Matrix.Translation(Vector(base_center)) @ rot_m
        self._add_part(src, mat_idx, mat)

    def pebble(self, center, scale, mat_idx, seed=1):
        self.foliage_puff(
            center,
            scale,
            mat_idx,
            subdivisions=1,
            jitter=0.18,
            rot=(0.2, 0.4, seed * 0.7),
            seed=seed,
            flatten_bottom=0.45,
        )

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
# LARGE TREES (3) — Height ~5.5m to 6.8m
# =====================================================================

def make_large_01_giant_oak(loc):
    """Hero big cozy faceted broadleaf oak (like the big green tree in Image 1)."""
    b = CozyTreeBuilder("Cozy_Tree_Large_01_GiantOak")
    # Sturdy tapered trunk with slight natural S-curve
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.06, -0.04, 1.05),
            (-0.04, 0.05, 2.25),
            (0.02, 0.02, 3.35),
        ],
        [0.44, 0.34, 0.27, 0.20],
        BARK_WARM,
        segs=7,
    )
    b.add_roots(4, 0.34, 0.62, 0.58, BARK_WARM, seed=101)
    # 3 spreading branches reaching into the canopy puffs
    b.tube_path([(0.0, 0.0, 2.35), (0.68, 0.36, 3.15), (1.18, 0.58, 3.68)], [0.19, 0.14, 0.09], BARK_WARM, segs=5)
    b.tube_path([(0.0, 0.0, 2.50), (-0.75, 0.25, 3.25), (-1.28, 0.35, 3.78)], [0.18, 0.13, 0.09], BARK_WARM, segs=5)
    b.tube_path([(0.0, 0.0, 2.60), (0.12, -0.78, 3.35), (0.20, -1.25, 3.85)], [0.17, 0.12, 0.08], BARK_WARM, segs=5)

    # Big bold multi-tone faceted icosphere canopy
    b.foliage_puff((0.02, 0.04, 4.45), (1.85, 1.78, 1.55), LEAF_MEADOW, subdivisions=2, jitter=0.11, rot=(0.1, 0.15, 0.4), seed=11)
    b.foliage_puff((1.25, 0.55, 3.85), (1.28, 1.22, 1.05), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(0.2, -0.1, 0.8), seed=12)
    b.foliage_puff((-1.32, 0.32, 3.92), (1.32, 1.20, 1.08), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(-0.15, 0.2, 1.4), seed=13)
    b.foliage_puff((0.20, -1.25, 3.95), (1.22, 1.18, 1.02), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.12, -0.18, 2.1), seed=14)
    b.foliage_puff((-0.35, -0.25, 5.45), (1.16, 1.12, 0.94), LEAF_SUNLIT, subdivisions=1, jitter=0.12, rot=(0.05, 0.1, 0.9), seed=15)
    b.foliage_puff((0.82, -0.42, 5.05), (0.98, 0.94, 0.84), LEAF_SUNLIT, subdivisions=1, jitter=0.12, rot=(-0.1, 0.2, 2.6), seed=16)
    return b.finish(loc)


def make_large_02_tall_spruce(loc):
    """Grand 4-tier faceted cozy spruce/pine (like the tall river pines in Image 1)."""
    b = CozyTreeBuilder("Cozy_Tree_Large_02_TallSpruce")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.02, 0.0, 1.25),
            (-0.02, 0.02, 2.85),
            (0.0, 0.0, 4.80),
        ],
        [0.36, 0.28, 0.20, 0.08],
        BARK_DARK,
        segs=7,
    )
    b.add_roots(4, 0.28, 0.45, 0.42, BARK_DARK, seed=202)

    tiers = [
        ((0.0, 0.0, 1.35), 1.88, 2.05, PINE_DARK, 7, 0.35, (0.02, -0.02, 0.10), 21),
        ((0.02, -0.01, 2.65), 1.54, 1.90, PINE_MID, 7, 0.28, (-0.02, 0.02, 0.55), 22),
        ((-0.01, 0.02, 3.88), 1.18, 1.72, PINE_MID, 7, 0.20, (0.02, 0.01, 1.05), 23),
        ((0.0, 0.0, 5.02), 0.82, 1.78, PINE_SAGE, 6, 0.0, (-0.01, -0.02, 1.50), 24),
    ]
    for pos, r_bot, h, mat, segs, r_top, tilt, sd in tiers:
        b.pine_tier(pos, r_bot, h, mat, segs=segs, top_radius=r_top, tilt=tilt, jitter=0.07, seed=sd)
    return b.finish(loc)


def make_large_03_autumn_maple(loc):
    """Large sweeping autumn broadleaf tree in gold, warm orange & crimson puffs."""
    b = CozyTreeBuilder("Cozy_Tree_Large_03_AutumnMaple")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (-0.08, 0.05, 1.05),
            (0.05, -0.04, 2.25),
            (0.14, 0.06, 3.30),
        ],
        [0.42, 0.32, 0.25, 0.18],
        BARK_WARM,
        segs=7,
    )
    b.add_roots(4, 0.32, 0.56, 0.52, BARK_WARM, seed=303)
    b.tube_path([(0.02, -0.02, 2.25), (-0.75, -0.35, 3.05), (-1.28, -0.52, 3.55)], [0.18, 0.13, 0.08], BARK_WARM, segs=5)
    b.tube_path([(0.06, 0.02, 2.55), (0.78, 0.42, 3.32), (1.30, 0.65, 3.82)], [0.17, 0.12, 0.08], BARK_WARM, segs=5)
    b.tube_path([(0.04, 0.0, 2.40), (-0.18, 0.78, 3.20), (-0.30, 1.18, 3.70)], [0.16, 0.11, 0.08], BARK_WARM, segs=5)

    b.foliage_puff((0.12, 0.04, 4.35), (1.68, 1.62, 1.42), AUTUMN_ORANGE, subdivisions=2, jitter=0.12, rot=(0.1, -0.1, 0.6), seed=31)
    b.foliage_puff((-1.32, -0.52, 3.75), (1.28, 1.20, 1.02), AUTUMN_RED, subdivisions=1, jitter=0.13, rot=(0.2, 0.15, 1.1), seed=32)
    b.foliage_puff((1.28, 0.62, 3.95), (1.22, 1.18, 1.04), AUTUMN_GOLD, subdivisions=1, jitter=0.13, rot=(-0.1, 0.2, 2.2), seed=33)
    b.foliage_puff((-0.32, 1.22, 3.85), (1.15, 1.12, 0.96), AUTUMN_RED, subdivisions=1, jitter=0.13, rot=(0.15, -0.1, 0.3), seed=34)
    b.foliage_puff((0.55, -0.68, 4.78), (1.05, 0.98, 0.88), AUTUMN_GOLD, subdivisions=1, jitter=0.12, rot=(-0.12, 0.18, 1.8), seed=35)
    b.foliage_puff((-0.45, 0.18, 5.05), (0.98, 0.94, 0.84), AUTUMN_ORANGE, subdivisions=1, jitter=0.12, rot=(0.1, 0.12, 2.9), seed=36)
    return b.finish(loc)


# =====================================================================
# MEDIUM TREES (3) — Height ~3.5m to 4.3m
# =====================================================================

def make_medium_01_meadow_oak(loc):
    """Medium round cozy oak with fresh meadow + sunlit green faceted canopy."""
    b = CozyTreeBuilder("Cozy_Tree_Medium_01_MeadowOak")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.05, -0.02, 0.78),
            (-0.03, 0.04, 1.65),
            (0.02, 0.02, 2.38),
        ],
        [0.29, 0.22, 0.18, 0.13],
        BARK_WARM,
        segs=6,
    )
    b.add_roots(3, 0.23, 0.38, 0.36, BARK_WARM, seed=404)
    b.tube_path([(0.0, 0.02, 1.65), (0.55, 0.20, 2.28)], [0.13, 0.08], BARK_WARM, segs=5)
    b.tube_path([(0.0, 0.02, 1.72), (-0.52, -0.18, 2.32)], [0.12, 0.08], BARK_WARM, segs=5)

    b.foliage_puff((0.0, 0.02, 3.05), (1.28, 1.24, 1.12), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.12, 0.08, 0.5), seed=41)
    b.foliage_puff((0.76, 0.26, 2.68), (0.86, 0.82, 0.74), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(-0.1, 0.15, 1.3), seed=42)
    b.foliage_puff((-0.72, -0.22, 2.74), (0.88, 0.84, 0.76), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.15, -0.12, 2.4), seed=43)
    b.foliage_puff((-0.12, 0.22, 3.68), (0.78, 0.76, 0.66), LEAF_SUNLIT, subdivisions=1, jitter=0.12, rot=(0.05, 0.2, 0.9), seed=44)
    return b.finish(loc)


def make_medium_02_forest_pine(loc):
    """Medium 3-tier crisp hexagonal pine with gentle characterful tilt."""
    b = CozyTreeBuilder("Cozy_Tree_Medium_02_ForestPine")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.03, 0.01, 0.95),
            (0.05, 0.02, 2.15),
            (0.04, 0.01, 3.10),
        ],
        [0.25, 0.20, 0.14, 0.07],
        BARK_DARK,
        segs=6,
    )
    b.add_roots(3, 0.20, 0.30, 0.28, BARK_DARK, seed=505)

    tiers = [
        ((0.02, 0.01, 0.95), 1.30, 1.48, PINE_DARK, 6, 0.26, (0.02, 0.03, 0.20), 51),
        ((0.04, 0.02, 1.98), 1.00, 1.36, PINE_MID, 6, 0.18, (-0.02, 0.02, 0.75), 52),
        ((0.05, 0.01, 2.95), 0.72, 1.42, PINE_SAGE, 6, 0.0, (0.01, -0.02, 1.25), 53),
    ]
    for pos, r_bot, h, mat, segs, r_top, tilt, sd in tiers:
        b.pine_tier(pos, r_bot, h, mat, segs=segs, top_radius=r_top, tilt=tilt, jitter=0.07, seed=sd)
    return b.finish(loc)


def make_medium_03_golden_birch(loc):
    """Medium slender light-bark birch with clustered amber-gold & orange faceted puffs."""
    b = CozyTreeBuilder("Cozy_Tree_Medium_03_GoldenBirch")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (-0.06, 0.04, 0.90),
            (0.04, 0.08, 1.85),
            (0.08, 0.03, 2.65),
        ],
        [0.22, 0.17, 0.13, 0.09],
        BARK_BIRCH,
        segs=6,
    )
    b.tube_path([(-0.01, 0.06, 1.52), (-0.48, -0.22, 2.22)], [0.10, 0.06], BARK_BIRCH, segs=5)
    b.tube_path([(0.05, 0.06, 1.88), (0.48, 0.24, 2.52)], [0.09, 0.06], BARK_BIRCH, segs=5)

    b.foliage_puff((0.08, 0.04, 3.18), (1.06, 1.02, 1.08), AUTUMN_GOLD, subdivisions=1, jitter=0.13, rot=(0.1, 0.14, 0.4), seed=61)
    b.foliage_puff((-0.62, -0.26, 2.55), (0.78, 0.74, 0.72), AUTUMN_ORANGE, subdivisions=1, jitter=0.14, rot=(-0.15, 0.1, 1.5), seed=62)
    b.foliage_puff((0.62, 0.28, 2.78), (0.74, 0.72, 0.68), AUTUMN_GOLD, subdivisions=1, jitter=0.13, rot=(0.12, -0.14, 2.3), seed=63)
    b.foliage_puff((-0.18, 0.42, 3.58), (0.68, 0.66, 0.62), AUTUMN_ORANGE, subdivisions=1, jitter=0.12, rot=(0.08, 0.18, 0.9), seed=64)
    return b.finish(loc)


# =====================================================================
# SMALL TREES (3) — Height ~1.9m to 2.5m
# =====================================================================

def make_small_01_round_sapling(loc):
    """Small cute low-poly green sapling with bold chunky faceted puff."""
    b = CozyTreeBuilder("Cozy_Tree_Small_01_RoundSapling")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.03, -0.02, 0.55),
            (0.0, 0.02, 1.18),
        ],
        [0.18, 0.14, 0.10],
        BARK_WARM,
        segs=6,
    )
    b.tube_path([(0.02, 0.0, 0.72), (0.34, 0.12, 1.12)], [0.08, 0.05], BARK_WARM, segs=5)

    b.foliage_puff((-0.04, 0.02, 1.58), (0.82, 0.78, 0.76), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.1, 0.12, 0.7), seed=71)
    b.foliage_puff((0.46, 0.16, 1.32), (0.52, 0.50, 0.48), LEAF_SUNLIT, subdivisions=1, jitter=0.13, rot=(-0.12, 0.15, 1.9), seed=72)
    return b.finish(loc)


def make_small_02_baby_pine(loc):
    """Small compact 2-tier cozy baby pine."""
    b = CozyTreeBuilder("Cozy_Tree_Small_02_BabyPine")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (0.0, 0.0, 0.75),
            (0.0, 0.0, 1.55),
        ],
        [0.17, 0.13, 0.06],
        BARK_DARK,
        segs=6,
    )
    b.pine_tier((0.0, 0.0, 0.58), 0.80, 1.05, PINE_MID, segs=6, top_radius=0.16, tilt=(0.01, -0.02, 0.25), jitter=0.07, seed=81)
    b.pine_tier((0.0, 0.0, 1.32), 0.56, 1.15, PINE_SAGE, segs=6, top_radius=0.0, tilt=(-0.02, 0.01, 0.80), jitter=0.07, seed=82)
    return b.finish(loc)


def make_small_03_autumn_mini(loc):
    """Small ornamental low-poly autumn tree with warm red/gold puffs & base pebbles."""
    b = CozyTreeBuilder("Cozy_Tree_Small_03_AutumnMini")
    b.tube_path(
        [
            (0.0, 0.0, 0.0),
            (-0.04, 0.02, 0.52),
            (0.02, -0.02, 1.05),
        ],
        [0.17, 0.13, 0.09],
        BARK_WARM,
        segs=6,
    )
    b.tube_path([(-0.02, 0.01, 0.65), (-0.34, 0.14, 1.02)], [0.08, 0.05], BARK_WARM, segs=5)

    b.foliage_puff((0.12, -0.04, 1.44), (0.72, 0.68, 0.66), AUTUMN_ORANGE, subdivisions=1, jitter=0.14, rot=(0.14, -0.1, 0.4), seed=91)
    b.foliage_puff((-0.42, 0.16, 1.24), (0.54, 0.52, 0.48), AUTUMN_RED, subdivisions=1, jitter=0.14, rot=(-0.1, 0.15, 1.6), seed=92)
    b.foliage_puff((-0.05, -0.22, 1.72), (0.44, 0.42, 0.38), AUTUMN_GOLD, subdivisions=1, jitter=0.12, rot=(0.08, 0.12, 2.5), seed=93)

    b.pebble((0.28, -0.24, 0.07), (0.20, 0.16, 0.12), STONE_PEBBLE, seed=94)
    b.pebble((-0.26, -0.18, 0.05), (0.14, 0.13, 0.09), STONE_PEBBLE, seed=95)
    return b.finish(loc)


# Arrange in 3 clean rows by size for preview: Back=Large, Mid=Medium, Front=Small
TREES = [
    # Large (Back row)
    make_large_01_giant_oak((-5.6, 4.4, 0.0)),
    make_large_02_tall_spruce((0.0, 4.6, 0.0)),
    make_large_03_autumn_maple((5.6, 4.4, 0.0)),
    # Medium (Middle row)
    make_medium_01_meadow_oak((-5.0, -0.2, 0.0)),
    make_medium_02_forest_pine((0.0, -0.2, 0.0)),
    make_medium_03_golden_birch((5.0, -0.2, 0.0)),
    # Small (Front row)
    make_small_01_round_sapling((-4.0, -4.0, 0.0)),
    make_small_02_baby_pine((0.0, -4.0, 0.0)),
    make_small_03_autumn_mini((4.0, -4.0, 0.0)),
]

# Export each tree as an individual FBX with clean upright pivot at (0, 0, 0)
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
        bake_space_transform=True,
        apply_scale_options="FBX_SCALE_ALL",
        path_mode="AUTO",
    )
    obj.location = orig_loc
    print("Exported:", fbx_path)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


# Cozy meadow grass floor slab for preview rendering only
floor_bm = bmesh.new()
bmesh.ops.create_cube(floor_bm, size=1.0)
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((0.0, 0.2, -0.10)) @ Matrix.Diagonal((18.5, 14.0, 0.20, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Meadow_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_GRASS])
floor_obj = bpy.data.objects.new("Preview_Meadow_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

# Camera & Cozy Warm Sun + Cool Sky Fill Lighting (inspired by reference images)
bpy.ops.object.camera_add(location=(0.0, -19.5, 10.2))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 35
point_at(camera, (0.0, 0.4, 2.2))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(6.0, -8.0, 12.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.93, 0.78)
sun.data.energy = 3.2
sun.data.angle = math.radians(12.0)
point_at(sun, (0.0, 0.0, 1.5))

bpy.ops.object.light_add(type="AREA", location=(-8.0, -6.0, 8.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 650.0
fill.data.size = 9.0
point_at(fill, (0.0, 0.0, 2.0))

bpy.ops.object.light_add(type="AREA", location=(2.0, 8.5, 7.5))
rim = bpy.context.object
rim.data.color = (1.0, 0.88, 0.68)
rim.data.energy = 520.0
rim.data.size = 8.0
point_at(rim, (0.0, 0.5, 2.2))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 1800
scene.render.resolution_y = 1050
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.34, 0.44, 0.56)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# 1. Front 3/4 Full Lineup Render (Large back, Medium mid, Small front)
scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# 2. Top-Down In-Game Angle Render
camera.location = (0.0, -13.5, 19.0)
point_at(camera, (0.0, 0.5, 1.2))
scene.render.filepath = TOP_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

print("All 9 Cozy Low-Poly Trees generated successfully!")

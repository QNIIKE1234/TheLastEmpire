import math
import os
import random
import bpy
import bmesh
from mathutils import Euler, Matrix, Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Bushes_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Bushes_Preview.png")
TOP_PREVIEW_PATH = os.path.join(OUT_DIR, "Cozy_LowPoly_Bushes_TopDown_Preview.png")

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


# Exact matching Matte Cozy Low-Poly Palette (Linear RGB for Blender -> Unity useSRGBMaterialColor=1)
MATS = [
    make_material("Bark | cozy warm wood", (0.2500, 0.1450, 0.0750), 0.92, 0.0),
    make_material("Foliage | cozy deep green", (0.0850, 0.2750, 0.1150), 0.92, 0.0),
    make_material("Foliage | cozy meadow green", (0.1550, 0.3850, 0.1350), 0.90, 0.0),
    make_material("Foliage | cozy sunlit leaf", (0.2650, 0.4550, 0.1250), 0.90, 0.0),
    make_material("Foliage | cozy autumn gold", (0.7484, 0.3931, 0.0272), 0.90, 0.0),
    make_material("Foliage | cozy autumn orange", (0.5800, 0.2150, 0.0450), 0.90, 0.0),
    make_material("Foliage | cozy autumn red", (0.4800, 0.1150, 0.0650), 0.90, 0.0),
    make_material("Berry | cozy crimson", (0.6200, 0.0750, 0.0850), 0.85, 0.0),
    make_material("Flower | cozy blossom pink", (0.6800, 0.2600, 0.3600), 0.88, 0.0),
    make_material("Flower | cozy daisy cream", (0.7800, 0.7200, 0.5600), 0.88, 0.0),
    make_material("Stone | cozy pebble", (0.2400, 0.2500, 0.2750), 0.94, 0.0),
    make_material("Stone | cozy dark rock", (0.1400, 0.1500, 0.1750), 0.94, 0.0),
    make_material("Preview | meadow grass", (0.1850, 0.3350, 0.1250), 0.95, 0.0),
]

(
    BARK_WARM,
    LEAF_DEEP,
    LEAF_MEADOW,
    LEAF_SUNLIT,
    AUTUMN_GOLD,
    AUTUMN_ORANGE,
    AUTUMN_RED,
    BERRY_RED,
    FLOWER_PINK,
    FLOWER_CREAM,
    STONE_PEBBLE,
    STONE_DARK,
    PREVIEW_GRASS,
) = range(len(MATS))


class CozyBushBuilder:
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

    def puff(
        self,
        center,
        scale,
        mat_idx,
        subdivisions=1,
        jitter=0.14,
        rot=(0.0, 0.0, 0.0),
        seed=1,
        flatten_bottom=0.35,
    ):
        rng = random.Random(seed)
        src = bmesh.new()
        bmesh.ops.create_icosphere(src, subdivisions=subdivisions, radius=1.0)
        sx, sy, sz = scale
        for v in src.verts:
            d = 1.0 + rng.uniform(-jitter, jitter)
            co = v.co * d
            if co.z < 0.0 and flatten_bottom > 0.0:
                co.z *= (1.0 - flatten_bottom)
            v.co = Vector((co.x * sx, co.y * sy, co.z * sz))
        mat = Matrix.Translation(Vector(center)) @ Euler(rot, "XYZ").to_matrix().to_4x4()
        self._add_part(src, mat_idx, mat)

    def berry(self, center, radius, mat_idx, seed=1):
        self.puff(
            center,
            (radius, radius, radius * 0.95),
            mat_idx,
            subdivisions=1,
            jitter=0.06,
            rot=(seed * 0.4, seed * 0.7, seed * 0.2),
            seed=seed,
            flatten_bottom=0.0,
        )

    def flower(self, center, normal_dir, size, petal_mat, center_mat, seed=1):
        """Cute stylized flat-shaded 4-petal blossom oriented outward on the bush surface."""
        up = Vector(normal_dir).normalized()
        quat = Vector((0.0, 0.0, 1.0)).rotation_difference(up)
        rot_m = quat.to_matrix().to_4x4()
        spin_m = Euler((0.0, 0.0, seed * 0.85), "XYZ").to_matrix().to_4x4()
        base_m = Matrix.Translation(Vector(center)) @ rot_m @ spin_m

        # 4 chunky low-poly diamond petals
        for i in range(4):
            ang = i * (math.pi * 0.5)
            p_src = bmesh.new()
            bmesh.ops.create_icosphere(p_src, subdivisions=1, radius=1.0)
            for v in p_src.verts:
                v.co = Vector((v.co.x * size * 0.52, v.co.y * size * 0.36, v.co.z * size * 0.20))
            p_m = base_m @ Euler((0.0, 0.0, ang), "XYZ").to_matrix().to_4x4() @ Matrix.Translation((size * 0.52, 0.0, 0.0))
            self._add_part(p_src, petal_mat, p_m)

        # Center pollen bud
        c_src = bmesh.new()
        bmesh.ops.create_icosphere(c_src, subdivisions=1, radius=size * 0.34)
        self._add_part(c_src, center_mat, base_m @ Matrix.Translation((0.0, 0.0, size * 0.10)))

    def grass_blade(self, base_pos, width, height, lean_angle, yaw_angle, mat_idx):
        """Crisp low-poly tapered triangular/bent grass blade."""
        src = bmesh.new()
        w2 = width * 0.5
        v0 = src.verts.new(Vector((-w2, 0.0, 0.0)))
        v1 = src.verts.new(Vector((w2, 0.0, 0.0)))
        v2 = src.verts.new(Vector((w2 * 0.55, height * 0.20, height * 0.55)))
        v3 = src.verts.new(Vector((-w2 * 0.55, height * 0.20, height * 0.55)))
        v4 = src.verts.new(Vector((0.0, height * 0.48, height)))
        # Add thin back thickness so it never disappears from reverse angles
        t = width * 0.18
        b0 = src.verts.new(Vector((-w2, -t, 0.0)))
        b1 = src.verts.new(Vector((w2, -t, 0.0)))
        b2 = src.verts.new(Vector((w2 * 0.55, height * 0.20 - t * 0.7, height * 0.55)))
        b3 = src.verts.new(Vector((-w2 * 0.55, height * 0.20 - t * 0.7, height * 0.55)))
        src.faces.new((v0, v1, v2, v3))
        src.faces.new((v3, v2, v4))
        src.faces.new((b1, b0, b3, b2))
        src.faces.new((b2, b3, v4))
        src.faces.new((v1, b1, b2, v2))
        src.faces.new((b0, v0, v3, b3))
        src.faces.new((v2, b2, v4))
        src.faces.new((b3, v3, v4))
        bmesh.ops.recalc_face_normals(src, faces=src.faces)
        mat = Matrix.Translation(Vector(base_pos)) @ Euler((lean_angle, 0.0, yaw_angle), "XYZ").to_matrix().to_4x4()
        self._add_part(src, mat_idx, mat)

    def rock(self, center, scale, mat_idx, seed=1):
        self.puff(
            center,
            scale,
            mat_idx,
            subdivisions=1,
            jitter=0.18,
            rot=(0.15 * seed, 0.28 * seed, 0.65 * seed),
            seed=seed,
            flatten_bottom=0.50,
        )

    def finish(self, location=(0.0, 0.0, 0.0)):
        # Snap lowest vertices cleanly so bush never floats above z=0
        min_z = min(v.co.z for v in self.bm.verts)
        if min_z > 0.0:
            bmesh.ops.translate(self.bm, vec=Vector((0.0, 0.0, -min_z)), verts=self.bm.verts)

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
# 5 COZY LOW-POLY BUSHES
# =====================================================================

def make_bush_01_round_cluster(loc):
    """01: Classic lush rounded multi-puff meadow bush."""
    b = CozyBushBuilder("Cozy_Bush_01_RoundCluster")
    b.puff((0.0, 0.0, 0.42), (0.62, 0.58, 0.54), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.1, 0.15, 0.4), seed=101)
    b.puff((0.46, 0.10, 0.30), (0.44, 0.42, 0.38), LEAF_DEEP, subdivisions=1, jitter=0.14, rot=(-0.1, 0.2, 1.2), seed=102)
    b.puff((-0.44, -0.12, 0.32), (0.46, 0.42, 0.40), LEAF_DEEP, subdivisions=1, jitter=0.14, rot=(0.15, -0.1, 2.1), seed=103)
    b.puff((0.06, -0.34, 0.28), (0.40, 0.38, 0.34), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.05, 0.12, 2.8), seed=104)
    b.puff((-0.12, 0.12, 0.68), (0.38, 0.36, 0.32), LEAF_SUNLIT, subdivisions=1, jitter=0.12, rot=(-0.08, 0.14, 0.9), seed=105)
    return b.finish(loc)


def make_bush_02_berry_shrub(loc):
    """02: Cozy wild berry shrub with vibrant crimson berries on deep green foliage."""
    b = CozyBushBuilder("Cozy_Bush_02_BerryShrub")
    b.puff((0.0, 0.0, 0.44), (0.64, 0.60, 0.56), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(0.08, -0.12, 0.5), seed=201)
    b.puff((-0.48, 0.12, 0.34), (0.46, 0.44, 0.40), LEAF_MEADOW, subdivisions=1, jitter=0.13, rot=(0.14, 0.1, 1.4), seed=202)
    b.puff((0.48, -0.08, 0.36), (0.45, 0.42, 0.40), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(-0.12, 0.18, 2.3), seed=203)
    b.puff((0.12, 0.34, 0.46), (0.40, 0.38, 0.36), LEAF_MEADOW, subdivisions=1, jitter=0.12, rot=(0.1, 0.08, 0.2), seed=204)

    # Clusters of cute faceted crimson berries sitting cleanly on outer facets
    berries = [
        (-0.22, -0.46, 0.56, 0.085),
        (-0.08, -0.50, 0.48, 0.072),
        (0.32, -0.42, 0.52, 0.082),
        (0.46, -0.34, 0.40, 0.070),
        (-0.56, -0.18, 0.46, 0.078),
        (0.18, 0.06, 0.88, 0.080),
        (0.05, 0.18, 0.84, 0.068),
        (0.62, 0.10, 0.48, 0.076),
        (-0.34, 0.36, 0.54, 0.078),
        (0.18, 0.56, 0.52, 0.080),
    ]
    for idx, (bx, by, bz, br) in enumerate(berries):
        b.berry((bx, by, bz), br, BERRY_RED, seed=210 + idx)
    return b.finish(loc)


def make_bush_03_flowering_bush(loc):
    """03: Cozy garden flowering bush with pink & cream-gold low-poly blossoms."""
    b = CozyBushBuilder("Cozy_Bush_03_FloweringBush")
    b.puff((0.0, 0.0, 0.38), (0.60, 0.56, 0.48), LEAF_MEADOW, subdivisions=1, jitter=0.12, rot=(0.05, 0.1, 0.3), seed=301)
    b.puff((0.44, -0.06, 0.28), (0.42, 0.40, 0.36), LEAF_SUNLIT, subdivisions=1, jitter=0.13, rot=(-0.1, 0.14, 1.3), seed=302)
    b.puff((-0.44, 0.08, 0.30), (0.44, 0.40, 0.36), LEAF_DEEP, subdivisions=1, jitter=0.13, rot=(0.12, -0.12, 2.2), seed=303)
    b.puff((0.04, 0.26, 0.48), (0.38, 0.36, 0.34), LEAF_SUNLIT, subdivisions=1, jitter=0.12, rot=(0.08, 0.06, 1.0), seed=304)

    flowers = [
        ((-0.10, -0.44, 0.54), (-0.15, -0.7, 0.7), 0.145, FLOWER_PINK, AUTUMN_GOLD),
        ((0.36, -0.34, 0.46), (0.4, -0.5, 0.75), 0.135, FLOWER_CREAM, AUTUMN_GOLD),
        ((-0.46, -0.18, 0.48), (-0.6, -0.3, 0.72), 0.130, FLOWER_CREAM, AUTUMN_ORANGE),
        ((0.04, 0.04, 0.80), (0.05, -0.1, 1.0), 0.150, FLOWER_PINK, AUTUMN_GOLD),
        ((0.58, 0.06, 0.42), (0.75, 0.1, 0.65), 0.125, FLOWER_PINK, AUTUMN_GOLD),
        ((-0.24, 0.34, 0.58), (-0.3, 0.5, 0.8), 0.135, FLOWER_CREAM, AUTUMN_GOLD),
        ((0.24, 0.38, 0.58), (0.3, 0.5, 0.8), 0.130, FLOWER_PINK, AUTUMN_GOLD),
    ]
    for idx, (pos, nrm, sz, p_mat, c_mat) in enumerate(flowers):
        b.flower(pos, nrm, sz, p_mat, c_mat, seed=320 + idx)
    return b.finish(loc)


def make_bush_04_long_hedge(loc):
    """04: Elongated organic low-poly hedge row for fences, yards & pathways."""
    b = CozyBushBuilder("Cozy_Bush_04_LongHedge")
    clumps = [
        ((-0.94, 0.02, 0.34), (0.48, 0.46, 0.44), LEAF_DEEP, 401),
        ((-0.46, -0.03, 0.42), (0.52, 0.48, 0.50), LEAF_MEADOW, 402),
        ((0.02, 0.03, 0.46), (0.54, 0.50, 0.54), LEAF_MEADOW, 403),
        ((0.48, -0.02, 0.40), (0.50, 0.47, 0.48), LEAF_DEEP, 404),
        ((0.94, 0.02, 0.34), (0.46, 0.44, 0.42), LEAF_DEEP, 405),
        ((-0.24, 0.02, 0.72), (0.38, 0.36, 0.30), LEAF_SUNLIT, 406),
        ((0.34, -0.02, 0.68), (0.36, 0.34, 0.28), LEAF_SUNLIT, 407),
    ]
    for pos, sc, mat, sd in clumps:
        b.puff(pos, sc, mat, subdivisions=1, jitter=0.11, rot=(0.06 * (sd % 3), 0.08 * (sd % 2), sd * 0.5), seed=sd)
    return b.finish(loc)


def make_bush_05_autumn_rock_bush(loc):
    """05: Warm autumn bush tucked beside chunky low-poly rocks & grass tufts."""
    b = CozyBushBuilder("Cozy_Bush_05_AutumnRockBush")
    # Autumn foliage clumps
    b.puff((-0.14, 0.06, 0.40), (0.56, 0.54, 0.48), AUTUMN_ORANGE, subdivisions=1, jitter=0.14, rot=(0.1, -0.08, 0.4), seed=501)
    b.puff((-0.56, -0.08, 0.28), (0.40, 0.38, 0.34), AUTUMN_RED, subdivisions=1, jitter=0.14, rot=(-0.12, 0.14, 1.5), seed=502)
    b.puff((0.06, 0.26, 0.58), (0.38, 0.36, 0.34), AUTUMN_GOLD, subdivisions=1, jitter=0.13, rot=(0.08, 0.12, 2.3), seed=503)

    # Chunky flat-shaded rocks nestled on right/front
    b.rock((0.46, -0.14, 0.24), (0.38, 0.32, 0.28), STONE_PEBBLE, seed=510)
    b.rock((0.20, -0.40, 0.15), (0.24, 0.20, 0.18), STONE_DARK, seed=511)
    b.rock((0.68, 0.12, 0.14), (0.22, 0.20, 0.16), STONE_DARK, seed=512)

    # Crisp low-poly grass blade tufts around rock base
    grass_specs = [
        ((0.12, -0.52, 0.0), 0.10, 0.36, 0.28, 0.3, LEAF_SUNLIT),
        ((0.22, -0.56, 0.0), 0.09, 0.30, 0.35, -0.5, LEAF_MEADOW),
        ((-0.40, -0.44, 0.0), 0.10, 0.34, 0.30, 1.1, LEAF_SUNLIT),
        ((-0.30, -0.48, 0.0), 0.09, 0.28, 0.32, 0.4, LEAF_MEADOW),
        ((0.62, -0.34, 0.0), 0.09, 0.32, 0.34, -0.9, LEAF_SUNLIT),
    ]
    for g_pos, gw, gh, gl, gy, g_mat in grass_specs:
        b.grass_blade(g_pos, gw, gh, gl, gy, g_mat)
    return b.finish(loc)


# Arrange all 5 bushes in a neat curved/two-row showcase
BUSHES = [
    make_bush_01_round_cluster((-3.6, 1.4, 0.0)),
    make_bush_02_berry_shrub((0.0, 1.5, 0.0)),
    make_bush_03_flowering_bush((3.6, 1.4, 0.0)),
    make_bush_04_long_hedge((-2.1, -1.6, 0.0)),
    make_bush_05_autumn_rock_bush((2.2, -1.6, 0.0)),
]

# Export each bush as an upright individual FBX with pivot at (0, 0, 0)
for obj in BUSHES:
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


# Meadow grass slab for preview rendering only
floor_bm = bmesh.new()
bmesh.ops.create_cube(floor_bm, size=1.0)
bmesh.ops.transform(floor_bm, matrix=Matrix.Translation((0.0, 0.0, -0.10)) @ Matrix.Diagonal((11.5, 6.8, 0.20, 1.0)), verts=floor_bm.verts)
floor_mesh = bpy.data.meshes.new("Preview_Meadow_Floor_Mesh")
floor_bm.to_mesh(floor_mesh)
floor_bm.free()
floor_mesh.materials.append(MATS[PREVIEW_GRASS])
floor_obj = bpy.data.objects.new("Preview_Meadow_Floor", floor_mesh)
bpy.context.collection.objects.link(floor_obj)

# Camera & Cozy Lighting (matching the tree previews)
bpy.ops.object.camera_add(location=(0.0, -9.2, 5.0))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 38
point_at(camera, (0.0, -0.1, 0.45))
bpy.context.scene.camera = camera

bpy.ops.object.light_add(type="SUN", location=(5.0, -7.0, 10.0))
sun = bpy.context.object
sun.data.color = (1.0, 0.93, 0.78)
sun.data.energy = 3.2
sun.data.angle = math.radians(12.0)
point_at(sun, (0.0, 0.0, 0.4))

bpy.ops.object.light_add(type="AREA", location=(-6.0, -4.5, 6.0))
fill = bpy.context.object
fill.data.color = (0.72, 0.84, 1.0)
fill.data.energy = 420.0
fill.data.size = 7.0
point_at(fill, (0.0, 0.0, 0.4))

bpy.ops.object.light_add(type="AREA", location=(1.0, 6.5, 5.5))
rim = bpy.context.object
rim.data.color = (1.0, 0.88, 0.68)
rim.data.energy = 360.0
rim.data.size = 6.0
point_at(rim, (0.0, 0.0, 0.5))

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
scene.world.color = (0.34, 0.44, 0.56)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# 1. Front 3/4 Preview
scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# 2. Top-Down In-Game Angle Preview
camera.location = (0.0, -5.8, 9.2)
point_at(camera, (0.0, 0.0, 0.25))
scene.render.filepath = TOP_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

print("All 5 Cozy Low-Poly Bushes generated successfully!")

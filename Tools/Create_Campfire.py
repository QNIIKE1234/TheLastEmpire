import math
import os
import bpy
from mathutils import Vector, Euler


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)

BLEND_PATH = os.path.join(OUT_DIR, "Campfire_Source.blend")
FBX_PATH = os.path.join(OUT_DIR, "Apoc_Prop_Campfire.fbx")
PREVIEW_PATH = os.path.join(OUT_DIR, "Campfire_Preview.png")
TOP_PREVIEW_PATH = os.path.join(OUT_DIR, "Campfire_TopDown_Preview.png")


# Clean scene
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)


def make_material(name, color, roughness=0.82, metallic=0.0, emission=None, emission_strength=0.0):
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
    make_material("Stone | weathered gray", (0.34, 0.35, 0.33), 0.88, 0.0),
    make_material("Stone | warm soot", (0.23, 0.21, 0.19), 0.90, 0.0),
    make_material("Concrete | cinder block", (0.48, 0.47, 0.43), 0.86, 0.0),
    make_material("Ash | dark bed", (0.11, 0.11, 0.12), 0.96, 0.0),
    make_material("Charcoal | burnt black", (0.05, 0.05, 0.055), 0.94, 0.0),
    make_material("Wood | dry bark", (0.30, 0.18, 0.095), 0.85, 0.0),
    make_material("Wood | split heartwood", (0.60, 0.41, 0.22), 0.78, 0.0),
    make_material("Metal | rusted tray", (0.34, 0.15, 0.085), 0.72, 0.45),
    make_material("Metal | oxidized steel", (0.19, 0.21, 0.22), 0.50, 0.70),
    make_material("Metal | soot tin pot", (0.28, 0.30, 0.32), 0.42, 0.78),
    make_material("Enamel | chipped blue mug", (0.14, 0.32, 0.44), 0.35, 0.15),
    make_material("Cloth | dirty rag tie", (0.58, 0.48, 0.33), 0.88, 0.0),
    make_material("Ember | glowing crimson", (0.90, 0.12, 0.02), 0.35, 0.0, (0.92, 0.11, 0.015), 1.35),
    make_material("Flame | outer red-orange", (0.98, 0.24, 0.02), 0.25, 0.0, (0.98, 0.22, 0.02), 1.45),
    make_material("Flame | mid bright orange", (1.0, 0.48, 0.05), 0.20, 0.0, (1.0, 0.46, 0.04), 1.55),
    make_material("Flame | inner gold", (1.0, 0.82, 0.22), 0.15, 0.0, (1.0, 0.84, 0.25), 1.75),
    make_material("Broth | warm stew", (0.62, 0.36, 0.16), 0.22, 0.0),
]

(
    STONE_GRAY,
    STONE_SOOT,
    CINDER_CONCRETE,
    ASH_BED,
    CHARCOAL,
    WOOD_BARK,
    WOOD_HEART,
    RUST_METAL,
    OXIDIZED_STEEL,
    TIN_POT,
    ENAMEL_BLUE,
    CLOTH_TIE,
    EMBER_RED,
    FLAME_RED_ORANGE,
    FLAME_ORANGE,
    FLAME_GOLD,
    STEW_BROTH,
) = range(len(MATS))


class MeshBuilder:
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

    def rock(self, center, radii, material, rot=(0.0, 0.0, 0.0), seed=0, flat_top=False):
        """Creates a clean convex faceted low-poly boulder/stone."""
        rx, ry, rz = radii
        start = len(self.vertices)
        rings = [
            (-0.85 * rz, 0.72),
            (-0.18 * rz, 1.00),
            (0.48 * rz, 0.84),
            (0.88 * rz if flat_top else 0.94 * rz, 0.56 if flat_top else 0.44),
        ]
        segs = 7
        for r_idx, (z_val, scale) in enumerate(rings):
            for i in range(segs):
                ang = (2.0 * math.pi * i / segs) + (r_idx * 0.18) + (seed * 0.55)
                wobble = 0.94 + 0.08 * math.sin(seed * 1.9 + i * 2.1 + r_idx * 0.9)
                px = rx * scale * wobble * math.cos(ang)
                py = ry * scale * wobble * math.sin(ang)
                pz = z_val if (flat_top and r_idx == 3) else z_val * (0.95 + 0.06 * math.cos(seed + i * 1.7))
                self.vertices.append(self._transform((px, py, pz), center, rot))
        bot_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, -rz), center, rot))
        top_idx = len(self.vertices)
        self.vertices.append(self._transform((0.0, 0.0, 0.90 * rz if flat_top else rz), center, rot))

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

    def cinder_block_half(self, center, angle_z, material):
        """Creates a broken/half hollow cinder block with a visible square cavity."""
        rot = (0.02, -0.02, angle_z)
        w, d, h = 0.22, 0.16, 0.13
        wall = 0.034
        self.box(self._transform((0, -d * 0.5 + wall * 0.5, 0), center, rot), (w, wall, h), material, rot)
        self.box(self._transform((0, d * 0.5 - wall * 0.5, 0), center, rot), (w, wall, h), material, rot)
        self.box(self._transform((-w * 0.5 + wall * 0.5, 0, 0), center, rot), (wall, d - wall * 2, h), material, rot)
        self.box(self._transform((w * 0.32, 0, 0), center, rot), (wall * 1.15, d - wall * 2, h), material, rot)

    def split_log(self, start_pt, end_pt, radius, rot_roll=0.0, charred_top=True):
        """Creates a stylized split firewood log with bark outside, heartwood split side, and charred tip."""
        a, b = Vector(start_pt), Vector(end_pt)
        axis = (b - a).normalized()
        u = axis.cross(Vector((0, 0, 1)))
        if u.length < 1e-4:
            u = axis.cross(Vector((0, 1, 0)))
        u.normalize()
        v = axis.cross(u).normalized()

        cr, sr = math.cos(rot_roll), math.sin(rot_roll)
        u_rot = cr * u + sr * v
        v_rot = -sr * u + cr * v

        segs = 8
        profile = []
        for i in range(segs):
            ang = -0.15 * math.pi + (1.30 * math.pi * i / (segs - 1))
            r_wobble = radius * (0.95 + 0.06 * math.sin(i * 2.1))
            profile.append((r_wobble * math.cos(ang), r_wobble * math.sin(ang)))

        t_steps = [0.0, 0.66, 1.0]
        base = len(self.vertices)
        for t in t_steps:
            pos_center = a.lerp(b, t)
            taper = 1.0 - (0.10 * t if charred_top else 0.0)
            for px, py in profile:
                pt = pos_center + (px * taper) * u_rot + (py * taper) * v_rot
                self.vertices.append((pt.x, pt.y, pt.z))

        self._face((base + i for i in reversed(range(segs))), WOOD_HEART, False)
        top_ring = base + 2 * segs
        self._face((top_ring + i for i in range(segs)), CHARCOAL if charred_top else WOOD_HEART, False)

        for sec in range(2):
            r0 = base + sec * segs
            r1 = base + (sec + 1) * segs
            for i in range(segs):
                j = (i + 1) % segs
                if sec == 1 and charred_top:
                    mat = CHARCOAL
                else:
                    mat = WOOD_HEART if i == (segs - 1) else WOOD_BARK
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), mat, i != (segs - 1))

    def rod_chain(self, points, radius, material, segments=8, closed=False):
        """Creates a continuous pipe/rod along a list of 3D points."""
        base_rings = []
        count = len(points)
        for idx, pt in enumerate(points):
            p = Vector(pt)
            if idx == 0:
                tangent = (Vector(points[1]) - p).normalized()
            elif idx == count - 1:
                tangent = (p - Vector(points[idx - 1])).normalized()
            else:
                tangent = (Vector(points[idx + 1]) - Vector(points[idx - 1])).normalized()

            u = tangent.cross(Vector((0, 0, 1)))
            if u.length < 1e-4:
                u = tangent.cross(Vector((0, 1, 0)))
            u.normalize()
            v = tangent.cross(u).normalized()

            ring_start = len(self.vertices)
            for i in range(segments):
                ang = 2.0 * math.pi * i / segments
                pos = p + radius * (math.cos(ang) * u + math.sin(ang) * v)
                self.vertices.append((pos.x, pos.y, pos.z))
            base_rings.append(ring_start)

        for idx in range(count - 1):
            r0 = base_rings[idx]
            r1 = base_rings[idx + 1]
            for i in range(segments):
                j = (i + 1) % segments
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), material, True)

        if not closed:
            self._face((base_rings[0] + i for i in reversed(range(segments))), material, False)
            self._face((base_rings[-1] + i for i in range(segments)), material, False)

    def lathe(self, center, profile_rz, material, segments=14, mat_per_band=None, cap_top_mat=None):
        """Revolves a (radius, z) profile around local Z axis."""
        cx, cy, cz = center
        rings = []
        for r, z in profile_rz:
            ring_start = len(self.vertices)
            for i in range(segments):
                ang = 2.0 * math.pi * i / segments
                self.vertices.append((cx + r * math.cos(ang), cy + r * math.sin(ang), cz + z))
            rings.append(ring_start)

        for b_idx in range(len(rings) - 1):
            r0 = rings[b_idx]
            r1 = rings[b_idx + 1]
            mat = mat_per_band[b_idx] if mat_per_band else material
            for i in range(segments):
                j = (i + 1) % segments
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), mat, True)

        self._face((rings[0] + i for i in reversed(range(segments))), material, False)
        self._face((rings[-1] + i for i in range(segments)), cap_top_mat if cap_top_mat is not None else material, False)

    def flame_tongue(self, base_pt, height, base_radius, material, tilt_x=0.0, tilt_y=0.0, twist=0.6):
        """Creates a stylized twisted low-poly flame tongue."""
        bx, by, bz = base_pt
        levels = [
            (0.00, 0.78, 0.0, 0.0),
            (0.28, 1.00, tilt_x * 0.25, tilt_y * 0.25),
            (0.60, 0.65, tilt_x * 0.65, tilt_y * 0.65),
            (0.85, 0.28, tilt_x * 0.90, tilt_y * 0.90),
        ]
        segs = 6
        rings = []
        for l_idx, (t, scale, dx, dy) in enumerate(levels):
            r_start = len(self.vertices)
            for i in range(segs):
                ang = (2.0 * math.pi * i / segs) + (t * twist)
                star = 1.0 if i % 2 == 0 else 0.78
                r = base_radius * scale * star
                self.vertices.append((bx + dx + r * math.cos(ang), by + dy + r * math.sin(ang), bz + t * height))
            rings.append(r_start)

        bot_idx = len(self.vertices)
        self.vertices.append((bx, by, bz))
        tip_idx = len(self.vertices)
        self.vertices.append((bx + tilt_x, by + tilt_y, bz + height))

        for i in range(segs):
            j = (i + 1) % segs
            self._face((bot_idx, rings[0] + j, rings[0] + i), material, False)
            self._face((tip_idx, rings[-1] + i, rings[-1] + j), material, False)

        for l_idx in range(len(rings) - 1):
            r0 = rings[l_idx]
            r1 = rings[l_idx + 1]
            for i in range(segs):
                j = (i + 1) % segs
                self._face((r0 + i, r0 + j, r1 + j, r1 + i), material, False)

    def build_object(self, add_bevel=True):
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
        if add_bevel:
            bevel = obj.modifiers.new("WornEdges", "BEVEL")
            bevel.width = 0.007
            bevel.segments = 1
            bevel.limit_method = "ANGLE"
            bevel.angle_limit = math.radians(38)
        return obj


def build_campfire():
    base = MeshBuilder("Campfire_Base")

    # 1. Salvaged Battered Sheet-Metal Ground Tray (Radius ~0.44m, sits flush at z=0)
    tray_segs = 12
    tray_r_outer = 0.44
    tray_r_inner = 0.38
    t_start = len(base.vertices)
    for i in range(tray_segs):
        ang = 2.0 * math.pi * i / tray_segs
        dent = 0.96 + 0.05 * math.sin(i * 2.7 + 0.5)
        ro = tray_r_outer * dent
        ri = tray_r_inner * dent
        base.vertices.append((ri * math.cos(ang), ri * math.sin(ang), 0.004))
        base.vertices.append((ro * math.cos(ang), ro * math.sin(ang), 0.022 + 0.006 * math.cos(i * 3.0)))
        base.vertices.append((ro * math.cos(ang), ro * math.sin(ang), 0.001))
    center_v = len(base.vertices)
    base.vertices.append((0.0, 0.0, 0.004))
    for i in range(tray_segs):
        j = (i + 1) % tray_segs
        i0, i1, i2 = t_start + i * 3, t_start + i * 3 + 1, t_start + i * 3 + 2
        j0, j1, j2 = t_start + j * 3, t_start + j * 3 + 1, t_start + j * 3 + 2
        base._face((center_v, i0, j0), RUST_METAL, False)
        base._face((i0, i1, j1, j0), RUST_METAL, False)
        base._face((i1, i2, j2, j1), OXIDIZED_STEEL, False)

    # 2. Domed Ash & Charcoal Bed inside the ring (Radius ~0.35m)
    base.lathe(
        (0.0, 0.0, 0.0),
        [(0.36, 0.006), (0.31, 0.032), (0.20, 0.056), (0.06, 0.068)],
        ASH_BED,
        segments=12,
        mat_per_band=[ASH_BED, ASH_BED, CHARCOAL],
    )

    # 3. Perimeter Ring: Weathered Stones + 2 Scavenged Half Cinder Blocks
    ring_radius = 0.355
    num_slots = 10
    mug_stone_pos = None
    for s in range(num_slots):
        ang = (2.0 * math.pi * s / num_slots) + 0.15
        cx = ring_radius * math.cos(ang)
        cy = ring_radius * math.sin(ang)
        if s in (2, 7):
            base.cinder_block_half((cx, cy, 0.065), ang + math.pi * 0.5, CINDER_CONCRETE)
        elif s == 9:
            # Wide flat-topped stone slab on front-right for resting the survivor's mug
            rz = 0.064
            base.rock(
                (cx, cy, rz * 0.80),
                (0.115, 0.098, rz),
                STONE_SOOT,
                rot=(0.0, 0.0, ang),
                seed=s + 1,
                flat_top=True,
            )
            mug_stone_pos = (cx, cy, rz * 0.80 + 0.88 * rz)
        else:
            mat = STONE_GRAY if s % 2 == 0 else STONE_SOOT
            rx = 0.095 + 0.018 * ((s * 3) % 3)
            ry = 0.082 + 0.015 * ((s * 5) % 3)
            rz = 0.062 + 0.014 * ((s * 2) % 3)
            base.rock(
                (cx, cy, rz * 0.82),
                (rx, ry, rz),
                mat,
                rot=(0.08 * math.sin(s), 0.08 * math.cos(s), ang),
                seed=s + 1,
            )

    # 4. Split Firewood Logs (Base Cribbing + Teepee Pyramid Stack)
    base_logs = [
        ((-0.21, -0.10, 0.055), (0.19, 0.12, 0.075), 0.044, 0.3),
        ((-0.14, 0.18, 0.060), (0.20, -0.14, 0.070), 0.042, 1.8),
        ((-0.04, -0.22, 0.052), (0.06, 0.21, 0.082), 0.040, -0.9),
    ]
    for p0, p1, rad, roll in base_logs:
        base.split_log(p0, p1, rad, rot_roll=roll, charred_top=True)

    teepee_angles = [0.1, 1.35, 2.55, 3.85, 5.10]
    for idx, ang in enumerate(teepee_angles):
        r_bottom = 0.23
        r_top = 0.055 + (0.012 if idx % 2 == 0 else 0.0)
        z_bottom = 0.065
        z_top = 0.265 + 0.020 * (idx % 2)
        p_start = (r_bottom * math.cos(ang), r_bottom * math.sin(ang), z_bottom)
        p_end = (r_top * math.cos(ang + 0.22), r_top * math.sin(ang + 0.22), z_top)
        base.split_log(p_start, p_end, 0.038, rot_roll=ang + 1.2, charred_top=True)

    # 5. Survivor Decoration #1: Bent Rebar Spit / Mini Tripod + Hanging Battered Tin Billy Pot
    rebar_main = [
        (-0.34, 0.21, 0.01),
        (-0.35, 0.22, 0.28),
        (-0.31, 0.19, 0.50),
        (-0.17, 0.10, 0.57),
        (-0.04, 0.02, 0.54),
        (-0.04, 0.02, 0.48),
    ]
    base.rod_chain(rebar_main, 0.013, OXIDIZED_STEEL, segments=8)

    rebar_strut = [
        (-0.21, 0.35, 0.01),
        (-0.27, 0.25, 0.26),
        (-0.31, 0.19, 0.47),
    ]
    base.rod_chain(rebar_strut, 0.010, RUST_METAL, segments=8)

    # Cloth lashing at the rebar joint
    base.box((-0.31, 0.19, 0.46), (0.044, 0.044, 0.040), CLOTH_TIE, rot=(0.2, 0.1, 0.6))

    # Battered Tin Billy Can / Camp Pot with hot stew inside
    pot_x, pot_y, pot_z = -0.04, 0.02, 0.325
    base.lathe(
        (pot_x, pot_y, pot_z),
        [
            (0.068, 0.000),
            (0.076, 0.015),
            (0.074, 0.055),
            (0.078, 0.095),
            (0.082, 0.102),
            (0.068, 0.102),
            (0.065, 0.080),
        ],
        TIN_POT,
        segments=12,
        mat_per_band=[CHARCOAL, CHARCOAL, TIN_POT, OXIDIZED_STEEL, TIN_POT, CHARCOAL],
        cap_top_mat=STEW_BROTH,
    )
    bail_pts = [
        (pot_x - 0.076, pot_y, pot_z + 0.092),
        (pot_x - 0.062, pot_y, pot_z + 0.145),
        (pot_x, pot_y, pot_z + 0.168),
        (pot_x + 0.062, pot_y, pot_z + 0.145),
        (pot_x + 0.076, pot_y, pot_z + 0.092),
    ]
    base.rod_chain(bail_pts, 0.0055, OXIDIZED_STEEL, segments=6)

    # 6. Survivor Decoration #2: Chipped Blue Enamel Mug resting squarely on the flat-top stone (s=9)
    mug_x, mug_y, mug_z = mug_stone_pos
    base.lathe(
        (mug_x, mug_y, mug_z),
        [
            (0.035, 0.000),
            (0.038, 0.068),
            (0.041, 0.074),
            (0.034, 0.074),
            (0.032, 0.054),
        ],
        ENAMEL_BLUE,
        segments=10,
        mat_per_band=[ENAMEL_BLUE, OXIDIZED_STEEL, OXIDIZED_STEEL, CHARCOAL],
        cap_top_mat=STEW_BROTH,
    )
    mug_handle = [
        (mug_x + 0.036, mug_y, mug_z + 0.055),
        (mug_x + 0.055, mug_y, mug_z + 0.049),
        (mug_x + 0.055, mug_y, mug_z + 0.020),
        (mug_x + 0.036, mug_y, mug_z + 0.015),
    ]
    base.rod_chain(mug_handle, 0.005, ENAMEL_BLUE, segments=6)

    # 7. Survivor Decoration #3: Tied Spare Kindling Bundle resting by outer edge (within footprint)
    bundle_center = (-0.28, -0.29, 0.042)
    for k_idx, (dz, dy_off, roll) in enumerate([(0.0, -0.022, 0.2), (0.0, 0.022, -0.5), (0.032, 0.0, 1.1)]):
        p0 = (bundle_center[0] - 0.09, bundle_center[1] + dy_off + 0.06, bundle_center[2] + dz)
        p1 = (bundle_center[0] + 0.09, bundle_center[1] + dy_off - 0.06, bundle_center[2] + dz + 0.01)
        base.split_log(p0, p1, 0.022, rot_roll=roll, charred_top=False)
    base.box(
        (bundle_center[0], bundle_center[1], bundle_center[2] + 0.015),
        (0.036, 0.078, 0.068),
        CLOTH_TIE,
        rot=(0.0, 0.0, -0.58),
    )

    base_obj = base.build_object(add_bevel=True)

    # Build separate Embers + 3-Layer Stylized Flame Mesh ("Campfire_Fire")
    fire = MeshBuilder("Campfire_Fire")

    ember_positions = [
        (0.00, 0.00, 0.068, (0.080, 0.075, 0.040)),
        (0.085, 0.05, 0.062, (0.058, 0.050, 0.032)),
        (-0.075, 0.06, 0.064, (0.054, 0.060, 0.032)),
        (-0.065, -0.075, 0.060, (0.062, 0.048, 0.032)),
        (0.075, -0.065, 0.062, (0.052, 0.056, 0.030)),
        (0.02, 0.115, 0.055, (0.045, 0.040, 0.026)),
        (-0.115, -0.01, 0.055, (0.042, 0.046, 0.026)),
    ]
    for idx, (ex, ey, ez, eradii) in enumerate(ember_positions):
        fire.rock((ex, ey, ez), eradii, EMBER_RED, rot=(0.2 * idx, -0.15 * idx, 0.8 * idx), seed=idx + 20)

    # Outer deep red-orange flame tongues around the base/perimeter of the fire
    fire.flame_tongue((0.065, 0.045, 0.072), 0.175, 0.062, FLAME_RED_ORANGE, tilt_x=0.025, tilt_y=0.015, twist=-0.6)
    fire.flame_tongue((-0.060, 0.055, 0.072), 0.165, 0.058, FLAME_RED_ORANGE, tilt_x=-0.02, tilt_y=0.02, twist=0.5)
    fire.flame_tongue((-0.045, -0.065, 0.072), 0.180, 0.060, FLAME_RED_ORANGE, tilt_x=-0.015, tilt_y=-0.025, twist=-0.7)
    fire.flame_tongue((0.055, -0.055, 0.072), 0.155, 0.055, FLAME_RED_ORANGE, tilt_x=0.02, tilt_y=-0.015, twist=0.65)
    fire.flame_tongue((0.005, 0.075, 0.072), 0.150, 0.052, FLAME_RED_ORANGE, tilt_x=0.005, tilt_y=0.02, twist=0.45)

    # Mid bright orange flame tongues
    fire.flame_tongue((0.00, 0.00, 0.078), 0.245, 0.088, FLAME_ORANGE, tilt_x=0.012, tilt_y=-0.008, twist=0.75)
    fire.flame_tongue((0.038, -0.025, 0.080), 0.195, 0.055, FLAME_ORANGE, tilt_x=0.015, tilt_y=-0.012, twist=0.5)
    fire.flame_tongue((-0.035, 0.030, 0.080), 0.185, 0.052, FLAME_ORANGE, tilt_x=-0.012, tilt_y=0.012, twist=-0.5)

    # Inner golden-yellow hot core & rising tip
    fire.flame_tongue((0.00, 0.00, 0.088), 0.270, 0.052, FLAME_GOLD, tilt_x=0.006, tilt_y=-0.004, twist=-0.55)
    fire.flame_tongue((0.018, 0.015, 0.095), 0.210, 0.038, FLAME_GOLD, tilt_x=0.008, tilt_y=0.008, twist=0.4)
    fire.flame_tongue((-0.018, -0.018, 0.095), 0.200, 0.036, FLAME_GOLD, tilt_x=-0.008, tilt_y=-0.008, twist=-0.4)

    fire_obj = fire.build_object(add_bevel=False)
    fire_obj.parent = base_obj

    return base_obj, fire_obj


base_obj, fire_obj = build_campfire()


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


# Export FBX (with both Campfire_Base and child Campfire_Fire)
bpy.ops.object.select_all(action="DESELECT")
base_obj.select_set(True)
fire_obj.select_set(True)
bpy.context.view_layer.objects.active = base_obj
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
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


# Add floor disc only for preview rendering (not exported into FBX)
preview_floor_builder = MeshBuilder("Preview_Ground")
preview_floor_builder.lathe((0, 0, -0.002), [(1.35, 0.0), (1.35, -0.02)], STONE_SOOT, segments=32)
preview_floor = preview_floor_builder.build_object(add_bevel=False)

# Camera & lighting for preview renders
bpy.ops.object.camera_add(location=(1.15, -1.25, 0.98))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 45
point_at(camera, (0.0, 0.0, 0.20))
bpy.context.scene.camera = camera

# Warm fire point light right in the campfire core
bpy.ops.object.light_add(type="POINT", location=(0.0, 0.0, 0.22))
fire_light = bpy.context.object
fire_light.name = "Campfire_PointLight"
fire_light.data.color = (1.0, 0.45, 0.12)
fire_light.data.energy = 85.0
fire_light.data.shadow_soft_size = 0.16

# Cool night/dusk key & rim lights to make the warm campfire pop
bpy.ops.object.light_add(type="AREA", location=(1.8, -1.5, 2.2))
key_light = bpy.context.object
key_light.data.color = (0.70, 0.78, 0.95)
key_light.data.energy = 38.0
key_light.data.size = 2.0
point_at(key_light, (0.0, 0.0, 0.15))

bpy.ops.object.light_add(type="AREA", location=(-1.6, 1.4, 1.8))
rim_light = bpy.context.object
rim_light.data.color = (0.55, 0.68, 0.90)
rim_light.data.energy = 30.0
rim_light.data.size = 1.8
point_at(rim_light, (0.0, 0.0, 0.20))

scene = bpy.context.scene
scene.render.engine = (
    "BLENDER_EEVEE_NEXT"
    if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    else "BLENDER_EEVEE"
)
scene.render.resolution_x = 1200
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.03, 0.036, 0.048)

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# 1. 3/4 Hero Preview Render
scene.render.filepath = PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# 2. Top-Down In-Game Camera Angle Render
camera.location = (0.0, -0.95, 1.45)
point_at(camera, (0.0, 0.0, 0.12))
scene.render.filepath = TOP_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

print("Campfire exported successfully:", FBX_PATH)

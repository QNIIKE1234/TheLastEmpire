import math
import os
import bpy
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedEnvironment")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Apocalypse_Environment_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Environment_Preview.png")
BACK_PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Environment_Back_Preview.png")
HOUSES_PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Houses_Preview.png")
VEHICLES_PREVIEW_PATH = os.path.join(OUT_DIR, "Apocalypse_Vehicles_Preview.png")


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)


def make_material(name, color, roughness=0.82, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return material


MATS = [
    make_material("Concrete | ash gray", (0.34, 0.35, 0.34)),
    make_material("Concrete | broken pale edge", (0.52, 0.50, 0.44)),
    make_material("Concrete | deep shadow", (0.13, 0.15, 0.16)),
    make_material("Glass | dead blue", (0.055, 0.10, 0.13), 0.28, 0.18),
    make_material("Glass | cracked haze", (0.20, 0.28, 0.30), 0.38, 0.08),
    make_material("Metal | oxidized", (0.20, 0.22, 0.21), 0.6, 0.55),
    make_material("Rust | burnt umber", (0.34, 0.14, 0.075)),
    make_material("Rust | faded red", (0.47, 0.19, 0.11)),
    make_material("Paint | old mustard", (0.57, 0.43, 0.16)),
    make_material("Paint | faded blue", (0.14, 0.25, 0.31)),
    make_material("Rubber | charcoal", (0.055, 0.06, 0.06), 0.95),
    make_material("Light | dirty ivory", (0.55, 0.52, 0.42)),
    make_material("Stripe | faded warning", (0.64, 0.32, 0.13)),
    make_material("Signal | dead red", (0.42, 0.08, 0.055), 0.52),
    make_material("Signal | dead amber", (0.52, 0.31, 0.08), 0.5),
    make_material("Signal | dead green", (0.08, 0.29, 0.17), 0.5),
    make_material("Brick | soot stained", (0.34, 0.16, 0.11)),
    make_material("Paint | flaked teal", (0.12, 0.29, 0.27)),
]
CONCRETE, BROKEN, SHADOW, GLASS, GLASS_HAZE, METAL, RUST, RED_RUST, MUSTARD, BLUE, RUBBER, IVORY, WARNING, SIGNAL_RED, SIGNAL_AMBER, SIGNAL_GREEN, BRICK, TEAL = range(len(MATS))


class AssetBuilder:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth_faces = []

    def _face(self, indices, material, smooth=False):
        self.faces.append(tuple(indices))
        self.material_indices.append(material)
        self.smooth_faces.append(smooth)

    def box(self, center, size, material, angle=0.0, pitch=0.0):
        cx, cy, cz = center
        sx, sy, sz = (v * 0.5 for v in size)
        start = len(self.vertices)
        ca, sa = math.cos(angle), math.sin(angle)
        cp, sp = math.cos(pitch), math.sin(pitch)
        for x, y, z in ((-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),
                        (-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)):
            pitched_x, pitched_z = x * cp + z * sp, -x * sp + z * cp
            self.vertices.append((cx + pitched_x * ca - y * sa, cy + pitched_x * sa + y * ca, cz + pitched_z))
        for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
            self._face((start + i for i in face), material)

    def cylinder(self, center, radius, depth, material, axis="z", segments=16):
        cx, cy, cz = center
        start = len(self.vertices)
        for side in (-0.5, 0.5):
            for i in range(segments):
                angle = 2.0 * math.pi * i / segments
                u, v = radius * math.cos(angle), radius * math.sin(angle)
                if axis == "x":
                    self.vertices.append((cx + side * depth, cy + u, cz + v))
                elif axis == "y":
                    self.vertices.append((cx + u, cy + side * depth, cz + v))
                else:
                    self.vertices.append((cx + u, cy + v, cz + side * depth))
        self._face((start + i for i in reversed(range(segments))), material)
        self._face((start + segments + i for i in range(segments)), material)
        for i in range(segments):
            j = (i + 1) % segments
            self._face((start + i, start + j, start + segments + j, start + segments + i), material, True)

    def rod(self, start, end, radius, material, segments=10):
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        u = axis.cross(Vector((0, 0, 1)))
        if u.length < 1e-4:
            u = axis.cross(Vector((0, 1, 0)))
        u.normalize()
        v = axis.cross(u).normalized()
        base = len(self.vertices)
        for point in (a, b):
            for i in range(segments):
                angle = 2 * math.pi * i / segments
                pos = point + radius * (math.cos(angle) * u + math.sin(angle) * v)
                self.vertices.append(tuple(pos))
        self._face((base + i for i in reversed(range(segments))), material)
        self._face((base + segments + i for i in range(segments)), material)
        for i in range(segments):
            j = (i + 1) % segments
            self._face((base + i, base + j, base + segments + j, base + segments + i), material, True)

    def wedge(self, points, z_bottom, z_top, material):
        start = len(self.vertices)
        self.vertices.extend((x, y, z_bottom) for x, y in points)
        self.vertices.extend((x, y, z_top) for x, y in points)
        count = len(points)
        self._face((start + i for i in reversed(range(count))), material)
        self._face((start + count + i for i in range(count)), material)
        for i in range(count):
            j = (i + 1) % count
            self._face((start + i, start + j, start + count + j, start + count + i), material)

    def gable_roof(self, center, width, depth, eaves_z, rise, material):
        cx, cy, cz = center
        half_w, half_d = width * 0.5, depth * 0.5
        start = len(self.vertices)
        self.vertices.extend([
            (cx-half_w, cy-half_d, cz+eaves_z), (cx-half_w, cy, cz+eaves_z+rise),
            (cx-half_w, cy+half_d, cz+eaves_z), (cx+half_w, cy-half_d, cz+eaves_z),
            (cx+half_w, cy, cz+eaves_z+rise), (cx+half_w, cy+half_d, cz+eaves_z),
        ])
        self._face((start+0,start+3,start+4,start+1), material)
        self._face((start+1,start+4,start+5,start+2), material)
        self._face((start+0,start+1,start+2), material)
        self._face((start+3,start+5,start+4), material)
        self._face((start+0,start+2,start+5,start+3), material)

    def rounded_body(self, profiles, material, radial_segments=32, steps=4, center_x=0.0, center_y=0.0):
        """Rounded-square cross-sections make a continuous car shell, not stacked boxes."""
        samples = []
        for left, right in zip(profiles, profiles[1:]):
            for step in range(steps):
                t = step / steps
                samples.append(tuple(a * (1.0 - t) + b * t for a, b in zip(left, right)))
        samples.append(profiles[-1])
        start = len(self.vertices)
        exponent = 0.60
        for x, width, half_height, center_z in samples:
            for ring in range(radial_segments):
                angle = 2.0 * math.pi * ring / radial_segments
                cy = math.cos(angle)
                sz = math.sin(angle)
                y = width * math.copysign(abs(cy) ** exponent, cy)
                z = center_z + half_height * math.copysign(abs(sz) ** exponent, sz)
                self.vertices.append((center_x + x, center_y + y, z))
        for section in range(len(samples) - 1):
            current = start + section * radial_segments
            nxt = current + radial_segments
            for ring in range(radial_segments):
                other = (ring + 1) % radial_segments
                self._face((current + ring, current + other, nxt + other, nxt + ring), material, True)
        self._face((start + i for i in reversed(range(radial_segments))), material)
        end = start + (len(samples) - 1) * radial_segments
        self._face((end + i for i in range(radial_segments)), material)

    def finish(self, location=(0, 0, 0)):
        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.materials.clear()
        for material in MATS:
            mesh.materials.append(material)
        mesh.update()
        for polygon, mat_index, smooth in zip(mesh.polygons, self.material_indices, self.smooth_faces):
            polygon.material_index = mat_index
            polygon.use_smooth = smooth
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        bevel = obj.modifiers.new("Small worn edge bevels", "BEVEL")
        bevel.width = 0.025
        bevel.segments = 2
        bevel.limit_method = "ANGLE"
        bevel.angle_limit = math.radians(35)
        bevel.harden_normals = True
        bevel = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
        bevel.keep_sharp = True
        return obj


ASSETS = []
BUILDINGS = []
HOUSES = []
VEHICLES = []
MOTORCYCLES = []
PROPS = []


def make_building(name, width, depth, height, stories, style, display_location):
    b = AssetBuilder(name)
    floor_h = height / stories
    # Dark interior core is visible through missing windows and broken wall panels.
    if style == 0:
        b.box((0, 0, height * 0.43), (width, depth, height * 0.86), CONCRETE)
        b.box((0.30, 0.12, height * 0.93), (width * 0.76, depth * 0.82, height * 0.14), CONCRETE)
    elif style == 1:
        b.box((0, 0, height * 0.5), (width * 0.94, depth * 0.94, height), SHADOW)
    elif style == 2:
        b.box((0, 0, height * 0.5), (width, depth, height), CONCRETE)
    else:
        b.box((-0.12, 0.04, height * 0.48), (width, depth, height * 0.96), CONCRETE)
        b.box((0.42, 0.08, height * 0.93), (width * 0.72, depth * 0.83, height * 0.12), SHADOW)

    front = -depth * 0.5 - 0.035
    columns = 3 if width < 5.5 else 4
    bay = (width - 0.46) / columns
    for floor in range(stories):
        zbase = floor * floor_h
        zwin = zbase + floor_h * 0.53
        # Concrete ledges and vertical ribs give the facade readable floor scale.
        b.box((0, front - 0.015, zbase + 0.10), (width + 0.20, 0.14, 0.19), BROKEN if floor % 4 == 3 else CONCRETE)
        for col in range(columns + 1):
            x = -width * 0.5 + 0.12 + col * bay
            b.box((x, front - 0.02, zbase + floor_h * 0.55), (0.14, 0.19, floor_h * 0.82), CONCRETE if (col + floor) % 3 else BROKEN)
        for col in range(columns):
            x = -width * 0.5 + bay * (col + 0.5) + 0.23
            win_w, win_h = bay * 0.63, floor_h * 0.52
            missing = ((floor * 3 + col * 5 + style) % (7 if style != 2 else 5) == 0)
            if not missing:
                glass_mat = GLASS_HAZE if (floor + col + style) % 4 == 0 else GLASS
                b.box((x, front - 0.045, zwin), (win_w, 0.08, win_h), glass_mat)
                b.box((x - win_w * 0.53, front - 0.09, zwin), (0.055, 0.10, win_h + 0.08), BROKEN)
                b.box((x + win_w * 0.53, front - 0.09, zwin), (0.055, 0.10, win_h + 0.08), BROKEN)
                b.box((x, front - 0.09, zwin - win_h * 0.53), (win_w + 0.09, 0.10, 0.055), CONCRETE)
                if (floor + col) % 6 == 1:
                    b.rod((x - win_w * 0.35, front - 0.10, zwin - 0.32),
                          (x + win_w * 0.27, front - 0.10, zwin + 0.27), 0.025, BROKEN, 6)
            else:
                # Empty bays read as collapsed window openings rather than clean glass.
                b.box((x, front - 0.055, zwin), (win_w, 0.08, win_h), SHADOW)
                b.box((x, front - 0.105, zwin - win_h * 0.53), (win_w + 0.03, 0.16, 0.07), BROKEN)
                b.box((x - win_w * 0.48, front - 0.10, zwin), (0.09, 0.17, win_h), CONCRETE)
                b.box((x + win_w * 0.44, front - 0.10, zwin + 0.13), (0.08, 0.17, win_h * 0.72), BROKEN)

        # Side windows with sill, lintel, and piers so the side elevations are finished too.
        for side in (-1, 1):
            xside = side * (width * 0.5 + 0.035)
            b.box((side * (width * 0.5 + 0.015), 0, zbase + 0.10), (0.14, depth + 0.16, 0.19), CONCRETE)
            for ybar in (-depth * 0.5 + 0.10, 0.0, depth * 0.5 - 0.10):
                b.box((xside, ybar, zbase + floor_h * 0.55), (0.18, 0.12, floor_h * 0.82), BROKEN if (floor + style) % 5 == 0 else CONCRETE)
            for bay_y in (-depth * 0.26, depth * 0.26):
                if (floor + int(bay_y > 0) + style) % 4 != 0:
                    b.box((side * (width * 0.5 + 0.06), bay_y, zwin), (0.08, depth * 0.31, floor_h * 0.49), GLASS)
                    b.box((side * (width * 0.5 + 0.11), bay_y - depth * 0.17, zwin), (0.12, 0.055, floor_h * 0.53), BROKEN)
                    b.box((side * (width * 0.5 + 0.11), bay_y + depth * 0.17, zwin), (0.12, 0.055, floor_h * 0.53), BROKEN)
                    b.box((side * (width * 0.5 + 0.11), bay_y, zwin - floor_h * 0.25), (0.12, depth * 0.37, 0.06), CONCRETE)
                else:
                    b.box((side * (width * 0.5 + 0.06), bay_y, zwin), (0.08, depth * 0.31, floor_h * 0.49), SHADOW)

        # Rear facade uses a different damage pattern, but receives the same floor structure.
        rear = depth * 0.5 + 0.035
        b.box((0, rear + 0.015, zbase + 0.10), (width + 0.20, 0.14, 0.19), BROKEN if floor % 3 == 2 else CONCRETE)
        for col in range(columns + 1):
            x = -width * 0.5 + 0.12 + col * bay
            b.box((x, rear + 0.02, zbase + floor_h * 0.55), (0.14, 0.19, floor_h * 0.82), BROKEN if (floor + col + 1) % 4 == 0 else CONCRETE)
        for col in range(columns):
            x = -width * 0.5 + bay * (col + 0.5) + 0.23
            win_w, win_h = bay * 0.62, floor_h * 0.50
            missing = ((floor * 5 + col * 2 + style + 2) % (6 if style == 2 else 8) == 0)
            pane = SHADOW if missing else (GLASS_HAZE if (floor + col) % 3 == 0 else GLASS)
            b.box((x, rear + 0.045, zwin), (win_w, 0.08, win_h), pane)
            for edge in (-1, 1):
                b.box((x + edge * win_w * 0.53, rear + 0.10, zwin), (0.055, 0.12, win_h + 0.08), BROKEN if missing else CONCRETE)
            b.box((x, rear + 0.10, zwin - win_h * 0.53), (win_w + 0.09, 0.12, 0.06), BROKEN if missing else CONCRETE)
            if missing:
                b.rod((x - win_w * 0.3, rear + 0.13, zwin - 0.2),
                      (x + win_w * 0.24, rear + 0.13, zwin + 0.24), 0.020, RUST, 6)

        # Exposed drain pipes continue around the rear corners; industrial blocks get ducting.
        for side in (-1, 1):
            pipe_x = side * (width * 0.5 + 0.16)
            b.rod((pipe_x, rear - 0.10, 0.24), (pipe_x + side * 0.08, rear - 0.10, height * 0.90), 0.035, METAL, 10)
            b.rod((pipe_x + side * 0.08, front + 0.10, 0.24), (pipe_x, front + 0.10, height * 0.76), 0.030, RUST, 8)
        if style == 3:
            for floor_vent in range(1, stories):
                vent_z = floor_vent * floor_h + floor_h * 0.44
                b.box((width * 0.5 + 0.10, 0.42, vent_z), (0.24, 0.72, 0.48), METAL)
                for slat in range(3):
                    b.box((width * 0.5 + 0.24, 0.42, vent_z - 0.12 + slat * 0.12), (0.07, 0.58, 0.035), BROKEN)
        if floor == 0:
            b.box((0.05, rear + 0.10, 0.85), (0.82, 0.12, 1.52), SHADOW)
            b.box((0.05, rear + 0.18, 0.85), (0.64, 0.06, 1.30), CONCRETE)
        if style == 1 and floor % 2 == 1:
            b.box((0.25, front - 0.23, zbase + 0.28), (width * 0.68, 0.42, 0.10), BROKEN)
            for x in (-width * 0.26, width * 0.26):
                b.rod((x, front - 0.40, zbase + 0.30), (x, front - 0.40, zbase + 0.88), 0.025, METAL, 8)
        if floor > 0 and (floor + style) % 3 == 1:
            # Bent rebar and a hanging slab break the repeated floor rhythm.
            x = -width * 0.43 if floor % 2 else width * 0.39
            b.box((x, front - 0.10, zbase + floor_h * 0.35), (0.62, 0.16, 0.48), BROKEN, angle=0.10 * (floor % 2 - 0.5))
            for offset in (-0.16, 0.08, 0.25):
                b.rod((x + offset, front - 0.20, zbase + floor_h * 0.38),
                      (x + offset + 0.10, front - 0.18, zbase + floor_h * 0.82), 0.018, RUST, 6)

    # Broken crown, roof equipment, and loose chunks anchor each tower as a ruin.
    b.box((-width * 0.27, 0.06, height + 0.13), (width * 0.38, depth * 0.42, 0.26), BROKEN, angle=0.04)
    b.box((width * 0.26, 0.12, height + 0.22), (width * 0.30, depth * 0.34, 0.42), CONCRETE, angle=-0.06)
    b.box((0.10, 0.10, height + 0.50), (width * 0.20, depth * 0.22, 0.55), SHADOW)
    if style == 0:
        b.cylinder((0.65, 0.30, height + 0.74), 0.38, 0.46, METAL, "z", 20)
        b.rod((0.68, 0.30, height + 0.95), (0.73, 0.30, height + 2.2), 0.055, METAL)
        b.rod((0.73, 0.30, height + 1.7), (1.25, 0.30, height + 2.05), 0.03, RUST)
    elif style == 2:
        b.rod((0, 0.1, height + 0.4), (0.12, 0.1, height + 3.2), 0.045, METAL)
        for z in (height + 1.2, height + 2.0, height + 2.8):
            b.rod((-0.45, 0.1, z), (0.58, 0.1, z), 0.024, METAL)
    else:
        for i in range(5):
            x = -width * 0.34 + i * width * 0.17
            b.box((x, depth * 0.18, height + 0.22), (0.16, 0.38, 0.43), METAL)

    # Irregular broken slabs and rebar at the base create a debris field.
    for i in range(6):
        x = -width * 0.46 + i * width * 0.18
        y = -depth * 0.59 - (0.16 if i % 2 else 0.0)
        points = [(x - 0.32, y - 0.18), (x + 0.19, y - 0.22),
                  (x + 0.34, y + 0.08), (x + 0.02, y + 0.26),
                  (x - 0.24, y + 0.12)]
        b.wedge(points, 0.0, 0.16 + (i % 3) * 0.06, BROKEN if i % 2 else CONCRETE)
    b.rod((-width * 0.42, -depth * 0.75, 0.28), (width * 0.28, -depth * 0.78, 0.42), 0.023, METAL, 8)
    obj = b.finish(display_location)
    ASSETS.append(obj)
    BUILDINGS.append(obj)
    return obj


def make_house(name, style, display_location):
    b = AssetBuilder(name)
    specs = [
        (5.2, 4.4, 5.3, BRICK),
        (4.7, 4.5, 4.8, CONCRETE),
        (3.9, 3.5, 3.3, RUST),
        (5.6, 4.6, 3.9, TEAL),
        (5.4, 4.7, 5.6, CONCRETE),
    ]
    width, depth, height, wall_mat = specs[style]
    front = -depth * 0.5 - 0.05
    b.box((0, 0, height * 0.5), (width, depth, height), wall_mat)
    b.box((0, 0, 0.16), (width + 0.28, depth + 0.22, 0.30), BROKEN)

    # Different openings and trim establish a distinct facade for each house.
    if style == 3:
        front_windows = [(-1.50, 1.65, 1.22, 1.20), (0.85, 1.67, 1.18, 1.32)]
    else:
        high_z = height - (1.45 if style in (0, 1, 4) else 1.30)
        front_windows = [(-width * 0.25, 1.95, 0.92, 1.05),
                         ( width * 0.25, 1.92, 0.92, 1.02)]
        if style in (0, 1, 4):
            front_windows.extend([(-width * 0.24, high_z, 0.84, 0.93),
                                  ( width * 0.24, high_z, 0.84, 0.93)])
    for index, (x, z, win_w, win_h) in enumerate(front_windows):
        broken = (style + index) % 4 == 1
        b.box((x, front - 0.055, z), (win_w, 0.07, win_h), SHADOW if broken else GLASS_HAZE)
        for edge in (-1, 1):
            b.box((x + edge * (win_w * 0.56), front - 0.10, z), (0.075, 0.12, win_h + 0.13), BROKEN)
        b.box((x, front - 0.10, z - win_h * 0.55), (win_w + 0.14, 0.12, 0.09), CONCRETE)
        if broken:
            b.rod((x - win_w * 0.30, front - 0.14, z - 0.28),
                  (x + win_w * 0.25, front - 0.14, z + 0.28), 0.022, RUST, 6)

    # Front door, doorstep, and a small rusted handle.
    door_x = -width * 0.06 if style == 3 else 0.0
    b.box((door_x, front - 0.075, 0.98), (0.78, 0.11, 1.78), RUST if style != 2 else METAL)
    b.box((door_x, front - 0.14, 1.02), (0.56, 0.045, 1.52), SHADOW if style == 2 else RED_RUST)
    b.cylinder((door_x + 0.22, front - 0.18, 0.94), 0.045, 0.035, METAL, "y", 10)
    b.box((door_x, front - 0.22, 0.12), (1.04, 0.46, 0.24), BROKEN)

    # Side and rear elevations receive windows, sills, utility lines, and damage too.
    rear = depth * 0.5 + 0.05
    for side in (-1, 1):
        xwall = side * (width * 0.5 + 0.04)
        b.rod((xwall, -depth*0.44, 0.28), (xwall + side*0.07, -depth*0.44, height*0.83), 0.026, METAL, 8)
        for y in (-depth * 0.22, depth * 0.22):
            z = 1.82 if style in (2, 3) else height * 0.60
            b.box((xwall, y, z), (0.09, depth * 0.24, 0.76), GLASS if (style + side) % 3 else SHADOW)
            b.box((side * (width*0.5+0.10), y, z-0.42), (0.16, depth*0.30, 0.08), BROKEN)
        if style == 2:
            for strip in range(5):
                b.box((xwall + side*0.055, -depth*0.38 + strip*0.34, height*0.48),
                      (0.08, 0.035, height*0.78), METAL if strip % 2 else RUST)
        else:
            b.box((xwall + side*0.02, 0, 0.16), (0.14, depth + 0.15, 0.18), CONCRETE)
    for x in (-width * 0.23, width * 0.23):
        b.box((x, rear + 0.06, height * 0.53), (0.86, 0.08, 0.92), GLASS if x < 0 else GLASS_HAZE)
        b.box((x, rear + 0.12, height * 0.53 - 0.51), (0.98, 0.12, 0.08), BROKEN)
    b.box((width * 0.30, rear + 0.06, 0.93), (0.66, 0.11, 1.62), RUST)

    if style == 0:  # brick row house with stepped parapet
        b.box((0, 0, height + 0.12), (width + 0.12, depth + 0.10, 0.24), BROKEN)
        b.box((-width * 0.30, front - 0.13, height * 0.52), (0.12, 0.20, height * 0.86), CONCRETE)
        for floor_z in (1.25, 2.55, 3.85):
            for x in (-width*0.43, -width*0.28, width*0.15, width*0.34, width*0.46):
                b.box((x, front - 0.10, floor_z), (0.17, 0.045, 0.07), BROKEN)
        b.box((width*0.25, 0.10, height+0.58), (0.72, 0.72, 0.92), RUST)
    elif style == 1:  # concrete duplex with a fractured balcony
        b.box((0, 0, height + 0.09), (width + 0.10, depth + 0.08, 0.18), CONCRETE)
        b.box((-0.10, front - 0.38, 2.86), (2.40, 0.72, 0.14), BROKEN, angle=-0.04)
        for x in (-1.10, -0.35, 0.40, 1.10):
            b.rod((x, front - 0.70, 2.92), (x+0.03, front - 0.70, 3.55), 0.025, METAL, 8)
        b.cylinder((1.30, 0.55, height + 0.46), 0.42, 0.48, METAL, "z", 18)
    elif style == 2:  # patchwork settlement shack with a sagging corrugated roof
        b.box((0, 0, height + 0.08), (width + 0.48, depth + 0.40, 0.18), METAL, pitch=math.radians(5))
        for i in range(9):
            x = -width * 0.48 + i * width * 0.12
            b.rod((x,-depth*0.62,height+0.06),(x,depth*0.62,height+0.16),0.022,RUST if i%3==0 else METAL,6)
        b.box((0.62, front - 0.20, 2.55), (1.45, 0.32, 0.12), MUSTARD, angle=0.06)
        b.box((-1.18, front - 0.12, 0.82), (0.22, 0.10, 1.50), BROKEN, angle=-0.14)
    elif style == 3:  # ruined corner shop with a torn awning and shutter
        b.box((0, front - 0.32, 2.76), (width + 0.55, 0.88, 0.16), WARNING, angle=-0.025)
        b.box((0, front - 0.44, 3.36), (width * 0.72, 0.14, 0.53), SHADOW)
        b.box((0, front - 0.54, 3.36), (width * 0.65, 0.05, 0.39), TEAL)
        for i in range(8):
            x = -width * 0.30 + i * width * 0.085
            b.box((x, front - 0.12, 1.60), (0.055, 0.10, 1.74), METAL)
        b.rod((width*0.28, front-0.12, 3.55),(width*0.42, front-0.18, 2.62),0.035,RUST,8)
    else:  # suburban house with a gable roof, chimney, and broken roof tiles
        b.gable_roof((0, 0, height - 0.10), width + 0.42, depth + 0.42, 0.0, 1.35, RUST)
        for side in (-1, 1):
            for row in range(1, 5):
                y = side * (depth * 0.10 + row * 0.36)
                z = height - 0.10 + 1.35 * (1.0 - abs(y) / ((depth + 0.42) * 0.5))
                b.rod((-width*0.47,y,z),(width*0.47,y,z),0.022,RED_RUST if row%2 else BROKEN,8)
        b.box((width*0.28, 0.20, height+1.12), (0.52, 0.62, 1.20), CONCRETE)
        b.box((-width*0.20, front-0.16, 1.20), (1.45, 0.15, 2.10), SHADOW)
        b.box((-width*0.20, front-0.25, 1.20), (1.20, 0.06, 1.84), METAL)
        b.rod((width*0.46, front-0.12, 2.0),(width*0.52, front-0.35, 2.6),0.026,RUST,6)

    # Low rubble and a short broken fence give each plot a lived-in, abandoned scale.
    for i in range(4):
        x = -width * 0.36 + i * width * 0.22
        b.wedge([(x-0.22,-depth*0.63),(x+0.17,-depth*0.66),(x+0.28,-depth*0.46),(x-0.12,-depth*0.43)],
                0.0, 0.14 + (i%2)*0.08, BROKEN if i%2 else CONCRETE)
    for x in (-width*0.46, -width*0.18):
        b.rod((x,-depth*0.72,0.18),(x+0.04,-depth*0.72,0.72),0.026,METAL,8)
    obj = b.finish(display_location)
    ASSETS.append(obj)
    HOUSES.append(obj)
    return obj


def make_vehicle(name, body_mat, roof_mat, style, display_location):
    b = AssetBuilder(name)
    if style == 0:  # high-roof van
        b.rounded_body([(-2.70,0.34,0.16,0.82),(-2.48,0.82,0.76,1.16),(-2.10,0.96,0.84,1.20),
                        (-0.70,0.97,0.86,1.21),(1.30,0.96,0.84,1.19),(2.18,0.88,0.77,1.13),
                        (2.62,0.55,0.30,0.88),(2.70,0.12,0.08,0.74)], body_mat)
        b.box((-2.52, 0, 0.71), (0.18, 1.66, 0.20), METAL)
        b.box((2.42, 0, 0.72), (0.20, 1.68, 0.18), RUST, angle=-0.08)
        b.box((0.42, 0, 1.98), (1.18, 1.60, 0.12), BROKEN, angle=0.04)
        for side in (-1, 1):
            for i, x in enumerate((-1.75, -0.63, 0.50, 1.60)):
                pane = SHADOW if (i == 2 and side == 1) else GLASS
                b.box((x, side * 0.985, 1.46), (0.86, 0.045, 0.55), pane)
                if pane == SHADOW:
                    b.rod((x-0.28, side*1.02, 1.22),(x+0.22, side*1.02,1.63),0.022,BROKEN,6)
            b.box((0.98, side * 0.99, 0.78), (0.92, 0.05, 0.18), RUST, angle=0.04 * side)
        b.box((-2.715, 0, 1.43), (0.05, 1.40, 0.53), GLASS_HAZE, pitch=math.radians(13))
        b.box((2.56, 0, 1.38), (0.05, 1.26, 0.42), SHADOW, pitch=math.radians(-8))
        wheel_x, wheel_r, wheel_y = (-1.78, 1.63), 0.43, 0.97
        chassis_len = 4.85
    elif style == 1:  # long-bed cargo truck
        b.box((0.0, 0, 0.53), (6.55, 1.82, 0.28), SHADOW)
        b.rounded_body([(-3.16,0.30,0.12,1.24),(-2.98,0.70,0.62,1.32),(-2.66,0.91,0.80,1.36),
                        (-1.62,0.91,0.79,1.35),(-1.12,0.84,0.66,1.26),(-0.82,0.50,0.18,1.02)], body_mat)
        # Open cargo bed with dented panels, exposed ribs, and scattered wreckage.
        b.box((1.18, 0, 0.78), (3.42, 1.92, 0.18), METAL)
        for side in (-1, 1):
            b.box((1.20, side * 0.91, 1.32), (3.42, 0.16, 1.10), roof_mat, angle=0.018 * side)
            for x in (-0.40, 0.48, 1.40, 2.28):
                b.box((x, side * 1.015, 1.30), (0.10, 0.07, 1.02), BROKEN)
        b.box((2.87, 0, 1.28), (0.14, 1.82, 1.00), RUST, angle=-0.08)
        for x, z, angle in ((0.18,1.30,0.12),(1.10,1.46,-0.18),(2.0,1.27,0.10)):
            b.box((x, 0, z), (0.90, 1.10, 0.32), BROKEN, angle=angle)
        b.box((-3.06, 0, 1.73), (0.06, 1.48, 0.68), GLASS, pitch=math.radians(16))
        for side in (-1, 1):
            b.box((-2.04, side * 0.93, 1.73), (0.70, 0.045, 0.54), GLASS_HAZE)
            b.rod((-2.68, side*0.95, 1.42), (-1.76, side*0.95, 1.96), 0.024, BROKEN, 6)
        b.box((-3.20, 0, 0.80), (0.16, 1.90, 0.24), RUST, angle=0.07)
        b.box((-3.22, 0, 1.08), (0.08, 1.22, 0.34), SHADOW)
        wheel_x, wheel_r, wheel_y = (-2.18, 0.42, 2.43), 0.48, 1.01
        chassis_len = 6.20
    else:  # damaged city bus
        b.rounded_body([(-4.12,0.30,0.16,1.55),(-3.92,0.85,0.90,1.62),(-3.42,1.11,1.12,1.64),
                        (-1.30,1.14,1.13,1.64),(1.80,1.14,1.12,1.63),(3.35,1.08,1.05,1.59),
                        (3.92,0.88,0.86,1.52),(4.10,0.36,0.16,1.30)], body_mat)
        # Long side windows and door are individually framed; several have collapsed inward.
        for side in (-1, 1):
            xs = (-3.12,-2.30,-1.48,-0.66,0.16,0.98,1.80,2.62)
            for i, x in enumerate(xs):
                pane = SHADOW if (i in (2, 6) and side == 1) or (i == 4 and side == -1) else GLASS
                b.box((x, side * 1.145, 2.03), (0.66, 0.045, 0.78), pane)
                b.box((x-0.36, side*1.17, 2.03), (0.055,0.09,0.86), BROKEN)
                b.box((x+0.36, side*1.17, 2.03), (0.055,0.09,0.86), CONCRETE)
                if pane == SHADOW:
                    b.rod((x-0.22,side*1.20,1.72),(x+0.21,side*1.20,2.33),0.022,RUST,6)
            b.box((0.55, side * 1.17, 0.92), (1.14, 0.09, 0.18), RUST)
        b.box((-4.05, 0, 2.07), (0.05, 1.72, 0.88), GLASS_HAZE, pitch=math.radians(12))
        b.box((-4.14, 0, 1.10), (0.18, 1.95, 0.26), METAL, angle=0.04)
        b.box((0.10, 0, 2.77), (0.76, 1.08, 0.28), METAL)
        b.box((1.46, 0, 0.78), (1.08, 2.10, 0.58), SHADOW)
        wheel_x, wheel_r, wheel_y = (-2.72, 2.55), 0.48, 1.10
        chassis_len = 7.55

    # Shared underbody, broken lamps, and vehicle-specific axles.
    b.box((0, 0, 0.34), (chassis_len, 1.62, 0.16), SHADOW)
    for x in wheel_x:
        for side in (-1, 1):
            b.cylinder((x, side * wheel_y, wheel_r), wheel_r, 0.22, RUBBER, "y", 24)
            b.cylinder((x, side * (wheel_y + 0.13), wheel_r), wheel_r * 0.46, 0.04, METAL, "y", 16)
    if style == 0:
        for side in (-1, 1):
            b.box((-2.72, side * 0.80, 0.63), (0.12, 0.24, 0.20), IVORY)
            b.box((2.62, side * 0.78, 0.62), (0.12, 0.22, 0.18), RED_RUST)
    elif style == 1:
        for x in (0.42, 2.43):
            for side in (-1, 1):
                b.cylinder((x-0.20, side*1.02, 0.48), 0.42, 0.18, RUBBER, "y", 20)
                b.cylinder((x-0.20, side*1.15, 0.48), 0.18, 0.035, METAL, "y", 14)
        b.rod((2.95, 0, 1.90),(3.12,0,2.82),0.055,RUST,8)
    else:
        for i in range(7):
            b.box((-3.20 + i * 0.95, -1.17, 0.88), (0.16, 0.035, 0.16), RED_RUST if i % 2 else IVORY)
        b.rod((-3.45, 1.20, 2.75), (-2.55, 1.20, 2.76), 0.025, RUST, 8)
    obj = b.finish(display_location)
    ASSETS.append(obj)
    VEHICLES.append(obj)
    return obj


def make_motorcycle(name, style, paint, display_location):
    b = AssetBuilder(name)
    if style == 0:  # rusted scrambler with luggage rack
        rear_x, front_x, wheel_r, wheel_y = -0.82, 0.84, 0.42, 0.13
        b.rod((rear_x,0,0.53),(-0.08,0,0.86),0.045,METAL,8)
        b.rod((-0.08,0,0.86),(0.50,0,1.15),0.045,RUST,8)
        b.rod((rear_x,0,0.53),(0.46,0,0.64),0.04,METAL,8)
        b.rounded_body([(-0.48,0.19,0.13,0.98),(-0.28,0.31,0.25,1.02),(0.02,0.34,0.26,1.03),(0.25,0.20,0.16,0.98)],paint)
        b.rounded_body([(-0.88,0.08,0.06,1.17),(-0.68,0.23,0.14,1.18),(-0.32,0.22,0.13,1.18),(-0.18,0.08,0.06,1.17)],RUBBER)
        b.rod((0.51,0,1.12),(front_x,0,wheel_r),0.055,METAL,10)
        b.rod((0.40,0,0.75),(front_x,0,wheel_r),0.036,BLUE,8)
        b.rod((0.47,0,1.18),(0.27,0,1.43),0.035,METAL,8)
        b.rod((0.16,-0.30,1.44),(0.16,0.30,1.44),0.035,RUST,8)
        b.rod((rear_x-0.12,0,0.92),(rear_x-0.82,0,0.92),0.028,METAL,8)
        b.rod((rear_x-0.82,0,0.92),(rear_x-0.86,0,0.62),0.028,METAL,8)
        b.cylinder((0.52,-0.10,1.28),0.13,0.06,IVORY,"y",16)
        b.rod((-0.18,0,0.68),(-0.60,0,0.61),0.05,RUST,8)
    elif style == 1:  # small abandoned step-through scooter
        rear_x, front_x, wheel_r, wheel_y = -0.63, 0.66, 0.31, 0.12
        b.rounded_body([(-0.62,0.14,0.11,0.74),(-0.46,0.31,0.37,0.84),(-0.18,0.38,0.45,0.87),
                        (0.20,0.36,0.42,0.85),(0.48,0.29,0.31,0.83),(0.65,0.12,0.12,0.77)],paint)
        b.rod((rear_x,0,0.42),(-0.14,0,0.73),0.036,METAL,8)
        b.rod((-0.14,0,0.73),(0.43,0,0.90),0.036,METAL,8)
        b.box((0.12,0,0.48),(0.86,0.42,0.14),METAL)
        b.box((-0.28,0,1.22),(0.72,0.50,0.16),RUBBER,angle=-0.06)
        b.rod((0.42,0,0.85),(front_x,0,wheel_r),0.045,METAL,8)
        b.rod((0.47,0,1.06),(0.40,0,1.39),0.032,METAL,8)
        b.rod((0.13,-0.28,1.40),(0.13,0.28,1.40),0.035,METAL,8)
        b.box((0.58,0,1.03),(0.32,0.44,0.32),RUST,angle=0.12)
        b.cylinder((0.75,-0.24,0.96),0.11,0.05,GLASS_HAZE,"y",14)
        b.box((-0.74,0,0.45),(0.12,0.56,0.20),BROKEN,angle=-0.10)
    else:  # exposed-frame dirt bike with tall suspension and knobby rubber
        rear_x, front_x, wheel_r, wheel_y = -0.86, 0.91, 0.49, 0.16
        b.rod((rear_x,0,0.60),(-0.12,0,0.91),0.055,METAL,8)
        b.rod((-0.12,0,0.91),(0.50,0,1.25),0.055,RUST,8)
        b.rod((rear_x,0,0.60),(0.37,0,0.66),0.045,METAL,8)
        b.rounded_body([(-0.43,0.16,0.09,1.00),(-0.24,0.29,0.24,1.08),(0.12,0.31,0.23,1.10),(0.39,0.15,0.10,1.03)],paint)
        b.rounded_body([(-0.90,0.05,0.05,1.34),(-0.68,0.22,0.12,1.35),(-0.29,0.20,0.10,1.34),(-0.12,0.06,0.04,1.33)],RUBBER)
        b.rod((0.50,0,1.23),(front_x,0,wheel_r),0.060,METAL,10)
        b.rod((0.35,0,0.70),(front_x,0,wheel_r),0.045,BLUE,8)
        b.rod((-0.48,0,0.62),(-0.06,0,0.97),0.036,CONCRETE,8)
        b.rod((0.48,0,1.27),(0.29,0,1.57),0.04,METAL,8)
        b.rod((0.08,-0.38,1.58),(0.08,0.38,1.58),0.04,RUST,8)
        b.box((front_x+0.12,0,0.93),(0.66,0.46,0.13),BLUE,angle=0.08)
        b.box((rear_x-0.08,0,1.09),(0.82,0.36,0.12),RED_RUST,angle=-0.06)
        b.cylinder((0.56,-0.12,1.36),0.12,0.05,GLASS_HAZE,"y",14)

    for x in (rear_x, front_x):
        b.cylinder((x,0,wheel_r),wheel_r,0.20,RUBBER,"y",24)
        b.cylinder((x,-0.12,wheel_r),wheel_r*0.31,0.04,METAL,"y",16)
        b.cylinder((x,0.12,wheel_r),wheel_r*0.31,0.04,METAL,"y",16)
        for side in (-1,1):
            spoke_y = side * 0.145
            for spoke in range(8):
                angle = spoke * math.pi / 4
                rim = (x+wheel_r*0.86*math.cos(angle),spoke_y,wheel_r+wheel_r*0.86*math.sin(angle))
                b.rod((x,spoke_y,wheel_r),rim,0.009,METAL,6)
    obj = b.finish(display_location)
    ASSETS.append(obj)
    MOTORCYCLES.append(obj)
    return obj


def make_prop(name, kind, display_location):
    b = AssetBuilder(name)
    if kind == "barrier":
        b.box((0, 0, 0.54), (2.35, 0.56, 1.04), CONCRETE)
        b.box((-0.38, -0.30, 0.55), (0.48, 0.035, 0.82), WARNING, angle=-0.20)
        b.box((0.40, -0.30, 0.55), (0.48, 0.035, 0.82), WARNING, angle=0.20)
        b.box((0, 0, 1.12), (2.52, 0.68, 0.12), BROKEN, angle=0.015)
    elif kind == "lamp":
        b.box((0, 0, 0.10), (0.70, 0.70, 0.20), CONCRETE)
        b.cylinder((0, 0, 1.75), 0.095, 3.2, METAL, "z", 14)
        b.rod((0, 0, 3.28), (0.35, 0, 3.65), 0.075, RUST, 10)
        b.box((0.64, 0, 3.60), (0.68, 0.35, 0.19), METAL, angle=-0.15)
        b.box((0.72, -0.015, 3.48), (0.44, 0.28, 0.08), IVORY, angle=-0.15)
        b.rod((0, 0, 2.90), (0.28, 0, 3.23), 0.035, METAL, 8)
    elif kind == "drums":
        for x, y, z, radius in ((-0.43, 0, 0.60, 0.39), (0.40, 0.05, 0.59, 0.36), (0.05, 0.03, 1.36, 0.34)):
            b.cylinder((x, y, z), radius, 1.12 if z < 1 else 0.94, RUST if x < 0.2 else MUSTARD, "z", 20)
            for band_z in (z - 0.34, z + 0.34):
                b.cylinder((x, y, band_z), radius * 1.025, 0.055, METAL, "z", 20)
            b.cylinder((x + radius * 0.38, y - radius * 0.46, z + 0.57), radius * 0.12, 0.025, METAL, "z", 12)
    elif kind == "sign":
        b.cylinder((0, 0.12, 1.38), 0.055, 2.7, METAL, "z", 12)
        b.box((0.08, -0.03, 2.52), (1.22, 0.13, 0.84), RUST, angle=-0.12)
        b.box((0.08, -0.11, 2.52), (1.08, 0.045, 0.70), CONCRETE, angle=-0.12)
        b.rod((-0.30, -0.145, 2.34), (0.42, -0.145, 2.68), 0.025, BROKEN, 6)
    elif kind == "traffic":
        b.box((0, 0, 0.10), (0.72, 0.72, 0.20), CONCRETE)
        b.rod((-0.14, 0, 0.18), (-0.28, 0, 1.78), 0.075, METAL, 12)
        b.rod((-0.28, 0, 1.78), (0.72, 0, 2.32), 0.065, RUST, 10)
        b.box((0.90, -0.02, 2.05), (0.48, 0.40, 1.22), SHADOW, angle=-0.06)
        for z, color in ((1.70, SIGNAL_RED), (2.05, SIGNAL_AMBER), (2.40, SIGNAL_GREEN)):
            b.cylinder((0.90, -0.245, z), 0.125, 0.06, METAL, "y", 16)
            b.cylinder((0.90, -0.282, z), 0.082, 0.025, color, "y", 16)
        b.rod((0.42, 0.03, 2.21), (0.48, 0.03, 1.28), 0.024, RUST, 8)
    elif kind == "utility":
        b.wedge([(-0.35,-0.32),(0.30,-0.30),(0.34,0.30),(-0.22,0.35)], 0, 0.28, CONCRETE)
        b.rod((0, 0, 0.20), (-0.12, 0.02, 2.72), 0.115, RUST, 12)
        b.rod((-0.12, 0.02, 2.72), (-0.32, 0.04, 3.72), 0.10, CONCRETE, 12)
        b.rod((-0.56, 0.04, 3.42), (0.18, 0.04, 3.56), 0.065, METAL, 10)
        for x in (-0.44, -0.08, 0.12):
            b.cylinder((x, 0.04, 3.72), 0.075, 0.36, IVORY, "z", 12)
        b.rod((-0.46, 0.02, 3.72), (0.20, 0.02, 2.18), 0.018, METAL, 8)
        b.rod((0.12, 0.07, 3.72), (1.25, 0.14, 2.45), 0.016, METAL, 8)
        b.rod((-0.52, 0.09, 3.72), (-1.00, -0.20, 3.10), 0.014, RUST, 8)
    elif kind == "generator":
        b.box((0, 0, 0.68), (1.55, 0.92, 1.12), METAL)
        b.box((-0.04, -0.48, 0.72), (1.28, 0.08, 0.76), CONCRETE)
        b.box((-0.04, -0.54, 0.72), (0.82, 0.035, 0.50), SHADOW)
        for slat in range(5):
            b.box((-0.04, -0.565, 0.51 + slat * 0.105), (0.72, 0.025, 0.032), BROKEN)
        b.box((0.48, -0.55, 0.91), (0.23, 0.045, 0.20), RUST)
        for x in (-0.55, 0.54):
            for y in (-0.34, 0.34):
                b.cylinder((x, y, 0.19), 0.15, 0.11, RUBBER, "y", 14)
        b.rod((-0.48, 0.12, 1.18), (-0.62, 0.12, 1.62), 0.045, RUST, 8)
        b.box((0.69, 0.02, 0.44), (0.20, 0.22, 0.42), BROKEN, angle=-0.14)
    elif kind == "sandbags":
        rows = [(-0.38, 0.22, -0.30), (0.0, 0.22, -0.30), (0.38, 0.22, -0.30),
                (-0.18, 0.46, 0.02), (0.20, 0.46, 0.02), (0.0, 0.70, 0.34)]
        for index, (x, z, y) in enumerate(rows):
            b.rounded_body([(-0.42,0.04,0.06,z),(-0.31,0.19,0.14,z),(0.20,0.20,0.14,z),
                            (0.38,0.12,0.11,z),(0.43,0.03,0.05,z)],
                           CONCRETE if index % 3 else BROKEN, radial_segments=20, steps=3,
                           center_x=x, center_y=y)
            # Flat irregular seams make each filled sack read separately in silhouette.
            b.rod((-0.20, y - 0.16, z + 0.02), (0.20, y - 0.16, z + 0.02), 0.010, BROKEN, 6)
    elif kind == "busstop":
        b.box((0, 0, 0.12), (2.80, 1.18, 0.24), CONCRETE)
        b.box((0, 0.04, 2.45), (2.95, 1.38, 0.20), RUST, angle=-0.035)
        for x in (-1.22, 1.22):
            b.rod((x, 0.38, 0.22), (x, 0.38, 2.42), 0.075, METAL, 10)
            b.box((x, -0.13, 1.45), (0.08, 0.055, 1.36), GLASS_HAZE)
        b.box((0, 0.34, 1.00), (2.35, 0.36, 0.12), METAL)
        b.box((0, 0.50, 2.38), (1.80, 0.06, 0.42), SHADOW)
        b.box((-0.28, 0.50, 2.38), (0.06, 0.10, 0.48), BROKEN)
        b.rod((-0.65, -0.18, 1.22), (0.50, -0.18, 1.68), 0.025, BROKEN, 6)
        b.box((1.26, -0.02, 1.30), (0.38, 0.60, 0.72), BROKEN, angle=0.16)
    else:  # irregular rubble pile
        chunks = [(-0.58,0.00,0.22,0.78,0.60,0.42,-0.12), (0.12,0.05,0.26,0.72,0.72,0.50,0.10),
                  (0.62,-0.04,0.18,0.68,0.55,0.34,-0.22), (-0.18,0.08,0.58,0.54,0.46,0.62,0.24),
                  (0.42,0.06,0.55,0.46,0.42,0.53,-0.18)]
        for i, (x,y,z,sx,sy,sz,angle) in enumerate(chunks):
            b.box((x,y,z), (sx,sy,sz), BROKEN if i % 2 == 0 else CONCRETE, angle=angle)
        b.rod((-0.8,0.12,0.28),(0.75,-0.10,0.54),0.028,METAL,8)
        b.rod((-0.34,-0.2,0.42),(0.56,0.18,0.34),0.022,RUST,8)
    obj = b.finish(display_location)
    ASSETS.append(obj)
    PROPS.append(obj)
    return obj


# Seven distinct ruined skyline silhouettes, sized in Unity meters.
make_building("Apoc_Building_01_Residential_Ruin", 5.0, 4.6, 15.5, 7, 0, (-18.0, 0, 0))
make_building("Apoc_Building_02_Office_Glass_Ruin", 5.8, 4.3, 13.5, 6, 1, (-12.0, 0, 0))
make_building("Apoc_Building_03_Narrow_Signal_Tower", 4.4, 4.0, 17.0, 8, 2, (-5.5, 0, 0))
make_building("Apoc_Building_04_Industrial_Hospital_Ruin", 6.0, 4.9, 12.0, 6, 3, (0.5, 0, 0))
make_building("Apoc_Building_05_Shattered_Hotel_Midrise", 5.5, 4.6, 9.2, 4, 1, (7.0, 0, 0))
make_building("Apoc_Building_06_Ruined_Clinic_Lowrise", 4.8, 4.3, 6.8, 3, 0, (13.0, 0, 0))
make_building("Apoc_Building_07_Collapsed_Depot_Midrise", 6.2, 5.0, 10.5, 4, 3, (18.5, 0, 0))

# Five distinct residential and neighborhood structures.
make_house("Apoc_House_01_Brick_Rowhouse", 0, (-11.0, 8.0, 0))
make_house("Apoc_House_02_Concrete_Duplex", 1, (-5.5, 8.0, 0))
make_house("Apoc_House_03_Salvaged_Tin_Shack", 2, (0.0, 8.0, 0))
make_house("Apoc_House_04_Ruined_Corner_Shop", 3, (5.5, 8.0, 0))
make_house("Apoc_House_05_Collapsed_Suburban_Home", 4, (11.0, 8.0, 0))

# Three road vehicles with distinct proportions and damage.
make_vehicle("Apoc_Vehicle_01_Damaged_Van", MUSTARD, CONCRETE, 0, (-9.0, -7.5, 0))
make_vehicle("Apoc_Vehicle_02_Ruined_Truck", RED_RUST, METAL, 1, (0.0, -7.5, 0))
make_vehicle("Apoc_Vehicle_03_Abandoned_Bus", BLUE, RUST, 2, (9.0, -7.5, 0))

# Three abandoned motorcycles: scrambler, scooter, and dirt bike.
make_motorcycle("Apoc_Motorcycle_01_Rusted_Scrambler", 0, RED_RUST, (-4.5, -11.5, 0))
make_motorcycle("Apoc_Motorcycle_02_Smashed_Scooter", 1, TEAL, (0.0, -11.5, 0))
make_motorcycle("Apoc_Motorcycle_03_Abandoned_Dirt_Bike", 2, MUSTARD, (4.5, -11.5, 0))

# Five reusable street props for dressing the environment.
make_prop("Apoc_Prop_Concrete_Barricade", "barrier", (-11.25, -13.8, 0))
make_prop("Apoc_Prop_Broken_Streetlight", "lamp", (-8.75, -13.8, 0))
make_prop("Apoc_Prop_Rusty_Drum_Stack", "drums", (-6.25, -13.8, 0))
make_prop("Apoc_Prop_Damaged_Road_Sign", "sign", (-3.75, -13.8, 0))
make_prop("Apoc_Prop_Rubble_Pile", "rubble", (-1.25, -13.8, 0))
make_prop("Apoc_Prop_Downed_Traffic_Light", "traffic", (1.25, -13.8, 0))
make_prop("Apoc_Prop_Broken_Utility_Pole", "utility", (3.75, -13.8, 0))
make_prop("Apoc_Prop_Abandoned_Generator", "generator", (6.25, -13.8, 0))
make_prop("Apoc_Prop_Sandbag_Pile", "sandbags", (8.75, -13.8, 0))
make_prop("Apoc_Prop_Ruined_Bus_Stop", "busstop", (11.25, -13.8, 0))


# A clean preview shows the arrangement; the ground, lights, and camera are not exported.
floor_mat = make_material("Preview | muted ground", (0.085, 0.095, 0.10), 0.95)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, -5.5, -0.06))
floor = bpy.context.object
floor.name = "Preview_Ground"
floor.data.materials.append(floor_mat)

def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add(location=(17, -42, 25))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 49
point_at(camera, (0, -5.5, 8.1))
bpy.context.scene.camera = camera

def area_light(name, location, energy, size, target):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    point_at(light, target)

area_light("Key_Light", (3, -16, 29), 2800, 14, (0, -5, 5))
area_light("Fill_Light", (-20, -10, 14), 1400, 12, (0, -4, 7))
area_light("Rim_Light", (2, 12, 22), 2200, 10, (0, -3, 8))
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys() else "BLENDER_EEVEE"
scene.render.resolution_x = 1600
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.view_settings.view_transform = "Standard"
scene.world.color = (0.025, 0.03, 0.035)
scene.render.filepath = PREVIEW_PATH

bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

def show_only(objects):
    visible = set(objects)
    for asset in ASSETS:
        asset.hide_render = asset not in visible

# City overview, then a reverse view to show the completed rear elevations.
show_only(BUILDINGS + VEHICLES + PROPS)
bpy.ops.render.render(write_still=True)
camera.location = (-17, 42, 25)
point_at(camera, (0, -5.5, 8.1))
scene.render.filepath = BACK_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# Separate close views keep the smaller residential and vehicle assets legible.
show_only(HOUSES)
camera.location = (10, -16, 14)
camera.data.ortho_scale = 28
point_at(camera, (0, 8, 2.9))
scene.render.filepath = HOUSES_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

show_only(VEHICLES + MOTORCYCLES)
camera.location = (12, -31, 13)
camera.data.ortho_scale = 28
point_at(camera, (0, -9.5, 1.5))
scene.render.filepath = VEHICLES_PREVIEW_PATH
bpy.ops.render.render(write_still=True)

show_only(ASSETS)
camera.location = (17, -42, 25)
camera.data.ortho_scale = 49
point_at(camera, (0, -5.5, 8.1))
scene.render.filepath = PREVIEW_PATH

# Export one FBX per reusable prop/model, all with clean origin pivots.
for obj in ASSETS:
    original_location = obj.location.copy()
    obj.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    safe_name = obj.name.replace(" ", "_")
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, safe_name + ".fbx"),
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
    obj.location = original_location

print("Environment pack saved:", OUT_DIR)
print("Assets:", [obj.name for obj in ASSETS])
print("Counts: buildings", len(BUILDINGS), "houses", len(HOUSES), "vehicles", len(VEHICLES),
      "motorcycles", len(MOTORCYCLES), "props", len(PROPS))

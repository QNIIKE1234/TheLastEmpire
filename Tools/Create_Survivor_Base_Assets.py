import math
import os
import random
import sys
import bpy
from mathutils import Vector, Euler


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedBaseItems")
os.makedirs(OUT_DIR, exist_ok=True)
BLEND_PATH = os.path.join(OUT_DIR, "Survivor_Base_Items_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Survivor_Base_Items_Preview.png")
CATEGORY_PREVIEWS = {
    "Building": os.path.join(OUT_DIR, "Survivor_Building_Preview.png"),
    "Utility": os.path.join(OUT_DIR, "Survivor_Utility_Preview.png"),
    "Defense": os.path.join(OUT_DIR, "Survivor_Defense_Preview.png"),
}


if "--previews-only" in sys.argv:
    # Re-render the category sheets quickly from a previously generated source scene.
    bpy.ops.wm.open_mainfile(filepath=BLEND_PATH)
    scene = bpy.context.scene
    camera = scene.camera
    categories = {name: bpy.data.collections.get(name) for name in ("Building", "Utility", "Defense")}
    for collection in categories.values():
        if collection:
            collection.hide_render = False
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 55
    camera.location = (18, -40, 34)
    camera.rotation_euler = (Vector((0, -1.4, 0.55)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.resolution_x, scene.render.resolution_y = 2100, 1500
    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    for category, target_y in (("Building", 8.6), ("Utility", -1.4), ("Defense", -11.4)):
        for name, collection in categories.items():
            if collection:
                collection.hide_render = name != category
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = 25
        camera.location = (0, target_y - 15, 12)
        camera.rotation_euler = (Vector((0, target_y, 1.0)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.resolution_x, scene.render.resolution_y = 1800, 1200
        scene.render.filepath = CATEGORY_PREVIEWS[category]
        bpy.ops.render.render(write_still=True)
    for collection in categories.values():
        if collection:
            collection.hide_render = False
    camera.location = (18, -40, 34)
    camera.data.ortho_scale = 55
    camera.rotation_euler = (Vector((0, -1.4, 0.55)) - camera.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = camera
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("Category previews re-rendered from the saved scene.")
    sys.exit(0)

random.seed(20261108)


# Start with a clean scene. Each exported asset is one mesh with many material slots.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for data_list in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    for data in list(data_list):
        if data.users == 0:
            data_list.remove(data)


def make_material(name, color, roughness=0.78, metallic=0.0, emission=None, strength=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    shader = nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    if emission is not None:
        if "Emission Color" in shader.inputs:
            shader.inputs["Emission Color"].default_value = (*emission, 1.0)
        elif "Emission" in shader.inputs:
            shader.inputs["Emission"].default_value = (*emission, 1.0)
        if "Emission Strength" in shader.inputs:
            shader.inputs["Emission Strength"].default_value = strength
    # A very fine bump breaks up broad flat surfaces without hiding the hand-built silhouette.
    if name.startswith(("Wood", "Rust", "Concrete", "Stone", "Chitin", "Leather", "Cloth")):
        noise = nodes.new("ShaderNodeTexNoise")
        noise.inputs["Scale"].default_value = 7.0
        noise.inputs["Detail"].default_value = 3.0
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.12
        bump.inputs["Distance"].default_value = 0.035
        links.new(noise.outputs["Fac"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], shader.inputs["Normal"])
    return material


MAT = {}
MAT["wood"] = make_material("Wood | weathered cedar", (0.31, 0.17, 0.085))
MAT["wood_light"] = make_material("Wood | exposed grain", (0.52, 0.32, 0.16))
MAT["wood_edge"] = make_material("Wood | cut end", (0.69, 0.47, 0.25))
MAT["wood_dark"] = make_material("Wood | charred and stained", (0.12, 0.09, 0.07))
MAT["iron"] = make_material("Iron | dark forged steel", (0.12, 0.15, 0.17), 0.42, 0.78)
MAT["iron_light"] = make_material("Iron | scraped edges", (0.40, 0.43, 0.43), 0.34, 0.82)
MAT["rust"] = make_material("Rust | orange oxide", (0.48, 0.20, 0.075), 0.82, 0.32)
MAT["rust_dark"] = make_material("Rust | deep brown", (0.22, 0.10, 0.055), 0.83, 0.22)
MAT["titanium"] = make_material("Titanium | graphite armor", (0.17, 0.22, 0.25), 0.31, 0.88)
MAT["titanium_edge"] = make_material("Titanium | bright bevel", (0.48, 0.57, 0.60), 0.26, 0.90)
MAT["blue"] = make_material("Paint | faded survival blue", (0.095, 0.23, 0.29), 0.58, 0.2)
MAT["yellow"] = make_material("Paint | worn warning yellow", (0.78, 0.47, 0.09), 0.62, 0.16)
MAT["glass"] = make_material("Glass | dusty teal", (0.18, 0.48, 0.48), 0.22, 0.18)
MAT["glass_light"] = make_material("Glass | pale reflection", (0.46, 0.75, 0.70), 0.19, 0.08)
MAT["concrete"] = make_material("Concrete | cracked ash", (0.39, 0.39, 0.35), 0.93)
MAT["concrete_light"] = make_material("Concrete | broken aggregate", (0.58, 0.55, 0.48), 0.91)
MAT["stone"] = make_material("Stone | charcoal gray", (0.28, 0.30, 0.29), 0.95)
MAT["stone_light"] = make_material("Stone | worn facets", (0.45, 0.44, 0.39), 0.92)
MAT["brick"] = make_material("Brick | fired clay", (0.48, 0.22, 0.15), 0.9)
MAT["mortar"] = make_material("Mortar | pale sand", (0.62, 0.57, 0.46), 0.98)
MAT["cloth"] = make_material("Cloth | field canvas", (0.34, 0.34, 0.23), 0.96)
MAT["cloth_light"] = make_material("Cloth | patched canvas", (0.54, 0.48, 0.33), 0.96)
MAT["leather"] = make_material("Leather | dark hide", (0.22, 0.13, 0.09), 0.9)
MAT["rope"] = make_material("Rope | plant fiber", (0.68, 0.52, 0.30), 0.93)
MAT["bone"] = make_material("Bone | infected ivory", (0.77, 0.71, 0.54), 0.72)
MAT["chitin"] = make_material("Chitin | dark carapace", (0.19, 0.25, 0.20), 0.45, 0.34)
MAT["chitin_light"] = make_material("Chitin | olive ridges", (0.42, 0.48, 0.27), 0.38, 0.26)
MAT["acid"] = make_material("Bile | toxic phosphor", (0.38, 0.82, 0.12), 0.25, 0.0, (0.16, 0.60, 0.035), 1.5)
MAT["plasma"] = make_material("Plasma | xenocrystal cyan", (0.06, 0.60, 0.94), 0.2, 0.28, (0.015, 0.40, 1.0), 2.2)
MAT["ember"] = make_material("Ember | orange glow", (1.0, 0.28, 0.035), 0.28, 0.0, (1.0, 0.10, 0.006), 2.4)
MAT["coal"] = make_material("Charcoal | blackened", (0.055, 0.061, 0.058), 0.95)
MAT["water"] = make_material("Water | purified cyan", (0.12, 0.63, 0.76), 0.18, 0.14)
MAT["warning"] = make_material("Hazard | faded amber", (0.91, 0.53, 0.08), 0.64, 0.12)
MAT["stage"] = make_material("Preview | charcoal platform", (0.085, 0.105, 0.11), 0.93)

MATERIALS = list(MAT.values())
MAT_INDEX = {material.name: index for index, material in enumerate(MATERIALS)}


class Builder:
    def __init__(self, name):
        self.name = name
        self.vertices = []
        self.faces = []
        self.material_ids = []
        self.smooth = []
        self.materials = []
        self.material_map = {}

    def face(self, indices, material, smooth=False):
        if len(set(indices)) < 3:
            return
        self.faces.append(tuple(indices))
        if material.name not in self.material_map:
            self.material_map[material.name] = len(self.materials)
            self.materials.append(material)
        self.material_ids.append(self.material_map[material.name])
        self.smooth.append(smooth)

    def box(self, center, size, material, rotation=(0, 0, 0)):
        cx, cy, cz = center
        sx, sy, sz = (v * 0.5 for v in size)
        rot = Euler(rotation, "XYZ")
        start = len(self.vertices)
        for point in ((-sx,-sy,-sz),(sx,-sy,-sz),(sx,sy,-sz),(-sx,sy,-sz),
                      (-sx,-sy,sz),(sx,-sy,sz),(sx,sy,sz),(-sx,sy,sz)):
            v = Vector(point)
            v.rotate(rot)
            self.vertices.append((cx + v.x, cy + v.y, cz + v.z))
        for face in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
            self.face(tuple(start + i for i in face), material)

    def oriented_basis(self, start, end):
        a, b = Vector(start), Vector(end)
        axis = (b - a).normalized()
        u = axis.cross(Vector((0, 0, 1)))
        if u.length < 1e-5:
            u = axis.cross(Vector((0, 1, 0)))
        u.normalize()
        v = axis.cross(u).normalized()
        return a, b, axis, u, v

    def rod(self, start, end, radius, material, segments=10, radius_end=None):
        a, b, axis, u, v = self.oriented_basis(start, end)
        radius_end = radius if radius_end is None else radius_end
        base = len(self.vertices)
        for point, rad in ((a, radius), (b, radius_end)):
            for i in range(segments):
                ang = 2 * math.pi * i / segments
                pos = point + rad * (math.cos(ang) * u + math.sin(ang) * v)
                self.vertices.append(tuple(pos))
        self.face(tuple(base + i for i in reversed(range(segments))), material)
        self.face(tuple(base + segments + i for i in range(segments)), material)
        for i in range(segments):
            j = (i + 1) % segments
            self.face((base+i, base+j, base+segments+j, base+segments+i), material, True)

    def cylinder(self, center, radius, depth, material, axis="z", segments=16, radius_top=None):
        c = Vector(center)
        direction = Vector((1, 0, 0)) if axis == "x" else Vector((0, 1, 0)) if axis == "y" else Vector((0, 0, 1))
        a = c - direction * depth * 0.5
        b = c + direction * depth * 0.5
        self.rod(a, b, radius, material, segments, radius if radius_top is None else radius_top)

    def sphere(self, center, radii, material, segments=16, rings=10):
        cx, cy, cz = center
        rx, ry, rz = radii
        base = len(self.vertices)
        for j in range(rings + 1):
            phi = math.pi * j / rings
            for i in range(segments):
                theta = 2 * math.pi * i / segments
                self.vertices.append((cx + rx * math.sin(phi) * math.cos(theta),
                                      cy + ry * math.sin(phi) * math.sin(theta),
                                      cz + rz * math.cos(phi)))
        for j in range(rings):
            for i in range(segments):
                ni = (i + 1) % segments
                a = base + j * segments + i
                b = base + j * segments + ni
                c = base + (j + 1) * segments + ni
                d = base + (j + 1) * segments + i
                self.face((a,b,c,d), material, True)

    def torus(self, center, major_radius, minor_radius, material, axis="z", major_segments=28, minor_segments=8):
        cx, cy, cz = center
        base = len(self.vertices)
        for i in range(major_segments):
            major = 2 * math.pi * i / major_segments
            for j in range(minor_segments):
                minor = 2 * math.pi * j / minor_segments
                radial = major_radius + minor_radius * math.cos(minor)
                if axis == "x":
                    point = (cx + minor_radius * math.sin(minor), cy + radial * math.cos(major), cz + radial * math.sin(major))
                elif axis == "y":
                    point = (cx + radial * math.cos(major), cy + minor_radius * math.sin(minor), cz + radial * math.sin(major))
                else:
                    point = (cx + radial * math.cos(major), cy + radial * math.sin(major), cz + minor_radius * math.sin(minor))
                self.vertices.append(point)
        for i in range(major_segments):
            for j in range(minor_segments):
                a = base + i * minor_segments + j
                b = base + ((i + 1) % major_segments) * minor_segments + j
                c = base + ((i + 1) % major_segments) * minor_segments + (j + 1) % minor_segments
                d = base + i * minor_segments + (j + 1) % minor_segments
                self.face((a,b,c,d), material, True)

    def tapered_spike(self, base, tip, radius, material, segments=7):
        self.rod(base, tip, radius, material, segments, 0.006)

    def bolt(self, center, radius=0.055, depth=0.035, material=None, axis="y"):
        material = material or MAT["iron_light"]
        self.cylinder(center, radius, depth, material, axis, 6)
        c = Vector(center)
        c += Vector((0, -1, 0)) * depth * 0.55 if axis == "y" else Vector((0, 0, 1)) * depth * 0.55
        self.box(tuple(c), (radius * 1.5, depth * 0.15, radius * 0.16), MAT["iron"])

    def rope_path(self, points, radius=0.025, material=None):
        material = material or MAT["rope"]
        for a, b in zip(points, points[1:]):
            self.rod(a, b, radius, material, 7)
        for point in points[1:-1]:
            self.sphere(point, (radius, radius, radius), material, 7, 5)

    def arc(self, center, radius, start_angle, end_angle, plane, tube_radius, material, steps=28):
        points = []
        for i in range(steps + 1):
            t = start_angle + (end_angle - start_angle) * i / steps
            if plane == "xy":
                points.append((center[0] + radius * math.cos(t), center[1] + radius * math.sin(t), center[2]))
            elif plane == "xz":
                points.append((center[0] + radius * math.cos(t), center[1], center[2] + radius * math.sin(t)))
            else:
                points.append((center[0], center[1] + radius * math.cos(t), center[2] + radius * math.sin(t)))
        for a, b in zip(points, points[1:]):
            self.rod(a, b, tube_radius, material, 7)

    def corrugated_wall(self, x_center, y_center, z_bottom, width, height, depth, material, period=0.34, amplitude=0.045, steps=48):
        base = len(self.vertices)
        for side in range(2):
            y_offset = depth * (0.5 if side == 0 else -0.5)
            for row in range(2):
                z = z_bottom + height * row
                for col in range(steps + 1):
                    x = x_center - width * 0.5 + width * col / steps
                    wave = amplitude * math.sin((x - (x_center - width * 0.5)) * 2 * math.pi / period)
                    self.vertices.append((x, y_center + y_offset + wave, z))
        stride = steps + 1
        for side in range(2):
            sbase = base + side * 2 * stride
            for col in range(steps):
                a = sbase + col
                b = sbase + col + 1
                c = sbase + stride + col + 1
                d = sbase + stride + col
                self.face((a,b,c,d) if side == 0 else (d,c,b,a), material)
        back = base + 2 * stride
        front = base
        for col in range(steps):
            self.face((front+col, back+col, back+col+1, front+col+1), material)
        for edge in (0, steps):
            self.face((front+edge, front+stride+edge, back+stride+edge, back+edge), material)
        self.face((front, front+steps, back+steps, back), material)
        self.face((front+stride, back+stride, back+stride+steps, front+stride+steps), material)

    def mesh(self, collection, location=(0, 0, 0)):
        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.materials.clear()
        for material in self.materials:
            mesh.materials.append(material)
        for poly, material_id, smooth in zip(mesh.polygons, self.material_ids, self.smooth):
            poly.material_index = material_id
            poly.use_smooth = smooth
        mesh.update()
        obj = bpy.data.objects.new(self.name, mesh)
        collection.objects.link(obj)
        obj.location = location
        bevel = obj.modifiers.new("Small softened manufactured edges", "BEVEL")
        bevel.width = 0.012
        bevel.segments = 2
        bevel.limit_method = "ANGLE"
        bevel.angle_limit = math.radians(38)
        normals = obj.modifiers.new("Weighted face normals", "WEIGHTED_NORMAL")
        normals.keep_sharp = True
        return obj


def make_collection(name, parent=None):
    collection = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(collection)
    return collection


ROOT_COLLECTION = bpy.context.scene.collection
ASSET_ROOT = make_collection("Survivor Base Building Kit")
CATEGORY_COLLECTIONS = {name: make_collection(name, ASSET_ROOT) for name in ("Building", "Utility", "Defense")}
ASSETS = []


def spawn(name, category, builder, location=(0, 0, 0)):
    obj = builder.mesh(CATEGORY_COLLECTIONS[category], location)
    obj["category"] = category
    obj["use_note"] = name.replace("_", " ")
    ASSETS.append(obj)
    return obj


def create_foundation():
    b = Builder("01_Wooden_Floor_Foundation")
    # Thick plank deck with cross beams, end grain, rope tie-downs and iron anchor corners.
    for i in range(8):
        x = -1.75 + i * 0.5
        b.box((x, 0, 0.17), (0.46, 3.8, 0.22), MAT["wood_light"] if i % 3 else MAT["wood"])
        for offset in (-1.35, -0.45, 0.55, 1.42):
            b.box((x + 0.11, offset, 0.286), (0.012, 0.31, 0.009), MAT["wood_edge"])
        for y in (-1.66, 1.66):
            b.cylinder((x, y, 0.294), 0.036, 0.025, MAT["iron_light"], "z", 8)
    for y in (-1.58, 1.58):
        b.box((0, y, -0.015), (4.15, 0.32, 0.33), MAT["wood"])
    for x in (-1.55, 1.55):
        b.box((x, 0, -0.015), (0.32, 3.35, 0.33), MAT["wood_dark"])
    for x in (-1.85, 1.85):
        for y in (-1.85, 1.85):
            b.box((x, y, 0.22), (0.32, 0.32, 0.11), MAT["iron"])
            b.bolt((x, y - 0.17, 0.24), 0.045, 0.035)
    b.rope_path([(-2.0, -1.3, 0.03), (-2.0, -1.62, 0.03), (-1.68, -1.62, 0.03)], 0.036)
    b.rope_path([(2.0, 1.3, 0.03), (2.0, 1.62, 0.03), (1.68, 1.62, 0.03)], 0.036)
    return spawn("Wooden Floor Foundation", "Building", b)


def create_wood_wall():
    b = Builder("02_Wooden_Wall_Panel")
    b.box((0, 0.12, 1.48), (3.05, 0.22, 2.96), MAT["wood_dark"])
    for row in range(7):
        z = 0.28 + row * 0.39
        offset = 0.04 * math.sin(row * 2.0)
        b.box((offset, -0.035, z), (2.95, 0.18, 0.355), MAT["wood_light"] if row % 2 else MAT["wood"])
        # Split seams, knots and dark nail heads make the wall read as distinct salvaged planks.
        b.ellipse = None
        for x in (-1.28, 1.28):
            b.bolt((x + offset, -0.139, z), 0.037, 0.027, MAT["iron_light"])
        if row in (1, 4, 6):
            b.sphere((-0.62 + row * 0.11, -0.141, z + 0.02), (0.10, 0.018, 0.038), MAT["wood_dark"], 12, 7)
    for x in (-1.45, 1.45):
        b.box((x, -0.18, 1.48), (0.18, 0.16, 3.05), MAT["wood_dark"])
        for z in (0.25, 1.5, 2.75):
            b.bolt((x, -0.277, z), 0.055, 0.025, MAT["iron"])
    b.box((0, 0.0, 0.14), (3.24, 0.38, 0.22), MAT["iron"])
    return spawn("Wooden Wall Panel", "Building", b)


def create_window_wall():
    b = Builder("03_Wooden_Window_Wall")
    # Leave a genuine opening around the pane rather than placing glass on a solid wall.
    b.box((-1.18, 0.10, 1.5), (0.66, 0.28, 3.0), MAT["wood"])
    b.box((1.18, 0.10, 1.5), (0.66, 0.28, 3.0), MAT["wood"])
    b.box((0, 0.10, 0.29), (1.72, 0.28, 0.58), MAT["wood_dark"])
    b.box((0, 0.10, 2.72), (1.72, 0.28, 0.56), MAT["wood"])
    b.box((0, 0.015, 1.51), (1.70, 0.045, 1.83), MAT["glass"])
    b.box((0, -0.115, 1.51), (1.91, 0.22, 0.12), MAT["wood_edge"])
    b.box((0, -0.115, 2.43), (1.91, 0.22, 0.12), MAT["wood_edge"])
    for x in (-0.90, 0.90):
        b.box((x, -0.115, 1.51), (0.12, 0.22, 0.98), MAT["wood_edge"])
    b.box((0, -0.16, 1.51), (0.065, 0.26, 0.92), MAT["wood"])
    b.box((0, -0.16, 1.51), (1.80, 0.26, 0.065), MAT["wood"])
    b.box((0, 0.245, 1.51), (1.78, 0.13, 0.10), MAT["iron"])
    # Broken glass shards in one corner and layered viewing slit shutters.
    b.tapered_spike((-0.76, -0.155, 2.30), (-0.49, -0.155, 2.00), 0.018, MAT["glass_light"], 5)
    b.tapered_spike((-0.75, -0.155, 2.24), (-0.56, -0.155, 2.08), 0.015, MAT["glass_light"], 5)
    for x in (-1.32, 1.32):
        for z in (0.36, 1.46, 2.55):
            b.bolt((x, -0.19, z), 0.046, 0.03)
    b.box((0, -0.265, 0.22), (3.2, 0.38, 0.22), MAT["iron"])
    return spawn("Wooden Window Wall", "Building", b)


def create_reinforced_door():
    b = Builder("04_Reinforced_Wooden_Door")
    b.box((0, 0.05, 1.18), (1.42, 0.22, 2.36), MAT["wood"])
    for x in (-0.51, 0.51):
        b.box((x, -0.085, 1.18), (0.09, 0.045, 2.22), MAT["wood_dark"])
    for i in range(6):
        z = 0.26 + i * 0.37
        b.box((0, -0.09, z), (1.30, 0.035, 0.024), MAT["wood_edge"])
    # Two mismatched armor plates, exposed hinges, spring coil and a chunky latch.
    b.box((-0.31, -0.17, 1.70), (0.74, 0.08, 0.63), MAT["titanium"])
    b.box((0.34, -0.17, 0.79), (0.66, 0.08, 0.52), MAT["rust"])
    for x, z in ((-0.59,1.45),(-0.02,1.45),(-0.59,1.95),(-0.02,1.95),(0.09,0.60),(0.59,0.60),(0.09,0.98),(0.59,0.98)):
        b.bolt((x,-0.224,z),0.048,0.03)
    for z in (0.43,1.85):
        b.box((-0.69, 0.03, z), (0.22, 0.32, 0.25), MAT["iron"])
        b.cylinder((-0.69, -0.005, z), 0.085, 0.11, MAT["iron_light"], "z", 12)
    b.box((0.53,-0.27,1.18),(0.18,0.18,0.30),MAT["iron_light"])
    b.rod((0.52,-0.30,1.18),(0.69,-0.30,1.18),0.035,MAT["iron"],8)
    for i in range(5):
        z = 0.83 + i * 0.105
        b.torus((0.67,-0.05,z),0.075,0.018,MAT["iron_light"],"z",16,6)
    b.box((0,0.18,1.18),(1.70,0.18,2.60),MAT["wood_dark"])
    return spawn("Reinforced Wooden Door", "Building", b)


def create_stone_brick_wall():
    b = Builder("05_Stone_and_Brick_Wall")
    b.box((0,0.08,1.4),(3.25,0.42,2.80),MAT["mortar"])
    # Interlocking irregular stone blocks, chipped cap and a few exposed brick repairs.
    for row in range(6):
        z = 0.25 + row * 0.46
        x = -1.42 + (0.34 if row % 2 else 0)
        while x < 1.55:
            width = random.uniform(0.48,0.89)
            if x + width > 1.62:
                width = 1.62 - x
            if width < 0.18:
                break
            mat = MAT["brick"] if (row in (2,4) and int((x+2)*5)%4==1) else MAT["stone_light"] if (row+int(x*3))%3==0 else MAT["stone"]
            b.box((x+width*0.5, -0.18 + random.uniform(-0.035,0.035), z), (width-0.035,0.20,0.40),mat,
                  (random.uniform(-0.025,0.025),random.uniform(-0.03,0.03),random.uniform(-0.012,0.012)))
            # Small angled chips interrupt the regular block rhythm.
            if random.random() < 0.38:
                b.tapered_spike((x+width*0.23,-0.296,z+0.12),(x+width*0.23+0.12,-0.30,z+0.19),0.045,MAT["concrete_light"],5)
            x += width
    for x in (-1.52,1.52):
        b.box((x,-0.34,1.35),(0.17,0.11,2.63),MAT["iron"])
        for z in (0.28,0.96,1.64,2.32):
            b.bolt((x,-0.41,z),0.052,0.03)
    b.box((0,0.02,2.83),(3.5,0.57,0.25),MAT["concrete_light"])
    return spawn("Stone and Brick Wall", "Building", b)


def create_scrap_wall():
    b = Builder("06_Scrap_Metal_Wall")
    b.box((0,0.20,1.48),(3.35,0.30,2.96),MAT["iron"])
    for i in range(5):
        x = -1.31 + i * 0.65
        w = 0.62
        material = MAT["rust"] if i % 2 == 0 else MAT["iron_light"]
        b.corrugated_wall(x,-0.02,0.12,w,2.7,0.075,material,0.22,0.045,24)
        # A contrasting strip and seam marks the overlap between salvaged sheets.
        b.box((x+w*0.5,-0.105,1.49),(0.035,0.04,2.72),MAT["rust_dark"])
        for z in (0.30,0.78,1.28,1.82,2.32,2.66):
            b.bolt((x,-0.12,z),0.040,0.028,MAT["iron_light"])
            b.bolt((x+w,-0.12,z),0.040,0.028,MAT["iron_light"])
    for x in (-1.48,1.48):
        b.rod((x,0.0,0.12),(x,0.06,2.85),0.095,MAT["rust_dark"],10)
        b.box((x,-0.12,1.48),(0.20,0.17,3.05),MAT["iron"])
    b.rod((-1.32,-0.23,0.35),(1.30,-0.23,2.55),0.065,MAT["iron_light"],8)
    b.rod((-1.32,-0.23,2.57),(1.30,-0.23,0.40),0.065,MAT["iron_light"],8)
    b.box((0,0.13,0.13),(3.48,0.55,0.22),MAT["iron"])
    return spawn("Scrap Metal Wall", "Building", b)


def create_titanium_wall():
    b = Builder("07_Armored_Titanium_Wall")
    b.box((0,0.12,1.58),(3.5,0.55,3.16),MAT["titanium"])
    # Overlapping armor slabs, buttress ribs, service hatch and heavy fasteners.
    for row in range(3):
        z = 0.55 + row * 0.96
        for col in range(3):
            x = -1.10 + col * 1.10 + (0.12 if row%2 else 0)
            width = 0.94 if not (row == 2 and col == 2) else 0.79
            b.box((x,-0.235,z),(width,0.16,0.82),MAT["titanium"] if (row+col)%2 else MAT["iron"])
            b.box((x,-0.325,z),(width-0.10,0.035,0.08),MAT["titanium_edge"])
            for px in (-width*0.38,width*0.38):
                for pz in (-0.29,0.29):
                    b.bolt((x+px,-0.36,z+pz),0.075,0.045,MAT["titanium_edge"])
    for x in (-1.62,1.62):
        b.box((x,-0.28,1.56),(0.22,0.23,3.04),MAT["iron"])
        for z in (0.38,1.08,1.80,2.55):
            b.box((x,-0.40,z),(0.33,0.10,0.18),MAT["warning"])
    b.box((0.15,-0.44,1.52),(0.67,0.12,0.53),MAT["iron"])
    b.box((0.15,-0.51,1.56),(0.42,0.035,0.29),MAT["blue"])
    for i in range(3):
        b.cylinder((-0.03+i*0.16,-0.545,1.57),0.035,0.022,MAT["plasma"],"y",12)
    for x in (-1.38,0,1.38):
        b.rod((x,0.1,0.12),(x,0.1,3.05),0.14,MAT["iron_light"],10)
        b.box((x,0.15,0.2),(0.42,0.72,0.30),MAT["iron"])
    return spawn("Armored Titanium Wall", "Building", b)


def create_blast_door():
    b = Builder("08_Motorized_Blast_Door")
    # Heavy recessed frame; the leaf is offset to one side in a parked open position.
    b.box((0,0.18,1.85),(3.90,0.74,3.70),MAT["iron"])
    b.box((0,0.08,1.85),(3.22,0.56,3.08),MAT["titanium"])
    for x in (-1.73,1.73):
        b.box((x,-0.34,1.86),(0.34,0.45,3.52),MAT["titanium_edge"])
        for z in (0.32,0.88,1.45,2.26,2.84,3.37):
            b.bolt((x,-0.59,z),0.072,0.045)
    for z in (0.21,3.50):
        b.box((0,-0.38,z),(3.60,0.53,0.24),MAT["iron_light"])
    # Door leaf with stepped reinforcement ribs.
    b.box((0.20,-0.46,1.83),(2.68,0.30,2.78),MAT["titanium"])
    for x in (-0.80,-0.15,0.50,1.14):
        b.box((x,-0.64,1.82),(0.12,0.12,2.42),MAT["iron_light"])
        for z in (0.66,3.00):
            b.bolt((x,-0.72,z),0.065,0.04)
    for x in (-0.90,0.0,0.90):
        b.box((x,-0.67,0.65),(0.62,0.08,0.18),MAT["warning"])
    for x in (-1.25,1.25):
        b.box((x,0.04,1.85),(0.20,0.48,3.18),MAT["rust_dark"])
        b.rod((x,0.22,0.75),(x+(-0.25 if x<0 else 0.25),0.22,0.75),0.13,MAT["iron_light"])
        b.rod((x,0.22,2.93),(x+(-0.25 if x<0 else 0.25),0.22,2.93),0.13,MAT["iron_light"])
    b.box((2.23,-0.18,1.36),(0.52,0.40,0.84),MAT["iron"])
    b.box((2.23,-0.405,1.40),(0.30,0.05,0.48),MAT["blue"])
    for i in range(3):
        b.cylinder((2.13+i*0.10,-0.45,1.42),0.035,0.03,MAT["plasma"],"y",12)
    b.rope_path([(2.15,0.1,1.0),(2.55,0.10,0.8),(2.55,0.10,0.42)],0.035,MAT["rust"])
    return spawn("Motorized Blast Door", "Building", b)


def create_campfire():
    b = Builder("09_Survivor_Campfire")
    b.cylinder((0,0,0.10),0.80,0.20,MAT["coal"],"z",24)
    for i in range(13):
        angle = 2*math.pi*i/13
        radius = 0.82 + 0.06*math.sin(i*2.1)
        x,y = radius*math.cos(angle),radius*math.sin(angle)
        b.sphere((x,y,0.22),(0.25,0.21,0.17),MAT["stone_light"] if i%3==0 else MAT["stone"],12,8)
    b.rod((-0.62,-0.18,0.38),(0.53,0.22,0.49),0.16,MAT["wood_dark"],9)
    b.rod((-0.45,0.42,0.37),(0.55,-0.34,0.47),0.15,MAT["wood"],9)
    b.rod((-0.48,-0.40,0.42),(0.42,0.39,0.54),0.11,MAT["coal"],8)
    for i in range(6):
        ang = i*math.tau/6
        b.sphere((0.25*math.cos(ang),0.22*math.sin(ang),0.50+0.05*(i%3)),(0.15,0.13,0.15),MAT["ember"],12,8)
    # Three bent rebar supports, hanging pot and a small mug.
    for x,y in ((-0.67,-0.44),(0.63,-0.38),(0.06,0.70)):
        b.rod((x*0.7,y*0.7,0.18),(x,y,1.12),0.045,MAT["iron"])
    b.rod((-0.67,-0.44,1.10),(0.63,-0.38,1.10),0.04,MAT["iron_light"])
    b.rod((-0.08,-0.41,1.08),(-0.08,-0.41,0.85),0.028,MAT["iron_light"])
    b.cylinder((-0.08,-0.41,0.79),0.25,0.16,MAT["iron"],"z",16)
    b.cylinder((-0.08,-0.41,0.885),0.20,0.025,MAT["warning"],"z",16)
    b.cylinder((0.62,0.55,0.27),0.12,0.26,MAT["blue"],"z",16)
    b.torus((0.77,0.55,0.30),0.10,0.022,MAT["blue"],"z",16,6)
    return spawn("Survivor Campfire", "Utility", b)


def create_workbench():
    b = Builder("10_Survivor_Workbench")
    # Laminated top with heavy trestle legs and cross braces.
    for i in range(6):
        y = -0.53 + i*0.21
        b.box((0,y,1.15),(2.82,0.195,0.18),MAT["wood_light"] if i%2 else MAT["wood"])
        b.box((0,y,1.245),(2.72,0.012,0.012),MAT["wood_edge"])
    for x in (-1.12,1.12):
        for y in (-0.40,0.40):
            b.box((x,y,0.54),(0.20,0.20,1.10),MAT["wood_dark"],(0,-0.08 if x<0 else 0.08,0))
        b.box((x,0,0.30),(0.25,1.05,0.16),MAT["wood"])
    b.box((0,0,0.40),(2.25,0.13,0.18),MAT["wood"])
    # Two drawers, iron pulls and a right-side open shelf.
    for z in (0.65,0.91):
        b.box((-0.38,-0.62,z),(0.86,0.38,0.20),MAT["wood"])
        b.box((-0.38,-0.827,z),(0.16,0.05,0.055),MAT["iron_light"])
        for x in (-0.70,-0.06):
            b.bolt((x,-0.65,z),0.035,0.025,MAT["iron"])
    b.box((0.77,-0.18,0.61),(0.54,0.68,0.07),MAT["iron"])
    b.box((0.77,-0.18,0.83),(0.54,0.68,0.07),MAT["iron"])
    # Bench vise, mallet, wrench, saw and a back rail with hanging hooks.
    b.box((-0.91,-0.52,1.40),(0.47,0.35,0.18),MAT["iron"])
    b.box((-0.91,-0.71,1.51),(0.42,0.16,0.16),MAT["iron_light"])
    b.rod((-1.20,-0.52,1.42),(-0.64,-0.52,1.42),0.035,MAT["iron_light"])
    b.box((0.42,0.38,1.51),(0.62,0.10,0.09),MAT["wood_dark"],(0,0.0,0.12))
    b.box((0.42,0.38,1.60),(0.20,0.13,0.22),MAT["iron"])
    b.rod((1.13,0.34,1.26),(1.13,0.34,1.78),0.026,MAT["iron_light"])
    for i in range(6):
        x = -1.15 + i*0.46
        b.rod((x,0.56,1.35),(x,0.56,1.72),0.035,MAT["wood"])
        b.rod((x,0.56,1.72),(x+0.10,0.56,1.80),0.018,MAT["iron"])
    b.box((0,0.60,1.60),(2.52,0.14,0.14),MAT["wood"])
    return spawn("Survivor Workbench", "Utility", b)


def create_crate():
    b = Builder("11_Wooden_Storage_Crate")
    # Open slat gaps reveal a dark interior and a separately braced, partly open lid.
    b.box((0,0,0.60),(1.55,1.14,1.12),MAT["wood_dark"])
    for row in range(5):
        z = 0.18 + row*0.205
        for x in (-0.71,0.71):
            b.box((x,-0.595,z),(0.18,0.09,0.17),MAT["wood_light"] if row%2 else MAT["wood"])
            b.box((x,0.595,z),(0.18,0.09,0.17),MAT["wood"])
        for y in (-0.49,0.49):
            b.box((0,y,z),(1.33,0.10,0.17),MAT["wood_light"] if row in (0,3) else MAT["wood"])
    # Corner cleats, rope grips and leather label sleeve.
    for x in (-0.76,0.76):
        for y in (-0.56,0.56):
            b.box((x,y,0.60),(0.08,0.08,1.20),MAT["iron"])
            for z in (0.16,1.04):
                b.bolt((x,y-0.055,z),0.040,0.022)
    b.box((0,-0.66,0.62),(0.52,0.035,0.32),MAT["leather"])
    b.box((0,-0.686,0.62),(0.31,0.02,0.10),MAT["iron_light"])
    b.torus((0,-0.73,0.58),0.16,0.027,MAT["rope"],"y",18,6)
    for x in (-0.63,0.63):
        b.box((x,0,1.27),(0.14,1.12,0.12),MAT["wood_dark"],(0,0,math.radians(18)))
    for y in (-0.42,0.42):
        b.box((0,y,1.32),(1.52,0.13,0.12),MAT["wood_light"],(0,0,math.radians(-10)))
    b.rod((-0.63,-0.42,1.24),(0.63,0.42,1.40),0.055,MAT["wood_dark"])
    return spawn("Wooden Storage Crate", "Utility", b)


def create_purifier():
    b = Builder("12_Water_Purifier_Station")
    b.box((0,0,0.15),(1.72,1.26,0.28),MAT["iron"])
    for x in (-0.64,0.64):
        for y in (-0.40,0.40):
            b.rod((x,y,0.22),(x,y,0.47),0.08,MAT["iron_light"])
    b.cylinder((-0.28,0.10,1.03),0.39,1.20,MAT["iron"],"z",24)
    b.cylinder((-0.28,0.10,1.60),0.41,0.08,MAT["iron_light"],"z",24)
    b.cylinder((-0.28,0.10,0.44),0.41,0.12,MAT["rust"],"z",24)
    b.cylinder((-0.28,0.10,0.85),0.345,0.34,MAT["coal"],"z",24)
    b.cylinder((-0.28,0.10,1.18),0.345,0.27,MAT["stone_light"],"z",24)
    b.cylinder((-0.28,0.10,1.43),0.345,0.19,MAT["water"],"z",24)
    b.torus((-0.28,0.10,0.64),0.395,0.026,MAT["iron_light"],"z",24,8)
    b.torus((-0.28,0.10,1.02),0.395,0.026,MAT["rust"],"z",24,8)
    for z in (0.72,1.03,1.37):
        b.box((-0.28,-0.31,z),(0.50,0.025,0.035),MAT["glass_light"])
    # Feed pipe, outlet gooseneck, valves and bottles.
    b.rod((-0.28,0.10,1.63),(-0.28,0.10,1.92),0.10,MAT["iron"])
    b.rod((-0.28,0.10,1.90),(0.23,0.10,1.90),0.085,MAT["iron"])
    b.rod((0.23,0.10,1.90),(0.23,0.10,1.60),0.085,MAT["iron"])
    b.torus((0.23,0.10,1.54),0.09,0.028,MAT["iron_light"],"z",16,6)
    b.cylinder((0.22,-0.01,0.82),0.18,0.42,MAT["glass"],"z",16)
    b.cylinder((0.22,-0.01,0.68),0.18,0.10,MAT["water"],"z",16)
    b.cylinder((0.22,-0.01,1.06),0.20,0.07,MAT["iron_light"],"z",16)
    b.rod((-0.75,0.1,1.35),(-0.75,0.1,1.77),0.055,MAT["iron_light"])
    b.box((-0.75,0.10,1.83),(0.18,0.18,0.12),MAT["blue"])
    b.rod((-0.75,0.10,1.84),(-0.62,0.10,1.94),0.026,MAT["iron"])
    b.torus((-0.63,0.10,1.96),0.11,0.024,MAT["iron_light"],"y",18,6)
    for x in (-0.74,0.0,0.65):
        b.bolt((x,-0.65,0.20),0.045,0.03)
    return spawn("Water Purifier Station", "Utility", b)


def create_bedroll():
    b = Builder("13_Survivor_Sleeping_Bedroll")
    # Soft uneven ground mat with stitched seams and two rolled sleeping-bag ends.
    for layer in range(3):
        z = 0.20 + layer*0.075
        b.box((0,0,z),(1.42,2.30,0.14),MAT["leather"] if layer==0 else MAT["cloth"])
    b.box((0,-0.68,0.46),(1.34,0.74,0.13),MAT["cloth_light"],(0.08,0.02,0.0))
    b.box((0,0.10,0.44),(1.30,0.85,0.13),MAT["cloth"],(-0.05,0.02,0.0))
    # Quilt panels and raised seams are raised above the cloth surface.
    for y in (-0.84,-0.38,0.10,0.57,0.92):
        b.rod((-0.57,y,0.53),(0.57,y+0.03,0.54),0.015,MAT["cloth_light"],6)
    for x in (-0.46,0.46):
        b.rod((x,-1.02,0.53),(x+0.02,0.98,0.53),0.015,MAT["leather"])
    for x in (-0.45,0.45):
        b.torus((x,0.96,0.40),0.18,0.055,MAT["cloth_light"],"y",18,8)
    for y in (-0.64,0.52):
        b.box((0,y,0.49),(1.48,0.17,0.08),MAT["leather"])
        b.box((0.0,y-0.10,0.50),(0.24,0.055,0.065),MAT["iron"])
    # Patch, pillow and a buckled gear tag.
    b.box((0.31,-0.58,0.54),(0.27,0.31,0.035),MAT["cloth_light"],(0,0,0.18))
    b.box((0,-0.99,0.53),(0.62,0.23,0.20),MAT["leather"])
    b.box((0,-0.995,0.64),(0.45,0.12,0.05),MAT["cloth_light"])
    return spawn("Survivor Sleeping Bedroll", "Utility", b)


def create_lantern():
    b = Builder("14_Sanity_Beacon_Lantern")
    b.cylinder((0,0,0.18),0.45,0.30,MAT["titanium"],"z",20)
    b.cylinder((0,0,0.38),0.39,0.10,MAT["iron_light"],"z",20)
    b.cylinder((0,0,1.15),0.28,1.42,MAT["glass"],"z",24)
    b.cylinder((0,0,0.55),0.34,0.12,MAT["titanium"],"z",20)
    b.cylinder((0,0,1.75),0.36,0.13,MAT["titanium"],"z",20)
    b.cylinder((0,0,1.88),0.45,0.17,MAT["iron"],"z",20)
    for x,y in ((-0.27,-0.27),(0.27,-0.27),(-0.27,0.27),(0.27,0.27)):
        b.rod((x*0.72,y*0.72,0.48),(x,y,1.82),0.050,MAT["iron_light"],9)
    b.torus((0,0,0.87),0.29,0.022,MAT["glass_light"],"z",24,6)
    b.torus((0,0,1.42),0.29,0.022,MAT["glass_light"],"z",24,6)
    b.cylinder((0,0,1.12),0.115,0.96,MAT["ember"],"z",16)
    for z in (0.78,1.02,1.26,1.50):
        b.torus((0,0,z),0.15,0.035,MAT["warning"],"z",16,7)
    b.torus((0,0,2.10),0.28,0.045,MAT["iron_light"],"x",24,8)
    b.rod((-0.28,0,2.10),(-0.28,0,1.88),0.04,MAT["iron_light"])
    b.rod((0.28,0,2.10),(0.28,0,1.88),0.04,MAT["iron_light"])
    b.box((0,-0.43,0.25),(0.24,0.09,0.26),MAT["blue"])
    b.cylinder((0,-0.50,0.27),0.055,0.045,MAT["warning"],"y",12)
    return spawn("Sanity Beacon Lantern", "Utility", b)


def create_fusion_generator():
    b = Builder("15_Compact_Fusion_Generator")
    b.box((0,0,0.15),(1.88,1.48,0.28),MAT["iron"])
    for x in (-0.72,0.72):
        for y in (-0.48,0.48):
            b.box((x,y,0.42),(0.22,0.22,0.52),MAT["titanium"])
            b.bolt((x,y-0.13,0.40),0.055,0.035,MAT["titanium_edge"])
    b.cylinder((0,0,1.27),0.56,1.42,MAT["titanium"],"z",24)
    b.cylinder((0,0,1.30),0.38,1.14,MAT["plasma"],"z",24)
    b.cylinder((0,0,0.58),0.64,0.16,MAT["iron_light"],"z",24)
    b.cylinder((0,0,1.99),0.64,0.16,MAT["iron_light"],"z",24)
    for z in (0.80,1.03,1.27,1.51,1.74):
        b.torus((0,0,z),0.48,0.035,MAT["titanium_edge"],"z",24,8)
    for i in range(8):
        ang = math.tau*i/8
        x,y = 0.52*math.cos(ang),0.52*math.sin(ang)
        b.rod((x,y,0.49),(x,y,2.05),0.060,MAT["titanium_edge"],9)
    # Control head, heat fins, capacitors, and thick torn copper power leads.
    b.box((0.72,-0.42,1.28),(0.50,0.30,0.73),MAT["iron"])
    b.box((0.72,-0.585,1.33),(0.33,0.05,0.42),MAT["blue"])
    for i in range(4):
        b.cylinder((0.60+i*0.08,-0.62,1.38),0.022,0.035,MAT["plasma"],"y",10)
    for x in (-0.75,0.75):
        b.cylinder((x,0.18,0.76),0.17,0.40,MAT["titanium"],"z",16)
        b.torus((x,0.18,0.98),0.145,0.025,MAT["warning"],"z",16,6)
    b.rope_path([(0.63,0.45,0.42),(1.20,0.45,0.37),(1.30,0.15,0.25),(0.87,-0.12,0.20)],0.055,MAT["rust"])
    b.rope_path([(-0.62,0.52,0.36),(-1.12,0.50,0.29),(-1.26,0.18,0.21),(-0.92,-0.26,0.18)],0.048,MAT["iron_light"])
    return spawn("Compact Fusion Generator", "Utility", b)


def create_spike_barricade():
    b = Builder("16_Wooden_Spike_Barricade")
    # Cross rails, uneven sharpened trunks, bone tips and fiber lashings.
    for z in (0.53,1.08,1.63):
        b.rod((-1.72,-0.06,z),(1.72,-0.06,z),0.14,MAT["wood_dark"],9)
    for i in range(7):
        x = -1.42 + i*0.47
        base_z = 0.18 + 0.04*(i%3)
        tip_z = 2.15 + (0.26 if i%2 else 0)
        b.rod((x,0.03,base_z),(x+0.08,0.02,tip_z-0.32),0.12,MAT["wood"] if i%2 else MAT["wood_light"],8,0.07)
        b.tapered_spike((x+0.08,0.02,tip_z-0.32),(x+0.10,0.02,tip_z),0.085,MAT["bone"],7)
        # Bone side teeth point outward from the cross logs.
        b.tapered_spike((x,-0.12,0.98),(x-0.20,-0.30,1.28),0.050,MAT["bone"],6)
        b.tapered_spike((x,-0.12,1.50),(x+0.18,-0.28,1.76),0.045,MAT["bone"],6)
        for z in (0.57,1.12,1.67):
            b.rope_path([(x-0.15,-0.19,z-0.10),(x,-0.24,z),(x+0.15,-0.19,z+0.10)],0.028,MAT["rope"])
    for x in (-1.60,1.60):
        b.rod((x,0.0,0.12),(x,0.0,1.87),0.16,MAT["wood_dark"])
        b.tapered_spike((x,-0.02,1.72),(x,-0.02,2.27),0.11,MAT["wood_light"],7)
    for x in (-1.35,1.35):
        b.rod((x,0.12,0.10),(x,0.12,0.52),0.18,MAT["iron"])
    return spawn("Wooden Spike Barricade", "Defense", b)


def create_razor_wire():
    b = Builder("17_Razor_Wire_Barricade")
    for x in (-1.48,-0.50,0.50,1.48):
        b.rod((x,0,0.10),(x+(0.12 if x<0 else -0.12),0.02,1.95),0.095,MAT["iron"],10)
        b.torus((x,0,0.20),0.17,0.034,MAT["iron_light"],"z",16,6)
        b.box((x,-0.09,0.34),(0.32,0.20,0.12),MAT["concrete"])
    # Three helical coils with crossed span wires and irregular barbs/fang fragments.
    for row,zbase in enumerate((0.63,1.15,1.68)):
        pts=[]
        turns=5
        count=turns*18
        for i in range(count+1):
            t=i/count
            theta=t*turns*math.tau
            x=-1.50+3.0*t+0.20*math.cos(theta)
            y=-0.13+0.28*math.sin(theta)
            z=zbase+0.21*math.sin(theta)
            pts.append((x,y,z))
        for a,c in zip(pts,pts[1:]):
            b.rod(a,c,0.024,MAT["iron_light"] if row==1 else MAT["rust"],6)
        for i in range(11):
            x=-1.42+i*0.284
            for side in (-1,1):
                z=zbase+0.06*math.sin(i*2+row)
                b.tapered_spike((x,-0.16,z),(x+0.095*side,-0.29,z+0.15),0.042,MAT["bone"] if i%3==0 else MAT["iron_light"],6)
    for x in (-1.40,0,1.40):
        b.rod((x,-0.12,0.36),(x,0.16,1.84),0.055,MAT["iron"])
    return spawn("Razor Wire Barricade", "Defense", b)


def create_chitin_wall():
    b = Builder("18_Chitin_Carapace_Wall")
    b.box((0,0.10,1.48),(3.22,0.48,2.92),MAT["iron"])
    # Five overlapping armored shell lobes with raised growth ribs and hooked spines.
    for i in range(5):
        x=-1.26+i*0.63
        z=1.53+0.10*math.sin(i*1.7)
        b.sphere((x,-0.27,z),(0.43,0.24,1.22 if i%2==0 else 1.08),MAT["chitin"] if i%2 else MAT["chitin_light"],18,12)
        for rib in (-0.22,0,0.22):
            b.rod((x+rib,-0.48,0.55),(x+rib*0.72,-0.48,2.50),0.035,MAT["titanium"])
        b.tapered_spike((x,-0.35,2.35),(x+0.10*math.sin(i),-0.43,2.90+0.10*(i%3)),0.12,MAT["bone"],8)
        if i%2==1:
            b.tapered_spike((x-0.29,-0.34,1.53),(x-0.58,-0.45,1.92),0.09,MAT["chitin_light"],7)
    for z in (0.55,2.34):
        b.rod((-1.50,-0.42,z),(1.50,-0.42,z),0.085,MAT["titanium"])
    # Acid-proof seals and steel foot anchors.
    for x in (-1.35,-0.68,0,0.68,1.35):
        b.cylinder((x,-0.52,0.36),0.075,0.10,MAT["acid"],"y",10)
        b.box((x,0.04,0.18),(0.36,0.72,0.22),MAT["titanium"])
    return spawn("Chitin Carapace Wall", "Defense", b)


def create_bear_trap():
    b = Builder("19_Snap_Jaw_Bear_Trap")
    b.box((0,0,0.14),(1.10,1.02,0.22),MAT["iron"])
    b.box((0,0,0.29),(0.58,0.54,0.12),MAT["iron_light"])
    # Opposed toothed jaws are shown open, with twin exposed spring coils and pivot bolts.
    for side in (-1,1):
        cx=side*0.41
        b.arc((cx,0.02,0.33),0.48,-math.pi*0.43,math.pi*0.43,"xy",0.085,MAT["iron_light"],20)
        for i in range(9):
            ang=-math.pi*0.43+i*(math.pi*0.86/8)
            x=cx+0.47*math.cos(ang)
            y=0.02+0.47*math.sin(ang)
            b.tapered_spike((x,y,0.39),(x,y,0.57+0.08*(i%2)),0.052,MAT["iron_light"],6)
        pivot_x=side*0.48
        b.cylinder((pivot_x,0.02,0.30),0.13,0.18,MAT["rust"],"y",12)
        for z in (0.25,0.37):
            b.torus((pivot_x,0.02,z),0.13,0.027,MAT["iron_light"],"y",18,6)
    # Trigger plate with embossed ribs and chain anchor.
    b.box((0,-0.02,0.40),(0.42,0.45,0.08),MAT["rust"])
    for i in range(4):
        b.box((0,-0.18+i*0.105,0.448),(0.32,0.025,0.018),MAT["iron_light"])
    b.rod((0,0.51,0.24),(0,0.86,0.15),0.045,MAT["iron"])
    for i in range(5):
        x=0.01+i*0.115
        b.rod((x,0.86,0.15),(x-0.09,0.96,0.14),0.022,MAT["iron_light"])
    for x in (-0.43,0.43):
        b.bolt((x,-0.51,0.18),0.055,0.03)
    return spawn("Snap-Jaw Bear Trap", "Defense", b)


def create_bile_mine():
    b = Builder("20_Corrosive_Bile_Landmine")
    b.cylinder((0,0,0.20),0.70,0.34,MAT["iron"],"z",24)
    b.cylinder((0,0,0.41),0.59,0.12,MAT["rust"],"z",24)
    b.torus((0,0,0.49),0.56,0.048,MAT["iron_light"],"z",24,8)
    # Asymmetric biological sacs sit under a protective metal cage, with glowing acid veins.
    b.sphere((0,-0.02,0.81),(0.43,0.40,0.37),MAT["acid"],18,12)
    for i in range(8):
        angle=math.tau*i/8
        x=0.49*math.cos(angle)
        y=0.49*math.sin(angle)
        b.sphere((x,y,0.73+0.05*math.sin(i*3)),(0.17,0.16,0.22),MAT["acid"] if i%2==0 else MAT["chitin"],12,8)
        b.rod((x*0.76,y*0.76,0.59),(x,y,1.01),0.055,MAT["titanium"])
    for i in range(12):
        angle=math.tau*i/12
        x,y=0.67*math.cos(angle),0.67*math.sin(angle)
        b.bolt((x,y,0.39),0.048,0.03,MAT["iron_light"],"z")
    # Detector puck and a jagged toothed perimeter.
    b.cylinder((0,0,1.11),0.22,0.14,MAT["titanium"],"z",16)
    b.cylinder((0,0,1.21),0.13,0.08,MAT["blue"],"z",16)
    b.cylinder((0,0,1.27),0.063,0.06,MAT["warning"],"z",12)
    for i in range(6):
        angle=math.tau*i/6
        x,y=0.70*math.cos(angle),0.70*math.sin(angle)
        b.tapered_spike((x,y,0.28),(x*1.28,y*1.28,0.40),0.045,MAT["bone"],6)
    for x in (-0.34,0.34):
        b.box((x,-0.42,0.54),(0.18,0.12,0.10),MAT["warning"],(0,0,0.6))
    return spawn("Corrosive Bile Landmine", "Defense", b)


def create_sentry():
    b = Builder("21_Automated_Sentry_Turret")
    b.cylinder((0,0,0.18),0.76,0.34,MAT["iron"],"z",24)
    for i in range(8):
        angle=math.tau*i/8
        x,y=0.62*math.cos(angle),0.62*math.sin(angle)
        b.bolt((x,y,0.37),0.074,0.045,MAT["titanium_edge"],"z")
    b.cylinder((0,0,0.48),0.47,0.30,MAT["titanium"],"z",24)
    b.torus((0,0,0.66),0.39,0.07,MAT["warning"],"z",24,8)
    b.cylinder((0,0,1.08),0.29,0.72,MAT["iron_light"],"z",20)
    b.torus((0,0,0.82),0.32,0.052,MAT["iron"],"z",20,8)
    # Servo head and armored cheek plates.
    b.sphere((0,-0.01,1.64),(0.63,0.48,0.41),MAT["titanium"],18,12)
    for side in (-1,1):
        x=side*0.42
        b.box((x,-0.30,1.58),(0.20,0.26,0.48),MAT["titanium_edge"],(0,side*0.13,0))
        b.cylinder((x,-0.46,1.66),0.11,0.08,MAT["plasma"],"y",16)
        b.torus((x,-0.52,1.66),0.13,0.024,MAT["iron_light"],"y",18,6)
    # Twin barrels with vents, muzzle collars and a box magazine.
    for x in (-0.24,0.24):
        b.rod((x,-0.33,1.51),(x,-1.43,1.45),0.105,MAT["iron"],12)
        b.rod((x,-1.10,1.47),(x,-1.53,1.43),0.078,MAT["iron_light"],10)
        b.torus((x,-1.46,1.45),0.088,0.024,MAT["titanium_edge"],"y",16,6)
        for j in range(4):
            b.rod((x-0.055,-0.68-j*0.12,1.56),(x-0.055,-0.68-j*0.12,1.63),0.014,MAT["warning"],5)
    b.box((0.59,0.06,1.40),(0.42,0.62,0.57),MAT["rust"])
    for z in (1.18,1.31,1.44,1.57,1.70):
        b.box((0.59,-0.265,z),(0.22,0.035,0.035),MAT["iron"])
    b.box((0.66,-0.38,1.86),(0.32,0.20,0.12),MAT["blue"])
    for i in range(4):
        b.cylinder((0.54+i*0.08,-0.50,1.87),0.022,0.026,MAT["plasma"],"y",10)
    # Ammo belt and cable harness.
    b.rope_path([(0.40,0.19,1.50),(0.72,0.32,1.22),(0.77,0.17,0.78),(0.42,-0.10,0.68)],0.045,MAT["rust_dark"])
    for i in range(8):
        y=0.22-i*0.11
        b.box((0.32,y,1.42),(0.08,0.09,0.07),MAT["warning"],(0.1,0,0.18))
    return spawn("Automated Sentry Turret", "Defense", b)


def create_shock_tower():
    b = Builder("22_Xenocrystal_Shock_Tower")
    b.cylinder((0,0,0.16),0.78,0.32,MAT["titanium"],"z",24)
    b.cylinder((0,0,0.37),0.60,0.12,MAT["iron_light"],"z",24)
    for i in range(6):
        angle=math.tau*i/6
        x,y=0.55*math.cos(angle),0.55*math.sin(angle)
        b.box((x,y,0.56),(0.20,0.20,0.42),MAT["titanium"],(0,0,angle))
        b.bolt((x,y,0.79),0.06,0.035,MAT["titanium_edge"],"z")
    b.cylinder((0,0,0.80),0.44,0.30,MAT["iron"],"z",20)
    b.torus((0,0,0.97),0.45,0.045,MAT["plasma"],"z",24,8)
    # Three tall emitter arms cradle a jagged crystal cluster.
    for angle in (0,math.tau/3,2*math.tau/3):
        x,y=0.49*math.cos(angle),0.49*math.sin(angle)
        b.rod((x,y,0.80),(x*1.12,y*1.12,2.54),0.080,MAT["titanium_edge"],10)
        b.torus((x*1.12,y*1.12,2.25),0.12,0.027,MAT["iron_light"],"z",18,6)
    b.tapered_spike((0,0,1.18),(0.05,0.01,2.75),0.24,MAT["plasma"],8)
    b.tapered_spike((-0.10,0.05,1.35),(-0.72,0.02,2.26),0.19,MAT["plasma"],7)
    b.tapered_spike((0.11,-0.06,1.42),(0.70,-0.03,2.18),0.18,MAT["plasma"],7)
    b.tapered_spike((0.0,-0.06,1.55),(-0.15,-0.54,2.34),0.15,MAT["blue"],7)
    b.tapered_spike((0.0,0.08,1.52),(0.05,0.45,2.30),0.14,MAT["plasma"],7)
    # Copper induction coils and short zigzag arcs imply stored electrical charge.
    for z in (1.10,1.25,1.40,1.55,1.70):
        b.torus((0,0,z),0.53,0.032,MAT["warning"],"z",24,7)
    for points in (((-0.2,-0.2,1.72),(-0.34,-0.18,1.88),(-0.2,-0.17,2.03),(0.03,-0.15,2.12)),
                   ((0.25,0.12,1.82),(0.42,0.12,1.98),(0.30,0.15,2.13),(0.51,0.10,2.22))):
        b.rope_path(points,0.025,MAT["plasma"])
    b.box((0,-0.60,0.62),(0.40,0.14,0.35),MAT["blue"])
    for i in range(3):
        b.cylinder((-0.10+i*0.10,-0.69,0.65),0.025,0.035,MAT["plasma"],"y",10)
    return spawn("Xenocrystal Shock Tower", "Defense", b)


# Build each recipe item at a clean local origin and export a standalone FBX.
BUILDERS = [
    create_foundation, create_wood_wall, create_window_wall, create_reinforced_door,
    create_stone_brick_wall, create_scrap_wall, create_titanium_wall, create_blast_door,
    create_campfire, create_workbench, create_crate, create_purifier, create_bedroll,
    create_lantern, create_fusion_generator, create_spike_barricade, create_razor_wire,
    create_chitin_wall, create_bear_trap, create_bile_mine, create_sentry, create_shock_tower,
]
for builder_fn in BUILDERS:
    builder_fn()


# Export every item before moving it into a display layout for the Blender source scene.
for obj in ASSETS:
    original_location = obj.location.copy()
    obj.location = (0, 0, 0)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_DIR, obj.name + ".fbx"),
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


# Organize the source scene into three labeled rows for easy asset browsing.
building = [o for o in ASSETS if o.get("category") == "Building"]
utility = [o for o in ASSETS if o.get("category") == "Utility"]
defense = [o for o in ASSETS if o.get("category") == "Defense"]
for group, y, columns in ((building,10.8,4),(utility,0.8,4),(defense,-9.2,4)):
    for index, obj in enumerate(group):
        row, col = divmod(index, columns)
        obj.location = ((col-1.5)*5.6, y-row*4.4, 0)


# A neutral presentation environment is stored in a separate collection and never exported.
PREVIEW_COLLECTION = make_collection("Preview Only | stage, lights and camera", ASSET_ROOT)
GROUND = Builder("Preview Ground")
GROUND.box((0,-1.4,-0.16),(27.5,34.5,0.24),MAT["stage"])
floor_obj = GROUND.mesh(PREVIEW_COLLECTION)
for y in (-16.0,-6.4,3.6,13.4):
    stripe = Builder("Preview Category Divider")
    stripe.box((0,y,-0.025),(26.5,0.045,0.02),MAT["warning"])
    stripe.mesh(PREVIEW_COLLECTION)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()


def setup_camera(location, target, ortho_scale, resolution=(1700,1100)):
    camera_data=bpy.data.cameras.new("BaseItem_Preview_Camera")
    camera=bpy.data.objects.new("BaseItem_Preview_Camera",camera_data)
    PREVIEW_COLLECTION.objects.link(camera)
    camera.location=location
    camera_data.type="ORTHO"
    camera_data.ortho_scale=ortho_scale
    point_at(camera,target)
    scene=bpy.context.scene
    scene.camera=camera
    scene.render.resolution_x=resolution[0]
    scene.render.resolution_y=resolution[1]
    scene.render.resolution_percentage=100
    return camera


scene=bpy.context.scene
scene.render.engine="BLENDER_EEVEE"
scene.eevee.taa_render_samples=48
scene.render.image_settings.file_format="PNG"
scene.render.image_settings.color_mode="RGBA"
scene.render.film_transparent=False
scene.render.resolution_percentage=100
scene.view_settings.view_transform="AgX"
scene.world.color=(0.06,0.07,0.08)
scene.world.use_nodes=True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value=(0.075,0.095,0.11,1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value=0.42

def add_area(name, location, power, size, color):
    data=bpy.data.lights.new(name,"AREA")
    data.energy=power
    data.shape="DISK"
    data.size=size
    data.color=color
    obj=bpy.data.objects.new(name,data)
    PREVIEW_COLLECTION.objects.link(obj)
    obj.location=location
    point_at(obj,(0,0,0.4))
    return obj

add_area("Large softbox | warm front",(1,-11,17),2300,11,(1.0,0.82,0.66))
add_area("Cool fill | left rim",(-14,-1,10),1800,10,(0.50,0.68,1.0))
add_area("Back rim | pale amber",(9,11,13),2400,9,(1.0,0.63,0.34))

# Main showcase. All 22 assets stay visible in the source file and the previews.
main_camera=setup_camera((18,-40,34),(0,-1.4,0.55),55,(2100,1500))
scene.render.filepath=PREVIEW_PATH
bpy.ops.render.render(write_still=True)

# One closer plate per category makes the distinct construction details easier to inspect.
for category, group, target_y in (("Building",building,8.6),("Utility",utility,-1.4),("Defense",defense,-11.4)):
    for other_category, other_collection in CATEGORY_COLLECTIONS.items():
        other_collection.hide_render = other_category != category
    camera=setup_camera((0,target_y-15,12),(0,target_y,1.0),25,(1800,1200))
    scene.render.filepath=CATEGORY_PREVIEWS[category]
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera,do_unlink=True)
scene.camera=main_camera
main_camera.location=(18,-40,34)
main_camera.data.ortho_scale=55
point_at(main_camera,(0,-1.4,0.55))
for category_collection in CATEGORY_COLLECTIONS.values():
    category_collection.hide_render = False

# Keep the source scene ready for inspection in solid/material mode from a useful angle.
bpy.ops.object.select_all(action="DESELECT")
for area in bpy.context.screen.areas:
    if area.type == "VIEW_3D":
        area.spaces.active.region_3d.view_distance=37
        area.spaces.active.region_3d.view_location=(0,-1.4,0.4)
        area.spaces.active.region_3d.view_rotation=Vector((17,-28,23)).to_track_quat("Z","Y")

bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
print("Survivor base models exported:",len(ASSETS),"items to",OUT_DIR)

import bpy
import bmesh
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedModels")
os.makedirs(OUT_DIR, exist_ok=True)
FBX_PATH = os.path.join(OUT_DIR, "MegaphoneGirl_Rigged.fbx")
BLEND_PATH = os.path.join(OUT_DIR, "MegaphoneGirl_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "MegaphoneGirl_Preview.png")


# Start with an empty, deterministic scene.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)


def make_material(name, color, roughness=0.62, metallic=0.0):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = metallic
    return material


MAT = {
    "skin": make_material("Skin | warm coral", (0.98, 0.36, 0.43)),
    "skin_light": make_material("Skin | soft blush", (1.0, 0.52, 0.59)),
    "blush": make_material("Cheek blush", (0.98, 0.22, 0.47)),
    "hair": make_material("Hair | vivid violet", (0.39, 0.19, 0.91), 0.48),
    "hair_light": make_material("Hair | lavender highlights", (0.65, 0.49, 1.0), 0.46),
    "hair_dark": make_material("Hair | deep purple", (0.23, 0.08, 0.53), 0.5),
    "shirt": make_material("Top | sunshine yellow", (1.0, 0.75, 0.08), 0.55),
    "shirt_light": make_material("Top | golden trim", (1.0, 0.9, 0.28), 0.52),
    "shorts": make_material("Shorts | indigo blue", (0.27, 0.25, 0.78), 0.62),
    "shorts_light": make_material("Shorts | lilac trim", (0.58, 0.48, 0.96), 0.58),
    "glove": make_material("Gloves | bright blue", (0.24, 0.37, 0.92), 0.55),
    "glove_light": make_material("Glove stitching", (0.72, 0.8, 1.0), 0.48),
    "red": make_material("Wrist bands | red", (0.78, 0.035, 0.12), 0.54),
    "white": make_material("Small details | ivory", (1.0, 0.91, 0.88), 0.56),
    "dark": make_material("Face details | plum", (0.18, 0.055, 0.12), 0.5),
    "mouth": make_material("Mouth | deep berry", (0.42, 0.025, 0.1), 0.5),
    "tongue": make_material("Tongue", (1.0, 0.22, 0.43), 0.52),
    "horn": make_material("Megaphone | lemon yellow", (1.0, 0.83, 0.035), 0.4, 0.03),
    "horn_light": make_material("Megaphone | bright panels", (1.0, 0.94, 0.25), 0.38, 0.02),
    "horn_dark": make_material("Megaphone | orange rim", (0.96, 0.34, 0.07), 0.42, 0.03),
    "floor": make_material("Preview floor", (0.13, 0.15, 0.22), 0.84),
}

parts = []


def finish_part(obj, name, material, bone, smooth=True):
    obj.name = name
    if material:
        obj.data.materials.append(material)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    group = obj.vertex_groups.new(name=bone)
    if obj.data.vertices:
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    parts.append(obj)
    return obj


def apply_world_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)


def sphere(name, location, scale, material, bone, rotation=(0, 0, 0), segments=18, rings=12, smooth=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.scale = scale
    obj.rotation_euler = rotation
    apply_world_transform(obj)
    return finish_part(obj, name, material, bone, smooth)


def cylinder_between(name, start, end, radius, material, bone, vertices=14, radius_end=None, smooth=True):
    a = Vector(start)
    b = Vector(end)
    axis = b - a
    if radius_end is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=axis.length, location=(a + b) * 0.5)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=radius_end, depth=axis.length, location=(a + b) * 0.5)
    obj = bpy.context.object
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = axis.normalized().to_track_quat("Z", "Y")
    apply_world_transform(obj)
    return finish_part(obj, name, material, bone, smooth)


def ellipsoid_between(name, start, end, width, depth, material, bone, segments=16, rings=10):
    a = Vector(start)
    b = Vector(end)
    axis = b - a
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=(a + b) * 0.5)
    obj = bpy.context.object
    obj.scale = (width, depth, axis.length * 0.56)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = axis.normalized().to_track_quat("Z", "Y")
    apply_world_transform(obj)
    return finish_part(obj, name, material, bone, True)


def strand(name, start, end, radius, material, bone, tip=0.012, vertices=12):
    return cylinder_between(name, start, end, radius, material, bone, vertices, tip, True)


def custom_mesh(name, vertices, faces, material, bone, smooth=False):
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish_part(obj, name, material, bone, smooth)


def torus_x(name, x, y, z, radius, tube, material, bone):
    bpy.ops.mesh.primitive_torus_add(major_segments=24, minor_segments=8, major_radius=radius, minor_radius=tube,
                                     location=(x, y, z), rotation=(0, math.pi / 2, 0))
    obj = bpy.context.object
    apply_world_transform(obj)
    return finish_part(obj, name, material, bone, True)


def horn_mesh():
    # Open, flared megaphone shell, built along the character's left-to-right axis.
    center_y, center_z = -0.56, 2.08
    rings = [(0.24, 0.105), (0.37, 0.135), (1.25, 0.335)]
    segments = 28
    verts = []
    faces = []
    for x, radius in rings:
        for i in range(segments):
            angle = (2.0 * math.pi * i) / segments
            verts.append((x, center_y + math.cos(angle) * radius, center_z + math.sin(angle) * radius))
    for ring in range(len(rings) - 1):
        for i in range(segments):
            a = ring * segments + i
            b = ring * segments + (i + 1) % segments
            c = (ring + 1) * segments + (i + 1) % segments
            d = (ring + 1) * segments + i
            faces.append((a, b, c, d))
    return custom_mesh("Megaphone_FlaredShell", verts, faces, MAT["horn"], "LeftHand", True)


# Rig: standard humanoid naming, with a small Root control above the ground.
arm_data = bpy.data.armatures.new("MegaphoneGirl_Rig")
armature = bpy.data.objects.new("MegaphoneGirl_Rig", arm_data)
bpy.context.collection.objects.link(armature)
bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
eb = arm_data.edit_bones


def bone(name, head, tail, parent=None, connected=False):
    item = eb.new(name)
    item.head = head
    item.tail = tail
    if parent:
        item.parent = eb[parent]
        item.use_connect = connected
    return item


bone("Root", (0, 0, 0.02), (0, 0, 0.92))
bone("Hips", (0, 0, 0.92), (0, 0, 1.22), "Root")
bone("Spine", (0, 0, 1.12), (0, 0, 1.56), "Hips")
bone("Chest", (0, 0, 1.49), (0, 0, 1.88), "Spine")
bone("Neck", (0, 0, 1.85), (0, 0, 2.08), "Chest")
bone("Head", (0, 0, 2.02), (0, 0, 2.65), "Neck")

bone("LeftShoulder", (0.34, 0, 1.82), (0.51, 0, 1.79), "Chest")
bone("LeftUpperArm", (0.51, 0, 1.79), (0.68, -0.06, 1.62), "LeftShoulder", True)
bone("LeftLowerArm", (0.68, -0.06, 1.62), (0.43, -0.48, 2.02), "LeftUpperArm", True)
bone("LeftHand", (0.43, -0.48, 2.02), (0.61, -0.63, 2.06), "LeftLowerArm", True)

bone("RightShoulder", (-0.34, 0, 1.82), (-0.49, 0, 1.78), "Chest")
bone("RightUpperArm", (-0.49, 0, 1.78), (-0.87, -0.04, 1.53), "RightShoulder", True)
bone("RightLowerArm", (-0.87, -0.04, 1.53), (-0.67, -0.12, 1.22), "RightUpperArm", True)
bone("RightHand", (-0.67, -0.12, 1.22), (-0.79, -0.25, 1.11), "RightLowerArm", True)

bone("LeftUpperLeg", (0.18, 0, 1.11), (0.25, 0, 0.59), "Hips")
bone("LeftLowerLeg", (0.25, 0, 0.59), (0.25, -0.01, 0.19), "LeftUpperLeg", True)
bone("LeftFoot", (0.25, -0.01, 0.19), (0.25, -0.28, 0.10), "LeftLowerLeg", True)
bone("RightUpperLeg", (-0.18, 0, 1.11), (-0.24, 0, 0.59), "Hips")
bone("RightLowerLeg", (-0.24, 0, 0.59), (-0.24, -0.01, 0.19), "RightUpperLeg", True)
bone("RightFoot", (-0.24, -0.01, 0.19), (-0.24, -0.28, 0.10), "RightLowerLeg", True)
bpy.ops.object.mode_set(mode="OBJECT")
armature.show_in_front = True
arm_data.display_type = "OCTAHEDRAL"


# Lower body: the missing legs are added as bare pink legs and blue ankle boots.
sphere("Shorts_Base", (0, 0.015, 1.08), (0.39, 0.29, 0.24), MAT["shorts"], "Hips")
sphere("Shorts_Waistband", (0, -0.01, 1.245), (0.38, 0.30, 0.075), MAT["shorts_light"], "Hips")
sphere("Shorts_BeltCenter", (0, -0.305, 1.205), (0.105, 0.035, 0.065), MAT["horn_dark"], "Hips", segments=14, rings=8)
sphere("Exposed_Midriff", (0, 0.0, 1.39), (0.30, 0.225, 0.22), MAT["skin"], "Spine")

for side, x, thigh, calf, foot in [
    ("Left", 0.20, "LeftUpperLeg", "LeftLowerLeg", "LeftFoot"),
    ("Right", -0.20, "RightUpperLeg", "RightLowerLeg", "RightFoot"),
]:
    hip = (x, 0.0, 1.02)
    knee = (x * 1.12, 0.005, 0.59)
    ankle = (x * 1.12, -0.015, 0.20)
    ellipsoid_between(side + "_Thigh", hip, knee, 0.135, 0.155, MAT["skin"], thigh)
    sphere(side + "_Knee", knee, (0.128, 0.145, 0.13), MAT["skin"], calf)
    ellipsoid_between(side + "_Calf", knee, ankle, 0.102, 0.12, MAT["skin"], calf)
    sphere(side + "_Ankle", ankle, (0.105, 0.12, 0.105), MAT["skin_light"], foot)
    # Short, chunky blue boots with a pale cuff and darker sole.
    sphere(side + "_Boot", (x * 1.12, -0.115, 0.12), (0.145, 0.255, 0.135), MAT["glove"], foot)
    sphere(side + "_BootToe", (x * 1.12, -0.275, 0.105), (0.15, 0.14, 0.095), MAT["shorts_light"], foot)
    cylinder_between(side + "_BootCuff", (x * 1.12, -0.02, 0.19), (x * 1.12, -0.02, 0.27), 0.12, MAT["shorts_light"], foot, 16)
    sphere(side + "_Sole", (x * 1.12, -0.16, 0.045), (0.15, 0.26, 0.045), MAT["shorts"], foot)


# Torso and crop top, with a visible strip of midriff like the reference.
sphere("Torso_Skin", (0, 0.015, 1.66), (0.405, 0.275, 0.34), MAT["skin"], "Chest")
sphere("Yellow_CropTop", (0, -0.015, 1.70), (0.425, 0.29, 0.275), MAT["shirt"], "Chest")
sphere("Top_Hem", (0, -0.018, 1.48), (0.405, 0.285, 0.055), MAT["shirt_light"], "Chest")
sphere("Top_Collar", (0, -0.02, 1.925), (0.20, 0.20, 0.055), MAT["shirt_light"], "Chest")
# Simple shoulder straps and a small dark triangular emblem.
for x in (-0.24, 0.24):
    strand("Top_Strap", (x, -0.16, 1.86), (x * 0.82, -0.23, 1.97), 0.055, MAT["shirt"], "Chest", 0.048, 10)
sphere("Top_Emblem", (0, -0.301, 1.69), (0.095, 0.018, 0.075), MAT["horn_dark"], "Chest", segments=12, rings=8)


# Neck and oversized chibi head.
cylinder_between("Neck", (0, 0, 1.82), (0, 0, 2.10), 0.17, MAT["skin"], "Neck", 16, 0.15)
sphere("Head", (0, 0.0, 2.42), (0.565, 0.47, 0.625), MAT["skin"], "Head", segments=24, rings=16)
sphere("Ear_L", (-0.54, -0.025, 2.34), (0.105, 0.115, 0.15), MAT["skin_light"], "Head", segments=14, rings=10)
sphere("Ear_R", (0.54, -0.025, 2.34), (0.105, 0.115, 0.15), MAT["skin_light"], "Head", segments=14, rings=10)

# Closed, sharply angled eyes, with cheek color and a wide open shouting mouth.
for eye_side, x in [("L", -0.27), ("R", 0.27)]:
    y = -0.423
    strand("ClosedEye_" + eye_side + "_A", (x - 0.105, y, 2.42), (x, y - 0.012, 2.385), 0.026, MAT["dark"], "Head", 0.018, 10)
    strand("ClosedEye_" + eye_side + "_B", (x, y - 0.012, 2.385), (x + 0.12, y, 2.405), 0.022, MAT["dark"], "Head", 0.015, 10)
    sphere("Cheek_" + eye_side, (x * 1.38, -0.405, 2.22), (0.12, 0.035, 0.07), MAT["blush"], "Head", segments=14, rings=8)
sphere("Mouth_Outer", (0.015, -0.443, 2.13), (0.205, 0.06, 0.205), MAT["dark"], "Head", segments=18, rings=12)
sphere("Mouth_Inside", (0.015, -0.493, 2.145), (0.167, 0.035, 0.156), MAT["mouth"], "Head", segments=18, rings=12)
sphere("Tongue", (0.035, -0.525, 2.075), (0.102, 0.025, 0.047), MAT["tongue"], "Head", segments=14, rings=8)


# Hair cap, layered bangs, side locks, and a high tied tuft.
sphere("Hair_Cap", (0, 0.045, 2.79), (0.60, 0.50, 0.42), MAT["hair"], "Head", segments=20, rings=14)
sphere("Hair_BackMass", (0, 0.30, 2.45), (0.59, 0.32, 0.53), MAT["hair_dark"], "Head", segments=18, rings=12)
ellipsoid_between("Bang_Sweep_Main", (-0.45, -0.27, 2.83), (-0.22, -0.49, 2.23), 0.24, 0.13, MAT["hair"], "Head")
ellipsoid_between("Bang_Sweep_Light", (-0.21, -0.37, 2.85), (0.02, -0.51, 2.34), 0.18, 0.105, MAT["hair_light"], "Head")
ellipsoid_between("Bang_Sweep_Right", (0.12, -0.31, 2.84), (0.39, -0.44, 2.31), 0.19, 0.11, MAT["hair"], "Head")
ellipsoid_between("Side_Lock_L", (-0.52, -0.10, 2.67), (-0.60, -0.25, 2.02), 0.17, 0.13, MAT["hair_light"], "Head")
ellipsoid_between("Side_Lock_R", (0.50, -0.08, 2.63), (0.57, -0.18, 2.10), 0.16, 0.12, MAT["hair_dark"], "Head")
strand("Crown_Spike_1", (-0.28, 0.02, 2.92), (-0.52, 0.04, 3.40), 0.22, MAT["hair_light"], "Head", 0.018, 14)
strand("Crown_Spike_2", (-0.12, 0.04, 2.96), (0.05, 0.02, 3.53), 0.19, MAT["hair"], "Head", 0.012, 14)
strand("Crown_Spike_3", (0.05, 0.10, 2.92), (0.43, 0.16, 3.28), 0.19, MAT["hair_light"], "Head", 0.012, 14)
strand("Crown_Spike_4", (0.22, 0.15, 2.88), (0.66, 0.20, 3.04), 0.15, MAT["hair_dark"], "Head", 0.01, 12)
sphere("Hair_Tie", (-0.35, 0.17, 2.94), (0.16, 0.13, 0.075), MAT["red"], "Head", segments=14, rings=8)
strand("Ponytail_Tuft_A", (-0.37, 0.20, 2.95), (-0.73, 0.27, 3.20), 0.18, MAT["hair"], "Head", 0.016, 12)
strand("Ponytail_Tuft_B", (-0.37, 0.22, 2.95), (-0.76, 0.25, 3.00), 0.16, MAT["hair_light"], "Head", 0.012, 12)


# Right arm is bent down to the hip, with a red cuff and blue glove.
ellipsoid_between("Right_UpperArm", (-0.43, 0.0, 1.80), (-0.84, -0.04, 1.55), 0.16, 0.17, MAT["skin"], "RightUpperArm")
sphere("Right_Elbow", (-0.84, -0.04, 1.55), (0.15, 0.16, 0.15), MAT["skin"], "RightLowerArm")
ellipsoid_between("Right_Forearm", (-0.84, -0.04, 1.55), (-0.67, -0.12, 1.23), 0.135, 0.145, MAT["skin"], "RightLowerArm")
cylinder_between("Right_Cuff", (-0.68, -0.12, 1.33), (-0.64, -0.13, 1.23), 0.145, MAT["red"], "RightHand", 16, 0.14)
sphere("Right_Glove", (-0.72, -0.19, 1.13), (0.19, 0.16, 0.18), MAT["glove"], "RightHand")
sphere("Right_Glove_Palm", (-0.76, -0.255, 1.12), (0.14, 0.075, 0.13), MAT["glove_light"], "RightHand")
for i in range(3):
    x = -0.86 + i * 0.075
    strand("Right_Glove_Stitch", (x, -0.33, 1.07), (x + 0.04, -0.33, 1.16), 0.014, MAT["shorts_light"], "RightHand", 0.012, 8)


# Left arm reaches up and curls around the megaphone.
ellipsoid_between("Left_UpperArm", (0.41, 0.0, 1.80), (0.66, -0.08, 1.62), 0.16, 0.17, MAT["skin"], "LeftUpperArm")
sphere("Left_Elbow", (0.66, -0.08, 1.62), (0.15, 0.16, 0.15), MAT["skin"], "LeftLowerArm")
ellipsoid_between("Left_Forearm", (0.66, -0.08, 1.62), (0.43, -0.48, 2.02), 0.125, 0.14, MAT["skin"], "LeftLowerArm")
sphere("Left_HandPalm", (0.48, -0.63, 2.10), (0.17, 0.10, 0.14), MAT["skin_light"], "LeftHand")
for i in range(4):
    x = 0.38 + i * 0.075
    sphere("Grip_Finger_" + str(i + 1), (x, -0.72, 2.17), (0.052, 0.065, 0.10), MAT["skin"], "LeftHand", rotation=(0.0, 0.12, -0.10), segments=12, rings=8)


# Megaphone and grip handle. Everything in the accessory follows LeftHand.
horn_mesh()
torus_x("Horn_Outer_Rim", 1.245, -0.56, 2.08, 0.335, 0.045, MAT["horn_dark"], "LeftHand")
torus_x("Horn_Inset_Rim", 1.205, -0.56, 2.08, 0.305, 0.018, MAT["horn_light"], "LeftHand")
torus_x("Horn_Accent_Band", 1.02, -0.56, 2.08, 0.26, 0.025, MAT["red"], "LeftHand")
cylinder_between("Horn_Grip", (0.47, -0.56, 1.95), (0.55, -0.56, 1.78), 0.065, MAT["horn_dark"], "LeftHand", 12, 0.055)
sphere("Horn_GripEnd", (0.55, -0.56, 1.77), (0.11, 0.09, 0.055), MAT["horn_light"], "LeftHand", segments=12, rings=8)


# Join all colored pieces into one skinned character mesh, retaining per-bone groups.
bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.join()
character = bpy.context.object
character.name = "MegaphoneGirl_Mesh"
character.data.name = "MegaphoneGirl_Geometry"
modifier = character.modifiers.new("Character Skin", "ARMATURE")
modifier.object = armature
modifier.use_deform_preserve_volume = True
character.parent = armature
character.matrix_parent_inverse = armature.matrix_world.inverted()
character.show_wire = False
character.show_all_edges = False


# Preview-only floor, camera and soft studio lights (not included in FBX export).
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.02))
floor = bpy.context.object
floor.name = "Preview_Floor"
floor.data.materials.append(MAT["floor"])

def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add(location=(3.8, -8.2, 3.35))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 56
point_at(camera, (0.10, -0.02, 1.72))
bpy.context.scene.camera = camera

def area_light(name, location, power, size, target):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = power
    light.data.shape = "DISK"
    light.data.size = size
    point_at(light, target)
    return light

area_light("Key_Light", (2.5, -4.5, 6.0), 520, 4.0, (0, 0, 1.8))
area_light("Fill_Light", (-4.0, -2.5, 3.2), 300, 3.8, (0, 0, 1.8))
area_light("Rim_Light", (0.0, 3.5, 5.0), 600, 3.0, (0, 0, 2.0))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys() else "BLENDER_EEVEE"
scene.render.resolution_x = 900
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.world.color = (0.045, 0.055, 0.09)
scene.view_settings.view_transform = "AgX"
scene.render.filepath = PREVIEW_PATH
scene.camera.data.dof.use_dof = False
scene.render.image_settings.color_depth = "8"

# Save an editable source file with the preview setup.
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)

# Render preview before selecting only the exportable objects.
bpy.ops.render.render(write_still=True)

# Export a Unity-friendly FBX with the armature and weighted character mesh.
bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
character.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_armature_deform_only=True,
    add_leaf_bones=False,
    bake_anim=False,
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="AUTO",
    use_custom_props=True,
)

print("Generated:", FBX_PATH)
print("Source:", BLEND_PATH)
print("Preview:", PREVIEW_PATH)
print("Rig bones:", len(arm_data.bones), "Mesh vertices:", len(character.data.vertices), "Mesh polygons:", len(character.data.polygons))

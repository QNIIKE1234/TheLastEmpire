import bpy
import bmesh
import math
import os
from mathutils import Vector


ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedModels")
os.makedirs(OUT_DIR, exist_ok=True)
FBX_PATH = os.path.join(OUT_DIR, "Chibi_Base_Rigged_Staging.fbx")
BLEND_PATH = os.path.join(OUT_DIR, "Chibi_Base_Source.blend")
PREVIEW_PATH = os.path.join(OUT_DIR, "Chibi_Base_Preview.png")


# Clear the scene so the result is reproducible.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights, bpy.data.armatures):
    for datablock in list(datablocks):
        if datablock.users == 0:
            datablocks.remove(datablock)


def make_material(name, color, roughness=0.78):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (*color, 1.0)
    material.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = roughness
    return material


CLAY = make_material("Base | matte white clay", (0.82, 0.83, 0.85), 0.76)
FLOOR_MAT = make_material("Preview | charcoal gray", (0.12, 0.125, 0.14), 0.9)
raw_parts = []


def apply_world_transform(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)


def add_sphere(name, location, scale, rotation=(0, 0, 0), segments=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rotation
    apply_world_transform(obj)
    raw_parts.append(obj)
    return obj


def add_capsule(name, start, end, radius_x, radius_y=None, segments=20, rings=12):
    a = Vector(start)
    b = Vector(end)
    axis = b - a
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=(a + b) * 0.5)
    obj = bpy.context.object
    obj.name = name
    obj.scale = (radius_x, radius_y if radius_y is not None else radius_x, axis.length * 0.62)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = axis.normalized().to_track_quat("Z", "Y")
    apply_world_transform(obj)
    raw_parts.append(obj)
    return obj


def add_profile_mesh(name, profiles, axis, sign=1.0, center=0.0, radial_segments=32, steps=5):
    """Build a smoothly varying elliptical tube from a simple cross-section profile."""
    samples = []
    for left, right in zip(profiles, profiles[1:]):
        for step in range(steps):
            t = step / steps
            samples.append(tuple(a * (1.0 - t) + b * t for a, b in zip(left, right)))
    samples.append(profiles[-1])

    vertices = []
    for profile in samples:
        if axis == "z":
            height, offset_x, radius_x, radius_y = profile
            for ring in range(radial_segments):
                angle = 2.0 * math.pi * ring / radial_segments
                vertices.append((center + sign * offset_x + radius_x * math.cos(angle),
                                 radius_y * math.sin(angle), height))
        else:
            distance, radius_y, radius_z = profile
            for ring in range(radial_segments):
                angle = 2.0 * math.pi * ring / radial_segments
                vertices.append((sign * distance, radius_y * math.sin(angle),
                                 center + radius_z * math.cos(angle)))

    faces = []
    for section in range(len(samples) - 1):
        start = section * radial_segments
        next_start = (section + 1) * radial_segments
        for ring in range(radial_segments):
            next_ring = (ring + 1) % radial_segments
            faces.append((start + ring, start + next_ring,
                          next_start + next_ring, next_start + ring))
    faces.append(tuple(range(radial_segments - 1, -1, -1)))
    end_start = (len(samples) - 1) * radial_segments
    faces.append(tuple(end_start + ring for ring in range(radial_segments)))

    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    raw_parts.append(obj)
    return obj


# A compact, neutral chibi mannequin: oversized blank head and a small body.
# Blender Z is up; the face/front is toward negative Y. Continuous profile meshes
# avoid the stacked-sphere look at shoulders, elbows, waist, knees, and wrists.
add_profile_mesh("Torso_Continuous", [
    (0.76, 0.00, 0.045, 0.050), (0.83, 0.00, 0.18, 0.135),
    (0.92, 0.00, 0.275, 0.185), (1.00, 0.00, 0.275, 0.190),
    (1.10, 0.00, 0.235, 0.170), (1.20, 0.00, 0.225, 0.165),
    (1.30, 0.00, 0.255, 0.180), (1.40, 0.00, 0.305, 0.200),
    (1.49, 0.00, 0.275, 0.180), (1.56, 0.00, 0.185, 0.145),
    (1.64, 0.00, 0.125, 0.115), (1.78, 0.00, 0.120, 0.115),
    (1.86, 0.00, 0.075, 0.075),
], axis="z", steps=4)
# Rounded, continuous arms with mitten-like hands in a true T-pose.
for side, sign in (("Left", 1.0), ("Right", -1.0)):
    add_profile_mesh(side + "_Arm_Continuous", [
        (0.22, 0.045, 0.055), (0.31, 0.125, 0.135),
        (0.42, 0.140, 0.145), (0.56, 0.125, 0.130),
        (0.70, 0.108, 0.112), (0.83, 0.092, 0.098),
        (0.95, 0.073, 0.078), (1.05, 0.068, 0.070),
        (1.12, 0.080, 0.078), (1.20, 0.097, 0.082),
        (1.28, 0.082, 0.070), (1.34, 0.042, 0.045),
        (1.36, 0.008, 0.010),
    ], axis="x", sign=sign, center=1.555, steps=4)

# Short legs and small simple feet.
for side, sign in (("Left", 1.0), ("Right", -1.0)):
    add_profile_mesh(side + "_Leg_Continuous", [
        (0.105, 0.19, 0.060, 0.070), (0.16, 0.19, 0.082, 0.090),
        (0.26, 0.19, 0.088, 0.098), (0.36, 0.185, 0.098, 0.108),
        (0.48, 0.18, 0.116, 0.125), (0.58, 0.175, 0.132, 0.140),
        (0.68, 0.17, 0.145, 0.150), (0.78, 0.165, 0.148, 0.150),
        (0.88, 0.16, 0.136, 0.140), (0.95, 0.16, 0.100, 0.105),
    ], axis="z", sign=sign, steps=4)
    add_sphere(side + "_Foot", (sign * 0.19, -0.105, 0.085), (0.105, 0.19, 0.085))


# Create a standard-named humanoid skeleton in the same T-pose.
arm_data = bpy.data.armatures.new("Chibi_Base_Armature")
armature = bpy.data.objects.new("Chibi_Base_Rig", arm_data)
bpy.context.collection.objects.link(armature)
bpy.context.view_layer.objects.active = armature
armature.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
edit_bones = arm_data.edit_bones
segments = {}


def make_bone(name, head, tail, parent=None, connected=False):
    item = edit_bones.new(name)
    item.head = head
    item.tail = tail
    if parent:
        item.parent = edit_bones[parent]
        item.use_connect = connected
    segments[name] = (Vector(head), Vector(tail))
    return item


make_bone("Root", (0, 0, 0.02), (0, 0, 0.80))
make_bone("Hips", (0, 0, 0.80), (0, 0, 1.06), "Root")
make_bone("Spine", (0, 0, 1.02), (0, 0, 1.31), "Hips")
make_bone("Chest", (0, 0, 1.28), (0, 0, 1.57), "Spine")
make_bone("Neck", (0, 0, 1.56), (0, 0, 1.86), "Chest")
make_bone("Head", (0, 0, 1.78), (0, 0, 2.52), "Neck")

make_bone("LeftShoulder", (0.25, 0, 1.555), (0.39, 0, 1.555), "Chest")
make_bone("LeftUpperArm", (0.39, 0, 1.555), (0.72, 0, 1.555), "LeftShoulder", True)
make_bone("LeftLowerArm", (0.72, 0, 1.555), (1.10, -0.005, 1.555), "LeftUpperArm", True)
make_bone("LeftHand", (1.10, -0.005, 1.555), (1.29, -0.02, 1.555), "LeftLowerArm", True)

make_bone("RightShoulder", (-0.25, 0, 1.555), (-0.39, 0, 1.555), "Chest")
make_bone("RightUpperArm", (-0.39, 0, 1.555), (-0.72, 0, 1.555), "RightShoulder", True)
make_bone("RightLowerArm", (-0.72, 0, 1.555), (-1.10, -0.005, 1.555), "RightUpperArm", True)
make_bone("RightHand", (-1.10, -0.005, 1.555), (-1.29, -0.02, 1.555), "RightLowerArm", True)

make_bone("LeftUpperLeg", (0.16, 0, 0.91), (0.185, 0, 0.49), "Hips")
make_bone("LeftLowerLeg", (0.185, 0, 0.49), (0.19, -0.005, 0.14), "LeftUpperLeg", True)
make_bone("LeftFoot", (0.19, -0.005, 0.14), (0.19, -0.24, 0.085), "LeftLowerLeg", True)
make_bone("RightUpperLeg", (-0.16, 0, 0.91), (-0.185, 0, 0.49), "Hips")
make_bone("RightLowerLeg", (-0.185, 0, 0.49), (-0.19, -0.005, 0.14), "RightUpperLeg", True)
make_bone("RightFoot", (-0.19, -0.005, 0.14), (-0.19, -0.24, 0.085), "RightLowerLeg", True)
bpy.ops.object.mode_set(mode="OBJECT")
armature.show_in_front = True
arm_data.display_type = "OCTAHEDRAL"


# Join the overlapping volumes and remesh them into a single, smooth clay surface.
bpy.ops.object.select_all(action="DESELECT")
for obj in raw_parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = raw_parts[0]
bpy.ops.object.join()
body = bpy.context.object
body.name = "Chibi_Base_Mesh"
body.data.name = "Chibi_Base_Sculpt"
body.data.remesh_voxel_size = 0.018
body.data.remesh_voxel_adaptivity = 0.0
bpy.ops.object.voxel_remesh()

smooth = body.modifiers.new("Clay surface smoothing", "SMOOTH")
smooth.factor = 0.82
smooth.iterations = 12
bpy.context.view_layer.objects.active = body
bpy.ops.object.modifier_apply(modifier=smooth.name)
subd = body.modifiers.new("Surface finish", "SUBSURF")
subd.subdivision_type = "CATMULL_CLARK"
subd.levels = 1
subd.render_levels = 1
bpy.ops.object.modifier_apply(modifier=subd.name)
for poly in body.data.polygons:
    poly.use_smooth = True
body.data.materials.clear()
body.data.materials.append(CLAY)


def point_segment_distance(point, start, end):
    line = end - start
    denom = line.length_squared
    if denom <= 1e-10:
        return (point - start).length
    t = max(0.0, min(1.0, (point - start).dot(line) / denom))
    return (point - (start + line * t)).length


# Smooth nearest-bone weights, blended around the joints rather than rigid parts.
deform_names = [name for name in segments if name not in ("Root", "Head")]
groups = {name: body.vertex_groups.new(name=name) for name in deform_names}
buckets = {name: {} for name in deform_names}
for index, vertex in enumerate(body.data.vertices):
    point = vertex.co
    ranked = sorted((point_segment_distance(point, *segments[name]), name) for name in deform_names)
    nearest = ranked[0][0]
    influences = [(dist, name) for dist, name in ranked[:3] if dist <= nearest + 0.14]
    if not influences:
        influences = [ranked[0]]
    raw = [math.exp(-(dist - nearest) / 0.075) for dist, _ in influences]
    total = sum(raw)
    for value, (_, name) in zip(raw, influences):
        weight = round(value / total, 2)
        if weight > 0:
            bucket = buckets[name].setdefault(weight, [])
            bucket.append(index)

for name, by_weight in buckets.items():
    group = groups[name]
    for weight, indices in by_weight.items():
        group.add(indices, weight, "REPLACE")

arm_mod = body.modifiers.new("Humanoid skin", "ARMATURE")
arm_mod.object = armature
arm_mod.use_deform_preserve_volume = False
body.parent = armature
body.matrix_parent_inverse = armature.matrix_world.inverted()

# The head is a separate, mathematically round high-resolution sphere. This
# keeps the silhouette spherical and lets the Humanoid Head bone move it rigidly.
bpy.ops.mesh.primitive_uv_sphere_add(segments=64, ring_count=48, location=(0, 0.0, 2.13))
head = bpy.context.object
head.name = "Chibi_Base_Head"
head.scale = (0.56, 0.56, 0.56)
apply_world_transform(head)
head.data.materials.append(CLAY)
for poly in head.data.polygons:
    poly.use_smooth = True
head_group = head.vertex_groups.new(name="Head")
head_group.add(list(range(len(head.data.vertices))), 1.0, "REPLACE")
head_mod = head.modifiers.new("Head follows Humanoid bone", "ARMATURE")
head_mod.object = armature
head.parent = armature
head.matrix_parent_inverse = armature.matrix_world.inverted()


def build_loop_action(name, samples):
    """Create a looping action; each sample is (frame, rotations, hips_bob)."""
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    pose_names = [pb.name for pb in armature.pose.bones]
    for frame, rotations, hips_bob in samples:
        bpy.context.scene.frame_set(frame)
        for pb in armature.pose.bones:
            pb.rotation_mode = "XYZ"
            pb.rotation_euler = rotations.get(pb.name, (0.0, 0.0, 0.0))
            pb.location = (0.0, 0.0, 0.0)
        armature.pose.bones["Hips"].location.z = hips_bob
        for pb in armature.pose.bones:
            pb.keyframe_insert(data_path="rotation_euler", frame=frame, group=pb.name)
        armature.pose.bones["Hips"].keyframe_insert(data_path="location", frame=frame, group="Hips")
    return action


def pose(**kwargs):
    return {name: tuple(math.radians(v) for v in values) for name, values in kwargs.items()}


ANIMATIONS = []
ANIMATIONS.append(build_loop_action("Idle", [
    (1, pose(LeftUpperArm=(0, 6, -65), RightUpperArm=(0, -6, 65), LeftLowerArm=(12, 0, -5), RightLowerArm=(12, 0, 5)), 0.0),
    (16, pose(Chest=(-1.5, 0, 0), Head=(0, 0, 1.5), LeftUpperArm=(0, 7, -64), RightUpperArm=(0, -7, 64), LeftLowerArm=(12, 0, -5), RightLowerArm=(12, 0, 5)), 0.012),
    (31, pose(LeftUpperArm=(0, 6, -65), RightUpperArm=(0, -6, 65), LeftLowerArm=(12, 0, -5), RightLowerArm=(12, 0, 5)), 0.0),
    (46, pose(Chest=(1.5, 0, 0), Head=(0, 0, -1.5), LeftUpperArm=(0, 5, -66), RightUpperArm=(0, -5, 66), LeftLowerArm=(12, 0, -5), RightLowerArm=(12, 0, 5)), 0.012),
    (61, pose(LeftUpperArm=(0, 6, -65), RightUpperArm=(0, -6, 65), LeftLowerArm=(12, 0, -5), RightLowerArm=(12, 0, 5)), 0.0),
]))

ANIMATIONS.append(build_loop_action("Walk", [
    (1, pose(LeftUpperLeg=(18, 0, 0), RightUpperLeg=(-18, 0, 0), LeftLowerLeg=(2, 0, 0), RightLowerLeg=(20, 0, 0), LeftUpperArm=(18, 0, -64), RightUpperArm=(-18, 0, 64), LeftLowerArm=(15, 0, -5), RightLowerArm=(15, 0, 5)), 0.015),
    (7, pose(LeftUpperLeg=(0, 0, 0), RightUpperLeg=(0, 0, 0), LeftLowerLeg=(22, 0, 0), RightLowerLeg=(22, 0, 0), LeftUpperArm=(0, 0, -66), RightUpperArm=(0, 0, 66), LeftLowerArm=(18, 0, -5), RightLowerArm=(18, 0, 5)), 0.035),
    (13, pose(LeftUpperLeg=(-18, 0, 0), RightUpperLeg=(18, 0, 0), LeftLowerLeg=(20, 0, 0), RightLowerLeg=(2, 0, 0), LeftUpperArm=(-18, 0, -64), RightUpperArm=(18, 0, 64), LeftLowerArm=(15, 0, -5), RightLowerArm=(15, 0, 5)), 0.015),
    (19, pose(LeftUpperLeg=(0, 0, 0), RightUpperLeg=(0, 0, 0), LeftLowerLeg=(22, 0, 0), RightLowerLeg=(22, 0, 0), LeftUpperArm=(0, 0, -66), RightUpperArm=(0, 0, 66), LeftLowerArm=(18, 0, -5), RightLowerArm=(18, 0, 5)), 0.035),
    (25, pose(LeftUpperLeg=(18, 0, 0), RightUpperLeg=(-18, 0, 0), LeftLowerLeg=(2, 0, 0), RightLowerLeg=(20, 0, 0), LeftUpperArm=(18, 0, -64), RightUpperArm=(-18, 0, 64), LeftLowerArm=(15, 0, -5), RightLowerArm=(15, 0, 5)), 0.015),
]))

ANIMATIONS.append(build_loop_action("Run", [
    (1, pose(Chest=(-5, 0, 0), LeftUpperLeg=(32, 0, 0), RightUpperLeg=(-32, 0, 0), LeftLowerLeg=(10, 0, 0), RightLowerLeg=(44, 0, 0), LeftUpperArm=(25, 0, -58), RightUpperArm=(-25, 0, 58), LeftLowerArm=(35, 0, -5), RightLowerArm=(35, 0, 5)), 0.035),
    (5, pose(Chest=(-5, 0, 0), LeftUpperLeg=(0, 0, 0), RightUpperLeg=(0, 0, 0), LeftLowerLeg=(40, 0, 0), RightLowerLeg=(40, 0, 0), LeftUpperArm=(0, 0, -62), RightUpperArm=(0, 0, 62), LeftLowerArm=(40, 0, -5), RightLowerArm=(40, 0, 5)), 0.085),
    (9, pose(Chest=(-5, 0, 0), LeftUpperLeg=(-32, 0, 0), RightUpperLeg=(32, 0, 0), LeftLowerLeg=(44, 0, 0), RightLowerLeg=(10, 0, 0), LeftUpperArm=(-25, 0, -58), RightUpperArm=(25, 0, 58), LeftLowerArm=(35, 0, -5), RightLowerArm=(35, 0, 5)), 0.035),
    (13, pose(Chest=(-5, 0, 0), LeftUpperLeg=(0, 0, 0), RightUpperLeg=(0, 0, 0), LeftLowerLeg=(40, 0, 0), RightLowerLeg=(40, 0, 0), LeftUpperArm=(0, 0, -62), RightUpperArm=(0, 0, 62), LeftLowerArm=(40, 0, -5), RightLowerArm=(40, 0, 5)), 0.085),
    (17, pose(Chest=(-5, 0, 0), LeftUpperLeg=(32, 0, 0), RightUpperLeg=(-32, 0, 0), LeftLowerLeg=(10, 0, 0), RightLowerLeg=(44, 0, 0), LeftUpperArm=(25, 0, -58), RightUpperArm=(-25, 0, 58), LeftLowerArm=(35, 0, -5), RightLowerArm=(35, 0, 5)), 0.035),
]))

# Two-handed aiming pose with a gentle breathing loop; a weapon can be attached to RightHand.
ANIMATIONS.append(build_loop_action("GunHold", [
    (1, pose(Chest=(-3, 0, 0), LeftUpperArm=(0, 0, -45), LeftLowerArm=(0, 0, -120), RightUpperArm=(0, 0, 45), RightLowerArm=(0, 0, 120), LeftHand=(0, 0, -5), RightHand=(0, 0, 5)), 0.0),
    (16, pose(Chest=(-2, 0, 0), LeftUpperArm=(0, 0, -45), LeftLowerArm=(0, 0, -120), RightUpperArm=(0, 0, 45), RightLowerArm=(0, 0, 120), LeftHand=(0, 0, -5), RightHand=(0, 0, 5)), 0.012),
    (31, pose(Chest=(-3, 0, 0), LeftUpperArm=(0, 0, -45), LeftLowerArm=(0, 0, -120), RightUpperArm=(0, 0, 45), RightLowerArm=(0, 0, 120), LeftHand=(0, 0, -5), RightHand=(0, 0, 5)), 0.0),
]))

# Keep the named clips in the editable .blend and open it with Idle selected.
armature.animation_data.action = ANIMATIONS[0]
bpy.context.scene.frame_set(1)


# Preview-only studio setup. The exported FBX contains just the mesh and rig.
bpy.ops.mesh.primitive_plane_add(size=2000, location=(0, 0, -0.035))
floor = bpy.context.object
floor.name = "Preview_Floor"
floor.data.materials.append(FLOOR_MAT)


def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


bpy.ops.object.camera_add(location=(2.0, -8.0, 2.75))
camera = bpy.context.object
camera.name = "Preview_Camera"
camera.data.lens = 72
point_at(camera, (0, 0, 1.35))
bpy.context.scene.camera = camera


def area_light(name, location, power, size, target):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = power
    light.data.shape = "DISK"
    light.data.size = size
    point_at(light, target)


area_light("Key_Light", (2.5, -4.5, 5.4), 420, 3.6, (0, 0, 1.3))
area_light("Fill_Light", (-4.0, -2.5, 2.8), 250, 3.5, (0, 0, 1.3))
area_light("Rim_Light", (0.0, 3.0, 4.0), 380, 3.0, (0, 0, 1.5))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys() else "BLENDER_EEVEE"
scene.render.resolution_x = 800
scene.render.resolution_y = 800
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.render.film_transparent = False
scene.world.color = (0.045, 0.05, 0.065)
scene.view_settings.view_transform = "Standard"
scene.render.filepath = PREVIEW_PATH

armature.animation_data.action = ANIMATIONS[0]
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
bpy.ops.render.render(write_still=True)

# Save a second preview of the aiming pose for easy review.
scene.render.filepath = os.path.join(OUT_DIR, "Chibi_GunHold_Preview.png")
armature.animation_data.action = next(action for action in ANIMATIONS if action.name == "GunHold")
scene.frame_set(16)
bpy.context.view_layer.update()
bpy.ops.render.render(write_still=True)
armature.animation_data.action = ANIMATIONS[0]
scene.frame_set(1)

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
body.select_set(True)
head.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.export_scene.fbx(
    filepath=FBX_PATH,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    use_armature_deform_only=True,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    axis_forward="-Z",
    axis_up="Y",
    apply_scale_options="FBX_SCALE_ALL",
    path_mode="AUTO",
    use_custom_props=True,
)

print("Generated:", FBX_PATH)
print("Source:", BLEND_PATH)
print("Preview:", PREVIEW_PATH)
print("Animations:", [action.name for action in ANIMATIONS])
print("Rig bones:", len(arm_data.bones), "Body vertices:", len(body.data.vertices), "Body polygons:", len(body.data.polygons))

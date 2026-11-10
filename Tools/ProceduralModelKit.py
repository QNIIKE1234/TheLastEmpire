"""
Procedural 3D Model Kit for The Last Empire
Provides procedural geometry construction and export helpers for Blender 3.6+.
"""

import math
import os
import bpy
from mathutils import Vector, Euler, Matrix


def clear_scene():
    """Wipes all objects, meshes, and unused datablocks from the Blender scene."""
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (
        bpy.data.meshes,
        bpy.data.curves,
        bpy.data.cameras,
        bpy.data.lights,
        bpy.data.armatures,
    ):
        for datablock in list(datablocks):
            if datablock.users == 0:
                datablocks.remove(datablock)


def create_material(name, color_rgb, roughness=0.7, metallic=0.0, emission_rgb=None, emission_strength=0.0):
    """Creates a Principled BSDF material in Blender with explicit diffuse and shader parameters."""
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color_rgb, 1.0)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color_rgb, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if emission_rgb is not None and emission_strength > 0.0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = (*emission_rgb, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


class EnemyMeshBuilder:
    def __init__(self, name="Model"):
        self.name = name
        self.vertices = []
        self.faces = []
        self.material_indices = []
        self.smooth_faces = []

    def _add_face(self, indices, mat_idx, smooth=False):
        idx = tuple(indices)
        if len(set(idx)) < 3:
            return
        self.faces.append(idx)
        self.material_indices.append(mat_idx)
        self.smooth_faces.append(smooth)

    def _transform_pt(self, pt, center, rot_euler):
        v = Vector(pt)
        if rot_euler is not None and any(rot_euler):
            v.rotate(Euler(rot_euler, "XYZ"))
        return (v.x + center[0], v.y + center[1], v.z + center[2])

    def add_box(self, center, size, mat_idx, rot=(0, 0, 0), smooth=False, mirror_x=False):
        """Adds a box centered at 'center' with dimensions 'size' (dx, dy, dz)."""
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        local_verts = [
            (-hx, -hy, -hz),
            (hx, -hy, -hz),
            (hx, hy, -hz),
            (-hx, hy, -hz),
            (-hx, -hy, hz),
            (hx, -hy, hz),
            (hx, hy, hz),
            (-hx, hy, hz),
        ]
        box_faces = [
            (0, 1, 2, 3),  # bottom (-Z)
            (4, 7, 6, 5),  # top (+Z)
            (0, 4, 5, 1),  # front (-Y)
            (2, 6, 7, 3),  # back (+Y)
            (0, 3, 7, 4),  # left (-X)
            (1, 5, 6, 2),  # right (+X)
        ]

        def _emit(c, r, flip_normals=False):
            base_idx = len(self.vertices)
            for lv in local_verts:
                self.vertices.append(self._transform_pt(lv, c, r))
            for f in box_faces:
                face = tuple(base_idx + i for i in f)
                if flip_normals:
                    face = face[::-1]
                self._add_face(face, mat_idx, smooth)

        _emit(center, rot, False)
        if mirror_x and abs(center[0]) > 0.001:
            c_mir = (-center[0], center[1], center[2])
            r_mir = (rot[0], -rot[1], -rot[2])
            _emit(c_mir, r_mir, True)

    def add_sphere(self, center, radii, mat_idx, segs=12, rings=8, rot=(0, 0, 0), smooth=True, mirror_x=False):
        """Adds an ellipsoid/sphere at 'center' with radii (rx, ry, rz)."""
        rx, ry, rz = radii

        def _emit(c, r, flip_normals=False):
            base_idx = len(self.vertices)
            # South pole
            self.vertices.append(self._transform_pt((0, 0, -rz), c, r))
            # Ring vertices
            for ring in range(1, rings):
                phi = -math.pi * 0.5 + math.pi * ring / rings
                z = rz * math.sin(phi)
                ring_r = math.cos(phi)
                for seg in range(segs):
                    theta = 2.0 * math.pi * seg / segs
                    x = rx * ring_r * math.cos(theta)
                    y = ry * ring_r * math.sin(theta)
                    self.vertices.append(self._transform_pt((x, y, z), c, r))
            # North pole
            self.vertices.append(self._transform_pt((0, 0, rz), c, r))
            north_pole = base_idx + 1 + (rings - 1) * segs

            # South cap
            for seg in range(segs):
                nxt = (seg + 1) % segs
                f = (base_idx, base_idx + 1 + seg, base_idx + 1 + nxt)
                if flip_normals:
                    f = f[::-1]
                self._add_face(f, mat_idx, smooth)

            # Middle bands
            for ring in range(1, rings - 1):
                r0 = base_idx + 1 + (ring - 1) * segs
                r1 = base_idx + 1 + ring * segs
                for seg in range(segs):
                    nxt = (seg + 1) % segs
                    f = (r0 + seg, r1 + seg, r1 + nxt, r0 + nxt)
                    if flip_normals:
                        f = f[::-1]
                    self._add_face(f, mat_idx, smooth)

            # North cap
            top_ring = base_idx + 1 + (rings - 2) * segs
            for seg in range(segs):
                nxt = (seg + 1) % segs
                f = (top_ring + seg, north_pole, top_ring + nxt)
                if flip_normals:
                    f = f[::-1]
                self._add_face(f, mat_idx, smooth)

        _emit(center, rot, False)
        if mirror_x and abs(center[0]) > 0.001:
            c_mir = (-center[0], center[1], center[2])
            r_mir = (rot[0], -rot[1], -rot[2])
            _emit(c_mir, r_mir, True)

    def add_cylinder(self, p1, p2, radius, mat_idx, segs=10, smooth=True, cap1=True, cap2=True, mirror_x=False):
        """Adds a cylinder between 3D points p1 and p2."""
        self.add_tapered_cylinder(
            p1, p2, radius, radius, mat_idx, segs=segs, smooth=smooth, cap1=cap1, cap2=cap2, mirror_x=mirror_x
        )

    def add_tapered_cylinder(
        self, p1, p2, r1, r2, mat_idx, segs=10, smooth=True, cap1=True, cap2=True, mirror_x=False
    ):
        """Adds a tapered cylinder (frustum) from p1 (radius r1) to p2 (radius r2)."""

        def _emit(v1, v2, rad1, rad2, flip_normals=False):
            axis = Vector(v2) - Vector(v1)
            length = axis.length
            if length < 1e-6:
                return
            z_axis = axis.normalized()
            # Construct orthogonal frame
            ref = Vector((0, 0, 1)) if abs(z_axis.z) < 0.9 else Vector((0, 1, 0))
            x_axis = z_axis.cross(ref).normalized()
            y_axis = z_axis.cross(x_axis).normalized()

            base_idx = len(self.vertices)
            # Bottom ring at v1
            for i in range(segs):
                ang = 2.0 * math.pi * i / segs
                offset = (x_axis * math.cos(ang) + y_axis * math.sin(ang)) * rad1
                self.vertices.append(tuple(Vector(v1) + offset))

            # Top ring at v2
            for i in range(segs):
                ang = 2.0 * math.pi * i / segs
                offset = (x_axis * math.cos(ang) + y_axis * math.sin(ang)) * rad2
                self.vertices.append(tuple(Vector(v2) + offset))

            # Side quads
            for i in range(segs):
                nxt = (i + 1) % segs
                f = (base_idx + i, base_idx + nxt, base_idx + segs + nxt, base_idx + segs + i)
                if flip_normals:
                    f = f[::-1]
                self._add_face(f, mat_idx, smooth)

            # Cap 1 (bottom)
            if cap1 and rad1 > 1e-5:
                cap_c = len(self.vertices)
                self.vertices.append(tuple(v1))
                for i in range(segs):
                    nxt = (i + 1) % segs
                    f = (cap_c, base_idx + nxt, base_idx + i)
                    if flip_normals:
                        f = f[::-1]
                    self._add_face(f, mat_idx, False)

            # Cap 2 (top)
            if cap2 and rad2 > 1e-5:
                cap_c = len(self.vertices)
                self.vertices.append(tuple(v2))
                for i in range(segs):
                    nxt = (i + 1) % segs
                    f = (cap_c, base_idx + segs + i, base_idx + segs + nxt)
                    if flip_normals:
                        f = f[::-1]
                    self._add_face(f, mat_idx, False)

        _emit(p1, p2, r1, r2, False)
        if mirror_x and (abs(p1[0]) > 0.001 or abs(p2[0]) > 0.001):
            p1_mir = (-p1[0], p1[1], p1[2])
            p2_mir = (-p2[0], p2[1], p2[2])
            _emit(p1_mir, p2_mir, r1, r2, True)

    def add_cone(self, base_center, tip, radius, mat_idx, segs=10, smooth=False, cap=True, mirror_x=False):
        """Adds a cone with circular base at base_center and vertex at tip."""
        self.add_tapered_cylinder(
            base_center, tip, radius, 0.0, mat_idx, segs=segs, smooth=smooth, cap1=cap, cap2=False, mirror_x=mirror_x
        )

    def add_capsule(self, p1, p2, radius, mat_idx, segs=10, smooth=True, mirror_x=False):
        """Adds a capsule (cylinder with spherical hemispherical ends)."""
        self.add_cylinder(p1, p2, radius, mat_idx, segs=segs, smooth=smooth, cap1=False, cap2=False, mirror_x=mirror_x)
        self.add_sphere(p1, (radius, radius, radius), mat_idx, segs=segs, rings=6, smooth=smooth, mirror_x=mirror_x)
        self.add_sphere(p2, (radius, radius, radius), mat_idx, segs=segs, rings=6, smooth=smooth, mirror_x=mirror_x)

    def add_wedge(self, center, size, mat_idx, rot=(0, 0, 0), smooth=False, mirror_x=False):
        """Adds a wedge (ramp/prism) centered at center with dimensions (dx, dy, dz). Slope goes up towards +Y."""
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        local_verts = [
            (-hx, -hy, -hz),  # 0 bottom-front-left
            (hx, -hy, -hz),  # 1 bottom-front-right
            (hx, hy, -hz),  # 2 bottom-back-right
            (-hx, hy, -hz),  # 3 bottom-back-left
            (-hx, hy, hz),  # 4 top-back-left
            (hx, hy, hz),  # 5 top-back-right
        ]
        wedge_faces = [
            (0, 1, 2, 3),  # bottom (-Z)
            (0, 3, 4),  # left (-X) triangle
            (1, 5, 2),  # right (+X) triangle
            (2, 5, 4, 3),  # back (+Y)
            (0, 4, 5, 1),  # sloped front (+Z / -Y)
        ]

        def _emit(c, r, flip_normals=False):
            base_idx = len(self.vertices)
            for lv in local_verts:
                self.vertices.append(self._transform_pt(lv, c, r))
            for f in wedge_faces:
                face = tuple(base_idx + i for i in f)
                if flip_normals:
                    face = face[::-1]
                self._add_face(face, mat_idx, smooth)

        _emit(center, rot, False)
        if mirror_x and abs(center[0]) > 0.001:
            c_mir = (-center[0], center[1], center[2])
            r_mir = (rot[0], -rot[1], -rot[2])
            _emit(c_mir, r_mir, True)

    def add_plate(self, p1, p2, p3, p4, thickness, mat_idx, smooth=False, mirror_x=False):
        """Adds a flat quad slab extruded by thickness along its normal."""

        def _emit(a, b, c, d, flip_normals=False):
            v_a, v_b, v_c = Vector(a), Vector(b), Vector(c)
            norm = (v_b - v_a).cross(v_c - v_a).normalized() * (thickness * 0.5)
            pts_front = [tuple(Vector(p) + norm) for p in (a, b, c, d)]
            pts_back = [tuple(Vector(p) - norm) for p in (a, b, c, d)]
            base_idx = len(self.vertices)
            self.vertices.extend(pts_front)  # 0,1,2,3
            self.vertices.extend(pts_back)  # 4,5,6,7
            faces = [
                (0, 1, 2, 3),  # front
                (7, 6, 5, 4),  # back
                (0, 4, 5, 1),  # bottom
                (1, 5, 6, 2),  # right
                (2, 6, 7, 3),  # top
                (3, 7, 4, 0),  # left
            ]
            for f in faces:
                face = tuple(base_idx + i for i in f)
                if flip_normals:
                    face = face[::-1]
                self._add_face(face, mat_idx, smooth)

        _emit(p1, p2, p3, p4, False)
        if mirror_x and any(abs(p[0]) > 0.001 for p in (p1, p2, p3, p4)):
            p1_m = (-p1[0], p1[1], p1[2])
            p2_m = (-p2[0], p2[1], p2[2])
            p3_m = (-p3[0], p3[1], p3[2])
            p4_m = (-p4[0], p4[1], p4[2])
            _emit(p1_m, p2_m, p3_m, p4_m, True)

    def add_curved_blade(self, spine_points, widths, thicknesses, mat_idx, smooth=True, mirror_x=False):
        """Builds a swept tapered curved scythe/blade along spine points."""

        def _emit(pts, wds, thks, flip_normals=False):
            n = len(pts)
            if n < 2:
                return
            base_idx = len(self.vertices)
            # Create a diamond cross-section at each spine point
            for i, pt in enumerate(pts):
                v_pt = Vector(pt)
                # Tangent along spine
                if i < n - 1:
                    tangent = (Vector(pts[i + 1]) - v_pt).normalized()
                else:
                    tangent = (v_pt - Vector(pts[i - 1])).normalized()
                ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.9 else Vector((0, 1, 0))
                edge_dir = tangent.cross(ref).normalized()
                flat_dir = tangent.cross(edge_dir).normalized()

                w = wds[i]
                t = thks[i]
                # 4 diamond vertices: sharp back, right flat, sharp front, left flat
                v0 = tuple(v_pt - edge_dir * (w * 0.5))
                v1 = tuple(v_pt + flat_dir * (t * 0.5))
                v2 = tuple(v_pt + edge_dir * (w * 0.5))
                v3 = tuple(v_pt - flat_dir * (t * 0.5))
                self.vertices.extend([v0, v1, v2, v3])

            # Bridge rings
            for i in range(n - 1):
                r0 = base_idx + i * 4
                r1 = base_idx + (i + 1) * 4
                for k in range(4):
                    nxt = (k + 1) % 4
                    f = (r0 + k, r1 + k, r1 + nxt, r0 + nxt)
                    if flip_normals:
                        f = f[::-1]
                    self._add_face(f, mat_idx, smooth)

        _emit(spine_points, widths, thicknesses, False)
        if mirror_x and any(abs(p[0]) > 0.001 for p in spine_points):
            pts_m = [(-p[0], p[1], p[2]) for p in spine_points]
            _emit(pts_m, widths, thicknesses, True)

    def build_and_export(self, filepath, materials, preview_render=None):
        """Constructs Blender Mesh and Object, attaches materials, and exports to OBJ."""
        os.makedirs(os.path.dirname(os.path.abspath(filepath)), exist_ok=True)
        clear_scene()

        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()

        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)

        for mat in materials:
            obj.data.materials.append(mat)

        for poly, m_idx, sm in zip(mesh.polygons, self.material_indices, self.smooth_faces):
            poly.material_index = m_idx
            poly.use_smooth = sm

        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)

        # Export OBJ with standard +Y up, +Z forward
        bpy.ops.wm.obj_export(
            filepath=filepath,
            export_selected_objects=True,
            forward_axis="Z",
            up_axis="Y",
            export_materials=True,
            export_normals=True,
            export_colors=True,
        )
        print(f"[ProceduralModelKit] Successfully generated: {filepath} ({len(self.vertices)} verts, {len(self.faces)} faces)")

        # Generate a Unity .obj.meta file if not already present
        meta_path = filepath + ".meta"
        if not os.path.exists(meta_path):
            import uuid
            guid_hex = uuid.uuid5(uuid.NAMESPACE_DNS, os.path.basename(filepath)).hex
            write_unity_obj_meta(meta_path, guid_hex)

        mtl_path = os.path.splitext(filepath)[0] + ".mtl"
        mtl_meta = mtl_path + ".meta"
        if os.path.exists(mtl_path) and not os.path.exists(mtl_meta):
            import uuid
            guid_hex_mtl = uuid.uuid5(uuid.NAMESPACE_DNS, os.path.basename(mtl_path)).hex
            write_unity_mtl_meta(mtl_meta, guid_hex_mtl)

        return obj


def write_unity_obj_meta(meta_path, guid):
    """Writes a standardized Unity ModelImporter .meta file for OBJ assets."""
    content = f"""fileFormatVersion: 2
guid: {guid}
ModelImporter:
  serializedVersion: 24200
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 2
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    removeConstantScaleCurves: 0
    motionNodeName: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 0
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useSRGBMaterialColor: 1
    sortHierarchyByName: 1
    importPhysicalCameras: 1
    importVisibility: 1
    importBlendShapes: 1
    importCameras: 1
    importLights: 1
    nodeNameCollisionStrategy: 1
    fileIdsGeneration: 2
    swapUVChannels: 0
    generateSecondaryUV: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 0
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    generateMeshLods: 0
    meshLodGenerationFlags: 0
    maximumMeshLod: -1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
    strictVertexDataChecks: 0
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 1
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{instanceID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 2
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  importBlendShapeDeformPercent: 1
  remapMaterialsIfMaterialImportModeIsNone: 0
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(content)


def write_unity_mtl_meta(meta_path, guid):
    """Writes a standardized Unity DefaultImporter .meta file for MTL assets."""
    content = f"""fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    with open(meta_path, "w", encoding="utf-8") as f:
        f.write(content)

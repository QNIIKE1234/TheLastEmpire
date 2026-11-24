import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "GeneratedModels", "Furniture")
os.makedirs(OUT_DIR, exist_ok=True)
PREVIEW_PATH = os.path.join(OUT_DIR, "Furniture_LowPoly_Preview.png")


def reset_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
        for db in list(datablocks):
            if db.users == 0:
                datablocks.remove(db)


def make_mat(name, color, roughness=0.78, metallic=0.0, emission=None, emission_strength=0.0):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1.0)
    m.use_nodes = True
    nodes = m.node_tree.nodes
    links = m.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*color, 1.0)
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if emission is not None and emission_strength > 0.0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
            elif "Emission" in bsdf.inputs:
                bsdf.inputs["Emission"].default_value = (*emission, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
        # Match Create_Survivor_Base_Assets.py subtle surface grain on organic/weathered materials
        if any(k in name for k in ("Wood", "Oak", "Caramel", "Rust", "Leather", "Cloth", "Terracotta", "Soil", "Red", "Blue", "Mint")):
            noise = nodes.new("ShaderNodeTexNoise")
            noise.inputs["Scale"].default_value = 7.5
            noise.inputs["Detail"].default_value = 3.0
            bump = nodes.new("ShaderNodeBump")
            bump.inputs["Strength"].default_value = 0.12
            bump.inputs["Distance"].default_value = 0.035
            links.new(noise.outputs["Fac"], bump.inputs["Height"])
            links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return m


def build_palette():
    """
    Post-apocalyptic weathered survival palette matched 1-to-1 with Create_Survivor_Base_Assets.py
    (01_Wooden_Floor .. 23_Wooden_Lantern_Post): weathered cedar/oak, forged dark iron, scraped steel,
    rusted brass/warning amber, muted canvas/leather/crimson, and punchy lantern/plasma glow accents.
    """
    return {
        "honey_wood":    make_mat("Wood_ExposedGrain",  (0.46, 0.29, 0.145), 0.80, 0.0),
        "warm_oak":      make_mat("Wood_WeatheredCedar",(0.31, 0.18, 0.090), 0.84, 0.0),
        "dark_wood":     make_mat("Wood_CharredStain",  (0.14, 0.10, 0.070), 0.88, 0.0),
        "caramel":       make_mat("Wood_MahoganyWorn",  (0.36, 0.19, 0.095), 0.80, 0.0),
        "locker_steel":  make_mat("Iron_ScrapedSteel",  (0.32, 0.36, 0.38),  0.42, 0.72),
        "silver":        make_mat("Iron_BrushedSteel",  (0.44, 0.48, 0.50),  0.35, 0.80),
        "chrome":        make_mat("Iron_BevelChrome",   (0.58, 0.63, 0.65),  0.26, 0.88),
        "dark_iron":     make_mat("Iron_DarkForged",    (0.12, 0.15, 0.17),  0.44, 0.78),
        "olive_metal":   make_mat("Iron_OliveScrap",    (0.26, 0.30, 0.24),  0.54, 0.55),
        "gold_yellow":   make_mat("Brass_WornWarning",  (0.76, 0.48, 0.11),  0.50, 0.45),
        "lamp_yellow":   make_mat("Glow_LanternShade",  (0.92, 0.62, 0.16),  0.45, 0.0, (0.98, 0.54, 0.10), 1.15),
        "bulb_glow":     make_mat("Glow_WarmAmberCore", (1.00, 0.82, 0.40),  0.22, 0.0, (1.00, 0.66, 0.18), 2.30),
        "plush_red":     make_mat("Cloth_WornCrimson",  (0.48, 0.15, 0.13),  0.90, 0.0),
        "deep_red":      make_mat("Cloth_DeepOxblood",  (0.30, 0.09, 0.08),  0.92, 0.0),
        "red_cross":     make_mat("Paint_MedicalRed",   (0.72, 0.14, 0.12),  0.64, 0.05),
        "sky_blue":      make_mat("Cloth_FadedBlue",    (0.16, 0.32, 0.44),  0.88, 0.0),
        "deep_blue":     make_mat("Cloth_NavyTrim",     (0.10, 0.21, 0.31),  0.90, 0.0),
        "tv_blue":       make_mat("Glow_ScreenCyan",    (0.08, 0.54, 0.78),  0.22, 0.20, (0.04, 0.42, 0.85), 1.75),
        "gas_blue":      make_mat("Glow_GasPlasma",     (0.08, 0.62, 0.94),  0.20, 0.15, (0.04, 0.48, 1.00), 2.40),
        "fire_orange":   make_mat("Glow_EmberOrange",   (1.00, 0.30, 0.04),  0.28, 0.0, (1.00, 0.16, 0.01), 2.40),
        "fire_yellow":   make_mat("Glow_FlameGold",     (1.00, 0.72, 0.16),  0.22, 0.0, (1.00, 0.58, 0.08), 2.30),
        "mint_fridge":   make_mat("Paint_WeatheredMint",(0.36, 0.52, 0.46),  0.48, 0.28),
        "warm_white":    make_mat("Cloth_PatchedCanvas",(0.62, 0.58, 0.48),  0.82, 0.0),
        "crisp_white":   make_mat("Paint_WornEnamel",   (0.74, 0.73, 0.68),  0.60, 0.05),
        "bandage_tan":   make_mat("Cloth_FieldBandage", (0.64, 0.52, 0.34),  0.92, 0.0),
        "leather":       make_mat("Leather_WornSaddle", (0.34, 0.18, 0.10),  0.74, 0.0),
        "leather_dark":  make_mat("Leather_DarkHide",   (0.20, 0.11, 0.07),  0.84, 0.0),
        "pcb_green":     make_mat("Tech_CircuitGreen",  (0.14, 0.42, 0.22),  0.48, 0.25),
        "led_green":     make_mat("Glow_ToxicPhosphor", (0.36, 0.84, 0.14),  0.25, 0.0, (0.18, 0.68, 0.05), 1.85),
        "book_purple":   make_mat("Cloth_MutedPlum",    (0.34, 0.18, 0.38),  0.88, 0.0),
        "book_green":    make_mat("Cloth_FieldOlive",   (0.22, 0.34, 0.20),  0.88, 0.0),
        "soil":          make_mat("Soil_DarkEarth",     (0.16, 0.11, 0.07),  0.96, 0.0),
        "leaf_dark":     make_mat("Foliage_DeepMoss",   (0.14, 0.32, 0.13),  0.80, 0.0),
        "leaf_bright":   make_mat("Foliage_WarmLeaf",   (0.28, 0.48, 0.16),  0.76, 0.0),
        "terracotta":    make_mat("Terracotta_Clay",    (0.52, 0.24, 0.14),  0.86, 0.0),
        "lime":          make_mat("Crop_FreshLime",     (0.42, 0.68, 0.14),  0.56, 0.0),
        "potato":        make_mat("Crop_EarthPotato",   (0.58, 0.43, 0.22),  0.86, 0.0),
        "tomato":        make_mat("Crop_RipeTomato",    (0.72, 0.15, 0.11),  0.48, 0.0),
        "carrot":        make_mat("Crop_SweetCarrot",   (0.78, 0.34, 0.08),  0.68, 0.0),
        "strawberry":    make_mat("Crop_RedBerry",      (0.74, 0.14, 0.18),  0.52, 0.0),
    }


class LowPolyBuilder:
    """
    Faceted, flat-shaded low-poly bmesh builder.
    Coordinate system in Blender before FBX export (axis_forward='-Z', axis_up='Y'):
      X = Right,  Y = Back (+Y is rear/wall, -Y is Front toward camera!),  Z = Up (0 = floor).
    """
    def __init__(self, name, palette):
        self.name = name
        self.palette = palette
        self.bm = bmesh.new()
        self.mat_list = []
        self.mat_idx = {}

    def _m(self, key):
        if key not in self.mat_idx:
            self.mat_idx[key] = len(self.mat_list)
            self.mat_list.append(self.palette[key])
        return self.mat_idx[key]

    def _xform(self, center, rot_deg=(0, 0, 0)):
        rot = Euler((math.radians(rot_deg[0]), math.radians(rot_deg[1]), math.radians(rot_deg[2])), "XYZ").to_matrix().to_4x4()
        return Matrix.Translation(center) @ rot

    def _tag_faces(self, geom, mat_key):
        mi = self._m(mat_key)
        for el in geom:
            if isinstance(el, bmesh.types.BMFace):
                el.material_index = mi
                el.smooth = False

    def box(self, center, size, mat_key, rot_deg=(0, 0, 0), bevel=0.0):
        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        sx, sy, sz = size
        for v in verts:
            v.co.x *= sx
            v.co.y *= sy
            v.co.z *= sz
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            b_ret = bmesh.ops.bevel(
                self.bm, geom=edges, offset=bevel, segments=1,
                affect="EDGES", clamp_overlap=True
            )
            verts = [v for v in (set(verts) | set(b_ret.get("verts", []))) if v.is_valid]
        xf = self._xform(center, rot_deg)
        bmesh.ops.transform(self.bm, matrix=xf, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_faces(faces, mat_key)

    def tapered_box(self, center, size_bot, size_top, height, mat_key, rot_deg=(0, 0, 0), bevel=0.0):
        ret = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = ret["verts"]
        bx, by = size_bot
        tx, ty = size_top
        for v in verts:
            if v.co.z > 0:
                v.co.x *= tx
                v.co.y *= ty
                v.co.z = height * 0.5
            else:
                v.co.x *= bx
                v.co.y *= by
                v.co.z = -height * 0.5
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            b_ret = bmesh.ops.bevel(
                self.bm, geom=edges, offset=bevel, segments=1,
                affect="EDGES", clamp_overlap=True
            )
            verts = [v for v in (set(verts) | set(b_ret.get("verts", []))) if v.is_valid]
        xf = self._xform(center, rot_deg)
        bmesh.ops.transform(self.bm, matrix=xf, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_faces(faces, mat_key)

    def cone(self, center, r_bot, r_top, height, mat_key, segments=8, rot_deg=(0, 0, 0)):
        ret = bmesh.ops.create_cone(
            self.bm, cap_ends=True, cap_tris=False, segments=segments,
            radius1=r_bot, radius2=r_top, depth=height
        )
        verts = ret["verts"]
        xf = self._xform(center, rot_deg)
        bmesh.ops.transform(self.bm, matrix=xf, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_faces(faces, mat_key)

    def ico(self, center, scale, mat_key, subdiv=1, rot_deg=(0, 0, 0)):
        ret = bmesh.ops.create_icosphere(self.bm, subdivisions=subdiv, radius=0.5)
        verts = ret["verts"]
        sx, sy, sz = scale
        for v in verts:
            v.co.x *= sx
            v.co.y *= sy
            v.co.z *= sz
        xf = self._xform(center, rot_deg)
        bmesh.ops.transform(self.bm, matrix=xf, verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._tag_faces(faces, mat_key)

    def arch_crown(self, center, width, depth, height, mat_key, steps=6):
        """Builds a faceted low-poly curved arch crown along X, extruded along Y."""
        hw = width * 0.5
        hd = depth * 0.5
        bm = self.bm
        front_verts = []
        back_verts = []
        # Bottom corners + curved top arc
        pts = [(-hw, 0.0)]
        for i in range(steps + 1):
            t = i / steps
            ang = math.pi * (1.0 - t)
            x = math.cos(ang) * hw
            z = height * (0.35 + 0.65 * math.sin(ang))
            pts.append((x, z))
        pts.append((hw, 0.0))
        for x, z in pts:
            front_verts.append(bm.verts.new((center[0] + x, center[1] - hd, center[2] + z)))
            back_verts.append(bm.verts.new((center[0] + x, center[1] + hd, center[2] + z)))
        created = []
        created.append(bm.faces.new(front_verts))
        created.append(bm.faces.new(list(reversed(back_verts))))
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            created.append(bm.faces.new((front_verts[i], front_verts[j], back_verts[j], back_verts[i])))
        bmesh.ops.recalc_face_normals(bm, faces=created)
        self._tag_faces(created, mat_key)

    def tube_path(self, points, radius, mat_key, segments=6):
        """Connects 3D points with faceted low-poly cylinder segments + icosphere elbow caps."""
        for i in range(len(points) - 1):
            p0 = Vector(points[i])
            p1 = Vector(points[i + 1])
            d = p1 - p0
            length = d.length
            if length < 1e-4:
                continue
            mid = (p0 + p1) * 0.5
            q = Vector((0, 0, 1)).rotation_difference(d.normalized())
            ret = bmesh.ops.create_cone(
                self.bm, cap_ends=True, cap_tris=False, segments=segments,
                radius1=radius, radius2=radius, depth=length
            )
            verts = ret["verts"]
            xf = Matrix.Translation(mid) @ q.to_matrix().to_4x4()
            bmesh.ops.transform(self.bm, matrix=xf, verts=verts)
            faces = list({f for v in verts for f in v.link_faces})
            self._tag_faces(faces, mat_key)
        for p in points[1:-1]:
            self.ico(p, (radius * 2.15, radius * 2.15, radius * 2.15), mat_key, subdiv=1)

    def legs(self, span_x, span_y, height, thick_bot, thick_top, mat_key):
        hx, hy = span_x * 0.5, span_y * 0.5
        for sx in (-1, 1):
            for sy in (-1, 1):
                self.tapered_box(
                    (sx * hx, sy * hy, height * 0.5),
                    (thick_bot, thick_bot), (thick_top, thick_top),
                    height, mat_key, bevel=0.008
                )

    def build_object(self, parent=None):
        mesh = bpy.data.meshes.new(self.name + "_Mesh")
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mat_list:
            mesh.materials.append(m)
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.collection.objects.link(obj)
        if parent is not None:
            obj.parent = parent
        return obj


# ==============================================================================
# 30 LOW-POLY FURNITURE BUILDERS (matching the user's 30 icon designs!)
# Convention: -Y = Front (camera-facing), +Y = Back (against wall), +Z = Up
# ==============================================================================

def build_01_wardrobe(p):
    b = LowPolyBuilder("BUILD0024_WoodenWardrobe", p)
    b.legs(1.14, 0.58, 0.14, 0.09, 0.13, "warm_oak")
    b.box((0, 0, 0.17), (1.36, 0.74, 0.11), "honey_wood", bevel=0.025)
    b.box((0, 0.02, 1.00), (1.28, 0.68, 1.56), "warm_oak", bevel=0.03)
    # Recessed twin doors on -Y front
    b.box((-0.30, -0.34, 1.00), (0.56, 0.07, 1.44), "honey_wood", bevel=0.02)
    b.box((0.30, -0.34, 1.00), (0.56, 0.07, 1.44), "honey_wood", bevel=0.02)
    b.box((-0.30, -0.375, 1.00), (0.40, 0.03, 1.20), "warm_oak", bevel=0.01)
    b.box((0.30, -0.375, 1.00), (0.40, 0.03, 1.20), "warm_oak", bevel=0.01)
    # Center seam + twin golden knobs
    b.box((0, -0.36, 1.00), (0.03, 0.04, 1.44), "dark_wood")
    b.ico((-0.09, -0.41, 0.96), (0.11, 0.09, 0.11), "gold_yellow", subdiv=1)
    b.ico((0.09, -0.41, 0.96), (0.11, 0.09, 0.11), "gold_yellow", subdiv=1)
    # Signature arched low-poly cornice crown + dark-iron corner brackets (Survivor Craft style)
    b.arch_crown((0, 0.0, 1.76), 1.42, 0.78, 0.30, "honey_wood", steps=6)
    for sx in (-1, 1):
        b.box((sx * 0.65, -0.35, 1.72), (0.12, 0.08, 0.14), "dark_iron", bevel=0.015)
        b.box((sx * 0.65, -0.35, 0.28), (0.12, 0.08, 0.14), "dark_iron", bevel=0.015)
    return [b.build_object()]


def build_02_locker(p):
    b = LowPolyBuilder("BUILD0025_MetalLockerCabinet", p)
    b.legs(0.82, 0.54, 0.10, 0.09, 0.11, "dark_iron")
    b.box((0, 0.02, 1.02), (1.02, 0.70, 1.82), "locker_steel", bevel=0.06)
    b.box((0, -0.34, 1.02), (0.86, 0.05, 1.66), "silver", bevel=0.025)
    # 3 crisp top vent slits
    for i in range(3):
        b.box((0.02, -0.37, 1.62 - i * 0.11), (0.44, 0.025, 0.045), "dark_iron")
    # Right hinges
    b.box((0.46, -0.35, 1.48), (0.08, 0.07, 0.18), "dark_iron", bevel=0.015)
    b.box((0.46, -0.35, 0.56), (0.08, 0.07, 0.18), "dark_iron", bevel=0.015)
    # Yellow padlock with curved silver U-shackle on left
    b.box((-0.29, -0.37, 1.08), (0.11, 0.06, 0.18), "dark_iron", bevel=0.01)
    b.tube_path([(-0.35, -0.42, 0.96), (-0.35, -0.42, 1.08), (-0.29, -0.42, 1.13), (-0.23, -0.42, 1.08), (-0.23, -0.42, 0.96)], 0.026, "chrome", segments=6)
    b.box((-0.29, -0.42, 0.86), (0.24, 0.11, 0.24), "gold_yellow", bevel=0.03)
    b.box((-0.29, -0.48, 0.85), (0.045, 0.02, 0.09), "dark_iron")
    return [b.build_object()]


def build_03_carpentry_table(p):
    b = LowPolyBuilder("BUILD0026_CarpentryCraftTable", p)
    b.legs(1.30, 0.68, 0.84, 0.10, 0.13, "warm_oak")
    b.box((0, 0, 0.28), (1.28, 0.54, 0.09), "warm_oak", bevel=0.018)
    b.box((0, 0, 0.76), (1.34, 0.72, 0.12), "warm_oak", bevel=0.015)
    b.box((0, 0, 0.89), (1.58, 0.92, 0.14), "honey_wood", bevel=0.03)
    # Iron corner gussets matching 10_Survivor_Workbench
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((sx * 0.74, sy * 0.42, 0.89), (0.14, 0.12, 0.16), "dark_iron", bevel=0.018)
            b.box((sx * 0.65, sy * 0.34, 0.07), (0.13, 0.13, 0.12), "dark_iron", bevel=0.012)
    # Yellow-handled claw hammer
    b.cone((-0.38, -0.08, 1.00), 0.036, 0.032, 0.46, "gold_yellow", segments=6, rot_deg=(0, 90, -24))
    b.box((-0.17, 0.01, 1.02), (0.11, 0.24, 0.10), "silver", rot_deg=(0, 0, -24), bevel=0.018)
    b.tapered_box((-0.14, -0.10, 1.02), (0.09, 0.10), (0.03, 0.08), 0.10, "dark_iron", rot_deg=(0, 0, -24))
    # Silver hand saw with wooden handle
    b.tapered_box((0.20, -0.06, 0.985), (0.64, 0.20), (0.64, 0.18), 0.028, "silver", rot_deg=(0, 0, 22))
    b.box((0.23, -0.15, 0.985), (0.60, 0.035, 0.03), "dark_iron", rot_deg=(0, 0, 22))
    b.box((0.55, 0.08, 1.01), (0.22, 0.22, 0.08), "warm_oak", rot_deg=(0, 0, 22), bevel=0.02)
    b.box((0.56, 0.08, 1.015), (0.09, 0.09, 0.09), "dark_wood", rot_deg=(0, 0, 22))
    return [b.build_object()]


def build_04_electronics_desk(p):
    b = LowPolyBuilder("BUILD0027_ElectronicsCraftDesk", p)
    b.tapered_box((-0.68, -0.34, 0.42), (0.09, 0.09), (0.12, 0.12), 0.84, "warm_oak", bevel=0.015)
    b.tapered_box((-0.68, 0.34, 0.42), (0.09, 0.09), (0.12, 0.12), 0.84, "warm_oak", bevel=0.015)
    b.box((0.46, 0.0, 0.42), (0.54, 0.78, 0.82), "warm_oak", bevel=0.025)
    b.box((0.46, -0.40, 0.60), (0.44, 0.05, 0.26), "honey_wood", bevel=0.015)
    b.box((0.46, -0.40, 0.26), (0.44, 0.05, 0.26), "honey_wood", bevel=0.015)
    b.box((0.46, -0.435, 0.60), (0.16, 0.03, 0.045), "dark_wood", bevel=0.008)
    b.box((0.46, -0.435, 0.26), (0.16, 0.03, 0.045), "dark_wood", bevel=0.008)
    b.box((0, 0, 0.87), (1.64, 0.90, 0.11), "honey_wood", bevel=0.028)
    # Green PCB work mat
    b.box((-0.06, -0.10, 0.935), (0.68, 0.44, 0.025), "pcb_green", bevel=0.006)
    b.box((-0.06, -0.10, 0.955), (0.14, 0.14, 0.03), "dark_iron", bevel=0.006)
    # Chunky retro CRT monitor with glowing blue screen
    b.tapered_box((-0.24, 0.18, 0.97), (0.28, 0.24), (0.18, 0.16), 0.10, "dark_iron", bevel=0.015)
    b.box((-0.24, 0.20, 1.24), (0.58, 0.28, 0.46), "locker_steel", bevel=0.04)
    b.box((-0.24, 0.055, 1.25), (0.46, 0.03, 0.35), "tv_blue", bevel=0.01)
    # Coiled red & grey cables hanging off left edge
    b.tube_path([(-0.40, 0.10, 0.94), (-0.58, -0.20, 0.90), (-0.62, -0.46, 0.72), (-0.50, -0.45, 0.64), (-0.42, -0.34, 0.82)], 0.025, "plush_red", segments=6)
    b.tube_path([(-0.34, 0.08, 0.94), (-0.48, -0.26, 0.88), (-0.46, -0.44, 0.72)], 0.022, "dark_iron", segments=6)
    # Grey mini oscilloscope box with green LEDs
    b.box((0.26, 0.18, 1.05), (0.24, 0.22, 0.25), "silver", bevel=0.025)
    b.ico((0.21, 0.06, 1.10), (0.055, 0.04, 0.055), "led_green", subdiv=1)
    b.ico((0.30, 0.06, 1.10), (0.055, 0.04, 0.055), "led_green", subdiv=1)
    # Yellow soldering iron in silver cradle on right
    b.tapered_box((0.56, -0.02, 0.955), (0.20, 0.20), (0.14, 0.14), 0.06, "silver", bevel=0.012)
    b.cone((0.60, -0.02, 1.12), 0.038, 0.030, 0.28, "gold_yellow", segments=6, rot_deg=(0, 22, 0))
    b.cone((0.53, -0.02, 0.99), 0.008, 0.022, 0.14, "chrome", segments=6, rot_deg=(0, 22, 0))
    return [b.build_object()]


def build_05_dining_chair(p):
    b = LowPolyBuilder("BUILD0028_WoodenDiningChair", p)
    b.legs(0.54, 0.52, 0.48, 0.065, 0.09, "warm_oak")
    b.box((0, 0, 0.51), (0.68, 0.64, 0.095), "honey_wood", bevel=0.025)
    # Twin rear posts (+Y back, faces -Y front) + 2 curved horizontal slats
    b.tapered_box((-0.26, 0.26, 0.88), (0.085, 0.085), (0.075, 0.075), 0.74, "warm_oak", bevel=0.015)
    b.tapered_box((0.26, 0.26, 0.88), (0.085, 0.085), (0.075, 0.075), 0.74, "warm_oak", bevel=0.015)
    b.box((0, 0.26, 1.11), (0.52, 0.065, 0.18), "honey_wood", bevel=0.018)
    b.box((0, 0.26, 0.83), (0.52, 0.065, 0.15), "honey_wood", bevel=0.018)
    return [b.build_object()]


def build_06_armchair(p):
    b = LowPolyBuilder("BUILD0029_ComfyArmchair", p)
    b.legs(0.82, 0.72, 0.12, 0.07, 0.10, "dark_wood")
    b.box((0, -0.02, 0.24), (1.02, 0.92, 0.24), "deep_red", bevel=0.04)
    # Plush crimson seat cushion
    b.box((0, -0.08, 0.44), (0.72, 0.70, 0.22), "plush_red", bevel=0.06)
    # High arched crimson backrest + 2 diamond button tufts
    b.tapered_box((0, 0.30, 0.80), (0.94, 0.28), (0.76, 0.24), 0.76, "plush_red", rot_deg=(8, 0, 0), bevel=0.08)
    b.ico((-0.17, 0.15, 0.84), (0.08, 0.05, 0.08), "deep_red", subdiv=1)
    b.ico((0.17, 0.15, 0.84), (0.08, 0.05, 0.08), "deep_red", subdiv=1)
    # Rolled crimson armrests
    b.box((-0.46, -0.02, 0.48), (0.24, 0.86, 0.40), "plush_red", bevel=0.05)
    b.box((0.46, -0.02, 0.48), (0.24, 0.86, 0.40), "plush_red", bevel=0.05)
    b.cone((-0.48, -0.02, 0.69), 0.14, 0.14, 0.88, "plush_red", segments=8, rot_deg=(90, 0, 0))
    b.cone((0.48, -0.02, 0.69), 0.14, 0.14, 0.88, "plush_red", segments=8, rot_deg=(90, 0, 0))
    return [b.build_object()]


def build_07_single_bed(p):
    b = LowPolyBuilder("BUILD0030_WoodenSingleBed", p)
    # 4 corner posts with rounded finials (+Y headboard, -Y footboard)
    for sx in (-1, 1):
        b.box((sx * 0.68, 0.98, 0.54), (0.14, 0.14, 1.08), "warm_oak", bevel=0.025)
        b.ico((sx * 0.68, 0.98, 1.12), (0.17, 0.17, 0.14), "honey_wood", subdiv=1)
        b.box((sx * 0.68, -0.98, 0.40), (0.14, 0.14, 0.80), "warm_oak", bevel=0.025)
        b.ico((sx * 0.68, -0.98, 0.84), (0.17, 0.17, 0.14), "honey_wood", subdiv=1)
    b.box((0, 0.98, 0.68), (1.26, 0.10, 0.66), "honey_wood", bevel=0.025)
    b.box((0, -0.98, 0.44), (1.26, 0.10, 0.46), "honey_wood", bevel=0.025)
    b.box((0, 0, 0.28), (1.36, 1.92, 0.22), "honey_wood", bevel=0.025)
    # White mattress + puffy low-poly pillow
    b.box((0, 0, 0.48), (1.24, 1.88, 0.24), "crisp_white", bevel=0.06)
    b.ico((0, 0.64, 0.66), (0.86, 0.48, 0.22), "crisp_white", subdiv=1)
    # Sky-blue duvet + deep-blue folded cuff
    b.box((0, -0.34, 0.55), (1.30, 1.24, 0.20), "sky_blue", bevel=0.06)
    b.box((0, 0.18, 0.61), (1.32, 0.26, 0.16), "deep_blue", bevel=0.05)
    return [b.build_object()]


def build_08_analog_radio(p):
    b = LowPolyBuilder("BUILD0031_AnalogRadio", p)
    b.box((0, 0, 0.35), (1.06, 0.44, 0.64), "caramel", bevel=0.08)
    b.box((0, -0.21, 0.35), (0.90, 0.05, 0.48), "warm_white", bevel=0.03)
    # 4 left speaker louver slots
    for i in range(4):
        b.box((-0.18, -0.24, 0.48 - i * 0.09), (0.40, 0.025, 0.04), "dark_wood", bevel=0.008)
    # Round analog dial + red needle on right
    b.cone((0.24, -0.24, 0.41), 0.14, 0.14, 0.03, "dark_wood", segments=10, rot_deg=(90, 0, 0))
    b.cone((0.24, -0.255, 0.41), 0.115, 0.115, 0.02, "crisp_white", segments=10, rot_deg=(90, 0, 0))
    b.box((0.27, -0.27, 0.44), (0.025, 0.015, 0.11), "red_cross", rot_deg=(0, 32, 0))
    # 3 small round tuning knobs
    for k in range(3):
        b.cone((0.14 + k * 0.10, -0.25, 0.20), 0.038, 0.034, 0.05, "honey_wood", segments=8, rot_deg=(90, 0, 0))
    # Tilted silver antenna + bright yellow ball tip
    b.tube_path([(-0.24, 0.08, 0.66), (0.46, 0.08, 1.12)], 0.018, "chrome", segments=6)
    b.ico((0.46, 0.08, 1.12), (0.11, 0.11, 0.11), "gold_yellow", subdiv=1)
    return [b.build_object()]


def build_09_desk_lamp(p):
    b = LowPolyBuilder("BUILD0032_DeskLamp", p)
    b.cone((0, 0, 0.05), 0.26, 0.22, 0.09, "gold_yellow", segments=10)
    b.ico((0, 0, 0.11), (0.24, 0.24, 0.14), "gold_yellow", subdiv=1)
    # Articulated swing arm (2 segments + elbow pivot)
    b.tube_path([(0.0, 0.02, 0.14), (-0.26, 0.10, 0.54), (0.08, -0.06, 0.84)], 0.038, "gold_yellow", segments=6)
    b.ico((-0.26, 0.10, 0.54), (0.13, 0.13, 0.13), "gold_yellow", subdiv=1)
    # Flared bell yellow lampshade + glowing bulb pointing down-front (-Y)
    b.cone((0.08, -0.06, 0.84), 0.11, 0.09, 0.12, "gold_yellow", segments=8, rot_deg=(-24, 34, 0))
    b.cone((0.20, -0.15, 0.69), 0.26, 0.11, 0.26, "lamp_yellow", segments=10, rot_deg=(-24, 34, 0))
    b.ico((0.25, -0.19, 0.60), (0.22, 0.22, 0.22), "bulb_glow", subdiv=1)
    return [b.build_object()]


def build_10_cooking_pot(p):
    b = LowPolyBuilder("BUILD0033_CookingPotStove", p)
    b.tapered_box((0, 0, 0.20), (1.02, 0.88), (0.94, 0.82), 0.36, "silver", bevel=0.05)
    b.box((0, 0, 0.40), (0.86, 0.74, 0.05), "locker_steel", bevel=0.015)
    # Front control dial knob
    b.cone((0, -0.45, 0.21), 0.09, 0.08, 0.06, "dark_iron", segments=8, rot_deg=(90, 0, 0))
    b.box((0, -0.485, 0.21), (0.035, 0.02, 0.11), "chrome")
    # Grate bars + glowing BLUE gas flame tongues
    b.box((0, 0, 0.46), (0.72, 0.07, 0.06), "dark_iron", bevel=0.01)
    b.box((0, 0, 0.46), (0.07, 0.72, 0.06), "dark_iron", bevel=0.01)
    for i in range(8):
        ang = i * (2 * math.pi / 8)
        b.cone((math.cos(ang) * 0.20, math.sin(ang) * 0.20, 0.52), 0.045, 0.008, 0.12, "gas_blue", segments=5)
    # Polished silver pot + curved handles + domed lid
    b.cone((0, 0, 0.76), 0.31, 0.34, 0.44, "silver", segments=10)
    b.cone((0, 0, 0.99), 0.36, 0.36, 0.04, "chrome", segments=10)
    b.cone((0, 0, 1.05), 0.33, 0.12, 0.11, "silver", segments=10)
    b.cone((0, 0, 1.13), 0.075, 0.055, 0.07, "dark_iron", segments=8)
    b.tube_path([(-0.33, 0, 0.90), (-0.44, 0, 0.87), (-0.44, 0, 0.80), (-0.33, 0, 0.78)], 0.028, "chrome", segments=6)
    b.tube_path([(0.33, 0, 0.90), (0.44, 0, 0.87), (0.44, 0, 0.80), (0.33, 0, 0.78)], 0.028, "chrome", segments=6)
    return [b.build_object()]


def build_planter(p, name, crop_kind):
    # 1. Crate base
    crate = LowPolyBuilder(name, p)
    crate.tapered_box((0, 0, 0.22), (1.16, 1.16), (1.24, 1.24), 0.42, "honey_wood", bevel=0.025)
    crate.box((0, 0, 0.22), (1.24, 1.24, 0.03), "warm_oak")
    c = 0.58
    for sx in (-1, 1):
        for sy in (-1, 1):
            crate.box((sx * c, sy * c, 0.23), (0.14, 0.14, 0.47), "warm_oak", bevel=0.018)
            # Forged dark-iron corner brackets matching 11_Wooden_Storage_Crate!
            crate.box((sx * 0.60, sy * 0.60, 0.43), (0.18, 0.18, 0.10), "locker_steel", bevel=0.015)
            crate.box((sx * 0.60, sy * 0.60, 0.06), (0.18, 0.18, 0.10), "locker_steel", bevel=0.015)
    crate.box((0, 0.56, 0.45), (1.30, 0.12, 0.08), "warm_oak", bevel=0.015)
    crate.box((0, -0.56, 0.45), (1.30, 0.12, 0.08), "warm_oak", bevel=0.015)
    crate.box((0.56, 0, 0.45), (0.12, 1.12, 0.08), "warm_oak", bevel=0.015)
    crate.box((-0.56, 0, 0.45), (0.12, 1.12, 0.08), "warm_oak", bevel=0.015)
    crate.box((0, 0, 0.43), (1.06, 1.06, 0.06), "soil", bevel=0.01)
    root_obj = crate.build_object()

    # 2. CropFoliageRoot child (exact name expected by CropPlanterFurniture.cs!)
    fol = LowPolyBuilder("CropFoliageRoot", p)
    # 3. CropFruitRoot child (exact name expected by CropPlanterFurniture.cs!)
    fru = LowPolyBuilder("CropFruitRoot", p)

    if crop_kind == "lime":
        fol.cone((0, 0, 0.62), 0.06, 0.04, 0.34, "warm_oak", segments=6)
        for i in range(7):
            ang = i * (2 * math.pi / 7)
            r = 0.28 if i % 2 == 0 else 0.18
            z = 0.78 if i % 2 == 0 else 0.90
            fol.ico((math.cos(ang) * r, math.sin(ang) * r, z), (0.34, 0.22, 0.14), "leaf_dark" if i % 2 == 0 else "leaf_bright", subdiv=1, rot_deg=(18, 0, math.degrees(ang)))
        for pos in [(-0.24, -0.18, 0.74), (0.24, -0.10, 0.76), (0.04, 0.14, 0.86), (-0.12, 0.22, 0.72)]:
            fru.ico(pos, (0.24, 0.19, 0.19), "lime", subdiv=1, rot_deg=(0, 15, 25))
    elif crop_kind == "potato":
        for i in range(6):
            ang = i * (2 * math.pi / 6)
            fol.ico((math.cos(ang) * 0.25, math.sin(ang) * 0.25, 0.68), (0.32, 0.24, 0.15), "leaf_bright" if i % 2 == 0 else "leaf_dark", subdiv=1, rot_deg=(15, 0, math.degrees(ang)))
        for fx, fy, fz in [(-0.14, 0.06, 0.88), (0.16, -0.04, 0.90)]:
            fol.ico((fx, fy, fz), (0.18, 0.18, 0.08), "crisp_white", subdiv=1)
            fol.ico((fx, fy, fz + 0.03), (0.08, 0.08, 0.06), "gold_yellow", subdiv=1)
        for pos in [(-0.22, -0.22, 0.52), (0.20, -0.18, 0.52), (0.02, 0.22, 0.52)]:
            fru.ico(pos, (0.30, 0.22, 0.18), "potato", subdiv=1, rot_deg=(0, 8, 20))
    elif crop_kind == "tomato":
        for i in range(7):
            ang = i * (2 * math.pi / 7)
            fol.ico((math.cos(ang) * 0.27, math.sin(ang) * 0.27, 0.78 + (i % 2) * 0.08), (0.32, 0.26, 0.16), "leaf_dark" if i % 2 == 0 else "leaf_bright", subdiv=1, rot_deg=(14, 0, math.degrees(ang)))
        for pos in [(-0.24, -0.18, 0.68), (0.22, -0.16, 0.74), (0.04, 0.08, 0.86), (-0.14, 0.22, 0.68)]:
            fru.ico(pos, (0.26, 0.26, 0.22), "tomato", subdiv=1)
    elif crop_kind == "carrot":
        for cx, cy in [(-0.26, -0.14), (0.04, 0.16), (0.26, -0.14)]:
            fru.cone((cx, cy, 0.58), 0.05, 0.11, 0.26, "carrot", segments=7)
            fru.ico((cx, cy, 0.71), (0.22, 0.22, 0.10), "carrot", subdiv=1)
            for j in range(3):
                ang = j * 120
                fol.cone((cx + math.cos(math.radians(ang)) * 0.05, cy + math.sin(math.radians(ang)) * 0.05, 0.88), 0.02, 0.08, 0.34, "leaf_bright" if j % 2 == 0 else "leaf_dark", segments=5, rot_deg=(18 * math.cos(math.radians(ang)), 18 * math.sin(math.radians(ang)), ang))
    else:  # strawberry
        for i in range(6):
            ang = i * (2 * math.pi / 6)
            fol.ico((math.cos(ang) * 0.28, math.sin(ang) * 0.28, 0.70), (0.32, 0.26, 0.14), "leaf_dark" if i % 2 == 0 else "leaf_bright", subdiv=1, rot_deg=(16, 0, math.degrees(ang)))
        for fx, fy, fz in [(-0.08, 0.04, 0.84), (0.15, 0.10, 0.82)]:
            fol.ico((fx, fy, fz), (0.17, 0.17, 0.07), "crisp_white", subdiv=1)
            fol.ico((fx, fy, fz + 0.03), (0.07, 0.07, 0.05), "gold_yellow", subdiv=1)
        for pos in [(-0.24, -0.22, 0.62), (0.22, -0.20, 0.62), (0.02, -0.28, 0.66), (-0.12, 0.24, 0.62)]:
            fru.cone(pos, 0.04, 0.11, 0.22, "strawberry", segments=7)

    fol_obj = fol.build_object(parent=root_obj)
    fru_obj = fru.build_object(parent=root_obj)
    return [root_obj, fol_obj, fru_obj]


def build_16_fridge(p):
    b = LowPolyBuilder("BUILD0039_VintageRefrigerator", p)
    b.legs(0.76, 0.64, 0.10, 0.08, 0.10, "dark_iron")
    b.box((0, 0.02, 0.98), (1.04, 0.86, 1.76), "mint_fridge", bevel=0.10)
    b.box((0, -0.43, 1.44), (0.94, 0.07, 0.62), "mint_fridge", bevel=0.04)
    b.box((0, -0.43, 0.60), (0.94, 0.07, 0.94), "mint_fridge", bevel=0.04)
    b.box((0, -0.44, 1.11), (1.00, 0.05, 0.035), "dark_iron")
    # Horizontal curved retro handles on left
    b.tube_path([(-0.38, -0.46, 1.32), (-0.38, -0.52, 1.32), (-0.16, -0.52, 1.32), (-0.16, -0.46, 1.32)], 0.034, "chrome", segments=6)
    b.tube_path([(-0.38, -0.46, 0.88), (-0.38, -0.52, 0.88), (-0.16, -0.52, 0.88), (-0.16, -0.46, 0.88)], 0.034, "chrome", segments=6)
    return [b.build_object()]


def build_17_med_cabinet(p):
    b = LowPolyBuilder("BUILD0040_MedicalWallCabinet", p)
    b.box((0, 0.08, 1.24), (0.98, 0.42, 1.06), "warm_white", bevel=0.06)
    b.box((0, -0.14, 1.24), (0.86, 0.05, 0.94), "crisp_white", bevel=0.03)
    b.box((0, -0.18, 1.24), (0.14, 0.03, 0.46), "red_cross", bevel=0.008)
    b.box((0, -0.18, 1.24), (0.46, 0.03, 0.14), "red_cross", bevel=0.008)
    b.box((-0.34, -0.19, 1.24), (0.05, 0.06, 0.22), "locker_steel", bevel=0.012)
    b.box((0.46, -0.15, 1.54), (0.06, 0.05, 0.14), "locker_steel", bevel=0.01)
    b.box((0.46, -0.15, 0.94), (0.06, 0.05, 0.14), "locker_steel", bevel=0.01)
    return [b.build_object()]


def build_18_leather_sofa(p):
    b = LowPolyBuilder("BUILD0041_LeatherSofa", p)
    b.legs(1.86, 0.72, 0.12, 0.07, 0.10, "dark_wood")
    b.box((0, -0.02, 0.24), (2.04, 0.88, 0.22), "leather_dark", bevel=0.04)
    b.box((-0.44, -0.06, 0.44), (0.84, 0.72, 0.21), "leather", bevel=0.06)
    b.box((0.44, -0.06, 0.44), (0.84, 0.72, 0.21), "leather", bevel=0.06)
    b.tapered_box((-0.44, 0.30, 0.82), (0.86, 0.28), (0.78, 0.22), 0.64, "leather", rot_deg=(7, 0, 0), bevel=0.07)
    b.tapered_box((0.44, 0.30, 0.82), (0.86, 0.28), (0.78, 0.22), 0.64, "leather", rot_deg=(7, 0, 0), bevel=0.07)
    b.box((-0.96, -0.02, 0.48), (0.24, 0.84, 0.40), "leather", bevel=0.05)
    b.box((0.96, -0.02, 0.48), (0.24, 0.84, 0.40), "leather", bevel=0.05)
    b.cone((-0.98, -0.02, 0.69), 0.135, 0.135, 0.86, "leather", segments=8, rot_deg=(90, 0, 0))
    b.cone((0.98, -0.02, 0.69), 0.135, 0.135, 0.86, "leather", segments=8, rot_deg=(90, 0, 0))
    return [b.build_object()]


def build_19_floor_lamp(p):
    b = LowPolyBuilder("BUILD0042_StandingFloorLamp", p)
    b.cone((0, 0, 0.05), 0.30, 0.24, 0.09, "silver", segments=10)
    b.cone((0, 0, 0.13), 0.14, 0.10, 0.09, "locker_steel", segments=8)
    b.cone((0, 0, 0.78), 0.045, 0.038, 1.32, "silver", segments=8)
    # Tapered yellow trapezoid shade + glowing bulb + pull chain
    b.cone((0, 0, 1.66), 0.42, 0.24, 0.48, "lamp_yellow", segments=10)
    b.cone((0, 0, 1.91), 0.24, 0.22, 0.04, "gold_yellow", segments=10)
    b.ico((0, 0, 1.44), (0.22, 0.22, 0.22), "bulb_glow", subdiv=1)
    b.cone((0.18, -0.10, 1.24), 0.012, 0.012, 0.36, "gold_yellow", segments=5)
    b.ico((0.18, -0.10, 1.04), (0.09, 0.09, 0.09), "gold_yellow", subdiv=1)
    return [b.build_object()]


def build_20_bookshelf(p):
    b = LowPolyBuilder("BUILD0043_TallBookshelf", p)
    b.box((0, 0.24, 1.04), (1.36, 0.06, 2.02), "dark_wood")
    b.box((-0.65, 0, 1.04), (0.10, 0.56, 2.04), "warm_oak", bevel=0.018)
    b.box((0.65, 0, 1.04), (0.10, 0.56, 2.04), "warm_oak", bevel=0.018)
    b.box((0, 0, 2.08), (1.46, 0.62, 0.12), "honey_wood", bevel=0.025)
    b.box((0, 0, 0.08), (1.44, 0.62, 0.14), "honey_wood", bevel=0.025)
    books = ["plush_red", "sky_blue", "gold_yellow", "book_green", "book_purple", "deep_blue", "plush_red"]
    for s in range(3):
        sy = 0.20 + s * 0.62
        if s > 0:
            b.box((0, -0.02, sy - 0.04), (1.24, 0.50, 0.07), "honey_wood", bevel=0.01)
        for i in range(7):
            bx = -0.49 + i * 0.162
            bh = 0.42 + ((i + s) % 3) * 0.045
            b.box((bx, -0.05, sy + bh * 0.5), (0.135, 0.36, bh), books[(i + s * 2) % len(books)], bevel=0.014)
    return [b.build_object()]


def build_21_dining_table(p):
    b = LowPolyBuilder("BUILD0044_WoodenDiningTable", p)
    b.legs(1.52, 0.86, 0.78, 0.095, 0.13, "warm_oak")
    b.box((0, 0, 0.72), (1.54, 0.88, 0.12), "warm_oak", bevel=0.015)
    b.box((0, 0, 0.84), (1.80, 1.12, 0.12), "honey_wood", bevel=0.03)
    return [b.build_object()]


def build_22_coffee_table(p):
    b = LowPolyBuilder("BUILD0045_LowCoffeeTable", p)
    b.legs(1.16, 0.60, 0.46, 0.08, 0.10, "warm_oak")
    b.box((0, 0, 0.18), (1.14, 0.52, 0.07), "warm_oak", bevel=0.015)
    b.box((0, 0, 0.50), (1.38, 0.78, 0.10), "honey_wood", bevel=0.028)
    # White ceramic coffee mug with curved handle
    b.cone((0.28, -0.06, 0.67), 0.095, 0.105, 0.22, "crisp_white", segments=8)
    b.cone((0.28, -0.06, 0.785), 0.08, 0.08, 0.015, "dark_wood", segments=8)
    b.tube_path([(0.37, -0.06, 0.73), (0.45, -0.06, 0.71), (0.45, -0.06, 0.63), (0.37, -0.06, 0.61)], 0.02, "crisp_white", segments=5)
    return [b.build_object()]


def build_23_kitchen_sink(p):
    b = LowPolyBuilder("BUILD0046_KitchenCounterSink", p)
    b.box((0, 0.02, 0.42), (1.44, 0.78, 0.80), "warm_oak", bevel=0.025)
    b.box((-0.34, -0.38, 0.42), (0.62, 0.05, 0.66), "honey_wood", bevel=0.02)
    b.box((0.34, -0.38, 0.42), (0.62, 0.05, 0.66), "honey_wood", bevel=0.02)
    b.ico((-0.09, -0.43, 0.44), (0.09, 0.07, 0.09), "gold_yellow", subdiv=1)
    b.ico((0.09, -0.43, 0.44), (0.09, 0.07, 0.09), "gold_yellow", subdiv=1)
    # Grey countertop + recessed left sink
    b.box((0, 0, 0.87), (1.56, 0.88, 0.11), "silver", bevel=0.025)
    b.box((-0.24, -0.02, 0.91), (0.68, 0.52, 0.06), "locker_steel", bevel=0.015)
    b.box((-0.24, -0.02, 0.935), (0.56, 0.40, 0.03), "dark_iron")
    # Curved gooseneck chrome faucet + twin knobs
    b.cone((0.32, 0.14, 0.96), 0.07, 0.055, 0.08, "chrome", segments=8)
    b.tube_path([(0.32, 0.14, 0.98), (0.32, 0.14, 1.32), (0.24, 0.10, 1.42), (0.10, 0.04, 1.40), (0.04, 0.02, 1.26)], 0.034, "chrome", segments=6)
    b.ico((0.18, 0.24, 0.99), (0.09, 0.09, 0.07), "chrome", subdiv=1)
    b.ico((0.46, 0.24, 0.99), (0.09, 0.09, 0.07), "chrome", subdiv=1)
    return [b.build_object()]


def build_24_first_aid(p):
    b = LowPolyBuilder("BUILD0047_FirstAidStation", p)
    b.box((0, 0.04, 0.42), (1.12, 0.64, 0.72), "plush_red", bevel=0.06)
    b.box((0, 0.26, 0.94), (1.08, 0.18, 0.42), "plush_red", bevel=0.05)
    # Top red carry handle
    b.tube_path([(-0.22, 0.26, 1.12), (-0.22, 0.26, 1.26), (0.22, 0.26, 1.26), (0.22, 0.26, 1.12)], 0.04, "plush_red", segments=6)
    # White front panel + bold red cross
    b.box((0, -0.29, 0.42), (0.98, 0.05, 0.46), "crisp_white", bevel=0.02)
    b.box((0, -0.33, 0.42), (0.11, 0.03, 0.34), "red_cross", bevel=0.008)
    b.box((0, -0.33, 0.42), (0.34, 0.03, 0.11), "red_cross", bevel=0.008)
    # Gauze roll + 2 tan band-aids peeking out
    b.cone((-0.22, -0.02, 0.84), 0.17, 0.17, 0.36, "warm_white", segments=10)
    b.cone((-0.22, -0.02, 0.85), 0.07, 0.07, 0.37, "bandage_tan", segments=8)
    b.box((0.16, 0.02, 0.88), (0.16, 0.08, 0.38), "bandage_tan", rot_deg=(0, 14, 0), bevel=0.025)
    b.box((0.34, -0.04, 0.84), (0.16, 0.08, 0.36), "bandage_tan", rot_deg=(0, 24, 0), bevel=0.025)
    return [b.build_object()]


def build_25_weapon_rack(p):
    b = LowPolyBuilder("BUILD0048_WeaponDisplayRack", p)
    b.box((-0.56, 0.14, 1.18), (0.14, 0.10, 1.26), "warm_oak", bevel=0.02)
    b.box((0.56, 0.14, 1.18), (0.14, 0.10, 1.26), "warm_oak", bevel=0.02)
    b.box((0, 0.10, 1.48), (1.58, 0.07, 0.30), "honey_wood", bevel=0.02)
    b.box((0, 0.10, 1.16), (1.58, 0.07, 0.30), "warm_oak", bevel=0.02)
    b.box((0, 0.10, 0.84), (1.58, 0.07, 0.30), "honey_wood", bevel=0.02)
    # Classic rifle across upper pegs
    b.tapered_box((-0.44, 0.01, 1.38), (0.52, 0.09), (0.44, 0.08), 0.15, "caramel", rot_deg=(0, -10, 0), bevel=0.02)
    b.box((-0.02, 0.01, 1.44), (0.38, 0.09, 0.12), "dark_iron", bevel=0.015)
    b.cone((0.36, 0.01, 1.46), 0.032, 0.026, 0.66, "dark_iron", segments=6, rot_deg=(0, 90, 0))
    # Wooden-handled silver axe across lower pegs
    b.cone((-0.08, 0.01, 0.94), 0.042, 0.036, 0.72, "gold_yellow", segments=6, rot_deg=(0, 78, 0))
    b.tapered_box((0.28, 0.00, 0.92), (0.28, 0.07), (0.18, 0.08), 0.32, "silver", rot_deg=(0, -12, 0), bevel=0.015)
    b.box((0.30, 0.00, 0.76), (0.30, 0.08, 0.05), "chrome", rot_deg=(0, -12, 0))
    return [b.build_object()]


def build_26_wood_stove(p):
    b = LowPolyBuilder("BUILD0049_WoodBurningStove", p)
    b.legs(0.86, 0.70, 0.20, 0.075, 0.11, "dark_iron")
    b.box((0, 0, 0.22), (1.14, 0.92, 0.10), "locker_steel", bevel=0.025)
    b.box((0, 0, 0.66), (1.02, 0.84, 0.80), "dark_iron", bevel=0.05)
    b.box((0, 0, 1.09), (1.16, 0.94, 0.10), "locker_steel", bevel=0.03)
    # Chimney pipe
    b.cone((0, 0.04, 1.18), 0.18, 0.16, 0.09, "locker_steel", segments=8)
    b.cone((0, 0.04, 1.48), 0.13, 0.13, 0.56, "dark_iron", segments=8)
    b.cone((0, 0.04, 1.76), 0.16, 0.16, 0.05, "locker_steel", segments=8)
    # Arched firebox window facing -Y with logs & sculpted flame cones
    b.box((0, -0.43, 0.62), (0.76, 0.06, 0.56), "locker_steel", bevel=0.04)
    b.box((0, -0.45, 0.60), (0.60, 0.04, 0.44), "dark_wood", bevel=0.03)
    b.cone((-0.10, -0.47, 0.62), 0.12, 0.02, 0.30, "fire_orange", segments=5)
    b.cone((0.10, -0.47, 0.60), 0.11, 0.02, 0.26, "fire_orange", segments=5)
    b.cone((0.0, -0.48, 0.60), 0.10, 0.02, 0.28, "fire_yellow", segments=5)
    b.cone((-0.10, -0.48, 0.44), 0.04, 0.04, 0.26, "warm_oak", segments=6, rot_deg=(0, 78, 0))
    b.cone((0.10, -0.48, 0.44), 0.04, 0.04, 0.26, "warm_oak", segments=6, rot_deg=(0, -78, 0))
    b.ico((0.42, -0.47, 0.62), (0.08, 0.06, 0.08), "gold_yellow", subdiv=1)
    return [b.build_object()]


def build_27_wall_tv(p):
    b = LowPolyBuilder("BUILD0050_WallMountedTV", p)
    b.box((0, 0.14, 1.24), (0.64, 0.12, 0.38), "dark_iron", bevel=0.02)
    b.box((0, 0.04, 1.24), (1.58, 0.12, 0.96), "dark_iron", bevel=0.045)
    b.box((0, -0.02, 1.24), (1.44, 0.03, 0.84), "locker_steel", bevel=0.02)
    b.box((0, -0.035, 1.24), (1.38, 0.02, 0.78), "tv_blue", bevel=0.01)
    b.box((0, 0.04, 0.72), (0.24, 0.10, 0.08), "dark_iron", bevel=0.015)
    return [b.build_object()]


def build_28_area_rug(p):
    b = LowPolyBuilder("BUILD0051_WovenAreaRug", p)
    b.box((0, 0, 0.02), (2.84, 2.04, 0.035), "plush_red", bevel=0.01)
    b.box((0, 0.80, 0.038), (2.44, 0.12, 0.015), "gold_yellow")
    b.box((0, -0.80, 0.038), (2.44, 0.12, 0.015), "gold_yellow")
    b.box((-1.16, 0, 0.038), (0.12, 1.72, 0.015), "gold_yellow")
    b.box((1.16, 0, 0.038), (0.12, 1.72, 0.015), "gold_yellow")
    b.box((0, 0, 0.042), (0.86, 0.86, 0.015), "gold_yellow", rot_deg=(0, 0, 45))
    b.box((0, 0, 0.046), (0.54, 0.54, 0.015), "plush_red", rot_deg=(0, 0, 45))
    b.box((0, 0, 0.050), (0.24, 0.24, 0.015), "gold_yellow", rot_deg=(0, 0, 45))
    for f in range(7):
        fy = -0.84 + f * 0.28
        b.box((-1.54, fy, 0.02), (0.24, 0.11, 0.025), "gold_yellow", bevel=0.008)
        b.box((1.54, fy, 0.02), (0.24, 0.11, 0.025), "gold_yellow", bevel=0.008)
    return [b.build_object()]


def build_29_potted_fern(p):
    b = LowPolyBuilder("BUILD0052_PottedIndoorFern", p)
    b.cone((0, 0, 0.22), 0.18, 0.26, 0.44, "terracotta", segments=10)
    b.cone((0, 0, 0.46), 0.28, 0.30, 0.10, "terracotta", segments=10)
    b.cone((0, 0, 0.50), 0.25, 0.25, 0.02, "soil", segments=10)
    # Tapered low-poly fern fronds arching outward in 2 tiers
    for i in range(10):
        ang = i * 36.0
        rad = math.radians(ang)
        mat = "leaf_bright" if i % 2 == 0 else "leaf_dark"
        b.tapered_box((math.sin(rad) * 0.34, math.cos(rad) * 0.34, 0.68), (0.18, 0.05), (0.04, 0.03), 0.54, mat, rot_deg=(64, 0, -ang))
    for i in range(6):
        ang = i * 60.0 + 18.0
        rad = math.radians(ang)
        b.tapered_box((math.sin(rad) * 0.20, math.cos(rad) * 0.20, 0.86), (0.16, 0.05), (0.04, 0.03), 0.44, "leaf_bright", rot_deg=(36, 0, -ang))
    return [b.build_object()]


def build_30_trash_bin(p):
    b = LowPolyBuilder("BUILD0053_TrashBinBarrel", p)
    b.cone((0, 0, 0.38), 0.25, 0.30, 0.76, "olive_metal", segments=10)
    b.cone((0, 0, 0.24), 0.285, 0.295, 0.05, "dark_iron", segments=10)
    b.cone((0, 0, 0.54), 0.305, 0.315, 0.05, "dark_iron", segments=10)
    b.cone((0, 0, 0.77), 0.32, 0.32, 0.05, "dark_iron", segments=10)
    b.box((-0.32, 0, 0.56), (0.08, 0.16, 0.06), "dark_iron", bevel=0.01)
    b.box((0.32, 0, 0.56), (0.08, 0.16, 0.06), "dark_iron", bevel=0.01)
    # Tilted lid + loop handle
    b.cone((0.04, 0, 0.83), 0.33, 0.31, 0.07, "olive_metal", segments=10, rot_deg=(0, 10, 0))
    b.tube_path([(-0.04, 0, 0.86), (-0.04, 0, 0.94), (0.14, 0, 0.91), (0.14, 0, 0.83)], 0.022, "dark_iron", segments=6)
    return [b.build_object()]


BUILDERS = [
    ("BUILD0024_WoodenWardrobe",       build_01_wardrobe),
    ("BUILD0025_MetalLockerCabinet",   build_02_locker),
    ("BUILD0026_CarpentryCraftTable",  build_03_carpentry_table),
    ("BUILD0027_ElectronicsCraftDesk", build_04_electronics_desk),
    ("BUILD0028_WoodenDiningChair",    build_05_dining_chair),
    ("BUILD0029_ComfyArmchair",        build_06_armchair),
    ("BUILD0030_WoodenSingleBed",      build_07_single_bed),
    ("BUILD0031_AnalogRadio",          build_08_analog_radio),
    ("BUILD0032_DeskLamp",             build_09_desk_lamp),
    ("BUILD0033_CookingPotStove",      build_10_cooking_pot),
    ("BUILD0034_PlanterBox_Lime",      lambda p: build_planter(p, "BUILD0034_PlanterBox_Lime", "lime")),
    ("BUILD0035_PlanterBox_Potato",    lambda p: build_planter(p, "BUILD0035_PlanterBox_Potato", "potato")),
    ("BUILD0036_PlanterBox_Tomato",    lambda p: build_planter(p, "BUILD0036_PlanterBox_Tomato", "tomato")),
    ("BUILD0037_PlanterBox_Carrot",    lambda p: build_planter(p, "BUILD0037_PlanterBox_Carrot", "carrot")),
    ("BUILD0038_PlanterBox_Strawberry",lambda p: build_planter(p, "BUILD0038_PlanterBox_Strawberry", "strawberry")),
    ("BUILD0039_VintageRefrigerator",  build_16_fridge),
    ("BUILD0040_MedicalWallCabinet",   build_17_med_cabinet),
    ("BUILD0041_LeatherSofa",          build_18_leather_sofa),
    ("BUILD0042_StandingFloorLamp",    build_19_floor_lamp),
    ("BUILD0043_TallBookshelf",        build_20_bookshelf),
    ("BUILD0044_WoodenDiningTable",    build_21_dining_table),
    ("BUILD0045_LowCoffeeTable",       build_22_coffee_table),
    ("BUILD0046_KitchenCounterSink",   build_23_kitchen_sink),
    ("BUILD0047_FirstAidStation",      build_24_first_aid),
    ("BUILD0048_WeaponDisplayRack",    build_25_weapon_rack),
    ("BUILD0049_WoodBurningStove",     build_26_wood_stove),
    ("BUILD0050_WallMountedTV",        build_27_wall_tv),
    ("BUILD0051_WovenAreaRug",         build_28_area_rug),
    ("BUILD0052_PottedIndoorFern",     build_29_potted_fern),
    ("BUILD0053_TrashBinBarrel",       build_30_trash_bin),
]


def export_all_and_render_sheet():
    reset_scene()
    palette = build_palette()

    # 1. Export each of the 30 low-poly FBX models individually at origin (0,0,0)
    exported = 0
    for name, fn in BUILDERS:
        objs = fn(palette)
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        fbx_path = os.path.join(OUT_DIR, f"{name}.fbx")
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
        exported += 1
        # Move into 6x5 showcase grid for preview sheet render!
        idx = exported - 1
        col = idx % 6
        row = idx // 6
        gx = (col - 2.5) * 3.2
        gy = (2.0 - row) * 3.0
        objs[0].location = (gx, gy, 0.0)

    print(f"EXPORTED_LOWPOLY_FBX: {exported}/30 -> {OUT_DIR}")

    # 2. Render clean isometric 6x5 preview contact sheet using Blender EEVEE
    bpy.ops.object.camera_add(location=(0.0, -20.0, 15.5))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 20.5
    direction = Vector((0.0, 0.0, 0.6)) - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    bpy.context.scene.camera = cam

    bpy.ops.object.light_add(type="SUN", location=(6.0, -10.0, 14.0))
    sun = bpy.context.object
    sun.data.energy = 3.2
    sun.rotation_euler = (math.radians(48), math.radians(15), math.radians(-25))

    bpy.ops.object.light_add(type="SUN", location=(-8.0, 8.0, 10.0))
    fill = bpy.context.object
    fill.data.energy = 1.1
    fill.rotation_euler = (math.radians(55), math.radians(-20), math.radians(145))

    scene = bpy.context.scene
    avail_engines = bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in avail_engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1280
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    if scene.world:
        scene.world.use_nodes = True
        bg = scene.world.node_tree.nodes.get("Background")
        if bg:
            bg.inputs["Color"].default_value = (0.14, 0.16, 0.20, 1.0)
            bg.inputs["Strength"].default_value = 0.85

    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)
    print(f"PREVIEW_RENDERED: {PREVIEW_PATH}")


if __name__ == "__main__":
    export_all_and_render_sheet()

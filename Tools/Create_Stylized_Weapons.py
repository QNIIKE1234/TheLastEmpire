import os
import math
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler

PROJECT_DIR = r"d:\Unity Game Project\Eruption\TheLastEmpire\TheLastEmpire"
OUT_DIR = os.path.join(PROJECT_DIR, "Assets", "GeneratedModels", "Weapons")
os.makedirs(OUT_DIR, exist_ok=True)
MTL_NAME = "Weapons.mtl"
MTL_PATH = os.path.join(OUT_DIR, MTL_NAME)
PREVIEW_PATH = os.path.join(OUT_DIR, "Weapons_Showcase_Preview.png")

# Stylized weapon color palette closely matching the reference icon sheet (sRGB values for Unity Weapons.mtl)
PALETTE = {
    "GunMetal":       ((0.20, 0.23, 0.28), 0.36, 0.55),
    "SteelBright":    ((0.80, 0.84, 0.89), 0.25, 0.72),
    "ChromeSilver":   ((0.88, 0.91, 0.95), 0.20, 0.82),
    "DarkPolymer":    ((0.09, 0.10, 0.12), 0.75, 0.06),
    "RubberGrip":     ((0.05, 0.05, 0.06), 0.88, 0.00),
    "WoodFinish":     ((0.58, 0.28, 0.10), 0.78, 0.00),
    "WoodLight":      ((0.80, 0.46, 0.16), 0.72, 0.00),
    "BrassGold":      ((0.94, 0.68, 0.10), 0.28, 0.76),
    "MilitaryGreen":  ((0.42, 0.49, 0.12), 0.72, 0.08),
    "SniperYellow":   ((0.76, 0.72, 0.08), 0.68, 0.06),
    "FireRed":        ((0.92, 0.09, 0.08), 0.46, 0.14),
    "HazardYellow":   ((0.99, 0.78, 0.03), 0.46, 0.10),
    "ElectricCyan":   ((1.00, 0.90, 0.12), 0.22, 0.15),  # Bright yellow electric glow matching baton icon
    "SafetyOrange":   ((0.98, 0.42, 0.02), 0.46, 0.10),
}


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def write_mtl():
    lines = ["# Material library for TheLastEmpire stylized weapons\n"]
    for name, (rgb, rough, metal) in PALETTE.items():
        r, g, b = rgb
        ns = int(max(10, (1.0 - rough) * 90))
        ks = 0.15 + metal * 0.70
        lines.append(f"newmtl {name}\n")
        lines.append(f"Ka {r*0.35:.3f} {g*0.35:.3f} {b*0.35:.3f}\n")
        lines.append(f"Kd {r:.3f} {g:.3f} {b:.3f}\n")
        lines.append(f"Ks {ks:.3f} {ks:.3f} {ks:.3f}\n")
        lines.append(f"Ns {ns}\n")
        lines.append("illum 2\n\n")
    with open(MTL_PATH, "w", encoding="utf-8") as f:
        f.writelines(lines)


def make_blender_mats():
    mats = {}
    for name, (rgb, rough, metal) in PALETTE.items():
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            lin = (_srgb_to_linear(rgb[0]), _srgb_to_linear(rgb[1]), _srgb_to_linear(rgb[2]), 1.0)
            bsdf.inputs["Base Color"].default_value = lin
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Metallic"].default_value = metal
            if name == "ElectricCyan" and "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (1.0, 0.78, 0.02, 1.0)
                bsdf.inputs["Emission Strength"].default_value = 1.8
        mats[name] = m
    return mats


# ---------------------------------------------------------------------------
# Procedural Stylized Weapon Mesh Builder (coordinates in Unity Y-up, Z-fwd!)
# ---------------------------------------------------------------------------
class WeaponBuilder:
    def __init__(self, name, mats):
        self.name = name
        self.mats = mats
        self.parts = []  # list of (bmesh, mat_name)

    def _finish_bm(self, bm, mat_name, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0):
        if bevel > 0.0:
            bmesh.ops.bevel(
                bm, geom=list(bm.edges), offset=bevel, segments=2,
                affect='EDGES', clamp_overlap=True
            )
        m_rot = Euler(rot, 'XYZ').to_matrix().to_4x4()
        m_tr = Matrix.Translation(Vector(loc))
        bmesh.ops.transform(bm, matrix=m_tr @ m_rot, verts=bm.verts)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self.parts.append((bm, mat_name))

    def box(self, size, loc=(0, 0, 0), rot=(0, 0, 0), mat="GunMetal", bevel=0.002):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
        self._finish_bm(bm, mat, loc, rot, bevel=min(bevel, min(size) * 0.35))

    def cyl(self, r1, r2, length, axis='Z', loc=(0, 0, 0), rot=(0, 0, 0), segs=14, mat="GunMetal", cap=True):
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm, cap_ends=cap, cap_tris=False, segments=segs,
            radius1=r1, radius2=r2, depth=length
        )
        # create_cone is along local Z; rotate if axis is X or Y
        if axis == 'Y':
            bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(-math.pi / 2, 3, 'X'), verts=bm.verts)
        elif axis == 'X':
            bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(math.pi / 2, 3, 'Y'), verts=bm.verts)
        self._finish_bm(bm, mat, loc, rot, bevel=0.0)

    def sphere(self, rx, ry, rz, loc=(0, 0, 0), rot=(0, 0, 0), segs=14, rings=10, mat="GunMetal"):
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
        bmesh.ops.scale(bm, vec=Vector((rx, ry, rz)), verts=bm.verts)
        self._finish_bm(bm, mat, loc, rot, bevel=0.0)

    def lathe_y(self, profile, segs=16, mat="WoodLight", loc=(0, 0, 0), rot=(0, 0, 0)):
        """profile = list of (y, radius). Builds smooth turned shaft along Y."""
        bm = bmesh.new()
        rings = []
        for y, r in profile:
            r = max(0.0004, r)
            ring = []
            for s in range(segs):
                a = 2.0 * math.pi * s / segs
                ring.append(bm.verts.new((r * math.cos(a), y, r * math.sin(a))))
            rings.append(ring)
        for i in range(len(rings) - 1):
            r0, r1 = rings[i], rings[i + 1]
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new([r0[s], r0[sn], r1[sn], r1[s]])
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        self._finish_bm(bm, mat, loc, rot, bevel=0.0)

    def blade_profile(self, sections, mat="SteelBright", loc=(0, 0, 0), rot=(0, 0, 0)):
        """
        Builds a sharp beveled blade along Y from cross-section specs:
        each section = (y, z_back, z_mid, z_edge, half_thick_back, half_thick_mid).
        Diamond/wedge cross-section: back_l, mid_l, edge, mid_r, back_r (5 verts per ring).
        """
        bm = bmesh.new()
        rings = []
        for y, zb, zm, ze, tb, tm in sections:
            pts = [
                (-tb, y, zb),
                (-tm, y, zm),
                (0.0, y, ze),
                ( tm, y, zm),
                ( tb, y, zb),
            ]
            rings.append([bm.verts.new(p) for p in pts])
        k = 5
        for i in range(len(rings) - 1):
            r0, r1 = rings[i], rings[i + 1]
            for s in range(k):
                sn = (s + 1) % k
                bm.faces.new([r0[s], r0[sn], r1[sn], r1[s]])
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        self._finish_bm(bm, mat, loc, rot, bevel=0.0)

    def tube_path(self, pts, radius, segs=10, mat="RubberGrip"):
        """Builds a curved tubular hose/handle along 3D points."""
        bm = bmesh.new()
        rings = []
        n = len(pts)
        prev_u = None
        for i, p in enumerate(pts):
            p = Vector(p)
            if i == 0:
                tang = (Vector(pts[1]) - p).normalized()
            elif i == n - 1:
                tang = (p - Vector(pts[-2])).normalized()
            else:
                tang = (Vector(pts[i + 1]) - Vector(pts[i - 1])).normalized()
            if prev_u is None:
                up = Vector((0, 1, 0)) if abs(tang.y) < 0.9 else Vector((1, 0, 0))
                u = tang.cross(up).normalized()
            else:
                u = prev_u - tang * prev_u.dot(tang)
                if u.length < 1e-4:
                    up = Vector((0, 1, 0)) if abs(tang.y) < 0.9 else Vector((1, 0, 0))
                    u = tang.cross(up)
                u.normalize()
            prev_u = u
            v = tang.cross(u).normalized()
            ring = []
            for s in range(segs):
                a = 2.0 * math.pi * s / segs
                ring.append(bm.verts.new(p + (u * math.cos(a) + v * math.sin(a)) * radius))
            rings.append(ring)
        for i in range(n - 1):
            r0, r1 = rings[i], rings[i + 1]
            for s in range(segs):
                sn = (s + 1) % segs
                bm.faces.new([r0[s], r0[sn], r1[sn], r1[s]])
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        self._finish_bm(bm, mat, (0, 0, 0), (0, 0, 0), bevel=0.0)

    def export_obj_and_build_preview_obj(self, preview_loc):
        """
        1) Exports clean multi-material OBJ directly in Unity Y-up, Z-forward space using Weapons.mtl
        2) Creates a Blender mesh object (converted to Blender Z-up) for our studio showcase render
        """
        obj_path = os.path.join(OUT_DIR, f"{self.name}.obj")

        # Group parts by material name for clean OBJ submeshes
        by_mat = {}
        for bm, mat_name in self.parts:
            by_mat.setdefault(mat_name, []).append(bm)

        verts_out = []
        norms_out = []
        mat_faces = []
        v_offset = 1
        n_offset = 1

        for mat_name, bm_list in by_mat.items():
            f_lines = []
            for bm in bm_list:
                bmesh.ops.triangulate(bm, faces=bm.faces[:])
                bm.verts.ensure_lookup_table()
                bm.faces.ensure_lookup_table()
                bm.normal_update()
                local_v_start = v_offset
                for v in bm.verts:
                    verts_out.append(f"v {v.co.x:.5f} {v.co.y:.5f} {v.co.z:.5f}\n")
                v_offset += len(bm.verts)

                for face in bm.faces:
                    fn = face.normal
                    norms_out.append(f"vn {fn.x:.4f} {fn.y:.4f} {fn.z:.4f}\n")
                    ni = n_offset
                    n_offset += 1
                    idxs = [local_v_start + v.index for v in face.verts]
                    f_lines.append(f"f {idxs[0]}//{ni} {idxs[1]}//{ni} {idxs[2]}//{ni}\n")
            mat_faces.append((mat_name, f_lines))

        with open(obj_path, "w", encoding="utf-8") as f:
            f.write(f"# TheLastEmpire Stylized Weapon: {self.name}\n")
            f.write(f"mtllib {MTL_NAME}\n")
            f.write("o default\n")
            f.writelines(verts_out)
            f.writelines(norms_out)
            for mat_name, f_lines in mat_faces:
                f.write(f"usemtl {mat_name}\n")
                f.writelines(f_lines)

        # Also assemble a Blender preview object (rotated from Y-up to Blender Z-up for studio camera)
        merged = bmesh.new()
        mat_list = list(by_mat.keys())
        for m_idx, mat_name in enumerate(mat_list):
            for bm in by_mat[mat_name]:
                v_map = {}
                for v in bm.verts:
                    # Unity (x, y, z) -> Blender studio display: X=z_fwd (side view!), Y=x_width, Z=y_up
                    nv = merged.verts.new((v.co.z, -v.co.x, v.co.y))
                    v_map[v.index] = nv
                for face in bm.faces:
                    try:
                        nf = merged.faces.new([v_map[v.index] for v in face.verts])
                        nf.material_index = m_idx
                        nf.smooth = False
                    except ValueError:
                        pass
                bm.free()

        me = bpy.data.meshes.new(self.name)
        merged.to_mesh(me)
        merged.free()
        obj = bpy.data.objects.new(self.name, me)
        bpy.context.collection.objects.link(obj)
        for mat_name in mat_list:
            obj.data.materials.append(self.mats[mat_name])

        # Center & normalize size inside showcase cell
        xs = [v.co.x for v in me.vertices]
        ys = [v.co.y for v in me.vertices]
        zs = [v.co.z for v in me.vertices]
        cx, cy, cz = (min(xs)+max(xs))*0.5, (min(ys)+max(ys))*0.5, (min(zs)+max(zs))*0.5
        span = max(max(xs)-min(xs), max(ys)-min(ys), max(zs)-min(zs), 0.01)
        scale = 0.72 / span
        for v in me.vertices:
            v.co = (v.co - Vector((cx, cy, cz))) * scale
        obj.location = Vector(preview_loc)
        obj.rotation_euler = Euler((0.18, -0.35, 0.25), 'XYZ')
        return obj


# ===========================================================================
# ALL 19 STYLIZED WEAPONS (MATCHING REFERENCE ICON SHEET 1-TO-1!)
# ===========================================================================

def build_pistol(mats):
    # Row 1 Col 6: Dark grey/black tactical pistol with bright yellow grip base & rear slide trim
    w = WeaponBuilder("Weapon_Pistol", mats)
    # Grip (ergonomically raked back along -Z)
    w.box((0.028, 0.115, 0.048), (0, -0.068, -0.036), rot=(0.24, 0, 0), mat="DarkPolymer", bevel=0.004)
    w.box((0.031, 0.088, 0.036), (0, -0.066, -0.036), rot=(0.24, 0, 0), mat="RubberGrip", bevel=0.003)
    # Iconic yellow magazine baseplate & grip stripe
    w.box((0.032, 0.020, 0.054), (0, -0.125, -0.050), rot=(0.24, 0, 0), mat="HazardYellow", bevel=0.003)
    # Lower frame & yellow accent trim along slide bottom
    w.box((0.032, 0.022, 0.175), (0, -0.006, 0.026), mat="DarkPolymer", bevel=0.003)
    w.box((0.034, 0.012, 0.178), (0, 0.003, 0.027), mat="HazardYellow", bevel=0.002)
    # Upper chunky gunmetal slide
    w.box((0.034, 0.034, 0.184), (0, 0.023, 0.026), mat="GunMetal", bevel=0.004)
    # Yellow rear slide serrations (signature icon look!)
    w.box((0.036, 0.028, 0.042), (0, 0.024, -0.038), mat="HazardYellow", bevel=0.003)
    for sz in (-0.048, -0.038, -0.028):
        w.box((0.038, 0.020, 0.004), (0, 0.024, sz), mat="DarkPolymer", bevel=0.0005)
    # Barrel + dark muzzle bore + sights
    w.cyl(0.010, 0.010, 0.035, 'Z', (0, 0.024, 0.124), mat="SteelBright")
    w.cyl(0.006, 0.006, 0.010, 'Z', (0, 0.024, 0.139), mat="RubberGrip")
    w.box((0.010, 0.010, 0.012), (0, 0.043, 0.105), mat="HazardYellow", bevel=0.001)
    w.box((0.022, 0.010, 0.012), (0, 0.043, -0.056), mat="DarkPolymer", bevel=0.001)
    # Trigger guard & trigger
    w.box((0.014, 0.008, 0.058), (0, -0.044, 0.024), mat="DarkPolymer", bevel=0.002)
    w.box((0.014, 0.034, 0.008), (0, -0.028, 0.052), mat="DarkPolymer", bevel=0.002)
    w.box((0.008, 0.022, 0.008), (0, -0.024, 0.018), rot=(0.35, 0, 0), mat="HazardYellow", bevel=0.001)
    return w


def build_shotgun(mats):
    # Row 2 Col 1: Sawed-off double-barrel shotgun with twin silver barrels, dark receiver, yellow pump/forend & grip
    w = WeaponBuilder("Weapon_Shotgun", mats)
    # Curved pistol grip (dark + yellow bottom cap, raked back cleanly!)
    w.box((0.034, 0.095, 0.055), (0, -0.052, -0.115), rot=(0.55, 0, 0), mat="DarkPolymer", bevel=0.006)
    w.box((0.036, 0.028, 0.056), (0, -0.088, -0.142), rot=(0.55, 0, 0), mat="HazardYellow", bevel=0.005)
    # Chunky break-action receiver
    w.box((0.050, 0.058, 0.145), (0, 0.002, -0.035), mat="GunMetal", bevel=0.006)
    w.box((0.054, 0.018, 0.085), (0, 0.026, -0.025), mat="DarkPolymer", bevel=0.003)
    # Twin side-by-side silver barrels + dark muzzles
    for sx in (-0.017, 0.017):
        w.cyl(0.016, 0.016, 0.365, 'Z', (sx, 0.016, 0.205), segs=14, mat="SteelBright")
        w.cyl(0.011, 0.011, 0.018, 'Z', (sx, 0.016, 0.382), segs=12, mat="RubberGrip")
    # Center barrel rib + front bead
    w.box((0.014, 0.012, 0.340), (0, 0.031, 0.195), mat="DarkPolymer", bevel=0.002)
    w.sphere(0.006, 0.006, 0.006, (0, 0.039, 0.365), mat="HazardYellow")
    # Iconic chunky yellow forend under barrels
    w.box((0.048, 0.036, 0.185), (0, -0.014, 0.145), mat="HazardYellow", bevel=0.007)
    for rz in (0.08, 0.12, 0.16, 0.20):
        w.box((0.051, 0.028, 0.010), (0, -0.015, rz), mat="DarkPolymer", bevel=0.001)
    # Trigger guard
    w.box((0.016, 0.009, 0.068), (0, -0.046, -0.045), mat="DarkPolymer", bevel=0.002)
    w.box((0.016, 0.032, 0.009), (0, -0.030, -0.012), mat="DarkPolymer", bevel=0.002)
    return w


def build_rifle(mats):
    # Row 2 Col 2: Tactical assault rifle with yellow stock/handguard accents & curved mag
    w = WeaponBuilder("Weapon_Rifle", mats)
    # Skeletal yellow/black buttstock
    w.box((0.030, 0.085, 0.024), (0, -0.018, -0.320), mat="HazardYellow", bevel=0.004)
    w.box((0.026, 0.022, 0.190), (0, 0.016, -0.225), mat="HazardYellow", bevel=0.004)
    w.box((0.022, 0.018, 0.165), (0, -0.044, -0.235), rot=(-0.18, 0, 0), mat="HazardYellow", bevel=0.003)
    w.box((0.032, 0.088, 0.014), (0, -0.018, -0.334), mat="RubberGrip", bevel=0.003)
    # Receiver (dark charcoal + yellow side accent bar)
    w.box((0.042, 0.064, 0.220), (0, 0.008, -0.020), mat="DarkPolymer", bevel=0.005)
    w.box((0.046, 0.018, 0.130), (0, 0.018, -0.025), mat="GunMetal", bevel=0.003)
    # Top rail & rear/front sights
    w.box((0.026, 0.024, 0.150), (0, 0.052, -0.020), mat="DarkPolymer", bevel=0.003)
    w.box((0.028, 0.010, 0.130), (0, 0.064, -0.020), mat="HazardYellow", bevel=0.002)
    # Grip
    w.box((0.028, 0.098, 0.042), (0, -0.064, -0.090), rot=(0.28, 0, 0), mat="RubberGrip", bevel=0.004)
    # Smooth forward-curved tactical banana magazine
    w.box((0.028, 0.088, 0.048), (0, -0.062, 0.042), rot=(-0.14, 0, 0), mat="DarkPolymer", bevel=0.004)
    w.box((0.027, 0.088, 0.046), (0, -0.132, 0.064), rot=(-0.34, 0, 0), mat="GunMetal", bevel=0.004)
    w.box((0.030, 0.016, 0.050), (0, -0.174, 0.080), rot=(-0.34, 0, 0), mat="HazardYellow", bevel=0.002)
    # Yellow ventilated handguard + barrel + muzzle brake
    w.box((0.044, 0.046, 0.195), (0, 0.012, 0.185), mat="HazardYellow", bevel=0.006)
    for hz in (0.125, 0.165, 0.205, 0.245):
        w.box((0.047, 0.018, 0.014), (0, 0.016, hz), mat="DarkPolymer", bevel=0.001)
    w.cyl(0.013, 0.013, 0.175, 'Z', (0, 0.016, 0.360), mat="GunMetal")
    w.box((0.014, 0.038, 0.018), (0, 0.044, 0.325), mat="HazardYellow", bevel=0.002)
    w.cyl(0.018, 0.016, 0.042, 'Z', (0, 0.016, 0.440), mat="DarkPolymer")
    return w


def build_smg(mats):
    # Row 4 Col 4: Compact boxy tactical SMG (UZI/MP5 vibe) with yellow folding stock & accents
    w = WeaponBuilder("Weapon_SMG", mats)
    # Compact rear wire/folding stock with yellow buttpad
    w.box((0.026, 0.014, 0.135), (0, 0.014, -0.185), mat="GunMetal", bevel=0.002)
    w.box((0.028, 0.072, 0.022), (0, -0.016, -0.252), mat="HazardYellow", bevel=0.004)
    # Chunky boxy receiver
    w.box((0.044, 0.068, 0.230), (0, 0.012, -0.005), mat="DarkPolymer", bevel=0.006)
    w.box((0.048, 0.022, 0.145), (0, 0.028, -0.010), mat="GunMetal", bevel=0.003)
    w.box((0.047, 0.014, 0.080), (0, -0.008, 0.025), mat="HazardYellow", bevel=0.002)
    # Top iron sights & charging knob
    w.box((0.022, 0.018, 0.020), (0, 0.054, -0.095), mat="GunMetal", bevel=0.002)
    w.box((0.014, 0.022, 0.014), (0, 0.056, 0.092), mat="HazardYellow", bevel=0.002)
    # Pistol grip + clean forward-raked magazine
    w.box((0.028, 0.095, 0.042), (0, -0.062, -0.068), rot=(0.22, 0, 0), mat="RubberGrip", bevel=0.004)
    w.box((0.026, 0.125, 0.038), (0, -0.092, 0.042), rot=(-0.16, 0, 0), mat="DarkPolymer", bevel=0.004)
    w.box((0.029, 0.016, 0.042), (0, -0.154, 0.052), rot=(-0.16, 0, 0), mat="HazardYellow", bevel=0.002)
    # Foregrip stub + stubby shrouded barrel + yellow collar
    w.cyl(0.016, 0.016, 0.095, 'Z', (0, 0.014, 0.152), mat="GunMetal")
    w.cyl(0.020, 0.020, 0.024, 'Z', (0, 0.014, 0.135), mat="HazardYellow")
    w.cyl(0.011, 0.011, 0.012, 'Z', (0, 0.014, 0.198), mat="RubberGrip")
    return w


def build_sniper(mats):
    # Row 4 Col 5: Long precision sniper rifle with olive-yellow sculpted stock & big top scope
    w = WeaponBuilder("Weapon_SniperRifle", mats)
    # Sculpted olive-yellow sniper stock & thumbhole chassis
    w.box((0.038, 0.088, 0.160), (0, -0.020, -0.355), mat="SniperYellow", bevel=0.007)
    w.box((0.040, 0.092, 0.018), (0, -0.020, -0.440), mat="RubberGrip", bevel=0.004)
    w.box((0.034, 0.026, 0.120), (0, 0.022, -0.330), mat="DarkPolymer", bevel=0.004)  # cheek rest
    w.box((0.036, 0.048, 0.340), (0, -0.005, -0.105), mat="SniperYellow", bevel=0.006)
    w.box((0.032, 0.095, 0.044), (0, -0.068, -0.172), rot=(0.28, 0, 0), mat="SniperYellow", bevel=0.005)
    # Slim magazine
    w.box((0.026, 0.062, 0.062), (0, -0.056, -0.040), mat="DarkPolymer", bevel=0.003)
    # Matte black receiver & long precision barrel + muzzle brake
    w.box((0.036, 0.036, 0.240), (0, 0.028, -0.055), mat="DarkPolymer", bevel=0.004)
    w.cyl(0.014, 0.012, 0.490, 'Z', (0, 0.030, 0.365), mat="DarkPolymer")
    w.box((0.028, 0.028, 0.062), (0, 0.030, 0.625), mat="GunMetal", bevel=0.004)
    # Bolt handle
    w.cyl(0.006, 0.006, 0.038, 'X', (0.026, 0.026, -0.110), mat="SteelBright")
    w.sphere(0.011, 0.011, 0.011, (0.046, 0.026, -0.110), mat="SteelBright")
    # High-mount telescopic sniper scope with bright yellow rings!
    for mz in (-0.115, 0.015):
        w.box((0.024, 0.032, 0.020), (0, 0.058, mz), mat="DarkPolymer", bevel=0.002)
    w.cyl(0.017, 0.017, 0.230, 'Z', (0, 0.084, -0.045), segs=14, mat="GunMetal")
    w.cyl(0.024, 0.020, 0.058, 'Z', (0, 0.084, 0.088), segs=14, mat="DarkPolymer")
    w.cyl(0.022, 0.019, 0.046, 'Z', (0, 0.084, -0.175), segs=14, mat="DarkPolymer")
    for rz in (-0.115, 0.015):
        w.cyl(0.021, 0.021, 0.018, 'Z', (0, 0.084, rz), segs=14, mat="HazardYellow")
    return w


def build_revolver(mats):
    # Row 4 Col 6: Classic chrome-silver 6-shot revolver with fluted cylinder & warm wood grip
    w = WeaponBuilder("Weapon_Revolver", mats)
    # Smooth contoured warm-brown wooden pistol grip + brass medallion
    w.box((0.032, 0.118, 0.048), (0, -0.064, -0.062), rot=(0.44, 0, 0), mat="WoodFinish", bevel=0.009)
    w.sphere(0.018, 0.024, 0.028, (0, -0.112, -0.088), rot=(0.44, 0, 0), mat="WoodFinish")
    w.cyl(0.007, 0.007, 0.036, 'X', (0, -0.072, -0.066), mat="BrassGold")
    # Chrome receiver frame + top strap
    w.box((0.034, 0.056, 0.115), (0, 0.004, 0.004), mat="ChromeSilver", bevel=0.005)
    # Fluted revolving cylinder
    w.cyl(0.028, 0.028, 0.068, 'Z', (0, 0.010, 0.012), segs=14, mat="SteelBright")
    for i in range(6):
        a = i * math.pi / 3.0
        w.cyl(0.006, 0.006, 0.056, 'Z', (math.cos(a)*0.026, 0.010 + math.sin(a)*0.026, 0.012), segs=8, mat="GunMetal")
    # Heavy ribbed silver barrel + ejector rod + front blade sight
    w.cyl(0.015, 0.014, 0.155, 'Z', (0, 0.022, 0.140), segs=12, mat="ChromeSilver")
    w.box((0.014, 0.016, 0.145), (0, 0.034, 0.138), mat="ChromeSilver", bevel=0.002)
    w.cyl(0.007, 0.007, 0.110, 'Z', (0, 0.002, 0.118), segs=10, mat="SteelBright")
    w.box((0.007, 0.016, 0.018), (0, 0.044, 0.205), mat="GunMetal", bevel=0.001)
    # Hammer spur & trigger guard
    w.box((0.012, 0.024, 0.020), (0, 0.034, -0.054), rot=(-0.45, 0, 0), mat="GunMetal", bevel=0.002)
    w.box((0.014, 0.008, 0.056), (0, -0.042, 0.006), mat="ChromeSilver", bevel=0.002)
    w.box((0.014, 0.032, 0.008), (0, -0.026, 0.034), mat="ChromeSilver", bevel=0.002)
    return w


def build_flamethrower(mats):
    # Row 5 Col 1: Twin bright yellow fuel canisters + black hoses + chunky flamethrower lance!
    w = WeaponBuilder("Weapon_Flamethrower", mats)
    # Twin vertical pressurized bright-yellow fuel tanks
    for tx in (-0.052, 0.052):
        w.cyl(0.042, 0.042, 0.175, 'Y', (tx, -0.008, -0.150), segs=14, mat="HazardYellow")
        w.sphere(0.042, 0.026, 0.042, (tx, 0.080, -0.150), mat="HazardYellow")
        w.sphere(0.042, 0.026, 0.042, (tx, -0.095, -0.150), mat="HazardYellow")
        w.cyl(0.016, 0.016, 0.028, 'Y', (tx, 0.106, -0.150), mat="GunMetal")
        for by in (-0.055, 0.040):
            w.cyl(0.045, 0.045, 0.016, 'Y', (tx, by, -0.150), segs=14, mat="DarkPolymer")
    # Tank crossbar & black fuel hose looping to receiver
    w.box((0.125, 0.026, 0.042), (0, 0.010, -0.150), mat="DarkPolymer", bevel=0.004)
    w.tube_path([(-0.052, -0.090, -0.145), (-0.070, -0.125, -0.070), (-0.028, -0.055, 0.015)], 0.012, mat="RubberGrip")
    w.tube_path([( 0.052, -0.090, -0.145), ( 0.070, -0.125, -0.070), ( 0.028, -0.055, 0.015)], 0.012, mat="RubberGrip")
    # Main gunmetal receiver & pistol grip
    w.box((0.058, 0.068, 0.240), (0, 0.004, 0.045), mat="GunMetal", bevel=0.007)
    w.box((0.032, 0.098, 0.046), (0, -0.068, -0.024), rot=(0.22, 0, 0), mat="RubberGrip", bevel=0.005)
    # Heavy perforated lance barrel + flared nozzle + glowing orange-red pilot igniter tip
    w.cyl(0.024, 0.022, 0.300, 'Z', (0, 0.014, 0.305), segs=14, mat="DarkPolymer")
    for hz in (0.21, 0.26, 0.31, 0.36):
        w.cyl(0.028, 0.028, 0.014, 'Z', (0, 0.014, hz), segs=14, mat="HazardYellow")
    w.cyl(0.026, 0.036, 0.068, 'Z', (0, 0.014, 0.482), segs=14, mat="GunMetal")
    w.cyl(0.014, 0.014, 0.035, 'Z', (0, 0.014, 0.520), segs=12, mat="SafetyOrange")
    # Under-barrel pilot gas tube
    w.cyl(0.010, 0.010, 0.260, 'Z', (0, -0.024, 0.315), mat="SteelBright")
    w.sphere(0.014, 0.014, 0.018, (0, -0.024, 0.460), mat="FireRed")
    return w


def build_rocket_launcher(mats):
    # Row 5 Col 2: Chunky olive-green bazooka tube with flared muzzle, yellow rings & pistol grips
    w = WeaponBuilder("Weapon_RocketLauncher", mats)
    # Main olive-green launch tube
    w.cyl(0.056, 0.056, 0.780, 'Z', (0, 0.015, 0.065), segs=18, mat="MilitaryGreen")
    # Flared dark-grey front muzzle bell & rear venturi exhaust cone
    w.cyl(0.058, 0.074, 0.115, 'Z', (0, 0.015, 0.505), segs=18, mat="DarkPolymer")
    w.cyl(0.054, 0.054, 0.025, 'Z', (0, 0.015, 0.556), segs=16, mat="RubberGrip")
    w.cyl(0.070, 0.058, 0.115, 'Z', (0, 0.015, -0.375), segs=18, mat="DarkPolymer")
    # Iconic bright yellow hazard band rings around tube
    for rz in (-0.24, 0.36):
        w.cyl(0.061, 0.061, 0.036, 'Z', (0, 0.015, rz), segs=18, mat="HazardYellow")
    # Center reinforcement collar + side sight
    w.cyl(0.062, 0.062, 0.095, 'Z', (0, 0.015, 0.045), segs=18, mat="GunMetal")
    w.box((0.036, 0.038, 0.088), (0.068, 0.042, 0.115), mat="DarkPolymer", bevel=0.005)
    w.cyl(0.014, 0.014, 0.096, 'Z', (0.068, 0.042, 0.115), mat="HazardYellow")
    # Pistol grip + front support grip + trigger box
    w.box((0.042, 0.038, 0.145), (0, -0.050, 0.025), mat="DarkPolymer", bevel=0.005)
    w.box((0.032, 0.098, 0.046), (0, -0.105, -0.018), rot=(0.22, 0, 0), mat="RubberGrip", bevel=0.005)
    w.cyl(0.018, 0.016, 0.092, 'Y', (0, -0.088, 0.215), mat="RubberGrip")
    return w


def build_crossbow(mats):
    # Row 5 Col 3: Tactical black & yellow crossbow with swept bow limbs, string & loaded bolt!
    w = WeaponBuilder("Weapon_Crossbow", mats)
    # Stock & main rail
    w.box((0.036, 0.044, 0.420), (0, -0.004, -0.065), mat="DarkPolymer", bevel=0.006)
    w.box((0.040, 0.020, 0.260), (0, 0.018, 0.030), mat="HazardYellow", bevel=0.003)
    # Buttstock end + pistol grip
    w.box((0.038, 0.072, 0.036), (0, -0.018, -0.285), mat="HazardYellow", bevel=0.005)
    w.box((0.028, 0.092, 0.044), (0, -0.066, -0.102), rot=(0.28, 0, 0), mat="RubberGrip", bevel=0.005)
    # Front stirrup ring
    w.box((0.088, 0.012, 0.012), (0, 0.002, 0.225), mat="HazardYellow", bevel=0.002)
    for sx in (-0.042, 0.042):
        w.box((0.012, 0.012, 0.075), (sx, 0.002, 0.188), mat="HazardYellow", bevel=0.002)
    # Swept bow prod limbs (left & right curving cleanly backward like icon!)
    for side in (-1.0, 1.0):
        pts = [
            (side * 0.025, 0.008, 0.145),
            (side * 0.135, 0.008, 0.135),
            (side * 0.245, 0.008, 0.085),
            (side * 0.305, 0.008, 0.010),
        ]
        w.tube_path(pts, 0.016, segs=10, mat="DarkPolymer")
        w.sphere(0.022, 0.022, 0.022, (side * 0.305, 0.008, 0.010), mat="HazardYellow")
    # Taut bowstring pulled back to catch
    w.tube_path([(-0.305, 0.008, 0.010), (0.0, 0.014, -0.080), (0.305, 0.008, 0.010)], 0.005, segs=6, mat="SteelBright")
    # Loaded bolt on rail with steel broadhead tip & yellow fletching
    w.cyl(0.007, 0.007, 0.310, 'Z', (0, 0.032, 0.068), segs=8, mat="DarkPolymer")
    w.cyl(0.016, 0.001, 0.048, 'Z', (0, 0.032, 0.238), segs=4, mat="SteelBright")
    w.box((0.032, 0.004, 0.038), (0, 0.032, -0.068), mat="HazardYellow", bevel=0.001)
    return w


def build_knife(mats):
    # Row 2 Col 6: Combat survival knife with serrated steel blade, black/yellow handle & guard
    w = WeaponBuilder("Weapon_Knife", mats)
    # Ribbed dark grip + yellow pommel & crossguard
    w.cyl(0.017, 0.018, 0.115, 'Y', (0, -0.058, 0), segs=12, mat="RubberGrip")
    for ry in (-0.095, -0.068, -0.041):
        w.cyl(0.020, 0.020, 0.010, 'Y', (0, ry, 0), segs=12, mat="DarkPolymer")
    w.cyl(0.022, 0.018, 0.024, 'Y', (0, -0.125, 0), segs=12, mat="HazardYellow")
    w.box((0.028, 0.018, 0.068), (0, 0.006, 0.004), mat="HazardYellow", bevel=0.004)
    # Bowie clip-point blade along +Y, cutting edge toward +Z
    w.blade_profile([
        (0.014, -0.018, 0.004, 0.028, 0.007, 0.009),
        (0.085, -0.018, 0.006, 0.032, 0.007, 0.009),
        (0.155, -0.014, 0.008, 0.030, 0.006, 0.008),
        (0.205, -0.004, 0.008, 0.018, 0.003, 0.004),
        (0.235,  0.002, 0.004, 0.006, 0.001, 0.001),
    ], mat="SteelBright")
    # Spine serration teeth along -Z back edge
    for sy in (0.045, 0.068, 0.091, 0.114):
        w.box((0.009, 0.011, 0.011), (0, sy, -0.020), rot=(0.78, 0, 0), mat="GunMetal", bevel=0.001)
    return w


def build_baseball_bat(mats):
    # Row 3 Col 1: Warm golden wood baseball bat with dark/yellow grip tape & knob
    w = WeaponBuilder("Weapon_BaseballBat", mats)
    # Smooth turned bat profile along Y
    w.lathe_y([
        (-0.265, 0.017),
        (-0.140, 0.018),
        (-0.020, 0.020),
        ( 0.120, 0.030),
        ( 0.250, 0.042),
        ( 0.365, 0.044),
        ( 0.405, 0.036),
        ( 0.420, 0.016),
    ], segs=16, mat="WoodLight")
    # Pommel knob & yellow grip wrap bands
    w.sphere(0.026, 0.018, 0.026, (0, -0.285, 0), mat="HazardYellow")
    for gy in (-0.235, -0.195, -0.155, -0.115):
        w.cyl(0.021, 0.021, 0.016, 'Y', (0, gy, 0), segs=14, mat="RubberGrip")
    for gy in (-0.255, -0.215, -0.175, -0.135):
        w.cyl(0.020, 0.020, 0.014, 'Y', (0, gy, 0), segs=14, mat="HazardYellow")
    return w


def build_machete(mats):
    # Row 3 Col 2: Heavy broad curved steel machete blade with black/yellow handle
    w = WeaponBuilder("Weapon_Machete", mats)
    w.cyl(0.018, 0.019, 0.135, 'Y', (0, -0.068, 0), segs=12, mat="RubberGrip")
    w.cyl(0.024, 0.020, 0.028, 'Y', (0, -0.145, 0), segs=12, mat="HazardYellow")
    w.box((0.030, 0.022, 0.056), (0, 0.008, 0.006), mat="HazardYellow", bevel=0.004)
    # Broad curved flaring machete blade
    w.blade_profile([
        (0.018, -0.018, 0.004, 0.028, 0.006, 0.008),
        (0.110, -0.018, 0.010, 0.042, 0.006, 0.008),
        (0.220, -0.014, 0.022, 0.062, 0.006, 0.008),
        (0.305,  0.002, 0.034, 0.066, 0.004, 0.006),
        (0.355,  0.024, 0.038, 0.048, 0.002, 0.002),
    ], mat="SteelBright")
    return w


def build_fire_axe(mats):
    # Row 3 Col 4: Iconic red fire-axe head with bright silver bevel edge + dark/red/yellow handle
    w = WeaponBuilder("Weapon_FireAxe", mats)
    # Ergonomic oval handle shaft along Y
    w.box((0.028, 0.680, 0.040), (0, -0.045, 0), mat="DarkPolymer", bevel=0.008)
    # Signature red center grip wrap + yellow pommel & collar bands
    w.box((0.032, 0.165, 0.044), (0, -0.135, 0), mat="FireRed", bevel=0.007)
    w.box((0.035, 0.045, 0.048), (0, -0.395, 0), mat="HazardYellow", bevel=0.008)
    w.box((0.034, 0.026, 0.046), (0, -0.035, 0), mat="HazardYellow", bevel=0.005)
    # Vivid Red Fire-Axe Head (curved blade cheek + rear pick spike!)
    w.box((0.044, 0.095, 0.072), (0, 0.255, 0.000), mat="FireRed", bevel=0.008)
    # Flared red axe body pointing +Z
    w.blade_profile([
        (0.160, 0.020, 0.065, 0.115, 0.016, 0.011),
        (0.255, 0.015, 0.072, 0.132, 0.018, 0.012),
        (0.335, 0.020, 0.065, 0.118, 0.015, 0.010),
    ], mat="FireRed")
    # Bright sharp silver cutting edge at front (+Z)
    w.blade_profile([
        (0.155, 0.105, 0.128, 0.148, 0.010, 0.005),
        (0.255, 0.118, 0.144, 0.166, 0.011, 0.006),
        (0.340, 0.108, 0.130, 0.150, 0.009, 0.005),
    ], mat="SteelBright")
    # Solid pointed rear pick spike (-Z) seated deeply into axe head
    w.cyl(0.020, 0.003, 0.105, 'Z', (0, 0.258, -0.066), rot=(0.10, 0, 0), segs=6, mat="FireRed")
    return w


def build_katana(mats):
    # Row 3 Col 5: Sleek curved samurai katana with black/yellow wrapped tsuka, gold tsuba & curved silver blade
    w = WeaponBuilder("Weapon_Katana", mats)
    # Tsuka handle with diamond black & yellow wrap bands
    w.box((0.024, 0.220, 0.034), (0, -0.115, 0), mat="RubberGrip", bevel=0.006)
    for wy in (-0.195, -0.160, -0.125, -0.090, -0.055, -0.025):
        w.box((0.027, 0.015, 0.037), (0, wy, 0), mat="HazardYellow", bevel=0.002)
    w.box((0.028, 0.022, 0.038), (0, -0.232, 0), mat="BrassGold", bevel=0.004)
    # Iconic round gold Tsuba handguard + habaki collar
    w.cyl(0.044, 0.044, 0.014, 'Y', (0, 0.002, 0), segs=16, mat="BrassGold")
    w.box((0.024, 0.024, 0.034), (0, 0.020, 0.002), mat="BrassGold", bevel=0.003)
    # Gently curved sharp silver Katana blade extending to +0.72!
    w.blade_profile([
        (0.030, -0.016, 0.002, 0.022, 0.006, 0.007),
        (0.200, -0.014, 0.004, 0.024, 0.006, 0.007),
        (0.400, -0.004, 0.014, 0.032, 0.005, 0.006),
        (0.580,  0.014, 0.030, 0.046, 0.004, 0.005),
        (0.680,  0.028, 0.040, 0.050, 0.003, 0.004),
        (0.730,  0.042, 0.046, 0.048, 0.001, 0.001),
    ], mat="SteelBright")
    return w


def build_sledgehammer(mats):
    # Row 3 Col 6: Heavy octagonal steel sledgehammer head on yellow/black ergonomic handle
    w = WeaponBuilder("Weapon_Sledgehammer", mats)
    # Handle shaft (yellow upper shaft + dark grip lower + yellow pommel!)
    w.cyl(0.020, 0.022, 0.340, 'Y', (0, 0.075, 0), segs=12, mat="HazardYellow")
    w.cyl(0.023, 0.021, 0.310, 'Y', (0, -0.240, 0), segs=12, mat="RubberGrip")
    w.cyl(0.026, 0.024, 0.032, 'Y', (0, -0.410, 0), segs=12, mat="HazardYellow")
    w.cyl(0.025, 0.025, 0.024, 'Y', (0, -0.088, 0), segs=12, mat="DarkPolymer")
    # Heavy chamfered gunmetal steel hammerhead
    w.box((0.088, 0.118, 0.205), (0, 0.285, 0), mat="GunMetal", bevel=0.016)
    # Bright steel impact faces on both ends (+Z and -Z)
    for sz in (-0.106, 0.106):
        w.box((0.074, 0.098, 0.018), (0, 0.285, sz), mat="SteelBright", bevel=0.005)
    # Reinforcement collar
    w.box((0.052, 0.038, 0.058), (0, 0.215, 0), mat="DarkPolymer", bevel=0.005)
    return w


def build_survival_spear(mats):
    # Row 4 Col 1: Long wooden shaft with dark wraps & sharp diamond silver spearhead
    w = WeaponBuilder("Weapon_SurvivalSpear", mats)
    # Long tapered wooden pole
    w.lathe_y([
        (-0.620, 0.018),
        (-0.200, 0.021),
        ( 0.200, 0.022),
        ( 0.500, 0.020),
    ], segs=12, mat="WoodLight")
    # Leather grip wrap near hand + spearhead collar lashing
    w.cyl(0.025, 0.025, 0.165, 'Y', (0, -0.020, 0), segs=12, mat="WoodFinish")
    w.cyl(0.026, 0.024, 0.068, 'Y', (0, 0.485, 0), segs=12, mat="RubberGrip")
    w.cyl(0.022, 0.020, 0.025, 'Y', (0, -0.625, 0), segs=12, mat="GunMetal")
    # Sharp faceted diamond silver spearhead
    w.blade_profile([
        (0.515, -0.014, 0.000, 0.014, 0.008, 0.010),
        (0.565, -0.044, 0.000, 0.044, 0.004, 0.014),
        (0.675, -0.026, 0.000, 0.026, 0.003, 0.010),
        (0.790,  0.000, 0.000, 0.000, 0.001, 0.002),
    ], mat="SteelBright")
    return w


def build_stun_baton(mats):
    # Row 4 Col 2: Black tactical baton with metallic rings & bright glowing yellow electric emitter head
    w = WeaponBuilder("Weapon_StunBaton", mats)
    # Lower knurled grip + pommel cap
    w.cyl(0.023, 0.022, 0.180, 'Y', (0, -0.105, 0), segs=14, mat="RubberGrip")
    w.sphere(0.025, 0.020, 0.025, (0, -0.205, 0), mat="GunMetal")
    w.cyl(0.028, 0.028, 0.024, 'Y', (0, -0.005, 0), segs=14, mat="SteelBright")
    # Upper dark conductive shaft
    w.cyl(0.022, 0.024, 0.270, 'Y', (0, 0.145, 0), segs=14, mat="DarkPolymer")
    # Power switch button + metallic shock rings
    w.box((0.014, 0.032, 0.018), (0, 0.055, 0.024), mat="HazardYellow", bevel=0.003)
    for ry in (0.14, 0.21, 0.28):
        w.cyl(0.027, 0.027, 0.014, 'Y', (0, ry, 0), segs=14, mat="SteelBright")
    # Glowing bright yellow electric emitter collar & twin stun prongs!
    w.cyl(0.028, 0.030, 0.052, 'Y', (0, 0.315, 0), segs=14, mat="ElectricCyan")
    for sx in (-0.016, 0.016):
        w.cyl(0.007, 0.004, 0.055, 'Y', (sx, 0.365, 0), segs=10, mat="ElectricCyan")
    return w


def build_chainsaw(mats):
    # Row 4 Col 3: Iconic vivid orange motor body, black wrap handle, silver guide bar & dark saw teeth!
    w = WeaponBuilder("Weapon_Chainsaw", mats)
    # Chunky vivid orange beveled motor housing
    w.box((0.128, 0.132, 0.215), (0, 0.012, -0.125), mat="SafetyOrange", bevel=0.018)
    # Side starter recoil cover + exhaust vents
    w.cyl(0.044, 0.044, 0.024, 'X', (0.068, 0.010, -0.125), segs=14, mat="DarkPolymer")
    w.cyl(0.028, 0.028, 0.030, 'X', (0.068, 0.010, -0.125), segs=12, mat="SafetyOrange")
    w.box((0.028, 0.065, 0.068), (-0.064, 0.015, -0.130), mat="GunMetal", bevel=0.005)
    # Curved black wrap-around top handle + rear trigger handle
    w.tube_path([
        (-0.056, 0.020, -0.055),
        (-0.078, 0.115, -0.055),
        ( 0.000, 0.128, -0.080),
        ( 0.068, 0.105, -0.105),
        ( 0.060, 0.040, -0.115),
    ], 0.014, segs=10, mat="RubberGrip")
    w.box((0.036, 0.030, 0.095), (0, -0.025, -0.255), mat="RubberGrip", bevel=0.006)
    w.box((0.036, 0.088, 0.024), (0, 0.012, -0.292), mat="RubberGrip", bevel=0.005)
    # Front hand guard
    w.box((0.088, 0.068, 0.016), (0, 0.092, -0.022), rot=(0.25, 0, 0), mat="DarkPolymer", bevel=0.004)
    # Long bright silver guide bar extending forward along +Z!
    w.box((0.018, 0.082, 0.455), (0, 0.005, 0.210), mat="SteelBright", bevel=0.004)
    w.cyl(0.041, 0.041, 0.018, 'X', (0, 0.005, 0.435), segs=14, mat="SteelBright")
    # Dark center guide groove
    w.box((0.021, 0.024, 0.340), (0, 0.005, 0.210), mat="GunMetal", bevel=0.002)
    # Sculpted chain teeth around top, bottom, and rounded nose!
    for tz in [0.02 + i * 0.045 for i in range(10)]:
        w.box((0.024, 0.016, 0.018), (0,  0.052, tz), rot=(0.35, 0, 0), mat="DarkPolymer", bevel=0.002)
        w.box((0.024, 0.016, 0.018), (0, -0.042, tz), rot=(-0.35, 0, 0), mat="DarkPolymer", bevel=0.002)
    for ang in (-0.8, -0.3, 0.3, 0.8):
        w.box((0.024, 0.016, 0.016), (0, 0.005 + math.sin(ang)*0.046, 0.435 + math.cos(ang)*0.046), mat="DarkPolymer", bevel=0.002)
    return w


def build_grenade(mats):
    # Row 3 Col 3: Olive-green pineapple fragmentation grenade with silver fuse & bright yellow ring pin!
    w = WeaponBuilder("Weapon_Grenade", mats)
    # Olive-green segmented oval body
    w.sphere(0.042, 0.052, 0.042, (0, 0.005, 0), segs=14, rings=10, mat="MilitaryGreen")
    # Pineapple grid ribs hugging the oval body cleanly (horizontal bands + contoured meridian ribs)
    for ry in (-0.022, 0.005, 0.030):
        r = 0.044 if abs(ry) < 0.01 else 0.038
        w.cyl(r, r, 0.006, 'Y', (0, ry, 0), segs=16, mat="DarkPolymer")
    for i in range(6):
        a = i * math.pi / 3.0
        w.sphere(0.004, 0.052, 0.0435, (0, 0.005, 0), rot=(0, a, 0), segs=12, rings=10, mat="DarkPolymer")
    # Silver fuse neck & top head + curved safety spoon
    w.cyl(0.016, 0.018, 0.028, 'Y', (0, 0.064, 0), segs=12, mat="SteelBright")
    w.box((0.028, 0.022, 0.032), (0, 0.084, 0), mat="SteelBright", bevel=0.004)
    w.tube_path([(0, 0.088, 0.014), (0, 0.082, 0.044), (0, 0.025, 0.054), (0, -0.022, 0.046)], 0.006, segs=8, mat="SteelBright")
    # Iconic bright yellow pull-ring pin!
    pts = []
    for s in range(13):
        a = 2.0 * math.pi * s / 12.0
        pts.append((0.022 + math.cos(a)*0.016, 0.072 + math.sin(a)*0.016, -0.014))
    w.tube_path(pts, 0.0045, segs=8, mat="HazardYellow")
    return w


# ===========================================================================
# STUDIO SHOWCASE RENDERER (5 x 4 GRID OF ALL 19 WEAPONS FOR VISUAL QA!)
# ===========================================================================
def render_showcase(scene):
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1280
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("WeaponWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.16, 0.18, 0.22, 1.0)
        bg.inputs["Strength"].default_value = 0.8

    # Studio 3-point lighting
    for name, loc, rot, energy in [
        ("KeyLight",  ( 2.0, -5.0,  3.5), (1.05, 0.0,  0.35), 4.0),
        ("FillLight", (-3.5, -4.0,  1.5), (1.20, 0.0, -0.65), 2.0),
        ("RimLight",  ( 0.0,  4.0,  4.5), (2.30, 0.0,  0.00), 2.5),
    ]:
        ldata = bpy.data.lights.new(name, type='SUN')
        ldata.energy = energy
        lobj = bpy.data.objects.new(name, ldata)
        bpy.context.collection.objects.link(lobj)
        lobj.location = loc
        lobj.rotation_euler = rot

    # Orthographic front camera framing all 5x4 cells
    cam_data = bpy.data.cameras.new("ShowcaseCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = 5.4
    cam_obj = bpy.data.objects.new("ShowcaseCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = (0.0, -6.0, 0.0)
    cam_obj.rotation_euler = (math.pi / 2.0, 0.0, 0.0)
    scene.camera = cam_obj

    scene.render.filepath = PREVIEW_PATH
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    write_mtl()
    mats = make_blender_mats()

    builders = [
        # Ranged (9)
        build_pistol, build_shotgun, build_rifle, build_smg, build_sniper,
        build_revolver, build_flamethrower, build_rocket_launcher, build_crossbow,
        # Melee (9)
        build_knife, build_baseball_bat, build_machete, build_fire_axe, build_katana,
        build_sledgehammer, build_survival_spear, build_stun_baton, build_chainsaw,
        # Throwable (1)
        build_grenade,
    ]

    cols = 5
    for idx, fn in enumerate(builders):
        col = idx % cols
        row = idx // cols
        px = (col - 2.0) * 1.02
        pz = (1.5 - row) * 1.02
        w = fn(mats)
        w.export_obj_and_build_preview_obj((px, 0.0, pz))

    render_showcase(scene)
    print(f"SUCCESSFULLY GENERATED ALL {len(builders)} STYLIZED WEAPONS IN: {OUT_DIR}")
    print(f"SHOWCASE PREVIEW: {PREVIEW_PATH}")


if __name__ == "__main__":
    main()

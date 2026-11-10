"""
Generates All 7 Alien Enemy 3D Models:
1. Alien_Normal: Xenomorph warrior with elongated head dome, claws, and stinger whip-tail
2. Alien_Crawler: 4-legged chitinous stalker/pouncer with 6 compound eyes & venom fangs
3. Alien_Hook: Sinewy predator with 4 thick prehensile barbed harpoon tendrils
4. Alien_Invisible: Phantasm shadow mantis alien with translucent/dark bio-carapace & scythe blades
5. Alien_Smart: Psionic cerebrate alien with massive glowing translucent brain dome & neural tendrils
6. Alien_Spitter: Arched hunchback bio-artillery alien with pulsing toxic bile bladder
7. Alien_Leader: Colossal Hive Queen / Brood Overlord with massive flared crown crest, 4 dorsal spines & brood core
"""

import math
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.append(SCRIPT_DIR)

from ProceduralModelKit import EnemyMeshBuilder, create_material

ROOT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
OUT_DIR = os.path.join(ROOT_DIR, "Assets", "GeneratedModels", "Aliens")
os.makedirs(OUT_DIR, exist_ok=True)


# ==============================================================================
# 1. ALIEN NORMAL (Xenomorph Warrior)
# ==============================================================================
def build_alien_normal():
    b = EnemyMeshBuilder("Alien_Normal")

    MAT_CARAPACE = 0
    MAT_FLESH = 1
    MAT_TEETH = 2
    MAT_GLOW = 3

    materials = [
        create_material("alien_carapace", (0.12, 0.18, 0.14), roughness=0.35, metallic=0.60),
        create_material("alien_flesh", (0.18, 0.12, 0.16), roughness=0.65, metallic=0.10),
        create_material("alien_teeth", (0.88, 0.86, 0.72), roughness=0.40, metallic=0.20),
        create_material("alien_acid_glow", (0.40, 0.95, 0.10), roughness=0.20, emission_rgb=(0.40, 0.95, 0.10), emission_strength=4.0),
    ]

    # Pelvis
    b.add_box((0.0, 0.0, 0.92), (0.34, 0.26, 0.20), MAT_CARAPACE)

    # Digitigrade muscular legs with raptor spurs
    b.add_capsule((0.14, 0.0, 0.90), (0.18, 0.12, 0.56), 0.09, MAT_FLESH, mirror_x=True)
    b.add_capsule((0.18, 0.12, 0.56), (0.14, -0.10, 0.22), 0.07, MAT_CARAPACE, mirror_x=True)
    # 3-toed clawed feet
    b.add_box((0.14, -0.08, 0.08), (0.16, 0.30, 0.10), MAT_CARAPACE, mirror_x=True)
    for fx in [-0.04, 0.0, 0.04]:
        b.add_cone((0.14 + fx, -0.22, 0.08), (0.14 + fx * 1.2, -0.36, 0.04), 0.025, MAT_TEETH, mirror_x=True)

    # Sinewy Torso with ribbed spine
    b.add_capsule((0.0, 0.02, 0.95), (0.0, -0.06, 1.34), 0.22, MAT_FLESH)
    # Dorsal rib ridges
    for rz in [1.05, 1.15, 1.25]:
        b.add_box((0.0, 0.14, rz), (0.28, 0.06, 0.04), MAT_CARAPACE)
    # Glowing bio-veins
    b.add_cylinder((0.0, -0.18, 1.18), (0.0, -0.18, 1.28), 0.04, MAT_GLOW)

    # Elongated Xenomorph Head Dome
    b.add_cylinder((0.0, 0.0, 1.36), (0.0, -0.08, 1.54), 0.10, MAT_FLESH)
    # Sweeping rear cranial crest
    b.add_capsule((0.0, -0.26, 1.58), (0.0, 0.44, 1.70), 0.16, MAT_CARAPACE)
    # Sharp fanged jaw
    b.add_box((0.0, -0.32, 1.48), (0.14, 0.22, 0.12), MAT_CARAPACE)
    for tz in [-0.03, 0.03]:
        b.add_cone((0.06, -0.42, 1.48 + tz), (0.06, -0.48, 1.48 + tz), 0.018, MAT_TEETH, mirror_x=True)

    # Long whip-like tail with stinger blade
    tail_pts = [
        (0.0, 0.16, 0.90),
        (0.0, 0.44, 0.82),
        (0.0, 0.72, 0.68),
        (0.0, 0.96, 0.52),
        (0.0, 1.20, 0.40),
    ]
    for i in range(len(tail_pts) - 1):
        b.add_cylinder(tail_pts[i], tail_pts[i + 1], 0.06 - i * 0.009, MAT_CARAPACE)
    # Stinger blade
    b.add_cone(tail_pts[-1], (0.0, 1.42, 0.36), 0.05, MAT_TEETH)

    # Arms & Talons
    b.add_sphere((0.24, 0.0, 1.30), (0.09, 0.09, 0.09), MAT_FLESH, mirror_x=True)
    b.add_capsule((0.24, 0.0, 1.30), (0.32, -0.14, 0.96), 0.07, MAT_CARAPACE, mirror_x=True)
    b.add_capsule((0.32, -0.14, 0.96), (0.34, -0.32, 0.68), 0.06, MAT_FLESH, mirror_x=True)
    # Claw hand
    b.add_box((0.34, -0.34, 0.64), (0.10, 0.14, 0.08), MAT_CARAPACE, mirror_x=True)
    for cx in [-0.03, 0.0, 0.03]:
        b.add_cone((0.34 + cx, -0.40, 0.64), (0.34 + cx * 1.3, -0.52, 0.60), 0.02, MAT_TEETH, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Alien_Normal.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 2. ALIEN CRAWLER (4-Legged Pouncing Bio-Hound)
# ==============================================================================
def build_alien_crawler():
    b = EnemyMeshBuilder("Alien_Crawler")

    MAT_CHITIN = 0
    MAT_BELLY = 1
    MAT_CLAWS = 2
    MAT_EYES = 3

    materials = [
        create_material("crawler_chitin", (0.28, 0.22, 0.32), roughness=0.45, metallic=0.50),
        create_material("crawler_belly", (0.42, 0.38, 0.44), roughness=0.70, metallic=0.10),
        create_material("crawler_claws", (0.82, 0.78, 0.65), roughness=0.35, metallic=0.30),
        create_material("crawler_eye_glow", (0.15, 0.95, 0.25), roughness=0.20, emission_rgb=(0.15, 0.95, 0.25), emission_strength=4.5),
    ]

    # Elongated low quadrupedal body
    b.add_capsule((0.0, 0.26, 0.50), (0.0, -0.34, 0.46), 0.22, MAT_BELLY)
    # Segmented carapace armor along back
    for y, z in [(0.20, 0.62), (0.0, 0.64), (-0.20, 0.60)]:
        b.add_box((0.0, y, z), (0.34, 0.18, 0.10), MAT_CHITIN)
        b.add_cone((0.0, y, z + 0.05), (0.0, y, z + 0.22), 0.04, MAT_CLAWS)

    # Low predatory skull
    b.add_sphere((0.0, -0.52, 0.44), (0.18, 0.22, 0.14), MAT_CHITIN)
    # Mandibles & venom fangs
    b.add_cone((0.08, -0.70, 0.42), (0.14, -0.84, 0.38), 0.03, MAT_CLAWS, mirror_x=True)
    # 6 glowing emerald eyes
    for ey, ez in [(-0.58, 0.48), (-0.54, 0.50), (-0.50, 0.52)]:
        b.add_sphere((0.10, ey, ez), (0.025, 0.025, 0.025), MAT_EYES, mirror_x=True)

    # 4 Muscular Quadrupedal Pouncing Limbs
    # Front limbs
    b.add_sphere((0.22, -0.24, 0.46), (0.09, 0.09, 0.09), MAT_CHITIN, mirror_x=True)
    b.add_capsule((0.22, -0.24, 0.46), (0.34, -0.32, 0.28), 0.07, MAT_BELLY, mirror_x=True)
    b.add_capsule((0.34, -0.32, 0.28), (0.28, -0.44, 0.06), 0.06, MAT_CHITIN, mirror_x=True)
    b.add_cone((0.28, -0.44, 0.06), (0.28, -0.58, 0.02), 0.04, MAT_CLAWS, mirror_x=True)

    # Rear limbs (longer, bent for spring pouncing)
    b.add_sphere((0.22, 0.24, 0.52), (0.11, 0.11, 0.11), MAT_CHITIN, mirror_x=True)
    b.add_capsule((0.22, 0.24, 0.52), (0.36, 0.38, 0.32), 0.08, MAT_BELLY, mirror_x=True)
    b.add_capsule((0.36, 0.38, 0.32), (0.30, 0.24, 0.06), 0.06, MAT_CHITIN, mirror_x=True)
    b.add_cone((0.30, 0.24, 0.06), (0.30, 0.12, 0.02), 0.045, MAT_CLAWS, mirror_x=True)

    # Whip tail
    b.add_cylinder((0.0, 0.44, 0.48), (0.0, 0.88, 0.42), 0.04, MAT_CHITIN)

    out_path = os.path.join(OUT_DIR, "Alien_Crawler.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 3. ALIEN HOOK (Harpoon Tendril Stalker)
# ==============================================================================
def build_alien_hook():
    b = EnemyMeshBuilder("Alien_Hook")

    MAT_SKIN = 0
    MAT_CARAPACE = 1
    MAT_TENDRIL = 2
    MAT_BARB = 3

    materials = [
        create_material("hook_skin", (0.48, 0.46, 0.44), roughness=0.65, metallic=0.15),
        create_material("hook_carapace", (0.22, 0.20, 0.24), roughness=0.45, metallic=0.60),
        create_material("hook_tendrils", (0.75, 0.15, 0.12), roughness=0.35, emission_rgb=(0.75, 0.15, 0.12), emission_strength=3.0),
        create_material("hook_barbs", (0.88, 0.84, 0.70), roughness=0.30, metallic=0.35),
    ]

    # Tall sinewy pelvis & legs
    b.add_box((0.0, 0.0, 0.94), (0.32, 0.24, 0.20), MAT_CARAPACE)
    b.add_capsule((0.14, 0.0, 0.92), (0.16, -0.04, 0.52), 0.08, MAT_SKIN, mirror_x=True)
    b.add_capsule((0.16, -0.04, 0.52), (0.15, 0.02, 0.14), 0.07, MAT_CARAPACE, mirror_x=True)
    b.add_box((0.15, -0.04, 0.07), (0.14, 0.28, 0.12), MAT_CARAPACE, mirror_x=True)

    # Desiccated tall torso
    b.add_capsule((0.0, 0.0, 0.96), (0.0, -0.06, 1.44), 0.20, MAT_SKIN)
    b.add_box((0.0, 0.10, 1.34), (0.34, 0.14, 0.32), MAT_CARAPACE)

    # Sinister hooded head
    b.add_sphere((0.0, -0.16, 1.68), (0.16, 0.22, 0.18), MAT_SKIN)
    b.add_plate((-0.18, -0.06, 1.82), (0.18, -0.06, 1.82), (0.22, -0.28, 1.64), (-0.22, -0.28, 1.64), 0.04, MAT_CARAPACE)

    # Sickle arms
    b.add_sphere((0.24, 0.0, 1.38), (0.08, 0.08, 0.08), MAT_SKIN, mirror_x=True)
    b.add_capsule((0.24, 0.0, 1.38), (0.32, -0.16, 1.02), 0.06, MAT_SKIN, mirror_x=True)
    b.add_cone((0.32, -0.16, 1.02), (0.34, -0.44, 0.88), 0.045, MAT_BARB, mirror_x=True)

    # 4 Prehensile Barbed Hook Tendrils arching forward from shoulders/back
    tendril_configs = [
        (0.20, 0.18, 1.48, 0.38, -0.62, 1.36),
        (-0.20, 0.18, 1.48, -0.38, -0.62, 1.36),
        (0.14, 0.16, 1.30, 0.28, -0.54, 0.92),
        (-0.14, 0.16, 1.30, -0.28, -0.54, 0.92),
    ]
    for x1, y1, z1, x2, y2, z2 in tendril_configs:
        # Fleshy arched tentacle
        mid_x = (x1 + x2) * 0.5 + (0.10 if x1 > 0 else -0.10)
        mid_y = -0.10
        mid_z = (z1 + z2) * 0.5 + 0.25
        b.add_capsule((x1, y1, z1), (mid_x, mid_y, mid_z), 0.04, MAT_TENDRIL)
        b.add_capsule((mid_x, mid_y, mid_z), (x2, y2, z2), 0.035, MAT_TENDRIL)
        # Wicked barbed bone hook tip
        b.add_cone((x2, y2, z2), (x2, y2 - 0.24, z2 - 0.08), 0.04, MAT_BARB)
        b.add_cone((x2, y2 - 0.12, z2), (x2 + (0.08 if x1 > 0 else -0.08), y2 - 0.04, z2 + 0.08), 0.025, MAT_BARB)

    out_path = os.path.join(OUT_DIR, "Alien_Hook.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 4. ALIEN INVISIBLE (Phantasm Shadow Mantis)
# ==============================================================================
def build_alien_invisible():
    b = EnemyMeshBuilder("Alien_Invisible")

    MAT_SHADOW = 0
    MAT_SCYTHE = 1
    MAT_GLOW = 2

    materials = [
        create_material("shadow_carapace", (0.08, 0.08, 0.11), roughness=0.30, metallic=0.75),
        create_material("shadow_scythe", (0.18, 0.20, 0.24), roughness=0.20, metallic=0.90),
        create_material("shadow_psionic_glow", (0.45, 0.25, 0.95), roughness=0.15, emission_rgb=(0.45, 0.25, 0.95), emission_strength=4.5),
    ]

    # Needle-thin pelvis & digitigrade legs
    b.add_box((0.0, 0.0, 0.90), (0.24, 0.18, 0.16), MAT_SHADOW)
    b.add_capsule((0.10, 0.0, 0.88), (0.16, 0.10, 0.54), 0.05, MAT_SHADOW, mirror_x=True)
    b.add_capsule((0.16, 0.10, 0.54), (0.12, -0.08, 0.18), 0.04, MAT_SHADOW, mirror_x=True)
    b.add_cone((0.12, -0.08, 0.18), (0.12, -0.22, 0.04), 0.035, MAT_SCYTHE, mirror_x=True)

    # Razor-thin aerodynamic torso
    b.add_capsule((0.0, 0.0, 0.92), (0.0, -0.04, 1.32), 0.14, MAT_SHADOW)
    b.add_box((0.0, -0.12, 1.18), (0.08, 0.02, 0.18), MAT_GLOW)

    # Smooth oval faceless head mask
    b.add_sphere((0.0, -0.12, 1.54), (0.12, 0.18, 0.14), MAT_SHADOW)
    b.add_box((0.0, -0.25, 1.54), (0.10, 0.02, 0.02), MAT_GLOW)

    # Praying-mantis folded forearm scythes
    b.add_sphere((0.16, 0.0, 1.28), (0.06, 0.06, 0.06), MAT_SHADOW, mirror_x=True)
    b.add_capsule((0.16, 0.0, 1.28), (0.22, -0.18, 1.44), 0.045, MAT_SHADOW, mirror_x=True)
    # Long folded monomolecular cutting scythe blades
    blade_spine = [
        (0.22, -0.18, 1.44),
        (0.24, -0.40, 1.18),
        (0.26, -0.58, 0.82),
        (0.28, -0.74, 0.46),
    ]
    b.add_curved_blade(blade_spine, [0.07, 0.08, 0.06, 0.015], [0.02, 0.015, 0.012, 0.005], MAT_SCYTHE, mirror_x=True)
    b.add_cylinder((0.24, -0.40, 1.18), (0.28, -0.72, 0.48), 0.01, MAT_GLOW, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Alien_Invisible.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 5. ALIEN SMART (Psionic Cerebrate Flanker)
# ==============================================================================
def build_alien_smart():
    b = EnemyMeshBuilder("Alien_Smart")

    MAT_CHITIN = 0
    MAT_BRAIN = 1
    MAT_NERVES = 2
    MAT_BODY = 3

    materials = [
        create_material("smart_chitin", (0.18, 0.16, 0.32), roughness=0.40, metallic=0.60),
        create_material("smart_brain_glow", (0.95, 0.15, 0.70), roughness=0.20, emission_rgb=(0.95, 0.15, 0.70), emission_strength=5.0),
        create_material("smart_nerves", (0.10, 0.85, 1.0), roughness=0.20, emission_rgb=(0.10, 0.85, 1.0), emission_strength=4.5),
        create_material("smart_body", (0.12, 0.12, 0.18), roughness=0.60, metallic=0.20),
    ]

    # Slender hovering bipedal frame
    b.add_box((0.0, 0.0, 0.90), (0.28, 0.20, 0.16), MAT_CHITIN)
    b.add_capsule((0.12, 0.0, 0.88), (0.14, -0.04, 0.52), 0.06, MAT_BODY, mirror_x=True)
    b.add_capsule((0.14, -0.04, 0.52), (0.12, 0.02, 0.16), 0.05, MAT_CHITIN, mirror_x=True)
    b.add_cone((0.12, 0.02, 0.16), (0.12, -0.10, 0.04), 0.04, MAT_CHITIN, mirror_x=True)

    # Torso with glowing neural spine channels
    b.add_capsule((0.0, 0.0, 0.92), (0.0, -0.04, 1.34), 0.16, MAT_BODY)
    b.add_cylinder((0.0, 0.14, 0.94), (0.0, 0.14, 1.36), 0.03, MAT_NERVES)

    # Head: Enormous Pulsing Psionic Brain Sac Dome
    b.add_cylinder((0.0, 0.0, 1.34), (0.0, -0.04, 1.46), 0.09, MAT_BODY)
    # Huge cerebral dome
    b.add_sphere((0.0, 0.06, 1.68), (0.28, 0.36, 0.26), MAT_BRAIN)
    # Translucent protective chitin base ring
    b.add_cylinder((0.0, 0.04, 1.48), (0.0, 0.04, 1.58), 0.24, MAT_CHITIN)

    # Neural sensory tendrils trailing behind head
    for ty in [0.24, 0.34, 0.44]:
        for tx in [-0.14, 0.0, 0.14]:
            b.add_capsule((tx, ty, 1.62), (tx * 1.3, ty + 0.18, 1.32), 0.022, MAT_NERVES)

    # Slender arms with psionic focus talons
    b.add_sphere((0.20, 0.0, 1.30), (0.07, 0.07, 0.07), MAT_BODY, mirror_x=True)
    b.add_capsule((0.20, 0.0, 1.30), (0.28, -0.16, 1.02), 0.05, MAT_BODY, mirror_x=True)
    b.add_sphere((0.28, -0.16, 1.02), (0.05, 0.05, 0.05), MAT_NERVES, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Alien_Smart.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 6. ALIEN SPITTER (Bio-Artillery / Toxic Bile Mortar)
# ==============================================================================
def build_alien_spitter():
    b = EnemyMeshBuilder("Alien_Spitter")

    MAT_CARAPACE = 0
    MAT_BILE = 1
    MAT_FLESH = 2
    MAT_TEETH = 3

    materials = [
        create_material("spitter_carapace", (0.32, 0.38, 0.22), roughness=0.55, metallic=0.40),
        create_material("spitter_bile_glow", (0.65, 0.95, 0.12), roughness=0.20, emission_rgb=(0.65, 0.95, 0.12), emission_strength=5.0),
        create_material("spitter_flesh", (0.22, 0.26, 0.18), roughness=0.75, metallic=0.10),
        create_material("spitter_teeth", (0.75, 0.78, 0.55), roughness=0.40, metallic=0.20),
    ]

    # Heavy hunched pelvis
    b.add_box((0.0, 0.0, 0.82), (0.44, 0.34, 0.24), MAT_CARAPACE)

    # Stout, anchored rear legs for absorbing mortar recoil
    b.add_capsule((0.20, 0.0, 0.80), (0.26, 0.10, 0.42), 0.14, MAT_FLESH, mirror_x=True)
    b.add_capsule((0.26, 0.10, 0.42), (0.22, -0.06, 0.12), 0.12, MAT_CARAPACE, mirror_x=True)
    b.add_box((0.22, -0.08, 0.06), (0.22, 0.36, 0.12), MAT_CARAPACE, mirror_x=True)

    # Heavily arched, hunchbacked torso
    b.add_sphere((0.0, 0.02, 1.18), (0.38, 0.34, 0.32), MAT_FLESH)

    # A COLOSSAL SWOLLEN TOXIC BILE BLADDER ON DORSAL BACK
    b.add_sphere((0.0, 0.18, 1.48), (0.36, 0.44, 0.34), MAT_BILE)
    # External bio-vein conduits anchoring the bladder
    b.add_cylinder((-0.26, 0.18, 1.48), (-0.18, 0.02, 1.20), 0.04, MAT_CARAPACE, mirror_x=True)
    b.add_cylinder((0.0, 0.46, 1.50), (0.0, 0.20, 1.18), 0.045, MAT_CARAPACE)

    # Wide distended jaw / acid spraying mortar muzzle
    b.add_sphere((0.0, -0.28, 1.26), (0.22, 0.24, 0.18), MAT_CARAPACE)
    # Gaping circular acid mortar nozzle
    b.add_cylinder((0.0, -0.36, 1.26), (0.0, -0.56, 1.26), 0.12, MAT_CARAPACE)
    b.add_cylinder((0.0, -0.54, 1.26), (0.0, -0.58, 1.26), 0.10, MAT_BILE)
    # Corroded fang ring around nozzle
    for ang in [0, 60, 120, 180, 240, 300]:
        rad = math.radians(ang)
        tx = math.cos(rad) * 0.11
        tz = 1.26 + math.sin(rad) * 0.11
        b.add_cone((tx, -0.54, tz), (tx * 1.2, -0.66, tz), 0.022, MAT_TEETH)

    # Stout front steering limbs
    b.add_sphere((0.26, -0.10, 1.18), (0.09, 0.09, 0.09), MAT_FLESH, mirror_x=True)
    b.add_capsule((0.26, -0.10, 1.18), (0.28, -0.28, 0.72), 0.08, MAT_CARAPACE, mirror_x=True)
    b.add_box((0.28, -0.30, 0.68), (0.12, 0.16, 0.08), MAT_CARAPACE, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Alien_Spitter.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 7. ALIEN LEADER (Hive Queen / Brood Overlord)
# ==============================================================================
def build_alien_leader():
    b = EnemyMeshBuilder("Alien_Leader")

    MAT_CHITIN = 0
    MAT_FLESH = 1
    MAT_BONE = 2
    MAT_PSIONIC = 3
    MAT_BROOD = 4

    materials = [
        create_material("queen_black_chitin", (0.10, 0.09, 0.12), roughness=0.35, metallic=0.70),
        create_material("queen_purple_flesh", (0.24, 0.16, 0.32), roughness=0.60, metallic=0.20),
        create_material("queen_bone_spikes", (0.82, 0.78, 0.68), roughness=0.40, metallic=0.30),
        create_material("queen_psionic_glow", (0.75, 0.15, 0.95), roughness=0.15, emission_rgb=(0.75, 0.15, 0.95), emission_strength=5.0),
        create_material("queen_brood_glow", (1.0, 0.60, 0.10), roughness=0.20, emission_rgb=(1.0, 0.60, 0.10), emission_strength=4.5),
    ]

    # --- 1. COLOSSAL DIGITIGRADE LEGS & PELVIS ---
    b.add_box((0.0, 0.0, 1.18), (0.64, 0.48, 0.32), MAT_CHITIN)

    # Muscular digitigrade thighs
    b.add_capsule((0.28, 0.0, 1.14), (0.36, 0.20, 0.66), 0.18, MAT_FLESH, mirror_x=True)
    b.add_capsule((0.36, 0.20, 0.66), (0.30, -0.12, 0.22), 0.15, MAT_CHITIN, mirror_x=True)
    # Heavy claw feet
    b.add_box((0.30, -0.10, 0.10), (0.26, 0.46, 0.16), MAT_CHITIN, mirror_x=True)
    for fx in [-0.07, 0.0, 0.07]:
        b.add_cone((0.30 + fx, -0.30, 0.10), (0.30 + fx * 1.3, -0.52, 0.04), 0.045, MAT_BONE, mirror_x=True)

    # --- 2. MASSIVE ARMORED TORSO & BROOD CORE ---
    b.add_capsule((0.0, 0.04, 1.20), (0.0, -0.10, 1.82), 0.36, MAT_FLESH)
    # Armored carapace plates along chest
    b.add_plate((-0.34, -0.36, 1.78), (0.34, -0.36, 1.78), (0.38, -0.18, 1.38), (-0.38, -0.18, 1.38), 0.10, MAT_CHITIN)
    # Pulsing golden-amber brood core in center chest
    b.add_sphere((0.0, -0.38, 1.54), (0.16, 0.12, 0.16), MAT_BROOD)

    # 4 Double-jointed dorsal carapace spikes rising high above shoulders
    spike_configs = [
        (0.24, 0.22, 1.88, 0.42, 0.48, 2.72),
        (-0.24, 0.22, 1.88, -0.42, 0.48, 2.72),
        (0.18, 0.26, 1.68, 0.34, 0.54, 2.38),
        (-0.18, 0.26, 1.68, -0.34, 0.54, 2.38),
    ]
    for x1, y1, z1, x2, y2, z2 in spike_configs:
        b.add_tapered_cylinder((x1, y1, z1), (x2, y2, z2), 0.08, 0.02, MAT_BONE)

    # --- 3. MAJESTIC QUEEN'S CROWN CREST & PSIONIC NODES ---
    b.add_cylinder((0.0, -0.04, 1.84), (0.0, -0.18, 2.12), 0.18, MAT_FLESH)
    # The Crown Shield: Enormous sweeping flared crest
    b.add_plate((-0.58, 0.42, 2.78), (0.58, 0.42, 2.78), (0.24, -0.22, 2.18), (-0.24, -0.22, 2.18), 0.08, MAT_CHITIN)
    # Crown serrated horns
    b.add_cone((-0.56, 0.42, 2.78), (-0.72, 0.56, 3.12), 0.06, MAT_BONE)
    b.add_cone((0.56, 0.42, 2.78), (0.72, 0.56, 3.12), 0.06, MAT_BONE)
    b.add_cone((0.0, 0.44, 2.80), (0.0, 0.58, 3.24), 0.07, MAT_BONE)
    # Amethyst psionic nodes in crown
    b.add_sphere((0.0, 0.20, 2.48), (0.12, 0.10, 0.12), MAT_PSIONIC)
    b.add_sphere((0.26, 0.28, 2.56), (0.07, 0.06, 0.07), MAT_PSIONIC, mirror_x=True)

    # Predatory Queen Jaw
    b.add_box((0.0, -0.38, 2.06), (0.22, 0.32, 0.18), MAT_CHITIN)
    for qx in [-0.07, 0.0, 0.07]:
        b.add_cone((qx, -0.52, 2.04), (qx * 1.2, -0.66, 1.98), 0.035, MAT_BONE)

    # --- 4. DUAL PRIMARY CRUSHING CLAWS & SECONDARY CHEST MANDIBLES ---
    # Primary massive arms
    b.add_sphere((0.44, 0.0, 1.76), (0.18, 0.18, 0.18), MAT_CHITIN, mirror_x=True)
    b.add_capsule((0.44, 0.0, 1.76), (0.58, -0.24, 1.30), 0.15, MAT_FLESH, mirror_x=True)
    b.add_capsule((0.58, -0.24, 1.30), (0.64, -0.46, 0.84), 0.14, MAT_CHITIN, mirror_x=True)
    # Massive crushing claw pincer
    b.add_cone((0.64, -0.48, 0.84), (0.62, -0.84, 0.72), 0.06, MAT_BONE, mirror_x=True)
    b.add_cone((0.64, -0.48, 0.84), (0.76, -0.78, 0.86), 0.05, MAT_BONE, mirror_x=True)

    # Secondary smaller chest grasping mandibles
    b.add_capsule((0.18, -0.34, 1.44), (0.16, -0.54, 1.26), 0.045, MAT_CHITIN, mirror_x=True)
    b.add_cone((0.16, -0.54, 1.26), (0.14, -0.68, 1.18), 0.03, MAT_BONE, mirror_x=True)

    # --- 5. HEAVY MUSCULAR COUNTERBALANCING TAIL ---
    tail_pts = [
        (0.0, 0.22, 1.14),
        (0.0, 0.62, 0.98),
        (0.0, 1.04, 0.78),
        (0.0, 1.48, 0.58),
        (0.0, 1.88, 0.44),
    ]
    for i in range(len(tail_pts) - 1):
        b.add_cylinder(tail_pts[i], tail_pts[i + 1], 0.12 - i * 0.018, MAT_CHITIN)
    # Multi-bladed tail fin
    b.add_plate((-0.22, 1.72, 0.48), (0.22, 1.72, 0.48), (0.0, 2.14, 0.40), (0.0, 2.14, 0.40), 0.06, MAT_BONE)

    out_path = os.path.join(OUT_DIR, "Alien_Leader.obj")
    b.build_and_export(out_path, materials)


if __name__ == "__main__":
    print("Building All 7 Alien Models...")
    build_alien_normal()
    build_alien_crawler()
    build_alien_hook()
    build_alien_invisible()
    build_alien_smart()
    build_alien_spitter()
    build_alien_leader()
    print("Completed All 7 Alien Models generation.")

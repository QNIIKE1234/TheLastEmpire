"""
Generates All 8 Robot Enemy 3D Models:
1. Robot_Normal: Patrol / security android with chest plate & pulse blaster
2. Robot_Blade: Mantis / Shinobi speed assassin mech with dual forearm vibro-blades
3. Robot_Drone: Quad-rotor hover recon / attack drone with underslung cannons
4. Robot_Gunner: Heavy suppression rotary Gatling cannon automaton with ammo drums
5. Robot_Kamikaze: Spider-mine demolition bomb bot with glowing unstable core
6. Robot_Laser: High-energy directed beam optical walker with capacitor banks
7. Robot_Tanker: Frontline heavy riot mech with massive ballistic blast shield
8. Robot_Leader: Titan dreadnought commander mech with dual fusion cores & missile pods
"""

import math
import os
import sys

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.append(SCRIPT_DIR)

from ProceduralModelKit import EnemyMeshBuilder, create_material

ROOT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
OUT_DIR = os.path.join(ROOT_DIR, "Assets", "GeneratedModels", "Robots")
os.makedirs(OUT_DIR, exist_ok=True)


# ==============================================================================
# 1. ROBOT NORMAL (Security Android)
# ==============================================================================
def build_robot_normal():
    b = EnemyMeshBuilder("Robot_Normal")

    MAT_STEEL = 0
    MAT_ARMOR = 1
    MAT_JOINT = 2
    MAT_GLOW = 3

    materials = [
        create_material("mech_steel", (0.28, 0.30, 0.32), roughness=0.45, metallic=0.80),
        create_material("mech_armor_gray", (0.62, 0.64, 0.66), roughness=0.40, metallic=0.60),
        create_material("mech_joint_black", (0.12, 0.12, 0.14), roughness=0.75, metallic=0.20),
        create_material("mech_amber_glow", (1.0, 0.65, 0.10), roughness=0.25, emission_rgb=(1.0, 0.65, 0.10), emission_strength=4.0),
    ]

    # Pelvis
    b.add_box((0.0, 0.0, 0.90), (0.36, 0.24, 0.18), MAT_STEEL)

    # Legs & Feet
    b.add_cylinder((0.14, 0.0, 0.88), (0.16, -0.04, 0.50), 0.08, MAT_JOINT, mirror_x=True)
    b.add_box((0.16, -0.06, 0.50), (0.12, 0.14, 0.12), MAT_ARMOR, mirror_x=True)
    b.add_cylinder((0.16, -0.04, 0.50), (0.15, 0.02, 0.12), 0.07, MAT_JOINT, mirror_x=True)
    b.add_plate((0.10, -0.10, 0.48), (0.22, -0.10, 0.48), (0.20, -0.06, 0.16), (0.10, -0.06, 0.16), 0.04, MAT_ARMOR, mirror_x=True)
    b.add_box((0.15, -0.04, 0.06), (0.14, 0.28, 0.12), MAT_STEEL, mirror_x=True)

    # Torso
    b.add_box((0.0, 0.0, 1.15), (0.38, 0.26, 0.32), MAT_JOINT)
    b.add_box((0.0, -0.06, 1.28), (0.42, 0.24, 0.32), MAT_ARMOR)
    b.add_cylinder((0.0, -0.16, 1.28), (0.0, -0.18, 1.28), 0.08, MAT_GLOW)
    b.add_box((0.0, 0.14, 1.28), (0.32, 0.06, 0.24), MAT_STEEL)

    # Head & Sensor Visor
    b.add_cylinder((0.0, 0.0, 1.44), (0.0, 0.0, 1.52), 0.09, MAT_JOINT)
    b.add_box((0.0, -0.02, 1.62), (0.24, 0.24, 0.18), MAT_STEEL)
    b.add_box((0.0, -0.13, 1.62), (0.22, 0.04, 0.05), MAT_GLOW)
    b.add_cylinder((0.12, 0.06, 1.65), (0.12, 0.06, 1.88), 0.015, MAT_STEEL)

    # Arms & Wrist Blasters
    b.add_sphere((0.26, 0.0, 1.34), (0.09, 0.09, 0.09), MAT_JOINT, mirror_x=True)
    b.add_cylinder((0.26, 0.0, 1.34), (0.30, -0.06, 1.02), 0.07, MAT_ARMOR, mirror_x=True)
    b.add_sphere((0.30, -0.06, 1.02), (0.07, 0.07, 0.07), MAT_JOINT, mirror_x=True)
    b.add_box((0.32, -0.14, 0.82), (0.12, 0.24, 0.14), MAT_STEEL, mirror_x=True)
    b.add_cylinder((0.32, -0.26, 0.82), (0.32, -0.40, 0.82), 0.025, MAT_GLOW, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Robot_Normal.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 2. ROBOT BLADE (Mantis / Shinobi Assassin Mech)
# ==============================================================================
def build_robot_blade():
    b = EnemyMeshBuilder("Robot_Blade")

    MAT_CARBON = 0
    MAT_EDGE = 1
    MAT_GLOW = 2
    MAT_CHASSIS = 3

    materials = [
        create_material("blade_carbon", (0.10, 0.10, 0.12), roughness=0.35, metallic=0.70),
        create_material("blade_edge_silver", (0.86, 0.88, 0.90), roughness=0.20, metallic=0.95),
        create_material("blade_ruby_glow", (1.0, 0.10, 0.15), roughness=0.20, emission_rgb=(1.0, 0.10, 0.15), emission_strength=4.5),
        create_material("blade_chassis", (0.22, 0.24, 0.26), roughness=0.50, metallic=0.80),
    ]

    # Sleek pelvis
    b.add_box((0.0, 0.0, 0.88), (0.28, 0.20, 0.16), MAT_CARBON)

    # Inverted-joint digitigrade agile legs
    b.add_capsule((0.12, 0.0, 0.86), (0.18, 0.12, 0.54), 0.06, MAT_CARBON, mirror_x=True)
    b.add_capsule((0.18, 0.12, 0.54), (0.14, -0.12, 0.22), 0.05, MAT_CARBON, mirror_x=True)
    # Runner claw feet
    b.add_cone((0.14, -0.12, 0.22), (0.14, -0.32, 0.04), 0.05, MAT_EDGE, mirror_x=True)

    # Aerodynamic torso
    b.add_box((0.0, -0.02, 1.12), (0.30, 0.22, 0.26), MAT_CARBON)
    b.add_wedge((0.0, -0.10, 1.24), (0.28, 0.22, 0.24), MAT_CHASSIS)
    # Burst thrusters on back
    b.add_cylinder((0.08, 0.14, 1.20), (0.08, 0.22, 1.20), 0.04, MAT_GLOW, mirror_x=True)

    # Streamlined wedge head
    b.add_cylinder((0.0, 0.0, 1.34), (0.0, 0.0, 1.42), 0.07, MAT_CARBON)
    b.add_wedge((0.0, -0.06, 1.50), (0.18, 0.26, 0.14), MAT_CARBON)
    b.add_box((0.0, -0.18, 1.50), (0.16, 0.02, 0.03), MAT_GLOW)

    # Mantis arms with extended high-frequency vibro-blades
    b.add_sphere((0.20, 0.02, 1.28), (0.07, 0.07, 0.07), MAT_CHASSIS, mirror_x=True)
    b.add_capsule((0.20, 0.02, 1.28), (0.26, -0.14, 1.02), 0.05, MAT_CARBON, mirror_x=True)
    b.add_sphere((0.26, -0.14, 1.02), (0.06, 0.06, 0.06), MAT_CHASSIS, mirror_x=True)
    # Forearm blade mount
    b.add_box((0.26, -0.22, 0.88), (0.08, 0.22, 0.08), MAT_CARBON, mirror_x=True)
    # Dual elongated curved mantis vibro-swords
    blade_spine = [
        (0.26, -0.10, 0.88),
        (0.28, -0.34, 0.84),
        (0.30, -0.62, 0.72),
        (0.32, -0.86, 0.52),
    ]
    b.add_curved_blade(blade_spine, [0.08, 0.09, 0.07, 0.02], [0.025, 0.02, 0.015, 0.005], MAT_EDGE, mirror_x=True)
    # Glowing edge channel
    b.add_cylinder((0.28, -0.34, 0.84), (0.32, -0.82, 0.54), 0.012, MAT_GLOW, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Robot_Blade.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 3. ROBOT DRONE (Quad-Rotor Hover Recon/Attack Drone)
# ==============================================================================
def build_robot_drone():
    b = EnemyMeshBuilder("Robot_Drone")

    MAT_WHITE = 0
    MAT_TURBINE = 1
    MAT_CYAN = 2
    MAT_DARK = 3

    materials = [
        create_material("drone_composite", (0.86, 0.88, 0.90), roughness=0.35, metallic=0.40),
        create_material("drone_turbines", (0.24, 0.26, 0.28), roughness=0.45, metallic=0.85),
        create_material("drone_sensor_cyan", (0.10, 0.85, 1.0), roughness=0.20, emission_rgb=(0.10, 0.85, 1.0), emission_strength=4.5),
        create_material("drone_trim_dark", (0.12, 0.12, 0.14), roughness=0.60, metallic=0.30),
    ]

    # Central spherical body
    b.add_sphere((0.0, 0.0, 0.90), (0.34, 0.34, 0.24), MAT_WHITE)
    # Upper dome
    b.add_cylinder((0.0, 0.0, 1.02), (0.0, 0.0, 1.12), 0.20, MAT_DARK)
    b.add_cylinder((0.0, 0.0, 1.12), (0.0, 0.0, 1.36), 0.015, MAT_TURBINE)

    # Front scanning optical eye
    b.add_sphere((0.0, -0.28, 0.90), (0.14, 0.10, 0.10), MAT_CYAN)

    # 4 Diagonal Outrigger Pylons & Ducted Rotor Turbines
    pylons = [
        (0.42, 0.38, 0.94),
        (-0.42, 0.38, 0.94),
        (0.44, -0.34, 0.94),
        (-0.44, -0.34, 0.94),
    ]
    for px, py, pz in pylons:
        # Carbon strut
        b.add_cylinder((0.0, 0.0, 0.92), (px, py, pz), 0.04, MAT_DARK)
        # Outer ducted turbine ring
        b.add_cylinder((px, py, pz - 0.08), (px, py, pz + 0.08), 0.20, MAT_WHITE)
        # Inner rotor hub
        b.add_cylinder((px, py, pz - 0.04), (px, py, pz + 0.04), 0.06, MAT_TURBINE)
        # Glowing turbine rim
        b.add_cylinder((px, py, pz + 0.07), (px, py, pz + 0.09), 0.18, MAT_CYAN)

    # Underslung Weapons Pod
    b.add_box((0.0, -0.06, 0.70), (0.18, 0.28, 0.14), MAT_DARK)
    # Dual machinegun / pulse barrels
    b.add_cylinder((0.06, -0.16, 0.68), (0.06, -0.46, 0.68), 0.025, MAT_TURBINE)
    b.add_cylinder((-0.06, -0.16, 0.68), (-0.06, -0.46, 0.68), 0.025, MAT_TURBINE)
    b.add_cylinder((0.06, -0.44, 0.68), (0.06, -0.48, 0.68), 0.035, MAT_CYAN)
    b.add_cylinder((-0.06, -0.44, 0.68), (-0.06, -0.48, 0.68), 0.035, MAT_CYAN)

    # Landing skids
    b.add_cylinder((0.20, 0.18, 0.68), (0.20, -0.22, 0.68), 0.02, MAT_DARK, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Robot_Drone.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 4. ROBOT GUNNER (Heavy Artillery Gatling Automaton)
# ==============================================================================
def build_robot_gunner():
    b = EnemyMeshBuilder("Robot_Gunner")

    MAT_OLIVE = 0
    MAT_STEEL = 1
    MAT_BRASS = 2
    MAT_RED = 3

    materials = [
        create_material("gunner_olive", (0.28, 0.32, 0.24), roughness=0.60, metallic=0.70),
        create_material("gunner_steel", (0.22, 0.23, 0.25), roughness=0.40, metallic=0.88),
        create_material("gunner_brass", (0.85, 0.68, 0.28), roughness=0.30, metallic=0.92),
        create_material("gunner_red_glow", (1.0, 0.12, 0.12), roughness=0.20, emission_rgb=(1.0, 0.12, 0.12), emission_strength=4.5),
    ]

    # Heavy chassis pelvis
    b.add_box((0.0, 0.0, 0.88), (0.46, 0.34, 0.22), MAT_STEEL)

    # Wide heavy bipedal legs with recoil stabilizers
    b.add_cylinder((0.20, 0.0, 0.86), (0.24, -0.05, 0.48), 0.12, MAT_STEEL, mirror_x=True)
    b.add_box((0.24, -0.08, 0.48), (0.18, 0.20, 0.16), MAT_OLIVE, mirror_x=True)
    b.add_cylinder((0.24, -0.05, 0.48), (0.22, 0.04, 0.14), 0.11, MAT_STEEL, mirror_x=True)
    b.add_box((0.22, -0.04, 0.07), (0.22, 0.36, 0.14), MAT_OLIVE, mirror_x=True)
    # Hydraulic heel recoil brace
    b.add_cylinder((0.22, 0.12, 0.24), (0.22, 0.28, 0.04), 0.04, MAT_STEEL, mirror_x=True)

    # Broad torso
    b.add_box((0.0, -0.02, 1.20), (0.52, 0.38, 0.38), MAT_OLIVE)
    b.add_box((0.0, -0.16, 1.22), (0.44, 0.14, 0.28), MAT_STEEL)

    # Armored head & cyclopean targeter
    b.add_box((0.0, -0.04, 1.50), (0.30, 0.28, 0.20), MAT_OLIVE)
    b.add_cylinder((0.0, -0.18, 1.50), (0.0, -0.22, 1.50), 0.07, MAT_RED)
    b.add_cylinder((0.10, -0.16, 1.54), (0.10, -0.20, 1.54), 0.025, MAT_RED, mirror_x=True)

    # Backpack: Dual large ammo drum canisters
    b.add_cylinder((0.18, 0.24, 1.24), (0.18, 0.24, 1.54), 0.14, MAT_STEEL, mirror_x=True)
    # Brass ammo chutes routing forward
    b.add_capsule((0.18, 0.24, 1.20), (0.38, 0.08, 1.00), 0.06, MAT_BRASS, mirror_x=True)

    # Dual 4-barrel heavy rotary Gatling cannons on forearms
    for side in [1.0, -1.0]:
        sx = side * 0.36
        b.add_sphere((sx, 0.0, 1.30), (0.12, 0.12, 0.12), MAT_STEEL)
        b.add_cylinder((sx, 0.0, 1.30), (side * 0.42, -0.12, 0.98), 0.09, MAT_OLIVE)
        # Gatling housing
        gh_x = side * 0.42
        b.add_cylinder((gh_x, -0.12, 0.98), (gh_x, -0.42, 0.98), 0.12, MAT_STEEL)
        # 4 rotating barrels
        for ang_deg in [0, 90, 180, 270]:
            rad = math.radians(ang_deg)
            bx = gh_x + math.cos(rad) * 0.06
            bz = 0.98 + math.sin(rad) * 0.06
            b.add_cylinder((bx, -0.40, bz), (bx, -0.88, bz), 0.022, MAT_STEEL)
        # Muzzle clamp ring
        b.add_cylinder((gh_x, -0.84, 0.98), (gh_x, -0.88, 0.98), 0.09, MAT_BRASS)

    out_path = os.path.join(OUT_DIR, "Robot_Gunner.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 5. ROBOT KAMIKAZE (Spider-Mine Demolition Bomb Bot)
# ==============================================================================
def build_robot_kamikaze():
    b = EnemyMeshBuilder("Robot_Kamikaze")

    MAT_YELLOW = 0
    MAT_HAZARD = 1
    MAT_GLOW = 2
    MAT_IRON = 3

    materials = [
        create_material("hazard_yellow", (0.92, 0.78, 0.12), roughness=0.45, metallic=0.50),
        create_material("hazard_black", (0.08, 0.08, 0.09), roughness=0.70, metallic=0.20),
        create_material("kamikaze_core_glow", (1.0, 0.22, 0.05), roughness=0.20, emission_rgb=(1.0, 0.22, 0.05), emission_strength=5.0),
        create_material("spider_iron", (0.20, 0.22, 0.22), roughness=0.50, metallic=0.85),
    ]

    # Central Volatile Bomb Core (Glowing fiery sphere)
    b.add_sphere((0.0, 0.0, 0.46), (0.28, 0.28, 0.28), MAT_GLOW)

    # Protective roll cage & hazard bands
    b.add_cylinder((0.0, 0.0, 0.18), (0.0, 0.0, 0.74), 0.31, MAT_YELLOW)
    b.add_cylinder((0.0, 0.0, 0.40), (0.0, 0.0, 0.52), 0.33, MAT_HAZARD)

    # Top warning strobe beacon & antenna
    b.add_cylinder((0.0, 0.0, 0.72), (0.0, 0.0, 0.84), 0.08, MAT_HAZARD)
    b.add_sphere((0.0, 0.0, 0.86), (0.06, 0.06, 0.06), MAT_GLOW)
    b.add_cylinder((0.06, 0.06, 0.80), (0.06, 0.06, 1.12), 0.012, MAT_IRON)

    # 4 Articulating Spider Legs
    legs_angles = [45, 135, 225, 315]
    for ang in legs_angles:
        rad = math.radians(ang)
        cos_a = math.cos(rad)
        sin_a = math.sin(rad)
        # Shoulder joint
        j_x = cos_a * 0.24
        j_y = sin_a * 0.24
        b.add_sphere((j_x, j_y, 0.46), (0.07, 0.07, 0.07), MAT_IRON)
        # Upper knee joint (arching upward)
        k_x = cos_a * 0.48
        k_y = sin_a * 0.48
        k_z = 0.62
        b.add_cylinder((j_x, j_y, 0.46), (k_x, k_y, k_z), 0.04, MAT_YELLOW)
        # Lower leg spear (spiking into ground)
        foot_x = cos_a * 0.68
        foot_y = sin_a * 0.68
        b.add_cone((k_x, k_y, k_z), (foot_x, foot_y, 0.02), 0.045, MAT_IRON)

    out_path = os.path.join(OUT_DIR, "Robot_Kamikaze.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 6. ROBOT LASER (High-Energy Optical Beam Walker)
# ==============================================================================
def build_robot_laser():
    b = EnemyMeshBuilder("Robot_Laser")

    MAT_WHITE = 0
    MAT_COBALT = 1
    MAT_COPPER = 2
    MAT_CYAN = 3

    materials = [
        create_material("laser_white", (0.88, 0.90, 0.92), roughness=0.30, metallic=0.30),
        create_material("laser_cobalt", (0.15, 0.28, 0.48), roughness=0.45, metallic=0.80),
        create_material("laser_heatsink", (0.78, 0.45, 0.25), roughness=0.35, metallic=0.90),
        create_material("laser_cyan_glow", (0.10, 0.90, 1.0), roughness=0.15, emission_rgb=(0.10, 0.90, 1.0), emission_strength=5.0),
    ]

    # Pelvis
    b.add_box((0.0, 0.0, 0.96), (0.36, 0.24, 0.18), MAT_COBALT)

    # Tall digitigrade bipedal walker legs
    b.add_capsule((0.15, 0.0, 0.94), (0.20, 0.10, 0.60), 0.08, MAT_COBALT, mirror_x=True)
    b.add_capsule((0.20, 0.10, 0.60), (0.16, -0.10, 0.20), 0.07, MAT_WHITE, mirror_x=True)
    b.add_box((0.16, -0.08, 0.08), (0.16, 0.32, 0.12), MAT_COBALT, mirror_x=True)

    # Torso
    b.add_box((0.0, 0.0, 1.25), (0.40, 0.28, 0.38), MAT_WHITE)

    # Back Capacitor Banks & Heat Sink Fins
    for cz in [1.12, 1.28, 1.44]:
        b.add_cylinder((-0.12, 0.18, cz), (0.12, 0.18, cz), 0.07, MAT_COBALT)
        b.add_cylinder((-0.08, 0.18, cz), (0.08, 0.18, cz), 0.08, MAT_CYAN)
    # Heat sink radiator fins
    for fz in [1.16, 1.26, 1.36, 1.46]:
        b.add_box((0.0, 0.26, fz), (0.34, 0.08, 0.02), MAT_COPPER)

    # Head: Heavy Cylindrical Laser Collimator Cannon
    b.add_cylinder((0.0, -0.06, 1.54), (0.0, -0.46, 1.54), 0.16, MAT_COBALT)
    # Concentric focusing rings
    b.add_cylinder((0.0, -0.44, 1.54), (0.0, -0.48, 1.54), 0.18, MAT_WHITE)
    b.add_cylinder((0.0, -0.48, 1.54), (0.0, -0.52, 1.54), 0.14, MAT_CYAN)
    b.add_cylinder((0.0, -0.52, 1.54), (0.0, -0.54, 1.54), 0.10, MAT_CYAN)

    # Stabilizer Arms with power conduits
    b.add_sphere((0.26, 0.0, 1.34), (0.08, 0.08, 0.08), MAT_COBALT, mirror_x=True)
    b.add_cylinder((0.26, 0.0, 1.34), (0.24, -0.22, 1.34), 0.05, MAT_WHITE, mirror_x=True)
    # High-tension conduit feeding into cannon
    b.add_capsule((0.24, -0.22, 1.34), (0.14, -0.32, 1.50), 0.03, MAT_CYAN, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Robot_Laser.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 7. ROBOT TANKER (Frontline Heavy Riot Mech with Ballistic Shield)
# ==============================================================================
def build_robot_tanker():
    b = EnemyMeshBuilder("Robot_Tanker")

    MAT_TUNGSTEN = 0
    MAT_SHIELD = 1
    MAT_CHROME = 2
    MAT_GLOW = 3
    MAT_HAZARD = 4

    materials = [
        create_material("tanker_tungsten", (0.24, 0.25, 0.26), roughness=0.50, metallic=0.82),
        create_material("tanker_shield_iron", (0.38, 0.40, 0.42), roughness=0.45, metallic=0.75),
        create_material("tanker_hydraulics", (0.80, 0.82, 0.85), roughness=0.20, metallic=0.92),
        create_material("tanker_amber_glow", (1.0, 0.65, 0.10), roughness=0.25, emission_rgb=(1.0, 0.65, 0.10), emission_strength=4.0),
        create_material("tanker_hazard", (0.90, 0.75, 0.12), roughness=0.50, metallic=0.40),
    ]

    # Broad, heavy pelvis & blast skirts
    b.add_box((0.0, 0.0, 0.86), (0.58, 0.42, 0.24), MAT_TUNGSTEN)
    b.add_plate((-0.28, -0.22, 0.88), (0.28, -0.22, 0.88), (0.30, -0.24, 0.68), (-0.30, -0.24, 0.68), 0.05, MAT_HAZARD)

    # Heavy hydraulic stomper legs
    b.add_cylinder((0.24, 0.0, 0.84), (0.26, -0.06, 0.44), 0.15, MAT_TUNGSTEN, mirror_x=True)
    b.add_cylinder((0.36, -0.04, 0.80), (0.36, -0.04, 0.48), 0.04, MAT_CHROME, mirror_x=True)
    b.add_cylinder((0.26, -0.06, 0.44), (0.24, 0.04, 0.14), 0.14, MAT_TUNGSTEN, mirror_x=True)
    b.add_box((0.24, -0.06, 0.08), (0.26, 0.42, 0.14), MAT_TUNGSTEN, mirror_x=True)

    # Colossal armored torso
    b.add_box((0.0, -0.02, 1.24), (0.64, 0.46, 0.48), MAT_TUNGSTEN)
    # Heavy sloped chest armor
    b.add_wedge((0.0, -0.22, 1.30), (0.58, 0.18, 0.40), MAT_SHIELD)

    # Armored head recessed into collar
    b.add_box((0.0, -0.08, 1.56), (0.30, 0.28, 0.18), MAT_TUNGSTEN)
    b.add_box((0.0, -0.20, 1.56), (0.22, 0.04, 0.04), MAT_GLOW)

    # Left Arm: Colossal Curved Ballistic Blast Shield
    b.add_sphere((-0.42, 0.02, 1.36), (0.14, 0.14, 0.14), MAT_TUNGSTEN)
    b.add_cylinder((-0.42, 0.02, 1.36), (-0.48, -0.18, 1.02), 0.11, MAT_TUNGSTEN)
    # The Shield Plate (massive curved slab)
    b.add_plate((-0.24, -0.48, 1.62), (-0.68, -0.42, 1.62), (-0.68, -0.42, 0.22), (-0.24, -0.48, 0.22), 0.08, MAT_SHIELD)
    # Shield Viewport Slit
    b.add_box((-0.46, -0.50, 1.42), (0.24, 0.06, 0.06), MAT_GLOW)
    # Shield hazard stripes
    b.add_plate((-0.24, -0.52, 0.50), (-0.68, -0.46, 0.50), (-0.68, -0.46, 0.30), (-0.24, -0.52, 0.30), 0.04, MAT_HAZARD)

    # Right Arm: Hydraulic Concussion Siege Cannon
    b.add_sphere((0.42, 0.02, 1.36), (0.14, 0.14, 0.14), MAT_TUNGSTEN)
    b.add_cylinder((0.42, 0.02, 1.36), (0.46, -0.16, 1.02), 0.11, MAT_TUNGSTEN)
    # Heavy barrel housing
    b.add_box((0.48, -0.26, 0.98), (0.22, 0.36, 0.22), MAT_SHIELD)
    b.add_cylinder((0.48, -0.40, 0.98), (0.48, -0.72, 0.98), 0.09, MAT_TUNGSTEN)
    b.add_cylinder((0.48, -0.70, 0.98), (0.48, -0.76, 0.98), 0.11, MAT_CHROME)

    out_path = os.path.join(OUT_DIR, "Robot_Tanker.obj")
    b.build_and_export(out_path, materials)


# ==============================================================================
# 8. ROBOT LEADER (Titan Dreadnought Commander Mech)
# ==============================================================================
def build_robot_leader():
    b = EnemyMeshBuilder("Robot_Leader")

    MAT_BLACK = 0
    MAT_GOLD = 1
    MAT_STEEL = 2
    MAT_FUSION = 3
    MAT_RED = 4

    materials = [
        create_material("leader_armor_black", (0.12, 0.13, 0.14), roughness=0.45, metallic=0.75),
        create_material("leader_gold_trim", (0.88, 0.72, 0.24), roughness=0.30, metallic=0.90),
        create_material("leader_gunmetal", (0.28, 0.30, 0.32), roughness=0.40, metallic=0.85),
        create_material("leader_fusion_glow", (1.0, 0.50, 0.08), roughness=0.20, emission_rgb=(1.0, 0.50, 0.08), emission_strength=5.0),
        create_material("leader_sensor_red", (1.0, 0.12, 0.12), roughness=0.20, emission_rgb=(1.0, 0.12, 0.12), emission_strength=4.5),
    ]

    # --- 1. LOWER BODY & COLOSSAL HYDRAULIC LEGS ---
    b.add_box((0.0, 0.0, 1.12), (0.72, 0.52, 0.34), MAT_BLACK)
    b.add_box((0.0, -0.16, 1.12), (0.44, 0.24, 0.24), MAT_GOLD)

    # Heavy thigh assemblies
    b.add_cylinder((0.32, 0.0, 1.10), (0.36, -0.08, 0.58), 0.18, MAT_STEEL, mirror_x=True)
    # Heavy knee armor with gold trim
    b.add_box((0.36, -0.18, 0.58), (0.24, 0.18, 0.22), MAT_BLACK, mirror_x=True)
    b.add_box((0.36, -0.26, 0.58), (0.16, 0.06, 0.14), MAT_GOLD, mirror_x=True)
    # Dual hydraulic shock pistons
    b.add_cylinder((0.48, -0.06, 0.98), (0.48, -0.06, 0.60), 0.045, MAT_STEEL, mirror_x=True)
    # Heavy lower legs
    b.add_cylinder((0.36, -0.08, 0.58), (0.34, 0.04, 0.18), 0.17, MAT_BLACK, mirror_x=True)
    # Colossal magnetic feet
    b.add_box((0.34, -0.08, 0.09), (0.34, 0.54, 0.18), MAT_BLACK, mirror_x=True)
    b.add_box((0.34, -0.32, 0.09), (0.28, 0.12, 0.14), MAT_GOLD, mirror_x=True)

    # --- 2. COLOSSAL COMMAND DREADNOUGHT CHASSIS ---
    b.add_box((0.0, -0.04, 1.62), (0.84, 0.62, 0.64), MAT_BLACK)
    # Sloped heavy chest armor
    b.add_wedge((0.0, -0.28, 1.70), (0.76, 0.24, 0.52), MAT_BLACK)

    # Dual glowing fusion reactor cores in chest
    b.add_cylinder((-0.20, -0.38, 1.68), (-0.20, -0.42, 1.68), 0.11, MAT_FUSION)
    b.add_cylinder((0.20, -0.38, 1.68), (0.20, -0.42, 1.68), 0.11, MAT_FUSION)
    b.add_box((0.0, -0.38, 1.68), (0.10, 0.06, 0.20), MAT_GOLD)

    # Twin heavy industrial plasma exhaust stacks on back
    b.add_cylinder((0.26, 0.32, 1.72), (0.28, 0.44, 2.38), 0.10, MAT_STEEL, mirror_x=True)
    b.add_cylinder((0.28, 0.44, 2.36), (0.28, 0.44, 2.44), 0.12, MAT_FUSION, mirror_x=True)

    # Command head recessed in armored cowl
    b.add_box((0.0, -0.12, 2.06), (0.36, 0.34, 0.24), MAT_BLACK)
    # Imperial command antennae crown
    b.add_plate((-0.18, -0.08, 2.18), (0.18, -0.08, 2.18), (0.24, -0.04, 2.48), (-0.24, -0.04, 2.48), 0.04, MAT_GOLD)
    # Red command scanner array
    b.add_box((0.0, -0.28, 2.06), (0.28, 0.04, 0.06), MAT_RED)

    # --- 3. MASSIVE SHOULDER PAULDRONS & MISSILE BATTERIES ---
    for side in [1.0, -1.0]:
        sx = side * 0.58
        b.add_box((sx, -0.02, 1.94), (0.34, 0.44, 0.32), MAT_BLACK)
        b.add_plate((sx - side * 0.16, -0.24, 2.10), (sx + side * 0.16, -0.24, 2.10),
                    (sx + side * 0.16, 0.20, 2.10), (sx - side * 0.16, 0.20, 2.10), 0.04, MAT_GOLD)
        # 4-tube micro missile launcher array
        for my in [-0.08, 0.08]:
            for mz in [1.90, 2.02]:
                b.add_cylinder((sx, my, mz), (sx, my - 0.22, mz), 0.035, MAT_FUSION)

    # --- 4. LEFT ARM: HEAVY MULTI-BARREL PLASMA CANNON ---
    b.add_sphere((-0.58, -0.04, 1.78), (0.18, 0.18, 0.18), MAT_STEEL)
    b.add_cylinder((-0.58, -0.04, 1.78), (-0.66, -0.24, 1.34), 0.15, MAT_BLACK)
    # Cannon housing
    b.add_box((-0.68, -0.38, 1.28), (0.30, 0.48, 0.30), MAT_BLACK)
    b.add_cylinder((-0.68, -0.42, 1.28), (-0.68, -0.92, 1.28), 0.12, MAT_STEEL)
    b.add_cylinder((-0.68, -0.90, 1.28), (-0.68, -0.98, 1.28), 0.14, MAT_FUSION)

    # --- 5. RIGHT ARM: HYDRAULIC POWER SIEGE CLAW ---
    b.add_sphere((0.58, -0.04, 1.78), (0.18, 0.18, 0.18), MAT_STEEL)
    b.add_cylinder((0.58, -0.04, 1.78), (0.66, -0.24, 1.34), 0.15, MAT_BLACK)
    # Power wrist assembly
    b.add_box((0.68, -0.36, 1.28), (0.28, 0.32, 0.28), MAT_GOLD)
    # 3 articulating crushing claws
    for cang in [0, 120, 240]:
        c_rad = math.radians(cang)
        cx = 0.68 + math.cos(c_rad) * 0.10
        cz = 1.28 + math.sin(c_rad) * 0.10
        b.add_cone((cx, -0.48, cz), (cx + math.cos(c_rad) * 0.08, -0.88, cz + math.sin(c_rad) * 0.08), 0.05, MAT_STEEL)

    out_path = os.path.join(OUT_DIR, "Robot_Leader.obj")
    b.build_and_export(out_path, materials)


if __name__ == "__main__":
    print("Building All 8 Robot Models...")
    build_robot_normal()
    build_robot_blade()
    build_robot_drone()
    build_robot_gunner()
    build_robot_kamikaze()
    build_robot_laser()
    build_robot_tanker()
    build_robot_leader()
    print("Completed All 8 Robot Models generation.")


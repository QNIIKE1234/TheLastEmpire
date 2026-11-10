"""
Generates Boss Zombie 3D Models:
1. Boss_Zombie_Leader: Tyrant / Mutant Commander with bone crest, mutated claw arm, necrotic core.
2. Boss_Zombie_Tank: Colossal bloated muscle juggernaut with calcified fists and toxic pustules.
"""

import math
import os
import sys

# Add Tools directory to sys.path
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.append(SCRIPT_DIR)

from ProceduralModelKit import EnemyMeshBuilder, create_material

ROOT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, ".."))
OUT_DIR = os.path.join(ROOT_DIR, "Assets", "GeneratedModels", "Zombies")
os.makedirs(OUT_DIR, exist_ok=True)


def build_boss_zombie_leader():
    b = EnemyMeshBuilder("Boss_Zombie_Leader")

    # Materials
    MAT_FLESH = 0
    MAT_FLESH_DARK = 1
    MAT_BONE = 2
    MAT_CORE = 3
    MAT_RAG = 4
    MAT_ARMOR = 5

    materials = [
        create_material("zombie_flesh", (0.34, 0.42, 0.30), roughness=0.75),
        create_material("zombie_flesh_dark", (0.16, 0.20, 0.15), roughness=0.85),
        create_material("zombie_bone", (0.86, 0.82, 0.68), roughness=0.55),
        create_material("zombie_necro_core", (0.95, 0.15, 0.08), roughness=0.3, emission_rgb=(1.0, 0.18, 0.10), emission_strength=4.0),
        create_material("zombie_tactical_rag", (0.18, 0.19, 0.20), roughness=0.88),
        create_material("zombie_metal_armor", (0.30, 0.32, 0.33), roughness=0.45, metallic=0.75),
    ]

    # --- 1. PELVIS & LOWER BODY ---
    b.add_box((0.0, 0.0, 0.95), (0.48, 0.36, 0.28), MAT_RAG)

    # --- 2. LEGS & FEET ---
    # Thighs
    b.add_capsule((0.20, 0.0, 0.92), (0.24, -0.05, 0.52), 0.16, MAT_FLESH, mirror_x=True)
    # Knees (calcified bone spur)
    b.add_cone((0.24, -0.10, 0.50), (0.26, -0.22, 0.50), 0.09, MAT_BONE, mirror_x=True)
    # Calves
    b.add_capsule((0.24, -0.05, 0.50), (0.22, 0.02, 0.14), 0.14, MAT_FLESH_DARK, mirror_x=True)
    # Boots / Mutated feet
    b.add_box((0.22, -0.06, 0.07), (0.20, 0.36, 0.14), MAT_RAG, mirror_x=True)
    b.add_cone((0.22, -0.24, 0.05), (0.22, -0.36, 0.03), 0.05, MAT_BONE, mirror_x=True)

    # --- 3. TORSO & SPINE ---
    # Abdomen
    b.add_cylinder((0.0, 0.02, 0.95), (0.0, -0.04, 1.25), 0.28, MAT_FLESH)
    # Chest (broad hunched ribcage)
    b.add_sphere((0.0, -0.06, 1.48), (0.44, 0.38, 0.32), MAT_FLESH)
    # Torn tactical vest harness
    b.add_box((0.0, -0.07, 1.44), (0.48, 0.36, 0.32), MAT_RAG)
    # Exposed pulsating necrotic heart core in upper left chest
    b.add_sphere((-0.14, -0.22, 1.50), (0.12, 0.10, 0.12), MAT_CORE)
    b.add_cylinder((-0.14, -0.22, 1.50), (-0.14, -0.15, 1.35), 0.03, MAT_CORE)

    # Mutated vertebral spine spikes along hunched back
    spine_z = [1.05, 1.20, 1.35, 1.50, 1.65, 1.78]
    spine_y = [0.18, 0.22, 0.25, 0.24, 0.18, 0.12]
    spike_lens = [0.18, 0.26, 0.34, 0.38, 0.28, 0.16]
    for z, y, slen in zip(spine_z, spine_y, spike_lens):
        b.add_cone((0.0, y, z), (0.0, y + slen, z + slen * 0.4), 0.06, MAT_BONE)

    # Left armored shoulder pauldron
    b.add_plate((-0.42, -0.05, 1.65), (-0.44, 0.15, 1.65), (-0.46, 0.15, 1.42), (-0.44, -0.05, 1.42), 0.08, MAT_ARMOR)

    # --- 4. HEAD & BONE CROWN ---
    # Neck (hunched forward)
    b.add_cylinder((0.0, -0.04, 1.62), (0.0, -0.16, 1.82), 0.16, MAT_FLESH_DARK)
    # Skull
    b.add_sphere((0.0, -0.20, 1.94), (0.22, 0.24, 0.24), MAT_FLESH)
    # Exposed bone jaw / teeth
    b.add_box((0.0, -0.32, 1.84), (0.18, 0.16, 0.14), MAT_BONE)
    # Gaping fanged mouth
    b.add_cone((0.06, -0.38, 1.84), (0.06, -0.42, 1.81), 0.025, MAT_BONE, mirror_x=True)
    # Glowing infected eye (left)
    b.add_sphere((-0.09, -0.38, 1.98), (0.045, 0.045, 0.045), MAT_CORE)
    # Sunken eye socket (right)
    b.add_sphere((0.09, -0.37, 1.98), (0.04, 0.04, 0.04), MAT_FLESH_DARK)

    # Regal Bone Crown / Horns on mutated skull
    b.add_cone((0.0, -0.18, 2.14), (0.0, -0.10, 2.38), 0.06, MAT_BONE)
    b.add_cone((0.12, -0.16, 2.10), (0.24, -0.08, 2.34), 0.05, MAT_BONE, mirror_x=True)
    b.add_cone((0.18, -0.12, 2.04), (0.34, -0.02, 2.22), 0.045, MAT_BONE, mirror_x=True)

    # --- 5. LEFT ARM (COMMANDER ARM) ---
    # Shoulder
    b.add_sphere((-0.38, -0.02, 1.58), (0.16, 0.16, 0.16), MAT_FLESH_DARK)
    # Bicep
    b.add_capsule((-0.38, -0.02, 1.58), (-0.44, -0.12, 1.20), 0.13, MAT_FLESH)
    # Forearm & armored bracer
    b.add_capsule((-0.44, -0.12, 1.20), (-0.46, -0.26, 0.88), 0.12, MAT_ARMOR)
    # Hand & claw talons
    b.add_box((-0.46, -0.28, 0.82), (0.14, 0.16, 0.10), MAT_FLESH_DARK)
    for fx in [-0.04, 0.0, 0.04]:
        b.add_cone((-0.46 + fx, -0.34, 0.82), (-0.46 + fx, -0.46, 0.78), 0.02, MAT_BONE)

    # --- 6. RIGHT ARM (OVERSIZED MUTANT CRUSHER) ---
    # Massive hypertrophied shoulder
    b.add_sphere((0.44, -0.04, 1.58), (0.26, 0.26, 0.26), MAT_FLESH_DARK)
    # Massive bicep with vein ridges
    b.add_capsule((0.44, -0.04, 1.58), (0.54, -0.18, 1.12), 0.22, MAT_FLESH)
    b.add_capsule((0.58, -0.28, 1.40), (0.64, -0.15, 1.18), 0.04, MAT_CORE)
    # Colossal mutated forearm
    b.add_capsule((0.54, -0.18, 1.12), (0.58, -0.36, 0.68), 0.25, MAT_FLESH)
    # Heavy calcified crusher fist
    b.add_box((0.58, -0.42, 0.58), (0.34, 0.32, 0.28), MAT_FLESH_DARK)
    # 4 deadly razor bone talons
    for tx in [-0.10, -0.03, 0.04, 0.11]:
        b.add_cone((0.58 + tx, -0.54, 0.58), (0.58 + tx * 1.3, -0.80, 0.48), 0.045, MAT_BONE)

    out_path = os.path.join(OUT_DIR, "Boss_Zombie_Leader.obj")
    b.build_and_export(out_path, materials)


def build_boss_zombie_tank():
    b = EnemyMeshBuilder("Boss_Zombie_Tank")

    # Materials
    MAT_SKIN = 0
    MAT_MUSCLE = 1
    MAT_BONE = 2
    MAT_PUSTULE = 3
    MAT_VEIN = 4

    materials = [
        create_material("tank_skin", (0.38, 0.36, 0.44), roughness=0.82),
        create_material("tank_muscle_dark", (0.24, 0.20, 0.30), roughness=0.88),
        create_material("tank_calcified_bone", (0.76, 0.74, 0.68), roughness=0.60),
        create_material("tank_toxic_pustule", (0.72, 0.88, 0.16), roughness=0.35, emission_rgb=(0.78, 0.94, 0.18), emission_strength=3.5),
        create_material("tank_vein", (0.16, 0.12, 0.24), roughness=0.75),
    ]

    # --- 1. MASSIVE LOWER BODY & COLUMNS ---
    b.add_box((0.0, 0.0, 0.82), (0.76, 0.54, 0.36), MAT_MUSCLE)

    # Columnar tree-trunk legs
    b.add_capsule((0.32, 0.0, 0.78), (0.34, -0.06, 0.38), 0.24, MAT_SKIN, mirror_x=True)
    # Calcified knee plates
    b.add_box((0.34, -0.22, 0.38), (0.26, 0.16, 0.20), MAT_BONE, mirror_x=True)
    # Calves
    b.add_capsule((0.34, -0.06, 0.38), (0.32, 0.04, 0.12), 0.22, MAT_SKIN, mirror_x=True)
    # Stomping feet
    b.add_box((0.32, -0.06, 0.07), (0.30, 0.44, 0.14), MAT_MUSCLE, mirror_x=True)
    b.add_cone((0.32, -0.26, 0.07), (0.32, -0.40, 0.05), 0.08, MAT_BONE, mirror_x=True)

    # --- 2. COLOSSAL BARREL TORSO ---
    b.add_sphere((0.0, -0.02, 1.25), (0.64, 0.50, 0.42), MAT_SKIN)
    # Bloated abdominal belly
    b.add_sphere((0.0, -0.26, 1.05), (0.48, 0.36, 0.34), MAT_MUSCLE)

    # Toxic pustules cluster across belly & chest
    pustules = [
        (0.18, -0.48, 1.10, 0.10),
        (-0.22, -0.46, 1.15, 0.12),
        (0.04, -0.52, 1.22, 0.09),
        (-0.14, -0.44, 0.95, 0.08),
        (0.24, -0.40, 1.30, 0.11),
        (0.36, -0.25, 1.45, 0.13),
        (-0.34, -0.28, 1.42, 0.12),
    ]
    for px, py, pz, pr in pustules:
        b.add_sphere((px, py, pz), (pr, pr, pr), MAT_PUSTULE)

    # Thick varicose vein ridges running across chest
    b.add_cylinder((-0.30, -0.38, 1.32), (-0.05, -0.48, 1.15), 0.035, MAT_VEIN)
    b.add_cylinder((0.32, -0.35, 1.30), (0.10, -0.46, 1.12), 0.035, MAT_VEIN)

    # --- 3. IMMENSE TRAPEZIUS HUMP & BRUTISH SKULL ---
    # Trapezius muscle hump
    b.add_sphere((0.0, 0.12, 1.64), (0.62, 0.48, 0.36), MAT_MUSCLE)
    # Sunken brutish head recessed between shoulders
    b.add_sphere((0.0, -0.22, 1.58), (0.24, 0.22, 0.20), MAT_SKIN)
    # Heavy brow ridge & calcified skull plate
    b.add_box((0.0, -0.32, 1.66), (0.28, 0.16, 0.10), MAT_BONE)
    # Brutish jaw
    b.add_box((0.0, -0.34, 1.50), (0.24, 0.18, 0.14), MAT_MUSCLE)
    # Glowing yellow toxic eyes
    b.add_sphere((0.09, -0.39, 1.62), (0.035, 0.035, 0.035), MAT_PUSTULE, mirror_x=True)

    # --- 4. COLOSSAL ARMS & GROUND-SLAM FISTS ---
    # Massive shoulders
    b.add_sphere((0.62, 0.02, 1.52), (0.34, 0.34, 0.34), MAT_MUSCLE, mirror_x=True)
    # Thick biceps
    b.add_capsule((0.62, 0.02, 1.52), (0.76, -0.16, 1.05), 0.28, MAT_SKIN, mirror_x=True)
    # Forearms with calcified bone ridges
    b.add_capsule((0.76, -0.16, 1.05), (0.80, -0.34, 0.55), 0.27, MAT_MUSCLE, mirror_x=True)
    b.add_plate((0.70, -0.40, 1.05), (0.92, -0.40, 1.05), (0.94, -0.52, 0.65), (0.72, -0.52, 0.65), 0.10, MAT_BONE, mirror_x=True)
    # Colossal boulder fists
    b.add_box((0.80, -0.42, 0.44), (0.42, 0.40, 0.36), MAT_SKIN, mirror_x=True)
    # Knuckle bone spikes for ground stomping
    for kx in [-0.10, 0.0, 0.10]:
        b.add_cone((0.80 + kx, -0.58, 0.44), (0.80 + kx * 1.2, -0.74, 0.40), 0.06, MAT_BONE, mirror_x=True)

    out_path = os.path.join(OUT_DIR, "Boss_Zombie_Tank.obj")
    b.build_and_export(out_path, materials)


if __name__ == "__main__":
    print("Building Boss Zombie models...")
    build_boss_zombie_leader()
    build_boss_zombie_tank()
    print("Completed Boss Zombie generation.")


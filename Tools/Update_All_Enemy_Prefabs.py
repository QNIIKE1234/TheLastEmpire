"""
Updates All 17 Enemy Prefabs in Assets/Resources_Addressable/Prefabs/Character/
1. Creates dedicated URP Lit Material assets (.mat + .meta) in Assets/Materials/Enemies/
2. Updates MeshFilter and MeshRenderer in all 17 prefabs with the new models and materials.
"""

import os
import re
import sys
import uuid

ROOT_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
PREFAB_DIR = os.path.join(ROOT_DIR, "Assets", "Resources_Addressable", "Prefabs", "Character")
MODEL_DIR = os.path.join(ROOT_DIR, "Assets", "GeneratedModels")
MAT_BASE_DIR = os.path.join(ROOT_DIR, "Assets", "Materials", "Enemies")

# Shader GUID for Universal Render Pipeline/Lit
URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"

# Material Specs: (name, category, color_rgb, metallic, smoothness, emission_rgb, emission_strength)
MAT_SPECS = {
    # --- Zombies ---
    "zombie_bone": ("Zombies", (0.86, 0.82, 0.68), 0.10, 0.45, None, 0.0),
    "zombie_flesh": ("Zombies", (0.34, 0.42, 0.30), 0.00, 0.25, None, 0.0),
    "zombie_flesh_dark": ("Zombies", (0.16, 0.20, 0.15), 0.00, 0.15, None, 0.0),
    "zombie_metal_armor": ("Zombies", (0.30, 0.32, 0.33), 0.75, 0.55, None, 0.0),
    "zombie_necro_core": ("Zombies", (0.95, 0.15, 0.08), 0.00, 0.30, (1.0, 0.18, 0.10), 4.0),
    "zombie_tactical_rag": ("Zombies", (0.18, 0.19, 0.20), 0.00, 0.12, None, 0.0),
    "tank_calcified_bone": ("Zombies", (0.76, 0.74, 0.68), 0.15, 0.40, None, 0.0),
    "tank_muscle_dark": ("Zombies", (0.24, 0.20, 0.30), 0.00, 0.12, None, 0.0),
    "tank_skin": ("Zombies", (0.38, 0.36, 0.44), 0.00, 0.18, None, 0.0),
    "tank_toxic_pustule": ("Zombies", (0.72, 0.88, 0.16), 0.00, 0.35, (0.78, 0.94, 0.18), 3.5),
    "tank_vein": ("Zombies", (0.16, 0.12, 0.24), 0.00, 0.25, None, 0.0),

    # --- Robots ---
    "mech_amber_glow": ("Robots", (1.0, 0.65, 0.10), 0.00, 0.25, (1.0, 0.65, 0.10), 4.0),
    "mech_armor_gray": ("Robots", (0.62, 0.64, 0.66), 0.60, 0.60, None, 0.0),
    "mech_joint_black": ("Robots", (0.12, 0.12, 0.14), 0.20, 0.25, None, 0.0),
    "mech_steel": ("Robots", (0.28, 0.30, 0.32), 0.80, 0.55, None, 0.0),

    "blade_carbon": ("Robots", (0.10, 0.10, 0.12), 0.70, 0.65, None, 0.0),
    "blade_chassis": ("Robots", (0.22, 0.24, 0.26), 0.80, 0.50, None, 0.0),
    "blade_edge_silver": ("Robots", (0.86, 0.88, 0.90), 0.95, 0.80, None, 0.0),
    "blade_ruby_glow": ("Robots", (1.0, 0.10, 0.15), 0.00, 0.20, (1.0, 0.10, 0.15), 4.5),

    "drone_composite": ("Robots", (0.86, 0.88, 0.90), 0.40, 0.65, None, 0.0),
    "drone_sensor_cyan": ("Robots", (0.10, 0.85, 1.0), 0.00, 0.20, (0.10, 0.85, 1.0), 4.5),
    "drone_trim_dark": ("Robots", (0.12, 0.12, 0.14), 0.30, 0.40, None, 0.0),
    "drone_turbines": ("Robots", (0.24, 0.26, 0.28), 0.85, 0.55, None, 0.0),

    "gunner_brass": ("Robots", (0.85, 0.68, 0.28), 0.92, 0.70, None, 0.0),
    "gunner_olive": ("Robots", (0.28, 0.32, 0.24), 0.70, 0.40, None, 0.0),
    "gunner_red_glow": ("Robots", (1.0, 0.12, 0.12), 0.00, 0.20, (1.0, 0.12, 0.12), 4.5),
    "gunner_steel": ("Robots", (0.22, 0.23, 0.25), 0.88, 0.60, None, 0.0),

    "hazard_black": ("Robots", (0.08, 0.08, 0.09), 0.20, 0.30, None, 0.0),
    "hazard_yellow": ("Robots", (0.92, 0.78, 0.12), 0.50, 0.55, None, 0.0),
    "kamikaze_core_glow": ("Robots", (1.0, 0.22, 0.05), 0.00, 0.20, (1.0, 0.22, 0.05), 5.0),
    "spider_iron": ("Robots", (0.20, 0.22, 0.22), 0.85, 0.50, None, 0.0),

    "laser_cobalt": ("Robots", (0.15, 0.28, 0.48), 0.80, 0.55, None, 0.0),
    "laser_cyan_glow": ("Robots", (0.10, 0.90, 1.0), 0.00, 0.15, (0.10, 0.90, 1.0), 5.0),
    "laser_heatsink": ("Robots", (0.78, 0.45, 0.25), 0.90, 0.65, None, 0.0),
    "laser_white": ("Robots", (0.88, 0.90, 0.92), 0.30, 0.70, None, 0.0),

    "tanker_amber_glow": ("Robots", (1.0, 0.65, 0.10), 0.00, 0.25, (1.0, 0.65, 0.10), 4.0),
    "tanker_hazard": ("Robots", (0.90, 0.75, 0.12), 0.40, 0.50, None, 0.0),
    "tanker_hydraulics": ("Robots", (0.80, 0.82, 0.85), 0.92, 0.80, None, 0.0),
    "tanker_shield_iron": ("Robots", (0.38, 0.40, 0.42), 0.75, 0.55, None, 0.0),
    "tanker_tungsten": ("Robots", (0.24, 0.25, 0.26), 0.82, 0.50, None, 0.0),

    "leader_armor_black": ("Robots", (0.12, 0.13, 0.14), 0.75, 0.55, None, 0.0),
    "leader_fusion_glow": ("Robots", (1.0, 0.50, 0.08), 0.00, 0.20, (1.0, 0.50, 0.08), 5.0),
    "leader_gold_trim": ("Robots", (0.88, 0.72, 0.24), 0.90, 0.70, None, 0.0),
    "leader_gunmetal": ("Robots", (0.28, 0.30, 0.32), 0.85, 0.60, None, 0.0),
    "leader_sensor_red": ("Robots", (1.0, 0.12, 0.12), 0.00, 0.20, (1.0, 0.12, 0.12), 4.5),

    # --- Aliens ---
    "alien_acid_glow": ("Aliens", (0.40, 0.95, 0.10), 0.00, 0.20, (0.40, 0.95, 0.10), 4.0),
    "alien_carapace": ("Aliens", (0.12, 0.18, 0.14), 0.60, 0.65, None, 0.0),
    "alien_flesh": ("Aliens", (0.18, 0.12, 0.16), 0.10, 0.35, None, 0.0),
    "alien_teeth": ("Aliens", (0.88, 0.86, 0.72), 0.20, 0.60, None, 0.0),

    "crawler_belly": ("Aliens", (0.42, 0.38, 0.44), 0.10, 0.30, None, 0.0),
    "crawler_chitin": ("Aliens", (0.28, 0.22, 0.32), 0.50, 0.55, None, 0.0),
    "crawler_claws": ("Aliens", (0.82, 0.78, 0.65), 0.30, 0.65, None, 0.0),
    "crawler_eye_glow": ("Aliens", (0.15, 0.95, 0.25), 0.00, 0.20, (0.15, 0.95, 0.25), 4.5),

    "hook_barbs": ("Aliens", (0.88, 0.84, 0.70), 0.35, 0.70, None, 0.0),
    "hook_carapace": ("Aliens", (0.22, 0.20, 0.24), 0.60, 0.55, None, 0.0),
    "hook_skin": ("Aliens", (0.48, 0.46, 0.44), 0.15, 0.35, None, 0.0),
    "hook_tendrils": ("Aliens", (0.75, 0.15, 0.12), 0.00, 0.35, (0.75, 0.15, 0.12), 3.0),

    "shadow_carapace": ("Aliens", (0.08, 0.08, 0.11), 0.75, 0.70, None, 0.0),
    "shadow_psionic_glow": ("Aliens", (0.45, 0.25, 0.95), 0.00, 0.15, (0.45, 0.25, 0.95), 4.5),
    "shadow_scythe": ("Aliens", (0.18, 0.20, 0.24), 0.90, 0.80, None, 0.0),

    "smart_body": ("Aliens", (0.12, 0.12, 0.18), 0.20, 0.40, None, 0.0),
    "smart_brain_glow": ("Aliens", (0.95, 0.15, 0.70), 0.00, 0.20, (0.95, 0.15, 0.70), 5.0),
    "smart_chitin": ("Aliens", (0.18, 0.16, 0.32), 0.60, 0.60, None, 0.0),
    "smart_nerves": ("Aliens", (0.10, 0.85, 1.0), 0.00, 0.20, (0.10, 0.85, 1.0), 4.5),

    "spitter_bile_glow": ("Aliens", (0.65, 0.95, 0.12), 0.00, 0.20, (0.65, 0.95, 0.12), 5.0),
    "spitter_carapace": ("Aliens", (0.32, 0.38, 0.22), 0.40, 0.45, None, 0.0),
    "spitter_flesh": ("Aliens", (0.22, 0.26, 0.18), 0.10, 0.25, None, 0.0),
    "spitter_teeth": ("Aliens", (0.75, 0.78, 0.55), 0.20, 0.60, None, 0.0),

    "queen_black_chitin": ("Aliens", (0.10, 0.09, 0.12), 0.70, 0.65, None, 0.0),
    "queen_bone_spikes": ("Aliens", (0.82, 0.78, 0.68), 0.30, 0.60, None, 0.0),
    "queen_brood_glow": ("Aliens", (1.0, 0.60, 0.10), 0.00, 0.20, (1.0, 0.60, 0.10), 4.5),
    "queen_psionic_glow": ("Aliens", (0.75, 0.15, 0.95), 0.00, 0.15, (0.75, 0.15, 0.95), 5.0),
    "queen_purple_flesh": ("Aliens", (0.24, 0.16, 0.32), 0.20, 0.40, None, 0.0),
}

# Mapping: Prefab Name -> (Model Folder, Model File, Model GUID, Visual Node Name, IsBoss)
ENEMY_PREFAB_MAP = {
    # Boss Zombies
    "ZombieLeader": ("Zombies", "Boss_Zombie_Leader.obj", "ed2eefcf35c45867baf469a181f100cc", "Boss_Zombie_Leader", True),
    "ZombieTank": ("Zombies", "Boss_Zombie_Tank.obj", "4bc1f3919a42523cbdc1e5b05d71b5c7", "Boss_Zombie_Tank", False),

    # Robots (8)
    "RobotNormal": ("Robots", "Robot_Normal.obj", "53edb32926c15f23aef34838abc4bae4", "Robot_Normal", False),
    "RobotBlade": ("Robots", "Robot_Blade.obj", "333c5ace400755f59c387b0f753dafef", "Robot_Blade", False),
    "RobotDrone": ("Robots", "Robot_Drone.obj", "1cefb74e066d586ab3f849e83e099591", "Robot_Drone", False),
    "RobotGunner": ("Robots", "Robot_Gunner.obj", "1c87ff2615eb5f4888db113454633845", "Robot_Gunner", False),
    "RobotKamikaze": ("Robots", "Robot_Kamikaze.obj", "cda43b9dabdc511795802bc391412fb9", "Robot_Kamikaze", False),
    "RobotLaser": ("Robots", "Robot_Laser.obj", "c07fc57b40385a4ea7b13e065c9c96f8", "Robot_Laser", False),
    "RobotTanker": ("Robots", "Robot_Tanker.obj", "91e710dc2775533cb4c870d9001503dc", "Robot_Tanker", False),
    "RobotLeader": ("Robots", "Robot_Leader.obj", "b2250313f307569a8080f68287f40840", "Robot_Leader", True),

    # Aliens (7)
    "AlienNormal": ("Aliens", "Alien_Normal.obj", "aa83263a17aa506a9b79b9f7ab6c15d9", "Alien_Normal", False),
    "AlienCrawler": ("Aliens", "Alien_Crawler.obj", "6e54809a062c5bdbacf7c17ab41a10c0", "Alien_Crawler", False),
    "AlienHook": ("Aliens", "Alien_Hook.obj", "8eff1fca70bb569887063110a4d7f2af", "Alien_Hook", False),
    "AlienInvisible": ("Aliens", "Alien_Invisible.obj", "87272d1548bd5e628fe333f07a6b98e2", "Alien_Invisible", False),
    "AlienSmart": ("Aliens", "Alien_Smart.obj", "8ce2a0e2b4d0567983bc864cf251c316", "Alien_Smart", False),
    "AlienSpitter": ("Aliens", "Alien_Spitter.obj", "df0e7e3a58cc52219a8466b4754ee4c9", "Alien_Spitter", False),
    "AlienLeader": ("Aliens", "Alien_Leader.obj", "b3d2adad0a1158ffa8564633875b05a6", "Alien_Leader", True),
}


def create_all_materials():
    """Generates all .mat and .mat.meta files in Assets/Materials/Enemies/"""
    mat_guid_map = {}
    for mat_name, (cat, col, metal, smooth, emis, emis_str) in MAT_SPECS.items():
        cat_dir = os.path.join(MAT_BASE_DIR, cat)
        os.makedirs(cat_dir, exist_ok=True)
        mat_path = os.path.join(cat_dir, f"Mat_{mat_name}.mat")
        meta_path = mat_path + ".meta"

        guid = uuid.uuid5(uuid.NAMESPACE_DNS, f"Mat_{mat_name}").hex
        mat_guid_map[mat_name] = guid

        # Format emission values
        if emis and emis_str > 0.0:
            er, eg, eb = emis
            emis_r, emis_g, emis_b = er * emis_str, eg * emis_str, eb * emis_str
            keywords = "[\"_EMISSION\"]"
        else:
            emis_r, emis_g, emis_b = 0.0, 0.0, 0.0
            keywords = "[]"

        r, g, b = col

        mat_content = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &-5625678584252473414
MonoBehaviour:
  m_ObjectHideFlags: 11
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: d0353a89b1f911e48b9e16bdc9f2e058, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: Mat_{mat_name}
  m_Shader: {{fileID: 4800000, guid: {URP_LIT_SHADER_GUID}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: {keywords}
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties: 
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
    - _BaseMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _BumpMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailAlbedoMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailMask:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _DetailNormalMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _EmissionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _MetallicGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _OcclusionMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - _SpecGlossMap:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - unity_Lightmaps:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - unity_LightmapsInd:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    - unity_ShadowMasks:
        m_Texture: {{fileID: 0}}
        m_Scale: {{x: 1, y: 1}}
        m_Offset: {{x: 0, y: 0}}
    m_Ints: []
    m_Floats:
    - _AddPrecomputedVelocity: 0
    - _AlphaClip: 0
    - _AlphaToMask: 0
    - _Blend: 0
    - _BlendModePreserveSpecular: 1
    - _BumpScale: 1
    - _ClearCoatMask: 0
    - _ClearCoatSmoothness: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DetailAlbedoMapScale: 1
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _DstBlendAlpha: 0
    - _EnvironmentReflections: 1
    - _GlossMapScale: 0
    - _Glossiness: 0
    - _GlossyReflections: 0
    - _Metallic: {metal}
    - _OcclusionStrength: 1
    - _Parallax: 0.005
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: {smooth}
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _SrcBlendAlpha: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _XRMotionVectorsPass: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _Color: {{r: {r}, g: {g}, b: {b}, a: 1}}
    - _EmissionColor: {{r: {emis_r}, g: {emis_g}, b: {emis_b}, a: 1}}
    - _SpecColor: {{r: 0.2, g: 0.2, b: 0.2, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
"""
        with open(mat_path, "w", encoding="utf-8") as f:
            f.write(mat_content)

        meta_content = f"""fileFormatVersion: 2
guid: {guid}
NativeFormatImporter:
  externalObjects: {{}}
  mainObjectFileID: 2100000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
        with open(meta_path, "w", encoding="utf-8") as f:
            f.write(meta_content)

    print(f"[Setup] Generated {len(MAT_SPECS)} URP Lit materials in {MAT_BASE_DIR}")
    return mat_guid_map


def get_model_materials(folder, model_file):
    """Reads .mtl file to get the exact list of material names."""
    mtl_name = os.path.splitext(model_file)[0] + ".mtl"
    mtl_path = os.path.join(MODEL_DIR, folder, mtl_name)
    mats = []
    if os.path.exists(mtl_path):
        with open(mtl_path, "r", encoding="utf-8") as f:
            for line in f:
                if line.startswith("newmtl "):
                    mats.append(line.strip().split()[1])
    return mats


def update_standard_prefab(prefab_name, folder, model_file, model_guid, visual_node_name, mat_guid_map):
    """Updates a standard prefab by updating MeshFilter, MeshRenderer materials, and renaming visual GameObject."""
    prefab_path = os.path.join(PREFAB_DIR, f"{prefab_name}.prefab")
    if not os.path.exists(prefab_path):
        print(f"[Error] Prefab not found: {prefab_path}")
        return

    with open(prefab_path, "r", encoding="utf-8") as f:
        content = f.read()

    # Get materials from .mtl
    mats = get_model_materials(folder, model_file)
    mat_entries = "\n".join([f"  - {{fileID: 2100000, guid: {mat_guid_map[m]}, type: 2}}" for m in mats])

    # 1. Update MeshFilter mesh
    # Match: m_Mesh: {fileID: -2432090755550338912, guid: [a-f0-9]{32}, type: 3}
    pattern_mesh = r"m_Mesh:\s*\{fileID:\s*-2432090755550338912,\s*guid:\s*[a-f0-9]{32},\s*type:\s*3\}"
    rep_mesh = f"m_Mesh: {{fileID: -2432090755550338912, guid: {model_guid}, type: 3}}"
    new_content, count = re.subn(pattern_mesh, rep_mesh, content)
    if count == 0:
        print(f"[Warning] No matching MeshFilter found in {prefab_name}.prefab")

    # 2. Update MeshRenderer m_Materials
    # Find the MeshRenderer block that had the old materials and replace m_Materials:
    pattern_mats = r"(MeshRenderer:[\s\S]*?m_Materials:\n)(?:  - \{fileID:[^\n]+\}\n)+"
    rep_mats = r"\g<1>" + mat_entries + "\n"
    new_content, mat_count = re.subn(pattern_mats, rep_mats, new_content, count=1)
    if mat_count == 0:
        print(f"[Warning] Could not replace MeshRenderer materials in {prefab_name}.prefab")

    # 3. Rename old dummy GameObject to visual_node_name
    for old_dummy in ["Zombie_01_Worker_Dummy", "Zombie_02_Civilian_Dummy", "Zombie_03_Police_Dummy", "Zombie_Dog_Dummy", "Zombie_Fat_Dummy"]:
        new_content = new_content.replace(f"m_Name: {old_dummy}", f"m_Name: {visual_node_name}")

    with open(prefab_path, "w", encoding="utf-8") as f:
        f.write(new_content)

    print(f"[Success] Updated standard prefab: {prefab_name}.prefab -> {visual_node_name} ({len(mats)} mats)")


def update_boss_prefab(prefab_name, folder, model_file, model_guid, visual_node_name, mat_guid_map):
    """Updates a Boss prefab (replacing 15-child ChibiGhostOperator with a clean model + default child)."""
    prefab_path = os.path.join(PREFAB_DIR, f"{prefab_name}.prefab")
    if not os.path.exists(prefab_path):
        print(f"[Error] Prefab not found: {prefab_path}")
        return

    with open(prefab_path, "r", encoding="utf-8") as f:
        content = f.read()

    mats = get_model_materials(folder, model_file)
    mat_entries = "\n".join([f"  - {{fileID: 2100000, guid: {mat_guid_map[m]}, type: 2}}" for m in mats])

    # In ZombieLeader, RobotLeader, AlienLeader:
    # They have root Transform `fileID: 2278688315463160727` whose children are:
    # - Canvas
    # - Capsule
    # - fileID: 1303949577371160665 (ChibiGhostOperator Transform)
    #
    # We replace the ChibiGhostOperator GameObject (1824907380430392035) & Transform (1303949577371160665)
    # and all its 15 sub-mesh GameObjects/Components with:
    # Visual GameObject (named visual_node_name) with Transform (1303949577371160665)
    # and child 'default' GameObject (7711000000000000001) with MeshFilter & MeshRenderer!

    # Define unique fileIDs for the new default child
    CHILD_GO_ID = "7711000000000000001"
    CHILD_TRANS_ID = "7711000000000000002"
    CHILD_MF_ID = "7711000000000000003"
    CHILD_MR_ID = "7711000000000000004"

    # Scale for bosses
    scale_xyz = "1.2" if "Leader" in prefab_name else "1.0"

    replacement_visual_block = f"""--- !u!1 &1824907380430392035
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: 1303949577371160665}}
  m_Layer: 0
  m_Name: {visual_node_name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &1303949577371160665
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1824907380430392035}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: -1, z: 0}}
  m_LocalScale: {{x: {scale_xyz}, y: {scale_xyz}, z: {scale_xyz}}}
  m_ConstrainProportionsScale: 1
  m_Children:
  - {{fileID: {CHILD_TRANS_ID}}}
  m_Father: {{fileID: 2278688315463160727}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!1 &{CHILD_GO_ID}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {CHILD_TRANS_ID}}}
  - component: {{fileID: {CHILD_MF_ID}}}
  - component: {{fileID: {CHILD_MR_ID}}}
  m_Layer: 0
  m_Name: default
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{CHILD_TRANS_ID}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {CHILD_GO_ID}}}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: 1303949577371160665}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
--- !u!33 &{CHILD_MF_ID}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {CHILD_GO_ID}}}
  m_Mesh: {{fileID: -2432090755550338912, guid: {model_guid}, type: 3}}
--- !u!23 &{CHILD_MR_ID}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {CHILD_GO_ID}}}
  m_Enabled: 1
  m_CastShadows: 1
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
{mat_entries}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""

    # We need to strip out all blocks from ChibiGhostOperator sub-objects:
    # Sub-objects were: Mat_MetalDark, Mat_BootSole, Mat_HelmetTan, Mat_HeadsetOlive, Mat_PolymerBlack,
    # Mat_VestTan, Mat_ScarfWoven, Mat_GlossyEye, Mat_SkullBone, Mat_BootTan, Mat_FoamDark, Mat_Balaclava,
    # Mat_ClothCharcoal, Mat_FlagPatch, Mat_SkinForearm.
    # And replace GameObject 1824907380430392035 with replacement_visual_block!

    # Split prefab into YAML documents:
    docs = content.split("\n--- ")
    cleaned_docs = []
    # Always keep header
    header = docs[0]
    replaced_visual = False

    for doc in docs[1:]:
        full_doc = "--- " + doc
        # Check if this doc is one of the 15 ChibiGhostOperator parts
        is_chibi_part = False
        for part_name in ["Mat_MetalDark", "Mat_BootSole", "Mat_HelmetTan", "Mat_HeadsetOlive", "Mat_PolymerBlack",
                          "Mat_VestTan", "Mat_ScarfWoven", "Mat_GlossyEye", "Mat_SkullBone", "Mat_BootTan",
                          "Mat_FoamDark", "Mat_Balaclava", "Mat_ClothCharcoal", "Mat_FlagPatch", "Mat_SkinForearm"]:
            if f"m_Name: {part_name}" in full_doc or f"m_Father: {{fileID: 1303949577371160665}}" in full_doc:
                is_chibi_part = True
                break

        if "GameObject: 1824907380430392035" in full_doc or "m_Name: ChibiGhostOperator" in full_doc:
            # Replace with new visual block
            if not replaced_visual:
                cleaned_docs.append(replacement_visual_block.strip())
                replaced_visual = True
            continue
        elif "Transform: 1303949577371160665" in full_doc or "&1303949577371160665" in full_doc:
            # Covered by replacement_visual_block
            continue
        elif is_chibi_part:
            # Drop obsolete Chibi sub-part document
            continue
        else:
            cleaned_docs.append(full_doc.strip())

    final_content = header + "\n" + "\n".join(cleaned_docs) + "\n"

    with open(prefab_path, "w", encoding="utf-8") as f:
        f.write(final_content)

    print(f"[Success] Updated Boss prefab: {prefab_name}.prefab -> {visual_node_name} ({len(mats)} mats)")


def main():
    print("==================================================")
    print("Updating All 17 Enemy Prefabs and Materials")
    print("==================================================")

    mat_guid_map = create_all_materials()

    for prefab_name, (folder, model_file, model_guid, visual_node_name, is_boss) in ENEMY_PREFAB_MAP.items():
        if is_boss:
            update_boss_prefab(prefab_name, folder, model_file, model_guid, visual_node_name, mat_guid_map)
        else:
            update_standard_prefab(prefab_name, folder, model_file, model_guid, visual_node_name, mat_guid_map)

    print("==================================================")
    print("All 17 Enemy Prefabs Successfully Updated!")
    print("==================================================")


if __name__ == "__main__":
    main()


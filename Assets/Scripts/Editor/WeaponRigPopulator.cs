#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Applies clean stylized URP material finishes to all Assets/GeneratedModels/Weapons/*.obj models,
    /// creates/updates prefabs in Assets/Resources_Addressable/Prefabs/Weapon/,
    /// and auto-populates all 18 handheld weapons (9 Melee under Malee + 9 Ranged under Range)
    /// into Chibi_PunkGirl_Rigged.prefab so every playable character automatically holds every weapon!
    /// </summary>
    public class WeaponMaterialPostprocessor : AssetPostprocessor
    {
        private static bool IsWeaponObj(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string norm = path.Replace('\\', '/');
            return norm.Contains("Assets/GeneratedModels/Weapons/") &&
                   norm.EndsWith(".obj", StringComparison.OrdinalIgnoreCase);
        }

        private void OnPostprocessMaterial(Material material)
        {
            if (!IsWeaponObj(assetPath) || material == null) return;
            ApplyStylizedWeaponMaterialFinish(material);
        }

        public static void ApplyStylizedWeaponMaterialFinish(Material mat)
        {
            if (mat == null) return;
            string n = mat.name;

            float smoothness = 0.22f;
            float metallic = 0.0f;

            if (n.Contains("ChromeSilver"))
            {
                smoothness = 0.72f;
                metallic = 0.85f;
            }
            else if (n.Contains("SteelBright"))
            {
                smoothness = 0.62f;
                metallic = 0.75f;
            }
            else if (n.Contains("BrassGold"))
            {
                smoothness = 0.58f;
                metallic = 0.78f;
            }
            else if (n.Contains("GunMetal"))
            {
                smoothness = 0.42f;
                metallic = 0.55f;
            }
            else if (n.Contains("FireRed") || n.Contains("HazardYellow") || n.Contains("SafetyOrange"))
            {
                smoothness = 0.34f;
                metallic = 0.10f;
            }
            else if (n.Contains("ElectricCyan"))
            {
                smoothness = 0.65f;
                metallic = 0.15f;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(1.0f, 0.88f, 0.15f) * 1.4f);
                }
            }
            else if (n.Contains("Rubber") || n.Contains("Wood") || n.Contains("Polymer") || n.Contains("Military") || n.Contains("Sniper"))
            {
                smoothness = 0.14f;
                metallic = 0.04f;
            }

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        }
    }

    [InitializeOnLoad]
    public static class WeaponRigPopulator
    {
        private const string WeaponsModelDir = "Assets/GeneratedModels/Weapons";
        private const string WeaponsPrefabDir = "Assets/Resources_Addressable/Prefabs/Weapon";
        private const string MasterWeaponRigPrefab = "Assets/Resources_Addressable/Prefabs/Character/Model/Chibi_PunkGirl_Rigged.prefab";

        private readonly struct WeaponSpec
        {
            public readonly string Name;
            public readonly bool IsRanged;
            public readonly Vector3 LocalPos;
            public readonly Vector3 LocalEuler;
            public readonly float UniformScale;

            public WeaponSpec(string name, bool isRanged, Vector3 pos, Vector3 euler, float scale)
            {
                Name = name;
                IsRanged = isRanged;
                LocalPos = pos;
                LocalEuler = euler;
                UniformScale = scale;
            }
        }

        // Calibrated from the existing user-tuned Pistol / SMG / Knife / Katana / FireAxe / BaseballBat / Machete slots!
        private static readonly WeaponSpec[] AllHandheldWeapons = new[]
        {
            // ---- RANGED (9) under "Range" ----
            new WeaponSpec("Weapon_Pistol",         true, new Vector3(0.00104f, 0.00158f, 0.00041f), new Vector3(-45f, -25f, -90f), 0.020f),
            new WeaponSpec("Weapon_Shotgun",        true, new Vector3(0.00050f, 0.00140f, 0.00160f), new Vector3(-53f, -31f, -80f), 0.017f),
            new WeaponSpec("Weapon_Rifle",          true, new Vector3(0.00045f, 0.00140f, 0.00180f), new Vector3(-53f, -31f, -80f), 0.016f),
            new WeaponSpec("Weapon_SMG",            true, new Vector3(0.00050f, 0.00140f, 0.00190f), new Vector3(-53f, -31f, -80f), 0.020f),
            new WeaponSpec("Weapon_SniperRifle",    true, new Vector3(0.00045f, 0.00140f, 0.00180f), new Vector3(-53f, -31f, -80f), 0.015f),
            new WeaponSpec("Weapon_Revolver",       true, new Vector3(0.00100f, 0.00155f, 0.00050f), new Vector3(-45f, -25f, -90f), 0.020f),
            new WeaponSpec("Weapon_Flamethrower",   true, new Vector3(0.00045f, 0.00145f, 0.00175f), new Vector3(-53f, -31f, -80f), 0.015f),
            new WeaponSpec("Weapon_RocketLauncher", true, new Vector3(0.00040f, 0.00160f, 0.00160f), new Vector3(-53f, -31f, -80f), 0.015f),
            new WeaponSpec("Weapon_Crossbow",       true, new Vector3(0.00055f, 0.00145f, 0.00150f), new Vector3(-53f, -31f, -80f), 0.016f),

            // ---- MELEE (9) under "Malee" ----
            new WeaponSpec("Weapon_Knife",          false, new Vector3(-0.00074f, 0.00086f,  0.00037f), new Vector3(-90f,   90f,   0f), 0.020f),
            new WeaponSpec("Weapon_BaseballBat",    false, new Vector3( 0.00512f,-0.00063f, -0.00100f), new Vector3(  0f,    0f, -90f), 0.020f),
            new WeaponSpec("Weapon_Machete",        false, new Vector3( 0.00101f, 0.00089f, -0.00004f), new Vector3(-90f, -180f,  90f), 0.015f),
            new WeaponSpec("Weapon_FireAxe",        false, new Vector3( 0.00213f, 0.00085f,  0.00000f), new Vector3(-90f,  180f,  90f), 0.015f),
            new WeaponSpec("Weapon_Katana",         false, new Vector3( 0.00113f, 0.00138f,  0.00000f), new Vector3(  0f,    0f, -90f), 0.010f),
            new WeaponSpec("Weapon_Sledgehammer",   false, new Vector3( 0.00210f, 0.00085f,  0.00000f), new Vector3(-90f,  180f,  90f), 0.015f),
            new WeaponSpec("Weapon_SurvivalSpear",  false, new Vector3( 0.00150f, 0.00085f,  0.00000f), new Vector3(  0f,    0f, -90f), 0.012f),
            new WeaponSpec("Weapon_StunBaton",      false, new Vector3( 0.00120f, 0.00090f,  0.00000f), new Vector3(  0f,    0f, -90f), 0.016f),
            // Chainsaw is horizontal (+Z forward like guns), so it uses the gun grip orientation under Malee!
            new WeaponSpec("Weapon_Chainsaw",       false, new Vector3( 0.00050f, 0.00140f,  0.00160f), new Vector3(-53f,  -31f, -80f), 0.016f),
        };

        static WeaponRigPopulator()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureAllWeaponsInstalled(forceRefreshPrefabs: false, showDialog: false);
            };
        }

        [MenuItem("Tools/The Last Empire/Populate All 18 Stylized Weapons on Player Rig")]
        public static void PopulateAllWeaponsMenu()
        {
            EnsureAllWeaponsInstalled(forceRefreshPrefabs: true, showDialog: true);
        }

        private static void EnsureAllWeaponsInstalled(bool forceRefreshPrefabs, bool showDialog)
        {
            if (!Directory.Exists(WeaponsModelDir) || !File.Exists(MasterWeaponRigPrefab)) return;

            if (!Directory.Exists(WeaponsPrefabDir))
            {
                Directory.CreateDirectory(WeaponsPrefabDir);
            }

            // 1. Ensure individual Weapon_*.prefab exists for all 19 weapon OBJs (18 handheld + Grenade)
            int createdPrefabs = 0;
            foreach (WeaponSpec spec in AllHandheldWeapons)
            {
                if (EnsureWeaponPrefab(spec.Name, spec.LocalPos, spec.LocalEuler, spec.UniformScale, forceRefreshPrefabs))
                {
                    createdPrefabs++;
                }
            }
            if (EnsureWeaponPrefab("Weapon_Grenade", Vector3.zero, Vector3.zero, 0.020f, forceRefreshPrefabs))
            {
                createdPrefabs++;
            }

            // 2. Check if MasterWeaponRigPrefab already has all 18 handheld weapons
            GameObject masterAsset = AssetDatabase.LoadAssetAtPath<GameObject>(MasterWeaponRigPrefab);
            if (masterAsset == null) return;

            Transform rangeCheck = FindDeepChild(masterAsset.transform, "Range");
            Transform meleeCheck = FindDeepChild(masterAsset.transform, "Malee");
            if (rangeCheck == null || meleeCheck == null) return;

            bool anyMissing = false;
            foreach (WeaponSpec spec in AllHandheldWeapons)
            {
                Transform holder = spec.IsRanged ? rangeCheck : meleeCheck;
                if (holder.Find(spec.Name) == null)
                {
                    anyMissing = true;
                    break;
                }
            }

            if (!anyMissing && !forceRefreshPrefabs && createdPrefabs == 0)
            {
                return;
            }

            int addedToRig = 0;
            if (anyMissing)
            {
                GameObject root = PrefabUtility.LoadPrefabContents(MasterWeaponRigPrefab);
                try
                {
                    Transform rangeHolder = FindDeepChild(root.transform, "Range");
                    Transform meleeHolder = FindDeepChild(root.transform, "Malee");
                    if (rangeHolder != null && meleeHolder != null)
                    {
                        foreach (WeaponSpec spec in AllHandheldWeapons)
                        {
                            Transform holder = spec.IsRanged ? rangeHolder : meleeHolder;
                            if (holder.Find(spec.Name) != null) continue; // Keep existing user-calibrated weapon intact!

                            string prefabPath = $"{WeaponsPrefabDir}/{spec.Name}.prefab";
                            GameObject srcAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                                               ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponsModelDir}/{spec.Name}.obj");
                            if (srcAsset == null) continue;

                            GameObject inst = PrefabUtility.InstantiatePrefab(srcAsset, holder) as GameObject;
                            if (inst == null) inst = UnityEngine.Object.Instantiate(srcAsset, holder);

                            inst.name = spec.Name;
                            inst.transform.localPosition = spec.LocalPos;
                            inst.transform.localRotation = Quaternion.Euler(spec.LocalEuler);
                            inst.transform.localScale = Vector3.one * spec.UniformScale;
                            inst.SetActive(false);
                            addedToRig++;
                        }

                        if (addedToRig > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, MasterWeaponRigPrefab);
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            if (createdPrefabs > 0 || addedToRig > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#50FA7B>[WeaponRigPopulator]</color> Created/updated {createdPrefabs} weapon prefabs and added {addedToRig} missing weapons to {MasterWeaponRigPrefab}!");
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Stylized Weapons Installed",
                    $"ติดตั้งอาวุธครบทั้ง 18 ชิ้น (+ Grenade) เรียบร้อยแล้ว!\n" +
                    $"- Weapon Prefabs สร้างใหม่: {createdPrefabs}\n" +
                    $"- เพิ่มเข้า Malee/Range ใน Player Rig: {addedToRig}",
                    "OK"
                );
            }
        }

        private static bool EnsureWeaponPrefab(string weaponName, Vector3 pos, Vector3 euler, float scale, bool force)
        {
            string objPath = $"{WeaponsModelDir}/{weaponName}.obj";
            string prefabPath = $"{WeaponsPrefabDir}/{weaponName}.prefab";
            if (!File.Exists(objPath)) return false;

            if (File.Exists(prefabPath) && !force) return false;

            GameObject objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (objAsset == null)
            {
                AssetDatabase.ImportAsset(objPath, ImportAssetOptions.ForceSynchronousImport);
                objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            }
            if (objAsset == null) return false;

            if (File.Exists(prefabPath))
            {
                // Preserve existing transform if prefab already existed (like Weapon_Pistol / Weapon_Knife / Weapon_SMG)
                return false;
            }

            GameObject inst = UnityEngine.Object.Instantiate(objAsset);
            inst.name = weaponName;
            inst.transform.localPosition = pos;
            inst.transform.localRotation = Quaternion.Euler(euler);
            inst.transform.localScale = Vector3.one * scale;
            try
            {
                PrefabUtility.SaveAsPrefabAsset(inst, prefabPath);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(inst);
            }
        }

        private static Transform FindDeepChild(Transform parent, string targetName)
        {
            if (parent == null) return null;
            foreach (Transform child in parent)
            {
                if (child.name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    return child;
                Transform found = FindDeepChild(child, targetName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif

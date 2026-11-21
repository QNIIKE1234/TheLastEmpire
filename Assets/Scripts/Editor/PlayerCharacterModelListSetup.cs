#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Auto-populates the Character Models list on PlayerPrefabs (supports both open Prefab Stage
    /// and closed prefab asset on disk) so all playable CharacterData entries appear immediately in Inspector.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerCharacterModelListSetup
    {
        private const string PlayerPrefabPath = "Assets/Resources_Addressable/Prefabs/Character/PlayerPrefabs.prefab";
        private const string CharacterDataDir = "Assets/Resources_Addressable/Database/Characters";

        static PlayerCharacterModelListSetup()
        {
            EditorApplication.delayCall += EnsurePopulatedAuto;
        }

        private static void EnsurePopulatedAuto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            PopulatePlayerCharacterModels(silentIfAlreadyPopulated: true);
        }

        [MenuItem("Tools/The Last Empire/Auto-Populate Player Character Models List")]
        public static void PopulateFromMenu()
        {
            PopulatePlayerCharacterModels(silentIfAlreadyPopulated: false);
        }

        public static void PopulatePlayerCharacterModels(bool silentIfAlreadyPopulated)
        {
            // 1. If PlayerPrefabs is currently open in Prefab Mode, edit the LIVE stage directly!
            PrefabStage openStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (openStage != null && openStage.assetPath.Equals(PlayerPrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                GameObject stageRoot = openStage.prefabContentsRoot;
                PlayerController stagePc = stageRoot != null ? stageRoot.GetComponent<PlayerController>() : null;
                if (stagePc != null)
                {
                    if (silentIfAlreadyPopulated && stagePc.CharacterModels != null && stagePc.CharacterModels.Count > 0)
                    {
                        return;
                    }

                    Undo.RecordObject(stagePc, "Auto-Populate Character Models");
                    int added = PopulateController(stageRoot, stagePc);
                    EditorUtility.SetDirty(stagePc);
                    EditorSceneManager.MarkSceneDirty(openStage.scene);

                    Debug.Log($"<color=#50FA7B>[PlayerCharacterModelListSetup]</color> Populated {added} Character Models directly in open Prefab Mode!");
                    if (!silentIfAlreadyPopulated)
                    {
                        EditorUtility.DisplayDialog("Character Models Populated",
                            $"เพิ่มรายการ Character Models ครบแล้ว ({stagePc.CharacterModels.Count} ตัวละคร)!\nสามารถลากสลับ Model Object ของแต่ละตัวใน Inspector ได้เลย", "OK");
                    }
                    return;
                }
            }

            // 2. Otherwise edit the prefab asset on disk
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefabAsset == null) return;

            PlayerController existingPc = prefabAsset.GetComponent<PlayerController>();
            if (existingPc == null) return;

            if (silentIfAlreadyPopulated && existingPc.CharacterModels != null && existingPc.CharacterModels.Count > 0)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (root == null) return;

            try
            {
                PlayerController pc = root.GetComponent<PlayerController>();
                if (pc == null) return;

                int added = PopulateController(root, pc);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log($"<color=#50FA7B>[PlayerCharacterModelListSetup]</color> Populated {added} entries in PlayerPrefabs.prefab!");
                if (!silentIfAlreadyPopulated)
                {
                    EditorUtility.DisplayDialog("Character Models Populated",
                        $"เพิ่มรายการ Character Models ลงใน PlayerPrefabs.prefab ครบแล้ว ({pc.CharacterModels.Count} ตัวละคร)!", "OK");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int PopulateController(GameObject root, PlayerController pc)
        {
            SerializedObject so = new SerializedObject(pc);
            SerializedProperty rootProp = so.FindProperty("characterModelRoot");
            SerializedProperty listProp = so.FindProperty("characterModels");
            if (listProp == null) return 0;

            Transform modelRoot = root.transform.Find("Model");
            if (rootProp != null && modelRoot != null)
            {
                rootProp.objectReferenceValue = modelRoot;
            }

            string[] guids = AssetDatabase.FindAssets("t:CharacterData", new[] { CharacterDataDir });
            if (guids == null || guids.Length == 0)
            {
                guids = AssetDatabase.FindAssets("t:CharacterData");
            }
            Array.Sort(guids, (a, b) => string.Compare(
                AssetDatabase.GUIDToAssetPath(a),
                AssetDatabase.GUIDToAssetPath(b),
                StringComparison.OrdinalIgnoreCase));

            HashSet<string> existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < listProp.arraySize; i++)
            {
                SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
                CharacterData cd = elem.FindPropertyRelative("characterData").objectReferenceValue as CharacterData;
                string cid = elem.FindPropertyRelative("characterId").stringValue;
                if (cd != null && !string.IsNullOrEmpty(cd.characterId)) existingIds.Add(cd.characterId);
                if (!string.IsNullOrEmpty(cid)) existingIds.Add(cid);
            }

            int added = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                CharacterData cd = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
                if (cd == null || existingIds.Contains(cd.characterId)) continue;

                GameObject matchedModel = FindDefaultModelChild(modelRoot, cd);

                int idx = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(idx);
                SerializedProperty elem = listProp.GetArrayElementAtIndex(idx);
                elem.FindPropertyRelative("characterData").objectReferenceValue = cd;
                elem.FindPropertyRelative("characterId").stringValue = cd.characterId;
                elem.FindPropertyRelative("modelObject").objectReferenceValue = matchedModel;
                existingIds.Add(cd.characterId);
                added++;
            }

            so.ApplyModifiedProperties();
            return added;
        }

        private static GameObject FindDefaultModelChild(Transform modelRoot, CharacterData cd)
        {
            if (cd == null) return null;
            if (modelRoot != null)
            {
                // 1. Prefer exact match with CharacterData.characterModelPrefab name under Model root
                if (cd.characterModelPrefab != null)
                {
                    string prefabName = cd.characterModelPrefab.name;
                    for (int i = 0; i < modelRoot.childCount; i++)
                    {
                        Transform ch = modelRoot.GetChild(i);
                        if (ch != null && ch.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase))
                        {
                            return ch.gameObject;
                        }
                    }
                }

                string hint = (cd.characterId ?? "").ToUpperInvariant() switch
                {
                    "VANGUARD" => "TacticalGirl",
                    "JUGGERNAUT" => "HeavyStriker",
                    "PHANTOM" => "BlueOperative",
                    "MARKSMAN" => "CurlyGirl",
                    "REBEL" => "PunkGirl",
                    "SCAVENGER" => "WhiteRonin",
                    "DEMOLITIONIST" => "DreadBrawler",
                    _ => null
                };
                if (!string.IsNullOrEmpty(hint))
                {
                    for (int i = 0; i < modelRoot.childCount; i++)
                    {
                        Transform ch = modelRoot.GetChild(i);
                        if (ch != null && ch.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return ch.gameObject;
                        }
                    }
                }
            }
            return cd.characterModelPrefab;
        }
    }
}
#endif

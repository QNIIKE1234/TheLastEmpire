#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// เครื่องมือช่วยตรวจสอบและผูก Serialized References สำหรับ Craft Thumbnail Selection และ Description Panel อัตโนมัติ
    /// </summary>
    public static class CraftDescriptionSetupEditor
    {
        private const string PrefabPath = "Assets/Resources_Addressable/Prefabs/UI/ItemCraftThumbnail.prefab";

        [MenuItem("Tools/The Last Empire/Setup Craft Description Panel & Selection")]
        public static void SetupCraftDescriptionAndSelection()
        {
            SetupItemCraftThumbnailPrefab();
            SetupGameplaySceneDescriptionPanel();
            EditorUtility.DisplayDialog("Craft Setup", "ผูก Serialized References สำหรับ Craft Thumbnail (Selected) และ Description Panel สำเร็จเรียบร้อย!", "ตกลง");
        }

        public static void SetupItemCraftThumbnailPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CraftDescriptionSetupEditor] ไม่พบ Prefab ที่ {PrefabPath}");
                return;
            }

            CraftThumbnail thumb = prefab.GetComponent<CraftThumbnail>();
            if (thumb == null)
            {
                thumb = prefab.AddComponent<CraftThumbnail>();
            }

            SerializedObject so = new SerializedObject(thumb);
            so.Update();

            // 1. ผูก Selected GameObject
            Transform selectedTrans = prefab.transform.Find("Selected");
            if (selectedTrans != null)
            {
                SerializedProperty selProp = so.FindProperty("selectedObject");
                if (selProp != null)
                {
                    selProp.objectReferenceValue = selectedTrans.gameObject;
                }
            }

            // 2. ผูก rowSelectButton (Button บน Root)
            Button rootBtn = prefab.GetComponent<Button>();
            if (rootBtn != null)
            {
                SerializedProperty btnProp = so.FindProperty("rowSelectButton");
                if (btnProp != null)
                {
                    btnProp.objectReferenceValue = rootBtn;
                }
            }

            // 3. ผูก craftHoldFillImage (Button/Fill)
            Transform fillTrans = prefab.transform.Find("Button/Fill");
            if (fillTrans != null)
            {
                Image fillImg = fillTrans.GetComponent<Image>();
                if (fillImg != null)
                {
                    SerializedProperty fillProp = so.FindProperty("craftHoldFillImage");
                    if (fillProp != null)
                    {
                        fillProp.objectReferenceValue = fillImg;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssets();

            Debug.Log("[CraftDescriptionSetupEditor] ตั้งค่า Serialized References ใน ItemCraftThumbnail.prefab เรียบร้อย");
        }

        public static void SetupGameplaySceneDescriptionPanel()
        {
            CraftController controller = UnityEngine.Object.FindFirstObjectByType<CraftController>();
            if (controller == null)
            {
                Debug.Log("[CraftDescriptionSetupEditor] ไม่พบ CraftController ใน Scene ปัจจุบัน (อาจต้องเปิด Gameplay_3D ก่อน)");
                return;
            }

            SerializedObject soController = new SerializedObject(controller);
            soController.Update();

            SerializedProperty craftPanelProp = soController.FindProperty("craftPanel");
            GameObject panelObj = craftPanelProp != null ? craftPanelProp.objectReferenceValue as GameObject : null;

            if (panelObj != null)
            {
                Transform descTrans = panelObj.transform.Find("Description");
                if (descTrans == null)
                {
                    // ค้นหาในชั้นลูก
                    CraftDescriptionPanel existing = panelObj.GetComponentInChildren<CraftDescriptionPanel>(true);
                    if (existing != null)
                    {
                        descTrans = existing.transform;
                    }
                }

                if (descTrans != null)
                {
                    CraftDescriptionPanel descPanel = descTrans.GetComponent<CraftDescriptionPanel>();
                    if (descPanel == null)
                    {
                        descPanel = descTrans.gameObject.AddComponent<CraftDescriptionPanel>();
                    }

                    SerializedObject soDesc = new SerializedObject(descPanel);
                    soDesc.Update();

                    SerializedProperty rootProp = soDesc.FindProperty("panelRoot");
                    if (rootProp != null) rootProp.objectReferenceValue = descTrans.gameObject;

                    // Image Icon
                    SerializedProperty iconProp = soDesc.FindProperty("itemIconImage");
                    Image[] images = descTrans.GetComponentsInChildren<Image>(true);
                    foreach (Image img in images)
                    {
                        if (img.gameObject != descTrans.gameObject && 
                            (img.name.IndexOf("Icon", StringComparison.OrdinalIgnoreCase) >= 0 || img.name.Equals("Image", StringComparison.OrdinalIgnoreCase)))
                        {
                            if (iconProp != null) iconProp.objectReferenceValue = img;
                            break;
                        }
                    }

                    // Texts
                    SerializedProperty nameProp = soDesc.FindProperty("itemNameText");
                    SerializedProperty descTextProp = soDesc.FindProperty("itemDescriptionText");
                    TMP_Text[] texts = descTrans.GetComponentsInChildren<TMP_Text>(true);
                    foreach (TMP_Text txt in texts)
                    {
                        string n = txt.gameObject.name;
                        if (nameProp != null && nameProp.objectReferenceValue == null && 
                            (n.Equals("Name", StringComparison.OrdinalIgnoreCase) || n.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            nameProp.objectReferenceValue = txt;
                        }
                        else if (descTextProp != null && descTextProp.objectReferenceValue == null && 
                                 (n.IndexOf("Desc", StringComparison.OrdinalIgnoreCase) >= 0 || n.Equals("Name (1)", StringComparison.OrdinalIgnoreCase)))
                        {
                            descTextProp.objectReferenceValue = txt;
                        }
                    }

                    soDesc.ApplyModifiedProperties();
                    EditorUtility.SetDirty(descPanel);

                    // ผูก descriptionPanel ใน CraftController
                    SerializedProperty descPanelProp = soController.FindProperty("descriptionPanel");
                    if (descPanelProp != null)
                    {
                        descPanelProp.objectReferenceValue = descPanel;
                    }
                }
            }

            soController.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            }

            Debug.Log("[CraftDescriptionSetupEditor] ตั้งค่า CraftDescriptionPanel และผูกกับ CraftController ใน Scene เรียบร้อย");
        }
    }
}
#endif

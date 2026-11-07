using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// QuickSlotBaker — Editor tool สำหรับตั้งค่า QuickSlot_HUD และเชื่อมต่อ QuickSlotController
    /// </summary>
    public static class QuickSlotBaker
    {
        [MenuItem("Tools/The Last Empire/Setup QuickSlot HUD Components", false, 110)]
        public static void SetupQuickSlotHUD()
        {
            GameObject hud = GameObject.Find("QuickSlot_HUD");
            if (hud == null)
            {
                EditorUtility.DisplayDialog("QuickSlot Setup", "ไม่พบ GameObject ชื่อ 'QuickSlot_HUD' ใน Scene ปัจจุบัน!", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(hud, "Setup QuickSlot HUD");

            // 1. ตรวจสอบ QuickSlotController บน hud
            QuickSlotController controller = hud.GetComponent<QuickSlotController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<QuickSlotController>(hud);
            }

            // 2. หา Panel และช่อง QuickSlot1 .. QuickSlot5
            Transform panelT = hud.transform.Find("Panel");
            Transform searchRoot = panelT != null ? panelT : hud.transform;

            SerializedObject so = new SerializedObject(controller);
            SerializedProperty hudProp = so.FindProperty("quickSlotHUD");
            SerializedProperty slotsProp = so.FindProperty("slotItems");

            if (hudProp != null) hudProp.objectReferenceValue = hud;
            if (slotsProp != null) slotsProp.ClearArray();

            for (int i = 0; i < QuickSlotController.MaxSlots; i++)
            {
                string slotName = $"QuickSlot{i + 1}";
                Transform childT = searchRoot.Find(slotName);
                if (childT == null && i < searchRoot.childCount)
                {
                    childT = searchRoot.GetChild(i);
                }

                if (childT != null)
                {
                    QuickSlotUIItem uiItem = childT.GetComponent<QuickSlotUIItem>();
                    if (uiItem == null)
                    {
                        uiItem = Undo.AddComponent<QuickSlotUIItem>(childT.gameObject);
                    }
                    uiItem.Setup(i);
                    EditorUtility.SetDirty(uiItem);

                    if (slotsProp != null)
                    {
                        slotsProp.InsertArrayElementAtIndex(i);
                        slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = uiItem;
                    }
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // 3. Setup Shiftskill HUD ด้วยถ้ามีใน Scene
            SetupShiftSkillHUDInternal(false);

            EditorSceneManager.MarkSceneDirty(hud.scene);

            EditorUtility.DisplayDialog("QuickSlot Setup", "ติดตั้ง QuickSlotController และเชื่อมต่อ QuickSlot 1 - 5 พร้อม Shift Skill HUD สำเร็จเรียบร้อย!", "OK");
            Debug.Log("[QuickSlotBaker] Successfully setup QuickSlot HUD and registered all slots!");
        }

        [MenuItem("Tools/The Last Empire/Setup Shift Skill HUD", false, 111)]
        public static void SetupShiftSkillHUDMenu()
        {
            SetupShiftSkillHUDInternal(true);
        }

        private static void SetupShiftSkillHUDInternal(bool showDialog)
        {
            GameObject skillObj = GameObject.Find("Shiftskill");
            if (skillObj == null)
            {
                if (showDialog)
                    EditorUtility.DisplayDialog("Shift Skill Setup", "ไม่พบ GameObject ชื่อ 'Shiftskill' ใน Scene ปัจจุบัน!", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(skillObj, "Setup Shift Skill HUD");

            ShiftSkillHUD hud = skillObj.GetComponent<ShiftSkillHUD>();
            if (hud == null)
            {
                hud = Undo.AddComponent<ShiftSkillHUD>(skillObj);
            }
            hud.ResolveReferences();
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(skillObj.scene);

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Shift Skill Setup", "ติดตั้ง ShiftSkillHUD และตั้งค่า Cooldown Image / Text สำเร็จเรียบร้อย!", "OK");
            }
            Debug.Log("[QuickSlotBaker] Successfully setup ShiftSkillHUD on Shiftskill object!");
        }

        [MenuItem("Tools/The Last Empire/Setup Projectile Visual Mappings", false, 112)]
        public static void SetupProjectileVisualMappings()
        {
            // 1. Setup ThrowingWeaponPrefabs
            string throwingPath = "Assets/Resources_Addressable/Prefabs/Item/ThrowingWeaponPrefabs.prefab";
            GameObject throwingObj = AssetDatabase.LoadAssetAtPath<GameObject>(throwingPath);
            if (throwingObj != null)
            {
                Projectile proj = throwingObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    SerializedObject so = new SerializedObject(proj);
                    SerializedProperty entriesProp = so.FindProperty("visualEntries");
                    if (entriesProp != null)
                    {
                        entriesProp.ClearArray();

                        Transform grenade1 = throwingObj.transform.Find("ITEM0008_Grenade");
                        if (grenade1 != null)
                        {
                            int idx = entriesProp.arraySize++;
                            SerializedProperty elem = entriesProp.GetArrayElementAtIndex(idx);
                            elem.FindPropertyRelative("id").stringValue = "Grenade";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = grenade1.gameObject;
                        }

                        Transform grenade2 = throwingObj.transform.Find("Weapon_Grenade");
                        if (grenade2 != null)
                        {
                            int idx = entriesProp.arraySize++;
                            SerializedProperty elem = entriesProp.GetArrayElementAtIndex(idx);
                            elem.FindPropertyRelative("id").stringValue = "Grenade";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = grenade2.gameObject;
                        }

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(throwingObj);
                        Debug.Log("[QuickSlotBaker] Successfully mapped ThrowingWeaponPrefabs visuals!");
                    }
                }
            }

            // 2. Setup BulletPrefabs / PostalBulletPrefabs
            string bulletPath = "Assets/Resources_Addressable/Prefabs/Item/BulletPrefabs.prefab";
            GameObject bulletObj = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);
            if (bulletObj == null)
            {
                bulletPath = "Assets/Resources_Addressable/Prefabs/Item/PostalBulletPrefabs.prefab";
                bulletObj = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);
            }
            if (bulletObj != null)
            {
                Projectile proj = bulletObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    SerializedObject so = new SerializedObject(proj);
                    SerializedProperty entriesProp = so.FindProperty("visualEntries");
                    if (entriesProp != null)
                    {
                        entriesProp.ClearArray();

                        Transform bulletChild = bulletObj.transform.Find("Bullet/ITEM0009_Bullet");
                        if (bulletChild == null) bulletChild = bulletObj.transform.Find("ITEM0009_Bullet");
                        if (bulletChild != null)
                        {
                            int idx = entriesProp.arraySize++;
                            SerializedProperty elem = entriesProp.GetArrayElementAtIndex(idx);
                            elem.FindPropertyRelative("id").stringValue = "Bullet";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = bulletChild.gameObject;
                        }

                        Transform arrowChild = bulletObj.transform.Find("Bullet/ITEM0010_Arrow");
                        if (arrowChild == null) arrowChild = bulletObj.transform.Find("ITEM0010_Arrow");
                        if (arrowChild != null)
                        {
                            int idx = entriesProp.arraySize++;
                            SerializedProperty elem = entriesProp.GetArrayElementAtIndex(idx);
                            elem.FindPropertyRelative("id").stringValue = "Arrow";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = arrowChild.gameObject;
                        }

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(bulletObj);
                        Debug.Log("[QuickSlotBaker] Successfully mapped PostalBulletPrefabs visuals!");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Projectile Visual Setup", "ตั้งค่าโมเดล Visual สำหรับ ThrowingWeaponPrefabs และ PostalBulletPrefabs เรียบร้อย!", "OK");
        }
    }
}


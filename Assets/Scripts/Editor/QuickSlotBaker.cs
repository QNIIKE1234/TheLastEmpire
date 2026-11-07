using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// QuickSlotBaker — Editor tool สำหรับตั้งค่า QuickSlot_HUD, เชื่อมต่อ Serialized References ให้ระบบทั้งหมด
    /// (QuickSlotController, PlayerController, InventoryUI, ShiftSkillHUD, PlayerHUD)
    /// โดยไม่มีการเรียก FindObjectsInactive หรือ FindFirstObjectByType ในขณะรันไทม์
    /// </summary>
    public static class QuickSlotBaker
    {
        [MenuItem("Tools/The Last Empire/Setup QuickSlot and HUD References", false, 110)]
        public static void SetupAllReferences()
        {
            // ค้นหา PlayerController และ PlayerInventory ใน Scene (Editor only)
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            PlayerInventory inventory = player != null ? player.GetComponent<PlayerInventory>() : Object.FindFirstObjectByType<PlayerInventory>();

            // 1. Setup QuickSlot_HUD
            GameObject hud = GameObject.Find("QuickSlot_HUD");
            QuickSlotController qsController = null;
            if (hud != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(hud, "Setup QuickSlot HUD");

                qsController = hud.GetComponent<QuickSlotController>();
                if (qsController == null)
                {
                    qsController = Undo.AddComponent<QuickSlotController>(hud);
                }

                Transform panelT = hud.transform.Find("Panel");
                Transform searchRoot = panelT != null ? panelT : hud.transform;

                SerializedObject so = new SerializedObject(qsController);
                SerializedProperty hudProp = so.FindProperty("quickSlotHUD");
                SerializedProperty slotsProp = so.FindProperty("slotItems");
                SerializedProperty playerProp = so.FindProperty("player");
                SerializedProperty invProp = so.FindProperty("inventory");

                if (hudProp != null) hudProp.objectReferenceValue = hud;
                if (playerProp != null && player != null) playerProp.objectReferenceValue = player;
                if (invProp != null && inventory != null) invProp.objectReferenceValue = inventory;

                if (slotsProp != null)
                {
                    slotsProp.ClearArray();
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

                            slotsProp.InsertArrayElementAtIndex(i);
                            slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = uiItem;
                        }
                    }
                }

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(qsController);
                Debug.Log("[QuickSlotBaker] Wired QuickSlotController serialized references.");
            }

            // 2. Wire PlayerController -> QuickSlotController
            if (player != null && qsController != null)
            {
                SerializedObject playerSO = new SerializedObject(player);
                SerializedProperty qsProp = playerSO.FindProperty("quickSlotController");
                if (qsProp != null)
                {
                    qsProp.objectReferenceValue = qsController;
                    playerSO.ApplyModifiedProperties();
                    EditorUtility.SetDirty(player);
                    Debug.Log("[QuickSlotBaker] Wired PlayerController -> QuickSlotController serialized reference.");
                }
            }

            // 3. Wire InventoryUI -> QuickSlotController, Player, Inventory
            InventoryUI invUI = Object.FindFirstObjectByType<InventoryUI>();
            if (invUI != null)
            {
                SerializedObject invSO = new SerializedObject(invUI);
                SerializedProperty qsProp = invSO.FindProperty("quickSlotController");
                SerializedProperty playerProp = invSO.FindProperty("player");
                SerializedProperty invProp = invSO.FindProperty("playerInventory");

                if (qsProp != null && qsController != null) qsProp.objectReferenceValue = qsController;
                if (playerProp != null && player != null) playerProp.objectReferenceValue = player;
                if (invProp != null && inventory != null) invProp.objectReferenceValue = inventory;

                invSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(invUI);
                Debug.Log("[QuickSlotBaker] Wired InventoryUI serialized references.");
            }

            // 4. Setup ShiftSkillHUD
            SetupShiftSkillHUDInternal(false, player);

            // 5. Setup Weapon_HUD and PlayerHUD
            SetupWeaponHUDAndPlayerHUDInternal(player);

            // 6. Setup Projectiles
            SetupProjectileVisualMappingsInternal();

            if (hud != null)
            {
                EditorSceneManager.MarkSceneDirty(hud.scene);
            }

            EditorUtility.DisplayDialog("HUD & QuickSlot Setup", "เชื่อมต่อ Serialized References ครบถ้วนทั้งโปรเจกต์ (QuickSlot, Player, Inventory, ShiftSkill, WeaponHUD) สำเร็จเรียบร้อย ไม่มีการใช้ runtime FindObject!", "OK");
            Debug.Log("[QuickSlotBaker] Completed all serialized reference wiring!");
        }

        [MenuItem("Tools/The Last Empire/Setup Shift Skill HUD", false, 111)]
        public static void SetupShiftSkillHUDMenu()
        {
            PlayerController player = Object.FindFirstObjectByType<PlayerController>();
            SetupShiftSkillHUDInternal(true, player);
        }

        private static void SetupShiftSkillHUDInternal(bool showDialog, PlayerController player = null)
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

            if (player == null)
            {
                player = Object.FindFirstObjectByType<PlayerController>();
            }

            SerializedObject so = new SerializedObject(hud);
            SerializedProperty playerProp = so.FindProperty("player");
            if (playerProp != null && player != null)
            {
                playerProp.objectReferenceValue = player;
            }
            so.ApplyModifiedProperties();

            hud.ResolveReferences();
            EditorUtility.SetDirty(hud);
            EditorSceneManager.MarkSceneDirty(skillObj.scene);

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Shift Skill Setup", "ติดตั้ง ShiftSkillHUD และตั้งค่า Cooldown Image / Text สำเร็จเรียบร้อย!", "OK");
            }
            Debug.Log("[QuickSlotBaker] Successfully setup ShiftSkillHUD on Shiftskill object!");
        }

        private static void SetupWeaponHUDAndPlayerHUDInternal(PlayerController player)
        {
            // ค้นหา Weapon_HUD
            GameObject weaponHUD = GameObject.Find("Weapon_HUD");
            if (weaponHUD != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(weaponHUD, "Format Weapon_HUD");

                Transform weaponT = weaponHUD.transform.Find("Weapon");
                if (weaponT != null)
                {
                    RectTransform rt = weaponT.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.anchorMin = new Vector2(0.5f, 0.5f);
                        rt.anchorMax = new Vector2(0.5f, 0.5f);
                        rt.pivot = new Vector2(0.5f, 0.5f);
                        rt.anchoredPosition = new Vector2(-85f, 0f);
                        rt.sizeDelta = new Vector2(75f, 75f);
                    }

                    Image img = weaponT.GetComponent<Image>();
                    if (img != null)
                    {
                        img.preserveAspect = true;
                    }
                    EditorUtility.SetDirty(weaponT.gameObject);
                }
            }

            // ค้นหา PlayerHUD
            PlayerHUD pHUD = Object.FindFirstObjectByType<PlayerHUD>();
            if (pHUD != null && player != null)
            {
                SerializedObject pSo = new SerializedObject(pHUD);
                SerializedProperty pProp = pSo.FindProperty("player");
                if (pProp != null)
                {
                    pProp.objectReferenceValue = player;
                }

                if (weaponHUD != null)
                {
                    Transform weaponT = weaponHUD.transform.Find("Weapon");
                    Transform ammoT = weaponHUD.transform.Find("Ammo");
                    Transform ammoTextT = weaponHUD.transform.Find("Ammo_text");

                    SerializedProperty wpnImgProp = pSo.FindProperty("weaponImage");
                    SerializedProperty ammoImgProp = pSo.FindProperty("ammoImage");
                    SerializedProperty ammoTxtProp = pSo.FindProperty("ammoTextHUD");

                    if (wpnImgProp != null && weaponT != null) wpnImgProp.objectReferenceValue = weaponT.GetComponent<Image>();
                    if (ammoImgProp != null && ammoT != null) ammoImgProp.objectReferenceValue = ammoT.GetComponent<Image>();
                    if (ammoTxtProp != null && ammoTextT != null) ammoTxtProp.objectReferenceValue = ammoTextT.GetComponent<TMPro.TMP_Text>();
                }

                pSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(pHUD);
                Debug.Log("[QuickSlotBaker] Wired PlayerHUD serialized references.");
            }
        }

        [MenuItem("Tools/The Last Empire/Setup Projectile Visual Mappings", false, 112)]
        public static void SetupProjectileVisualMappings()
        {
            SetupProjectileVisualMappingsInternal();
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Projectile Visual Setup", "ตั้งค่าโมเดล Visual สำหรับ ThrowingWeaponPrefabs และ BulletPrefabs เรียบร้อย!", "OK");
        }

        private static void SetupProjectileVisualMappingsInternal()
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
                    SerializedProperty lifetimeProp = so.FindProperty("lifetime");
                    if (lifetimeProp != null) lifetimeProp.floatValue = 3.0f;

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
                            elem.FindPropertyRelative("id").stringValue = "Weapon_Grenade";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = grenade2.gameObject;
                        }

                        if (grenade1 != null)
                        {
                            int idx = entriesProp.arraySize++;
                            SerializedProperty elem = entriesProp.GetArrayElementAtIndex(idx);
                            elem.FindPropertyRelative("id").stringValue = "ITEM0008_Grenade";
                            elem.FindPropertyRelative("visualObject").objectReferenceValue = grenade1.gameObject;
                        }
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(throwingObj);
                    Debug.Log("[QuickSlotBaker] Successfully mapped ThrowingWeaponPrefabs visuals!");
                }
            }

            // 2. Setup BulletPrefabs
            string bulletPath = "Assets/Resources_Addressable/Prefabs/Item/BulletPrefabs.prefab";
            GameObject bulletObj = AssetDatabase.LoadAssetAtPath<GameObject>(bulletPath);
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
                    }

                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(bulletObj);
                    Debug.Log("[QuickSlotBaker] Successfully mapped BulletPrefabs visuals!");
                }
            }
        }
    }
}

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
    }
}


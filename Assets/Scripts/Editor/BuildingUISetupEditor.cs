#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// BuildingUISetupEditor — สร้าง GameObject สำหรับ UI ของแต่ละ Building ภายใต้ BuildingUI_Canvas อัตโนมัติ
    /// รายการ HUD:
    /// - Storage_HUD (กล่องเก็บของ)
    /// - Workbench_HUD (โต๊ะคราฟต์)
    /// - Campfire_HUD (กองไฟทำอาหาร/พักผ่อน)
    /// - WaterPurifier_HUD (เครื่องกรองน้ำสะอาด)
    /// - Bedroll_HUD (ที่นอน/ข้ามคืน)
    /// - Generator_HUD (เครื่องกำเนิดไฟฟ้า)
    /// - Turret_HUD (ป้อมปืนกลป้องกันฐาน)
    /// - ShockTower_HUD (หอคอยสายฟ้าผลึกเอเลี่ยน)
    /// </summary>
    [InitializeOnLoad]
    public static class BuildingUISetupEditor
    {
        public static readonly string[] HudNames = new string[]
        {
            "Build_HUD",
            "Storage_HUD",
            "Workbench_HUD",
            "Campfire_HUD",
            "WaterPurifier_HUD",
            "Bedroll_HUD",
            "Generator_HUD",
            "Turret_HUD",
            "ShockTower_HUD"
        };

        [MenuItem("TheLastEmpire/Setup Building UI HUDs")]
        public static void SetupBuildingHUDs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SetupHUDsInActiveScene();
            GameObject managerGo = GameObject.Find("BuildingManager");
            if (managerGo != null)
            {
                Selection.activeGameObject = managerGo;
                EditorGUIUtility.PingObject(managerGo);
            }
            else
            {
                GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
                if (canvasGo != null)
                {
                    Selection.activeGameObject = canvasGo;
                    EditorGUIUtility.PingObject(canvasGo);
                }
            }
        }

        public static void SetupHUDsInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.name != "Gameplay_3D")
            {
                string scenePath = "Assets/Scenes/Gameplay_3D.unity";
                if (System.IO.File.Exists(scenePath))
                {
                    currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }

            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo == null)
            {
                Debug.LogWarning("[BuildingUISetup] 'BuildingUI_Canvas' not found in current scene!");
                return;
            }

            bool modified = false;
            foreach (string hudName in HudNames)
            {
                Transform existing = canvasGo.transform.Find(hudName);
                if (existing == null)
                {
                    GameObject hudGo = new GameObject(hudName);
                    hudGo.layer = 5; // UI layer
                    RectTransform rt = hudGo.AddComponent<RectTransform>();
                    rt.SetParent(canvasGo.transform, false);

                    // ยืดเต็มขอบจอ Canvas (Full Screen Anchor)
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;

                    hudGo.AddComponent<CanvasRenderer>();
                    hudGo.AddComponent<CanvasGroup>();

                    // ซ่อนไว้เป็นค่าเริ่มต้น (Inactive) เพื่อให้ผู้เล่น/ผู้ใช้เข้าไปตกแต่งเองได้สะดวก
                    hudGo.SetActive(false);

                    modified = true;
                    Debug.Log($"[BuildingUISetup] Created '{hudName}' under BuildingUI_Canvas.");
                }
            }

            if (modified)
            {
                EditorSceneManager.MarkSceneDirty(currentScene);
                EditorSceneManager.SaveScene(currentScene);
                Debug.Log("[BuildingUISetup] Successfully saved Gameplay_3D scene with all Building UI HUDs!");
            }
            else
            {
                Debug.Log("[BuildingUISetup] All Building UI HUDs already exist under BuildingUI_Canvas.");
            }

            // 2. ตรวจสอบให้แน่ใจว่ามี GameObject ชื่อ BuildingManager อยู่ในฉากสำหรับดูและปรับแต่งใน Inspector
            GameObject managerGo = GameObject.Find("BuildingManager");
            if (managerGo == null)
            {
                managerGo = new GameObject("BuildingManager");
                managerGo.AddComponent<BuildingManager>();
                managerGo.AddComponent<BuildingPlacer>();
                EditorSceneManager.MarkSceneDirty(currentScene);
                EditorSceneManager.SaveScene(currentScene);
                Debug.Log("[BuildingUISetup] Created 'BuildingManager' in scene with BuildingManager and BuildingPlacer components.");
            }
            else
            {
                bool compAdded = false;
                if (managerGo.GetComponent<BuildingManager>() == null)
                {
                    managerGo.AddComponent<BuildingManager>();
                    compAdded = true;
                }
                if (managerGo.GetComponent<BuildingPlacer>() == null)
                {
                    managerGo.AddComponent<BuildingPlacer>();
                    compAdded = true;
                }
                if (compAdded)
                {
                    EditorSceneManager.MarkSceneDirty(currentScene);
                    EditorSceneManager.SaveScene(currentScene);
                }
            }

            BuildingPlacer placer = managerGo.GetComponent<BuildingPlacer>();
            if (placer != null && placer.BuildPromptHUD == null)
            {
                Transform buildHud = canvasGo.transform.Find("Build_HUD");
                if (buildHud != null)
                {
                    placer.BuildPromptHUD = buildHud.gameObject;
                    EditorUtility.SetDirty(placer);
                    EditorSceneManager.MarkSceneDirty(currentScene);
                    EditorSceneManager.SaveScene(currentScene);
                }
            }
        }
    }
}
#endif


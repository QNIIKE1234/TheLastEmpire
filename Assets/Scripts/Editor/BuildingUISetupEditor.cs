#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// BuildingUISetupEditor — เครื่องมือช่วยสร้าง GameObject สำหรับ UI ของแต่ละ Building ภายใต้ BuildingUI_Canvas (รันด้วยมือผ่านเมนู)
    /// </summary>
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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[BuildingUISetup] Cannot setup HUDs while in play mode!");
                return;
            }
            SetupHUDsInActiveScene();
        }

        public static void SetupHUDsInActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo == null)
            {
                Debug.LogWarning("[BuildingUISetup] 'BuildingUI_Canvas' not found in current scene!");
                return;
            }

            Scene currentScene = canvasGo.scene;
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

                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;

                    hudGo.AddComponent<CanvasRenderer>();
                    hudGo.AddComponent<CanvasGroup>();
                    hudGo.SetActive(false);

                    modified = true;
                    Debug.Log($"[BuildingUISetup] Created '{hudName}' under BuildingUI_Canvas.");
                }
            }

            if (modified && !Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(currentScene);
                EditorSceneManager.SaveScene(currentScene);
                Debug.Log("[BuildingUISetup] Successfully saved Gameplay_3D scene with all Building UI HUDs!");
            }
        }
    }
}
#endif


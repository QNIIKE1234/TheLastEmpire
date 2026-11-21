#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// MultiplayerDualClientLauncher — เครื่องมือสำหรับเปิดทดสอบ 2 Clients (Host + Client) บนคอมเครื่องเดียว
    /// - ผูกปุ่ม "🖥️ LAUNCH 2ND CLIENT WINDOW" ในหน้าต่าง [F2] Co-op Lobby UI
    /// - และมีเมนูบนแถบด้านบนของ Unity Editor: Tools > Multiplayer > Launch 2nd Client Window
    /// </summary>
    [InitializeOnLoad]
    public static class MultiplayerDualClientLauncher
    {
        private const string BuildRelativeDir = "Builds/CoopTestClient";
        private const string ExeName = "TheLastEmpire.exe";
        private const string PendingBuildSessionKey = "TLE_PendingDualClientBuildAndLaunch";

        static MultiplayerDualClientLauncher()
        {
            MultiplayerLobbyUI.OnRequestLaunchSecondClientWindow = LaunchOrBuildSecondClientWindow;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(PendingBuildSessionKey, false))
            {
                SessionState.SetBool(PendingBuildSessionKey, false);
                EditorApplication.delayCall += () =>
                {
                    if (BuildStandaloneTestClient())
                    {
                        string exePath = GetExeFullPath();
                        SpawnClientProcess(exePath, "-coopClient 127.0.0.1:7777 -screen-fullscreen 0 -screen-width 1280 -screen-height 720");
                        EditorApplication.isPlaying = true;
                    }
                };
            }
        }

        private static string GetExeFullPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, BuildRelativeDir, ExeName);
        }

        [MenuItem("Tools/Multiplayer/1. Launch 2nd Client Window (Auto-Connect to Editor Host)", false, 10)]
        public static void LaunchOrBuildSecondClientWindow()
        {
            string exePath = GetExeFullPath();
            if (!File.Exists(exePath))
            {
                if (EditorApplication.isPlaying)
                {
                    bool confirmPlay = EditorUtility.DisplayDialog(
                        "Build 2nd Test Client (.exe)",
                        "การเปิดหน้าต่างเกมแยกจอที่ 2 ครั้งแรก ต้อง Build ไฟล์ทดสอบ (Builds/CoopTestClient/TheLastEmpire.exe) ก่อน ซึ่ง Unity ต้องออกจาก Play Mode ชั่วคราวตอน Build\n\nกด 'Auto Stop, Build & Relaunch' เพื่อให้ระบบหยุด Play Mode, สร้างไฟล์ .exe, เปิดจอที่ 2 และกลับเข้า Play Mode ให้อัตโนมัติเลยหรือไม่?",
                        "Auto Stop, Build & Relaunch",
                        "Cancel");

                    if (!confirmPlay) return;
                    SessionState.SetBool(PendingBuildSessionKey, true);
                    EditorApplication.isPlaying = false;
                    return;
                }

                bool confirm = EditorUtility.DisplayDialog(
                    "Build 2nd Test Client (.exe)",
                    "ยังไม่มีไฟล์ Build สำหรับเปิดหน้าต่าง Client ที่ 2 (Builds/CoopTestClient/TheLastEmpire.exe)\n\nต้องการให้ระบบ Build ไฟล์ทดสอบ (Windowed 1280x720) แล้วเปิดเชื่อมต่อเข้าหาเครื่อง Host อัตโนมัติเลยหรือไม่?",
                    "Build & Launch Now",
                    "Cancel");

                if (!confirm) return;
                if (!BuildStandaloneTestClient()) return;
            }

            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string launchArg = string.Equals(activeSceneName, "Lobby_NetCode", System.StringComparison.OrdinalIgnoreCase)
                ? "-coopLobby -screen-fullscreen 0 -screen-width 1280 -screen-height 720"
                : "-coopClient 127.0.0.1:7777 -screen-fullscreen 0 -screen-width 1280 -screen-height 720";
            SpawnClientProcess(exePath, launchArg);
        }

        [MenuItem("Tools/Multiplayer/2. Rebuild Test Client (.exe)", false, 11)]
        public static bool BuildStandaloneTestClient()
        {
            string exePath = GetExeFullPath();
            string dir = Path.GetDirectoryName(exePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            List<string> scenes = new List<string>();
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                {
                    scenes.Add(s.path);
                }
            }

            // หากกำลังเปิด Scene ทดสอบอยู่ (เช่น Gameplay_3D) ให้มั่นใจว่าอยู่ในรายการ Build
            string activeScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(activeScenePath) && !scenes.Contains(activeScenePath))
            {
                scenes.Insert(0, activeScenePath);
            }

            BuildPlayerOptions opts = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };

            UnityEngine.Debug.Log($"[MultiplayerDualClientLauncher] Building test client to: {exePath}...");
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log("<color=#22C55E>[MultiplayerDualClientLauncher]</color> Test Client build succeeded!");
                return true;
            }

            UnityEngine.Debug.LogError($"[MultiplayerDualClientLauncher] Build failed: {report.summary.result}");
            return false;
        }

        [MenuItem("Tools/Multiplayer/3. Launch 2 Standalone Windows (Host + Client)", false, 12)]
        public static void LaunchTwoStandaloneWindows()
        {
            string exePath = GetExeFullPath();
            if (!File.Exists(exePath))
            {
                if (!BuildStandaloneTestClient()) return;
            }

            SpawnClientProcess(exePath, "-coopHost -screen-fullscreen 0 -screen-width 1280 -screen-height 720");
            string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string launchArg = string.Equals(activeSceneName, "Lobby_NetCode", System.StringComparison.OrdinalIgnoreCase)
                ? "-coopLobby -screen-fullscreen 0 -screen-width 1280 -screen-height 720"
                : "-coopClient 127.0.0.1:7777 -screen-fullscreen 0 -screen-width 1280 -screen-height 720";
            SpawnClientProcess(exePath, launchArg);
        }

        private static void SpawnClientProcess(string exePath, string arguments)
        {
            if (!File.Exists(exePath)) return;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            };
            Process.Start(psi);
            UnityEngine.Debug.Log($"<color=#38BDF8>[MultiplayerDualClientLauncher]</color> Launched process: {exePath} {arguments}");
        }
    }
}
#endif

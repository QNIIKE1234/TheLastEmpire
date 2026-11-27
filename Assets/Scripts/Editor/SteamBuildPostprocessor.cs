using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// Ensures steam_appid.txt (default 480 = Spacewar test AppID) exists at project root
    /// and automatically copies it into Standalone builds so friends can test Steam Relay immediately.
    /// Note: Remove steam_appid.txt from the final build folder before uploading to Steamworks.
    /// </summary>
    public class SteamBuildPostprocessor : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        [MenuItem("The Last Empire/Networking/Ensure steam_appid.txt (480)")]
        public static void EnsureSteamAppIdFile()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string appIdPath = Path.Combine(projectRoot, "steam_appid.txt");
            if (!File.Exists(appIdPath))
            {
                File.WriteAllText(appIdPath, "480\n");
                Debug.Log($"[SteamBuildPostprocessor] Created {appIdPath} with test AppID 480 (Spacewar).");
            }
            else
            {
                Debug.Log($"[SteamBuildPostprocessor] steam_appid.txt already exists at {appIdPath} (AppID={File.ReadAllText(appIdPath).Trim()}).");
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows &&
                target != BuildTarget.StandaloneWindows64 &&
                target != BuildTarget.StandaloneLinux64 &&
                target != BuildTarget.StandaloneOSX)
            {
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot)) return;

            string sourceAppId = Path.Combine(projectRoot, "steam_appid.txt");
            if (!File.Exists(sourceAppId))
            {
                File.WriteAllText(sourceAppId, "480\n");
            }

            string outputDir = Path.GetDirectoryName(report.summary.outputPath);
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir)) return;

            string destAppId = Path.Combine(outputDir, "steam_appid.txt");
            File.Copy(sourceAppId, destAppId, overwrite: true);
            Debug.Log($"[SteamBuildPostprocessor] Copied steam_appid.txt to build folder: {destAppId}");
        }
    }
}

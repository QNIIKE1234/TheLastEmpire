using System;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public static class PackageManagerSecurityFixer
    {
        [MenuItem("Tools/The Last Empire/Open Package Manager Settings (Fix Signatures)")]
        public static void OpenSettings()
        {
            SettingsService.OpenProjectSettings("Project/Package Manager");
        }
    }
}

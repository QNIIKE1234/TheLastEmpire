#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    /// <summary>
    /// NetcodeAsmdefAutoLinker — ตรวจสอบอัตโนมัติว่าแพ็กเกจ Unity Netcode for GameObjects (com.unity.netcode.gameobjects)
    /// ถูกติดตั้งใน Unity Editor หรือยัง หากติดตั้งแล้วจะเชื่อม Reference "Unity.Netcode.Runtime" เข้ากับ
    /// TheLastEmpire.Runtime.asmdef ให้อัตโนมัติ แต่ถ้ายังไม่ได้ติดตั้งก็จะใช้ NetcodeCompatLayer.cs แทนโดยไม่มี Error
    /// </summary>
    [InitializeOnLoad]
    public static class NetcodeAsmdefAutoLinker
    {
        private const string AsmdefPath = "Assets/Scripts/Runtime/TheLastEmpire.Runtime.asmdef";

        static NetcodeAsmdefAutoLinker()
        {
            EditorApplication.delayCall += CheckAndLinkNetcodeAssembly;
        }

        private static void CheckAndLinkNetcodeAssembly()
        {
            if (!File.Exists(AsmdefPath)) return;

            bool hasOfficialNgo = false;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string asmName = assemblies[i].GetName().Name;
                if (string.Equals(asmName, "Unity.Netcode.Runtime", StringComparison.OrdinalIgnoreCase))
                {
                    hasOfficialNgo = true;
                    break;
                }
            }

            if (!hasOfficialNgo) return;

            string json = File.ReadAllText(AsmdefPath);
            if (!json.Contains("\"Unity.Netcode.Runtime\""))
            {
                json = json.Replace(
                    "\"Unity.RenderPipelines.Universal.Runtime\"",
                    "\"Unity.RenderPipelines.Universal.Runtime\",\n        \"Unity.Netcode.Runtime\",\n        \"Unity.Collections\"");
                File.WriteAllText(AsmdefPath, json);
                AssetDatabase.ImportAsset(AsmdefPath);
                Debug.Log("[NetcodeAsmdefAutoLinker] Linked Unity.Netcode.Runtime into TheLastEmpire.Runtime.asmdef.");
            }
        }
    }
}
#endif

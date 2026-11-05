#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire.Editor
{
    public static class BulletLightEditorHelper
    {
        [MenuItem("TheLastEmpire/Bullets/Setup Front Light on All Bullet Prefabs")]
        public static void SetupAllBulletPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs/Item" });
            int modifiedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                Projectile projectile = prefab.GetComponent<Projectile>();
                if (projectile == null) continue;

                bool modified = SetupBulletLightOnPrefab(prefab, projectile);
                if (modified)
                {
                    EditorUtility.SetDirty(prefab);
                    modifiedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[BulletLightEditorHelper] Completed bullet lights setup! {modifiedCount} bullet prefabs checked/updated.");
        }

        private static bool SetupBulletLightOnPrefab(GameObject prefabRoot, Projectile projectile)
        {
            Transform lightTransform = prefabRoot.transform.Find("FrontLight");
            Light lightComp = null;

            if (lightTransform == null)
            {
                GameObject lightObj = new GameObject("FrontLight");
                lightObj.transform.SetParent(prefabRoot.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0f, 0.6f);
                lightComp = lightObj.AddComponent<Light>();
                lightTransform = lightObj.transform;
            }
            else
            {
                lightComp = lightTransform.GetComponent<Light>();
                if (lightComp == null)
                {
                    lightComp = lightTransform.gameObject.AddComponent<Light>();
                }
            }

            // Configure Light properties
            lightComp.type = LightType.Point;
            lightComp.shadows = LightShadows.None;
            lightComp.enabled = true;

            SerializedObject serializedProjectile = new SerializedObject(projectile);
            SerializedProperty bulletLightProp = serializedProjectile.FindProperty("bulletLight");
            SerializedProperty useBulletLightProp = serializedProjectile.FindProperty("useBulletLight");

            if (bulletLightProp != null)
            {
                bulletLightProp.objectReferenceValue = lightComp;
            }
            if (useBulletLightProp != null)
            {
                useBulletLightProp.boolValue = true;
            }

            serializedProjectile.ApplyModifiedProperties();
            return true;
        }

        [MenuItem("CONTEXT/Projectile/Setup or Fix Front Light")]
        public static void SetupContextLight(MenuCommand command)
        {
            Projectile projectile = (Projectile)command.context;
            if (projectile == null) return;

            Undo.RecordObject(projectile.gameObject, "Setup Bullet Front Light");
            Transform lightTransform = projectile.transform.Find("FrontLight");
            Light lightComp = null;

            if (lightTransform == null)
            {
                GameObject lightObj = new GameObject("FrontLight");
                lightObj.transform.SetParent(projectile.transform, false);
                lightObj.transform.localPosition = new Vector3(0f, 0f, 0.6f);
                Undo.RegisterCreatedObjectUndo(lightObj, "Create Front Light");
                lightComp = lightObj.AddComponent<Light>();
            }
            else
            {
                lightComp = lightTransform.GetComponent<Light>();
                if (lightComp == null)
                {
                    lightComp = Undo.AddComponent<Light>(lightTransform.gameObject);
                }
            }

            lightComp.type = LightType.Point;
            lightComp.shadows = LightShadows.None;
            lightComp.range = 6f;
            lightComp.intensity = 2.5f;
            lightComp.color = new Color(1f, 0.85f, 0.35f, 1f);
            lightComp.enabled = true;

            SerializedObject so = new SerializedObject(projectile);
            SerializedProperty lightProp = so.FindProperty("bulletLight");
            SerializedProperty useProp = so.FindProperty("useBulletLight");
            if (lightProp != null) lightProp.objectReferenceValue = lightComp;
            if (useProp != null) useProp.boolValue = true;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(projectile.gameObject);
            Debug.Log($"[BulletLightEditorHelper] Front light attached and configured on {projectile.gameObject.name}!");
        }
    }
}
#endif


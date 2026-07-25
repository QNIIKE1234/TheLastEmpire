using UnityEngine;
using UnityEditor;

public class ChangeVFXColor
{
    [MenuItem("Tools/Change Water Blast to Red")]
    public static void ChangeColorToRed()
    {
        string path = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Water/Water_blast_Red.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        ParticleSystem[] psArray = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in psArray)
        {
            var main = ps.main;
            ParticleSystem.MinMaxGradient startColor = main.startColor;
            startColor.colorMin = TintRed(startColor.colorMin);
            startColor.colorMax = TintRed(startColor.colorMax);
            startColor.color = TintRed(startColor.color);
            main.startColor = startColor;

            var col = ps.colorOverLifetime;
            if (col.enabled)
            {
                ParticleSystem.MinMaxGradient gradient = col.color;
                if (gradient.mode == ParticleSystemGradientMode.Gradient || gradient.mode == ParticleSystemGradientMode.TwoGradients)
                {
                    TintGradient(gradient.gradientMin);
                    TintGradient(gradient.gradientMax);
                    TintGradient(gradient.gradient);
                }
                col.color = gradient;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        Debug.Log("Successfully changed Water_blast_Red to RED!");
    }

    [MenuItem("Tools/Scale Down Water Blast (Red) to 30%")]
    public static void ScaleDownVFX()
    {
        string path = "Assets/VFX/Lana Studio/Hyper Casual FX/Prefabs/Water/Water_blast_Red.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        // 1. Scale the root transform
        instance.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);

        // 2. Ensure all child particle systems scale with hierarchy
        ParticleSystem[] psArray = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in psArray)
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        Debug.Log("Successfully scaled down Water_blast_Red to 30%!");
    }

    private static Color TintRed(Color original)
    {
        float intensity = Mathf.Max(original.r, original.g, original.b);
        return new Color(intensity, original.r * 0.2f, original.b * 0.2f, original.a);
    }

    private static void TintGradient(Gradient g)
    {
        if (g == null) return;
        GradientColorKey[] keys = g.colorKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].color = TintRed(keys[i].color);
        }
        g.colorKeys = keys;
    }
}
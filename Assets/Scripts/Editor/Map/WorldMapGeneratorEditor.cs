using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    [CustomEditor(typeof(WorldMapGenerator))]
    public class WorldMapGeneratorEditor : Editor
    {
        private Texture2D _cachedPreviewTexture;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            WorldMapGenerator generator = (WorldMapGenerator)target;

            GUILayout.Space(15);
            if (GUILayout.Button("Generate Map", GUILayout.Height(35)))
            {
                generator.GenerateMap();
                _cachedPreviewTexture = generator.GeneratePreviewTexture();

                byte[] bytes = _cachedPreviewTexture.EncodeToPNG();
                string assetPath = AssetDatabase.GetAssetPath(generator);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    string directory = System.IO.Path.GetDirectoryName(assetPath);
                    string pngPath = System.IO.Path.Combine(directory, generator.name + "_Preview.png");
                    System.IO.File.WriteAllBytes(pngPath, bytes);
                    AssetDatabase.ImportAsset(pngPath);
                    Debug.Log("Generated and saved map preview to: " + pngPath);
                }
                
                EditorUtility.SetDirty(generator);
            }

            if (_cachedPreviewTexture == null && generator.gridData != null && generator.gridData.Length == WorldMapGenerator.GridSize * WorldMapGenerator.GridSize)
            {
                _cachedPreviewTexture = generator.GeneratePreviewTexture();
            }

            if (_cachedPreviewTexture != null)
            {
                GUILayout.Space(15);
                GUILayout.Label($"Map Preview ({WorldMapGenerator.GridSize}x{WorldMapGenerator.GridSize} Grid):", EditorStyles.boldLabel);
                
                StageData spawnStage = generator.GetStage(generator.spawnX, generator.spawnY);
                string spawnBiomeName = spawnStage != null ? spawnStage.biome.ToString() : "Unknown";
                EditorGUILayout.HelpBox($"Spawn Coordinate: ({generator.spawnX}, {generator.spawnY})\nStarting Biome: {spawnBiomeName}", MessageType.Info);

                float width = EditorGUIUtility.currentViewWidth - 40;
                width = Mathf.Min(width, 256);

                Rect rect = GUILayoutUtility.GetRect(width, width);
                GUI.DrawTexture(rect, _cachedPreviewTexture, ScaleMode.ScaleToFit);

                GUILayout.Space(10);
                GUILayout.Label("Legend:", EditorStyles.boldLabel);
                DrawLegend("Waterways", WorldMapGenerator.GetBiomeColor(BiomeType.Waterways));
                DrawLegend("Overgrown Forests", WorldMapGenerator.GetBiomeColor(BiomeType.OvergrownForests));
                DrawLegend("Suburban Villages", WorldMapGenerator.GetBiomeColor(BiomeType.SuburbanVillages));
                DrawLegend("Highways", WorldMapGenerator.GetBiomeColor(BiomeType.Highways));
                DrawLegend("Urban Ruins", WorldMapGenerator.GetBiomeColor(BiomeType.UrbanRuins));
                DrawLegend("Highlands", WorldMapGenerator.GetBiomeColor(BiomeType.Highlands));
                DrawLegend("Special Events", WorldMapGenerator.GetBiomeColor(BiomeType.SpecialEvent));
            }
        }

        private void DrawLegend(string label, Color color)
        {
            EditorGUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(16, 16, GUILayout.Width(16));
            EditorGUI.DrawRect(colorRect, color);
            GUILayout.Space(8);
            GUILayout.Label(label);
            EditorGUILayout.EndHorizontal();
        }
    }
}

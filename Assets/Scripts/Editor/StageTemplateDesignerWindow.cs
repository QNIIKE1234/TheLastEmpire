using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TheLastEmpire
{
    public class StageTemplateDesignerWindow : EditorWindow
    {
        private const string RootDesignName = "[Stage_Template_Design]";
        private const string GuidesName = "__RoomGuides__";
        private const string ObstaclesFolderName = "Obstacles";
        private const string DecorationsFolderName = "Decorations";
        private const string EnemySpawnsFolderName = "EnemySpawns";
        private const string LootSpawnsFolderName = "LootSpawns";
        private const string NPCSpawnsFolderName = "NPCSpawns";

        private string templateId = "Urban_Ambush_01";
        private string templateName = "Urban Ambush";
        private BiomeType selectedBiome = BiomeType.UrbanRuins;
        private float selectionWeight = 1.0f;
        private Vector2 roomSize = new Vector2(32f, 18f);

        private Vector2 scrollPos;
        private int selectedPaletteTab = 0;
        private readonly string[] paletteTabs = { "Obstacles", "Enemies", "Loot & Crates", "NPCs" };

        private List<string> existingTemplatePaths = new List<string>();
        private int selectedTemplateIndex = 0;
        private string statusMessage = "";
        private MessageType statusMessageType = MessageType.Info;

        [MenuItem("Tools/The Last Empire/Stage Template Designer", false, 50)]
        public static void OpenWindow()
        {
            var win = GetWindow<StageTemplateDesignerWindow>("Stage Designer");
            win.minSize = new Vector2(480, 680);
            win.Show();
        }

        private void OnEnable()
        {
            RefreshExistingTemplatesList();
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawTemplateMetadata();
            DrawSceneSetupControls();
            DrawPaletteSection();
            DrawExportSection();
            DrawLoadExistingSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.75f, 0.2f) }
            };
            EditorGUILayout.LabelField("STAGE TEMPLATE DESIGNER", titleStyle);
            EditorGUILayout.LabelField("Author Handcrafted Stage Templates & Export to JSON", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(6);

            if (!string.IsNullOrEmpty(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusMessageType);
                EditorGUILayout.Space(4);
            }
        }

        private void DrawTemplateMetadata()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Template Metadata", EditorStyles.boldLabel);

            templateId = EditorGUILayout.TextField("Template ID (Filename):", templateId.Trim().Replace(" ", "_"));
            templateName = EditorGUILayout.TextField("Template Display Name:", templateName);
            selectedBiome = (BiomeType)EditorGUILayout.EnumPopup("Target Biome:", selectedBiome);
            selectionWeight = EditorGUILayout.Slider("Selection Weight:", selectionWeight, 0.1f, 5.0f);
            roomSize = EditorGUILayout.Vector2Field("Room Size (W x H):", roomSize);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawSceneSetupControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Scene Workspace", EditorStyles.boldLabel);

            GameObject root = GameObject.Find(RootDesignName);
            bool rootExists = root != null;

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = rootExists ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.3f, 0.7f, 1.0f);
            if (GUILayout.Button(rootExists ? "Reset / Re-align Workspace Guides" : "Spawn Design Workspace in Scene", GUILayout.Height(32)))
            {
                SetupDesignWorkspace();
            }

            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Clear Workspace", GUILayout.Height(32), GUILayout.Width(130)))
            {
                ClearDesignWorkspace();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (rootExists)
            {
                EditorGUILayout.HelpBox($"Workspace active: Place objects under '{RootDesignName}' sub-folders or click below.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawPaletteSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. Quick-Spawn Object Palette (Click to Place)", EditorStyles.boldLabel);

            selectedPaletteTab = GUILayout.Toolbar(selectedPaletteTab, paletteTabs);
            EditorGUILayout.Space(4);

            switch (selectedPaletteTab)
            {
                case 0:
                    DrawObstaclePalette();
                    break;
                case 1:
                    DrawEnemyPalette();
                    break;
                case 2:
                    DrawLootPalette();
                    break;
                case 3:
                    DrawNPCPalette();
                    break;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawObstaclePalette()
        {
            EditorGUILayout.LabelField("Road & Street Barriers:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Concrete Barricade")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Concrete_Barricade");
            if (GUILayout.Button("Sandbag Pile")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Sandbag_Pile");
            if (GUILayout.Button("Rusty Drum Stack")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Rusty_Drum_Stack");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rubble Pile")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Rubble_Pile");
            if (GUILayout.Button("Damaged Van")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Vehicle_01_Damaged_Van");
            if (GUILayout.Button("Ruined Truck")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Vehicle_02_Ruined_Truck");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Abandoned Bus")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Vehicle_03_Abandoned_Bus");
            if (GUILayout.Button("Ruined Bus Stop")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Ruined_Bus_Stop");
            if (GUILayout.Button("Broken Streetlight")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Apoc_Prop_Broken_Streetlight");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Nature & Urban Structures:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Pine Tree")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Tree_Pine");
            if (GUILayout.Button("Dead Tree")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/Tree_Dead");
            if (GUILayout.Button("Flowering Bush")) SpawnPrefabToCategory(DecorationsFolderName, "Prefabs/Environment/Bush_Flowering");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Suburban House A")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/House_Suburban_A");
            if (GUILayout.Button("Suburban House B")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/House_Suburban_B");
            if (GUILayout.Button("Suburban House C")) SpawnPrefabToCategory(ObstaclesFolderName, "Prefabs/Environment/House_Suburban_C");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawEnemyPalette()
        {
            EditorGUILayout.LabelField("Zombies & Special Mutants:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Zombie Normal")) SpawnEnemyMarker("ZombieNormal", "Prefabs/Character/ZombieNormal");
            if (GUILayout.Button("Zombie Runner")) SpawnEnemyMarker("ZombieRunner", "Prefabs/Character/ZombieRunner");
            if (GUILayout.Button("Zombie Boomer")) SpawnEnemyMarker("ZombieBoomer", "Prefabs/Character/ZombieBoomer");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Zombie Dog")) SpawnEnemyMarker("ZombieDog", "Prefabs/Character/ZombieDog");
            if (GUILayout.Button("Zombie Leader")) SpawnEnemyMarker("ZombieLeader", "Prefabs/Character/ZombieLeader");
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLootPalette()
        {
            EditorGUILayout.LabelField("Currency Drops:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Money: $50")) SpawnLootMarker("Money", 1, true, 50);
            if (GUILayout.Button("Money: $100")) SpawnLootMarker("Money", 1, true, 100);
            if (GUILayout.Button("Money: $250")) SpawnLootMarker("Money", 1, true, 250);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Ration & Medical Supplies:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bread (x2)")) SpawnLootMarker("Bread", 2, false, 0);
            if (GUILayout.Button("Canned Meat")) SpawnLootMarker("Canned Meat", 1, false, 0);
            if (GUILayout.Button("Small Potion")) SpawnLootMarker("Small Potion", 1, false, 0);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Large Potion")) SpawnLootMarker("Large Potion", 1, false, 0);
            if (GUILayout.Button("Bandage (x2)")) SpawnLootMarker("Bandage", 2, false, 0);
            if (GUILayout.Button("Adrenaline Shot")) SpawnLootMarker("Adrenaline Shot", 1, false, 0);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNPCPalette()
        {
            EditorGUILayout.LabelField("Story & Merchant NPCs:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Merchant NPC (Shop)")) SpawnNPCMarker(NPCType.Shop);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawExportSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("4. Export Template", EditorStyles.boldLabel);

            string saveFolder = $"Assets/Resources/Stages/{selectedBiome}";
            string fullPath = $"{saveFolder}/{templateId}.json";
            EditorGUILayout.LabelField("Export Path:", fullPath, EditorStyles.miniLabel);

            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.45f);
            if (GUILayout.Button("Save & Export Template to JSON", GUILayout.Height(38)))
            {
                ExportTemplateToJson();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        private void DrawLoadExistingSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("5. Load Existing Template for Editing", EditorStyles.boldLabel);

            if (existingTemplatePaths.Count == 0)
            {
                EditorGUILayout.LabelField("No JSON templates found in Assets/Resources/Stages/.", EditorStyles.miniLabel);
            }
            else
            {
                string[] displayNames = new string[existingTemplatePaths.Count];
                for (int i = 0; i < existingTemplatePaths.Count; i++)
                {
                    displayNames[i] = Path.GetFileNameWithoutExtension(existingTemplatePaths[i]);
                }

                selectedTemplateIndex = EditorGUILayout.Popup("Select Template:", selectedTemplateIndex, displayNames);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load Template into Scene", GUILayout.Height(28)))
                {
                    LoadTemplateIntoScene(existingTemplatePaths[selectedTemplateIndex]);
                }
                if (GUILayout.Button("Refresh List", GUILayout.Height(28), GUILayout.Width(100)))
                {
                    RefreshExistingTemplatesList();
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }

        private void SetupDesignWorkspace()
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root == null)
            {
                root = new GameObject(RootDesignName);
                root.transform.position = Vector3.zero;
            }

            EnsureFolder(root.transform, ObstaclesFolderName);
            EnsureFolder(root.transform, DecorationsFolderName);
            EnsureFolder(root.transform, EnemySpawnsFolderName);
            EnsureFolder(root.transform, LootSpawnsFolderName);
            EnsureFolder(root.transform, NPCSpawnsFolderName);

            // Create or update Room Guides
            Transform guidesT = root.transform.Find(GuidesName);
            GameObject guidesObj;
            if (guidesT == null)
            {
                guidesObj = new GameObject(GuidesName);
                guidesObj.transform.SetParent(root.transform, false);
                guidesObj.tag = "EditorOnly";
            }
            else
            {
                guidesObj = guidesT.gameObject;
                for (int i = guidesObj.transform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(guidesObj.transform.GetChild(i).gameObject);
                }
            }

            // Floor visual guide
            GameObject floorGuide = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floorGuide.name = "FloorGuide";
            floorGuide.transform.SetParent(guidesObj.transform, false);
            floorGuide.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floorGuide.transform.localScale = new Vector3(roomSize.x, roomSize.y, 1f);
            var col = floorGuide.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            // Portal Safety Zones (North, South, East, West)
            CreatePortalGuide(guidesObj.transform, "Gate_North", new Vector3(0f, 0.1f, roomSize.y * 0.5f));
            CreatePortalGuide(guidesObj.transform, "Gate_South", new Vector3(0f, 0.1f, -roomSize.y * 0.5f));
            CreatePortalGuide(guidesObj.transform, "Gate_East", new Vector3(roomSize.x * 0.5f, 0.1f, 0f));
            CreatePortalGuide(guidesObj.transform, "Gate_West", new Vector3(-roomSize.x * 0.5f, 0.1f, 0f));

            Selection.activeGameObject = root;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            statusMessage = "Design workspace is ready! Room boundaries and gate safety markers generated.";
            statusMessageType = MessageType.Info;
        }

        private void CreatePortalGuide(Transform parent, string name, Vector3 pos)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"{name}_SafetyZone";
            marker.transform.SetParent(parent, false);
            marker.transform.position = pos;
            marker.transform.localScale = new Vector3(3.6f, 0.05f, 3.6f);
            var col = marker.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
        }

        private Transform EnsureFolder(Transform parent, string folderName)
        {
            Transform f = parent.Find(folderName);
            if (f == null)
            {
                GameObject obj = new GameObject(folderName);
                obj.transform.SetParent(parent, false);
                f = obj.transform;
            }
            return f;
        }

        private void ClearDesignWorkspace()
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
                statusMessage = "Workspace cleared from scene.";
                statusMessageType = MessageType.None;
            }
        }

        private void SpawnPrefabToCategory(string folderName, string resourcePath)
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root == null) SetupDesignWorkspace();
            root = GameObject.Find(RootDesignName);

            Transform folder = EnsureFolder(root.transform, folderName);
            GameObject prefab = Resources.Load<GameObject>(resourcePath);

            GameObject spawned;
            if (prefab != null)
            {
                spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, folder);
            }
            else
            {
                spawned = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spawned.name = Path.GetFileNameWithoutExtension(resourcePath);
                spawned.transform.SetParent(folder, false);
            }

            spawned.transform.position = GetSceneCenterSpawnPosition();
            StageObjectMarker marker = spawned.GetComponent<StageObjectMarker>();
            if (marker == null) marker = spawned.AddComponent<StageObjectMarker>();
            marker.category = folderName == ObstaclesFolderName ? StageObjectMarker.MarkerCategory.Obstacle : StageObjectMarker.MarkerCategory.Decoration;
            marker.originalPrefabName = spawned.name.Replace("(Clone)", "").Trim();
            marker.resourcePath = resourcePath;

            Undo.RegisterCreatedObjectUndo(spawned, $"Spawn {spawned.name}");
            Selection.activeGameObject = spawned;
        }

        private void SpawnEnemyMarker(string enemyName, string resourcePath)
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root == null) SetupDesignWorkspace();
            root = GameObject.Find(RootDesignName);

            Transform folder = EnsureFolder(root.transform, EnemySpawnsFolderName);
            GameObject prefab = Resources.Load<GameObject>(resourcePath);

            GameObject spawned;
            if (prefab != null)
            {
                spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, folder);
            }
            else
            {
                spawned = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                spawned.name = enemyName;
                spawned.transform.SetParent(folder, false);
            }

            spawned.transform.position = GetSceneCenterSpawnPosition();
            StageObjectMarker marker = spawned.GetComponent<StageObjectMarker>();
            if (marker == null) marker = spawned.AddComponent<StageObjectMarker>();
            marker.category = StageObjectMarker.MarkerCategory.EnemySpawn;
            marker.originalPrefabName = enemyName;
            marker.resourcePath = resourcePath;

            Undo.RegisterCreatedObjectUndo(spawned, $"Spawn Enemy {enemyName}");
            Selection.activeGameObject = spawned;
        }

        private void SpawnLootMarker(string itemName, int quantity, bool isMoney, int moneyAmount)
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root == null) SetupDesignWorkspace();
            root = GameObject.Find(RootDesignName);

            Transform folder = EnsureFolder(root.transform, LootSpawnsFolderName);
            GameObject markerObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            markerObj.name = isMoney ? $"Drop_Money_${moneyAmount}" : $"Drop_{itemName}_x{quantity}";
            markerObj.transform.SetParent(folder, false);
            markerObj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            markerObj.transform.position = GetSceneCenterSpawnPosition();

            StageObjectMarker marker = markerObj.AddComponent<StageObjectMarker>();
            marker.category = StageObjectMarker.MarkerCategory.LootSpawn;
            marker.itemName = itemName;
            marker.quantity = quantity;
            marker.isMoney = isMoney;
            marker.moneyAmount = moneyAmount;

            Undo.RegisterCreatedObjectUndo(markerObj, $"Spawn Loot {markerObj.name}");
            Selection.activeGameObject = markerObj;
        }

        private void SpawnNPCMarker(NPCType npcType)
        {
            GameObject root = GameObject.Find(RootDesignName);
            if (root == null) SetupDesignWorkspace();
            root = GameObject.Find(RootDesignName);

            Transform folder = EnsureFolder(root.transform, NPCSpawnsFolderName);
            GameObject npcPrefab = Resources.Load<GameObject>("Prefabs/Character/NPCPrefabs");

            GameObject spawned;
            if (npcPrefab != null)
            {
                spawned = (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab, folder);
            }
            else
            {
                spawned = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spawned.name = $"NPC_{npcType}";
                spawned.transform.SetParent(folder, false);
            }

            spawned.transform.position = GetSceneCenterSpawnPosition();
            StageObjectMarker marker = spawned.GetComponent<StageObjectMarker>();
            if (marker == null) marker = spawned.AddComponent<StageObjectMarker>();
            marker.category = StageObjectMarker.MarkerCategory.NPCSpawn;
            marker.npcType = npcType;

            Undo.RegisterCreatedObjectUndo(spawned, $"Spawn NPC {npcType}");
            Selection.activeGameObject = spawned;
        }

        private Vector3 GetSceneCenterSpawnPosition()
        {
            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 pivot = SceneView.lastActiveSceneView.pivot;
                return new Vector3(Mathf.Clamp(pivot.x, -12f, 12f), 0.2f, Mathf.Clamp(pivot.z, -6f, 6f));
            }
            return new Vector3(0f, 0.2f, 0f);
        }

        private void ExportTemplateToJson()
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                EditorUtility.DisplayDialog("Error", "Template ID cannot be empty!", "OK");
                return;
            }

            GameObject root = GameObject.Find(RootDesignName);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Error", "No design workspace found in scene. Click 'Spawn Design Workspace' first.", "OK");
                return;
            }

            StageTemplateData data = new StageTemplateData
            {
                templateId = templateId.Trim(),
                templateName = templateName.Trim(),
                biome = selectedBiome,
                selectionWeight = selectionWeight,
                roomSize = roomSize
            };

            // 1. Gather Obstacles
            Transform obstaclesFolder = root.transform.Find(ObstaclesFolderName);
            if (obstaclesFolder != null)
            {
                foreach (Transform child in obstaclesFolder)
                {
                    data.obstacles.Add(ExtractPlacedObject(child));
                }
            }

            // 2. Gather Decorations
            Transform decorationsFolder = root.transform.Find(DecorationsFolderName);
            if (decorationsFolder != null)
            {
                foreach (Transform child in decorationsFolder)
                {
                    data.decorations.Add(ExtractPlacedObject(child));
                }
            }

            // 3. Gather Enemy Spawns
            Transform enemiesFolder = root.transform.Find(EnemySpawnsFolderName);
            if (enemiesFolder != null)
            {
                foreach (Transform child in enemiesFolder)
                {
                    StageObjectMarker marker = child.GetComponent<StageObjectMarker>();
                    string enemyName = marker != null && !string.IsNullOrEmpty(marker.originalPrefabName) ? marker.originalPrefabName : child.name.Replace("(Clone)", "").Trim();

                    data.enemySpawns.Add(new StageEnemySpawnPoint
                    {
                        enemyPrefabName = enemyName,
                        position = child.position,
                        rotation = child.eulerAngles
                    });
                }
            }

            // 4. Gather Loot Spawns
            Transform lootFolder = root.transform.Find(LootSpawnsFolderName);
            if (lootFolder != null)
            {
                foreach (Transform child in lootFolder)
                {
                    StageObjectMarker marker = child.GetComponent<StageObjectMarker>();
                    data.lootSpawns.Add(new StageLootSpawnPoint
                    {
                        itemName = marker != null ? marker.itemName : "Bread",
                        quantity = marker != null ? marker.quantity : 1,
                        isMoney = marker != null && marker.isMoney,
                        moneyAmount = marker != null ? marker.moneyAmount : 50,
                        position = child.position
                    });
                }
            }

            // 5. Gather NPC Spawns
            Transform npcFolder = root.transform.Find(NPCSpawnsFolderName);
            if (npcFolder != null)
            {
                foreach (Transform child in npcFolder)
                {
                    StageObjectMarker marker = child.GetComponent<StageObjectMarker>();
                    data.npcSpawns.Add(new StageNPCSpawnPoint
                    {
                        npcType = marker != null ? marker.npcType : NPCType.Shop,
                        position = child.position,
                        rotation = child.eulerAngles
                    });
                }
            }

            // Write to file
            string dirPath = Path.Combine(Application.dataPath, "Resources", "Stages", selectedBiome.ToString());
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            string filePath = Path.Combine(dirPath, $"{data.templateId}.json");
            File.WriteAllText(filePath, data.ToJson(true));
            AssetDatabase.Refresh();

            statusMessage = $"Template '{data.templateId}' exported successfully ({data.obstacles.Count} obstacles, {data.enemySpawns.Count} enemies, {data.lootSpawns.Count} loot)!";
            statusMessageType = MessageType.Info;
            RefreshExistingTemplatesList();

            EditorUtility.DisplayDialog("Stage Template Saved", $"Successfully exported to:\nAssets/Resources/Stages/{selectedBiome}/{data.templateId}.json", "OK");
        }

        private StagePlacedObject ExtractPlacedObject(Transform t)
        {
            StageObjectMarker marker = t.GetComponent<StageObjectMarker>();
            string pName = marker != null && !string.IsNullOrEmpty(marker.originalPrefabName)
                ? marker.originalPrefabName
                : t.name.Replace("(Clone)", "").Trim();

            string resPath = marker != null ? marker.resourcePath : "";

            return new StagePlacedObject
            {
                prefabName = pName,
                resourcePath = resPath,
                position = t.position,
                rotation = t.eulerAngles,
                scale = t.localScale
            };
        }

        private void LoadTemplateIntoScene(string filePath)
        {
            if (!File.Exists(filePath)) return;

            string json = File.ReadAllText(filePath);
            StageTemplateData data = StageTemplateData.FromJson(json);
            if (data == null)
            {
                EditorUtility.DisplayDialog("Error", "Could not parse JSON template file.", "OK");
                return;
            }

            templateId = data.templateId;
            templateName = data.templateName;
            selectedBiome = data.biome;
            selectionWeight = data.selectionWeight;
            roomSize = data.roomSize;

            SetupDesignWorkspace();
            GameObject root = GameObject.Find(RootDesignName);

            // Clear folders
            Transform obstF = EnsureFolder(root.transform, ObstaclesFolderName);
            Transform decoF = EnsureFolder(root.transform, DecorationsFolderName);
            Transform enemyF = EnsureFolder(root.transform, EnemySpawnsFolderName);
            Transform lootF = EnsureFolder(root.transform, LootSpawnsFolderName);
            Transform npcF = EnsureFolder(root.transform, NPCSpawnsFolderName);

            ClearFolderChildren(obstF);
            ClearFolderChildren(decoF);
            ClearFolderChildren(enemyF);
            ClearFolderChildren(lootF);
            ClearFolderChildren(npcF);

            // Reconstruct Obstacles
            foreach (var obs in data.obstacles)
            {
                GameObject obj = InstantiateFromPlaced(obs, obstF);
                var marker = obj.AddComponent<StageObjectMarker>();
                marker.category = StageObjectMarker.MarkerCategory.Obstacle;
                marker.originalPrefabName = obs.prefabName;
                marker.resourcePath = obs.resourcePath;
            }

            // Reconstruct Decorations
            foreach (var dec in data.decorations)
            {
                GameObject obj = InstantiateFromPlaced(dec, decoF);
                var marker = obj.AddComponent<StageObjectMarker>();
                marker.category = StageObjectMarker.MarkerCategory.Decoration;
                marker.originalPrefabName = dec.prefabName;
                marker.resourcePath = dec.resourcePath;
            }

            // Reconstruct Enemies
            foreach (var en in data.enemySpawns)
            {
                GameObject prefab = Resources.Load<GameObject>($"Prefabs/Character/{en.enemyPrefabName}");
                GameObject obj = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, enemyF) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                obj.name = en.enemyPrefabName;
                obj.transform.SetParent(enemyF, false);
                obj.transform.position = en.position;
                obj.transform.eulerAngles = en.rotation;

                var marker = obj.AddComponent<StageObjectMarker>();
                marker.category = StageObjectMarker.MarkerCategory.EnemySpawn;
                marker.originalPrefabName = en.enemyPrefabName;
            }

            // Reconstruct Loot
            foreach (var lt in data.lootSpawns)
            {
                GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = lt.isMoney ? $"Drop_Money_${lt.moneyAmount}" : $"Drop_{lt.itemName}_x{lt.quantity}";
                obj.transform.SetParent(lootF, false);
                obj.transform.position = lt.position;
                obj.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);

                var marker = obj.AddComponent<StageObjectMarker>();
                marker.category = StageObjectMarker.MarkerCategory.LootSpawn;
                marker.itemName = lt.itemName;
                marker.quantity = lt.quantity;
                marker.isMoney = lt.isMoney;
                marker.moneyAmount = lt.moneyAmount;
            }

            // Reconstruct NPCs
            foreach (var npc in data.npcSpawns)
            {
                GameObject npcPrefab = Resources.Load<GameObject>("Prefabs/Character/NPCPrefabs");
                GameObject obj = npcPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(npcPrefab, npcF) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                obj.name = $"NPC_{npc.npcType}";
                obj.transform.SetParent(npcF, false);
                obj.transform.position = npc.position;
                obj.transform.eulerAngles = npc.rotation;

                var marker = obj.AddComponent<StageObjectMarker>();
                marker.category = StageObjectMarker.MarkerCategory.NPCSpawn;
                marker.npcType = npc.npcType;
            }

            statusMessage = $"Loaded template '{data.templateName}' ({data.templateId}) into workspace!";
            statusMessageType = MessageType.Info;
        }

        private GameObject InstantiateFromPlaced(StagePlacedObject placed, Transform parent)
        {
            GameObject prefab = null;
            if (!string.IsNullOrEmpty(placed.resourcePath))
            {
                prefab = Resources.Load<GameObject>(placed.resourcePath);
            }
            if (prefab == null && !string.IsNullOrEmpty(placed.prefabName))
            {
                prefab = Resources.Load<GameObject>($"Prefabs/Environment/{placed.prefabName}");
            }

            GameObject obj = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = placed.prefabName;
            obj.transform.SetParent(parent, false);
            obj.transform.position = placed.position;
            obj.transform.eulerAngles = placed.rotation;
            obj.transform.localScale = placed.scale;
            return obj;
        }

        private void ClearFolderChildren(Transform folder)
        {
            if (folder == null) return;
            for (int i = folder.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(folder.GetChild(i).gameObject);
            }
        }

        private void RefreshExistingTemplatesList()
        {
            existingTemplatePaths.Clear();
            string rootDir = Path.Combine(Application.dataPath, "Resources", "Stages");
            if (Directory.Exists(rootDir))
            {
                string[] files = Directory.GetFiles(rootDir, "*.json", SearchOption.AllDirectories);
                existingTemplatePaths.AddRange(files);
            }
            selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, Mathf.Max(0, existingTemplatePaths.Count - 1));
        }
    }
}

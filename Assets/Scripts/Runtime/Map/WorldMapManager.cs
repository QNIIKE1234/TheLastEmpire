using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    public class WorldMapManager : MonoBehaviour
    {
        public static WorldMapManager Instance { get; private set; }

        [SerializeField] private WorldMapGenerator mapGenerator;

        public int CurrentPlayerX { get; private set; }
        public int CurrentPlayerY { get; private set; }

        public delegate void StageChangedHandler(int newX, int newY);
        public event StageChangedHandler OnStageChanged;

        public WorldMapGenerator MapGenerator
        {
            get { return mapGenerator; }
            set { mapGenerator = value; }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            if (mapGenerator != null)
            {
                // Reset persistent editor memory at startup so it doesn't auto-save exploration
                ResetMapProgression();

                // If map is not generated yet, generate it
                if (mapGenerator.gridData == null || mapGenerator.gridData.Length != WorldMapGenerator.GridSize * WorldMapGenerator.GridSize)
                {
                    StartNewGame(mapGenerator.seed);
                }
                else
                {
                    // Map is already generated (persisted on the ScriptableObject asset),
                    // but we still need to pick player starting coordinates at runtime startup!
                    InitializePlayerCoordinates();
                }
            }
        }

        public void StartNewGame(int seed)
        {
            if (mapGenerator == null)
            {
                Debug.LogError("WorldMapManager: WorldMapGenerator asset reference is missing!");
                return;
            }

            mapGenerator.seed = seed;
            mapGenerator.GenerateMap();

            InitializePlayerCoordinates();
        }

        private void InitializePlayerCoordinates()
        {
            if (mapGenerator == null) return;

            CurrentPlayerX = mapGenerator.spawnX;
            CurrentPlayerY = mapGenerator.spawnY;

            // Reveal the starting location
            StageData startingStage = mapGenerator.GetStage(CurrentPlayerX, CurrentPlayerY);
            if (startingStage != null)
            {
                startingStage.isExplored = true;
            }

            // Trigger the initial stage update event so that visualizers and managers set up correctly
            OnStageChanged?.Invoke(CurrentPlayerX, CurrentPlayerY);
        }

        public void SaveGame()
        {
            if (mapGenerator == null || mapGenerator.gridData == null)
            {
                Debug.LogError("WorldMapManager: Cannot save game. Map data is not generated.");
                return;
            }

            SaveData data = new SaveData
            {
                worldSeed = mapGenerator.seed,
                playerCoordX = CurrentPlayerX,
                playerCoordY = CurrentPlayerY
            };

            for (int i = 0; i < mapGenerator.gridData.Length; i++)
            {
                StageData stage = mapGenerator.gridData[i];
                if (stage.isExplored)
                {
                    data.exploredStageIndices.Add(i);
                }
                if (stage.isCleared)
                {
                    data.clearedStageIndices.Add(i);
                }
            }

            SaveSystem.Save(data);
        }

        public bool LoadGame()
        {
            if (!SaveSystem.HasSaveFile())
            {
                Debug.LogWarning("WorldMapManager: No save file exists to load.");
                return false;
            }

            SaveData data = SaveSystem.Load();
            if (data == null) return false;

            if (mapGenerator == null)
            {
                Debug.LogError("WorldMapManager: WorldMapGenerator asset reference is missing!");
                return false;
            }

            mapGenerator.seed = data.worldSeed;
            mapGenerator.GenerateMap();

            CurrentPlayerX = data.playerCoordX;
            CurrentPlayerY = data.playerCoordY;

            // Restore explored / cleared status from save data
            foreach (int index in data.exploredStageIndices)
            {
                if (index >= 0 && index < mapGenerator.gridData.Length)
                {
                    mapGenerator.gridData[index].isExplored = true;
                }
            }

            foreach (int index in data.clearedStageIndices)
            {
                if (index >= 0 && index < mapGenerator.gridData.Length)
                {
                    mapGenerator.gridData[index].isCleared = true;
                }
            }

            Debug.Log("WorldMapManager: Game state successfully loaded and restored.");
            OnStageChanged?.Invoke(CurrentPlayerX, CurrentPlayerY);
            return true;
        }

        public void MovePlayer(int targetX, int targetY)
        {
            if (mapGenerator == null) return;

            if (targetX >= 0 && targetX < WorldMapGenerator.GridSize &&
                targetY >= 0 && targetY < WorldMapGenerator.GridSize)
            {
                CurrentPlayerX = targetX;
                CurrentPlayerY = targetY;

                // Reveal the newly visited stage (Fog of War)
                StageData newStage = mapGenerator.GetStage(CurrentPlayerX, CurrentPlayerY);
                if (newStage != null)
                {
                    newStage.isExplored = true;
                }

                OnStageChanged?.Invoke(CurrentPlayerX, CurrentPlayerY);
            }
        }

        [ContextMenu("Reset Map Exploration")]
        public void ResetMapProgression()
        {
            if (mapGenerator == null || mapGenerator.gridData == null) return;
            foreach (var stage in mapGenerator.gridData)
            {
                if (stage != null)
                {
                    stage.isExplored = false;
                    stage.isCleared = false;
                }
            }
        }
    }
}

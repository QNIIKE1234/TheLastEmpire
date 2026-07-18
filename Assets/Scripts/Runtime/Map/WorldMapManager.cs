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
            if (mapGenerator != null && (mapGenerator.gridData == null || mapGenerator.gridData.Length == 0))
            {
                StartNewGame(mapGenerator.seed);
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

            // Pick a random walkable starting location (not on Waterways) based on the seed
            System.Random rand = new System.Random(seed);
            int attempts = 0;
            do
            {
                CurrentPlayerX = rand.Next(0, WorldMapGenerator.GridSize);
                CurrentPlayerY = rand.Next(0, WorldMapGenerator.GridSize);
                attempts++;
            } 
            while (mapGenerator.GetStage(CurrentPlayerX, CurrentPlayerY)?.biome == BiomeType.Waterways && attempts < 100);

            // Reveal the starting location
            StageData startingStage = mapGenerator.GetStage(CurrentPlayerX, CurrentPlayerY);
            if (startingStage != null)
            {
                startingStage.isExplored = true;
            }

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
    }
}

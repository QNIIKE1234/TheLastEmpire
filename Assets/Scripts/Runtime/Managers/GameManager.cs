using UnityEngine;

namespace TheLastEmpire
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

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
            // Register to listen to the stage change event
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged += OnPlayerEnteredNewStage;
                
                // Trigger initial setup for the starting stage
                OnPlayerEnteredNewStage(WorldMapManager.Instance.CurrentPlayerX, WorldMapManager.Instance.CurrentPlayerY);
            }
            else
            {
                Debug.LogWarning("GameManager: WorldMapManager.Instance is null at startup!");
            }
        }

        private void OnDestroy()
        {
            // Unregister to prevent memory leaks
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= OnPlayerEnteredNewStage;
            }
        }

        // Called automatically whenever the player transitions coordinates
        private void OnPlayerEnteredNewStage(int x, int y)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (stage == null) return;

            Debug.Log($"[GameManager] Player entered stage ({x}, {y}) | Biome: {stage.biome} | Seed: {stage.stageSeed}");

            // Execute local stage gameplay spawns and audio setups
            SpawnLocalStageContent(stage);
            PlayBiomeBackgroundMusic(stage.biome);
        }

        private void SpawnLocalStageContent(StageData stage)
        {
            // Reset Unity's random state using the deterministic stageSeed
            // This guarantees the exact same items/enemies spawn if player returns to this stage coordinates
            Random.InitState(stage.stageSeed);

            // Placeholder logs representing procedural spawning
            switch (stage.biome)
            {
                case BiomeType.UrbanRuins:
                    Debug.Log($"[GameManager] Spawning Urban Ruins zombies and debris at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.Highways:
                    Debug.Log($"[GameManager] Spawning Highway robot runners and road obstacles at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.OvergrownForests:
                    Debug.Log($"[GameManager] Spawning Overgrown Forest alien beasts and trees at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.SuburbanVillages:
                    Debug.Log($"[GameManager] Spawning Suburban Village safe shelters or minor threats at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.Highlands:
                    Debug.Log($"[GameManager] Spawning Highland rocky terrains and high-tier loot at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.Waterways:
                    Debug.Log($"[GameManager] Spawning Waterways bridges or aquatic obstacles at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
                case BiomeType.SpecialEvent:
                    Debug.Log($"[GameManager] Spawning SPECIAL EVENT boss or rare supply drop at ({stage.x}, {stage.y}) using seed {stage.stageSeed}.");
                    break;
            }
        }

        private void PlayBiomeBackgroundMusic(BiomeType biome)
        {
            // Placeholder: Switch audio tracks based on current biome type
            // e.g. SoundManager.Instance.PlayMusic(biomeMusicDictionary[biome]);
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// Manages entering and exiting unique procedurally-generated building interiors.
    /// Visual style: centered cutaway top-down room surrounded by pitch-black void (HUD intact).
    /// Supports deterministic per-building layouts (no duplicate houses), 4 interior themes,
    /// mixed Safe (~40%) & Ambush (~60%) rooms, persistent loot/clear states, and Online Co-op sync.
    /// </summary>
    public class BuildingInteriorManager : MonoBehaviour
    {
        public static BuildingInteriorManager Instance { get; private set; }
        public static bool IsInsideInterior => Instance != null && Instance._isInsideInterior;
        public static bool IsTransitioning => Instance != null && Instance._isTransitioning;
        public static int CurrentFloorIndex => (Instance != null && Instance._isInsideInterior) ? Instance._currentFloorIndex : 0;
        public static int CurrentTotalFloors => (Instance != null && Instance._isInsideInterior) ? Instance._currentTotalFloors : 0;
        // Encodes buildingId + floorIndex (e.g. Bldg 1 Floor 2 => 12) for automatic per-floor Co-op isolation!
        public static int CurrentInteriorId => (Instance != null && Instance._isInsideInterior) ? (Instance._currentBuildingId * 10 + Instance._currentFloorIndex) : 0;
        public static int CurrentInteriorSeed => (Instance != null && Instance._isInsideInterior) ? Instance._currentInteriorSeed : 0;

        private struct SavedContainerState
        {
            public int remainingMoney;
            public List<LootSlot> remainingItems;
        }

        private struct Palette
        {
            public Color floorA;
            public Color floorB;
            public Color wallMain;
            public Color wallTrim;
            public Color accent;
            public Color lightColor;
        }

        private enum LayoutArchetype
        {
            OpenHall = 0,
            SidePartition = 1,
            BackPartition = 2,
            CornerAlcove = 3,
            TwinZones = 4
        }

        private bool _isInsideInterior = false;
        private bool _isTransitioning = false;
        private int _currentBuildingId = 0;
        private int _currentFloorIndex = 1;
        private int _currentTotalFloors = 1;
        private int _currentInteriorSeed = 0;
        private int _currentStageX = 64;
        private int _currentStageY = 64;
        private string _currentInteriorKey = "";
        private string _currentDisplayName = "";
        private InteriorTheme _currentTheme = InteriorTheme.House;
        private InteriorTemplateData _currentTemplate;
        private bool _currentMirrorX = false;
        private bool _currentRoomIsAmbush = false;
        private bool _currentRoomClearedNotified = false;

        private Vector3 _returnExteriorPos = Vector3.zero;
        private Vector3 _returnExteriorForward = Vector3.back;

        private GameObject _interiorRoot;
        private readonly List<GameObject> _activeInteriorEnemies = new List<GameObject>();
        private readonly List<LootContainer> _activeInteriorContainers = new List<LootContainer>();

        // Suspended exterior state
        private readonly List<GameObject> _suspendedExteriorObjects = new List<GameObject>();
        private readonly List<GameObject> _suspendedExteriorEnemies = new List<GameObject>();
        private readonly List<GameObject> _suspendedExteriorDrops = new List<GameObject>();
        private readonly List<GameObject> _suspendedExteriorBuildings = new List<GameObject>();
        private readonly List<GameObject> _suspendedExteriorNpcs = new List<GameObject>();
        private readonly List<TransitionPortal> _suspendedPortals = new List<TransitionPortal>();
        private readonly List<Renderer> _suspendedRenderers = new List<Renderer>();

        // Reused Center Ground tile ("CGround") original state (while the other 8 neighbor tiles are hidden)
        private Transform _centerGroundTransform;
        private MeshRenderer _centerGroundRenderer;
        private Vector3 _savedCenterGroundScale = new Vector3(5f, 1f, 2.5f);
        private Material _savedCenterGroundMaterial;
        private bool _hasSavedCenterGround = false;

        // Saved camera & lighting state
        private CameraClearFlags _savedClearFlags = CameraClearFlags.Skybox;
        private Color _savedBgColor = Color.black;
        private bool _savedFog = false;
        private Color _savedAmbientLight = Color.gray;
        private bool _hasSavedCameraState = false;

        // Persistent match caches (backed by JSON file at persistentDataPath/InteriorProgressSave.json!)
        private readonly HashSet<string> _visitedInteriorKeys = new HashSet<string>();
        private readonly HashSet<string> _clearedInteriorKeys = new HashSet<string>();
        private readonly Dictionary<string, InteriorSavedContainerRecord> _localContainerStates = new Dictionary<string, InteriorSavedContainerRecord>();
        private bool _hasLoadedPersistence = false;

        // Lightweight fade & English status banner overlay
        private float _fadeAlpha = 0f;
        private Texture2D _blackTex;
        private string _bannerTitle = "";
        private string _bannerSubtitle = "";
        private Color _bannerColor = Color.white;
        private float _bannerTimer = 0f;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            EnsureExists();
        }

        public static BuildingInteriorManager EnsureExists()
        {
            if (Instance != null) return Instance;
            BuildingInteriorManager existing = FindFirstObjectByType<BuildingInteriorManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            GameObject go = new GameObject("[BuildingInteriorManager]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<BuildingInteriorManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleWorldStageChanged;
            }
            if (_blackTex != null)
            {
                Destroy(_blackTex);
                _blackTex = null;
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            HookWorldMapEvents();
            EnsurePersistenceLoaded();
        }

        private void OnApplicationQuit()
        {
            if (_isInsideInterior)
            {
                SaveActiveInteriorContainerStates();
            }
            PersistStateToJson();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                if (_isInsideInterior) SaveActiveInteriorContainerStates();
                PersistStateToJson();
            }
        }

        private static int GetCurrentWorldSeed()
        {
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                StageData center = WorldMapManager.Instance.MapGenerator.GetStage(64, 64);
                if (center != null) return center.stageSeed;
            }
            return 0;
        }

        private void EnsurePersistenceLoaded()
        {
            if (_hasLoadedPersistence) return;
            _hasLoadedPersistence = true;
            InteriorPersistenceStore.LoadInto(GetCurrentWorldSeed(), _visitedInteriorKeys, _clearedInteriorKeys, _localContainerStates);
        }

        private void PersistStateToJson()
        {
            InteriorPersistenceStore.SaveToDisk(GetCurrentWorldSeed(), _visitedInteriorKeys, _clearedInteriorKeys, _localContainerStates);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_isInsideInterior)
            {
                ForceResetOnSceneChange();
            }
            HookWorldMapEvents();
        }

        private void HookWorldMapEvents()
        {
            if (WorldMapManager.Instance != null)
            {
                WorldMapManager.Instance.OnStageChanged -= HandleWorldStageChanged;
                WorldMapManager.Instance.OnStageChanged += HandleWorldStageChanged;
            }
        }

        private void HandleWorldStageChanged(int newX, int newY)
        {
            if (_isInsideInterior)
            {
                ExitCurrentBuilding(immediate: true);
            }
        }

        /// <summary>
        /// Clears cleared-house memory and local container snapshots when starting a new match / retry.
        /// </summary>
        public void ClearMatchCache()
        {
            if (_isInsideInterior)
            {
                ExitCurrentBuilding(immediate: true);
            }
            _visitedInteriorKeys.Clear();
            _clearedInteriorKeys.Clear();
            _localContainerStates.Clear();
            _hasLoadedPersistence = true;
            InteriorPersistenceStore.DeleteSaveFile();
        }

        private void ForceResetOnSceneChange()
        {
            StopAllCoroutines();
            _isTransitioning = false;
            _isInsideInterior = false;
            _currentBuildingId = 0;
            _currentInteriorSeed = 0;
            _fadeAlpha = 0f;
            _bannerTimer = 0f;
            if (_interiorRoot != null)
            {
                Destroy(_interiorRoot);
                _interiorRoot = null;
            }
            _activeInteriorEnemies.Clear();
            _activeInteriorContainers.Clear();
            _suspendedExteriorObjects.Clear();
            _suspendedExteriorEnemies.Clear();
            _suspendedExteriorDrops.Clear();
            _suspendedExteriorBuildings.Clear();
            _suspendedExteriorNpcs.Clear();
            _suspendedPortals.Clear();
            _suspendedRenderers.Clear();
            _hasSavedCameraState = false;
            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.InteriorOverrideActive = false;
            }
        }

        private void Update()
        {
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
            }

            if (!_isInsideInterior || _isTransitioning) return;

            // Check if local player died while inside — restore exterior cleanly if dead & game over triggers
            PlayerController localPlayer = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (localPlayer == null) return;

            // Track ambush room clear state
            if (_currentRoomIsAmbush && !_currentRoomClearedNotified && !string.IsNullOrEmpty(_currentInteriorKey))
            {
                bool anyAlive = false;
                for (int i = _activeInteriorEnemies.Count - 1; i >= 0; i--)
                {
                    GameObject e = _activeInteriorEnemies[i];
                    if (e == null || !e.activeInHierarchy)
                    {
                        _activeInteriorEnemies.RemoveAt(i);
                        continue;
                    }
                    Health h = e.GetComponent<Health>();
                    if (h != null && h.IsDead)
                    {
                        _activeInteriorEnemies.RemoveAt(i);
                        continue;
                    }
                    anyAlive = true;
                }

                // In Co-op Client mode with Host inside the same room, also check any active puppet enemies
                if (!anyAlive && EmpireNetworkManager.IsOnlineClient && IsHostInSameInterior())
                {
                    if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
                    {
                        for (int i = 0; i < GameManager.Instance.ActiveEnemies.Count; i++)
                        {
                            GameObject e = GameManager.Instance.ActiveEnemies[i];
                            if (e == null || !e.activeInHierarchy) continue;
                            Health h = e.GetComponent<Health>();
                            if (h == null || !h.IsDead)
                            {
                                anyAlive = true;
                                break;
                            }
                        }
                    }
                }

                if (!anyAlive)
                {
                    _currentRoomClearedNotified = true;
                    _clearedInteriorKeys.Add(_currentInteriorKey);
                    PersistStateToJson();
                    ShowBanner("BUILDING CLEARED", _currentDisplayName.ToUpperInvariant(), new Color(0.40f, 1f, 0.58f, 1f), 2.2f);
                }
            }
        }

        public static int ComputeInteriorSeed(int stageSeed, int stageX, int stageY, int buildingId, string prefabName)
        {
            unchecked
            {
                int hash = (int)2166136261u;
                hash = (hash ^ stageSeed) * 16777619;
                hash = (hash ^ (stageX * 73856093)) * 16777619;
                hash = (hash ^ (stageY * 19349663)) * 16777619;
                hash = (hash ^ (buildingId * 83492791)) * 16777619;
                if (!string.IsNullOrEmpty(prefabName))
                {
                    for (int i = 0; i < prefabName.Length; i++)
                    {
                        hash = (hash ^ prefabName[i]) * 16777619;
                    }
                }
                return hash == 0 ? 1337 : hash;
            }
        }

        private static bool IsAnyModalUIOpen()
        {
            if (LootUI.HasInstance && LootUI.Instance.IsOpen) return true;
            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen) return true;
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return true;
            if (ShopUI.Instance != null && ShopUI.Instance.IsOpen) return true;
            return false;
        }

        public void EnterBuilding(BuildingEntrance entrance)
        {
            if (entrance == null || _isTransitioning || _isInsideInterior) return;
            if (IsAnyModalUIOpen()) return;

            PlayerController player = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (player == null || player.IsDowned) return;
            if (player.PlayerHealth != null && player.PlayerHealth.IsDead) return;

            InteriorTemplateData template = InteriorTemplateLibrary.SelectTemplateForBuilding(
                entrance.SourcePrefabName, entrance.BuildingWorldHeight, entrance.InteriorSeed);

            StartCoroutine(EnterBuildingRoutine(
                entrance.BuildingId,
                entrance.StageX,
                entrance.StageY,
                entrance.InteriorSeed,
                entrance.Theme,
                entrance.BuildingDisplayName,
                entrance.ExteriorReturnPosition,
                entrance.ExteriorReturnForward,
                template,
                entrance,
                player));
        }

        public void EnterBuildingFromDoorTrigger(BuildingDoorWarpTrigger trigger)
        {
            if (trigger == null || _isTransitioning || _isInsideInterior) return;
            if (IsAnyModalUIOpen()) return;

            PlayerController player = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (player == null || player.IsDowned) return;
            if (player.PlayerHealth != null && player.PlayerHealth.IsDead) return;

            InteriorTemplateData template = trigger.ResolveTemplate();

            StartCoroutine(EnterBuildingRoutine(
                trigger.BuildingId,
                trigger.StageX,
                trigger.StageY,
                trigger.InteriorSeed,
                trigger.Theme,
                trigger.BuildingDisplayName,
                trigger.ExteriorReturnPosition,
                trigger.ExteriorReturnForward,
                template,
                fallbackEntrance: null,
                player));
        }

        public void ExitCurrentBuilding(bool immediate = false)
        {
            if (!_isInsideInterior) return;
            if (_isTransitioning && !immediate) return;

            if (immediate)
            {
                StopAllCoroutines();
                _isTransitioning = false;
                PerformExitSwapImmediate();
                _fadeAlpha = 0f;
            }
            else
            {
                StartCoroutine(ExitBuildingRoutine());
            }
        }

        private IEnumerator EnterBuildingRoutine(
            int buildingId,
            int stageX,
            int stageY,
            int interiorSeed,
            InteriorTheme theme,
            string displayName,
            Vector3 returnExteriorPos,
            Vector3 returnExteriorForward,
            InteriorTemplateData template,
            BuildingEntrance fallbackEntrance,
            PlayerController player)
        {
            _isTransitioning = true;
            LootUI.Instance?.HidePrompt();

            yield return FadeRoutine(0f, 1f, 0.14f);

            // 1. Capture return location & building identity
            _returnExteriorPos = returnExteriorPos;
            _returnExteriorForward = returnExteriorForward;
            _currentBuildingId = buildingId;
            _currentInteriorSeed = interiorSeed;
            _currentStageX = stageX;
            _currentStageY = stageY;
            _currentDisplayName = displayName;
            _currentTheme = theme;
            _currentTemplate = template;
            _currentTotalFloors = _currentTemplate != null ? Mathf.Clamp(_currentTemplate.floorCount, 1, 4) : 1;
            _currentFloorIndex = 1;
            _currentMirrorX = (Mathf.Abs(interiorSeed) % 2) == 1;
            _currentInteriorKey = BuildFloorKey(_currentStageX, _currentStageY, _currentBuildingId, _currentFloorIndex);

            // 2. Suspend exterior world & entities cleanly
            SuspendExteriorWorld();

            // 3. Configure pitch-black void camera & cozy interior ambient
            ApplyInteriorCameraAndLighting();

            // 4. Mark active interior state BEFORE generating containers/enemies so NetworkInteriorId & loot keys match!
            _isInsideInterior = true;
            player.NetworkInteriorId = CurrentInteriorId;

            // 5. Build Floor 1 from matched JSON template (or procedural fallback)
            Vector3 spawnPos;
            float roomW;
            float roomD;
            string floorLabel;
            if (_currentTemplate != null)
            {
                BuildFloorFromTemplate(_currentTemplate, _currentFloorIndex, arrivedFromStairs: false, arrivedFromBelow: false, out spawnPos, out roomW, out roomD, out floorLabel);
            }
            else if (fallbackEntrance != null)
            {
                BuildUniqueInteriorRoom(fallbackEntrance, out spawnPos, out roomW, out roomD);
                floorLabel = "FLOOR 1 / 1";
            }
            else
            {
                spawnPos = new Vector3(0f, 0.5f, -3.5f);
                roomW = 14f;
                roomD = 10f;
                floorLabel = "FLOOR 1 / 1";
            }

            // 6. Place player inside doorway facing inward (+Z)
            TeleportPlayerSafely(player, spawnPos, Vector3.forward);

            // 7. Configure CameraFollow for black-void interior framing and snap immediately
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.SetInteriorMode(true, Vector3.zero, roomW * 0.5f, roomD * 0.5f);
                CameraFollow.Instance.SnapToTargetImmediate();
            }

            // 8. Refresh remote co-op players visibility so teammates inside same house/floor appear & outside ones hide
            RefreshAllRemotePlayersVisibility();

            yield return null;
            yield return FadeRoutine(1f, 0f, 0.16f);

            _isTransitioning = false;

            ShowFloorEntryBanner(floorLabel);
        }

        private static string BuildFloorKey(int stageX, int stageY, int buildingId, int floorIndex)
        {
            return $"{stageX}_{stageY}_{buildingId}_F{floorIndex}";
        }

        private void ShowFloorEntryBanner(string floorLabel)
        {
            string statusTag = _currentRoomIsAmbush
                ? (_currentRoomClearedNotified ? "CLEARED" : "DANGER - HOSTILES")
                : "SAFE";
            string title = $"{_currentDisplayName.ToUpperInvariant()}  (F{_currentFloorIndex}/{_currentTotalFloors})";
            string sub = string.IsNullOrWhiteSpace(floorLabel) ? statusTag : $"{floorLabel.ToUpperInvariant()}  |  {statusTag}";
            Color bannerCol = _currentRoomIsAmbush && !_currentRoomClearedNotified
                ? new Color(1f, 0.42f, 0.35f, 1f)
                : new Color(0.45f, 0.96f, 0.65f, 1f);
            ShowBanner(title, sub, bannerCol, 2.5f);
        }

        /// <summary>
        /// Transitions smoothly between floors (1..4) of the currently occupied multi-floor building.
        /// </summary>
        public void TransitionToFloor(int targetFloorIndex, bool arrivedFromBelow)
        {
            if (!_isInsideInterior || _isTransitioning || _currentTemplate == null) return;
            if (IsAnyModalUIOpen()) return;

            int clampedFloor = Mathf.Clamp(targetFloorIndex, 1, _currentTotalFloors);
            if (clampedFloor == _currentFloorIndex) return;

            PlayerController player = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (player == null || player.IsDowned) return;
            if (player.PlayerHealth != null && player.PlayerHealth.IsDead) return;

            StartCoroutine(TransitionFloorRoutine(clampedFloor, arrivedFromBelow, player));
        }

        private IEnumerator TransitionFloorRoutine(int targetFloorIndex, bool arrivedFromBelow, PlayerController player)
        {
            _isTransitioning = true;
            LootUI.Instance?.HidePrompt();

            if (LootUI.HasInstance && LootUI.Instance.IsOpen) LootUI.Instance.Close();
            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen) StorageUI.Instance.Close();

            yield return FadeRoutine(0f, 1f, 0.12f);

            // 1. Persist loot states of the floor we are leaving & clean up current floor entities
            SaveActiveInteriorContainerStates();
            CleanupInteriorEntities();
            if (_interiorRoot != null)
            {
                Destroy(_interiorRoot);
                _interiorRoot = null;
            }
            _activeInteriorContainers.Clear();

            // 2. Switch active floor index & update Co-op interiorId (buildingId * 10 + floorIndex)
            _currentFloorIndex = targetFloorIndex;
            _currentInteriorKey = BuildFloorKey(_currentStageX, _currentStageY, _currentBuildingId, _currentFloorIndex);
            player.NetworkInteriorId = CurrentInteriorId;

            // 3. Build target floor layout from JSON template on the reused CGround tile
            BuildFloorFromTemplate(_currentTemplate, _currentFloorIndex, arrivedFromStairs: true, arrivedFromBelow: arrivedFromBelow,
                out Vector3 spawnPos, out float roomW, out float roomD, out string floorLabel);

            TeleportPlayerSafely(player, spawnPos, Vector3.forward);

            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.SetInteriorMode(true, Vector3.zero, roomW * 0.5f, roomD * 0.5f);
                CameraFollow.Instance.SnapToTargetImmediate();
            }

            RefreshAllRemotePlayersVisibility();

            yield return null;
            yield return FadeRoutine(1f, 0f, 0.14f);

            _isTransitioning = false;
            ShowFloorEntryBanner(floorLabel);
        }

        private IEnumerator ExitBuildingRoutine()
        {
            _isTransitioning = true;
            LootUI.Instance?.HidePrompt();

            // Close any open interior container UI cleanly before destroying room
            if (LootUI.HasInstance && LootUI.Instance.IsOpen) LootUI.Instance.Close();
            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen) StorageUI.Instance.Close();

            yield return FadeRoutine(0f, 1f, 0.14f);

            PerformExitSwapImmediate();

            yield return null;
            yield return FadeRoutine(1f, 0f, 0.16f);

            _isTransitioning = false;
        }

        private void PerformExitSwapImmediate()
        {
            // 1. Save interior container states locally (for Single-Player persistence on re-entry)
            SaveActiveInteriorContainerStates();

            // 2. Clean up interior enemies & ground drops
            CleanupInteriorEntities();

            // 3. Destroy procedural room
            if (_interiorRoot != null)
            {
                Destroy(_interiorRoot);
                _interiorRoot = null;
            }
            _activeInteriorContainers.Clear();

            // 4. Restore exterior state flag
            _isInsideInterior = false;
            _currentBuildingId = 0;
            _currentFloorIndex = 1;
            _currentTotalFloors = 1;
            _currentTemplate = null;
            _currentInteriorSeed = 0;
            _currentInteriorKey = "";
            _currentRoomIsAmbush = false;
            _currentRoomClearedNotified = false;

            PlayerController player = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
            if (player != null)
            {
                player.NetworkInteriorId = 0;
            }

            // 5. Restore exterior camera, lighting & night darkness overlay
            RestoreExteriorCameraAndLighting();

            // 6. Restore exterior world objects, enemies, drops, portals, buildings
            RestoreExteriorWorld();

            // 7. Teleport player right outside the front doorway
            if (player != null)
            {
                TeleportPlayerSafely(player, _returnExteriorPos, _returnExteriorForward);
            }

            // 8. Restore CameraFollow clamping & snap to player
            if (CameraFollow.Instance != null)
            {
                CameraFollow.Instance.SetInteriorMode(false, Vector3.zero, 10f, 6f);
                CameraFollow.Instance.SnapToTargetImmediate();
            }

            // 9. Refresh remote co-op players visibility
            RefreshAllRemotePlayersVisibility();
        }

        private void SuspendExteriorWorld()
        {
            _suspendedExteriorObjects.Clear();
            _suspendedExteriorEnemies.Clear();
            _suspendedExteriorDrops.Clear();
            _suspendedExteriorBuildings.Clear();
            _suspendedExteriorNpcs.Clear();
            _suspendedPortals.Clear();
            _suspendedRenderers.Clear();

            // 1. EnvironmentManager container (all stage buildings, props, trees, background scenery)
            if (EnvironmentManager.Instance != null && EnvironmentManager.Instance.CurrentEnvContainer != null)
            {
                GameObject env = EnvironmentManager.Instance.CurrentEnvContainer;
                if (env.activeSelf)
                {
                    env.SetActive(false);
                    _suspendedExteriorObjects.Add(env);
                }
            }

            // 2. Ground hierarchy ("Grouind" / "Ground"):
            // Reuse the 1 center map tile (CGround) as the interior floor base, and hide ONLY the 8 surrounding tiles!
            _centerGroundTransform = null;
            _centerGroundRenderer = null;
            _hasSavedCenterGround = false;

            GameObject groundObj = GameObject.Find("Grouind") ?? GameObject.Find("Ground");
            if (groundObj != null)
            {
                Transform gTransform = groundObj.transform;
                for (int i = 0; i < gTransform.childCount; i++)
                {
                    Transform child = gTransform.GetChild(i);
                    if (child == null) continue;
                    string upper = child.name.ToUpperInvariant();
                    bool isCenterTile = upper == "CGROUND" || upper == "CENTER" || upper == "CENTERGROUND";
                    if (isCenterTile)
                    {
                        _centerGroundTransform = child;
                        _centerGroundRenderer = child.GetComponent<MeshRenderer>();
                        _savedCenterGroundScale = child.localScale;
                        if (_centerGroundRenderer != null)
                        {
                            _savedCenterGroundMaterial = _centerGroundRenderer.sharedMaterial;
                        }
                        _hasSavedCenterGround = true;
                        if (!child.gameObject.activeSelf) child.gameObject.SetActive(true);
                    }
                    else if (child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(false);
                        _suspendedExteriorObjects.Add(child.gameObject);
                    }
                }
            }

            // 3. LocalStageVisualizer background plane
            LocalStageVisualizer visualizer = FindFirstObjectByType<LocalStageVisualizer>();
            if (visualizer != null && visualizer.BackgroundRenderer != null && visualizer.BackgroundRenderer.enabled)
            {
                visualizer.BackgroundRenderer.enabled = false;
                _suspendedRenderers.Add(visualizer.BackgroundRenderer);
            }

            // 4. World TransitionPortals (disable so player can't trigger stage warp while inside)
            TransitionPortal[] portals = FindObjectsByType<TransitionPortal>(FindObjectsSortMode.None);
            for (int i = 0; i < portals.Length; i++)
            {
                TransitionPortal p = portals[i];
                if (p == null || !TransitionPortal.IsWorldPortal(p)) continue;
                if (p.gameObject.activeSelf)
                {
                    p.gameObject.SetActive(false);
                    _suspendedPortals.Add(p);
                }
            }

            // 5. Exterior Enemies (deactivate & temporarily detach from GameManager.ActiveEnemies)
            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                var active = GameManager.Instance.ActiveEnemies;
                for (int i = active.Count - 1; i >= 0; i--)
                {
                    GameObject enemy = active[i];
                    if (enemy == null) continue;
                    if (enemy.activeSelf)
                    {
                        enemy.SetActive(false);
                        _suspendedExteriorEnemies.Add(enemy);
                    }
                }
                active.Clear();
            }

            // 6. Exterior CollectibleItems on ground
            CollectibleItem[] drops = FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            for (int i = 0; i < drops.Length; i++)
            {
                if (drops[i] != null && drops[i].gameObject.activeSelf)
                {
                    drops[i].gameObject.SetActive(false);
                    _suspendedExteriorDrops.Add(drops[i].gameObject);
                }
            }

            // 7. Player-placed Exterior Buildings
            if (BuildingManager.Instance != null && BuildingManager.Instance.ActiveBuildings != null)
            {
                var placed = BuildingManager.Instance.ActiveBuildings;
                for (int i = 0; i < placed.Count; i++)
                {
                    BuildingObject b = placed[i];
                    if (b != null && b.gameObject.activeSelf)
                    {
                        b.gameObject.SetActive(false);
                        _suspendedExteriorBuildings.Add(b.gameObject);
                    }
                }
            }

            // 8. Exterior NPCs
            NPCController[] npcs = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null && npcs[i].gameObject.activeSelf)
                {
                    npcs[i].gameObject.SetActive(false);
                    _suspendedExteriorNpcs.Add(npcs[i].gameObject);
                }
            }

            // 9. Clear any stray exterior projectiles in flight
            Projectile[] projs = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
            for (int i = 0; i < projs.Length; i++)
            {
                if (projs[i] != null && projs[i].gameObject.activeSelf)
                {
                    if (!string.IsNullOrEmpty(projs[i].PoolKey) && ObjectPoolManager.Instance != null)
                    {
                        ObjectPoolManager.Instance.ReturnToPool(projs[i].PoolKey, projs[i].gameObject);
                    }
                    else
                    {
                        projs[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void RestoreExteriorWorld()
        {
            // Restore the reused center map tile (CGround) scale & biome material first
            if (_hasSavedCenterGround && _centerGroundTransform != null)
            {
                _centerGroundTransform.localScale = _savedCenterGroundScale;
                if (_centerGroundRenderer != null && _savedCenterGroundMaterial != null)
                {
                    _centerGroundRenderer.sharedMaterial = _savedCenterGroundMaterial;
                    _centerGroundRenderer.SetPropertyBlock(null);
                }
                _hasSavedCenterGround = false;
            }

            for (int i = 0; i < _suspendedExteriorObjects.Count; i++)
            {
                if (_suspendedExteriorObjects[i] != null) _suspendedExteriorObjects[i].SetActive(true);
            }
            _suspendedExteriorObjects.Clear();

            // Re-apply biome blending across all 9 ground tiles cleanly
            if (GroundBiomeMaterialController.Instance != null && WorldMapManager.Instance != null)
            {
                GroundBiomeMaterialController.Instance.HandleStageChanged(
                    WorldMapManager.Instance.CurrentPlayerX,
                    WorldMapManager.Instance.CurrentPlayerY);
            }

            for (int i = 0; i < _suspendedRenderers.Count; i++)
            {
                if (_suspendedRenderers[i] != null) _suspendedRenderers[i].enabled = true;
            }
            _suspendedRenderers.Clear();

            for (int i = 0; i < _suspendedPortals.Count; i++)
            {
                if (_suspendedPortals[i] != null) _suspendedPortals[i].gameObject.SetActive(true);
            }
            _suspendedPortals.Clear();

            if (GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                for (int i = 0; i < _suspendedExteriorEnemies.Count; i++)
                {
                    GameObject enemy = _suspendedExteriorEnemies[i];
                    if (enemy == null) continue;
                    Health h = enemy.GetComponent<Health>();
                    if (h != null && h.IsDead) continue;
                    enemy.SetActive(true);
                    if (!GameManager.Instance.ActiveEnemies.Contains(enemy))
                    {
                        GameManager.Instance.ActiveEnemies.Add(enemy);
                    }
                }
            }
            _suspendedExteriorEnemies.Clear();

            for (int i = 0; i < _suspendedExteriorDrops.Count; i++)
            {
                if (_suspendedExteriorDrops[i] != null) _suspendedExteriorDrops[i].SetActive(true);
            }
            _suspendedExteriorDrops.Clear();

            for (int i = 0; i < _suspendedExteriorBuildings.Count; i++)
            {
                if (_suspendedExteriorBuildings[i] != null) _suspendedExteriorBuildings[i].SetActive(true);
            }
            _suspendedExteriorBuildings.Clear();

            for (int i = 0; i < _suspendedExteriorNpcs.Count; i++)
            {
                if (_suspendedExteriorNpcs[i] != null) _suspendedExteriorNpcs[i].SetActive(true);
            }
            _suspendedExteriorNpcs.Clear();
        }

        private void ApplyInteriorCameraAndLighting()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                _savedClearFlags = cam.clearFlags;
                _savedBgColor = cam.backgroundColor;
                _hasSavedCameraState = true;

                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            _savedFog = RenderSettings.fog;
            _savedAmbientLight = RenderSettings.ambientLight;
            RenderSettings.fog = false;
            RenderSettings.ambientLight = new Color(0.32f, 0.30f, 0.33f, 1f);

            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.InteriorOverrideActive = true;
            }
        }

        private void RestoreExteriorCameraAndLighting()
        {
            Camera cam = Camera.main;
            if (cam != null && _hasSavedCameraState)
            {
                cam.clearFlags = _savedClearFlags;
                cam.backgroundColor = _savedBgColor;
            }
            _hasSavedCameraState = false;

            RenderSettings.fog = _savedFog;
            RenderSettings.ambientLight = _savedAmbientLight;

            if (NightDarknessController.Instance != null)
            {
                NightDarknessController.Instance.InteriorOverrideActive = false;
            }
        }

        private void CleanupInteriorEntities()
        {
            for (int i = 0; i < _activeInteriorEnemies.Count; i++)
            {
                GameObject e = _activeInteriorEnemies[i];
                if (e == null) continue;
                GameManager.Instance?.ActiveEnemies?.Remove(e);

                BaseEnemyAI ai = e.GetComponent<BaseEnemyAI>();
                string poolKey = ai != null ? ai.PoolKey : "";
                if (!string.IsNullOrEmpty(poolKey) && ObjectPoolManager.Instance != null)
                {
                    Health h = e.GetComponent<Health>();
                    if (h != null) h.ResetHealth();
                    ObjectPoolManager.Instance.ReturnToPool(poolKey, e);
                }
                else
                {
                    Destroy(e);
                }
            }
            _activeInteriorEnemies.Clear();

            // Also clean any client puppet enemies that were active inside
            if (EmpireNetworkManager.IsOnlineClient && GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                for (int i = GameManager.Instance.ActiveEnemies.Count - 1; i >= 0; i--)
                {
                    GameObject e = GameManager.Instance.ActiveEnemies[i];
                    if (e == null) continue;
                    BaseEnemyAI ai = e.GetComponent<BaseEnemyAI>();
                    if (ai != null && ai.IsNetworkPuppet)
                    {
                        ai.IsNetworkPuppet = false;
                        if (!string.IsNullOrEmpty(ai.PoolKey) && ObjectPoolManager.Instance != null)
                        {
                            ObjectPoolManager.Instance.ReturnToPool(ai.PoolKey, e);
                        }
                        else
                        {
                            Destroy(e);
                        }
                    }
                }
                GameManager.Instance.ActiveEnemies.Clear();
            }

            // Destroy uncollected interior ground drops so they don't float outside
            CollectibleItem[] interiorDrops = FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            for (int i = 0; i < interiorDrops.Length; i++)
            {
                if (interiorDrops[i] != null && interiorDrops[i].gameObject.activeInHierarchy)
                {
                    Destroy(interiorDrops[i].gameObject);
                }
            }
        }

        private void SaveActiveInteriorContainerStates()
        {
            if (string.IsNullOrEmpty(_currentInteriorKey)) return;
            for (int i = 0; i < _activeInteriorContainers.Count; i++)
            {
                LootContainer lc = _activeInteriorContainers[i];
                if (lc == null) continue;
                string key = BuildLocalContainerKey(_currentInteriorKey, lc.transform.position);
                var rec = new InteriorSavedContainerRecord
                {
                    containerKey = key,
                    remainingMoney = lc.moneyAmount,
                    isSearched = lc.isSearched || (lc.moneyAmount <= 0 && (lc.itemsInside == null || lc.itemsInside.Count == 0))
                };
                if (lc.itemsInside != null)
                {
                    for (int s = 0; s < lc.itemsInside.Count; s++)
                    {
                        rec.remainingItems.Add(new InteriorSavedLootSlot
                        {
                            itemName = lc.itemsInside[s].itemName,
                            quantity = lc.itemsInside[s].quantity
                        });
                    }
                }
                _localContainerStates[key] = rec;
            }
            PersistStateToJson();
        }

        private static string BuildLocalContainerKey(string interiorKey, Vector3 pos)
        {
            return $"{interiorKey}_{Mathf.RoundToInt(pos.x * 2f)}_{Mathf.RoundToInt(pos.z * 2f)}";
        }

        private static void TeleportPlayerSafely(PlayerController player, Vector3 targetPos, Vector3 forwardDir)
        {
            if (player == null) return;
            if (player.IsSitting) player.SetSitting(false);

            Physics.SyncTransforms();
            Vector3 clearPos = ResolveClearGroundSpawn(targetPos, player);

            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = clearPos;
            }
            player.transform.position = clearPos;

            Vector3 flatFwd = new Vector3(forwardDir.x, 0f, forwardDir.z);
            if (flatFwd.sqrMagnitude > 0.001f)
            {
                player.transform.forward = flatFwd.normalized;
            }
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Ensures the teleport target sits flush on the floor (y = 0.05) and never overlaps any solid
        /// furniture/wall collider so Unity PhysX never launches the player upward on arrival!
        /// </summary>
        private static Vector3 ResolveClearGroundSpawn(Vector3 desiredPos, PlayerController player)
        {
            const float groundY = 0.05f;
            Vector3 basePos = new Vector3(desiredPos.x, groundY, desiredPos.z);
            if (!IsPositionBlockedBySolidObstacle(basePos, player))
            {
                return basePos;
            }

            // Step progressively toward room center (0, 0, 0) and around small spiral offsets until completely clear
            Vector2 toCenter = new Vector2(-basePos.x, -basePos.z);
            if (toCenter.sqrMagnitude < 0.01f) toCenter = Vector2.up;
            toCenter.Normalize();
            Vector2 perp = new Vector2(-toCenter.y, toCenter.x);

            float[] forwardSteps = { 0.8f, 1.5f, 2.2f, 3.0f, 3.8f };
            float[] sideSteps = { 0f, -0.9f, 0.9f, -1.7f, 1.7f };

            for (int fi = 0; fi < forwardSteps.Length; fi++)
            {
                for (int si = 0; si < sideSteps.Length; si++)
                {
                    Vector2 offset = toCenter * forwardSteps[fi] + perp * sideSteps[si];
                    Vector3 cand = new Vector3(basePos.x + offset.x, groundY, basePos.z + offset.y);
                    if (!IsPositionBlockedBySolidObstacle(cand, player))
                    {
                        return cand;
                    }
                }
            }

            return new Vector3(0f, groundY, 0f);
        }

        private static bool IsPositionBlockedBySolidObstacle(Vector3 pos, PlayerController player)
        {
            Collider[] hits = Physics.OverlapSphere(new Vector3(pos.x, 0.65f, pos.z), 0.42f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i];
                if (col == null) continue;
                if (player != null && (col.transform == player.transform || col.transform.IsChildOf(player.transform))) continue;

                string n = col.gameObject.name.ToLowerInvariant();
                if (n.Contains("ground") || n.Contains("floor") || n.Contains("rug") || n.Contains("street") || n.Contains("road"))
                    continue;
                if (col.GetComponentInParent<BaseEnemyAI>() != null) continue;

                return true;
            }
            return false;
        }

        private static void RefreshAllRemotePlayersVisibility()
        {
            var players = PlayerRegistry.AllPlayers;
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].IsRemotePlayer)
                {
                    players[i].RefreshStageVisibility();
                }
            }
        }

        private bool IsHostInSameInterior()
        {
            PlayerController host = PlayerRegistry.GetPlayerByClientId(0);
            if (host == null) return false;
            return host.NetworkStageX == _currentStageX &&
                   host.NetworkStageY == _currentStageY &&
                   host.NetworkInteriorId == CurrentInteriorId;
        }

        // =========================================================================
        //  JSON MULTI-FLOOR INTERIOR BUILDER (Uses Existing Building Prefabs!)
        // =========================================================================

        private void BuildFloorFromTemplate(
            InteriorTemplateData template,
            int floorIndex,
            bool arrivedFromStairs,
            bool arrivedFromBelow,
            out Vector3 playerSpawnPos,
            out float roomWidth,
            out float roomDepth,
            out string floorLabel)
        {
            if (_interiorRoot != null)
            {
                Destroy(_interiorRoot);
            }

            InteriorFloorData floor = template.GetFloor(floorIndex);
            InteriorTheme theme = template.GetThemeEnum();
            _currentTheme = theme;

            _interiorRoot = new GameObject($"ActiveInterior_{template.templateId}_F{floorIndex}");
            _interiorRoot.transform.position = Vector3.zero;
            _activeInteriorContainers.Clear();

            int floorSeed = unchecked(_currentInteriorSeed ^ (floorIndex * 73856093));
            System.Random rng = new System.Random(floorSeed);

            roomWidth = Mathf.Clamp(floor.width, 10f, 24f);
            roomDepth = Mathf.Clamp(floor.depth, 8f, 16f);
            float wallHeight = Mathf.Clamp(floor.wallHeight, 2.4f, 4.0f);
            float wallThick = 0.35f;
            float doorWidth = 2.15f;
            floorLabel = floor.floorName ?? $"Floor {floorIndex}";

            float mirrorSign = _currentMirrorX ? -1f : 1f;
            float halfW = roomWidth * 0.5f;
            float halfD = roomDepth * 0.5f;

            // Parse floor hex palette + apply subtle per-house seed tint so every house feels unique
            float hueJitter = (float)(rng.NextDouble() * 0.04 - 0.02);
            float valJitter = (float)(rng.NextDouble() * 0.06 - 0.03);
            Color cFloorA = JitterColor(ParseHexColor(floor.floorColorHex, new Color(0.42f, 0.30f, 0.20f)), hueJitter, valJitter);
            Color cFloorB = cFloorA * 0.86f; cFloorB.a = 1f;
            Color cWall = JitterColor(ParseHexColor(floor.wallColorHex, new Color(0.75f, 0.72f, 0.64f)), hueJitter, valJitter);
            Color cTrim = ParseHexColor(floor.trimColorHex, new Color(0.22f, 0.17f, 0.13f));
            Color cAccent = ParseHexColor(floor.accentColorHex, new Color(0.65f, 0.28f, 0.25f));
            Color cLight = theme switch
            {
                InteriorTheme.Clinic => new Color(0.86f, 0.97f, 1.0f),
                InteriorTheme.CornerShop => new Color(0.96f, 0.96f, 0.88f),
                InteriorTheme.Warehouse => new Color(1.0f, 0.82f, 0.56f),
                _ => new Color(1.0f, 0.88f, 0.68f)
            };

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material floorMatA = CreateMat(litShader, cFloorA, 0.22f);
            Material floorMatB = CreateMat(litShader, cFloorB, 0.20f);
            Material wallMat = CreateMat(litShader, cWall, 0.15f);
            Material trimMat = CreateMat(litShader, cTrim, 0.25f);
            Material accentMat = CreateMat(litShader, cAccent, 0.30f);

            EnsurePersistenceLoaded();
            _visitedInteriorKeys.Add(_currentInteriorKey);
            PersistStateToJson();

            // 1. Reuse existing CGround tile (single map!) + theme floor pattern
            BuildFloorAndPattern(_interiorRoot.transform, theme, roomWidth, roomDepth, floorMatA, floorMatB, accentMat, rng);

            // 2. Back Wall (+Z): solid interior wall (with 3 live Day/Night windows built below!)
            CreateSolidBox(_interiorRoot.transform, "Wall_Back",
                new Vector3(0f, wallHeight * 0.5f, halfD + wallThick * 0.5f),
                new Vector3(roomWidth + wallThick * 2f, wallHeight, wallThick), wallMat, true);
            CreateSolidBox(_interiorRoot.transform, "Wall_BackCap",
                new Vector3(0f, wallHeight + 0.05f, halfD + wallThick * 0.5f),
                new Vector3(roomWidth + wallThick * 2.15f, 0.10f, wallThick + 0.08f), trimMat, false);

            // 3. Three-Slot Doorway System around the room (Slot 0 = Left -X, Slot 1 = Front -Z, Slot 2 = Right +X):
            //    - Floor 1: Pick 1 of 3 slots as the active Main Exit Doorway; if Floor 2 exists, use 1 of the remaining 2 slots for Stairs Up!
            //    - Upper Floors (2..4): Use 1 slot for Stairs Down, and (if higher floor exists) 1 of the remaining 2 slots for Stairs Up!
            int exitSlot = -1;
            int stairsUpSlot = -1;
            int stairsDownSlot = -1;

            int baseSlot = Mathf.Abs(_currentInteriorSeed) % 3; // 0=Left, 1=Front, 2=Right
            int secondOffset = 1 + (Mathf.Abs(_currentInteriorSeed / 3) % 2); // 1 or 2

            if (floorIndex == 1)
            {
                exitSlot = baseSlot;
                if (floor.hasStairsUp && floorIndex < _currentTotalFloors)
                {
                    stairsUpSlot = (exitSlot + secondOffset) % 3;
                }
            }
            else
            {
                // Arrive on the same-side or opposite edge cleanly connected to the floor below
                int prevUpSlot = (baseSlot + ((floorIndex - 1) * secondOffset)) % 3;
                stairsDownSlot = prevUpSlot;
                if (floor.hasStairsUp && floorIndex < _currentTotalFloors)
                {
                    stairsUpSlot = (stairsDownSlot + secondOffset) % 3;
                }
            }

            bool leftHasPortal = (exitSlot == 0 || stairsUpSlot == 0 || stairsDownSlot == 0);
            bool frontHasPortal = (exitSlot == 1 || stairsUpSlot == 1 || stairsDownSlot == 1);
            bool rightHasPortal = (exitSlot == 2 || stairsUpSlot == 2 || stairsDownSlot == 2);

            float frontWallHeight = 0.52f;
            float sideDoorZ = 0f; // Centered along Left/Right walls (per sketch)
            float frontDoorX = 0f;

            // Left Wall (-X): split cleanly if Slot 0 is an active Door/Stairs
            BuildSideWallWithOptionalOpening(_interiorRoot.transform, "Wall_Left", isLeft: true, halfW, halfD, wallHeight, wallThick, doorWidth, sideDoorZ, leftHasPortal, wallMat, trimMat);

            // Right Wall (+X): split cleanly if Slot 2 is an active Door/Stairs
            BuildSideWallWithOptionalOpening(_interiorRoot.transform, "Wall_Right", isLeft: false, halfW, halfD, wallHeight, wallThick, doorWidth, sideDoorZ, rightHasPortal, wallMat, trimMat);

            // Front Cutaway Sill (-Z): split cleanly if Slot 1 is an active Door/Stairs
            if (frontHasPortal)
            {
                float leftEdge = -halfW - wallThick;
                float rightEdge = halfW + wallThick;
                float dLeft = frontDoorX - doorWidth * 0.5f;
                float dRight = frontDoorX + doorWidth * 0.5f;

                float leftSegW = dLeft - leftEdge;
                if (leftSegW > 0.15f)
                {
                    CreateSolidBox(_interiorRoot.transform, "Wall_FrontLeft",
                        new Vector3(leftEdge + leftSegW * 0.5f, frontWallHeight * 0.5f, -halfD - wallThick * 0.5f),
                        new Vector3(leftSegW, frontWallHeight, wallThick), trimMat, true);
                }

                float rightSegW = rightEdge - dRight;
                if (rightSegW > 0.15f)
                {
                    CreateSolidBox(_interiorRoot.transform, "Wall_FrontRight",
                        new Vector3(dRight + rightSegW * 0.5f, frontWallHeight * 0.5f, -halfD - wallThick * 0.5f),
                        new Vector3(rightSegW, frontWallHeight, wallThick), trimMat, true);
                }

                GameObject frontBackstop = new GameObject("FrontSlotVoidBackstop");
                frontBackstop.transform.SetParent(_interiorRoot.transform, false);
                frontBackstop.transform.position = new Vector3(frontDoorX, 1.2f, -halfD - 0.50f);
                BoxCollider fbCol = frontBackstop.AddComponent<BoxCollider>();
                fbCol.size = new Vector3(doorWidth + 0.6f, 2.4f, 0.35f);
            }
            else
            {
                CreateSolidBox(_interiorRoot.transform, "Wall_FrontFull",
                    new Vector3(0f, frontWallHeight * 0.5f, -halfD - wallThick * 0.5f),
                    new Vector3(roomWidth + wallThick * 2f, frontWallHeight, wallThick), trimMat, true);
            }

            // Instantiate active Portals at their assigned slots (1 of 3 for Exit, others for Stairs Up/Down)
            Vector3 exitPos = Vector3.zero;
            Vector3 exitInward = Vector3.forward;
            if (exitSlot >= 0)
            {
                GetSlotTransform(exitSlot, halfW, halfD, frontDoorX, sideDoorZ, out exitPos, out Quaternion exitRot, out exitInward);
                GameObject exitObj = new GameObject("BuildingExitPortal");
                exitObj.transform.SetParent(_interiorRoot.transform, false);
                exitObj.transform.position = exitPos;
                exitObj.transform.rotation = exitRot;
                BuildingExitPortal exitPortal = exitObj.AddComponent<BuildingExitPortal>();
                exitPortal.BuildExitVisual(doorWidth);
            }

            Vector3 stairsUpPos = Vector3.zero;
            Vector3 stairsUpInward = Vector3.forward;
            if (stairsUpSlot >= 0)
            {
                GetSlotTransform(stairsUpSlot, halfW, halfD, frontDoorX, sideDoorZ, out stairsUpPos, out Quaternion upRot, out stairsUpInward);
                GameObject upObj = new GameObject($"StairsUp_ToF{floorIndex + 1}");
                upObj.transform.SetParent(_interiorRoot.transform, false);
                upObj.transform.position = stairsUpPos + stairsUpInward * 0.45f;
                upObj.transform.rotation = upRot;
                BuildingStairsPortal upPortal = upObj.AddComponent<BuildingStairsPortal>();
                upPortal.Configure(goingUp: true, targetFloorIndex: floorIndex + 1);
            }

            Vector3 stairsDownPos = Vector3.zero;
            Vector3 stairsDownInward = Vector3.forward;
            if (stairsDownSlot >= 0)
            {
                GetSlotTransform(stairsDownSlot, halfW, halfD, frontDoorX, sideDoorZ, out stairsDownPos, out Quaternion downRot, out stairsDownInward);
                GameObject downObj = new GameObject($"StairsDown_ToF{floorIndex - 1}");
                downObj.transform.SetParent(_interiorRoot.transform, false);
                downObj.transform.position = stairsDownPos + stairsDownInward * 0.45f;
                downObj.transform.rotation = downRot;
                BuildingStairsPortal downPortal = downObj.AddComponent<BuildingStairsPortal>();
                downPortal.Configure(goingUp: false, targetFloorIndex: floorIndex - 1);
            }

            // Determine player arrival spawn point cleanly just inside the active door/stairs slot
            if (!arrivedFromStairs && exitSlot >= 0)
            {
                playerSpawnPos = new Vector3(exitPos.x + exitInward.x * 1.45f, 0.5f, exitPos.z + exitInward.z * 1.45f);
            }
            else if (arrivedFromBelow && stairsDownSlot >= 0)
            {
                playerSpawnPos = new Vector3(stairsDownPos.x + stairsDownInward.x * 1.75f, 0.5f, stairsDownPos.z + stairsDownInward.z * 1.75f);
            }
            else if (!arrivedFromBelow && stairsUpSlot >= 0)
            {
                playerSpawnPos = new Vector3(stairsUpPos.x + stairsUpInward.x * 1.75f, 0.5f, stairsUpPos.z + stairsUpInward.z * 1.75f);
            }
            else
            {
                playerSpawnPos = new Vector3(0f, 0.5f, -halfD + 1.8f);
            }

            // 4. Wall Details (Windows, Art, Clinic Cross)
            BuildWallDetails(_interiorRoot.transform, theme, roomWidth, roomDepth, wallHeight, litShader, accentMat, trimMat, rng);

            // 5. Spawn Existing Building Prefabs defined in JSON floor.objects[]
            //    Skip any solid furniture that would block an active Exit Doorway, Stairs Up, Stairs Down, or Player Spawn!
            List<Vector2> protectedDoorZones = new List<Vector2>();
            if (exitSlot >= 0) protectedDoorZones.Add(new Vector2(exitPos.x, exitPos.z));
            if (stairsUpSlot >= 0) protectedDoorZones.Add(new Vector2(stairsUpPos.x, stairsUpPos.z));
            if (stairsDownSlot >= 0) protectedDoorZones.Add(new Vector2(stairsDownPos.x, stairsDownPos.z));
            protectedDoorZones.Add(new Vector2(playerSpawnPos.x, playerSpawnPos.z));

            if (floor.objects != null)
            {
                for (int i = 0; i < floor.objects.Count; i++)
                {
                    InteriorPlacedObjectData objData = floor.objects[i];
                    if (objData == null || string.IsNullOrWhiteSpace(objData.buildingId)) continue;

                    float ox = Mathf.Clamp(objData.x * mirrorSign, -halfW + 0.7f, halfW - 0.7f);
                    float oz = Mathf.Clamp(objData.z, -halfD + 0.7f, halfD - 0.7f);

                    // Flat walkable rugs never block doors; solid furniture must stay clear of all active doors/stairs!
                    bool isWalkableRug = objData.buildingId.IndexOf("Rug", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isWalkableRug)
                    {
                        bool blocksDoorOrStairs = false;
                        Vector2 objXZ = new Vector2(ox, oz);
                        for (int z = 0; z < protectedDoorZones.Count; z++)
                        {
                            if ((objXZ - protectedDoorZones[z]).sqrMagnitude < 2.65f * 2.65f)
                            {
                                blocksDoorOrStairs = true;
                                break;
                            }
                        }
                        if (blocksDoorOrStairs) continue;
                    }

                    float rotY = _currentMirrorX ? (360f - objData.rotY) % 360f : objData.rotY;
                    float scl = objData.scale > 0.05f ? objData.scale : 1f;

                    GameObject spawned = SpawnInteriorBuildingObject(_interiorRoot.transform, objData.buildingId, new Vector3(ox, objData.y, oz), rotY, scl, litShader, cAccent);
                    if (spawned != null && objData.isLootable)
                    {
                        string cName = !string.IsNullOrWhiteSpace(objData.containerName) ? objData.containerName : "Supply Crate";
                        ConfigureJsonLootContainer(spawned, theme, cName, floorIndex, rng);
                    }
                }
            }

            // 6. Cozy Interior Lighting
            BuildInteriorLights(_interiorRoot.transform, roomWidth, roomDepth, wallHeight, cLight);

            // 7. Mixed Safe vs Ambush Encounters per floor from JSON enemySpawnPoints[]
            SpawnJsonFloorEncounter(template, floor, floorIndex, mirrorSign, halfW, halfD, playerSpawnPos, rng);
        }

        private GameObject SpawnInteriorBuildingObject(Transform parent, string buildingId, Vector3 pos, float rotY, float scale, Shader fallbackShader, Color accentColor)
        {
            GameObject prefab = null;
            Vector3 posOffset = Vector3.zero;
            Vector3 rotOffset = Vector3.zero;

            // 1. Resolve from existing BuildingManager BuildingData database first!
            if (BuildingManager.Instance != null)
            {
                BuildingData bData = BuildingManager.Instance.GetBuildingDataByName(buildingId);
                if (bData != null && bData.prefab != null)
                {
                    prefab = bData.prefab;
                    posOffset = bData.positionOffset;
                    rotOffset = bData.rotationOffset;
                }
            }

            // 2. Fallback: load directly from Addressables (Prefabs/Furniture or Building) or Resources
            if (prefab == null)
            {
                prefab = AddressableManager.LoadAsset<GameObject>($"Prefabs/Furniture/{buildingId}")
                      ?? AddressableManager.LoadAsset<GameObject>($"Prefabs/Building/{buildingId}")
                      ?? AddressableManager.LoadAsset<GameObject>($"Prefabs/Environment/Building/{buildingId}")
                      ?? AddressableManager.LoadAsset<GameObject>($"Prefabs/Environment/{buildingId}")
                      ?? Resources.Load<GameObject>($"Prefabs/Furniture/{buildingId}");
            }

            GameObject instance;
            if (prefab != null)
            {
                Quaternion rot = Quaternion.Euler(0f, rotY, 0f) * Quaternion.Euler(rotOffset);
                instance = Instantiate(prefab, pos + posOffset, rot, parent);
                instance.name = $"Interior_{buildingId}";
                if (Mathf.Abs(scale - 1f) > 0.01f)
                {
                    instance.transform.localScale *= scale;
                }

                // Strip exterior player-building & health trackers so interior Building prefabs don't register
                // as player-owned exterior structures or break from stray bullets inside the room!
                SanitizeInteriorBuildingInstance(instance);
            }
            else
            {
                // Safe procedural fallback cube if prefab isn't loaded in a bare test scene
                Material m = CreateMat(fallbackShader, accentColor * 0.85f, 0.25f);
                instance = CreateSolidBox(parent, $"InteriorFallback_{buildingId}",
                    new Vector3(pos.x, 0.55f, pos.z),
                    new Vector3(1.2f * scale, 1.1f * scale, 1.2f * scale), m, true);
                instance.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            }

            return instance;
        }

        private static void SanitizeInteriorBuildingInstance(GameObject instance)
        {
            if (instance == null) return;

            // Remove exterior storage-crate / health wrappers so interior props stay cleanly isolated
            // while preserving interactive furniture (Workbench, Bedroll, Chair, Radio, Lamp, CropPlanter, Utility)!
            StorageCrateBuilding[] crates = instance.GetComponentsInChildren<StorageCrateBuilding>(true);
            for (int i = 0; i < crates.Length; i++) Destroy(crates[i]);

            BuildingHealthDisplay[] hpDisplays = instance.GetComponentsInChildren<BuildingHealthDisplay>(true);
            for (int i = 0; i < hpDisplays.Length; i++) Destroy(hpDisplays[i]);

            // Disable BuildingObject instead of destroying when another component has [RequireComponent(typeof(BuildingObject))]
            BuildingObject[] bObjs = instance.GetComponentsInChildren<BuildingObject>(true);
            for (int i = 0; i < bObjs.Length; i++)
            {
                if (bObjs[i] == null) continue;
                bool requiredBySibling = bObjs[i].GetComponent<WorkbenchBuilding>() != null ||
                                         bObjs[i].GetComponent<BedrollBuilding>() != null;
                if (requiredBySibling)
                {
                    bObjs[i].enabled = false;
                }
                else
                {
                    Destroy(bObjs[i]);
                }
            }

            Health[] healths = instance.GetComponentsInChildren<Health>(true);
            for (int i = 0; i < healths.Length; i++) Destroy(healths[i]);

            // Remove pre-baked LootContainer so we configure a fresh deterministic interior LootContainer cleanly
            LootContainer[] oldContainers = instance.GetComponentsInChildren<LootContainer>(true);
            for (int i = 0; i < oldContainers.Length; i++) Destroy(oldContainers[i]);
        }

        private void ConfigureJsonLootContainer(GameObject prop, InteriorTheme theme, string displayName, int floorIndex, System.Random rng)
        {
            LootContainer lc = prop.AddComponent<LootContainer>();
            lc.randomizeLoot = false;
            lc.interactionRadius = 2.35f;
            lc.containerName = displayName;
            lc.itemsInside.Clear();

            // Higher floors yield richer cash & slightly more loot rolls!
            float floorBonus = 1f + (floorIndex - 1) * 0.22f;
            int baseMinCash = theme == InteriorTheme.CornerShop ? 15 : 8;
            int baseMaxCash = theme == InteriorTheme.CornerShop ? 75 : (theme == InteriorTheme.Warehouse ? 58 : 48);
            lc.moneyAmount = Mathf.RoundToInt(rng.Next(baseMinCash, baseMaxCash) * floorBonus);

            int itemRolls = rng.Next(2, 4 + Mathf.Min(2, floorIndex));
            bool rolledAmmo = false;

            for (int r = 0; r < itemRolls; r++)
            {
                PickThemeLootItem(theme, rng, out string itemName, out int qty);
                if (string.IsNullOrEmpty(itemName) || qty <= 0) continue;

                if (string.Equals(itemName, "Ammo", StringComparison.OrdinalIgnoreCase))
                {
                    if (rolledAmmo) continue;
                    rolledAmmo = true;
                    qty = rng.Next(1, 4);
                }

                lc.AddItem(itemName, qty);
            }

            string localKey = BuildLocalContainerKey(_currentInteriorKey, prop.transform.position);
            if (_localContainerStates.TryGetValue(localKey, out InteriorSavedContainerRecord saved))
            {
                lc.moneyAmount = saved.remainingMoney;
                lc.itemsInside.Clear();
                if (saved.remainingItems != null)
                {
                    for (int s = 0; s < saved.remainingItems.Count; s++)
                    {
                        lc.itemsInside.Add(new LootSlot(saved.remainingItems[s].itemName, saved.remainingItems[s].quantity));
                    }
                }
                lc.isSearched = saved.isSearched || (lc.moneyAmount <= 0 && lc.itemsInside.Count == 0);
            }

            NetworkLootManager.Instance?.ApplyCachedStateToContainer(lc);
            _activeInteriorContainers.Add(lc);
        }

        private static void GetSlotTransform(int slot, float halfW, float halfD, float frontDoorX, float sideDoorZ, out Vector3 pos, out Quaternion rot, out Vector3 inwardDir)
        {
            if (slot == 0) // Left (-X)
            {
                pos = new Vector3(-halfW + 0.15f, 0f, sideDoorZ);
                rot = Quaternion.Euler(0f, 90f, 0f);
                inwardDir = Vector3.right;
            }
            else if (slot == 2) // Right (+X)
            {
                pos = new Vector3(halfW - 0.15f, 0f, sideDoorZ);
                rot = Quaternion.Euler(0f, -90f, 0f);
                inwardDir = Vector3.left;
            }
            else // Front (-Z)
            {
                pos = new Vector3(frontDoorX, 0f, -halfD + 0.15f);
                rot = Quaternion.identity;
                inwardDir = Vector3.forward;
            }
        }

        private static void BuildSideWallWithOptionalOpening(
            Transform parent,
            string wallName,
            bool isLeft,
            float halfW,
            float halfD,
            float wallHeight,
            float wallThick,
            float doorWidth,
            float doorZ,
            bool hasOpening,
            Material wallMat,
            Material trimMat)
        {
            float wx = isLeft ? (-halfW - wallThick * 0.5f) : (halfW + wallThick * 0.5f);

            if (!hasOpening)
            {
                CreateSolidBox(parent, wallName, new Vector3(wx, wallHeight * 0.5f, 0f), new Vector3(wallThick, wallHeight, halfD * 2f), wallMat, true);
                CreateSolidBox(parent, wallName + "_Cap", new Vector3(wx, wallHeight + 0.05f, 0f), new Vector3(wallThick + 0.08f, 0.10f, halfD * 2f + wallThick * 2f), trimMat, false);
                return;
            }

            float doorMinZ = doorZ - doorWidth * 0.5f;
            float doorMaxZ = doorZ + doorWidth * 0.5f;

            // Back segment (doorMaxZ -> halfD)
            float backLen = halfD - doorMaxZ;
            if (backLen > 0.15f)
            {
                CreateSolidBox(parent, wallName + "_BackSeg", new Vector3(wx, wallHeight * 0.5f, doorMaxZ + backLen * 0.5f), new Vector3(wallThick, wallHeight, backLen), wallMat, true);
                CreateSolidBox(parent, wallName + "_BackCap", new Vector3(wx, wallHeight + 0.05f, doorMaxZ + backLen * 0.5f), new Vector3(wallThick + 0.08f, 0.10f, backLen), trimMat, false);
            }

            // Front segment (-halfD -> doorMinZ)
            float frontLen = doorMinZ - (-halfD);
            if (frontLen > 0.15f)
            {
                CreateSolidBox(parent, wallName + "_FrontSeg", new Vector3(wx, wallHeight * 0.5f, -halfD + frontLen * 0.5f), new Vector3(wallThick, wallHeight, frontLen), wallMat, true);
                CreateSolidBox(parent, wallName + "_FrontCap", new Vector3(wx, wallHeight + 0.05f, -halfD + frontLen * 0.5f), new Vector3(wallThick + 0.08f, 0.10f, frontLen), trimMat, false);
            }

            // Doorway lintel header above opening
            float lintelH = Mathf.Max(0.35f, wallHeight - 2.15f);
            CreateSolidBox(parent, wallName + "_Lintel", new Vector3(wx, wallHeight - lintelH * 0.5f, doorZ), new Vector3(wallThick + 0.08f, lintelH, doorWidth + 0.2f), trimMat, false);

            // Invisible void backstop behind side doorway opening so player cannot walk into black void
            GameObject backstop = new GameObject(wallName + "_VoidBackstop");
            backstop.transform.SetParent(parent, false);
            backstop.transform.position = new Vector3(wx + (isLeft ? -0.35f : 0.35f), 1.2f, doorZ);
            BoxCollider bc = backstop.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.35f, 2.4f, doorWidth + 0.6f);
        }

        private void SpawnJsonFloorEncounter(
            InteriorTemplateData template,
            InteriorFloorData floor,
            int floorIndex,
            float mirrorSign,
            float halfW,
            float halfD,
            Vector3 playerSpawnPos,
            System.Random rng)
        {
            _activeInteriorEnemies.Clear();
            _currentRoomIsAmbush = false;
            _currentRoomClearedNotified = false;

            bool isSpecialSafeStage = false;
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                StageData st = WorldMapManager.Instance.MapGenerator.GetStage(_currentStageX, _currentStageY);
                if (st != null && st.biome == BiomeType.SpecialEvent) isSpecialSafeStage = true;
            }

            float chance = Mathf.Clamp01(template.ambushChance + (floorIndex - 1) * 0.08f);
            bool isAmbush = !isSpecialSafeStage && floor.enemySpawnPoints != null && floor.enemySpawnPoints.Count > 0 && rng.NextDouble() < chance;
            _currentRoomIsAmbush = isAmbush;

            if (!isAmbush) return;

            if (_clearedInteriorKeys.Contains(_currentInteriorKey))
            {
                _currentRoomClearedNotified = true;
                return;
            }

            if (EmpireNetworkManager.IsOnlineClient && IsHostInSameInterior())
            {
                return;
            }

            if (GameManager.Instance == null) return;

            string[] fallbackKeys = { "ZombieNormal", "ZombieRunner", "ZombieCrawler", "ZombieSpitter" };

            for (int i = 0; i < floor.enemySpawnPoints.Count; i++)
            {
                InteriorEnemySpawnData sp = floor.enemySpawnPoints[i];
                if (sp == null) continue;

                float zx = Mathf.Clamp(sp.x * mirrorSign, -halfW + 1.1f, halfW - 1.1f);
                float zz = Mathf.Clamp(sp.z, -halfD + 1.1f, halfD - 1.1f);

                // Keep safe buffer from player entry/stairs spawn
                if ((new Vector2(zx - playerSpawnPos.x, zz - playerSpawnPos.z)).sqrMagnitude < 2.8f * 2.8f)
                {
                    Vector2 push = new Vector2(zx - playerSpawnPos.x, zz - playerSpawnPos.z);
                    if (push.sqrMagnitude < 0.01f) push = Vector2.up;
                    push.Normalize();
                    zx = Mathf.Clamp(playerSpawnPos.x + push.x * 3.2f, -halfW + 1.1f, halfW - 1.1f);
                    zz = Mathf.Clamp(playerSpawnPos.z + push.y * 3.2f, -halfD + 1.1f, halfD - 1.1f);
                }

                string chosenName = !string.IsNullOrWhiteSpace(sp.preferredEnemy)
                    ? sp.preferredEnemy
                    : fallbackKeys[rng.Next(fallbackKeys.Length)];
                GameObject prefab = GameManager.Instance.GetPrefabByName(chosenName) ?? GameManager.Instance.DefaultEnemyPrefab;
                if (prefab == null) continue;

                string poolKey = GameManager.Instance.GetPoolKeyForPrefab(prefab);
                Quaternion rot = Quaternion.Euler(0f, (float)rng.Next(0, 360), 0f);
                GameObject enemy = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, new Vector3(zx, 0.25f, zz), rot);
                if (enemy != null)
                {
                    _activeInteriorEnemies.Add(enemy);
                }
            }

            if (_activeInteriorEnemies.Count == 0)
            {
                _currentRoomIsAmbush = false;
            }
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                parsed.a = 1f;
                return parsed;
            }
            return fallback;
        }

        // =========================================================================
        //  UNIQUE PROCEDURAL INTERIOR GENERATOR (Every House Unique by Seed!)
        // =========================================================================

        private void BuildUniqueInteriorRoom(BuildingEntrance entrance, out Vector3 playerSpawnPos, out float roomWidth, out float roomDepth)
        {
            if (_interiorRoot != null)
            {
                Destroy(_interiorRoot);
            }
            _interiorRoot = new GameObject($"ActiveBuildingInterior_{entrance.BuildingId}_{entrance.Theme}");
            _interiorRoot.transform.position = Vector3.zero;
            _activeInteriorContainers.Clear();

            System.Random rng = new System.Random(entrance.InteriorSeed);

            // 1. Unique dimensions per house (sized comfortably on the single center map tile, surrounded by black void)
            roomWidth = 13.0f + (float)rng.Next(0, 15) * 0.5f;  // 13.0m - 20.0m
            roomDepth = 9.5f + (float)rng.Next(0, 10) * 0.5f;   //  9.5m - 14.0m
            float wallHeight = 2.75f + (float)rng.Next(0, 6) * 0.1f; // 2.75m - 3.25m
            float wallThick = 0.35f;
            float doorWidth = 2.1f;

            // Varied exit door position along front (-Z) wall
            float maxDoorOffset = Mathf.Max(0.5f, roomWidth * 0.25f);
            float exitDoorX = Mathf.Round(((float)(rng.NextDouble() * 2.0 - 1.0) * maxDoorOffset) * 2f) * 0.5f;

            LayoutArchetype layout = (LayoutArchetype)rng.Next(0, 5);
            bool mirrorX = rng.Next(0, 2) == 1;

            Palette palette = GenerateThemePalette(entrance.Theme, rng);
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material floorMatA = CreateMat(litShader, palette.floorA, 0.22f);
            Material floorMatB = CreateMat(litShader, palette.floorB, 0.20f);
            Material wallMat = CreateMat(litShader, palette.wallMain, 0.15f);
            Material trimMat = CreateMat(litShader, palette.wallTrim, 0.25f);
            Material accentMat = CreateMat(litShader, palette.accent, 0.30f);

            float halfW = roomWidth * 0.5f;
            float halfD = roomDepth * 0.5f;

            // 2. Floor Slab + Theme Patterned Flooring
            BuildFloorAndPattern(_interiorRoot.transform, entrance.Theme, roomWidth, roomDepth, floorMatA, floorMatB, accentMat, rng);

            // 3. Outer Cutaway Walls (Tall Back/Left/Right + Low Front Sill with Exit Doorway Opening)
            // Back wall (+Z)
            CreateSolidBox(_interiorRoot.transform, "Wall_Back",
                new Vector3(0f, wallHeight * 0.5f, halfD + wallThick * 0.5f),
                new Vector3(roomWidth + wallThick * 2f, wallHeight, wallThick), wallMat, true);
            // Back top cap trim
            CreateSolidBox(_interiorRoot.transform, "Wall_BackCap",
                new Vector3(0f, wallHeight + 0.05f, halfD + wallThick * 0.5f),
                new Vector3(roomWidth + wallThick * 2.15f, 0.10f, wallThick + 0.08f), trimMat, false);

            // Left wall (-X)
            CreateSolidBox(_interiorRoot.transform, "Wall_Left",
                new Vector3(-halfW - wallThick * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(wallThick, wallHeight, roomDepth), wallMat, true);
            CreateSolidBox(_interiorRoot.transform, "Wall_LeftCap",
                new Vector3(-halfW - wallThick * 0.5f, wallHeight + 0.05f, 0f),
                new Vector3(wallThick + 0.08f, 0.10f, roomDepth + wallThick * 2f), trimMat, false);

            // Right wall (+X)
            CreateSolidBox(_interiorRoot.transform, "Wall_Right",
                new Vector3(halfW + wallThick * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(wallThick, wallHeight, roomDepth), wallMat, true);
            CreateSolidBox(_interiorRoot.transform, "Wall_RightCap",
                new Vector3(halfW + wallThick * 0.5f, wallHeight + 0.05f, 0f),
                new Vector3(wallThick + 0.08f, 0.10f, roomDepth + wallThick * 2f), trimMat, false);

            // Cutaway Front Wall (-Z) flanking the Exit Doorway so top-down camera is never blocked!
            float frontWallHeight = 0.52f;
            float leftEdge = -halfW - wallThick;
            float rightEdge = halfW + wallThick;
            float doorLeft = Mathf.Clamp(exitDoorX - doorWidth * 0.5f, -halfW + 0.6f, halfW - doorWidth - 0.6f);
            float doorRight = doorLeft + doorWidth;
            exitDoorX = (doorLeft + doorRight) * 0.5f;

            float leftSegW = doorLeft - leftEdge;
            if (leftSegW > 0.15f)
            {
                CreateSolidBox(_interiorRoot.transform, "Wall_FrontLeft",
                    new Vector3(leftEdge + leftSegW * 0.5f, frontWallHeight * 0.5f, -halfD - wallThick * 0.5f),
                    new Vector3(leftSegW, frontWallHeight, wallThick), trimMat, true);
            }

            float rightSegW = rightEdge - doorRight;
            if (rightSegW > 0.15f)
            {
                CreateSolidBox(_interiorRoot.transform, "Wall_FrontRight",
                    new Vector3(doorRight + rightSegW * 0.5f, frontWallHeight * 0.5f, -halfD - wallThick * 0.5f),
                    new Vector3(rightSegW, frontWallHeight, wallThick), trimMat, true);
            }

            // Invisible physics backstop behind the exit doorway so player never falls into the black void
            GameObject doorBackstop = new GameObject("ExitVoidBackstop");
            doorBackstop.transform.SetParent(_interiorRoot.transform, false);
            doorBackstop.transform.position = new Vector3(exitDoorX, 1.2f, -halfD - 0.45f);
            BoxCollider backstopCol = doorBackstop.AddComponent<BoxCollider>();
            backstopCol.size = new Vector3(doorWidth + 0.6f, 2.4f, 0.35f);

            // Exit portal mat + [E] interactable
            GameObject exitObj = new GameObject("BuildingExitPortal");
            exitObj.transform.SetParent(_interiorRoot.transform, false);
            exitObj.transform.position = new Vector3(exitDoorX, 0f, -halfD + 0.15f);
            BuildingExitPortal exitPortal = exitObj.AddComponent<BuildingExitPortal>();
            exitPortal.BuildExitVisual(doorWidth);

            // Player spawn point right inside exit doorway facing inward
            playerSpawnPos = new Vector3(exitDoorX, 0.5f, -halfD + 1.35f);

            // 4. Track occupied 2D footprints (walls, partitions, door clearances, furniture)
            List<Rect> occupiedRects = new List<Rect>();
            // Reserve clear zone around entrance/exit door
            occupiedRects.Add(new Rect(exitDoorX - 1.65f, -halfD, 3.3f, 2.75f));

            // 5. Architectural Partition Walls (unique floorplans!)
            BuildInteriorPartitions(_interiorRoot.transform, layout, mirrorX, roomWidth, roomDepth, wallHeight * 0.78f, exitDoorX, wallMat, trimMat, occupiedRects, rng);

            // 6. Wall Details (Windows, Paintings/Posters, Clinic Cross, Warehouse Hazard Signs)
            BuildWallDetails(_interiorRoot.transform, entrance.Theme, roomWidth, roomDepth, wallHeight, litShader, accentMat, trimMat, rng);

            // 7. Theme-specific Unique Furniture & Searchable LootContainers (2-5 lootables + decor)
            BuildThemedFurnitureAndLoot(_interiorRoot.transform, entrance.Theme, roomWidth, roomDepth, litShader, palette, occupiedRects, rng);

            // 8. Cozy Interior Lighting (readable both day & night against pitch-black void)
            BuildInteriorLights(_interiorRoot.transform, roomWidth, roomDepth, wallHeight, palette.lightColor);

            // 9. Mixed Encounters: Safe House (~40%) vs Ambush House (~60%)
            SpawnInteriorEncounterIfNeeded(entrance, roomWidth, roomDepth, playerSpawnPos, occupiedRects, rng);
        }

        private Palette GenerateThemePalette(InteriorTheme theme, System.Random rng)
        {
            Palette p = new Palette();
            float hueJitter = (float)(rng.NextDouble() * 0.06 - 0.03);
            float valJitter = (float)(rng.NextDouble() * 0.08 - 0.04);

            switch (theme)
            {
                case InteriorTheme.House:
                    {
                        Color[] woodFloors =
                        {
                            new Color(0.42f, 0.29f, 0.19f),
                            new Color(0.35f, 0.24f, 0.16f),
                            new Color(0.49f, 0.37f, 0.25f),
                            new Color(0.30f, 0.23f, 0.18f)
                        };
                        Color[] walls =
                        {
                            new Color(0.76f, 0.72f, 0.63f),
                            new Color(0.68f, 0.72f, 0.68f),
                            new Color(0.75f, 0.65f, 0.58f),
                            new Color(0.66f, 0.68f, 0.74f)
                        };
                        Color[] accents =
                        {
                            new Color(0.62f, 0.24f, 0.22f),
                            new Color(0.22f, 0.44f, 0.52f),
                            new Color(0.55f, 0.44f, 0.22f),
                            new Color(0.28f, 0.48f, 0.32f)
                        };
                        p.floorA = JitterColor(woodFloors[rng.Next(woodFloors.Length)], hueJitter, valJitter);
                        p.floorB = p.floorA * 0.86f;
                        p.wallMain = JitterColor(walls[rng.Next(walls.Length)], hueJitter, valJitter);
                        p.wallTrim = new Color(0.22f, 0.16f, 0.12f);
                        p.accent = JitterColor(accents[rng.Next(accents.Length)], hueJitter, valJitter);
                        p.lightColor = new Color(1f, 0.86f, 0.66f);
                        break;
                    }
                case InteriorTheme.CornerShop:
                    {
                        Color[] tilesA =
                        {
                            new Color(0.74f, 0.74f, 0.70f),
                            new Color(0.76f, 0.72f, 0.64f),
                            new Color(0.66f, 0.72f, 0.74f)
                        };
                        Color[] tilesB =
                        {
                            new Color(0.36f, 0.42f, 0.46f),
                            new Color(0.50f, 0.34f, 0.30f),
                            new Color(0.34f, 0.46f, 0.38f)
                        };
                        p.floorA = JitterColor(tilesA[rng.Next(tilesA.Length)], hueJitter, valJitter);
                        p.floorB = JitterColor(tilesB[rng.Next(tilesB.Length)], hueJitter, valJitter);
                        p.wallMain = JitterColor(new Color(0.74f, 0.76f, 0.72f), hueJitter, valJitter);
                        p.wallTrim = new Color(0.20f, 0.24f, 0.28f);
                        p.accent = JitterColor(new Color(0.22f, 0.55f, 0.78f), hueJitter, valJitter);
                        p.lightColor = new Color(0.96f, 0.96f, 0.86f);
                        break;
                    }
                case InteriorTheme.Clinic:
                    {
                        p.floorA = JitterColor(new Color(0.74f, 0.82f, 0.80f), hueJitter * 0.5f, valJitter);
                        p.floorB = JitterColor(new Color(0.62f, 0.72f, 0.72f), hueJitter * 0.5f, valJitter);
                        p.wallMain = JitterColor(new Color(0.82f, 0.87f, 0.86f), hueJitter * 0.5f, valJitter);
                        p.wallTrim = new Color(0.24f, 0.36f, 0.38f);
                        p.accent = new Color(0.82f, 0.20f, 0.22f);
                        p.lightColor = new Color(0.84f, 0.97f, 1f);
                        break;
                    }
                default: // Warehouse
                    {
                        p.floorA = JitterColor(new Color(0.36f, 0.37f, 0.38f), hueJitter * 0.4f, valJitter);
                        p.floorB = p.floorA * 0.84f;
                        p.wallMain = JitterColor(new Color(0.45f, 0.43f, 0.40f), hueJitter, valJitter);
                        p.wallTrim = new Color(0.20f, 0.19f, 0.18f);
                        p.accent = new Color(0.88f, 0.62f, 0.14f);
                        p.lightColor = new Color(1f, 0.80f, 0.54f);
                        break;
                    }
            }

            p.floorA.a = 1f;
            p.floorB.a = 1f;
            p.wallMain.a = 1f;
            p.wallTrim.a = 1f;
            p.accent.a = 1f;
            p.lightColor.a = 1f;
            return p;
        }

        private static Color JitterColor(Color c, float hueOffset, float valOffset)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            h = Mathf.Repeat(h + hueOffset, 1f);
            v = Mathf.Clamp(v + valOffset, 0.14f, 0.92f);
            return Color.HSVToRGB(h, s, v);
        }

        private static Material CreateMat(Shader shader, Color color, float smoothness = 0.2f)
        {
            Material m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.color = color;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            return m;
        }

        private void BuildFloorAndPattern(Transform parent, InteriorTheme theme, float roomW, float roomD, Material floorA, Material floorB, Material accentMat, System.Random rng)
        {
            // Reuse existing CGround (10m x 10m Unity Plane) as the single interior floor tile!
            if (_hasSavedCenterGround && _centerGroundTransform != null)
            {
                _centerGroundTransform.localScale = new Vector3((roomW + 0.2f) * 0.1f, 1f, (roomD + 0.2f) * 0.1f);
                if (_centerGroundRenderer != null)
                {
                    _centerGroundRenderer.SetPropertyBlock(null);
                    _centerGroundRenderer.sharedMaterial = floorA;
                }
            }
            else
            {
                // Fallback floor slab if CGround was not found in a test scene
                CreateSolidBox(parent, "InteriorFloorBase",
                    new Vector3(0f, -0.15f, 0f),
                    new Vector3(roomW + 0.2f, 0.30f, roomD + 0.2f), floorA, true);
            }

            if (theme == InteriorTheme.House)
            {
                // Wood plank alternating strips
                int strips = Mathf.Max(5, Mathf.RoundToInt(roomD / 1.15f));
                float stripD = roomD / strips;
                for (int i = 0; i < strips; i++)
                {
                    if (i % 2 == 0) continue;
                    float z = -roomD * 0.5f + (i + 0.5f) * stripD;
                    CreateSolidBox(parent, $"FloorPlank_{i}",
                        new Vector3(0f, 0.008f, z),
                        new Vector3(roomW - 0.1f, 0.014f, stripD * 0.96f), floorB, false);
                }

                // Cozy decorative rug with unique size/offset
                float rugW = Mathf.Clamp(roomW * (0.28f + (float)rng.NextDouble() * 0.16f), 2.4f, 5.2f);
                float rugD = Mathf.Clamp(roomD * (0.26f + (float)rng.NextDouble() * 0.16f), 2.0f, 3.8f);
                float rugX = (float)(rng.NextDouble() * 2.0 - 1.0) * (roomW * 0.14f);
                float rugZ = (float)(rng.NextDouble() * 1.4 - 0.3) * (roomD * 0.12f);
                CreateSolidBox(parent, "HouseRug",
                    new Vector3(rugX, 0.018f, rugZ),
                    new Vector3(rugW, 0.02f, rugD), accentMat, false);
            }
            else if (theme == InteriorTheme.CornerShop || theme == InteriorTheme.Clinic)
            {
                // Checkerboard / clinic floor tiles
                float tileSize = theme == InteriorTheme.CornerShop ? 1.5f : 1.8f;
                int cols = Mathf.Max(4, Mathf.FloorToInt(roomW / tileSize));
                int rows = Mathf.Max(4, Mathf.FloorToInt(roomD / tileSize));
                float cw = roomW / cols;
                float cd = roomD / rows;

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        if ((r + c) % 2 == 0) continue;
                        float x = -roomW * 0.5f + (c + 0.5f) * cw;
                        float z = -roomD * 0.5f + (r + 0.5f) * cd;
                        CreateSolidBox(parent, $"Tile_{r}_{c}",
                            new Vector3(x, 0.008f, z),
                            new Vector3(cw * 0.96f, 0.014f, cd * 0.96f), floorB, false);
                    }
                }
            }
            else // Warehouse
            {
                // Industrial yellow safety lane lines along interior edges
                CreateSolidBox(parent, "HazardStripe_Back",
                    new Vector3(0f, 0.012f, roomD * 0.5f - 0.45f),
                    new Vector3(roomW - 0.9f, 0.016f, 0.16f), accentMat, false);
                CreateSolidBox(parent, "HazardStripe_Left",
                    new Vector3(-roomW * 0.5f + 0.45f, 0.012f, 0f),
                    new Vector3(0.16f, 0.016f, roomD - 0.9f), accentMat, false);
                CreateSolidBox(parent, "HazardStripe_Right",
                    new Vector3(roomW * 0.5f - 0.45f, 0.012f, 0f),
                    new Vector3(0.16f, 0.016f, roomD - 0.9f), accentMat, false);
            }
        }

        private void BuildInteriorPartitions(
            Transform parent,
            LayoutArchetype layout,
            bool mirrorX,
            float roomW,
            float roomD,
            float partH,
            float exitDoorX,
            Material wallMat,
            Material trimMat,
            List<Rect> occupiedRects,
            System.Random rng)
        {
            float halfW = roomW * 0.5f;
            float halfD = roomD * 0.5f;
            float thick = 0.24f;
            float sign = mirrorX ? -1f : 1f;

            switch (layout)
            {
                case LayoutArchetype.OpenHall:
                    // Open layout — no internal partition walls
                    break;

                case LayoutArchetype.SidePartition:
                    {
                        float partX = sign * (roomW * (0.14f + (float)rng.NextDouble() * 0.08f));
                        // Avoid splitting directly across the exit doorway
                        if (Mathf.Abs(partX - exitDoorX) < 1.8f) partX = -partX;
                        float gapZ = (float)(rng.NextDouble() * 2.0 - 1.0) * (roomD * 0.16f);
                        float gapSize = 2.3f;

                        // Back segment (gapZ + gapSize/2 -> halfD)
                        float zStart1 = gapZ + gapSize * 0.5f;
                        float len1 = halfD - zStart1;
                        if (len1 > 0.6f)
                        {
                            AddPartitionSegment(parent, "Partition_SideBack", new Vector3(partX, partH * 0.5f, zStart1 + len1 * 0.5f), new Vector3(thick, partH, len1), wallMat, trimMat, occupiedRects);
                        }

                        // Front segment (-halfD -> gapZ - gapSize/2)
                        float zEnd2 = gapZ - gapSize * 0.5f;
                        float len2 = zEnd2 - (-halfD);
                        if (len2 > 0.6f)
                        {
                            AddPartitionSegment(parent, "Partition_SideFront", new Vector3(partX, partH * 0.5f, -halfD + len2 * 0.5f), new Vector3(thick, partH, len2), wallMat, trimMat, occupiedRects);
                        }

                        // Reserve clear walkthrough at doorway gap
                        occupiedRects.Add(new Rect(partX - 1.25f, gapZ - gapSize * 0.55f, 2.5f, gapSize * 1.1f));
                        break;
                    }

                case LayoutArchetype.BackPartition:
                    {
                        float partZ = roomD * (0.06f + (float)rng.NextDouble() * 0.10f);
                        float gapX = sign * (float)(rng.NextDouble() * (roomW * 0.22f));
                        float gapSize = 2.4f;

                        float xEnd1 = gapX - gapSize * 0.5f;
                        float len1 = xEnd1 - (-halfW);
                        if (len1 > 0.7f)
                        {
                            AddPartitionSegment(parent, "Partition_BackLeft", new Vector3(-halfW + len1 * 0.5f, partH * 0.5f, partZ), new Vector3(len1, partH, thick), wallMat, trimMat, occupiedRects);
                        }

                        float xStart2 = gapX + gapSize * 0.5f;
                        float len2 = halfW - xStart2;
                        if (len2 > 0.7f)
                        {
                            AddPartitionSegment(parent, "Partition_BackRight", new Vector3(xStart2 + len2 * 0.5f, partH * 0.5f, partZ), new Vector3(len2, partH, thick), wallMat, trimMat, occupiedRects);
                        }

                        occupiedRects.Add(new Rect(gapX - gapSize * 0.55f, partZ - 1.25f, gapSize * 1.1f, 2.5f));
                        break;
                    }

                case LayoutArchetype.CornerAlcove:
                    {
                        float alcoveW = roomW * (0.32f + (float)rng.NextDouble() * 0.10f);
                        float alcoveD = roomD * (0.34f + (float)rng.NextDouble() * 0.10f);
                        float wallX = sign > 0f ? (halfW - alcoveW) : (-halfW + alcoveW);
                        float wallZ = halfD - alcoveD;

                        // Short vertical leg leaving wide entrance at the bottom
                        float vLen = Mathf.Max(1.4f, alcoveD - 1.8f);
                        AddPartitionSegment(parent, "Partition_AlcoveV",
                            new Vector3(wallX, partH * 0.5f, halfD - vLen * 0.5f),
                            new Vector3(thick, partH, vLen), wallMat, trimMat, occupiedRects);

                        // Reserve clear passage into alcove
                        occupiedRects.Add(new Rect(wallX - 1.2f, wallZ - 0.6f, 2.4f, 2.2f));
                        break;
                    }

                case LayoutArchetype.TwinZones:
                    {
                        float stubLen = roomW * (0.24f + (float)rng.NextDouble() * 0.06f);
                        float zPos = (float)(rng.NextDouble() * 1.4 - 0.4);
                        AddPartitionSegment(parent, "Partition_TwinL",
                            new Vector3(-halfW + stubLen * 0.5f, partH * 0.5f, zPos),
                            new Vector3(stubLen, partH, thick), wallMat, trimMat, occupiedRects);
                        AddPartitionSegment(parent, "Partition_TwinR",
                            new Vector3(halfW - stubLen * 0.5f, partH * 0.5f, zPos),
                            new Vector3(stubLen, partH, thick), wallMat, trimMat, occupiedRects);
                        occupiedRects.Add(new Rect(-1.5f, zPos - 1.2f, 3.0f, 2.4f));
                        break;
                    }
            }
        }

        private void AddPartitionSegment(Transform parent, string segName, Vector3 center, Vector3 size, Material wallMat, Material trimMat, List<Rect> occupiedRects)
        {
            CreateSolidBox(parent, segName, center, size, wallMat, true);
            CreateSolidBox(parent, segName + "_Cap",
                new Vector3(center.x, size.y + 0.04f, center.z),
                new Vector3(size.x + 0.06f, 0.08f, size.z + 0.06f), trimMat, false);

            occupiedRects.Add(new Rect(
                center.x - size.x * 0.5f - 0.35f,
                center.z - size.z * 0.5f - 0.35f,
                size.x + 0.70f,
                size.z + 0.70f));
        }

        private void BuildWallDetails(Transform parent, InteriorTheme theme, float roomW, float roomD, float wallH, Shader shader, Material accentMat, Material trimMat, System.Random rng)
        {
            float halfW = roomW * 0.5f;

            // Build 3 live Day/Night windows along the back (+Z) wall with real sunlight/moonlight shafts shining onto the floor!
            GameObject winObj = new GameObject("BackWallDayNightWindows");
            winObj.transform.SetParent(parent, false);
            InteriorWindowDayNightController winCtrl = winObj.AddComponent<InteriorWindowDayNightController>();
            winCtrl.BuildThreeBackWallWindows(winObj.transform, roomW, roomD, wallH, shader, trimMat);

            // Theme wall emblem / poster on left or right wall (offset toward back corner to stay clear of side doors)
            if (theme == InteriorTheme.Clinic)
            {
                float sideX = rng.Next(0, 2) == 0 ? (-halfW + 0.04f) : (halfW - 0.04f);
                CreateSolidBox(parent, "ClinicCross_H", new Vector3(sideX, wallH * 0.64f, roomD * 0.28f), new Vector3(0.08f, 0.24f, 0.95f), accentMat, false);
                CreateSolidBox(parent, "ClinicCross_V", new Vector3(sideX, wallH * 0.64f, roomD * 0.28f), new Vector3(0.08f, 0.95f, 0.24f), accentMat, false);
            }
            else if (theme == InteriorTheme.House || theme == InteriorTheme.CornerShop)
            {
                float sideX = rng.Next(0, 2) == 0 ? (-halfW + 0.04f) : (halfW - 0.04f);
                CreateSolidBox(parent, "WallFrame", new Vector3(sideX, wallH * 0.62f, roomD * 0.28f), new Vector3(0.08f, 0.9f, 1.3f), trimMat, false);
                CreateSolidBox(parent, "WallArt", new Vector3(sideX * 0.992f, wallH * 0.62f, roomD * 0.28f), new Vector3(0.06f, 0.72f, 1.1f), accentMat, false);
            }
        }

        private void BuildThemedFurnitureAndLoot(
            Transform parent,
            InteriorTheme theme,
            float roomW,
            float roomD,
            Shader shader,
            Palette palette,
            List<Rect> occupiedRects,
            System.Random rng)
        {
            Material darkWood = CreateMat(shader, new Color(0.32f, 0.21f, 0.14f), 0.22f);
            Material lightWood = CreateMat(shader, new Color(0.56f, 0.42f, 0.28f), 0.20f);
            Material metalMat = CreateMat(shader, new Color(0.42f, 0.46f, 0.50f), 0.55f);
            Material whiteMetal = CreateMat(shader, new Color(0.84f, 0.88f, 0.88f), 0.45f);
            Material fabricMat = CreateMat(shader, palette.accent, 0.10f);
            Material crateWood = CreateMat(shader, new Color(0.52f, 0.38f, 0.22f), 0.15f);
            Material cushionMat = CreateMat(shader, new Color(0.86f, 0.84f, 0.78f), 0.08f);

            // 1. Place 2-5 unique Searchable Loot Containers based on theme + room area
            int lootContainerTarget = rng.Next(2, 5); // 2 to 4 guaranteed, +1 on larger rooms
            if (roomW * roomD >= 120f && rng.NextDouble() < 0.65) lootContainerTarget++;
            lootContainerTarget = Mathf.Clamp(lootContainerTarget, 2, 5);

            int placedLoot = 0;
            for (int i = 0; i < lootContainerTarget; i++)
            {
                string propKind = PickLootablePropKind(theme, i, rng);
                Vector2 size = GetPropFootprint(propKind);
                if (TryFindClearSpot(roomW, roomD, size.x, size.y, preferNearWall: true, occupiedRects, rng, out Vector3 pos, out float rotY))
                {
                    GameObject prop = BuildFurnitureMesh(parent, propKind, pos, rotY, darkWood, lightWood, metalMat, whiteMetal, fabricMat, crateWood, cushionMat);
                    AttachInteriorLootContainer(prop, theme, propKind, rng);
                    placedLoot++;
                }
            }

            // Guarantee at least 2 lootable containers even in cramped partitioned rooms
            if (placedLoot < 2)
            {
                for (int extra = placedLoot; extra < 2; extra++)
                {
                    if (TryFindClearSpot(roomW, roomD, 1.15f, 1.15f, preferNearWall: false, occupiedRects, rng, out Vector3 pos, out float rotY))
                    {
                        GameObject crate = BuildFurnitureMesh(parent, "SupplyCrate", pos, rotY, darkWood, lightWood, metalMat, whiteMetal, fabricMat, crateWood, cushionMat);
                        AttachInteriorLootContainer(crate, theme, "SupplyCrate", rng);
                    }
                }
            }

            // 2. Place 3-6 decorative/cover furniture pieces (beds, tables, sofas, desks, barrels, racks)
            int decorTarget = rng.Next(3, 7);
            for (int i = 0; i < decorTarget; i++)
            {
                string decorKind = PickDecorPropKind(theme, i, rng);
                Vector2 size = GetPropFootprint(decorKind);
                bool nearWall = decorKind == "Bed" || decorKind == "HospitalBed" || decorKind == "Bookshelf" || decorKind == "Sofa" || decorKind == "BarrelCluster";
                if (TryFindClearSpot(roomW, roomD, size.x, size.y, nearWall, occupiedRects, rng, out Vector3 pos, out float rotY))
                {
                    BuildFurnitureMesh(parent, decorKind, pos, rotY, darkWood, lightWood, metalMat, whiteMetal, fabricMat, crateWood, cushionMat);
                }
            }
        }

        private static string PickLootablePropKind(InteriorTheme theme, int index, System.Random rng)
        {
            switch (theme)
            {
                case InteriorTheme.House:
                    string[] houseLoot = { "Wardrobe", "Refrigerator", "KitchenCabinet", "SupplyCrate", "Footlocker" };
                    return houseLoot[(index + rng.Next(0, houseLoot.Length)) % houseLoot.Length];
                case InteriorTheme.CornerShop:
                    string[] shopLoot = { "CheckoutCounter", "DisplayShelf", "BeverageCooler", "SupplyCrate" };
                    return index == 0 ? "CheckoutCounter" : shopLoot[rng.Next(1, shopLoot.Length)];
                case InteriorTheme.Clinic:
                    string[] clinicLoot = { "MedicalCabinet", "ReceptionDesk", "BioSupplyBox", "MedicalCabinet" };
                    return clinicLoot[index % clinicLoot.Length];
                default: // Warehouse
                    string[] whLoot = { "SteelRack", "CargoCrate", "ToolChest", "SupplyCrate" };
                    return whLoot[(index + rng.Next(0, whLoot.Length)) % whLoot.Length];
            }
        }

        private static string PickDecorPropKind(InteriorTheme theme, int index, System.Random rng)
        {
            switch (theme)
            {
                case InteriorTheme.House:
                    string[] hDecor = { "Bed", "DiningTable", "Sofa", "Bookshelf", "CoffeeTable" };
                    return hDecor[(index + rng.Next(0, hDecor.Length)) % hDecor.Length];
                case InteriorTheme.CornerShop:
                    string[] sDecor = { "DisplayShelf", "DiningTable", "SupplyCrate", "BarrelCluster" };
                    return sDecor[(index + rng.Next(0, sDecor.Length)) % sDecor.Length];
                case InteriorTheme.Clinic:
                    string[] cDecor = { "HospitalBed", "HospitalBed", "CoffeeTable", "DiningTable" };
                    return cDecor[(index + rng.Next(0, cDecor.Length)) % cDecor.Length];
                default:
                    string[] wDecor = { "PalletStack", "BarrelCluster", "Workbench", "CargoCrate" };
                    return wDecor[(index + rng.Next(0, wDecor.Length)) % wDecor.Length];
            }
        }

        private static Vector2 GetPropFootprint(string kind)
        {
            return kind switch
            {
                "Bed" => new Vector2(2.0f, 1.35f),
                "HospitalBed" => new Vector2(2.05f, 1.15f),
                "Sofa" => new Vector2(2.1f, 0.95f),
                "DiningTable" => new Vector2(1.75f, 1.15f),
                "CheckoutCounter" => new Vector2(2.2f, 0.95f),
                "ReceptionDesk" => new Vector2(2.1f, 0.95f),
                "DisplayShelf" => new Vector2(1.85f, 0.85f),
                "SteelRack" => new Vector2(2.0f, 0.90f),
                "Workbench" => new Vector2(1.95f, 0.95f),
                "Wardrobe" => new Vector2(1.45f, 0.85f),
                "Bookshelf" => new Vector2(1.55f, 0.70f),
                "KitchenCabinet" => new Vector2(1.65f, 0.85f),
                "MedicalCabinet" => new Vector2(1.35f, 0.80f),
                "Refrigerator" => new Vector2(1.05f, 0.95f),
                "BeverageCooler" => new Vector2(1.25f, 0.90f),
                "PalletStack" => new Vector2(1.55f, 1.35f),
                "CargoCrate" => new Vector2(1.25f, 1.25f),
                "ToolChest" => new Vector2(1.20f, 0.80f),
                "BarrelCluster" => new Vector2(1.30f, 1.30f),
                "CoffeeTable" => new Vector2(1.25f, 0.85f),
                "Footlocker" => new Vector2(1.20f, 0.75f),
                "BioSupplyBox" => new Vector2(1.10f, 0.85f),
                _ => new Vector2(1.15f, 1.15f)
            };
        }

        private static bool TryFindClearSpot(
            float roomW,
            float roomD,
            float sizeX,
            float sizeZ,
            bool preferNearWall,
            List<Rect> occupiedRects,
            System.Random rng,
            out Vector3 pos,
            out float rotY)
        {
            float halfW = roomW * 0.5f;
            float halfD = roomD * 0.5f;
            float wallMargin = 0.42f;
            float clearance = 0.45f;

            for (int attempt = 0; attempt < 36; attempt++)
            {
                bool swap90 = rng.Next(0, 2) == 1;
                float fx = swap90 ? sizeZ : sizeX;
                float fz = swap90 ? sizeX : sizeZ;
                rotY = swap90 ? (rng.Next(0, 2) == 0 ? 90f : 270f) : (rng.Next(0, 2) == 0 ? 0f : 180f);

                float minX = -halfW + wallMargin + fx * 0.5f;
                float maxX = halfW - wallMargin - fx * 0.5f;
                float minZ = -halfD + wallMargin + fz * 0.5f;
                float maxZ = halfD - wallMargin - fz * 0.5f;
                if (minX >= maxX || minZ >= maxZ) continue;

                float px;
                float pz;
                if (preferNearWall && attempt < 22)
                {
                    int side = rng.Next(0, 3); // Back, Left, Right walls
                    if (side == 0)
                    {
                        px = minX + (float)rng.NextDouble() * (maxX - minX);
                        pz = maxZ;
                        rotY = 0f;
                        fx = sizeX;
                        fz = sizeZ;
                    }
                    else if (side == 1)
                    {
                        px = minX;
                        pz = minZ + (float)rng.NextDouble() * (maxZ - minZ);
                        rotY = 90f;
                        fx = sizeZ;
                        fz = sizeX;
                    }
                    else
                    {
                        px = maxX;
                        pz = minZ + (float)rng.NextDouble() * (maxZ - minZ);
                        rotY = 270f;
                        fx = sizeZ;
                        fz = sizeX;
                    }

                    // Re-clamp after orienting against wall
                    px = Mathf.Clamp(px, -halfW + wallMargin + fx * 0.5f, halfW - wallMargin - fx * 0.5f);
                    pz = Mathf.Clamp(pz, -halfD + wallMargin + fz * 0.5f, halfD - wallMargin - fz * 0.5f);
                }
                else
                {
                    px = minX + (float)rng.NextDouble() * (maxX - minX);
                    pz = minZ + (float)rng.NextDouble() * (maxZ - minZ);
                }

                Rect candidate = new Rect(
                    px - fx * 0.5f - clearance,
                    pz - fz * 0.5f - clearance,
                    fx + clearance * 2f,
                    fz + clearance * 2f);

                bool overlaps = false;
                for (int i = 0; i < occupiedRects.Count; i++)
                {
                    if (candidate.Overlaps(occupiedRects[i]))
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    occupiedRects.Add(new Rect(px - fx * 0.5f - 0.2f, pz - fz * 0.5f - 0.2f, fx + 0.4f, fz + 0.4f));
                    pos = new Vector3(px, 0f, pz);
                    return true;
                }
            }

            pos = Vector3.zero;
            rotY = 0f;
            return false;
        }

        private GameObject BuildFurnitureMesh(
            Transform parent,
            string kind,
            Vector3 pos,
            float rotY,
            Material darkWood,
            Material lightWood,
            Material metalMat,
            Material whiteMetal,
            Material fabricMat,
            Material crateWood,
            Material cushionMat)
        {
            GameObject root = new GameObject($"Prop_{kind}");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, rotY, 0f);

            Vector2 fp = GetPropFootprint(kind);
            BoxCollider mainCol = root.AddComponent<BoxCollider>();
            mainCol.size = new Vector3(fp.x * 0.92f, 1.15f, fp.y * 0.92f);
            mainCol.center = new Vector3(0f, 0.575f, 0f);

            switch (kind)
            {
                case "Bed":
                    CreateSolidBox(root.transform, "Frame", new Vector3(0f, 0.22f, 0f), new Vector3(1.92f, 0.34f, 1.26f), darkWood, false);
                    CreateSolidBox(root.transform, "Mattress", new Vector3(0f, 0.44f, 0f), new Vector3(1.80f, 0.18f, 1.16f), cushionMat, false);
                    CreateSolidBox(root.transform, "Blanket", new Vector3(0.24f, 0.49f, 0f), new Vector3(1.22f, 0.14f, 1.18f), fabricMat, false);
                    CreateSolidBox(root.transform, "Pillow", new Vector3(-0.62f, 0.58f, 0f), new Vector3(0.36f, 0.14f, 0.88f), cushionMat, false);
                    CreateSolidBox(root.transform, "Headboard", new Vector3(-0.92f, 0.56f, 0f), new Vector3(0.12f, 0.88f, 1.28f), darkWood, false);
                    break;

                case "HospitalBed":
                    CreateSolidBox(root.transform, "MetalFrame", new Vector3(0f, 0.34f, 0f), new Vector3(1.96f, 0.16f, 1.06f), whiteMetal, false);
                    CreateSolidBox(root.transform, "Mattress", new Vector3(0f, 0.50f, 0f), new Vector3(1.86f, 0.20f, 0.98f), cushionMat, false);
                    CreateSolidBox(root.transform, "RailHead", new Vector3(-0.94f, 0.65f, 0f), new Vector3(0.08f, 0.72f, 1.04f), metalMat, false);
                    CreateSolidBox(root.transform, "RailFoot", new Vector3(0.94f, 0.55f, 0f), new Vector3(0.08f, 0.52f, 1.04f), metalMat, false);
                    break;

                case "Sofa":
                    CreateSolidBox(root.transform, "Seat", new Vector3(0f, 0.28f, 0.05f), new Vector3(1.95f, 0.38f, 0.78f), fabricMat, false);
                    CreateSolidBox(root.transform, "Back", new Vector3(0f, 0.62f, -0.32f), new Vector3(1.95f, 0.62f, 0.22f), fabricMat, false);
                    CreateSolidBox(root.transform, "ArmL", new Vector3(-0.92f, 0.46f, 0f), new Vector3(0.20f, 0.46f, 0.86f), darkWood, false);
                    CreateSolidBox(root.transform, "ArmR", new Vector3(0.92f, 0.46f, 0f), new Vector3(0.20f, 0.46f, 0.86f), darkWood, false);
                    break;

                case "DiningTable":
                case "CoffeeTable":
                    float h = kind == "CoffeeTable" ? 0.46f : 0.78f;
                    CreateSolidBox(root.transform, "Top", new Vector3(0f, h, 0f), new Vector3(fp.x * 0.94f, 0.10f, fp.y * 0.92f), lightWood, false);
                    CreateSolidBox(root.transform, "LegFL", new Vector3(-fp.x * 0.38f, h * 0.5f, -fp.y * 0.35f), new Vector3(0.10f, h, 0.10f), darkWood, false);
                    CreateSolidBox(root.transform, "LegFR", new Vector3(fp.x * 0.38f, h * 0.5f, -fp.y * 0.35f), new Vector3(0.10f, h, 0.10f), darkWood, false);
                    CreateSolidBox(root.transform, "LegBL", new Vector3(-fp.x * 0.38f, h * 0.5f, fp.y * 0.35f), new Vector3(0.10f, h, 0.10f), darkWood, false);
                    CreateSolidBox(root.transform, "LegBR", new Vector3(fp.x * 0.38f, h * 0.5f, fp.y * 0.35f), new Vector3(0.10f, h, 0.10f), darkWood, false);
                    break;

                case "Wardrobe":
                case "Bookshelf":
                    CreateSolidBox(root.transform, "Body", new Vector3(0f, 0.92f, 0f), new Vector3(fp.x * 0.92f, 1.84f, fp.y * 0.86f), darkWood, false);
                    CreateSolidBox(root.transform, "FrontPanel", new Vector3(0f, 0.92f, -fp.y * 0.42f), new Vector3(fp.x * 0.82f, 1.65f, 0.06f), lightWood, false);
                    break;

                case "Refrigerator":
                case "BeverageCooler":
                    CreateSolidBox(root.transform, "FridgeBody", new Vector3(0f, 0.92f, 0f), new Vector3(fp.x * 0.88f, 1.84f, fp.y * 0.86f), whiteMetal, false);
                    CreateSolidBox(root.transform, "Handle", new Vector3(fp.x * 0.30f, 1.10f, -fp.y * 0.45f), new Vector3(0.06f, 0.55f, 0.08f), metalMat, false);
                    if (kind == "BeverageCooler")
                    {
                        CreateSolidBox(root.transform, "HeaderSign", new Vector3(0f, 1.72f, -fp.y * 0.45f), new Vector3(fp.x * 0.82f, 0.24f, 0.05f), fabricMat, false);
                    }
                    break;

                case "MedicalCabinet":
                    CreateSolidBox(root.transform, "CabBody", new Vector3(0f, 0.86f, 0f), new Vector3(1.22f, 1.72f, 0.68f), whiteMetal, false);
                    CreateSolidBox(root.transform, "RedCrossH", new Vector3(0f, 1.22f, -0.36f), new Vector3(0.34f, 0.10f, 0.04f), fabricMat, false);
                    CreateSolidBox(root.transform, "RedCrossV", new Vector3(0f, 1.22f, -0.36f), new Vector3(0.10f, 0.34f, 0.04f), fabricMat, false);
                    break;

                case "CheckoutCounter":
                case "ReceptionDesk":
                case "KitchenCabinet":
                    Material bodyM = kind == "ReceptionDesk" ? whiteMetal : (kind == "CheckoutCounter" ? lightWood : darkWood);
                    CreateSolidBox(root.transform, "CounterBase", new Vector3(0f, 0.48f, 0f), new Vector3(fp.x * 0.94f, 0.94f, fp.y * 0.86f), bodyM, false);
                    CreateSolidBox(root.transform, "CounterTop", new Vector3(0f, 0.98f, 0f), new Vector3(fp.x * 0.98f, 0.10f, fp.y * 0.92f), cushionMat, false);
                    if (kind == "CheckoutCounter")
                    {
                        CreateSolidBox(root.transform, "CashRegister", new Vector3(0.45f, 1.18f, 0f), new Vector3(0.42f, 0.30f, 0.36f), metalMat, false);
                    }
                    break;

                case "DisplayShelf":
                case "SteelRack":
                    Material shelfM = kind == "SteelRack" ? metalMat : lightWood;
                    CreateSolidBox(root.transform, "PostL", new Vector3(-fp.x * 0.42f, 0.85f, 0f), new Vector3(0.10f, 1.70f, fp.y * 0.82f), shelfM, false);
                    CreateSolidBox(root.transform, "PostR", new Vector3(fp.x * 0.42f, 0.85f, 0f), new Vector3(0.10f, 1.70f, fp.y * 0.82f), shelfM, false);
                    CreateSolidBox(root.transform, "Shelf1", new Vector3(0f, 0.45f, 0f), new Vector3(fp.x * 0.90f, 0.08f, fp.y * 0.84f), shelfM, false);
                    CreateSolidBox(root.transform, "Shelf2", new Vector3(0f, 1.05f, 0f), new Vector3(fp.x * 0.90f, 0.08f, fp.y * 0.84f), shelfM, false);
                    CreateSolidBox(root.transform, "Shelf3", new Vector3(0f, 1.62f, 0f), new Vector3(fp.x * 0.90f, 0.08f, fp.y * 0.84f), shelfM, false);
                    CreateSolidBox(root.transform, "StockBox1", new Vector3(-0.35f, 0.68f, 0f), new Vector3(0.45f, 0.36f, 0.45f), crateWood, false);
                    CreateSolidBox(root.transform, "StockBox2", new Vector3(0.32f, 1.28f, 0f), new Vector3(0.50f, 0.36f, 0.42f), fabricMat, false);
                    break;

                case "Workbench":
                    CreateSolidBox(root.transform, "BenchTop", new Vector3(0f, 0.84f, 0f), new Vector3(1.85f, 0.14f, 0.85f), darkWood, false);
                    CreateSolidBox(root.transform, "BenchBase", new Vector3(0f, 0.40f, 0f), new Vector3(1.68f, 0.78f, 0.72f), metalMat, false);
                    break;

                case "ToolChest":
                case "Footlocker":
                case "BioSupplyBox":
                    Material chestM = kind == "BioSupplyBox" ? whiteMetal : (kind == "ToolChest" ? fabricMat : darkWood);
                    CreateSolidBox(root.transform, "ChestBody", new Vector3(0f, 0.36f, 0f), new Vector3(fp.x * 0.92f, 0.72f, fp.y * 0.88f), chestM, false);
                    CreateSolidBox(root.transform, "ChestLatch", new Vector3(0f, 0.56f, -fp.y * 0.45f), new Vector3(0.24f, 0.14f, 0.06f), metalMat, false);
                    break;

                case "BarrelCluster":
                    CreateCylinderProp(root.transform, "Barrel1", new Vector3(-0.28f, 0.48f, -0.20f), new Vector3(0.52f, 0.48f, 0.52f), metalMat);
                    CreateCylinderProp(root.transform, "Barrel2", new Vector3(0.28f, 0.48f, 0.22f), new Vector3(0.52f, 0.48f, 0.52f), fabricMat);
                    break;

                case "PalletStack":
                    CreateSolidBox(root.transform, "Pallet", new Vector3(0f, 0.10f, 0f), new Vector3(1.45f, 0.18f, 1.25f), lightWood, false);
                    CreateSolidBox(root.transform, "BoxA", new Vector3(-0.26f, 0.54f, -0.15f), new Vector3(0.70f, 0.70f, 0.70f), crateWood, false);
                    CreateSolidBox(root.transform, "BoxB", new Vector3(0.32f, 0.46f, 0.24f), new Vector3(0.55f, 0.55f, 0.55f), darkWood, false);
                    break;

                default: // SupplyCrate / CargoCrate
                    CreateSolidBox(root.transform, "CrateBody", new Vector3(0f, 0.48f, 0f), new Vector3(fp.x * 0.88f, 0.96f, fp.y * 0.88f), crateWood, false);
                    CreateSolidBox(root.transform, "CrateTrim", new Vector3(0f, 0.96f, 0f), new Vector3(fp.x * 0.92f, 0.08f, fp.y * 0.92f), darkWood, false);
                    break;
            }

            return root;
        }

        private void AttachInteriorLootContainer(GameObject prop, InteriorTheme theme, string propKind, System.Random rng)
        {
            LootContainer lc = prop.AddComponent<LootContainer>();
            lc.randomizeLoot = false; // We populate deterministically right here so every house is unique & synced!
            lc.interactionRadius = 2.25f;
            lc.containerName = FormatPropDisplayName(propKind);
            lc.itemsInside.Clear();

            // Subtle warm highlight indicator on top of lootable furniture so players spot searchable objects easily
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indicator.name = "LootableAccent";
            indicator.transform.SetParent(prop.transform, false);
            indicator.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            indicator.transform.localScale = new Vector3(0.18f, 0.04f, 0.18f);
            Collider ic = indicator.GetComponent<Collider>();
            if (ic != null) Destroy(ic);

            // Roll cash
            lc.moneyAmount = theme switch
            {
                InteriorTheme.CornerShop => rng.Next(15, 75),
                InteriorTheme.Warehouse => rng.Next(10, 55),
                InteriorTheme.Clinic => rng.Next(5, 35),
                _ => rng.Next(8, 48)
            };

            // Roll 2-4 theme-appropriate real items from ItemDatabase
            int itemRolls = rng.Next(2, 5);
            bool rolledAmmo = false;

            for (int r = 0; r < itemRolls; r++)
            {
                PickThemeLootItem(theme, rng, out string itemName, out int qty);
                if (string.IsNullOrEmpty(itemName) || qty <= 0) continue;

                if (string.Equals(itemName, "Ammo", StringComparison.OrdinalIgnoreCase))
                {
                    if (rolledAmmo) continue;
                    rolledAmmo = true;
                    qty = rng.Next(1, 4); // Mystery Ammo Box 1-3 boxes
                }

                lc.AddItem(itemName, qty);
            }

            // 1. Restore local SP saved state (persisted to InteriorProgressSave.json!)
            string localKey = BuildLocalContainerKey(_currentInteriorKey, prop.transform.position);
            if (_localContainerStates.TryGetValue(localKey, out InteriorSavedContainerRecord saved))
            {
                lc.moneyAmount = saved.remainingMoney;
                lc.itemsInside.Clear();
                if (saved.remainingItems != null)
                {
                    for (int s = 0; s < saved.remainingItems.Count; s++)
                    {
                        lc.itemsInside.Add(new LootSlot(saved.remainingItems[s].itemName, saved.remainingItems[s].quantity));
                    }
                }
                lc.isSearched = saved.isSearched || (lc.moneyAmount <= 0 && lc.itemsInside.Count == 0);
            }

            // 2. Restore Online Co-op synced state (takes precedence if teammate looted it!)
            NetworkLootManager.Instance?.ApplyCachedStateToContainer(lc);

            _activeInteriorContainers.Add(lc);
        }

        private static string FormatPropDisplayName(string propKind)
        {
            return propKind switch
            {
                "KitchenCabinet" => "Kitchen Cabinet",
                "CheckoutCounter" => "Checkout Counter",
                "DisplayShelf" => "Display Shelf",
                "BeverageCooler" => "Beverage Cooler",
                "MedicalCabinet" => "Medical Cabinet",
                "ReceptionDesk" => "Reception Desk",
                "BioSupplyBox" => "Medical Supply Box",
                "SteelRack" => "Steel Supply Rack",
                "CargoCrate" => "Cargo Crate",
                "ToolChest" => "Tool Chest",
                "SupplyCrate" => "Supply Crate",
                _ => propKind
            };
        }

        private static void PickThemeLootItem(InteriorTheme theme, System.Random rng, out string itemName, out int qty)
        {
            double roll = rng.NextDouble();
            switch (theme)
            {
                case InteriorTheme.Clinic:
                    if (roll < 0.26) { itemName = "Bandage"; qty = rng.Next(1, 4); }
                    else if (roll < 0.46) { itemName = "First Aid Kit"; qty = rng.Next(1, 3); }
                    else if (roll < 0.62) { itemName = "Potion"; qty = rng.Next(1, 3); }
                    else if (roll < 0.74) { itemName = "Adrenaline Shot"; qty = 1; }
                    else if (roll < 0.85) { itemName = "Anti-Panic Sedatives"; qty = rng.Next(1, 3); }
                    else if (roll < 0.93) { itemName = "Water Bottle"; qty = rng.Next(1, 3); }
                    else { itemName = "Clean Water Filter"; qty = 1; }
                    break;

                case InteriorTheme.CornerShop:
                    if (roll < 0.22) { itemName = "Canned Food"; qty = rng.Next(1, 3); }
                    else if (roll < 0.38) { itemName = "Energy Bar"; qty = rng.Next(1, 4); }
                    else if (roll < 0.52) { itemName = "Water Bottle"; qty = rng.Next(1, 3); }
                    else if (roll < 0.64) { itemName = "Bread"; qty = rng.Next(1, 3); }
                    else if (roll < 0.74) { itemName = "Hot Black Coffee"; qty = rng.Next(1, 3); }
                    else if (roll < 0.83) { itemName = "Duct Tape"; qty = rng.Next(1, 3); }
                    else if (roll < 0.91) { itemName = "Ammo"; qty = rng.Next(1, 4); }
                    else if (roll < 0.96) { itemName = "Pack of Cigarettes"; qty = 1; }
                    else { itemName = "Bandage"; qty = rng.Next(1, 3); }
                    break;

                case InteriorTheme.Warehouse:
                    if (roll < 0.18) { itemName = "Scrap Metal Plate"; qty = rng.Next(1, 4); }
                    else if (roll < 0.32) { itemName = "Ammo"; qty = rng.Next(1, 4); }
                    else if (roll < 0.44) { itemName = "Gunpowder Mix"; qty = rng.Next(1, 3); }
                    else if (roll < 0.55) { itemName = "Box of Nails"; qty = rng.Next(1, 3); }
                    else if (roll < 0.65) { itemName = "Duct Tape"; qty = rng.Next(1, 3); }
                    else if (roll < 0.74) { itemName = "Scrap Steel Pipe"; qty = rng.Next(1, 3); }
                    else if (roll < 0.82) { itemName = "Torn Copper Wiring"; qty = rng.Next(1, 3); }
                    else if (roll < 0.89) { itemName = "Refined Fuel Can"; qty = 1; }
                    else if (roll < 0.94) { itemName = "Reinforced Titanium Plate"; qty = 1; }
                    else if (roll < 0.97) { itemName = "Grenade"; qty = rng.Next(1, 3); }
                    else
                    {
                        string[] weapons = { "Fire Axe", "Machete", "Sledgehammer", "Shotgun", "SMG" };
                        itemName = weapons[rng.Next(weapons.Length)];
                        qty = 1;
                    }
                    break;

                default: // House
                    if (roll < 0.18) { itemName = "Canned Food"; qty = rng.Next(1, 3); }
                    else if (roll < 0.32) { itemName = "Bread"; qty = rng.Next(1, 3); }
                    else if (roll < 0.44) { itemName = "Water Bottle"; qty = rng.Next(1, 3); }
                    else if (roll < 0.56) { itemName = "Bandage"; qty = rng.Next(1, 3); }
                    else if (roll < 0.68) { itemName = "Ammo"; qty = rng.Next(1, 4); }
                    else if (roll < 0.77) { itemName = "Duct Tape"; qty = rng.Next(1, 3); }
                    else if (roll < 0.85) { itemName = "Wood Log"; qty = rng.Next(1, 3); }
                    else if (roll < 0.92) { itemName = "Herbal Calming Tea"; qty = 1; }
                    else if (roll < 0.97) { itemName = "First Aid Kit"; qty = 1; }
                    else
                    {
                        string[] houseWeapons = { "Knife", "Baseball Bat", "Pistol", "Revolver" };
                        itemName = houseWeapons[rng.Next(houseWeapons.Length)];
                        qty = 1;
                    }
                    break;
            }
        }

        private void BuildInteriorLights(Transform parent, float roomW, float roomD, float wallH, Color lightColor)
        {
            int count = roomW >= 12.5f ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                float lx = count == 1 ? 0f : (i == 0 ? -roomW * 0.23f : roomW * 0.23f);
                GameObject lObj = new GameObject($"InteriorLight_{i}");
                lObj.transform.SetParent(parent, false);
                lObj.transform.position = new Vector3(lx, wallH + 1.15f, 0.2f);
                Light light = lObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = lightColor;
                light.range = Mathf.Max(roomW, roomD) * 1.15f;
                light.intensity = count == 1 ? 3.2f : 2.35f;
                light.shadows = LightShadows.None;
            }
        }

        private void SpawnInteriorEncounterIfNeeded(
            BuildingEntrance entrance,
            float roomW,
            float roomD,
            Vector3 playerSpawnPos,
            List<Rect> occupiedRects,
            System.Random rng)
        {
            _activeInteriorEnemies.Clear();
            _currentRoomIsAmbush = false;
            _currentRoomClearedNotified = false;

            // Safe stages (e.g. SpecialEvent / Starting center safe stage if needed)
            bool isSpecialSafeStage = false;
            if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
            {
                StageData st = WorldMapManager.Instance.MapGenerator.GetStage(entrance.StageX, entrance.StageY);
                if (st != null && st.biome == BiomeType.SpecialEvent)
                {
                    isSpecialSafeStage = true;
                }
            }

            // Mixed Encounters: ~60% Ambush, ~40% Safe House (deterministic per house!)
            bool isAmbush = !isSpecialSafeStage && rng.NextDouble() < 0.60;
            _currentRoomIsAmbush = isAmbush;

            if (!isAmbush) return;

            if (_clearedInteriorKeys.Contains(_currentInteriorKey))
            {
                _currentRoomClearedNotified = true;
                return;
            }

            // In Co-op Client mode, if Host is already inside this exact building, let Host sync interior zombies via NetworkEnemyBridge
            if (EmpireNetworkManager.IsOnlineClient && IsHostInSameInterior())
            {
                return;
            }

            if (GameManager.Instance == null) return;

            int distFromCenter = Mathf.Abs(entrance.StageX - 64) + Mathf.Abs(entrance.StageY - 64);
            int maxZombies = distFromCenter >= 6 ? 4 : (distFromCenter >= 3 ? 3 : 2);
            int zombieCount = rng.Next(1, maxZombies + 1);

            string[] candidateKeys = { "ZombieNormal", "ZombieRunner", "ZombieCrawler", "ZombieSpitter" };

            float halfW = roomW * 0.5f - 1.0f;
            float halfD = roomD * 0.5f - 1.0f;

            for (int i = 0; i < zombieCount; i++)
            {
                Vector3 spawnPos = Vector3.zero;
                bool found = false;

                for (int attempt = 0; attempt < 28; attempt++)
                {
                    float zx = -halfW + (float)rng.NextDouble() * (halfW * 2f);
                    float zz = -halfD + (float)rng.NextDouble() * (halfD * 2f);
                    Vector3 cand = new Vector3(zx, 0.25f, zz);

                    // Keep safe distance from entrance doorway so player is never instakilled on entry
                    if ((new Vector2(zx - playerSpawnPos.x, zz - playerSpawnPos.z)).sqrMagnitude < 3.4f * 3.4f)
                        continue;

                    Rect r = new Rect(zx - 0.55f, zz - 0.55f, 1.1f, 1.1f);
                    bool blocked = false;
                    for (int k = 0; k < occupiedRects.Count; k++)
                    {
                        if (r.Overlaps(occupiedRects[k]))
                        {
                            blocked = true;
                            break;
                        }
                    }

                    if (!blocked)
                    {
                        spawnPos = cand;
                        found = true;
                        break;
                    }
                }

                if (!found) continue;

                // Pick zombie prefab (fallback safely to DefaultEnemyPrefab)
                int poolPick = rng.Next(0, distFromCenter >= 4 ? candidateKeys.Length : 3);
                string chosenName = candidateKeys[poolPick];
                GameObject prefab = GameManager.Instance.GetPrefabByName(chosenName) ?? GameManager.Instance.DefaultEnemyPrefab;
                if (prefab == null) continue;

                string poolKey = GameManager.Instance.GetPoolKeyForPrefab(prefab);
                Quaternion rot = Quaternion.Euler(0f, (float)rng.Next(0, 360), 0f);
                GameObject enemy = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, spawnPos, rot);
                if (enemy != null)
                {
                    _activeInteriorEnemies.Add(enemy);
                }
            }

            if (_activeInteriorEnemies.Count == 0)
            {
                _currentRoomIsAmbush = false;
            }
        }

        private static GameObject CreateSolidBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (!keepCollider)
            {
                Collider col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
            }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null)
            {
                r.sharedMaterial = mat;
            }
            return go;
        }

        private static GameObject CreateCylinderProp(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return go;
        }

        // =========================================================================
        //  SCREEN FADE & LOCATION BANNER (100% English for Teko SDF Compatibility)
        // =========================================================================

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _fadeAlpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            _fadeAlpha = to;
        }

        private void ShowBanner(string title, string subtitle, Color color, float duration)
        {
            _bannerTitle = title ?? "";
            _bannerSubtitle = subtitle ?? "";
            _bannerColor = color;
            _bannerTimer = duration;
        }

        private void OnGUI()
        {
            if (_fadeAlpha > 0.005f)
            {
                if (_blackTex == null)
                {
                    _blackTex = new Texture2D(1, 1);
                    _blackTex.SetPixel(0, 0, Color.black);
                    _blackTex.Apply();
                }

                GUI.depth = -1000;
                Color prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, _fadeAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _blackTex);
                GUI.color = prev;
            }

            if (_bannerTimer > 0f && !string.IsNullOrEmpty(_bannerTitle))
            {
                if (_titleStyle == null)
                {
                    _titleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 26,
                        fontStyle = FontStyle.Bold
                    };
                }
                if (_subtitleStyle == null)
                {
                    _subtitleStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15,
                        fontStyle = FontStyle.Bold
                    };
                }

                float alpha = Mathf.Clamp01(_bannerTimer / 0.45f);
                Color prev = GUI.color;
                float w = 440f;
                float x = (Screen.width - w) * 0.5f;
                float y = 78f;

                GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                GUI.Label(new Rect(x + 2f, y + 2f, w, 34f), _bannerTitle, _titleStyle);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(x, y, w, 34f), _bannerTitle, _titleStyle);

                if (!string.IsNullOrEmpty(_bannerSubtitle))
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
                    GUI.Label(new Rect(x + 1f, y + 33f, w, 24f), _bannerSubtitle, _subtitleStyle);
                    GUI.color = new Color(_bannerColor.r, _bannerColor.g, _bannerColor.b, alpha);
                    GUI.Label(new Rect(x, y + 32f, w, 24f), _bannerSubtitle, _subtitleStyle);
                }
                GUI.color = prev;
            }
        }
    }
}

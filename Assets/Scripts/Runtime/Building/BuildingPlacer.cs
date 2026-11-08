using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// BuildingPlacer — จัดการระบบการวางสิ่งปลูกสร้างในโหมดก่อสร้าง (Build Mode)
    /// ควบคุม:
    /// - แสดงโมเดลตัวอย่างโปร่งใส (Hologram Ghost) ตามตำแหน่งเมาส์
    /// - สลับสีเขียว (วางได้) / สีแดง (วางไม่ได้/ติดสิ่งกีดขวาง/อยู่นอกระยะ)
    /// - การหมุนสิ่งปลูกสร้างด้วยปุ่ม R หรือ Mouse Scroll
    /// - คลิกซ้ายเพื่อวาง -> หักของออกจากกระเป๋า -> เกิดสิ่งปลูกสร้างจริง
    /// - คลิกขวา หรือ ESC เพื่อยกเลิก
    /// </summary>
    public class BuildingPlacer : MonoBehaviour
    {
        public static BuildingPlacer Instance { get; private set; }

        [Header("Layer Masks")]
        [Tooltip("Layer ของพื้นผิวที่อนุญาตให้วางได้ เช่น Ground, Default, Terrain")]
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Tooltip("Layer ของสิ่งกีดขวางที่ไม่ให้วางทับ เช่น Default, Enemy, Obstacle")]
        [SerializeField] private LayerMask obstacleLayerMask = ~0;

        [Header("Ghost Materials Colors")]
        [SerializeField] private Color validColor = new Color(0.2f, 1.0f, 0.4f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1.0f, 0.2f, 0.2f, 0.55f);

        [Header("Audio & VFX")]
        [SerializeField] private GameObject buildEffectPrefab;

        private BuildingData _activeData;
        private CraftingRecipe _activeRecipe;
        private GameObject _ghostInstance;
        private List<Renderer> _ghostRenderers = new List<Renderer>();
        private Material _ghostMaterialInstance;

        private float _currentRotationY = 0f;
        private bool _isValidPlacement = false;
        private Vector3 _currentPlacementPosition;
        private Quaternion _currentPlacementRotation;

        public bool IsInBuildMode => _activeData != null && _ghostInstance != null;
        public BuildingData ActiveBuilding => _activeData;

        public event Action<BuildingData> OnBuildModeStarted;
        public event Action OnBuildModeEnded;
        public event Action<BuildingObject> OnBuildingPlaced;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            CreateGhostMaterial();
        }

        private void CreateGhostMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");

            if (shader != null)
            {
                _ghostMaterialInstance = new Material(shader);
                _ghostMaterialInstance.name = "Ghost_Placement_Material";

                // Setup transparency if Standard shader
                if (_ghostMaterialInstance.HasProperty("_Mode"))
                {
                    _ghostMaterialInstance.SetFloat("_Mode", 3f); // Transparent
                    _ghostMaterialInstance.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _ghostMaterialInstance.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    _ghostMaterialInstance.SetInt("_ZWrite", 0);
                    _ghostMaterialInstance.DisableKeyword("_ALPHATEST_ON");
                    _ghostMaterialInstance.EnableKeyword("_ALPHABLEND_ON");
                    _ghostMaterialInstance.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    _ghostMaterialInstance.renderQueue = 3000;
                }
            }
        }

        private void Update()
        {
            if (!IsInBuildMode) return;

            HandleCancelInput();
            HandleRotationInput();
            UpdateGhostPositionAndValidation();
            HandleConfirmPlacement();
        }

        /// <summary>
        /// เริ่มต้นเข้าสู่โหมดวางสิ่งปลูกสร้าง
        /// </summary>
        public void StartPlacement(BuildingData data, CraftingRecipe recipe = null)
        {
            if (data == null) return;

            CancelPlacement();

            _activeData = data;
            _activeRecipe = recipe;
            _currentRotationY = 0f;

            CreateGhostInstance();
            OnBuildModeStarted?.Invoke(_activeData);
            Debug.Log($"[BuildingPlacer] Entered build mode for '{data.buildingName}'. Press R to rotate, LMB to place, RMB/ESC to cancel.");
        }

        /// <summary>
        /// ยกเลิกโหมดวางสิ่งปลูกสร้าง
        /// </summary>
        public void CancelPlacement()
        {
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
            }

            _activeData = null;
            _activeRecipe = null;
            _ghostRenderers.Clear();

            OnBuildModeEnded?.Invoke();
        }

        private void CreateGhostInstance()
        {
            if (_activeData == null) return;

            GameObject sourcePrefab = _activeData.ghostPrefab != null ? _activeData.ghostPrefab : _activeData.prefab;
            if (sourcePrefab == null)
            {
                Debug.LogWarning($"[BuildingPlacer] Cannot create ghost: Prefab for '{_activeData.buildingName}' is missing!");
                return;
            }

            _ghostInstance = Instantiate(sourcePrefab);
            _ghostInstance.name = $"[Ghost]_{_activeData.buildingName}";

            // ปิด Collider, NavMeshObstacle, Light, Audio, และ Scripts อื่นๆ บนตัว Ghost ทั้งหมด
            Collider[] colliders = _ghostInstance.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in colliders) c.enabled = false;

            UnityEngine.AI.NavMeshObstacle[] obstacles = _ghostInstance.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true);
            foreach (var o in obstacles) o.enabled = false;

            Light[] lights = _ghostInstance.GetComponentsInChildren<Light>(true);
            foreach (var l in lights) l.enabled = false;

            MonoBehaviour[] scripts = _ghostInstance.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var s in scripts)
            {
                if (s != null && s.GetType() != typeof(BuildingPlacer))
                {
                    s.enabled = false;
                }
            }

            // รวบรวม Renderer ทั้งหมดเพื่อเปลี่ยนเป็น Material สีเขียว/แดง
            _ghostRenderers.Clear();
            _ghostInstance.GetComponentsInChildren(true, _ghostRenderers);

            ApplyGhostColor(validColor);
        }

        private void ApplyGhostColor(Color color)
        {
            if (_ghostMaterialInstance == null) CreateGhostMaterial();
            if (_ghostMaterialInstance != null)
            {
                if (_ghostMaterialInstance.HasProperty("_BaseColor"))
                    _ghostMaterialInstance.SetColor("_BaseColor", color);
                else if (_ghostMaterialInstance.HasProperty("_Color"))
                    _ghostMaterialInstance.SetColor("_Color", color);

                for (int i = 0; i < _ghostRenderers.Count; i++)
                {
                    if (_ghostRenderers[i] != null)
                    {
                        Material[] mats = new Material[_ghostRenderers[i].sharedMaterials.Length];
                        for (int m = 0; m < mats.Length; m++)
                        {
                            mats[m] = _ghostMaterialInstance;
                        }
                        _ghostRenderers[i].materials = mats;
                    }
                }
            }
        }

        private void HandleCancelInput()
        {
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CancelPlacement();
                return;
            }
        }

        private void HandleRotationInput()
        {
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotationY = (_currentRotationY + 45f) % 360f;
            }

            if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.05f)
                {
                    float delta = scroll > 0 ? 45f : -45f;
                    _currentRotationY = (_currentRotationY + delta + 360f) % 360f;
                }
            }
        }

        private void UpdateGhostPositionAndValidation()
        {
            if (_ghostInstance == null || _activeData == null || Camera.main == null) return;

            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            Vector3 hitPoint = Vector3.zero;
            bool hitGround = false;

            if (Physics.Raycast(ray, out RaycastHit hit, 150f, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                hitPoint = hit.point;
                hitGround = true;
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float enter))
                {
                    hitPoint = ray.GetPoint(enter);
                    hitGround = true;
                }
            }

            if (!hitGround)
            {
                _isValidPlacement = false;
                ApplyGhostColor(invalidColor);
                return;
            }

            // Snap to Grid (ถ้าเปิดใช้งาน)
            if (_activeData.snapToGrid && _activeData.gridSize > 0.05f)
            {
                float g = _activeData.gridSize;
                hitPoint.x = Mathf.Round(hitPoint.x / g) * g;
                hitPoint.z = Mathf.Round(hitPoint.z / g) * g;
            }

            _currentPlacementPosition = hitPoint;
            _currentPlacementRotation = Quaternion.Euler(0f, _currentRotationY, 0f);

            _ghostInstance.transform.position = _currentPlacementPosition;
            _ghostInstance.transform.rotation = _currentPlacementRotation;

            // ตรวจสอบความถูกต้องในการวาง:
            // 1. ตรวจสอบระยะห่างจากผู้เล่น
            bool inRange = true;
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                float dist = Vector3.Distance(hitPoint, player.transform.position);
                inRange = dist <= _activeData.buildRange;
            }

            // 2. ตรวจสอบการชนกับสิ่งกีดขวาง (OverlapBox)
            Vector3 center = _currentPlacementPosition + Vector3.up * (_activeData.footprintSize.y * 0.5f);
            Vector3 halfExtents = _activeData.footprintSize * 0.48f;

            Collider[] hits = Physics.OverlapBox(center, halfExtents, _currentPlacementRotation, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            bool isObstructed = false;

            foreach (Collider c in hits)
            {
                if (c == null || c.isTrigger) continue;
                if (player != null && (c.gameObject == player.gameObject || c.transform.IsChildOf(player.transform)))
                {
                    isObstructed = true; // ห้ามวางทับตัวผู้เล่น
                    break;
                }

                // ข้าม Collider พื้นดิน
                if (((1 << c.gameObject.layer) & groundLayerMask) != 0 && c.gameObject.name.ToLowerInvariant().Contains("ground"))
                {
                    continue;
                }

                isObstructed = true;
                break;
            }

            // 3. ตรวจสอบวัตถุดิบในกระเป๋า
            bool hasMaterials = true;
            PlayerInventory inv = PlayerInventory.Instance;
            if (!CraftingDatabase.IsFreeCraft && inv != null)
            {
                List<CraftingIngredient> ingredients = _activeData.ingredients;
                if (_activeRecipe != null && _activeRecipe.ingredients != null && _activeRecipe.ingredients.Count > 0)
                {
                    ingredients = _activeRecipe.ingredients;
                }

                if (ingredients != null)
                {
                    foreach (var ing in ingredients)
                    {
                        if (inv.GetItemCount(ing.ResolvedItemName) < ing.amount)
                        {
                            hasMaterials = false;
                            break;
                        }
                    }
                }
            }

            _isValidPlacement = inRange && !isObstructed && hasMaterials;
            ApplyGhostColor(_isValidPlacement ? validColor : invalidColor);
        }

        private void HandleConfirmPlacement()
        {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // ตรวจสอบว่าไม่ได้กดทับ UI ปุ่มอื่นๆ
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!_isValidPlacement)
            {
                Debug.Log("[BuildingPlacer] Cannot place: Position obstructed, out of range, or not enough materials!");
                return;
            }

            ConfirmPlacement();
        }

        private void ConfirmPlacement()
        {
            if (_activeData == null || _activeData.prefab == null) return;

            PlayerInventory inv = PlayerInventory.Instance;

            // 1. หักวัตถุดิบออกจากกระเป๋า (ถ้าไม่ใช่โหมด Free Craft)
            if (!CraftingDatabase.IsFreeCraft && inv != null)
            {
                List<CraftingIngredient> ingredients = _activeData.ingredients;
                if (_activeRecipe != null && _activeRecipe.ingredients != null && _activeRecipe.ingredients.Count > 0)
                {
                    ingredients = _activeRecipe.ingredients;
                }

                if (ingredients != null)
                {
                    foreach (var ing in ingredients)
                    {
                        inv.RemoveItem(ing.ResolvedItemName, ing.amount);
                    }
                }
            }

            // 2. เกิดสิ่งปลูกสร้างจริงลงในฉาก
            GameObject instance = Instantiate(_activeData.prefab, _currentPlacementPosition, _currentPlacementRotation);
            instance.name = $"{_activeData.buildingName}_{System.DateTime.Now.Ticks % 10000}";

            BuildingObject buildingComp = instance.GetComponent<BuildingObject>();
            if (buildingComp == null)
            {
                buildingComp = instance.AddComponent<BuildingObject>();
            }
            buildingComp.Initialize(_activeData);

            // 3. แสดง VFX ก่อสร้าง
            if (buildEffectPrefab != null)
            {
                Instantiate(buildEffectPrefab, _currentPlacementPosition + Vector3.up * 0.2f, Quaternion.identity);
            }

            Debug.Log($"[BuildingPlacer] Successfully placed '{_activeData.buildingName}' at {_currentPlacementPosition}!");
            OnBuildingPlaced?.Invoke(buildingComp);

            // 4. ถ้ากด Shift ค้างไว้ จะอนุญาตให้วางต่อชิ้นถัดไปได้ (หากยังมีของพอ)
            bool keepPlacing = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
            if (keepPlacing)
            {
                UpdateGhostPositionAndValidation();
                if (!_isValidPlacement)
                {
                    CancelPlacement();
                }
            }
            else
            {
                CancelPlacement();
            }
        }
    }
}


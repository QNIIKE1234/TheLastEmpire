using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// GeneratorBuilding — เครื่องกำเนิดไฟฟ้าปฏิกรณ์ฟิวชัน (BUILD0015_CompactFusionGenerator)
    /// คุณสมบัติ:
    /// - ผลิตและจ่ายพลังงานไฟฟ้าแก่ระบบป้องกันฐานในรัศมี 20 เมตร
    /// - ทำหน้าที่จ่ายไฟให้ Automated Sentry Turret และ Xenocrystal Shock Tower
    /// - แสดงสถานะกำลังไฟฟ้าและรัศมีจ่ายไฟ
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class GeneratorBuilding : MonoBehaviour
    {
        public static readonly List<GeneratorBuilding> ActiveGenerators = new List<GeneratorBuilding>();

        [Header("Power Grid Settings")]
        [Tooltip("รัศมีสายส่งพลังงานไฟฟ้า (เมตร)")]
        [SerializeField] private float powerRadius = 20.0f;

        [Tooltip("สถานะการทำงานของเครื่องกำเนิดไฟฟ้า")]
        [SerializeField] private bool isRunning = true;

        [Header("Visual & Effects")]
        [SerializeField] private Light powerCoreLight;
        [SerializeField] private float interactionDistance = 2.5f;

        private BuildingObject _buildingObject;
        private Transform _playerTransform;
        private bool _isPlayerNearby = false;

        public float PowerRadius => powerRadius;
        public bool IsRunning => isRunning && (_buildingObject == null || !_buildingObject.IsDead);

        /// <summary>
        /// ตรวจสอบว่าพิกัดที่กำหนดอยู่ในรัศมีจ่ายไฟของ Generator ตัวใดตัวหนึ่งที่ทำงานอยู่หรือไม่
        /// </summary>
        public static bool IsPositionPowered(Vector3 position)
        {
            for (int i = 0; i < ActiveGenerators.Count; i++)
            {
                GeneratorBuilding gen = ActiveGenerators[i];
                if (gen != null && gen.IsRunning)
                {
                    if (Vector3.Distance(position, gen.transform.position) <= gen.powerRadius)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void Awake()
        {
            _buildingObject = GetComponent<BuildingObject>();
            if (powerCoreLight == null)
            {
                powerCoreLight = GetComponentInChildren<Light>();
            }
        }

        private void OnEnable()
        {
            if (!ActiveGenerators.Contains(this))
            {
                ActiveGenerators.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveGenerators.Remove(this);
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }

        private void Update()
        {
            if (powerCoreLight != null)
            {
                powerCoreLight.enabled = IsRunning;
                if (IsRunning)
                {
                    powerCoreLight.intensity = 2.0f + Mathf.PingPong(Time.time * 2f, 0.8f);
                }
            }

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = dist <= interactionDistance;

            if (_isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ToggleGenerator();
            }
        }

        public void ToggleGenerator()
        {
            isRunning = !isRunning;

            if (DamageNumberManager.Instance != null)
            {
                DamageNumberManager.Instance.SpawnDamageNumber(
                    transform.position + Vector3.up * 2.0f,
                    0f,
                    false,
                    isRunning ? "POWER GRID ACTIVE" : "POWER OFFLINE"
                );
            }

            Debug.Log($"[GeneratorBuilding] Generator is now {(isRunning ? "ONLINE" : "OFFLINE")}.");
        }

        private void OnDestroy()
        {
            ActiveGenerators.Remove(this);
        }

        private void OnGUI()
        {
            if (_isPlayerNearby)
            {
                Camera cam = Camera.main;
                if (cam == null) return;

                Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * 1.8f);
                if (screenPos.z > 0)
                {
                    float width = 260f;
                    float height = 40f;
                    Rect rect = new Rect(screenPos.x - width * 0.5f, Screen.height - screenPos.y - height * 0.5f, width, height);

                    GUI.color = isRunning ? new Color(0.2f, 1f, 0.4f, 0.95f) : new Color(1f, 0.3f, 0.3f, 0.95f);
                    string status = isRunning ? "[E] ปิดเครื่อง (Power: ONLINE)" : "[E] เปิดเครื่อง (Power: OFFLINE)";
                    GUI.Box(rect, status);
                }
            }
        }
    }
}


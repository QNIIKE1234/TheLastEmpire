using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire
{
    /// <summary>
    /// GeneratorBuilding — เครื่องกำเนิดไฟฟ้าปฏิกรณ์ฟิวชัน (BUILD0015_CompactFusionGenerator)
    /// คุณสมบัติ:
    /// - ผลิตและจ่ายพลังงานไฟฟ้าแก่ระบบป้องกันฐานในรัศมี 20 เมตร
    /// - ทำหน้าที่จ่ายไฟให้ Automated Sentry Turret และ Xenocrystal Shock Tower
    /// - รวมศูนย์ Interaction Prompt ผ่าน Loot_HUD หรือเปิด Generator_HUD
    /// </summary>
    [RequireComponent(typeof(BuildingObject))]
    public class GeneratorBuilding : MonoBehaviour, IBuildingInteractable
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

        public float PowerRadius => powerRadius;
        public bool IsRunning => isRunning && (_buildingObject == null || !_buildingObject.IsDead);

        // IBuildingInteractable implementation
        public Transform InteractionTransform => transform;
        public Vector3 PromptWorldOffset => Vector3.up * 1.8f;
        public float InteractionDistance => interactionDistance;
        public bool CanInteract => _buildingObject == null || !_buildingObject.IsDead;

        public string GetInteractionPrompt()
        {
            return isRunning 
                ? "<color=yellow><b>[E]</b></color> Generator (ON)" 
                : "<color=yellow><b>[E]</b></color> Generator (OFF)";
        }

        public void OnInteract()
        {
            // ตรวจสอบว่ามี Generator_HUD ให้เปิดหรือไม่
            GameObject canvasGo = GameObject.Find("BuildingUI_Canvas");
            if (canvasGo != null)
            {
                Transform hud = canvasGo.transform.Find("Generator_HUD");
                if (hud != null && hud.childCount > 0)
                {
                    hud.gameObject.SetActive(true);
                    Debug.Log("[GeneratorBuilding] Opened Generator_HUD.");
                    return;
                }
            }

            ToggleGenerator();
        }

        public void OnSecondaryInteract()
        {
        }

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
            BuildingInteractionRegistry.Register(this);
        }

        private void OnDisable()
        {
            ActiveGenerators.Remove(this);
            BuildingInteractionRegistry.Unregister(this);
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
    }
}

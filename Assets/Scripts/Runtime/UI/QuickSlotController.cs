using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// Controller สำหรับจัดการระบบ Quick Slot (ช่อง 1 - 5)
    /// รองรับการใส่ไอเทมทั้งแบบ Drag & Drop และ Hover + กดเลข 1-5
    /// และกดใช้/สลับอาวุธตอนเล่นเกมปกติ
    /// ข้อมูลถูกบันทึกใน PlayerPrefs อัตโนมัติ
    /// </summary>
    public class QuickSlotController : MonoBehaviour
    {
        public static QuickSlotController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindFirstObjectByType<QuickSlotController>();
                    if (_instance == null)
                    {
                        // พยายามหาจาก QuickSlot_HUD ใน Scene ก่อน
                        GameObject hud = GameObject.Find("QuickSlot_HUD");
                        if (hud != null)
                        {
                            _instance = hud.AddComponent<QuickSlotController>();
                        }
                        else
                        {
                            GameObject go = new GameObject("QuickSlotController");
                            _instance = go.AddComponent<QuickSlotController>();
                        }
                    }
                }
                return _instance;
            }
        }
        private static QuickSlotController _instance;

        public const int MaxSlots = 5;
        private const string PrefsPrefix = "QuickSlot_";

        [Header("UI References")]
        [Tooltip("Root GameObject ของ QuickSlot_HUD")]
        [SerializeField] private GameObject quickSlotHUD;

        [Tooltip("รายการ QuickSlotUIItem ทั้ง 5 ช่อง (ถ้าว่างจะ Auto-discover จาก QuickSlot_HUD)")]
        [SerializeField] private List<QuickSlotUIItem> slotItems = new List<QuickSlotUIItem>();

        private string[] _slotItemNames = new string[MaxSlots];
        private PlayerInventory _inventory;
        private PlayerController _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            if (_instance == null)
            {
                var found = UnityEngine.Object.FindFirstObjectByType<QuickSlotController>();
                if (found != null)
                {
                    _instance = found;
                }
                else
                {
                    GameObject hud = GameObject.Find("QuickSlot_HUD");
                    if (hud != null)
                    {
                        _instance = hud.AddComponent<QuickSlotController>();
                    }
                }
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            LoadSavedSlots();
            DiscoverSlots();
        }

        private void Start()
        {
            HookPlayerReferences();
            RefreshUI();
        }

        private void OnEnable()
        {
            HookPlayerReferences();
            RefreshUI();
        }

        private void OnDestroy()
        {
            UnhookPlayerReferences();
        }

        private void HookPlayerReferences()
        {
            if (_player == null)
            {
                _player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (_player != null)
                {
                    _player.OnAmmoChanged -= RefreshUI;
                    _player.OnAmmoChanged += RefreshUI;
                }
            }

            if (_inventory == null)
            {
                if (_player != null)
                {
                    _inventory = _player.GetComponent<PlayerInventory>();
                }
                else
                {
                    _inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
                }

                if (_inventory != null)
                {
                    _inventory.OnInventoryChanged -= RefreshUI;
                    _inventory.OnInventoryChanged += RefreshUI;
                }
            }
        }

        private void UnhookPlayerReferences()
        {
            if (_player != null)
            {
                _player.OnAmmoChanged -= RefreshUI;
            }

            if (_inventory != null)
            {
                _inventory.OnInventoryChanged -= RefreshUI;
            }
        }

        /// <summary>
        /// ค้นหา QuickSlot1 - QuickSlot5 จาก QuickSlot_HUD
        /// </summary>
        public void DiscoverSlots()
        {
            if (quickSlotHUD == null)
            {
                quickSlotHUD = GameObject.Find("QuickSlot_HUD");
            }

            if (slotItems == null)
            {
                slotItems = new List<QuickSlotUIItem>();
            }

            if (slotItems.Count < MaxSlots && quickSlotHUD != null)
            {
                slotItems.Clear();

                Transform panelT = quickSlotHUD.transform.Find("Panel");
                Transform searchRoot = panelT != null ? panelT : quickSlotHUD.transform;

                // ค้นหา QuickSlot1 .. QuickSlot5 ตามชื่อ
                for (int i = 0; i < MaxSlots; i++)
                {
                    string slotName = $"QuickSlot{i + 1}";
                    Transform childT = searchRoot.Find(slotName);
                    if (childT == null)
                    {
                        // Fallback หาด้วย index
                        if (i < searchRoot.childCount)
                        {
                            childT = searchRoot.GetChild(i);
                        }
                    }

                    if (childT != null)
                    {
                        QuickSlotUIItem uiItem = childT.GetComponent<QuickSlotUIItem>();
                        if (uiItem == null)
                        {
                            uiItem = childT.gameObject.AddComponent<QuickSlotUIItem>();
                        }
                        uiItem.Setup(i);
                        slotItems.Add(uiItem);
                    }
                }
            }
        }

        private void LoadSavedSlots()
        {
            for (int i = 0; i < MaxSlots; i++)
            {
                _slotItemNames[i] = PlayerPrefs.GetString($"{PrefsPrefix}{i}", "");
            }
        }

        private void SaveSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return;
            string val = _slotItemNames[index] ?? "";
            if (string.IsNullOrEmpty(val))
            {
                PlayerPrefs.DeleteKey($"{PrefsPrefix}{index}");
            }
            else
            {
                PlayerPrefs.SetString($"{PrefsPrefix}{index}", val);
            }
            PlayerPrefs.Save();
        }

        // =====================================================
        //  Public API
        // =====================================================

        /// <summary>
        /// บรรจุไอเทมลง Quick Slot
        /// </summary>
        public void AssignSlot(int index, string itemName)
        {
            if (index < 0 || index >= MaxSlots) return;
            if (string.IsNullOrEmpty(itemName) || itemName == "- Empty -") return;

            // ตรวจสอบว่าไอเทมมีอยู่จริงใน ItemDatabase
            if (ItemDatabase.Instance != null)
            {
                var itemData = ItemDatabase.Instance.GetItemByName(itemName);
                if (itemData != null && !string.IsNullOrEmpty(itemData.itemName))
                {
                    itemName = itemData.itemName;
                }
            }

            _slotItemNames[index] = itemName;
            SaveSlot(index);
            RefreshUI();

            Debug.Log($"[QuickSlot] Assigned Slot {index + 1}: {itemName}");
        }

        /// <summary>
        /// เคลียร์ Quick Slot ให้ว่าง
        /// </summary>
        public void ClearSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return;

            _slotItemNames[index] = "";
            SaveSlot(index);
            RefreshUI();

            Debug.Log($"[QuickSlot] Cleared Slot {index + 1}");
        }

        /// <summary>
        /// ตรวจสอบว่าช่องนี้มีไอเทมตั้งค่าไว้หรือไม่
        /// </summary>
        public bool HasSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return false;
            return !string.IsNullOrEmpty(_slotItemNames[index]);
        }

        /// <summary>
        /// ดึงชื่อไอเทมใน Quick Slot
        /// </summary>
        public string GetSlotItem(int index)
        {
            if (index < 0 || index >= MaxSlots) return null;
            return _slotItemNames[index];
        }

        /// <summary>
        /// ใช้งานไอเทมใน Quick Slot (สลับอาวุธ หรือ กิน/ใช้ยา)
        /// </summary>
        public void UseSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return;

            string itemName = _slotItemNames[index];
            if (string.IsNullOrEmpty(itemName)) return;

            HookPlayerReferences();

            if (_inventory == null)
            {
                Debug.LogWarning("[QuickSlot] PlayerInventory not found!");
                return;
            }

            // ถ้าเป็น Weapon หรือ Consumable ให้เรียก PlayerInventory.UseItem()
            bool used = _inventory.UseItem(itemName);
            if (used)
            {
                Debug.Log($"[QuickSlot] Used Slot {index + 1}: {itemName}");
            }
            else
            {
                Debug.Log($"[QuickSlot] Could not use {itemName} (maybe 0 count or condition not met).");
            }

            RefreshUI();
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            {
                InventoryUI.Instance.RefreshUI();
            }
        }

        /// <summary>
        /// อัปเดตการแสดงผล HUD ทั้ง 5 ช่อง
        /// </summary>
        public void RefreshUI()
        {
            if (slotItems == null || slotItems.Count == 0)
            {
                DiscoverSlots();
            }

            HookPlayerReferences();

            string currentEquippedRanged = _player != null ? (_player.CurrentWeaponName ?? "").Trim().ToLower() : "";
            string currentEquippedMelee = _player != null ? (_player.CurrentMeleeWeaponName ?? "").Trim().ToLower() : "";

            for (int i = 0; i < MaxSlots; i++)
            {
                if (i >= slotItems.Count || slotItems[i] == null) continue;

                string itemName = _slotItemNames[i];
                if (string.IsNullOrEmpty(itemName))
                {
                    slotItems[i].SetData("", null, 0, false, false);
                    continue;
                }

                ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
                Sprite icon = itemData != null ? itemData.icon : null;
                int count = _inventory != null ? _inventory.GetItemCount(itemName) : 0;

                bool isWeapon = false;
                bool isConsumable = false;

                if (itemData != null)
                {
                    isWeapon = itemData.type == ItemType.RangedWeapon ||
                               itemData.type == ItemType.MeleeWeapon ||
                               itemData.type == ItemType.ThrowingWeapon;

                    isConsumable = itemData.type == ItemType.Potion ||
                                   itemData.type == ItemType.Bread ||
                                   itemData.type == ItemType.Ammo;
                }

                bool isEquipped = false;
                if (isWeapon)
                {
                    string cleanName = itemName.Trim().ToLower();
                    if (!string.IsNullOrEmpty(currentEquippedRanged) &&
                        (cleanName.Contains(currentEquippedRanged) || currentEquippedRanged.Contains(cleanName) ||
                        (cleanName.Contains("pist") && currentEquippedRanged.Contains("pist"))))
                    {
                        isEquipped = true;
                    }
                    else if (!string.IsNullOrEmpty(currentEquippedMelee) &&
                        (cleanName.Contains(currentEquippedMelee) || currentEquippedMelee.Contains(cleanName)))
                    {
                        isEquipped = true;
                    }
                }

                slotItems[i].SetData(itemName, icon, count, isConsumable, isEquipped);
            }
        }
    }
}

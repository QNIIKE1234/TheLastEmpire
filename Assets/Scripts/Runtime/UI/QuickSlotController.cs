using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLastEmpire
{
    /// <summary>
    /// Controller สำหรับจัดการระบบ Quick Slot (ช่อง 1 - 5)
    /// รองรับการใส่ไอเทมทั้งแบบ Drag & Drop และ Hover + กดเลข 1-5
    /// ควบคุมการเลือกใช้งานอาวุธทีละอย่าง (Single Active Weapon/Item)
    /// กดยิง/ฟัน/กินอาหารด้วยการคลิกซ้ายตามไอเทมที่ถืออยู่ในมือ
    /// และดักกรองไอเทมที่ไม่สามารถใส่ Quick Slot ได้ (เช่น กระสุน, วัสดุ)
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
        private int _activeSlotIndex = 0;
        private PlayerInventory _inventory;
        private PlayerController _player;

        public int ActiveSlotIndex => _activeSlotIndex;

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
            EnsureDefaultStartingSlots();
            SelectSlot(_activeSlotIndex);
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
        /// ตรวจสอบว่าไอเทมนี้สามารถใส่ใน Quick Slot และถือในมือได้หรือไม่
        /// ห้ามกระสุน (Ammo), วัตถุดิบ (Material), ไอเทมเบ็ดเตล็ด (ETC)
        /// อนุญาต: ปืน (RangedWeapon), อาวุธประชิด (MeleeWeapon), อาวุธขว้าง (ThrowingWeapon), ยา (Potion), อาหาร (Bread)
        /// </summary>
        public static bool IsItemQuickSlotEligible(string itemName)
        {
            if (string.IsNullOrEmpty(itemName) || itemName == "- Empty -")
                return false;

            string clean = itemName.Trim().ToLower();

            // ดักกระสุนทุกชนิด ป้องกันการเอาใส่ quick slot แล้วเกิดบัคไร้รูป/บัคมือเปล่า
            if (clean.Contains("ammo") || clean.Contains("bullet") || clean.Contains("shell") || clean.Contains("cartridge") || clean.Contains("bolt"))
            {
                return false;
            }

            if (ItemDatabase.Instance != null)
            {
                ItemData item = ItemDatabase.Instance.GetItemByName(itemName);
                if (item != null)
                {
                    if (item.type == ItemType.Ammo || item.type == ItemType.Material)
                        return false;

                    return item.type == ItemType.RangedWeapon ||
                           item.type == ItemType.MeleeWeapon ||
                           item.type == ItemType.ThrowingWeapon ||
                           item.type == ItemType.Potion ||
                           item.type == ItemType.Bread;
                }
            }

            // Fallback เช็คชื่อถ้ายังไม่โหลด ItemDatabase
            if (clean.Contains("knife") || clean.Contains("bat") || clean.Contains("sword") || clean.Contains("axe") || clean.Contains("dagger"))
                return true;
            if (clean.Contains("pistol") || clean.Contains("rifle") || clean.Contains("shotgun") || clean.Contains("smg") || clean.Contains("revolver") || clean.Contains("crossbow"))
                return true;
            if (clean.Contains("bread") || clean.Contains("potion") || clean.Contains("medkit") || clean.Contains("food") || clean.Contains("apple"))
                return true;

            return false;
        }

        /// <summary>
        /// ตั้งค่าเริ่มต้นถ้า QuickSlot ยังไม่มี: ช่อง 1 = ปืนเริ่มต้น, ช่อง 2 = มีด Knife
        /// </summary>
        public void EnsureDefaultStartingSlots()
        {
            HookPlayerReferences();

            // ช่อง 1 (Index 0): ปืนเริ่มต้น
            if (string.IsNullOrEmpty(_slotItemNames[0]) || !IsItemQuickSlotEligible(_slotItemNames[0]))
            {
                string startingGun = "Rifle";
                if (_player != null && _player.WeaponsList != null && _player.WeaponsList.Count > 0)
                {
                    startingGun = !string.IsNullOrEmpty(_player.CurrentWeaponName) ? _player.CurrentWeaponName : _player.WeaponsList[0].weaponName;
                }
                else if (_player != null && _player.ActiveCharacterData != null && _player.ActiveCharacterData.startingWeapons != null && _player.ActiveCharacterData.startingWeapons.Count > 0)
                {
                    startingGun = _player.ActiveCharacterData.startingWeapons[0];
                }
                _slotItemNames[0] = startingGun;
                SaveSlot(0);
            }

            // ช่อง 2 (Index 1): มีด Knife
            if (string.IsNullOrEmpty(_slotItemNames[1]) || !IsItemQuickSlotEligible(_slotItemNames[1]))
            {
                string startingMelee = "Knife";
                if (_player != null && _player.MeleeWeaponsList != null && _player.MeleeWeaponsList.Count > 0)
                {
                    startingMelee = !string.IsNullOrEmpty(_player.CurrentMeleeWeaponName) ? _player.CurrentMeleeWeaponName : _player.MeleeWeaponsList[0].weaponName;
                }
                else if (_player != null && _player.ActiveCharacterData != null && _player.ActiveCharacterData.startingWeapons != null && _player.ActiveCharacterData.startingWeapons.Count > 1)
                {
                    startingMelee = _player.ActiveCharacterData.startingWeapons[1];
                }
                _slotItemNames[1] = startingMelee;
                SaveSlot(1);
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

                for (int i = 0; i < MaxSlots; i++)
                {
                    string slotName = $"QuickSlot{i + 1}";
                    Transform childT = searchRoot.Find(slotName);
                    if (childT == null && i < searchRoot.childCount)
                    {
                        childT = searchRoot.GetChild(i);
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
                string val = PlayerPrefs.GetString($"{PrefsPrefix}{i}", "");
                // ล้างไอเทมที่ไม่ผ่านเงื่อนไข (เช่น ถ้าผู้เล่นเคยลากกระสุนลง slot ไว้ก่อนหน้า)
                if (!string.IsNullOrEmpty(val) && !IsItemQuickSlotEligible(val))
                {
                    val = "";
                    PlayerPrefs.DeleteKey($"{PrefsPrefix}{i}");
                }
                _slotItemNames[i] = val;
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

            // ตรวจสอบความถูกต้องของไอเทม
            if (!IsItemQuickSlotEligible(itemName))
            {
                Debug.LogWarning($"[QuickSlot] Rejecting '{itemName}': Item is not eligible for quick slot (Ammo/Material cannot be assigned).");
                return;
            }

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

            ClearSlotDataOnly(index);
            RefreshUI();
            Debug.Log($"[QuickSlot] Cleared Slot {index + 1}");
        }

        private void ClearSlotDataOnly(int index)
        {
            if (index < 0 || index >= MaxSlots) return;

            _slotItemNames[index] = "";
            SaveSlot(index);

            if (_activeSlotIndex == index)
            {
                SwitchToAvailableWeapon();
            }
        }

        /// <summary>
        /// ใช้งานหรือเลือก Quick Slot (รองรับทั้ง UseSlot และ SelectSlot)
        /// </summary>
        public void UseSlot(int index)
        {
            SelectSlot(index);
        }

        public bool HasSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return false;
            return !string.IsNullOrEmpty(_slotItemNames[index]);
        }

        public string GetSlotItem(int index)
        {
            if (index < 0 || index >= MaxSlots) return null;
            return _slotItemNames[index];
        }

        /// <summary>
        /// เลือกช่อง Quick Slot เพื่อถือในมือ (กดเลข 1 - 5 หรือคลิกที่ช่อง Quick Slot)
        /// สามารถเป็นปืน, มีด, หรือของกิน (Bread/Potion)
        /// </summary>
        public void SelectSlot(int index)
        {
            if (index < 0 || index >= MaxSlots) return;

            string itemName = _slotItemNames[index];
            if (string.IsNullOrEmpty(itemName))
            {
                Debug.Log($"[QuickSlot] Slot {index + 1} is empty.");
                return;
            }

            if (!IsItemQuickSlotEligible(itemName))
            {
                Debug.LogWarning($"[QuickSlot] Slot {index + 1} contains ineligible item '{itemName}'.");
                return;
            }

            HookPlayerReferences();
            if (_player == null) return;

            ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemByName(itemName) : null;
            ItemType itemType = itemData != null ? itemData.type : ItemType.ETC;

            bool isRanged = (itemType == ItemType.RangedWeapon) ||
                            (_player.WeaponsList != null && _player.WeaponsList.Exists(w => w.weaponName.Equals(itemName, StringComparison.OrdinalIgnoreCase)));

            bool isMelee = (itemType == ItemType.MeleeWeapon) ||
                           (_player.MeleeWeaponsList != null && _player.MeleeWeaponsList.Exists(m => m.weaponName.Equals(itemName, StringComparison.OrdinalIgnoreCase)));

            bool isConsumable = (itemType == ItemType.Potion || itemType == ItemType.Bread);

            if (isRanged)
            {
                _activeSlotIndex = index;
                _player.SetHeldRangedWeapon(itemName);
                Debug.Log($"[QuickSlot] Selected Ranged Weapon: {itemName} (Slot {index + 1})");
            }
            else if (isMelee)
            {
                _activeSlotIndex = index;
                _player.SetHeldMeleeWeapon(itemName);
                Debug.Log($"[QuickSlot] Selected Melee Weapon: {itemName} (Slot {index + 1})");
            }
            else if (isConsumable)
            {
                int count = _inventory != null ? _inventory.GetItemCount(itemName) : 0;
                if (count <= 0)
                {
                    Debug.Log($"[QuickSlot] Cannot select {itemName}: You don't have any in inventory.");
                    return;
                }

                _activeSlotIndex = index;
                _player.SetHeldConsumable(itemName);
                Debug.Log($"[QuickSlot] Selected Consumable to Hand: {itemName} (Slot {index + 1})");
            }
            else
            {
                // Fallback attempt
                _activeSlotIndex = index;
                _player.SetHeldRangedWeapon(itemName);
            }

            RefreshUI();
            _player.NotifyWeaponChanged();
        }

        /// <summary>
        /// ใช้ไอเทมประเภท Consumable ที่กำลังถืออยู่ในมือเมื่อผู้เล่นคลิกซ้าย
        /// </summary>
        public void UseActiveHeldItem()
        {
            HookPlayerReferences();
            if (_player == null || _inventory == null) return;

            if (!_player.IsHoldingConsumable) return;

            string consumableName = _player.HeldConsumableName;
            if (string.IsNullOrEmpty(consumableName)) return;

            int countBefore = _inventory.GetItemCount(consumableName);
            if (countBefore <= 0)
            {
                SwitchToAvailableWeapon();
                RefreshUI();
                return;
            }

            bool used = _inventory.UseItem(consumableName);
            if (used)
            {
                Debug.Log($"[QuickSlot] Left-click used consumable: {consumableName}");
                int countAfter = _inventory.GetItemCount(consumableName);
                if (countAfter <= 0)
                {
                    // กินหมดแล้ว เคลียร์ช่อง Quick Slot ที่ใส่ไอเทมนี้ทันทีตามคำสั่งผู้ใช้
                    for (int i = 0; i < MaxSlots; i++)
                    {
                        if (!string.IsNullOrEmpty(_slotItemNames[i]) && _slotItemNames[i].Equals(consumableName, StringComparison.OrdinalIgnoreCase))
                        {
                            ClearSlotDataOnly(i);
                        }
                    }

                    // สลับกลับไปถืออาวุธ
                    SwitchToAvailableWeapon();
                }
                else
                {
                    _player.NotifyWeaponChanged();
                }
            }

            RefreshUI();
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen)
            {
                InventoryUI.Instance.RefreshUI();
            }
        }

        /// <summary>
        /// สลับกลับไปถืออาวุธหลักที่มีใน Quick Slot ช่อง 1 หรือ 2 หรืออาวุธที่มี
        /// </summary>
        public void SwitchToAvailableWeapon()
        {
            HookPlayerReferences();
            if (_player == null) return;

            // พยายามเลือกช่อง 0 ก่อน (ปืน)
            if (!string.IsNullOrEmpty(_slotItemNames[0]) && IsItemQuickSlotEligible(_slotItemNames[0]))
            {
                SelectSlot(0);
                return;
            }

            // พยายามเลือกช่อง 1 (มีด)
            if (!string.IsNullOrEmpty(_slotItemNames[1]) && IsItemQuickSlotEligible(_slotItemNames[1]))
            {
                SelectSlot(1);
                return;
            }

            // Fallback สลับปืน/มีดเริ่มต้น
            if (_player.WeaponsList != null && _player.WeaponsList.Count > 0)
            {
                _player.SetHeldRangedWeapon(_player.WeaponsList[0].weaponName);
            }
            else if (_player.MeleeWeaponsList != null && _player.MeleeWeaponsList.Count > 0)
            {
                _player.SetHeldMeleeWeapon(_player.MeleeWeaponsList[0].weaponName);
            }
        }

        /// <summary>
        /// อัปเดตการแสดงผล HUD ทั้ง 5 ช่อง และไฮไลต์ช่องที่กำลังเลือกอยู่
        /// ตรวจสอบอัตโนมัติ: ถ้าไม่มีของ (count <= 0 หรือไม่ได้ถือครอง) ให้เคลียร์ QuickSlot ให้ว่างทันที
        /// </summary>
        public void RefreshUI()
        {
            if (slotItems == null || slotItems.Count == 0)
            {
                DiscoverSlots();
            }

            HookPlayerReferences();

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

                bool isConsumable = false;
                bool isWeapon = false;
                if (itemData != null)
                {
                    isConsumable = itemData.type == ItemType.Potion || itemData.type == ItemType.Bread;
                    isWeapon = itemData.type == ItemType.RangedWeapon || itemData.type == ItemType.MeleeWeapon || itemData.type == ItemType.ThrowingWeapon;
                }
                else
                {
                    string lower = itemName.ToLower();
                    isConsumable = lower.Contains("bread") || lower.Contains("potion") || lower.Contains("food");
                    isWeapon = !isConsumable;
                }

                // 🛑 ตรวจสอบ: ถ้าเป็นของกิน/ยา แล้วจำนวนหมด (count <= 0) ให้เคลียร์ QuickSlot ให้ว่างทันที
                if (isConsumable && _inventory != null && count <= 0)
                {
                    Debug.Log($"[QuickSlot] Auto-clearing Slot {i + 1} ('{itemName}') because quantity reached 0.");
                    ClearSlotDataOnly(i);
                    slotItems[i].SetData("", null, 0, false, false);
                    continue;
                }

                // 🛑 ตรวจสอบ: ถ้าเป็นอาวุธ แต่ผู้เล่นไม่ได้ถือครอง (ไม่มีในคลัง/กระเป๋า) ให้เคลียร์ QuickSlot ให้ว่างทันที
                if (isWeapon && _player != null && (_player.WeaponsList.Count > 0 || _player.MeleeWeaponsList.Count > 0))
                {
                    bool hasAccess = _player.HasRangedWeaponAccess(itemName) || _player.HasMeleeWeaponAccess(itemName);
                    if (!hasAccess)
                    {
                        Debug.Log($"[QuickSlot] Auto-clearing Slot {i + 1} ('{itemName}') because player does not own this weapon.");
                        ClearSlotDataOnly(i);
                        slotItems[i].SetData("", null, 0, false, false);
                        continue;
                    }
                }

                bool isSelected = (i == _activeSlotIndex);
                slotItems[i].SetData(itemName, icon, count, isConsumable, isSelected);
            }
        }
    }
}

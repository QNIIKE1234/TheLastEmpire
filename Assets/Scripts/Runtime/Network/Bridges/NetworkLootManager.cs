using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkLootManager — ระบบป้องกันการเก็บของซ้ำ (Atomic Server Claim / Anti-Dupe Mutex Lock)
    /// และซิงค์ตัวเลขดาเมจลอย (Floating Damage Numbers) ให้เห็นตรงกันทุกหน้าจอ (Section 4.5)
    /// </summary>
    public class NetworkLootManager : NetworkBehaviour
    {
        public static NetworkLootManager Instance { get; private set; }

        private readonly Dictionary<int, CollectibleItem> _trackedItemsById = new Dictionary<int, CollectibleItem>();
        private readonly HashSet<int> _claimedDropIds = new HashSet<int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>
        /// คำนวณรหัสไอเทมดรอปบนพื้นแบบ Deterministic จากพิกัดและชื่อไอเทม
        /// เพื่อให้ Host และ Client อ้างอิงไอเทมชิ้นเดียวกันด้วย dropId เดียวกัน
        /// </summary>
        public static int ComputeDeterministicDropId(CollectibleItem item)
        {
            if (item == null) return 0;
            unchecked
            {
                int qx = Mathf.RoundToInt(item.transform.position.x * 10f);
                int qz = Mathf.RoundToInt(item.transform.position.z * 10f);
                int nameHash = (item.ItemName ?? "").GetHashCode();
                return (qx * 73856093) ^ (qz * 19349663) ^ nameHash ^ (item.Quantity * 83492791);
            }
        }

        public bool TryClaimLootOnHostImmediate(CollectibleItem item, PlayerController collector)
        {
            if (item == null || item.IsCollected) return false;

            int dropId = ComputeDeterministicDropId(item);
            if (_claimedDropIds.Contains(dropId))
            {
                return false; // ถูกเพื่อนเก็บไปในเสี้ยววินาทีเดียวกันแล้ว!
            }

            _claimedDropIds.Add(dropId);
            _trackedItemsById[dropId] = item;

            if (EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                ulong hostId = EmpireNetworkManager.Instance.LocalClientId;
                string iName = item.ItemName ?? "";
                int qty = item.Quantity;
                bool isMoney = item.IsMoney;
                int moneyAmt = item.MoneyAmount;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.LootClaimResult, w =>
                {
                    w.Write(dropId);
                    w.Write(hostId);
                    w.Write(iName);
                    w.Write(qty);
                    w.Write(isMoney);
                    w.Write(moneyAmt);
                });
            }

            return true;
        }

        public void RequestClaimLootFromClient(CollectibleItem item)
        {
            if (item == null || item.IsCollected || EmpireNetworkManager.Instance == null) return;

            int dropId = ComputeDeterministicDropId(item);
            _trackedItemsById[dropId] = item;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.LootClaimRequest, w =>
            {
                w.Write(dropId);
            });
        }

        public void HandleClaimLootRequestOnServer(int dropId, ulong requestingClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;
            if (_claimedDropIds.Contains(dropId)) return; // Mutex Lock: มีคนเก็บไปแล้ว

            CollectibleItem targetItem = FindCollectibleByDropId(dropId);
            if (targetItem == null || targetItem.IsCollected) return;

            _claimedDropIds.Add(dropId);

            string iName = targetItem.ItemName ?? "";
            int qty = targetItem.Quantity;
            bool isMoney = targetItem.IsMoney;
            int moneyAmt = targetItem.MoneyAmount;

            targetItem.Collect();

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.LootClaimResult, w =>
            {
                w.Write(dropId);
                w.Write(requestingClientId);
                w.Write(iName);
                w.Write(qty);
                w.Write(isMoney);
                w.Write(moneyAmt);
            });
        }

        public void HandleLootClaimResultOnClient(int dropId, ulong winnerClientId, string itemName, int quantity, bool isMoney, int moneyAmount)
        {
            _claimedDropIds.Add(dropId);

            // หากเครื่องเราเป็นคนที่ได้รับสิทธิ์เก็บไอเทมชิ้นนี้ ให้เสกเข้ากระเป๋า
            if (EmpireNetworkManager.Instance != null && winnerClientId == EmpireNetworkManager.Instance.LocalClientId)
            {
                PlayerInventory inv = PlayerInventory.Instance;
                if (inv != null)
                {
                    if (isMoney) inv.AddMoney(moneyAmount);
                    else inv.AddItem(itemName, quantity);
                }
            }

            // ลบไอเทมออกจากพื้นของหน้าจอทุกเครื่องทันที
            CollectibleItem item = FindCollectibleByDropId(dropId);
            if (item != null && !item.IsCollected)
            {
                item.Collect();
            }
        }

        private CollectibleItem FindCollectibleByDropId(int dropId)
        {
            if (_trackedItemsById.TryGetValue(dropId, out CollectibleItem cached) && cached != null)
            {
                return cached;
            }

            CollectibleItem[] all = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && ComputeDeterministicDropId(all[i]) == dropId)
                {
                    return all[i];
                }
            }
            return null;
        }

        public static void BroadcastFloatingDamageNumber(Vector3 worldPos, float damageAmount, bool isCritical)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.DamageNumberBroadcast, w =>
            {
                w.Write(worldPos.x);
                w.Write(worldPos.y);
                w.Write(worldPos.z);
                w.Write(damageAmount);
                w.Write(isCritical);
            });
        }
    }
}

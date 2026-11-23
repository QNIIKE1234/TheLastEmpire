using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkLootManager — ระบบป้องกันการเก็บของซ้ำ (Atomic Server Claim / Anti-Dupe Mutex Lock),
    /// ซิงค์ไอเทมที่ดรอปจากศัตรูบนพื้น (LootSpawnBroadcast), ซิงค์การหยิบของออกจากกล่อง/ตู้ (ContainerLootSync),
    /// และซิงค์ตัวเลขดาเมจลอย (Floating Damage Numbers) ให้เห็นตรงกันทุกหน้าจอ (Section 4.5)
    /// </summary>
    public class NetworkLootManager : NetworkBehaviour
    {
        public static NetworkLootManager Instance { get; private set; }

        private struct SyncedContainerState
        {
            public int stageX;
            public int stageY;
            public float posX;
            public float posZ;
            public int remainingMoney;
            public List<LootSlot> remainingItems;
        }

        private int _nextHostDropId = 1000;
        private readonly Dictionary<int, CollectibleItem> _trackedItemsById = new Dictionary<int, CollectibleItem>();
        private readonly HashSet<int> _claimedDropIds = new HashSet<int>();
        private readonly Dictionary<int, float> _pendingClientClaimTime = new Dictionary<int, float>();
        private readonly Dictionary<string, SyncedContainerState> _syncedContainerStates = new Dictionary<string, SyncedContainerState>();

        public void ClearMatchLootCache()
        {
            _trackedItemsById.Clear();
            _claimedDropIds.Clear();
            _pendingClientClaimTime.Clear();
            _syncedContainerStates.Clear();
            _nextHostDropId = 1000;
            WeaponArtifactManager.ClearAll();
        }

        private static string BuildContainerKey(int stageX, int stageY, float posX, float posZ)
        {
            return $"{stageX}_{stageY}_{Mathf.RoundToInt(posX * 2f)}_{Mathf.RoundToInt(posZ * 2f)}";
        }

        private static void ResolveContainerStageCoords(LootContainer container, out int stageX, out int stageY)
        {
            if (container != null)
            {
                BuildingObject bObj = container.GetComponent<BuildingObject>();
                if (bObj != null)
                {
                    stageX = bObj.StageX;
                    stageY = bObj.StageY;
                    return;
                }
            }
            stageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            stageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
        }

        /// <summary>
        /// เรียกจาก LootContainer.Start() / StorageCrateBuilding.OnEnable() เมื่อผู้เล่นเดินเข้ามาในช่องพิกัด (X, Y) ทีหลัง
        /// หากเพื่อน (A) เคยเปิดรถ/กล่อง/หีบเก็บของใบนี้แล้วหยิบหรือฝากของไว้ จะอัปเดตของที่เหลือทันที (ไม่ให้ของโผล่ซ้ำหรือหาย!)
        /// </summary>
        public void ApplyCachedStateToContainer(LootContainer container)
        {
            if (container == null || !EmpireNetworkManager.IsOnline) return;
            ResolveContainerStageCoords(container, out int stageX, out int stageY);
            Vector3 pos = container.transform.position;

            string exactKey = BuildContainerKey(stageX, stageY, pos.x, pos.z);
            if (_syncedContainerStates.TryGetValue(exactKey, out SyncedContainerState state))
            {
                ApplyStateToContainerObject(container, state);
                return;
            }

            // Fallback เผื่อพิกัดคลาดเคลื่อนเล็กน้อยข้ามขอบ 0.5m bucket
            foreach (var kvp in _syncedContainerStates)
            {
                var s = kvp.Value;
                if (s.stageX != stageX || s.stageY != stageY) continue;
                float dx = pos.x - s.posX;
                float dz = pos.z - s.posZ;
                if (dx * dx + dz * dz <= 0.36f)
                {
                    ApplyStateToContainerObject(container, s);
                    return;
                }
            }
        }

        /// <summary>
        /// ส่งสถานะกล่อง/หีบเก็บของทั้งหมดที่เคยถูกแก้ไขไปยัง Client ที่เพิ่งเชื่อมต่อเข้ามา (Late-Join Sync)
        /// </summary>
        public void SendAllCachedContainersToClient(ulong targetClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            foreach (var kvp in _syncedContainerStates)
            {
                SyncedContainerState s = kvp.Value;
                List<LootSlot> items = s.remainingItems;
                int count = items != null ? items.Count : 0;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.ContainerLootSync, w =>
                {
                    w.Write(s.stageX);
                    w.Write(s.stageY);
                    w.Write(s.posX);
                    w.Write(s.posZ);
                    w.Write(s.remainingMoney);
                    w.Write(count);
                    for (int i = 0; i < count; i++)
                    {
                        w.Write(items[i].itemName ?? "");
                        w.Write(items[i].quantity);
                    }
                }, targetClientId);
            }
        }

        private static void ApplyStateToContainerObject(LootContainer container, SyncedContainerState state)
        {
            container.moneyAmount = state.remainingMoney;
            container.itemsInside.Clear();
            if (state.remainingItems != null)
            {
                container.itemsInside.AddRange(state.remainingItems);
            }
            container.isSearched = (state.remainingMoney <= 0 && container.itemsInside.Count == 0);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>
        /// ดึงรหัสไอเทมดรอปบนพื้น (ใช้ NetworkDropId ที่ซิงค์มาจาก Host ก่อน หรือคำนวณจาก InitialSpawnPosition
        /// เพื่อไม่ให้รหัสเปลี่ยนเมื่อไอเทมถูกแม่เหล็กดูดเคลื่อนที่เข้าหาตัวผู้เล่น!)
        /// </summary>
        public static int ComputeDeterministicDropId(CollectibleItem item)
        {
            if (item == null) return 0;
            if (item.NetworkDropId != 0)
            {
                return item.NetworkDropId;
            }
            unchecked
            {
                Vector3 origin = item.InitialSpawnPosition != Vector3.zero ? item.InitialSpawnPosition : item.transform.position;
                int qx = Mathf.RoundToInt(origin.x * 10f);
                int qz = Mathf.RoundToInt(origin.z * 10f);
                int nameHash = (item.ItemName ?? "").GetHashCode();
                int computed = (qx * 73856093) ^ (qz * 19349663) ^ nameHash ^ (item.Quantity * 83492791);
                if (computed == 0) computed = 1;
                item.NetworkDropId = computed;
                return computed;
            }
        }

        /// <summary>
        /// เรียกจาก CollectibleItem.Start() บนเครื่อง Host เพื่อออกรหัส DropId กลางและกระจายให้ Client ในช่องเดียวกันเห็นของดรอปตรงกัน
        /// </summary>
        public void RegisterAndBroadcastHostDrop(CollectibleItem item)
        {
            if (item == null || !EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            int dropId = item.NetworkDropId != 0 ? item.NetworkDropId : _nextHostDropId++;
            item.NetworkDropId = dropId;
            _trackedItemsById[dropId] = item;

            int stageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int stageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            Vector3 spawnPos = item.InitialSpawnPosition != Vector3.zero ? item.InitialSpawnPosition : item.transform.position;
            string iName = item.ItemName ?? "";
            int qty = item.Quantity;
            bool isMoney = item.IsMoney;
            int moneyAmt = item.MoneyAmount;
            bool requireManual = item.RequireManualPickup;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.LootSpawnBroadcast, w =>
            {
                w.Write(dropId);
                w.Write(stageX);
                w.Write(stageY);
                w.Write(iName);
                w.Write(qty);
                w.Write(isMoney);
                w.Write(moneyAmt);
                w.Write(spawnPos.x);
                w.Write(spawnPos.y);
                w.Write(spawnPos.z);
                w.Write(requireManual);
            });
        }

        public void RequestClientDropItemToHost(string itemName, int quantity, Vector3 dropPos)
        {
            if (!EmpireNetworkManager.IsOnlineClient || EmpireNetworkManager.Instance == null) return;

            int stageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int stageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.ClientDropItemRequest, w =>
            {
                w.Write(stageX);
                w.Write(stageY);
                w.Write(itemName ?? "");
                w.Write(quantity);
                w.Write(dropPos.x);
                w.Write(dropPos.y);
                w.Write(dropPos.z);
            });
        }

        public void HandleClientDropItemRequestOnHost(int dropStageX, int dropStageY, string itemName, int quantity, Vector3 dropPos)
        {
            if (!EmpireNetworkManager.IsOnlineHost || GameManager.Instance == null) return;

            int hostStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int hostStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            if (dropStageX != hostStageX || dropStageY != hostStageY) return;

            // สร้างไอเทมบนเครื่อง Host (ซึ่ง CollectibleItem.Start() จะกระจาย LootSpawnBroadcast กลับไปให้ทุกเครื่องอัตโนมัติ)
            InventoryUI.SpawnManualPickupDropLocally(itemName, quantity, dropPos);
        }

        /// <summary>
        /// เรียกเมื่อ Client เพิ่งเดินตามเข้ามาในช่องพิกัด (X, Y) เดียวกับ Host
        /// เพื่อส่งของดรอปบนพื้นที่ยังไม่มีคนเก็บ (เช่น Airdrop หรือของจากซอมบี้ที่ตายก่อนเพื่อนเดินเข้ามา) ให้ Client เห็นครบทุกชิ้น
        /// </summary>
        public void BroadcastAllActiveStageDropsToClient(ulong targetClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            int stageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int stageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;

            CollectibleItem[] all = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                CollectibleItem item = all[i];
                if (item == null || item.IsCollected || !item.gameObject.activeInHierarchy) continue;

                int dropId = ComputeDeterministicDropId(item);
                if (_claimedDropIds.Contains(dropId)) continue;
                _trackedItemsById[dropId] = item;

                Vector3 pos = item.transform.position;
                string iName = item.ItemName ?? "";
                int qty = item.Quantity;
                bool isMoney = item.IsMoney;
                int moneyAmt = item.MoneyAmount;
                bool requireManual = item.RequireManualPickup;

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.LootSpawnBroadcast, w =>
                {
                    w.Write(dropId);
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(iName);
                    w.Write(qty);
                    w.Write(isMoney);
                    w.Write(moneyAmt);
                    w.Write(pos.x);
                    w.Write(pos.y);
                    w.Write(pos.z);
                    w.Write(requireManual);
                }, targetClientId);
            }
        }

        public void HandleHostLootSpawnOnClient(int dropId, int stageX, int stageY, string itemName, int qty, bool isMoney, int moneyAmt, Vector3 spawnPos, bool requireManualPickup = false)
        {
            if (!EmpireNetworkManager.IsOnlineClient || GameManager.Instance == null) return;
            if (_claimedDropIds.Contains(dropId)) return;

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            if (stageX != myStageX || stageY != myStageY) return;

            if (_trackedItemsById.TryGetValue(dropId, out CollectibleItem existing) && existing != null)
            {
                return;
            }

            // ป้องกันการเสกซ้ำกับไอเทมที่เกิดจาก Seed ห้องเดียวกัน (เช่น SupplyAirdrop / ScavengerCache)
            CollectibleItem[] all = Object.FindObjectsByType<CollectibleItem>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                CollectibleItem c = all[i];
                if (c == null || c.IsCollected) continue;
                Vector3 cOrigin = c.InitialSpawnPosition != Vector3.zero ? c.InitialSpawnPosition : c.transform.position;
                bool sameKind = (c.IsMoney == isMoney) && (isMoney ? (c.MoneyAmount == moneyAmt) : string.Equals(c.ItemName, itemName, System.StringComparison.OrdinalIgnoreCase));
                if (sameKind && (cOrigin - spawnPos).sqrMagnitude < 0.35f)
                {
                    c.MarkAsSpawnedFromNetwork(dropId);
                    if (requireManualPickup) c.ConfigureAsPlayerDroppedItem();
                    _trackedItemsById[dropId] = c;
                    return;
                }
            }

            CollectibleItem prefab = isMoney
                ? (GameManager.Instance.MoneyDropPrefab != null ? GameManager.Instance.MoneyDropPrefab : GameManager.Instance.ItemDropPrefab)
                : GameManager.Instance.ItemDropPrefab;
            if (prefab == null) return;

            CollectibleItem spawned = Instantiate(prefab, spawnPos, Quaternion.identity);
            spawned.MarkAsSpawnedFromNetwork(dropId);
            if (isMoney)
            {
                spawned.SetMoneyDetails(moneyAmt);
            }
            else
            {
                spawned.SetItemDetails(itemName, qty);
            }
            if (requireManualPickup)
            {
                spawned.ConfigureAsPlayerDroppedItem();
            }
            _trackedItemsById[dropId] = spawned;
        }

        // =====================================================
        //  LootContainer / Crate / Locker Sync (Anti-Dupe)
        // =====================================================

        public void BroadcastContainerState(LootContainer container)
        {
            if (container == null || !EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            ResolveContainerStageCoords(container, out int stageX, out int stageY);
            Vector3 pos = container.transform.position;
            int money = container.moneyAmount;
            var items = container.itemsInside;
            int itemCount = items != null ? items.Count : 0;

            // บันทึกสถานะล่าสุดของรถ/กล่องลง Cache กลางด้วย เผื่อผู้เล่นคนอื่นเดินเข้ามาช่องนี้ทีหลัง
            List<LootSlot> snapshot = new List<LootSlot>(itemCount);
            for (int i = 0; i < itemCount; i++) snapshot.Add(items[i]);
            _syncedContainerStates[BuildContainerKey(stageX, stageY, pos.x, pos.z)] = new SyncedContainerState
            {
                stageX = stageX,
                stageY = stageY,
                posX = pos.x,
                posZ = pos.z,
                remainingMoney = money,
                remainingItems = snapshot
            };

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.ContainerLootSync, w =>
            {
                w.Write(stageX);
                w.Write(stageY);
                w.Write(pos.x);
                w.Write(pos.z);
                w.Write(money);
                w.Write(itemCount);
                for (int i = 0; i < itemCount; i++)
                {
                    w.Write(items[i].itemName ?? "");
                    w.Write(items[i].quantity);
                }
            });
        }

        public void HandleContainerLootSyncPacket(BinaryReader reader, bool relayIfHost)
        {
            int stageX = reader.ReadInt32();
            int stageY = reader.ReadInt32();
            float posX = reader.ReadSingle();
            float posZ = reader.ReadSingle();
            int remainingMoney = reader.ReadInt32();
            int itemCount = reader.ReadInt32();
            List<LootSlot> syncedItems = new List<LootSlot>(Mathf.Max(0, itemCount));
            for (int i = 0; i < itemCount; i++)
            {
                string iName = reader.ReadString();
                int qty = reader.ReadInt32();
                syncedItems.Add(new LootSlot(iName, qty));
            }

            // บันทึกสถานะรถ/กล่องที่เพื่อนเพิ่งเก็บไปเก็บไว้ใน Cache ทันที (แม้ตอนนี้เราจะอยู่คนละช่องพิกัดกับเพื่อน!)
            // พอเราเดินตามเข้าไปในช่อง (stageX, stageY) นั้นทีหลัง รถ/กล่องใบนั้นจะว่างตามที่เพื่อนเก็บไปทันที
            _syncedContainerStates[BuildContainerKey(stageX, stageY, posX, posZ)] = new SyncedContainerState
            {
                stageX = stageX,
                stageY = stageY,
                posX = posX,
                posZ = posZ,
                remainingMoney = remainingMoney,
                remainingItems = new List<LootSlot>(syncedItems)
            };

            // หากเครื่อง Host ได้รับจาก Client ให้กระจายต่อให้ลูกทีมคนอื่นรู้ด้วย
            if (relayIfHost && EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.ContainerLootSync, w =>
                {
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(posX);
                    w.Write(posZ);
                    w.Write(remainingMoney);
                    w.Write(syncedItems.Count);
                    for (int i = 0; i < syncedItems.Count; i++)
                    {
                        w.Write(syncedItems[i].itemName ?? "");
                        w.Write(syncedItems[i].quantity);
                    }
                });
            }

            // 1. อัปเดตสิ่งปลูกสร้างหีบเก็บของ (StorageCrateBuilding) ที่อยู่ใน BuildingManager ด้วย
            // แม้จะอยู่คนละช่องพิกัด (GameObject ถูก SetActive(false) อยู่) เพื่อไม่ให้ของค้างเป็นข้อมูลเก่าตอนเดินกลับฐาน
            bool updatedAnyBuildingCrate = false;
            if (BuildingManager.Instance != null && BuildingManager.Instance.ActiveBuildings != null)
            {
                var buildings = BuildingManager.Instance.ActiveBuildings;
                for (int i = 0; i < buildings.Count; i++)
                {
                    BuildingObject b = buildings[i];
                    if (b == null || b.StageX != stageX || b.StageY != stageY) continue;
                    Vector3 bPos = b.transform.position;
                    float dx = bPos.x - posX;
                    float dz = bPos.z - posZ;
                    if (dx * dx + dz * dz <= 0.36f)
                    {
                        LootContainer lc = b.GetComponent<LootContainer>();
                        if (lc != null)
                        {
                            ApplySyncedItemsAndRefreshUI(lc, remainingMoney, syncedItems);
                            updatedAnyBuildingCrate = true;
                            break;
                        }
                    }
                }
            }

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            if (stageX != myStageX || stageY != myStageY) return;

            if (!updatedAnyBuildingCrate)
            {
                LootContainer[] containers = Object.FindObjectsByType<LootContainer>(FindObjectsSortMode.None);
                for (int i = 0; i < containers.Length; i++)
                {
                    LootContainer c = containers[i];
                    if (c == null) continue;
                    Vector3 cPos = c.transform.position;
                    float dx = cPos.x - posX;
                    float dz = cPos.z - posZ;
                    if (dx * dx + dz * dz <= 0.36f)
                    {
                        ApplySyncedItemsAndRefreshUI(c, remainingMoney, syncedItems);
                        break;
                    }
                }
            }
        }

        private static void ApplySyncedItemsAndRefreshUI(LootContainer c, int remainingMoney, List<LootSlot> syncedItems)
        {
            c.moneyAmount = remainingMoney;
            c.itemsInside.Clear();
            c.itemsInside.AddRange(syncedItems);
            c.isSearched = (remainingMoney <= 0 && syncedItems.Count == 0);

            if (LootUI.HasInstance && LootUI.Instance.IsOpen && LootUI.Instance.CurrentContainer == c)
            {
                LootUI.Instance.RefreshUI();
            }
            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen && StorageUI.Instance.CurrentContainer == c)
            {
                StorageUI.Instance.RefreshUI();
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
            if (!item.CanPlayerCollectThisItem())
            {
                (PlayerRegistry.LocalPlayer ?? PlayerController.Instance)?.ShowInventoryFullFeedback();
                return;
            }

            int dropId = ComputeDeterministicDropId(item);
            if (_pendingClientClaimTime.TryGetValue(dropId, out float lastSent) && Time.unscaledTime - lastSent < 1.0f)
            {
                return; // ป้องกันส่ง Claim ซ้ำทุกเฟรมระหว่างรอ Host ตอบกลับ
            }
            _pendingClientClaimTime[dropId] = Time.unscaledTime;
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

            PlayerController claimingPlayer = PlayerRegistry.GetPlayerByClientId(requestingClientId);
            if (claimingPlayer != null && claimingPlayer.gameObject.activeInHierarchy)
            {
                targetItem.CollectWithVisualFlyTo(claimingPlayer.transform);
            }
            else
            {
                targetItem.Collect();
            }

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
            _pendingClientClaimTime.Remove(dropId);

            // หากเครื่องเราเป็นคนที่ได้รับสิทธิ์เก็บไอเทมชิ้นนี้ ให้เสกเข้ากระเป๋า
            if (EmpireNetworkManager.Instance != null && winnerClientId == EmpireNetworkManager.Instance.LocalClientId)
            {
                PlayerController localPlayer = PlayerRegistry.LocalPlayer ?? PlayerController.Instance;
                PlayerInventory inv = localPlayer != null ? localPlayer.GetComponent<PlayerInventory>() : PlayerInventory.Instance;
                if (inv == null) inv = PlayerInventory.Instance;

                if (inv != null)
                {
                    if (isMoney)
                    {
                        inv.AddMoney(moneyAmount);
                    }
                    else
                    {
                        int safeQty = Mathf.Max(1, quantity);
                        bool added = inv.CanAddItem(itemName, safeQty) && inv.AddItem(itemName, safeQty);
                        if (!added)
                        {
                            // กรณีกระเป๋าเพิ่งเต็มพอดีระหว่างรอ Host ตอบกลับ: โยนไอเทมกลับลงพื้นให้เพื่อน/ตัวเองเก็บใหม่ได้ ไม่หายฟรี!
                            localPlayer?.ShowInventoryFullFeedback();
                            Vector3 dropPos = localPlayer != null ? localPlayer.transform.position : Vector3.zero;
                            dropPos.y = CollectibleItem.GROUND_Y;
                            if (EmpireNetworkManager.IsOnlineClient)
                            {
                                RequestClientDropItemToHost(itemName, safeQty, dropPos);
                            }
                            else
                            {
                                InventoryUI.SpawnManualPickupDropLocally(itemName, safeQty, dropPos);
                            }
                        }
                    }
                }
            }

            // แสดงภาพไอเทมบินเข้าตัวคนเก็บ (เพื่อนร่วมทีม) แทนการหายวับไปเฉยๆ
            CollectibleItem item = FindCollectibleByDropId(dropId);
            if (item != null && !item.IsCollected)
            {
                bool isLocalWinner = EmpireNetworkManager.Instance != null && winnerClientId == EmpireNetworkManager.Instance.LocalClientId;
                PlayerController winnerPlayer = isLocalWinner
                    ? (PlayerRegistry.LocalPlayer ?? PlayerController.Instance)
                    : PlayerRegistry.GetPlayerByClientId(winnerClientId);

                if (winnerPlayer != null && winnerPlayer.gameObject.activeInHierarchy)
                {
                    float distToWinner = Vector3.Distance(item.transform.position, winnerPlayer.transform.position + Vector3.up * 0.7f);
                    if (!isLocalWinner || distToWinner > 0.65f)
                    {
                        item.CollectWithVisualFlyTo(winnerPlayer.transform);
                    }
                    else
                    {
                        item.Collect();
                    }
                }
                else
                {
                    item.Collect();
                }
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

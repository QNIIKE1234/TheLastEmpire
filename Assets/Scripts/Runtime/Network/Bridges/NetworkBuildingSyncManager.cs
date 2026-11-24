using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    public enum FurnitureStateSyncType : byte
    {
        LampToggle = 1,
        RadioState = 2,
        PlanterHarvest = 3,
        UtilityCooldown = 4,
        BedSleepToDawn = 5
    }

    /// <summary>
    /// NetworkBuildingSyncManager — ซิงค์การวางสิ่งปลูกสร้าง (ทั้งภายนอกและภายในบ้าน InteriorId),
    /// สถานะเฟอร์นิเจอร์ (โคมไฟ/วิทยุ/แปลงผัก/ตู้ยา/เตียงนอน), และหีบเก็บของส่วนกลาง StorageCrateBuilding
    /// </summary>
    public class NetworkBuildingSyncManager : NetworkBehaviour
    {
        public static NetworkBuildingSyncManager Instance { get; private set; }

        private struct CachedFurnitureState
        {
            public int stageX;
            public int stageY;
            public int interiorId;
            public Vector3 position;
            public FurnitureStateSyncType stateType;
            public int intValue;
            public float floatValue;
        }

        private int _nextBuildingInstanceId = 1;
        private readonly List<PlacedBuildingNetData> _placedBuildingRegistry = new List<PlacedBuildingNetData>();
        private readonly Dictionary<string, CachedFurnitureState> _furnitureStateCache = new Dictionary<string, CachedFurnitureState>();
        private static bool _isApplyingRemotePlacement = false;
        private static bool _isApplyingRemoteDestruction = false;
        private static bool _isApplyingRemoteFurnitureState = false;

        public void ClearRegistry()
        {
            _placedBuildingRegistry.Clear();
            _furnitureStateCache.Clear();
            _nextBuildingInstanceId = 1;
        }

        public static void NotifyLocalBuildingDestroyed(BuildingObject buildingObj)
        {
            if (_isApplyingRemoteDestruction || buildingObj == null) return;
            if (!EmpireNetworkManager.IsOnline || Instance == null || EmpireNetworkManager.Instance == null) return;

            int stX = buildingObj.StageX;
            int stY = buildingObj.StageY;
            int intId = buildingObj.InteriorId;
            Vector3 pos = buildingObj.transform.position;

            Instance.RemoveFromRegistry(stX, stY, intId, pos);

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingDestroyBroadcast, w =>
            {
                w.Write(stX);
                w.Write(stY);
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(pos.z);
                w.Write(intId);
            });
        }

        public void HandleRemoteBuildingDestroyed(int stageX, int stageY, int interiorId, Vector3 position, bool relayIfHost)
        {
            RemoveFromRegistry(stageX, stageY, interiorId, position);

            if (relayIfHost && EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingDestroyBroadcast, w =>
                {
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(position.x);
                    w.Write(position.y);
                    w.Write(position.z);
                    w.Write(interiorId);
                });
            }

            if (BuildingManager.Instance == null || BuildingManager.Instance.ActiveBuildings == null) return;

            _isApplyingRemoteDestruction = true;
            try
            {
                var list = BuildingManager.Instance.ActiveBuildings;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    BuildingObject b = list[i];
                    if (b == null) continue;
                    if (b.StageX == stageX && b.StageY == stageY && b.InteriorId == interiorId && (b.transform.position - position).sqrMagnitude < 0.25f)
                    {
                        BuildingManager.Instance.UntrackBuilding(b);
                        Destroy(b.gameObject);
                        break;
                    }
                }
            }
            finally
            {
                _isApplyingRemoteDestruction = false;
            }
        }

        private void RemoveFromRegistry(int stageX, int stageY, int interiorId, Vector3 position)
        {
            for (int i = _placedBuildingRegistry.Count - 1; i >= 0; i--)
            {
                var b = _placedBuildingRegistry[i];
                if (b.stageX == stageX && b.stageY == stageY && b.interiorId == interiorId && (b.position - position).sqrMagnitude < 0.25f)
                {
                    _placedBuildingRegistry.RemoveAt(i);
                }
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        /// <summary>
        /// เรียกจาก BuildingPlacer.ConfirmPlacement() เมื่อผู้เล่นวางสิ่งปลูกสร้างสำเร็จ
        /// </summary>
        public static void NotifyLocalBuildingPlaced(BuildingObject buildingObj, BuildingData data)
        {
            if (_isApplyingRemotePlacement || buildingObj == null || data == null) return;
            if (!EmpireNetworkManager.IsOnline || Instance == null) return;

            PlacedBuildingNetData netData = new PlacedBuildingNetData
            {
                instanceId = Instance._nextBuildingInstanceId++,
                buildingId = !string.IsNullOrEmpty(data.buildingId) ? data.buildingId : data.buildingName,
                stageX = buildingObj.StageX,
                stageY = buildingObj.StageY,
                interiorId = buildingObj.InteriorId,
                position = buildingObj.transform.position,
                rotationY = buildingObj.transform.eulerAngles.y,
                currentHealth = buildingObj.CurrentHealth
            };

            if (EmpireNetworkManager.IsOnlineHost)
            {
                Instance._placedBuildingRegistry.Add(netData);
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingPlaceBroadcast, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    netData.NetworkSerialize(serializer);
                });
            }
            else if (EmpireNetworkManager.IsOnlineClient)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingPlaceRequest, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    netData.NetworkSerialize(serializer);
                });
            }
        }

        public void HandleClientPlaceBuildingRequest(PlacedBuildingNetData data)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            data.instanceId = _nextBuildingInstanceId++;
            _placedBuildingRegistry.Add(data);

            ApplyRemotePlacedBuilding(data);

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingPlaceBroadcast, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                data.NetworkSerialize(serializer);
            });
        }

        public void ApplyRemotePlacedBuilding(PlacedBuildingNetData data)
        {
            if (BuildingManager.Instance == null) return;

            // ตรวจสอบว่ามีตึกชิ้นเดียวกันวางอยู่ในพิกัดนี้แล้วหรือยัง (ป้องกันการเกิดซ้ำบนเครื่องคนที่วางเอง)
            var activeBuildings = BuildingManager.Instance.ActiveBuildings;
            for (int i = 0; i < activeBuildings.Count; i++)
            {
                BuildingObject existing = activeBuildings[i];
                if (existing != null &&
                    existing.StageX == data.stageX &&
                    existing.StageY == data.stageY &&
                    existing.InteriorId == data.interiorId &&
                    (existing.transform.position - data.position).sqrMagnitude < 0.05f)
                {
                    return;
                }
            }

            BuildingData bData = BuildingManager.Instance.GetBuildingDataByName(data.buildingId);
            if (bData == null || bData.prefab == null) return;

            _isApplyingRemotePlacement = true;
            try
            {
                Quaternion rot = Quaternion.Euler(0f, data.rotationY, 0f);
                GameObject instance = Instantiate(bData.prefab, data.position, rot);
                if (instance != null)
                {
                    instance.name = $"{bData.buildingName}_Net_{data.instanceId}";
                    BuildingObject bComp = instance.GetComponent<BuildingObject>();
                    if (bComp == null) bComp = instance.AddComponent<BuildingObject>();
                    bComp.Initialize(bData);
                    bComp.SetStageCoordinates(data.stageX, data.stageY, data.interiorId);

                    instance.transform.SetParent(BuildingManager.Instance.BuildingsContainer, true);
                    BuildingManager.Instance.TrackBuilding(bComp);

                    int curX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : data.stageX;
                    int curY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : data.stageY;
                    int curInterior = BuildingInteriorManager.CurrentInteriorId;
                    instance.SetActive(data.stageX == curX && data.stageY == curY && data.interiorId == curInterior);

                    LootContainer crateContainer = instance.GetComponent<LootContainer>();
                    if (crateContainer != null && NetworkLootManager.Instance != null)
                    {
                        NetworkLootManager.Instance.ApplyCachedStateToContainer(crateContainer);
                    }
                }
            }
            finally
            {
                _isApplyingRemotePlacement = false;
            }
        }

        public void SendAllPlacedBuildingsToClient(ulong targetClientId)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            for (int i = 0; i < _placedBuildingRegistry.Count; i++)
            {
                PlacedBuildingNetData bData = _placedBuildingRegistry[i];
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingPlaceBroadcast, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    bData.NetworkSerialize(serializer);
                }, targetClientId);
            }

            foreach (var kvp in _furnitureStateCache)
            {
                CachedFurnitureState st = kvp.Value;
                if (st.stateType == FurnitureStateSyncType.BedSleepToDawn) continue;
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.FurnitureStateSync, w =>
                {
                    w.Write(st.stageX);
                    w.Write(st.stageY);
                    w.Write(st.interiorId);
                    w.Write(st.position.x);
                    w.Write(st.position.y);
                    w.Write(st.position.z);
                    w.Write((byte)st.stateType);
                    w.Write(st.intValue);
                    w.Write(st.floatValue);
                }, targetClientId);
            }

            // ซิงค์รายการไอเทมในกล่องเก็บของ (StorageCrate) ทั้งหมดให้ Client ที่เพิ่งเข้าห้องด้วย
            NetworkLootManager.Instance?.SendAllCachedContainersToClient(targetClientId);
        }

        // =====================================================
        //  Interactive Furniture State Sync (Lamp, Radio, Planter, MedStation, Bed)
        // =====================================================

        private static string BuildFurnitureCacheKey(int stageX, int stageY, int interiorId, Vector3 pos, FurnitureStateSyncType type)
        {
            return $"{stageX}_{stageY}_{interiorId}_{(int)type}_{Mathf.RoundToInt(pos.x * 4f)}_{Mathf.RoundToInt(pos.z * 4f)}";
        }

        public static void NotifyLocalFurnitureStateChanged(
            BuildingObject buildingObj,
            FurnitureStateSyncType stateType,
            int intValue = 0,
            float floatValue = 0f)
        {
            if (_isApplyingRemoteFurnitureState) return;
            if (!EmpireNetworkManager.IsOnline || Instance == null || EmpireNetworkManager.Instance == null) return;

            int stX = buildingObj != null ? buildingObj.StageX : (WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64);
            int stY = buildingObj != null ? buildingObj.StageY : (WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64);
            int intId = buildingObj != null ? buildingObj.InteriorId : BuildingInteriorManager.CurrentInteriorId;
            Vector3 pos = buildingObj != null ? buildingObj.transform.position : Vector3.zero;

            if (EmpireNetworkManager.IsOnlineHost && stateType != FurnitureStateSyncType.BedSleepToDawn)
            {
                string key = BuildFurnitureCacheKey(stX, stY, intId, pos, stateType);
                Instance._furnitureStateCache[key] = new CachedFurnitureState
                {
                    stageX = stX,
                    stageY = stY,
                    interiorId = intId,
                    position = pos,
                    stateType = stateType,
                    intValue = intValue,
                    floatValue = floatValue
                };
            }

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.FurnitureStateSync, w =>
            {
                w.Write(stX);
                w.Write(stY);
                w.Write(intId);
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(pos.z);
                w.Write((byte)stateType);
                w.Write(intValue);
                w.Write(floatValue);
            });
        }

        public void HandleRemoteFurnitureState(
            int stageX,
            int stageY,
            int interiorId,
            Vector3 position,
            FurnitureStateSyncType stateType,
            int intValue,
            float floatValue,
            bool relayIfHost)
        {
            if (stateType == FurnitureStateSyncType.BedSleepToDawn)
            {
                if (relayIfHost && EmpireNetworkManager.IsOnlineHost && DayNightManager.Instance != null && DayNightManager.Instance.IsNight)
                {
                    DayNightManager.Instance.AddDays(1);
                    DayNightManager.Instance.SetPhase(DayNightManager.TimePhase.Day, true);
                }
                return;
            }

            if (relayIfHost && EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                string key = BuildFurnitureCacheKey(stageX, stageY, interiorId, position, stateType);
                _furnitureStateCache[key] = new CachedFurnitureState
                {
                    stageX = stageX,
                    stageY = stageY,
                    interiorId = interiorId,
                    position = position,
                    stateType = stateType,
                    intValue = intValue,
                    floatValue = floatValue
                };

                EmpireNetworkManager.Instance.SendPacket(NetPacketType.FurnitureStateSync, w =>
                {
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(interiorId);
                    w.Write(position.x);
                    w.Write(position.y);
                    w.Write(position.z);
                    w.Write((byte)stateType);
                    w.Write(intValue);
                    w.Write(floatValue);
                });
            }

            if (BuildingManager.Instance == null || BuildingManager.Instance.ActiveBuildings == null) return;

            BuildingObject target = null;
            var list = BuildingManager.Instance.ActiveBuildings;
            for (int i = 0; i < list.Count; i++)
            {
                BuildingObject b = list[i];
                if (b == null) continue;
                if (b.StageX == stageX && b.StageY == stageY && b.InteriorId == interiorId && (b.transform.position - position).sqrMagnitude < 0.36f)
                {
                    target = b;
                    break;
                }
            }
            if (target == null) return;

            _isApplyingRemoteFurnitureState = true;
            try
            {
                switch (stateType)
                {
                    case FurnitureStateSyncType.LampToggle:
                        target.GetComponent<ToggleableLampFurniture>()?.ApplyNetworkState(intValue != 0);
                        break;
                    case FurnitureStateSyncType.RadioState:
                        target.GetComponent<AnalogRadioFurniture>()?.ApplyNetworkState((intValue & 1) != 0, intValue >> 1);
                        break;
                    case FurnitureStateSyncType.PlanterHarvest:
                        target.GetComponent<CropPlanterFurniture>()?.ApplyNetworkGrowthProgress(floatValue);
                        break;
                    case FurnitureStateSyncType.UtilityCooldown:
                        target.GetComponent<FurnitureUtilityInteractable>()?.ApplyNetworkCooldown(floatValue);
                        break;
                }
            }
            finally
            {
                _isApplyingRemoteFurnitureState = false;
            }
        }

        // =====================================================
        //  StorageCrateBuilding Transaction Sync (Section 4.6 ข้อ 3)
        // =====================================================

        public void RequestCrateTransaction(int buildingInstanceId, string itemName, int quantity, bool isDeposit)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null) return;

            CrateTransactionNetPayload tx = new CrateTransactionNetPayload
            {
                buildingInstanceId = buildingInstanceId,
                requestingClientId = EmpireNetworkManager.Instance.LocalClientId,
                itemName = itemName ?? "",
                quantity = quantity,
                isDeposit = isDeposit
            };

            if (EmpireNetworkManager.IsOnlineHost)
            {
                HandleCrateTransactionOnServer(tx);
            }
            else
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.CrateTransactionRequest, w =>
                {
                    var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                    tx.NetworkSerialize(serializer);
                });
            }
        }

        public void HandleCrateTransactionOnServer(CrateTransactionNetPayload tx)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.CrateTransactionBroadcast, w =>
            {
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                tx.NetworkSerialize(serializer);
            });
        }

        public void ApplyCrateTransactionOnClient(CrateTransactionNetPayload tx)
        {
            // อัปเดตสถานะหีบเก็บของเมื่อเพื่อนฝากหรือดึงของออก (รองรับทั้ง StorageUI และ LootUI)
            LootContainer targetContainer = null;
            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen && StorageUI.Instance.CurrentContainer != null)
            {
                targetContainer = StorageUI.Instance.CurrentContainer;
            }
            else if (LootUI.HasInstance && LootUI.Instance.IsOpen && LootUI.Instance.CurrentContainer != null)
            {
                targetContainer = LootUI.Instance.CurrentContainer;
            }

            if (targetContainer == null || targetContainer.itemsInside == null) return;

            var items = targetContainer.itemsInside;
            if (tx.isDeposit)
            {
                targetContainer.AddItem(tx.itemName, tx.quantity);
            }
            else
            {
                int remainingToRemove = tx.quantity;
                for (int i = items.Count - 1; i >= 0 && remainingToRemove > 0; i--)
                {
                    if (string.Equals(items[i].itemName, tx.itemName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        int take = Mathf.Min(items[i].quantity, remainingToRemove);
                        targetContainer.RemoveSlotAt(i, take);
                        remainingToRemove -= take;
                    }
                }
            }

            if (StorageUI.HasInstance && StorageUI.Instance.IsOpen && StorageUI.Instance.CurrentContainer == targetContainer)
            {
                StorageUI.Instance.RefreshUI();
            }
            if (LootUI.HasInstance && LootUI.Instance.IsOpen && LootUI.Instance.CurrentContainer == targetContainer)
            {
                LootUI.Instance.RefreshUI();
            }
        }
    }
}

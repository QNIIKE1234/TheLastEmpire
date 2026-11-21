using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkBuildingSyncManager — ซิงค์การวางสิ่งปลูกสร้าง, ระบบไฟฟ้า PowerGridManager,
    /// และหีบเก็บของส่วนกลาง StorageCrateBuilding แบบ Server-Authoritative Transaction (Section 4.6)
    /// </summary>
    public class NetworkBuildingSyncManager : NetworkBehaviour
    {
        public static NetworkBuildingSyncManager Instance { get; private set; }

        private int _nextBuildingInstanceId = 1;
        private readonly List<PlacedBuildingNetData> _placedBuildingRegistry = new List<PlacedBuildingNetData>();
        private static bool _isApplyingRemotePlacement = false;
        private static bool _isApplyingRemoteDestruction = false;

        public void ClearRegistry()
        {
            _placedBuildingRegistry.Clear();
            _nextBuildingInstanceId = 1;
        }

        public static void NotifyLocalBuildingDestroyed(BuildingObject buildingObj)
        {
            if (_isApplyingRemoteDestruction || buildingObj == null) return;
            if (!EmpireNetworkManager.IsOnline || Instance == null || EmpireNetworkManager.Instance == null) return;

            int stX = buildingObj.StageX;
            int stY = buildingObj.StageY;
            Vector3 pos = buildingObj.transform.position;

            Instance.RemoveFromRegistry(stX, stY, pos);

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingDestroyBroadcast, w =>
            {
                w.Write(stX);
                w.Write(stY);
                w.Write(pos.x);
                w.Write(pos.y);
                w.Write(pos.z);
            });
        }

        public void HandleRemoteBuildingDestroyed(int stageX, int stageY, Vector3 position, bool relayIfHost)
        {
            RemoveFromRegistry(stageX, stageY, position);

            if (relayIfHost && EmpireNetworkManager.IsOnlineHost && EmpireNetworkManager.Instance != null)
            {
                EmpireNetworkManager.Instance.SendPacket(NetPacketType.BuildingDestroyBroadcast, w =>
                {
                    w.Write(stageX);
                    w.Write(stageY);
                    w.Write(position.x);
                    w.Write(position.y);
                    w.Write(position.z);
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
                    if (b.StageX == stageX && b.StageY == stageY && (b.transform.position - position).sqrMagnitude < 0.25f)
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

        private void RemoveFromRegistry(int stageX, int stageY, Vector3 position)
        {
            for (int i = _placedBuildingRegistry.Count - 1; i >= 0; i--)
            {
                var b = _placedBuildingRegistry[i];
                if (b.stageX == stageX && b.stageY == stageY && (b.position - position).sqrMagnitude < 0.25f)
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
                    bComp.SetStageCoordinates(data.stageX, data.stageY);

                    instance.transform.SetParent(BuildingManager.Instance.BuildingsContainer, true);
                    BuildingManager.Instance.TrackBuilding(bComp);

                    int curX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : data.stageX;
                    int curY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : data.stageY;
                    instance.SetActive(data.stageX == curX && data.stageY == curY);
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
            // อัปเดตสถานะหีบเก็บของเมื่อเพื่อนฝากหรือดึงของออก
            if (LootUI.Instance != null && LootUI.Instance.IsOpen && LootUI.Instance.CurrentContainer != null)
            {
                var items = LootUI.Instance.CurrentContainer.itemsInside;
                if (items == null) return;

                if (tx.isDeposit)
                {
                    items.Add(new LootSlot(tx.itemName, tx.quantity));
                }
                else
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (string.Equals(items[i].itemName, tx.itemName, System.StringComparison.OrdinalIgnoreCase))
                        {
                            int rem = items[i].quantity - tx.quantity;
                            if (rem <= 0) items.RemoveAt(i);
                            else items[i] = new LootSlot(tx.itemName, rem);
                            break;
                        }
                    }
                }
            }
        }
    }
}

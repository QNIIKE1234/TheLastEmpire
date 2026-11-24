using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// NetworkPoolHandler — เชื่อมต่อ ObjectPoolManager เข้ากับ Unity Netcode INetworkPrefabInstanceHandler
    /// เพื่อให้การ Spawn/Despawn ศัตรูดึงและคืนออบเจกต์จาก Pool ตรงกันทั้ง Host และ Client (Section 4.4 ข้อ 4)
    /// </summary>
    public class NetworkPoolHandler : INetworkPrefabInstanceHandler
    {
        private readonly GameObject _prefab;
        private readonly string _poolKey;

        public NetworkPoolHandler(GameObject prefab, string poolKey)
        {
            _prefab = prefab;
            _poolKey = poolKey;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            GameObject instance = null;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(_poolKey))
            {
                instance = ObjectPoolManager.Instance.SpawnFromPool(_poolKey, position, rotation);
            }
            if (instance == null && _prefab != null)
            {
                instance = Object.Instantiate(_prefab, position, rotation);
            }
            if (instance == null) return null;

            NetworkObject netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null) netObj = instance.AddComponent<NetworkObject>();
            netObj.OwnerClientId = ownerClientId;
            return netObj;
        }

        public void Destroy(NetworkObject networkObject)
        {
            if (networkObject == null) return;
            if (ObjectPoolManager.Instance != null && !string.IsNullOrEmpty(_poolKey))
            {
                ObjectPoolManager.Instance.ReturnToPool(_poolKey, networkObject.gameObject);
            }
            else
            {
                Object.Destroy(networkObject.gameObject);
            }
        }
    }

    /// <summary>
    /// NetworkEnemyBridge — ซิงค์ตำแหน่ง เลือด อนิเมชัน และโหมดทะลุสิ่งกีดขวาง (FleeingSurvivor Chaser Phasing)
    /// ของฝูงศัตรูจากเครื่อง Host ไปยังเครื่อง Client แบบประหยัดแบนด์วิดท์
    /// </summary>
    public class NetworkEnemyBridge : NetworkBehaviour
    {
        public static NetworkEnemyBridge Instance { get; private set; }

        private float _broadcastTimer = 0f;
        private const float EnemyBroadcastInterval = 0.065f; // ~15 Hz batch sync

        private readonly Dictionary<int, BaseEnemyAI> _clientPuppetEnemies = new Dictionary<int, BaseEnemyAI>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Update()
        {
            if (!EmpireNetworkManager.IsOnlineHost) return;

            _broadcastTimer -= Time.unscaledDeltaTime;
            if (_broadcastTimer <= 0f)
            {
                _broadcastTimer = EnemyBroadcastInterval;
                BroadcastActiveEnemiesFromHost();
            }
        }

        private void BroadcastActiveEnemiesFromHost()
        {
            if (GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return;
            if (EmpireNetworkManager.Instance == null) return;

            var activeList = GameManager.Instance.ActiveEnemies;
            List<EnemyStateNetPayload> batch = new List<EnemyStateNetPayload>(activeList.Count);

            for (int i = 0; i < activeList.Count; i++)
            {
                GameObject enemyObj = activeList[i];
                if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

                BaseEnemyAI ai = enemyObj.GetComponent<BaseEnemyAI>();
                if (ai == null) continue;

                if (ai.NetworkEnemyId <= 0)
                {
                    ai.NetworkEnemyId = GameManager.AllocateEnemyNetworkId();
                }

                Health h = enemyObj.GetComponent<Health>();
                if (h != null && h.IsDead) continue;

                Rigidbody rb = enemyObj.GetComponent<Rigidbody>();
                Vector3 vel = rb != null ? rb.linearVelocity : Vector3.zero;
                string cleanName = enemyObj.name.Replace("(Clone)", "").Trim();

                batch.Add(new EnemyStateNetPayload
                {
                    enemyNetId = ai.NetworkEnemyId,
                    prefabName = cleanName,
                    poolKey = ai.PoolKey ?? "",
                    posX = enemyObj.transform.position.x,
                    posZ = enemyObj.transform.position.z,
                    rotY = enemyObj.transform.eulerAngles.y,
                    velX = vel.x,
                    velZ = vel.z,
                    currentHealth = h != null ? h.CurrentHealth : 100f,
                    maxHealth = h != null ? h.MaxHealth : 100f,
                    aiState = (byte)ai.CurrentState,
                    isChaserPhasing = ai.IsChaserPhasing
                });
            }

            int hostStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int hostStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int hostInteriorId = BuildingInteriorManager.CurrentInteriorId;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.EnemyBatchSync, w =>
            {
                w.Write(hostStageX);
                w.Write(hostStageY);
                w.Write(hostInteriorId);
                w.Write(batch.Count);
                var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateWriter(w));
                for (int i = 0; i < batch.Count; i++)
                {
                    EnemyStateNetPayload item = batch[i];
                    item.NetworkSerialize(serializer);
                }
            });
        }

        public void ReceiveEnemyBatchFromHost(BinaryReader reader)
        {
            int hostStageX = reader.ReadInt32();
            int hostStageY = reader.ReadInt32();
            int hostInteriorId = reader.ReadInt32();
            int count = reader.ReadInt32();
            var serializer = new BufferSerializer<BinaryBufferReaderWriter>(BinaryBufferReaderWriter.CreateReader(reader));

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInteriorId = BuildingInteriorManager.CurrentInteriorId;
            if (hostStageX != myStageX || hostStageY != myStageY || hostInteriorId != myInteriorId)
            {
                // หากเราอยู่คนละช่องพิกัด (Stage X, Y) กับ Host ให้ข้ามข้อมูลศัตรูของช่อง Host
                for (int i = 0; i < count; i++)
                {
                    EnemyStateNetPayload dummy = default;
                    dummy.NetworkSerialize(serializer);
                }
                return;
            }

            // เมื่อ Client อยู่ในช่อง (Stage X, Y) เดียวกับ Host:
            // เคลียร์ศัตรู Local ที่ Client อาจเสกไว้เองตอนเดินเข้าช่องนี้ก่อน Host เพื่อไม่ให้มีศัตรูซ้ำซ้อน/ไม่ตรงกัน!
            DespawnLocalNonPuppetEnemiesOnClient();

            HashSet<int> aliveIds = new HashSet<int>();

            for (int i = 0; i < count; i++)
            {
                EnemyStateNetPayload state = default;
                state.NetworkSerialize(serializer);
                aliveIds.Add(state.enemyNetId);

                if (!_clientPuppetEnemies.TryGetValue(state.enemyNetId, out BaseEnemyAI puppet) || puppet == null || !puppet.gameObject.activeInHierarchy)
                {
                    puppet = SpawnPuppetEnemyOnClient(state);
                    if (puppet != null)
                    {
                        _clientPuppetEnemies[state.enemyNetId] = puppet;
                    }
                }

                if (puppet != null)
                {
                    puppet.ApplyNetworkPuppetState(state);
                }
            }

            // ลบหรือคืน Pool ศัตรูที่ตายไปแล้วบนเครื่อง Host
            List<int> toRemove = null;
            foreach (var kvp in _clientPuppetEnemies)
            {
                if (!aliveIds.Contains(kvp.Key))
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    int id = toRemove[i];
                    if (_clientPuppetEnemies.TryGetValue(id, out BaseEnemyAI oldPuppet) && oldPuppet != null)
                    {
                        oldPuppet.IsNetworkPuppet = false;
                        if (!string.IsNullOrEmpty(oldPuppet.PoolKey) && ObjectPoolManager.Instance != null)
                        {
                            ObjectPoolManager.Instance.ReturnToPool(oldPuppet.PoolKey, oldPuppet.gameObject);
                        }
                        else
                        {
                            Destroy(oldPuppet.gameObject);
                        }
                    }
                    _clientPuppetEnemies.Remove(id);
                }
            }
        }

        private BaseEnemyAI SpawnPuppetEnemyOnClient(EnemyStateNetPayload state)
        {
            if (GameManager.Instance == null) return null;

            GameObject prefab = GameManager.Instance.GetPrefabByName(state.prefabName);
            if (prefab == null) prefab = GameManager.Instance.DefaultEnemyPrefab;
            if (prefab == null) return null;

            string poolKey = !string.IsNullOrEmpty(state.poolKey) ? state.poolKey : GameManager.Instance.GetPoolKeyForPrefab(prefab);
            Vector3 spawnPos = new Vector3(state.posX, 0.25f, state.posZ);
            Quaternion spawnRot = Quaternion.Euler(0f, state.rotY, 0f);

            GameObject obj = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, spawnPos, spawnRot);
            if (obj == null) return null;

            BaseEnemyAI ai = obj.GetComponent<BaseEnemyAI>();
            if (ai != null)
            {
                ai.IsNetworkPuppet = true;
                ai.NetworkEnemyId = state.enemyNetId;
            }
            return ai;
        }

        public void ClearClientPuppets()
        {
            foreach (var kvp in _clientPuppetEnemies)
            {
                BaseEnemyAI oldPuppet = kvp.Value;
                if (oldPuppet == null) continue;
                oldPuppet.IsNetworkPuppet = false;
                if (!string.IsNullOrEmpty(oldPuppet.PoolKey) && ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.ReturnToPool(oldPuppet.PoolKey, oldPuppet.gameObject);
                }
                else
                {
                    Destroy(oldPuppet.gameObject);
                }
            }
            _clientPuppetEnemies.Clear();
        }

        private void DespawnLocalNonPuppetEnemiesOnClient()
        {
            if (GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return;
            var list = GameManager.Instance.ActiveEnemies;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                GameObject obj = list[i];
                if (obj == null)
                {
                    list.RemoveAt(i);
                    continue;
                }
                BaseEnemyAI ai = obj.GetComponent<BaseEnemyAI>();
                if (ai != null && !ai.IsNetworkPuppet)
                {
                    list.RemoveAt(i);
                    if (!string.IsNullOrEmpty(ai.PoolKey) && ObjectPoolManager.Instance != null)
                    {
                        ObjectPoolManager.Instance.ReturnToPool(ai.PoolKey, obj);
                    }
                    else
                    {
                        Destroy(obj);
                    }
                }
            }
        }

        public void SendClientDamageToHost(int enemyNetId, float damage, bool isCrit)
        {
            if (!EmpireNetworkManager.IsOnlineClient || EmpireNetworkManager.Instance == null || enemyNetId <= 0) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.EnemyDamageRequest, w =>
            {
                w.Write(enemyNetId);
                w.Write(damage);
                w.Write(isCrit);
            });
        }

        public void HandleClientEnemyDamageOnHost(int enemyNetId, float damage, bool isCrit)
        {
            if (!EmpireNetworkManager.IsOnlineHost || GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return;

            var list = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                GameObject obj = list[i];
                if (obj == null || !obj.activeInHierarchy) continue;

                BaseEnemyAI ai = obj.GetComponent<BaseEnemyAI>();
                if (ai != null && ai.NetworkEnemyId == enemyNetId)
                {
                    Health h = obj.GetComponent<Health>();
                    if (h != null && !h.IsDead)
                    {
                        h.TakeDamage(damage, obj.transform.position + Vector3.up * 1.2f, isCrit, false);
                    }
                    return;
                }
            }
        }
    }
}

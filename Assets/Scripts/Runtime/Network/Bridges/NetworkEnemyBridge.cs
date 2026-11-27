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

    public struct SyncedStageEnemyRecord
    {
        public string prefabName;
        public string poolKey;
        public float posX;
        public float posZ;
        public float rotY;
        public float currentHealth;
        public float maxHealth;
    }

    public class StageEnemyProgressSnapshot
    {
        public int stageX;
        public int stageY;
        public bool isCleared;
        public List<SyncedStageEnemyRecord> enemies = new List<SyncedStageEnemyRecord>();
    }

    /// <summary>
    /// NetworkEnemyBridge — ซิงค์ตำแหน่ง เลือด อนิเมชัน และโหมดทะลุสิ่งกีดขวาง (FleeingSurvivor Chaser Phasing)
    /// ของฝูงศัตรูจากเครื่อง Host ไปยังเครื่อง Client แบบประหยัดแบนด์วิดท์
    /// พร้อมซิงค์ความคืบหน้ามอนสเตอร์ราย Node (128x128) เพื่อไม่ให้มอนสเตอร์ Respawn เต็มจำนวนเมื่อ Host วาร์ปตามเข้า Node ของเพื่อน
    /// </summary>
    public class NetworkEnemyBridge : NetworkBehaviour
    {
        public static NetworkEnemyBridge Instance { get; private set; }

        private float _broadcastTimer = 0f;
        private const float EnemyBroadcastInterval = 0.065f; // ~15 Hz batch sync

        private float _clientSoloSyncTimer = 0f;
        private const float ClientSoloSyncInterval = 0.40f;

        private readonly Dictionary<int, BaseEnemyAI> _clientPuppetEnemies = new Dictionary<int, BaseEnemyAI>();
        private readonly Dictionary<long, StageEnemyProgressSnapshot> _stageProgressCache = new Dictionary<long, StageEnemyProgressSnapshot>();

        private static long MakeStageKey(int x, int y) => ((long)x << 16) | (uint)(y & 0xFFFF);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        private void Update()
        {
            if (EmpireNetworkManager.IsOnlineHost)
            {
                _broadcastTimer -= Time.unscaledDeltaTime;
                if (_broadcastTimer <= 0f)
                {
                    _broadcastTimer = EnemyBroadcastInterval;
                    BroadcastActiveEnemiesFromHost();
                }
                return;
            }

            // หาก Client กำลังลุยมอนสเตอร์อยู่ใน Node แยกคนเดียว (ไม่ได้อยู่ Node เดียวกับ Host)
            // ให้ส่งจำนวน/ตำแหน่ง/เลือดมอนสเตอร์ที่เหลือไปอัปเดตให้ Host ทราบทุก 0.4 วิ
            // เพื่อว่าตอน Host วาร์ปเข้ามาใน Node นี้ จะเกิดเฉพาะมอนที่เหลืออยู่จริง (ไม่ Respawn กลับเป็น 5 ตัวใหม่!)
            if (EmpireNetworkManager.IsOnlineClient)
            {
                _clientSoloSyncTimer -= Time.unscaledDeltaTime;
                if (_clientSoloSyncTimer <= 0f)
                {
                    _clientSoloSyncTimer = ClientSoloSyncInterval;
                    TrySendClientSoloStageProgress();
                }
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

                // หาก Host เพิ่งวาร์ปออกจาก Node นี้ไปโดยที่ Client ยังอยู่สู้ต่อ:
                // ปลดโหมด Puppet ให้มอนสเตอร์ที่เหลือกลับมาเดิน AI บนเครื่อง Client ต่อทันที ณ ตำแหน่งและเลือดเดิม
                PromoteClientPuppetsToLocalEnemies();
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

            bool isAlienWorldBoss = state.prefabName != null &&
                                    state.prefabName.IndexOf("AlienLeader", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                    state.maxHealth >= 10000f;
            string poolKey = isAlienWorldBoss
                ? ""
                : (!string.IsNullOrEmpty(state.poolKey) ? state.poolKey : GameManager.Instance.GetPoolKeyForPrefab(prefab));
            Vector3 spawnPos = new Vector3(state.posX, 0.25f, state.posZ);
            Quaternion spawnRot = Quaternion.Euler(0f, state.rotY, 0f);

            GameObject obj = GameManager.Instance.SpawnEnemyInstanceAt(prefab, poolKey, spawnPos, spawnRot);
            if (obj == null) return null;

            if (isAlienWorldBoss)
            {
                obj.transform.localScale = Vector3.one * RoamingAlienBossManager.BossScale;
                if (obj.GetComponent<AlienBossVisualController>() == null)
                {
                    obj.AddComponent<AlienBossVisualController>();
                }
            }

            BaseEnemyAI ai = obj.GetComponent<BaseEnemyAI>();
            if (ai != null)
            {
                ai.IsNetworkPuppet = true;
                ai.NetworkEnemyId = state.enemyNetId;
                if (isAlienWorldBoss)
                {
                    ai.SetPoolKey("");
                }
            }
            return ai;
        }

        public void ClearClientPuppets(bool returnToPool = true)
        {
            foreach (var kvp in _clientPuppetEnemies)
            {
                BaseEnemyAI oldPuppet = kvp.Value;
                if (oldPuppet == null) continue;
                oldPuppet.IsNetworkPuppet = false;
                oldPuppet.NetworkEnemyId = -1;
                if (returnToPool)
                {
                    if (!string.IsNullOrEmpty(oldPuppet.PoolKey) && ObjectPoolManager.Instance != null)
                    {
                        ObjectPoolManager.Instance.ReturnToPool(oldPuppet.PoolKey, oldPuppet.gameObject);
                    }
                    else
                    {
                        Destroy(oldPuppet.gameObject);
                    }
                }
            }
            _clientPuppetEnemies.Clear();
        }

        private void PromoteClientPuppetsToLocalEnemies()
        {
            if (_clientPuppetEnemies.Count == 0) return;

            foreach (var kvp in _clientPuppetEnemies)
            {
                BaseEnemyAI puppet = kvp.Value;
                if (puppet == null) continue;
                puppet.IsNetworkPuppet = false;
                puppet.NetworkEnemyId = -1;
            }
            _clientPuppetEnemies.Clear();
            _clientSoloSyncTimer = 0f;
            TrySendClientSoloStageProgress();
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

        public void SendClientDamageToHost(int enemyNetId, float damage, bool isCrit, bool isExplosion = false)
        {
            if (!EmpireNetworkManager.IsOnlineClient || EmpireNetworkManager.Instance == null || enemyNetId <= 0) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.EnemyDamageRequest, w =>
            {
                w.Write(enemyNetId);
                w.Write(damage);
                w.Write(isCrit);
                w.Write(isExplosion);
            });
        }

        public void HandleClientEnemyDamageOnHost(int enemyNetId, float damage, bool isCrit, bool isExplosion = false, ulong sourceClientId = ulong.MaxValue)
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
                        h.TakeNetworkClientDamage(damage, obj.transform.position + Vector3.up * 1.2f, isCrit, isExplosion, sourceClientId);
                    }
                    return;
                }
            }
        }

        // =====================================================
        //  Per-Node Enemy Progress Sync (Prevents Respawn When Joining Friend's Node)
        // =====================================================

        public void ClearStageProgressCache()
        {
            _stageProgressCache.Clear();
        }

        public void NotifyClientSoloEnemyKilled()
        {
            if (!EmpireNetworkManager.IsOnlineClient) return;
            _clientSoloSyncTimer = 0f;
            TrySendClientSoloStageProgress();
        }

        private bool IsClientInSameStageAsHost()
        {
            if (!EmpireNetworkManager.IsOnlineClient || WorldMapManager.Instance == null) return false;
            PlayerController hostAvatar = PlayerRegistry.GetPlayerByClientId(0);
            if (hostAvatar == null) return false;
            return hostAvatar.NetworkStageX == WorldMapManager.Instance.CurrentPlayerX &&
                   hostAvatar.NetworkStageY == WorldMapManager.Instance.CurrentPlayerY;
        }

        private void TrySendClientSoloStageProgress()
        {
            if (!EmpireNetworkManager.IsOnlineClient || WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;
            if (IsClientInSameStageAsHost()) return;

            int x = WorldMapManager.Instance.CurrentPlayerX;
            int y = WorldMapManager.Instance.CurrentPlayerY;
            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(x, y);
            if (stage == null) return;

            // ส่งเฉพาะเมื่อ Node นี้มีมอนสเตอร์ Local ที่ถูกเสกแล้ว หรือถูกเคลียร์แล้ว (ไม่ส่งทับตอนกำลังโหลดห้องเปล่า)
            bool hasAnyNonPuppet = HasAnyLocalNonPuppetEnemy();
            if (!hasAnyNonPuppet && !stage.isCleared && stage.remainingEnemyPrefabNames == null) return;

            SendStageProgressSnapshot(stage);
        }

        private bool HasAnyLocalNonPuppetEnemy()
        {
            if (GameManager.Instance == null || GameManager.Instance.ActiveEnemies == null) return false;
            var list = GameManager.Instance.ActiveEnemies;
            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = list[i];
                if (go == null) continue;
                BaseEnemyAI ai = go.GetComponent<BaseEnemyAI>();
                if (ai != null && !ai.IsNetworkPuppet) return true;
            }
            return false;
        }

        public void SendStageProgressSnapshot(StageData stage)
        {
            if (!EmpireNetworkManager.IsOnline || EmpireNetworkManager.Instance == null || stage == null) return;
            if (EmpireNetworkManager.IsOnlineClient && IsClientInSameStageAsHost()) return;

            List<SyncedStageEnemyRecord> aliveRecords = new List<SyncedStageEnemyRecord>();
            if (!stage.isCleared && GameManager.Instance != null && GameManager.Instance.ActiveEnemies != null)
            {
                var list = GameManager.Instance.ActiveEnemies;
                for (int i = 0; i < list.Count; i++)
                {
                    GameObject enemy = list[i];
                    if (enemy == null || !enemy.activeInHierarchy) continue;

                    BaseEnemyAI ai = enemy.GetComponent<BaseEnemyAI>();
                    if (ai != null && ai.IsNetworkPuppet) continue;

                    Health h = enemy.GetComponent<Health>();
                    if (h != null && h.IsDead) continue;

                    aliveRecords.Add(new SyncedStageEnemyRecord
                    {
                        prefabName = enemy.name.Replace("(Clone)", "").Trim(),
                        poolKey = ai != null ? (ai.PoolKey ?? "") : "",
                        posX = enemy.transform.position.x,
                        posZ = enemy.transform.position.z,
                        rotY = enemy.transform.eulerAngles.y,
                        currentHealth = h != null ? h.CurrentHealth : 100f,
                        maxHealth = h != null ? h.MaxHealth : 100f
                    });
                }
            }

            bool cleared = stage.isCleared || (aliveRecords.Count == 0 && HasAnyLocalNonPuppetEnemy());
            if (cleared)
            {
                stage.isCleared = true;
            }
            SendStageEnemyProgressPacket(stage.x, stage.y, cleared, aliveRecords);
        }

        private void SendStageEnemyProgressPacket(int stageX, int stageY, bool isCleared, List<SyncedStageEnemyRecord> records)
        {
            if (EmpireNetworkManager.Instance == null) return;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageEnemyProgressSync, w =>
            {
                w.Write(stageX);
                w.Write(stageY);
                w.Write(isCleared);
                int count = records != null ? records.Count : 0;
                w.Write(count);
                for (int i = 0; i < count; i++)
                {
                    SyncedStageEnemyRecord r = records[i];
                    w.Write(r.prefabName ?? "");
                    w.Write(r.poolKey ?? "");
                    w.Write(r.posX);
                    w.Write(r.posZ);
                    w.Write(r.rotY);
                    w.Write(r.currentHealth);
                    w.Write(r.maxHealth);
                }
            });
        }

        public void ReceiveStageEnemyProgress(BinaryReader reader, bool isHost, ulong senderClientId)
        {
            int stageX = reader.ReadInt32();
            int stageY = reader.ReadInt32();
            bool isCleared = reader.ReadBoolean();
            int count = reader.ReadInt32();

            StageEnemyProgressSnapshot snapshot = new StageEnemyProgressSnapshot
            {
                stageX = stageX,
                stageY = stageY,
                isCleared = isCleared,
                enemies = new List<SyncedStageEnemyRecord>(Mathf.Max(0, count))
            };

            List<string> prefabNames = new List<string>(Mathf.Max(0, count));
            for (int i = 0; i < count; i++)
            {
                SyncedStageEnemyRecord rec = new SyncedStageEnemyRecord
                {
                    prefabName = reader.ReadString(),
                    poolKey = reader.ReadString(),
                    posX = reader.ReadSingle(),
                    posZ = reader.ReadSingle(),
                    rotY = reader.ReadSingle(),
                    currentHealth = reader.ReadSingle(),
                    maxHealth = reader.ReadSingle()
                };
                snapshot.enemies.Add(rec);
                if (!string.IsNullOrEmpty(rec.prefabName))
                {
                    prefabNames.Add(rec.prefabName);
                }
            }

            // หากเครื่องเราเองกำลังอยู่ใน Node เดียวกันนี้อยู่แล้ว (Host เป็นเจ้าของห้องนี้อยู่) ไม่ต้องทับ
            bool weAreCurrentlyInThisStage = WorldMapManager.Instance != null &&
                                             WorldMapManager.Instance.CurrentPlayerX == stageX &&
                                             WorldMapManager.Instance.CurrentPlayerY == stageY;
            if (!weAreCurrentlyInThisStage)
            {
                _stageProgressCache[MakeStageKey(stageX, stageY)] = snapshot;

                if (WorldMapManager.Instance != null && WorldMapManager.Instance.MapGenerator != null)
                {
                    StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(stageX, stageY);
                    if (stage != null)
                    {
                        stage.isCleared = isCleared;
                        stage.remainingEnemyPrefabNames = isCleared ? new List<string>() : prefabNames;
                    }
                }
            }

            // Host กระจายความคืบหน้า Node นี้ให้ลูกห้องคนอื่นด้วย (ยกเว้นคนส่ง)
            if (isHost && EmpireNetworkManager.Instance != null && senderClientId != 0)
            {
                SendStageEnemyProgressPacket(stageX, stageY, isCleared, snapshot.enemies);
            }
        }

        /// <summary>
        /// เรียกใน GameManager.SpawnLocalStageContent(stage) เมื่อ Host หรือ Client เดินเข้า Node
        /// หากมีข้อมูลความคืบหน้ามอนสเตอร์ที่เพื่อนกำลังตีค้างไว้อยู่ใน Node นี้ -> เสกเฉพาะมอนสเตอร์ที่เหลืออยู่จริงพร้อมตำแหน่งและเลือดเดิมเป๊ะ!
        /// </summary>
        public bool TrySpawnFromSyncedProgress(StageData stage)
        {
            if (stage == null || GameManager.Instance == null) return false;
            long key = MakeStageKey(stage.x, stage.y);
            if (!_stageProgressCache.TryGetValue(key, out StageEnemyProgressSnapshot snapshot) || snapshot == null)
            {
                return false;
            }

            _stageProgressCache.Remove(key);

            if (snapshot.isCleared || snapshot.enemies == null || snapshot.enemies.Count == 0)
            {
                stage.isCleared = true;
                stage.remainingEnemyPrefabNames = new List<string>();
                Debug.Log($"<color=#38BDF8>[NetworkEnemyBridge]</color> Restored synced CLEARED state for Stage ({stage.x}, {stage.y}) (0 enemies).");
                return true;
            }

            for (int i = 0; i < snapshot.enemies.Count; i++)
            {
                SyncedStageEnemyRecord rec = snapshot.enemies[i];
                GameObject prefab = GameManager.Instance.GetPrefabByName(rec.prefabName);
                if (prefab == null) prefab = GameManager.Instance.DefaultEnemyPrefab;
                if (prefab == null) continue;

                string pKey = !string.IsNullOrEmpty(rec.poolKey) ? rec.poolKey : GameManager.Instance.GetPoolKeyForPrefab(prefab);
                Vector3 pos = new Vector3(rec.posX, 0.25f, rec.posZ);
                Quaternion rot = Quaternion.Euler(0f, rec.rotY, 0f);

                GameObject enemyObj = GameManager.Instance.SpawnEnemyInstanceAt(prefab, pKey, pos, rot);
                if (enemyObj != null)
                {
                    Health h = enemyObj.GetComponent<Health>();
                    if (h != null && rec.maxHealth > 0f && rec.currentHealth > 0f)
                    {
                        h.SetHealthFromNetwork(rec.currentHealth, rec.maxHealth);
                    }
                }
            }

            Debug.Log($"<color=#38BDF8>[NetworkEnemyBridge]</color> Adopted {snapshot.enemies.Count} remaining synced enemies for Stage ({stage.x}, {stage.y}) without respawning full wave!");
            return true;
        }

        // =====================================================
        //  Boss / Enemy Attack Telegraph Sync (Host -> Clients)
        // =====================================================

        public const byte TelegraphBossStomp = 1;
        public const byte TelegraphBossCharge = 2;
        public const byte TelegraphRobotBladeDash = 3;
        public const byte TelegraphRobotKamikaze = 4;
        public const byte TelegraphZombieBoomer = 5;
        public const byte TelegraphBossLeap = 6;
        public const byte TelegraphZombieSpit = 7;
        public const byte TelegraphAlienSpit = 8;
        public const byte TelegraphZombieSpitAim = 9;
        public const byte TelegraphAlienSpitAim = 10;

        public void BroadcastEnemyTelegraph(
            BaseEnemyAI enemy,
            byte telegraphType,
            Vector3 origin,
            Vector3 dir,
            float sizeParam,
            float duration)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null) return;
            if (duration <= 0.02f) return;

            int enemyNetId = enemy != null ? enemy.NetworkEnemyId : -1;
            int hostStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int hostStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int hostInteriorId = BuildingInteriorManager.CurrentInteriorId;

            EmpireNetworkManager.Instance.SendPacket(NetPacketType.EnemyTelegraphSync, w =>
            {
                w.Write(hostStageX);
                w.Write(hostStageY);
                w.Write(hostInteriorId);
                w.Write(enemyNetId);
                w.Write(telegraphType);
                w.Write(origin.x);
                w.Write(origin.y);
                w.Write(origin.z);
                w.Write(dir.x);
                w.Write(dir.y);
                w.Write(dir.z);
                w.Write(sizeParam);
                w.Write(duration);
            });
        }

        public void HandleEnemyTelegraphFromHost(
            int hostStageX,
            int hostStageY,
            int hostInteriorId,
            int enemyNetId,
            byte telegraphType,
            Vector3 origin,
            Vector3 dir,
            float sizeParam,
            float duration)
        {
            if (EmpireNetworkManager.IsOnlineHost) return;

            int myStageX = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerX : 64;
            int myStageY = WorldMapManager.Instance != null ? WorldMapManager.Instance.CurrentPlayerY : 64;
            int myInteriorId = BuildingInteriorManager.CurrentInteriorId;
            if (hostStageX != myStageX || hostStageY != myStageY || hostInteriorId != myInteriorId) return;

            Transform followTarget = null;
            if (enemyNetId > 0 && _clientPuppetEnemies.TryGetValue(enemyNetId, out BaseEnemyAI puppet) && puppet != null)
            {
                followTarget = puppet.transform;
            }

            // Replicate Spitter directional ground hitbox telegraph before spit launch on Co-op Clients!
            if (telegraphType == TelegraphZombieSpitAim || telegraphType == TelegraphAlienSpitAim)
            {
                SpitProjectileStyle style = telegraphType == TelegraphZombieSpitAim
                    ? SpitProjectileStyle.ZombieAcid
                    : SpitProjectileStyle.AlienPlasma;
                float laneLen = Mathf.Max(3f, sizeParam);
                float laneWidth = laneLen >= 13.0f ? 1.42f : 1.05f;
                SpitterProjectileVisual.SpawnSpitAimTelegraph(
                    style,
                    origin,
                    dir,
                    laneLen,
                    laneWidth,
                    Mathf.Clamp(duration, 0.1f, 2.0f),
                    followTarget);
                return;
            }

            // Replicate glowing 3D Zombie (Green) / Alien (Purple) Spitter projectiles on Co-op Clients!
            if (telegraphType == TelegraphZombieSpit || telegraphType == TelegraphAlienSpit)
            {
                SpitProjectileStyle style = telegraphType == TelegraphZombieSpit
                    ? SpitProjectileStyle.ZombieAcid
                    : SpitProjectileStyle.AlienPlasma;
                GameObject ownerObj = followTarget != null ? followTarget.gameObject : null;
                SpitterProjectileVisual.CreateSpitProjectile(style, origin, dir, duration, ownerObj, Mathf.Max(0.75f, sizeParam));
                return;
            }

            StartCoroutine(ShowClientEnemyTelegraphRoutine(followTarget, telegraphType, origin, dir, sizeParam, Mathf.Clamp(duration, 0.1f, 5f)));
        }

        private System.Collections.IEnumerator ShowClientEnemyTelegraphRoutine(
            Transform followTarget,
            byte telegraphType,
            Vector3 origin,
            Vector3 dir,
            float sizeParam,
            float duration)
        {
            List<GameObject> spawned = new List<GameObject>();

            bool isChevronDash = telegraphType == TelegraphBossCharge || telegraphType == TelegraphRobotBladeDash;
            if (isChevronDash)
            {
                Sprite chevSp = HitboxSpriteLibrary.DirectionalChevrons;
                if (chevSp != null)
                {
                    Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
                    if (flatDir.sqrMagnitude < 0.001f) flatDir = Vector3.forward;
                    flatDir.Normalize();

                    float length = Mathf.Max(2f, sizeParam);
                    int segCount = telegraphType == TelegraphBossCharge ? 3 : 1;
                    float segLen = telegraphType == TelegraphBossCharge ? Mathf.Max(1.8f, length / segCount) : length;
                    float segWidth = telegraphType == TelegraphBossCharge ? 2.4f : 1.15f;
                    float bw = chevSp.bounds.size.x;
                    float bh = chevSp.bounds.size.y;

                    for (int i = 0; i < segCount; i++)
                    {
                        SpriteRenderer sr = HitboxSpriteLibrary.CreateGroundSpriteIndicator($"NetTelegraphChevron_{i}", null, chevSp, HitboxSpriteLibrary.DefaultOriginalColor, 7);
                        if (sr == null) continue;
                        float distAlong = segCount == 1 ? (length * 0.5f) : (length * ((i + 0.5f) / segCount));
                        sr.transform.position = new Vector3(origin.x + flatDir.x * distAlong, 0.06f, origin.z + flatDir.z * distAlong);
                        sr.transform.rotation = HitboxSpriteLibrary.GetGroundChevronRotation(flatDir);
                        if (bw > 0.001f && bh > 0.001f)
                        {
                            sr.transform.localScale = new Vector3(segLen / bw, segWidth / bh, 1f);
                        }
                        spawned.Add(sr.gameObject);
                    }
                }
            }
            else
            {
                Sprite hazardSp = HitboxSpriteLibrary.HazardCircle;
                if (hazardSp != null)
                {
                    SpriteRenderer sr = HitboxSpriteLibrary.CreateGroundSpriteIndicator("NetTelegraphCircle", null, hazardSp, HitboxSpriteLibrary.DefaultOriginalColor, 7);
                    if (sr != null)
                    {
                        float diameter = Mathf.Max(1f, sizeParam * 2f);
                        sr.transform.position = new Vector3(origin.x, 0.06f, origin.z);
                        sr.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        HitboxSpriteLibrary.SetWorldDiameter(sr, diameter);
                        spawned.Add(sr.gameObject);
                    }
                }
            }

            bool shouldFollowEnemy = followTarget != null &&
                (telegraphType == TelegraphBossStomp || telegraphType == TelegraphRobotKamikaze || telegraphType == TelegraphZombieBoomer);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                if (shouldFollowEnemy)
                {
                    if (followTarget == null || !followTarget.gameObject.activeInHierarchy) break;
                    Vector3 p = followTarget.position;
                    for (int i = 0; i < spawned.Count; i++)
                    {
                        if (spawned[i] != null)
                        {
                            spawned[i].transform.position = new Vector3(p.x, 0.06f, p.z);
                        }
                    }
                }
                yield return null;
            }

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null) Destroy(spawned[i]);
            }
        }
    }
}

using System;
using Unity.Netcode;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// ข้อมูลซิงค์การย้ายห้องของปาร์ตี้ (ส่งครั้งเดียวตอนเปลี่ยนด่าน รับประกัน 0% Diff)
    /// </summary>
    [Serializable]
    public struct StageTransitionNetPayload : INetworkSerializable
    {
        public int worldSeed;
        public int targetStageX;
        public int targetStageY;
        public byte entryDirection;       // TransitionPortal.TransitionDirection enum
        public byte triggeredEventType;   // StageEventType enum
        public int stageEventSeed;        // Seed สำหรับสุ่มชนิดซอมบี้และตำแหน่งของในอีเวนต์

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref worldSeed);
            serializer.SerializeValue(ref targetStageX);
            serializer.SerializeValue(ref targetStageY);
            serializer.SerializeValue(ref entryDirection);
            serializer.SerializeValue(ref triggeredEventType);
            serializer.SerializeValue(ref stageEventSeed);
        }
    }

    /// <summary>
    /// ข้อมูลสิ่งปลูกสร้างที่วางในด่าน
    /// </summary>
    [Serializable]
    public struct PlacedBuildingNetData : INetworkSerializable
    {
        public int instanceId;
        public string buildingId;
        public int stageX;
        public int stageY;
        public Vector3 position;
        public float rotationY;
        public float currentHealth;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref instanceId);
            serializer.SerializeValue(ref buildingId);
            serializer.SerializeValue(ref stageX);
            serializer.SerializeValue(ref stageY);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref rotationY);
            serializer.SerializeValue(ref currentHealth);
        }
    }

    /// <summary>
    /// ข้อมูลสถานะผู้เล่น 1–6 คนสำหรับซิงค์การเคลื่อนที่ อาวุธ เลือด และสถานะล้มรอชุบ (Downed & Revive)
    /// </summary>
    [Serializable]
    public struct PlayerStateNetPayload : INetworkSerializable
    {
        public ulong clientId;
        public int slotIndex;
        public string playerName;
        public string characterId;
        public int stageX;
        public int stageY;
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 aimDirection;
        public float currentHealth;
        public float maxHealth;
        public byte heldItemType;
        public string activeWeaponName;
        public bool isSprinting;
        public bool isSitting;
        public bool isDashing;
        public bool isShiftActive;
        public byte shiftType;
        public bool isDowned;
        public float downedRemainingSeconds;
        public float reviveProgress01;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref clientId);
            serializer.SerializeValue(ref slotIndex);
            serializer.SerializeValue(ref playerName);
            serializer.SerializeValue(ref characterId);
            serializer.SerializeValue(ref stageX);
            serializer.SerializeValue(ref stageY);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref velocity);
            serializer.SerializeValue(ref aimDirection);
            serializer.SerializeValue(ref currentHealth);
            serializer.SerializeValue(ref maxHealth);
            serializer.SerializeValue(ref heldItemType);
            serializer.SerializeValue(ref activeWeaponName);
            serializer.SerializeValue(ref isSprinting);
            serializer.SerializeValue(ref isSitting);
            serializer.SerializeValue(ref isDashing);
            serializer.SerializeValue(ref isShiftActive);
            serializer.SerializeValue(ref shiftType);
            serializer.SerializeValue(ref isDowned);
            serializer.SerializeValue(ref downedRemainingSeconds);
            serializer.SerializeValue(ref reviveProgress01);
        }
    }

    /// <summary>
    /// ข้อมูลการยิงกระสุนพร้อม Client-Side Visual Prediction
    /// </summary>
    [Serializable]
    public struct ProjectileFireNetPayload : INetworkSerializable
    {
        public ulong shooterClientId;
        public int stageX;
        public int stageY;
        public Vector3 spawnPosition;
        public Vector3 aimDirection;
        public Vector3 aimTargetPoint;
        public string weaponName;
        public int pelletSeed;
        public float damage;
        public float weaponRange;
        public bool isRocketLauncher;
        public int pelletCount;
        public float spreadAngle;
        public bool canPierce;
        public string visualId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref shooterClientId);
            serializer.SerializeValue(ref stageX);
            serializer.SerializeValue(ref stageY);
            serializer.SerializeValue(ref spawnPosition);
            serializer.SerializeValue(ref aimDirection);
            serializer.SerializeValue(ref aimTargetPoint);
            serializer.SerializeValue(ref weaponName);
            serializer.SerializeValue(ref pelletSeed);
            serializer.SerializeValue(ref damage);
            serializer.SerializeValue(ref weaponRange);
            serializer.SerializeValue(ref isRocketLauncher);
            serializer.SerializeValue(ref pelletCount);
            serializer.SerializeValue(ref spreadAngle);
            serializer.SerializeValue(ref canPierce);
            serializer.SerializeValue(ref visualId);
        }
    }

    /// <summary>
    /// ข้อมูลการฟันอาวุธระยะประชิด
    /// </summary>
    [Serializable]
    public struct MeleeAttackNetPayload : INetworkSerializable
    {
        public ulong attackerClientId;
        public int stageX;
        public int stageY;
        public Vector3 attackOrigin;
        public Vector3 aimDirection;
        public string weaponName;
        public int comboStep;
        public float damage;
        public float radius;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref attackerClientId);
            serializer.SerializeValue(ref stageX);
            serializer.SerializeValue(ref stageY);
            serializer.SerializeValue(ref attackOrigin);
            serializer.SerializeValue(ref aimDirection);
            serializer.SerializeValue(ref weaponName);
            serializer.SerializeValue(ref comboStep);
            serializer.SerializeValue(ref damage);
            serializer.SerializeValue(ref radius);
        }
    }

    /// <summary>
    /// ข้อมูลซิงค์ศัตรูแบบประหยัดแบนด์วิดท์ (พิกัด XZ, มุมหมุน Y, เลือด และโหมดทะลุสิ่งกีดขวาง)
    /// </summary>
    [Serializable]
    public struct EnemyStateNetPayload : INetworkSerializable
    {
        public int enemyNetId;
        public string prefabName;
        public string poolKey;
        public float posX;
        public float posZ;
        public float rotY;
        public float velX;
        public float velZ;
        public float currentHealth;
        public float maxHealth;
        public byte aiState;
        public bool isChaserPhasing;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref enemyNetId);
            serializer.SerializeValue(ref prefabName);
            serializer.SerializeValue(ref poolKey);
            serializer.SerializeValue(ref posX);
            serializer.SerializeValue(ref posZ);
            serializer.SerializeValue(ref rotY);
            serializer.SerializeValue(ref velX);
            serializer.SerializeValue(ref velZ);
            serializer.SerializeValue(ref currentHealth);
            serializer.SerializeValue(ref maxHealth);
            serializer.SerializeValue(ref aiState);
            serializer.SerializeValue(ref isChaserPhasing);
        }
    }

    /// <summary>
    /// ข้อมูลไอเทมดรอปบนพื้นพร้อมรหัส Mutex ป้องกันการเก็บซ้ำ (Anti-Dupe)
    /// </summary>
    [Serializable]
    public struct GroundLootNetData : INetworkSerializable
    {
        public int dropId;
        public int stageX;
        public int stageY;
        public string itemName;
        public int quantity;
        public bool isMoney;
        public int moneyAmount;
        public Vector3 position;
        public bool isClaimed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref dropId);
            serializer.SerializeValue(ref stageX);
            serializer.SerializeValue(ref stageY);
            serializer.SerializeValue(ref itemName);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref isMoney);
            serializer.SerializeValue(ref moneyAmount);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref isClaimed);
        }
    }

    /// <summary>
    /// ข้อมูลธุรกรรมฝาก/ถอนของในหีบเก็บของส่วนกลาง (StorageCrateBuilding)
    /// </summary>
    [Serializable]
    public struct CrateTransactionNetPayload : INetworkSerializable
    {
        public int buildingInstanceId;
        public ulong requestingClientId;
        public string itemName;
        public int quantity;
        public bool isDeposit;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref buildingInstanceId);
            serializer.SerializeValue(ref requestingClientId);
            serializer.SerializeValue(ref itemName);
            serializer.SerializeValue(ref quantity);
            serializer.SerializeValue(ref isDeposit);
        }
    }
}

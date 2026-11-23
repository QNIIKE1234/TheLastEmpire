using System;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastEmpire.Network
{
    /// <summary>
    /// PlayerRegistry — ทะเบียนกลางสำหรับผู้เล่น 1–6 คนในฉาก
    /// - แยกแยะ LocalPlayer (ตัวละครของเครื่องเรา) ออกจาก RemotePlayers (เพื่อนร่วมทีม)
    /// - ให้ AI ศัตรูค้นหาผู้เล่นที่ยังมีชีวิตและอยู่ใกล้ที่สุดได้ใน O(N) โดยไม่ต้องเรียก FindObjectsByType
    /// - ตรวจสอบเงื่อนไข Party Wipe (ทุกคนตายหรือล้มหมด)
    /// </summary>
    public static class PlayerRegistry
    {
        public const int MaxPartySize = 6;

        private static readonly List<PlayerController> _allPlayers = new List<PlayerController>(MaxPartySize);
        public static IReadOnlyList<PlayerController> AllPlayers => _allPlayers;

        public static PlayerController LocalPlayer { get; private set; }

        public static event Action<PlayerController> OnPlayerRegistered;
        public static event Action<PlayerController> OnPlayerUnregistered;

        public static void Register(PlayerController player)
        {
            if (player == null) return;

            CleanupNullEntries();

            if (!_allPlayers.Contains(player))
            {
                _allPlayers.Add(player);
            }

            if (!player.IsRemotePlayer)
            {
                LocalPlayer = player;
            }

            OnPlayerRegistered?.Invoke(player);
        }

        public static void Unregister(PlayerController player)
        {
            if (player == null) return;

            _allPlayers.Remove(player);
            if (LocalPlayer == player)
            {
                LocalPlayer = null;
                for (int i = 0; i < _allPlayers.Count; i++)
                {
                    if (_allPlayers[i] != null && !_allPlayers[i].IsRemotePlayer)
                    {
                        LocalPlayer = _allPlayers[i];
                        break;
                    }
                }
            }

            CleanupNullEntries();
            OnPlayerUnregistered?.Invoke(player);
        }

        public static void CleanupNullEntries()
        {
            for (int i = _allPlayers.Count - 1; i >= 0; i--)
            {
                if (_allPlayers[i] == null)
                {
                    _allPlayers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// อัปเดตการแสดงผลของเพื่อนร่วมทีมทุกคนตามช่องพิกัด (Stage X, Y) ปัจจุบันของเรา
        /// ถ้าอยู่ในช่องเดียวกันจะเปิดแสดงตัวละครให้เจอกัน ถ้าอยู่คนละช่องจะซ่อนไว้
        /// </summary>
        public static void RefreshAllRemotePlayersStageVisibility()
        {
            CleanupNullEntries();
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController p = _allPlayers[i];
                if (p != null && p.IsRemotePlayer)
                {
                    p.RefreshStageVisibility();
                }
            }
        }

        public static int GetTotalPlayerCount()
        {
            CleanupNullEntries();
            return _allPlayers.Count;
        }

        /// <summary>
        /// คืนค่าจำนวนผู้เล่นที่ยังยืนสู้ได้ (ยังไม่ตาย และไม่ได้อยู่ในสถานะ Downed คลานรอชุบ)
        /// </summary>
        public static int GetAliveAndUpPlayerCount()
        {
            CleanupNullEntries();
            int count = 0;
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController p = _allPlayers[i];
                if (p == null) continue;
                Health h = p.PlayerHealth ?? p.GetComponent<Health>();
                bool isDead = h != null && h.IsDead;
                if (!isDead && !p.IsDowned)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// ตรวจสอบว่ายังมีเพื่อนร่วมทีมคนอื่น (ที่ไม่ใช่ excludePlayer) ที่ยังยืนสู้ได้อยู่หรือไม่
        /// ใช้ตัดสินใจไม่ให้ขึ้นหน้าจอ GameOver ในโหมด Online/Co-op ตราบใดที่ยังมีเพื่อนรอดชีวิตอยู่
        /// </summary>
        public static bool HasAnyOtherAlivePlayer(PlayerController excludePlayer)
        {
            CleanupNullEntries();
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController p = _allPlayers[i];
                if (p == null || p == excludePlayer) continue;
                Health h = p.PlayerHealth ?? p.GetComponent<Health>();
                bool isDead = h != null && h.IsDead;
                if (!isDead && !p.IsDowned)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// ตรวจสอบว่าปาร์ตี้ล้ม/ตายครบทุกคนแล้วหรือยัง (ใช้ตัดสิน Game Over ในโหมด Co-op)
        /// </summary>
        public static bool AreAllPlayersDeadOrDowned()
        {
            CleanupNullEntries();
            if (_allPlayers.Count == 0) return false;
            return GetAliveAndUpPlayerCount() == 0;
        }

        public static PlayerController GetPlayerByClientId(ulong clientId)
        {
            CleanupNullEntries();
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController p = _allPlayers[i];
                if (p != null && p.NetworkClientId == clientId)
                {
                    return p;
                }
            }
            return null;
        }

        public static PlayerController GetPlayerBySlotIndex(int slotIndex)
        {
            CleanupNullEntries();
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController p = _allPlayers[i];
                if (p != null && p.PartySlotIndex == slotIndex)
                {
                    return p;
                }
            }
            return null;
        }

        /// <summary>
        /// ค้นหาผู้เล่นที่ยังมีชีวิต (และไม่ล้ม Downed) ที่อยู่ใกล้ตำแหน่ง fromPosition ที่สุด
        /// สำหรับให้ BaseEnemyAI / ZombieAI / RobotAI / AlienAI ใช้เลือกเป้าหมายในโหมด Co-op 1–6 คน
        /// </summary>
        public static PlayerController GetClosestAlivePlayer(Vector3 fromPosition, float maxRange = float.MaxValue)
        {
            CleanupNullEntries();

            PlayerController bestPlayer = null;
            float bestDistSq = maxRange < float.MaxValue ? maxRange * maxRange : float.MaxValue;

            for (int i = 0; i < _allPlayers.Count; i++)
            {
                PlayerController candidate = _allPlayers[i];
                if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;

                Health h = candidate.PlayerHealth ?? candidate.GetComponent<Health>();
                if (h != null && h.IsDead) continue;
                if (candidate.IsDowned) continue;

                float distSq = (candidate.transform.position - fromPosition).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestPlayer = candidate;
                }
            }

            // Fallback: หากทุกคนล้ม Downed หมด ให้ชี้ไปที่คนที่ยังไม่ตายสนิทที่ใกล้ที่สุด หรือ LocalPlayer
            if (bestPlayer == null)
            {
                for (int i = 0; i < _allPlayers.Count; i++)
                {
                    PlayerController candidate = _allPlayers[i];
                    if (candidate == null || !candidate.gameObject.activeInHierarchy) continue;
                    Health h = candidate.PlayerHealth ?? candidate.GetComponent<Health>();
                    if (h == null || !h.IsDead)
                    {
                        return candidate;
                    }
                }
            }

            return bestPlayer != null ? bestPlayer : (LocalPlayer != null ? LocalPlayer : PlayerController.Instance);
        }
    }
}

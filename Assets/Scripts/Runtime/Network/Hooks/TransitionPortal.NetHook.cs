using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// TransitionPortal.NetHook — ส่วนขยาย partial class สำหรับระบบย้ายห้องแบบปาร์ตี้ (Party Travel & Spawn Formation)
    /// - เมื่ออยู่ในโหมด Co-op Multiplayer การกด [E] ที่ประตูจะส่งคำขอไปยัง NetworkStageSyncManager
    ///   เพื่อนับถอยหลังและพาสมาชิกทุกคนในปาร์ตี้ (1–6 คน) ย้ายห้องพร้อมกันด้วย Two-Phase Barrier
    /// - จัดแถวยืนหน้าประตูทางเข้าห้องใหม่ห่างกันคนละ 1.2m ตาม PartySlotIndex (0..5) ป้องกันฟิสิกส์ชนกันกระเด็น
    /// </summary>
    public partial class TransitionPortal
    {
        private bool TryTriggerNetworkPartyTransition(PlayerController player)
        {
            if (player == null || player.IsRemotePlayer) return false;

            // ผู้เล่นแต่ละคนเดินข้ามช่องพิกัด (Stage X, Y) ได้อย่างอิสระ:
            // - ถ้าเดินมาอยู่ในช่อง (X, Y) เดียวกัน จะมองเห็นและเจอกันในฉาก
            // - ถ้าเดินแยกไปคนละช่อง (X, Y) จะไม่เจอกันจนกว่าจะเดินมาช่องเดียวกัน
            if (EmpireNetworkManager.IsOnline)
            {
                ExecuteTeleport(player);
                if (WorldMapManager.Instance != null)
                {
                    player.NetworkStageX = WorldMapManager.Instance.CurrentPlayerX;
                    player.NetworkStageY = WorldMapManager.Instance.CurrentPlayerY;
                }
                NetworkEnemyBridge.Instance?.ClearClientPuppets();
                PlayerRegistry.RefreshAllRemotePlayersStageVisibility();
                return true;
            }

            // กรณีออฟไลน์แต่มีบอทเพื่อนร่วมทีม (Simulated Co-op Teammates) ให้วาร์ปบอทตามไปด้วย
            if (PlayerRegistry.GetTotalPlayerCount() > 1)
            {
                ExecuteTeleport(player);
                ArrangeAllPartyMembersAtArrivalPortal(LastPlayerWarpInPos, LastTransitionEntryDirection);
                return true;
            }

            return false;
        }

        /// <summary>
        /// คำนวณตำแหน่งยืนแบบเรียงแถวหน้ากระดาน (Spawn Formation) เว้นระยะห่างคนละ 1.2 เมตร
        /// รอบจุด GetInwardEntryPosition() ของประตูทางเข้า เพื่อไม่ให้ผู้เล่น 1–6 คนเกิดทับจุดเดียวกัน
        /// </summary>
        public static Vector3 GetPartySpawnFormationPosition(Vector3 baseEntryPos, TransitionDirection entryPortalDir, int slotIndex, int totalPlayers)
        {
            if (totalPlayers <= 1 || slotIndex <= 0)
            {
                return baseEntryPos;
            }

            const float spacing = 1.2f;
            // แกนเรียงแถวตั้งฉากกับทิศของประตู
            Vector3 lateralAxis = (entryPortalDir == TransitionDirection.North || entryPortalDir == TransitionDirection.South)
                ? Vector3.right
                : Vector3.forward;

            int pairIndex = (slotIndex + 1) / 2;
            float sideSign = (slotIndex % 2 == 1) ? 1f : -1f;
            Vector3 offset = lateralAxis * (pairIndex * spacing * sideSign);

            Vector3 finalPos = baseEntryPos + offset;
            finalPos.x = Mathf.Clamp(finalPos.x, -13.5f, 13.5f);
            finalPos.z = Mathf.Clamp(finalPos.z, -7.2f, 7.2f);
            finalPos.y = Mathf.Max(baseEntryPos.y, 0.5f);
            return finalPos;
        }

        public static void ArrangeAllPartyMembersAtArrivalPortal(Vector3 baseEntryPos, TransitionDirection entryPortalDir)
        {
            var all = PlayerRegistry.AllPlayers;
            int total = all.Count;
            for (int i = 0; i < total; i++)
            {
                PlayerController member = all[i];
                if (member == null) continue;
                int slot = member.PartySlotIndex > 0 ? member.PartySlotIndex : i;
                Vector3 formationPos = GetPartySpawnFormationPosition(baseEntryPos, entryPortalDir, slot, total);

                member.transform.position = formationPos;
                Rigidbody rb = member.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = formationPos;
                    rb.linearVelocity = Vector3.zero;
                }
            }
            Physics.SyncTransforms();
        }
    }
}

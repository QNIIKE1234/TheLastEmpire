using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// GameManager.NetHook — ส่วนขยาย partial class สำหรับซิงค์ Seed ประจำห้องและรายการศัตรู
    /// </summary>
    public partial class GameManager
    {
        private static int _nextEnemyNetworkId = 1;

        /// <summary>
        /// คำนวณ Seed ประจำห้อง (stageX, stageY) แบบ Deterministic 100% จาก worldSeed
        /// รับประกันว่าตึก ต้นไม้ และพื้นรอบห้องของทั้ง Host และ Client ตรงกันทุกตารางเมตรโดยไม่ต้องส่งผ่านเน็ต
        /// </summary>
        public static int GetDeterministicStageSeed(int worldSeed, int stageX, int stageY)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + worldSeed;
                hash = hash * 31 + stageX;
                hash = hash * 31 + stageY;
                return hash;
            }
        }

        public static int AllocateEnemyNetworkId()
        {
            return _nextEnemyNetworkId++;
        }

        private bool IsNetworkClientOnly()
        {
            if (!EmpireNetworkManager.IsOnlineClient) return false;

            // หาก Client อยู่ในช่องพิกัด (Stage X, Y) เดียวกับ Host ให้ใช้ศัตรูที่ซิงค์มาจาก Host
            // แต่ถ้า Client เดินแยกไปสำรวจช่องพิกัดอื่นคนเดียว ให้ช่องนั้นเสกศัตรูและไอเทมตามปกติ!
            PlayerController hostAvatar = PlayerRegistry.GetPlayerByClientId(0);
            if (hostAvatar != null && WorldMapManager.Instance != null)
            {
                return hostAvatar.NetworkStageX == WorldMapManager.Instance.CurrentPlayerX &&
                       hostAvatar.NetworkStageY == WorldMapManager.Instance.CurrentPlayerY &&
                       hostAvatar.NetworkInteriorId == BuildingInteriorManager.CurrentInteriorId;
            }

            return true;
        }
    }
}

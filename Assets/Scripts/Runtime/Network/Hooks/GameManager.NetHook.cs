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
            return EmpireNetworkManager.IsOnlineClient;
        }
    }
}

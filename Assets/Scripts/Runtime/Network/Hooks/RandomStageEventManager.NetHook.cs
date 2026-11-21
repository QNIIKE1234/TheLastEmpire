using UnityEngine;
using TheLastEmpire.Network;

namespace TheLastEmpire
{
    /// <summary>
    /// RandomStageEventManager.NetHook — ส่วนขยาย partial class สำหรับซิงค์อีเวนต์และคัตซีน (เช่น FleeingSurvivor)
    /// ให้เกิดพร้อมกันและใช้ Seed เดียวกัน 100% บนเครื่อง Host และ Client ทุกเครื่อง
    /// </summary>
    public partial class RandomStageEventManager
    {
        private bool IsClientSharingStageWithHost()
        {
            if (!EmpireNetworkManager.IsOnlineClient || WorldMapManager.Instance == null) return false;
            PlayerController hostAvatar = PlayerRegistry.GetPlayerByClientId(0);
            if (hostAvatar == null) return false;
            return hostAvatar.NetworkStageX == WorldMapManager.Instance.CurrentPlayerX &&
                   hostAvatar.NetworkStageY == WorldMapManager.Instance.CurrentPlayerY;
        }

        private bool ShouldDeferStageEventToHost()
        {
            // หาก Client อยู่ในช่องพิกัด (Stage X, Y) เดียวกับ Host ให้รอรับ StageEventBroadcast จาก Host เพื่อให้คัตซีน/อีเวนต์เริ่มพร้อมกันเป๊ะ
            // แต่ถ้า Client เดินแยกไปสำรวจช่องอื่นคนเดียว ให้รันอีเวนต์จาก Seed ประจำห้องของตัวเองได้ตามปกติ 100%!
            return IsClientSharingStageWithHost();
        }

        private void BroadcastStageEventIfHost(StageEventType eventType, StageData stage)
        {
            if (!EmpireNetworkManager.IsOnlineHost || EmpireNetworkManager.Instance == null || stage == null || eventType == StageEventType.None)
            {
                return;
            }

            int eventSeed = stage.stageSeed + 4242;
            EmpireNetworkManager.Instance.SendPacket(NetPacketType.StageEventBroadcast, w =>
            {
                w.Write(stage.x);
                w.Write(stage.y);
                w.Write((byte)eventType);
                w.Write(eventSeed);
            });
        }

        public void HandleSyncedStageEventFromHost(int stageX, int stageY, StageEventType eventType, int eventSeed)
        {
            if (WorldMapManager.Instance == null || WorldMapManager.Instance.MapGenerator == null) return;
            if (WorldMapManager.Instance.CurrentPlayerX != stageX || WorldMapManager.Instance.CurrentPlayerY != stageY) return;

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(stageX, stageY);
            if (stage == null || stage.hasEventTriggered) return;

            ExecuteEventWithSeed(eventType, stage, eventSeed);
        }

        /// <summary>
        /// สั่งรันอีเวนต์ประจำห้องด้วยชนิดอีเวนต์และ Seed ที่ Host กำหนดมาใน StageTransitionNetPayload
        /// รับประกันว่าคัตซีน Maya วิ่งหนี (FleeingSurvivor), กล่องแอร์ดรอป, หรือพ่อค้าพเนจร จะเกิดตรงกันทุกหน้าจอ
        /// </summary>
        public void ExecuteEventWithSeed(StageEventType eventType, StageData stage, int eventSeed)
        {
            if (stage == null || eventType == StageEventType.None) return;

            CleanupActiveEvent();
            stage.randomEventType = eventType;
            stage.hasEventTriggered = true;

            System.Random deterministicRng = new System.Random(eventSeed);
            Debug.Log($"<color=#38BDF8>[RandomStageEventManager.NetHook]</color> Executing synced stage event '{eventType}' with seed {eventSeed} at Stage ({stage.x}, {stage.y})");
            ExecuteEvent(eventType, stage, deterministicRng);
        }
    }
}

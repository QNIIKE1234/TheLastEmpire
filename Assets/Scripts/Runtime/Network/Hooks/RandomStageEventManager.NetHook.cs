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

        /// <summary>
        /// เรียกเมื่อ Client เดินตามเข้ามาในช่องพิกัด (X, Y) ทีหลังจากที่ Host เริ่ม Event ไปแล้ว:
        /// - คืนค่าบรรยากาศ (BloodMoon แสงแดง / Radioactive หมอกเขียว) และพ่อค้าพเนจร (NomadTrader) ให้เห็นตรงกัน
        /// - ไม่เล่นคัตซีน Maya ซ้ำและไม่เสกมอน Ambush ซ้ำ (มอนที่เหลืออยู่ซิงค์ผ่าน EnemyBatchSync โดยตรง)
        /// </summary>
        private void RestoreOngoingEventStateForLateEntryClient(StageData stage)
        {
            if (stage == null || !stage.hasEventTriggered || stage.randomEventType == StageEventType.None) return;

            CleanupActiveEvent();
            switch (stage.randomEventType)
            {
                case StageEventType.BloodMoonSurge:
                    ApplyEventAtmosphere(stage.randomEventType);
                    break;

                case StageEventType.RadioactiveFallout:
                    ApplyEventAtmosphere(stage.randomEventType);
                    break;

                case StageEventType.NomadTrader:
                    if (!stage.isCleared)
                    {
                        System.Random rng = new System.Random(stage.stageSeed + 4242);
                        ExecuteNomadTrader(stage, rng);
                    }
                    break;
            }
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

            StageData stage = WorldMapManager.Instance.MapGenerator.GetStage(stageX, stageY);
            if (stage == null) return;

            // บันทึกสถานะอีเวนต์ลง StageData เสมอแม้ตอนนั้น Client จะอยู่คนละช่องพิกัด!
            // 1) ป้องกันไม่ให้ Client เดินเข้ามาทีหลังแล้วทริกเกอร์คัตซีน/แอร์ดรอปซ้ำเป็นรอบที่ 2
            // 2) เพื่อให้ Client ที่เดินตามเข้ามาทีหลังดึงบรรยากาศ (BloodMoon/Radioactive) และ NomadTrader ขึ้นมาแสดงได้ตรงกัน
            bool alreadyTriggered = stage.hasEventTriggered;
            stage.randomEventType = eventType;
            stage.hasEventTriggered = true;

            if (WorldMapManager.Instance.CurrentPlayerX != stageX || WorldMapManager.Instance.CurrentPlayerY != stageY)
            {
                return;
            }

            if (alreadyTriggered) return;

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
